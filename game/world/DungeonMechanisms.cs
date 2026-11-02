namespace Mosslight;

/// <summary>
/// The mission grammar's lever and puzzle nodes, made physical. RegionState features remain the
/// only authority: a "lever" tile raises every "portcullis" gate on its own block's lever passages
/// (a one-way shortcut from deep in the ruin back toward the entrance), and a ring of four
/// "brazier" tiles breaks every "seal" gate on its own block's puzzle passages once all four burn.
/// Lighting a brazier also flips its two ring neighbours, so the ring is a small, always-solvable
/// lights-out puzzle. Nothing here consumes mission RNG or changes room/landmark composition.
/// </summary>
public static class DungeonMechanisms
{
    public const int Revision = 1;
    public const int RingSize = 4;
    // Clockwise north, east, south, west of the core centre. Every offset lies on the landmark
    // protected cross (local 4..6 on one axis), which templates must leave as open floor.
    static readonly (int X, int Y)[] RingOffsets = { (0, -3), (3, 0), (0, 3), (-3, 0) };
    const int AllLit = (1 << RingSize) - 1;

    public readonly record struct Control(string Kind, int[] Controllers, int[] Gates);

    /// <summary>Runs once on a fresh dungeon after landmark composition.</summary>
    public static void Install(RegionState r, IEnumerable<(int X, int Y, string Kind)> rooms)
    {
        foreach (var (x, y, kind) in rooms.OrderBy(m => m.Y).ThenBy(m => m.X))
        {
            var room = r.Rooms.Single(c => c.X == x && c.Y == y);
            var passages = r.Passages.Where(p => !p.Locked && p.Mechanism == kind && p.Touches(x, y)).ToList();
            if (passages.Count == 0 || room.Kind != "room") continue;
            if (kind == "lever")
            {
                int at = room.CenterY * r.Width + room.CenterX;
                if (r.Features.ContainsKey(at)) continue;
                r.Features[at] = "lever";
                foreach (var passage in passages) foreach (int cell in passage.GateCells(r.Width)) r.Features[cell] = "portcullis";
            }
            else if (kind == "puzzle")
            {
                var ring = RingOffsets.Select(o => (room.CenterY + o.Y) * r.Width + room.CenterX + o.X).ToArray();
                if (ring.Any(r.Features.ContainsKey) || ring.Any(at => r.Tiles[at] != Terrain.Floor)) continue;
                foreach (int at in ring) r.Features[at] = "brazier";
                // Pick a starting pattern that needs at least two lightings. The ring matrix is
                // invertible over GF(2) for four braziers, so every pattern has one minimal answer.
                var presses = Enumerable.Range(1, AllLit).Where(m => System.Numerics.BitOperations.PopCount((uint)m) >= 2).ToArray();
                int answer = presses[(int)(DungeonLayout.Hash(r.Seed, x, y, 4057 + Revision) % (uint)presses.Length)];
                int lit = AllLit ^ Effect(answer);
                for (int i = 0; i < RingSize; i++) if ((lit & (1 << i)) != 0) r.Lit.Add(ring[i]);
                foreach (var passage in passages) foreach (int cell in passage.GateCells(r.Width)) r.Features[cell] = "seal";
            }
        }
        // Mechanisms only ever add optional structure; the ruin keeps its proven solution.
        var reach = DungeonValidator.Reach(r, allKeyGatesOpen: true);
        bool controllersReachable = Controls(r).All(c => c.Controllers.Any(reach.Contains));
        if (!controllersReachable || !DungeonValidator.Solvable(r)) Strip(r);
        foreach (var passage in r.Passages)
            if (passage.Mechanism != "" && !passage.GateCells(r.Width).All(at => r.Features.GetValueOrDefault(at) is "portcullis" or "seal"))
                passage.Mechanism = "";
    }

    static void Strip(RegionState r)
    {
        foreach (int at in r.Features.Where(f => f.Value is "lever" or "brazier" or "portcullis" or "seal").Select(f => f.Key).ToArray())
            r.Features.Remove(at);
        r.Lit.Clear();
        foreach (var passage in r.Passages) passage.Mechanism = "";
    }

    static int Effect(int presses)
    {
        int flips = 0;
        for (int i = 0; i < RingSize; i++)
            if ((presses & (1 << i)) != 0)
                flips ^= (1 << i) | (1 << ((i + 1) % RingSize)) | (1 << ((i + RingSize - 1) % RingSize));
        return flips;
    }

    static (int X, int Y) Block(RegionState r, int at) => (at % r.Width / DungeonGeometry.Cell, at / r.Width / DungeonGeometry.Cell);

    public static int[] GatesFor(RegionState r, int blockX, int blockY, string kind)
    {
        string gate = kind == "lever" ? "portcullis" : "seal";
        return r.Passages.Where(p => p.Mechanism == kind && p.Touches(blockX, blockY))
            .SelectMany(p => p.GateCells(r.Width)).Where(at => r.Features.GetValueOrDefault(at) == gate).Order().ToArray();
    }

    /// <summary>Brazier tiles of one puzzle room, in ring order.</summary>
    public static int[] Ring(RegionState r, DungeonRoom room) =>
        RingOffsets.Select(o => (room.CenterY + o.Y) * r.Width + room.CenterX + o.X).Where(at => r.Features.GetValueOrDefault(at) == "brazier").ToArray();

    public static int[] RingAt(RegionState r, int brazier)
    {
        var (bx, by) = Block(r, brazier);
        var room = r.Rooms.FirstOrDefault(c => c.X == bx && c.Y == by);
        return room == null ? Array.Empty<int>() : Ring(r, room);
    }

    /// <summary>Every lever and brazier ring with the gates it controls. Derived from saved features.</summary>
    public static List<Control> Controls(RegionState r)
    {
        var result = new List<Control>();
        foreach (int lever in r.Features.Where(f => f.Value == "lever").Select(f => f.Key).Order())
        {
            var (bx, by) = Block(r, lever);
            result.Add(new("lever", new[] { lever }, GatesFor(r, bx, by, "lever")));
        }
        foreach (var room in r.Rooms)
        {
            var ring = Ring(r, room);
            if (ring.Length > 0) result.Add(new("puzzle", ring, GatesFor(r, room.X, room.Y, "puzzle")));
        }
        return result;
    }

    public static bool Solved(RegionState r, int[] ring) => ring.Length > 0 && ring.All(r.Lit.Contains);

    /// <summary>Pulls a lever once. Returns the portcullis cells raised.</summary>
    public static int[] Pull(RegionState r, int lever)
    {
        if (r.Features.GetValueOrDefault(lever) != "lever" || r.Depleted.Contains(lever)) return Array.Empty<int>();
        var (bx, by) = Block(r, lever);
        var gates = GatesFor(r, bx, by, "lever");
        r.Depleted.Add(lever);
        foreach (int at in gates) r.OpenDoors.Add(at);
        return gates;
    }

    /// <summary>Lights (or douses) one brazier and flips its ring neighbours. When the whole ring
    /// burns, the braziers are spent and every seal they control is broken.</summary>
    public static bool Kindle(RegionState r, int brazier, out bool solved)
    {
        solved = false;
        var ring = RingAt(r, brazier);
        int i = Array.IndexOf(ring, brazier);
        if (i < 0 || r.Depleted.Contains(brazier)) return false;
        foreach (int k in new[] { i - 1, i, i + 1 })
        {
            int at = ring[(k + ring.Length) % ring.Length];
            if (!r.Lit.Remove(at)) r.Lit.Add(at);
        }
        if (!Solved(r, ring)) return true;
        solved = true;
        var (bx, by) = Block(r, brazier);
        foreach (int at in ring) r.Depleted.Add(at);
        foreach (int at in GatesFor(r, bx, by, "puzzle")) r.OpenDoors.Add(at);
        return true;
    }

    /// <summary>The fewest braziers to light, in ring order, from the current state.</summary>
    public static int[] Solution(RegionState r, int[] ring)
    {
        if (ring.Length != RingSize || Solved(r, ring)) return Array.Empty<int>();
        int lit = 0;
        for (int i = 0; i < RingSize; i++) if (r.Lit.Contains(ring[i])) lit |= 1 << i;
        int best = Enumerable.Range(1, AllLit).Where(m => (lit ^ Effect(m)) == AllLit)
            .OrderBy(m => System.Numerics.BitOperations.PopCount((uint)m)).ThenBy(m => m).First();
        return Enumerable.Range(0, RingSize).Where(i => (best & (1 << i)) != 0).Select(i => ring[i]).ToArray();
    }
}
