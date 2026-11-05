using Godot;
namespace Mosslight;

/// <summary>Read-only hero presentation. Uses an independent cached PPC portrait; never changes the gameplay actor.</summary>
public partial class CharacterSheet : VBoxContainer
{
    readonly RpgUi ui;
    public bool Compact { get; private set; }
    public CharacterSheet(RpgUi owner) { ui = owner; Name = "character_sheet"; }
    public override void _Ready() => Rebuild();

    static VBoxContainer Scroll(Node parent, string name)
    {
        var scroll = new TouchScroll { Name = name, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        parent.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(column);
        return column;
    }
    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddThemeConstantOverride("separation", 10);
        Compact = ui.PackAvailableSize.X / ui.TextScale < 1000;
        var actor = ui.App.World.Player;
        var regions = new HBoxContainer { Name = "character_regions", SizeFlagsVertical = SizeFlags.ExpandFill };
        regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
        if (!Compact)
        {
            var pane = UiDesign.InventoryPanel(regions, "hero_pane", "THE WAYFARER", "person");
            pane.GetParent<Control>().SizeFlagsStretchRatio = .36f;
            Hero(Scroll(pane, "hero_scroll"));
        }
        var pages = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = .64f };
        pages.AddThemeConstantOverride("separation", 10);
        regions.AddChild(pages);
        var identity = new HBoxContainer { Name = "hero_identity", MouseFilter = MouseFilterEnum.Ignore };
        identity.AddThemeConstantOverride("separation", 8);
        pages.AddChild(identity);
        UiDesign.Chip(identity, "Level " + actor.Level, Tone.Gold, "star");
        UiDesign.Chip(identity, actor.SkillPoints + (actor.SkillPoints == 1 ? " skill point" : " skill points"), actor.SkillPoints > 0 ? Tone.Moss : Tone.Neutral, actor.SkillPoints > 0 ? "plus" : null);
        UiDesign.Chip(identity, actor.Abilities.Count + (actor.Abilities.Count == 1 ? " ability" : " abilities"), Tone.Arcane);
        // Compact gets a dedicated Hero page; other views retain a full-width reading area.
        string[] labels = Compact ? new[] { "Hero", "Stats", "Train", "Abilities" } : new[] { "Stats", "Train", "Abilities" };
        int selected = Compact ? ui.CharacterTab : Math.Max(0, ui.CharacterTab - 1);
        UiDesign.Tabs(pages, labels, selected, index =>
        {
            ui.CharacterTab = Compact ? index : index + 1;
            ui.Character();
        }, "character_tab_");
        var content = Scroll(pages, "character_detail_scroll");
        if (Compact && ui.CharacterTab == 0) Hero(content);
        else if (ui.CharacterTab <= 1) Attributes(content);
        else CharacterOverview.Progression(content, ui.App, ui, abilities: ui.CharacterTab == 3, skills: ui.CharacterTab == 2);
    }

    void Hero(VBoxContainer column)
    {
        var app = ui.App; var actor = app.World.Player;
        float scale = Math.Min(ui.TextScale, 1.2f);
        var pedestal = new PanelContainer { ThemeTypeVariation = "Well", Name = "hero_pedestal", MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(pedestal);
        var stage = new Control { CustomMinimumSize = new Vector2(0, 168 * scale), MouseFilter = MouseFilterEnum.Ignore };
        pedestal.AddChild(stage);
        var glow = new Halo(); stage.AddChild(glow); glow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var portrait = HeroPortrait.Get(app.Views.GetValueOrDefault(actor.Id)?.Creature?.Model);
        if (portrait != null)
        {
            var picture = new TextureRect { Name = "hero_portrait", Texture = portrait,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
            stage.AddChild(picture); picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            picture.OffsetTop = picture.OffsetBottom = 0; picture.OffsetTop = 8; picture.OffsetBottom = -8;
        }
        else UiDesign.Note(pedestal, actor.Appearance.Family + " · Portrait unavailable");
        var name = UiDesign.Title(column, actor.Name);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        var origin = UiDesign.Caption(column, $"{actor.Appearance.Family} · {(actor.Region == "ruin" ? "The Ruin" : actor.Region == "reach" ? "The Reach" : actor.Region)}");
        origin.HorizontalAlignment = HorizontalAlignment.Center;
        Meter(column, "character_health", "HP", actor.Health, actor.MaxHealth, UiDesign.Health);
        Meter(column, "character_mana", "MP", actor.Mana, actor.MaxMana, UiDesign.Mana);
        Meter(column, "character_experience", "XP", actor.Experience, actor.Level * 100, UiDesign.Experience);
        UiDesign.Heading(column, "EQUIPPED GEAR");
        var gear = app.World.Items.View(actor.Id).Where(v => v.Equipped).ToList();
        foreach (var slot in new[] { "weapon", "body" })
        {
            var item = gear.FirstOrDefault(v => v.EquippedSlot == slot);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 10);
            column.AddChild(row);
            var well = UiDesign.IconWell(row, item == null ? null : ItemIconAtlas.Get(item.Id), 48 * scale, item != null);
            if (item == null) well.GetChild(0).AddChild(new Glyph(slot == "weapon" ? "sword" : "shield", new Color(UiDesign.Faint, .45f), 24) { Position = new Vector2(12, 12) * scale });
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeConstantOverride("separation", 0);
            row.AddChild(text);
            UiDesign.Caption(text, slot == "weapon" ? "Weapon" : "Body");
            var title = UiDesign.Text(item?.Title ?? "Empty"); title.ThemeTypeVariation = item == null ? "RowSubtitle" : "RowTitle"; text.AddChild(title);
        }
        var equipment = UiDesign.Action(column, "Manage equipment", ui.Inventory); equipment.Name = "character_equipment";
    }

    void Meter(VBoxContainer column, string name, string caption, float value, float maximum, Color fill)
    {
        var meter = new Meter { Name = name, Caption = caption, Value = $"{Math.Ceiling(value):0} / {maximum:0}", Fill = fill,
            Ratio = value / Math.Max(1, maximum), CustomMinimumSize = new Vector2(0, 24 * Math.Min(ui.TextScale, 1.2f)) };
        column.AddChild(meter);
    }

    void Attributes(VBoxContainer column)
    {
        var app = ui.App; var actor = app.World.Player;
        UiDesign.Heading(column, "COMBAT ATTRIBUTES");
        UiDesign.Note(column, "Effective totals include gear, training and active effects.");
        foreach (var (stat, glyph, tone) in new[] { ("strength", "sword", Tone.Ember), ("defence", "shield", Tone.Moss), ("power", "bolt", Tone.Arcane) })
        {
            var card = new PanelContainer { Name = "attribute_" + stat, ThemeTypeVariation = "Card", MouseFilter = MouseFilterEnum.Pass };
            column.AddChild(card);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 14);
            card.AddChild(row);
            var badge = new PanelContainer { ThemeTypeVariation = "IconWell", CustomMinimumSize = new Vector2(48, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(badge);
            badge.AddChild(new Glyph(glyph, UiDesign.ToneColor(tone), 26) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeConstantOverride("separation", 1);
            row.AddChild(text);
            var label = UiDesign.Text(char.ToUpperInvariant(stat[0]) + stat[1..]); label.ThemeTypeVariation = "RowTitle"; text.AddChild(label);
            // Full breakdown sentence kept as the accessible description.
            var breakdown = CharacterOverview.Breakdown(app.World, actor, stat);
            var parts = UiDesign.Text(breakdown.Contains('(') ? breakdown[(breakdown.IndexOf('(') + 1)..].TrimEnd(')') : breakdown);
            parts.ThemeTypeVariation = "RowSubtitle"; text.AddChild(parts);
            card.TooltipText = breakdown;
            UiDesign.Value(row, app.World.Stat(actor, stat).ToString("0.##"), "BigValue").SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        }
        var bonus = UiDesign.Well(column, "weapon_bonus");
        UiDesign.Stat(bonus, "sword", "Weapon damage bonus", app.World.Items.Bonus(actor, "damage").ToString("+0.##;-0.##;0"));
        UiDesign.Heading(column, "EXPLORATION & STANDING");
        var traits = new HFlowContainer { MouseFilter = MouseFilterEnum.Ignore };
        traits.AddThemeConstantOverride("h_separation", 8); traits.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(traits);
        foreach (var (label, has, glyph) in new[] { ("Flight", actor.Flies, "feather"), ("Swimming", actor.Swims, "wave"), ("Grasping", actor.Grasps, "hand") })
            UiDesign.Chip(traits, label + (has ? " · yes" : " · no"), has ? Tone.Moss : Tone.Neutral, has ? "check" : "cross");
        var standing = UiDesign.Well(column, "standing");
        UiDesign.Stat(standing, "shield", "Warden reputation", actor.Reputation.GetValueOrDefault("wardens").ToString(), tone: Tone.Moss);
    }
}
