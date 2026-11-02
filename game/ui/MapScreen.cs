using Godot;
namespace Mosslight;

/// <summary>
/// Map screen (PR #7 Part 3). Renders only what RegionState already knows: explored ruin
/// tiles, discovered landmarks and explored mechanisms. It never reveals or regenerates.
/// </summary>
public static class MapScreen
{
    public static readonly Color PlayerColour = Colors.Gold, EntranceColour = Colors.LightGreen, LandmarkColour = Colors.White,
        ObjectiveColour = Colors.Cyan, LockColour = new("d4b66b"), PortcullisColour = new("a7b0b2"), SealColour = new("9fe3e6"),
        LeverColour = new("d4b66b"), BrazierColour = new("f2a443");
    public static readonly (string Letter, string Template, string Name)[] Landmarks =
    {
        ("E", "ruined-entrance", "Ruined entrance"), ("A", "forgotten-archive", "Forgotten archive"), ("S", "abandoned-shrine", "Abandoned shrine"),
        ("T", "collapsed-storeroom", "Collapsed storeroom"), ("R", "relic-sanctuary", "Relic sanctuary")
    };

    public static void Populate(VBoxContainer body, RpgUi ui)
    {
        var app = ui.App;
        var region = app.World.RegionsById[app.World.Player.Region];
        bool ruin = region.Id == "ruin";
        UiDesign.Tabs(body, new[] { "Whole map", "Around you" }, ui.MapZoomed ? 1 : 0, i => { ui.MapZoomed = i == 1; ui.Map(); }, "map_");
        var discovered = region.Landmarks.Where(l => region.DiscoveredLandmarks.Contains(Exploration.Identity(region, l))).ToList();
        if (ruin)
        {
            int walkable = 0;
            for (int i = 0; i < region.Tiles.Length; i++) if (region.Tiles[i] is not (Terrain.Wall or Terrain.Rock)) walkable++;
            int explored = region.ExploredTiles.Count(i => i >= 0 && i < region.Tiles.Length && region.Tiles[i] is not (Terrain.Wall or Terrain.Rock));
            UiDesign.Note(body, $"The ruin · explored about {Math.Min(100, explored * 100 / Math.Max(1, walkable))}% · unexplored areas stay dark");
        }
        else UiDesign.Note(body, "The Reach · the whole settlement is known");
        var map = new RegionMap
        {
            Name = "region_map", Region = region, Player = app.World.Player, Zoomed = ui.MapZoomed,
            CustomMinimumSize = new Vector2(0, ui.MobileLayout ? 300 : 340), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        body.AddChild(map);

        var legend = UiDesign.Card(body, "map_legend");
        UiDesign.Heading(legend, "LEGEND");
        void Key(Color colour, string marker, string text)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); legend.AddChild(row);
            var swatch = new ColorRect { Color = colour, CustomMinimumSize = new Vector2(20, 20), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(swatch);
            var label = UiDesign.Text(marker == "" ? text : $"{marker} · {text}"); row.AddChild(label);
        }
        Key(PlayerColour, "", "You (gold dot)");
        Key(EntranceColour, "", ruin ? "Way back to the Reach (green square)" : "Ruin entrance (green square)");
        if (ruin)
        {
            Key(ObjectiveColour, "", "Objective room (cyan dot)");
            Key(LockColour, "", "Locked gate · needs a ruin key (solid gold bar; faded once open)");
            Key(PortcullisColour, "", "Portcullis · raised by a lever (solid grey bar; faded once open)");
            Key(SealColour, "", "Light seal · broken by a brazier ring (solid cyan bar; faded once open)");
            Key(LeverColour, "L", "Lever (grey once pulled)");
            Key(BrazierColour, "B", "Brazier ring (cyan once solved)");
            var found = UiDesign.Card(body, "map_landmarks");
            UiDesign.Heading(found, "DISCOVERED ROOMS");
            if (discovered.Count == 0) UiDesign.Note(found, "None yet. Rooms appear here once you have seen them.");
            foreach (var landmark in discovered)
            {
                var entry = Landmarks.FirstOrDefault(x => x.Template == landmark.Template);
                found.AddChild(UiDesign.Text(entry.Letter == null ? landmark.Template : $"{entry.Letter} · {entry.Name}{(landmark.Roles.Contains("objective") ? " (objective)" : "")}"));
            }
        }
    }
}

public partial class RegionMap : Control
{
    public RegionState Region { get; set; } = null!;
    public ActorState Player { get; set; } = null!;
    /// <summary>Show a window around the player instead of the whole region.</summary>
    public bool Zoomed { get; set; }
    const int WindowWidth = 28, WindowHeight = 18;
    public Rect2 TileWindow { get; private set; }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(.05f, .08f, .08f));
        int px = (int)(Player.X / RegionState.Tile), py = (int)(Player.Y / RegionState.Tile);
        int w = Zoomed ? Math.Min(WindowWidth, Region.Width) : Region.Width, h = Zoomed ? Math.Min(WindowHeight, Region.Height) : Region.Height;
        int x0 = Zoomed ? Math.Clamp(px - w / 2, 0, Region.Width - w) : 0, y0 = Zoomed ? Math.Clamp(py - h / 2, 0, Region.Height - h) : 0;
        TileWindow = new Rect2(x0, y0, w, h);
        float scale = Math.Min(Size.X / w, Size.Y / h);
        var origin = new Vector2((Size.X - w * scale) / 2, (Size.Y - h * scale) / 2);
        Vector2 At(float tx, float ty) => origin + new Vector2((tx - x0) * scale, (ty - y0) * scale);
        bool Inside(float tx, float ty) => tx >= x0 && ty >= y0 && tx < x0 + w && ty < y0 + h;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (Region.Id == "ruin" && !Region.ExploredTiles.Contains(y * Region.Width + x)) continue;
                var t = Region.Tiles[y * Region.Width + x];
                DrawRect(new Rect2(At(x, y), new Vector2(scale + 1, scale + 1)), t is Terrain.Wall or Terrain.Rock ? new Color(.2f, .24f, .22f) : t == Terrain.Water ? new Color(.2f, .4f, .46f) : new Color(.48f, .55f, .34f));
            }
        var font = ThemeDB.FallbackFont;
        int fontSize = Zoomed ? 18 : 16;
        void Letter(Vector2 at, string text)
        {
            DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, fontSize, 4, Colors.Black);
            DrawString(font, at, text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
        }
        foreach (var landmark in Region.Landmarks)
        {
            if (!Region.DiscoveredLandmarks.Contains(Exploration.Identity(Region, landmark))) continue;
            if (!Inside(landmark.OriginX + 5.5f, landmark.OriginY + 5.5f)) continue;
            var at = At(landmark.OriginX + 5.5f, landmark.OriginY + 5.5f);
            DrawCircle(at, 5, landmark.Roles.Contains("objective") ? MapScreen.ObjectiveColour : MapScreen.LandmarkColour);
            Letter(at + new Vector2(7, -4), MapScreen.Landmarks.FirstOrDefault(l => l.Template == landmark.Template).Letter ?? "?");
        }
        // Explored mechanisms: gates as bars (faded once open), levers and brazier rings by letter.
        foreach (var (cell, kind) in Region.Features)
        {
            if (Region.Id == "ruin" && !Region.ExploredTiles.Contains(cell)) continue;
            int cx = cell % Region.Width, cy = cell / Region.Width;
            if (RegionState.IsGate(kind))
            {
                if (!Inside(cx, cy)) continue;
                var colour = kind switch { "lock" => MapScreen.LockColour, "portcullis" => MapScreen.PortcullisColour, _ => MapScreen.SealColour };
                DrawRect(new Rect2(At(cx, cy), new Vector2(scale + 1, scale + 1)), Region.OpenDoors.Contains(cell) ? new Color(colour, .35f) : colour);
            }
            else if (kind == "lever" || (kind == "brazier" && DungeonMechanisms.RingAt(Region, cell).FirstOrDefault() == cell))
            {
                float ty = kind == "lever" ? cy : cy + 3;
                if (!Inside(cx, ty)) continue;
                var centre = At(cx, ty);
                DrawCircle(centre, 4, kind == "lever" ? (Region.Depleted.Contains(cell) ? MapScreen.PortcullisColour : MapScreen.LeverColour) : (Region.Depleted.Contains(cell) ? MapScreen.SealColour : MapScreen.BrazierColour));
                Letter(centre + new Vector2(6, -3), kind == "lever" ? "L" : "B");
            }
        }
        int ex = Region.Entrance % Region.Width, ey = Region.Entrance / Region.Width;
        if (Inside(ex, ey)) DrawRect(new Rect2(At(ex + .5f, ey + .5f) - new Vector2(5, 5), new Vector2(10, 10)), MapScreen.EntranceColour);
        var you = At(Player.X / RegionState.Tile, Player.Y / RegionState.Tile);
        DrawCircle(you, 7, Colors.Black);
        DrawCircle(you, 5, MapScreen.PlayerColour);
        Letter(you + new Vector2(9, -6), "You");
    }
}
