using Godot;
using System.Globalization;
namespace Mosslight;

/// <summary>
/// Pack screen (PR #7 Part 3): items and equipment, crafting and trading. Every row is
/// read from Gloot through <see cref="Items"/> each time the screen is built; every
/// action goes through the existing transactional Items/World/Foundation calls.
/// </summary>
public static class PackScreen
{
    public static readonly string[] Tabs = { "Items", "Craft", "Trade" };
    static readonly string[] Slots = { "weapon", "body" };
    static readonly string[] StatOrder = { "damage", "defence", "power", "healing" };

    public static void Populate(VBoxContainer body, RpgUi ui)
    {
        var app = ui.App;
        UiDesign.Tabs(body, Tabs, ui.PackTab, tab => { ui.PackTab = tab; ui.Inventory(); });
        switch (ui.PackTab)
        {
            case 1: Craft(body, ui, app); break;
            case 2: Trade(body, ui, app); break;
            default: ItemsTab(body, ui, app); break;
        }
    }

    static string Name(string id) => id.ToLowerInvariant().Replace(':', '_');

    /// <summary>Plain-language item category shown as a chip.</summary>
    public static string Kind(ItemView item) => item.Slot switch
    {
        "weapon" => "Weapon", "body" => "Body armour",
        _ => item.Usable ? "Consumable" : item.Id == "coin" ? "Currency" : item.Id is "relic" or "dungeon_key" ? "Quest item" : "Material"
    };
    public static Tone KindTone(ItemView item) => item.Slot != "" ? Tone.Gold : item.Usable ? Tone.Ember : item.Id is "relic" or "dungeon_key" ? Tone.Arcane : item.Id == "coin" ? Tone.Gold : Tone.Moss;
    public static string StatGlyph(string stat) => stat switch
    {
        "damage" => "sword", "defence" => "shield", "power" => "bolt", "healing" => "leaf", "condition" => "star", "strength" => "sword", _ => "diamond"
    };
    /// <summary>Displayed stat rows in a fixed order: (stat, label, value).</summary>
    public static IEnumerable<(string Stat, string Label, string Value)> StatRows(ItemView item)
    {
        foreach (var stat in StatOrder.Where(item.Properties.ContainsKey)) yield return (stat, Cap(stat), item.Properties[stat]);
        if (item.Properties.TryGetValue("condition", out var c)) yield return ("condition", "Condition", c + "%");
    }
    /// <summary>Signed change against the equipped item in the same slot, or null when nothing changes.</summary>
    public static (string Text, bool Better)? Delta(ItemView candidate, ItemView? current, string stat)
    {
        if (current == null || stat is "healing" or "condition") return null;
        float delta = candidate.Stat(stat) - current.Stat(stat);
        if (Math.Abs(delta) < .005f) return ("±0", true);
        return ((delta > 0 ? "▲ +" : "▼ −") + Math.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture), delta > 0);
    }
    static string Cap(string text) => text == "" ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Short identity: material and quality for generated or material items.</summary>
    public static string Describe(ItemView item)
    {
        var parts = new List<string>();
        if (item.Properties.TryGetValue("material", out var material)) parts.Add(material);
        if (item.Properties.TryGetValue("quality", out var quality)) parts.Add("quality " + quality);
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }

    public static string Stats(ItemView item) => string.Join("  ·  ",
        StatOrder.Where(s => item.Properties.ContainsKey(s)).Select(s => $"{Cap(s)} {item.Properties[s]}")
            .Concat(item.Properties.TryGetValue("condition", out var c) ? new[] { $"Condition {c}%" } : Array.Empty<string>()));

    /// <summary>Stat changes against whatever the slot currently holds. Words and signs, not colour.</summary>
    public static string Comparison(ItemView candidate, ItemView? current)
    {
        if (current == null) return "Slot is empty · equipping adds " + (Stats(candidate) == "" ? "this item" : Stats(candidate));
        var changes = StatOrder.Where(s => s != "healing" && (candidate.Properties.ContainsKey(s) || current.Properties.ContainsKey(s)))
            .Select(s =>
            {
                float delta = candidate.Stat(s) - current.Stat(s);
                string sign = delta > 0.005f ? "+" : delta < -0.005f ? "−" : "±";
                return $"{Cap(s)} {sign}{Math.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture)}";
            });
        return $"Compared with equipped {current.Title}: " + string.Join(" · ", changes);
    }

    /// <summary>Why this item cannot be equipped now, or null.</summary>
    public static string? EquipBlocker(Foundation app, ItemView item)
    {
        var actor = app.World.Player;
        if (item.Slot == "") return "This item is not equipment";
        if (item.Slot == "weapon" && !actor.Grasps) return "Your current form cannot grasp a weapon";
        if (app.World.Items.EquippedSelector(actor.Id, item.Slot) != "" && !app.World.Items.CanUnequip(actor.Id, item.Slot))
            return "Pack full · make room to swap out the equipped item";
        if (!app.CanManageItems) return "Items cannot be managed right now";
        return null;
    }

    static void ItemsTab(VBoxContainer body, RpgUi ui, Foundation app)
    {
        var actor = app.World.Player;
        var view = app.World.Items.View(actor.Id);
        var bag = view.Where(v => !v.Equipped).ToList();
        var equipped = view.Where(v => v.Equipped).ToDictionary(v => v.EquippedSlot);
        int coins = bag.Where(v => v.Id == "coin").Sum(v => v.Count);
        UiDesign.Note(body, $"Crowns {coins}  ·  {bag.Count} stack{(bag.Count == 1 ? "" : "s")} in pack");

        var gear = UiDesign.Card(body, "equipped");
        UiDesign.Heading(gear, "EQUIPPED");
        foreach (var slot in Slots)
        {
            if (!equipped.TryGetValue(slot, out var item)) { gear.AddChild(UiDesign.Text($"{Cap(slot)}: empty")); continue; }
            gear.AddChild(UiDesign.Text($"{Cap(slot)}: {item.Title}{(Describe(item) == "" ? "" : " (" + Describe(item) + ")")}"));
            if (Stats(item) != "") UiDesign.Note(gear, Stats(item));
            var unequip = UiDesign.Action(gear, "Unequip " + item.Title, () =>
            {
                bool ok = app.ManageItem(() => app.World.Items.Unequip(actor, slot));
                ui.Notify(ok ? item.Title + " returned to pack" : "Pack full · make room to unequip " + item.Title);
                ui.Inventory();
            });
            unequip.Name = "unequip_" + slot;
            if (!app.World.Items.CanUnequip(actor.Id, slot)) { unequip.Disabled = true; UiDesign.Note(gear, "Pack full · make room to unequip"); }
        }

        UiDesign.Heading(body, "PACK");
        if (bag.Count == 0)
        {
            UiDesign.Note(body, "Your pack is empty. Gather plants, open chests and search fallen foes.");
            return;
        }
        foreach (var item in bag.OrderBy(v => v.Slot == "" ? 1 : 0).ThenBy(v => v.Title, StringComparer.Ordinal).ThenBy(v => v.Selector, StringComparer.Ordinal))
        {
            bool selected = ui.PackSelection == item.Selector;
            string detail = Describe(item);
            string label = item.Count > 1 ? $"{item.Title} ×{item.Count}" : item.Title;
            if (detail != "") label += " · " + detail;
            var row = UiDesign.Action(body, (selected ? "[Selected] " : "") + label, () =>
            {
                ui.PackSelection = selected ? null : item.Selector;
                ui.Inventory();
            });
            row.Name = "item_" + Name(item.Selector);
            row.Alignment = HorizontalAlignment.Left;
            if (selected) Detail(body, ui, app, item, item.Slot == "" ? null : equipped.GetValueOrDefault(item.Slot));
        }
    }

    static void Detail(VBoxContainer body, RpgUi ui, Foundation app, ItemView item, ItemView? current)
    {
        var card = UiDesign.Card(body, "item_detail");
        UiDesign.Heading(card, item.Title.ToUpperInvariant());
        var facts = new List<string>();
        if (item.Count > 1 || item.MaxStack > 1) facts.Add($"Stack {item.Count} of {item.MaxStack}");
        if (item.Instance != "") facts.Add("Unique item " + item.Instance.Replace("item:", "#"));
        if (item.Slot != "") facts.Add("Slot: " + item.Slot);
        if (item.Properties.TryGetValue("mass", out var mass)) facts.Add("Mass " + mass);
        if (Describe(item) != "") facts.Add(Describe(item));
        card.AddChild(UiDesign.Text(string.Join("  ·  ", facts)));
        if (Stats(item) != "") card.AddChild(UiDesign.Text(Stats(item)));
        if (item.Slot != "")
        {
            card.AddChild(UiDesign.Text(Comparison(item, current)));
            var blocker = EquipBlocker(app, item);
            var equip = UiDesign.Action(card, "Equip " + item.Title, () =>
            {
                bool ok = app.ManageItem(() => app.World.Items.Equip(app.World.Player, item.Selector, item.Slot));
                ui.Notify(ok ? "Equipped " + item.Title : EquipBlocker(app, item) ?? "Could not equip " + item.Title);
                if (ok) ui.PackSelection = null;
                ui.Inventory();
            });
            equip.Name = "equip";
            if (blocker != null) { equip.Disabled = true; UiDesign.Note(card, blocker); }
        }
        if (item.Usable)
        {
            var actor = app.World.Player;
            var use = UiDesign.Action(card, $"Use {item.Title} · restores {item.Stat("healing"):0} health", () => ui.UseFromPack(item.Selector));
            use.Name = "use";
            if (actor.Health >= actor.MaxHealth) UiDesign.Note(card, "Health is already full; using it now restores nothing.");
        }
        if (item.Slot == "" && !item.Usable)
            UiDesign.Note(card, app.World.Content.Recipes.Any(r => r.Ingredients.ContainsKey(item.Id))
                ? "Crafting material · see the Craft tab" : item.Id == "coin" ? "Currency for merchants" : "Keep this; it may matter to a quest");
    }

    public static string Reason(CraftFailure failure) => failure switch
    {
        CraftFailure.MissingIngredients => "Missing ingredients",
        CraftFailure.CannotMake => "This recipe cannot be made",
        CraftFailure.PackFull => "Pack full · nothing was used; make room and try again",
        CraftFailure.Failed => "Crafting failed · nothing was used",
        _ => ""
    };

    static void Craft(VBoxContainer body, RpgUi ui, Foundation app)
    {
        var items = app.World.Items;
        string owner = app.World.Player.Id;
        UiDesign.Note(body, "Crafting uses ingredients from your pack. If the result does not fit, nothing is used.");
        foreach (var recipe in app.World.Content.Recipes)
        {
            var card = UiDesign.Card(body, "recipe_" + recipe.Id);
            string output = items.Title(recipe.Output) + (recipe.Count > 1 ? " ×" + recipe.Count : "");
            UiDesign.Heading(card, output.ToUpperInvariant());
            if (recipe.Material != "") UiDesign.Note(card, $"Makes a unique {recipe.Material} item, quality {recipe.Quality}");
            var missing = new List<string>();
            foreach (var (ingredient, need) in recipe.Ingredients)
            {
                int have = items.Count(owner, ingredient);
                string title = items.Title(ingredient);
                card.AddChild(UiDesign.Text($"{title}: have {have} · need {need}{(have < need ? $" · missing {need - have}" : " · ok")}"));
                if (have < need) missing.Add($"{title} ×{need - have}");
            }
            var preview = items.CraftPreview(owner, recipe);
            string status = preview switch
            {
                CraftFailure.MissingIngredients => "Missing: " + string.Join(", ", missing),
                CraftFailure.None when !items.HasSpace(owner, recipe.Output, recipe.Count) => "Ready · pack is full, so this only works if the ingredients free a slot",
                CraftFailure.None => "Ready to craft",
                _ => Reason(preview)
            };
            UiDesign.Note(card, status);
            var make = UiDesign.Action(card, "Craft " + output, () =>
            {
                bool ok = app.ManageItem(() => items.Craft(owner, recipe));
                ui.Notify(ok ? "Crafted " + output : app.CanManageItems ? Reason(items.LastCraftFailure) : "Items cannot be managed right now");
                ui.Inventory();
            });
            make.Name = "craft_" + recipe.Id;
            make.Disabled = preview != CraftFailure.None;
        }
    }

    public static string Reason(TradeFailure failure, int price, int coins) => failure switch
    {
        TradeFailure.OutOfStock => "Out of stock",
        TradeFailure.InsufficientFunds => $"Need {price} crowns · you have {coins}",
        TradeFailure.PackFull => "Pack full · make room first",
        TradeFailure.Invalid => "Not for sale",
        TradeFailure.Failed => "Trade failed · nothing changed hands",
        _ => ""
    };

    static void Trade(VBoxContainer body, RpgUi ui, Foundation app)
    {
        var merchant = app.NearbyMerchant();
        if (merchant == null)
        {
            UiDesign.Note(body, "No merchant nearby. Stand beside the warden in Reach, then open the Pack to trade.");
            return;
        }
        var items = app.World.Items;
        string owner = app.World.Player.Id;
        int coins = items.Count(owner, "coin");
        var summary = UiDesign.Text($"Trading with {merchant.Name}  ·  your crowns: {coins}");
        summary.Name = "trade_summary"; body.AddChild(summary);
        // Coins received from earlier sales are payment, not stock for sale.
        var stock = items.View(merchant.Id).Where(v => !v.Equipped && v.Id != "coin").GroupBy(v => v.Id).ToList();
        if (stock.Count == 0) { UiDesign.Note(body, "This merchant has nothing left to sell."); return; }
        foreach (var group in stock.OrderBy(g => g.First().Title, StringComparer.Ordinal))
        {
            var sample = group.First();
            int price = Items.Price(sample.Id), count = group.Sum(v => v.Count);
            var card = UiDesign.Card(body, "stock_" + sample.Id);
            UiDesign.Heading(card, sample.Title.ToUpperInvariant());
            card.AddChild(UiDesign.Text($"Price {price} crowns  ·  in stock {count}" + (Stats(sample) == "" ? "" : "  ·  " + Stats(sample))));
            var preview = items.TradePreview(owner, merchant.Id, sample.Id, price);
            var buy = UiDesign.Action(card, $"Buy {sample.Title} · {price} crowns", () =>
            {
                bool ok = app.ManageItem(() => items.Trade(owner, merchant.Id, sample.Id, price));
                ui.Notify(ok ? $"Bought {sample.Title} for {price} crowns" : app.CanManageItems ? Reason(items.LastTradeFailure, price, items.Count(owner, "coin")) : "Items cannot be managed right now");
                ui.Inventory();
            });
            buy.Name = "buy_" + sample.Id;
            if (preview != TradeFailure.None) { buy.Disabled = true; UiDesign.Note(card, Reason(preview, price, coins)); }
        }
    }
}
