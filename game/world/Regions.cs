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
    // True defaults preserve historical populations/loot even if no surviving actor remains.
    public bool EncountersInitialized { get; set; } = true;
    public bool ExplorationInitialized { get; set; } = true;
    public HashSet<int> ExploredTiles { get; set; } = new();
    public HashSet<string> DiscoveredLandmarks { get; set; } = new(StringComparer.Ordinal);
    public int PlacementRevision { get; set; }
    public int Entrance { get; set; }
    public int Objective { get; set; }
    public List<LandmarkRoom> Landmarks { get; set; } = new();
    public List<DungeonRoom> Rooms { get; set; } = new();
    public List<DungeonPassage> Passages { get; set; } = new();
    public DungeonRoom? RoomAt(int x, int y) => Rooms.FirstOrDefault(r => r.X == x / DungeonGeometry.Cell && r.Y == y / DungeonGeometry.Cell);

    public const int Tile = 24;
    public int Index(Vector2 p) => p.X < 0 || p.Y < 0 || p.X >= Width * Tile || p.Y >= Height * Tile ? -1 : (int)(p.Y / Tile) * Width + (int)(p.X / Tile);
    public Vector2 Center(int index) => new((index % Width + .5f) * Tile, (index / Width + .5f) * Tile);
    // gatesOpen treats every locked gate as unlocked, for placement queries only.
    public bool Passable(int index, ActorState a, bool gatesOpen = false)
    {
        if (index < 0 || index >= Tiles.Length)
            return false;
        if (!gatesOpen && Features.GetValueOrDefault(index) == "lock" && !OpenDoors.Contains(index))
            return false;
        return Tiles[index] switch
        {
            Terrain.Wall => false,
            Terrain.Rock => false,
            Terrain.Water => a.Flies || a.Swims,
            _ => true
        };
    }

    // Gates are isolated cardinally connected lock components. Each locked passage
    // owns one component; passages keep clear of block corners, so they never touch.
    // Derived from serialized feature indices.
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
    public bool CanStand(Vector2 p, ActorState a, bool gatesOpen = false) => BlockingCell(p,a,gatesOpen)==-1;
    // -1 means clear; -2 is a boundary/invalid position; otherwise the blocking footprint tile.
    public int BlockingCell(Vector2 p, ActorState a, bool gatesOpen = false)
    {
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X - a.Radius < 0 || p.Y - a.Radius < 0 || p.X + a.Radius >= Width * Tile || p.Y + a.Radius >= Height * Tile)
            return -2;
        // Circle versus every intersected tile: the four-corner approximation missed
        // interior blockers for large bodies and falsely blocked rounded corners.
        for (int y = (int)((p.Y - a.Radius) / Tile); y <= (int)((p.Y + a.Radius) / Tile); y++)
            for (int x = (int)((p.X - a.Radius) / Tile); x <= (int)((p.X + a.Radius) / Tile); x++)
            {
                if (Passable(y * Width + x, a, gatesOpen)) continue;
                var nearest = new Vector2(Math.Clamp(p.X, x * Tile, (x + 1) * Tile), Math.Clamp(p.Y, y * Tile, (y + 1) * Tile));
                if (Vector2.DistanceSquared(p, nearest) < a.Radius * a.Radius) return y * Width + x;
            }
        return -1;
    }

    public bool CanTravel(Vector2 from, Vector2 to, ActorState a, bool gatesOpen = false)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(from, to) / Math.Min(2, a.Radius / 2)));
        for (int i = 0; i <= steps; i++)
            if (!CanStand(Vector2.Lerp(from, to, (float)i / steps), a, gatesOpen)) return false;
        return true;
    }

    // throughGates searches the whole mission, so inhabitants may start beyond locked
    // gates instead of being pulled onto the entrance side. The chosen point still
    // clears every closed gate, feature and occupant.
    public Vector2 FindPlacement(Vector2 preferred, ActorState a, IEnumerable<ActorState> occupants, bool throughGates = false)
    {
        var others = occupants.Where(b => b.Region == a.Region && b.Id != a.Id && b.Alive).ToArray();
        var reachable = new HashSet<int> { Entrance };
        var pending = new Queue<int>(); pending.Enqueue(Entrance);
        while(pending.Count>0)
        {
            int at=pending.Dequeue();
            foreach(int next in Neighbours(at))
                if(!reachable.Contains(next) && CanTravel(Center(at),Center(next),a,throughGates)) { reachable.Add(next);pending.Enqueue(next); }
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

public static class Regions
{
    public const string Version = "rpg-regions-2";
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
            int columns = cells.GetLength(0), rows = cells.GetLength(1);
            var occupied = new List<(int X, int Y, bool Hall)>();
            var links = new List<DungeonLayout.Link>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    var c = cells[x, y];
                    if (c == null)
                        continue;
                    occupied.Add((x, y, c.type == LG.CellType.Connection));
                    foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        var door = c.getDoor(-dx, -dy);
                        if ((int)door == 0)
                            continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= columns || ny >= rows || cells[nx, ny] == null || (int)cells[nx, ny].getDoor(dx, dy) == 0)
                            throw new InvalidOperationException($"Dungeon seed {seed}: nonreciprocal connection {x},{y} -> {nx},{ny}");
                        // Each connection is carved once, from its west or north cell.
                        if (dx + dy > 0)
                            links.Add(new(x, y, nx, ny, door == LG.DoorType.KeyLock || cells[nx, ny].getDoor(dx, dy) == LG.DoorType.KeyLock));
                    }
                }
            // Room sizes, wings and corridor bends use their own hash, never mission RNG draws.
            var carved = DungeonLayout.Carve(seed, columns, rows, occupied, links);
            var r = new RegionState
            {
                Id = "ruin",
                Seed = seed,
                EncountersInitialized = false,
                ExplorationInitialized = false,
                ContainersInitialized = false,
                PlacementRevision = 1,
                Width = carved.Width,
                Height = carved.Height,
                Tiles = carved.Tiles,
                Rooms = carved.Rooms,
                Passages = carved.Passages
            };
            foreach (var room in r.Rooms)
            {
                var node = cells[room.X, room.Y].node;
                int mid = room.CenterY * r.Width + room.CenterX;
                switch (node?.type)
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
            // A locked passage gates both boundary rows across its full width.
            foreach (var passage in r.Passages.Where(p => p.Locked))
                for (int k = 0; k <= 1; k++)
                    for (int side = -DungeonGeometry.HalfOpening; side <= DungeonGeometry.HalfOpening; side++)
                        r.Features[(passage.Y + passage.Dy * k + passage.Dx * side) * r.Width + passage.X + passage.Dx * k + passage.Dy * side] = "lock";

            if (DungeonValidator.Solvable(r))
            {
                Landmarks.Compose(r);
                return r;
            }
        }

        throw new InvalidOperationException($"Dungeon seed {seed}: exceeded 30 bounded grammar/layout attempts");
    }
}
