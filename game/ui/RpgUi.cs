using Godot;

namespace Mosslight;
public partial class RpgUi : CanvasLayer
{
    public Foundation App { get; set; } = null !;

    readonly Dictionary<string, Button> abilityButtons = new();
    Label health = null !, message = null !, title = null !;
    PanelContainer panel = null !, menu = null !;
    VBoxContainer body = null !;
    public bool BlocksInput => panel.Visible || menu.Visible;
    public bool SettingsOpen { get; private set; }
    public bool Muted { get; private set; }
    public int SaveSlot { get; private set; } = 1;

    public override void _Ready()
    {
        var root = new Control
        {
            LayoutMode = 0,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(root);
        var theme = new Theme();
        theme.DefaultFontSize = 16;
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
        var hotbar = new HBoxContainer
        {
            Position = new Vector2(24, 680)
        };
        root.AddChild(hotbar);
        foreach (var(text, action)in new (string, Action)[]
        {
            ("1 · Sword", () => App.Cast("slash")),
            ("2 · Ember", () => App.Cast("ember")),
            ("3 · Mend", () => App.Cast("mend")),
            ("4 · Draught", () => App.World.UsePotion(App.World.Player)),
            ("E · Interact", App.Interact),
            ("I · Pack", Inventory),
            ("J · Journal", Journal),
            ("C · Character", Character),
            ("M · Map", Map),
            ("Esc · Menu", Menu)
        }

        )
        {
            var button = Button(hotbar, text, action);
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
            CustomMinimumSize = new Vector2(460, 510),
            Visible = false
        };
        root.AddChild(panel);
        body = new VBoxContainer();
        panel.AddChild(body);
        menu = new PanelContainer
        {
            Position = new Vector2(380, 160),
            CustomMinimumSize = new Vector2(520, 430)
        };
        root.AddChild(menu);
        BuildMenu();
    }

    static Button Button(Node parent, string text, Action action)
    {
        var b = new Button
        {
            Text = text
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
        Clear(body);
        panel.Visible = true;
        body.AddChild(new Label { Text = heading });
        Button(body, "Close", () => panel.Hide());
    }

    public void Notify(string text) => message.Text = text;
    public void Update()
    {
        var a = App.World.Player;
        health.Text = $"{a.Name}   ♥ {a.Health:0}/{a.MaxHealth}    Mana {a.Mana:0}/{a.MaxMana}    Lv {a.Level} · XP {a.Experience}/{a.Level * 100}    {a.State}\n{a.Region} · {(int)(App.World.Time / 60) % 24:00}:{(int)App.World.Time % 60:00}";
        foreach (var(id, button)in abilityButtons)
        {
            var def = App.World.Content.Abilities.Single(d => d.Id == id);
            float cooldown = a.Cooldowns.GetValueOrDefault(id);
            button.Disabled = cooldown > 0 || a.Mana < def.Cost || !a.Alive;
            button.Text = cooldown > 0 ? $"{def.Name} · {cooldown:0.0}s" : def.Name;
        }
    }

    public void ClosePanels() => panel.Hide();
    public void Inventory()
    {
        Open("PACK & EQUIPMENT");
        body.AddChild(new Label { Text = App.World.Items.Describe("player") });
        Button(body, "Equip iron sword", () =>
        {
            App.World.Items.Equip(App.World.Player, "iron_sword", "weapon");
            Inventory();
        });
        Button(body, "Equip leather coat", () =>
        {
            App.World.Items.Equip(App.World.Player, "leather_coat", "body");
            Inventory();
        });
        foreach (var recipe in App.World.Content.Recipes)
            Button(body, "Craft · " + recipe.Id, () =>
            {
                Notify(App.World.Items.Craft("player", recipe) ? "Crafted " + recipe.Output : "Missing ingredients");
                Inventory();
            });
        var merchant = App.World.Actors.Values.FirstOrDefault(a => a.Profession == "merchant" && a.Alive && a.Region == App.World.Player.Region && System.Numerics.Vector2.Distance(a.Position, App.World.Player.Position) < 80);
        if (merchant != null)
            Button(body, "Buy draught · 5 crowns", () =>
            {
                Notify(App.World.Items.Trade("player", merchant.Id, "potion", 5) ? "Purchase complete" : "Insufficient funds or stock");
                Inventory();
            });
    }

    public void Journal()
    {
        Open("QUEST JOURNAL");
        body.AddChild(new Label { Text = App.Narrative.Call("journal").AsString(), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 300) });
    }

    public void Character()
    {
        var a = App.World.Player;
        Open("WAYFARER");
        body.AddChild(new Label { Text = $"Level {a.Level} · skill points {a.SkillPoints}\nStrength {a.Strength}\nDefence {a.Defence + App.World.Items.Bonus(a, "defence")}\nWeapon damage {App.World.Items.Bonus(a, "damage")}\nFamily {a.Appearance.Family} · seed {a.Appearance.Seed}\nFlight {a.Flies} · Swimming {a.Swims} · Grasping {a.Grasps}\nWarden reputation {a.Reputation.GetValueOrDefault("wardens")}\nAbilities: {string.Join(", ", a.Abilities)}" });
        Button(body, "Learn Bark ward (level 2 / 1 point)", () =>
        {
            Notify(Progression.Unlock(App.World, a, "ward") ? "Learned Bark ward" : "Requirements not met");
            Character();
        });
        Button(body, "Train blades (1 point)", () =>
        {
            Progression.Train(App.World, a, "blades");
            Character();
        });
        if (a.Abilities.Contains("ward"))
            Button(body, "Cast Bark ward", () =>
            {
                ClosePanels();
                App.World.Activate(a.Id, "ward", a.Id);
            });
    }

    public void Map()
    {
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
        panel.Hide();
        menu.Visible = !menu.Visible;
        if (menu.Visible)
            BuildMenu();
    }

    void BuildMenu()
    {
        Clear(menu);
        var box = new VBoxContainer();
        menu.AddChild(box);
        box.AddChild(new Label { Text = "MOSSLIGHT\nRPG FOUNDATION", HorizontalAlignment = HorizontalAlignment.Center });
        Button(box, "Enter / Resume", () => menu.Hide());
        Button(box, "New game", () =>
        {
            App.Reset();
            menu.Hide();
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
                Persistence.Load(App.World, App.Narrative, SaveSlot);
                App.RebuildViews();
                menu.Hide();
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
        Button(box, "Developer diagnostics", () =>
        {
            menu.Hide();
            Diagnostics();
        });
        Button(box, "Quit", () => GetTree().Quit());
    }

    public void Workshop()
    {
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
