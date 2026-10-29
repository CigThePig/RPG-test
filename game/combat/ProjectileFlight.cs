using System.Numerics;

namespace Mosslight;
public readonly record struct ProjectileCollision(string Outcome, ActorState? Actor = null);
public static class ProjectileFlight
{
    public static ProjectileState Release(ActorState source, ActorState target, AbilityDef ability)
    {
        var heading = CombatGeometry.TryNormalize(target.Position - source.Position, out var d) ? d : Vector2.UnitX;
        return new() { Source = source.Id, Target = target.Id, Ability = ability.Id, Region = source.Region,
            X = source.X, Y = source.Y, HeadingX = heading.X, HeadingY = heading.Y, Remaining = ability.Projectile?.Lifetime ?? 4 };
    }
    public static Vector2 Turn(Vector2 heading, Vector2 desired, float maximum)
    {
        float angle = MathF.Atan2(heading.X * desired.Y - heading.Y * desired.X, Vector2.Dot(heading, desired));
        angle = Math.Clamp(angle, -maximum, maximum);
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new(heading.X * c - heading.Y * s, heading.X * s + heading.Y * c);
    }
    public static bool Circle(Vector2 from, Vector2 delta, Vector2 center, float radius, out float t)
    {
        t = 0;
        var offset = from - center;
        float c = offset.LengthSquared() - radius * radius;
        if (c <= 0) return true;
        float a = delta.LengthSquared();
        if (a <= .000001f) return false;
        float b = Vector2.Dot(offset, delta), disc = b * b - a * c;
        if (disc < 0) return false;
        t = (-b - MathF.Sqrt(disc)) / a;
        return t >= 0 && t <= 1;
    }
    static bool Box(Vector2 from, Vector2 delta, Vector2 min, Vector2 max, out float t)
    {
        float low = 0, high = 1;
        for (int axis = 0; axis < 2; axis++)
        {
            float p = axis == 0 ? from.X : from.Y, v = axis == 0 ? delta.X : delta.Y;
            float lo = axis == 0 ? min.X : min.Y, hi = axis == 0 ? max.X : max.Y;
            if (MathF.Abs(v) < .000001f) { if (p < lo || p > hi) { t = 0; return false; } continue; }
            float x = (lo - p) / v, y = (hi - p) / v;
            low = Math.Max(low, Math.Min(x, y)); high = Math.Min(high, Math.Max(x, y));
            if (low > high) { t = 0; return false; }
        }
        t = low; return true;
    }
    // Exact disk sweep against a tile: two side rectangles plus four rounded corners.
    static bool TileHit(Vector2 from, Vector2 delta, int x, int y, float radius, out float t)
    {
        var min = new Vector2(x * 24, y * 24); var max = min + new Vector2(24);
        t = float.PositiveInfinity;
        if (Box(from, delta, min - new Vector2(radius, 0), max + new Vector2(radius, 0), out float q)) t = q;
        if (Box(from, delta, min - new Vector2(0, radius), max + new Vector2(0, radius), out q)) t = Math.Min(t, q);
        for (int i = 0; i < 4; i++)
            if (Circle(from, delta, new Vector2(i % 2 == 0 ? min.X : max.X, i < 2 ? min.Y : max.Y), radius, out q)) t = Math.Min(t, q);
        return float.IsFinite(t);
    }
    public static ProjectileCollision Advance(World w, ProjectileState p, AbilityDef ability, RegionState r, float dt)
    {
        var from = new Vector2(p.X, p.Y);
        var heading = new Vector2(p.HeadingX, p.HeadingY);
        w.Actors.TryGetValue(p.Target, out var target);
        bool validTarget = target != null && target.Alive && target.Region == r.Id;
        string mode = ability.Projectile?.Mode ?? "legacy";
        if (!CombatGeometry.TryNormalize(heading, out heading))
            heading = validTarget && CombatGeometry.TryNormalize(target!.Position - from, out var initial) ? initial : Vector2.UnitX;
        if (mode != "straight" && validTarget && CombatGeometry.TryNormalize(target!.Position - from, out var desired))
            heading = mode == "legacy" ? desired : Turn(heading, desired, (ability.Projectile?.TurnDegrees ?? 60) * MathF.PI / 180 * dt);
        p.HeadingX = heading.X; p.HeadingY = heading.Y;
        p.Region = r.Id;
        if (p.Remaining <= 0) return new("expired");
        // Target loss continues along the last heading, including migrated legacy records.
        float travelTime = Math.Min(dt, p.Remaining);
        var delta = heading * ability.ProjectileSpeed * travelTime;
        float radius = ability.Projectile?.Radius ?? 0;
        float earliest = float.PositiveInfinity;
        string outcome = ""; ActorState? victim = null;
        // DDA visits only crossed grid cells; radius <= 12 needs at most the adjacent ring.
        int cx = (int)MathF.Floor(from.X / 24), cy = (int)MathF.Floor(from.Y / 24);
        int sx = Math.Sign(delta.X), sy = Math.Sign(delta.Y);
        float tx = sx == 0 ? float.PositiveInfinity : ((sx > 0 ? cx + 1 : cx) * 24 - from.X) / delta.X;
        float ty = sy == 0 ? float.PositiveInfinity : ((sy > 0 ? cy + 1 : cy) * 24 - from.Y) / delta.Y;
        float dx = sx == 0 ? float.PositiveInfinity : 24 / MathF.Abs(delta.X), dy = sy == 0 ? float.PositiveInfinity : 24 / MathF.Abs(delta.Y);
        int limit = r.Width + r.Height + 4;
        for (int n = 0; n < limit; n++)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    bool boundary = x < 0 || y < 0 || x >= r.Width || y >= r.Height;
                    if (!boundary && !r.BlocksSight(y * r.Width + x)) continue;
                    if (TileHit(from, delta, x, y, radius, out float t) && t < earliest)
                    { earliest = t; outcome = boundary ? "boundary" : "terrain"; }
                }
            float next = Math.Min(tx, ty);
            if (next > 1 || next > earliest) break;
            if (tx <= ty) { cx += sx; tx += dx; } else { cy += sy; ty += dy; }
        }
        if (w.Actors.TryGetValue(p.Source, out var source))
            foreach (var actor in w.Actors.Values)
            {
                if (!actor.Alive || actor.Region != r.Id || !w.Hostile(source, actor)) continue;
                if (!Circle(from, delta, actor.Position, actor.Radius + radius, out float t)) continue;
                // Terrain wins near-equal contacts. Actor ties use stable IDs.
                if (t < earliest - .000001f || (outcome == "actor" && MathF.Abs(t - earliest) <= .000001f && StringComparer.Ordinal.Compare(actor.Id, victim!.Id) < 0))
                { earliest = t; victim = actor; outcome = "actor"; }
            }
        var at = from + delta * (outcome == "" ? 1 : earliest);
        p.X = at.X; p.Y = at.Y; p.Remaining = Math.Max(0, p.Remaining - dt);
        if (outcome != "") return new(outcome, victim);
        return new(p.Remaining <= 0 ? "expired" : "");
    }
}
