using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Core.Genetics;
using NVector = System.Numerics.Vector2;

namespace Mosslight;
public partial class Foundation : Node2D
{
    public World World { get; private set; } = null !;
    public Node Narrative { get; private set; } = null !;
    public Dictionary<string, CharacterView> Views { get; } = new();
    public RpgUi Ui => ui;
    public bool GameplayBlocked => ui == null || suspended || !World.Player.Alive || ui.BlocksInput || Narrative.Get("speaking").AsBool();

    bool suspended;
    public Vector2 TouchMovement { get; set; }
    public bool CanManageItems => !suspended && World.Player.Alive && !Narrative.Get("speaking").AsBool() && !ui.MenuOpen;
    public bool ManageItem(Func<bool> operation) => CanManageItems && operation();
    public bool UseQuickItem(string selector) => !GameplayBlocked && World.UseItem(World.Player, selector);
    public void ShowCharacters(bool show) => actors.Visible = show;
    Node items = null !, planner = null !;
    RpgUi ui = null !;
    Camera2D camera = null !;
    Node2D actors = null !;
    Dictionary<string, Texture2D> art = new();
    string drawnRegion = "";
    Node2D ground = null !;
    double accumulator;
    Node audio = null !;
    readonly Dictionary<string, Key> bindings = new()
    {
        {
            "north",
            Key.W
        },
        {
            "south",
            Key.S
        },
        {
            "west",
            Key.A
        },
        {
            "east",
            Key.D
        },
        {
            "interact",
            Key.E
        },
        {
            "slash",
            Key.Key1
        },
        {
            "ember",
            Key.Key2
        },
        {
            "mend",
            Key.Key3
        },
        {
            "potion",
            Key.Key4
        },
        {
            "inventory",
            Key.I
        },
        {
            "journal",
            Key.J
        },
        {
            "character",
            Key.C
        },
        {
            "map",
            Key.M
        },
        {
            "menu",
            Key.Escape
        }
    };
    static Node ScriptNode(string path) => GD.Load<GDScript>(path).New().AsGodotObject() as Node ?? throw new InvalidOperationException(path);
    public override void _Ready()
    {
        foreach (var(name, key)in bindings)
        {
            if (!InputMap.HasAction(name))
                InputMap.AddAction(name);
            InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        }

        items = ScriptNode("res://game/items/ItemService.gd");
        AddChild(items);
        planner = ScriptNode("res://game/actors/Planner.gd");
        AddChild(planner);
        Narrative = ScriptNode("res://game/narrative/Narrative.gd");
        AddChild(Narrative);
        Narrative.Set("item_service", items);
        actors = new Node2D
        {
            YSortEnabled = true
        };
        AddChild(actors);
        ground = new Node2D
        {
            ZIndex = -10
        };
        AddChild(ground);
        camera = new Camera2D
        {
            Zoom = new Vector2(1.5f, 1.5f),
            PositionSmoothingEnabled = true
        };
        AddChild(camera);
        audio = ScriptNode("res://game/audio/AudioService.gd");
        AddChild(audio);
        foreach (var name in new[]
        {
            "grass",
            "dirt",
            "water",
            "tree1",
            "rock1",
            "bush1"
        }

        )
        {
            var path = "res://game/art/" + name + ".png";
            if (ResourceLoader.Exists(path))
                art[name] = GD.Load<Texture2D>(path);
        }

        Reset();
        ui = new RpgUi
        {
            App = this
        };
        AddChild(ui);
        Narrative.Connect("reward", Callable.From<string, int>((item, count) => World.Items.Add("player", item, count)));
        Narrative.Connect("reputation", Callable.From<string, int>((faction, amount) =>
        {
            World.Player.Reputation[faction] = World.Player.Reputation.GetValueOrDefault(faction) + amount;
            World.Events.Publish(new("reputation_changed", "player", "", faction, amount));
        }));
        if (OS.GetCmdlineUserArgs().Contains("--test"))
            CallDeferred(nameof(RunChecks));
        if (OS.GetCmdlineUserArgs().Contains("--visual-check"))
            CallDeferred(nameof(RunVisualChecks));
    }

    public void Reset()
    {
        if (World != null)
        {
            items.Call("import_state", "{\"serial\":0,\"bags\":{},\"slots\":{}}");
            Narrative.Call("import_state", "{\"flags\":{},\"pools\":{},\"quests\":{}}");
        }

        World = new(Content.Load(), items)
        {
            Planner = planner
        };
        World.Events.Published += e =>
        {
            Narrative.Call("receive", e.Kind, e.Actor, e.Target, e.Value, e.Count);
            if (e.Kind == "region_entered")
                RebuildViews();
            if (e.Kind == "item_equipped" && Views.TryGetValue(e.Actor, out var changed))
                changed.Equipment(items.Call("equipment_appearance", e.Actor).AsString());
        };
        World.Presentation += (id, cue) =>
        {
            if (Views.TryGetValue(id, out var view))
                view.Cue(cue);
            if (cue is "hit" or "cast")
                PlayCue(cue);
        };
        ContentAudit.Validate(World.Content);
        World.NewGame();
        RebuildViews();
        drawnRegion = "";
    }

    public void RebuildViews()
    {
        foreach (var view in Views.Values)
        {
            actors.RemoveChild(view);
            view.QueueFree();
        }

        Views.Clear();
        foreach (var a in World.Actors.Values)
        {
            var view = new CharacterView();
            actors.AddChild(view);
            view.Setup(a);
            Views[a.Id] = view;
            view.Equipment(items.Call("equipment_appearance", a.Id).AsString());
        }

        drawnRegion = "";
    }

    public void ChangeAppearance(string family, ulong seed)
    {
        var a = World.Player;
        World.SetAppearance(a, new Appearance { Family = family, Seed = seed });
        RebuildViews();
    }

    public void MutateAppearance(ulong seed)
    {
        var a = World.Player;
        var genome = CreatureRuntime.Serializer.Deserialize(a.Appearance.GenomeJson).Genome!;
        a.Appearance.Seed = seed;
        var appearance = new Appearance
        {
            Family = a.Appearance.Family,
            Seed = seed,
            GenomeJson = CreatureRuntime.Serializer.Serialize(GenomeOps.Mutate(genome, new MutationSettings(), seed))
        };
        World.SetAppearance(a, appearance);
        RebuildViews();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ui == null)
            return;
        if (!GameplayBlocked)
        {
            accumulator += Math.Min(delta, .1);
            while (accumulator >= 1.0 / 60)
            {
                var v = (Input.GetVector("west", "east", "north", "south") + TouchMovement).LimitLength();
                World.PlayerMove(new(v.X, v.Y), 1f / 60);
                World.Step(1f / 60);
                accumulator -= 1.0 / 60;
            }
        }
        else
            accumulator = 0;
        var player = World.Player;
        foreach (var(id, view)in Views)
        {
            view.Visible = World.Actors[id].Region == player.Region;
            view.Creature.Paused = !view.Visible;
            view.Sync((float)delta);
        }

        camera.Position = GodotConvert.GroundToScreen(player.X, player.Y);
        if (drawnRegion != player.Region)
        {
            BuildTerrain();
            drawnRegion = player.Region;
        }

        ui.Update();
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("menu"))
        {
            ui.Menu();
            return;
        }

        if (GameplayBlocked)
            return;
        if (e.IsActionPressed("interact"))
            Interact();
        if (e.IsActionPressed("inventory"))
            ui.Inventory();
        if (e.IsActionPressed("journal"))
            ui.Journal();
        if (e.IsActionPressed("character"))
            ui.Character();
        if (e.IsActionPressed("map"))
            ui.Map();
        foreach (var key in new[]
        {
            "slash",
            "ember",
            "mend"
        }

        )
            if (e.IsActionPressed(key))
                Cast(key);
        if (e.IsActionPressed("potion"))
            UseQuickItem("potion");
    }

    public void Cast(string ability)
    {
        if (GameplayBlocked)
            return;
        var d = World.Content.Abilities.Single(x => x.Id == ability);
        var a = World.Player;
        var t = d.Self ? a : World.Actors.Values.Where(x => x.Alive && x.Region == a.Region && World.Hostile(a, x)).OrderBy(x => NVector.DistanceSquared(a.Position, x.Position)).FirstOrDefault();
        ui.Notify(t != null && World.Activate(a.Id, ability, t.Id) ? d.Name : "No valid target, equipment, mana or cooldown");
    }

    public void Interact()
    {
        if (GameplayBlocked)
            return;
        var a = World.Player;
        var npc = World.Actors.Values.FirstOrDefault(x => x.Alive && x.Profession == "merchant" && x.Region == a.Region && NVector.Distance(x.Position, a.Position) < 65 && !World.Hostile(a, x) && World.LineOfSight(a, x));
        if (npc != null)
        {
            Narrative.Call("talk");
            return;
        }

        ui.Notify(World.Interact(a) ? "Interacted" : "Approach an object, fallen foe, or warden");
        drawnRegion = "";
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused || what == NotificationApplicationFocusOut)
        {
            TouchMovement = Vector2.Zero;
            suspended = true;
            if (World != null && ui != null)
            {
                ui.PauseForVerification();
                try { Persistence.Save(World, Narrative, ui.SaveSlot); }
                catch (Exception e) { GD.PushWarning("Background save: " + e.Message); }
            }
        }
        if (what == NotificationApplicationResumed || what == NotificationApplicationFocusIn)
            suspended = false;
    }

    public void CycleZoom()
    {
        camera.Zoom = camera.Zoom.X > 2 ? new Vector2(1.25f, 1.25f) : camera.Zoom + new Vector2(.5f, .5f);
    }

    void BuildTerrain()
    {
        foreach (var node in ground.GetChildren())
        {
            ground.RemoveChild(node);
            node.QueueFree();
        }

        var r = World.RegionsById[World.Player.Region];
        var tilemap = new TerrainView
        {
            Region = r,
            Art = art
        };
        ground.AddChild(tilemap);
    }

    public override void _Draw()
    {
        if (World == null)
            return;
        var r = World.RegionsById[World.Player.Region];
        foreach (var(index, kind)in r.Features)
        {
            if (r.Depleted.Contains(index) || r.OpenDoors.Contains(index))
                continue;
            var p = r.Center(index);
            var screen = GodotConvert.GroundToScreen(p.X, p.Y);
            var color = kind switch
            {
                "portal" or "exit" => new Color(.5f, .8f, .8f),
                "lock" => Colors.Orange,
                "relic" => Colors.Gold,
                _ => new Color(.65f, .72f, .4f)};
            DrawCircle(screen, kind == "portal" ? 9 : 5, color);
            DrawString(ThemeDB.FallbackFont, screen + new Vector2(-12, 20), kind, HorizontalAlignment.Left, -1, 12, color);
        }

        foreach (var p in World.Projectiles)
        {
            if (World.Actors[p.Source].Region != r.Id)
                continue;
            DrawCircle(GodotConvert.GroundToScreen(p.X, p.Y), 3, new Color(1, .55f, .2f));
        }
    }

    void PlayCue(string kind) => audio.Call("cue", kind);
    public async void RunVisualChecks()
    {
        try
        {
            await VisualChecks.Run(this);
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    public async void RunChecks()
    {
        try
        {
            await FoundationChecks.Run(this);
            ui.Notify("Integration checks passed");
            if (OS.GetCmdlineUserArgs().Contains("--test"))
                GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            ui.Notify(e.Message);
            if (OS.GetCmdlineUserArgs().Contains("--test"))
                GetTree().Quit(1);
        }
    }
}

public partial class TerrainView : Node2D
{
    public RegionState Region { get; set; } = null !;
    public Dictionary<string, Texture2D> Art { get; set; } = null !;

    public override void _Draw()
    {
        // Ground layers do not overlap: group by texture so the engine can batch them.
        for (int y = 0; y < Region.Height; y++)
            for (int x = 0; x < Region.Width; x++)
            {
                var tile = Region.Tiles[y * Region.Width + x];
                var color = tile switch
                {
                    Terrain.Water => new Color(.19f, .33f, .35f),
                    Terrain.Rock => new Color(.27f, .3f, .26f),
                    Terrain.Wall => new Color(.12f, .17f, .18f),
                    Terrain.Floor => new Color(.31f, .33f, .29f),
                    Terrain.Earth => new Color(.36f, .37f, .26f),
                    _ => new Color(.3f, .39f, .27f)};
                DrawRect(new Rect2(GodotConvert.GroundToScreen(x * 24, y * 24), new Vector2(24, 12)), color);
            }

        foreach (var(terrain, key)in new[]
        {
            (Terrain.Grass, "grass"),
            (Terrain.Earth, "dirt"),
            (Terrain.Water, "water")
        }

        )
        {
            if (!Art.TryGetValue(key, out var texture))
                continue;
            for (int y = 0; y < Region.Height; y++)
                for (int x = 0; x < Region.Width; x++)
                    if (Region.Tiles[y * Region.Width + x] == terrain)
                        DrawTextureRect(texture, new Rect2(GodotConvert.GroundToScreen(x * 24, y * 24), new Vector2(24, 12)), false, new Color(1, 1, 1, .32f));
        }

        if (!Art.TryGetValue("rock1", out var rock))
            return;
        for (int y = 0; y < Region.Height; y++)
            for (int x = 0; x < Region.Width; x++)
                if (Region.Tiles[y * Region.Width + x] == Terrain.Rock && (x * 7 + y * 11) % 3 == 0)
                    DrawTexture(rock, GodotConvert.GroundToScreen(x * 24, y * 24) + new Vector2(-8, -15), new Color(1, 1, 1, .8f));
    }
}
