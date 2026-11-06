using System.Text.Json;
namespace Mosslight;
public static class OverworldGroundChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int seed in new[]{1,42,1337})
        {
            var r=Regions.Overworld(seed);string before=JsonSerializer.Serialize(r,Content.Json);
            for(int y=0;y<=r.Height;y+=3)for(int x=0;x<=r.Width;x+=3)
            {
                var a=OverworldGroundArt.Sample(r,x*24,y*24);
                check(a==OverworldGroundArt.Sample(r,x*24,y*24),"stable ground transition");
                check(a.Soil is >=-48 and <=48 && a.Wet is >=0 and <=48,"bounded ground field including map border");
                float noise=OverworldGroundArt.Noise(seed,x,y,0);
                check(noise is >=0 and <=1,"controlled ground clusters");
            }
            check(before==JsonSerializer.Serialize(r,Content.Json),"ground never mutates region "+seed);
        }
        var strip=new RegionState{Width=3,Height=1,Tiles=new[]{Terrain.Grass,Terrain.Earth,Terrain.Water}};
        check(OverworldGroundArt.Sample(strip,12,12).Soil<0 && OverworldGroundArt.Sample(strip,36,12).Soil>0,"signed grass-earth transition");
        check(OverworldGroundArt.Sample(strip,24,12).Soil==0,"shared material edge is continuous");
        check(OverworldGroundArt.Sample(strip,47,12).Wet==1,"damp soil proximity uses authoritative water");
        check(!OverworldGroundArt.Dry(strip,-1,0)&&!OverworldGroundArt.Dry(strip,2,0)&&OverworldGroundArt.Dry(strip,1,0),"dry geometry excludes water and outside");
    }
}
