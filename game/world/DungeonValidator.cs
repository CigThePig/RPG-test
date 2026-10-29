namespace Mosslight;
public static class DungeonValidator
{
    /// <summary>Searches actual tile lock/key states. Greedy key spending incorrectly rejects branching missions.</summary>
    public static bool Solvable(RegionState r) => TrySolve(r, out _);

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
            var flood = new Queue<int>();
            flood.Enqueue(r.Entrance);
            var visited = new HashSet<int>();
            var frontier = new HashSet<int>();
            while (flood.Count > 0)
            {
                int at = flood.Dequeue();
                if (!visited.Add(at))
                    continue;
                if (at == r.Objective) { gatePlan = plan; return true; }
                int key = Array.IndexOf(keys, at);
                if (key >= 0 && (collected & (1 << key)) == 0)
                {
                    collected |= 1 << key;
                    count++;
                }

                foreach (int n in r.Neighbours(at))
                {
                    if (r.Tiles[n] is Terrain.Wall or Terrain.Rock or Terrain.Water)
                        continue;
                    if (groupAt.TryGetValue(n, out int gate) && (open & (1 << gate)) == 0)
                    {
                        frontier.Add(gate);
                        continue;
                    }

                    if (!visited.Contains(n))
                        flood.Enqueue(n);
                }
            }

            if (count > 0)
                foreach (int gate in frontier.Order())
                    states.Enqueue((open | (1 << gate), collected, count - 1, plan.Append(gateGroups[gate][0]).ToArray()));
            if (seen.Count > 10000)
                return false;
        }

        return false;
    }
}
