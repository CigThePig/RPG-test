using Godot;
namespace Mosslight;

/// <summary>
/// PR #7 Part 2: gameplay HUD, structured quest objective, contextual Interact and
/// action availability. Headless checks use production World/Narrative/Gloot; rendered
/// checks use the actual controls and touch input path.
/// </summary>
public static class HudChecks
{
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World; var n = app.Narrative;
        string original = Persistence.Capture(w, n);
        try
        {
            Persistence.Restore(w, n, original);
            n.Call("import_state", "{\"flags\":{},\"pools\":{},\"quests\":{}}");
            QuestObjective Objective() => QuestObjective.Read(n) ?? throw new InvalidOperationException("objective missing");
            string before = n.Call("export_state").AsString();
            var fresh = Objective();
            check(n.Call("export_state").AsString() == before, "objective read model does not mutate quest state");
            check(fresh.Id == 1 && fresh.Status == "available" && fresh.Objective.Contains("warden"), "fresh game tracks the first quest the warden offers");
            n.Call("accept_quest", 1);
            var counted = Objective();
            check(counted.Status == "active" && counted.Progress == 0 && counted.Required == 2 && !counted.Staged && counted.Heading.Contains("0/2"), "accepted counted quest exposes structured progress");
            n.Call("receive", "defeated", "player", "", "beast", 1);
            check(Objective().Progress == 1 && Objective().Heading.Contains("1/2"), "objective follows authoritative quest progress");
            check(Objective() == Objective(), "unchanged objective compares equal, so the HUD does not relayout");
            n.Call("receive", "defeated", "player", "", "beast", 1);
            check(n.Call("completed", 1).AsBool() && Objective().Id == 2 && Objective().Status == "available", "completed quest advances tracker to the next offer");
            n.Call("accept_quest", 2);
            var staged = Objective();
            check(staged.Staged && staged.StageId == "enter_ruin" && staged.Required == 4 && staged.Heading.Contains("step 1 of 4"), "staged adventure exposes stage identity and count");
            w.Enter(w.Player, "ruin");
            check(Objective().StageId == "recover_relic" && Objective().Objective.StartsWith("Find keys"), "entering the ruin advances the tracked stage");
            string saved = Persistence.Capture(w, n);
            Persistence.Restore(w, n, original);
            check(Objective().StageId != "recover_relic", "restoring another save changes the tracked objective");
            Persistence.Restore(w, n, saved);
            check(Objective().StageId == "recover_relic" && Objective().Id == 2, "objective tracker survives save and load");
            check(!n.Call("journal").AsString().Contains("REWARD WAITING") && n.Call("journal").AsString().Contains("Find keys"), "existing journal API remains intact");

            // Pending reward: a full pack keeps the completed reward waiting.
            Persistence.Restore(w, n, original);
            n.Call("import_state", "{\"flags\":{\"accepted_1\":true},\"pools\":{},\"quests\":{}}");
            var bag = w.Items.Service.Call("bag", "player").AsGodotObject() as Node;
            var limit = GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd").New().AsGodotObject() as Node;
            bag!.AddChild(limit!); limit!.Set("capacity", bag.Call("get_item_count").AsInt32());
            w.Items.Add("player", "coin", 999 - w.Items.Count("player", "coin"));
            try
            {
                n.Call("accept_quest", 3); n.Call("receive", "crafted", "player", "", "draught", 1);
                var pending = Objective();
                check(n.Call("has_pending").AsBool() && pending.Id == 3 && pending.Status == "reward_pending" && pending.PendingReward && pending.Heading.StartsWith("REWARD WAITING"), "unclaimed reward is tracked with its reason");
            }
            finally { bag.RemoveChild(limit); limit.Free(); }

            // Every interaction label must agree with what Interact actually does.
            Persistence.Restore(w, n, original);
            w.RegionsById["ruin"] = Regions.Dungeon(1);
            int compared = 0;
            foreach (var regionId in new[] { "reach", "ruin" })
            {
                var r = w.RegionsById[regionId];
                foreach (var group in r.Features.GroupBy(f => f.Value).OrderBy(g => g.Key, StringComparer.Ordinal))
                    foreach (var (at, kind) in group.OrderBy(f => f.Key).Take(3))
                    {
                        string snapshot = Persistence.Capture(w, n);
                        var p = w.Player;
                        p.Region = regionId; p.State = ActionState.Idle; p.Health = p.MaxHealth;
                        var centre = r.Center(at); p.X = centre.X; p.Y = centre.Y;
                        var preview = w.PreviewInteraction(p);
                        bool acted = w.Interact(p);
                        check(preview.Available == acted, $"{regionId} {kind}: Interact label '{preview.Label}' agrees with action ({w.InteractionFeedback})");
                        if (!acted) check(preview.Label != "" && preview.Reason != "", $"{regionId} {kind}: unavailable interaction names a reason");
                        compared++;
                        Persistence.Restore(w, n, snapshot);
                    }
            }
            check(compared >= 8, "interaction preview compared against real features");
            // Seed 33 carries lever shortcuts and portcullises (see the CI adventure route).
            w.RegionsById["ruin"] = Regions.Dungeon(33);
            int mechanisms = 0;
            var shortcutRuin = w.RegionsById["ruin"];
            foreach (var (at, kind) in shortcutRuin.Features.Where(f => f.Value is "lever" or "portcullis").OrderBy(f => f.Key).Take(6))
            {
                string snapshot = Persistence.Capture(w, n);
                var p = w.Player;
                p.Region = "ruin"; p.State = ActionState.Idle;
                var centre = shortcutRuin.Center(at); p.X = centre.X; p.Y = centre.Y;
                var preview = w.PreviewInteraction(p);
                bool acted = w.Interact(p);
                check(preview.Available == acted, $"ruin {kind}: Interact label '{preview.Label}' agrees with action ({w.InteractionFeedback})");
                mechanisms++;
                Persistence.Restore(w, n, snapshot);
            }
            check(mechanisms > 0, "lever/portcullis previews exercised");
            w.RegionsById["ruin"] = Regions.Dungeon(1);
            // Lock with and without a key: the label must not lie about the outcome.
            var ruin = w.RegionsById["ruin"];
            var gate = ruin.Features.First(f => f.Value == "lock").Key;
            {
                string snapshot = Persistence.Capture(w, n);
                var p = w.Player;
                p.Region = "ruin"; p.State = ActionState.Idle;
                w.Items.Take("player", "dungeon_key", w.Items.Count("player", "dungeon_key"));
                var near = ruin.Center(gate); p.X = near.X; p.Y = near.Y;
                var locked = w.PreviewInteraction(p);
                if (!locked.Available) check(locked.Label == "Need key" && !w.Interact(p) && w.InteractionFeedback == "A ruin key is required", "locked gate without key says Need key and explains");
                w.Items.Add("player", "dungeon_key");
                var unlock = w.PreviewInteraction(p);
                check(unlock.Available && w.Interact(p), $"gate with key offers {unlock.Label} and opens");
                Persistence.Restore(w, n, snapshot);
            }
            // Merchant precedence matches Foundation.Interact.
            var merchant = w.Actors.Values.FirstOrDefault(a => a.Profession == "merchant" && a.Alive);
            if (merchant != null)
            {
                string snapshot = Persistence.Capture(w, n);
                var p = w.Player;
                p.Region = merchant.Region;
                bool found = false;
                foreach (var offset in new[] { new System.Numerics.Vector2(20, 0), new(-20, 0), new(0, 20), new(0, -20), new(30, 30), new(-30, -30) })
                {
                    p.X = merchant.X + offset.X; p.Y = merchant.Y + offset.Y;
                    if (app.NearbyMerchant() == null) continue;
                    found = true;
                    bool competing = w.InteractionTargets(p).Any() || w.LootTargets(p).Any();
                    check(competing ? app.PreviewInteract().Label != "Talk" : app.PreviewInteract().Label == "Talk", "merchant Talk label follows Interact precedence");
                }
                check(found, "merchant reachable for Talk preview");
                Persistence.Restore(w, n, snapshot);
            }
            var dead = w.PreviewInteraction(new ActorState { Id = "ghost", Region = "reach", Health = 0, MaxHealth = 1 });
            check(!dead.Available, "defeated actor has no interaction");
        }
        finally { Persistence.Restore(w, n, original); }
    }

    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        async System.Threading.Tasks.Task Frames(int count = 8)
        { for (int i = 0; i < count; i++) await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Require(bool value, string text) { if (!value) throw new InvalidOperationException("HUD: " + text); }
        var ui = app.Ui; var w = app.World; var n = app.Narrative; var p = w.Player;
        var window = app.GetWindow(); var originalSize = window.Size;
        string original = Persistence.Capture(w, n);
        string prefix = ui.MobileLayout ? "mobile-" : "";
        void Tap(string id)
        {
            var at = ui.ActionControl(id).GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch { Index = 12, Pressed = true, Position = at });
            Input.ParseInputEvent(new InputEventScreenTouch { Index = 12, Pressed = false, Position = at });
        }
        try
        {
            app.ResumeGame();
            p.Invulnerable = 1000; p.Region = "reach"; p.State = ActionState.Idle; app.RebuildViews();
            n.Call("accept_quest", 1); n.Call("receive", "defeated", "player", "", "beast", 1);
            ui.InvalidateHud(); await Frames();
            Require(ui.Hud.ObjectiveHeading.Contains("Trouble in the Reach") && ui.Hud.ObjectiveHeading.Contains("1/2"), "tracker shows production quest progress: " + ui.Hud.ObjectiveHeading);
            Require(ui.Hud.HealthBar.Text.StartsWith("HP") && ui.Hud.ManaBar.Text.StartsWith("MP") && ui.Hud.ExperienceBar.Text.StartsWith("XP"), "bars carry captions and numbers, not colour alone");
            p.Health = p.MaxHealth * .2f; await Frames(2);
            Require(ui.Hud.HealthBar.Warning && ui.Hud.HealthBar.Text.Contains("LOW"), "low health has a text warning");
            p.Health = p.MaxHealth;

            // Layout: HUD inside safe bounds and clear of every control and the notice area.
            foreach (var size in new[] { new Vector2I(1280, 800), new Vector2I(1280, 720), new Vector2I(1920, 864), new Vector2I(1024, 768), new Vector2I(900, 560), new Vector2I(2340, 1080) })
            {
                window.Size = size; await Frames(4);
                var viewport = app.GetViewport().GetVisibleRect();
                var safe = new Rect2(viewport.Position + new Vector2(60, 28), viewport.Size - new Vector2(120, 56));
                ui.VerificationSafeArea = ui.MobileLayout ? safe : null;
                ui.Layout(); ui.Notify("Discovered: Forgotten Archive\nQuest stage advanced"); await Frames(4);
                var hud = ui.StatusRect;
                if (ui.MobileLayout) Require(safe.Encloses(hud), $"{size} HUD inside safe area");
                foreach (var control in ui.HotbarControls.Where(c => c.IsVisibleInTree()))
                    Require(!hud.Intersects(control.GetGlobalRect()), $"{size} HUD clears {control.Name}");
                Require(!hud.Intersects(ui.NoticeRect), $"{size} HUD clears notices");
                foreach (var control in ui.HotbarControls.Where(c => c.IsVisibleInTree()))
                    Require(!ui.NoticeRect.Intersects(control.GetGlobalRect()), $"{size} two-line notice clears {control.Name}");
                Directory.CreateDirectory("docs/native-screenshots");
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                app.GetViewport().GetTexture().GetImage().SavePng($"docs/native-screenshots/{prefix}hud-{size.X}x{size.Y}.png");
            }
            window.Size = originalSize; ui.VerificationSafeArea = null; ui.Layout(); ui.Notify(""); await Frames(4);

            // Value and label changes never move touch rectangles.
            var rects = ui.HotbarControls.ToDictionary(c => c.Name.ToString(), c => c.GetGlobalRect());
            var hudRect = ui.StatusRect;
            foreach (var id in new[] { "slash", "ember", "mend", "dodge" }) p.Cooldowns[id] = 3;
            p.Mana = 0; int potions = w.Items.Count("player", "potion");
            w.Items.Take("player", "potion", potions);
            var chest = w.RegionsById["reach"].Features.First(f => f.Value == "chest" && !w.RegionsById["reach"].Depleted.Contains(f.Key)).Key;
            var chestAt = w.RegionsById["reach"].Center(chest);
            p.Region = "reach"; p.X = chestAt.X; p.Y = chestAt.Y + 20; p.Level = 12; p.Experience = 1150;
            ui.InvalidateHud(); await Frames();
            foreach (var c in ui.HotbarControls)
                Require(c.GetGlobalRect() == rects[c.Name.ToString()], $"label/cooldown change kept rectangle of {c.Name}: {rects[c.Name.ToString()]} -> {c.GetGlobalRect()} text {(c as Button)?.Text}");
            Require(ui.StatusRect == hudRect, "HUD value changes kept its rectangle");
            Require(ui.ActionControl("attack").Disabled && ui.ActionControl("attack").Text.Contains('.') && ui.ActionControl("attack").Text.EndsWith("s"), "cooldown shown and disabled: " + ui.ActionControl("attack").Text);
            Require(ui.ActionControl("mend").Disabled && ui.UnavailableReason("mend") != null, "unavailable spell explains itself");
            Require(ui.ActionControl("potion").Disabled && ui.ActionControl("potion").Text.Contains("none") && ui.UnavailableReason("potion")!.Contains("draught", StringComparison.OrdinalIgnoreCase), "empty potion disabled with a reason");
            if (ui.MobileLayout)
            {
                Tap("potion"); await Frames(2);
                Require(w.Items.Count("player", "potion") == 0 && ui.NoticeText == ui.UnavailableReason("potion"), "tapping a disabled control explains instead of dispatching");
            }
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"docs/native-screenshots/{prefix}hud-unavailable.png");

            // Contextual Interact names the real action, and taking it does exactly that.
            p.Cooldowns.Clear(); p.Mana = p.MaxMana; w.Items.Add("player", "potion", Math.Max(1, potions));
            p.State = ActionState.Idle; ui.InvalidateHud(); await Frames();
            Require(ui.InteractionLabel.Label == "Open chest" && ui.InteractionLabel.Available, "Interact label names the chest: " + ui.InteractionLabel.Label);
            Require(ui.ActionControl("interact").Text.Replace('\n', ' ').Contains("Open chest"), "Interact button shows the contextual label");
            Require(ui.ActionControl("potion").Text.Contains('×') && !ui.ActionControl("potion").Disabled, "potion count shown");
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"docs/native-screenshots/{prefix}hud-interact.png");
            if (ui.MobileLayout) Tap("interact"); else app.Interact();
            await Frames(2);
            Require(w.RegionsById["reach"].Depleted.Contains(chest), "Open chest label led to the chest being opened");
            ui.InvalidateHud(); await Frames();
            Require(ui.InteractionLabel.Label != "Open chest", "depleted chest no longer advertised");

            // Quest progression reaches the tracker and is not hidden by a routine action notice.
            n.Call("receive", "defeated", "player", "", "beast", 1);
            app.Cast("slash"); await Frames();
            Require(ui.NoticeText.Contains("Quest complete"), "routine attack notice does not displace quest completion: " + ui.NoticeText);
            Require(ui.Hud.Objective?.Id == 2, "tracker moves to the next quest after completion");
        }
        finally
        {
            window.Size = originalSize; ui.VerificationSafeArea = null;
            Persistence.Restore(w, n, original); app.RebuildViews(); ui.Layout(); ui.Notify(""); ui.InvalidateHud(); ui.PauseForVerification(); await Frames(2);
        }
    }
}
