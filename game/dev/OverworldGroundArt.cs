using Godot;
namespace Mosslight;

/// <summary>Presentation-only dry material transitions. Coordinates are ground units;
/// positive soil distance is earth, negative is grass. Never touches simulation RNG.</summary>
public static class OverworldGroundArt
{
    public const int Revision=3;
    // 0-5 turf shadow to blade tip, 6-10 soil, 11 damp soil, 12-14 wildflowers,
    // 15-18 strand waterline, sand, pale sand and pebble, 19 spare.
    public static readonly Color[] Ramp={new("2a5540"),new("376b44"),new("468045"),new("5a9648"),new("78ae50"),new("a3c95f"),
        new("553f33"),new("6f533c"),new("8a6a47"),new("a48356"),new("c0a06d"),new("4a4436"),
        new("f3ecd2"),new("f0c95a"),new("d98bb6"),
        new("77705a"),new("a99f76"),new("c6bb8c"),new("ded6b4"),new("2a5540")};
    public static float Noise(int seed,float x,float y,int slot)
    {
        int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float fx=x-ix,fy=y-iy;
        fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        float H(int a,int b)=>(EnvironmentStyle.Hash(seed,a,b,8100+Revision*17+slot)&65535)/65535f;
        return Mathf.Lerp(Mathf.Lerp(H(ix,iy),H(ix+1,iy),fx),Mathf.Lerp(H(ix,iy+1),H(ix+1,iy+1),fx),fy);
    }
    /// <summary>Cells the ground material paints: dry land, plus the soil beneath outcrops.</summary>
    public static bool Covered(RegionState r,int x,int y)=>Dry(r,x,y)||(OverworldRockRenderer.Enabled&&x>=0&&y>=0&&x<r.Width&&y<r.Height&&r.Tiles[y*r.Width+x]==Terrain.Rock);
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
