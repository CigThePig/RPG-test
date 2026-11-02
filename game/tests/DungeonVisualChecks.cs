using Godot;
using System.Diagnostics;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;
public static class DungeonVisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app,int count=12)
    {
        for(int i=0;i<count;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    static async System.Threading.Tasks.Task Capture(Foundation app,string name)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        app.GetViewport().GetTexture().GetImage().SavePng("docs/native-screenshots/"+(app.Ui.MobileLayout?"mobile-":"")+"dungeon-"+name+".png");
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;
        var stats=new List<object>();
        try
        {
            Engine.TimeScale=0;
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            {
                var r=Regions.Dungeon(42);
                r.Id="ruin";w.RegionsById["ruin"]=r;
                var p=w.Player;p.Region="ruin";w.Cancel(p);p.Health=p.MaxHealth;p.Invulnerable=1000;
                var room=r.Center(r.Entrance);p.X=room.X;p.Y=room.Y;
                app.RebuildViews();app.ResumeGame();
                await Capture(app,"room");
                var camera=app.GetViewport().GetCamera2D();
                var intervals=new List<double>();var clock=Stopwatch.StartNew();double last=clock.Elapsed.TotalMilliseconds;
                for(int frame=0;frame<90;frame++)
                {
                    await Frames(app,1);double now=clock.Elapsed.TotalMilliseconds;intervals.Add(now-last);last=now;
                }
                intervals.Sort();stats.Add(new{seed=42,cell=DungeonGeometry.Cell,core=DungeonGeometry.Core,passage=DungeonGeometry.Opening,rooms=r.Rooms.Count(c=>c.Kind=="room"),halls=r.Rooms.Count(c=>c.Kind=="hall"),zoom=camera.Zoom.X,width=r.Width,height=r.Height,terrainBuildMs=TerrainView.LastBuildMilliseconds+TerrainWallRow.BuildMilliseconds,wallBuildMs=TerrainWallRow.BuildMilliseconds,p50ms=intervals[45],p95ms=intervals[85],renderer=RenderingServer.GetVideoAdapterName()});
                // First ungated corridor crossing in generation order.
                var passage=r.Passages.First(q=>!q.Locked);V direction=new(passage.Dx,passage.Dy);
                var doorway=r.Center(passage.Y*r.Width+passage.X)+direction*12;p.X=doorway.X;p.Y=doorway.Y;app.RebuildViews();
                await Capture(app,"doorway");
                w.Move(p,direction*48);app.Views["player"].SnapPosition();await Capture(app,"corridor");
                var gate=r.GateGroups().First();
                int stand=gate.SelectMany(r.Neighbours).Distinct().Order().First(at=>r.CanStand(r.Center(at),p)&&gate.Any(g=>V.Distance(r.Center(g),r.Center(at))<48));
                var atStand=r.Center(stand);p.X=atStand.X;p.Y=atStand.Y;app.RebuildViews();await Capture(app,"gate-closed");
                w.Items.Add(p.Id,"dungeon_key",1);
                if(!w.Interact(p)||!gate.All(r.OpenDoors.Contains))throw new InvalidOperationException("Visual gate interaction failed");
                await Capture(app,"gate-open");
                p.X=room.X;p.Y=room.Y;
                var foes=w.Actors.Values.Where(a=>a.Id.StartsWith("ruin:spawn:")).Take(3).ToArray();
                for(int i=0;i<foes.Length;i++)
                {
                    var a=foes[i];string id=new[]{"prowler","brute","spitter"}[i];
                    a.Region="ruin";a.CombatProfile=id;a.Abilities=w.Content.CombatProfiles.Single(c=>c.Id==id).Priorities.Keys.ToList();a.Health=a.MaxHealth=1000;a.Faction="beasts";
                    var pos=r.FindPlacement(room+new V((i-1)*60,70),a,w.Actors.Values);a.X=pos.X;a.Y=pos.Y;w.Cancel(a);
                }
                app.RebuildViews();await Capture(app,"mixed-room");
                var foe=foes[0];var near=doorway+direction*42;
                if(!r.CanStand(near,foe))throw new InvalidOperationException("Doorway enemy footprint");
                foe.X=near.X;foe.Y=near.Y;p.X=doorway.X-direction.X*24;p.Y=doorway.Y-direction.Y*24;
                app.RebuildViews();await Capture(app,"enemy-doorway");
            }
            Directory.CreateDirectory("build/dungeon");File.WriteAllText("build/dungeon/render-"+(app.Ui.MobileLayout?"mobile":"desktop")+".json",JsonSerializer.Serialize(stats,Content.Json));
        }
        finally{Engine.TimeScale=scale;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
