using Godot;
using System.Text.Json;
namespace Mosslight;
public static class OverworldWaterChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        // Exhaustive eight-neighbour truth table, including both diagonal saddle cases.
        for(int mask=0;mask<256;mask++)
        {
            var r=new RegionState {Width=3,Height=3,Tiles=new Terrain[9]};r.Tiles[4]=Terrain.Water;
            for(int bit=0;bit<8;bit++){var d=OverworldWaterArt.Neighbours[bit];if((mask&(1<<bit))!=0)r.Tiles[(1+d.Y)*3+1+d.X]=Terrain.Water;}
            check(OverworldWaterArt.Mask(r,1,1)==mask,"water eight-neighbour topology "+mask);
            var marks=OverworldWaterArt.Build(r);
            check(marks.All(m=>m.Rect.Position.X>=0 && m.Rect.Position.Y>=0 && m.Rect.End.X<=72 && m.Rect.End.Y<=36 && m.Rect.Size.X>0 && m.Rect.Size.Y>0),"bounded topology geometry "+mask);
            // The opaque base covers every water pixel; transition overlays stay within it.
            var coverage=new int[72*36];foreach(var m in marks)for(int y=(int)m.Rect.Position.Y;y<m.Rect.End.Y;y++)for(int x=(int)m.Rect.Position.X;x<m.Rect.End.X;x++)coverage[y*72+x]++;
            check(coverage.Select((n,i)=>(r.Tiles[(i/72/12)*3+i%72/24]==Terrain.Water?n is >=1 and <=2:n==0)).All(b=>b),"seam-free bounded base/transition coverage "+mask);
        }
        foreach(int seed in new[]{1,42,1337})
        {
            var r=Regions.Overworld(seed);string state=JsonSerializer.Serialize(r,Content.Json);
            var a=OverworldWaterArt.Build(r);var b=OverworldWaterArt.Build(r);
            check(a.SequenceEqual(b),"water deterministic recipes "+seed);
            check(a.Count<=r.Tiles.Count(t=>t==Terrain.Water)*289,"bounded water geometry "+seed);
            check(state==JsonSerializer.Serialize(r,Content.Json),"water preserves serialized region "+seed);
            check(a.All(m=>OverworldWaterArt.Ramp[m.Shade].A==1),"complete static reduced-motion material "+seed);
        }
        var pool=new RegionState {Width=1,Height=1,Tiles=new[]{Terrain.Water}};
        check(OverworldWaterArt.Mask(pool,0,0)==0 && OverworldWaterArt.Build(pool).Count>0,"isolated boundary pool");
        check(OverworldWaterArt.Shade(pool,0,0,12,6)<6,"tiny pool has no forced deep centre");
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);
        try
        {
            string Simulate(bool art)
            {
                Persistence.Restore(w,app.Narrative,snapshot);
                if(art){var region=w.RegionsById[w.Player.Region];_=OverworldWaterArt.Build(region);for(int y=0;y<region.Height;y++)for(int x=0;x<region.Width;x++)_=OverworldGroundArt.Sample(region,x*24+12,y*24+12);}
                for(int tick=0;tick<180;tick++){w.Move(w.Player,new System.Numerics.Vector2(.3f,.1f));w.Step(1f/60);}
                return Persistence.Capture(w,app.Narrative);
            }
            check(Simulate(false)==Simulate(true),"water leaves gameplay RNG movement AI combat and saves identical");
            Persistence.Restore(w,app.Narrative,snapshot);
            check(Persistence.Capture(w,app.Narrative)==snapshot,"water full save roundtrip without migration");
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();}
    }
}
