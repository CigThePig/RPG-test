using Godot;
using System.Text.Json;

namespace Mosslight;
public partial class RpgUi : CanvasLayer
{
    public Foundation App { get; set; } = null !;

    readonly Dictionary<string, Button> abilityButtons = new();
    Label health = null !, message = null !, title = null !;
    PanelContainer panel = null !, menu = null !;
    VBoxContainer body = null !;
    Control root = null !;
    GridContainer hotbar = null !;
    public TouchStick Stick { get; private set; } = null!;
    Label metrics = null!;
    bool showMetrics;
    bool labVisible;
    Button marker = null!;
    public bool MobileLayout { get; set; } = OS.HasFeature("mobile") || OS.GetCmdlineUserArgs().Contains("--mobile-ui");
    readonly Queue<double> frames = new();
    public bool MenuOpen => menu.Visible;
    public bool BlocksInput => panel.Visible || menu.Visible;
    public bool SettingsOpen { get; private set; }
    public bool Muted { get; private set; }
    public int SaveSlot { get; private set; } = 1;

    public override void _Ready()
    {
        root = new Control
        {
            LayoutMode = 0,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(root);
        var theme = new Theme();
        theme.DefaultFontSize = 20;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(.065f, .1f, .1f, .96f),
            BorderColor = new Color(.48f, .52f, .34f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 16
        };
        theme.SetStylebox("panel", "PanelContainer", style);
        theme.SetColor("font_color", "Label", new Color(.87f, .9f, .78f));
        root.Theme = theme;
        title = new Label
        {
            Text = "MOSSLIGHT  /  THE REACH",
            Position = new Vector2(24, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(title);
        health = new Label
        {
            Position = new Vector2(24, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(health);
        message = new Label
        {
            Position = new Vector2(24, 730),
            Size = new Vector2(1200, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(message);
        hotbar = new GridContainer
        {
            Columns = 5,
            Position = new Vector2(500, 640)
        };
        root.AddChild(hotbar);
        foreach (var(text, action)in new (string, Action)[]
        {
            ("1 · Sword", () => App.Cast("slash")),
            ("2 · Ember", () => App.Cast("ember")),
            ("3 · Mend", () => App.Cast("mend")),
            ("4 · Draught", () => App.UseQuickItem("potion")),
            ("E · Interact", App.Interact),
            ("I · Pack", Inventory),
            ("J · Journal", Journal),
            ("C · Character", Character),
            ("M · Map", Map),
            ("Esc · Menu", Menu)
        }

        )
        {
            if (MobileLayout && (text.StartsWith("J ·") || text.StartsWith("C ·") || text.StartsWith("M ·"))) continue;
            var button = Button(hotbar, MobileLayout ? text.Split(" · ").Last() : text, action);
            if (MobileLayout) button.CustomMinimumSize = new Vector2(135, 72);
            var id = text.StartsWith("1") ? "slash" : text.StartsWith("2") ? "ember" : text.StartsWith("3") ? "mend" : "";
            if (id != "")
            {
                abilityButtons[id] = button;
                var def = App.World.Content.Abilities.Single(a => a.Id == id);
                button.TooltipText = $"{def.Name} · mana {def.Cost} · range {def.Range}";
            }
        }

        panel = new PanelContainer
        {
            Position = new Vector2(780, 100),
            CustomMinimumSize = new Vector2(740, 510),
            Visible = false
        };
        root.AddChild(panel);
        body = new VBoxContainer();
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(690, 440)
        };
        panel.AddChild(scroll);
        scroll.AddChild(body);
        body.CustomMinimumSize = new Vector2(400, 0);
        menu = new PanelContainer
        {
            Position = new Vector2(380, 160),
            CustomMinimumSize = new Vector2(560, 430)
        };
        root.AddChild(menu);
        BuildMenu();
        Stick = new TouchStick { App = App, Size = new Vector2(200, 200) };
        root.AddChild(Stick);
        metrics = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        root.AddChild(metrics);
        marker=Button(root,"Mark issue",()=> { App.Trace.Marker("player-observation"); Notify("Marked this moment in playtest trace"); });
        marker.Position=new Vector2(1040,110); marker.Visible=false;
        GetViewport().SizeChanged += Layout;
        Layout();
    }

    readonly Dictionary<int, Button> touchButtons = new();
    public override void _Input(InputEvent e)
    {
        // Hotbar touches are dispatched below for independent multitouch ownership.
        // Godot also synthesizes a mouse click for the same finger; consuming that
        // event prevents a second native Button.Pressed without disabling mouse
        // emulation for menus, dialogue, scroll panels or actual desktop mice.
        if (e is InputEventMouse mouse && mouse.Device == InputEvent.DeviceIdEmulation && hotbar.IsVisibleInTree() && hotbar.GetGlobalRect().HasPoint(mouse.Position))
        { GetViewport().SetInputAsHandled(); return; }
        if (e is not InputEventScreenTouch touch) return;
        if (touch.Canceled)
        {
            if (touchButtons.Remove(touch.Index)) GetViewport().SetInputAsHandled();
            return; // Unowned cancellation must still reach the joystick or native controls.
        }
        if (touch.Pressed)
        {
            var button = hotbar.GetChildren().OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && !b.Disabled && b.GetGlobalRect().HasPoint(touch.Position));
            if (button == null) return;
            touchButtons[touch.Index] = button;
            GetViewport().SetInputAsHandled();
        }
        else if (touchButtons.Remove(touch.Index, out var button))
        {
            if (button.IsVisibleInTree() && !button.Disabled && button.GetGlobalRect().HasPoint(touch.Position))
                button.EmitSignal(Godot.Button.SignalName.Pressed);
            GetViewport().SetInputAsHandled();
        }
    }

    void Layout()
    {
        var size = GetViewport().GetVisibleRect().Size;
        hotbar.Position = new Vector2(size.X - 760, size.Y - (MobileLayout ? 200 : 160));
        Stick.Position = new Vector2(40, size.Y - 260);
        message.Position = new Vector2(24, size.Y - 45);
        message.Size = new Vector2(size.X - 48, 40);
        panel.Position = new Vector2((size.X - 740) / 2, 90);
        panel.Size = panel.CustomMinimumSize = new Vector2(740, size.Y - 290);
        menu.Position = new Vector2((size.X - 560) / 2, 70);
        menu.Size = new Vector2(560, size.Y - 140);
        metrics.Position = new Vector2(size.X - 480, 20);
    }

    static Button Button(Node parent, string text, Action action)
    {
        var b = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 56)
        };
        b.Pressed += action;
        parent.AddChild(b);
        return b;
    }

    void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    void Open(string heading)
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        App.TouchMovement = Vector2.Zero;
        Clear(body);
        panel.Visible = true;
        body.AddChild(new Label { Text = heading });
        Button(body, "Close", () => panel.Hide());
    }

    public override void _Process(double delta)
    {
        frames.Enqueue(delta * 1000);
        while (frames.Count > 600) frames.Dequeue();
    }

    public void Notify(string text) => message.Text = text;
    public void Update()
    {
        hotbar.Visible = !BlocksInput && !App.Narrative.Get("speaking").AsBool();
        if (!hotbar.Visible) touchButtons.Clear();
        Stick.Visible = MobileLayout && !App.GameplayBlocked;
        if (App.GameplayBlocked) Stick.Cancel();
        marker.Visible=App.Trace.Active && !BlocksInput;
        marker.Position=new Vector2(GetViewport().GetVisibleRect().Size.X-180,110);
        metrics.Visible = showMetrics || labVisible;
        if(labVisible && !showMetrics) metrics.Text=$"FEEL LAB · {App.Feel.Id} · {(App.Trace.Active ? "REC" : "idle")}\nSpeed {App.World.ActualVelocity.Length():0.0} · input {App.TouchMovement.Length():0.00}";
        if (showMetrics && frames.Count > 0 && App.World.Tick % 30 == 0)
        {
            var sorted = frames.OrderBy(x => x).ToArray();
            var interaction=App.World.InteractionTargets(App.World.Player).FirstOrDefault();
            string nearby=interaction.Value==null ? "none" : interaction.Value+" "+System.Numerics.Vector2.Distance(App.World.Player.Position,App.World.RegionsById[App.World.Player.Region].Center(interaction.Key)).ToString("0.0");
            metrics.Text = $"Target {nearby}\nXY {App.World.Player.X:0},{App.World.Player.Y:0} · tile {App.World.RegionsById[App.World.Player.Region].Index(App.World.Player.Position)} · stand {App.World.RegionsById[App.World.Player.Region].CanStand(App.World.Player.Position,App.World.Player)}\nInput {App.World.LastInput.Length():0.00} · velocity {App.World.ActualVelocity.Length():0.0} · sim {App.SimulationMs:0.00}ms\nBlocked {App.World.LastBlockedReason}\nFPS {Engine.GetFramesPerSecond()} · p50/p95/p99 {sorted[(int)((sorted.Length - 1) * .5)]:0.0}/{sorted[(int)((sorted.Length - 1) * .95)]:0.0}/{sorted[(int)((sorted.Length - 1) * .99)]:0.0} ms\nActors {App.Views.Values.Count(v => v.Visible)}/{App.World.Actors.Count} · managed {GC.GetTotalMemory(false)/1048576.0:0} MB\nRaster {PixelCreatures.Runtime.CreatureRenderQueue.LastRasterMs:0.0} ms · upload {PixelCreatures.Runtime.CreatureRenderQueue.LastUploadMs:0.0} ms · save {Persistence.LastSaveMilliseconds:0.0} ms";
        }
        var a = App.World.Player;
        health.Text = $"{a.Name}   ♥ {a.Health:0}/{a.MaxHealth}    Mana {a.Mana:0}/{a.MaxMana}    Lv {a.Level} · XP {a.Experience}/{a.Level * 100}    {a.State}\n{a.Region} · {(int)(App.World.Time / 60) % 24:00}:{(int)App.World.Time % 60:00}";
        foreach (var(id, button)in abilityButtons)
        {
            var def = App.World.Content.Abilities.Single(d => d.Id == id);
            float cooldown = a.Cooldowns.GetValueOrDefault(id);
            button.Disabled = cooldown > 0 || a.Mana < def.Cost || !a.Alive;
            string binding = id == "slash" ? "1" : id == "ember" ? "2" : "3";
            string label = MobileLayout ? (id == "slash" ? "Attack" : def.Name.Split(' ')[0]) : $"{binding} · {def.Name}";
            button.Text = cooldown > 0 ? $"{label} {cooldown:0.0}s" : label;
        }
    }

    public void PauseForVerification()
    {
        panel.Hide();
        menu.Show();
    }

    public void HideMenu() => menu.Hide();
    public void ClosePanels() => panel.Hide();
    public void Inventory()
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        Open("PACK & EQUIPMENT");
        body.AddChild(new Label { Text = App.World.Items.Describe("player") });
        using var catalog = JsonDocument.Parse(App.World.Items.Service.Call("catalog", "player").AsString());
        foreach (var row in catalog.RootElement.EnumerateArray())
        {
            string selector = row.GetProperty("selector").GetString()!;
            string slot = row.GetProperty("slot").GetString()!;
            string title = row.GetProperty("title").GetString()!;
            if (slot != "")
            {
                var button = Button(body, "Equip · " + title, () =>
                {
                    App.ManageItem(() => App.World.Items.Equip(App.World.Player, selector, slot));
                    Inventory();
                });
                button.TooltipText = row.GetProperty("properties").GetString();
            }

            if (row.GetProperty("healing").GetSingle() > 0)
                Button(body, "Use · " + title, () =>
                {
                    ClosePanels();
                    App.UseQuickItem(selector);
                    Inventory();
                });
        }

        foreach (var recipe in App.World.Content.Recipes)
            Button(body, "Craft · " + System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(recipe.Id.Replace("_", " ")), () =>
            {
                Notify(App.ManageItem(() => App.World.Items.Craft("player", recipe)) ? "Crafted " + recipe.Output : "Missing ingredients");
                Inventory();
            });
        var player = App.World.Player;
        var merchant = App.World.Actors.Values.FirstOrDefault(a => a.Profession == "merchant" && a.Alive && a.Region == player.Region && System.Numerics.Vector2.Distance(a.Position, player.Position) < 80 && !App.World.Hostile(player, a) && App.World.LineOfSight(player, a));
        if (merchant != null)
        {
            using var stock = JsonDocument.Parse(App.World.Items.Service.Call("catalog", merchant.Id).AsString());
            foreach (var row in stock.RootElement.EnumerateArray())
            {
                string id = row.GetProperty("id").GetString()!;
                string title = row.GetProperty("title").GetString()!;
                Button(body, "Buy · " + title + " · 5 crowns", () =>
                {
                    Notify(App.ManageItem(() => App.World.Items.Trade("player", merchant.Id, id, 5)) ? "Purchase complete" : "Insufficient funds or stock");
                    Inventory();
                });
            }
        }
    }

    public void Journal()
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        Open("QUEST JOURNAL");
        body.AddChild(new Label { Text = App.Narrative.Call("journal").AsString(), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 300) });
    }

    public void Character()
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        var actor = App.World.Player;
        Open("WAYFARER");
        body.AddChild(new Label { Text = $"Level {actor.Level} · skill points {actor.SkillPoints}\nStrength {App.World.Stat(actor, "strength")}\nDefence {App.World.Stat(actor, "defence")}\nWeapon damage {App.World.Items.Bonus(actor, "damage")}\nFamily {actor.Appearance.Family} · seed {actor.Appearance.Seed}\nFlight {actor.Flies} · Swimming {actor.Swims} · Grasping {actor.Grasps}\nWarden reputation {actor.Reputation.GetValueOrDefault("wardens")}" });
        foreach (var ability in App.World.Content.Abilities)
        {
            if (actor.Abilities.Contains(ability.Id))
                Button(body, "Use · " + ability.Name, () =>
                {
                    ClosePanels();
                    App.Cast(ability.Id);
                });
            else
                Button(body, $"Learn · {ability.Name} (level {ability.UnlockLevel} / {ability.UnlockCost} points)", () =>
                {
                    Notify(App.ManageItem(() => Progression.Unlock(App.World, actor, ability.Id)) ? "Ability learned" : "Requirements not met");
                    Character();
                });
        }

        foreach (var skill in App.World.Content.Skills)
            Button(body, $"Train · {skill.Id} [{actor.SkillRanks.GetValueOrDefault(skill.Id)}/{skill.MaxRank}]", () =>
            {
                App.ManageItem(() => Progression.Train(App.World, actor, skill.Id));
                Character();
            });
    }

    public void Map()
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        Open("REGION MAP");
        var map = new RegionMap
        {
            Region = App.World.RegionsById[App.World.Player.Region],
            Player = App.World.Player,
            CustomMinimumSize = new Vector2(400, 320)
        };
        body.AddChild(map);
    }

    public void Menu()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        App.TouchMovement = Vector2.Zero;
        panel.Hide();
        menu.Visible = !menu.Visible;
        if (menu.Visible)
            BuildMenu();
    }

    void BuildMenu()
    {
        Clear(menu);
        var box = new VBoxContainer();
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(510, 600) };
        menu.AddChild(scroll);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(box);
        box.AddChild(new Label { Text = "MOSSLIGHT\nRPG FOUNDATION", HorizontalAlignment = HorizontalAlignment.Center });
        Button(box, "Enter / Resume", App.ResumeGame);
        foreach (var entry in new (string, Action)[] { ("Pack", Inventory), ("Journal", Journal), ("Character", Character), ("Map", Map) })
            Button(box, entry.Item1, () => { menu.Hide(); entry.Item2(); });
        Button(box, "Recover background autosave", () =>
        {
            try { if(App.InFeelCourse)throw new InvalidOperationException("Return from the controlled course before loading"); Persistence.LoadAutosave(App.World, App.Narrative); App.RebuildViews(); App.ResumeGame(); Notify("Recovered autosave"); }
            catch (Exception e) { Notify(e.Message); }
        });
        Button(box, "New game", () =>
        {
            App.Reset();
            App.ResumeGame();
        });
        Button(box, $"Save slot: {SaveSlot} (click to cycle)", () =>
        {
            SaveSlot = SaveSlot % 3 + 1;
            BuildMenu();
        });
        Button(box, "Save", () =>
        {
            try
            {
                if(App.InFeelCourse)throw new InvalidOperationException("Return from the controlled course before saving");
                Persistence.Save(App.World, App.Narrative, SaveSlot);
                Notify("Saved slot " + SaveSlot);
            }
            catch (Exception e)
            {
                Notify(e.Message);
            }
        });
        Button(box, "Load", () =>
        {
            try
            {
                if(App.InFeelCourse)throw new InvalidOperationException("Return from the controlled course before loading");
                Persistence.Load(App.World, App.Narrative, SaveSlot);
                App.RebuildViews();
                App.ResumeGame();
                Notify("Loaded slot " + SaveSlot);
            }
            catch (Exception e)
            {
                Notify(e.Message);
            }
        });
        Button(box, "Audio: " + (Muted ? "Muted" : "On"), () =>
        {
            Muted = !Muted;
            AudioServer.SetBusMute(0, Muted);
            BuildMenu();
        });
        Button(box, "Pixel scale / camera zoom", () => App.CycleZoom());
        Button(box, "Creature workshop", () =>
        {
            menu.Hide();
            Workshop();
        });
        Button(box, "Collision / navigation overlay: " + (App.CollisionDebug ? "On" : "Off"), () => { App.CollisionDebug = !App.CollisionDebug; BuildMenu(); });
        Button(box, "Performance overlay: " + (showMetrics ? "On" : "Off"), () => { showMetrics = !showMetrics; BuildMenu(); });
        Button(box, "Game-feel lab / record playtest", () => { menu.Hide(); FeelLab(); });
        Button(box, "Developer diagnostics", () =>
        {
            menu.Hide();
            Diagnostics();
        });
        Button(box, "Quit", () => GetTree().Quit());
    }

    public void FeelLab()
    {
        Open("GAME-FEEL LAB · development tools");
        labVisible=true;
        body.AddChild(new Label { Text=$"Active: {App.Feel.Id} · baseline restores original controls\nCompare the same settlement chest → road → ruin route.\nRecording stays local; use Android Screen recorder for video." });
        Button(body,"Start / reset controlled obstacle course (normal progress preserved)",App.BeginFeelCourse);
        if(App.InFeelCourse)Button(body,"Return from course to normal game",App.LeaveFeelCourse);
        foreach(var preset in MovementFeel.Presets)
            Button(body,$"{preset.Id} · speed ×{preset.Scale:0.0} · response {preset.Response*1000:0}ms",()=> { App.SelectFeel(preset); ClosePanels(); Notify("Feel preset: "+preset.Id); });
        void Slider(string label,double min,double max,double step,double value,Action<float> changed)
        {
            var caption=new Label {Text=$"{label}: {value:0.00}"};body.AddChild(caption);
            var slider=new HSlider {MinValue=min,MaxValue=max,Step=step,Value=value,CustomMinimumSize=new Vector2(550,44)};
            slider.ValueChanged+=v=> {caption.Text=$"{label}: {v:0.00}";changed((float)v);};body.AddChild(slider);
        }
        Slider("Player speed multiplier",1,4,.1,App.Feel.Scale,v=>App.SelectFeel(App.Feel with {Id="custom",Scale=v}));
        Slider("Response seconds (release always immediate)",0,.12,.01,App.Feel.Response,v=>App.SelectFeel(App.Feel with {Id="custom",Response=v}));
        Slider("Joystick dead zone",.02,.25,.01,App.Feel.Deadzone,v=>App.SelectFeel(App.Feel with {Id="custom",Deadzone=v}));
        Slider("Joystick curve",.5,1.5,.05,App.Feel.Curve,v=>App.SelectFeel(App.Feel with {Id="custom",Curve=v}));
        Slider("Camera follow rate",6,30,1,App.Feel.CameraFollow,v=>App.SelectFeel(App.Feel with {Id="custom",CameraFollow=v}));
        Slider("Camera zoom",1,2,.05,App.Feel.Zoom,v=>App.SelectFeel(App.Feel with {Id="custom",Zoom=v}));
        Button(body,App.Trace.Active ? "Stop local recording" : "Start local recording",()=>
        {
            if(App.Trace.Active)App.Trace.Dispose();
            else { try {App.Trace.Start(ProjectSettings.GlobalizePath("user://playtests/session.jsonl"));} catch(Exception e) {Notify("Recording failed: "+e.Message);return;} }
            ClosePanels(); Notify(App.Trace.Active ? "Recording · tap Mark issue at any time" : "Recording stopped");
        });
        foreach(var label in new[]{"Movement too slow","Turning awkward","Collision sticky","Camera wrong","Animation disconnected","Performance stutter"})
            Button(body,"Mark · "+label,()=> {App.Trace.Marker(label);ClosePanels();});
        Button(body,"Stop and export playtest ZIP to Files…",()=>PlaytestExport.Show(App));
        Button(body,"Hide lab overlay",()=> {labVisible=false;ClosePanels();});
    }

    public void Workshop()
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        Open("PROCEDURAL CREATURE WORKSHOP");
        var family = new OptionButton();
        foreach (var f in PixelCreatures.Runtime.CreatureRuntime.Registry.Families)
            family.AddItem(f.Id);
        body.AddChild(family);
        var seed = new SpinBox
        {
            MinValue = 0,
            MaxValue = 1000000,
            Value = 42
        };
        body.AddChild(seed);
        Button(body, "Apply generated appearance to player", () =>
        {
            App.ChangeAppearance(family.GetItemText(family.Selected), (ulong)seed.Value);
            Character();
        });
        Button(body, "Mutate appearance deterministically", () =>
        {
            App.MutateAppearance((ulong)seed.Value);
            Character();
        });
        Button(body, "Export genome to user://workshop.json", () => Notify(PixelCreatures.Runtime.CreatureRuntime.SavePreset("user://workshop.json", App.Views["player"].Creature.Genome!) ?? "Exported workshop genome"));
    }

    public void Diagnostics()
    {
        if (App.Narrative.Get("speaking").AsBool() || menu.Visible) return;
        Open("DEVELOPER TOOLS");
        body.AddChild(new Label { Text = $"Tick {App.World.Tick}\nActors {App.World.Actors.Count}\nRegions {App.World.RegionsById.Count}\nProjectiles {App.World.Projectiles.Count}\nManaged memory {GC.GetTotalMemory(false) / 1048576.0:0.0} MB\nFPS {Engine.GetFramesPerSecond()}\nEvents:\n" + string.Join("\n", App.World.Events.Recent.TakeLast(8).Select(e => $"{e.Kind}: {e.Actor} {e.Value}")) });
        Button(body, "Enter generated dungeon", () =>
        {
            App.World.Enter(App.World.Player, "ruin");
            panel.Hide();
        });
        Button(body, "Return to settlement", () =>
        {
            App.World.Enter(App.World.Player, "reach");
            panel.Hide();
        });
        Button(body, "Complete integration checks", () =>
        {
            App.RunChecks();
            panel.Hide();
        });
    }
}

public partial class RegionMap : Control
{
    public RegionState Region { get; set; } = null !;
    public ActorState Player { get; set; } = null !;

    public override void _Draw()
    {
        float scale = Math.Min(Size.X / Region.Width, Size.Y / Region.Height);
        for (int y = 0; y < Region.Height; y++)
            for (int x = 0; x < Region.Width; x++)
            {
                var t = Region.Tiles[y * Region.Width + x];
                DrawRect(new Rect2(x * scale, y * scale, scale + 1, scale + 1), t is Terrain.Wall or Terrain.Rock ? new Color(.2f, .24f, .22f) : t == Terrain.Water ? new Color(.2f, .4f, .46f) : new Color(.48f, .55f, .34f));
            }

        DrawCircle(new Vector2(Player.X / 24 * scale, Player.Y / 24 * scale), 4, Colors.Gold);
    }
}
