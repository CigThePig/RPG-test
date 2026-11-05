using Godot;
namespace Mosslight;

/// <summary>
/// Map screen (PR #7 Part 3). Renders only what RegionState already knows: explored ruin
/// tiles, discovered landmarks and explored mechanisms. It never reveals or regenerates.
/// </summary>
public static class MapScreen
{
    public static readonly Color PlayerColour = new("f6dc9c"), EntranceColour = new("93d08f"), LandmarkColour = new("f3edd9"),
        ObjectiveColour = new("7fe0e6"), LockColour = new("e3b965"), PortcullisColour = new("b3bcbe"), SealColour = new("9fe3e6"),
        LeverColour = new("e3b965"), BrazierColour = new("f2a443");

    public static int ExploredPercent(RegionState region)
    {
        int walkable = 0;
        for (int i = 0; i < region.Tiles.Length; i++) if (region.Tiles[i] is not (Terrain.Wall or Terrain.Rock)) walkable++;
        int explored = region.ExploredTiles.Count(i => i >= 0 && i < region.Tiles.Length && region.Tiles[i] is not (Terrain.Wall or Terrain.Rock));
        return Math.Min(100, explored * 100 / Math.Max(1, walkable));
    }
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

        Guide(body, ui);
    }

    public static void Guide(VBoxContainer body, RpgUi ui)
    {
        var region = ui.App.World.RegionsById[ui.App.World.Player.Region];
        bool ruin = region.Id == "ruin";
        var discovered = region.Landmarks.Where(l => region.DiscoveredLandmarks.Contains(Exploration.Identity(region, l))).ToList();
        var legend = UiDesign.Card(body, "map_legend", "Well");
        UiDesign.Caption(legend, "Legend");
        void Key(string marker, string text)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 10); legend.AddChild(row);
            row.AddChild(new MapMarker { Kind = marker, CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            var label = UiDesign.Text(text); label.ThemeTypeVariation = "RowSubtitle"; row.AddChild(label);
        }
        Key("you", "You");
        Key("entrance", ruin ? "Way back to the Reach" : "Ruin entrance");
        if (ruin)
        {
            Key("objective", "Objective room");
            Key("landmark", "Discovered room");
            Key("lock", "Locked gate · needs a ruin key (faded once open)");
            Key("portcullis", "Portcullis · raised by a lever (faded once open)");
            Key("seal", "Light seal · broken by a brazier ring (faded once open)");
            Key("lever", "Lever (grey once pulled)");
            Key("brazier", "Brazier ring (cyan once solved)");
            var found = UiDesign.Card(body, "map_landmarks", "Well");
            UiDesign.Caption(found, "Discovered rooms");
            if (discovered.Count == 0) UiDesign.Note(found, "None yet. Rooms appear here once you have seen them.");
            foreach (var landmark in discovered)
            {
                var entry = Landmarks.FirstOrDefault(x => x.Template == landmark.Template);
                var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 10); found.AddChild(row);
                bool objective = landmark.Roles.Contains("objective");
                row.AddChild(new MapMarker { Kind = objective ? "objective" : "landmark", Letter = entry.Letter ?? "?", CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                var label = UiDesign.Text(entry.Letter == null ? landmark.Template : $"{entry.Letter} · {entry.Name}{(objective ? " (objective)" : "")}");
                label.ThemeTypeVariation = "RowTitle"; row.AddChild(label);
            }
        }
    }
}

/// <summary>One legend sample, drawn with exactly the marker the map uses.</summary>
public partial class MapMarker : Control
{
    public string Kind { get; set; } = "you";
    public string Letter { get; set; } = "";
    public MapMarker() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw() => RegionMap.Marker(this, Kind, Size / 2, 1, Letter);
}

public partial class RegionMap : Control
{
    public RegionState Region { get; set; } = null!;
    public ActorState Player { get; set; } = null!;
    /// <summary>Show a window around the player instead of the whole region.</summary>
    public bool Zoomed { get; set; }
    public Vector2I? Centre { get; set; }
    const int WindowWidth = 28, WindowHeight = 18;
    public Rect2 TileWindow { get; private set; }

    public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

    static Color TileColour(Terrain t) => t switch
    {
        Terrain.Wall => new Color("232b26"), Terrain.Rock => new Color("2b302b"), Terrain.Water => new Color("2c5866"),
        Terrain.Earth => new Color("6a5c3f"), Terrain.Floor => new Color("5d604a"), _ => new Color("4d6a3c")
    };

    /// <summary>Shared marker painter for the map and its legend.</summary>
    public static void Marker(CanvasItem c, string kind, Vector2 at, float scale, string letter = "")
    {
        var font = UiDesign.BodySemi;
        switch (kind)
        {
            case "you":
                c.DrawCircle(at, 12 * scale, new Color(MapScreen.PlayerColour, .25f), true, -1, true);
                c.DrawCircle(at, 7.5f * scale, Colors.Black, true, -1, true);
                c.DrawCircle(at, 6 * scale, MapScreen.PlayerColour, true, -1, true);
                c.DrawArc(at, 9.5f * scale, 0, MathF.Tau, 32, new Color(1, 1, 1, .85f), 1.5f * scale, true); break;
            case "entrance":
                c.DrawColoredPolygon(new[] { at + new Vector2(0, -7) * scale, at + new Vector2(7, 0) * scale, at + new Vector2(0, 7) * scale, at + new Vector2(-7, 0) * scale }, Colors.Black);
                c.DrawColoredPolygon(new[] { at + new Vector2(0, -5.5f) * scale, at + new Vector2(5.5f, 0) * scale, at + new Vector2(0, 5.5f) * scale, at + new Vector2(-5.5f, 0) * scale }, MapScreen.EntranceColour); break;
            case "landmark": case "objective":
                var colour = kind == "objective" ? MapScreen.ObjectiveColour : MapScreen.LandmarkColour;
                GlyphPainter.Draw(c, "pin", new Rect2(at - new Vector2(10, 14) * scale, new Vector2(20, 20) * scale), Colors.Black);
                GlyphPainter.Draw(c, "pin", new Rect2(at - new Vector2(8.5f, 12.5f) * scale, new Vector2(17, 17) * scale), colour, new Color("16201d"));
                if (letter != "")
                {
                    int size = (int)(11 * scale);
                    c.DrawString(font, at + new Vector2(-8, -3) * scale, letter, HorizontalAlignment.Center, 16 * scale, size, new Color("16201d"));
                }
                break;
            case "lock": case "portcullis": case "seal":
                var bar = kind == "lock" ? MapScreen.LockColour : kind == "portcullis" ? MapScreen.PortcullisColour : MapScreen.SealColour;
                c.DrawRect(new Rect2(at - new Vector2(9, 3.5f) * scale, new Vector2(18, 7) * scale), Colors.Black);
                c.DrawRect(new Rect2(at - new Vector2(8, 2.5f) * scale, new Vector2(16, 5) * scale), bar); break;
            case "lever":
                GlyphPainter.Draw(c, "lever", new Rect2(at - new Vector2(9, 9) * scale, new Vector2(18, 18) * scale), MapScreen.LeverColour); break;
            case "brazier":
                GlyphPainter.Draw(c, "flame", new Rect2(at - new Vector2(9, 9) * scale, new Vector2(18, 18) * scale), MapScreen.BrazierColour, new Color("ffe2a8")); break;
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("070c0b"));
        int px = (int)(Player.X / RegionState.Tile), py = (int)(Player.Y / RegionState.Tile);
        int w = Zoomed ? Math.Min(WindowWidth, Region.Width) : Region.Width, h = Zoomed ? Math.Min(WindowHeight, Region.Height) : Region.Height;
        int x0 = Zoomed ? Math.Clamp((Centre?.X ?? px) - w / 2, 0, Region.Width - w) : 0, y0 = Zoomed ? Math.Clamp((Centre?.Y ?? py) - h / 2, 0, Region.Height - h) : 0;
        TileWindow = new Rect2(x0, y0, w, h);
        float scale = Math.Min(Size.X / w, Size.Y / h);
        var origin = new Vector2((Size.X - w * scale) / 2, (Size.Y - h * scale) / 2);
        Vector2 At(float tx, float ty) => origin + new Vector2((tx - x0) * scale, (ty - y0) * scale);
        bool Inside(float tx, float ty) => tx >= x0 && ty >= y0 && tx < x0 + w && ty < y0 + h;
        // Faint survey grid under the known land.
        var gridColour = new Color(UiDesign.Gold, .05f);
        int step = Zoomed ? 1 : Math.Max(1, (int)Math.Ceiling(12 / Math.Max(1, scale)));
        for (int x = x0; x <= x0 + w; x += step) DrawLine(At(x, y0), At(x, y0 + h), gridColour, 1);
        for (int y = y0; y <= y0 + h; y += step) DrawLine(At(x0, y), At(x0 + w, y), gridColour, 1);
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (Region.Id == "ruin" && !Region.ExploredTiles.Contains(y * Region.Width + x)) continue;
                var t = Region.Tiles[y * Region.Width + x];
                var colour = TileColour(t);
                // Subtle checker keeps large fields from reading as flat colour.
                if (((x + y) & 1) == 0 && t is not (Terrain.Wall or Terrain.Rock)) colour = colour.Lightened(.04f);
                DrawRect(new Rect2(At(x, y), new Vector2(scale + 1, scale + 1)), colour);
            }
        float markerScale = Math.Clamp(scale / 14f, .8f, 1.4f);
        foreach (var landmark in Region.Landmarks)
        {
            if (!Region.DiscoveredLandmarks.Contains(Exploration.Identity(Region, landmark))) continue;
            if (!Inside(landmark.OriginX + landmark.FocalX + .5f, landmark.OriginY + landmark.FocalY + .5f)) continue;
            var at = At(landmark.OriginX + landmark.FocalX + .5f, landmark.OriginY + landmark.FocalY + .5f);
            Marker(this, landmark.Roles.Contains("objective") ? "objective" : "landmark", at, markerScale,
                MapScreen.Landmarks.FirstOrDefault(l => l.Template == landmark.Template).Letter ?? "?");
        }
        // Explored mechanisms: gates as bars (faded once open), levers and brazier rings by icon.
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
                var centre = At(cx + .5f, ty + .5f);
                bool used = Region.Depleted.Contains(cell);
                if (kind == "lever") GlyphPainter.Draw(this, "lever", new Rect2(centre - new Vector2(9, 9) * markerScale, new Vector2(18, 18) * markerScale), used ? MapScreen.PortcullisColour : MapScreen.LeverColour);
                else GlyphPainter.Draw(this, "flame", new Rect2(centre - new Vector2(9, 9) * markerScale, new Vector2(18, 18) * markerScale), used ? MapScreen.SealColour : MapScreen.BrazierColour, new Color("ffe2a8"));
            }
        }
        int ex = Region.Entrance % Region.Width, ey = Region.Entrance / Region.Width;
        if (Inside(ex, ey) && (Region.Id != "ruin" || Region.ExploredTiles.Contains(Region.Entrance))) Marker(this, "entrance", At(ex + .5f, ey + .5f), markerScale);
        var font = UiDesign.BodySemi;
        if (Inside(Player.X / RegionState.Tile, Player.Y / RegionState.Tile))
        {
            var you = At(Player.X / RegionState.Tile, Player.Y / RegionState.Tile);
            Marker(this, "you", you, markerScale);
            var tag = new Rect2(you + new Vector2(13, -22) * markerScale, new Vector2(38, 20));
            var plate = new StyleBoxFlat { BgColor = new Color("070c0b", .9f), BorderColor = new Color(MapScreen.PlayerColour, .7f), AntiAliasing = true };
            plate.SetBorderWidthAll(1); plate.SetCornerRadiusAll(10);
            plate.Draw(GetCanvasItem(), tag);
            DrawString(font, tag.Position + new Vector2(0, 15), "You", HorizontalAlignment.Center, tag.Size.X, 13, MapScreen.PlayerColour);
        }
        // Compass rose in the upper-right corner.
        float r = Math.Min(26, Math.Min(Size.X, Size.Y) * .08f);
        if (r > 10)
        {
            var at = new Vector2(Size.X - r - 14, r + 18);
            DrawCircle(at, r + 4, new Color("070c0b", .75f), true, -1, true);
            GlyphPainter.Draw(this, "compass", new Rect2(at - new Vector2(r, r), new Vector2(r * 2, r * 2)), UiDesign.Gold, new Color(UiDesign.Gold, .35f));
            DrawString(UiDesign.Display, at + new Vector2(-r, -r - 4), "N", HorizontalAlignment.Center, r * 2, 12, UiDesign.GoldBright);
        }
    }
}
