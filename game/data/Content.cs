using System.Text.Json;

namespace Mosslight;
public sealed class AbilityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "physical";
    public string Status { get; set; } = "";
    public float Range { get; set; }
    public float Damage { get; set; }
    public float Cost { get; set; }
    public float Cooldown { get; set; }
    public float Windup { get; set; }
    public float Recovery { get; set; }
    public ProjectileDef? Projectile { get; set; }
    public float ProjectileSpeed { get; set; }
    public int UnlockLevel { get; set; } = 1;
    public int UnlockCost { get; set; } = 1;
    public bool Self { get; set; }
    public bool RequiresWeapon { get; set; }
    // Test fixtures are excluded from normal content, including progression UI.
    public bool VerificationOnly { get; set; }
    public bool PlayerLearnable { get; set; } = true;
    // Omitted geometry preserves the original target-locked SPELL behavior.
    public AttackGeometryDef? Geometry { get; set; }
    public TelegraphDef? Telegraph { get; set; }
    // Optional PPC action; an omitted or anatomy-incompatible style uses the rig default.
    public string ActionStyle { get; set; } = "";
    // The tactician does not start this closer than MinRange (gap closers, charges).
    public float MinRange { get; set; }
    // Ground pixels a direct hit pushes its victim.
    public float Knockback { get; set; } = 15;
    // Committed movement along the locked action direction during the windup.
    public LungeDef? Lunge { get; set; }
    // A "line" attack: after the windup the actor runs down its committed lane.
    public ChargeDef? Charge { get; set; }
    // Self abilities with a status also grant it to allies this close.
    public float AllyRadius { get; set; }
    // A "blast" attack: lobbed at the ground where the target stands when it is thrown.
    public BlastDef? Blast { get; set; }
    // What a blast leaves behind on the ground.
    public PoolDef? Pool { get; set; }
}

public sealed class BlastDef
{
    public float Radius { get; set; }
    // Seconds in the air after the windup, during which the landing spot is still warned.
    public float Delay { get; set; } = .6f;
}

public sealed class PoolDef
{
    public float Duration { get; set; }
    public float Interval { get; set; } = .5f;
    // Periodic damage to hostiles standing in it; never interrupts.
    public float Damage { get; set; }
    public string Status { get; set; } = "";
}

public sealed class LungeDef
{
    public float Distance { get; set; }
    // Seconds from the start of the action.
    public float Start { get; set; }
    public float End { get; set; }
}

public sealed class ChargeDef
{
    public float Distance { get; set; }
    public float Speed { get; set; }
    // Extra contact margin beyond the two body radii.
    public float Reach { get; set; } = 6;
    // Seconds stunned after running into terrain without hitting anything.
    public float CrashStagger { get; set; } = 1;
    public float Duration => Speed > 0 ? Distance / Speed : 0;
}

public sealed class ProjectileDef
{
    public string Mode { get; set; } = "legacy";
    public float TurnDegrees { get; set; } = 60;
    public float Lifetime { get; set; } = 4;
    public float Radius { get; set; } = 2;
    public string Visual { get; set; } = "ember";
    public bool InterceptHostiles { get; set; }
    // A volley fans Count shots evenly across SpreadDegrees.
    public int Count { get; set; } = 1;
    public float SpreadDegrees { get; set; }
}

public sealed class CombatProfileDef
{
    public string Id { get; set; } = "";
    public float MinimumDistance { get; set; }
    public float PreferredDistance { get; set; } = 28;
    public float ApproachDistance { get; set; } = 38;
    public float PursuitDistance { get; set; } = 180;
    public float RetreatThreshold { get; set; } = .2f;
    public float DecisionSeconds { get; set; } = .2f;
    public float Reposition { get; set; }
    public string Behaviour { get; set; } = "melee";
    public Dictionary<string, float> Priorities { get; set; } = new();
    // Perception. Zero sight falls back to PursuitDistance.
    public float SightDistance { get; set; }
    public float SightHalfAngle { get; set; } = 105;
    // Close enough to be noticed from any side.
    public float HearingDistance { get; set; } = 60;
    // The alarm beat between noticing and acting.
    public float NoticeSeconds { get; set; } = .45f;
    public float CallRadius { get; set; } = 150;
    // How long a lost quarry is still chased, then how long the area is searched.
    public float MemorySeconds { get; set; } = 3.5f;
    public float SearchSeconds { get; set; } = 5;
    public float LeashDistance { get; set; } = 420;
    // Idle wandering radius around home; zero stands guard.
    public float RoamDistance { get; set; } = 64;
    // Lies still until something comes close.
    public bool Ambusher { get; set; }
    // Guard that absorbs this much direct damage before a hit staggers. Zero always staggers.
    public float Poise { get; set; }
    // Chance to hop clear when the quarry starts an attack, and how far.
    public float EvadeChance { get; set; }
    public float EvadeDistance { get; set; } = 44;
    public float EvadeCooldown { get; set; } = 4;
    // Self ability used once health falls below this fraction.
    public float EnrageThreshold { get; set; }
    public string EnrageAbility { get; set; } = "";
    // Self ability with AllyRadius used to embolden nearby allies, or to mend them if its status heals.
    public string RallyAbility { get; set; } = "";
    // A hard front: direct hits arriving within this many degrees of its facing are turned.
    // Open while it recovers from its own attack, while staggered, and before it has noticed.
    public float ShieldArc { get; set; }
    // Fraction of a turned hit's damage that still gets through.
    public float ShieldFactor { get; set; } = .25f;
    // Degrees per second it can turn while fighting; zero turns at once.
    public float TurnDegrees { get; set; }
    // Its fall breaks the nerve of every follower that sees it.
    public bool Leader { get; set; }
    // Chance to hold its nerve when an ally falls in view. One never breaks.
    public float Morale { get; set; } = 1;

    public float Sight => SightDistance > 0 ? SightDistance : PursuitDistance;
    /// <summary>Every ability an actor of this kind carries, in stable order.</summary>
    public List<string> Loadout() => Priorities.Keys.Concat(new[] { EnrageAbility, RallyAbility }.Where(id => id != "")).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    /// <summary>Tactics for actors saved before profiles existed, or with none authored.</summary>
    public static readonly CombatProfileDef Legacy = new() { Id = "" };
}

public sealed class AttackGeometryDef
{
    public string Shape { get; set; } = "target";
    public float Range { get; set; }
    public float HalfAngleDegrees { get; set; }
}

public sealed class TelegraphDef
{
    public string Style { get; set; } = "none";
}

public sealed class StatusDef
{
    public string ModifierStat { get; set; } = "";
    public float Modifier { get; set; }
    public string Id { get; set; } = "";
    public float Duration { get; set; }
    public float Interval { get; set; }
    public float Damage { get; set; }
    public int MaxStacks { get; set; }
    public string Type { get; set; } = "physical";
    public bool BlocksActions { get; set; }
    // Further stat changes beside ModifierStat, per stack.
    public Dictionary<string, float> Modifiers { get; set; } = new();
    // Health restored each interval, per stack.
    public float Heal { get; set; }
}

public sealed class RecipeDef
{
    public string Material { get; set; } = "";
    public int Quality { get; set; } = 1;
    public string Id { get; set; } = "";
    public Dictionary<string, int> Ingredients { get; set; } = new();
    public string Output { get; set; } = "";
    public int Count { get; set; }
}

public sealed class LootDef
{
    public string Item { get; set; } = "";
    public int Min { get; set; }
    public int Max { get; set; }
    public double Chance { get; set; }
}

public sealed class ScheduleEntry
{
    public int Hour { get; set; }
    public string Activity { get; set; } = "";
}

public sealed class NpcProfile
{
    public string Id { get; set; } = "";
    public string Family { get; set; } = "";
    public string Faction { get; set; } = "";
    public string Profession { get; set; } = "";
    public List<ScheduleEntry> Schedule { get; set; } = new();
}

public sealed class Content
{
    public List<CombatProfileDef> CombatProfiles { get; set; } = new();
    public List<SkillDef> Skills { get; set; } = new();
    public List<AbilityDef> Abilities { get; set; } = new();
    public List<StatusDef> Statuses { get; set; } = new();
    public List<RecipeDef> Recipes { get; set; } = new();
    public List<LootDef> Loot { get; set; } = new();
    public List<NpcProfile> NpcProfiles { get; set; } = new();
    public Dictionary<string, int> Factions { get; set; } = new();

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    public static Content Load(bool includeVerification = false)
    {
        var content = JsonSerializer.Deserialize<Content>(Godot.FileAccess.GetFileAsString("res://game/data/foundation.json"), Json)!;
        if (!includeVerification) content.Abilities.RemoveAll(a => a.VerificationOnly);
        return content;
    }
    public List<string> Validate(HashSet<string> items)
    {
        var errors = new List<string>();
        void Unique(IEnumerable<string> ids, string domain)
        {
            foreach (var g in ids.GroupBy(x => x))
                if (string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1)
                    errors.Add($"{domain}: invalid/duplicate id '{g.Key}'");
        }

        Unique(CombatProfiles.Select(x => x.Id), "combat profile");
        foreach (var p in CombatProfiles)
        {
            var values = new[] { p.MinimumDistance, p.PreferredDistance, p.ApproachDistance, p.PursuitDistance, p.RetreatThreshold, p.DecisionSeconds, p.Reposition };
            if (values.Any(v => !float.IsFinite(v) || v < 0) || p.MinimumDistance > p.PreferredDistance || p.PreferredDistance > p.ApproachDistance || p.ApproachDistance > p.PursuitDistance || p.PursuitDistance > 360 || p.RetreatThreshold > 1 || p.DecisionSeconds < .2f || p.DecisionSeconds > 2 || p.Reposition > 1 || p.Behaviour is not ("melee" or "ranged" or "skirmish" or "support") || p.Priorities.Count == 0 || p.Priorities.Any(x => !Abilities.Any(a => a.Id == x.Key && !a.Self) || !float.IsFinite(x.Value) || x.Value <= 0))
                errors.Add($"combat profile {p.Id}: invalid distances, cadence, behaviour or abilities");
            var senses = new[] { p.SightDistance, p.SightHalfAngle, p.HearingDistance, p.NoticeSeconds, p.CallRadius, p.MemorySeconds, p.SearchSeconds, p.LeashDistance, p.RoamDistance, p.Poise, p.EvadeChance, p.EvadeDistance, p.EvadeCooldown, p.EnrageThreshold };
            if (senses.Any(v => !float.IsFinite(v) || v < 0) || p.SightDistance > 360 || p.SightHalfAngle > 180 || p.HearingDistance > p.Sight || p.NoticeSeconds > 2 ||
                p.CallRadius > 360 || p.MemorySeconds > 30 || p.SearchSeconds > 30 || p.LeashDistance < p.PursuitDistance || p.RoamDistance > 200 || p.EvadeChance > 1 || p.EvadeDistance > 90 ||
                (p.EvadeChance > 0 && p.EvadeCooldown < 1) || p.EnrageThreshold >= 1)
                errors.Add($"combat profile {p.Id}: invalid senses, leash, guard or evasion");
            var temper = new[] { p.ShieldArc, p.ShieldFactor, p.TurnDegrees, p.Morale };
            if (temper.Any(v => !float.IsFinite(v) || v < 0) || p.ShieldArc > 150 || p.ShieldFactor > 1 || p.TurnDegrees > 720 || p.Morale > 1 ||
                (p.ShieldArc > 0 && p.TurnDegrees <= 0) || (p.Leader && p.Morale < 1))
                errors.Add($"combat profile {p.Id}: invalid shield, turning or morale");
            foreach (string own in new[] { p.EnrageAbility, p.RallyAbility })
                if (own != "" && !Abilities.Any(a => a.Id == own && a.Self && a.Status != ""))
                    errors.Add($"combat profile {p.Id}: '{own}' is not a self ability that grants a status");
            if ((p.EnrageThreshold > 0) != (p.EnrageAbility != "") || (p.RallyAbility != "" && Abilities.Any(a => a.Id == p.RallyAbility && a.AllyRadius <= 0)))
                errors.Add($"combat profile {p.Id}: enrage or rally is only half authored");
        }
        Unique(Skills.Select(x => x.Id), "skill");
        foreach (var skill in Skills)
            if (skill.MaxRank < 1 || !float.IsFinite(skill.PerRank))
                errors.Add($"skill {skill.Id}: invalid rank/modifier");
        Unique(Abilities.Select(x => x.Id), "ability");
        Unique(Statuses.Select(x => x.Id), "status");
        Unique(Recipes.Select(x => x.Id), "recipe");
        Unique(NpcProfiles.Select(x => x.Id), "npc");
        foreach (var a in Abilities)
        {
            static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0;
            if (!Nonnegative(a.Range) || !Nonnegative(a.Cost) || !Nonnegative(a.Cooldown) ||
                !Nonnegative(a.Windup) || !Nonnegative(a.Recovery) || !Nonnegative(a.ProjectileSpeed) ||
                !float.IsFinite(a.Damage) || (!string.IsNullOrEmpty(a.Status) && !Statuses.Any(s => s.Id == a.Status)))
                errors.Add($"ability {a.Id}: invalid timing, cost, damage or status reference");
            if (!Nonnegative(a.MinRange) || a.MinRange > a.Range || !Nonnegative(a.Knockback) || a.Knockback > 80 || !Nonnegative(a.AllyRadius) || a.AllyRadius > 360 || (a.AllyRadius > 0 && (!a.Self || a.Status == "")))
                errors.Add($"ability {a.Id}: invalid minimum range, knockback or ally radius");
            if (a.Lunge is { } lunge && (!Nonnegative(lunge.Distance) || lunge.Distance <= 0 || lunge.Distance > 120 || !Nonnegative(lunge.Start) || !(lunge.End > lunge.Start) ||
                lunge.End > a.Windup + a.Recovery || a.Self || a.ProjectileSpeed > 0 || a.Charge != null || a.Id == "slash"))
                errors.Add($"ability {a.Id}: invalid lunge");
            if (a.Charge is { } charge && (!Nonnegative(charge.Distance) || charge.Distance <= 0 || charge.Distance > 320 || !Nonnegative(charge.Speed) || charge.Speed <= 0 || charge.Speed > 600 ||
                !Nonnegative(charge.Reach) || charge.Reach > 24 || !Nonnegative(charge.CrashStagger) || charge.CrashStagger > 3 || charge.Duration > a.Recovery || a.Geometry?.Shape != "line"))
                errors.Add($"ability {a.Id}: invalid charge");
            if (a.Projectile is { } volley && (volley.Count < 1 || volley.Count > 7 || !Nonnegative(volley.SpreadDegrees) || volley.SpreadDegrees > 120 || (volley.Count == 1 && volley.SpreadDegrees != 0)))
                errors.Add($"ability {a.Id}: invalid volley");
            if (a.Projectile is { } p && (a.ProjectileSpeed <= 0 || p.Mode is not ("legacy" or "straight" or "bounded_homing") || !Nonnegative(p.TurnDegrees) || p.TurnDegrees > 720 || !Nonnegative(p.Radius) || p.Radius > 12 || !Nonnegative(p.Lifetime) || p.Lifetime <= 0 || p.Lifetime > 30 || p.Visual is not ("ember" or "spit")))
                errors.Add($"ability {a.Id}: invalid projectile configuration");
            string? shape = a.Geometry == null ? "target" : a.Geometry.Shape;
            string? style = a.Telegraph == null ? "none" : a.Telegraph.Style;
            if (shape is not ("target" or "cone" or "line" or "blast" or "burst"))
                errors.Add($"ability {a.Id}: unsupported attack shape '{shape}'");
            if (a.Geometry != null && (!Nonnegative(a.Geometry.Range) || !Nonnegative(a.Geometry.HalfAngleDegrees) ||
                a.Geometry.HalfAngleDegrees > 180 || (shape == "cone" && (a.Geometry.Range <= 0 || a.Geometry.HalfAngleDegrees <= 0)) || (shape == "line" && a.Geometry.Range <= 0) ||
                (shape is "blast" or "burst" && (a.Geometry.Range <= 0 || a.Geometry.Range > 120 || a.Geometry.HalfAngleDegrees != 0)) ||
                (shape == "target" && (a.Geometry.Range != 0 || a.Geometry.HalfAngleDegrees != 0))))
                errors.Add($"ability {a.Id}: invalid attack geometry");
            if (shape is "cone" or "line" or "blast" or "burst" && (a.Self || a.ProjectileSpeed > 0 || a.Damage <= 0 || a.Id == "slash"))
                errors.Add($"ability {a.Id}: cone requires a direct hostile damaging ability");
            if ((shape == "blast") != (a.Blast != null) || (a.Blast is { } blast && (!Nonnegative(blast.Radius) || blast.Radius != a.Geometry!.Range || !Nonnegative(blast.Delay) || blast.Delay > 3 || a.Lunge != null || a.Charge != null)))
                errors.Add($"ability {a.Id}: a blast attack needs a blast whose radius is its geometry range");
            if (a.Pool is { } pool && (a.Blast == null || !Nonnegative(pool.Duration) || pool.Duration <= 0 || pool.Duration > 20 || !Nonnegative(pool.Interval) || pool.Interval < .2f ||
                !Nonnegative(pool.Damage) || (pool.Damage <= 0 && pool.Status == "") || (pool.Status != "" && !Statuses.Any(s => s.Id == pool.Status))))
                errors.Add($"ability {a.Id}: invalid pool");
            if (shape == "burst" && (a.Lunge != null || a.Charge != null || a.Geometry!.Range < a.Range * .5f))
                errors.Add($"ability {a.Id}: invalid burst");
            if (shape == "line" && (a.Charge == null || a.Geometry!.Range != a.Charge.Distance || a.Geometry.HalfAngleDegrees != 0))
                errors.Add($"ability {a.Id}: a line attack is a charge whose range is its distance");
            if (style is not ("none" or "target" or "cone" or "line" or "blast" or "burst") ||
                (style != "none" && (style != shape || a.Self || a.Damage <= 0 || a.ProjectileSpeed > 0)))
                errors.Add($"ability {a.Id}: telegraph does not match implemented danger");
            if (a.ActionStyle != "" && (!Enum.TryParse<PixelCreatures.Core.Anatomy.ActionStyle>(a.ActionStyle, out var actionStyle) || !Enum.IsDefined(actionStyle) || actionStyle.ToString() != a.ActionStyle))
                errors.Add($"ability {a.Id}: unknown procedural action style");
        }
        foreach (var s in Statuses)
            if (s.Duration <= 0 || s.Interval <= 0 || s.MaxStacks < 1 || !float.IsFinite(s.Heal) || s.Heal < 0 || (s.Heal > 0 && s.Damage > 0) || s.Modifiers == null || s.Modifiers.Any(m => !float.IsFinite(m.Value) || m.Key is not ("strength" or "defence" or "speed" or "damage" or "power")))
                errors.Add($"status {s.Id}: invalid stacking/timing");
        foreach (var r in Recipes)
            if (!items.Contains(r.Output) || r.Count < 1 || r.Ingredients.Count == 0 || r.Ingredients.Any(i => !items.Contains(i.Key) || i.Value < 1))
                errors.Add($"recipe {r.Id}: invalid item or quantity");
        foreach (var l in Loot)
            if (!items.Contains(l.Item) || l.Min < 1 || l.Max < l.Min || l.Chance < 0 || l.Chance > 1)
                errors.Add($"loot {l.Item}: invalid reference/range");
        return errors;
    }
}
