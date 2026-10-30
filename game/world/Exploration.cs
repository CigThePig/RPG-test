using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Consumes saved room/landmark manifests; never reads mutable template definitions.</summary>
public static class Exploration
{
    public static string Identity(RegionState r, LandmarkRoom l) => $"{r.Id}:{l.X},{l.Y}:{l.Template}@{l.Revision}:{l.Rotation}";
    public static void Discover(World w)
    {
        var p = w.Player;
        if (!p.Alive || !w.RegionsById.TryGetValue(p.Region, out var r) || r.Landmarks.Count == 0) return;
        int x = (int)(p.X / RegionState.Tile), y = (int)(p.Y / RegionState.Tile);
        int cell = r.Index(p.Position);
        if (!r.ExploredTiles.Contains(cell))
            for (int dy=-3;dy<=3;dy++) for(int dx=-3;dx<=3;dx++)
                if(x+dx>=0 && y+dy>=0 && x+dx<r.Width && y+dy<r.Height)
                    r.ExploredTiles.Add((y+dy)*r.Width+x+dx);
        var room = r.RoomAt(x, y);
        if (room == null || room.Kind != "room" || !room.Contains(x, y)) return;
        var l = r.Landmarks.FirstOrDefault(l => l.X == room.X && l.Y == room.Y);
        if (l == null) return;
        string id = Identity(r,l);
        if (r.DiscoveredLandmarks.Add(id)) w.Events.Publish(new("landmark_discovered", p.Id, id, l.Template));
    }

    // Only freshly generated regions opt in. Old saves keep their exact features and bags.
    public static void PrepareCache(RegionState r)
    {
        if (r.ExplorationInitialized) return;
        r.ExplorationInitialized = true;
        var probe = new ActorState { Id = "cache-probe", Region = r.Id, Radius = 11 };
        foreach (var l in r.Landmarks.Where(l => l.Template is "collapsed-storeroom" or "forgotten-archive" or "abandoned-shrine")
                     .OrderBy(l => l.Template == "collapsed-storeroom" ? 0 : 1).ThenBy(l => l.Y).ThenBy(l => l.X))
        {
            var room = r.Rooms.Single(a => a.X == l.X && a.Y == l.Y);
            var cells = room.Tiles().Select(t => t.Y * r.Width + t.X)
                .Where(at => r.CanStand(r.Center(at), probe, true) && !r.Features.ContainsKey(at) &&
                    r.Features.Keys.All(f => V.DistanceSquared(r.Center(at), r.Center(f)) >= 72 * 72))
                .OrderBy(at => DungeonLayout.Hash(r.Seed, at, 0, 619)).ThenBy(at => at);
            foreach (int at in cells)
            {
                // Chest has the same nonblocking interaction footprint as ordinary mission chests.
                r.Features[at] = "chest";
                return; // One optional cache, bounded and deterministic.
            }
        }
    }
}
