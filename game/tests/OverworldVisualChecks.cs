using Godot;
using System.Text.Json;
using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Reversible production-renderer composition. Never loads or writes user slots.</summary>
public static class OverworldVisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app,int n=12)
    { for(int i=0;i<n;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame); }
    static async System.Threading.Tasks.Task Capture(Foundation app,string name)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=app.GetViewport().GetTexture().GetImage();
        if(image.GetWidth()<800 || image.IsEmpty())throw new InvalidOperationException("Missing overworld raster");
        image.SavePng("docs/overworld-screenshots/"+name+".png");
    }
    public static RegionState Fixture()
    {
        var r=new RegionState {Id="living-reach-reference",Seed=42,Width=64,Height=64,Tiles=Enumerable.Repeat(Terrain.Grass,4096).ToArray(),Entrance=34*64+32};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float dx=(x-42)/10f,dy=(y-28)/9f;
            if(dx*dx+dy*dy<1+.12f*MathF.Sin(y*1.2f)+.1f*MathF.Cos(x*.8f))r.Tiles[y*64+x]=Terrain.Water;
            if(x is >=19 and <=24 && y is >=27 and <=29 || x==24 && y is >=29 and <=34)r.Tiles[y*64+x]=Terrain.Water;
            if(y is >=36 and <=38)r.Tiles[y*64+x]=Terrain.Earth;
        }
        r.Tiles[28*64+42]=Terrain.Grass; // island
        r.Tiles[34*64+40]=Terrain.Rock; // Raised PPC rock over the northern water margin.
        foreach(var (x,y,kind) in new[]{(29,34,"chest"),(25,24,"wood"),(29,23,"wood"),(46,40,"wood"),(27,30,"ore"),(35,40,"herb")})r.Features[y*64+x]=kind;
        return r;
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        Directory.CreateDirectory("docs/overworld-screenshots");Directory.CreateDirectory("build/overworld");
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);var prefs=app.Ui.Preferences.Copy();
        double scale=Engine.TimeScale;var window=app.GetWindow();var size=window.Size;
        bool part2=OS.GetCmdlineUserArgs().Contains("--part2-reference");
        bool previousGround=OverworldGroundRenderer.VerificationPrevious;
        string version=OS.GetCmdlineUserArgs().Contains("--baseline-water")?"baseline":OS.GetCmdlineUserArgs().Contains("--water-static-fallback")?"fallback":"final";
        if(part2)version=OverworldGroundRenderer.Enabled?"part2-after":"part2-before";
        bool compare=OS.GetCmdlineUserArgs().Contains("--compare-water");
        bool compareGround=OS.GetCmdlineUserArgs().Contains("--compare-ground");
        bool originalLegacy=OverworldWaterRenderer.VerificationLegacy;
        try
        {
            Engine.TimeScale=0;app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var r=Fixture();w.RegionsById[r.Id]=r;var p=w.Player;p.Region=r.Id;p.X=32.5f*24;p.Y=34.5f*24;p.Health=p.MaxHealth;p.Invulnerable=10000;w.Cancel(p);
            int i=0;foreach(var a in w.Actors.Values.Where(a=>a.Id!="player").OrderBy(a=>a.Id,StringComparer.Ordinal).Take(3))
            {a.Region=r.Id;var stand=r.FindPlacement(new V((30+i*2)*24,(32-i)*24),a,w.Actors.Values);a.X=stand.X;a.Y=stand.Y;a.Health=a.MaxHealth;w.Cancel(a);i++;}
            window.Size=new Vector2I(1280,800);app.RebuildViews();await Capture(app,version+"-reference");
            await Capture(app,version+"-multiple-creatures");
            if(version=="final" && !OS.GetCmdlineUserArgs().Contains("--water-static-fallback"))
            {
                var water=app.GetTree().GetNodesInGroup("overworld-water").OfType<OverworldWaterRenderer>().Single();
                try
                {
                    water.ZIndex=0;await Frames(app,2);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var oldOrder=app.GetViewport().GetTexture().GetImage();
                    water.ZIndex=-1;await Frames(app,2);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var correct=app.GetViewport().GetTexture().GetImage();
                    var transform=water.GetGlobalTransformWithCanvas();
                    var lo=transform*new Vector2(40*24,34*12-7);var hi=transform*new Vector2(41*24,34*12);int restored=0;
                    for(int y=(int)MathF.Ceiling(lo.Y);y<(int)hi.Y;y++)for(int x=(int)MathF.Ceiling(lo.X);x<(int)hi.X;x++)
                        if(oldOrder.GetPixel(x,y)!=correct.GetPixel(x,y))restored++;
                    if(restored<10)throw new InvalidOperationException("Raised shore rock was not restored over water: "+restored);
                    oldOrder.SavePng("docs/overworld-screenshots/final-rock-old-order-reproduction.png");
                    correct.SavePng("docs/overworld-screenshots/final-rock-correct-order.png");
                    File.WriteAllText("build/overworld/rock-depth.json",JsonSerializer.Serialize(new {restoredRockPixels=restored},Content.Json));
                }
                finally {water.ZIndex=-1;}
            }
            if(version=="final" && !OS.GetCmdlineUserArgs().Contains("--water-static-fallback"))
            {
                async System.Threading.Tasks.Task<int> Changes(bool reducedMotion)
                {
                    var setting=prefs.Copy();setting.ReducedMotion=reducedMotion;app.Ui.ApplyPreferences(setting,false);await Frames(app);
                    foreach(var water in app.GetTree().GetNodesInGroup("overworld-water").OfType<OverworldWaterRenderer>())water.SetAnimationPreviewTime(0);
                    await Frames(app,2);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var first=app.GetViewport().GetTexture().GetImage();
                    foreach(var water in app.GetTree().GetNodesInGroup("overworld-water").OfType<OverworldWaterRenderer>())water.SetAnimationPreviewTime(4);
                    await Frames(app,2);
                    await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var second=app.GetViewport().GetTexture().GetImage();int changed=0;
                    for(int y=170;y<390;y++)for(int x=800;x<1250;x++)if(first.GetPixel(x,y)!=second.GetPixel(x,y))changed++;
                    return changed;
                }
                int animated=await Changes(false),still=await Changes(true);
                if(animated==0 || still!=0)throw new InvalidOperationException($"Water animation/static pixels: {animated}/{still}");
                File.WriteAllText("build/overworld/motion.json",JsonSerializer.Serialize(new {animatedPixels=animated,reducedMotionPixels=still},Content.Json));
                app.Ui.ApplyPreferences(prefs,false);
                foreach(var water in app.GetTree().GetNodesInGroup("overworld-water").OfType<OverworldWaterRenderer>())water.SetAnimationPreviewTime(-1);
            }
            p.X=31*24;p.Y=27*24;app.RebuildViews();await Capture(app,version+"-shoreline");
            p.X=32.5f*24;p.Y=34.5f*24;app.RebuildViews();
            foreach(var resolution in WidescreenChecks.Sizes)
            {window.Size=resolution;await Capture(app,version+"-"+resolution.X+"x"+resolution.Y);}
            var reduced=prefs.Copy();reduced.ReducedMotion=true;app.Ui.ApplyPreferences(reduced,false);
            await Capture(app,version+"-reduced-motion");app.Ui.ApplyPreferences(prefs,false);
            window.Size=new Vector2I(1280,800);
            string frozen=Persistence.Capture(w,app.Narrative);
            for(int iteration=0;iteration<((compare||compareGround)?12:6);iteration++)
            {
                OverworldWaterRenderer.VerificationLegacy=compare && iteration%2==0;
                OverworldGroundRenderer.VerificationPrevious=compareGround && iteration%2==0;
                app.RebuildViews();await Frames(app);
                long builds=OverworldWaterRenderer.BuildCount,groundBuilds=OverworldGroundRenderer.BuildCount;
                await Frames(app,120);
                if(OverworldGroundRenderer.BuildCount!=groundBuilds)throw new InvalidOperationException("Ground rebuilt during static interval");
                if(OverworldWaterRenderer.BuildCount!=builds)throw new InvalidOperationException("Water rebuilt during static interval");
                if(Persistence.Capture(w,app.Narrative)!=frozen)throw new InvalidOperationException("Water renderer changed authoritative state");
            }
            OverworldWaterRenderer.VerificationLegacy=false;OverworldGroundRenderer.VerificationPrevious=previousGround;
            foreach(int seed in new[]{1,42,1337})
            {
                r=Regions.Overworld(seed);r.Id="living-reach-seed";w.RegionsById[r.Id]=r;p.Region=r.Id;
                int at=Enumerable.Range(0,r.Tiles.Length).Where(n=>r.Tiles[n]==Terrain.Grass && r.Neighbours(n).Any(k=>r.Tiles[k]==Terrain.Water)).OrderBy(n=>Math.Abs(n%r.Width-32)+Math.Abs(n/r.Width-24)).First();
                var point=r.FindPlacement(r.Center(at),p,w.Actors.Values);p.X=point.X;p.Y=point.Y;app.RebuildViews();await Capture(app,version+"-exploration-seed"+seed);
            }
            r=Fixture();r.Id="living-reach-edge-cases";Array.Fill(r.Tiles,Terrain.Grass);r.Features.Clear();
            string[] shapes={"#","###","##/.#",".#./###/.#.","#.#/.#./#.#","####/#..#/####","#../#../###","###/..#/###"};
            for(int s=0;s<shapes.Length;s++){var rows=shapes[s].Split('/');for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)if(rows[y][x]=='#')r.Tiles[(25+s/4*7+y)*64+20+s%4*7+x]=Terrain.Water;}
            w.RegionsById[r.Id]=r;p.Region=r.Id;p.X=32*24;p.Y=32*24;app.RebuildViews();await Capture(app,version+"-edge-cases");
            // Explicit water at the world boundary, including clipped bank vegetation.
            for(int y=0;y<=18;y++)for(int x=56;x<64;x++)r.Tiles[y*64+x]=Terrain.Water;
            p.X=53.5f*24;p.Y=20.5f*24;app.RebuildViews();await Capture(app,version+"-region-boundary");
            if(part2)
            {
                r=Fixture();r.Id="living-reach-ground-transitions";Array.Fill(r.Tiles,Terrain.Grass);r.Features.Clear();
                for(int y=22;y<40;y++)for(int x=17;x<48;x++)
                {
                    if(y>=34 || (x>=22&&x<=24) || (x>=35&&y<29) || Math.Abs(x+y-58)<2)r.Tiles[y*64+x]=Terrain.Earth;
                    if(x>=40&&y>=29&&y<=32)r.Tiles[y*64+x]=Terrain.Water;
                }
                w.RegionsById[r.Id]=r;p.Region=r.Id;p.X=32*24;p.Y=32*24;app.RebuildViews();await Capture(app,version+"-ground-transitions");
            }
            GD.Print("OVERWORLD VISUAL CHECKS PASSED: production references, six sizes, seeds, topology and rebuilds");
        }
        finally {OverworldGroundRenderer.VerificationPrevious=previousGround;OverworldWaterRenderer.VerificationLegacy=originalLegacy;Engine.TimeScale=scale;window.Size=size;Persistence.Restore(w,app.Narrative,original);app.Ui.ApplyPreferences(prefs,false);app.RebuildViews();app.ResumeGame();}
    }
}
