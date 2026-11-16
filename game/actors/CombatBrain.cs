using System.Numerics;

namespace Mosslight;
public readonly record struct CombatIntention(string Kind, Vector2 Destination, string Ability = "", float Pace = 1);

/// <summary>
/// Intent only: SPELL remains the sole eligibility/cost/effect executor. A pure function of
/// world state, so identical state always yields the identical intention.
/// </summary>
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
            // Far enough: a routed actor stops running and watches from where it is.
            if (retreat && distance >= CombatTuning.RoutDistance) return new("cower", a.Position);
            var escape = LocalDestination(w, a, retreat ? Rout(w, a, target, toward) : -toward, 42);
            if (escape != a.Position) return new("retreat", escape);
            // Cornered enemies may attack; never repeat an invalid escape vector.
        }
        // A roar or a rallying call comes before the next blow.
        if (profile != null && SelfAbility(w, a, profile) is { } own) return new("ability", a.Position, own);
        bool contact = profile != null && profile.Behaviour is "melee" or "skirmish";
        if (profile?.Behaviour == "melee" && distance < profile.ApproachDistance + 72 &&
            w.Actors.Values.Any(b => b.Id != a.Id && b.Id != target.Id && b.Alive && b.Region == a.Region && !w.Hostile(a,b) &&
                Vector2.DistanceSquared(a.Position,b.Position) < MathF.Pow(a.Radius+b.Radius+12,2) && w.LineOfSight(a,b)))
        {
            // Local congestion: step to an actor-phased spot rather than strike through an ally.
            float angle = ((uint)World.StableId(a.Id) % 8) * MathF.PI / 4;
            var position = target.Position + new Vector2(MathF.Cos(angle),MathF.Sin(angle)) * profile.ApproachDistance;
            if (Vector2.DistanceSquared(position,a.Position)>12*12 && w.RegionsById[a.Region].CanTravel(a.Position,position,a))
                return new("circle",position);
        }
        // Only a few may press one target at once; the others work around to its flanks.
        if (contact && !HasToken(w, a, target))
            return Flank(w, a, target, profile!, toward, distance);
        AbilityDef? best = null;
        float bestScore = float.NegativeInfinity;
        foreach (var ability in w.Content.Abilities)
        {
            if (ability.Self || !w.CanActivate(a, target, ability) || !InReach(w, a, ability, toward, distance)) continue;
            float score = profile?.Priorities.GetValueOrDefault(ability.Id, .1f) ?? ability.Range;
            if (ability.ProjectileSpeed > 0 && profile != null && distance < profile.MinimumDistance) score *= .2f;
            if (score > bestScore || (score == bestScore && StringComparer.Ordinal.Compare(ability.Id, best?.Id) < 0))
            { best = ability; bestScore = score; }
        }
        if (best != null)
        {
            // A slow turner must first bring its front round to what it means to hit.
            if (profile is { TurnDegrees: > 0 } && CombatGeometry.TryNormalize(a.Facing, out var front) &&
                !CombatGeometry.InCone(a.Position, target.Position, front, distance + 1, CombatTuning.TurnAttackHalfAngle))
                return new("turn", a.Position);
            return new("ability", a.Position, best.Id);
        }
        if (profile == null) return new("approach", target.Position); // Legacy actors retain range-first eligible attacks and pursuit.
        float approach = profile.ApproachDistance;
        float preferred = profile.PreferredDistance;
        if (profile.Behaviour == "skirmish")
        {
            // Dart in when the sting is ready, otherwise keep a wary distance.
            if (StrikeReady(w, a, profile)) return new("approach", target.Position - toward * Math.Min(preferred, 22));
            if (distance < preferred - 6)
            {
                var back = LocalDestination(w, a, -toward, Math.Min(42, preferred - distance + 12));
                if (back != a.Position) return new("withdraw", back);
            }
        }
        if (distance > approach || (a.TacticalIntent == "approach" && distance > preferred))
            return new("approach", target.Position - toward * preferred);
        if (Strafing(w, a, profile))
        {
            var point = Orbit(w, a, target, toward, Math.Clamp(distance, preferred * .8f, approach), profile);
            if (point != a.Position) return new("circle", point, Pace: .6f);
        }
        return new("hold", a.Position);
    }

    /// <summary>Can the attack actually arrive: cones cover their own range, lunges and charges need a clear run.</summary>
    static bool InReach(World w, ActorState a, AbilityDef ability, Vector2 toward, float distance)
    {
        var region = w.RegionsById[a.Region];
        if (ability.Charge is { } charge)
            return distance <= charge.Distance && region.CanTravel(a.Position, a.Position + toward * Math.Min(distance, charge.Distance), a);
        if (ability.Lunge is { } lunge)
        {
            float reach = lunge.Distance + (ability.Geometry?.Shape == "cone" ? ability.Geometry.Range : ability.Range + a.NaturalReach);
            return distance <= reach && region.CanTravel(a.Position, a.Position + toward * Math.Min(distance, lunge.Distance), a);
        }
        // A burst reaches all round the body but no further; leave a margin so a step back does not clear it at once.
        if (ability.Geometry?.Shape == "burst") return distance <= ability.Geometry.Range - 6;
        return ability.Geometry?.Shape != "cone" || distance <= ability.Geometry.Range;
    }

    static string? SelfAbility(World w, ActorState a, CombatProfileDef profile)
    {
        if (profile.EnrageAbility != "" && a.Health < a.MaxHealth * profile.EnrageThreshold && w.FindAbility(profile.EnrageAbility) is { } rage &&
            a.Abilities.Contains(rage.Id) && !a.Statuses.Any(s => s.Id == rage.Status) && w.CanActivate(a, a, rage))
            return rage.Id;
        if (profile.RallyAbility != "" && w.FindAbility(profile.RallyAbility) is { } rally && a.Abilities.Contains(rally.Id) && w.CanActivate(a, a, rally))
        {
            // A mending status is for the wounded, itself included; any other is for allies in the fight.
            bool mends = w.Content.Statuses.Any(s => s.Id == rally.Status && s.Heal > 0);
            if (w.Actors.Values.Any(c => c.Alive && c.Region == a.Region && c.Faction == a.Faction && !w.Hostile(a, c) && (mends || c.Id != a.Id) &&
                (mends ? c.Health < c.MaxHealth * .8f : c.Awareness == Awareness.Engaged) &&
                Vector2.DistanceSquared(a.Position, c.Position) <= rally.AllyRadius * rally.AllyRadius && !c.Statuses.Any(s => s.Id == rally.Status)))
                return rally.Id;
        }
        return null;
    }

    static bool IsContact(World w, ActorState b) => w.FindProfile(b.CombatProfile)?.Behaviour is "melee" or "skirmish";

    /// <summary>Has attacked recently enough that it should let another take its place.</summary>
    static bool Winded(World w, ActorState b)
    {
        foreach (var (id, remaining) in b.Cooldowns)
            if (remaining > 0 && w.FindAbility(id) is { Self: false } d && remaining > d.Cooldown * .5f) return true;
        return false;
    }

    // Lower presses first: anyone already swinging, then the nearest who is not winded.
    static float Rank(World w, ActorState b, ActorState target) =>
        (b.State is ActionState.Windup or ActionState.Recovery ? -1000 : 0) + Vector2.Distance(b.Position, target.Position) +
        (Winded(w, b) ? 60 : 0) - (b.TacticalIntent is "approach" or "ability" ? 20 : 0);

    static IEnumerable<ActorState> Rivals(World w, ActorState a, ActorState target) =>
        w.Actors.Values.Where(b => b.Id != a.Id && b.Alive && b.Region == a.Region && b.Awareness == Awareness.Engaged && b.Target == target.Id && IsContact(w, b));

    public static bool HasToken(World w, ActorState a, ActorState target)
    {
        float mine = Rank(w, a, target);
        int ahead = 0;
        foreach (var b in Rivals(w, a, target))
        {
            float theirs = Rank(w, b, target);
            if (theirs < mine || (theirs == mine && StringComparer.Ordinal.Compare(b.Id, a.Id) < 0)) ahead++;
        }
        return ahead < CombatTuning.MeleeTokens;
    }

    /// <summary>Walk the ring around the target to a slot on the far side of whoever is pressing it.</summary>
    static CombatIntention Flank(World w, ActorState a, ActorState target, CombatProfileDef profile, Vector2 toward, float distance)
    {
        var rivals = Rivals(w, a, target).ToArray();
        var pressing = rivals.Where(b => HasToken(w, b, target)).ToArray();
        var anchor = pressing.Length == 0 ? -toward : pressing.Aggregate(Vector2.Zero, (sum, b) => sum + (b.Position - target.Position));
        if (!CombatGeometry.TryNormalize(anchor, out anchor)) anchor = -toward;
        // Waiting actors fan out from the point opposite the attackers, in stable ID order.
        int index = rivals.Count(b => !pressing.Contains(b) && StringComparer.Ordinal.Compare(b.Id, a.Id) < 0);
        float fan = (index + 1) / 2 * .87f * (index % 2 == 0 ? 1 : -1);
        float slot = MathF.Atan2(-anchor.Y, -anchor.X) + fan;
        float ring = profile.ApproachDistance + 28;
        var offset = a.Position - target.Position;
        float now = MathF.Atan2(offset.Y, offset.X);
        float delta = MathF.IEEERemainder(slot - now, MathF.Tau);
        var region = w.RegionsById[a.Region];
        if (MathF.Abs(delta) < .12f && MathF.Abs(distance - ring) < 10) return new("flank", a.Position);
        float step = Math.Clamp(delta, -.55f, .55f);
        foreach (float turn in new[] { step, step * .5f, 0f })
        {
            var point = target.Position + new Vector2(MathF.Cos(now + turn), MathF.Sin(now + turn)) * ring;
            if (Vector2.DistanceSquared(point, a.Position) > 16 && region.CanTravel(a.Position, point, a)) return new("flank", point, Pace: .8f);
        }
        return new("flank", a.Position);
    }

    // The primary attack is off cooldown, or will be by the time the actor arrives.
    static bool StrikeReady(World w, ActorState a, CombatProfileDef profile)
    {
        string? primary = null; float top = float.NegativeInfinity;
        foreach (var (id, weight) in profile.Priorities)
            if (a.Abilities.Contains(id) && (weight > top || (weight == top && StringComparer.Ordinal.Compare(id, primary) < 0))) { primary = id; top = weight; }
        return primary != null && a.Cooldowns.GetValueOrDefault(primary) <= .35f;
    }

    // Strafing comes in bouts, phased per actor so a group does not sway in step.
    static bool Strafing(World w, ActorState a, CombatProfileDef profile)
    {
        if (profile.Reposition <= 0) return false;
        const double bout = 3.2;
        double phase = (w.Time + (uint)World.StableId(a.Id) % 100 * bout / 100) / bout;
        return phase - Math.Floor(phase) < profile.Reposition;
    }

    static Vector2 Orbit(World w, ActorState a, ActorState target, Vector2 toward, float radius, CombatProfileDef profile)
    {
        // Direction holds for several seconds, then may reverse.
        int key = World.StableId(a.Id);
        float sign = EnemyMind.Hash01(key, (long)Math.Floor((w.Time + (uint)key % 7) / 4.5), 9) < .5f ? 1 : -1;
        float arc = Math.Clamp(Math.Max(0, w.Stat(a, "speed")) * .6f * profile.DecisionSeconds * 1.6f / Math.Max(12, radius), .2f, .7f);
        var region = w.RegionsById[a.Region];
        foreach (float turn in new[] { sign * arc, -sign * arc })
        {
            float c = MathF.Cos(turn), s = MathF.Sin(turn);
            var point = target.Position + new Vector2(-toward.X * c + toward.Y * s, -toward.X * s - toward.Y * c) * radius;
            if (region.CanTravel(a.Position, point, a)) return point;
        }
        return a.Position;
    }

    // Routed actors make for the nearest ally that is not right beside them; alone, straight away.
    static Vector2 Rout(World w, ActorState a, ActorState target, Vector2 toward)
    {
        ActorState? ally = null; float nearest = float.PositiveInfinity;
        foreach (var c in w.Actors.Values)
        {
            if (c.Id == a.Id || !c.Alive || c.Region != a.Region || c.Faction != a.Faction || w.Hostile(a, c)) continue;
            float d = Vector2.DistanceSquared(a.Position, c.Position);
            if (d < 60 * 60 || d > 260 * 260 || Vector2.Dot(c.Position - a.Position, toward) > 0) continue;
            if (d < nearest || (d == nearest && ally != null && StringComparer.Ordinal.Compare(c.Id, ally.Id) < 0)) { ally = c; nearest = d; }
        }
        return ally != null && CombatGeometry.TryNormalize(ally.Position - a.Position, out var to) ? to : -toward;
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
