using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core;
using NVector = System.Numerics.Vector2;

namespace Mosslight;
public partial class Foundation : Node2D
{
    public World World { get; private set; } = null !;
    public Node Narrative { get; private set; } = null !;
    public Dictionary<string, CharacterView> Views { get; } = new();
    public RpgUi Ui => ui;
    public bool PresentationBlocked => ui == null || suspended || ui.BlocksInput || Narrative.Get("speaking").AsBool();
    public bool GameplayBlocked => PresentationBlocked || !World.Player.Alive;

    public MovementFeel Feel { get; private set; } = MovementFeel.Presets[1];
    public Camera2D Camera => camera;
    public Vector2 ScreenInput { get; private set; }
    public Vector2 ControllerVelocity { get; set; }
    public double DroppedSeconds { get; private set; }
    public PlaytestTrace Trace { get; private set; } = null!;
    GameplayReplay? replay;
    public CharacterView? ReplayObservedView => replay != null && Views.TryGetValue(replay.ObservedActorId, out var view) ? view : null;
    public bool VerificationMode => OS.GetCmdlineUserArgs().Any(x => x is "--test" or "--visual-check" or "--gameplay-check" or "--adventure-check" or "--adventure-visual");
    public void SelectFeel(MovementFeel feel)
    {
        Feel=feel;
        ControllerVelocity=Vector2.Zero;
        camera.PositionSmoothingSpeed=feel.CameraFollow;
        camera.Zoom=Vector2.One*feel.Zoom;
        foreach(var view in Views.Values) ConfigureView(view);
        Trace?.Marker("preset/"+feel.Id);
        Trace?.Write(new {kind="configuration", tick=World.Tick, frame=Engine.GetFramesDrawn(), configuration=feel});
    }
    void ConfigureView(CharacterView view)
    {
        view.PreferMeasuredExternalVelocity=Feel.Id!="baseline";
        view.Creature.UseMeasuredExternalVelocity=view.PreferMeasuredExternalVelocity;
        view.Creature.AnimationFps=view.State.Id=="player" && Feel.Id!="baseline" ? 20 : 10;
        if (view.State.Id == "player")
            view.GroundTerrain = actor =>
            {
                if (!World.RegionsById.TryGetValue(actor.Region, out var region)) return Terrain.Grass;
                int index = region.Index(actor.Position);
                return index >= 0 && index < region.Tiles.Length ? region.Tiles[index] : Terrain.Grass;
            };
    }
    public override void _Process(double delta) => Trace?.Frame(delta);
    public override void _ExitTree() { presentation?.Dispose(); Trace?.Dispose(); }
    string? courseSnapshot;
    public bool InFeelCourse => courseSnapshot != null;
    public void BeginFeelCourse()
    {
        if(courseSnapshot==null)courseSnapshot=Persistence.Capture(World,Narrative);
        else Persistence.Restore(World,Narrative,courseSnapshot);
        var map=GameplayReplay.CreateCourse(); World.RegionsById[map.Id]=map;World.InitializeContainers(map);
        var p=World.Player;p.Region=map.Id;p.X=300;p.Y=300;p.Health=p.MaxHealth;p.Mana=p.MaxMana;p.State=ActionState.Idle;p.StateRemaining=0;p.PendingAbility="";p.Statuses.Clear();p.Cooldowns.Clear();
        TouchMovement=ControllerVelocity=Vector2.Zero; Ui.Stick.Cancel();RebuildViews();Ui.ClosePanels();ResumeGame();Trace.Marker("course/start-or-reset");
    }
    public string CombatCourseId { get; private set; } = "";
    public void BeginCombatCourse() => BeginCombatCourse("mixed", true);
    public void BeginCombatCourse(string encounter, bool protectedPlayer, ulong seed=42)
    {
        BeginFeelCourse();
        CombatCourseId=encounter;
        World.Seed=checked((int)seed);
        var p=World.Player;
        if(protectedPlayer)p.Health=p.MaxHealth=10000;
        p.Invulnerable=protectedPlayer ? 10000 : 0;
        World.RegionsById[p.Region].Features.Clear();
        foreach(var other in World.Actors.Values.Where(a=>a.Id!="player"))other.Region="reach";
        string[] profiles=encounter switch {
            "prowler" => new[]{"prowler"}, "brute" => new[]{"brute"},
            "spitter" or "projectiles" => new[]{"spitter"},
            "two melee" => new[]{"prowler","brute"},
            "four mixed" => new[]{"prowler","brute","spitter","prowler"},
            "crowd" => Enumerable.Range(0,8).Select(i=>new[]{"prowler","brute","spitter"}[i%3]).ToArray(),
            _ => new[]{"prowler","brute","spitter"}
        };
        for(int i=0;i<profiles.Length;i++)
        {
            string id=profiles[i];
            float x=380+(i/3)*42,y=275+(i%3)*30;
            if(id=="spitter")x+=65;
            var a=new ActorState{Id="lab:"+id+(i<3 ? "" : ":"+i),Name=id,Region=p.Region,Faction="beasts",Profile="beast",CombatProfile=id,
                Appearance=new(){Family=id=="prowler" ? "quadruped" : id=="brute" ? "reptile" : "amorphous",Seed=seed+(ulong)i},
                X=x,Y=y,HomeX=x,HomeY=y,Health=id=="prowler"?65:id=="brute"?120:55,MaxHealth=id=="prowler"?65:id=="brute"?120:55,
                Abilities=World.Content.CombatProfiles.Single(c=>c.Id==id).Priorities.Keys.ToList()};
            AnatomyAdapter.Apply(a);a.Speed=id=="prowler"?88:id=="brute"?46:62;World.Actors[a.Id]=a;
        }
        Trace.Marker($"combat-course/{encounter}/seed-{seed}/{(protectedPlayer ? "protected" : "normal")}");
        Ui.Notify($"Combat lab: {encounter} · {(protectedPlayer ? "protected observation" : "normal health")} · reset / return through Feel lab");
        RebuildViews();
        if(OS.HasFeature("android"))GD.Print("MOSSLIGHT_TACTICAL_LAB profiles=prowler,brute,spitter");
    }
    public void LeaveFeelCourse()
    {
        if(courseSnapshot==null)return;
        Persistence.Restore(World,Narrative,courseSnapshot);courseSnapshot=null;lastRuntimeLog=World.Tick;
        TouchMovement=ControllerVelocity=Vector2.Zero;Ui.Stick.Cancel();RebuildViews();Ui.ClosePanels();ResumeGame();Trace.Marker("course/return");
    }
    bool suspended;
    bool sessionEntered;
    // A real simulation tick also establishes a session, regardless of which UI
    // route dismissed the initial menu. Loaded progress is eligible for recovery.
    public bool SessionStarted => sessionEntered || (World != null && World.Tick > 0);
    public void ResumeGame() { sessionEntered = true; Ui.HideMenu(); if(OS.HasFeature("android"))GD.Print($"MOSSLIGHT_SESSION_STARTED tick={World.Tick}"); }
    public Vector2 TouchMovement { get; set; }
    public bool CanManageItems => !suspended && World.Player.Alive && !Narrative.Get("speaking").AsBool() && !ui.MenuOpen;
    public bool ManageItem(Func<bool> operation) => CanManageItems && operation();
    public bool UseQuickItem(string selector) => !GameplayBlocked && World.UseItem(World.Player, selector);
    bool showCharacters = true;
    public void ShowCharacters(bool show)
    {
        showCharacters = show;
        foreach (var view in Views.Values) view.Visible = show && view.State.Region == World.Player.Region;
        foreach (var feature in features) feature.Visible = show;
    }
    Node items = null !, planner = null !;
    RpgUi ui = null !;
    Camera2D camera = null !;
    Node2D actors = null !;
    Dictionary<string, Texture2D> art = new();
    string drawnRegion = "";
    readonly List<FeatureView> features = new();
    readonly List<TerrainWallRow> wallRows = new();
    readonly List<EnvironmentDecoration> decorations = new();
    public bool CollisionDebug { get; set; }
    public double SimulationMs { get; private set; }
    Node2D ground = null !;
    TelegraphView? telegraphs;
    public CombatEffects CombatEffects { get; private set; } = null!;
    public CameraFeedback CombatCamera { get; } = new();
    public CombatPresentationController? CombatPresentation => presentation;
    public Node Audio => audio;
    CombatPresentationController? presentation;
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
            "dodge",
            Key.Space
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
        AddChild(new PlayerLocator { App=this,ZIndex=50 });
        ground = new Node2D
        {
            ZIndex = -10
        };
        AddChild(ground);
        camera = new Camera2D
        {
            Zoom = new Vector2(1.5f, 1.5f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 12,
            // Godot's idle-frame smoothing can extrapolate on long frames;
            // fixed physics ticks retain the follow speed without that overshoot.
            ProcessCallback = Camera2D.Camera2DProcessCallback.Physics
        };
        AddChild(camera);
        audio = ScriptNode("res://game/audio/AudioService.gd");
        AddChild(audio);
        CombatEffects = new CombatEffects(); // after actors, below the later UI canvas
        AddChild(CombatEffects);
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

        Trace = new PlaytestTrace(this);
        Reset();
        ui = new RpgUi
        {
            App = this
        };
        AddChild(ui);
        AddChild(new PlaytestInputObserver { Trace=Trace });
        Narrative.Connect("notice", Callable.From<string>(text => ui.Notify(text)));
        Narrative.Set("deliver_reward", Callable.From<string, int, bool>((item, count) => World.Items.GrantReward(item, count)));
        Narrative.Connect("reputation", Callable.From<string, int>((faction, amount) =>
        {
            World.Player.Reputation[faction] = World.Player.Reputation.GetValueOrDefault(faction) + amount;
            World.Events.Publish(new("reputation_changed", "player", "", faction, amount));
        }));
        GD.Print($"MOSSLIGHT_READY actors={World.Actors.Count} families={CreatureRuntime.Registry.Families.Count()} framework={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} source={BuildIdentity.Source}");
        var preset=OS.GetCmdlineUserArgs().FirstOrDefault(x=>x.StartsWith("--preset="))?.Split('=',2)[1];
        SelectFeel(MovementFeel.Presets.FirstOrDefault(x=>x.Id==preset) ?? MovementFeel.Presets[1]);
        if(OS.GetCmdlineUserArgs().Contains("--validate-landmarks"))
        { GD.Print($"LANDMARK VALIDATION PASSED: {Landmarks.Library.Count} templates");GetTree().Quit();return; }
        var dump=OS.GetCmdlineUserArgs().FirstOrDefault(x=>x.StartsWith("--dump-dungeons="))?.Split('=',2)[1];
        if(dump!=null)
        { foreach(int seed in dump.Split(',').Select(int.Parse))GD.Print(DungeonLayout.Ascii(Regions.Dungeon(seed)));GetTree().Quit();return; }
        if(OS.GetCmdlineUserArgs().Contains("--gameplay-check")) replay=new GameplayReplay(this);
        if (OS.GetCmdlineUserArgs().Contains("--adventure-visual")) CallDeferred(nameof(RunAdventureVisual));
        if (OS.GetCmdlineUserArgs().Contains("--adventure-check")) CallDeferred(nameof(RunAdventure));
        if (OS.GetCmdlineUserArgs().Contains("--test"))
            CallDeferred(nameof(RunChecks));
        if (OS.GetCmdlineUserArgs().Contains("--visual-check"))
            CallDeferred(nameof(RunVisualChecks));
    }

    public void Reset()
    {
        courseSnapshot=null;
        sessionEntered = false;
        lastRuntimeLog=0;
        accumulator = 0;
        TouchMovement = Vector2.Zero;
        if (World != null)
        {
            items.Call("import_state", "{\"serial\":0,\"bags\":{},\"slots\":{}}");
            Narrative.Call("import_state", "{\"flags\":{},\"pools\":{},\"quests\":{}}");
        }

        presentation?.Dispose();
        World = new(Content.Load(VerificationMode), items)
        {
            Planner = planner
        };
        World.Events.Published += e =>
        {
            if (OS.HasFeature("android") && e.Kind is "resource_depleted" or "region_entered" or "landmark_discovered")
                GD.Print($"MOSSLIGHT_GAMEPLAY kind={e.Kind} value={e.Value} tick={World.Tick}");
            if(OS.HasFeature("android") && e.Kind=="ability_started" && e.Actor=="player")
                GD.Print($"MOSSLIGHT_PLAYER_ABILITY id={e.Value} tick={World.Tick}");
            if (OS.HasFeature("android") && e.Kind == "damage" && e.Target == "player")
                GD.Print($"MOSSLIGHT_PLAYER_DAMAGE tick={World.Tick} health={World.Player.Health:0.0}");
            if(Trace.Active && e.Kind is "region_entered" or "resource_depleted" or "ability_started" or "defeated")Trace.Marker(e.Kind+"/"+e.Value);
            Narrative.Call("receive", e.Kind, e.Actor, e.Target, e.Value, e.Count);
            if (e.Kind == "region_entered")
                RebuildViews();
            if (e.Kind == "item_equipped" && Views.TryGetValue(e.Actor, out var changed))
            {
                changed.Equipment(items.Call("equipment_appearance", e.Actor).AsString());
                changed.RestoreWindup(World);
            }
        };
        World.CombatDiagnostic += (kind, direction, context) =>
        {
            if (Trace.Active) Trace.Write(new { kind = "combat", action = kind, tick = World.Tick, x = direction.X, y = direction.Y, context });
            if (OS.HasFeature("android") && (kind is "attack_started" or "attack_completed" or "dodge_started" or "dodge_completed"))
                GD.Print($"MOSSLIGHT_COMBAT kind={kind} tick={World.Tick}");
        };
        World.Presentation += e =>
        {
            if(OS.HasFeature("android") && InFeelCourse && e.Kind is CombatPresentationKind.ProjectileReleased or CombatPresentationKind.ProjectileImpact)
                GD.Print($"MOSSLIGHT_LAB_PROJECTILE kind={e.Kind} ability={e.AbilityId} outcome={e.Outcome} tick={e.Tick}");
        };
        presentation = new CombatPresentationController(World, Views, CombatEffects, CombatCamera, audio);
        ContentAudit.Validate(World.Content);
        World.NewGame();
        if (telegraphs == null)
        {
            telegraphs = new TelegraphView { World = World, ZIndex = -1 };
            AddChild(telegraphs);
        }
        else telegraphs.World = World;
        RebuildViews();
        drawnRegion = "";
    }

    public void RebuildViews()
    {
        presentation?.Reset();
        if (camera != null) camera.Offset = Vector2.Zero;
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
            ConfigureView(view);
            Views[a.Id] = view;
            view.Equipment(items.Call("equipment_appearance", a.Id).AsString());
        }
        foreach (var view in Views.Values) view.RestoreWindup(World);

        drawnRegion = "";
    }

    public void ChangeAppearance(string family, ulong seed)
    {
        var a = World.Player;
        if (!World.SetAppearance(a, new Appearance { Family = family, Seed = seed }))
        { ui.Notify("No safe position for that body; appearance unchanged"); return; }
        RebuildViews();
    }

    public void MutateAppearance(ulong seed)
    {
        var a = World.Player;
        var genome = CreatureRuntime.Serializer.Deserialize(a.Appearance.GenomeJson).Genome!;
        var appearance = new Appearance
        {
            Family = a.Appearance.Family,
            Seed = seed,
            GenomeJson = CreatureRuntime.Serializer.Serialize(GenomeOps.Mutate(genome, new MutationSettings(), seed))
        };
        if (!World.SetAppearance(a, appearance))
        { ui.Notify("No safe position for that body; appearance unchanged"); return; }
        RebuildViews();
    }

    bool adventureVisual;
    public void RebuildViewsIfNeededForAdventure()
    {
        if(Views.Count!=World.Actors.Count || Views.Any(v=>!World.Actors.TryGetValue(v.Key,out var state) || !ReferenceEquals(state,v.Value.State))) RebuildViews();
    }
    public async void RunAdventureVisual()
    {
        adventureVisual=true;
        try { await AdventureVisualRun.Run(this); GetTree().Quit(0); }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (ui == null)
            return;
        try { replay?.BeforeStep(); } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); return; }
        if (!GameplayBlocked && !adventureVisual)
        {
            DroppedSeconds += Math.Max(0,delta-.1);
            accumulator += Math.Min(delta, .1);
            while (accumulator >= 1.0 / 60)
            {
                var v = (Input.GetVector("west", "east", "north", "south") + TouchMovement).LimitLength();
                // Input describes screen direction; PPC projects ground south at half height.
                ScreenInput=v;
                if (World.Player.State == ActionState.Dodging) ControllerVelocity=Vector2.Zero;
                else if (GameplayBlocked || v == Vector2.Zero || Feel.Response <= 0) ControllerVelocity=v;
                else ControllerVelocity=ControllerVelocity.Lerp(v,1-MathF.Exp(-(1f/60)/Feel.Response));
                var groundInput = Feel.Convert(ControllerVelocity);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                World.PlayerMove(new(groundInput.X, groundInput.Y), 1f / 60, Feel.Scale, Feel.ScreenUniform);
                World.Step(1f / 60);
                SimulationMs = timer.Elapsed.TotalMilliseconds;
                accumulator -= 1.0 / 60;
            }
        }
        else
        { accumulator = 0; ControllerVelocity=Vector2.Zero; ScreenInput=Vector2.Zero; if(!adventureVisual)World.CancelPlayerDodge(); World.ClearPlayerMotion(); }
        presentation?.FlushPending();
        var player = World.Player;
        if (PresentationBlocked) CombatCamera.Reset();
        else { CombatEffects.Advance((float)delta); CombatCamera.Advance((float)delta); }
        foreach (var(id, view)in Views)
        {
            if (!World.Actors.TryGetValue(id, out var live)) { view.Visible = false; continue; }
            view.Visible = showCharacters && live.Region == player.Region;
            // A player death blocks gameplay but must not freeze the defeat pose.
            view.Creature.Paused = !view.Visible || PresentationBlocked || (!player.Alive && view.State.Alive);
            view.HasLoot = !view.State.Alive && World.Items.HasLoot(id);
            view.SyncCombatFacing(World);
            view.Sync((float)delta);
        }

        var region = World.RegionsById[player.Region];
        var bounds = GodotConvert.GroundToScreen(region.Width * 24, region.Height * 24);
        var half = GetViewportRect().Size / camera.Zoom / 2;
        var focus = GodotConvert.GroundToScreen(player.X, player.Y);
        camera.Position = new Vector2(bounds.X <= half.X*2 ? bounds.X/2 : Mathf.Clamp(focus.X,half.X,bounds.X-half.X), bounds.Y <= half.Y*2 ? bounds.Y/2 : Mathf.Clamp(focus.Y,half.Y,bounds.Y-half.Y));
        camera.Offset = CombatCamera.Confine(camera.Position, bounds, half);
        var target = World.InteractionTargets(player).FirstOrDefault();
        foreach (var feature in features)
        {
            int at = feature.GetMeta("tile").AsInt32();
            bool selected = target.Value != null && (at == target.Key || feature.GateCells.Contains(target.Key));
            bool spent = region.Depleted.Contains(at) || (feature.GateCells.Length > 0 ? feature.GateCells.All(region.OpenDoors.Contains) : region.OpenDoors.Contains(at));
            if (feature.Spent != spent) { feature.Spent = spent; feature.QueueRedraw(); }
            if (feature.Selected != selected) { feature.Selected = selected; feature.QueueRedraw(); }
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
        Trace.Sample(delta);
        try { replay?.AfterStep(); }
        catch(Exception error) { GD.PushError(error.ToString()); Trace.Flush(); GetTree().Quit(1); return; }
        QueueRedraw();
    }

    public override void _Input(InputEvent e)
    {
        // Space is also Godot's default UI accept key. Intercept it before a
        // focused hotbar button can consume it during active gameplay; menus
        // and other modal UI keep normal keyboard handling.
        if (e is InputEventKey && e.IsAction("dodge") && !GameplayBlocked)
        {
            if (e.IsActionPressed("dodge")) Dodge();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventKey developerKey && developerKey.Pressed && !developerKey.Echo && (developerKey.PhysicalKeycode==Key.F9 || developerKey.Keycode==Key.F9) && !Narrative.Get("speaking").AsBool())
        { BeginCombatCourse(); GetViewport().SetInputAsHandled(); return; }
        // Development shortcut for the existing Diagnostics entry/return actions.
        // Android OS-key smoke can verify exported-region art without bypassing
        // World.Enter, regenerating saved terrain, or adding gameplay state.
        if(e is InputEventKey regionKey && regionKey.Pressed && !regionKey.Echo &&
            (regionKey.PhysicalKeycode==Key.F10 || regionKey.Keycode==Key.F10) && !Narrative.Get("speaking").AsBool())
        {
            LeaveFeelCourse();Ui.ClosePanels();
            World.Enter(World.Player,World.Player.Region=="ruin"?"reach":"ruin");
            TouchMovement=ControllerVelocity=Vector2.Zero;Ui.Stick.Cancel();RebuildViews();
            BuildTerrain();drawnRegion=World.Player.Region;
            _=EnvironmentAssetCache.Floors;_=EnvironmentAssetCache.Walls;_=EnvironmentAssetCache.Props;ResumeGame();
            var activeRegion=World.RegionsById[World.Player.Region];
            GD.Print($"MOSSLIGHT_LANDMARK_READY region={activeRegion.Id} rooms={activeRegion.Landmarks.Count} structures={activeRegion.Landmarks.Sum(l=>l.Anchors.Count(a=>a.Collision=="terrain"))} templates={string.Join(",",activeRegion.Landmarks.Select(l=>l.Template).Distinct())}");
            GD.Print($"MOSSLIGHT_ENVIRONMENT_READY region={World.Player.Region} walls={wallRows.Count} decorations={decorations.Count} atlases={EnvironmentAssetCache.TextureCount}");
            GetViewport().SetInputAsHandled();return;
        }
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
        if (ability == "slash")
        {
            ui.Notify(World.TryStartPlayerSlash(VisualFacing()) ? d.Name : "Cannot attack: action, equipment, mana or cooldown");
            return;
        }
        var t = d.Self ? a : World.Actors.Values.Where(x => World.CanActivate(a, x, d)).OrderBy(x => NVector.DistanceSquared(a.Position, x.Position)).FirstOrDefault();
        ui.Notify(t != null && World.Activate(a.Id, ability, t.Id) ? d.Name : "No valid target, equipment, mana or cooldown");
    }

    public void Dodge()
    {
        if (GameplayBlocked) return;
        // TouchMovement is dead-zone processed but has not passed through response smoothing.
        var intent = (Input.GetVector("west", "east", "north", "south") + TouchMovement).LimitLength();
        var ground = GodotConvert.ScreenToGround(intent);
        var direction = intent.LengthSquared() > .000001f ? new NVector(ground.X, ground.Y) : VisualFacing();
        if (World.TryStartPlayerDodge(direction)) ControllerVelocity = Vector2.Zero;
    }

    // PPC yaw maps to east/south ground coordinates; projection to screen is separate.
    public NVector VisualFacing()
    {
        double yaw = Views.TryGetValue("player", out var view) && view.Creature.Motor != null
            ? view.Creature.Motor.Yaw : PixelCamera.DirectionYaw(7);
        if (!double.IsFinite(yaw)) yaw = PixelCamera.DirectionYaw(7);
        return new NVector((float)Math.Cos(yaw), (float)-Math.Sin(yaw));
    }

    // Shared by Interact (dialogue) and the pack (Buy), so both use one range.
    public ActorState? NearbyMerchant() => World.Actors.Values.FirstOrDefault(x => x.Alive && x.Profession == "merchant" && x.Region == World.Player.Region && NVector.Distance(x.Position, World.Player.Position) < 65 && !World.Hostile(World.Player, x) && World.LineOfSight(World.Player, x));
    public bool HasInteraction() => World.InteractionTargets(World.Player).Any() || World.LootTargets(World.Player).Any() || NearbyMerchant() != null;

    public void Interact()
    {
        if (GameplayBlocked)
            return;
        var a = World.Player;
        var npc = NearbyMerchant();
        if (npc != null && !World.InteractionTargets(a).Any() && !World.LootTargets(a).Any())
        {
            Narrative.Call("talk");
            return;
        }

        World.Interact(a);
        ui.Notify(World.InteractionFeedback);
        if (OS.HasFeature("android")) GD.Print($"MOSSLIGHT_INTERACT feedback={World.InteractionFeedback}");
        drawnRegion = "";
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused || what == NotificationApplicationFocusOut)
        {
            Trace?.Marker("lifecycle/pause"); Trace?.Flush();
            TouchMovement = Vector2.Zero;
            ui?.Stick.Cancel();
            ui?.CancelActionTouches();
            if (OS.HasFeature("android")) GD.Print("MOSSLIGHT_INPUT_CANCELLED");
            suspended = true;
            if (World != null && ui != null)
            {
                World.CancelPlayerDodge();
                ControllerVelocity = Vector2.Zero;
                ui.PauseForVerification();
                // Never replace the recovery autosave with a defeated player.
                try { if (SessionStarted && !VerificationMode && replay == null && !InFeelCourse && World.Player.Alive) Persistence.SaveAutosave(World, Narrative); }
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
        foreach (var row in wallRows) { actors.RemoveChild(row); row.QueueFree(); }
        wallRows.Clear();
        wallRows.AddRange(TerrainWallRow.Create(r));
        foreach (var row in wallRows) actors.AddChild(row);
        foreach(var decoration in decorations) { actors.RemoveChild(decoration); decoration.QueueFree(); }
        decorations.Clear();
        decorations.AddRange(EnvironmentDecoration.Create(r,art));
        foreach(var decoration in decorations)actors.AddChild(decoration);
        foreach (var feature in features) { actors.RemoveChild(feature); feature.QueueFree(); }
        features.Clear();
        var gates = r.GateGroups();
        foreach (var (at, kind) in r.Features)
        {
            int[] gate = kind == "lock" ? gates.Single(g => Array.BinarySearch(g, at) >= 0) : Array.Empty<int>();
            if (gate.Length > 0 && gate[0] != at) continue;
            var pos = r.Center(at);
            var feature = new FeatureView { Kind = kind, Art = art, VisualSeed = EnvironmentStyle.Hash(r.Seed,at%r.Width,at/r.Width), GateCells = gate, Visible = showCharacters,
                GateOffsets = gate.Select(cell => GodotConvert.GroundToScreen(r.Center(cell).X-pos.X, r.Center(cell).Y-pos.Y)).ToArray(),
                Spent = r.Depleted.Contains(at) || r.OpenDoors.Contains(at), Position = GodotConvert.GroundToScreen(pos.X,pos.Y) };
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
            foreach(var (at,kind) in r.Features)
            {
                var point=r.Center(at);
                if(NVector.DistanceSquared(point,player.Position)>260*260)continue;
                DrawSetTransform(GodotConvert.GroundToScreen(point.X,point.Y),0,new Vector2(1,.5f));
                DrawArc(Vector2.Zero,48,0,Mathf.Tau,24,new Color(1,.7f,.2f,.35f),1);
                DrawCircle(Vector2.Zero,2,Colors.Gold);DrawSetTransform(Vector2.Zero);
            }
            var playerScreen=GodotConvert.GroundToScreen(player.X,player.Y);
            var desired=World.RequestedVelocity*.2f;
            var accepted=World.ActualVelocity*.2f;
            var requestedEnd=playerScreen+GodotConvert.GroundToScreen(desired.X,desired.Y);
            var acceptedEnd=playerScreen+GodotConvert.GroundToScreen(accepted.X,accepted.Y);
            DrawLine(playerScreen,requestedEnd,Colors.DodgerBlue,3);
            DrawLine(playerScreen,acceptedEnd,Colors.LimeGreen,2);
            DrawLine(acceptedEnd,requestedEnd,Colors.Red,1);
            var input=World.LastInput*40;
            DrawLine(playerScreen,playerScreen+GodotConvert.GroundToScreen(input.X,input.Y),World.LastBlockedReason=="" ? Colors.Gold : Colors.Red,2);
            foreach(var actor in World.Actors.Values.Where(a=>a.Region==player.Region))
                DrawRect(new Rect2(GodotConvert.GroundToScreen(actor.HomeX,actor.HomeY)-new Vector2(2,2),new Vector2(4,4)),Colors.Magenta);
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
            if ((p.Region == "" ? World.Actors[p.Source].Region : p.Region) != r.Id) continue;
            var at = GodotConvert.GroundToScreen(p.X, p.Y);
            var heading = GodotConvert.GroundToScreen(p.HeadingX, p.HeadingY).Normalized();
            if (heading.LengthSquared() < .001f) heading = Vector2.Right;
            bool organic = World.FindAbility(p.Ability)?.Projectile?.Visual == "spit";
            var color = organic ? new Color(.45f, 1, .25f) : new Color(1, .48f, .12f);
            // A bounded velocity-aligned streak reads authoritative flight; it owns no timer.
            DrawLine(at - heading * 15, at, new Color(color, .35f), organic ? 5 : 3);
            if(organic)DrawCircle(at,5.5f,new Color(.9f,.88f,.63f,.9f));
            DrawCircle(at, organic ? 4 : 3.5f, new Color(.08f, .12f, .06f));
            DrawCircle(at, organic ? 3 : 2.5f, color);
            DrawCircle(at + heading, 1.3f, organic ? Colors.LightYellow : Colors.White);
        }
    }

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

    public void RunAdventure()
    {
        try { new AdventureRun(this).Run(); GetTree().Quit(0); }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
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
    public static double LastBuildMilliseconds { get; private set; }
    public static long LastVertexBytes { get; private set; }
    public override void _ExitTree() { foreach(var batch in batches)batch.Dispose(); }
    readonly List<TerrainBatch> batches=new();
    public RegionState Region { get; set; } = null !;
    public Dictionary<string, Texture2D> Art { get; set; } = null !;

    public override void _Draw()
    {
        var buildTimer = System.Diagnostics.Stopwatch.StartNew();
        foreach(var oldBatch in batches)oldBatch.Dispose();
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
                var origin = GodotConvert.GroundToScreen(x * 24, y * 24);
                batch.Rect(new Rect2(origin, new Vector2(24, 12)), color);
            }

        batch.Draw(this);
        var floorBatch=new TerrainBatch { Texture=EnvironmentAssetCache.Floors };batches.Add(floorBatch);
        for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;x++)
            if(Region.Tiles[y*Region.Width+x]==Terrain.Floor)
            {
                int variant=(int)(EnvironmentStyle.Hash(Region.Seed,x,y)&15);
                floorBatch.Sprite(new Rect2(GodotConvert.GroundToScreen(x*24,y*24),new Vector2(24,12)),new Rect2(variant*24,0,24,12),EnvironmentThemes.Tint(EnvironmentThemes.At(Region,x,y)));
            }
        floorBatch.Draw(this);
        var landmarkGround = new TerrainBatch(); batches.Add(landmarkGround);
        foreach(var room in Region.Landmarks)
            foreach(var anchor in room.Anchors.Where(a=>a.Collision=="cosmetic"))
                EnvironmentArtGenerator.GroundDetail(landmarkGround,GodotConvert.GroundToScreen((room.OriginX+anchor.X+.5f)*24,(room.OriginY+anchor.Y+.5f)*24),anchor,room.Theme);
        landmarkGround.Draw(this);
        batch=new TerrainBatch();batches.Add(batch);
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

        if(Art.TryGetValue("rock1",out var rock))
        {
            var rockBatch=new TerrainBatch { Texture=rock };batches.Add(rockBatch);
            for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;x++)
                if(Region.Tiles[y*Region.Width+x]==Terrain.Rock)
                    rockBatch.Sprite(new Rect2(GodotConvert.GroundToScreen(x*24,y*24)+new Vector2(0,-7),new Vector2(24,19)),new Rect2(Vector2.Zero,rock.GetSize()));
            rockBatch.Draw(this);
        }
        for (int y = 0; y < Region.Height; y++)
            for (int x = 0; x < Region.Width; x++)
            {
                int at=y*Region.Width+x;
                var tile=Region.Tiles[at];
                var p=GodotConvert.GroundToScreen(x*24,y*24);
                // Raised dungeon walls live in TerrainWallRow beside the actors.
                // Natural rocks retain their established overworld rendering here.
                if(tile==Terrain.Water)
                {
                    if(y>0 && Region.Tiles[at-Region.Width]!=Terrain.Water) batch.Line(p,p+new Vector2(24,0),new Color("b2bea0"),2);
                    if(y<Region.Height-1 && Region.Tiles[at+Region.Width]!=Terrain.Water) batch.Line(p+new Vector2(0,12),p+new Vector2(24,12),new Color("b2bea0"),2);
                    if(x>0 && Region.Tiles[at-1]!=Terrain.Water) batch.Line(p,p+new Vector2(0,12),new Color("b2bea0"),2);
                    if(x<Region.Width-1 && Region.Tiles[at+1]!=Terrain.Water) batch.Line(p+new Vector2(24,0),p+new Vector2(24,12),new Color("b2bea0"),2);
                }
            }
        batch.Draw(this);
        LastVertexBytes=batches.Sum(b=>b.VertexBytes);
        LastBuildMilliseconds = buildTimer.Elapsed.TotalMilliseconds;
    }
}
