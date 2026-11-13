using System.Numerics;

namespace Mosslight;

public enum CombatPresentationKind
{
    AttackStarted, AttackCommitted, AttackImpact, ProjectileReleased, ProjectileImpact,
    Hit, Miss, AttackCanceled, ActionCompleted, DefensiveAvoidance, DodgeStarted, DodgeCanceled, Death,
    // Enemy behaviour beats. Outcome carries the cause ("sighted", "called", "hurt", "lost", "home").
    Alerted, AwarenessChanged, GuardBroken, ChargeStarted, ChargeCrashed, Rallied,
    // Heard: went to look at a noise (Position is where). Disturbed: found, or was warned of, a
    // fallen ally. Woke: roused from sleep. Shaken: nerve broken ("leader" or "loss").
    Heard, Disturbed, Woke, Shaken,
    // A lobbed attack left the hand (Position is where it will land), and landed.
    BlastLaunched, BlastLanded
}

/// <summary>Immutable cosmetic observation; no subscriber owns gameplay outcomes.</summary>
public readonly record struct CombatPresentationEvent(
    CombatPresentationKind Kind, string ActorId, string TargetId, string AbilityId,
    long Tick, Vector2 Origin, Vector2 Position, Vector2 Direction, string Outcome = "");
