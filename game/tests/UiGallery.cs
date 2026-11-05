using Godot;
namespace Mosslight;

/// <summary>
/// Design review gallery: renders every player-facing screen in representative states
/// at a phone-sized and a compact window into build/ui-gallery. Run with
/// <c>godot --path . -- --visual-check --mobile-ui --ui-gallery</c>. Restores the world afterwards.
/// </summary>
public static class UiGallery
{
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var ui = app.Ui; var w = app.World; var n = app.Narrative;
        var window = app.GetWindow(); var originalSize = window.Size;
        string original = Persistence.Capture(w, n);
        string directory = "build/ui-gallery";
        Directory.CreateDirectory(directory);
        async System.Threading.Tasks.Task Frames(int count = 6)
        { for (int i = 0; i < count; i++) await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame); }
        string sizeTag = "";
        async System.Threading.Tasks.Task Shot(string name)
        {
            if (name != "hud") ui.Notify(""); // Setup feedback would otherwise cover the screen under review.
            await Frames(6);
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"{directory}/{(ui.MobileLayout ? "mobile" : "desktop")}-{sizeTag}-{name}.png");
        }
        string? only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--gallery-only="))?.Split('=', 2)[1];
        if (OS.GetCmdlineUserArgs().Contains("--gallery-perf"))
        {
            async System.Threading.Tasks.Task<double> Measure(string label)
            {
                await Frames(10);
                ulong start = Time.GetTicksUsec();
                for (int i = 0; i < 120; i++) await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                double ms = (Time.GetTicksUsec() - start) / 120.0 / 1000.0;
                GD.Print($"PERF {label}: {ms:0.00} ms/frame");
                return ms;
            }
            app.ResumeGame(); await Measure("gameplay");
            ui.PauseForVerification(); await Measure("menu");
            ui.PackTab = 0; ui.Inventory(); await Measure("pack");
            var scrimNode = (Control)ui.FindChild("scrim", true, false)!;
            scrimNode.Visible = false; await Measure("pack-no-scrim"); scrimNode.Visible = true;
            ui.Journal(); await Measure("journal");
            ui.Character(); await Measure("character");
            ui.ClosePanels(); app.ResumeGame(); await Measure("gameplay-again");
            return;
        }
        bool Want(string name) => only == null || only.Split(',').Any(name.StartsWith);
        try
        {
            var sizes = OS.GetCmdlineUserArgs().Contains("--gallery-compact") ? new[] { new Vector2I(900, 560) } :
                OS.GetCmdlineUserArgs().Contains("--gallery-all") ? new[] { new Vector2I(1280, 800), new Vector2I(900, 560), new Vector2I(1920, 864) } : new[] { new Vector2I(1280, 800) };
            foreach (var size in sizes)
            {
                window.Size = size; sizeTag = $"{size.X}x{size.Y}"; await Frames();
                var viewport = app.GetViewport().GetVisibleRect();
                ui.VerificationSafeArea = ui.MobileLayout ? new Rect2(viewport.Position + new Vector2(48, 20), viewport.Size - new Vector2(96, 40)) : null;
                Persistence.Restore(w, n, original); app.RebuildViews();
                var p = w.Player; p.Invulnerable = 1000; p.Region = "reach"; p.State = ActionState.Idle; p.Grasps = true;
                n.Call("accept_quest", 1); n.Call("receive", "defeated", "player", "", "beast", 1);
                w.Items.Add("player", "ore", 3); w.Items.Add("player", "wood", 2); w.Items.Add("player", "herb", 2);
                p.SkillPoints = 2; p.Experience = 64; p.Health = p.MaxHealth * .78f;
                app.ResumeGame(); ui.Layout(); ui.InvalidateHud(); await Frames();

                if (Want("hud"))
                {
                    var reach = w.RegionsById["reach"];
                    var chest = reach.Features.First(f => f.Value == "chest" && !reach.Depleted.Contains(f.Key)).Key;
                    var at = reach.Center(chest); p.X = at.X; p.Y = at.Y + 20; app.RebuildViews();
                    ui.InvalidateHud(); ui.Notify("Discovered: Forgotten Archive\nQuest stage advanced");
                    await Shot("hud");
                    p.Cooldowns["slash"] = .5f; p.Cooldowns["ember"] = 2.2f; p.Mana = 4;
                    ui.InvalidateHud(); ui.Notify(""); await Shot("hud-cooldowns");
                    p.Cooldowns.Clear(); p.Mana = p.MaxMana;
                }
                if (Want("menu")) { ui.PauseForVerification(); await Shot("menu"); }
                if (Want("confirm")) { ui.PauseForVerification(); ui.Confirm("Start a new game? Unsaved progress will be lost. Saved slots are retained.", "Start new game", () => { }, "new_game", refreshScreen: false); await Shot("confirm"); ui.Back(); }
                if (Want("pack"))
                {
                    ui.PauseForVerification(); ui.PackTab = 0; ui.PackSelection = "potion"; ui.Inventory(); await Shot("pack-items");
                    ui.PackState.SelectEquipped("weapon", w.Items.EquippedSelector("player", "weapon")); ui.Inventory(); await Shot("pack-equipped");
                    ui.PackTab = 1; ui.CraftSelection = "forge_sword"; ui.Inventory(); await Shot("pack-craft");
                    var merchant = w.Actors.Values.First(a => a.Profession == "merchant");
                    p.Region = merchant.Region; p.X = merchant.X + 20; p.Y = merchant.Y;
                    ui.PackTab = 2; ui.Inventory(); await Shot("pack-trade");
                    ui.PackTab = 0;
                }
                if (Want("character"))
                {
                    foreach (var (tab, name) in new[] { (1, "stats"), (2, "train"), (3, "abilities"), (0, "hero") })
                    { ui.CharacterTab = tab; ui.Character(); await Shot("character-" + name); }
                }
                if (Want("journal"))
                {
                    n.Call("receive", "defeated", "player", "", "beast", 1); n.Call("accept_quest", 2); w.Enter(p, "ruin");
                    ui.JournalSelection = 2; ui.JournalReading = false; ui.Journal(); await Shot("journal");
                    ui.JournalReading = true; ui.Journal(); await Shot("journal-reading");
                }
                if (Want("map"))
                {
                    if (p.Region != "ruin") w.Enter(p, "ruin");
                    ui.MapZoomed = false; ui.MapGuide = false; ui.Map(); await Shot("map-ruin");
                    ui.MapGuide = true; ui.Map(); await Shot("map-guide");
                    ui.MapGuide = false; ui.MapZoomed = true; ui.Map(); await Shot("map-zoom");
                    w.Enter(p, "reach"); ui.MapZoomed = false; ui.Map(); await Shot("map-reach");
                }
                if (Want("save")) { ui.SaveSelection = 1; ui.SaveLoad(); await Shot("save"); }
                if (Want("settings")) { foreach (var page in new[] { 0, 1, 2 }) { ui.SettingsPage = page; ui.Settings(); await Shot("settings-" + page); } }
                if (Want("developer")) { ui.Developer(); await Shot("developer"); }
                if (Want("dialogue"))
                {
                    ui.ClosePanels(); app.ResumeGame(); n.Call("talk"); await Frames(10); await Shot("dialogue");
                    n.Call("import_state", n.Call("export_state"));
                }
                if (Want("contrast"))
                {
                    ui.ApplyPreferences(new UiPreferences { TextSize = 2, HighContrast = true }, persist: false);
                    ui.PackTab = 0; ui.PackSelection = "potion"; ui.Inventory(); await Shot("contrast-pack");
                    ui.PauseForVerification(); await Shot("contrast-menu");
                    ui.ApplyPreferences(new UiPreferences(), persist: false);
                }
                ui.PauseForVerification();
            }
        }
        finally
        {
            if (n.Get("speaking").AsBool()) n.Call("import_state", n.Call("export_state"));
            window.Size = originalSize; ui.VerificationSafeArea = null;
            ui.ApplyPreferences(new UiPreferences(), persist: false);
            Persistence.Restore(w, n, original); app.RebuildViews(); ui.Layout(); ui.Notify(""); ui.PauseForVerification(); await Frames(2);
        }
    }
}
