using Vector2 = System.Numerics.Vector2;
using Godot;
using MG = ObstacleTowerGeneration.MissionGraph;
using LG = ObstacleTowerGeneration.LayoutGrammar;

namespace Mosslight;
public enum Terrain
{
    Grass,
    Earth,
    Water,
    Rock,
    Floor,
    Wall
}

public sealed class RegionState
{
    public string Id { get; set; } = "reach";
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Terrain[] Tiles { get; set; } = Array.Empty<Terrain>();
    public Dictionary<int, string> Features { get; set; } = new();
    public HashSet<int> OpenDoors { get; set; } = new();
    public HashSet<int> Depleted { get; set; } = new();
    public int Entrance { get; set; }
    public int Objective { get; set; }

    public const int Tile = 24;
    public int Index(Vector2 p) => (int)(p.Y / Tile) * Width + (int)(p.X / Tile);
    public Vector2 Center(int index) => new((index % Width + .5f) * Tile, (index / Width + .5f) * Tile);
    public bool Passable(int index, ActorState a)
    {
        if (index < 0 || index >= Tiles.Length)
            return false;
        if (Features.GetValueOrDefault(index) == "lock" && !OpenDoors.Contains(index))
            return false;
        return Tiles[index] switch
        {
            Terrain.Wall => false,
            Terrain.Rock => false,
            Terrain.Water => a.Flies || a.Swims,
            _ => true
        };
    }

    public bool BlocksSight(int index) => index < 0 || index >= Tiles.Length || Tiles[index] is Terrain.Wall or Terrain.Rock || (Features.GetValueOrDefault(index) == "lock" && !OpenDoors.Contains(index));
    public bool CanStand(Vector2 p, ActorState a)
    {
        foreach (var offset in new[]
        {
            new Vector2(-a.Radius, -a.Radius),
            new Vector2(a.Radius, -a.Radius),
            new Vector2(-a.Radius, a.Radius),
            new Vector2(a.Radius, a.Radius)
        }

        )
        {
            var v = p + offset;
            if (v.X < 0 || v.Y < 0 || v.X >= Width * Tile || v.Y >= Height * Tile || !Passable(Index(v), a))
                return false;
        }

        return true;
    }

    public List<int> Path(Vector2 start, Vector2 end, ActorState a)
    {
        int s = Index(start), t = Index(end);
        var prev = new Dictionary<int, int>
        {
            {
                s,
                -1
            }
        };
        var q = new Queue<int>();
        q.Enqueue(s);
        while (q.Count > 0)
        {
            int n = q.Dequeue();
            if (n == t)
                break;
            foreach (var k in Neighbours(n))
                if (!prev.ContainsKey(k) && CanStand(Center(k), a))
                {
                    prev[k] = n;
                    q.Enqueue(k);
                }
        }

        if (!prev.ContainsKey(t))
            return new();
        var result = new List<int>();
        for (int n = t; n != s; n = prev[n])
            result.Add(n);
        result.Reverse();
        return result;
    }

    public IEnumerable<int> Neighbours(int n)
    {
        int x = n % Width, y = n / Width;
        if (x > 0)
            yield return n - 1;
        if (x < Width - 1)
            yield return n + 1;
        if (y > 0)
            yield return n - Width;
        if (y < Height - 1)
            yield return n + Width;
    }
}

public static class Regions
{
    public const string Version = "rpg-regions-1";
    public static RegionState Overworld(int seed, int width = 64, int height = 48)
    {
        if (width < 16 || height < 16 || width > 256 || height > 256)
            throw new ArgumentOutOfRangeException(nameof(width));
        var r = new RegionState
        {
            Seed = seed,
            Width = width,
            Height = height,
            Tiles = new Terrain[width * height]
        };
        var noise = new FastNoiseLite
        {
            Seed = seed,
            Frequency = .055f,
            FractalOctaves = 3
        };
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int n = y * width + x;
                float h = noise.GetNoise2D(x, y);
                r.Tiles[n] = h < -.3 ? Terrain.Water : h > .48 ? Terrain.Rock : h > .15 ? Terrain.Earth : Terrain.Grass;
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    r.Tiles[n] = Terrain.Rock;
            }

        // Settlement clearing and a connected causeway form authored constraints within generated geography.
        for (int y = height / 2 - 4; y <= height / 2 + 4; y++)
            for (int x = 3; x < width - 3; x++)
                r.Tiles[y * width + x] = Terrain.Earth;
        r.Entrance = (height / 2) * width + 8;
        r.Objective = (height / 2) * width + width - 8;
        r.Features[r.Objective] = "portal";
        r.Features[r.Entrance + 4] = "chest";
        r.Features[r.Entrance + width * 2] = "herb";
        r.Features[r.Entrance + width * 2 + 3] = "ore";
        r.Features[r.Entrance + width * 2 + 6] = "wood";
        return r;
    }

    public static RegionState Dungeon(int seed)
    {
        string root = DungeonRules.Prepare();
        for (int trial = 0; trial < 30; trial++)
        {
            var random = new Random(unchecked(seed + trial * 7919));
            var generator = new MG.Generator(random);
            generator.loadPatterns(root + "grammar/");
            var graph = generator.GenerateDungeon(root + "graphStart.txt", root + "graphRecipe.txt");
            if (graph == null || !ObstacleTowerGeneration.Helper.checkIsSolvable(graph, graph.nodes[0]))
                continue;
            var layout = new LG.Generator(random).generateDungeon(graph);
            if (layout == null || !ObstacleTowerGeneration.Helper.checkIsSolvable(layout.get2DMap(), layout.getCell(0)))
                continue;
            var cells = layout.get2DMap();
            int room = 7, w = cells.GetLength(0) * room, h = cells.GetLength(1) * room;
            var r = new RegionState
            {
                Id = "ruin",
                Seed = seed,
                Width = w,
                Height = h,
                Tiles = Enumerable.Repeat(Terrain.Wall, w * h).ToArray()
            };
            for (int x = 0; x < cells.GetLength(0); x++)
                for (int y = 0; y < cells.GetLength(1); y++)
                {
                    var c = cells[x, y];
                    if (c == null)
                        continue;
                    for (int dx = 1; dx < room - 1; dx++)
                        for (int dy = 1; dy < room - 1; dy++)
                            r.Tiles[(y * room + dy) * w + x * room + dx] = Terrain.Floor;
                    int mid = (y * room + 3) * w + x * room + 3;
                    if (c.node != null)
                    {
                        switch (c.node.type)
                        {
                            case MG.NodeType.Start:
                                r.Entrance = mid;
                                r.Features[mid] = "exit";
                                break;
                            case MG.NodeType.End:
                                r.Objective = mid;
                                r.Features[mid] = "relic";
                                break;
                            case MG.NodeType.Key:
                                r.Features[mid] = "key";
                                break;
                            case MG.NodeType.Puzzle:
                                r.Features[mid] = "chest";
                                break;
                        }
                    }

                    foreach (var(dx, dy)in new[]
                    {
                        (-1, 0),
                        (1, 0),
                        (0, -1),
                        (0, 1)
                    }

                    )
                    {
                        var door = c.getDoor(-dx, -dy);
                        if ((int)door == 0)
                            continue;
                        // Half corridor to room boundary. Matching adjacent cell carves the other half.
                        for (int k = 1; k <= 3; k++)
                        {
                            int at = mid + dx * k + dy * k * w;
                            r.Tiles[at] = Terrain.Floor;
                        }

                        if (door == LG.DoorType.KeyLock)
                        {
                            int at = mid + dx * 3 + dy * 3 * w;
                            r.Features[at] = "lock";
                        }
                    }
                }

            if (DungeonValidator.Solvable(r))
                return r;
        }

        throw new InvalidOperationException($"Dungeon seed {seed}: exceeded 30 bounded grammar/layout attempts");
    }
}
