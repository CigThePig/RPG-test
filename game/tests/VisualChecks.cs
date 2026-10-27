using Godot;
using System.Diagnostics;
using System.Text.Json;

namespace Mosslight;
public static class VisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app, int count = 12)
    {
        for (int i = 0; i < count; i++)
            await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    static async System.Threading.Tasks.Task Capture(Foundation app, string name)
    {
        await Frames(app);
        await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = app.GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory("docs/native-screenshots");
        image.SavePng("docs/native-screenshots/" + (app.Ui.MobileLayout ? "mobile-" : "") + name + ".png");
    }

    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        await Frames(app, 30);
        await Capture(app, "menu");
        app.Ui.Menu();
        // Input flows through Godot's actual action mapping and physics process.
        float before = app.World.Player.X;
        Input.ActionPress("east");
        await Frames(app, 30);
        Input.ActionRelease("east");
        if (app.World.Player.X <= before)
            throw new InvalidOperationException("Visual movement input did not move player");
        await Capture(app, "exploration");
        var player=app.World.Player;
        var region=app.World.RegionsById[player.Region];
        int chest=region.Features.Single(x=>x.Value=="chest").Key;
        var chestPos=region.Center(chest);
        // Walk to the chest using the same screen-space input conversion as touch.
        for(int i=0;i<240 && System.Numerics.Vector2.Distance(player.Position,chestPos)>28;i++)
        {
            var d=chestPos-player.Position;
            app.TouchMovement=PixelCreatures.Runtime.GodotConvert.GroundToScreen(d.X,d.Y).Normalized();
            await Frames(app,1);
        }
        app.Ui.Stick.Cancel();
        await Capture(app,"chest");
        app.Interact();
        if(!region.Depleted.Contains(chest)) throw new InvalidOperationException("Visual chest walk/loot failed");
        await Capture(app,"opened-chest");
        app.CollisionDebug=true;await Capture(app,"collision-overlay");app.CollisionDebug=false;
        app.Ui.Inventory();
        await Capture(app, "inventory");
        app.Ui.ClosePanels();
        app.Ui.Character();
        await Capture(app, "character");
        app.Ui.ClosePanels();
        app.Narrative.Call("talk");
        await Capture(app, "dialogue");
        // Exercise the real Dialogue Manager choice mutation, not a simulated quest acceptance.
        var choices = app.Narrative.Get("choices").AsGodotObject() as VBoxContainer ?? throw new InvalidOperationException("Missing dialogue UI");
        if (choices.GetChildren().OfType<Button>().Any(x => x.Text.Contains("[if")))
            throw new InvalidOperationException("Dialogue conditions rendered as text");
        var locked = choices.GetChildren().OfType<Button>().FirstOrDefault(x => x.Text.Contains("buried"));
        if (locked != null && !locked.Disabled)
            throw new InvalidOperationException("Dialogue prerequisite option enabled");
        var response = choices.GetChildren().OfType<Button>().First(x => x.Text.Contains("prowlers"));
        response.EmitSignal(Button.SignalName.Pressed);
        await Frames(app);
        if (!app.Narrative.Get("flags").AsGodotDictionary().ContainsKey("accepted_1"))
            throw new InvalidOperationException("Dialogue choice failed to start quest");
        // Finish the current line through its actual Continue button.
        foreach (var button in choices.GetChildren().OfType<Button>().ToArray())
            button.EmitSignal(Button.SignalName.Pressed);
        await Frames(app);
        app.Ui.Journal();
        await Capture(app, "journal");
        app.Ui.ClosePanels();
        app.World.Enter(app.World.Player, "ruin");
        await Capture(app, "dungeon");
        app.World.Enter(app.World.Player, "reach");
        if (app.Ui.MobileLayout) await Capture(app, "controls");
        app.Ui.Workshop();
        await Capture(app, "workshop");
        app.Ui.ClosePanels();
        var frames = new List<double>();
        var timer = Stopwatch.StartNew();
        double last = timer.Elapsed.TotalMilliseconds;
        for (int i = 0; i < 180; i++)
        {
            await Frames(app, 1);
            double now = timer.Elapsed.TotalMilliseconds;
            frames.Add(now - last);
            last = now;
        }

        frames.Sort();
        var data = new
        {
            frames = frames.Count,
            p50ms = frames[frames.Count / 2],
            p95ms = frames[(int)(frames.Count * .95)],
            p99ms = frames[(int)(frames.Count * .99)],
            averageMs = frames.Average(),
            renderer = RenderingServer.GetVideoAdapterName(),
            actors = app.Views.Count,
            visibleActors = app.Views.Values.Count(v => v.Visible),
            totalRasterizations = app.Views.Values.Sum(v => v.Creature.RenderCount),
            managedBytes = GC.GetTotalMemory(false)
        };
        File.WriteAllText(app.Ui.MobileLayout ? "docs/render-verification-mobile.json" : "docs/render-verification.json", JsonSerializer.Serialize(data, Content.Json));
        GD.Print("VISUAL CHECKS PASSED");
    }
}
