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

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 12);
        AddChild(header);
        var top = UiDesign.Tabs(header, PackScreen.Tabs, 0, tab =>
        {
            ui.PackTab = tab;
            state.DetailOpen = false;
            ui.Inventory();
        });
        top.Name = "pack_nav";
        top.GetChild<Control>(2).Visible = ui.App.NearbyMerchant() != null;
        // Small fixed navigation choices: they never steal width from the three
        // inventory regions and keep the backpack as the dominant surface.
        var track = top.GetParent<Control>(); track.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        foreach (var tab in top.GetChildren().OfType<Button>())
        {
            tab.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            tab.CustomMinimumSize = new Vector2(112 * ui.TextScale, UiDesign.Target - 8);
        }
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        int coins = bag.Where(v => v.Id == "coin").Sum(v => v.Count);
        void Purse(Node parent)
        {
            var purse = UiDesign.Chip(parent, coins + " crowns", Tone.Gold, "coin");
            purse.Name = "purse"; purse.TooltipText = "Crowns · " + bag.Count + (bag.Count == 1 ? " stack" : " stacks") + " in your backpack";
        }
        if (Mode != LayoutKind.Single) Purse(header);
        if (Mode == LayoutKind.Single)
        {
            // One local destination at a time keeps the header compact and leaves
            // enough vertical room for the inspector at the largest text size.
            var switcher = new HBoxContainer { Name = "single_region_switcher",
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
            switcher.AddThemeConstantOverride("separation", 6);
            header.AddChild(switcher);
            var backpack = UiDesign.Action(switcher, "Bag", () => { state.GearOpen = false; state.DetailOpen = false; UpdateVisibility(); });
            backpack.Name = "view_backpack"; backpack.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            backpack.CustomMinimumSize = new Vector2(80 * ui.TextScale, UiDesign.Target);
            backpack.TooltipText = "View backpack items";
            var equipment = UiDesign.Action(switcher, "Gear", () => { state.GearOpen = true; state.DetailOpen = false; UpdateVisibility(); });
            equipment.Name = "view_equipment"; equipment.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            equipment.CustomMinimumSize = new Vector2(80 * ui.TextScale, UiDesign.Target);
            equipment.TooltipText = "View weapon and body equipment";
        }
        var regions = new HBoxContainer { Name = "pack_regions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        regions.AddThemeConstantOverride("separation", 10);
        AddChild(regions);
        var gearContent = UiDesign.InventoryPanel(regions, "equipment_panel", "EQUIPPED", "shield");
        gearPanel = (Control)gearContent.GetParent();
        var bagContent = UiDesign.InventoryPanel(regions, "backpack_panel", "BACKPACK", "bag");
        bagPanel = (Control)bagContent.GetParent();
        var bagHeader = bagContent.GetChild<HBoxContainer>(0);
        bagHeader.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        if (Mode == LayoutKind.Single) Purse(bagHeader);
        else UiDesign.Caption(bagHeader, bag.Count + (bag.Count == 1 ? " stack" : " stacks"));
        var detailContent = UiDesign.InventoryPanel(regions, "inspector_panel", "INSPECT", "eye");
        detailPanel = (Control)detailContent.GetParent();
        if (Mode == LayoutKind.Wide)
        {
            gearPanel.SizeFlagsStretchRatio = .27f;
            bagPanel.SizeFlagsStretchRatio = .40f;
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
        if (Mode == LayoutKind.Single || ui.TextScale > 1.2f || ui.PackAvailableSize.Y < 640)
        {
            var scroll = new TouchScroll { Name = "equipment_scroll",
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
                ScrollDeadzone = 12 };
            column.AddChild(scroll);
            content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(content);
        }

        var actor = ui.App.World.Player;
        var intro = UiDesign.Well(content, "wayfarer_portrait_mount");
        var hero = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        hero.AddThemeConstantOverride("separation", 10);
        intro.AddChild(hero);
        var portrait = HeroPortrait.Get(ui.App.Views.GetValueOrDefault(actor.Id)?.Creature?.Model);
        var frame = new Control { CustomMinimumSize = new Vector2(64, 64) * Math.Min(ui.TextScale, 1.2f), MouseFilter = MouseFilterEnum.Ignore };
        hero.AddChild(frame);
        var glow = new Halo(); frame.AddChild(glow); glow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (portrait != null)
        {
            var picture = new TextureRect { Texture = portrait, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
            frame.AddChild(picture); picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        else frame.AddChild(new Glyph("person", UiDesign.Gold, 40));
        var who = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        who.AddThemeConstantOverride("separation", 0);
        hero.AddChild(who);
        UiDesign.Heading(who, actor.Name.ToUpperInvariant());
        UiDesign.Note(who, "Level " + actor.Level + " · your travelling gear");
        foreach (var slot in new[] { "weapon", "body" })
        {
            var current = equipped.GetValueOrDefault(slot);
            var card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddThemeConstantOverride("separation", 4);
            content.AddChild(card);
            var button = new RichButton(current?.Title ?? (slot == "weapon" ? "Weapon slot" : "Body slot"), 10, 8)
            {
                Name = "inspect_" + slot, SizeFlagsHorizontal = SizeFlags.ExpandFill, MinHeight = 76 * ui.TextScale,
                TooltipText = current == null ? slot + " is empty" : current.Title + " (equipped)"
            };
            button.ThemeTypeVariation = "RowButton";
            button.Pressed += () =>
            {
                if (current == null)
                {
                    ui.Notify(slot == "weapon" ? "Weapon slot is empty" : "Body slot is empty", minor: true);
                    return;
                }
                state.SelectEquipped(slot, current.Selector);
                state.DetailOpen = true;
                UpdateSelection(); UpdateInspector(); UpdateVisibility();
            };
            card.AddChild(button);
            gearButtons[slot] = button;
            var well = UiDesign.IconWell(button.Content, current == null ? null : ItemIconAtlas.Get(current.Id), 56 * Math.Min(ui.TextScale, 1.25f), current != null);
            if (current == null) well.GetChild(0).AddChild(new Glyph(slot == "weapon" ? "sword" : "shield", new Color(UiDesign.Faint, .45f), 28) { Position = new Vector2(14, 14) * Math.Min(ui.TextScale, 1.25f) });
            var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            identity.AddThemeConstantOverride("separation", 1);
            button.Content.AddChild(identity);
            UiDesign.Caption(identity, slot == "weapon" ? "Weapon" : "Body");
            var itemName = UiDesign.Text(current?.Title ?? "Empty slot");
            itemName.ThemeTypeVariation = current == null ? "RowSubtitle" : "RowTitle";
            identity.AddChild(itemName);
            if (current == null) continue;
            var stats = PackScreen.Stats(current);
            if (stats != "") { var line = UiDesign.Text(stats); line.ThemeTypeVariation = "RowSubtitle"; identity.AddChild(line); }
            var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddChild(actions);
            actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            var unequip = UiDesign.Quiet(UiDesign.Action(actions, "Unequip", () => Unequip(slot, current)));
            unequip.Name = "unequip_" + slot;
            unequip.TooltipText = "Return " + current.Title + " to backpack";
            unequip.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            unequip.CustomMinimumSize = new Vector2(124 * ui.TextScale, UiDesign.Target);
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
            var empty = UiDesign.Well(column);
            empty.AddChild(new Glyph("bag", new Color(UiDesign.Gold, .6f), 40) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
            var note = UiDesign.Note(empty, "Your backpack is empty. Search chests, collect plants and explore the ruins.");
            note.HorizontalAlignment = HorizontalAlignment.Center;
            return;
        }
        bagScroll = new TouchScroll { Name = "backpack_scroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        bagScroll.GetVScrollBar().CustomMinimumSize = new Vector2(28, 0);
        column.AddChild(bagScroll);

        float scale = ui.TextScale;
        float usableWidth = Math.Max(220, ui.PackAvailableSize.X - 32);
        float share = Mode switch { LayoutKind.Wide => .40f, LayoutKind.Compact => .64f, _ => 1f };
        int columns = Math.Clamp((int)((usableWidth * share - 24 - 28 + 8) / (ItemTile.Width * scale + 8)), 2, 6);
        var grid = new GridContainer { Name = "item_grid", Columns = columns,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
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
        // Complete the last row and show a little spare capacity so the bag reads as a bag.
        int cells = Math.Max(columns * 3, (bag.Count + columns - 1) / columns * columns);
        for (int i = bag.Count; i < cells; i++) grid.AddChild(new EmptySlot(scale) { Name = "empty_slot_" + i });
        bagScroll.GetVScrollBar().ValueChanged += value => state.BagScroll = (int)value;
    }

    void BuildInspector(VBoxContainer column)
    {
        if (Mode != LayoutKind.Wide)
        {
            var toBag = UiDesign.Quiet(UiDesign.Action(column, "‹  Back to items", () => DismissDetail()));
            toBag.Name = "back_to_items";
            toBag.Alignment = HorizontalAlignment.Left;
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
        inspectorActions.AddThemeConstantOverride("separation", 6);
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
            var hint = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            inspectorDetails.AddChild(hint);
            hint.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
            hint.AddChild(new Glyph("eye", new Color(UiDesign.Gold, .55f), 44) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
            var note = UiDesign.Note(hint, "Select a backpack item or a piece of equipment to inspect it.");
            note.HorizontalAlignment = HorizontalAlignment.Center;
            return;
        }

        float scale = Math.Min(ui.TextScale, 1.25f);
        var banner = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        banner.AddThemeConstantOverride("separation", 12);
        inspectorDetails.AddChild(banner);
        UiDesign.IconWell(banner, ItemIconAtlas.Get(selected.Id), 84 * scale);
        var caption = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        caption.AddThemeConstantOverride("separation", 4);
        banner.AddChild(caption);
        var title = UiDesign.Heading(caption, selected.Title.ToUpperInvariant());
        title.AddThemeFontSizeOverride("font_size", (int)(19 * ui.TextScale));
        title.AddThemeColorOverride("font_color", UiDesign.GoldBright);
        var chips = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        chips.AddThemeConstantOverride("h_separation", 6); chips.AddThemeConstantOverride("v_separation", 4);
        caption.AddChild(chips);
        UiDesign.Chip(chips, state.EquippedSlot != null ? "Equipped · " + state.EquippedSlot : PackScreen.Kind(selected), state.EquippedSlot != null ? Tone.Moss : PackScreen.KindTone(selected),
            state.EquippedSlot != null ? "check" : null);
        if (PackScreen.Describe(selected) is var described && described != "") UiDesign.Chip(chips, described, Tone.Neutral);

        UiDesign.Divider(inspectorDetails, ornament: false);
        var facts = new VBoxContainer();
        facts.AddThemeConstantOverride("separation", 6);
        inspectorDetails.AddChild(facts);
        if (selected.Count > 1 || selected.MaxStack > 1)
            UiDesign.Stat(facts, "bag", "Quantity", selected.Count + " of " + selected.MaxStack + " per stack", tone: Tone.Neutral);
        if (selected.Instance != "") UiDesign.Stat(facts, "diamond-outline", "Instance", selected.Instance.Replace("item:", "#"), tone: Tone.Neutral);
        if (selected.Properties.TryGetValue("mass", out var mass)) UiDesign.Stat(facts, "dot", "Mass", mass, tone: Tone.Neutral);

        var current = selected.Slot != "" && state.EquippedSlot == null ? equipped.GetValueOrDefault(selected.Slot) : null;
        var rows = PackScreen.StatRows(selected, current).ToList();
        if (rows.Count > 0)
        {
            UiDesign.Heading(inspectorDetails, current != null ? "ATTRIBUTES · VS " + current.Title.ToUpperInvariant() : "ATTRIBUTES");
            var sheet = UiDesign.Well(inspectorDetails, "item_attributes");
            foreach (var (stat, label, value) in rows)
            {
                var delta = PackScreen.Delta(selected, current, stat);
                UiDesign.Stat(sheet, PackScreen.StatGlyph(stat), label, value, delta?.Text, delta?.Better ?? true);
            }
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
            if (current == null) UiDesign.Note(inspectorDetails, PackScreen.Comparison(selected, null));
            string? reason = PackScreen.EquipBlocker(ui.App, selected);
            var equip = UiDesign.Primary(UiDesign.Action(inspectorActions, "Equip " + selected.Title, () => Equip(selected)));
            equip.Name = "equip";
            if (reason != null) { equip.Disabled = true; UiDesign.Note(inspectorDetails, reason); }
        }
        if (state.EquippedSlot == null && selected.Usable)
        {
            var use = UiDesign.Primary(UiDesign.Action(inspectorActions,
                "Use · restores " + selected.Stat("healing").ToString("0") + " health",
                () => ui.UseFromPack(selected.Selector)));
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
            var usage = UiDesign.Card(inspectorDetails, null, "Quote");
            UiDesign.Note(usage, reason);
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
            UiDesign.SetSelected(button, state.EquippedSlot == slot);
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
            if (FindChild("view_backpack", true, false) is Control bagSwitch) bagSwitch.Visible = details || state.GearOpen;
            if (FindChild("view_equipment", true, false) is Control gearSwitch) gearSwitch.Visible = !details && !state.GearOpen;
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
