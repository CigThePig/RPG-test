using Godot;
using PixelCreatures.Runtime;
using V=System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Tick-scheduled real input dispatch. Fixture setup is explicit; controller is never mocked.</summary>
public sealed class GameplayReplay
{
    readonly Foundation app;
    int frame;
    int local;
    int index=-1;
    V origin;
    float distance;
    double incomingYaw;
    int combatHits, combatMisses, dodges, dodgedHits, lungeCollisions;
    int turnSign, turnChanges;
    double turnPrevious;
    readonly List<object> results=new();
    Queue<V> route=new();
    public bool Finished { get; private set; }
    public string Output { get; }
    record Scenario(string Id,int Ticks,V Start,Vector2 Direction,string Region="feel-lab");
    readonly Scenario[] scenarios = {
        new("horizontal",100,new(300,300),Vector2.Right),
        new("vertical",100,new(300,300),Vector2.Down),
        new("diagonal",100,new(300,300),new Vector2(1,1).Normalized()),
        new("tap-stop",50,new(300,300),Vector2.Right),
        new("reversal",100,new(300,300),Vector2.Right),
        new("eight-turns",160,new(300,300),Vector2.Right),
        new("analog-ramp",100,new(300,300),Vector2.Right),
        new("camera-follow",100,new(1050,600),Vector2.Left),
        new("wall-slide",140,new(680,300),new Vector2(1,1).Normalized()),
        new("wall-recover",140,new(680,300),Vector2.Right),
        new("narrow-passage",100,new(700,588),Vector2.Right),
        new("menu-cancel",70,new(300,300),Vector2.Right),
        new("multitouch",70,new(300,300),Vector2.Right),
        new("chest",100,new(374,300),Vector2.Right),
        new("settlement-route",240,new(0,0),Vector2.Right,"reach"),
        new("dungeon-route",240,new(0,0),Vector2.Right,"ruin"),
        new("combat-movement",180,new(0,0),Vector2.Right,"reach"),
        new("attack-readability",200,new(300,300),Vector2.Right),
        new("combat-empty",170,new(300,300),Vector2.Zero),
        new("combat-ahead",75,new(300,300),Vector2.Zero),
        new("combat-behind",75,new(300,300),Vector2.Zero),
        new("combat-enter",75,new(300,300),Vector2.Zero),
        new("combat-wall",75,new(700,300),Vector2.Zero),
        new("dodge-idle",75,new(300,300),Vector2.Zero),
        new("dodge-held",75,new(300,300),Vector2.Right),
        new("dodge-wall",75,new(700,300),Vector2.Zero),
        new("dodge-evasion",75,new(300,300),Vector2.Zero),
        new("turn-south-north",100,new(300,300),Vector2.Up)
    };
    public GameplayReplay(Foundation owner)
    {
        app=owner;
        var args=OS.GetCmdlineUserArgs();
        Output=args.FirstOrDefault(x=>x.StartsWith("--evidence="))?.Split('=',2)[1] ?? "build/gameplay";
        Directory.CreateDirectory(Output);
        app.Trace.Start(Path.Combine(Output,"trace.jsonl"));
        app.World.CombatDiagnostic += (kind, _, context) =>
        {
            if (kind == "attack_impact") { if (context.StartsWith("hit/")) combatHits++; else combatMisses++; }
            if (kind == "dodge_started") dodges++;
            if (kind == "direct_hit_dodged") dodgedHits++;
            if (kind == "attack_lunge_collision") lungeCollisions++;
        };
        app.Trace.Marker("replay-warmup");
        app.ResumeGame();
        // Dedicated region with production circular collision, terrain view, PPC, UI and camera.
        // No changes to authored regions or on-disk saves.
        var r=CreateCourse();
        app.World.RegionsById[r.Id]=r; app.World.InitializeContainers(r);
        app.World.Player.Region=r.Id;
        app.RebuildViews();
    }
    public static RegionState CreateCourse()
    {
        var r=new RegionState {Id="feel-lab",Width=64,Height=48,Tiles=Enumerable.Repeat(Terrain.Grass,64*48).ToArray(),Entrance=12*64+12, ContainersInitialized=false};
        for(int y=0;y<48;y++) for(int x=0;x<64;x++)
            if(x==0||y==0||x==63||y==47||(x==30 && y!=24))r.Tiles[y*64+x]=Terrain.Rock;
        r.Features[12*64+18]="chest";
        r.Features[17*64+16]="herb";
        r.Features[18*64+18]="ore";
        r.Features[17*64+20]="wood";
        return r;
    }
    void TapAction(string label)
    {
        var button=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith(label));
        var at=button.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=at});
        Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=at});
    }
    void Touch(bool pressed) => Input.ParseInputEvent(new InputEventScreenTouch {Index=7,Pressed=pressed,Position=app.Ui.Stick.GlobalPosition+app.Ui.Stick.Size/2});
    void Drag(Vector2 direction) => Input.ParseInputEvent(new InputEventScreenDrag {Index=7,Position=app.Ui.Stick.GlobalPosition+app.Ui.Stick.Size/2+direction*72});
    public void BeforeStep()
    {
        if(Finished)return;
        if(frame++<30)return; // scene is ready; warm raster cache, not a wall-clock sleep
        if(index<0 || local>=scenarios[index].Ticks)
        {
            if(index>=0) EndScenario();
            if(++index>=scenarios.Length){Finish();return;}
            local=0; distance=0;
            var s=scenarios[index]; var p=app.World.Player;
            if(p.Region!=s.Region)app.World.Enter(p,s.Region);
            var map=app.World.RegionsById[p.Region];
            var start=s.Region=="feel-lab" ? s.Start : map.Center(map.Entrance);
            p.X=start.X;p.Y=start.Y;p.State=ActionState.Idle;p.StateRemaining=0;
            p.Health=p.MaxHealth;p.Mana=p.MaxMana;p.Statuses.Clear();p.Cooldowns.Clear();p.PendingAbility="";
            route.Clear();
            if(s.Id.EndsWith("-route"))
            {
                var best=map.Features.Keys.Select(at=>map.Path(p.Position,map.Center(at),p)).Where(path=>path.Count>0).OrderByDescending(path=>path.Count).First();
                route=new Queue<V>(best.Select(map.Center));
            }
            if(s.Id=="combat-movement")
            {
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast" && a.Alive);
                enemy.Region=p.Region;
                var at=map.FindPlacement(p.Position+new V(110,0),enemy,app.World.Actors.Values);
                enemy.X=at.X;enemy.Y=at.Y;
                app.Views[enemy.Id].SnapPosition();
            }
            if(s.Id=="attack-readability")
            {
                app.World.Events.Recent.Clear();
                // The fixture's enemy is east; don't depend on the preceding scenario's yaw.
                app.Views["player"].Creature.Motor!.Teleport(new PixelCreatures.Core.Vec3(p.X, 0, p.Y), 0);
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast");
                enemy.Region=p.Region;enemy.X=344;enemy.Y=300;enemy.Speed=0;
                enemy.Health=enemy.MaxHealth=10000;enemy.Abilities.Clear();
                enemy.State=ActionState.Recovery;enemy.StateRemaining=100;
                app.Views[enemy.Id].SnapPosition();
            }
            app.ControllerVelocity=Vector2.Zero;
            app.Ui.ClosePanels(); app.ResumeGame();
            app.Views[p.Id].SnapPosition();
            if (s.Id.StartsWith("combat-") && s.Id != "combat-movement" || s.Id.StartsWith("dodge-") || s.Id == "turn-south-north")
            {
                combatHits=combatMisses=dodges=dodgedHits=lungeCollisions=0; turnSign=turnChanges=0;
                if (s.Id is "combat-wall" or "dodge-wall")
                {
                    // Start two units from the wall using the actual actor radius.
                    p.X=720-p.Radius-2; app.Views[p.Id].SnapPosition();
                }
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast");
                enemy.Region="reach"; enemy.Abilities.Clear();
                if (s.Id is "combat-ahead" or "combat-behind" or "combat-enter" or "dodge-evasion")
                {
                    enemy.Region=p.Region; enemy.X=p.X+(s.Id=="combat-behind" ? -30 : s.Id=="combat-enter" ? 100 : 40); enemy.Y=p.Y;
                    enemy.Health=enemy.MaxHealth=10000; enemy.Invulnerable=0; enemy.State=ActionState.Recovery; enemy.StateRemaining=100;
                    app.Views[enemy.Id].SnapPosition();
                }
                double yaw=s.Id=="turn-south-north" ? -Math.PI/2 : 0;
                app.Views[p.Id].Creature.ClearFacingRequest();
                app.Views[p.Id].Creature.Motor!.Teleport(new PixelCreatures.Core.Vec3(p.X,0,p.Y),yaw);
                turnPrevious=yaw;
            }
            origin=p.Position;
            app.Ui.Notify($"REPLAY · {app.Feel.Id} · {s.Id}");
            app.Trace.Scenario=s.Id; app.Trace.Marker("scenario-start/teleport");
            Touch(true);
        }
        var test=scenarios[index]; var direction=test.Direction;
        if(test.Id.EndsWith("-route"))
        {
            while(route.Count>0 && V.Distance(app.World.Player.Position,route.Peek())<5)route.Dequeue();
            if(route.Count==0)direction=Vector2.Zero;
            else {var d=route.Peek()-app.World.Player.Position;direction=GodotConvert.GroundToScreen(d.X,d.Y).Normalized();}
        }
        if(test.Id=="combat-movement")
        {
            direction=Vector2.Right.Rotated(local*Mathf.Pi/90);
            if(local==30 || local==100)app.Cast("ember");
        }
        if(test.Id=="attack-readability")
        {
            if(local>=10)direction=Vector2.Zero;
            if(local is 25 or 75 or 150)
            {
                // Reset target range after knockback so every speed preset exercises the same three strikes.
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast");
                enemy.X=app.World.Player.X+28;enemy.Y=app.World.Player.Y;app.Views[enemy.Id].SnapPosition();
                app.Trace.Marker("fixture/target-range-reset-for-slash");
                var button=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith("Attack"));
                var pos=button.GetGlobalRect().GetCenter();
                Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=pos});
                Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=pos});
            }
            // Explicit NPC combat command: real ability windup/damage/knockback, not player input injection.
            if(local==110)
            {
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast");
                enemy.X=app.World.Player.X+28;enemy.Y=app.World.Player.Y;app.Views[enemy.Id].SnapPosition();
                app.Trace.Marker("fixture/npc-reposition-for-bite");
                enemy.Abilities.Add("bite");enemy.State=ActionState.Idle;enemy.StateRemaining=0;enemy.Cooldowns.Clear();enemy.Mana=enemy.MaxMana;
                incomingYaw=app.Views["player"].Creature.Motor!.Yaw;
                if(!app.World.Activate(enemy.Id,"bite","player"))throw new InvalidOperationException("Hit-reaction fixture could not activate bite");
            }
        }
        if(test.Id=="attack-readability" && local==145 && Math.Abs(PixelCreatures.Core.DMath.WrapAngle(app.Views["player"].Creature.Motor!.Yaw-incomingYaw))>.12)
            throw new InvalidOperationException("Incoming hit changed player facing");
        if (test.Id == "combat-empty" && (local is 10 or 60 or 110)) TapAction("Attack");
        if (test.Id is "combat-ahead" or "combat-behind" or "combat-enter" or "combat-wall")
        {
            if (local == 10) TapAction("Attack");
            if (test.Id == "combat-enter" && local == 16)
            {
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast");
                enemy.X=app.World.Player.X+30; enemy.Y=app.World.Player.Y; app.Views[enemy.Id].SnapPosition();
            }
        }
        if (test.Id.StartsWith("dodge-"))
        {
            if (local == 12) TapAction("Dodge");
            if (test.Id == "dodge-held" && local >= 36) direction=Vector2.Zero;
            if (test.Id == "dodge-evasion" && local == 17)
            {
                var enemy=app.World.Actors.Values.First(a=>a.Profile=="beast");
                app.World.Projectiles.Add(new ProjectileState { Source=enemy.Id, Target="player", Ability="ember", X=app.World.Player.X, Y=app.World.Player.Y });
            }
        }
        if (test.Id == "turn-south-north" && local >= 70) direction=Vector2.Zero;
        if(test.Id=="tap-stop" && local>=3) direction=Vector2.Zero;
        if(test.Id=="reversal" && local>=45) direction=Vector2.Left;
        if(test.Id=="eight-turns") direction=Vector2.Right.Rotated((local/20)*Mathf.Pi/4);
        if(test.Id=="analog-ramp") direction*=Math.Min(1,local/60f);
        if(test.Id=="wall-recover" && local>=65)direction=Vector2.Left;
        if(test.Id=="menu-cancel")
        {
            if(local==20)app.Ui.Menu();
            if(local==40)app.ResumeGame();
            if(local>=20)direction=Vector2.Zero;
        }
        if(test.Id=="multitouch" && local==30)
        {
            var b=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(x=>x.Text.Contains("Draught"));
            var pos=b.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=pos});
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=pos});
        }
        if(test.Id=="chest" && V.Distance(app.World.Player.Position,new V(444,300))<30)direction=Vector2.Zero;
        Drag(direction);
        if(local==test.Ticks-15)Touch(false);
        if(test.Id=="chest" && local==test.Ticks-5)
        {
            // Keyboard event goes through _UnhandledInput and production interaction guards.
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.E,Pressed=true});
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.E,Pressed=false});
        }
        // Godot may coalesce drag events until the rendered frame boundary. Flush
        // the injected batch before this physics step so two catch-up ticks cannot
        // accidentally reuse the previous tick's thumb direction.
        Input.FlushBufferedEvents();
        local++;
    }
    V previous;
    public void AfterStep()
    {
        if(index<0||Finished)return;
        var p=app.World.Player.Position;
        if(!app.World.RegionsById[app.World.Player.Region].CanStand(p,app.World.Player))throw new InvalidOperationException("Replay lost footprint clearance: "+scenarios[index].Id);
        if(local>1)distance+=V.Distance(p,previous);
        if (scenarios[index].Id=="turn-south-north")
        {
            var motor=app.Views["player"].Creature.Motor!;
            double delta=PixelCreatures.Core.DMath.WrapAngle(motor.Yaw-turnPrevious);
            double error=Math.Abs(PixelCreatures.Core.DMath.WrapAngle(motor.Context.TargetYaw-motor.Yaw));
            int sign=Math.Abs(delta)>.00001 ? Math.Sign(delta) : 0;
            if (error>.12 && sign!=0) { if(turnSign!=0 && sign!=turnSign) turnChanges++; turnSign=sign; }
            turnPrevious=motor.Yaw;
        }
        previous=p;
    }
    void EndScenario()
    {
        Touch(false);
        var s=scenarios[index]; var p=app.World.Player;
        bool valid=app.World.RegionsById[p.Region].CanStand(p.Position,p);
        bool moved=distance>1;
        results.Add(new {scenario=s.Id,ticks=s.Ticks,groundDistance=distance,projectedDisplacement=GodotConvert.GroundToScreen(p.X-origin.X,p.Y-origin.Y).Length(),x=p.X,y=p.Y,clearance=valid,moved,frame=Engine.GetFramesDrawn(),speed=p.Speed});
        app.Trace.Marker("scenario-end");
        if(s.Id=="attack-readability" && !app.World.Events.Recent.Any(e=>e.Kind=="damage" && e.Target=="player"))throw new InvalidOperationException("Hit-reaction fixture never hit the player");
        if(s.Id=="attack-readability" && app.World.Events.Recent.Count(e=>e.Kind=="ability_started" && e.Actor=="player" && e.Value=="slash")!=3)throw new InvalidOperationException("Three Attack touches did not activate exactly three slashes");
        if (s.Id=="combat-empty" && combatMisses!=3) throw new InvalidOperationException("Empty Attack touches did not produce three misses");
        if ((s.Id is "combat-ahead" or "combat-enter") && combatHits!=1) throw new InvalidOperationException("Directional replay did not hit its eligible target: "+s.Id);
        if ((s.Id is "combat-behind" or "combat-wall") && combatMisses!=1) throw new InvalidOperationException("Directional replay did not miss: "+s.Id);
        if (s.Id=="combat-wall" && lungeCollisions==0) throw new InvalidOperationException("Lunge fixture never contacted the wall");
        if (s.Id=="dodge-wall" && (distance>5 || !valid)) throw new InvalidOperationException("Wall did not block the dodge");
        if (s.Id.StartsWith("dodge-") && dodges!=1) throw new InvalidOperationException("Dodge touch did not start exactly one dodge: "+s.Id);
        if (s.Id=="dodge-evasion" && dodgedHits!=1) throw new InvalidOperationException("Dodge did not reject the incoming projectile");
        if (s.Id=="turn-south-north" && (turnChanges!=0 || Math.Abs(PixelCreatures.Core.DMath.WrapAngle(app.Views["player"].Creature.Motor!.Yaw-Math.PI/2))>.12)) throw new InvalidOperationException("South-to-north facing oscillated or did not complete");
        if(s.Id=="chest" && !app.World.RegionsById[p.Region].Depleted.Contains(12*64+18))throw new InvalidOperationException("Chest input interaction failed");
        if(!valid||!moved)throw new InvalidOperationException($"Replay failed: {s.Id} at {p.Position}, blocked={app.World.LastBlockedReason}");
    }
    void Finish()
    {
        Finished=true;
        app.Trace.Dispose();
        File.WriteAllText(Path.Combine(Output,"scenarios.json"),System.Text.Json.JsonSerializer.Serialize(results,Content.Json));
        GD.Print("GAMEPLAY REPLAY PASSED");app.GetTree().Quit();
    }
}
