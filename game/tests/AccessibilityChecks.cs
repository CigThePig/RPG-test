using Godot;
namespace Mosslight;

/// <summary>
/// PR #7 Part 4: settings persistence, save-slot states and confirmations, the
/// player/developer menu split, dialogue presentation and accessibility rules
/// (contrast, target size, focus order, Back, interruption, blocked input).
/// </summary>
public static class AccessibilityChecks
{
    public static double Contrast(Color a, Color b)
    {
        static double Channel(float c) => c <= .03928 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
        static double Luminance(Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    public static void Run(Foundation app, Action<bool, string> check)
    {
        // Contrast of shared tokens (WCAG 4.5:1 for normal text).
        // Every text pairing the theme declares, in both normal and high-contrast palettes.
        bool contrastMode = UiDesign.HighContrast;
        try
        {
            foreach (bool high in new[] { false, true })
            {
                UiDesign.HighContrast = high;
                foreach (var (fg, bg, name) in UiDesign.ContrastPairs())
                    check(Contrast(fg, bg) >= 4.5, $"contrast {(high ? "high-contrast " : "")}{name} {Contrast(fg, bg):0.0}:1 ≥ 4.5:1");
            }
        }
        finally { UiDesign.HighContrast = contrastMode; }

        // Preferences: round trip, damaged file, isolation from world saves.
        string previousPath = UiPreferences.FilePath;
        UiPreferences.FilePath = previousPath.Replace("ui_preferences.json", "prefs-check.json");
        string file = ProjectSettings.GlobalizePath(UiPreferences.FilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            File.Delete(file);
            check(UiPreferences.Load(out var none).TextSize == 0 && none == null, "missing preferences use defaults silently");
            new UiPreferences { TextSize = 2, Muted = true, ReducedMotion = true, HighContrast = true }.Save();
            var loaded = UiPreferences.Load(out _);
            check(loaded.TextSize == 2 && loaded.Muted && loaded.ReducedMotion && loaded.HighContrast && loaded.TextScale == 1.4f, "preferences round-trip");
            File.WriteAllText(file, "{not json");
            check(UiPreferences.Load(out var damaged).TextSize == 0 && damaged != null && File.ReadAllText(file) == "{not json", "damaged preferences fall back to defaults and are not overwritten");
            File.WriteAllText(file, "{\"Version\":9,\"TextSize\":2}");
            check(UiPreferences.Load(out var future).TextSize == 0 && future != null, "unknown preference version is ignored");
            check(!Persistence.Capture(app.World, app.Narrative).Contains("HighContrast") && !Persistence.Capture(app.World, app.Narrative).Contains("ReducedMotion"), "world saves contain no UI preferences");
        }
        finally { File.Delete(file); UiPreferences.FilePath = previousPath; }

        // Save-slot inspection uses load validation and never mutates files.
        string previousSlots = Persistence.SlotDirectory;
        Persistence.SlotDirectory = previousSlots + "slot-check/";
        string dir = ProjectSettings.GlobalizePath(Persistence.SlotDirectory);
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var content = app.World.Content;
            check(Persistence.Inspect(Persistence.SlotPath(1), content).State == SlotState.Empty, "empty slot");
            Persistence.Save(app.World, app.Narrative, 1);
            var ready = Persistence.Inspect(Persistence.SlotPath(1), content);
            check(ready.State == SlotState.Ready && ready.Level == app.World.Player.Level && ready.Describe().StartsWith("Level"), "compatible save summarised from validated data");
            var old = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Persistence.SlotPath(1)))!; old["Generator"] = "older-generator";
            File.WriteAllText(Persistence.SlotPath(2), old.ToJsonString());
            string oldText = File.ReadAllText(Persistence.SlotPath(2));
            check(Persistence.Inspect(Persistence.SlotPath(2), content).State == SlotState.Incompatible, "older generator is incompatible, not damaged");
            bool refused = false; try { Persistence.Save(app.World, app.Narrative, 2); } catch (InvalidDataException) { refused = true; }
            check(refused && File.ReadAllText(Persistence.SlotPath(2)) == oldText, "older adventure is never overwritten");
            File.WriteAllText(Persistence.SlotPath(3), "{\"Schema\":1,\"Generator\":\"" + Regions.Version + "\",\"Actors\":{}}");
            check(Persistence.Inspect(Persistence.SlotPath(3), content).State == SlotState.Damaged, "current-version file failing validation is damaged");
            // Valid JSON can still contain invalid types/nulls. Inspection must
            // classify it as damaged instead of aborting the entire slot screen.
            string validSave = File.ReadAllText(Persistence.SlotPath(1));
            foreach (string malformed in new[]
            {
                "{\"Generator\":\"" + Regions.Version + "\",\"Schema\":\"one\"}",
                "{\"Generator\":\"" + Regions.Version + "\",\"Schema\":null}",
                "{}"
            })
            {
                File.WriteAllText(Persistence.SlotPath(3), malformed);
                check(Persistence.Inspect(Persistence.SlotPath(3), content).State == SlotState.Damaged
                    && File.ReadAllText(Persistence.SlotPath(3)) == malformed, "malformed metadata is damaged and unchanged");
            }
            foreach (string property in new[] { "Actors", "Regions", "Events", "Projectiles" })
            {
                var malformed = System.Text.Json.Nodes.JsonNode.Parse(validSave)!;
                malformed[property] = null;
                File.WriteAllText(Persistence.SlotPath(3), malformed.ToJsonString());
                check(Persistence.Inspect(Persistence.SlotPath(3), content).State == SlotState.Damaged,
                    "null " + property + " is damaged instead of throwing");
            }
            string liveBeforeInspection = Persistence.Capture(app.World, app.Narrative);
            int retainedNumber = 0;
            foreach (string property in new[] { "Items", "Narrative" })
            {
                foreach (string? badSnapshot in new string?[] { null, "", "{}", "not json" })
                {
                    var damagedSave = System.Text.Json.Nodes.JsonNode.Parse(validSave)!;
                    if (badSnapshot == null) damagedSave.AsObject().Remove(property);
                    else damagedSave[property] = badSnapshot;
                    string damagedText = damagedSave.ToJsonString();
                    File.WriteAllText(Persistence.SlotPath(3), damagedText);
                    check(Persistence.Inspect(Persistence.SlotPath(3), content).State == SlotState.Damaged
                        && File.ReadAllText(Persistence.SlotPath(3)) == damagedText
                        && Persistence.Capture(app.World, app.Narrative) == liveBeforeInspection,
                        property + " invalid snapshot is damaged without changing file or live state");
                    Persistence.ReplaceDamaged(app.World, app.Narrative, 3);
                    check(File.ReadAllText(Persistence.SlotPath(3) + ".damaged-" + (++retainedNumber) + ".json") == damagedText,
                        property + " damaged snapshot retained on replacement");
                }
            }
            File.WriteAllText(Persistence.SlotPath(3), "{truncated");
            check(Persistence.Inspect(Persistence.SlotPath(3), content).State == SlotState.Damaged && File.ReadAllText(Persistence.SlotPath(3)) == "{truncated", "corrupt file is damaged and untouched by inspection");
            Persistence.ReplaceDamaged(app.World, app.Narrative, 3);
            check(Persistence.Inspect(Persistence.SlotPath(3), content).State == SlotState.Ready && File.ReadAllText(Persistence.SlotPath(3) + ".damaged-9.json") == "{truncated", "replacing a damaged slot keeps a copy of it");
            refused = false; try { Persistence.ReplaceDamaged(app.World, app.Narrative, 1); } catch (InvalidOperationException) { refused = true; }
            check(refused, "only damaged slots can be replaced without confirmation of overwrite");
            string malformedRecovery = "{\"Generator\":\"" + Regions.Version + "\",\"Schema\":\"broken\"}";
            File.WriteAllText(Persistence.AutosavePath, malformedRecovery);
            Persistence.SaveAutosave(app.World, app.Narrative);
            check(Persistence.Inspect(Persistence.AutosavePath, content).State == SlotState.Ready
                && File.ReadAllText(Persistence.AutosavePath + ".retained-1.json") == malformedRecovery,
                "malformed autosave metadata is retained before writing a usable recovery save");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); Persistence.SlotDirectory = previousSlots; }
    }

    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        async System.Threading.Tasks.Task Frames(int count = 4)
        { for (int i = 0; i < count; i++) await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Require(bool value, string text) { if (!value) throw new InvalidOperationException("Part 4: " + text); }
        var ui = app.Ui; var w = app.World; var n = app.Narrative;
        var window = app.GetWindow(); var originalSize = window.Size;
        string original = Persistence.Capture(w, n);
        string prefix = ui.MobileLayout ? "mobile-" : "";
        string slotDir = ProjectSettings.GlobalizePath(Persistence.SlotDirectory);
        Button Find(string name)
        {
            var button = ui.FindChild(name, true, false) as Button;
            Require(button != null && button.IsVisibleInTree(), "missing control " + name);
            return button!;
        }
        void Tap(Button button)
        {
            var at = button.GetGlobalRect().GetCenter();
            app.GetViewport().InjectLocal(new InputEventScreenTouch { Index = 16, Pressed = true, Position = at });
            app.GetViewport().InjectLocal(new InputEventScreenTouch { Index = 16, Pressed = false, Position = at });
        }
        async System.Threading.Tasks.Task Press(string name)
        {
            var button = Find(name);
            Require(!button.Disabled, name + " should be enabled");
            var scroll = ui.FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>().FirstOrDefault(s => s.IsVisibleInTree() && s.IsAncestorOf(button));
            if (scroll != null) { scroll.EnsureControlVisible(button); await Frames(3); }
            if (ui.MobileLayout) Tap(button); else button.EmitSignal(Button.SignalName.Pressed);
            await Frames();
        }
        async System.Threading.Tasks.Task Accept() { ui.ConfirmationButton("confirm_accept")!.EmitSignal(Button.SignalName.Pressed); await Frames(); }
        async System.Threading.Tasks.Task Cancel() { ui.ConfirmationButton("confirm_cancel")!.EmitSignal(Button.SignalName.Pressed); await Frames(); }
        async System.Threading.Tasks.Task Shot(string name)
        {
            await Frames(4);
            Directory.CreateDirectory("docs/native-screenshots");
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"docs/native-screenshots/{prefix}{name}.png");
        }
        try
        {
            if (Directory.Exists(slotDir)) Directory.Delete(slotDir, true);

            // Menu: player entries only, ≥56 targets, complete keyboard focus order.
            ui.PauseForVerification(); await Frames();
            var menuButtons = ui.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree() && ui.MenuOpen && ui.FindChild("pause_content", true, false)!.IsAncestorOf(b)).ToList();
            Require(menuButtons.Count >= 10, "menu lists its entries");
            string[] developerOnly = { "Creature workshop", "Performance overlay", "Game-feel lab", "Developer diagnostics", "Collision" };
            Require(!menuButtons.Any(b => developerOnly.Any(d => b.Text.StartsWith(d))), "developer tools are not mixed into the player menu");
            Require(menuButtons.All(b => b.Size.Y >= UiDesign.Target), "menu targets are at least 56 units tall");
            var visited = new HashSet<Control>(); Control? at = menuButtons[0];
            for (int i = 0; i < 40 && at != null && visited.Add(at); i++) at = at.FindNextValidFocus();
            Require(menuButtons.All(visited.Contains), "Tab focus order reaches every menu entry");
            await Shot("menu-part5");
            await Press("menu_quit");
            Require(ui.Navigation.Confirmation && ui.ConfirmationText.Contains("Unsaved progress"), "Quit requires confirmation");
            await Cancel(); Require(ui.MenuOpen, "cancel Quit returns to pause");

            // Developer tools remain reachable behind one entry; Back returns to the menu.
            await Press("menu_developer");
            Require(ui.Navigation.Screen == "DEVELOPER TOOLS" && ui.FindChild("dev_workshop", true, false) != null && ui.FindChild("dev_feel", true, false) != null && ui.FindChild("dev_diagnostics", true, false) != null, "developer screen holds workshop, feel lab and diagnostics");
            await Shot("developer-tools");
            ui.Back(); await Frames(); Require(ui.MenuOpen, "developer Back returns to the menu");

            // Settings persist immediately and apply to presentation only.
            string prefsFile = ProjectSettings.GlobalizePath(UiPreferences.FilePath);
            string world = Persistence.Capture(w, n);
            await Press("menu_settings");
            await Press("set_text");
            Require(ui.Preferences.TextSize == 1 && Math.Abs(ui.TextScale - 1.3f) < .01f && UiPreferences.Load(out _).TextSize == 1, "text size changes and persists");
            await Press("set_contrast");
            Require(UiDesign.HighContrast && UiPreferences.Load(out _).HighContrast, "high contrast applies and persists");
            await Shot("settings-display-part5");
            await Press("settings_comfort");
            await Press("set_motion");
            Require(app.CombatCamera.Strength == 0 && UiPreferences.Load(out _).ReducedMotion, "reduced motion removes camera nudges");
            await Shot("settings-comfort-part5");
            await Press("settings_audio");
            await Press("set_sound");
            Require(AudioServer.IsBusMute(0) && UiPreferences.Load(out _).Muted, "sound preference mutes and persists");
            Require(Persistence.Capture(w, n) == world, "changing settings leaves world state untouched");
            await Shot("settings");
            Persistence.Restore(w, n, world);
            Require(ui.Preferences.HighContrast && ui.Preferences.TextSize == 1, "loading a world keeps UI preferences");
            await Press("set_reset");
            Require(ui.Navigation.Confirmation, "restoring defaults asks first");
            await Accept();
            Require(ui.Preferences.TextSize == 0 && !UiDesign.HighContrast && !AudioServer.IsBusMute(0) && app.CombatCamera.Strength == 1 && File.Exists(prefsFile), "defaults restored and saved");
            ui.Back(); await Frames(); Require(ui.MenuOpen, "settings Back returns to the menu");

            // Save & load with explicit states and confirmations.
            await Press("menu_save_load");
            Require(Find("load_1").Disabled && Find("save_1").Text.StartsWith("Save to slot 1"), "empty slot offers Save only");
            await Press("save_1");
            Require(File.Exists(Persistence.SlotPath(1)) && !ui.Navigation.Confirmation && Find("load_1") is { Disabled: false }, "saving to an empty slot needs no confirmation");
            var stamp = File.GetLastWriteTimeUtc(Persistence.SlotPath(1));
            await Press("save_1");
            Require(ui.Navigation.Confirmation && ui.ConfirmationText.StartsWith("Overwrite slot 1"), "overwriting an existing save asks first");
            await Cancel();
            Require(File.GetLastWriteTimeUtc(Persistence.SlotPath(1)) == stamp && ui.Navigation.Screen == "SAVE & LOAD", $"Cancel keeps the existing save: stamp {File.GetLastWriteTimeUtc(Persistence.SlotPath(1)) == stamp} screen {ui.Navigation.Screen} confirm {ui.Navigation.Confirmation} menu {ui.MenuOpen}");
            int level = w.Player.Level; w.Player.Level = level + 5;
            await Press("load_1");
            Require(ui.Navigation.Confirmation && ui.ConfirmationText.Contains("will be lost"), "loading asks before discarding progress");
            await Accept();
            Require(w.Player.Level == level && !ui.BlocksInput, "confirmed load restores the slot and resumes play");
            var old = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Persistence.SlotPath(1)))!; old["Generator"] = "older-generator";
            File.WriteAllText(Persistence.SlotPath(2), old.ToJsonString());
            File.WriteAllText(Persistence.SlotPath(3), "{\"Generator\":\"" + Regions.Version + "\",\"Schema\":\"broken\"}");
            ui.PauseForVerification(); ui.SaveLoad(); await Frames();
            await Press("choose_slot_2");
            Require(Find("load_2").Disabled && Find("save_2").Disabled, "older-version slot can be neither loaded nor overwritten");
            await Press("choose_slot_3");
            Require(Find("load_3").Disabled && Find("save_3").Text.StartsWith("Replace damaged"), "damaged slot cannot load and offers an explicit replacement");
            await Shot("save-load");
            await Press("save_3");
            Require(ui.Navigation.Confirmation && ui.ConfirmationText.Contains("copied aside"), "replacing a damaged slot explains the kept copy");
            await Accept();
            Require(Persistence.Inspect(Persistence.SlotPath(3), w.Content).State == SlotState.Ready && File.Exists(Persistence.SlotPath(3) + ".damaged-1.json"), "damaged slot replaced, original kept");
            ui.Back(); await Frames();

            // A real lifecycle pause must write only to the verification folder.
            // Never seed, replace or delete a player's actual recovery file, even in a test.
            string playerAutosave = ProjectSettings.GlobalizePath("user://autosave.json");
            byte[]? previousAutosave = File.Exists(playerAutosave) ? File.ReadAllBytes(playerAutosave) : null;
            Require(Persistence.AutosavePath.StartsWith(slotDir, StringComparison.Ordinal), "autosave is inside the isolated slot directory");
            app.ResumeGame();
            app._Notification((int)Node.NotificationApplicationFocusOut);
            app._Notification((int)Node.NotificationApplicationFocusIn);
            Require(previousAutosave == null ? !File.Exists(playerAutosave)
                : File.Exists(playerAutosave) && File.ReadAllBytes(playerAutosave).SequenceEqual(previousAutosave),
                "lifecycle pause preserves the player's real recovery save");
            Require(Persistence.Inspect(Persistence.AutosavePath, w.Content).State == SlotState.Ready, "lifecycle pause writes an isolated usable autosave");

            // Dialogue: themed, safe, focusable, uninterruptible by Back, blocks gameplay input.
            app.ResumeGame(); await Frames();
            foreach (var size in new[] { new Vector2I(1280, 800), new Vector2I(900, 560), new Vector2I(2340, 1080) })
            {
                window.Size = size; await Frames();
                var viewport = app.GetViewport().GetVisibleRect();
                var safe = new Rect2(viewport.Position + new Vector2(60, 28), viewport.Size - new Vector2(120, 56));
                ui.VerificationSafeArea = safe; ui.SetTextScale(1.4f);
                n.Call("talk"); await Frames(8);
                var panel = (Control)n.Get("ui").AsGodotObject();
                Require(safe.Encloses(panel.GetGlobalRect()), $"{size} dialogue inside the safe area");
                var choices = ((VBoxContainer)n.Get("choices").AsGodotObject()).GetChildren().OfType<Button>().Where(b => b.Visible).ToList();
                Require(choices.Count > 0 && choices.All(b => b.Size.Y >= UiDesign.Target && b.GetGlobalRect().End.X <= panel.GetGlobalRect().End.X), $"{size} responses are large and inside the panel");
                Require(app.GetViewport().GuiGetFocusOwner() == choices[0], $"{size} first response has keyboard focus");
                await Shot($"dialogue-{size.X}x{size.Y}-largest");
                // Back and Escape never dismiss dialogue; app interruption keeps it.
                ui.SystemBack(); app.GetViewport().InjectLocal(new InputEventKey { Keycode = Key.Escape, Pressed = true }); await Frames();
                app._Notification((int)Node.NotificationApplicationFocusOut); app._Notification((int)Node.NotificationApplicationFocusIn); await Frames();
                Require(n.Get("speaking").AsBool() && !ui.MenuOpen && panel.Visible, $"{size} dialogue survives Back and focus loss");
                // A tap where Attack normally lives cannot cause combat. The wider
                // dialogue may put a legitimate reply there, so refresh reply references afterwards.
                int attacks = 0; void Count(string kind, System.Numerics.Vector2 d, string c) { if (kind == "attack_started") attacks++; }
                w.CombatDiagnostic += Count;
                var attackAt = MobileCombatControls.Geometry(safe)["attack"].GetCenter();
                app.GetViewport().InjectLocal(new InputEventScreenTouch { Index = 17, Pressed = true, Position = attackAt });
                app.GetViewport().InjectLocal(new InputEventScreenTouch { Index = 17, Pressed = false, Position = attackAt });
                await Frames(); w.CombatDiagnostic -= Count;
                Require(attacks == 0, $"{size} gameplay actions blocked during dialogue");
                for (int i = 0; i < 20 && n.Get("advancing").AsBool(); i++) await Frames();
                if (n.Get("speaking").AsBool())
                {
                    var goodbye = ((VBoxContainer)n.Get("choices").AsGodotObject()).GetChildren().OfType<Button>().LastOrDefault(b => b.Visible && !b.Disabled);
                    if (goodbye != null)
                    {
                        if (ui.MobileLayout)
                        {
                            ((ScrollContainer)n.Get("choice_scroll").AsGodotObject()).EnsureControlVisible(goodbye); await Frames(3);
                            Tap(goodbye);
                        }
                        else goodbye.EmitSignal(Button.SignalName.Pressed);
                    }
                }
                for (int i = 0; i < 20 && n.Get("speaking").AsBool(); i++)
                {
                    await Frames(2);
                    var next = ((VBoxContainer)n.Get("choices").AsGodotObject()).GetChildren().OfType<Button>().FirstOrDefault(b => b.Visible && !b.Disabled);
                    next?.EmitSignal(Button.SignalName.Pressed);
                }
                Require(!n.Get("speaking").AsBool(), $"{size} dialogue closes through its own responses");
                ui.PauseForVerification(); app.ResumeGame(); await Frames();
            }
            window.Size = new Vector2I(900, 560); await Frames();
            var compactViewport = app.GetViewport().GetVisibleRect();
            ui.VerificationSafeArea = new Rect2(compactViewport.Position + new Vector2(60, 28), compactViewport.Size - new Vector2(120, 56));
            ui.ApplyPreferences(new UiPreferences { TextSize = 2, HighContrast = true }, persist: false);
            ui.PauseForVerification(); await Shot("pause-part5-900x560-largest");
            ui.SettingsPage = 0; ui.Settings(); await Shot("settings-part5-900x560-largest");
            ui.SaveSelection = 3; ui.SaveLoad(); await Shot("saves-part5-900x560-largest");
        }
        finally
        {
            if (n.Get("speaking").AsBool()) n.Call("import_state", n.Call("export_state"));
            window.Size = originalSize; ui.VerificationSafeArea = null;
            ui.ApplyPreferences(new UiPreferences(), persist: true);
            if (Directory.Exists(slotDir)) Directory.Delete(slotDir, true);
            Persistence.Restore(w, n, original); app.RebuildViews(); ui.Layout(); ui.Notify(""); ui.PauseForVerification(); await Frames(2);
        }
    }
}
