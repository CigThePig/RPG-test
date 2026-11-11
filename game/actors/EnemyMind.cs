using System.Numerics;

namespace Mosslight;

/// <summary>
/// Perception and the calm half of an actor's life: noticing, raising the alarm, hunting a
/// lost quarry, giving up and going home. Everything is derived from saved actor state and
/// the world clock, so a reloaded save or a replay makes the same decisions.
/// <see cref="CombatBrain"/> owns tactics once a quarry is engaged.
/// </summary>
public static class EnemyMind
{
    /// <summary>Stable 0..1 value for scheduling; never a random stream.</summary>
    public static float Hash01(int a, long b, int c)
    {
        unchecked
        {
            uint x = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2246822519u;
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }

    static Vector2 Unit(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

    static void FaceToward(ActorState a, Vector2 point)
    {
        if (CombatGeometry.TryNormalize(point - a.Position, out var d)) { a.FacingX = d.X; a.FacingY = d.Y; }
    }

    static void Set(World w, ActorState a, Awareness next, string cause)
    {
        if (a.Awareness == next) return;
        a.Awareness = next;
        w.Present(CombatPresentationKind.AwarenessChanged, a, a.Target, outcome: cause);
        w.Diagnose("ai_awareness", a.Position, a.Id + "/" + next + "/" + cause);
    }

    /// <summary>Forget everything without ceremony: used when the player is far away.</summary>
    public static void Calm(ActorState a)
    {
        a.Awareness = Awareness.Unaware;
        a.Alertness = a.AwarenessRemaining = 0;
        a.Target = "";
        a.Called = false;
        a.TacticalIntent = "";
    }

    /// <summary>Give an actor a new post: where it stands, where it returns to and which way it looks.</summary>
    public static void Resettle(ActorState a, Vector2 at, Vector2 facing)
    {
        Calm(a);
        a.X = a.HomeX = at.X; a.Y = a.HomeY = at.Y;
        a.MotionX = a.MotionY = 0;
        a.NextTacticalTime = 0;
        a.Cooldowns.Clear();
        if (CombatGeometry.TryNormalize(facing, out var d)) { a.FacingX = d.X; a.FacingY = d.Y; }
        else a.FacingX = a.FacingY = 0;
    }

    /// <summary>Can this actor notice that one right now, given its facing, range and sightline?</summary>
    public static bool Notices(World w, ActorState a, ActorState b, CombatProfileDef p)
    {
        if (!b.Alive || b.Region != a.Region || !w.Hostile(a, b)) return false;
        float distance = Vector2.Distance(a.Position, b.Position);
        if (distance > p.Sight) return false;
        bool facing = CombatGeometry.TryNormalize(a.Facing, out var forward);
        // Close movement is heard from any side; otherwise the quarry must be in the view cone.
        if (facing && distance > p.HearingDistance && p.SightHalfAngle < 180 &&
            !CombatGeometry.InCone(a.Position, b.Position, forward, p.Sight, p.SightHalfAngle)) return false;
        return w.LineOfSight(a, b);
    }

    static ActorState? Spot(World w, ActorState a, CombatProfileDef p)
    {
        ActorState? best = null;
        float nearest = float.PositiveInfinity;
        foreach (var b in w.Actors.Values)
        {
            if (b.Id == a.Id || !Notices(w, a, b, p)) continue;
            float d = Vector2.DistanceSquared(a.Position, b.Position);
            if (d < nearest || (d == nearest && best != null && StringComparer.Ordinal.Compare(b.Id, best.Id) < 0)) { best = b; nearest = d; }
        }
        return best;
    }

    static bool WithinLeash(ActorState a, Vector2 point, CombatProfileDef p) => Vector2.Distance(point, a.Home) <= p.LeashDistance;

    /// <summary>The alarm beat: a short startled pause, and a call that brings nearby allies.</summary>
    public static void Alarm(World w, ActorState a, ActorState quarry, CombatProfileDef p, bool called)
    {
        a.Target = quarry.Id;
        a.Alertness = 1;
        a.Called = called;
        // Called allies answer after a short, staggered delay so a pack does not turn as one.
        a.AwarenessRemaining = called ? .15f + ((uint)World.StableId(a.Id) % 5) * .06f : p.NoticeSeconds;
        a.LastKnownX = quarry.X; a.LastKnownY = quarry.Y; a.LastSeenTime = w.Time;
        a.Knowledge[quarry.Id] = "seen";
        a.MotionX = a.MotionY = 0;
        a.Cooldowns.Remove("leash");
        FaceToward(a, quarry.Position);
        a.Awareness = Awareness.Alerted;
        a.TacticalIntent = "alert";
        w.Present(CombatPresentationKind.Alerted, a, quarry.Id, outcome: called ? "called" : "sighted");
        w.Diagnose("ai_awareness", a.Position, a.Id + "/Alerted/" + (called ? "called" : "sighted"));
        if (!called) Call(w, a, quarry, p);
    }

    /// <summary>Allies in earshot join the hunt. Those brought by a call do not call in turn.</summary>
    static void Call(World w, ActorState a, ActorState quarry, CombatProfileDef p)
    {
        if (p.CallRadius <= 0) return;
        foreach (var c in w.Actors.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (c.Id == a.Id || c.Id == "player" || !c.Alive || c.Region != a.Region || c.Faction != a.Faction || w.Hostile(a, c) ||
                c.Awareness is Awareness.Alerted or Awareness.Engaged || !w.Hostile(c, quarry)) continue;
            float distance = Vector2.Distance(a.Position, c.Position);
            if (distance > p.CallRadius || (distance > 72 && !w.LineOfSight(a, c))) continue;
            var theirs = w.FindProfile(c.CombatProfile) ?? CombatProfileDef.Legacy;
            if (!WithinLeash(c, quarry.Position, theirs)) continue;
            Alarm(w, c, quarry, theirs, called: true);
        }
    }

    /// <summary>Being struck settles the question of where the enemy is.</summary>
    public static void Hurt(World w, ActorState victim, ActorState source)
    {
        if (victim.Id == "player" || source.Region != victim.Region) return;
        var p = w.FindProfile(victim.CombatProfile) ?? CombatProfileDef.Legacy;
        victim.Cooldowns.Remove("leash");
        if (victim.Awareness == Awareness.Engaged)
        {
            if (victim.Target == source.Id) { victim.LastKnownX = source.X; victim.LastKnownY = source.Y; victim.LastSeenTime = w.Time; }
            return;
        }
        bool calm = victim.Awareness != Awareness.Alerted;
        victim.Target = source.Id;
        victim.Alertness = 1;
        victim.AwarenessRemaining = 0;
        victim.LastKnownX = source.X; victim.LastKnownY = source.Y; victim.LastSeenTime = w.Time;
        FaceToward(victim, source.Position);
        if (victim.Alive)
        {
            victim.Awareness = Awareness.Engaged;
            w.Present(CombatPresentationKind.Alerted, victim, source.Id, outcome: "hurt");
            w.Diagnose("ai_awareness", victim.Position, victim.Id + "/Engaged/hurt");
        }
        // A cry of pain carries as far as a warning, even a dying one.
        if (calm) Call(w, victim, source, p);
    }

    static void BeginSearch(World w, ActorState a, CombatProfileDef p)
    {
        a.AwarenessRemaining = p.SearchSeconds;
        Set(w, a, Awareness.Searching, "lost");
    }

    static void BeginReturn(World w, ActorState a, string cause)
    {
        a.Target = "";
        a.Alertness = a.AwarenessRemaining = 0;
        // A moment of resolve, so a leashed actor does not turn straight back.
        a.Cooldowns["leash"] = 1.5f;
        Set(w, a, Awareness.Returning, cause);
    }

    /// <summary>Advance what the actor knows. Returns the quarry while engaged, otherwise null.</summary>
    static ActorState? Perceive(World w, ActorState a, CombatProfileDef p, float dt)
    {
        ActorState? quarry = a.Awareness is Awareness.Engaged or Awareness.Alerted && w.Actors.TryGetValue(a.Target, out var t) &&
            t.Alive && t.Region == a.Region && w.Hostile(a, t) ? t : null;
        if (a.Awareness == Awareness.Alerted)
        {
            if (quarry == null) { BeginReturn(w, a, "gone"); return null; }
            if (w.LineOfSight(a, quarry)) { a.LastKnownX = quarry.X; a.LastKnownY = quarry.Y; a.LastSeenTime = w.Time; }
            a.AwarenessRemaining -= dt;
            if (a.AwarenessRemaining > 0) return null;
            a.AwarenessRemaining = 0;
            Set(w, a, Awareness.Engaged, "engage");
        }
        if (a.Awareness == Awareness.Engaged)
        {
            if (quarry == null)
            {
                // The quarry fell or left; another in plain sight takes its place.
                var next = Spot(w, a, p);
                if (next == null) { BeginReturn(w, a, "gone"); return null; }
                a.Target = next.Id; quarry = next;
            }
            bool sees = Vector2.Distance(a.Position, quarry.Position) <= p.PursuitDistance && w.LineOfSight(a, quarry);
            // For a moment after losing sight the pursuer still reads where the quarry went.
            if (sees) a.LastSeenTime = w.Time;
            if (sees || w.Time - a.LastSeenTime <= 1.0) { a.LastKnownX = quarry.X; a.LastKnownY = quarry.Y; }
            if (!WithinLeash(a, a.Position, p) && !WithinLeash(a, quarry.Position, p)) { BeginReturn(w, a, "leash"); return null; }
            if (!sees && w.Time - a.LastSeenTime > p.MemorySeconds) { BeginSearch(w, a, p); return null; }
            return quarry;
        }

        var seen = a.Cooldowns.GetValueOrDefault("leash") > 0 ? null : Spot(w, a, p);
        if (seen != null && WithinLeash(a, seen.Position, p))
        {
            float distance = Vector2.Distance(a.Position, seen.Position);
            // Recognition is near instant up close and takes about a second at the edge of sight.
            float needed = distance <= p.HearingDistance ? .12f : .12f + .78f * Math.Clamp(distance / Math.Max(1, p.Sight), 0, 1);
            if (a.Awareness is Awareness.Searching or Awareness.Returning) needed *= .5f;
            a.Alertness = Math.Min(1, a.Alertness + dt / needed);
            a.LastKnownX = seen.X; a.LastKnownY = seen.Y;
            if (a.Alertness >= 1) { Alarm(w, a, seen, p, called: false); return null; }
            if (a.Awareness == Awareness.Unaware) Set(w, a, Awareness.Suspicious, "glimpse");
        }
        else
        {
            a.Alertness = Math.Max(0, a.Alertness - dt / 2.5f);
            if (a.Awareness == Awareness.Suspicious && a.Alertness <= 0) Set(w, a, Awareness.Unaware, "calm");
        }
        if (a.Awareness == Awareness.Searching)
        {
            a.AwarenessRemaining -= dt;
            if (a.AwarenessRemaining <= 0) BeginReturn(w, a, "gave up");
        }
        return null;
    }

    public static void Think(World w, ActorState a, CombatProfileDef? authored, float step)
    {
        var p = authored ?? CombatProfileDef.Legacy;
        var quarry = Perceive(w, a, p, step);
        switch (a.Awareness)
        {
            case Awareness.Engaged when quarry != null: Fight(w, a, quarry, authored, p, step); break;
            case Awareness.Alerted:
                a.TacticalIntent = "alert";
                FaceToward(a, a.LastKnown);
                break;
            case Awareness.Suspicious:
                a.TacticalIntent = "investigate";
                FaceToward(a, a.LastKnown);
                // A long look first; only a growing suspicion draws it closer.
                if (!p.Ambusher && a.Alertness > .45f && Vector2.Distance(a.Position, a.LastKnown) > 36) w.Follow(a, a.LastKnown, step, .4f);
                break;
            case Awareness.Searching: Search(w, a, p, step); break;
            case Awareness.Returning: Return(w, a, step); break;
            default: Idle(w, a, p, step); break;
        }
    }

    static void Fight(World w, ActorState a, ActorState quarry, CombatProfileDef? authored, CombatProfileDef p, float step)
    {
        a.Knowledge[quarry.Id] = "seen";
        a.Target = quarry.Id;
        if (!w.LineOfSight(a, quarry))
        {
            // Hunting on memory: go where it was last seen, then look around from there.
            a.TacticalIntent = "pursue";
            w.Diagnose("ai_intent", a.Position, a.Id + "/" + a.CombatProfile + "/pursue/");
            if (Vector2.Distance(a.Position, a.LastKnown) < 14) { BeginSearch(w, a, p); return; }
            w.Follow(a, a.LastKnown, step);
            return;
        }
        // Low health shakes its nerve for a few seconds at a time, not for the rest of the fight.
        bool hurt = a.Health < a.MaxHealth * p.RetreatThreshold;
        if (hurt && a.Cooldowns.GetValueOrDefault("nerve") <= 0) a.Cooldowns["nerve"] = 9;
        bool shaken = hurt && a.Cooldowns.GetValueOrDefault("nerve") > 6;
        bool retreat = w.Plan(shaken ? "safe" : "defeated", true, shaken ? "retreat" : "attack") == "retreat";
        var intent = CombatBrain.Select(w, a, quarry, authored, retreat);
        a.TacticalIntent = intent.Kind;
        w.Diagnose("ai_intent", a.Position, a.Id + "/" + a.CombatProfile + "/" + intent.Kind + "/" + intent.Ability);
        switch (intent.Kind)
        {
            case "ability":
                w.Activate(a.Id, intent.Ability, w.FindAbility(intent.Ability)?.Self == true ? a.Id : quarry.Id);
                break;
            case "approach":
                w.Follow(a, intent.Destination, step, intent.Pace);
                if (a.MotionX == 0 && a.MotionY == 0) w.Follow(a, quarry.Position, step, intent.Pace);
                break;
            case "circle" or "flank" or "retreat" or "withdraw":
                w.Follow(a, intent.Destination, step, intent.Pace);
                break;
        }
    }

    static void Search(World w, ActorState a, CombatProfileDef p, float step)
    {
        a.TacticalIntent = "search";
        float elapsed = p.SearchSeconds - a.AwarenessRemaining;
        int leg = (int)(elapsed / 1.7f);
        int key = World.StableId(a.Id);
        var probe = leg == 0 ? a.LastKnown : a.LastKnown + Unit(Hash01(key, leg, 5) * MathF.Tau) * 52;
        var region = w.RegionsById[a.Region];
        if (Vector2.Distance(a.Position, probe) > 10 && (leg == 0 || region.CanTravel(a.Position, probe, a)))
        {
            w.Follow(a, probe, step, .6f);
            if (a.MotionX != 0 || a.MotionY != 0) return;
        }
        // Standing at a probe point: sweep the head from side to side.
        var sweep = Unit(Hash01(key, (long)(elapsed / .8f), 6) * MathF.Tau);
        a.FacingX = sweep.X; a.FacingY = sweep.Y;
    }

    static void Return(World w, ActorState a, float step)
    {
        a.TacticalIntent = "return";
        a.Health = Math.Min(a.MaxHealth, a.Health + a.MaxHealth * CombatTuning.ReturnHealing * step);
        if (Vector2.Distance(a.Position, a.Home) > 10)
        {
            w.Follow(a, a.Home, step, .7f);
            if (a.MotionX != 0 || a.MotionY != 0) return;
        }
        // Home, or as near as the way allows.
        a.Called = false;
        a.Alertness = 0;
        Set(w, a, Awareness.Unaware, "home");
    }

    static void Idle(World w, ActorState a, CombatProfileDef p, float step)
    {
        if (p.Ambusher)
        {
            // Dormant on its spot; the view shows it folded down.
            a.Activity = "ambush";
            a.TacticalIntent = "lurk";
            return;
        }
        string action = w.Plan(a.Activity, false, a.Activity);
        a.TacticalIntent = "";
        if (action == "rest") { w.Rest(a, step); return; }
        if (action != "patrol") return;
        Roam(w, a, p, step);
    }

    /// <summary>
    /// Unhurried wandering in legs: walk to a spot near home, stand and look about, repeat.
    /// A function of the clock and the actor's identity, so nothing extra is saved.
    /// </summary>
    static void Roam(World w, ActorState a, CombatProfileDef p, float step)
    {
        int key = World.StableId(a.Id);
        double period = 4.5 + (uint)key % 4 * .8;
        double clock = w.Time + (uint)key % 97 * .37;
        long leg = (long)Math.Floor(clock / period);
        double phase = clock / period - leg;
        var region = w.RegionsById[a.Region];
        if (p.RoamDistance > 0 && phase < .6)
        {
            float radius = p.RoamDistance * (.35f + .65f * Hash01(key, leg, 2));
            var spoke = Unit(Hash01(key, leg, 1) * MathF.Tau);
            var destination = a.Home + new Vector2(spoke.X, spoke.Y * .7f) * radius;
            if (Vector2.Distance(a.Position, destination) > 6 && region.CanTravel(a.Home, destination, a))
            {
                w.Follow(a, destination, step, .45f);
                if (a.MotionX != 0 || a.MotionY != 0) { a.TacticalIntent = "roam"; return; }
            }
        }
        // Between legs it glances around, which is also what lets a careful approach from behind work.
        var glance = Unit(Hash01(key, (long)Math.Floor(clock / 2.6), 3) * MathF.Tau);
        a.FacingX = glance.X; a.FacingY = glance.Y;
    }

    /// <summary>Authoritative facing, every tick. Fighters keep their eyes on the quarry while they circle or back away.</summary>
    public static void Face(World w, ActorState a)
    {
        if (!a.Alive) return;
        if (a.State is ActionState.Windup or ActionState.Recovery or ActionState.Dodging or ActionState.Staggered)
        {
            if (a.State is ActionState.Windup or ActionState.Recovery && (a.ActionDirectionX != 0 || a.ActionDirectionY != 0))
            { a.FacingX = a.ActionDirectionX; a.FacingY = a.ActionDirectionY; }
            else if (a.State != ActionState.Dodging && w.Actors.TryGetValue(a.Target, out var struck) && struck.Region == a.Region) FaceToward(a, struck.Position);
            return;
        }
        bool watching = a.Awareness is Awareness.Alerted or Awareness.Suspicious ||
            (a.Awareness == Awareness.Engaged && a.TacticalIntent is "hold" or "circle" or "flank" or "withdraw" or "ability" or "wait" or "alert" or "retreat");
        if (watching)
        {
            var point = a.Awareness == Awareness.Engaged && w.Actors.TryGetValue(a.Target, out var quarry) && quarry.Region == a.Region ? quarry.Position : a.LastKnown;
            // A routed actor turns and runs; one merely giving ground keeps facing the threat.
            if (a.TacticalIntent != "retreat" || Vector2.Distance(a.Position, point) < 120) { FaceToward(a, point); return; }
        }
        if (a.MotionX * a.MotionX + a.MotionY * a.MotionY > 1 && CombatGeometry.TryNormalize(new Vector2(a.MotionX, a.MotionY), out var heading))
        { a.FacingX = heading.X; a.FacingY = heading.Y; }
    }
}
