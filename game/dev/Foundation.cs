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
    public bool SessionStarted { get; private set; }
    public void ResumeGame() { SessionStarted = true; Ui.HideMenu(); }
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
    readonly List<FeatureView> features = new();
    public bool CollisionDebug { get; set; }
    public double SimulationMs { get; private set; }
    Node2D ground = null !;
    double accumulator;
    long lastRuntimeLog;
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
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 12
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
        GD.Print($"MOSSLIGHT_READY actors={World.Actors.Count} families={CreatureRuntime.Registry.Families.Count()} framework={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
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
            if (OS.HasFeature("android") && e.Kind is "resource_depleted" or "region_entered")
                GD.Print($"MOSSLIGHT_GAMEPLAY kind={e.Kind} value={e.Value} tick={World.Tick}");
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
                // Input describes screen direction; PPC projects ground south at half height.
                var groundInput = GodotConvert.ScreenToGround(v).Normalized() * v.Length();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                World.PlayerMove(new(groundInput.X, groundInput.Y), 1f / 60);
                World.Step(1f / 60);
                SimulationMs = timer.Elapsed.TotalMilliseconds;
                accumulator -= 1.0 / 60;
            }
        }
        else
            accumulator = 0;
        var player = World.Player;
        foreach (var(id, view)in Views)
        {
            view.Visible = World.Actors[id].Region == player.Region;
            view.Creature.Paused = !view.Visible || GameplayBlocked;
            view.Sync((float)delta);
        }

        var region = World.RegionsById[player.Region];
        var bounds = GodotConvert.GroundToScreen(region.Width * 24, region.Height * 24);
        var half = GetViewportRect().Size / camera.Zoom / 2;
        var focus = GodotConvert.GroundToScreen(player.X, player.Y);
        camera.Position = new Vector2(bounds.X <= half.X*2 ? bounds.X/2 : Mathf.Clamp(focus.X,half.X,bounds.X-half.X), bounds.Y <= half.Y*2 ? bounds.Y/2 : Mathf.Clamp(focus.Y,half.Y,bounds.Y-half.Y));
        var target = World.InteractionTargets(player).FirstOrDefault();
        foreach (var feature in features)
        {
            feature.Selected = target.Value != null && feature.GetMeta("tile").AsInt32() == target.Key;
            feature.QueueRedraw();
        }
        if (drawnRegion != player.Region)
        {
            BuildTerrain();
            drawnRegion = player.Region;
        }

        if (OS.HasFeature("android") && World.Tick - lastRuntimeLog >= 300)
        {
            lastRuntimeLog = World.Tick;
            GD.Print($"MOSSLIGHT_RUNTIME tick={World.Tick} position={player.X:0.0},{player.Y:0.0} rasterizations={Views.Values.Sum(v => v.Creature.RenderCount)}");
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
        var t = d.Self ? a : World.Actors.Values.Where(x => World.CanActivate(a, x, d)).OrderBy(x => NVector.DistanceSquared(a.Position, x.Position)).FirstOrDefault();
        ui.Notify(t != null && World.Activate(a.Id, ability, t.Id) ? d.Name : "No valid target, equipment, mana or cooldown");
    }

    public void Interact()
    {
        if (GameplayBlocked)
            return;
        var a = World.Player;
        var npc = World.Actors.Values.FirstOrDefault(x => x.Alive && x.Profession == "merchant" && x.Region == a.Region && NVector.Distance(x.Position, a.Position) < 65 && !World.Hostile(a, x) && World.LineOfSight(a, x));
        if (npc != null && !World.InteractionTargets(a).Any() && !World.Actors.Values.Any(x => !x.Alive && x.Region == a.Region && NVector.Distance(x.Position,a.Position) < 48 && World.Items.HasLoot(x.Id)))
        {
            Narrative.Call("talk");
            return;
        }

        World.Interact(a);
        ui.Notify(World.InteractionFeedback);
        drawnRegion = "";
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused || what == NotificationApplicationFocusOut)
        {
            TouchMovement = Vector2.Zero;
            ui?.Stick.Cancel();
            if (OS.HasFeature("android")) GD.Print("MOSSLIGHT_INPUT_CANCELLED");
            suspended = true;
            if (World != null && ui != null)
            {
                ui.PauseForVerification();
                try { if (SessionStarted) Persistence.SaveAutosave(World, Narrative); }
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
        foreach (var feature in features) { actors.RemoveChild(feature); feature.QueueFree(); }
        features.Clear();
        foreach (var (at, kind) in r.Features)
        {
            var pos = r.Center(at);
            var feature = new FeatureView { Kind = kind, Spent = r.Depleted.Contains(at) || r.OpenDoors.Contains(at), Position = GodotConvert.GroundToScreen(pos.X,pos.Y) };
            feature.SetMeta("tile",at);
            actors.AddChild(feature); features.Add(feature);
        }
        camera.ResetSmoothing();
    }

    public override void _Draw()
    {
        if (World == null)
            return;
        var r = World.RegionsById[World.Player.Region];
        if (CollisionDebug)
        {
            var player = World.Player;
            for (int at=0; at<r.Tiles.Length; at++)
            {
                var p = r.Center(at);
                if (NVector.DistanceSquared(p,player.Position) > 260*260) continue;
                DrawRect(new Rect2(GodotConvert.GroundToScreen(p.X-12,p.Y-12),new Vector2(24,12)),r.CanStand(p,player) ? new Color(0,1,0,.12f) : new Color(1,0,0,.3f));
            }
            foreach(var a in World.Actors.Values.Where(a=>a.Region==player.Region))
            {
                var p=GodotConvert.GroundToScreen(a.X,a.Y);
                DrawSetTransform(p,0,new Vector2(1,.5f));
                DrawArc(Vector2.Zero,a.Radius,0,Mathf.Tau,24,Colors.Cyan,1);
                DrawSetTransform(Vector2.Zero);
                DrawLine(p,p+GodotConvert.GroundToScreen(a.MotionX*.2f,a.MotionY*.2f),Colors.Yellow,1);
            }
            var target=World.InteractionTargets(player).FirstOrDefault();
            if(target.Value!=null)
            {
                var path=r.Path(player.Position,r.Center(target.Key),player);
                var previous=GodotConvert.GroundToScreen(player.X,player.Y);
                foreach(int at in path) { var p=r.Center(at);var next=GodotConvert.GroundToScreen(p.X,p.Y);DrawLine(previous,next,Colors.Gold,2);previous=next; }
            }
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
    readonly List<TerrainBatch> batches=new();
    public RegionState Region { get; set; } = null !;
    public Dictionary<string, Texture2D> Art { get; set; } = null !;

    public override void _Draw()
    {
        batches.Clear();
        var batch=new TerrainBatch();batches.Add(batch);
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
                batch.Rect(new Rect2(GodotConvert.GroundToScreen(x * 24, y * 24), new Vector2(24, 12)), color);
            }

        batch.Draw(this);batch=new TerrainBatch();batches.Add(batch);
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

        for (int y = 0; y < Region.Height; y++)
            for (int x = 0; x < Region.Width; x++)
            {
                int at=y*Region.Width+x;
                var tile=Region.Tiles[at];
                var p=GodotConvert.GroundToScreen(x*24,y*24);
                if(tile is Terrain.Rock or Terrain.Wall)
                {
                    float rise=tile==Terrain.Wall ? 14 : 8;
                    batch.Rect(new Rect2(p+new Vector2(0,-rise),new Vector2(24,12)),new Color(tile==Terrain.Wall ? "4d5d60" : "697268"));
                    batch.Rect(new Rect2(p+new Vector2(0,12-rise),new Vector2(24,rise)),new Color("354344"));
                    batch.Line(p+new Vector2(0,-rise),p+new Vector2(24,-rise),new Color("92988a"),1);
                    batch.Line(p+new Vector2(3,-rise+4),p+new Vector2(13,-rise+4),new Color("7d8477"),1);
                }
                if(tile==Terrain.Water)
                {
                    if(y>0 && Region.Tiles[at-Region.Width]!=Terrain.Water) batch.Line(p,p+new Vector2(24,0),new Color("b2bea0"),2);
                    if(y<Region.Height-1 && Region.Tiles[at+Region.Width]!=Terrain.Water) batch.Line(p+new Vector2(0,12),p+new Vector2(24,12),new Color("b2bea0"),2);
                    if(x>0 && Region.Tiles[at-1]!=Terrain.Water) batch.Line(p,p+new Vector2(0,12),new Color("b2bea0"),2);
                    if(x<Region.Width-1 && Region.Tiles[at+1]!=Terrain.Water) batch.Line(p+new Vector2(24,0),p+new Vector2(24,12),new Color("b2bea0"),2);
                }
            }
        batch.Draw(this);
    }
}
