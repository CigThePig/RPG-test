using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;
/// <summary>Raised wall geometry shares actor ground-depth sorting. One mesh per
/// occupied row keeps openings exact without creating a node for every tile.</summary>
public partial class TerrainWallRow : Node2D
{
    public const float Rise = 16;
    public int Seed { get; init; }
    public Color[] Tints { get; init; } = Array.Empty<Color>();
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
                Row = y, Columns = columns, Seed = region.Seed,
                Tints=Enumerable.Range(0,region.Width).Select(x=>EnvironmentThemes.Tint(EnvironmentThemes.At(region,x,y))).ToArray(),
                // The front ground edge is the depth anchor, not the raised cap.
                // Actors north of it render first; actors south render afterward.
                Position = GodotConvert.GroundToScreen(0, (y + 1) * RegionState.Tile)
            });
        }
        return rows;
    }

    public override void _ExitTree() => batch?.Dispose();
    public override void _Draw()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        batch?.Dispose();
        batch = new TerrainBatch { Texture=EnvironmentAssetCache.Walls };
        TextureFilter=TextureFilterEnum.Nearest;
        foreach (int x in Columns)
        {
            var p = GodotConvert.GroundToScreen(x * RegionState.Tile, Row * RegionState.Tile) - Position;
            int ends=(Array.BinarySearch(Columns,x-1)<0?1:0)|(Array.BinarySearch(Columns,x+1)<0?2:0);
            int variant=(int)(EnvironmentStyle.Hash(Seed,x,Row)&15);
            int module=ends*16+variant;
            batch.Sprite(new Rect2(p+new Vector2(0,-Rise),new Vector2(24,12+Rise)),
                new Rect2(module*26+1,1,24,12+Rise),Tints.Length>x?Tints[x]:Colors.White);
        }
        batch.Draw(this);
        BuildMilliseconds += timer.Elapsed.TotalMilliseconds;
    }
}
