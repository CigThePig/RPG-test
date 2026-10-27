using Godot;
using V = System.Numerics.Vector2;
namespace Mosslight;
public static class PlayabilityChecks
{
    public static void Run(Foundation app, Action<bool,string> check)
    {
        var w=app.World;
        string saved=Persistence.Capture(w,app.Narrative);
        try
        {
            var r=new RegionState { Id="movement-test",Width=12,Height=10,Tiles=Enumerable.Repeat(Terrain.Grass,120).ToArray() };
            w.RegionsById[r.Id]=r;
            var a=new ActorState { Id="probe",Region=r.Id,X=60,Y=60,Radius=9 };
            check(r.Index(new V(-1,30))==-1 && r.Index(new V(300,30))==-1,"out of bounds cannot alias an adjacent row");
            foreach(var terrain in new[]{Terrain.Rock,Terrain.Water,Terrain.Wall})
            {
                r.Tiles[2*12+4]=terrain; a.X=60;a.Y=60;
                w.Move(a,new V(100,0));
                check(a.X<=87.01f && r.CanStand(a.Position,a),"swept footprint blocks "+terrain);
                a.Flies=true; check(r.CanStand(new V(108,60),a)==(terrain==Terrain.Water),"flying water capability "+terrain);a.Flies=false;
                a.Swims=true;check(r.CanStand(new V(108,60),a)==(terrain==Terrain.Water),"swimming water capability "+terrain);a.Swims=false;
            }
            r.Tiles[28]=Terrain.Grass;
            for(int y=0;y<10;y++) r.Tiles[y*12+5]=Terrain.Wall;
            a.X=110;a.Y=60;w.Move(a,new V(40,60));
            check(a.X<=111.01f && a.Y>110 && r.CanStand(a.Position,a),"diagonal wall sliding retains tangent motion");
            a.X=60;a.Y=60;w.Move(a,new V(-1000,0));check(a.X>=a.Radius,"map edge and long knockback stay inside world");
            // Circle clearance at a corner; a square radius model rejected this valid point.
            r.Tiles[5]=Terrain.Grass;
            a.Radius=9;check(r.CanStand(new V(113,17),a),"rounded footprint fits beside obstacle corner");
            r.Tiles[5]=Terrain.Wall;
            a.Radius=30;check(!r.CanStand(new V(108,60),a),"large body checks all interior intersected tiles");a.Radius=9;
            // A gap is reachable, but requires following an actual cornered route.
            r.Tiles[7*12+5]=Terrain.Grass;
            a.X=60;a.Y=60;
            var target=new V(204,204);
            var path=r.Path(a.Position,target,a);
            check(path.Count>0,"route exists around wall through gap");
            for(int tick=0;tick<3600 && V.Distance(a.Position,target)>1;tick++)
            {
                if(tick%12==0) w.Follow(a,target,.2f);
                w.Move(a,new V(a.MotionX,a.MotionY)/60);
            }
            check(V.Distance(a.Position,target)<1 && r.CanStand(a.Position,a),"production steering executes route around corner without oscillation");
            foreach(float step in new[]{.5f,2f,17f,100f})
            {
                a.X=60;a.Y=60;
                for(float d=0;d<130;d+=step) w.Move(a,new V(step,0));
                check(a.X<=111.01f && r.CanStand(a.Position,a),"step-size independent wall collision "+step);
            }
            var seeds=new[]{1,42,1337}.Concat(Enumerable.Range(0,30));
            foreach(int seed in seeds)
            {
                var map=Regions.Overworld(seed);a.Region=map.Id;a.Radius=11;
                bool reachable=true;
                foreach(int at in map.Features.Keys)
                {
                    var route=map.Path(map.Center(map.Entrance),map.Center(at),a);
                    reachable &= route.Count>0;
                    w.RegionsById[map.Id]=map;a.X=map.Center(map.Entrance).X;a.Y=map.Center(map.Entrance).Y;
                    foreach(int cell in route)
                    {
                        V goal=map.Center(cell);
                        for(int t=0;t<100 && V.Distance(a.Position,goal)>.05f;t++)
                        { V d=goal-a.Position;w.Move(a,V.Normalize(d)*Math.Min(d.Length(),1.5f)); }
                        reachable &= V.Distance(a.Position,goal)<.1f;
                    }
                }
                check(reachable,"all overworld feature routes physically execute seed "+seed);
            }
            foreach(int seed in new[]{1,42,1337})
            {
                var dungeon=Regions.Dungeon(seed);a.Region=dungeon.Id;a.Radius=11;
                foreach(var door in dungeon.Features.Where(x=>x.Value=="lock")) dungeon.OpenDoors.Add(door.Key);
                w.RegionsById[dungeon.Id]=dungeon;
                var origin=dungeon.Center(dungeon.Entrance);a.X=origin.X;a.Y=origin.Y;
                var route=dungeon.Path(origin,dungeon.Center(dungeon.Objective),a);
                bool executed=route.Count>0;
                foreach(int cell in route)
                {
                    V goal=dungeon.Center(cell);
                    for(int t=0;t<100&&V.Distance(a.Position,goal)>.05f;t++)
                    { V d=goal-a.Position;w.Move(a,V.Normalize(d)*Math.Min(d.Length(),1.5f)); }
                    executed &= V.Distance(a.Position,goal)<.1f;
                }
                check(executed,"dungeon route executes with large footprint after unlocking seed "+seed);
            }
            Persistence.Restore(w,app.Narrative,saved);
            var reach=w.RegionsById["reach"];
            check(w.Actors.Values.All(b=>reach.CanStand(b.Position,b)),"all initial actors have terrain clearance");
            check(w.Actors.Values.All(b=>b.Id=="player" || reach.Features.Keys.All(at=>V.Distance(b.Position,reach.Center(at))>=40)),"NPC spawns separate from every feature");
            var actors=w.Actors.Values.ToArray();
            check(actors.All(b=>actors.All(c=>b==c || V.Distance(b.Position,c.Position)>=b.Radius+c.Radius+16)),"initial actor footprints separated");
            var p=w.Player;int chest=reach.Features.Single(f=>f.Value=="chest").Key;
            p.X=reach.Center(chest).X;p.Y=reach.Center(chest).Y;
            string owner="reach:"+chest;
            var fullActor=new ActorState { Id="full-chest-test",Region="reach",X=p.X,Y=p.Y };
            w.Items.Add(fullActor.Id,"iron_sword");
            var bag=(Node)w.Items.Service.Call("bag",fullActor.Id).AsGodotObject();
            var capacity=(Node)GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd").New().AsGodotObject();
            bag.AddChild(capacity);
            check(!w.Interact(fullActor) && !reach.Depleted.Contains(chest) && w.Items.HasLoot(owner),"full bag leaves authoritative chest contents and state intact");
            check(!w.Items.Add(fullActor.Id,"herb",3) && w.Items.Count(fullActor.Id,"herb")==0,"failed multi-item resource grant is atomic");
            capacity.Free();
            int coins=w.Items.Count(p.Id,"coin"), contents=w.Items.Count(owner,"coin");
            check(contents>0 && w.Items.HasLoot(owner),"chest rewards owned by Gloot before opening");
            check(w.Interact(p) && w.Items.Count(p.Id,"coin")==coins+contents && !w.Items.HasLoot(owner) && reach.Depleted.Contains(chest),"chest transfers actual inventory and depletes only when empty");
            int after=w.Items.Count(p.Id,"coin");
            check(!w.Interact(p) && w.InteractionFeedback=="Chest already empty","repeated empty chest interaction gives specific feedback");
            w.Enter(p,"reach");w.InitializeContainers(reach);
            check(w.Items.Count(p.Id,"coin")==after && !w.Items.HasLoot(owner),"repeated interaction and re-entry cannot refill chest");
            string emptySave=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,emptySave);
            check(w.RegionsById["reach"].Depleted.Contains(chest) && !w.Items.HasLoot(owner),"empty container survives coordinated restore");
            // Old schema-1 regions lack initialization metadata: their existing bags are authoritative.
            var old=emptySave.Replace("\"ContainersInitialized\": true,","");
            check(old != emptySave,"legacy fixture actually removes new initialization metadata");
            Persistence.Restore(w,app.Narrative,old);w.InitializeContainers(w.RegionsById["reach"]);
            check(!w.Items.HasLoot(owner),"legacy region metadata cannot regenerate spent loot");
            var legacy=System.Text.Json.Nodes.JsonNode.Parse(saved)!;
            legacy["Regions"]!["reach"]!.AsObject().Remove("PlacementRevision");
            var oldChest=w.RegionsById["reach"].Center(chest);
            legacy["Actors"]!["npc:0"]!["X"]=oldChest.X;
            legacy["Actors"]!["npc:0"]!["Y"]=oldChest.Y;
            Persistence.Restore(w,app.Narrative,legacy.ToJsonString());
            check(V.Distance(w.Actors["npc:0"].Position,oldChest)>=40 && w.RegionsById["reach"].PlacementRevision==1,"legacy chest-overlapping NPC is explicitly migrated");
            string migrated=Persistence.Capture(w,app.Narrative);
            var invalid=System.Text.Json.Nodes.JsonNode.Parse(saved)!;
            invalid["Regions"]!["reach"]!.AsObject().Remove("PlacementRevision");
            var tiles=invalid["Regions"]!["reach"]!["Tiles"]!.AsArray();
            for(int i=0;i<tiles.Count;i++)tiles[i]=(int)Terrain.Wall;
            bool rejected=false;try { Persistence.Restore(w,app.Narrative,invalid.ToJsonString()); }catch(InvalidOperationException){rejected=true;}
            check(rejected && Persistence.Capture(w,app.Narrative)==migrated,"failed legacy placement migration cannot mutate running game");
            check(TouchStick.Analog(new Vector2(3,0))==Vector2.Zero && Math.Abs(TouchStick.Analog(new Vector2(72,72)).Length()-1)<.001f,"analog deadzone and diagonal normalization");
        }
        finally { Persistence.Restore(w,app.Narrative,saved); }
    }
}
