using System.Numerics;

namespace Mosslight;
/// <summary>Reusable regional spatial buckets. Snapshot before movement avoids iteration-order bias.</summary>
public sealed class ActorSeparation
{
    readonly Dictionary<(string, int, int), List<(string Id, Vector2 Position, float Radius)>> buckets = new();
    readonly List<List<(string, Vector2, float)>> pool = new();
    int used;
    const float Cell = 48;
    public void Rebuild(IEnumerable<ActorState> actors)
    {
        buckets.Clear(); used = 0;
        foreach (var a in actors.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            if (!a.Alive) continue;
            var key = (a.Region, (int)(a.X / Cell), (int)(a.Y / Cell));
            if (!buckets.TryGetValue(key, out var list))
            {
                if (used == pool.Count) pool.Add(new());
                list = pool[used++]; list.Clear(); buckets[key] = list;
            }
            list.Add((a.Id, a.Position, a.Radius));
        }
    }
    public Vector2 Velocity(ActorState a, RegionState r, Vector2 desired, float speed, float dt)
    {
        Vector2 correction = Vector2.Zero;
        int cx = (int)(a.X / Cell), cy = (int)(a.Y / Cell);
        for (int y = cy - 1; y <= cy + 1; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                if (buckets.TryGetValue((a.Region, x, y), out var neighbours))
                    foreach (var b in neighbours)
                    {
                        if (b.Id == a.Id) continue;
                        var delta = a.Position - b.Position;
                        float reach = (a.Radius + b.Radius) * 1.6f + 8, distance = delta.Length();
                        if (distance >= reach) continue;
                        if (distance < .001f)
                        {
                            string first = StringComparer.Ordinal.Compare(a.Id, b.Id) < 0 ? a.Id : b.Id;
                            float angle = ((uint)World.StableId(first) % 360) * MathF.PI / 180;
                            delta = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (a.Id == first ? 1 : -1);
                        }
                        else delta /= distance;
                        correction += delta * ((reach - distance) / reach);
                    }
        Vector2 velocity = desired + correction * speed * 1.25f;
        if (velocity.LengthSquared() > speed * speed && speed > 0) velocity = Vector2.Normalize(velocity) * speed;
        // Never let avoidance defeat a valid navigation waypoint in a narrow passage.
        if (!r.CanTravel(a.Position, a.Position + velocity * dt, a)) return desired;
        return velocity;
    }
}
