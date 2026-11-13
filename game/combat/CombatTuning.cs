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
    // Sound. A quarry moving slower than SneakPace of its top speed, and not acting, is quiet:
    // it is heard only inside QuietHearing of a listener's hearing distance.
    public const float SneakPace = .6f;
    public const float QuietHearing = .45f;
    // How far the noise of each kind of event carries; walls muffle it.
    public const float FightNoise = 170f;
    public const float SwingNoise = 55f;
    public const float RollNoise = 70f;
    public const float CrashNoise = 230f;
    public const float MechanismNoise = 180f;
    public const float MuffledNoise = .6f;
    // Sleepers: hear half as far, take a long moment to rouse, and are struck twice as hard.
    public const float SleepingHearing = .5f;
    public const float WakeSeconds = .9f;
    public const float SleepingMultiplier = 2f;
    // After a hunt or a disturbing find an actor stays up and notices faster for this long.
    public const float WarySeconds = 25f;
    public const float WaryNotice = .6f;
    // Broken nerve: seconds on the run after an ally, or the pack's leader, falls in view.
    public const float RoutSeconds = 3.5f;
    public const float LeaderRoutSeconds = 5.5f;
    // A routed actor runs this far from its quarry and no further.
    public const float RoutDistance = 170f;
    // A slow turner only starts an attack on something this close to dead ahead.
    public const float TurnAttackHalfAngle = 55f;
}
