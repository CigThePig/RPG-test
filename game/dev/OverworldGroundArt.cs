using Godot;
namespace Mosslight;

/// <summary>Presentation-only dry material transitions. Coordinates are ground units;
/// positive soil distance is earth, negative is grass. Never touches simulation RNG.</summary>
public static class OverworldGroundArt
{
    public const int Revision=2;
    public static readonly Color[] Ramp={new("304d3b"),new("416347"),new("486d4c"),new("507650"),new("6a8e61"),
        new("514638"),new("735b45"),new("80634b"),new("8a6e51"),new("a08762"),new("42664a"),new("779464")};
    public static float Noise(int seed,float x,float y,int slot)
    {
        int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float fx=x-ix,fy=y-iy;
        fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        float H(int a,int b)=>(EnvironmentStyle.Hash(seed,a,b,8100+Revision*17+slot)&65535)/65535f;
        return Mathf.Lerp(Mathf.Lerp(H(ix,iy),H(ix+1,iy),fx),Mathf.Lerp(H(ix,iy+1),H(ix+1,iy+1),fx),fy);
    }
    public static bool Dry(RegionState r,int x,int y)=>x>=0&&y>=0&&x<r.Width&&y<r.Height&&r.Tiles[y*r.Width+x] is Terrain.Grass or Terrain.Earth;
    public static float Proximity(RegionState r,int tx,int ty,float px,float py,Func<Terrain,bool> matches)
    {
        float nearest=48;
        for(int y=Math.Max(0,ty-2);y<=Math.Min(r.Height-1,ty+2);y++)
            for(int x=Math.Max(0,tx-2);x<=Math.Min(r.Width-1,tx+2);x++)
            {
                if(!matches(r.Tiles[y*r.Width+x]))continue;
                float dx=Math.Max(Math.Max(x*24-px,px-(x+1)*24),0),dy=Math.Max(Math.Max(y*24-py,py-(y+1)*24),0);
                nearest=Math.Min(nearest,MathF.Sqrt(dx*dx+dy*dy));
            }
        return nearest;
    }
    static readonly Func<Terrain,bool> IsEarth=t=>t==Terrain.Earth,IsGrass=t=>t==Terrain.Grass,IsWater=t=>t==Terrain.Water;
    public static (float Soil,float Wet) Sample(RegionState r,float px,float py)
    {
        int tx=Math.Clamp((int)(px/24),0,r.Width-1),ty=Math.Clamp((int)(py/24),0,r.Height-1);
        bool earth=r.Tiles[ty*r.Width+tx]==Terrain.Earth;
        float soil=Proximity(r,tx,ty,px,py,earth?IsGrass:IsEarth)*(earth?1:-1);
        return (soil,Proximity(r,tx,ty,px,py,IsWater));
    }
}
