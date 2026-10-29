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
            foreach(bool legacy in new[]{true,false})
            {
                var r=legacy?JsonSerializer.Deserialize<RegionState>(Godot.FileAccess.GetFileAsString("res://game/tests/fixtures/pr4-seed42-region.json"),Content.Json)!:Regions.Dungeon(42);
                r.Id="ruin";w.RegionsById["ruin"]=r;
                var p=w.Player;p.Region="ruin";w.Cancel(p);p.Health=p.MaxHealth;p.Invulnerable=1000;
                int cell=legacy?7:11,center=cell/2;
                var room=r.Center(r.Entrance);p.X=room.X;p.Y=room.Y;
                app.RebuildViews();app.ResumeGame();
                string tag=legacy?"before":"after";
                await Capture(app,tag+"-room");
                var camera=app.GetViewport().GetCamera2D();
                var intervals=new List<double>();var clock=Stopwatch.StartNew();double last=clock.Elapsed.TotalMilliseconds;
                for(int frame=0;frame<90;frame++)
                {
                    await Frames(app,1);double now=clock.Elapsed.TotalMilliseconds;intervals.Add(now-last);last=now;
                }
                intervals.Sort();stats.Add(new{legacy,seed=42,cell,interior=cell-2,passage=legacy?1:3,zoom=camera.Zoom.X,width=r.Width,height=r.Height,terrainBuildMs=TerrainView.LastBuildMilliseconds,p50ms=intervals[45],p95ms=intervals[85],renderer=RenderingServer.GetVideoAdapterName()});
                // Equivalent first open graph edge, same seed and scan ordering.
                int door=-1;V direction=V.Zero;
                for(int y=0;y<r.Height && door<0;y+=cell)for(int x=0;x<r.Width && door<0;x+=cell)
                    foreach(var d in new[]{new V(1,0),new V(0,1)})
                    {
                        int at=(y+center+(int)d.Y*center)*r.Width+x+center+(int)d.X*center;
                        if(r.Tiles[at]!=Terrain.Floor||r.Features.ContainsKey(at))continue;
                        door=at;direction=d;break;
                    }
                if(door<0)throw new InvalidOperationException("No ordinary visual doorway");
                var doorway=r.Center(door)+direction*12;p.X=doorway.X;p.Y=doorway.Y;app.RebuildViews();
                await Capture(app,tag+"-doorway");
                if(legacy)continue;
                w.Move(p,direction*48);app.Views["player"].SnapPosition();await Capture(app,"adjacent-room");
                var gate=r.GateGroups().First();
                int stand=gate.SelectMany(r.Neighbours).Distinct().Order().First(at=>r.CanStand(r.Center(at),p)&&gate.Any(g=>V.Distance(r.Center(g),r.Center(at))<48));
                var atStand=r.Center(stand);p.X=atStand.X;p.Y=atStand.Y;app.RebuildViews();await Capture(app,"gate-closed");
                w.Items.Add(p.Id,"dungeon_key",1);
                if(!w.Interact(p)||!gate.All(r.OpenDoors.Contains))throw new InvalidOperationException("Visual gate interaction failed");
                await Capture(app,"gate-open");
                p.X=room.X;p.Y=room.Y;
                var foes=w.Actors.Values.Where(a=>a.Id!="player"&&a.Profile=="beast").Take(3).ToArray();
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
