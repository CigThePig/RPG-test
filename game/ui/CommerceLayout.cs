using Godot;
namespace Mosslight;

/// <summary>Recipe collection or merchant catalogue alongside one selected result/purchase. Items remains authoritative.</summary>
public partial class CommerceLayout : VBoxContainer
{
    readonly RpgUi ui;
    public CommerceLayout(RpgUi owner) { ui = owner; Name = "commerce"; }
    public override void _Ready() => Rebuild();

    static VBoxContainer Pane(Node parent, string scrollName, string heading, string glyph, float ratio)
    {
        var column = UiDesign.InventoryPanel(parent, scrollName + "_pane", heading, glyph);
        var pane = column.GetParent<Control>(); pane.SizeFlagsStretchRatio = ratio;
        return JournalLayout.Scroll(column, scrollName);
    }

    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddThemeConstantOverride("separation", 10);
        bool craft = ui.PackTab == 1;
        var app = ui.App; var items = app.World.Items; var actor = app.World.Player;
        var merchant = craft ? null : app.NearbyMerchant();
        int coins = items.Count(actor.Id, "coin");

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 12);
        AddChild(header);
        var navigation = UiDesign.Tabs(header, PackScreen.Tabs, ui.PackTab, tab => { ui.PackTab = tab; ui.Inventory(); });
        navigation.GetParent<Control>().SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        foreach (var tab in navigation.GetChildren().OfType<Button>()) { tab.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; tab.CustomMinimumSize = new Vector2(112 * ui.TextScale, UiDesign.Target - 8); }
        navigation.GetChild<Control>(2).Visible = !craft || app.NearbyMerchant() != null;
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        UiDesign.Chip(header, coins + " crowns", Tone.Gold, "coin").Name = "purse";

        var summary = UiDesign.Note(this, craft ? "Choose a recipe. Ingredients are only used when the complete result fits in your pack." :
            merchant == null ? "No merchant nearby. Stand beside the warden in Reach to browse their wares." :
            $"{merchant.Name} has wares for sale · prices are in crowns.");
        summary.Name = craft ? "craft_summary" : "trade_summary";
        if (!craft && merchant == null)
        {
            var away = UiDesign.Well(this);
            away.AddChild(new Glyph("coin", new Color(UiDesign.Gold, .55f), 44) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
            UiDesign.Note(away, "Merchants trade only while you stand beside them.").HorizontalAlignment = HorizontalAlignment.Center;
            return;
        }
        bool compact = ui.PackAvailableSize.X / ui.TextScale < 850;
        if (compact)
        {
            var tabs = UiDesign.Tabs(this, new[] { "Collection", "Details" }, ui.CommerceDetail ? 1 : 0,
                i => { ui.CommerceDetail = i == 1; ui.Inventory(); }, "commerce_");
            tabs.GetParent<Control>().SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }
        var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 10); AddChild(regions);
        var collection = Pane(regions, "commerce_collection", craft ? "RECIPES" : "FOR SALE", craft ? "hammer" : "coin", .40f);
        var detailColumn = UiDesign.InventoryPanel(regions, "commerce_detail_pane", craft ? "WORKBENCH" : "PURCHASE", craft ? "spark" : "bag");
        var detailPane = detailColumn.GetParent<Control>(); detailPane.SizeFlagsStretchRatio = .60f;
        var detail = JournalLayout.Scroll(detailColumn, "commerce_detail");
        detail.AddThemeConstantOverride("separation", 10);
        var actions = new VBoxContainer { Name = "commerce_actions", SizeFlagsHorizontal = SizeFlags.ExpandFill }; detailColumn.AddChild(actions);
        actions.AddThemeConstantOverride("separation", 6);
        if (compact)
        {
            collection.GetParent().GetParent().GetParent<Control>().Visible = !ui.CommerceDetail;
            detailPane.Visible = ui.CommerceDetail;
        }
        if (craft)
        {
            var recipes = app.World.Content.Recipes;
            if (!recipes.Any(r => r.Id == ui.CraftSelection)) ui.CraftSelection = recipes.FirstOrDefault()?.Id;
            foreach (var recipe in recipes)
            {
                var state = items.CraftPreview(actor.Id, recipe);
                bool ready = state == CraftFailure.None;
                var row = Row(collection, "recipe_" + recipe.Id, recipe.Output, items.Title(recipe.Output),
                    recipe.Count > 1 ? "Makes ×" + recipe.Count : recipe.Material != "" ? $"{recipe.Material} · quality {recipe.Quality}" : "Recipe",
                    ready ? "Ready" : state == CraftFailure.MissingIngredients ? "Missing" : "Blocked", ready ? Tone.Moss : Tone.Ember,
                    ui.CraftSelection == recipe.Id, () => { ui.CraftSelection = recipe.Id; ui.CommerceDetail = true; ui.Inventory(); });
                row.TooltipText = items.Title(recipe.Output) + " · " + (ready ? "Materials ready" : PackScreen.Reason(state));
            }
            var chosen = recipes.FirstOrDefault(r => r.Id == ui.CraftSelection);
            if (chosen == null) { UiDesign.Note(detail, "No recipes available."); return; }
            var product = items.ProductPreview(chosen);
            Product(detail, chosen.Output, items.Title(chosen.Output), product, chosen.Count > 1 ? "×" + chosen.Count : null);
            UiDesign.Heading(detail, "MATERIALS");
            var materials = UiDesign.Well(detail, "materials");
            foreach (var (id, need) in chosen.Ingredients)
            {
                int have = items.Count(actor.Id, id);
                var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; line.AddThemeConstantOverride("separation", 10);
                materials.AddChild(line);
                line.AddChild(new TextureRect { Texture = ItemIconAtlas.Get(id), CustomMinimumSize = new Vector2(32, 32) * Math.Min(ui.TextScale, 1.25f),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore });
                var name = UiDesign.Text(items.Title(id)); name.ThemeTypeVariation = "RowTitle"; line.AddChild(name);
                UiDesign.Chip(line, have >= need ? $"{have} / {need}" : $"{have} / {need} · need {need - have}", have >= need ? Tone.Moss : Tone.Ember, have >= need ? "check" : "cross");
            }
            var preview = items.CraftPreview(actor.Id, chosen);
            string reason = preview != CraftFailure.None ? PackScreen.Reason(preview) : !app.CanManageItems ? "Items cannot be managed right now" : "Ready to craft";
            Status(actions, reason, preview == CraftFailure.None && app.CanManageItems);
            if (preview == CraftFailure.None && !items.HasSpace(actor.Id, chosen.Output, chosen.Count))
                UiDesign.Note(detail, "Your pack is full. Crafting can still succeed if consumed ingredients free space; otherwise nothing is used.");
            var make = UiDesign.Primary(UiDesign.Action(actions, "Craft " + items.Title(chosen.Output), () =>
            {
                // Resolve current recipe/state again; never spend from a stale presentation snapshot.
                var current = app.World.Content.Recipes.FirstOrDefault(r => r.Id == chosen.Id);
                bool ok = current != null && app.ManageItem(() => app.World.Items.Craft(app.World.Player.Id, current));
                ui.Notify(ok ? "Crafted " + items.Title(chosen.Output) : app.CanManageItems ? PackScreen.Reason(items.LastCraftFailure) : "Items cannot be managed right now");
                ui.Inventory();
            }));
            make.Name = "craft_" + chosen.Id; make.Disabled = preview != CraftFailure.None || !app.CanManageItems;
        }
        else
        {
            var stock = items.View(merchant!.Id).Where(v => !v.Equipped && v.Id != "coin").GroupBy(v => v.Id)
                .OrderBy(g => g.First().Title, StringComparer.Ordinal).ToList();
            if (!stock.Any(g => g.Key == ui.TradeSelection)) ui.TradeSelection = stock.FirstOrDefault()?.Key;
            foreach (var group in stock)
            {
                var item = group.First();
                Row(collection, "stock_" + item.Id, item.Id, item.Title, "In stock " + group.Sum(v => v.Count),
                    Items.Price(item.Id) + " crowns", Tone.Gold, ui.TradeSelection == item.Id,
                    () => { ui.TradeSelection = item.Id; ui.CommerceDetail = true; ui.Inventory(); });
            }
            var chosen = stock.FirstOrDefault(g => g.Key == ui.TradeSelection);
            if (chosen == null) { UiDesign.Note(detail, "This merchant has nothing left to sell."); return; }
            var sample = chosen.First(); int price = Items.Price(sample.Id);
            Product(detail, sample.Id, sample.Title, sample, null);
            UiDesign.Heading(detail, "LEDGER");
            var ledger = UiDesign.Well(detail, "ledger");
            UiDesign.Stat(ledger, "coin", "Price", price + " crowns");
            UiDesign.Stat(ledger, "bag", "In stock", chosen.Sum(v => v.Count).ToString(), tone: Tone.Neutral);
            UiDesign.Stat(ledger, "coin", "Your crowns", coins.ToString(), tone: Tone.Neutral);
            if (coins >= price) UiDesign.Stat(ledger, "coin", "After purchase", (coins - price).ToString(), tone: Tone.Neutral);
            var preview = items.TradePreview(actor.Id, merchant.Id, sample.Id, price);
            Status(actions, !app.CanManageItems ? "Items cannot be managed right now" : preview == TradeFailure.None ? "Ready to buy" : PackScreen.Reason(preview, price, coins),
                preview == TradeFailure.None && app.CanManageItems);
            var buy = UiDesign.Primary(UiDesign.Action(actions, $"Buy {sample.Title} · {price} crowns", () =>
            {
                var nearby = app.NearbyMerchant();
                bool ok = nearby?.Id == merchant.Id && app.ManageItem(() => app.World.Items.Trade(app.World.Player.Id, merchant.Id, sample.Id, price));
                ui.Notify(ok ? $"Bought {sample.Title} for {price} crowns" : nearby?.Id != merchant.Id ? "Merchant is no longer nearby" :
                    !app.CanManageItems ? "Items cannot be managed right now" : PackScreen.Reason(items.LastTradeFailure, price, items.Count(app.World.Player.Id, "coin")));
                ui.Inventory();
            }));
            buy.Name = "buy_" + sample.Id; buy.Disabled = preview != TradeFailure.None || !app.CanManageItems;
        }
    }

    RichButton Row(VBoxContainer list, string name, string icon, string title, string subtitle, string chip, Tone tone, bool selected, Action select)
    {
        var row = new RichButton(title, 10, 8) { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill, MinHeight = 68 * Math.Min(ui.TextScale, 1.2f) };
        UiDesign.SetSelected(row, selected);
        row.Pressed += select;
        UiDesign.IconWell(row.Content, ItemIconAtlas.Get(icon), 50 * Math.Min(ui.TextScale, 1.2f), selected);
        row.Stack(title, subtitle);
        UiDesign.Chip(row.Content, chip, tone);
        list.AddChild(row);
        return row;
    }

    static void Status(VBoxContainer actions, string text, bool ready)
    {
        var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        line.AddThemeConstantOverride("separation", 8);
        actions.AddChild(line);
        line.AddChild(new Glyph(ready ? "check" : "warning", ready ? UiDesign.Moss : UiDesign.Ember, 18));
        var label = new Label { Text = text, ThemeTypeVariation = ready ? "PositiveLabel" : "NegativeLabel", MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        line.AddChild(label);
    }

    void Product(VBoxContainer parent, string id, string title, ItemView? item, string? count)
    {
        var banner = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        banner.AddThemeConstantOverride("separation", 14);
        parent.AddChild(banner);
        UiDesign.IconWell(banner, ItemIconAtlas.Get(id), 96 * Math.Min(ui.TextScale, 1.2f), name: "product_icon");
        var caption = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        caption.AddThemeConstantOverride("separation", 6);
        banner.AddChild(caption);
        var heading = UiDesign.Heading(caption, title.ToUpperInvariant() + (count == null ? "" : "  " + count));
        heading.AddThemeFontSizeOverride("font_size", (int)(20 * ui.TextScale)); heading.AddThemeColorOverride("font_color", UiDesign.GoldBright);
        var chips = new HFlowContainer(); chips.AddThemeConstantOverride("h_separation", 6); chips.AddThemeConstantOverride("v_separation", 4);
        caption.AddChild(chips);
        if (item == null) return;
        UiDesign.Chip(chips, PackScreen.Kind(item), PackScreen.KindTone(item));
        if (PackScreen.Describe(item) != "") UiDesign.Chip(chips, PackScreen.Describe(item), Tone.Neutral);
        var rows = PackScreen.StatRows(item).ToList();
        if (rows.Count == 0) return;
        var current = item.Slot == "" ? null : ui.App.World.Items.View(ui.App.World.Player.Id).FirstOrDefault(i => i.Equipped && i.EquippedSlot == item.Slot);
        UiDesign.Heading(parent, current != null ? "ATTRIBUTES · VS " + current.Title.ToUpperInvariant() : "ATTRIBUTES");
        var sheet = UiDesign.Well(parent, "product_attributes");
        foreach (var (stat, label, value) in rows)
        {
            var delta = PackScreen.Delta(item, current, stat);
            UiDesign.Stat(sheet, PackScreen.StatGlyph(stat), label, value, delta?.Text, delta?.Better ?? true);
        }
        if (item.Slot != "" && current == null) UiDesign.Note(parent, PackScreen.Comparison(item, null));
    }
    public bool DismissDetails()
    {
        if (!ui.CommerceDetail || ui.PackAvailableSize.X / ui.TextScale >= 850) return false;
        ui.CommerceDetail = false; Rebuild(); return true;
    }
}
