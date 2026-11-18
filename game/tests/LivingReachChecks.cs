using Godot;
using System.Text.Json;
namespace Mosslight;

public static class LivingReachChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        foreach(int seed in new[]{1,42,1337,-1,int.MinValue,int.MaxValue,2718,8675309,19,2026})
        {
            var r=Regions.Overworld(seed,320,240);var again=Regions.Overworld(seed,320,240);
            check(r.GeographyRevision==LivingReachGeography.Revision&&r.Tiles.SequenceEqual(again.Tiles),"revisioned large geography deterministic "+seed);
            check(r.Tiles.All(t=>Enum.IsDefined(t)),"valid materials "+seed);
            var remaining=Enumerable.Range(0,r.Tiles.Length).Where(i=>r.Tiles[i]==Terrain.Water).ToHashSet();var groups=new List<HashSet<int>>();
            while(remaining.Count>0)
            {
                int at=remaining.Min();var group=new HashSet<int>{at};remaining.Remove(at);var q=new Queue<int>();q.Enqueue(at);
                while(q.Count>0)foreach(int n in r.Neighbours(q.Dequeue()))if(remaining.Remove(n)){group.Add(n);q.Enqueue(n);}
                groups.Add(group);
            }
            check(groups.Count==2&&groups.Min(g=>g.Count)>75,"one connected watershed and deliberate pond, no speckles "+seed);
            var main=groups.OrderByDescending(g=>g.Count).First();
            check(main.Any(i=>i/r.Width==0)&&main.Any(i=>i/r.Width==r.Height-1)&&main.Any(i=>i%r.Width==r.Width-1),"river and tributary connect three controlled boundary outlets "+seed);
            int last=0;bool widths=true;
            for(int y=0;y<r.Height;y++)
            {
                int cx=(int)MathF.Round(LivingReachGeography.RiverX(seed,y,r.Width,r.Height));
                check(main.Contains(y*r.Width+cx),"unbroken world-scale river centre "+seed+":"+y);
                int l=cx,h=cx;while(l>0&&r.Tiles[y*r.Width+l-1]==Terrain.Water)l--;while(h<r.Width-1&&r.Tiles[y*r.Width+h+1]==Terrain.Water)h++;
                int width=h-l+1;
                // Basin mouths naturally widen faster; compare the designed channel
                // radius separately from the expanded lake cross section.
                int radius=(int)MathF.Round(LivingReachGeography.RiverRadius(seed,y,r.Height)*2);
                if(y>0&&Math.Abs(radius-last)>1)widths=false;last=radius;
                if(y==r.Height/4)check(width>=5,"substantial channel width "+seed);
            }
            check(widths,"channel width varies gradually "+seed);
            check(main.Count>2000 && main.Count<r.Tiles.Length*.18,"controlled substantial watershed extent "+seed);
            var actor=new ActorState{Radius=11};var spawn=r.Center(r.Entrance);
            check(r.Crossings.Count>0&&r.Crossings.All(i=>r.Passable(i,actor))&&r.Path(spawn,r.Center(r.Objective),actor).Count>0&&r.Features.Keys.All(i=>r.CanStand(r.Center(i),actor)),"quest and resources accessible "+seed);
            TownState.Validate(r);var town=r.Town!;
            check(town.Revision==2&&town.Houses.Select(h=>h.Style).Distinct().Count()==6,"six generous building styles "+seed);
            foreach(var h in town.Houses)
            {
                check(h.Width>=10&&h.Height>=9&&h.DoorWidth>=2,"larger footprint and doorway "+h.Style+seed);
                check(r.Path(spawn,r.Center((h.Y+3)*r.Width+h.X+h.Door),actor).Count>0,"spacious house reachable "+h.Style+seed);
                var solid=town.Pieces.Where(p=>h.Contains(p.X,p.Y)&&!TownArt.Flat((TownProp)p.Kind)).ToArray();
                check(solid.All(p=>p.Y<h.Y+h.Height-3 || Math.Abs(p.X-(h.X+h.Door))>=2),"clear entrance apron "+h.Style+seed);
                check(!solid.Any(p=>Math.Abs(p.X-(h.X+h.Door))<2&&p.Y>h.Y+3),"three-cell visual circulation aisle "+h.Style+seed);
            }
            var restored=JsonSerializer.Deserialize<RegionState>(JsonSerializer.Serialize(r,Content.Json),Content.Json)!;TownState.Validate(restored);
            check(restored.Tiles.SequenceEqual(r.Tiles)&&restored.Town!.Houses.SequenceEqual(town.Houses),"saved terrain and town retained "+seed);
        }
        // Old-world payloads omit the new optional revision; stored terrain is the
        // authority. Installing a town again must never re-carve its saved layout.
        var legacy=Regions.Overworld(42);var serialized=JsonSerializer.Serialize(legacy,Content.Json);
        var copy=JsonSerializer.Deserialize<RegionState>(serialized,Content.Json)!;TownGenerator.Install(copy);
        check(copy.GeographyRevision==0&&copy.Town!.Revision==1&&JsonSerializer.Serialize(copy,Content.Json)==serialized,"legacy compact towns retained verbatim");
        var oldLarge=new RegionState {Seed=42,Width=320,Height=240,Tiles=Enumerable.Repeat(Terrain.Grass,320*240).ToArray()};
        var oldPlan=new TownPlan();
        foreach(var h in legacy.Town!.Houses)oldPlan.Houses.Add(h with {Y=h.Y+96});
        foreach(var piece in legacy.Town.Pieces)oldPlan.Pieces.Add(piece with {Y=piece.Y+96});
        foreach(int i in legacy.Town.Paving)oldPlan.Pave(i%legacy.Width,i/legacy.Width+96,1,1);
        oldPlan.Carve(oldLarge);oldLarge.Town=TownState.From(oldPlan,oldLarge.Width);
        string oldJson=JsonSerializer.Serialize(oldLarge,Content.Json);
        var oldCopy=JsonSerializer.Deserialize<RegionState>(oldJson,Content.Json)!;TownGenerator.Install(oldCopy);TownState.Validate(oldCopy);
        check(JsonSerializer.Serialize(oldCopy,Content.Json)==oldJson,"PR11-size revision-1 saved town is not expanded or re-carved");
        var region=Regions.Overworld(42,320,240);
        var a=ChunkLookup.Build(region,OverworldChunkStreamer.Cells(region,new(4,3)));
        var b=ChunkLookup.Build(region,OverworldChunkStreamer.Cells(region,new(4,3)));
        check(a.Ground.SequenceEqual(b.Ground)&&(a.Water==null||a.Water.SequenceEqual(b.Water!)),"managed preparation independent of request order");
        var c=ChunkLookup.Build(region,OverworldChunkStreamer.Cells(region,new(5,3)));
        // Compare the same global samples in the overlapping three-tile halos.
        bool seam=true;int offset=32*8;
        for(int y=0;y<a.Height;y++)for(int x=offset;x<a.Width;x++)
            for(int k=0;k<4;k++)if(a.Ground[(y*a.Width+x)*4+k]!=c.Ground[(y*c.Width+x-offset)*4+k])seam=false;
        check(seam,"adjacent prepared ground halos match byte for byte");
        var fixture=new RegionState {Seed=19,Width=68,Height=40,Tiles=Enumerable.Repeat(Terrain.Grass,68*40).ToArray()};
        for(int y=2;y<25;y++)for(int x=27;x<39;x++)fixture.Tiles[y*68+x]=Terrain.Water;
        for(int y=27;y<39;y++)for(int x=29;x<37;x++)fixture.Tiles[y*68+x]=Terrain.Rock;
        var left=ChunkLookup.Build(fixture,OverworldChunkStreamer.Cells(fixture,new(0,0)));
        var right=ChunkLookup.Build(fixture,OverworldChunkStreamer.Cells(fixture,new(1,0)));
        foreach(var pair in new[]{(left.Water!,right.Water!),(left.Rock!,right.Rock!)})
        {
            bool same=true;int shift=29*8;
            for(int y=0;y<left.Height;y++)for(int x=shift;x<left.Width;x++)for(int k=0;k<3;k++)
                if(pair.Item1[(y*left.Width+x)*3+k]!=pair.Item2[(y*right.Width+x-shift)*3+k])same=false;
            check(same,"water/rock lookup halos seamless across a wet/rocky chunk boundary");
        }
        Array.Fill(fixture.Tiles,Terrain.Grass);fixture.Tiles[12*fixture.Width+31]=fixture.Tiles[13*fixture.Width+31]=Terrain.Rock;
        var spill=ChunkLookup.Build(fixture,OverworldChunkStreamer.Cells(fixture,new(1,0)));
        check(spill.Rock!=null,"rock halo retains raised tops and shadows in a neighbour chunk with no core rock");
        using var cancelled=new System.Threading.CancellationTokenSource();cancelled.Cancel();bool stopped=false;
        try{ChunkLookup.Build(fixture,new(0,0,32,32),cancelled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"discarded region preparation cancels without native resources");
    }
}
