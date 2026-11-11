using System.Numerics;
using MHLab.Spells;
using MHLab.Spells.Definitions;

namespace Mosslight;
public sealed class World
{
    public bool ProfileCombat { get; set; }
    public double TacticalMilliseconds { get; private set; }
    public double AvoidanceMilliseconds { get; private set; }
    public double ProjectileMilliseconds { get; private set; }
    public double NavigationMilliseconds { get; private set; }
    static long Stamp() => System.Diagnostics.Stopwatch.GetTimestamp();
    static double Milliseconds(long start) => (Stamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    public int Seed { get; set; }
    public long Tick { get; set; }
    public double Time { get; set; }
    public Dictionary<string, ActorState> Actors { get; set; } = new();
    public Dictionary<string, RegionState> RegionsById { get; set; } = new();
    public List<ProjectileState> Projectiles { get; set; } = new();
    public Content Content { get; }
    public Items Items { get; }
    public EventStream Events { get; } = new();
    public string InteractionFeedback { get; private set; } = "Approach an object or fallen foe";
    public string LastBlockedReason { get; private set; } = "";
    public Vector2 RequestedVelocity { get; private set; }
    public int CollisionAttempts { get; private set; }
    public int CollisionSlides { get; private set; }
    public Vector2 LastInput { get; private set; }
    public Vector2 ActualVelocity { get; private set; }
    public ActorState Player => Actors["player"];
    public SpellsContext Spells { get; private set; } = new();

    readonly ActorSeparation separation = new();
    readonly Dictionary<string, Spell> spells = new();
    readonly Dictionary<string, AbilityDef> abilityDefinitions = new();
    readonly Dictionary<string, CombatProfileDef> profileDefinitions = new();
    public AbilityDef? FindAbility(string id) => abilityDefinitions.GetValueOrDefault(id);
    /// <summary>Authored tactics for an actor, or null for legacy actors without a profile.</summary>
    public CombatProfileDef? FindProfile(string id) => id == "" ? null : profileDefinitions.GetValueOrDefault(id);
    public Godot.Node? Planner { get; set; }

    public event Action<CombatPresentationEvent>? Presentation;
    // Separate from narrative events: opt-in, bounded by the existing trace writer.
    public event Action<string, Vector2, string>? CombatDiagnostic;
    internal void Diagnose(string kind, Vector2 at, string context) => CombatDiagnostic?.Invoke(kind, at, context);
    internal void Present(CombatPresentationKind kind, ActorState actor, string target = "", string ability = "", Vector2? position = null, string outcome = "") =>
        Presentation?.Invoke(new(kind, actor.Id, target, ability, Tick, actor.Position, position ?? actor.Position,
            new Vector2(actor.ActionDirectionX, actor.ActionDirectionY), outcome));
    public World(Content content, Godot.Node itemService, int seed = 42)
    {
        Content = content;
        Items = new(itemService, Events);
        Seed = seed;
        RebuildSpells();
    }

    public void RebuildSpells()
    {
        Spells = new();
        spells.Clear();
        abilityDefinitions.Clear();
        profileDefinitions.Clear();
        foreach (var profile in Content.CombatProfiles) profileDefinitions[profile.Id] = profile;
        foreach (var d in Content.Abilities)
        {
            spells[d.Id] = new Spell(new RpgAbility(this, d));
            abilityDefinitions[d.Id] = d;
        }
    }

    public float Stat(ActorState actor, string stat)
    {
        float value = stat switch
        {
            "strength" => actor.Strength,
            "defence" => actor.Defence,
            "speed" => actor.Speed,
            _ => 0
        };
        value += Items.Bonus(actor, stat);
        foreach (var skill in Content.Skills.Where(s => s.Stat == stat))
            value += actor.SkillRanks.GetValueOrDefault(skill.Id) * skill.PerRank;
        foreach (var status in actor.Statuses)
        {
            var definition = Content.Statuses.Single(d => d.Id == status.Id);
            if (definition.ModifierStat == stat)
                value += definition.Modifier * status.Stacks;
            if (definition.Modifiers.TryGetValue(stat, out float extra))
                value += extra * status.Stacks;
        }

        return value;
    }

    public bool Hostile(ActorState a, ActorState b) => a.Id != b.Id && (Content.Factions.GetValueOrDefault(a.Faction + ":" + b.Faction) + a.Reputation.GetValueOrDefault(b.Faction)) < 0;
    public bool CanActivate(ActorState a, ActorState b, AbilityDef d) => a.Alive && b.Alive && a.Region == b.Region && a.Abilities.Contains(d.Id) && a.State is ActionState.Idle or ActionState.Moving && a.Mana >= d.Cost && a.Cooldowns.GetValueOrDefault(d.Id) <= 0 && (!d.RequiresWeapon || (a.Grasps && Items.Bonus(a, "damage") > 0)) && (d.Self ? a == b : Hostile(a, b)) && (d.Self || Vector2.Distance(a.Position, b.Position) <= d.Range + a.NaturalReach) && (d.Self || d.MinRange <= 0 || Vector2.Distance(a.Position, b.Position) >= d.MinRange) && (!Directed(d) || CombatGeometry.TryNormalize(b.Position - a.Position, out _)) && LineOfSight(a, b);
    /// <summary>Attacks that commit to a ground direction when they start.</summary>
    public static bool Directed(AbilityDef? d) => d?.Geometry?.Shape is "cone" or "line";
    public bool Activate(string id, string ability, string target)
    {
        if (!Actors.TryGetValue(id, out var actor) || !Actors.TryGetValue(target, out var victim) || !spells.TryGetValue(ability, out var spell))
            return false;
        bool ok = Spells.CasterSystem.Cast(actor, new[] { victim }, spell, out _).State == SpellCastState.Success;
        if (ok)
            Spells.Update(0);
        return ok;
    }

    static bool TryDirection(Vector2 value, out Vector2 direction)
    {
        direction = Vector2.Zero;
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.LengthSquared() < .000001f || !float.IsFinite(value.LengthSquared())) return false;
        direction = Vector2.Normalize(value);
        return true;
    }

    // Only steer a few degrees toward an already nearby, visible hostile. Never
    // acquire behind the player, snap directly to a target, or change an attack
    // once it starts. The returned vector owns lunge, artwork and impact arc.
    Vector2 SoftAimPlayerSlash(ActorState attacker, AbilityDef ability, Vector2 facing)
    {
        float radius = ability.Range + attacker.NaturalReach + CombatTuning.SlashImpactTolerance
            + CombatTuning.SlashLungeDistance;
        float radiusSquared = radius * radius;
        float cone = CombatTuning.SlashAssistHalfDegrees * MathF.PI / 180f;
        float minimumDot = MathF.Cos(cone);
        ActorState? best = null;
        float bestScore = float.PositiveInfinity;
        float bestAngle = 0;
        float bestDistance = 0;
        foreach (var candidate in Actors.Values)
        {
            if (!candidate.Alive || candidate.Id == attacker.Id || candidate.Region != attacker.Region
                || !Hostile(attacker, candidate) || candidate.Invulnerable > 0 || DodgeImmune(candidate))
                continue;
            Vector2 offset = candidate.Position - attacker.Position;
            float squared = offset.LengthSquared();
            if (squared <= .000001f || squared > radiusSquared) continue;
            float distance = MathF.Sqrt(squared);
            Vector2 toward = offset / distance;
            float dot = Vector2.Dot(facing, toward);
            if (dot < minimumDot || !LineOfSight(attacker, candidate)) continue;
            float cross = facing.X * toward.Y - facing.Y * toward.X;
            float angle = MathF.Atan2(cross, dot);
            // Prioritize what the player already faces, breaking near-equal
            // scores by ordinal actor ID regardless of dictionary enumeration.
            float score = MathF.Abs(angle) / cone + .25f * distance / radius;
            if (score < bestScore - .000001f ||
                (MathF.Abs(score - bestScore) <= .000001f && best != null &&
                 StringComparer.Ordinal.Compare(candidate.Id, best.Id) < 0))
            {
                best = candidate;
                bestScore = score;
                bestAngle = angle;
                bestDistance = distance;
            }
        }
        if (best == null) return facing;
        float angularFalloff = 1f - .55f * MathF.Abs(bestAngle) / cone;
        float distanceFalloff = 1f - .25f * bestDistance / radius;
        float maximum = CombatTuning.SlashAssistMaximumDegrees * MathF.PI / 180f;
        float correction = Math.Clamp(bestAngle * CombatTuning.SlashAssistStrength *
            angularFalloff * distanceFalloff, -maximum, maximum);
        float c = MathF.Cos(correction), s = MathF.Sin(correction);
        return new Vector2(facing.X * c - facing.Y * s, facing.X * s + facing.Y * c);
    }

    /// <summary>Starts only the player sword swing; target selection occurs at impact.</summary>
    public bool TryStartPlayerSlash(Vector2 direction)
    {
        var a = Player;
        var d = Content.Abilities.Single(x => x.Id == "slash");
        if (!TryDirection(direction, out var facing) || !a.Alive || !a.Abilities.Contains(d.Id) ||
            a.State is not (ActionState.Idle or ActionState.Moving) || a.Mana < d.Cost ||
            a.Cooldowns.GetValueOrDefault(d.Id) > 0 ||
            (d.RequiresWeapon && (!a.Grasps || Items.Bonus(a, "damage") <= 0))) return false;
        a.Mana -= d.Cost;
        a.Cooldowns[d.Id] = d.Cooldown;
        a.MotionX = a.MotionY = 0;
        a.State = ActionState.Windup;
        a.StateRemaining = d.Windup;
        a.PendingAbility = d.Id;
        a.RecoveryAbility = "";
        a.Target = "";
        a.FreeSlash = true;
        a.ActionElapsed = 0;
        // Assist once before presentation; do not steer toward target movement
        // during the windup or change the authoritative 110-degree impact arc.
        Vector2 strike = SoftAimPlayerSlash(a, d, facing);
        a.ActionDirectionX = strike.X; a.ActionDirectionY = strike.Y;
        Present(CombatPresentationKind.AttackStarted, a, ability: d.Id);
        Present(CombatPresentationKind.AttackCommitted, a, ability: d.Id);
        Events.Publish(new("ability_started", a.Id, "", d.Id));
        CombatDiagnostic?.Invoke("attack_started", strike, "slash");
        ThreatenFrom(a, strike, d.Range + a.NaturalReach + CombatTuning.SlashImpactTolerance + CombatTuning.SlashLungeDistance, CombatTuning.SlashArcHalfDegrees);
        return true;
    }

    public bool TryStartPlayerDodge(Vector2 direction)
    {
        var a = Player;
        if (!TryDirection(direction, out var facing) || !a.Alive ||
            a.State is not (ActionState.Idle or ActionState.Moving) || a.Cooldowns.GetValueOrDefault("dodge") > 0) return false;
        a.MotionX = a.MotionY = 0;
        a.FreeSlash = false;
        a.ActionElapsed = 0;
        a.ActionDirectionX = facing.X; a.ActionDirectionY = facing.Y;
        a.State = ActionState.Dodging;
        a.StateRemaining = CombatTuning.DodgeDuration;
        a.Cooldowns["dodge"] = CombatTuning.DodgeCooldown;
        ClearPlayerMotion();
        Present(CombatPresentationKind.DodgeStarted, a, ability: "dodge");
        CombatDiagnostic?.Invoke("dodge_started", facing, "dodge");
        return true;
    }

    public static bool DodgeImmune(ActorState a) => a.State == ActionState.Dodging && (a.Id == "player"
        ? a.ActionElapsed >= CombatTuning.DodgeInvulnerableStart && a.ActionElapsed < CombatTuning.DodgeInvulnerableEnd
        : a.ActionElapsed >= CombatTuning.EvadeInvulnerableStart && a.ActionElapsed < CombatTuning.EvadeInvulnerableEnd);

    static float DodgeDuration(ActorState a) => a.Id == "player" ? CombatTuning.DodgeDuration : CombatTuning.EvadeDuration;
    float DodgeDistance(ActorState a) => a.Id == "player" ? CombatTuning.DodgeDistance : FindProfile(a.CombatProfile)?.EvadeDistance ?? 0;

    /// <summary>A short hop clear of an incoming attack. Enemies only; the player's roll is input-driven.</summary>
    public bool TryStartEvade(ActorState a, Vector2 direction)
    {
        var profile = FindProfile(a.CombatProfile);
        if (a.Id == "player" || profile == null || profile.EvadeDistance <= 0 || !TryDirection(direction, out var facing) || !a.Alive ||
            a.State is not (ActionState.Idle or ActionState.Moving) || a.Cooldowns.GetValueOrDefault("evade") > 0) return false;
        a.MotionX = a.MotionY = 0;
        a.FreeSlash = false;
        a.ActionElapsed = 0;
        a.ActionDirectionX = facing.X; a.ActionDirectionY = facing.Y;
        a.State = ActionState.Dodging;
        a.StateRemaining = CombatTuning.EvadeDuration;
        a.Cooldowns["evade"] = profile.EvadeCooldown;
        Present(CombatPresentationKind.DodgeStarted, a, ability: "evade");
        CombatDiagnostic?.Invoke("evade_started", facing, a.Id);
        return true;
    }

    // The player's committed attack is the cue. Each threatened enemy rolls once, from
    // world state only, so a replay or a reloaded save makes the same choice.
    void ThreatenFrom(ActorState attacker, Vector2 direction, float reach, float halfDegrees)
    {
        foreach (var b in Actors.Values.OrderBy(b => b.Id, StringComparer.Ordinal))
        {
            if (b.Id == attacker.Id || !b.Alive || b.Region != attacker.Region || b.Awareness != Awareness.Engaged || !Hostile(b, attacker)) continue;
            var profile = FindProfile(b.CombatProfile);
            if (profile == null || profile.EvadeChance <= 0 || b.State is not (ActionState.Idle or ActionState.Moving) || b.Cooldowns.GetValueOrDefault("evade") > 0) continue;
            if (!CombatGeometry.InCone(attacker.Position, b.Position, direction, reach + b.Radius, halfDegrees)) continue;
            if (EnemyMind.Hash01(StableId(b.Id) ^ Seed, Tick, 17) >= profile.EvadeChance) continue;
            var away = CombatGeometry.TryNormalize(b.Position - attacker.Position, out var back) ? back : -direction;
            var region = RegionsById[b.Region];
            // Back and to a side first; straight back keeps the attacker's line.
            float side = (StableId(b.Id) + Tick) % 2 == 0 ? 1 : -1;
            foreach (float angle in new[] { .9f * side, -.9f * side, 0f, 1.57f * side, -1.57f * side })
            {
                float c = MathF.Cos(angle), s = MathF.Sin(angle);
                var hop = new Vector2(away.X * c - away.Y * s, away.X * s + away.Y * c);
                if (!region.CanTravel(b.Position, b.Position + hop * profile.EvadeDistance, b)) continue;
                TryStartEvade(b, hop);
                break;
            }
        }
    }

    public void CancelPlayerDodge()
    {
        if (Player.State != ActionState.Dodging) return;
        Cancel(Player);
        ClearPlayerMotion();
    }

    void AdvanceDodge(ActorState a, float dt)
    {
        float duration = DodgeDuration(a);
        float old = Math.Clamp(a.ActionElapsed / duration, 0, 1);
        a.ActionElapsed = Math.Min(duration, a.ActionElapsed + dt);
        float now = Math.Clamp(a.ActionElapsed / duration, 0, 1);
        float distance = DodgeDistance(a) * ((1 - (1 - now) * (1 - now)) - (1 - (1 - old) * (1 - old)));
        Move(a, new Vector2(a.ActionDirectionX, a.ActionDirectionY) * distance);
        a.StateRemaining = Math.Max(0, duration - a.ActionElapsed);
    }

    // Committed movement inside an attack: a pounce or dart during its windup, or a
    // charge down its lane once the windup ends. Travel stops at terrain like any move.
    void AdvanceAttackMotion(ActorState a, float dt)
    {
        var d = FindAbility(a.State == ActionState.Windup ? a.PendingAbility : a.RecoveryAbility);
        if (d == null || (d.Lunge == null && d.Charge == null)) return;
        var direction = new Vector2(a.ActionDirectionX, a.ActionDirectionY);
        float previous = a.ActionElapsed;
        a.ActionElapsed += dt;
        if (d.Lunge is { } lunge)
        {
            float span = lunge.End - lunge.Start;
            float distance = lunge.Distance * (SmoothStep((a.ActionElapsed - lunge.Start) / span) - SmoothStep((previous - lunge.Start) / span));
            if (distance > 0) Move(a, direction * distance);
            return;
        }
        var charge = d.Charge!;
        if (a.State != ActionState.Recovery) return;
        float running = a.ActionElapsed - d.Windup, before = previous - d.Windup;
        if (before >= charge.Duration) return;
        float travel = charge.Speed * (Math.Min(running, charge.Duration) - Math.Max(before, 0));
        var from = a.Position;
        Move(a, direction * travel);
        if (!a.ActionConnected)
        {
            // The nearest hostile body in the lane takes the one hit; stable IDs break ties.
            var victim = Actors.Values.Where(b => b.Alive && b.Region == a.Region && Hostile(a, b) &&
                    Vector2.Distance(a.Position, b.Position) <= a.Radius + b.Radius + charge.Reach && Vector2.Dot(b.Position - from, direction) > -b.Radius)
                .OrderBy(b => Vector2.DistanceSquared(a.Position, b.Position)).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
            if (victim != null)
            {
                a.ActionConnected = true;
                var position = victim.Position;
                Present(CombatPresentationKind.AttackImpact, a, victim.Id, d.Id, position);
                if (!Resolve(a, victim, d, geometryValidated: true))
                    Present(CombatPresentationKind.Miss, a, victim.Id, d.Id, position, "miss");
                return;
            }
        }
        if (!a.ActionConnected && travel > .01f && Vector2.Distance(from, a.Position) < travel * .35f)
        {
            // Ran into terrain: the charge ends in a daze instead of an ordinary recovery.
            Present(CombatPresentationKind.ChargeCrashed, a, a.Target, d.Id);
            CombatDiagnostic?.Invoke("charge_crashed", direction, a.Id);
            Cancel(a);
            a.State = ActionState.Staggered;
            a.StateRemaining = charge.CrashStagger;
        }
        else if (running >= charge.Duration && !a.ActionConnected)
        {
            a.ActionConnected = true;
            Present(CombatPresentationKind.AttackImpact, a, a.Target, d.Id);
            Present(CombatPresentationKind.Miss, a, a.Target, d.Id, outcome: "miss");
        }
    }

    ActorState? PlayerSlashTarget(ActorState attacker, AbilityDef ability)
    {
        var direction = new Vector2(attacker.ActionDirectionX, attacker.ActionDirectionY);
        float maxRange = ability.Range + attacker.NaturalReach + CombatTuning.SlashImpactTolerance;
        float maxDistanceSquared = maxRange * maxRange;
        float minimumDot = MathF.Cos(CombatTuning.SlashArcHalfDegrees * MathF.PI / 180);
        ActorState? best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var candidate in Actors.Values)
        {
            if (!candidate.Alive || candidate.Invulnerable > 0 || DodgeImmune(candidate) || candidate.Region != attacker.Region || !Hostile(attacker, candidate)) continue;
            var offset = candidate.Position - attacker.Position;
            float distance = offset.LengthSquared();
            if (distance > maxDistanceSquared || (distance > .000001f && Vector2.Dot(offset / MathF.Sqrt(distance), direction) < minimumDot) || !LineOfSight(attacker, candidate)) continue;
            if (distance < bestDistance || (distance == bestDistance && best != null && StringComparer.Ordinal.Compare(candidate.Id, best.Id) < 0))
            { best = candidate; bestDistance = distance; }
        }
        return best;
    }

    static float SmoothStep(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    void AdvanceSlashLunge(ActorState a, float dt)
    {
        float previous = a.ActionElapsed;
        a.ActionElapsed += dt;
        float span = CombatTuning.SlashLungeEnd - CombatTuning.SlashLungeStart;
        float distance = CombatTuning.SlashLungeDistance * (SmoothStep((a.ActionElapsed - CombatTuning.SlashLungeStart) / span) - SmoothStep((previous - CombatTuning.SlashLungeStart) / span));
        if (distance <= 0) return;
        var before = a.Position;
        int attempts = CollisionAttempts;
        Move(a, new Vector2(a.ActionDirectionX, a.ActionDirectionY) * distance);
        if (CollisionAttempts != attempts) CombatDiagnostic?.Invoke("attack_lunge_collision", a.Position - before, LastBlockedReason);
    }

    public void BeginAbility(ActorState a, ActorState b, AbilityDef d)
    {
        a.MotionX = 0;
        a.MotionY = 0;
        a.State = ActionState.Windup;
        a.StateRemaining = d.Windup;
        a.PendingAbility = d.Id;
        a.RecoveryAbility = "";
        a.Target = b.Id;
        a.FreeSlash = false;
        a.ActionElapsed = 0;
        a.ActionConnected = false;
        // Directed attacks and lunges commit to where the target stands now.
        if ((Directed(d) || d.Lunge != null) && CombatGeometry.TryNormalize(b.Position - a.Position, out var direction))
        { a.ActionDirectionX = direction.X; a.ActionDirectionY = direction.Y; }
        else a.ActionDirectionX = a.ActionDirectionY = 0;
        if (a.ActionDirectionX != 0 || a.ActionDirectionY != 0) { a.FacingX = a.ActionDirectionX; a.FacingY = a.ActionDirectionY; }
        Present(CombatPresentationKind.AttackStarted, a, b.Id, d.Id, b.Position);
        Present(CombatPresentationKind.AttackCommitted, a, b.Id, d.Id, b.Position);
        Events.Publish(new("ability_started", a.Id, b.Id, d.Id));
    }

    public void Cancel(ActorState a)
    {
        if (a.State == ActionState.Dodging)
        {
            CombatDiagnostic?.Invoke("dodge_interrupted", new Vector2(a.ActionDirectionX, a.ActionDirectionY), "cancel");
            Present(CombatPresentationKind.DodgeCanceled, a, ability: "dodge");
        }
        if (a.State == ActionState.Windup && a.PendingAbility != "")
            Present(CombatPresentationKind.AttackCanceled, a, a.Target, a.PendingAbility);
        a.PendingAbility = "";
        a.RecoveryAbility = "";
        a.FreeSlash = false;
        a.ActionElapsed = 0;
        a.ActionConnected = false;
        a.ActionDirectionX = a.ActionDirectionY = 0;
        a.State = a.Alive ? ActionState.Idle : ActionState.Dead;
        a.StateRemaining = 0;
    }

    public bool Damage(ActorState source, ActorState target, float amount, string type, bool periodic = false, string ability = "")
    {
        if (!target.Alive || (!periodic && source.Region != target.Region) || !Hostile(source, target))
            return false;
        if (!periodic && target.Invulnerable > 0)
        {
            Present(CombatPresentationKind.DefensiveAvoidance, source, target.Id, ability, target.Position, "invulnerability");
            return false;
        }
        if (!periodic && DodgeImmune(target))
        {
            CombatDiagnostic?.Invoke("direct_hit_dodged", new Vector2(target.ActionDirectionX, target.ActionDirectionY), source.Id);
            Present(CombatPresentationKind.DefensiveAvoidance, source, target.Id, ability, target.Position, "dodge");
            return false;
        }
        var impactPosition = target.Position;
        // Striking a foe that has not noticed its attacker lands harder.
        bool ambush = !periodic && source.Id == "player" && target.Id != "player" && target.Awareness is Awareness.Unaware or Awareness.Suspicious;
        if (ambush) amount *= CombatTuning.AmbushMultiplier;
        float resistance = type == "physical" ? Stat(target, "defence") : 0;
        float actual = Math.Max(1, amount - resistance);
        target.Health = Math.Max(0, target.Health - actual);
        // Only direct hits grant the short post-hit window. A damage-over-time tick
        // must not make its victim immune to the next sword or projectile.
        if (!periodic) target.Invulnerable = .12f;
        // Guarded foes shrug off blows until their poise is spent; the breaking hit staggers for longer.
        var guard = target.Alive ? FindProfile(target.CombatProfile) : null;
        bool guarded = false, broken = false;
        if (guard is { Poise: > 0 })
        {
            if (target.PoiseDelay <= 0) target.Poise = guard.Poise;
            if (!periodic) { target.Poise -= actual; target.PoiseDelay = CombatTuning.PoiseRecoverySeconds; }
            if (periodic || target.Poise > 0) guarded = true;
            else { broken = true; target.Poise = guard.Poise; }
        }
        // Venom and burns hurt the player without stealing control every second.
        if (periodic && target.Id == "player" && target.Alive) guarded = true;
        if (!guarded)
        {
            Cancel(target);
            target.State = target.Alive ? ActionState.Staggered : ActionState.Dead;
            target.StateRemaining = broken ? CombatTuning.GuardBreakStagger : .22f;
        }
        var away = target.Position - source.Position;
        if (!periodic && away.LengthSquared() > 0)
            Move(target, Vector2.Normalize(away) * (guarded ? 4 : FindAbility(ability)?.Knockback ?? 15));
        Present(CombatPresentationKind.Hit, source, target.Id, ability, impactPosition, periodic ? "periodic" : ambush ? "ambush" : guarded ? "guarded" : "hit");
        if (broken) Present(CombatPresentationKind.GuardBroken, source, target.Id, ability, impactPosition);
        Events.Publish(new("damage", source.Id, target.Id, type, (int)actual));
        if (ambush) Events.Publish(new("ambush", source.Id, target.Id, type, (int)actual));
        target.Knowledge[source.Id] = "attacker";
        target.Relationships[source.Id] = target.Relationships.GetValueOrDefault(source.Id) - actual;
        EnemyMind.Hurt(this, target, source);
        if (!target.Alive)
        {
            Items.Service.Call("unequip_all", target.Id);
            target.PendingAbility = "";
            Present(CombatPresentationKind.Death, source, target.Id, ability, impactPosition);
            Events.Publish(new("defeated", source.Id, target.Id, target.Profile));
            Progression.Award(this, source, 25);
            var random = new Random(unchecked(Seed + (int)Tick + StableId(target.Id)));
            foreach (var l in Content.Loot)
                if (random.NextDouble() <= l.Chance)
                    Items.Add(target.Id, l.Item, random.Next(l.Min, l.Max + 1));
        }
        return true;
    }

    public static int StableId(string text)
    {
        unchecked
        {
            int hash = 216613626;
            foreach (char c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }

    public void ApplyStatus(ActorState a, string id, string source)
    {
        var d = Content.Statuses.Single(x => x.Id == id);
        var s = a.Statuses.Find(x => x.Id == id);
        if (s == null)
            a.Statuses.Add(new() { Id = id, Source = source, Remaining = d.Duration, UntilTick = d.Interval });
        else
        {
            s.Stacks = Math.Min(d.MaxStacks, s.Stacks + 1);
            s.Remaining = d.Duration;
            s.Source = source;
        }

        // The player is told what has taken hold; enemy afflictions are shown on the body instead.
        if (a.Id == "player" && source != a.Id) Events.Publish(new("afflicted", source, a.Id, id, (int)MathF.Ceiling(d.Duration)));
        if (d.BlocksActions)
        {
            Cancel(a);
            a.State = ActionState.Staggered;
            a.StateRemaining = d.Duration;
        }
    }

    bool Resolve(ActorState a, ActorState b, AbilityDef d, bool projectile = false, bool geometryValidated = false)
    {
        if (!a.Alive || !b.Alive || a.Region != b.Region)
            return false;
        if (d.RequiresWeapon && (!a.Grasps || Items.Bonus(a, "damage") <= 0))
            return false;
        if (d.Self)
        {
            a.Health = Math.Min(a.MaxHealth, a.Health - d.Damage);
            if (d.Status != "")
                ApplyStatus(a, d.Status, a.Id);
            if (d.AllyRadius > 0 && d.Status != "")
            {
                foreach (var ally in Actors.Values.Where(c => c.Id != a.Id && c.Alive && c.Region == a.Region && c.Faction == a.Faction && !Hostile(a, c) &&
                    Vector2.DistanceSquared(a.Position, c.Position) <= d.AllyRadius * d.AllyRadius).OrderBy(c => c.Id, StringComparer.Ordinal))
                    ApplyStatus(ally, d.Status, a.Id);
                Present(CombatPresentationKind.Rallied, a, a.Id, d.Id);
            }
            return true;
        }

        if (!projectile && !geometryValidated && (Vector2.Distance(a.Position, b.Position) > d.Range + a.NaturalReach + 8 || !LineOfSight(a, b)))
            return false;
        // Intentionally non-damaging hostile effects retain their status-only behavior.
        bool connected = d.Damage <= 0 ? Hostile(a, b) && !DodgeImmune(b) : Damage(a, b, d.Damage + Stat(a, "strength") + Stat(a, d.Type == "physical" ? "damage" : "power"), d.Type, ability: d.Id);
        if (connected && b.Alive && d.Status != "")
            ApplyStatus(b, d.Status, a.Id);
        return connected;
    }

    public void Step(float dt)
    {
        if (dt <= 0 || dt > .25f || !float.IsFinite(dt))
            throw new ArgumentOutOfRangeException(nameof(dt));
        Tick++;
        Exploration.Discover(this);
        Time += dt;
        Spells.Update(dt);
        // Advance before any actor resolves an attack so protection is independent
        // of dictionary iteration order at the fixed-step boundary.
        if (Player.Alive && Player.State == ActionState.Dodging) AdvanceDodge(Player, dt);
        TacticalMilliseconds = AvoidanceMilliseconds = ProjectileMilliseconds = NavigationMilliseconds = 0;
        long avoidanceStart = ProfileCombat ? Stamp() : 0;
        separation.Rebuild(Actors.Values);
        if(ProfileCombat) AvoidanceMilliseconds += Milliseconds(avoidanceStart);
        foreach (var a in Actors.Values.OrderBy(a => a.Id == "player" ? 0 : 1).ThenBy(a => a.Id, StringComparer.Ordinal))
        {
            foreach (var key in a.Cooldowns.Keys.ToArray())
                a.Cooldowns[key] = Math.Max(0, a.Cooldowns[key] - dt);
            a.Invulnerable = Math.Max(0, a.Invulnerable - dt);
            if (!a.Alive)
                continue;
            a.Mana = Math.Min(a.MaxMana, a.Mana + dt * 2);
            foreach (var s in a.Statuses.ToArray())
            {
                var d = Content.Statuses.Single(x => x.Id == s.Id);
                s.Remaining -= dt;
                s.UntilTick -= dt;
                if (s.UntilTick <= 0 && d.Damage > 0 && Actors.TryGetValue(s.Source, out var source))
                {
                    Damage(source, a, d.Damage * s.Stacks, d.Type, periodic: true);
                    s.UntilTick += d.Interval;
                }

                if (s.Remaining <= 0)
                    a.Statuses.Remove(s);
            }

            if (!a.Alive)
                continue;
            if (a.PoiseDelay > 0) a.PoiseDelay = Math.Max(0, a.PoiseDelay - dt);
            if (a.FreeSlash && a.State is ActionState.Windup or ActionState.Recovery)
                AdvanceSlashLunge(a, dt);
            else if (a.Id != "player" && a.State is ActionState.Windup or ActionState.Recovery)
                AdvanceAttackMotion(a, dt);
            if (a.Id != "player" && a.State == ActionState.Dodging) AdvanceDodge(a, dt);
            if (a.State == ActionState.Dodging && a.ActionElapsed >= DodgeDuration(a))
            {
                CombatDiagnostic?.Invoke("dodge_completed", new Vector2(a.ActionDirectionX, a.ActionDirectionY), a.Id == "player" ? "dodge" : "evade");
                a.State = ActionState.Idle;
                a.StateRemaining = 0;
                a.ActionElapsed = 0;
                a.ActionDirectionX = a.ActionDirectionY = 0;
            }
            // A zero-duration windup/recovery still crosses its boundary exactly once
            // on the next simulation step; presentation never adds a delay.
            if (a.State != ActionState.Dodging && (a.StateRemaining > 0 || a.State is ActionState.Windup or ActionState.Recovery))
            {
                a.StateRemaining -= dt;
                if (a.StateRemaining <= 0)
                {
                    if (a.State == ActionState.Windup && a.PendingAbility != "")
                    {
                        var d = Content.Abilities.Single(x => x.Id == a.PendingAbility);
                        if (a.Id == "player" && a.FreeSlash && d.Id == "slash")
                        {
                            Present(CombatPresentationKind.AttackImpact, a, ability: d.Id);
                            var target = PlayerSlashTarget(a, d);
                            bool hit = target != null && Resolve(a, target, d);
                            if (!hit) Present(CombatPresentationKind.Miss, a, target?.Id ?? "", d.Id, target?.Position, "miss");
                            CombatDiagnostic?.Invoke("attack_impact", new Vector2(a.ActionDirectionX, a.ActionDirectionY), hit ? "hit/" + target!.Id : "miss");
                        }
                        else if (d.Charge != null)
                        {
                            // The lane is committed; contact is resolved while running it.
                            Present(CombatPresentationKind.ChargeStarted, a, a.Target, d.Id);
                        }
                        else if (d.Geometry?.Shape == "cone")
                        {
                            Present(CombatPresentationKind.AttackImpact, a, a.Target, d.Id);
                            var direction = new Vector2(a.ActionDirectionX, a.ActionDirectionY);
                            // Stable IDs define the order independently of dictionary insertion.
                            var victims = Actors.Values.Where(b => b.Alive && b.Region == a.Region && Hostile(a, b) &&
                                CombatGeometry.InCone(a.Position, b.Position, direction, d.Geometry.Range, d.Geometry.HalfAngleDegrees) && LineOfSight(a, b))
                                .OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
                            foreach (var victim in victims)
                            {
                                var position = victim.Position;
                                if (!Resolve(a, victim, d, geometryValidated: true))
                                    Present(CombatPresentationKind.Miss, a, victim.Id, d.Id, position, "miss");
                            }
                            if (victims.Length == 0) Present(CombatPresentationKind.Miss, a, a.Target, d.Id, outcome: "miss");
                        }
                        else if (Actors.TryGetValue(a.Target, out var t))
                        {
                            if (d.ProjectileSpeed > 0)
                            {
                                int count = Math.Max(1, d.Projectile?.Count ?? 1);
                                for (int shot = 0; shot < count; shot++)
                                {
                                    float fan = count == 1 ? 0 : (shot / (count - 1f) - .5f) * (d.Projectile?.SpreadDegrees ?? 0) * MathF.PI / 180;
                                    Projectiles.Add(ProjectileFlight.Release(a, t, d, fan));
                                }
                                Present(CombatPresentationKind.ProjectileReleased, a, t.Id, d.Id, a.Position);
                            }
                            else
                            {
                                Present(CombatPresentationKind.AttackImpact, a, t.Id, d.Id, t.Position);
                                if (!Resolve(a, t, d)) Present(CombatPresentationKind.Miss, a, t.Id, d.Id, t.Position, "miss");
                            }
                        }
                        else if (d.ProjectileSpeed <= 0)
                        {
                            Present(CombatPresentationKind.AttackImpact, a, a.Target, d.Id);
                            Present(CombatPresentationKind.Miss, a, a.Target, d.Id, outcome: "target unavailable");
                        }

                        a.PendingAbility = "";
                        a.RecoveryAbility = d.Id;
                        a.State = ActionState.Recovery;
                        a.StateRemaining = d.Recovery;
                    }
                    else
                    {
                        if (a.FreeSlash) CombatDiagnostic?.Invoke("attack_completed", new Vector2(a.ActionDirectionX, a.ActionDirectionY), "slash");
                        if (a.State == ActionState.Recovery && a.RecoveryAbility != "")
                            Present(CombatPresentationKind.ActionCompleted, a, a.Target, a.RecoveryAbility);
                        a.State = ActionState.Idle;
                        a.FreeSlash = false;
                        a.ActionElapsed = 0;
                        a.ActionConnected = false;
                        a.RecoveryAbility = "";
                    }
                }
            }

            if (a.Id != "player")
            {
                bool near = a.Region == Player.Region && Vector2.DistanceSquared(a.Position, Player.Position) < 800 * 800;
                if (near)
                {
                    if ((Tick + ((uint)StableId(a.Id) % 12)) % 12 == 0)
                    {
                        long start = ProfileCombat ? Stamp() : 0;
                        Think(a, .2f);
                        if(ProfileCombat) TacticalMilliseconds += Milliseconds(start);
                    }
                    EnemyMind.Face(this, a);
                    if (a.State is ActionState.Idle or ActionState.Moving)
                    {
                        long start = ProfileCombat ? Stamp() : 0;
                        // Avoidance is for combatants, not passive friendly NPCs. Otherwise
                        // a nearby player produces movement even when a merchant's planner
                        // requested zero velocity, making them appear pushable.
                        var desired = new Vector2(a.MotionX, a.MotionY);
                        var velocity = a.CombatProfile != "" || Hostile(a, Player)
                            ? separation.Velocity(a, RegionsById[a.Region], desired, Math.Max(0, Stat(a, "speed")), dt)
                            : desired;
                        if(ProfileCombat) AvoidanceMilliseconds += Milliseconds(start);
                        Move(a, velocity * dt);
                    }
                }
                else
                {
                    a.MotionX = 0;
                    a.MotionY = 0;
                    // Out of earshot a hunt simply ends; the actor is calm when next met.
                    if (a.Awareness != Awareness.Unaware && a.State is ActionState.Idle or ActionState.Moving) EnemyMind.Calm(a);
                    if (Tick % 600 == 0)
                        UpdateSchedule(a);
                }
            }
        }

        long projectileStart = ProfileCombat ? Stamp() : 0;
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            var d = FindAbility(p.Ability);
            if (d == null || !Actors.TryGetValue(p.Source, out var source) || !RegionsById.TryGetValue(p.Region == "" ? source.Region : p.Region, out var region) || source.Region != region.Id)
            { CombatDiagnostic?.Invoke("projectile_terminal", new Vector2(p.X,p.Y), p.Ability+"/source or region unavailable"); Projectiles.RemoveAt(i); continue; }
            var hit = ProjectileFlight.Advance(this, p, d, region, dt);
            if (hit.Outcome == "") continue;
            CombatDiagnostic?.Invoke("projectile_terminal", new Vector2(p.X, p.Y), d.Id + "/" + hit.Outcome);
            if (hit.Actor != null)
            {
                Present(CombatPresentationKind.ProjectileImpact, source, hit.Actor.Id, d.Id, new Vector2(p.X, p.Y), "actor");
                if (!Resolve(source, hit.Actor, d, projectile: true))
                    Present(CombatPresentationKind.Miss, source, hit.Actor.Id, d.Id, new Vector2(p.X, p.Y), "miss");
            }
            else if (hit.Outcome is "terrain" or "boundary")
                Present(CombatPresentationKind.ProjectileImpact, source, ability: d.Id, position: new Vector2(p.X, p.Y), outcome: hit.Outcome);
            // Expiration/target loss is diagnostic only, never a fabricated impact.
            Projectiles.RemoveAt(i);
        }
        if(ProfileCombat) ProjectileMilliseconds = Milliseconds(projectileStart);
    }

    public bool VisibleTo(ActorState actor, Vector2 point, bool allowBlockedTarget = false)
    {
        var region = RegionsById[actor.Region];
        int target = region.Index(point);
        int steps = (int)(Vector2.Distance(actor.Position, point) / 4) + 1;
        for (int i = 1; i <= steps; i++)
        {
            int tile = region.Index(Vector2.Lerp(actor.Position, point, (float)i / steps));
            if (allowBlockedTarget && tile == target)
                continue;
            if (region.BlocksSight(tile))
                return false;
        }

        return true;
    }

    public bool LineOfSight(ActorState a, ActorState b) => a.Region == b.Region && VisibleTo(a, b.Position);
    public bool Move(ActorState a, Vector2 delta)
    {
        if (!a.Alive || !float.IsFinite(delta.X) || !float.IsFinite(delta.Y))
            return false;
        var r = RegionsById[a.Region];
        bool moved = false;
        // Small substeps prevent wall tunnelling, including knockback and large frame deltas.
        int steps = Math.Max(1, (int)Math.Ceiling(delta.Length() / Math.Min(2, a.Radius / 2)));
        delta /= steps;
        for (int n = 0; n < steps; n++)
        {
            if (r.CanStand(a.Position + delta, a))
            {
                a.X += delta.X; a.Y += delta.Y;
                moved |= delta.LengthSquared() > 0;
                continue;
            }
            if (a.Id == "player")
            {
                CollisionAttempts++;
                int at = r.BlockingCell(a.Position + delta,a);
                LastBlockedReason = at < 0 ? "Map boundary" : r.Features.GetValueOrDefault(at) switch { "lock" => "Locked door", "portcullis" => "Lowered portcullis", "seal" => "Light seal", _ => null } ?? $"Terrain / footprint: {r.Tiles[at]} at {at%r.Width},{at/r.Width}";
            }
            // Advance to contact before sliding. Previously an entire (up to 2px)
            // substep vanished at contact; deterministic bisection retains the valid
            // part and makes wall clearance less sensitive to approach speed.
            float lo=0,hi=1;
            for(int k=0;k<6;k++) { float mid=(lo+hi)*.5f; if(r.CanStand(a.Position+delta*mid,a))lo=mid;else hi=mid; }
            var accepted=delta*lo;
            a.X+=accepted.X;a.Y+=accepted.Y;moved |= accepted.LengthSquared()>0;
            var remainder=delta*(1-lo);
            // Prefer the larger tangent request rather than always preferring X.
            bool xFirst=Math.Abs(remainder.X)>=Math.Abs(remainder.Y);
            for(int axis=0;axis<2;axis++)
            {
                bool horizontal=(axis==0)==xFirst;
                var slide=horizontal ? new Vector2(remainder.X,0) : new Vector2(0,remainder.Y);
                if(slide.LengthSquared()==0 || !r.CanStand(a.Position+slide,a))continue;
                a.X+=slide.X;a.Y+=slide.Y;moved=true;
                if(a.Id=="player")CollisionSlides++;
            }
        }

        return moved;
    }

    public void ClearPlayerMotion() { ActualVelocity=RequestedVelocity=Vector2.Zero; LastInput=Vector2.Zero; LastBlockedReason=""; CollisionAttempts=CollisionSlides=0; }

    public bool PlayerMove(Vector2 direction, float dt, float speedScale=1, bool projected=false)
    {
        var a = Player;
        ActualVelocity=RequestedVelocity=Vector2.Zero;
        CollisionAttempts=CollisionSlides=0;
        LastInput=float.IsFinite(direction.X) && float.IsFinite(direction.Y) ? direction : Vector2.Zero; LastBlockedReason="";
        if (a.State is not (ActionState.Idle or ActionState.Moving)) { LastBlockedReason="Action: "+a.State; return false; }
        if(!float.IsFinite(dt)||dt<=0||!float.IsFinite(speedScale)||speedScale<0||!float.IsFinite(direction.X)||!float.IsFinite(direction.Y))return false;
        float magnitude=projected ? new Vector2(direction.X,direction.Y*.5f).Length() : direction.Length();
        if(magnitude>1)direction/=magnitude;
        LastInput = direction;
        LastBlockedReason = "";
        var before = a.Position;
        RequestedVelocity=direction * Math.Max(0, Stat(a, "speed")) * speedScale;
        bool moved = Move(a, RequestedVelocity * dt);
        ActualVelocity = dt > 0 ? (a.Position - before) / dt : Vector2.Zero;
        a.State = moved ? ActionState.Moving : ActionState.Idle;
        return moved;
    }

    void UpdateSchedule(ActorState a)
    {
        var profile = Content.NpcProfiles.Find(p => p.Id == a.Profile);
        a.Activity = profile?.Schedule.Where(s => s.Hour <= ((Time / 60) % 24)).OrderBy(s => s.Hour).LastOrDefault()?.Activity ?? "patrol";
        // An undisturbed ambusher lies folded on its spot whatever the hour.
        if (a.Awareness == Awareness.Unaware && FindProfile(a.CombatProfile)?.Ambusher == true) a.Activity = "ambush";
    }

    public bool Rest(ActorState a, float dt)
    {
        if (!a.Alive || a.State is not (ActionState.Idle or ActionState.Moving))
            return false;
        a.Health = Math.Min(a.MaxHealth, a.Health + dt);
        return true;
    }

    void Think(ActorState a, float dt)
    {
        if (a.State is not (ActionState.Idle or ActionState.Moving))
            return;
        UpdateSchedule(a);
        if (Time + .00001 < a.NextTacticalTime) return;
        a.MotionX = a.MotionY = 0;
        var profile = FindProfile(a.CombatProfile);
        float step = profile?.DecisionSeconds ?? dt;
        a.NextTacticalTime = Time + step;
        EnemyMind.Think(this, a, profile, step);
    }

    /// <summary>Top-level goal from the planner; perception and tactics choose how to pursue it.</summary>
    internal string Plan(string goal, bool seesTarget, string fallback) => Planner?.Call("choose", goal, seesTarget).AsString() ?? fallback;

    public void Follow(ActorState a, Vector2 target, float dt, float pace = 1)
    {
        a.MotionX = a.MotionY = 0;
        var r = RegionsById[a.Region];
        Vector2 waypoint = target;
        long navigationStart = ProfileCombat ? Stamp() : 0;
        if (!r.CanTravel(a.Position, target, a))
        {
            var path = r.Path(a.Position, target, a);
            if (path.Count == 0) { if(ProfileCombat) NavigationMilliseconds += Milliseconds(navigationStart); return; }
            waypoint = r.Center(path[0]);
        }
        if(ProfileCombat) NavigationMilliseconds += Milliseconds(navigationStart);
        var delta = waypoint - a.Position;
        if (delta.Length() <= .1f) return;
        // Planning happens every .2s. Stop exactly at the waypoint instead of
        // overshooting it and alternating direction on subsequent plans.
        var velocity = Vector2.Normalize(delta) * Math.Min(Math.Max(0, Stat(a, "speed")) * Math.Clamp(pace, 0, 1), delta.Length() / Math.Max(.001f, dt));
        a.MotionX = velocity.X; a.MotionY = velocity.Y;
        a.State = ActionState.Moving;
    }

    public IEnumerable<KeyValuePair<int, string>> InteractionTargets(ActorState a)
    {
        var r = RegionsById[a.Region];
        return r.Features.Where(x => !r.Depleted.Contains(x.Key) && !r.OpenDoors.Contains(x.Key) && Vector2.Distance(r.Center(x.Key), a.Position) < 48 && VisibleTo(a, r.Center(x.Key), allowBlockedTarget: RegionState.IsGate(x.Value)))
            .OrderBy(x => Vector2.DistanceSquared(r.Center(x.Key), a.Position)).ThenBy(x => x.Key);
    }

    public IEnumerable<ActorState> LootTargets(ActorState a) =>
        Actors.Values.Where(b => !b.Alive && b.Region == a.Region && Vector2.Distance(a.Position, b.Position) < 48 && LineOfSight(a, b) && Items.HasLoot(b.Id))
            .OrderBy(b => Vector2.DistanceSquared(a.Position, b.Position)).ThenBy(b => b.Id, StringComparer.Ordinal);

    public bool Interact(ActorState a)
    {
        if (!a.Alive || a.State is not (ActionState.Idle or ActionState.Moving))
            return false;
        var r = RegionsById[a.Region];
        InteractionFeedback = "No accessible object nearby";
        var nearby = InteractionTargets(a).ToArray();
        bool keyRequired = false;
        string sealedHint = "";
        foreach (var(at, kind)in nearby)
        {
            // Mechanism gates never open by hand; explain them, but keep looking for objects in reach.
            if (kind == "portcullis") { sealedHint = "The portcullis is raised by a lever deeper in the ruin"; continue; }
            if (kind == "seal") { sealedHint = "A ward of light bars the way — kindle every brazier in the nearby chamber"; continue; }
            if (kind == "lever")
            {
                var raised = DungeonMechanisms.Pull(r, at);
                if (raised.Length == 0) continue;
                InteractionFeedback = "Lever pulled — a portcullis grinds open";
                Events.Publish(new("lever_pulled", a.Id, a.Region, at.ToString()));
                return true;
            }
            if (kind == "brazier")
            {
                if (!DungeonMechanisms.Kindle(r, at, out bool solved)) continue;
                int lit = DungeonMechanisms.RingAt(r, at).Count(r.Lit.Contains);
                InteractionFeedback = solved ? "Every brazier burns — the light seal breaks" : $"The flames shift: {lit} of {DungeonMechanisms.RingSize} braziers burn";
                Events.Publish(new("brazier_kindled", a.Id, a.Region, at.ToString(), lit));
                if (solved) Events.Publish(new("seal_broken", a.Id, a.Region, at.ToString()));
                return true;
            }
            if (kind == "lock")
            {
                if (r.OpenDoors.Contains(at))
                    continue;
                // A closer locked gate must not hide a key, chest or exit also in reach.
                if (!Items.Take(a.Id, "dungeon_key", 1))
                { keyRequired = true; continue; }
                InteractionFeedback = "Door unlocked";
                foreach (int cell in r.GateAt(at)) r.OpenDoors.Add(cell);
                Events.Publish(new("door_unlocked", a.Id, a.Region, at.ToString()));
                return true;
            }

            if (kind is "portal" or "exit")
            {
                InteractionFeedback = kind == "portal" ? "Entered the ruin" : "Returned to the Reach";
                Enter(a, kind == "portal" ? "ruin" : "reach");
                return true;
            }

            if (kind == "chest")
            {
                string owner = a.Region + ":" + at;
                bool changed = Items.LootAll(owner, a.Id);
                if (Items.HasLoot(owner))
                { InteractionFeedback = changed ? "Some loot collected; make room for the rest" : "Inventory full"; return changed; }
                InteractionFeedback = changed ? "Chest emptied — loot added to pack" : "Chest is empty";
            }
            else if (kind is "herb" or "ore" or "wood" or "relic" or "key")
            {
                if (!Items.Add(a.Id, kind == "key" ? "dungeon_key" : kind, kind is "relic" or "key" ? 1 : 3))
                { InteractionFeedback = "Inventory full"; return false; }
                InteractionFeedback = "Collected " + kind;
            }
            else
                continue;
            r.Depleted.Add(at);
            Events.Publish(new("resource_depleted", a.Id, a.Region, kind));
            return true;
        }

        var corpses = LootTargets(a);
        foreach (var corpse in corpses)
            if (Items.LootAll(corpse.Id, a.Id)) { InteractionFeedback = "Loot collected"; return true; }
        if (keyRequired)
            InteractionFeedback = "A ruin key is required";
        else if (sealedHint != "")
            InteractionFeedback = sealedHint;
        else if (r.Features.Any(x => x.Value == "chest" && r.Depleted.Contains(x.Key) && Vector2.Distance(a.Position,r.Center(x.Key)) < 48 && VisibleTo(a,r.Center(x.Key))))
            InteractionFeedback = "Chest already empty";
        return false;
    }

    /// <summary>
    /// Read-only preview of what <see cref="Interact"/> would do now, walking the same
    /// targets in the same order. Never mutates regions, items or events. The HUD uses
    /// this so the contextual Interact label names the action actually taken.
    /// </summary>
    public InteractionPreview PreviewInteraction(ActorState a)
    {
        if (!a.Alive) return InteractionPreview.None;
        var r = RegionsById[a.Region];
        bool keyRequired = false;
        InteractionPreview? hint = null;
        foreach (var (at, kind) in InteractionTargets(a))
        {
            switch (kind)
            {
                case "portcullis": hint = new("Gate shut", "Raised by a lever deeper in the ruin", false); continue;
                case "seal": hint = new("Sealed", "Kindle every brazier in the nearby chamber", false); continue;
                case "lever": if (DungeonMechanisms.CanPull(r, at)) return new("Pull lever", "Raise a portcullis", true); continue;
                case "brazier": if (DungeonMechanisms.CanKindle(r, at)) return new("Kindle", "Light this brazier", true); continue;
                case "lock":
                    if (Items.Count(a.Id, "dungeon_key") > 0) return new("Unlock", "Use a ruin key", true);
                    keyRequired = true; continue;
                case "portal": return new("Enter ruin", "Enter the ruin", true);
                case "exit": return new("Leave ruin", "Return to the Reach", true);
                case "chest": return new("Open chest", "Collect the chest's contents", true);
                case "herb" or "ore" or "wood": return new("Gather " + kind, "Collect " + kind, true);
                case "relic": return new("Take relic", "Take the relic", true);
                case "key": return new("Take key", "Take the ruin key", true);
            }
        }
        if (LootTargets(a).Any()) return new("Loot", "Search the fallen foe", true);
        if (keyRequired) return new("Need key", "A ruin key is required", false);
        return hint ?? InteractionPreview.None;
    }

    public void Enter(ActorState a, string region)
    {
        Cancel(a);
        if (!RegionsById.ContainsKey(region))
            RegionsById[region] = Mosslight.Regions.Dungeon(Seed ^ 1337);
        a.Region = region;
        var r = RegionsById[region];
        var pos = r.Center(r.Entrance);
        // The entrance tile also owns the return marker. Arrive on nearby
        // reachable floor, clear of that feature and surviving inhabitants.
        // Loading a snapshot never enters here or relocates saved actors.
        if (region == "ruin")
        {
            // Placement only refines arrival; without a clear stand, keep the entrance tile.
            try { pos = r.FindPlacement(pos + new Vector2(RegionState.Tile * 2, RegionState.Tile), a, Actors.Values); }
            catch (InvalidOperationException) { }
        }
        a.X = pos.X;
        a.Y = pos.Y;
        Exploration.PrepareCache(r);
        InitializeContainers(r);
        PopulateRegion(region);
        Events.Publish(new("region_entered", a.Id, "", region));
    }

    void PopulateRegion(string region)
    {
        if (region != "ruin" || RegionsById[region].EncountersInitialized)
            return;
        using var json = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://game/data/spawns.json"));
        var def = json.RootElement.GetProperty(region);
        int count = def.GetProperty("count").GetInt32();
        var archetypes = def.GetProperty("archetypes").EnumerateArray().ToArray();
        // An authored roster fixes the mix; without one the archetypes simply take turns.
        string[] roster = def.TryGetProperty("roster", out var listed) ? listed.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
        var r = RegionsById[region];
        r.EncountersInitialized = true;
        for (int i = 0; i < count; i++)
        {
            var pos = r.Center(r.Objective);
            var archetype = roster.Length > 0
                ? archetypes.First(x => x.GetProperty("profile").GetString() == roster[i % roster.Length])
                : archetypes[(i + 1) % archetypes.Length];
            string combatId = archetype.GetProperty("profile").GetString()!;
            var combat = Content.CombatProfiles.Single(p => p.Id == combatId);
            var actor = new ActorState
            {
                Id = "ruin:spawn:" + i,
                Name = archetype.GetProperty("name").GetString()!,
                CombatProfile = combatId,
                Region = region,
                Profile = "beast",
                Faction = def.GetProperty("faction").GetString()!,
                Appearance = new()
                {
                    Family = archetype.GetProperty("family").GetString()!,
                    Seed = (ulong)(uint)(Seed + i * 271)
                },
                X = pos.X,
                Y = pos.Y,
                HomeX = pos.X,
                HomeY = pos.Y,
                Abilities = combat.Loadout()
            };
            var looking = EnemyMind.Hash01(StableId(actor.Id), Seed, 4) * MathF.Tau;
            actor.FacingX = MathF.Cos(looking); actor.FacingY = MathF.Sin(looking);
            AnatomyAdapter.Apply(actor);
            actor.Speed = archetype.GetProperty("speed").GetSingle();
            actor.Health = actor.MaxHealth = archetype.GetProperty("health").GetSingle();
            Vector2 placed;
            // Seeded floor may lie beyond a locked gate; keep it there. An inhabitant
            // without any clear stand is skipped rather than aborting region entry.
            if (!DungeonEncounters.Place(r, actor, Actors.Values, i, out placed)) continue;
            if (i == 0) actor.Name = "Sanctuary guardian";
            actor.X = actor.HomeX = placed.X; actor.Y = actor.HomeY = placed.Y;
            Actors[actor.Id] = actor;
        }
    }

    public bool SetAppearance(ActorState actor, Appearance appearance)
    {
        // Stage the body and a safe location before changing live state or equipment.
        // A loss of flight/swimming or a larger radius must not strand the actor.
        var staged = new ActorState { Id = actor.Id, Region = actor.Region, Appearance = new Appearance
        {
            Family = appearance.Family, Seed = appearance.Seed,
            GenomeJson = appearance.GenomeJson, Generator = appearance.Generator
        }, Speed = actor.Speed };
        AnatomyAdapter.Apply(staged);
        var region = RegionsById[actor.Region];
        var position = actor.Position;
        if (!region.CanStand(position, staged))
        {
            try { position = region.FindPlacement(position, staged, Actors.Values); }
            catch (InvalidOperationException) { return false; }
        }
        var home = new Vector2(actor.HomeX, actor.HomeY);
        if (!region.CanStand(home, staged)) home = position;

        Cancel(actor);
        actor.Appearance = staged.Appearance;
        actor.Flies = staged.Flies; actor.Swims = staged.Swims; actor.Grasps = staged.Grasps;
        actor.Radius = staged.Radius; actor.NaturalReach = staged.NaturalReach; actor.Speed = staged.Speed;
        actor.X = position.X; actor.Y = position.Y;
        actor.HomeX = home.X; actor.HomeY = home.Y;
        actor.MotionX = actor.MotionY = 0;
        Items.Reconcile(actor);
        Events.Publish(new("appearance_changed", actor.Id, "", appearance.Family));
        return true;
    }

    public bool UsePotion(ActorState actor) => UseItem(actor, "potion");
    public bool UseItem(ActorState actor, string selector)
    {
        if (!actor.Alive || actor.State is not (ActionState.Idle or ActionState.Moving))
            return false;
        float healing = Items.Service.Call("consume", actor.Id, selector).AsSingle();
        if (healing <= 0)
            return false;
        actor.Health = Math.Min(actor.MaxHealth, actor.Health + healing);
        Events.Publish(new("item_used", actor.Id, "", selector));
        return true;
    }

    public void InitializeContainers(RegionState r)
    {
        if (r.ContainersInitialized) return;
        foreach (var (at, kind) in r.Features.OrderBy(x => x.Key))
        {
            if (kind != "chest" || r.Depleted.Contains(at)) continue;
            string owner = r.Id + ":" + at;
            var random = new Random(unchecked(r.Seed ^ at * 7919));
            Items.Add(owner, "potion", 1 + random.Next(2));
            Items.Add(owner, "coin", 4 + random.Next(9));
            Items.Add(owner, new[] { "herb", "ore", "wood" }[random.Next(3)], 2 + random.Next(3));
        }
        r.ContainersInitialized = true;
    }

    public void NewGame()
    {
        RegionsById["reach"] = Mosslight.Regions.Overworld(Seed);
        var r = RegionsById["reach"];
        var pos = r.Center(r.Entrance);
        var player = new ActorState
        {
            Id = "player",
            Name = "Wayfarer",
            X = pos.X,
            Y = pos.Y,
            HomeX = pos.X,
            HomeY = pos.Y,
            Abilities = new()
            {
                "slash",
                "ember",
                "mend"
            }
        };
        AnatomyAdapter.Apply(player);
        Actors.Add(player.Id, player);
        for (int i = 0; i < 8; i++)
        {
            var p = Content.NpcProfiles[i < 2 ? 0 : 1];
            var a = new ActorState
            {
                Id = "npc:" + i,
                Name = i < 2 ? "Reach warden" : "Mire prowler",
                Profile = p.Id,
                Profession = p.Profession,
                Faction = p.Faction,
                X = pos.X + 100 + i * 68,
                Y = pos.Y + (i % 2) * 48,
                Appearance = new()
                {
                    Family = p.Family,
                    Seed = (ulong)(Seed + i * 17)
                },
                Abilities = new()
                {
                    i < 2 ? "slash" : "bite"
                }
            };
            // The Reach's prowlers hunt as a loose pack with the authored senses of their kind.
            if (i >= 2 && FindProfile("mire") is { } mire) { a.CombatProfile = mire.Id; a.Abilities = mire.Loadout(); }
            a.HomeX = a.X;
            a.HomeY = a.Y;
            AnatomyAdapter.Apply(a);
            var placed = r.FindPlacement(a.Position, a, Actors.Values);
            a.X = a.HomeX = placed.X; a.Y = a.HomeY = placed.Y;
            if (i >= 2)
            {
                float looking = EnemyMind.Hash01(StableId(a.Id), Seed, 4) * MathF.Tau;
                a.FacingX = MathF.Cos(looking); a.FacingY = MathF.Sin(looking);
            }
            Actors.Add(a.Id, a);
            if (i < 2)
            {
                Items.Add(a.Id, "iron_sword");
                Items.Equip(a, "iron_sword", "weapon");
                Items.Add(a.Id, "potion", 4);
            }
        }

        Items.Add("player", "iron_sword");
        Items.Equip(player, "iron_sword", "weapon");
        Items.Add("player", "leather_coat");
        Items.Add("player", "potion", 3);
        Items.Add("player", "coin", 20);
        Items.Add("player", "herb", 4);
        InitializeContainers(r);
    }
}

/// <summary>Presentation-only description of the next interaction. Label is short enough for the touch button.</summary>
public sealed record InteractionPreview(string Label, string Reason, bool Available)
{
    public static readonly InteractionPreview None = new("Interact", "Nothing to interact with nearby", false);
}
