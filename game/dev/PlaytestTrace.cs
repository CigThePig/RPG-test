using Godot;
using System.Diagnostics;
using System.Text.Json;
using PixelCreatures.Runtime;

namespace Mosslight;
/// <summary>Opt-in bounded local JSONL. No saves, network, or capture work when disabled.</summary>
public sealed class PlaytestTrace : IDisposable
{
    StreamWriter? writer;
    readonly Stopwatch clock = new();
    readonly Foundation app;
    long rows;
    public bool Active => writer != null;
    public string LastPath { get; private set; } = "";
    public string Scenario { get; set; } = "free-play";
    public PlaytestTrace(Foundation owner) { app = owner; }
    public void Start(string path)
    {
        Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        LastPath = path;
        writer = new StreamWriter(path, false, System.Text.Encoding.UTF8, 65536);
        rows = 0; clock.Restart();
        Write(new { kind="session", schema=1, utc=DateTime.UtcNow, engine=Engine.GetVersionInfo()["string"].AsString(), renderer=RenderingServer.GetVideoAdapterName(), platform=OS.GetName(), source=BuildIdentity.Source, units="ground px, projected canvas px, seconds, milliseconds", timebase="tick=simulation 60Hz; frame=Godot drawn frame; wallMs=monotonic session; movieSeconds=frame/60 only in Movie Maker", preset=app.Feel.Id, configuration=app.Feel });
    }
    public void Write(object row)
    {
        if (writer == null) return;
        if (++rows > 120000 || clock.Elapsed.TotalMinutes > 15) { Dispose(); return; }
        writer.WriteLine(JsonSerializer.Serialize(row));
        if (rows % 120 == 0) writer.Flush();
    }
    public void Marker(string label) { if(Active) Write(new { kind="marker", label, scenario=Scenario, tick=app.World.Tick, frame=Engine.GetFramesDrawn(), wallMs=clock.Elapsed.TotalMilliseconds }); }
    public void Input(InputEvent e)
    {
        if (!Active) return;
        if(e is InputEventScreenTouch t) Write(new {kind="touch", tick=app.World.Tick, frame=Engine.GetFramesDrawn(), wallMs=clock.Elapsed.TotalMilliseconds, finger=t.Index, x=t.Position.X, y=t.Position.Y, pressed=t.Pressed, canceled=t.Canceled});
        if(e is InputEventScreenDrag d) Write(new {kind="drag", tick=app.World.Tick, frame=Engine.GetFramesDrawn(), wallMs=clock.Elapsed.TotalMilliseconds, finger=d.Index, x=d.Position.X, y=d.Position.Y});
    }
    public void Sample(double dt)
    {
        if(!Active) return;
        var p=app.World.Player; var stick=app.Ui.Stick; var c=app.Camera;
        var screen=app.GetViewport().GetCanvasTransform()*GodotConvert.GroundToScreen(p.X,p.Y);
        Write(new { kind="sample", scenario=Scenario, preset=app.Feel.Id, tick=app.World.Tick, frame=Engine.GetFramesDrawn(), wallMs=clock.Elapsed.TotalMilliseconds,
            x=p.X, y=p.Y, vx=app.World.ActualVelocity.X, vy=app.World.ActualVelocity.Y, requestedX=app.World.RequestedVelocity.X, requestedY=app.World.RequestedVelocity.Y,
            screenInputX=app.ScreenInput.X, screenInputY=app.ScreenInput.Y, groundInputX=app.World.LastInput.X, groundInputY=app.World.LastInput.Y,
            finger=stick.Finger, anchorX=stick.Anchor.X, anchorY=stick.Anchor.Y, displacementX=stick.Displacement.X, displacementY=stick.Displacement.Y, analog=app.TouchMovement.Length(),
            speed=app.World.Stat(p,"speed"), radius=p.Radius, flies=p.Flies, swims=p.Swims, action=p.State.ToString(), blocked=app.World.LastBlockedReason, attempts=app.World.CollisionAttempts, slides=app.World.CollisionSlides,
            animation=app.Views[p.Id].Creature.State.ToString(), yaw=app.Views[p.Id].Creature.Motor?.Yaw,
            cameraX=c.GetScreenCenterPosition().X, cameraY=c.GetScreenCenterPosition().Y, cameraTargetX=c.Position.X, cameraTargetY=c.Position.Y, zoom=c.Zoom.X, playerScreenX=screen.X, playerScreenY=screen.Y,
            physicsMs=dt*1000, simulationMs=app.SimulationMs, droppedSeconds=app.DroppedSeconds, fps=Engine.GetFramesPerSecond(), rasterMs=CreatureRenderQueue.LastRasterMs, uploadMs=CreatureRenderQueue.LastUploadMs,
            managedBytes=app.World.Tick%60==0 ? GC.GetTotalMemory(false) : 0 });
    }
    public void Dispose() { writer?.Dispose(); writer=null; clock.Stop(); }
}
