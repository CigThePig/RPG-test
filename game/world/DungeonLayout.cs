using System.Text.Json.Serialization;
namespace Mosslight;

public static class DungeonGeometry
{
    // Each mission-graph layout cell owns a Cell x Cell block of tiles. Its outer ring
    // stays solid except where a passage crosses it, so blocks only meet at passages.
    public const int Cell = 17;
    public const int Boundary = 1;
    // Smallest room floor. Landmark templates are authored for this core.
    public const int Core = 9;
    // Template frame: the core plus one tile on each side (local 0..10).
    public const int Frame = Core + 2;
    // Corridors and gates are three tiles wide.
    public const int Opening = 3;
    public const int HalfOpening = Opening / 2;
    // Passages keep this distance from block corners, so neighbouring gates never touch.
    public const int CrossingMargin = 4;
}

/// <summary>A carved layout cell. Rooms are a 9x9 core plus optional wings; halls are a
/// 3x3 junction reached only by corridors. Rects are absolute tiles [x, y, w, h].</summary>
public sealed class DungeonRoom
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Kind { get; set; } = "room";
    public int CoreX { get; set; }
    public int CoreY { get; set; }
    public List<int[]> Rects { get; set; } = new();
    // N=1 E=2 S=4 W=8
    public int Doors { get; set; }
    [JsonIgnore] public int CoreSize => Kind == "hall" ? DungeonGeometry.Opening : DungeonGeometry.Core;
    [JsonIgnore] public int CenterX => CoreX + CoreSize / 2;
    [JsonIgnore] public int CenterY => CoreY + CoreSize / 2;
    public bool Contains(int x, int y) => Rects.Any(r => x >= r[0] && y >= r[1] && x < r[0] + r[2] && y < r[1] + r[3]);
    public IEnumerable<(int X, int Y)> Tiles() => Rects.SelectMany(r => Enumerable.Range(r[1], r[3]).SelectMany(y => Enumerable.Range(r[0], r[2]).Select(x => (x, y)))).Distinct();
}

/// <summary>A corridor crossing between two blocks. (X, Y) is the middle lane tile on the
/// first block's boundary; (Dx, Dy) points east or south into the second block.</summary>
public sealed class DungeonPassage
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Dx { get; set; }
    public int Dy { get; set; }
    public bool Locked { get; set; }
    // Mission grammar seal on an unlocked crossing: "" open, "lever" one-way shortcut
    // (portcullis raised by a lever in an adjacent block), "puzzle" light seal (broken by
    // solving the adjacent block's brazier ring). Older saves deserialize as "".
    public string Mechanism { get; set; } = "";
    /// <summary>The two blocks this crossing joins.</summary>
    public bool Touches(int blockX, int blockY)
    {
        int ax = X / DungeonGeometry.Cell, ay = Y / DungeonGeometry.Cell;
        return (ax == blockX && ay == blockY) || (ax + Dx == blockX && ay + Dy == blockY);
    }
    /// <summary>Both boundary rows across the full corridor width (a 2 x 3 group).</summary>
    public IEnumerable<int> GateCells(int width)
    {
        for (int k = 0; k <= 1; k++)
            for (int side = -DungeonGeometry.HalfOpening; side <= DungeonGeometry.HalfOpening; side++)
                yield return (Y + Dy * k + Dx * side) * width + X + Dx * k + Dy * side;
    }
}

public static class DungeonLayout
{
    public const int Revision = 1;
    public readonly record struct Link(int AX, int AY, int BX, int BY, bool Locked, string Mechanism = "");
    public sealed class Result
    {
        public int Width, Height;
        public Terrain[] Tiles = Array.Empty<Terrain>();
        public List<DungeonRoom> Rooms = new();
        public List<DungeonPassage> Passages = new();
    }

    // Geometry owns its own integer hash and consumes no mission RNG draws.
    public static uint Hash(int seed, int x, int y, int slot)
    {
        unchecked
        {
            uint n = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^ (uint)slot * 0xc2b2ae35u ^ (uint)Revision * 0x27d4eb2fu ^ 0x6c61796fu;
            n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; return n ^ (n >> 16);
        }
    }

    // Shapes simplify when a corridor cannot be routed: 0 varied, 1 plain core, 2 centered.
    // Centered shapes always admit straight corridors, so carving always succeeds.
    public static Result Carve(int seed, int columns, int rows, IReadOnlyList<(int X, int Y, bool Hall)> cells, IReadOnlyList<Link> links)
    {
        int size = DungeonGeometry.Cell, width = columns * size, height = rows * size;
        var level = cells.ToDictionary(c => (c.X, c.Y), _ => 0);
        for (int attempt = 0; attempt <= 3; attempt++)
        {
            var rooms = cells.ToDictionary(c => (c.X, c.Y), c => Shape(seed, c.X, c.Y, c.Hall, level[(c.X, c.Y)]));
            var tiles = Enumerable.Repeat(Terrain.Wall, width * height).ToArray();
            foreach (var room in rooms.Values)
                foreach (var (x, y) in room.Tiles()) tiles[y * width + x] = Terrain.Floor;
            var passages = new List<DungeonPassage>();
            var failed = new HashSet<(int, int)>();
            foreach (var link in links)
            {
                var a = rooms[(link.AX, link.AY)]; var b = rooms[(link.BX, link.BY)];
                bool horizontal = link.BX == link.AX + 1;
                a.Doors |= horizontal ? 2 : 4; b.Doors |= horizontal ? 8 : 1;
                var passage = Route(seed, tiles, width, a, b, horizontal, link.Locked);
                if (passage != null) passage.Mechanism = link.Locked ? "" : link.Mechanism;
                if (passage == null) { failed.Add((link.AX, link.AY)); failed.Add((link.BX, link.BY)); }
                else passages.Add(passage);
            }
            if (failed.Count == 0)
                return new Result { Width = width, Height = height, Tiles = tiles, Rooms = rooms.Values.OrderBy(r => r.Y).ThenBy(r => r.X).ToList(), Passages = passages };
            foreach (var cell in failed) level[cell] = Math.Min(2, level[cell] + 1);
        }
        throw new InvalidOperationException($"Dungeon seed {seed}: corridor routing did not converge");
    }

    static DungeonRoom Shape(int seed, int cx, int cy, bool hall, int level)
    {
        int size = DungeonGeometry.Cell, bx = cx * size, by = cy * size;
        int lo = DungeonGeometry.Boundary, hi = size - 1 - DungeonGeometry.Boundary;
        uint H(int slot) => Hash(seed, cx, cy, slot);
        int Pick(int min, int max, int slot) => min + (int)(H(slot) % (uint)(max - min + 1));
        if (hall)
        {
            // The hub's lane must be a legal crossing lane so a straight corridor exists.
            int min = DungeonGeometry.CrossingMargin, max = size - 1 - DungeonGeometry.CrossingMargin;
            int hx = level == 0 ? Pick(min, max, 1) : size / 2, hy = level == 0 ? Pick(min, max, 2) : size / 2;
            return new DungeonRoom { X = cx, Y = cy, Kind = "hall", CoreX = bx + hx - 1, CoreY = by + hy - 1, Rects = { new[] { bx + hx - 1, by + hy - 1, 3, 3 } } };
        }
        int core = DungeonGeometry.Core, slack = hi - lo + 1 - core;
        int ox = level == 2 ? lo + slack / 2 : Pick(lo, lo + slack, 3), oy = level == 2 ? lo + slack / 2 : Pick(lo, lo + slack, 4);
        var room = new DungeonRoom { X = cx, Y = cy, CoreX = bx + ox, CoreY = by + oy, Rects = { new[] { bx + ox, by + oy, core, core } } };
        if (level > 0) return room;
        int wings = (H(5) % 100) switch { < 20 => 0, < 55 => 1, < 85 => 2, _ => 3 };
        for (int i = 0; i < wings; i++)
        {
            int side = (int)(H(10 + i * 7) % 4); // 0 N, 1 E, 2 S, 3 W
            int room0 = side is 0 or 2 ? oy : ox, along0 = side is 0 or 2 ? ox : oy;
            int depthAvailable = side is 0 or 3 ? room0 - lo : hi - (room0 + core - 1);
            if (depthAvailable < 2) continue;
            int depth = Pick(2, depthAvailable, 11 + i * 7);
            // A wing overlaps the core side by at least three tiles; any overhang is
            // at least two tiles, so wings never leave one-tile slivers.
            var starts = new List<(int Start, int Length)>();
            for (int length = 3; length <= hi - lo + 1; length++)
                for (int start = lo; start + length - 1 <= hi; start++)
                {
                    int end = start + length - 1, overlap = Math.Min(end, along0 + core - 1) - Math.Max(start, along0) + 1;
                    int before = along0 - start, after = end - (along0 + core - 1);
                    if (overlap >= 3 && before is <= 0 or >= 2 && after is <= 0 or >= 2) starts.Add((start, length));
                }
            var (s, l) = starts[(int)(H(12 + i * 7) % (uint)starts.Count)];
            room.Rects.Add(side switch
            {
                0 => new[] { bx + s, by + oy - depth, l, depth },
                1 => new[] { bx + ox + core, by + s, depth, l },
                2 => new[] { bx + s, by + oy + core, l, depth },
                _ => new[] { bx + ox - depth, by + s, depth, l }
            });
        }
        return room;
    }

    /// <summary>Authoring preview: # wall, . room, , corridor, = key gate, % portcullis, ~ light seal, features by initial.</summary>
    public static string Ascii(RegionState r)
    {
        var text = new System.Text.StringBuilder($"seed={r.Seed} {r.Width}x{r.Height} rooms={r.Rooms.Count(x => x.Kind == "room")} halls={r.Rooms.Count(x => x.Kind == "hall")} passages={r.Passages.Count} levers={r.Features.Count(f => f.Value == "lever")} seals={r.GateGroups("seal").Count} shortcuts={r.GateGroups("portcullis").Count}\n");
        for (int y = 0; y < r.Height; y++)
        {
            for (int x = 0; x < r.Width; x++)
            {
                int at = y * r.Width + x;
                string feature = r.Features.GetValueOrDefault(at, "");
                text.Append(feature == "lock" ? '=' : feature == "portcullis" ? '%' : feature == "seal" ? '~' : feature != "" ? char.ToUpperInvariant(feature[0]) : r.Tiles[at] == Terrain.Wall ? '#' : r.Rooms.Any(c => c.Contains(x, y)) ? '.' : ',');
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    // Lane coordinates: u runs from block A to block B, v across the corridor.
    static int Far(DungeonRoom r, bool horizontal, int v)
    {
        int best = int.MinValue;
        foreach (var q in r.Rects)
        {
            int v0 = horizontal ? q[1] : q[0], vs = horizontal ? q[3] : q[2], u1 = horizontal ? q[0] + q[2] - 1 : q[1] + q[3] - 1;
            if (v >= v0 && v < v0 + vs) best = Math.Max(best, u1);
        }
        return best;
    }
    static int Near(DungeonRoom r, bool horizontal, int v)
    {
        int best = int.MaxValue;
        foreach (var q in r.Rects)
        {
            int v0 = horizontal ? q[1] : q[0], vs = horizontal ? q[3] : q[2], u0 = horizontal ? q[0] : q[1];
            if (v >= v0 && v < v0 + vs) best = Math.Min(best, u0);
        }
        return best;
    }

    sealed record Leg(int Lane, int Turn);

    static DungeonPassage? Route(int seed, Terrain[] tiles, int width, DungeonRoom a, DungeonRoom b, bool horizontal, bool locked)
    {
        int size = DungeonGeometry.Cell, half = DungeonGeometry.HalfOpening;
        int ringA = (horizontal ? b.X : b.Y) * size - 1, ringB = ringA + 1;
        int v0 = (horizontal ? a.Y : a.X) * size;
        int laneMin = v0 + DungeonGeometry.CrossingMargin, laneMax = v0 + size - 1 - DungeonGeometry.CrossingMargin;
        bool FlatA(int v) => Far(a, horizontal, v) != int.MinValue && Far(a, horizontal, v - 1) == Far(a, horizontal, v) && Far(a, horizontal, v + 1) == Far(a, horizontal, v);
        bool FlatB(int v) => Near(b, horizontal, v) != int.MaxValue && Near(b, horizontal, v - 1) == Near(b, horizontal, v) && Near(b, horizontal, v + 1) == Near(b, horizontal, v);
        int slotBase = (horizontal ? 0 : 1) + a.X * 2 + a.Y * 64;
        uint H(int slot) => Hash(seed, a.X, a.Y, 1000 + slotBase * 16 + slot);
        // A side leg either continues straight from a door on this lane or turns once,
        // keeping one wall between the turn and the room and two tiles before the ring.
        List<Leg> Legs(bool sideA, int lane)
        {
            var legs = new List<Leg>();
            var room = sideA ? a : b;
            if (sideA ? FlatA(lane) : FlatB(lane)) legs.Add(new Leg(lane, int.MinValue));
            foreach (int door in Enumerable.Range(v0 + 1, size - 2).Where(v => Math.Abs(v - lane) >= 2 && (sideA ? FlatA(v) : FlatB(v))))
            {
                int from = Math.Min(door, lane) - half, to = Math.Max(door, lane) + half;
                if (sideA)
                {
                    int edge = Enumerable.Range(from, to - from + 1).Max(v => Far(room, horizontal, v));
                    for (int t = Math.Max(edge, Far(room, horizontal, door)) + 3; t + 1 <= ringA - 3; t++) legs.Add(new Leg(door, t));
                }
                else
                {
                    int edge = Enumerable.Range(from, to - from + 1).Min(v => Near(room, horizontal, v));
                    for (int t = ringB + 4; t + 1 <= Math.Min(edge, Near(room, horizontal, door)) - 2; t++) legs.Add(new Leg(door, t));
                }
            }
            return legs;
        }
        Leg Choose(List<Leg> legs, int slot)
        {
            var straight = legs.FirstOrDefault(l => l.Turn == int.MinValue);
            var turns = legs.Where(l => l.Turn != int.MinValue).ToList();
            if (straight != null && (turns.Count == 0 || H(slot) % 100 < 40)) return straight;
            return turns[(int)(H(slot + 1) % (uint)turns.Count)];
        }
        var lanes = Enumerable.Range(laneMin, laneMax - laneMin + 1).OrderBy(v => H(2 + v - v0)).ThenBy(v => v);
        foreach (int lane in lanes)
        {
            var legsA = Legs(true, lane); var legsB = Legs(false, lane);
            if (legsA.Count == 0 || legsB.Count == 0) continue;
            var legA = Choose(legsA, 40); var legB = Choose(legsB, 60);
            void Fill(int u0, int u1, int w0, int w1)
            {
                for (int u = Math.Min(u0, u1); u <= Math.Max(u0, u1); u++)
                    for (int v = Math.Min(w0, w1); v <= Math.Max(w0, w1); v++)
                        tiles[horizontal ? v * width + u : u * width + v] = Terrain.Floor;
            }
            if (legA.Turn == int.MinValue) Fill(Far(a, horizontal, lane) + 1, ringA, lane - half, lane + half);
            else
            {
                Fill(Far(a, horizontal, legA.Lane) + 1, legA.Turn + half, legA.Lane - half, legA.Lane + half);
                Fill(legA.Turn - half, legA.Turn + half, legA.Lane, lane);
                Fill(legA.Turn - half, ringA, lane - half, lane + half);
            }
            if (legB.Turn == int.MinValue) Fill(ringB, Near(b, horizontal, lane) - 1, lane - half, lane + half);
            else
            {
                Fill(ringB, legB.Turn + half, lane - half, lane + half);
                Fill(legB.Turn - half, legB.Turn + half, lane, legB.Lane);
                Fill(legB.Turn - half, Near(b, horizontal, legB.Lane) - 1, legB.Lane - half, legB.Lane + half);
            }
            return new DungeonPassage { X = horizontal ? ringA : lane, Y = horizontal ? lane : ringA, Dx = horizontal ? 1 : 0, Dy = horizontal ? 0 : 1, Locked = locked };
        }
        return null;
    }
}
