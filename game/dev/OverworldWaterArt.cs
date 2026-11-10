using Godot;
namespace Mosslight;

/// <summary>Living Reach presentation revision. All sampling uses absolute ground coordinates.
/// No simulation state, Random, native resources or process-dependent hashes.</summary>
public static class OverworldWaterArt
{
    public const int Revision = 2;
    public static bool Part1=>!OverworldGroundRenderer.Enabled;
    public readonly record struct Mark(Rect2 Rect, int Shade);
    public static readonly Color[] Ramp = {
        new("465e43"),new("4c5140"),new("596b50"),new("527e75"),new("487571"),
        new("3e686b"),new("325b65"),new("294f60"),new("2d5361"),new("658d80"),new("45695b"),new("244b59") };
    // Living Reach surface: 0-2 bank face, wet foot and its shadow on the water,
    // 3-7 shallow to deep, 8 spare, 9 sparkle, 10 foam, 11 ripple.
    public static readonly Color[] Living = {
        new("5a4636"),new("33343a"),new("1d4468"),new("5fb7b0"),new("439fae"),
        new("3184a2"),new("286b91"),new("21557e"),new("3184a2"),new("eefbe9"),new("c9eedd"),new("86d2c6") };
    // Bits clockwise from north, including diagonals. Outside the region is dry bank.
    public static readonly (int X,int Y)[] Neighbours = {(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)};
    public static bool Water(RegionState r,int x,int y) => x>=0 && y>=0 && x<r.Width && y<r.Height && r.Tiles[y*r.Width+x]==Terrain.Water;
    public static int Mask(RegionState r,int x,int y)
    {int mask=0;for(int i=0;i<8;i++)if(Water(r,x+Neighbours[i].X,y+Neighbours[i].Y))mask|=1<<i;return mask;}
    public static uint Hash(int seed,int x,int y,int slot=0) => EnvironmentStyle.Hash(seed,x,y,7300+(Part1?1:Revision)*31+slot);
    public static float Variation(int seed,float x,float y)
    {
        float scaleX=Part1?7:30,scaleY=Part1?10:42;
        int ix=(int)MathF.Floor(x/scaleX),iy=(int)MathF.Floor(y/scaleY);float fx=x/scaleX-ix,fy=y/scaleY-iy;
        float H(int a,int b)=>(Hash(seed,a,b)&255)/255f;
        return Mathf.Lerp(Mathf.Lerp(H(ix,iy),H(ix+1,iy),fx),Mathf.Lerp(H(ix,iy+1),H(ix+1,iy+1),fx),fy);
    }
    public static float Distance(RegionState r,int tx,int ty,float px,float py)
    {
        float distance=60;
        for(int y=ty-2;y<=ty+2;y++)for(int x=tx-2;x<=tx+2;x++)
        {
            if(Water(r,x,y))continue;
            float dx=Math.Max(Math.Max(x*24-px,px-(x+1)*24),0),dy=Math.Max(Math.Max(y*24-py,py-(y+1)*24),0);
            distance=Math.Min(distance,MathF.Sqrt(dx*dx+dy*dy));
        }
        // Round convex water corners within sixteen ground pixels. Cardinal connections stay open.
        float radius=Part1?12:16;
        for(int cy=0;cy<2;cy++)for(int cx=0;cx<2;cx++)
            if(!Water(r,tx+(cx==0?-1:1),ty) && !Water(r,tx,ty+(cy==0?-1:1)))
            {
                float dx=cx==0?px-tx*24:(tx+1)*24-px,dy=cy==0?py-ty*24:(ty+1)*24-py;
                if(dx<radius && dy<radius)distance=Math.Min(distance,radius-MathF.Sqrt((radius-dx)*(radius-dx)+(radius-dy)*(radius-dy)));
            }
        return distance;
    }
    public static float BankInset(RegionState r,int tx,int ty,float px,float py)
    {
        if(Part1)return .2f+Variation(r.Seed,px,py)*2.6f;
        float n=OverworldGroundArt.Noise(r.Seed,px/37f,py/49f,8);
        float inset=.4f+6.6f*Math.Clamp((n-.28f)*1.9f,0,1);
        bool tiny=(!Water(r,tx-1,ty)||!Water(r,tx+1,ty))&&(!Water(r,tx,ty-1)||!Water(r,tx,ty+1));
        return Math.Min(inset,tiny?2.8f:7f);
    }
    // Contract: authoritative category -> stable material variant -> neighbour transition
    // -> surface detail -> optional animation. Part 2 may extend dry-side material recipes.
    public static int Shade(RegionState r,int tx,int ty,int sx,int sy)
    {
        float px=tx*24+sx+.5f,py=ty*24+sy*2+1;
        float d=Distance(r,tx,ty,px,py),v=Variation(r.Seed,px,py);
        float inset=BankInset(r,tx,ty,px,py),edge=d-inset;
        if(edge<.1f)return v<.42f?0:1;
        if(edge<1.3f)return v>.7f?2:1;
        if(edge<2.6f)return 2;
        float depth=d+(v-.5f)*10-(Part1?0:inset);
        int shade=depth<6?3:depth<12?4:depth<21?5:depth<33?6:7;
        uint h=Hash(r.Seed,(int)px/5,(int)py/6,1);
        // Sparse sediment/stone clusters and broken horizontal reflections; no per-pixel noise.
        if(d<9 && h%17==0 && (int)px%5<3 && (int)py%6<3)return 10;
        if(d>5 && h%47==0 && (int)py%6<2 && (int)px%5<4)return shade<6?9:8;
        return shade;
    }
    public static List<Mark> Build(RegionState r)
    {
        var marks=new List<Mark>();
        var bases=new int[r.Tiles.Length];
        for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)
        {
            if(!Water(r,x,y))continue;
            int shade=Shade(r,x,y,12,6);bases[y*r.Width+x]=shade;
            marks.Add(new Mark(new Rect2(x*24,y*12,24,12),shade));
        }
        // Merge consecutive same-shade pixels horizontally, then identical runs vertically.
        // One static mesh per region; no per-water-cell nodes or textures.
        var previous=new Dictionary<(int X,int Width,int Shade),int>();
        for(int sy=0;sy<r.Height*12;sy++)
        {
            var current=new Dictionary<(int X,int Width,int Shade),int>();int ty=sy/12;
            for(int tx=0;tx<r.Width;tx++)
            {
                if(!Water(r,tx,ty))continue;
                int start=0,shade=Shade(r,tx,ty,0,sy%12);
                for(int sx=1;sx<=24;sx++)
                {
                    int next=sx==24?-1:Shade(r,tx,ty,sx,sy%12);
                    if(next==shade)continue;
                    if(shade==bases[ty*r.Width+tx]){start=sx;shade=next;continue;}
                    var key=(tx*24+start,sx-start,shade);
                    if(previous.TryGetValue(key,out int index))
                    {var old=marks[index];marks[index]=old with {Rect=new Rect2(old.Rect.Position,old.Rect.Size+new Vector2(0,1))};current[key]=index;}
                    else {current[key]=marks.Count;marks.Add(new Mark(new Rect2(key.Item1,sy,key.Item2,1),shade));}
                    start=sx;shade=next;
                }
            }
            previous=current;
        }
        return marks;
    }
}
