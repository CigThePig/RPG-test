using Godot;
using System.Diagnostics;
using System.Text.Json;
namespace Mosslight;

/// <summary>Production renders and real-time streaming trace. Desktop/software results
/// are comparative evidence only, never a substitute for the target Android device.</summary>
public static class LivingReachVisualChecks
{
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string saved=Persistence.Capture(w,app.Narrative);
        bool videoOnly=OS.GetCmdlineUserArgs().Contains("--living-reach-video");
        string tag=videoOnly?"video":OS.GetCmdlineUserArgs().Contains("--stream-baseline")?"before":"after";
        string folder="build/living-reach/"+tag;Directory.CreateDirectory(folder);
        var window=app.GetWindow();var size=window.Size;var zoom=app.Camera.Zoom;
        var samples=new List<double>();var stages=new List<object>();var trace=new List<object>();int missingVisibleFrames=0;
        async System.Threading.Tasks.Task Frame(int count=1)
        {
            for(int i=0;i<count;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
        }
        async System.Threading.Tasks.Task Shot(string name)
        {
            await Frame(4);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=window.GetTexture().GetImage();image.SavePng(folder+"/"+name+".png");
        }
        async System.Threading.Tasks.Task Settle()
        {
            // A wide overview may require every chunk. Keep pumping real frames until
            // the production streamer reports completion; do not build a mock overview.
            await Frame(3);
            for(int i=0;i<2400;i++)
            {
                var stream=app.GetTree().GetNodesInGroup("overworld-chunks").OfType<OverworldChunkStreamer>().SingleOrDefault();
                if(stream==null || stream.CoversVisible){await Frame(5);return;}
                await Frame();
            }
            throw new InvalidOperationException("Streaming did not settle");
        }
        void Position(float x,float y){w.Player.X=x*24;w.Player.Y=y*24;app.Views["player"].SnapPosition();}
        try
        {
            app.FreezeSimulationForVerification=true;app.Ui.ClosePanels();window.Size=new(2340,1080);window.Position=Vector2I.Zero;
            // Remove unrelated actors from this isolated rendering fixture only.
            foreach(string id in w.Actors.Keys.Where(id=>id!="player").ToArray())w.Actors.Remove(id);
            w.Projectiles.Clear();w.Hazards.Clear();
            foreach(int seed in new[]{1,42,1337})
            {
                if(videoOnly&&seed!=42)continue;
                var r=Regions.Overworld(seed,320,240);w.RegionsById["reach"]=r;w.Player.Region="reach";
                Position(160,120);app.Camera.Zoom=Vector2.One*.19f;app.RebuildViews();await Settle();
                app.Ui.Visible=false;await Shot("seed-"+seed+"-world");app.Ui.Visible=true;
                if(seed!=42)continue;
                app.Camera.Zoom=zoom;
                float join=LivingReachGeography.RiverX(seed,132,320,240),branchT=(243-join)/(320-join);
                foreach(var (name,x,y) in new[]{("river-bend",LivingReachGeography.RiverX(seed,110,320,240),110f),("lake",LivingReachGeography.RiverX(seed,70,320,240),70f),("pond",80f,53f),("confluence",LivingReachGeography.RiverX(seed,132,320,240),132f),("river-crossing",LivingReachGeography.RiverX(seed,141,320,240),141f),("tributary-crossing",243f,132-64.8f*branchT+3*MathF.Sin(branchT*MathF.Tau)),("town",39f,122f)})
                {Position(x,y);await Settle();await Shot(name);}
                app.Camera.Zoom=Vector2.One*.65f;Position(39,123);await Settle();await Shot("town-overview");app.Camera.Zoom=zoom;
                foreach(var h in r.Town!.Houses)
                {
                    Position(h.X+h.Door+.5f,h.Y+h.Height+1.5f);await Settle();await Shot("outside-"+h.Style);
                    w.Move(w.Player,new System.Numerics.Vector2(0,-3*24));app.Views["player"].SnapPosition();await Frame(20);app.Ui.Visible=false;await Shot("inside-"+h.Style);app.Ui.Visible=true;
                    w.Move(w.Player,new System.Numerics.Vector2(0,3*24));app.Views["player"].SnapPosition();await Frame(20);
                }
                Position(85,120);await Settle();
                var timer=Stopwatch.StartNew();double previous=timer.Elapsed.TotalMilliseconds;
                // Sustained horizontal, diagonal, reverse and repeated boundary travel.
                for(int frame=0;frame<900;frame++)
                {
                    float t=frame<450?frame:900-frame;
                    var destination=new System.Numerics.Vector2((85+t*.16f)*24,(120+MathF.Sin(t/100f)*24)*24);
                    var offset=destination-w.Player.Position;
                    w.PlayerMove(offset.Length()>1?System.Numerics.Vector2.Normalize(offset):System.Numerics.Vector2.Zero,1f/60);w.Step(1f/60);
                    app.Views["player"].SnapPosition();
                    await Frame();double now=timer.Elapsed.TotalMilliseconds;double frameMs=now-previous;samples.Add(frameMs);previous=now;
                    var current=app.GetTree().GetNodesInGroup("overworld-chunks").OfType<OverworldChunkStreamer>().Single();
                    if(!current.CoversVisible)missingVisibleFrames++;
                    trace.Add(new {frame,ms=frameMs,streamMs=current.LastMainThreadMs,current.LoadedChunks,current.CachedChunks,current.LookupBytes,cpuManaged=GC.GetTotalMemory(false),textureBytes=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed)});
                    if(frame%30==0)await Shot("travel-"+(frame/30).ToString("D3"));
                    // Screenshot readback is excluded from the next measured frame.
                    previous=timer.Elapsed.TotalMilliseconds;
                }
                var streamer=app.GetTree().GetNodesInGroup("overworld-chunks").OfType<OverworldChunkStreamer>().Single();
                File.WriteAllText(folder+"/streaming.json",JsonSerializer.Serialize(new {streamer.CacheHits,streamer.CacheMisses,streamer.LoadedChunks,streamer.CachedChunks,streamer.PreparedChunks,streamer.PendingDisposals,streamer.LookupBytes,streamer.WorstMainThreadMs,streamer.Measurements},new JsonSerializerOptions{WriteIndented=true}));
                if(tag=="after" && (streamer.CachedChunks>OverworldChunkStreamer.MaximumCachedChunks || streamer.LookupBytes>32_000_000))
                    throw new InvalidOperationException("Unbounded scenery cache or lookup retention");
                stages.Add(new {groundMs=OverworldGroundRenderer.LastBuildMilliseconds,waterMs=OverworldWaterRenderer.LastBuildMilliseconds,rockMs=OverworldRockRenderer.LastBuildMilliseconds});
            }
            // Fast relocation, distant save/load, reduced motion and region teardown
            // happen after the timed route, keeping the comparison uncontaminated.
            Position(230,190);await Settle();
            var distantRegion=w.RegionsById["reach"];var terrain=(Terrain[])distantRegion.Tiles.Clone();
            var houses=distantRegion.Town!.Houses.ToArray();
            distantRegion.ExploredTiles.Add(distantRegion.Index(w.Player.Position));
            w.Player.Health=87;string distant=Persistence.Capture(w,app.Narrative);
            Persistence.Restore(w,app.Narrative,distant);app.RebuildViews();await Settle();
            if(w.Player.Health!=87 || !w.RegionsById["reach"].Tiles.SequenceEqual(terrain) || !w.RegionsById["reach"].Town!.Houses.SequenceEqual(houses))
                throw new InvalidOperationException("Distant save/load changed authoritative world");
            await Shot("distant-save-load");
            bool reduced=app.Ui.Preferences.ReducedMotion;app.Ui.Preferences.ReducedMotion=true;
            await Frame(10);await Shot("reduced-motion");app.Ui.Preferences.ReducedMotion=reduced;
            w.Enter(w.Player,"ruin");app.RebuildViews();await Frame(12);await Shot("dungeon-transition");
            if(app.GetTree().GetNodesInGroup("overworld-chunks").Count!=0)throw new InvalidOperationException("Overworld streamer survived dungeon transition");
            w.Enter(w.Player,"reach");app.RebuildViews();await Settle();await Shot("return-from-dungeon");
            if(!w.RegionsById["reach"].Tiles.SequenceEqual(terrain))throw new InvalidOperationException("Streaming teardown mutated terrain");
            File.WriteAllText(folder+"/frames.json",JsonSerializer.Serialize(trace));
            if(tag=="after"&&missingVisibleFrames>0)throw new InvalidOperationException("Visible scenery missing during travel: "+missingVisibleFrames);
            samples.Sort();double At(float p)=>samples[Math.Min(samples.Count-1,(int)(samples.Count*p))];
            File.WriteAllText(folder+"/performance.json",JsonSerializer.Serialize(new{environment="Godot 4.6.1 Linux, software GL; not Galaxy S25",missingVisibleFrames,p50=At(.5f),p95=At(.95f),p99=At(.99f),worst=samples.Last(),stages},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("LIVING REACH RENDERED CHECKS PASSED "+tag);
        }
        finally{window.Size=size;app.Camera.Zoom=zoom;app.FreezeSimulationForVerification=false;Persistence.Restore(w,app.Narrative,saved);app.RebuildViews();}
    }
}
