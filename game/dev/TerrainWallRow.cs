using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;
/// <summary>Raised wall geometry shares actor ground-depth sorting. One mesh per
/// occupied row keeps openings exact without creating a node for every tile.</summary>
public partial class TerrainWallRow : Node2D
{
    public const float Rise = 16;
    public int Seed { get; init; }
    public int Row { get; init; }
    public int[] Columns { get; init; } = Array.Empty<int>();
    /// <summary>The region's shared lookups and wall material; rows without one draw nothing.</summary>
    public DungeonSurface? Surface { get; set; }
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
                // The front ground edge is the depth anchor, not the raised cap.
                // Actors north of it render first; actors south render afterward.
                Position = GodotConvert.GroundToScreen(0, (y + 1) * RegionState.Tile)
            });
        }
        return rows;
    }

    /// <summary>Hang this row's torches and banners once its surface is known.</summary>
    public void Dress(DungeonDressing.Fixture[] fixtures)
    {
        if(fixtures.Length>0)AddChild(new WallFixtures{Fixtures=fixtures,Seed=Seed});
    }
    public override void _ExitTree() => batch?.Dispose();
    public override void _Draw()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        batch?.Dispose();
        batch = new TerrainBatch();
        if (Surface == null) return;
        TextureFilter=TextureFilterEnum.Nearest;Material=Surface.Wall;
        var r=Surface.Region;
        bool Solid(int x,int y)=>DungeonArt.Solid(Surface.Masonry,r,x,y);
        foreach (int x in Columns)
        {
            // A landmark's structural prop stands on open floor, not on a block of wall.
            if(!Solid(x,Row))continue;
            var p = GodotConvert.GroundToScreen(x * RegionState.Tile, Row * RegionState.Tile) - Position;
            batch.Rect(new Rect2(p+new Vector2(0,-Rise),new Vector2(24,12)),new Color(0,0,0));
            if(Solid(x,Row+1))continue;
            // The face shows only where floor lies to the south. Its ends tell the shader
            // whether the wall stops there (0.5), turns south (1) or simply continues (0).
            float End(int nx)=>!Solid(nx,Row)?.5f:Solid(nx,Row+1)?1:0;
            batch.Rect(new Rect2(p+new Vector2(0,12-Rise),new Vector2(24,Rise)),new Color(1,End(x-1),End(x+1)));
        }
        batch.Draw(this);
        BuildMilliseconds += timer.Elapsed.TotalMilliseconds;
    }
}
