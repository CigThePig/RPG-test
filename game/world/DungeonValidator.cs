namespace Mosslight;
public static class DungeonValidator
{
    /// <summary>Searches actual tile lock/key states. Greedy key spending incorrectly rejects branching missions.</summary>
    public static bool Solvable(RegionState r) => TrySolve(r, out _);

    sealed class Mechanisms
    {
        public readonly List<DungeonMechanisms.Control> Controls;
        public readonly Dictionary<int, int> GateOf = new();
        public Mechanisms(RegionState r)
        {
            Controls = DungeonMechanisms.Controls(r);
            for (int i = 0; i < Controls.Count; i++)
                foreach (int at in Controls[i].Gates) GateOf.TryAdd(at, i);
        }
    }

    // Floods walkable tiles from the entrance. Levers and brazier rings cost nothing, so every
    // controller the player can reach opens its gates and the flood repeats to a fixed point.
    static HashSet<int> Flood(RegionState r, Mechanisms mechanisms, Func<int, bool> keyGateClosed, Action<int>? visit = null, Action<int>? blockedByKeyGate = null)
    {
        var raised = new bool[mechanisms.Controls.Count];
        for (int i = 0; i < raised.Length; i++)
            raised[i] = mechanisms.Controls[i].Gates.Length > 0 && mechanisms.Controls[i].Gates.All(r.OpenDoors.Contains);
        while (true)
        {
            var visited = new HashSet<int>();
            var flood = new Queue<int>();
            flood.Enqueue(r.Entrance);
            while (flood.Count > 0)
            {
                int at = flood.Dequeue();
                if (!visited.Add(at)) continue;
                visit?.Invoke(at);
                foreach (int n in r.Neighbours(at))
                {
                    if (r.Tiles[n] is Terrain.Wall or Terrain.Rock or Terrain.Water || visited.Contains(n)) continue;
                    if (keyGateClosed(n)) { blockedByKeyGate?.Invoke(n); continue; }
                    // A closed mechanism gate is a wall until its own reachable controller opens it;
                    // an orphaned one (no lever or ring left) never opens.
                    if (r.Features.GetValueOrDefault(n) is "portcullis" or "seal" && !r.OpenDoors.Contains(n) &&
                        !(mechanisms.GateOf.TryGetValue(n, out int m) && raised[m])) continue;
                    flood.Enqueue(n);
                }
            }
            bool changed = false;
            for (int i = 0; i < raised.Length; i++)
                if (!raised[i] && mechanisms.Controls[i].Controllers.Any(visited.Contains)) raised[i] = changed = true;
            if (!changed) return visited;
        }
    }

    /// <summary>Tiles reachable from the entrance using every reachable lever and brazier ring,
    /// optionally treating every key gate as already unlocked.</summary>
    public static HashSet<int> Reach(RegionState r, bool allKeyGatesOpen = false) =>
        Flood(r, new Mechanisms(r), at => !allKeyGatesOpen && r.Features.GetValueOrDefault(at) == "lock" && !r.OpenDoors.Contains(at));

    // Gate representatives form a reproducible witness, also used by production
    // interaction replays. Already collected keys can be supplied for saved states.
    public static bool TrySolve(RegionState r, out int[] gatePlan, int availableKeys = 0)
    {
        gatePlan = Array.Empty<int>();
        var keys = r.Features.Where(x => x.Value == "key" && !r.Depleted.Contains(x.Key)).Select(x => x.Key).Order().ToArray();
        var gateGroups = r.GateGroups();
        var groupAt = new Dictionary<int, int>();
        for (int group = 0; group < gateGroups.Count; group++)
            foreach (int at in gateGroups[group]) groupAt.Add(at, group);
        int groups = gateGroups.Count;

        if (groups > 24 || keys.Length > 24)
            return false;
        var mechanisms = new Mechanisms(r);
        var states = new Queue<(int Open, int Collected, int Count, int[] Plan)>();
        int alreadyOpen = 0;
        for (int group = 0; group < groups; group++)
            if (gateGroups[group].All(r.OpenDoors.Contains)) alreadyOpen |= 1 << group;
        states.Enqueue((alreadyOpen, 0, availableKeys, Array.Empty<int>()));
        var seen = new HashSet<(int, int)>();
        while (states.Count > 0)
        {
            var(open, collected, count, plan) = states.Dequeue();
            if (!seen.Add((open, collected)))
                continue;
            var frontier = new HashSet<int>();
            var visited = Flood(r, mechanisms,
                at => groupAt.TryGetValue(at, out int gate) && (open & (1 << gate)) == 0,
                at =>
                {
                    int key = Array.IndexOf(keys, at);
                    if (key >= 0 && (collected & (1 << key)) == 0) { collected |= 1 << key; count++; }
                },
                at => frontier.Add(groupAt[at]));
            if (visited.Contains(r.Objective)) { gatePlan = plan; return true; }

            if (count > 0)
                foreach (int gate in frontier.Order())
                    states.Enqueue((open | (1 << gate), collected, count - 1, plan.Append(gateGroups[gate][0]).ToArray()));
            if (seen.Count > 10000)
                return false;
        }

        return false;
    }
}
