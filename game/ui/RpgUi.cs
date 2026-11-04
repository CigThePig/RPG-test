using Godot;
using System.Text.Json;

namespace Mosslight;
public partial class RpgUi : CanvasLayer
{
    public Foundation App { get; set; } = null !;

    readonly Dictionary<string, Button> abilityButtons = new();
    readonly Dictionary<string, Button> actions = new();
    readonly Dictionary<string, Button> navigationTargets = new();
    int layoutTraceFrames;
    bool? lastSystemBackFromKey;
    ulong lastSystemBackAt;
    public Button ActionControl(string id) => actions[id];
    internal Rect2 StatusRect => hud.GetGlobalRect();
    public GameplayHud Hud => hud;
    public InteractionPreview InteractionLabel { get; private set; } = InteractionPreview.None;
    public string NoticeText => message.Text;
    internal Rect2 NoticeRect => message.GetGlobalRect();
    internal bool NoticeVisible => message.Visible;
    internal IEnumerable<Control> HotbarControls => hotbar.GetChildren().OfType<Control>().Where(c => c.IsVisibleInTree()).Append(Stick);
    internal Rect2? VerificationSafeArea { get; set; }
    public MobileCombatControls? CombatControls => hotbar as MobileCombatControls;
    Label message = null !;
    GameplayHud hud = null!;
    readonly Dictionary<string, ColorRect> cooldownShades = new();
    readonly Dictionary<string, int> actionKeys = new();
    readonly Dictionary<Button, string> unavailableReasons = new();
    UiScreenHost panel = null!;
    PanelContainer menu = null!, confirmation = null!;
    readonly UiNavigation navigation = new();
    public UiNavigation Navigation => navigation;
    public float TextScale { get; private set; } = 1;
    public Rect2 ScreenRect => panel.GetGlobalRect();
    internal Vector2 PackAvailableSize { get; private set; }
    public Rect2 DialogueRect { get; private set; }
    public Button ScreenBack => panel.BackButton;
    internal PackViewState PackState { get; } = new();
    PackLayout? packLayout;
    internal int CharacterTab;
    CharacterSheet? characterSheet;
    VBoxContainer body = null !;
    Control root = null !;
    Control hotbar = null !;
    public TouchStick Stick { get; private set; } = null!;
    Label metrics = null!;
    bool showMetrics;
    bool labVisible;
    Button marker = null!;
    public bool MobileLayout { get; set; } = OS.HasFeature("mobile") || OS.GetCmdlineUserArgs().Contains("--mobile-ui");
    readonly Queue<double> frames = new();
    public bool MenuOpen => menu.Visible;
    public bool BlocksInput => navigation.Blocked;
    public bool SettingsOpen { get; private set; }
    public bool Muted => Preferences.Muted;
    public int SaveSlot { get; private set; } = 1;

    public override void _Ready()
    {
        root = new Control
        {
            LayoutMode = 0,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(root);
        // Verification runs use their own preferences file and always start from defaults,
        // so a developer's settings can neither change test evidence nor be overwritten.
        if (App.VerificationMode)
        {
            // Separate folders per layout: desktop and phone suites may run at the same time.
            string verification = "user://verification/" + (MobileLayout ? "mobile/" : "desktop/");
            UiPreferences.FilePath = verification + "ui_preferences.json";
            Persistence.SlotDirectory = verification;
            string verificationPreferences = ProjectSettings.GlobalizePath(UiPreferences.FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(verificationPreferences)!);
            try { File.Delete(verificationPreferences); } catch (IOException) { }
        }
        Preferences = UiPreferences.Load(out var preferencesProblem);
        PreferencesProblem = preferencesProblem;
        UiDesign.HighContrast = Preferences.HighContrast;
        root.Theme = UiDesign.Theme(TextScale);
        hud = new GameplayHud { Name = "GameplayHud" };
        root.AddChild(hud);
        message = new Label
        {
            Visible = false,
            Position = new Vector2(24, 730),
            Size = new Vector2(1200, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        message.MaxLinesVisible = 2;
        message.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        message.AddThemeFontSizeOverride("font_size", 17);
        message.AddThemeStyleboxOverride("normal", UiDesign.Notification());
        root.AddChild(message);
        hotbar = MobileLayout ? new MobileCombatControls() : new GridContainer { Columns = 5 };
        hotbar.Position = new Vector2(500, 640);

        root.AddChild(hotbar);
        foreach (var(actionId, text, action)in new (string, string, Action)[]
        {
            ("attack", "1 · Sword", () => App.Cast("slash")),
            ("dodge", "Space · Dodge", App.Dodge),
            ("ember", "2 · Ember", () => App.Cast("ember")),
            ("mend", "3 · Mend", () => App.Cast("mend")),
            ("potion", "4 · Draught", () => App.UseQuickItem("potion")),
            ("interact", "E · Interact", App.Interact),
            ("inventory", "I · Pack", Inventory),
            ("journal", "J · Journal", Journal),
            ("character", "C · Character", Character),
            ("map", "M · Map", Map),
            ("menu", "Esc · Menu", Menu)
        }

        )
        {
            if (MobileLayout && actionId is "journal" or "character" or "map") continue;
            if (!MobileLayout && actionId == "dodge") continue;
            var button = Button(hotbar, MobileLayout ? text.Split(" · ").Last() : text, action);
            if (!MobileLayout)
            {
                // Fixed face: contextual labels and cooldowns never resize the grid cells.
                // Two label lines fit without growth (72 = 2 × 16px lines + face margins).
                button.CustomMinimumSize = new Vector2(140, 72);
                button.AutowrapMode = TextServer.AutowrapMode.Off; button.ClipText = true;
                button.AddThemeFontSizeOverride("font_size", 16);
            }
            actions[actionId] = button;
            button.Name = "action_" + actionId;
            if (hotbar is MobileCombatControls mobile) mobile.Register(actionId, button);
            if (actionId == "dodge") abilityButtons["dodge"] = button;
            var id = actionId switch { "attack" => "slash", "ember" => "ember", "mend" => "mend", _ => "" };
            if (actionId is "attack" or "dodge" or "ember" or "mend")
            {
                // Cooldown shade is a child, so it never changes the button's touch rectangle.
                var shade = new ColorRect { Color = new Color(0, 0, 0, .45f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
                button.AddChild(shade); cooldownShades[actionId == "attack" ? "slash" : actionId] = shade;
            }
            if (id != "")
            {
                abilityButtons[id] = button;
                var def = App.World.Content.Abilities.Single(a => a.Id == id);
                button.TooltipText = $"{def.Name} · mana {def.Cost} · range {def.Range}";
            }
        }

        panel = new UiScreenHost { Visible = false };
        root.AddChild(panel);
        panel.Build(Back);
        navigationTargets["screen_back"] = panel.BackButton;
        body = panel.Body;
        body.ChildEnteredTree += child =>
        {
            if (child is Label label) { label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; }
        };
        menu = new PanelContainer
        {
            Position = new Vector2(380, 160),
            CustomMinimumSize = new Vector2(560, 430)
        };
        root.AddChild(menu);
        BuildMenu();
        confirmation = new PanelContainer { Visible = false };
        root.AddChild(confirmation);
        Stick = new TouchStick { App = App, Size = new Vector2(200, 200) };
        root.AddChild(Stick);
        metrics = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        root.AddChild(metrics);
        marker=Button(root,"Mark issue",()=> { App.Trace.Marker("player-observation"); Notify("Marked this moment in playtest trace"); });
        marker.Position=new Vector2(1040,110); marker.Visible=false;
        GetViewport().SizeChanged += Layout;
        Layout();
        ApplyPreferences(Preferences, persist: false);
    }

    readonly Dictionary<int, Button> touchButtons = new();
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape || key.Keycode == Key.Back))
        { SystemBack(fromKey: true); GetViewport().SetInputAsHandled(); return; }
        // Hotbar touches are dispatched below for independent multitouch ownership.
        // Godot also synthesizes a mouse click for the same finger; consuming that
        // event prevents a second native Button.Pressed without disabling mouse
        // emulation for menus, dialogue, scroll panels or actual desktop mice.
        if (e is InputEventMouse mouse && mouse.Device == InputEvent.DeviceIdEmulation && hotbar.IsVisibleInTree() && hotbar.GetChildren().OfType<Button>().Any(b => b.IsVisibleInTree() && b.GetGlobalRect().HasPoint(mouse.Position)))
        { GetViewport().SetInputAsHandled(); return; }
        if (e is InputEventScreenDrag drag && touchButtons.ContainsKey(drag.Index))
        { GetViewport().SetInputAsHandled(); return; }
        if (e is not InputEventScreenTouch touch) return;
        // Death blocks gameplay but leaves the hotbar visible; its Menu button must
        // still dispatch, and every gameplay command already rejects a dead player.
        if (App.PresentationBlocked) { CancelActionTouches(); return; }
        if (touch.Canceled)
        {
            if (touchButtons.Remove(touch.Index,out var canceled)) { canceled.Modulate=Colors.White; GetViewport().SetInputAsHandled(); }
            return; // Unowned cancellation must still reach the joystick or native controls.
        }
        if (touch.Pressed)
        {
            var button = hotbar.GetChildren().OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && b.GetGlobalRect().HasPoint(touch.Position));
            if (button == null) return;
            if (button.Disabled)
            {
                // A disabled control never dispatches; it explains itself instead and does
                // not take finger ownership, so its release cannot fire anything later.
                if (unavailableReasons.TryGetValue(button, out var reason)) Notify(reason, minor: true);
                GetViewport().SetInputAsHandled();
                return;
            }
            touchButtons[touch.Index] = button;
            button.Modulate=new Color(.7f,.8f,.8f);
            GetViewport().SetInputAsHandled();
        }
        else if (touchButtons.Remove(touch.Index, out var button))
        {
            button.Modulate=Colors.White;
            if (button.IsVisibleInTree() && !button.Disabled && button.GetGlobalRect().HasPoint(touch.Position))
                button.EmitSignal(Godot.Button.SignalName.Pressed);
            GetViewport().SetInputAsHandled();
        }
        else if (!touch.Pressed && hotbar.IsVisibleInTree() && actions.Values.Any(b => b.IsVisibleInTree() && b.GetGlobalRect().HasPoint(touch.Position)))
        {
            // A canceled owner may release after focus/modal restoration. Never let
            // that orphaned release reach Button's native touchscreen GUI path.
            GetViewport().SetInputAsHandled();
        }
    }

    public void CancelActionTouches()
    {
        foreach (var held in touchButtons.Values) held.Modulate = Colors.White;
        touchButtons.Clear();
    }
    public void Layout()
    {
        var size = GetViewport().GetVisibleRect().Size;
        CancelGestures();
        hotbar.Position = MobileLayout ? Vector2.Zero : new Vector2(size.X - 760, size.Y - 160);
        hotbar.Size = MobileLayout ? size : new Vector2(740, 140);
        Stick.Position = new Vector2(40, size.Y - 260);
        // Desktop notices sit left of the hotbar instead of underneath its second row.
        message.Position = new Vector2(24, size.Y - 84);
        message.Size = new Vector2(Math.Max(240, hotbar.Position.X - 48), 72);
        hud.Arrange(new Vector2(24, 18), GameplayHud.MaxWidth);
        metrics.Position = new Vector2(size.X - 480, 100);
        if (hotbar is MobileCombatControls mobile)
        {
            var safe = new Rect2(Vector2.Zero, size);
            if (OS.HasFeature("android"))
                safe = MobileCombatControls.ToLogicalSafeArea(safe, DisplayServer.GetDisplaySafeArea(), GetViewport().GetScreenTransform());
            safe=VerificationSafeArea ?? safe;
            mobile.Arrange(safe);
            var pad = MobileCombatControls.Geometry(safe)["stick"];
            Stick.Position=pad.Position; Stick.Size=pad.Size;
            // HUD stays top-left, clear of the top-right utilities and the lower action cluster.
            var utilities = MobileCombatControls.Geometry(safe)["inventory"];
            hud.Arrange(safe.Position+new Vector2(20,14), utilities.Position.X-safe.Position.X-44);
            // Keep notices in the bottom gap between the stick and Attack so the
            // panel never covers a control or its touch feedback.
            var attack = MobileCombatControls.Geometry(safe)["attack"];
            float noticeLeft = pad.End.X + UiDesign.Gap, noticeRight = attack.Position.X - UiDesign.Gap;
            message.Position=new Vector2(noticeLeft,safe.End.Y-84);
            message.Size=new Vector2(Math.Max(160,noticeRight-noticeLeft),72);
            if(OS.HasFeature("android"))
                foreach(var (id, button) in actions)
                {
                    var center=GetViewport().GetScreenTransform()*button.GetGlobalRect().GetCenter();
                    GD.Print($"MOSSLIGHT_CONTROL version={MobileCombatControls.LayoutVersion} id={id} x={center.X:0} y={center.Y:0}");
                }
        }
        var overlaySafe = new Rect2(Vector2.Zero, size);
        if (OS.HasFeature("android")) overlaySafe = MobileCombatControls.ToLogicalSafeArea(overlaySafe, DisplayServer.GetDisplaySafeArea(), GetViewport().GetScreenTransform());
        overlaySafe = VerificationSafeArea ?? overlaySafe;
        PackAvailableSize = new Vector2(Math.Max(0, overlaySafe.Size.X - 32), Math.Max(0, overlaySafe.Size.Y - 32));
        void Arrange(Control control, float width)
        {
            var rect = UiDesign.Overlay(overlaySafe, width);
            control.CustomMinimumSize = Vector2.Zero; control.Position = rect.Position; control.Size = rect.Size;
        }
        if ((navigation.Screen == "PACK & EQUIPMENT" && PackTab == 0) || navigation.Screen == "CHARACTER · WAYFARER")
        {
            panel.CustomMinimumSize = Vector2.Zero;
            panel.Position = overlaySafe.Position + new Vector2(16, 16);
            panel.Size = new Vector2(Math.Max(300, overlaySafe.Size.X - 32),
                Math.Max(240, overlaySafe.Size.Y - 32));
        }
        else Arrange(panel, 740);
        Arrange(menu, 560); Arrange(confirmation, 560);
        // Dialogue: lower part of the safe area, never wider than a comfortable line.
        float dialogueWidth = Math.Min(900, overlaySafe.Size.X - 32);
        float dialogueHeight = Math.Clamp(overlaySafe.Size.Y * .52f, Math.Min(260, overlaySafe.Size.Y - 32), 420);
        DialogueRect = new Rect2(overlaySafe.Position.X + (overlaySafe.Size.X - dialogueWidth) / 2, overlaySafe.End.Y - 16 - dialogueHeight, dialogueWidth, dialogueHeight);
        App.Narrative.Call("present", UiDesign.Theme(TextScale), DialogueRect);
        layoutTraceFrames = 3;
        if (navigation.Screen == "PACK & EQUIPMENT" && PackTab == 0 &&
            packLayout != null && GodotObject.IsInstanceValid(packLayout) && packLayout.IsInsideTree())
            packLayout.Rebuild();
        if (navigation.Screen == "CHARACTER · WAYFARER" && characterSheet != null &&
            GodotObject.IsInstanceValid(characterSheet) && characterSheet.IsInsideTree()) characterSheet.Rebuild();
    }

    static Button Button(Node parent, string text, Action action) => UiDesign.Action(parent, text, action);

    void CancelGestures()
    {
        CancelActionTouches(); Stick.Cancel(); App.TouchMovement = Vector2.Zero;
        App.World.CancelPlayerDodge();
        foreach (var scroll in root.FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
    }
    void SyncNavigation()
    {
        // A closed Pack must not leave invisible ScrollContainers processing
        // queued Android touches. QueueFree is safe even from a Button callback;
        // the next inventory open mounts a fresh presentation from Gloot.
        if (packLayout != null && (navigation.Screen != "PACK & EQUIPMENT" || PackTab != 0))
        {
            if (GodotObject.IsInstanceValid(packLayout) && !packLayout.IsQueuedForDeletion())
            {
                packLayout.Hide();
                packLayout.QueueFree();
            }
            packLayout = null;
        }
        if (characterSheet != null && navigation.Screen != "CHARACTER · WAYFARER")
        {
            if (GodotObject.IsInstanceValid(characterSheet) && !characterSheet.IsQueuedForDeletion())
            { characterSheet.Hide(); characterSheet.QueueFree(); }
            characterSheet = null;
        }
        bool dialogue = App.Narrative.Get("speaking").AsBool();
        panel.Visible = navigation.Screen != null && !navigation.Confirmation && !dialogue;
        menu.Visible = navigation.Menu && !navigation.Confirmation && !dialogue;
        confirmation.Visible = navigation.Confirmation && !dialogue;
        hotbar.Visible = !navigation.Blocked && !App.Narrative.Get("speaking").AsBool();
        if (menu.Visible) menu.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault()?.GrabFocus();
        // Dialogue owns focus (its first response); only release focus for plain gameplay.
        if (!navigation.Blocked && !dialogue) GetViewport().GuiReleaseFocus();
        layoutTraceFrames = 3;
        TraceNavigationState();
    }
    void TraceNavigationState()
    {
        if (OS.HasFeature("android")) GD.Print($"MOSSLIGHT_UI_STATE menu={menu.Visible} screen={panel.Visible} confirmation={confirmation.Visible}");
    }
    void TraceNavigationTargets()
    {
        if (!OS.HasFeature("android")) return;
        TraceNavigationState();
        foreach (var (id, button) in navigationTargets)
        {
            if (!GodotObject.IsInstanceValid(button) || !button.IsVisibleInTree()) continue;
            var center = GetViewport().GetScreenTransform() * button.GetGlobalRect().GetCenter();
            GD.Print($"MOSSLIGHT_UI_TARGET version=1 id={id} x={center.X:0} y={center.Y:0}");
        }
    }
    // Android delivers key/window Back through different queues; the paired
    // event may arrive in the next frame. Coalesce only opposite delivery types.
    public void SystemBack(bool fromKey = false)
    {
        ulong now = Time.GetTicksMsec();
        if (lastSystemBackFromKey.HasValue && lastSystemBackFromKey.Value != fromKey && now - lastSystemBackAt < 250) return;
        lastSystemBackFromKey = fromKey; lastSystemBackAt = now;
        NavigateBack();
    }
    public void Back()
    {
        lastSystemBackFromKey = null;
        NavigateBack();
    }
    void NavigateBack()
    {
        CancelGestures();
        if (navigation.Screen == "PACK & EQUIPMENT" && PackTab == 0 &&
            packLayout != null && GodotObject.IsInstanceValid(packLayout) && packLayout.IsInsideTree() &&
            packLayout.DismissDetail()) return;
        navigation.Back(App.Narrative.Get("speaking").AsBool(), App.World.Player.Alive);
        SyncNavigation();
    }
    public void SetTextScale(float scale)
    {
        TextScale = Math.Clamp(scale, 1, 1.4f);
        panel.Theme = menu.Theme = confirmation.Theme = UiDesign.Theme(TextScale); Layout();
        // Notices follow text size, capped so two lines still fit the notification region.
        message.AddThemeFontSizeOverride("font_size", (int)Math.Min(20, 17 * TextScale));
    }
    void ConfirmNewGame()
    {
        Confirm("Start a new game? Unsaved progress will be lost. Saved slots are retained.", "Start new game",
            () => { App.Reset(); App.ResumeGame(); }, "new_game", refreshScreen: false);
    }

    /// <summary>
    /// Modal confirmation for destructive or irreversible choices. Cancel/Back returns to
    /// the surface underneath (menu or screen) with nothing changed. Confirm runs the action
    /// and, when a screen was underneath, rebuilds it from production state.
    /// </summary>
    public void Confirm(string question, string confirmLabel, Action action, string target = "action", bool refreshScreen = true)
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        lastSystemBackFromKey = null;
        CancelGestures(); navigation.Confirm(); Clear(confirmation);
        var box = new VBoxContainer(); confirmation.AddChild(box);
        box.AddChild(UiDesign.Text(question));
        var cancel = Button(box, "Cancel / Back", Back);
        cancel.Name = "confirm_cancel";
        navigationTargets["cancel_" + target] = cancel;
        var accept = Button(box, confirmLabel, () =>
        {
            string? screen = navigation.Screen;
            navigation.CancelConfirmation();
            action();
            SyncNavigation();
            if (refreshScreen && screen != null && navigation.Screen == screen) RefreshScreen(screen);
        });
        accept.Name = "confirm_accept";
        navigationTargets["confirm_" + target] = accept;
        SyncNavigation(); cancel.GrabFocus();
    }
    public string ConfirmationText => confirmation.GetChildCount() > 0 && confirmation.GetChild(0).GetChildCount() > 0 ? ((Label)confirmation.GetChild(0).GetChild(0)).Text : "";
    public Button? ConfirmationButton(string name) => navigation.Confirmation ? confirmation.FindChild(name, true, false) as Button : null;

    void RefreshScreen(string screen)
    {
        switch (screen)
        {
            case "PACK & EQUIPMENT": Inventory(); break;
            case "QUEST JOURNAL": Journal(); break;
            case "CHARACTER · WAYFARER": Character(); break;
            case "REGION MAP": Map(); break;
            case "SAVE & LOAD": SaveLoad(); break;
            case "SETTINGS": Settings(); break;
        }
    }

    void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    bool Open(string heading)
    {
        if (navigation.Confirmation || App.Narrative.Get("speaking").AsBool()) return false;
        lastSystemBackFromKey = null;
        // Rebuilding the same screen after an action keeps the reader's scroll position.
        bool refresh = navigation.Screen == heading && panel.Visible;
        CancelGestures(); navigation.Open(heading);
        // Present retires the previous custom tree; Layout must never rebuild it.
        packLayout = null;
        characterSheet = null;
        panel.Present(heading == "PACK & EQUIPMENT" && PackTab == 0 ? "THE WAYFARER'S KIT" : heading, refresh);
        SyncNavigation(); return true;
    }

    public override void _Process(double delta)
    {
        // Wait for deferred Container sorting before reporting real physical target centers.
        if (layoutTraceFrames > 0 && --layoutTraceFrames == 0) TraceNavigationTargets();
        frames.Enqueue(delta * 1000);
        while (frames.Count > 600) frames.Dequeue();
    }

    long refreshFrame;
    bool defeatAnnounced, dialogueActive;
    // Notices are transient so the world stays visible; the defeat notice persists
    // while the player remains defeated because it names the only recovery path.
    const ulong NoticeMilliseconds = 4500, NoticeMergeMilliseconds = 1500;
    const string DefeatNotice = "Defeated · open the menu to load a save or start a new game";
    ulong noticeUntil, noticeShownAt;
    bool noticeMinor;
    string? previousNotice;
    /// <summary>
    /// Shows a concise notice. Minor notices (routine action confirmations) never displace
    /// an important one. Important notices arriving together, such as a quest stage and the
    /// loot that caused it, are shown as two lines instead of overwriting each other.
    /// </summary>
    public void Notify(string text, bool minor = false)
    {
        if (string.IsNullOrWhiteSpace(text)) { message.Visible = false; previousNotice = null; return; }
        ulong now = Time.GetTicksMsec();
        bool active = message.Visible && (now < noticeUntil || message.Text.Contains(DefeatNotice));
        if (minor && active && !noticeMinor) return;
        bool merge = !minor && active && !noticeMinor && now - noticeShownAt < NoticeMergeMilliseconds && previousNotice != null && previousNotice != text;
        message.Text = merge ? previousNotice + "\n" + text : text;
        previousNotice = text; noticeMinor = minor;
        message.Visible = true;
        noticeShownAt = now; noticeUntil = now + NoticeMilliseconds;
        objectiveDirty = true;
    }
    bool objectiveDirty = true;
    int objectivePolls;
    /// <summary>Forces the next HUD update to re-read objective, interaction and item state (load, reset, tests).</summary>
    public void InvalidateHud() { objectiveDirty = true; refreshFrame = 0; actionKeys.Clear(); }
    public void Update()
    {
        hotbar.Visible = !BlocksInput && !App.Narrative.Get("speaking").AsBool();
        if (!hotbar.Visible || App.PresentationBlocked) CancelActionTouches();
        bool speaking = App.Narrative.Get("speaking").AsBool();
        bool defeated = !App.World.Player.Alive;
        if ((speaking && !dialogueActive) || (defeated && !defeatAnnounced)) CancelGestures();
        if (dialogueActive != speaking) { dialogueActive = speaking; SyncNavigation(); }
        if (defeated && !defeatAnnounced) Notify(DefeatNotice);
        // A restored or reset player must not keep the stale recovery instruction.
        if (!defeated && defeatAnnounced && message.Text.Contains(DefeatNotice)) message.Visible = false;
        defeatAnnounced = defeated;
        if (message.Visible && !defeated && Time.GetTicksMsec() > noticeUntil) message.Visible = false;
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
        hud.Refresh(a, a.Region, App.World.Time);
        // Queries that cross into Gloot/GDScript or scan actors run every sixth frame;
        // per-frame work only compares cached keys and touches changed controls.
        bool slow = refreshFrame-- <= 0;
        if (slow)
        {
            refreshFrame = 5;
            // Quest notices mark the objective dirty; otherwise re-read about twice a second
            // so load, reset and dialogue outcomes are reflected without event plumbing.
            if (objectiveDirty || ++objectivePolls >= 5) { hud.ShowObjective(QuestObjective.Read(App.Narrative)); objectiveDirty = false; objectivePolls = 0; }
            RefreshInteract();
            RefreshPotion(a);
        }
        RefreshAbilities(a, slow);
    }

    // Short labels fit the fixed mobile buttons; a space becomes a line break there.
    string ButtonLabel(string binding, string name, string detail) => MobileLayout
        ? (detail == "" ? name.Replace(' ', '\n') : name + "\n" + detail)
        : binding + " · " + name + (detail == "" ? "" : "\n" + detail);

    void RefreshInteract()
    {
        var preview = App.PreviewInteract();
        var button = actions["interact"];
        // Never disabled: pressing an unavailable Interact still explains why through the
        // authoritative interaction feedback. The dimmed face and label say so in advance.
        button.SelfModulate = preview.Available ? new Color(1.25f, 1.25f, 1.05f) : new Color(.7f, .74f, .74f);
        if (preview == InteractionLabel && actionKeys.ContainsKey("interact")) return;
        InteractionLabel = preview; actionKeys["interact"] = 1;
        button.Text = ButtonLabel("E", preview.Label, "");
        button.TooltipText = preview.Reason;
    }

    void RefreshPotion(ActorState a)
    {
        var button = actions["potion"];
        int count = App.World.Items.Count("player", "potion");
        int key = HashCode.Combine(count, a.Alive);
        if (actionKeys.TryGetValue("potion", out int old) && old == key) return;
        actionKeys["potion"] = key;
        button.Disabled = count <= 0 || !a.Alive;
        button.Text = ButtonLabel("4", "Draught", count > 0 ? "×" + count : "none");
        SetUnavailable(button, count <= 0 ? "No draughts left · craft one from silverleaf in the Pack" : !a.Alive ? "Defeated" : null);
    }

    void SetUnavailable(Button button, string? reason)
    {
        if (reason == null) unavailableReasons.Remove(button); else unavailableReasons[button] = reason;
    }

    readonly Dictionary<string, AbilityDef> abilityDefs = new();
    object? abilityContent;
    AbilityDef Ability(string id)
    {
        // Reset/load replaces World and its Content; re-resolve definitions then.
        if (!ReferenceEquals(abilityContent, App.World.Content)) { abilityDefs.Clear(); abilityContent = App.World.Content; }
        if (!abilityDefs.TryGetValue(id, out var def)) abilityDefs[id] = def = App.World.Content.Abilities.Single(d => d.Id == id);
        return def;
    }
    /// <summary>Why a visible action control is currently disabled, or null when it is usable.</summary>
    public string? UnavailableReason(string actionId) => unavailableReasons.GetValueOrDefault(actions[actionId]);

    void RefreshAbilities(ActorState a, bool slow)
    {
        foreach (var (id, button) in abilityButtons)
        {
            float cooldown = a.Cooldowns.GetValueOrDefault(id);
            var def = id == "dodge" ? null : Ability(id);
            float total = def == null ? CombatTuning.DodgeCooldown : def.Cooldown;
            if (cooldownShades.TryGetValue(id, out var shade))
            {
                shade.Visible = cooldown > 0;
                if (shade.Visible) { float ratio = Math.Clamp(cooldown / Math.Max(total, cooldown), 0, 1); shade.Position = Vector2.Zero; shade.Size = new Vector2(button.Size.X, button.Size.Y * ratio); }
            }
            bool lowMana = def != null && a.Mana < def.Cost;
            // Target availability is advisory: the button stays enabled and Cast reports the reason.
            bool noTarget = def != null && !def.Self && id != "slash" && (slow ? !HasTarget(a, def) : actionKeys.GetValueOrDefault(id + ":target") == 1);
            if (slow && def != null && !def.Self && id != "slash") actionKeys[id + ":target"] = noTarget ? 1 : 0;
            int state = !a.Alive ? 4 : cooldown > 0 ? 1 : lowMana ? 2 : noTarget ? 3 : 0;
            int tenths = cooldown > 0 ? (int)Math.Ceiling(cooldown * 10) : 0;
            int key = HashCode.Combine(state, tenths);
            if (actionKeys.TryGetValue(id, out int old) && old == key) continue;
            actionKeys[id] = key;
            button.Disabled = state is 1 or 2 or 4;
            string name = id == "dodge" ? "Dodge" : id == "slash" ? (MobileLayout ? "Attack" : def!.Name) : MobileLayout ? def!.Name.Split(' ')[0] : def!.Name;
            string binding = id switch { "slash" => "1", "ember" => "2", "mend" => "3", _ => "Space" };
            string detail = state switch
            {
                1 => $"{cooldown:0.0}s",
                2 => "low mana",
                3 => "no target",
                _ => def != null && id != "slash" && MobileLayout ? $"{def.Cost:0} mana" : ""
            };
            button.Text = ButtonLabel(binding, name, detail);
            SetUnavailable(button, state switch
            {
                1 => $"{name} recovering · {cooldown:0.0}s",
                2 => $"{name} needs {def!.Cost:0} mana · you have {a.Mana:0}",
                4 => "Defeated",
                _ => null
            });
        }
    }

    // Presentation-only: a living hostile in range and sight. Cooldown, mana and action
    // state are reported separately so "no target" never flickers during an attack.
    bool HasTarget(ActorState a, AbilityDef def)
    {
        var world = App.World;
        foreach (var other in world.Actors.Values)
            if (other.Alive && other.Region == a.Region && world.Hostile(a, other)
                && System.Numerics.Vector2.Distance(a.Position, other.Position) <= def.Range + a.NaturalReach && world.LineOfSight(a, other))
                return true;
        return false;
    }

    public void PauseForVerification()
    {
        lastSystemBackFromKey = null;
        CancelGestures(); navigation.Pause(); SyncNavigation();
    }
    public void HideMenu() { lastSystemBackFromKey = null; CancelGestures(); navigation.Resume(); SyncNavigation(); }
    public void ClosePanels() { lastSystemBackFromKey = null; CancelGestures(); navigation.Resume(); SyncNavigation(); }
    /// <summary>Selected Pack tab (0 Items, 1 Craft, 2 Trade). Presentation state only.</summary>
    public int PackTab { get; set; }
    /// <summary>Selected bag row's Gloot selector (instance ID or prototype). Presentation state only.</summary>
    public string? PackSelection { get => PackState.Selector; set => PackState.SelectBag(value); }
    public bool MapZoomed { get; set; }

    public void Inventory()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("PACK & EQUIPMENT")) return;
        Layout(); // Choose the real safe-area geometry after navigation changes.
        if (PackTab == 0)
        {
            packLayout = new PackLayout(this);
            panel.MountCustom(packLayout);
        }
        else
        {
            packLayout = null;
            PackScreen.Populate(body, this);
        }
    }

    /// <summary>Uses a consumable through the gameplay path, then restores the Pack and its caller.</summary>
    public void UseFromPack(string selector)
    {
        bool returnToMenu = navigation.ReturnsToMenu;
        ClosePanels();
        bool used = App.UseQuickItem(selector);
        if (returnToMenu) navigation.Pause();
        Inventory();
        Notify(used ? "Used " + App.World.Items.Title(selector) : App.World.Player.Alive ? "Finish your current action first" : "Defeated");
    }

    public void Journal()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("QUEST JOURNAL")) return;
        JournalScreen.Populate(body, App);
    }

    public void Character()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("CHARACTER · WAYFARER")) return;
        Layout();
        characterSheet = new CharacterSheet(this);
        panel.MountCustom(characterSheet);
    }

    public void Map()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("REGION MAP")) return;
        MapScreen.Populate(body, this);
    }

    public void Menu() => Back();

    void BuildMenu()
    {
        Clear(menu);
        var box = new VBoxContainer();
        var scroll = new TouchScroll { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        scroll.GetVScrollBar().CustomMinimumSize = new Vector2(28,0);
        menu.AddChild(scroll);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(box);
        box.AddThemeConstantOverride("separation", 8);
        box.AddChild(new Label { Text = "MOSSLIGHT", HorizontalAlignment = HorizontalAlignment.Center });
        navigationTargets["resume"] = Button(box, "Enter / Resume", () => { if (App.World.Player.Alive) App.ResumeGame(); else Notify("Defeated · Load a save or start a New game to continue"); });
        foreach (var entry in new (string, Action)[] { ("Pack", Inventory), ("Journal", Journal), ("Character", Character), ("Map", Map), ("Save & load", SaveLoad), ("Settings", Settings) })
            Button(box, entry.Item1, entry.Item2).Name = "menu_" + entry.Item1.ToLowerInvariant().Replace(" & ", "_");
        navigationTargets["new_game"] = Button(box, "New game", ConfirmNewGame);
        // Test, tuning and diagnostic tools stay reachable, but behind one explicit entry.
        Button(box, "Developer tools", Developer).Name = "menu_developer";
        Button(box, "Quit", () => GetTree().Quit());
        if (menu.IsVisibleInTree()) box.GetChildren().OfType<Button>().FirstOrDefault()?.GrabFocus();
        layoutTraceFrames = 3;
    }

    // ---- Save & load (PR #7 Part 4) ----
    string? FeelCourseBlocker(string verb) => App.InFeelCourse ? $"Return from the controlled course before {verb}" : null;

    public void SaveLoad()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("SAVE & LOAD")) return;
        UiDesign.Note(body, "Three slots plus a background autosave. Saves from older Mosslight builds are kept but cannot be loaded here. Settings are stored separately and never change when you load.");
        for (int slot = 1; slot <= 3; slot++)
        {
            int index = slot;
            string path = Persistence.SlotPath(slot);
            var info = Persistence.Inspect(path, App.World.Content);
            var card = UiDesign.Card(body, "slot_" + slot);
            UiDesign.Heading(card, $"SLOT {slot}  ·  {info.State switch { SlotState.Ready => "Saved game", SlotState.Empty => "Empty", SlotState.Incompatible => "Older version", _ => "Damaged" }}");
            card.AddChild(UiDesign.Text(info.Describe()));
            string? saveBlocker = FeelCourseBlocker("saving") ?? (!App.World.Player.Alive ? "Cannot save while defeated" : info.State == SlotState.Incompatible ? "Kept safe · choose another slot to save" : null);
            var save = Button(card, info.State == SlotState.Damaged ? "Replace damaged file (a copy is kept)" : info.State == SlotState.Ready ? $"Overwrite slot {slot}" : $"Save to slot {slot}", () =>
            {
                void Write()
                {
                    try
                    {
                        if (Persistence.Inspect(path, App.World.Content).State == SlotState.Damaged) Persistence.ReplaceDamaged(App.World, App.Narrative, index);
                        else Persistence.Save(App.World, App.Narrative, index);
                        SaveSlot = index; Notify("Saved slot " + index);
                    }
                    catch (Exception e) { Notify(e.Message); }
                }
                if (info.State == SlotState.Empty) { Write(); SaveLoad(); }
                else Confirm(info.State == SlotState.Damaged
                        ? $"Replace the damaged file in slot {index}? The damaged file is copied aside, not deleted."
                        : $"Overwrite slot {index}? The saved game there ({info.Describe()}) will be replaced.",
                    info.State == SlotState.Damaged ? "Replace" : "Overwrite", Write, "save_" + index);
            });
            save.Name = "save_" + slot;
            if (saveBlocker != null) { save.Disabled = true; UiDesign.Note(card, saveBlocker); }
            string? loadBlocker = FeelCourseBlocker("loading") ?? info.State switch
            {
                SlotState.Empty => "Nothing saved here yet", SlotState.Incompatible => "Needs an older Mosslight build", SlotState.Damaged => "This file cannot be loaded", _ => null
            };
            var load = Button(card, $"Load slot {slot}", () => Confirm($"Load slot {index}? Progress since your last save will be lost.", "Load", () => LoadSlot(index), "load_" + index));
            load.Name = "load_" + slot;
            if (loadBlocker != null) { load.Disabled = true; UiDesign.Note(card, loadBlocker); }
        }
        var auto = Persistence.Inspect(Persistence.AutosavePath, App.World.Content);
        var autoCard = UiDesign.Card(body, "slot_autosave");
        UiDesign.Heading(autoCard, "BACKGROUND AUTOSAVE");
        autoCard.AddChild(UiDesign.Text(auto.State == SlotState.Empty ? "None yet · written when the app goes to the background" : auto.Describe()));
        var recover = Button(autoCard, "Recover background autosave", () => Confirm("Recover the background autosave? Progress since then will be lost.", "Recover", () =>
        {
            try { if (App.InFeelCourse) throw new InvalidOperationException("Return from the controlled course before loading"); Persistence.LoadAutosave(App.World, App.Narrative); App.RebuildViews(); App.ResumeGame(); Notify("Recovered autosave"); }
            catch (Exception e) { Notify(e.Message); }
        }, "load_autosave"));
        recover.Name = "load_autosave";
        string? autoBlocker = FeelCourseBlocker("loading") ?? (auto.State == SlotState.Ready ? null : auto.State == SlotState.Empty ? "No autosave yet" : auto.Message);
        if (autoBlocker != null) { recover.Disabled = true; UiDesign.Note(autoCard, autoBlocker); }
    }

    void LoadSlot(int slot)
    {
        try
        {
            if (App.InFeelCourse) throw new InvalidOperationException("Return from the controlled course before loading");
            Persistence.Load(App.World, App.Narrative, slot);
            SaveSlot = slot;
            App.RebuildViews();
            App.ResumeGame();
            InvalidateHud();
            Notify("Loaded slot " + slot);
        }
        catch (Exception e) { Notify(e.Message); }
    }

    // ---- Settings (PR #7 Part 4) ----
    public UiPreferences Preferences { get; private set; } = new();
    public string? PreferencesProblem { get; private set; }

    /// <summary>Applies preferences to presentation only. Never touches world state or saves.</summary>
    public void ApplyPreferences(UiPreferences preferences, bool persist)
    {
        Preferences = preferences.Copy();
        UiDesign.HighContrast = Preferences.HighContrast;
        AudioServer.SetBusMute(0, Preferences.Muted);
        App.CombatCamera.Strength = Preferences.ReducedMotion ? 0 : 1;
        if (Preferences.ReducedMotion) App.CombatCamera.Reset();
        hud?.ApplyContrast(Preferences.HighContrast);
        message?.AddThemeStyleboxOverride("normal", UiDesign.Notification());
        if (persist)
        {
            try { Preferences.Save(); PreferencesProblem = null; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Notify("Settings could not be saved: " + e.Message); }
        }
        SetTextScale(Preferences.TextScale);
        BuildMenu();
    }

    void ChangePreference(Action<UiPreferences> change)
    {
        var next = Preferences.Copy(); change(next);
        ApplyPreferences(next, persist: true);
        Settings();
    }

    public void Settings()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("SETTINGS")) return;
        UiDesign.Note(body, "Saved on this device, separately from game saves. Loading a game never changes them.");
        if (PreferencesProblem != null) UiDesign.Note(body, PreferencesProblem);
        void Option(string name, string title, string state, string explanation, Action<UiPreferences> change)
        {
            var card = UiDesign.Card(body, "setting_" + name);
            UiDesign.Heading(card, title.ToUpperInvariant());
            UiDesign.Note(card, explanation);
            Button(card, $"{title}: {state} · tap to change", () => ChangePreference(change)).Name = "set_" + name;
        }
        Option("text", "Text size", Preferences.TextSizeName, "Menus, screens, dialogue and notices. The HUD keeps a fixed size so it never covers controls.",
            p => p.TextSize = (p.TextSize + 1) % UiPreferences.TextScales.Length);
        Option("sound", "Sound", Preferences.Muted ? "Off" : "On", "Combat and interface sounds.", p => p.Muted = !p.Muted);
        Option("motion", "Reduced motion", Preferences.ReducedMotion ? "On" : "Off", "Removes the camera nudge on impacts. Combat timing, sounds and hit cues are unchanged.", p => p.ReducedMotion = !p.ReducedMotion);
        Option("contrast", "High contrast", Preferences.HighContrast ? "On" : "Off", "Opaque panels, ivory borders and brighter secondary text.", p => p.HighContrast = !p.HighContrast);
        var zoom = UiDesign.Card(body, "setting_zoom");
        UiDesign.Heading(zoom, "CAMERA ZOOM");
        UiDesign.Note(zoom, "Pixel scale of the world view for this session.");
        Button(zoom, "Change camera zoom", () => App.CycleZoom()).Name = "set_zoom";
        var reset = Button(body, "Restore default settings", () => Confirm("Restore default settings? Text size, sound, motion and contrast return to their defaults.", "Restore defaults", () => ApplyPreferences(new UiPreferences(), persist: true), "reset_settings"));
        reset.Name = "set_reset";
    }

    // ---- Developer tools: separated from player navigation, still reachable ----
    public void Developer()
    {
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("DEVELOPER TOOLS")) return;
        UiDesign.Note(body, "Testing, tuning and diagnostic tools. Not part of normal play.");
        Button(body, "Creature workshop", Workshop).Name = "dev_workshop";
        Button(body, "Collision / navigation overlay: " + (App.CollisionDebug ? "On" : "Off"), () => { App.CollisionDebug = !App.CollisionDebug; Developer(); }).Name = "dev_collision";
        Button(body, "Performance overlay: " + (showMetrics ? "On" : "Off"), () => { showMetrics = !showMetrics; Developer(); }).Name = "dev_metrics";
        Button(body, "Game-feel lab / record playtest", FeelLab).Name = "dev_feel";
        Button(body, "Developer diagnostics", Diagnostics).Name = "dev_diagnostics";
    }

    public void FeelLab()
    {
        if (!Open("GAME-FEEL LAB · development tools")) return;
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
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("PROCEDURAL CREATURE WORKSHOP")) return;
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
        if (App.Narrative.Get("speaking").AsBool()) return;
        if (!Open("DEVELOPER TOOLS")) return;
        body.AddChild(new Label { Text = $"Tick {App.World.Tick}\nActors {App.World.Actors.Count}\nRegions {App.World.RegionsById.Count}\nProjectiles {App.World.Projectiles.Count}\nManaged memory {GC.GetTotalMemory(false) / 1048576.0:0.0} MB\nFPS {Engine.GetFramesPerSecond()}\nEvents:\n" + string.Join("\n", App.World.Events.Recent.TakeLast(8).Select(e => $"{e.Kind}: {e.Actor} {e.Value}")) });
        Button(body, "Enter generated dungeon", () =>
        {
            App.World.Enter(App.World.Player, "ruin");
            ClosePanels();
        });
        Button(body, "Return to settlement", () =>
        {
            App.World.Enter(App.World.Player, "reach");
            ClosePanels();
        });
        Button(body, "Combat archetypes test encounter (F9)", App.BeginCombatCourse);
        foreach(var encounter in new[]{"prowler","brute","spitter","two melee","mixed","four mixed","projectiles","crowd"})
        {
            string selected=encounter;
            Button(body, "Combat · " + selected + " · normal health", () => App.BeginCombatCourse(selected,false));
            Button(body, "Combat · " + selected + " · protected observation", () => App.BeginCombatCourse(selected,true));
        }
        if(App.InFeelCourse)Button(body,"Return from combat lab",App.LeaveFeelCourse);
        Button(body, "Complete integration checks", () =>
        {
            App.RunChecks();
            ClosePanels();
        });
    }
}
