using Godot;
namespace Mosslight;

/// <summary>
/// PR #7 Part 3: Pack (items/equipment, crafting, trading), Character progression,
/// Journal and Map. Headless checks exercise the read models and failure reasons on
/// production Gloot/Narrative; rendered checks drive the actual screens end to end.
/// </summary>
public static class RpgScreensChecks
{
    static Node CapacityLimit(World w, string owner)
    {
        var bag = w.Items.Service.Call("bag", owner).AsGodotObject() as Node;
        var limit = GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd").New().AsGodotObject() as Node;
        bag!.AddChild(limit!); limit!.Set("capacity", bag.Call("get_item_count").AsInt32());
        return limit;
    }
    // A rolled-back transaction re-imports Gloot and frees the old bag with its constraint.
    static void Release(Node limit) { if (!GodotObject.IsInstanceValid(limit)) return; limit.GetParent().RemoveChild(limit); limit.Free(); }

    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World; var n = app.Narrative;
        string original = Persistence.Capture(w, n);
        try
        {
            var items = w.Items; var p = w.Player;
            var forge = w.Content.Recipes.Single(r => r.Id == "forge_sword");
            var draught = w.Content.Recipes.Single(r => r.Id == "draught");

            // Inventory read model mirrors Gloot exactly.
            items.Add("player", "ore", 3); items.Add("player", "wood", 1);
            var priorInstances = items.View("player").Select(v => v.Instance).ToHashSet();
            check(items.Craft("player", forge) && items.LastCraftFailure == CraftFailure.None, "generated sword crafted");
            var view = items.View("player");
            using (var catalog = System.Text.Json.JsonDocument.Parse(items.Service.Call("catalog", "player").AsString()))
                check(view.Where(v => !v.Equipped).Sum(v => v.Count) == catalog.RootElement.EnumerateArray().Sum(r => r.GetProperty("count").GetInt32()), "inventory view matches Gloot catalog counts");
            var sword = view.Single(v => v.Id == "iron_sword" && !priorInstances.Contains(v.Instance));
            check(sword.Properties["material"] == "iron" && sword.Properties["quality"] == "2" && sword.Stat("damage") > 8, "generated instance exposes material, quality and scaled damage");
            check(view.Where(v => v.Instance != "").Select(v => v.Instance).Distinct().Count() == view.Count(v => v.Instance != ""), "instance identities are unique in the view");
            check(PackScreen.Comparison(sword, null).Contains("Slot is empty"), "comparison against an empty slot");

            // Equip / unequip through Gloot slots, with events and no duplication.
            int unequipped = 0; void Count(WorldEvent e) { if (e.Kind == "item_unequipped") unequipped++; }
            w.Events.Published += Count;
            try
            {
                p.Grasps = true;
                check(items.Equip(p, sword.Selector, "weapon"), "equip generated sword by instance");
                var equipped = items.View("player");
                check(equipped.Count(v => v.Instance == sword.Instance) == 1 && equipped.Single(v => v.Instance == sword.Instance).Equipped, "equipped item leaves the bag and appears in its slot once");
                check(items.EquippedSelector("player", "weapon") == sword.Selector, "slot reports the equipped instance");
                check(items.Unequip(p, "weapon") && unequipped == 1 && items.EquippedSelector("player", "weapon") == "", "unequip returns the item and publishes one event");
                check(items.View("player").Single(v => v.Instance == sword.Instance) is { Equipped: false }, "unequipped instance keeps its identity");
                check(!items.Unequip(p, "weapon") && unequipped == 1, "unequipping an empty slot changes nothing");
                items.Equip(p, sword.Selector, "weapon");
                var limit = CapacityLimit(w, "player");
                try
                {
                    string before = items.Service.Call("export_state").AsString();
                    check(!items.CanUnequip("player", "weapon") && !items.Unequip(p, "weapon") && items.Service.Call("export_state").AsString() == before, "full pack keeps the item equipped and unchanged");
                }
                finally { Release(limit); }
                p.Grasps = false;
                var staff = items.View("player").FirstOrDefault(v => v.Slot == "weapon" && !v.Equipped);
                if (staff != null) check(PackScreen.EquipBlocker(app, staff)!.Contains("grasp"), "non-grasping form explains why a weapon cannot be equipped");
                p.Grasps = true;
            }
            finally { w.Events.Published -= Count; }

            // Crafting failure reasons and transactional rollback.
            items.Take("player", "herb", items.Count("player", "herb"));
            check(items.CraftPreview("player", draught) == CraftFailure.MissingIngredients && !items.Craft("player", draught) && items.LastCraftFailure == CraftFailure.MissingIngredients, "missing ingredients are distinguished");
            items.Add("player", "ore", 4); items.Add("player", "wood", 2);
            var full = CapacityLimit(w, "player");
            try
            {
                // Rollback re-imports Gloot (dropping this test's constraint node), so compare contents.
                string Contents() => string.Join("|", items.View("player").OrderBy(v => v.Selector, StringComparer.Ordinal).Select(v => $"{v.Selector}:{v.Count}:{v.Equipped}:{string.Join(",", v.Properties.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value))}"));
                string before = Contents();
                check(!items.Craft("player", forge) && items.LastCraftFailure == CraftFailure.PackFull, "full pack is distinguished from missing ingredients");
                check(Contents() == before, "pack-full craft rolls back every stack, instance and equipped item");
            }
            finally { Release(full); }
            check(PackScreen.Reason(CraftFailure.PackFull).Contains("nothing was used"), "pack-full reason says nothing was consumed");

            // Trading reasons.
            var merchant = w.Actors.Values.First(a => a.Profession == "merchant");
            var stock = items.View(merchant.Id).First(v => v.Id != "coin");
            items.Take("player", "coin", items.Count("player", "coin"));
            int price = Items.Price(stock.Id);
            check(items.TradePreview("player", merchant.Id, stock.Id, price) == TradeFailure.InsufficientFunds && !items.Trade("player", merchant.Id, stock.Id, price) && items.LastTradeFailure == TradeFailure.InsufficientFunds, "insufficient funds distinguished");
            check(PackScreen.Reason(TradeFailure.InsufficientFunds, price, 0) == $"Need {price} crowns · you have 0", "funds reason names price and purse");
            check(items.TradePreview("player", merchant.Id, "relic", price) == TradeFailure.OutOfStock, "items the merchant lacks are out of stock");
            items.Add("player", "coin", price);
            int owned = items.Count("player", stock.Id);
            check(items.Trade("player", merchant.Id, stock.Id, price) && items.LastTradeFailure == TradeFailure.None && items.Count("player", stock.Id) == owned + 1 && items.Count("player", "coin") == 0, "successful trade moves goods and payment");

            // Journal rows: status, stages, prerequisites, without mutation.
            Persistence.Restore(w, n, original);
            n.Call("import_state", "{\"flags\":{},\"pools\":{},\"quests\":{}}");
            string quests = n.Call("export_state").AsString();
            var rows = JournalQuest.Read(n);
            check(n.Call("export_state").AsString() == quests, "journal rows do not mutate quest state");
            check(rows.Count == 3 && rows[0].Status == "available" && rows[1].Status == "locked" && !rows[1].PrerequisiteMet && rows[1].PrerequisiteTitle == "Trouble in the Reach", "locked quest names its prerequisite");
            check(rows[1].Staged && rows[1].Stages.Count == 4 && rows[1].Stages.All(s => !s.Done && !s.Current), "locked staged quest reveals no progress");
            n.Call("accept_quest", 1); n.Call("receive", "defeated", "player", "", "beast", 2);
            rows = JournalQuest.Read(n);
            check(rows[0].Status == "completed" && rows[0].Rewarded && rows[1].Status == "available" && rows[1].PrerequisiteMet, "completion unlocks the next quest");
            n.Call("accept_quest", 2); w.RegionsById["ruin"] = Regions.Dungeon(1); w.Enter(w.Player, "ruin");
            rows = JournalQuest.Read(n);
            check(rows[1].Status == "active" && rows[1].Stages[0].Done && rows[1].Stages[1].Current && !rows[1].Stages[2].Done, "stage history marks done and current stages");
            string saved = Persistence.Capture(w, n); Persistence.Restore(w, n, original); Persistence.Restore(w, n, saved);
            check(JournalQuest.Read(n)[1].Stages[1].Current, "journal stage survives save and load");
            n.Call("receive", "landmark_discovered", "player", "ruin:1", "forgotten-archive", 1);
            var extras = JournalExtras.Read(n);
            check(extras.Discoveries.Contains("Forgotten Archive") && extras.Lore.Count > 0, "discoveries and lore come from narrative flags");
            check(n.Call("journal").AsString().Contains("Find keys"), "legacy journal API intact");

            // Progression requirements mirror Progression.Unlock/Train.
            Persistence.Restore(w, n, original);
            p = w.Player;
            var learnable = w.Content.Abilities.FirstOrDefault(a => a.PlayerLearnable && !p.Abilities.Contains(a.Id));
            if (learnable != null)
            {
                p.Level = Math.Max(1, learnable.UnlockLevel - 1); p.SkillPoints = 5;
                check(CharacterOverview.LearnBlocker(p, learnable) == (learnable.UnlockLevel > p.Level ? $"Requires level {learnable.UnlockLevel} · you are level {p.Level}" : null), "level requirement explained");
                p.Level = learnable.UnlockLevel; p.SkillPoints = 0;
                check(CharacterOverview.LearnBlocker(p, learnable)!.StartsWith("Costs") && !Progression.Unlock(w, p, learnable.Id), "missing skill points explained and refused");
                p.SkillPoints = learnable.UnlockCost;
                check(CharacterOverview.LearnBlocker(p, learnable) == null && Progression.Unlock(w, p, learnable.Id), "no blocker means unlock succeeds");
            }
            var skill = w.Content.Skills.First();
            p.SkillPoints = 0;
            check(CharacterOverview.TrainBlocker(p, skill)!.Contains("skill point") && !Progression.Train(w, p, skill.Id), "training without points explained");
            p.SkillPoints = 1; float statBefore = w.Stat(p, skill.Stat);
            check(CharacterOverview.TrainBlocker(p, skill) == null && Progression.Train(w, p, skill.Id) && CharacterOverview.Breakdown(w, p, skill.Stat).Contains("training +"), "stat breakdown attributes training");
            check(w.Stat(p, skill.Stat) > statBefore, "training changes the authoritative stat");
        }
        finally { Persistence.Restore(w, n, original); }
    }

    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        async System.Threading.Tasks.Task Frames(int count = 4)
        { for (int i = 0; i < count; i++) await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Require(bool value, string text) { if (!value) throw new InvalidOperationException("RPG screens: " + text); }
        var ui = app.Ui; var w = app.World; var n = app.Narrative;
        var window = app.GetWindow(); var originalSize = window.Size;
        string original = Persistence.Capture(w, n);
        string prefix = ui.MobileLayout ? "mobile-" : "";
        Button Find(string name)
        {
            var button = ui.FindChild(name, true, false) as Button;
            Require(button != null && button.IsVisibleInTree(), "missing control " + name);
            return button!;
        }
        // Press through the real touch path on phones (TouchScroll owns screen rows); click signal on desktop.
        async System.Threading.Tasks.Task Press(string name)
        {
            var button = Find(name);
            Require(!button.Disabled, name + " should be enabled");
            if (ui.MobileLayout)
            {
                var scroll = ui.FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>().FirstOrDefault(s => s.IsVisibleInTree() && s.IsAncestorOf(button));
                if (scroll != null) { scroll.EnsureControlVisible(button); await Frames(3); }
                var at = button.GetGlobalRect().GetCenter();
                Input.ParseInputEvent(new InputEventScreenTouch { Index = 15, Pressed = true, Position = at });
                Input.ParseInputEvent(new InputEventScreenTouch { Index = 15, Pressed = false, Position = at });
            }
            else button.EmitSignal(Button.SignalName.Pressed);
            await Frames();
        }
        async System.Threading.Tasks.Task Shot(string name)
        {
            await Frames(4);
            Directory.CreateDirectory("docs/native-screenshots");
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"docs/native-screenshots/{prefix}{name}.png");
        }
        try
        {
            app.ResumeGame();
            var p = w.Player; p.Invulnerable = 1000; p.Region = "reach"; p.State = ActionState.Idle; p.Grasps = true; app.RebuildViews();
            // Loot: a real chest interaction feeds the Pack.
            var reach = w.RegionsById["reach"];
            var chest = reach.Features.First(f => f.Value == "chest" && !reach.Depleted.Contains(f.Key)).Key;
            var at = reach.Center(chest); p.X = at.X; p.Y = at.Y + 20;
            int stacks = w.Items.View("player").Count;
            app.Interact(); await Frames();
            Require(reach.Depleted.Contains(chest) || w.Items.View("player").Count > stacks, "chest loot reached the pack");

            // Craft: production recipe through the Craft tab.
            w.Items.Add("player", "ore", 3); w.Items.Add("player", "wood", 1);
            ui.PackTab = 1; ui.PackSelection = null; ui.Inventory(); await Frames();
            Require(Find("tab_craft").Text == "[Craft]", "selected tab is marked in words");
            await Shot("pack-craft");
            int swords = w.Items.Count("player", "iron_sword");
            await Press("craft_forge_sword");
            Require(w.Items.Count("player", "iron_sword") == swords + 1 && ui.NoticeText.Contains("Crafted"), $"craft button made exactly one sword: {swords}->{w.Items.Count("player", "iron_sword")} notice '{ui.NoticeText}' failure {w.Items.LastCraftFailure}");
            Require(ui.Navigation.Screen == "PACK & EQUIPMENT", "pack stays open after crafting");
            w.Items.Take("player", "herb", w.Items.Count("player", "herb")); ui.Inventory(); await Frames();
            Require(Find("craft_draught").Disabled, "missing ingredients disable the recipe with a reason");

            // Inspect and equip the generated instance.
            var sword = w.Items.View("player").Where(v => v.Id == "iron_sword" && v.Instance != "").OrderByDescending(v => v.Instance).First();
            await Press("tab_items");
            await Press("item_" + sword.Selector.Replace(':', '_'));
            Require(ui.PackSelection == sword.Selector && ui.FindChild("item_detail", true, false) != null, "selecting a row opens its detail");
            await Shot("pack-detail");
            await Press("equip");
            Require(w.Items.EquippedSelector("player", "weapon") == sword.Selector, "Equip equipped the selected instance");
            await Shot("pack-items");

            // Use a consumable from its detail.
            if (w.Items.Count("player", "potion") == 0) w.Items.Add("player", "potion");
            p.Health = p.MaxHealth / 2; ui.PackSelection = "potion"; ui.Inventory(); await Frames();
            int potions = w.Items.Count("player", "potion");
            await Press("use");
            Require(w.Items.Count("player", "potion") == potions - 1 && p.Health > p.MaxHealth / 2 && ui.Navigation.Screen == "PACK & EQUIPMENT", "Use consumed one draught and the Pack stayed open");

            // Save/load fidelity of equipment, then unequip.
            string equipped = Persistence.Capture(w, n);
            await Press("unequip_weapon");
            Require(w.Items.EquippedSelector("player", "weapon") == "", "Unequip returned the sword");
            Persistence.Restore(w, n, equipped); p = w.Player; ui.Inventory(); await Frames();
            Require(w.Items.EquippedSelector("player", "weapon") == sword.Selector && Find("unequip_weapon") != null, "reloaded pack shows the saved equipment");

            // Trade beside the warden.
            var merchant = w.Actors.Values.First(a => a.Profession == "merchant");
            p.Region = merchant.Region; p.X = merchant.X + 20; p.Y = merchant.Y;
            w.Items.Take("player", "coin", w.Items.Count("player", "coin"));
            ui.PackTab = 2; ui.Inventory(); await Frames();
            var buyable = w.Items.View(merchant.Id).First(v => v.Id != "coin");
            Require(Find("buy_" + buyable.Id).Disabled, "no crowns disables Buy");
            w.Items.Add("player", "coin", Items.Price(buyable.Id)); ui.Inventory(); await Frames();
            int had = w.Items.Count("player", buyable.Id);
            await Press("buy_" + buyable.Id);
            Require(w.Items.Count("player", buyable.Id) == had + 1 && w.Items.Count("player", "coin") == 0, "Buy completed one trade");
            await Shot("pack-trade");
            p.X = merchant.X + 400; ui.Inventory(); await Frames();
            Require(ui.FindChild("buy_" + buyable.Id, true, false) == null, "trade offers vanish away from the merchant");

            // Character progression requires confirmation; Cancel changes nothing.
            p.SkillPoints = 1; var skill = w.Content.Skills.First();
            int rank = p.SkillRanks.GetValueOrDefault(skill.Id);
            ui.Character(); await Frames();
            await Press("train_" + skill.Id);
            Require(ui.Navigation.Confirmation && ui.ConfirmationText.Contains("cannot be undone"), "training asks for confirmation");
            await Shot("character-confirm");
            ui.ConfirmationButton("confirm_cancel")!.EmitSignal(Button.SignalName.Pressed); await Frames();
            Require(p.SkillPoints == 1 && p.SkillRanks.GetValueOrDefault(skill.Id) == rank && ui.Navigation.Screen == "CHARACTER · WAYFARER", "Cancel returns to Character unchanged");
            await Press("train_" + skill.Id);
            ui.ConfirmationButton("confirm_accept")!.EmitSignal(Button.SignalName.Pressed); await Frames();
            Require(p.SkillPoints == 0 && p.SkillRanks.GetValueOrDefault(skill.Id) == rank + 1 && ui.Navigation.Screen == "CHARACTER · WAYFARER", "Confirm trains once and returns to Character");
            Require(Find("train_" + skill.Id).Disabled, "no points left disables training with a reason");
            await Shot("character-progression");

            // Journal and map present production quest and exploration state.
            n.Call("accept_quest", 1); n.Call("receive", "defeated", "player", "", "beast", 1);
            ui.Journal(); await Frames();
            Require(ui.FindChild("quest_1", true, false) != null && ui.FindChild("quest_2", true, false) != null, "journal lists quests as cards");
            await Shot("journal");
            ui.MapZoomed = false; ui.Map(); await Frames();
            var map = (RegionMap)ui.FindChild("region_map", true, false)!;
            Require(map.TileWindow.Size.X == w.RegionsById[p.Region].Width, "whole-map view");
            await Press("map_around you");
            map = (RegionMap)ui.FindChild("region_map", true, false)!;
            Require(ui.MapZoomed && map.TileWindow.Size.X < w.RegionsById[p.Region].Width, "zoom shows a window around the player");
            await Shot("map");
            w.RegionsById["ruin"] = Regions.Dungeon(1); w.Enter(p, "ruin"); ui.MapZoomed = false; ui.Map(); await Frames();
            var ruin = w.RegionsById["ruin"];
            Require(ruin.ExploredTiles.Count < ruin.Tiles.Length && ui.FindChild("map_landmarks", true, false) != null, "ruin map renders only explored state with a discovered-room list");
            await Shot("map-ruin");

            // Large text at a compact phone viewport: no control is clipped horizontally.
            window.Size = new Vector2I(900, 560); await Frames();
            var viewport = app.GetViewport().GetVisibleRect();
            ui.VerificationSafeArea = new Rect2(viewport.Position + new Vector2(60, 28), viewport.Size - new Vector2(120, 56));
            ui.SetTextScale(1.3f);
            foreach (var open in new Action[] { () => { ui.PackTab = 0; ui.Inventory(); }, () => { ui.PackTab = 1; ui.Inventory(); }, ui.Journal, ui.Character, ui.Map })
            {
                open(); await Frames(6);
                var screen = ui.ScreenRect;
                var scroll = ui.FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>().First(s => s.IsVisibleInTree());
                float right = scroll.GetVScrollBar().GetGlobalRect().Position.X + 1;
                foreach (var control in scroll.FindChildren("*", "Control", true, false).OfType<Control>().Where(c => c.IsVisibleInTree() && c is Button or PanelContainer))
                    Require(control.GetGlobalRect().End.X <= right && control.GetGlobalRect().Position.X >= screen.Position.X, $"{ui.Navigation.Screen}: {control.Name} fits beside the scrollbar with large text");
            }
            ui.PackTab = 0; ui.Inventory(); await Shot("pack-items-large-900x560");
        }
        finally
        {
            window.Size = originalSize; ui.VerificationSafeArea = null; ui.SetTextScale(1);
            ui.PackTab = 0; ui.PackSelection = null; ui.MapZoomed = false;
            Persistence.Restore(w, n, original); app.RebuildViews(); ui.Layout(); ui.Notify(""); ui.PauseForVerification(); await Frames(2);
        }
    }
}
