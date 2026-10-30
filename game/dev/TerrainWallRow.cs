using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;
/// <summary>Raised wall geometry shares actor ground-depth sorting. One mesh per
/// occupied row keeps openings exact without creating a node for every tile.</summary>
public partial class TerrainWallRow : Node2D
{
    public const float Rise = 14;
    public int Row { get; init; }
    public int[] Columns { get; init; } = Array.Empty<int>();
    public static double BuildMilliseconds { get; private set; }
    TerrainBatch? batch;

    public static List<TerrainWallRow> Create(RegionState region)
    {
        BuildMilliseconds = 0;
        var rows = new List<TerrainWallRow>();
        for (int y = 0; y < region.Height; y++)
        {
            var columns = Enumerable.Range(0, region.Width)
                .Where(x => region.Tiles[y * region.Width + x] == Terrain.Wall).ToArray();
            if (columns.Length == 0) continue;
            rows.Add(new TerrainWallRow
            {
                Row = y, Columns = columns,
                // The front ground edge is the depth anchor, not the raised cap.
                // Actors north of it render first; actors south render afterward.
                Position = GodotConvert.GroundToScreen(0, (y + 1) * RegionState.Tile)
            });
        }
        return rows;
    }

    public override void _Draw()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        batch = new TerrainBatch();
        foreach (int x in Columns)
        {
            var p = GodotConvert.GroundToScreen(x * RegionState.Tile, Row * RegionState.Tile) - Position;
            batch.Rect(new Rect2(p + new Vector2(0, -Rise), new Vector2(24, 12)), new Color("4d5d60"));
            batch.Rect(new Rect2(p + new Vector2(0, 12 - Rise), new Vector2(24, Rise)), new Color("354344"));
            batch.Line(p + new Vector2(0, -Rise), p + new Vector2(24, -Rise), new Color("92988a"), 1);
            batch.Line(p + new Vector2(3, -Rise + 4), p + new Vector2(13, -Rise + 4), new Color("7d8477"), 1);
        }
        batch.Draw(this);
        BuildMilliseconds += timer.Elapsed.TotalMilliseconds;
    }
}
