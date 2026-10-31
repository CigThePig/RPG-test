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

    /// <summary>Operates every reachable lever and unsolved brazier ring through production
    /// interaction, walking to each controller. Returns how many mechanisms were operated.</summary>
    public static int Operate(World w, RegionState r, Action<bool,string> check, string label)
    {
        var p=w.Player;int operated=0;bool progressed=true;
        while(progressed)
        {
            progressed=false;
            foreach(var c in DungeonMechanisms.Controls(r))
            {
                if(c.Gates.Length>0 && c.Gates.All(r.OpenDoors.Contains))continue;
                if(r.Path(p.Position,r.Center(c.Controllers[0]),p).Count==0 && r.Index(p.Position)!=c.Controllers[0])continue;
                if(c.Kind=="lever")
                {
                    Walk(w,r,c.Controllers[0],check);
                    check(w.Interact(p) && r.Depleted.Contains(c.Controllers[0]) && c.Gates.All(r.OpenDoors.Contains),"production lever raises its portcullis "+label);
                }
                else
                {
                    var answer=DungeonMechanisms.Solution(r,c.Controllers);
                    check(answer.Length>=1,"unsolved brazier ring has a solution "+label);
                    for(int i=0;i<answer.Length;i++)
                    {
                        Walk(w,r,answer[i],check);
                        check(w.Interact(p),"production brazier kindles "+label);
                        check(c.Gates.All(r.OpenDoors.Contains)==(i==answer.Length-1),"seal breaks exactly when the ring burns whole "+label);
                    }
                    check(c.Controllers.All(r.Depleted.Contains) && !w.InteractionTargets(p).Any(t=>t.Value=="brazier"),"solved braziers stay lit and inert "+label);
                }
                operated++;progressed=true;
            }
        }
        return operated;
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
            // Seeded inhabitants may start beyond a locked gate; placement must not pull them to the entrance side.
            var closed=Reachable(r,p);
            if(!closed.Contains(r.Objective))
            {
                var beyond=r.FindPlacement(r.Center(r.Objective)+new V(48,48),p,Array.Empty<ActorState>(),throughGates:true);
                check(!closed.Contains(r.Index(beyond)) && r.CanStand(beyond,p),"placement beyond a locked gate stays beyond it "+seed);
            }
            check(DungeonValidator.TrySolve(r,out var plan),"progression witness "+seed);
            int count=w.Items.Count(p.Id,"dungeon_key");if(count>0)w.Items.Take(p.Id,"dungeon_key",count);
            foreach(int gate in plan)
            {
                Operate(w,r,check,seed.ToString());
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
            Operate(w,r,check,seed.ToString());
            Walk(w,r,r.Objective,check);check(w.Interact(p) && r.Depleted.Contains(r.Objective),"relic production interaction "+seed);
            Operate(w,r,check,seed.ToString()); // A lever behind the relic still opens its shortcut home.
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
        var measurements=new List<object>();
        var footprints=new HashSet<string>();int wingRooms=0,halls=0,bends=0,corridorTiles=0,largeRooms=0;
        Regions.Dungeon(42); // warm up grammar, JIT and resource caches for both comparisons
        foreach(int seed in Enumerable.Range(0,64).Append(1337))
        {
            var r=Regions.Dungeon(seed);var repeat=Regions.Dungeon(seed);
            string label=$"seed={seed} size={r.Width}x{r.Height} features={string.Join(',',r.Features.Select(x=>$"{x.Key}:{x.Value}"))} gates={string.Join(';',r.GateGroups().Select(g=>string.Join(',',g)))}";
            try
            {
                int cell=DungeonGeometry.Cell;
                check(r.Width%cell==0 && r.Height%cell==0 && r.Width<=512 && r.Height<=512,"block dimensions "+seed);
                check(r.Tiles.SequenceEqual(repeat.Tiles) && r.Features.SequenceEqual(repeat.Features) && r.GateGroups().Select(g=>string.Join(',',g)).SequenceEqual(repeat.GateGroups().Select(g=>string.Join(',',g))) && JsonSerializer.Serialize(r.Rooms,Content.Json)==JsonSerializer.Serialize(repeat.Rooms,Content.Json),"deterministic terrain/features/gates/rooms "+seed);
                check(DungeonValidator.Solvable(r),"mission solvable "+seed);
                check(new[]{"exit","key","relic"}.All(kind=>r.Features.Values.Contains(kind)),"mission features "+seed);
                Geometry(r);
                foreach(var room in r.Rooms)
                {
                    if(room.Kind=="hall"){halls++;continue;}
                    var tiles=room.Tiles().ToArray();
                    int w=tiles.Max(t=>t.X)-tiles.Min(t=>t.X)+1,h=tiles.Max(t=>t.Y)-tiles.Min(t=>t.Y)+1;
                    footprints.Add($"{w}x{h}:{tiles.Length}");if(room.Rects.Count>1)wingRooms++;if(tiles.Length>81)largeRooms++;
                }
                corridorTiles+=Enumerable.Range(0,r.Tiles.Length).Count(at=>r.Tiles[at]==Terrain.Floor && r.Rooms.All(c=>!c.Contains(at%r.Width,at/r.Width)));
                bends+=Bends(r);
                check(true,"validated room cores/wings, sealed block rings, three-wide corridor crossings and graph topology "+seed);
                foreach(var gate in new[]{"lock","portcullis","seal"}.SelectMany(kind=>r.GateGroups(kind)))
                {
                    int xs=gate.Select(i=>i%r.Width).Distinct().Count(),ys=gate.Select(i=>i/r.Width).Distinct().Count();
                    check(gate.Length==6 && ((xs==2&&ys==3)||(xs==3&&ys==2)),"isolated rectangular full-width gate "+seed);
                    check(gate.All(i=>!r.Passable(i,actors[0]) && r.BlocksSight(i)),"gate blocks every lateral lane "+seed);
                }
                var open=r.Features.Where(x=>RegionState.IsGate(x.Value)).Select(x=>x.Key).ToArray();r.OpenDoors.UnionWith(open);
                foreach(var a in (seed is 1 or 42 or 1337 ? actors : actors.Take(1)))
                {
                    var reachable=Reachable(r,a);
                    check(r.Features.Keys.All(reachable.Contains),$"footprint reaches all features and open gates {a.Id} radius={a.Radius} seed={seed}");
                    // Radius-aware short turn inside each room core; every carved floor tile is reachable.
                    foreach(var room in r.Rooms.Where(c=>c.Kind=="room"))
                    {
                        var center=r.Center(room.CenterY*r.Width+room.CenterX);
                        foreach(var direction in new[]{V.UnitX,-V.UnitX,V.UnitY,-V.UnitY})
                            if(!r.CanTravel(center,center+direction*84,a))throw new InvalidOperationException("protected combat lane clearance "+a.Id);
                    }
                    foreach(int at in Enumerable.Range(0,r.Tiles.Length).Where(at=>r.Tiles[at]==Terrain.Floor))
                        if(!reachable.Contains(at))throw new InvalidOperationException("unreachable floor "+a.Id+" at "+at%r.Width+","+at/r.Width);
                }
                r.OpenDoors.Clear();
                if(seed is 1 or 42 or 1337)
                {
                    var samples=new List<object>();
                    for(int i=0;i<5;i++)
                    {
                        long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();var d=Regions.Dungeon(seed);double gen=timer.Elapsed.TotalMilliseconds;long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                        timer.Restart();DungeonValidator.Solvable(d);double validation=timer.Elapsed.TotalMilliseconds;
                        d.OpenDoors.UnionWith(d.Features.Where(x=>RegionState.IsGate(x.Value)).Select(x=>x.Key));
                        timer.Restart();d.Path(d.Center(d.Entrance),d.Center(d.Objective),actors[0]);double nav=timer.Elapsed.TotalMilliseconds;
                        timer.Restart();d.FindPlacement(d.Center(d.Objective),actors[0],Array.Empty<ActorState>());double place=timer.Elapsed.TotalMilliseconds;
                        samples.Add(new{compositionMs=Landmarks.LastComposeMilliseconds,selectionMs=Landmarks.LastSelectionMilliseconds,structuralValidationMs=Landmarks.LastValidationMilliseconds,generationMs=gen,allocatedBytes=allocated,validationMs=validation,navigationMs=nav,placementMs=place});
                    }
                    measurements.Add(new{seed,width=r.Width,height=r.Height,tiles=r.Tiles.Length,walkable=r.Tiles.Count(t=>t==Terrain.Floor),tileArrayBytes=r.Tiles.Length*4,samples});
                    Progress(app,check,seed);
                }
            }
            catch(Exception e){throw new InvalidOperationException(label,e);}
        }
        // Variety across the sample: the minimum 9x9 room is a floor, not the norm.
        check(footprints.Count>=100,"room footprints vary across seeds: "+footprints.Count);
        check(wingRooms>=250 && largeRooms>=250,$"rooms grow past the 9x9 minimum and gain wings: wings={wingRooms} large={largeRooms}");
        check(halls>=20 && bends>=150 && corridorTiles>=8000,$"hallways connect rooms: halls={halls} bends={bends} corridorTiles={corridorTiles}");
        Directory.CreateDirectory("build/dungeon");File.WriteAllText("build/dungeon/expanded.json",JsonSerializer.Serialize(measurements,Content.Json));
        SaveChecks(app,check);
        CombatClearance(app,check);
    }

    // Structural invariants of a carved ruin. Throws with the offending location.
    public static void Geometry(RegionState r)
    {
        int cell=DungeonGeometry.Cell,core=DungeonGeometry.Core,half=DungeonGeometry.HalfOpening;
        void Fail(string why,int x,int y)=>throw new InvalidOperationException($"{why} at {x},{y}");
        var crossings=new HashSet<int>();
        foreach(var q in r.Passages)
        {
            if(q.Dx+q.Dy!=1 || (q.Dx==1 ? q.X%cell!=cell-1 : q.Y%cell!=cell-1))Fail("passage not on a block boundary",q.X,q.Y);
            int lane=q.Dx==1?q.Y%cell:q.X%cell;
            if(lane<DungeonGeometry.CrossingMargin || lane>cell-1-DungeonGeometry.CrossingMargin)Fail("passage too close to a block corner",q.X,q.Y);
            var a=r.RoomAt(q.X,q.Y)!;var b=r.RoomAt(q.X+q.Dx,q.Y+q.Dy)!;
            if(a==null || b==null || (a.Doors&(q.Dx==1?2:4))==0 || (b.Doors&(q.Dx==1?8:1))==0)Fail("passage without matching room doors",q.X,q.Y);
            for(int k=0;k<=1;k++)for(int side=-half;side<=half;side++)
            {
                int x=q.X+q.Dx*k+q.Dy*side,y=q.Y+q.Dy*k+q.Dx*side,at=y*r.Width+x;
                if(r.Tiles[at]!=Terrain.Floor)Fail("passage crossing not open",x,y);
                if(r.Features.GetValueOrDefault(at,"")!=(q.Locked?"lock":q.Mechanism switch{"lever"=>"portcullis","puzzle"=>"seal",_=>""}))Fail("gate does not match passage lock/mechanism",x,y);
                crossings.Add(at);
            }
        }
        if(r.Rooms.Sum(c=>BitCount(c.Doors))!=2*r.Passages.Count)throw new InvalidOperationException("room doors and passages disagree");
        if(r.Features.Any(f=>RegionState.IsGate(f.Value) && !crossings.Contains(f.Key)))throw new InvalidOperationException("gate outside a passage");
        // Blocks meet only at passage crossings: every other ring tile is solid.
        for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)
        {
            bool ring=x%cell is 0 || x%cell==cell-1 || y%cell is 0 || y%cell==cell-1;
            if(ring && r.Tiles[y*r.Width+x]==Terrain.Floor && !crossings.Contains(y*r.Width+x))Fail("accidental connection through block ring",x,y);
        }
        var rooms=r.Rooms.ToDictionary(c=>(c.X,c.Y));
        if(rooms.Count!=r.Rooms.Count)throw new InvalidOperationException("duplicate room cell");
        for(int cy=0;cy<r.Height/cell;cy++)for(int cx=0;cx<r.Width/cell;cx++)
        {
            var interior=new List<int>();
            for(int y=cy*cell+1;y<(cy+1)*cell-1;y++)for(int x=cx*cell+1;x<(cx+1)*cell-1;x++)if(r.Tiles[y*r.Width+x]==Terrain.Floor)interior.Add(y*r.Width+x);
            if(!rooms.TryGetValue((cx,cy),out var room)){if(interior.Count>0)Fail("unoccupied block carved",cx*cell,cy*cell);continue;}
            // Every room and corridor tile in a block joins its room; corridors leave through the ring only.
            var set=interior.ToHashSet();var seen=new HashSet<int>{room.CenterY*r.Width+room.CenterX};var queue=new Queue<int>(seen);
            while(queue.Count>0){int at=queue.Dequeue();foreach(int n in r.Neighbours(at))if(set.Contains(n)&&seen.Add(n))queue.Enqueue(n);}
            if(seen.Count!=set.Count)Fail("disconnected floor inside block",cx*cell,cy*cell);
            foreach(var q in room.Rects)
                if(q[0]<=cx*cell || q[1]<=cy*cell || q[0]+q[2]>=(cx+1)*cell-1+1 || q[1]+q[3]>=(cy+1)*cell-1+1)Fail("room rect touches block ring",q[0],q[1]);
            if(room.Kind=="hall")
            {
                if(room.Rects.Count!=1 || room.Rects[0][2]!=3 || room.Rects[0][3]!=3 || r.Features.ContainsKey(room.CenterY*r.Width+room.CenterX))Fail("hall is not a bare 3x3 junction",room.CoreX,room.CoreY);
                continue;
            }
            if(!room.Rects.Any(q=>q[0]==room.CoreX && q[1]==room.CoreY && q[2]==core && q[3]==core))Fail("room lacks its 9x9 core",room.CoreX,room.CoreY);
            foreach(var (x,y) in room.Tiles())
            {
                var tile=r.Tiles[y*r.Width+x];int lx=x-room.CoreX+1,ly=y-room.CoreY+1;
                bool authored=lx is >=2 and <=8 && ly is >=2 and <=8 && !Landmarks.Protected(lx,ly);
                if(tile!=Terrain.Floor && (tile!=Terrain.Wall || !authored))Fail("illegal room architecture",x,y);
            }
            if(!Landmarks.RoomSafe(r,room))Fail("room footprint/lane disconnected",room.CoreX,room.CoreY);
        }
    }
    static int BitCount(int n){int c=0;for(;n!=0;n&=n-1)c++;return c;}
    // Corridor turns: convex corners of corridor or hall floor, walled on two perpendicular sides.
    static int Bends(RegionState r)
    {
        bool Wall(int x,int y)=>r.Tiles[y*r.Width+x]!=Terrain.Floor;
        int bends=0;
        for(int y=1;y<r.Height-1;y++)for(int x=1;x<r.Width-1;x++)
        {
            if(Wall(x,y) || r.Rooms.Any(c=>c.Kind=="room" && c.Contains(x,y)))continue;
            bool w=Wall(x-1,y),e=Wall(x+1,y),n=Wall(x,y-1),s=Wall(x,y+1);
            if((w?1:0)+(e?1:0)+(n?1:0)+(s?1:0)==2 && w!=e && n!=s)bends++;
        }
        return bends;
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
            w.RegionsById.Remove("ruin");foreach(string id in w.Actors.Keys.Where(x=>x.StartsWith("ruin:spawn:")).ToArray())w.Actors.Remove(id);
            w.Enter(w.Player,"reach");string unvisited=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,unvisited);w.Enter(w.Player,"ruin");
            check(w.RegionsById["ruin"].Width%DungeonGeometry.Cell==0 && w.RegionsById["ruin"].Rooms.Count>0,"save without a ruin generates it on entry");
            string fresh=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,fresh);
            check(fresh==Persistence.Capture(w,app.Narrative),"dungeon exact save roundtrip");
            for(int i=0;i<60;i++)w.Step(1f/60);string next=Persistence.Capture(w,app.Narrative);
            Persistence.Restore(w,app.Narrative,fresh);for(int i=0;i<60;i++)w.Step(1f/60);
            check(next==Persistence.Capture(w,app.Narrative),"new dungeon deterministic continuation");
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);}
    }
}
