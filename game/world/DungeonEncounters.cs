using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Once-only placement policy. Combat, navigation and saved actors remain World-owned.</summary>
public static class DungeonEncounters
{
    public static bool Place(RegionState r, ActorState actor, IEnumerable<ActorState> occupants, int slot, out V point)
    {
        var others = occupants.Where(a => a.Region == r.Id && a.Alive && a.Id != actor.Id).ToArray();
        var entrance = r.RoomAt(r.Entrance % r.Width, r.Entrance / r.Width);
        var sanctuary = r.RoomAt(r.Objective % r.Width, r.Objective / r.Width);
        var rooms = r.Rooms.Where(room => room.Kind == "room" && room != entrance)
            .Select(room => new { Room = room, Area = room.Tiles().Count(), Local = others.Count(a => room.Contains((int)(a.X/24),(int)(a.Y/24))) })
            .Where(a => a.Local < (a.Area >= 100 ? 2 : 1))
            .OrderBy(a => slot == 0 && a.Room == sanctuary ? -1 : a.Local)
            .ThenBy(a => actor.CombatProfile == "brute" ? -a.Area : 0)
            .ThenBy(a => DungeonLayout.Hash(r.Seed, a.Room.X, a.Room.Y, 773 + slot)).ThenBy(a => a.Room.Y).ThenBy(a => a.Room.X);
        foreach (var candidate in rooms)
        {
            foreach (int at in candidate.Room.Tiles().Select(t => t.Y*r.Width+t.X)
                .OrderBy(at => DungeonLayout.Hash(r.Seed, at, slot, 811)).ThenBy(at => at))
            {
                var p = r.Center(at);
                if (!r.CanStand(p,actor) || V.DistanceSquared(p,r.Center(r.Entrance)) < 240*240 ||
                    r.Features.Keys.Any(f => V.DistanceSquared(p,r.Center(f)) < 48*48) ||
                    others.Any(a => V.DistanceSquared(p,a.Position) < MathF.Pow(a.Radius+actor.Radius+16,2))) continue;
                // Check useful local movement in all directions; spitter needs open sightlines.
                float lane = actor.CombatProfile == "spitter" ? 48 : 24;
                int clear = new[]{V.UnitX,-V.UnitX,V.UnitY,-V.UnitY}.Count(d => r.CanTravel(p,p+d*lane,actor));
                if (clear < (actor.CombatProfile == "spitter" ? 3 : 2)) continue;
                point=p; return true;
            }
        }
        point=default; return false;
    }
}
