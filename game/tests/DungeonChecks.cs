using Godot;
using System.Diagnostics;
using System.Text.Json;
using V = System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Production geometry, collision, interaction and persistence. No slot writes.</summary>
public static class DungeonChecks
{
    public static HashSet<int> Reachable(RegionState r, ActorState a)
    {
        var seen=new HashSet<int>();var queue=new Queue<int>();
        if(r.CanStand(r.Center(r.Entrance),a)){seen.Add(r.Entrance);queue.Enqueue(r.Entrance);}
        while(queue.Count>0)
        {
            int at=queue.Dequeue();
            foreach(int n in r.Neighbours(at))
                if(!seen.Contains(n) && r.CanTravel(r.Center(at),r.Center(n),a)){seen.Add(n);queue.Enqueue(n);}
        }
        return seen;
    }

    static void Walk(World w, RegionState r, int target, Action<bool,string> check)
    {
        var p=w.Player;var path=r.Path(p.Position,r.Center(target),p);
        check(r.Index(p.Position)==target || path.Count>0,"runtime route exists to "+target);
        foreach(int at in path)
        {
            var end=r.Center(at);
            while(V.Distance(p.Position,end)>.01f)
            {
                var delta=end-p.Position;if(delta.Length()>4)delta=V.Normalize(delta)*4;
                var before=p.Position;w.Move(p,delta);
                if(V.Distance(before,p.Position)<.001f)throw new InvalidOperationException("Dungeon movement stuck at "+at);
            }
        }
        check(r.CanStand(p.Position,p),"runtime walk retains footprint at "+target);
    }

    public static void Progress(Foundation app, Action<bool,string> check, int seed)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);
        try
        {
            w.RegionsById.Remove("ruin");w.RegionsById["ruin"]=Regions.Dungeon(seed);w.Enter(w.Player,"ruin");
            var r=w.RegionsById["ruin"];var p=w.Player;
            var inhabitants=w.Actors.Values.Where(a=>a.Id.StartsWith("ruin:spawn:")&&a.Region=="ruin").ToArray();
            check(r.CanStand(p.Position,p) && r.Features.Keys.All(at=>V.Distance(p.Position,r.Center(at))>=40),"arrival clears walls and return marker "+seed);
            check(inhabitants.Length==8 && inhabitants.All(a=>r.CanStand(a.Position,a) && V.Distance(a.Position,p.Position)>a.Radius+p.Radius+16),"eight production inhabitants and safe entrance "+seed);
            check(inhabitants.All(a=>r.Features.Keys.All(at=>V.Distance(a.Position,r.Center(at))>=40) && inhabitants.Where(b=>b.Id!=a.Id).All(b=>V.Distance(a.Position,b.Position)>=a.Radius+b.Radius+16)),"spawn separation from features/actors "+seed);
            check(DungeonValidator.TrySolve(r,out var plan),"progression witness "+seed);
            int count=w.Items.Count(p.Id,"dungeon_key");if(count>0)w.Items.Take(p.Id,"dungeon_key",count);
            foreach(int gate in plan)
            {
                foreach(int key in r.Features.Where(x=>x.Value=="key").Select(x=>x.Key).Order())
                {
                    if(r.Depleted.Contains(key) || r.Path(p.Position,r.Center(key),p).Count==0)continue;
                    Walk(w,r,key,check);check(w.Interact(p) && r.Depleted.Contains(key),"collect production key "+seed);
                }
                var group=r.GateAt(gate);
                int stand=group.SelectMany(r.Neighbours).Distinct().Order().First(at=>r.CanStand(r.Center(at),p) && r.Path(p.Position,r.Center(at),p).Count>0 && group.Any(g=>V.Distance(r.Center(at),r.Center(g))<48));
                Walk(w,r,stand,check);count=w.Items.Count(p.Id,"dungeon_key");
                check(count>0 && w.Interact(p),"production unlock "+seed);
                check(group.All(r.OpenDoors.Contains) && w.Items.Count(p.Id,"dungeon_key")==count-1,"one key opens complete gate "+seed);
                count=w.Items.Count(p.Id,"dungeon_key");w.Interact(p);
                check(w.Items.Count(p.Id,"dungeon_key")==count,"duplicate cannot charge key "+seed);
                // The next key/objective route must physically walk through opened gates.
            }
            Walk(w,r,r.Objective,check);check(w.Interact(p) && r.Depleted.Contains(r.Objective),"relic production interaction "+seed);
            Walk(w,r,r.Entrance,check);check(w.Interact(p) && p.Region=="reach","return exit production interaction "+seed);
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);}
    }

    public static void Run(Foundation app, Action<bool,string> check)
    {
        var actors=new List<ActorState>{app.World.Player,app.World.Actors.Values.First(a=>a.Appearance.Family=="biped" && a.Id!="player")};
        foreach(var (id,family) in new[]{("prowler","quadruped"),("brute","reptile"),("spitter","amorphous")})
        {
            var a=new ActorState{Id=id,CombatProfile=id,Appearance=new(){Family=family,Seed=42}};AnatomyAdapter.Apply(a);actors.Add(a);
        }
        var topology=JsonSerializer.Deserialize<Dictionary<int,string>>(Godot.FileAccess.GetFileAsString("res://game/tests/fixtures/pr4-topology.json"),Content.Json)!;
        var measurements=new List<object>();
        Regions.Dungeon(42); // warm up grammar, JIT and resource caches for both comparisons
        foreach(int seed in Enumerable.Range(0,64).Append(1337))
        {
            var r=Regions.Dungeon(seed);var repeat=Regions.Dungeon(seed);
            string label=$"seed={seed} size={r.Width}x{r.Height} features={string.Join(',',r.Features.Select(x=>$"{x.Key}:{x.Value}"))} gates={string.Join(';',r.GateGroups().Select(g=>string.Join(',',g)))}";
            try
            {
                var signature=new List<string>{$"{r.Width/11}x{r.Height/11}"};
                for(int y=0;y<r.Height;y+=11)for(int x=0;x<r.Width;x+=11)
                {
                    int mid=(y+5)*r.Width+x+5;
                    signature.Add($"{r.Tiles[mid]}:{r.Features.GetValueOrDefault(mid, "")}:{r.Tiles[mid-5]}:{r.Tiles[mid+5]}:{r.Tiles[mid-5*r.Width]}:{r.Tiles[mid+5*r.Width]}");
                }
                check(topology[seed]==string.Join("|",signature),"PR4 graph topology/mission roles unchanged "+seed);
                check(r.Width%11==0 && r.Height%11==0 && r.Width<=512 && r.Height<=512,"expanded dimensions "+seed);
                check(r.Tiles.SequenceEqual(repeat.Tiles) && r.Features.SequenceEqual(repeat.Features) && r.GateGroups().Select(g=>string.Join(',',g)).SequenceEqual(repeat.GateGroups().Select(g=>string.Join(',',g))),"deterministic terrain/features/gates "+seed);
                check(DungeonValidator.Solvable(r),"mission solvable "+seed);
                check(new[]{"exit","key","relic"}.All(kind=>r.Features.Values.Contains(kind)),"mission features "+seed);
                // Every cell corner stays solid, all occupied interiors are exactly 9x9;
                // each boundary cross-section is either solid or centered width three.
                for(int cy=0;cy<r.Height;cy+=11)for(int cx=0;cx<r.Width;cx+=11)
                {
                    bool occupied=r.Tiles[(cy+5)*r.Width+cx+5]==Terrain.Floor;
                    for(int y=1;y<=9;y++)for(int x=1;x<=9;x++)
                        if((r.Tiles[(cy+y)*r.Width+cx+x]==Terrain.Floor)!=occupied)throw new InvalidOperationException("incomplete interior");
                    foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1)})
                    {
                        int mid=(cy+5+dy*5)*r.Width+cx+5+dx*5;
                        bool opening=r.Tiles[mid]==Terrain.Floor;
                        for(int side=-5;side<=5;side++)
                        {
                            int at=mid+side*(-dy+dx*r.Width);
                            bool floor=r.Tiles[at]==Terrain.Floor;
                            if(floor!=(opening && Math.Abs(side)<=1))throw new InvalidOperationException("boundary width/corner");
                            if(opening && Math.Abs(side)<=1)
                            {
                                int next=at+dx+dy*r.Width;
                                if(next<0||next>=r.Tiles.Length||r.Tiles[next]!=Terrain.Floor)throw new InvalidOperationException("asymmetric opening");
                            }
                        }
                    }
                }
                check(true,"9x9 interiors/four-direction symmetric three-tile boundaries/solid corners "+seed);
                foreach(var gate in r.GateGroups())
                {
                    int xs=gate.Select(i=>i%r.Width).Distinct().Count(),ys=gate.Select(i=>i/r.Width).Distinct().Count();
                    check(gate.Length==6 && ((xs==2&&ys==3)||(xs==3&&ys==2)),"isolated rectangular full-width gate "+seed);
                    check(gate.All(i=>!r.Passable(i,actors[0]) && r.BlocksSight(i)),"gate blocks every lateral lane "+seed);
                }
                var open=r.Features.Where(x=>x.Value=="lock").Select(x=>x.Key).ToArray();r.OpenDoors.UnionWith(open);
                foreach(var a in (seed is 1 or 42 or 1337 ? actors : actors.Take(1)))
                {
                    var reachable=Reachable(r,a);
                    check(r.Features.Keys.All(reachable.Contains),$"footprint reaches all features and open gates {a.Id} radius={a.Radius} seed={seed}");
                    // Radius-aware short turn inside each occupied chamber.
                    for(int cy=0;cy<r.Height;cy+=11)for(int cx=0;cx<r.Width;cx+=11)
                    {
                        int mid=(cy+5)*r.Width+cx+5;if(r.Tiles[mid]!=Terrain.Floor)continue;
                        var center=r.Center(mid);var corner=center+new V(72,72);
                        if(!r.CanTravel(center,corner,a)||!r.CanTravel(corner,corner+new V(-48,0),a))throw new InvalidOperationException("corner clearance "+a.Id);
                    }
                }
                r.OpenDoors.Clear();
                if(seed is 1 or 42 or 1337)
                {
                    var samples=new List<object>();
                    for(int i=0;i<5;i++)
                    {
                        long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();var d=Regions.Dungeon(seed);double gen=timer.Elapsed.TotalMilliseconds;long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                        timer.Restart();DungeonValidator.Solvable(d);double validation=timer.Elapsed.TotalMilliseconds;
                        d.OpenDoors.UnionWith(d.Features.Where(x=>x.Value=="lock").Select(x=>x.Key));
                        timer.Restart();d.Path(d.Center(d.Entrance),d.Center(d.Objective),actors[0]);double nav=timer.Elapsed.TotalMilliseconds;
                        timer.Restart();d.FindPlacement(d.Center(d.Objective),actors[0],Array.Empty<ActorState>());double place=timer.Elapsed.TotalMilliseconds;
                        samples.Add(new{generationMs=gen,allocatedBytes=allocated,validationMs=validation,navigationMs=nav,placementMs=place});
                    }
                    measurements.Add(new{seed,width=r.Width,height=r.Height,tiles=r.Tiles.Length,walkable=r.Tiles.Count(t=>t==Terrain.Floor),tileArrayBytes=r.Tiles.Length*4,samples});
                    Progress(app,check,seed);
                }
            }
            catch(Exception e){throw new InvalidOperationException(label,e);}
        }
        Directory.CreateDirectory("build/dungeon");File.WriteAllText("build/dungeon/expanded.json",JsonSerializer.Serialize(measurements,Content.Json));
        SaveChecks(app,check);
        CombatClearance(app,check);
    }

    static void CombatClearance(Foundation app, Action<bool,string> check)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);
        try
        {
            var r=Regions.Dungeon(42);w.RegionsById["ruin"]=r;var p=w.Player;p.Region="ruin";
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            var center=r.Center(r.Entrance);
            void Reset(V point){w.Cancel(p);p.X=point.X;p.Y=point.Y;p.Cooldowns.Clear();p.Statuses.Clear();p.Health=p.MaxHealth;}
            void Advance(){for(int i=0;i<30;i++)w.Step(1f/60);}
            Reset(center-new V(48,0));var start=p.Position;
            check(w.TryStartPlayerDodge(V.UnitX),"expanded room starts production dodge");Advance();
            check(Math.Abs(V.Distance(start,p.Position)-90)<.1f && r.CanStand(p.Position,p),"90-unit room dodge has clearance");
            // Pick an unconnected wall edge so this checks real enclosing terrain.
            var direction=new[]{V.UnitX,-V.UnitX,V.UnitY,-V.UnitY}.First(d=>r.Tiles[r.Index(center+d*120)]==Terrain.Wall);
            Reset(center+direction*85);start=p.Position;
            check(w.TryStartPlayerDodge(direction),"wallward production dodge starts");Advance();
            check(V.Distance(start,p.Position)<30 && r.CanStand(p.Position,p),"roll stops at generated enclosing wall");
            Reset(center+direction*(108-p.Radius-2));start=p.Position;
            check(w.TryStartPlayerSlash(direction),"wallward production sword lunge starts");Advance();
            check(V.Distance(start,p.Position)<3 && r.CanStand(p.Position,p),"approved lunge cannot cross generated wall");
            var gate=r.GateGroups().First();
            // For a 2x3 gate, approach along its two-cell (travel) axis.
            bool horizontal=gate.Select(at=>at%r.Width).Distinct().Count()==2;
            direction=horizontal?V.UnitX:V.UnitY;
            int first=gate.OrderBy(at=>horizontal?at%r.Width:at/r.Width).ThenBy(at=>at).First();
            var middle=gate.Select(r.Center).Aggregate(V.Zero,(sum,v)=>sum+v)/gate.Length;
            var approach=middle-direction*48;
            Reset(approach);start=p.Position;
            check(r.CanStand(p.Position,p) && w.TryStartPlayerDodge(direction),"closed gate production dodge starts");Advance();
            check(V.Distance(start,p.Position)<30 && gate.All(at=>!r.OpenDoors.Contains(at)) && r.CanStand(p.Position,p),"roll cannot bypass full-width closed gate");
            var source=w.Actors.Values.First(a=>a.Id!="player");source.Region="ruin";source.X=approach.X;source.Y=approach.Y;
            p.X=middle.X+direction.X*72;p.Y=middle.Y+direction.Y*72;
            var spit=w.FindAbility("spit")!;
            var shot=ProjectileFlight.Release(source,p,spit);
            check(ProjectileFlight.Advance(w,shot,spit,r,1).Outcome=="terrain","production projectile collides with generated gate");
            r.OpenDoors.UnionWith(gate);
            var crossing=middle+direction*48;
            check(r.CanTravel(approach,crossing,p),"opened full gate admits swept player footprint");
            Reset(approach);w.Move(p,crossing-approach);
            check(V.Distance(p.Position,crossing)<.1f,"production movement crosses opened gate");
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);}
    }

    static void SaveChecks(Foundation app, Action<bool,string> check)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);
        try
        {
            string legacy=Godot.FileAccess.GetFileAsString("res://game/tests/fixtures/pr4-dungeon-save.json");
            var old=Persistence.Parse(legacy,w.Content);Persistence.Restore(w,app.Narrative,legacy);
            var loaded=Persistence.Parse(Persistence.Capture(w,app.Narrative),w.Content);
            File.WriteAllText("build/dungeon/legacy-before.json",JsonSerializer.Serialize(old,Content.Json));
            File.WriteAllText("build/dungeon/legacy-after.json",JsonSerializer.Serialize(loaded,Content.Json));
            check(JsonSerializer.Serialize(old,Content.Json)==JsonSerializer.Serialize(loaded,Content.Json),"PR4 fixture roundtrip preserves regions/actors/inventory/equipment/quest/events");
            var r=loaded.Regions["ruin"];
            check(r.Width%7==0 && r.Height%7==0 && r.OpenDoors.Count>0 && r.Depleted.Any(i=>r.Features[i]=="key") && r.Depleted.Any(i=>r.Features[i]=="chest"),"legacy layout and world mutations preserved");
            check(loaded.Actors.Values.Any(a=>a.Region=="ruin"&&!a.Alive)&&loaded.Actors.Values.Any(a=>a.Region=="ruin"&&a.Alive),"legacy living/dead identities preserved");
            w.RegionsById.Remove("ruin");foreach(string id in w.Actors.Keys.Where(x=>x.StartsWith("ruin:spawn:")).ToArray())w.Actors.Remove(id);
            w.Enter(w.Player,"reach");string unvisited=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,unvisited);w.Enter(w.Player,"ruin");
            check(w.RegionsById["ruin"].Width%11==0,"old save without ruin generates new geometry on entry");
            string fresh=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,fresh);
            check(fresh==Persistence.Capture(w,app.Narrative),"new expanded dungeon exact save roundtrip");
            for(int i=0;i<60;i++)w.Step(1f/60);string next=Persistence.Capture(w,app.Narrative);
            Persistence.Restore(w,app.Narrative,fresh);for(int i=0;i<60;i++)w.Step(1f/60);
            check(next==Persistence.Capture(w,app.Narrative),"new dungeon deterministic continuation");
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);}
    }
}
