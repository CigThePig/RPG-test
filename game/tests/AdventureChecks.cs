using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;
using V = System.Numerics.Vector2;
namespace Mosslight;

public static class AdventureChecks
{
    public static void Run(Foundation app, Action<bool,string> check)
    {
        var w=app.World;var n=app.Narrative;string original=Persistence.Capture(w,n);
        var rows=new List<object>();
        try
        {
            foreach(int seed in Enumerable.Range(0,64).Append(1337))
            {
                Persistence.Restore(w,n,original);
                foreach(string id in w.Actors.Keys.Where(id=>id.StartsWith("ruin:spawn:")).ToArray())w.Actors.Remove(id);
                var watch=System.Diagnostics.Stopwatch.StartNew();var r=Regions.Dungeon(seed);double generation=watch.Elapsed.TotalMilliseconds;
                w.RegionsById["ruin"]=r;watch.Restart();w.Enter(w.Player,"ruin");double population=watch.Elapsed.TotalMilliseconds;
                var enemies=w.Actors.Values.Where(a=>a.Region=="ruin"&&a.Id!="player").OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray();
                check(enemies.Length==8,"adventure bounded eight-actor population "+seed);
                var entrance=r.RoomAt(r.Entrance%r.Width,r.Entrance/r.Width);
                check(enemies.All(a=>r.CanStand(a.Position,a)&&r.RoomAt((int)(a.X/24),(int)(a.Y/24)) is { Kind:"room" } room&&room.Contains((int)(a.X/24),(int)(a.Y/24))&&room!=entrance),"adventure actual-room spawns and safe entrance "+seed);
                check(enemies.All(a=>r.Features.Keys.All(at=>V.Distance(a.Position,r.Center(at))>=48)),"adventure features clear "+seed);
                foreach(var room in r.Rooms)
                    check(enemies.Count(a=>room.Contains((int)(a.X/24),(int)(a.Y/24)))<=(room.Tiles().Count()>=100?2:1),"adventure room density "+seed+"/"+room.X+","+room.Y);
                string positions=JsonSerializer.Serialize(enemies.Select(a=>new{a.Id,a.CombatProfile,a.X,a.Y}));
                var planned=new List<ActorState>();
                foreach(var actor in enemies)
                {
                    var copy=new ActorState{Id=actor.Id,Region=actor.Region,Radius=actor.Radius,CombatProfile=actor.CombatProfile};
                    check(DungeonEncounters.Place(r,copy,planned,int.Parse(actor.Id.Split(':').Last()),out var point)&&point==actor.Position,"dedicated encounter stream repeats "+seed+"/"+actor.Id);
                    copy.X=point.X;copy.Y=point.Y;planned.Add(copy);
                }
                string save=Persistence.Capture(w,n);Persistence.Restore(w,n,save);
                w.Enter(w.Player,"reach");w.Enter(w.Player,"ruin");
                check(positions==JsonSerializer.Serialize(w.Actors.Values.Where(a=>a.Id.StartsWith("ruin:spawn:")).OrderBy(a=>a.Id,StringComparer.Ordinal).Select(a=>new{a.Id,a.CombatProfile,a.X,a.Y})),"adventure revisit preserves actors "+seed);
                var killed=w.Actors[enemies[0].Id];w.Damage(w.Player,killed,10000,"magic"); // Explicit persistence fixture, not combat proof.
                string corpse=Persistence.Capture(w,n);Persistence.Restore(w,n,corpse);w.Enter(w.Player,"reach");w.Enter(w.Player,"ruin");
                check(!w.Actors[killed.Id].Alive&&w.Actors.Count(a=>a.Key.StartsWith("ruin:spawn:"))==8,"defeated actor survives save and revisit without duplication "+seed);
                foreach(var a in enemies)w.Actors.Remove(a.Id); // Explicit no-actors persistence fixture, not combat proof.
                w.Enter(w.Player,"reach");w.Enter(w.Player,"ruin");
                check(!w.Actors.Keys.Any(id=>id.StartsWith("ruin:spawn:")),"adventure empty population never respawns "+seed);
                rows.Add(new{seed,generationMs=generation,populationMs=population,rooms=r.Rooms.Count,actors=enemies.Length});
            }
            Persistence.Restore(w,n,original);
            var region=Regions.Dungeon(1);w.RegionsById["ruin"]=region;w.Enter(w.Player,"ruin");
            int events=0;void Observe(WorldEvent e){if(e.Kind=="landmark_discovered")events++;}
            w.Events.Published+=Observe;
            try
            {
                var room=region.Rooms.First(room=>room.Kind=="room");
                var landmark=region.Landmarks.Single(l=>l.X==room.X&&l.Y==room.Y);
                int corridor=Enumerable.Range(0,region.Tiles.Length).First(at=>region.Tiles[at]==Terrain.Floor&&region.RoomAt(at%region.Width,at/region.Width)==room&&!room.Contains(at%region.Width,at/region.Width));
                w.Player.X=region.Center(corridor).X;w.Player.Y=region.Center(corridor).Y;Exploration.Discover(w);
                check(events==0,"same-block corridor does not discover room");
                w.Player.X=region.Center(room.CenterY*region.Width+room.CenterX).X;w.Player.Y=region.Center(room.CenterY*region.Width+room.CenterX).Y;
                Exploration.Discover(w);Exploration.Discover(w);
                check(events==1&&region.DiscoveredLandmarks.Contains(Exploration.Identity(region,landmark)),"actual room discovery emitted once with stable identity");
                var timing=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<10000;i++)Exploration.Discover(w);
                double discoveryMicros=timing.Elapsed.TotalMilliseconds/10;
                timing.Restart();for(int i=0;i<10000;i++)n.Call("receive","benchmark_noop","player","","",0);
                double narrativeMicros=timing.Elapsed.TotalMilliseconds/10;
                Directory.CreateDirectory("build/adventure");File.WriteAllText("build/adventure/processing.json",JsonSerializer.Serialize(new{iterations=10000,discoveryMicrosecondsPerCall=discoveryMicros,questBridgeMicrosecondsPerCall=narrativeMicros,managedBytes=GC.GetTotalMemory(false)},Content.Json));
                string discovered=Persistence.Capture(w,n);Persistence.Restore(w,n,discovered);Exploration.Discover(w);
                check(events==1,"discovery restore emits no duplicate event");
                var legacy=JsonNode.Parse(discovered)!;foreach(var entry in legacy["Regions"]!.AsObject())
                {entry.Value!.AsObject().Remove("EncountersInitialized");entry.Value.AsObject().Remove("ExplorationInitialized");entry.Value.AsObject().Remove("DiscoveredLandmarks");entry.Value.AsObject().Remove("ExploredTiles");}
                Persistence.Restore(w,n,legacy.ToJsonString());
                check(w.RegionsById["ruin"].EncountersInitialized&&w.RegionsById["ruin"].ExplorationInitialized&&w.RegionsById["ruin"].DiscoveredLandmarks.Count==0,"current-generator absent fields preserve old population and loot without retro rewards");
            }
            finally{w.Events.Published-=Observe;}
            Persistence.Restore(w,n,original);
            w.RegionsById["ruin"]=Regions.Dungeon(1);w.Enter(w.Player,"ruin");
            var relicRegion=w.RegionsById["ruin"];var relicPoint=relicRegion.Center(relicRegion.Objective);
            w.Player.X=relicPoint.X;w.Player.Y=relicPoint.Y; // Explicit capacity fixture, never adventure traversal evidence.
            var relicBag=w.Items.Service.Call("bag","player").AsGodotObject() as Node;
            var relicLimit=GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd").New().AsGodotObject() as Node;
            relicBag!.AddChild(relicLimit!);relicLimit!.Set("capacity",relicBag.Call("get_item_count").AsInt32());
            check(!w.Interact(w.Player)&&!relicRegion.Depleted.Contains(relicRegion.Objective)&&w.Items.Count("player","relic")==0,"full inventory preserves physical relic for retry");
            relicBag.RemoveChild(relicLimit);relicLimit.Free();
            check(w.Interact(w.Player)&&relicRegion.Depleted.Contains(relicRegion.Objective)&&w.Items.Count("player","relic")==1,"relic retry transfers exactly one objective");
            check(!w.Interact(w.Player)&&w.Items.Count("player","relic")==1,"repeated relic interaction cannot duplicate objective");
            Persistence.Restore(w,n,original);
            // Deliberate integration setup for transactional failure tests, separate from adventure run.
            n.Call("accept_quest",3);w.Items.Craft("player",w.Content.Recipes.Single(r=>r.Id=="draught"));
            int rep=w.Player.Reputation.GetValueOrDefault("wardens");
            n.Call("claim_reward",3);check(w.Player.Reputation.GetValueOrDefault("wardens")==rep,"repeated completed reward is idempotent");
            Persistence.Restore(w,n,original);
            w.Items.Add("player","coin",999-w.Items.Count("player","coin"));
            var bag=w.Items.Service.Call("bag","player").AsGodotObject() as Node;
            var limit=GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd").New().AsGodotObject() as Node;
            bag!.AddChild(limit!);limit!.Set("capacity",bag.Call("get_item_count").AsInt32());
            // Force a completed legitimate crafting objective, with a whole reward that cannot fit.
            n.Call("accept_quest",3);int coins=w.Items.Count("player","coin");rep=w.Player.Reputation.GetValueOrDefault("wardens");
            n.Call("receive","crafted","player","","draught",1);
            check(n.Call("completed",3).AsBool()&&n.Call("has_pending").AsBool(),"failed synchronous delivery remains pending after completion");
            check(w.Items.Count("player","coin")==coins&&w.Player.Reputation.GetValueOrDefault("wardens")==rep,"failed reward changes no items or reputation");
            string pending=Persistence.Capture(w,n);Persistence.Restore(w,n,pending);
            n.Call("claim_pending");
            check(w.Items.Count("player","coin")==coins&&n.Call("has_pending").AsBool(),"full-inventory pending survives reload and retry");
            w.Items.Take("player","coin",1);coins--;
            n.Call("claim_pending");
            check(w.Items.Count("player","coin")==coins&&n.Call("has_pending").AsBool(),"partially available reward stack cannot partially deliver");
            // Equipment has no merge space; reject the entire multi-item grant.
            check(!w.Items.GrantReward("iron_sword",2),"full inventory equipment reward rejected wholly");
            bag=w.Items.Service.Call("bag","player").AsGodotObject() as Node;
            foreach(var child in bag!.GetChildren().Where(child=>child.GetScript().AsGodotObject()==GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd")).ToArray()) {bag.RemoveChild(child);child.Free();}
            n.Call("claim_pending");n.Call("claim_pending");
            check(w.Items.Count("player","coin")==coins+5&&w.Player.Reputation.GetValueOrDefault("wardens")==rep+5&&!n.Call("has_pending").AsBool(),"pending reward delivers and grants reputation exactly once");
            string complete=Persistence.Capture(w,n);Persistence.Restore(w,n,complete);n.Call("claim_pending");
            check(w.Items.Count("player","coin")==coins+5,"claimed reward remains unique after save/load");
            string recovery=Path.Combine(Path.GetTempPath(),"mosslight-recovery-"+Guid.NewGuid()+".json");
            try
            {
                const string oldRecovery="{\"Schema\":1,\"Generator\":\"rpg-regions-1\",\"history\":\"keep\"}";
                File.WriteAllText(recovery,oldRecovery);Persistence.PreserveOlderBackgroundSave(recovery);
                check(File.ReadAllText(recovery)==oldRecovery&&File.ReadAllText(recovery+".retained-1.json")==oldRecovery,"older background save preserved byte-for-byte before replacement");
                Persistence.PreserveOlderBackgroundSave(recovery);
                check(File.ReadAllText(recovery+".retained-1.json")==oldRecovery&&File.Exists(recovery+".retained-2.json"),"older background backup never overwrites prior retained copy");
                File.WriteAllText(recovery,"{\"Schema\":1,\"Generator\":\"rpg-regions-2\"}");Persistence.PreserveOlderBackgroundSave(recovery);
                check(!File.Exists(recovery+".retained-3.json"),"current-generation background saves do not create repeated backups");
            }
            finally {foreach(string file in Directory.GetFiles(Path.GetDirectoryName(recovery)!,Path.GetFileName(recovery)+"*"))File.Delete(file);}
            Directory.CreateDirectory("build/adventure");File.WriteAllText("build/adventure/seed-measurements.json",JsonSerializer.Serialize(rows,Content.Json));
        }
        finally{Persistence.Restore(w,n,original);app.RebuildViews();}
    }
}
