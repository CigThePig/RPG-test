using Godot;
using PixelCreatures.Runtime;
using System.Text.Json;
namespace Mosslight;

/// <summary>Geometry coverage is separate from real Window/renderer coverage. No synthetic
/// safe rectangle is accepted as proof of an expanded application surface.</summary>
public static class WidescreenChecks
{
    public static readonly Vector2I[] Sizes = { new(1280,800),new(1280,720),new(1920,864),new(2340,1080),new(1024,768),new(900,560) };
    public static void Geometry(Action<bool,string> check)
    {
        foreach(var size in Sizes)
        foreach(var inset in new[]{new Rect2(0,0,size.X,size.Y),new Rect2(63,19,size.X-88,size.Y-50)})
        {
            var region=DisplayLayout.Gameplay(inset);
            check(inset.Encloses(region),$"{size}: gameplay region inside asymmetric safe area");
            check(Math.Abs(region.GetCenter().X-inset.GetCenter().X)<.01,$"{size}: safe centring");
            check(region.Size.X<=region.Size.Y*1.6f+.01,$"{size}: reference aspect maximum");
        }
        var baseline=MobileCombatControls.Geometry(new Rect2(0,0,1280,800));
        var wide=MobileCombatControls.Geometry(new Rect2(0,0,2340f/1080*800,800));
        float offset=(2340f/1080*800-1280)/2;
        foreach(var(id,rect) in baseline)
        {
            // Common physical height 1080: old pillarboxed origin and new centred origin.
            var oldPhysical=(rect.Position+new Vector2(offset,0))*1.35f;
            check(oldPhysical.DistanceTo(wide[id].Position*1.35f)<1,$"{id}: physical position invariant within 1 pixel at 1080 height");
            check(rect.Size.IsEqualApprox(wide[id].Size),$"{id}: unchanged touch size");
        }
        foreach(var zoom in new[]{1f,1.25f,1.5f,2f,2.75f})
        foreach(var bounds in new[]{new Vector2(2400,1200),new Vector2(96,48)})
        foreach(var size in Sizes)
        foreach(var focus in new[]{Vector2.Zero,bounds,new Vector2(0,bounds.Y),new Vector2(bounds.X,0),bounds/2})
        {
            var half=(Vector2)size/zoom/2;
            var centre=DisplayLayout.CameraCenter(focus,bounds,half);
            check(bounds.X<half.X*2 ? centre.X==bounds.X/2 : centre.X-half.X>=-.01 && centre.X+half.X<=bounds.X+.01,"camera horizontal clamp or deliberate small-map centre");
            check(bounds.Y<half.Y*2 ? centre.Y==bounds.Y/2 : centre.Y-half.Y>=-.01 && centre.Y+half.Y<=bounds.Y+.01,"camera vertical clamp or deliberate small-map centre");
        }
    }

    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        if(!app.Ui.MobileLayout)return;
        var ui=app.Ui;var window=app.GetWindow();var oldSize=window.Size;
        var oldPosition=window.Position;var oldAspect=window.ContentScaleAspect;var preferences=ui.Preferences.Copy();
        string save=Persistence.Capture(app.World,app.Narrative);
        var rows=new List<object>();
        var performance=new Dictionary<string,object>();
        async System.Threading.Tasks.Task Frames(int n=8) { for(int i=0;i<n;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame); }
        void Require(bool ok,string why) { if(!ok)throw new InvalidOperationException("Widescreen: "+why); }
        Dictionary<string,Rect2> Controls() => ui.CombatControls!.Actions.ToDictionary(p=>p.Key,p=>p.Value.GetGlobalRect())
            .Concat(new[]{new KeyValuePair<string,Rect2>("stick",ui.Stick.GetGlobalRect()),new KeyValuePair<string,Rect2>("hud",ui.StatusRect)}).ToDictionary(p=>p.Key,p=>p.Value);
        object Rect(Rect2 r) => new {x=r.Position.X,y=r.Position.Y,width=r.Size.X,height=r.Size.Y};
        Rect2 Physical(Rect2 r) { var t=window.GetScreenTransform();return new Rect2(t*r.Position,(t*r.End)-(t*r.Position)); }
        async System.Threading.Tasks.Task Shot(string name)
        {
            await Frames(); await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Directory.CreateDirectory("docs/native-screenshots");
            // Viewport textures omit Keep-mode bars. Capture the actual native window
            // from X11 instead, so both images include the full 2340x1080 surface.
            var screen=DisplayServer.ScreenGetImage(window.CurrentScreen) ?? throw new InvalidOperationException("Native screenshot unavailable");
            screen.GetRegion(new Rect2I(window.Position,window.Size)).SavePng("docs/native-screenshots/widescreen-"+name+".png");
        }
        async System.Threading.Tasks.Task Measure(string name)
        {
            await Frames(20);
            var timer=System.Diagnostics.Stopwatch.StartNew();var samples=new List<double>();
            int layouts=ui.LayoutPasses;long terrainBuilds=TerrainView.BuildCount;
            double previous=timer.Elapsed.TotalMilliseconds;
            for(int i=0;i<60;i++)
            {
                await Frames(1);double now=timer.Elapsed.TotalMilliseconds;samples.Add(now-previous);previous=now;
            }
            samples.Sort();
            Require(ui.LayoutPasses==layouts && TerrainView.BuildCount==terrainBuilds,"steady rendering does not relayout UI or rebuild terrain");
            performance[name]=new {layoutPasses=ui.LayoutPasses-layouts,terrainRebuilds=TerrainView.BuildCount-terrainBuilds,medianFrameMs=samples[30],p95FrameMs=samples[57],drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),objects=Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),staticBytes=Performance.GetMonitor(Performance.Monitor.MemoryStatic),textureBytes=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed),terrainBuildMs=TerrainView.LastBuildMilliseconds,terrainVertexBytes=TerrainView.LastVertexBytes};
        }
        async System.Threading.Tasks.Task WorldEdges()
        {
            var region=app.World.RegionsById[app.World.Player.Region];var p=app.World.Player;
            var position=p.Position;var zoom=app.Camera.Zoom;
            var bounds=GodotConvert.GroundToScreen(region.Width*24,region.Height*24);
            foreach(float z in new[]{1f,1.25f,1.5f,2f,2.75f})
            foreach(var at in new[]{Vector2.Zero,bounds,new Vector2(0,bounds.Y),new Vector2(bounds.X,0),bounds/2})
            {
                p.X=at.X;p.Y=at.Y*2;app.Camera.Zoom=Vector2.One*z;
                await Frames(2);app.Camera.ResetSmoothing();app.CombatCamera.Impact(3.5f,17);await Frames(3);
                var centre=app.Camera.GetScreenCenterPosition();var half=window.GetVisibleRect().Size/app.Camera.Zoom/2;
                Require(bounds.X<half.X*2 ? Math.Abs(centre.X-bounds.X/2)<1 : centre.X-half.X>=-1 && centre.X+half.X<=bounds.X+1,"rendered camera horizontal boundary including shake");
                Require(bounds.Y<half.Y*2 ? Math.Abs(centre.Y-bounds.Y/2)<1 : centre.Y-half.Y>=-1 && centre.Y+half.Y<=bounds.Y+1,"rendered camera vertical boundary including shake");
            }
            p.X=position.X;p.Y=position.Y;app.Camera.Zoom=zoom;app.CombatCamera.Reset();await Frames();app.Camera.ResetSmoothing();
        }
        async System.Threading.Tasks.Task AssertTerrainInSideStrips()
        {
            await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var image=window.GetTexture().GetImage();
            Require(image.GetWidth()==window.Size.X && image.GetHeight()==window.Size.Y,"rendered texture matches actual surface");
            // Known Reach geometry reaches both sides at this spawn. Test palette-independent
            // coverage: opaque scenery must replace both undefined output and bedrock backing.
            foreach(int x in new[]{4,image.GetWidth()-5})
            {
                int scenery=0,total=0;
                for(int y=100;y<image.GetHeight()-100;y+=11)
                {
                    var color=image.GetPixel(x,y);total++;
                    if(color.A>.99f && color.ToHtml(false) is not "000000" and not "171e20" and not "20292b")scenery++;
                }
                Require(scenery>total*.95,"known Reach terrain fills outer side strip");
            }
        }
        try
        {
            ui.PauseForVerification();ui.ClosePanels(); app.World.Player.Health=app.World.Player.MaxHealth;
            app.FreezeSimulationForVerification=true;await Frames();
            string stationary=Persistence.Capture(app.World,app.Narrative);
            // ClosePanels resumes the UI; pause simulation independently so equal scenes and
            // serialization can be compared while rendering, UI and camera continue to update.
            window.ContentScaleAspect=Window.ContentScaleAspectEnum.Keep;
            window.Size=new(2340,1080);window.Position=Vector2I.Zero; await Frames();ui.Layout();await Frames();
            // The old Keep path gives us the exact same-height, pillarboxed physical baseline.
            Require(window.GetVisibleRect().Size.IsEqualApprox(new Vector2(1280,800)),"old Keep baseline is actually 1280x800");
            // Warm both render target sizes before comparing allocations/frame pacing.
            window.ContentScaleAspect=Window.ContentScaleAspectEnum.Expand;await Frames(20);
            window.ContentScaleAspect=Window.ContentScaleAspectEnum.Keep;await Frames(20);ui.Layout();
            var oldControls=Controls().ToDictionary(p=>p.Key,p=>Physical(p.Value));
            var oldScale=window.GetScreenTransform().X.Length();
            await Shot("before-reach");await Measure("keep-2340x1080");
            window.ContentScaleAspect=Window.ContentScaleAspectEnum.Expand;await Frames();ui.Layout();await Measure("expand-2340x1080");
            foreach(var size in Sizes)
            {
                window.Size=size;await Frames();ui.Layout();await Frames();
                var logical=window.GetVisibleRect();var t=window.GetScreenTransform();
                var expectedScale=Math.Min(size.X/1280f,size.Y/800f);
                Require(logical.Size.DistanceTo((Vector2)size/expectedScale)<2,"actual expanded viewport "+size+" got "+logical.Size);
                Require(Math.Abs(t.X.Length()-t.Y.Length())<.001,"uniform canvas scale");
                Require((t*logical.Position).Length()<2 && (t*logical.End).DistanceTo(size)<2,"logical rendering covers native surface without bars");
                Require(Persistence.Capture(app.World,app.Narrative)==stationary,"resize leaves serialized world unchanged");
                var controls=Controls();
                foreach(var(id,r) in controls)Require(logical.Encloses(r),id+" actual bounds inside viewport");
                if(size==new Vector2I(2340,1080))
                {
                    Require(Math.Abs(t.X.Length()-oldScale)<.002,"vertical world and sprite scale unchanged");
                    foreach(var(id,r) in controls)
                    {
                        var physical=Physical(r);var before=oldControls[id];
                        Require(physical.GetCenter().DistanceTo(before.GetCenter())<=2,id+" physical centre migrated");
                        Require(physical.Size.DistanceTo(before.Size)<=2,id+" physical size changed");
                    }
                    await Shot("after-reach");await AssertTerrainInSideStrips();await WorldEdges();
                    // Actual native input path, with the expanded transform active.
                    app.FreezeSimulationForVerification=false;
                    await MobileCombatControlsChecks.InputPipeline(app);
                    app.FreezeSimulationForVerification=true;
                    Persistence.Restore(app.World,app.Narrative,stationary);app.RebuildViews();ui.ClosePanels();await Frames();
                }
                rows.Add(new {requested=size.ToString(),drawable=window.Size.ToString(),logical=Rect(logical),scale=t.X.Length(),gameplay=Rect(ui.GameplayRegion),zoom=app.Camera.Zoom.ToString(),worldSpan=(logical.Size/app.Camera.Zoom).ToString(),controls=controls.ToDictionary(p=>p.Key,p=>Rect(Physical(p.Value)))});
                // Real surfaces and asymmetric cutouts. Every screen must remain readable and
                // control placement must survive opening/closing it, including enlarged text.
                foreach(float text in new[]{1f,1.3f,1.4f})
                {
                    ui.VerificationSafeArea=new Rect2(44,16,logical.Size.X-68,logical.Size.Y-40);
                    ui.ApplyPreferences(new UiPreferences {HighContrast=text>1},false);ui.SetTextScale(text);
                    var positions=Controls();
                    foreach(var open in new Action[]{ui.Inventory,ui.Character,ui.Journal,ui.Map,ui.Settings,ui.SaveLoad})
                    {
                        ui.ClosePanels();open();await Frames(3);
                        Require(ui.OverlaySafe.Encloses(ui.ScreenRect),"screen inside safe area at "+size+" text "+text);
                        ui.ClosePanels();ui.Layout();
                        foreach(var(id,r) in Controls())Require(r.IsEqualApprox(positions[id]),"menu changed gameplay placement: "+id);
                    }
                    ui.PackTab=1;ui.Inventory();await Frames(3);ui.PackTab=2;ui.Inventory();await Frames(3);ui.ClosePanels();ui.PackTab=0;
                    ui.PauseForVerification();ui.Confirm("Widescreen verification","Keep",()=>{});await Frames(3);ui.Back();ui.ClosePanels();
                }
                ui.VerificationSafeArea=null;ui.SetTextScale(1);
            }
            window.Size=new(2340,1080);await Frames();ui.Layout();
            ui.ApplyPreferences(preferences,false);ui.SetTextScale(1);ui.ClosePanels();
            ui.Inventory();await Shot("pack");ui.ClosePanels();
            app.Narrative.Call("talk");await Shot("dialogue");
            Require(ui.OverlaySafe.Encloses(ui.DialogueRect),"widescreen dialogue safe");
            app.Narrative.Call("import_state",app.Narrative.Call("export_state"));ui.ClosePanels();
            app.World.Enter(app.World.Player,"ruin");await Frames(12);ui.ClosePanels();await Shot("dungeon");
            await WorldEdges();
            // Same production generation, serialization and 120 fixed simulation inputs across
            // the viewport matrix, for each canonical seed. Presentation cannot affect results.
            foreach(int seed in new[]{1,42,1337})
            {
                Persistence.Restore(app.World,app.Narrative,stationary);
                app.World.Seed=seed;var reach=Regions.Overworld(seed);app.World.RegionsById["reach"]=reach;
                var p=app.World.Player;var spawn=reach.Center(reach.Entrance);p.Region="reach";p.X=spawn.X;p.Y=spawn.Y;
                string initial=Persistence.Capture(app.World,app.Narrative);string? expected=null;
                foreach(var size in Sizes)
                {
                    Persistence.Restore(app.World,app.Narrative,initial);app.RebuildViews();window.Size=size;ui.ClosePanels();await Frames();
                    Require(Persistence.Capture(app.World,app.Narrative)==initial,"seed "+seed+" resize/save round trip");
                    for(int tick=0;tick<120;tick++)
                    {
                        app.World.PlayerMove(new System.Numerics.Vector2(tick<60 ? 1 : -1,0),1f/60);
                        app.World.Step(1f/60);
                    }
                    string result=Persistence.Capture(app.World,app.Narrative);
                    expected ??=result;Require(expected==result,"seed "+seed+" authoritative simulation diverged with viewport "+size);
                }
            }
            Directory.CreateDirectory("build/widescreen");
            File.WriteAllText("build/widescreen/measurements.json",JsonSerializer.Serialize(new {performance,baseline=oldControls.ToDictionary(p=>p.Key,p=>Rect(p.Value)),surfaces=rows},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally
        {
            app.FreezeSimulationForVerification=false;
            ui.VerificationSafeArea=null;window.ContentScaleAspect=oldAspect;window.Size=oldSize;window.Position=oldPosition;
            Persistence.Restore(app.World,app.Narrative,save);app.RebuildViews();ui.ApplyPreferences(preferences,false);ui.PauseForVerification();await Frames();
        }
    }
}

/// <summary>UI fixtures use canvas coordinates. Native screen coordinates belong to the OS
/// smoke tests. Convert fixture points to native window coordinates once so ParseInputEvent
/// still exercises project touch/mouse emulation before Godot converts them back.</summary>
public static class RenderedInput
{
    public static void InjectLocal(this Viewport viewport, InputEvent e) => Input.ParseInputEvent(e.XformedBy(viewport.GetScreenTransform()));
}
