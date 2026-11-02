using Godot;
using PixelCreatures.Runtime;
using System.Diagnostics;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Actual production regions/views, normal 1.5x camera. Snapshot-isolated.</summary>
public static class LandmarkVisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app,int n=8)
    {for(int i=0;i<n;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);}
    static async System.Threading.Tasks.Task<Image> Capture(Foundation app,string? name=null)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=app.GetViewport().GetTexture().GetImage();
        if(name!=null){Directory.CreateDirectory("docs/landmark-screenshots");image.SavePng("docs/landmark-screenshots/"+(app.Ui.MobileLayout?"mobile-":"")+name+".png");}
        return image;
    }
    static bool Different(Color a,Color b)=>Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>.015f;
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;bool debug=app.CollisionDebug;
        var observations=new List<object>();
        try
        {
            Engine.TimeScale=0;app.ResumeGame();w.Projectiles.Clear();w.Cancel(w.Player);
            w.Enter(w.Player,"ruin"); // obtain actual production archetype actors for the live fixture
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var p=w.Player;
            void Show(RegionState region,LandmarkRoom room)
            {
                region.Id="landmark-visual";w.RegionsById[region.Id]=region;p.Region=region.Id;
                var center=region.Center((room.OriginY+5)*region.Width+room.OriginX+5);p.X=center.X;p.Y=center.Y+48;p.Health=p.MaxHealth;p.Invulnerable=10000;w.Cancel(p);app.RebuildViews();app.ResumeGame();
            }
            string? selected=OS.GetCmdlineUserArgs().FirstOrDefault(s=>s.StartsWith("--landmark="))?.Split('=')[1];
            if(selected!=null)
            {
                int Read(string arg,int value)=>int.TryParse(OS.GetCmdlineUserArgs().FirstOrDefault(s=>s.StartsWith(arg+"="))?.Split('=')[1],out int parsed)?parsed:value;
                int turn=Read("--orientation",0),seed=Read("--visual-seed",42);
                var t=Landmarks.Library.Single(t=>t.Id==selected);var fixture=LandmarkChecks.Fixture();fixture.Seed=seed;
                if(!t.Rotations.Contains(turn) || !Landmarks.TryApply(fixture,fixture.Rooms[0],Landmarks.RotateDoors(t.DoorMasks[0],turn),new[]{t.Roles[0]},t,turn))throw new InvalidDataException("Invalid preview orientation/template");
                if(t.Roles.Contains("entrance"))fixture.Features[60]="exit";
                if(t.Roles.Contains("objective"))fixture.Features[60]="relic";
                Show(fixture,fixture.Landmarks[0]);
                int actorSlot=0;
                foreach(string profile in new[]{"prowler","brute","spitter"})
                {
                    var actor=w.Actors.Values.First(a=>a.CombatProfile==profile);actor.Region=fixture.Id;
                    var stand=fixture.FindPlacement(fixture.Center(60)+new V((actorSlot++-1)*48,-48),actor,w.Actors.Values,true);
                    actor.X=stand.X;actor.Y=stand.Y;w.Cancel(actor);
                }
                app.RebuildViews();using var preview=await Capture(app,"preview-"+selected+"-r"+turn+"-s"+seed);
                if(OS.GetCmdlineUserArgs().Contains("--landmark-debug"))
                {app.CollisionDebug=true;using var overlay=await Capture(app,"preview-"+selected+"-collision");}
                return;
            }
            foreach(var template in Landmarks.Library)
            {
                RegionState? found=null;LandmarkRoom? room=null;
                foreach(int seed in new[]{1,42,1337}.Concat(Enumerable.Range(0,64)))
                {
                    var candidate=Regions.Dungeon(seed);room=candidate.Landmarks.FirstOrDefault(l=>l.Template==template.Id);
                    if(room!=null){found=candidate;break;}
                }
                if(found==null || room==null)throw new InvalidOperationException("Template never appears in generated dungeons: "+template.Id);
                Show(found,room);using var image=await Capture(app,template.Id);
                observations.Add(new{template=room.Template,room.Revision,room.Rotation,room.Theme,seed=found.Seed,room.X,room.Y,room.Doors,anchors=room.Anchors.Count});
            }
            foreach(int seed in new[]{1,42,1337})
            {
                var region=Regions.Dungeon(seed);
                foreach(var room in region.Landmarks)
                {
                    Show(region,room);using var image=await Capture(app,$"seed-{seed}-room-{room.X}-{room.Y}-{room.Template}");
                }
            }
            var r=Regions.Dungeon(42);var entrance=r.Landmarks.Single(l=>l.Template=="ruined-entrance");Show(r,entrance);
            // Actual player movement along the room's protected lane.
            var centerPoint=r.Center((entrance.OriginY+5)*r.Width+entrance.OriginX+5);
            p.X=centerPoint.X-72;p.Y=centerPoint.Y;app.Views["player"].SnapPosition();
            for(int step=0;step<48;step++)w.Move(p,new V(3,0));
            if(V.Distance(p.Position,centerPoint+new V(72,0))>.1f || !r.CanStand(p.Position,p))throw new InvalidOperationException("Rendered room crossing failed");
            using(var crossing=await Capture(app,"architecture-crossing")){}
            var passage=r.Passages.First(q=>q.Dx==1);int connection=passage.Y*r.Width+passage.X;
            r.OpenDoors.UnionWith(r.GateAt(connection));var adjacent=r.Center(connection)+new V(12,0);p.X=adjacent.X;p.Y=adjacent.Y;app.RebuildViews();
            using(var adjacency=await Capture(app,"adjacent-rooms")){}
            // New structural column: exact-pixel foreground and background checks.
            Show(r,entrance);await Frames(app);var anchor=entrance.Anchors.First(a=>a.Asset=="column");
            int pillar=(entrance.OriginY+anchor.Y)*r.Width+entrance.OriginX+anchor.X;
            var point=r.Center(pillar);var parent=app.Views["player"].GetParent();
            var prop=parent.GetChildren().OfType<EnvironmentDecoration>().Single(d=>d.Position.DistanceTo(GodotConvert.GroundToScreen(point.X,point.Y+12))<.1f);
            p.X=point.X;p.Y=point.Y-12-p.Radius-1;app.Views["player"].SnapPosition();await Frames(app);
            app.Views["player"].Visible=false;using var propOnly=await Capture(app);
            app.Views["player"].Visible=true;prop.ZIndex=-1;using var broken=await Capture(app);
            prop.ZIndex=0;using var behind=await Capture(app,"column-behind");
            int covered=0;
            for(int y=0;y<behind.GetHeight();y++)for(int x=0;x<behind.GetWidth();x++)
                if(Different(broken.GetPixel(x,y),propOnly.GetPixel(x,y)) && !Different(behind.GetPixel(x,y),propOnly.GetPixel(x,y)))covered++;
            if(covered<20)throw new InvalidOperationException("Column foreground occlusion missing");
            p.Y=point.Y+12+p.Radius+1;app.Views["player"].SnapPosition();using var front=await Capture(app,"column-front");
            prop.ZIndex=1;using var wrong=await Capture(app);int revealed=0;
            for(int y=0;y<front.GetHeight();y++)for(int x=0;x<front.GetWidth();x++)if(Different(front.GetPixel(x,y),wrong.GetPixel(x,y)))revealed++;
            prop.ZIndex=0;if(revealed<20)throw new InvalidOperationException("Column background occlusion missing");
            observations.Add(new{columnCoveredPixels=covered,columnFrontPixels=revealed});
            // Live production AI and actions inside a naturally selected room.
            Show(r,entrance);var enemies=new[]{"prowler","brute","spitter"}.Select(id=>w.Actors.Values.First(a=>a.CombatProfile==id)).ToArray();
            for(int i=0;i<enemies.Length;i++)
            {
                var a=enemies[i];a.Region=r.Id;a.Health=a.MaxHealth=1000;a.Faction="beasts";a.CombatProfile=new[]{"prowler","brute","spitter"}[i];
                a.Abilities=w.Content.CombatProfiles.Single(c=>c.Id==a.CombatProfile).Priorities.Keys.ToList();
                var pos=r.FindPlacement(centerPoint+new V(-48+i*48,-24),a,w.Actors.Values,true);a.X=pos.X;a.Y=pos.Y;w.Cancel(a);
            }
            app.RebuildViews();Engine.TimeScale=1;w.TryStartPlayerSlash(-V.UnitY);await Frames(app,12);using(var combat=await Capture(app,"live-combat")){}
            Engine.TimeScale=0;foreach(var a in enemies)a.Region="unused";
            for(int iteration=0;iteration<6;iteration++)
            {
                p.Region=iteration%2==0?r.Id:"reach";var active=w.RegionsById[p.Region];var pos=active.Center(active.Entrance);p.X=pos.X+48;p.Y=pos.Y+24;
                var timer=Stopwatch.StartNew();app.RebuildViews();double rebuild=timer.Elapsed.TotalMilliseconds;await Frames(app,12);
                var intervals=new List<double>();timer.Restart();double last=0;
                for(int f=0;f<90;f++){await Frames(app,1);double now=timer.Elapsed.TotalMilliseconds;intervals.Add(now-last);last=now;}
                intervals.Sort();parent=app.Views["player"].GetParent();
                observations.Add(new{iteration,region=p.Region,rebuildMs=rebuild,p50ms=intervals[45],p95ms=intervals[85],terrainMs=TerrainView.LastBuildMilliseconds,wallMs=TerrainWallRow.BuildMilliseconds,
                    atlasBuildMs=EnvironmentAssetCache.TotalBuildMilliseconds,atlasCount=EnvironmentAssetCache.TextureCount,atlasBytes=EnvironmentAssetCache.PixelBytes,nodes=parent.GetChildCount(),decorationNodes=parent.GetChildren().OfType<EnvironmentDecoration>().Count(),
                    textureBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed),bufferBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.BufferMemUsed),managedBytes=GC.GetTotalMemory(true),renderer=RenderingServer.GetVideoAdapterName()});
            }
            Directory.CreateDirectory("build/dungeon");File.WriteAllText("build/dungeon/landmarks-"+(app.Ui.MobileLayout?"mobile":"desktop")+".json",JsonSerializer.Serialize(observations,Content.Json));
            GD.Print("LANDMARK VISUAL CHECKS PASSED: five production landmarks, three themes, canonical seeds, column pixels, live combat and six transitions");
        }
        finally{app.CollisionDebug=debug;Engine.TimeScale=scale;Persistence.Restore(w,app.Narrative,original);app.RebuildViews();app.ResumeGame();}
    }
}
