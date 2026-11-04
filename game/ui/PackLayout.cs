using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mosslight;

/// <summary>
/// Flagship PR7 inventory: real equipment, independently scrolling bag, stable
/// item inspector. All snapshots and transactions originate from Items/Gloot.
/// </summary>
public partial class PackLayout : VBoxContainer
{
    public enum LayoutKind { Wide, Compact, Single }

    readonly RpgUi ui;
    readonly PackViewState state;
    readonly Dictionary<string, ItemTile> tiles = new(StringComparer.Ordinal);
    readonly Dictionary<string, Button> gearButtons = new(StringComparer.Ordinal);
    IReadOnlyList<ItemView> snapshot = Array.Empty<ItemView>();
    List<ItemView> bag = new();
    Dictionary<string, ItemView> equipped = new(StringComparer.Ordinal);
    Control? gearPanel, bagPanel, detailPanel;
    VBoxContainer? inspectorDetails, inspectorActions;
    TouchScroll? bagScroll;
    int pendingScroll = -1, scrollFrames;
    public LayoutKind Mode { get; private set; }

    public PackLayout(RpgUi owner)
    {
        ui = owner;
        state = owner.PackState;
        Name = "wayfarers_kit";
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
    }

    public static LayoutKind ChooseMode(Vector2 size, float scale)
    {
        float width = size.X / scale, height = size.Y / scale;
        if (width >= 1040 && height >= 420) return LayoutKind.Wide;
        if (width >= 740 && height >= 350) return LayoutKind.Compact;
        return LayoutKind.Single;
    }

    public override void _Ready() => Rebuild();

    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        if (bagScroll != null && GodotObject.IsInstanceValid(bagScroll))
        {
            state.BagScroll = bagScroll.ScrollVertical;
            bagScroll.CancelGesture();
        }
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        tiles.Clear(); gearButtons.Clear();
        gearPanel = bagPanel = detailPanel = null;
        inspectorDetails = inspectorActions = null;
        bagScroll = null;
        snapshot = ui.App.World.Items.View(ui.App.World.Player.Id);
        bag = snapshot.Where(v => !v.Equipped)
            .OrderBy(v => v.Slot == "" ? 1 : 0)
            .ThenBy(v => v.Title, StringComparer.Ordinal)
            .ThenBy(v => v.Selector, StringComparer.Ordinal).ToList();
        equipped = snapshot.Where(v => v.Equipped && v.EquippedSlot is "weapon" or "body")
            .GroupBy(v => v.EquippedSlot).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        state.Reconcile(snapshot);
        // Use the intended logical safe area, not the current Container minimum size.
        // The previous three-pane children may still be influencing the panel's
        // measured rectangle when the viewport contracts in this same frame.
        Mode = ChooseMode(ui.PackAvailableSize - new Vector2(32, 72), ui.TextScale);

        var top = UiDesign.Tabs(this, PackScreen.Tabs, 0, tab =>
        {
            ui.PackTab = tab;
            state.DetailOpen = false;
            ui.Inventory();
        });
        top.Name = "pack_nav";
        top.GetChild<Control>(2).Visible = ui.App.NearbyMerchant() != null;
        // Unlike the legacy full-width segmented bar, these are small, fixed
        // navigation choices. They do not steal horizontal space from the three
        // inventory regions and keep the backpack as the dominant surface.
        foreach (var tab in top.GetChildren().OfType<Button>())
        {
            tab.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            tab.CustomMinimumSize = new Vector2(124 * ui.TextScale, UiDesign.Target);
        }

        var summary = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(summary);
        int coins = bag.Where(v => v.Id == "coin").Sum(v => v.Count);
        UiDesign.Note(summary, "CROWNS  " + coins + "    ·    " + bag.Count +
            (bag.Count == 1 ? " stack" : " stacks"));
        if (Mode == LayoutKind.Single)
        {
            // Preserve a full tile row on small phones: keep the two local view
            // switches beside the currency summary instead of spending a second
            // full-height row underneath it.
            var switcher = new HBoxContainer { Name = "single_region_switcher",
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
            switcher.AddThemeConstantOverride("separation", 6);
            summary.AddChild(switcher);
            var backpack = UiDesign.Action(switcher, state.GearOpen ? "Bag" : "[Bag]", () =>
            {
                state.GearOpen = false; state.DetailOpen = false; UpdateVisibility();
                switcher.GetChild<Button>(0).Text = "[Bag]";
                switcher.GetChild<Button>(1).Text = "Gear";
            });
            backpack.Name = "view_backpack"; backpack.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            backpack.CustomMinimumSize = new Vector2(86 * ui.TextScale, UiDesign.Target);
            backpack.TooltipText = "View backpack items";
            var equipment = UiDesign.Action(switcher, state.GearOpen ? "[Gear]" : "Gear", () =>
            {
                state.GearOpen = true; state.DetailOpen = false; UpdateVisibility();
                switcher.GetChild<Button>(0).Text = "Bag";
                switcher.GetChild<Button>(1).Text = "[Gear]";
            });
            equipment.Name = "view_equipment"; equipment.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            equipment.CustomMinimumSize = new Vector2(88 * ui.TextScale, UiDesign.Target);
            equipment.TooltipText = "View weapon and body equipment";
        }
        var regions = new HBoxContainer { Name = "pack_regions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        regions.AddThemeConstantOverride("separation", 8);
        AddChild(regions);
        var gearContent = UiDesign.InventoryPanel(regions, "equipment_panel", "EQUIPPED");
        gearPanel = (Control)gearContent.GetParent();
        var bagContent = UiDesign.InventoryPanel(regions, "backpack_panel", "BACKPACK");
        bagPanel = (Control)bagContent.GetParent();
        var detailContent = UiDesign.InventoryPanel(regions, "inspector_panel", "INSPECT");
        detailPanel = (Control)detailContent.GetParent();
        if (Mode == LayoutKind.Wide)
        {
            gearPanel.SizeFlagsStretchRatio = .26f;
            bagPanel.SizeFlagsStretchRatio = .41f;
            detailPanel.SizeFlagsStretchRatio = .33f;
        }
        else if (Mode == LayoutKind.Compact)
        {
            gearPanel.SizeFlagsStretchRatio = .35f;
            bagPanel.SizeFlagsStretchRatio = .65f;
            detailPanel.SizeFlagsStretchRatio = .65f;
        }
        BuildGear(gearContent);
        BuildBag(bagContent);
        BuildInspector(detailContent);
        UpdateSelection();
        UpdateVisibility();
        pendingScroll = state.BagScroll;
        scrollFrames = 3;
    }

    void BuildGear(VBoxContainer column)
    {
        // The fixed two-slot equipment composition stays visible at wide sizes.
        // On a short single-pane view, its contents can scroll so neither action
        // is pushed outside the playable safe area at 140% text size.
        VBoxContainer content = column;
        if (Mode == LayoutKind.Single)
        {
            var scroll = new TouchScroll { Name = "equipment_scroll",
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Reserve,
                ScrollDeadzone = 12 };
            column.AddChild(scroll);
            content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(content);
        }

        var intro = UiDesign.Card(content, "wayfarer_portrait_mount");
        UiDesign.Heading(intro, "WAYFARER");
        UiDesign.Note(intro, "Your travelling equipment");
        foreach (var slot in new[] { "weapon", "body" })
        {
            var current = equipped.GetValueOrDefault(slot);
            var card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddThemeConstantOverride("separation", 4);
            content.AddChild(card);
            var button = UiDesign.Action(card, "", () =>
            {
                if (current == null)
                {
                    ui.Notify(slot == "weapon" ? "Weapon slot is empty" : "Body slot is empty", minor: true);
                    return;
                }
                state.SelectEquipped(slot, current.Selector);
                state.DetailOpen = true;
                UpdateSelection(); UpdateInspector(); UpdateVisibility();
            });
            button.Name = "inspect_" + slot;
            button.CustomMinimumSize = new Vector2(0, 80 * ui.TextScale);
            button.TooltipText = current == null ? slot + " is empty" : current.Title + " (equipped)";
            gearButtons[slot] = button;
            var padding = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            padding.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            padding.AddThemeConstantOverride("margin_left", 8);
            padding.AddThemeConstantOverride("margin_right", 8);
            padding.AddThemeConstantOverride("margin_top", 4);
            padding.AddThemeConstantOverride("margin_bottom", 4);
            button.AddChild(padding);
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            padding.AddChild(row);
            if (current != null)
            {
                row.AddChild(new TextureRect { Texture = ItemIconAtlas.Get(current.Id),
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    CustomMinimumSize = new Vector2(48 * ui.TextScale, 48 * ui.TextScale),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore });
            }
            else
            {
                var empty = UiDesign.Text("◇");
                empty.CustomMinimumSize = new Vector2(48 * ui.TextScale, 48 * ui.TextScale);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.VerticalAlignment = VerticalAlignment.Center;
                row.AddChild(empty);
            }
            var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            identity.AddThemeConstantOverride("separation", 3);
            row.AddChild(identity);
            var caption = UiDesign.Heading(identity, slot == "weapon" ? "WEAPON" : "BODY");
            caption.AddThemeFontSizeOverride("font_size", (int)(14 * ui.TextScale));
            var itemName = UiDesign.Text(current?.Title ?? "Empty slot");
            itemName.AddThemeFontSizeOverride("font_size", (int)(17 * ui.TextScale));
            identity.AddChild(itemName);
            if (current == null) continue;
            var stats = PackScreen.Stats(current);
            if (stats != "") UiDesign.Note(card, stats);
            var unequip = UiDesign.Action(card, "Unequip", () => Unequip(slot, current));
            unequip.Name = "unequip_" + slot;
            unequip.TooltipText = "Return " + current.Title + " to backpack";
            unequip.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            unequip.CustomMinimumSize = new Vector2(132 * ui.TextScale, UiDesign.Target);
            if (!ui.App.World.Items.CanUnequip(ui.App.World.Player.Id, slot))
            {
                unequip.Disabled = true;
                UiDesign.Note(card, "Pack full · make room to unequip");
            }
        }
    }

    void BuildBag(VBoxContainer column)
    {
        if (bag.Count == 0)
        {
            UiDesign.Note(column, "Your backpack is empty. Search chests, collect plants and explore the ruins.");
            return;
        }
        bagScroll = new TouchScroll { Name = "backpack_scroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        bagScroll.GetVScrollBar().CustomMinimumSize = new Vector2(28, 0);
        column.AddChild(bagScroll);

        float scale = ui.TextScale;
        float usableWidth = Math.Max(220, ui.PackAvailableSize.X - 32);
        float share = Mode switch { LayoutKind.Wide => .41f, LayoutKind.Compact => .64f, _ => 1f };
        int columns = Math.Clamp((int)((usableWidth * share - 24 + 7) / (78 * scale + 7)), 2, 6);
        var grid = new GridContainer { Name = "item_grid", Columns = columns,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 7);
        grid.AddThemeConstantOverride("v_separation", 7);
        bagScroll.AddChild(grid);
        foreach (var item in bag)
        {
            var tile = new ItemTile(item, scale, () =>
            {
                state.SelectBag(item.Selector);
                state.DetailOpen = true;
                UpdateSelection(); UpdateInspector(); UpdateVisibility();
            });
            grid.AddChild(tile);
            tiles[item.Selector] = tile;
        }
        bagScroll.GetVScrollBar().ValueChanged += value => state.BagScroll = (int)value;
    }

    void BuildInspector(VBoxContainer column)
    {
        if (Mode != LayoutKind.Wide)
        {
            var toBag = UiDesign.Action(column, "Back to items", () => DismissDetail());
            toBag.Name = "back_to_items";
        }
        var scroll = new TouchScroll { Name = "inspector_scroll",
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        column.AddChild(scroll);
        inspectorDetails = new VBoxContainer { Name = "item_detail", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inspectorDetails.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(inspectorDetails);
        inspectorActions = new VBoxContainer { Name = "inspector_actions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inspectorActions.AddThemeConstantOverride("separation", 5);
        column.AddChild(inspectorActions);
        UpdateInspector();
    }

    static void Clear(Node container)
    {
        foreach (var child in container.GetChildren()) { container.RemoveChild(child); child.QueueFree(); }
    }

    void UpdateInspector()
    {
        if (inspectorDetails == null || inspectorActions == null) return;
        Clear(inspectorDetails); Clear(inspectorActions);
        var selected = state.Reconcile(snapshot);
        if (selected == null)
        {
            UiDesign.Note(inspectorDetails, "Select a backpack item or a piece of equipment to inspect it.");
            return;
        }

        var banner = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inspectorDetails.AddChild(banner);
        banner.AddChild(new TextureRect { Texture = ItemIconAtlas.Get(selected.Id),
            CustomMinimumSize = new Vector2(56 * ui.TextScale, 56 * ui.TextScale),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        var caption = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        banner.AddChild(caption);
        UiDesign.Heading(caption, selected.Title.ToUpperInvariant());
        UiDesign.Note(caption, state.EquippedSlot != null ? "EQUIPPED · " + state.EquippedSlot :
            selected.Slot != "" ? "Gear · " + selected.Slot : selected.Usable ? "Consumable" :
            selected.Id == "coin" ? "Currency" : "Inventory item");

        var details = new List<string>();
        if (selected.Count > 1 || selected.MaxStack > 1)
            details.Add("Quantity  " + selected.Count + " · maximum per stack " + selected.MaxStack);
        if (selected.Instance != "") details.Add("Instance " + selected.Instance.Replace("item:", "#"));
        if (PackScreen.Describe(selected) != "") details.Add(PackScreen.Describe(selected));
        if (selected.Properties.TryGetValue("mass", out var mass)) details.Add("Mass " + mass);
        foreach (var fact in details) UiDesign.Note(inspectorDetails, fact);
        if (PackScreen.Stats(selected) is var stats && stats != "")
        {
            UiDesign.Heading(inspectorDetails, "ATTRIBUTES");
            UiDesign.Note(inspectorDetails, stats);
        }

        if (state.EquippedSlot != null)
        {
            UiDesign.Note(inspectorDetails, "This item is currently equipped.");
            string slot = state.EquippedSlot;
            var unequip = UiDesign.Action(inspectorActions, "Unequip", () => Unequip(slot, selected));
            unequip.Name = "unequip_inspector_" + slot;
            if (!ui.App.World.Items.CanUnequip(ui.App.World.Player.Id, slot))
            {
                unequip.Disabled = true;
                UiDesign.Note(inspectorDetails, "Pack full · make room to unequip");
            }
        }
        else if (selected.Slot != "")
        {
            UiDesign.Heading(inspectorDetails, "EQUIPMENT COMPARISON");
            UiDesign.Note(inspectorDetails, PackScreen.Comparison(selected,
                equipped.GetValueOrDefault(selected.Slot)));
            string? reason = PackScreen.EquipBlocker(ui.App, selected);
            var equip = UiDesign.Action(inspectorActions, "Equip " + selected.Title, () => Equip(selected));
            equip.Name = "equip";
            if (reason != null) { equip.Disabled = true; UiDesign.Note(inspectorDetails, reason); }
        }
        if (state.EquippedSlot == null && selected.Usable)
        {
            var use = UiDesign.Action(inspectorActions,
                "Use · restores " + selected.Stat("healing").ToString("0") + " health",
                () => ui.UseFromPack(selected.Selector));
            use.Name = "use";
            if (ui.App.World.Player.Health >= ui.App.World.Player.MaxHealth)
                UiDesign.Note(inspectorDetails, "Health is already full; using this now restores nothing.");
        }
        if (selected.Slot == "" && !selected.Usable)
        {
            string reason = ui.App.World.Content.Recipes.Any(r => r.Ingredients.ContainsKey(selected.Id))
                ? "Crafting material · usable in the Craft section" :
                selected.Id == "coin" ? "Currency · used when buying from merchants" :
                "Keep this item; it may be useful during your adventures.";
            UiDesign.Note(inspectorDetails, reason);
        }
    }

    void Equip(ItemView item)
    {
        bool ok = ui.App.ManageItem(() => ui.App.World.Items.Equip(ui.App.World.Player, item.Selector, item.Slot));
        ui.Notify(ok ? "Equipped " + item.Title : PackScreen.EquipBlocker(ui.App, item) ?? "Could not equip " + item.Title);
        if (ok) state.SelectEquipped(item.Slot, item.Selector);
        ui.Inventory();
    }

    void Unequip(string slot, ItemView item)
    {
        bool ok = ui.App.ManageItem(() => ui.App.World.Items.Unequip(ui.App.World.Player, slot));
        ui.Notify(ok ? item.Title + " returned to pack" : "Pack full · make room to unequip " + item.Title);
        if (ok) state.SelectBag(item.Selector);
        ui.Inventory();
    }

    void UpdateSelection()
    {
        foreach (var (selector, tile) in tiles)
            tile.SetSelected(state.EquippedSlot == null && state.Selector == selector);
        foreach (var (slot, button) in gearButtons)
        {
            bool selected = state.EquippedSlot == slot;
            var face = UiDesign.InventoryTile(selected);
            button.AddThemeStyleboxOverride("normal", face);
            button.AddThemeStyleboxOverride("hover", face);
        }
    }

    void UpdateVisibility()
    {
        if (gearPanel == null || bagPanel == null || detailPanel == null) return;
        if (Mode == LayoutKind.Wide)
        {
            gearPanel.Show(); bagPanel.Show(); detailPanel.Show();
            return;
        }
        bool details = state.DetailOpen && state.Reconcile(snapshot) != null;
        if (Mode == LayoutKind.Compact)
        {
            gearPanel.Show();
            bagPanel.Visible = !details;
            detailPanel.Visible = details;
        }
        else
        {
            gearPanel.Visible = !details && state.GearOpen;
            bagPanel.Visible = !details && !state.GearOpen;
            detailPanel.Visible = details;
        }
        // A simple view switch is local; the header remains fixed and accessible.
    }

    public bool DismissDetail()
    {
        if (Mode == LayoutKind.Wide) return false;
        if (state.DetailOpen)
        {
            state.DetailOpen = false;
            UpdateVisibility();
            return true;
        }
        if (Mode == LayoutKind.Single && state.GearOpen)
        {
            state.GearOpen = false;
            UpdateVisibility();
            return true;
        }
        return false;
    }

    public override void _Process(double delta)
    {
        if (pendingScroll < 0 || --scrollFrames > 0) return;
        if (bagScroll != null && GodotObject.IsInstanceValid(bagScroll))
            bagScroll.ScrollVertical = pendingScroll;
        pendingScroll = -1;
    }
}
