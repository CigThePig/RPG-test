using System.Numerics;

namespace Mosslight;
public readonly record struct CombatIntention(string Kind, Vector2 Destination, string Ability = "");

/// <summary>Intent only: SPELL remains the sole eligibility/cost/effect executor.</summary>
public static class CombatBrain
{
    public static CombatIntention Select(World w, ActorState a, ActorState target, CombatProfileDef? profile, bool retreat)
    {
        if (!a.Alive || a.State is not (ActionState.Idle or ActionState.Moving) || !w.Hostile(a, target))
            return new("wait", a.Position);
        float distance = Vector2.Distance(a.Position, target.Position);
        var toward = CombatGeometry.TryNormalize(target.Position - a.Position, out var dir) ? dir : Vector2.UnitX;
        if (retreat || (profile != null && distance < profile.MinimumDistance) ||
            (a.TacticalIntent == "retreat" && profile != null && distance < profile.MinimumDistance + 12))
        {
            var escape = LocalDestination(w, a, -toward, 42);
            if (escape != a.Position) return new("retreat", escape);
            // Cornered enemies may attack; never repeat an invalid escape vector.
        }
        AbilityDef? best = null;
        float bestScore = float.NegativeInfinity;
        foreach (var ability in w.Content.Abilities)
        {
            if (ability.Self || !w.CanActivate(a, target, ability) || (ability.Geometry?.Shape == "cone" && distance > ability.Geometry.Range)) continue;
            float score = profile?.Priorities.GetValueOrDefault(ability.Id, .1f) ?? 1;
            if (ability.ProjectileSpeed > 0 && profile != null && distance < profile.MinimumDistance) score *= .2f;
            if (score > bestScore || (score == bestScore && StringComparer.Ordinal.Compare(ability.Id, best?.Id) < 0))
            { best = ability; bestScore = score; }
        }
        if (best != null) return new("ability", a.Position, best.Id);
        float approach = profile?.ApproachDistance ?? 28;
        float preferred = profile?.PreferredDistance ?? 22;
        if (distance > approach || (a.TacticalIntent == "approach" && distance > preferred))
            return new("approach", target.Position - toward * preferred);
        // Stable actor/slow-time slots avoid synchronized perpetual orbiting.
        int slot = (int)((uint)World.StableId(a.Id) % 100);
        if (profile != null && slot < profile.Reposition * 100 && ((long)(w.Time * 2) + slot) % 5 == 0)
        {
            var side = new Vector2(-toward.Y, toward.X) * (slot % 2 == 0 ? 1 : -1);
            var point = LocalDestination(w, a, side, 24);
            if (point != a.Position) return new("circle", point);
        }
        return new("hold", a.Position);
    }

    static Vector2 LocalDestination(World w, ActorState a, Vector2 direction, float length)
    {
        var r = w.RegionsById[a.Region];
        // Fixed ordered alternatives, bounded to six local travel tests, no path searches.
        foreach (float angle in new[] { 0f, .65f, -.65f, 1.3f, -1.3f, 1.57f })
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            var point = a.Position + new Vector2(direction.X * c - direction.Y * s, direction.X * s + direction.Y * c) * length;
            if (r.CanTravel(a.Position, point, a)) return point;
        }
        return a.Position;
    }
}
