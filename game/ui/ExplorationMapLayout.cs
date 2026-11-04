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
        var region = ui.App.World.RegionsById[ui.App.World.Player.Region];
        if (ui.MapRegion != region.Id) { ui.MapRegion = region.Id; ui.MapCentre = null; }
        UiDesign.Note(this, region.Id == "ruin" ? "THE RUIN · only explored terrain and discovered places are shown" : "THE REACH · known settlement");
        var bar = UiDesign.Tabs(this, new[] { "Whole map", "Around you" }, ui.MapZoomed ? 1 : 0,
            i => { ui.MapZoomed = i == 1; ui.MapCentre = null; ui.Map(); }, "map_");
        var guideToggle = UiDesign.Action(bar, ui.MapGuide ? "[Guide]" : "Guide", () => { ui.MapGuide = !ui.MapGuide; ui.Map(); });
        guideToggle.Name = "map_guide"; guideToggle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
        bool compact = ui.PackAvailableSize.X / ui.TextScale < 1000;
        var canvas = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .7f };
        regions.AddChild(canvas); canvas.Visible = !compact || !ui.MapGuide;
        var map = new RegionMap { Name = "region_map", Region = region, Player = ui.App.World.Player,
            Zoomed = ui.MapZoomed, Centre = ui.MapCentre, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 80), MouseFilter = MouseFilterEnum.Stop };
        canvas.AddChild(map);
        if (ui.MapZoomed)
        {
            var pan = new HBoxContainer(); canvas.AddChild(pan);
            foreach (var (name, label, dx, dy) in new[] { ("west", "West", -8, 0), ("north", "North", 0, -6), ("south", "South", 0, 6), ("east", "East", 8, 0) })
            {
                var move = UiDesign.Action(pan, label, () =>
                {
                    var centre = ui.MapCentre ?? new Vector2I((int)(map.Player.X / RegionState.Tile), (int)(map.Player.Y / RegionState.Tile));
                    ui.MapCentre = new Vector2I(Math.Clamp(centre.X + dx, 0, region.Width - 1), Math.Clamp(centre.Y + dy, 0, region.Height - 1));
                    map.Centre = ui.MapCentre; map.QueueRedraw();
                }); move.Name = "map_pan_" + name; move.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }
            var centreButton = UiDesign.Action(pan, "You", () => { ui.MapCentre = map.Centre = null; map.QueueRedraw(); });
            centreButton.Name = "map_recenter"; centreButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }
        if (ui.MapGuide)
        {
            var guide = JournalLayout.Scroll(regions, "map_guide_scroll"); guide.GetParent<Control>().SizeFlagsStretchRatio = .3f;
            MapScreen.Guide(guide, ui);
        }
    }
    public bool DismissGuide()
    {
        if (!ui.MapGuide) return false;
        ui.MapGuide = false; ui.Map(); return true;
    }
}
