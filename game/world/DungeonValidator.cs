namespace Mosslight;
public static class DungeonValidator
{
    /// <summary>Searches actual tile lock/key states. Greedy key spending incorrectly rejects branching missions.</summary>
    public static bool Solvable(RegionState r)
    {
        var keys = r.Features.Where(x => x.Value == "key").Select(x => x.Key).Order().ToArray();
        var locks = r.Features.Where(x => x.Value == "lock").Select(x => x.Key).Order().ToArray();
        var groupAt = new Dictionary<int, int>();
        int groups = 0;
        foreach (int at in locks)
        {
            if (groupAt.ContainsKey(at))
                continue;
            var queue = new Queue<int>();
            queue.Enqueue(at);
            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                if (!groupAt.TryAdd(n, groups))
                    continue;
                foreach (int k in r.Neighbours(n))
                    if (r.Features.GetValueOrDefault(k) == "lock")
                        queue.Enqueue(k);
            }

            groups++;
        }

        if (groups > 24 || keys.Length > 24)
            return false;
        var states = new Queue<(int Open, int Collected, int Count)>();
        states.Enqueue((0, 0, 0));
        var seen = new HashSet<(int, int)>();
        while (states.Count > 0)
        {
            var(open, collected, count) = states.Dequeue();
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
                if (at == r.Objective)
                    return true;
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
                foreach (int gate in frontier)
                    states.Enqueue((open | (1 << gate), collected, count - 1));
            if (seen.Count > 10000)
                return false;
        }

        return false;
    }
}
