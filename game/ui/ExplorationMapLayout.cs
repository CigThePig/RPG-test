using Godot;
namespace Mosslight;

/// <summary>Map-first exploration view, with optional independently scrolling guide and bounded pan controls.</summary>
public partial class ExplorationMapLayout : VBoxContainer
{
    readonly RpgUi ui;
    public ExplorationMapLayout(RpgUi owner) { ui = owner; Name = "exploration_map"; }
    public override void _Ready() => Rebuild();
    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddThemeConstantOverride("separation", 10);
        var region = ui.App.World.RegionsById[ui.App.World.Player.Region];
        bool compact = ui.PackAvailableSize.X / ui.TextScale < 1000;
        if (ui.MapRegion != region.Id) { ui.MapRegion = region.Id; ui.MapCentre = null; }
        var toolbar = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toolbar.AddThemeConstantOverride("separation", 10);
        AddChild(toolbar);
        var bar = UiDesign.Tabs(toolbar, new[] { "Whole map", "Around you" }, ui.MapZoomed ? 1 : 0,
            i => { ui.MapZoomed = i == 1; ui.MapCentre = null; ui.Map(); }, "map_");
        bar.GetParent<Control>().SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        foreach (var tab in bar.GetChildren().OfType<Button>()) { tab.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; tab.CustomMinimumSize = new Vector2(132 * ui.TextScale, UiDesign.Target - 8); }
        toolbar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        bool ruin = region.Id == "ruin";
        string where = ruin ? $"The Ruin · {MapScreen.ExploredPercent(region)}% explored" : "The Reach · fully known";
        // At large text sizes the region chip cannot share one row with both
        // zoom tabs and Guide. Its own row keeps every action inside the safe area.
        var regionLabel = UiDesign.Chip(compact ? this : toolbar, where, ruin ? Tone.Arcane : Tone.Moss, ruin ? "eye" : "map");
        regionLabel.Name = "map_region";
        var guideToggle = new RichButton("Guide", 14, 6) { Name = "map_guide", CustomMinimumSize = new Vector2(120 * ui.TextScale, UiDesign.Target), TooltipText = ui.MapGuide ? "Hide the legend and rooms" : "Show the legend and rooms" };
        UiDesign.SetSelected(guideToggle, ui.MapGuide, "Button", "RowSelected");
        guideToggle.Content.Alignment = BoxContainer.AlignmentMode.Center;
        guideToggle.Content.AddChild(new Glyph("book", UiDesign.Gold, 18));
        guideToggle.Content.AddChild(new Label { Text = "Guide", ThemeTypeVariation = "RowTitle", MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center });
        guideToggle.Pressed += () => { ui.MapGuide = !ui.MapGuide; ui.Map(); };
        toolbar.AddChild(guideToggle);
        var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
        var frame = new PanelContainer { Name = "map_frame", ThemeTypeVariation = "IconWell", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .7f };
        regions.AddChild(frame); frame.Visible = !compact || !ui.MapGuide;
        var canvas = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        frame.AddChild(canvas);
        var map = new RegionMap { Name = "region_map", Region = region, Player = ui.App.World.Player,
            Zoomed = ui.MapZoomed, Centre = ui.MapCentre, CustomMinimumSize = new Vector2(0, 80), MouseFilter = MouseFilterEnum.Stop };
        canvas.AddChild(map); map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (ui.MapZoomed)
        {
            // Bounded pan pad floats over the map's lower-right corner.
            float b = Math.Max(UiDesign.Target, 52 * ui.TextScale);
            var pad = new PanelContainer { Name = "map_pan", ThemeTypeVariation = "Plate", MouseFilter = MouseFilterEnum.Pass };
            canvas.AddChild(pad);
            pad.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
            pad.GrowHorizontal = GrowDirection.Begin; pad.GrowVertical = GrowDirection.Begin;
            pad.OffsetRight = -12; pad.OffsetBottom = -12;
            var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4);
            pad.AddChild(grid);
            Control Gap() { var gap = new Control { CustomMinimumSize = new Vector2(b, b), MouseFilter = MouseFilterEnum.Ignore }; grid.AddChild(gap); return gap; }
            void Move(string name, string glyph, string tip, int dx, int dy)
            {
                var move = new RichButton(tip, 0, 0) { Name = "map_pan_" + name, CustomMinimumSize = new Vector2(b, b), MinHeight = b, TooltipText = tip };
                move.Content.Alignment = BoxContainer.AlignmentMode.Center;
                move.Content.AddChild(new Glyph(glyph, UiDesign.GoldBright, b * .36f) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
                move.Pressed += () =>
                {
                    var centre = ui.MapCentre ?? new Vector2I((int)(map.Player.X / RegionState.Tile), (int)(map.Player.Y / RegionState.Tile));
                    ui.MapCentre = new Vector2I(Math.Clamp(centre.X + dx, 0, region.Width - 1), Math.Clamp(centre.Y + dy, 0, region.Height - 1));
                    map.Centre = ui.MapCentre; map.QueueRedraw();
                };
                grid.AddChild(move);
            }
            Gap(); Move("north", "up", "North", 0, -6); Gap();
            Move("west", "left", "West", -8, 0);
            var centreButton = new RichButton("You", 0, 0) { Name = "map_recenter", CustomMinimumSize = new Vector2(b, b), MinHeight = b, TooltipText = "Centre on you" };
            centreButton.ThemeTypeVariation = "PrimaryButton";
            centreButton.Content.Alignment = BoxContainer.AlignmentMode.Center;
            centreButton.Content.AddChild(new Glyph("target", UiDesign.GoldInk, b * .44f) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
            centreButton.Pressed += () => { ui.MapCentre = map.Centre = null; map.QueueRedraw(); };
            grid.AddChild(centreButton);
            Move("east", "right", "East", 8, 0);
            Gap(); Move("south", "down", "South", 0, 6); Gap();
        }
        if (ui.MapGuide)
        {
            var column = UiDesign.InventoryPanel(regions, "map_guide_pane", "GUIDE", "compass");
            column.GetParent<Control>().SizeFlagsStretchRatio = .3f;
            var guide = JournalLayout.Scroll(column, "map_guide_scroll");
            MapScreen.Guide(guide, ui);
        }
    }
    public bool DismissGuide()
    {
        if (!ui.MapGuide) return false;
        ui.MapGuide = false; Rebuild(); return true;
    }
}
