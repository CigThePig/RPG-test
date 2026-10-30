using Godot;
using System.Diagnostics;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Production renderer reference compositions and bounded region-entry observations.</summary>
public static class EnvironmentVisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app,int n=8)
    { for(int i=0;i<n;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame); }
    static async System.Threading.Tasks.Task Capture(Foundation app,string name)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Directory.CreateDirectory("docs/native-screenshots");
        using var image=app.GetViewport().GetTexture().GetImage();
        image.SavePng("docs/native-screenshots/"+(app.Ui.MobileLayout?"mobile-":"")+"environment-"+name+".png");
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;
        var observations=new List<object>();
        try
        {
            Engine.TimeScale=0;app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var r=Regions.Dungeon(42);r.Id="environment-reference";
            var room=r.Center(r.Entrance);r.Features.Clear();r.OpenDoors.Clear();r.Depleted.Clear();
            // Reference props are ordinary FeatureViews consuming authoritative fixture features.
            int At(float dx,float dy)=>r.Index(room+new V(dx,dy));
            foreach(var entry in new[]{(-48f,24f,"chest"),(48f,-24f,"ore"),(72f,48f,"herb"),(-72f,-48f,"relic")})
                r.Features[At(entry.Item1,entry.Item2)]=entry.Item3;
            w.RegionsById[r.Id]=r;var p=w.Player;p.Region=r.Id;p.X=room.X;p.Y=room.Y+24;p.Health=p.MaxHealth;p.Invulnerable=1000;w.Cancel(p);
            var foes=w.Actors.Values.Where(a=>a.Id!="player").OrderBy(a=>a.Id,StringComparer.Ordinal).Take(3).ToArray();
            for(int i=0;i<foes.Length;i++)
            {
                var a=foes[i];a.Region=r.Id;a.X=room.X+30+i*24;a.Y=room.Y+24+i*24;
                a.Health=a.MaxHealth=1000;a.Faction="beasts";w.Cancel(a);
                a.CombatProfile=new[]{"prowler","brute","spitter"}[i];
                a.Abilities=w.Content.CombatProfiles.Single(c=>c.Id==a.CombatProfile).Priorities.Keys.ToList();
            }
            app.RebuildViews();await Capture(app,"dungeon-reference");
            // Normal-zoom source for optional nearest-neighbour object crops.
            await Capture(app,"objects");
            // Sample real windup/telegraph and multi-actor animation at production zoom.
            Engine.TimeScale=.15;w.TryStartPlayerSlash(V.UnitX);
            await Capture(app,"dungeon-combat");
            Engine.TimeScale=1;
            var combatFrames=new List<double>();var combatTimer=Stopwatch.StartNew();double combatLast=0;
            for(int frame=0;frame<120;frame++)
            {
                await Frames(app,1);double now=combatTimer.Elapsed.TotalMilliseconds;
                combatFrames.Add(now-combatLast);combatLast=now;
            }
            combatFrames.Sort();
            observations.Add(new {workload="three live archetypes",p50ms=combatFrames[60],p95ms=combatFrames[114],
                managedBytes=GC.GetTotalMemory(true),godotStaticBytes=OS.GetStaticMemoryUsage(),
                textureMemoryBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed),
                bufferMemoryBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.BufferMemUsed)});
            await Capture(app,"dungeon-combat-live");Engine.TimeScale=0;
            // Same infrastructure, real overworld geography and interactive tree/chest/resources.
            Persistence.Restore(w,app.Narrative,snapshot);p=w.Player;
            p.Region="reach";w.Cancel(p);
            var overworld=w.RegionsById["reach"];
            var arrival=overworld.FindPlacement(overworld.Center(overworld.Entrance)+new V(72,0),p,w.Actors.Values);
            p.X=arrival.X;p.Y=arrival.Y;app.RebuildViews();app.ResumeGame();
            await Capture(app,"overworld");
            foreach(string destination in new[]{"ruin","reach"})
            {
                Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.F10,Keycode=Key.F10,Pressed=true});
                Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.F10,Keycode=Key.F10,Pressed=false});
                Input.FlushBufferedEvents();await Frames(app,16);
                if(w.Player.Region!=destination)throw new InvalidOperationException("Developer region key failed: "+destination);
                await Capture(app,"diagnostic-"+destination);
            }
            for(int iteration=0;iteration<6;iteration++)
            {
                p.Region=iteration%2==0?r.Id:"reach";
                if(p.Region==r.Id){w.RegionsById[r.Id]=r;p.X=room.X;p.Y=room.Y+24;}
                else {p.X=w.RegionsById["reach"].Center(w.RegionsById["reach"].Entrance).X;p.Y=w.RegionsById["reach"].Center(w.RegionsById["reach"].Entrance).Y;}
                var timer=Stopwatch.StartNew();app.RebuildViews();double synchronous=timer.Elapsed.TotalMilliseconds;
                await Frames(app,12);double ready=timer.Elapsed.TotalMilliseconds;
                var parent=app.Views["player"].GetParent();
                var intervals=new List<double>();timer.Restart();double last=0;
                for(int f=0;f<60;f++){await Frames(app,1);double now=timer.Elapsed.TotalMilliseconds;intervals.Add(now-last);last=now;}
                intervals.Sort();
                observations.Add(new {iteration,region=p.Region,viewBuildMs=synchronous,readyIncluding12FramesMs=ready,
                    terrainBuildMs=TerrainView.LastBuildMilliseconds,wallBuildMs=TerrainWallRow.BuildMilliseconds,
                    terrainVertexBytes=TerrainView.LastVertexBytes,managedBytes=GC.GetTotalMemory(true),
                    godotStaticBytes=OS.GetStaticMemoryUsage(),
                    textureMemoryBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed),
                    bufferMemoryBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.BufferMemUsed),
                    actorLayerNodes=parent.GetChildCount(),wallNodes=parent.GetChildren().OfType<TerrainWallRow>().Count(),
                    decorationNodes=parent.GetChildren().OfType<EnvironmentDecoration>().Count(),
                    cachedModules=EnvironmentArtGenerator.CachedModuleCount,environmentTextures=EnvironmentAssetCache.TextureCount,environmentAtlasBytes=EnvironmentAssetCache.PixelBytes,
                    p50ms=intervals[30],p95ms=intervals[57],renderer=RenderingServer.GetVideoAdapterName()});
            }
            Directory.CreateDirectory("build/dungeon");
            File.WriteAllText("build/dungeon/environment-"+(app.Ui.MobileLayout?"mobile":"desktop")+".json",JsonSerializer.Serialize(observations,Content.Json));
            GD.Print("ENVIRONMENT VISUAL CHECKS PASSED: references and six region rebuilds");
        }
        finally {Engine.TimeScale=scale;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
