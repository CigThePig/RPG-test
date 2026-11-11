namespace Mosslight;
/// <summary>Player combat distances and timings, independent of movement-feel presets.</summary>
public static class CombatTuning
{
    public const float SlashArcHalfDegrees = 55f;
    // Gentle directional magnetism, not auto-targeting or a guaranteed hit.
    public const float SlashAssistHalfDegrees = 65f;
    public const float SlashAssistMaximumDegrees = 15f;
    public const float SlashAssistStrength = .85f;
    public const float SlashImpactTolerance = 8f;
    public const float SlashLungeDistance = 12f;
    public const float SlashLungeStart = .08f;
    public const float SlashLungeEnd = .18f;
    public const float DodgeDuration = .40f;
    public const float DodgeDistance = 90f;
    public const float DodgeInvulnerableStart = .10f;
    public const float DodgeInvulnerableEnd = .30f;
    public const float DodgeCooldown = .70f;
    // Enemy hop: shorter than the player's roll, protected for most of its travel.
    public const float EvadeDuration = .30f;
    public const float EvadeInvulnerableStart = 0f;
    public const float EvadeInvulnerableEnd = .22f;
    // At most this many melee enemies press one target; the rest circle to flank.
    public const int MeleeTokens = 2;
    public const float AmbushMultiplier = 1.5f;
    public const float GuardBreakStagger = .70f;
    public const float PoiseRecoverySeconds = 3f;
    // Fraction of maximum health regained per second while walking home.
    public const float ReturnHealing = .12f;
}
