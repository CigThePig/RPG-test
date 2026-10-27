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
        if (app.Ui.MobileLayout)
        {
            var scroller=app.Ui.FindChildren("*","ScrollContainer",true,false).OfType<ScrollContainer>().First(s=>s.IsVisibleInTree());
            var start=scroller.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=true,Position=start});
            await Frames(app,1);
            for(int i=1;i<=6;i++)
            {
                Input.ParseInputEvent(new InputEventScreenDrag {Index=9,Position=start-new Vector2(0,i*30),Relative=new Vector2(0,-30)});
                await Frames(app,1);
            }
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=false,Position=start-new Vector2(0,180)});
            await Frames(app,2);
            if(scroller.ScrollVertical<60 || !app.Ui.MenuOpen)throw new InvalidOperationException("Menu swipe over button rows did not scroll safely");
            scroller.ScrollVertical=0;
            await Frames(app,3);
            var enter=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Enter / Resume");
            var enterPoint=enter.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=true,Position=enterPoint});
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=false,Canceled=true,Position=enterPoint});
            await Frames(app,2);
            if(!app.Ui.MenuOpen)throw new InvalidOperationException("Canceled menu touch activated Enter");
        }
        app.Ui.Menu();
        await Frames(app,2);
        if(app.Ui.MobileLayout)
        {
            // Menu sits above the primary action cluster. Its upper edge must still
            // suppress the OS-synthesized mouse pair after the owned touch.
            var menuButton=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Menu" && b.IsVisibleInTree());
            var menuPoint=menuButton.GlobalPosition+new Vector2(menuButton.Size.X/2,5);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=menuPoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=true,Position=menuPoint,GlobalPosition=menuPoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=menuPoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=false,Position=menuPoint,GlobalPosition=menuPoint});
            await Frames(app,2);
            if(!app.Ui.MenuOpen)throw new InvalidOperationException("Menu edge touch plus emulated mouse did not open exactly once");
            app.Ui.Menu();await Frames(app,2);
        }
        if (app.Ui.MobileLayout)
        {
            // Exercise action ownership through the actual input pipeline while a
            // different finger owns the stick, including canceled and dragged touches.
            string snapshot=Persistence.Capture(app.World,app.Narrative);
            var mobilePlayer=app.World.Player;
            var dodge=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Dodge" && b.IsVisibleInTree());
            var attack=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Attack" && b.IsVisibleInTree());
            var dodgePoint=dodge.GetGlobalRect().GetCenter();
            var dodgeSize=dodge.Size;
            int starts=0;
            void OnCombat(string kind, System.Numerics.Vector2 direction, string context) { if(kind=="dodge_started") starts++; }
            app.World.CombatDiagnostic += OnCombat;
            var mobileStick=app.Ui.Stick;
            var stickPoint=mobileStick.GlobalPosition+mobileStick.Size/2;
            Input.ParseInputEvent(new InputEventScreenTouch {Index=7,Pressed=true,Position=stickPoint});
            Input.ParseInputEvent(new InputEventScreenDrag {Index=7,Position=stickPoint+new Vector2(72,0)});
            await Frames(app,2);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=dodgePoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Canceled=true,Position=dodgePoint});
            await Frames(app,2);
            if(starts!=0) throw new InvalidOperationException("Canceled Dodge touch activated");
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=dodgePoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenDrag {Index=8,Position=dodgePoint+new Vector2(-180,-180)});
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=dodgePoint+new Vector2(-180,-180)});
            await Frames(app,2);
            if(starts!=0) throw new InvalidOperationException("Dodge touch released outside activated");
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=dodgePoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=true,Position=dodgePoint,GlobalPosition=dodgePoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=dodgePoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=false,Position=dodgePoint,GlobalPosition=dodgePoint});
            await Frames(app,2);
            if(starts!=1 || mobilePlayer.State!=ActionState.Dodging || mobilePlayer.ActionDirectionX<.99f || mobileStick.Finger!=7)
                throw new InvalidOperationException("Dodge multitouch, direction or emulated mouse ownership failed");
            if(dodge.Size!=dodgeSize) throw new InvalidOperationException("Dodge cooldown changed button dimensions");
            app.World.CancelPlayerDodge();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=7,Pressed=false,Position=stickPoint});
            app.World.CombatDiagnostic -= OnCombat;
            Persistence.Restore(app.World,app.Narrative,snapshot); app.RebuildViews();
            await Frames(app,2);
            if(attack.GetGlobalRect().Intersects(dodge.GetGlobalRect())) throw new InvalidOperationException("Attack and Dodge touch targets overlap");
        }
        // Use real GUI dispatch for desktop mice; headless checks exercise raw touch.
        var draught=app.Ui.GetChildren().OfType<Control>().First().FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.Contains("Draught"));
        var point=draught.GetGlobalRect().GetCenter();
        int potions=app.World.Items.Count("player","potion");
        Input.ParseInputEvent(new InputEventMouseButton { Device=0,ButtonIndex=MouseButton.Left,Pressed=true,Position=point,GlobalPosition=point });
        await Frames(app,1);
        Input.ParseInputEvent(new InputEventMouseButton { Device=0,ButtonIndex=MouseButton.Left,Pressed=false,Position=point,GlobalPosition=point });
        await Frames(app,2);
        if(app.World.Items.Count("player","potion")!=potions-1) throw new InvalidOperationException("Real desktop mouse hotbar dispatch failed");
        app.World.Items.Add("player","potion");
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
        app.Ui.FeelLab();
        await Capture(app,"feel-lab");
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
