using System.Numerics;

namespace Mosslight;

public enum CombatPresentationKind
{
    AttackStarted, AttackCommitted, AttackImpact, ProjectileReleased, ProjectileImpact,
    Hit, Miss, AttackCanceled, ActionCompleted, DefensiveAvoidance, DodgeStarted, DodgeCanceled, Death
}

/// <summary>Immutable cosmetic observation; no subscriber owns gameplay outcomes.</summary>
public readonly record struct CombatPresentationEvent(
    CombatPresentationKind Kind, string ActorId, string TargetId, string AbilityId,
    long Tick, Vector2 Origin, Vector2 Position, Vector2 Direction, string Outcome = "");
