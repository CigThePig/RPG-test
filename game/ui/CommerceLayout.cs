using Godot;
namespace Mosslight;

/// <summary>Recipe collection or merchant catalogue alongside one selected result/purchase. Items remains authoritative.</summary>
public partial class CommerceLayout : VBoxContainer
{
    readonly RpgUi ui;
    public CommerceLayout(RpgUi owner) { ui = owner; Name = "commerce"; }
    public override void _Ready() => Rebuild();
    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        bool craft = ui.PackTab == 1;
        var navigation = UiDesign.Tabs(this, PackScreen.Tabs, ui.PackTab, tab => { ui.PackTab = tab; ui.Inventory(); });
        navigation.GetChild<Control>(2).Visible = !craft || ui.App.NearbyMerchant() != null;
        var app = ui.App; var items = app.World.Items; var actor = app.World.Player;
        var merchant = craft ? null : app.NearbyMerchant();
        UiDesign.Heading(this, craft ? "THE WORKBENCH" : merchant == null ? "MERCHANTS" : merchant.Name.ToUpperInvariant());
        var summary = UiDesign.Note(this, craft ? "Select a recipe · ingredients are taken only when the complete result fits." :
            merchant == null ? "No merchant nearby. Stand beside the warden in Reach to browse their wares." :
            $"Your crowns: {items.Count(actor.Id, "coin")} · purchases use the prices shown below.");
        summary.Name = craft ? "craft_summary" : "trade_summary";
        if (!craft && merchant == null) return;
        var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 16); AddChild(regions);
        var collection = JournalLayout.Scroll(regions, "commerce_collection"); collection.GetParent<Control>().SizeFlagsStretchRatio = .38f;
        var detailPane = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .62f };
        regions.AddChild(detailPane);
        var detail = JournalLayout.Scroll(detailPane, "commerce_detail");
        var actions = new VBoxContainer { Name = "commerce_actions", SizeFlagsHorizontal = SizeFlags.ExpandFill }; detailPane.AddChild(actions);
        bool compact = ui.PackAvailableSize.X / ui.TextScale < 850;
        if (compact)
        {
            var tabs = UiDesign.Tabs(this, new[] { "Collection", "Details" }, ui.CommerceDetail ? 1 : 0,
                i => { ui.CommerceDetail = i == 1; ui.Inventory(); }, "commerce_");
            MoveChild(tabs, GetChildCount() - 2);
            collection.GetParent<Control>().Visible = !ui.CommerceDetail;
            detailPane.Visible = ui.CommerceDetail;
        }
        if (craft)
        {
            var recipes = app.World.Content.Recipes;
            if (!recipes.Any(r => r.Id == ui.CraftSelection)) ui.CraftSelection = recipes.FirstOrDefault()?.Id;
            UiDesign.Heading(collection, "RECIPES");
            foreach (var recipe in recipes)
            {
                var state = items.CraftPreview(actor.Id, recipe);
                var select = UiDesign.Action(collection, (ui.CraftSelection == recipe.Id ? "[Selected] " : "") + items.Title(recipe.Output)
                    + "\n" + (state == CraftFailure.None ? "Materials ready" : PackScreen.Reason(state)),
                    () => { ui.CraftSelection = recipe.Id; ui.CommerceDetail = true; ui.Inventory(); });
                select.Name = "recipe_" + recipe.Id;
            }
            var chosen = recipes.FirstOrDefault(r => r.Id == ui.CraftSelection);
            if (chosen == null) { UiDesign.Note(detail, "No recipes available."); return; }
            var product = items.ProductPreview(chosen);
            Product(detail, chosen.Output, items.Title(chosen.Output), product);
            UiDesign.Note(detail, $"Result: {items.Title(chosen.Output)} ×{chosen.Count}");
            UiDesign.Heading(detail, "MATERIALS");
            foreach (var (id, need) in chosen.Ingredients)
                UiDesign.Note(detail, $"{items.Title(id)} · owned {items.Count(actor.Id, id)} / required {need}" +
                    (items.Count(actor.Id, id) < need ? $" · missing {need - items.Count(actor.Id, id)}" : " · ready"));
            var preview = items.CraftPreview(actor.Id, chosen);
            string reason = preview != CraftFailure.None ? PackScreen.Reason(preview) : !app.CanManageItems ? "Items cannot be managed right now" : "Ready to craft";
            UiDesign.Note(actions, reason);
            if (preview == CraftFailure.None && !items.HasSpace(actor.Id, chosen.Output, chosen.Count))
                UiDesign.Note(detail, "Your pack is full. Crafting can still succeed if consumed ingredients free space; otherwise nothing is used.");
            var make = UiDesign.Action(actions, "Craft " + items.Title(chosen.Output), () =>
            {
                // Resolve current recipe/state again; never spend from a stale presentation snapshot.
                var current = app.World.Content.Recipes.FirstOrDefault(r => r.Id == chosen.Id);
                bool ok = current != null && app.ManageItem(() => app.World.Items.Craft(app.World.Player.Id, current));
                ui.Notify(ok ? "Crafted " + items.Title(chosen.Output) : app.CanManageItems ? PackScreen.Reason(items.LastCraftFailure) : "Items cannot be managed right now");
                ui.Inventory();
            });
            make.Name = "craft_" + chosen.Id; make.Disabled = preview != CraftFailure.None || !app.CanManageItems;
        }
        else
        {
            var stock = items.View(merchant!.Id).Where(v => !v.Equipped && v.Id != "coin").GroupBy(v => v.Id)
                .OrderBy(g => g.First().Title, StringComparer.Ordinal).ToList();
            if (!stock.Any(g => g.Key == ui.TradeSelection)) ui.TradeSelection = stock.FirstOrDefault()?.Key;
            UiDesign.Heading(collection, "FOR SALE");
            foreach (var group in stock)
            {
                var item = group.First();
                var select = UiDesign.Action(collection, (ui.TradeSelection == item.Id ? "[Selected] " : "") + item.Title +
                    $"\n{Items.Price(item.Id)} crowns · stock {group.Sum(v => v.Count)}", () => { ui.TradeSelection = item.Id; ui.CommerceDetail = true; ui.Inventory(); });
                select.Name = "stock_" + item.Id;
            }
            var chosen = stock.FirstOrDefault(g => g.Key == ui.TradeSelection);
            if (chosen == null) { UiDesign.Note(detail, "This merchant has nothing left to sell."); return; }
            var sample = chosen.First(); int price = Items.Price(sample.Id); int coins = items.Count(actor.Id, "coin");
            Product(detail, sample.Id, sample.Title, sample);
            UiDesign.Heading(detail, "PURCHASE");
            UiDesign.Note(detail, $"Price {price} crowns · stock {chosen.Sum(v => v.Count)}\nYour crowns {coins}" + (coins >= price ? $" · after purchase {coins - price}" : ""));
            var preview = items.TradePreview(actor.Id, merchant.Id, sample.Id, price);
            UiDesign.Note(actions, !app.CanManageItems ? "Items cannot be managed right now" : preview == TradeFailure.None ? "Ready to buy" : PackScreen.Reason(preview, price, coins));
            var buy = UiDesign.Action(actions, $"Buy {sample.Title} · {price} crowns", () =>
            {
                var nearby = app.NearbyMerchant();
                bool ok = nearby?.Id == merchant.Id && app.ManageItem(() => app.World.Items.Trade(app.World.Player.Id, merchant.Id, sample.Id, price));
                ui.Notify(ok ? $"Bought {sample.Title} for {price} crowns" : nearby?.Id != merchant.Id ? "Merchant is no longer nearby" :
                    !app.CanManageItems ? "Items cannot be managed right now" : PackScreen.Reason(items.LastTradeFailure, price, items.Count(app.World.Player.Id, "coin")));
                ui.Inventory();
            });
            buy.Name = "buy_" + sample.Id; buy.Disabled = preview != TradeFailure.None || !app.CanManageItems;
        }
    }
    void Product(VBoxContainer parent, string id, string title, ItemView? item)
    {
        UiDesign.Heading(parent, title.ToUpperInvariant());
        parent.AddChild(new TextureRect { Texture = ItemIconAtlas.Get(id), CustomMinimumSize = new Vector2(0, 96 * ui.TextScale),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore });
        if (item == null) return;
        if (PackScreen.Describe(item) != "") UiDesign.Note(parent, PackScreen.Describe(item));
        if (PackScreen.Stats(item) != "") UiDesign.Note(parent, PackScreen.Stats(item));
        if (item.Slot != "") UiDesign.Note(parent, PackScreen.Comparison(item,
            ui.App.World.Items.View(ui.App.World.Player.Id).FirstOrDefault(i => i.Equipped && i.EquippedSlot == item.Slot)));
    }
    public bool DismissDetails()
    {
        if (!ui.CommerceDetail || ui.PackAvailableSize.X / ui.TextScale >= 850) return false;
        ui.CommerceDetail = false; Rebuild(); return true;
    }
}
