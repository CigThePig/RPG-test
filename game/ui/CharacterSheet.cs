using Godot;
namespace Mosslight;

/// <summary>Read-only hero presentation. Shares the world's PPC texture; never creates a second actor or renderer.</summary>
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
        scroll.AddChild(column);
        return column;
    }
    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        Compact = ui.PackAvailableSize.X / ui.TextScale < 1000;
        var actor = ui.App.World.Player;
        var summary = UiDesign.Text($"{actor.Name} · Level {actor.Level} · {actor.SkillPoints} skill points");
        summary.Name = "hero_identity"; AddChild(summary);
        var regions = new HBoxContainer { Name = "character_regions", SizeFlagsVertical = SizeFlags.ExpandFill };
        regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
        if (!Compact)
        {
            var hero = Scroll(regions, "hero_scroll");
            hero.GetParent<Control>().SizeFlagsStretchRatio = .36f;
            Hero(hero);
        }
        var pages = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = .64f };
        regions.AddChild(pages);
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
        UiDesign.Heading(column, "THE WAYFARER");
        // CreatureActor already caches its last render. Sharing its texture is bounded,
        // follows actual equipped appearance, and respects reduced motion while paused.
        var texture = app.Views.GetValueOrDefault(actor.Id)?.Creature?.Texture;
        if (texture != null)
        {
            // PPC canvases include transparent space for the full animation range.
            // Inspect alpha once on opening/rebuild, then share only the occupied
            // region through an atlas view. No extra creature or texture upload.
            using var image = texture.GetImage();
            var occupied = image.GetUsedRect();
            Texture2D portrait = occupied.HasArea() ? new AtlasTexture { Atlas = texture, Region = occupied, FilterClip = true } : texture;
            column.AddChild(new TextureRect { Name = "hero_portrait", Texture = portrait,
                CustomMinimumSize = new Vector2(0, 190 * ui.TextScale),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore });
        }
        else UiDesign.Note(column, actor.Appearance.Family + " · Portrait unavailable");
        UiDesign.Note(column, $"{actor.Appearance.Family} · {actor.Region}\nHealth {actor.Health:0} / {actor.MaxHealth} · Mana {actor.Mana:0} / {actor.MaxMana}");
        var xp = new ProgressBar { Name = "character_experience", MinValue = 0, MaxValue = actor.Level * 100,
            Value = actor.Experience, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 16), MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(xp);
        UiDesign.Note(column, $"Experience {actor.Experience} / {actor.Level * 100}");
        UiDesign.Heading(column, "EQUIPPED GEAR");
        var gear = app.World.Items.View(actor.Id).Where(v => v.Equipped).ToList();
        foreach (var slot in new[] { "weapon", "body" })
        {
            var item = gear.FirstOrDefault(v => v.EquippedSlot == slot);
            var row = new HBoxContainer(); column.AddChild(row);
            if (item != null) row.AddChild(new TextureRect { Texture = ItemIconAtlas.Get(item.Id),
                CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore });
            UiDesign.Note(row, slot.ToUpperInvariant() + " · " + (item?.Title ?? "Empty"));
        }
        var equipment = UiDesign.Action(column, "Manage equipment", ui.Inventory); equipment.Name = "character_equipment";
    }

    void Attributes(VBoxContainer column)
    {
        var app = ui.App; var actor = app.World.Player;
        UiDesign.Heading(column, "COMBAT ATTRIBUTES");
        UiDesign.Note(column, "Effective totals include gear, training and active effects.");
        foreach (var stat in new[] { "strength", "defence", "power" })
        {
            var line = UiDesign.Card(column, "attribute_" + stat);
            UiDesign.Note(line, CharacterOverview.Breakdown(app.World, actor, stat));
        }
        UiDesign.Note(column, $"Weapon damage bonus {app.World.Items.Bonus(actor, "damage"):+0.##;-0.##;0}");
        UiDesign.Heading(column, "EXPLORATION & STANDING");
        UiDesign.Note(column, $"Flight: {(actor.Flies ? "Yes" : "No")} · Swimming: {(actor.Swims ? "Yes" : "No")}\nGrasping: {(actor.Grasps ? "Yes" : "No")}\nWarden reputation {actor.Reputation.GetValueOrDefault("wardens")}");
    }
}
