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
    public bool ContainersInitialized { get; set; } = true;
    public int PlacementRevision { get; set; }
    public int Entrance { get; set; }
    public int Objective { get; set; }

    public const int Tile = 24;
    public int Index(Vector2 p) => p.X < 0 || p.Y < 0 || p.X >= Width * Tile || p.Y >= Height * Tile ? -1 : (int)(p.Y / Tile) * Width + (int)(p.X / Tile);
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

    // Gates are isolated cardinally connected lock components. Each shared room
    // boundary owns one component; separate boundaries cannot touch (solid corners).
    // Derived from serialized feature indices, so legacy two-cell gates need no migration.
    public List<int[]> GateGroups()
    {
        var remaining = Features.Where(x => x.Value == "lock").Select(x => x.Key).ToHashSet();
        var groups = new List<int[]>();
        foreach (int start in remaining.Order().ToArray())
        {
            if (!remaining.Remove(start)) continue;
            var cells = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue(); cells.Add(at);
                foreach (int next in Neighbours(at))
                    if (remaining.Remove(next)) queue.Enqueue(next);
            }
            groups.Add(cells.Order().ToArray());
        }
        return groups;
    }

    public int[] GateAt(int at) => GateGroups().FirstOrDefault(g => Array.BinarySearch(g, at) >= 0) ?? Array.Empty<int>();

    public bool BlocksSight(int index) => index < 0 || index >= Tiles.Length || Tiles[index] is Terrain.Wall or Terrain.Rock || (Features.GetValueOrDefault(index) == "lock" && !OpenDoors.Contains(index));
    public bool CanStand(Vector2 p, ActorState a) => BlockingCell(p,a)==-1;
    // -1 means clear; -2 is a boundary/invalid position; otherwise the blocking footprint tile.
    public int BlockingCell(Vector2 p, ActorState a)
    {
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X - a.Radius < 0 || p.Y - a.Radius < 0 || p.X + a.Radius >= Width * Tile || p.Y + a.Radius >= Height * Tile)
            return -2;
        // Circle versus every intersected tile: the four-corner approximation missed
        // interior blockers for large bodies and falsely blocked rounded corners.
        for (int y = (int)((p.Y - a.Radius) / Tile); y <= (int)((p.Y + a.Radius) / Tile); y++)
            for (int x = (int)((p.X - a.Radius) / Tile); x <= (int)((p.X + a.Radius) / Tile); x++)
            {
                if (Passable(y * Width + x, a)) continue;
                var nearest = new Vector2(Math.Clamp(p.X, x * Tile, (x + 1) * Tile), Math.Clamp(p.Y, y * Tile, (y + 1) * Tile));
                if (Vector2.DistanceSquared(p, nearest) < a.Radius * a.Radius) return y * Width + x;
            }
        return -1;
    }

    public bool CanTravel(Vector2 from, Vector2 to, ActorState a)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(from, to) / Math.Min(2, a.Radius / 2)));
        for (int i = 0; i <= steps; i++)
            if (!CanStand(Vector2.Lerp(from, to, (float)i / steps), a)) return false;
        return true;
    }

    public Vector2 FindPlacement(Vector2 preferred, ActorState a, IEnumerable<ActorState> occupants)
    {
        var others = occupants.Where(b => b.Region == a.Region && b.Id != a.Id && b.Alive).ToArray();
        var reachable = new HashSet<int> { Entrance };
        var pending = new Queue<int>(); pending.Enqueue(Entrance);
        while(pending.Count>0)
        {
            int at=pending.Dequeue();
            foreach(int next in Neighbours(at))
                if(!reachable.Contains(next) && CanTravel(Center(at),Center(next),a)) { reachable.Add(next);pending.Enqueue(next); }
        }
        foreach (int at in reachable.OrderBy(i => Vector2.DistanceSquared(Center(i), preferred)).ThenBy(i => i))
        {
            var point = Center(at);
            if (!CanStand(point, a) || Features.Keys.Any(i => Vector2.DistanceSquared(Center(i), point) < 40 * 40)) continue;
            if (others.Any(b => Vector2.DistanceSquared(b.Position, point) < MathF.Pow(a.Radius + b.Radius + 16, 2))) continue;
            return point;
        }
        throw new InvalidOperationException("No clear actor placement in " + Id);
    }

    public List<int> Path(Vector2 start, Vector2 end, ActorState a)
    {
        int s = Index(start), t = Index(end);
        if (s < 0 || t < 0 || !CanStand(start, a) || !CanStand(end, a)) return new();
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
                if (!prev.ContainsKey(k) && CanTravel(n == s ? start : Center(n), Center(k), a))
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

public static class DungeonGeometry
{
    public const int Cell = 11;
    public const int Boundary = 1;
    public const int Interior = Cell - Boundary * 2;
    public const int Center = Cell / 2;
    public const int Opening = 3;
    public const int HalfOpening = Opening / 2;
    public const int Reach = Center;
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
            ContainersInitialized = false,
            PlacementRevision = 1,
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

        // A compact settlement and a winding, three-cell road preserve landscape
        // barriers without flattening a nine-cell strip across the entire region.
        for (int y = 1; y < height-1; y++)
            for (int x = 1; x < width-1; x++)
            {
                float sx=(x-10)/7f, sy=(y-height/2)/4f;
                int roadY = height/2 + (int)MathF.Round(MathF.Sin((x-8)*.14f)*2);
                if(sx*sx+sy*sy < 1 || (x>=8 && x<=width-8 && Math.Abs(y-roadY)<=1))
                    r.Tiles[y*width+x]=Terrain.Earth;
            }
        r.Entrance = (height / 2) * width + 8;
        r.Objective = (height / 2) * width + width - 8;
        r.Features[r.Objective] = "portal";
        r.Features[r.Entrance + 4] = "chest";
        r.Features[r.Entrance + width * 2] = "herb";
        r.Features[r.Entrance + width * 2 + 3] = "ore";
        r.Features[r.Entrance + width * 2 + 6] = "wood";
        // Keep reachable interaction stands around each authored feature; connect
        // them to the road rather than erasing unrelated natural barriers.
        foreach(int at in r.Features.Keys.Append(r.Entrance))
        {
            int fx=at%width, fy=at/width;
            int roadY=height/2+(int)MathF.Round(MathF.Sin((fx-8)*.14f)*2);
            for(int y=Math.Min(fy-1,roadY-1);y<=Math.Max(fy+1,roadY+1);y++)
                for(int x=fx-1;x<=fx+1;x++)
                    if(x>0&&x<width-1&&y>0&&y<height-1) r.Tiles[y*width+x]=Terrain.Earth;
        }
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
            int room = DungeonGeometry.Cell, w = cells.GetLength(0) * room, h = cells.GetLength(1) * room;
            var r = new RegionState
            {
                Id = "ruin",
                Seed = seed,
            ContainersInitialized = false,
            PlacementRevision = 1,
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
                    for (int dx = DungeonGeometry.Boundary; dx < room - DungeonGeometry.Boundary; dx++)
                        for (int dy = DungeonGeometry.Boundary; dy < room - DungeonGeometry.Boundary; dy++)
                            r.Tiles[(y * room + dy) * w + x * room + dx] = Terrain.Floor;
                    int mid = (y * room + DungeonGeometry.Center) * w + x * room + DungeonGeometry.Center;
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
                        // Both boundary rows stay solid unless a reciprocal layout
                        // connection exists. No random draws are consumed by geometry.
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= cells.GetLength(0) || ny >= cells.GetLength(1) ||
                            cells[nx, ny] == null || (int)cells[nx, ny].getDoor(dx, dy) == 0)
                            throw new InvalidOperationException($"Dungeon seed {seed}: nonreciprocal connection {x},{y} -> {nx},{ny}");
                        bool locked = door == LG.DoorType.KeyLock || cells[nx, ny].getDoor(dx, dy) == LG.DoorType.KeyLock;
                        for (int k = 1; k <= DungeonGeometry.Reach; k++)
                            for (int side = -DungeonGeometry.HalfOpening; side <= DungeonGeometry.HalfOpening; side++)
                            {
                                int at = mid + dx * k + dy * k * w + side * (-dy + dx * w);
                                r.Tiles[at] = Terrain.Floor;
                                if (locked && k == DungeonGeometry.Reach) r.Features[at] = "lock";
                            }

                    }
                }

            if (DungeonValidator.Solvable(r))
                return r;
        }

        throw new InvalidOperationException($"Dungeon seed {seed}: exceeded 30 bounded grammar/layout attempts");
    }
}
