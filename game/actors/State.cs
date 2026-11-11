using System.Numerics;
using MHLab.Spells;

namespace Mosslight;
public enum ActionState
{
    Idle = 0,
    Moving = 1,
    Windup = 2,
    Recovery = 3,
    Staggered = 4,
    Dead = 5,
    Dodging = 6
}

/// <summary>What a non-player actor currently knows about its quarry. Saved; default is legacy-compatible.</summary>
public enum Awareness
{
    Unaware = 0,
    Suspicious = 1,
    Alerted = 2,
    Engaged = 3,
    Searching = 4,
    Returning = 5
}

public sealed class Appearance
{
    public string Family { get; set; } = "biped";
    public ulong Seed { get; set; } = 42;
    public string GenomeJson { get; set; } = "";
    public string Generator { get; set; } = "ppc-a2207c4";
}

public sealed class StatusState
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public float Remaining { get; set; }
    public float UntilTick { get; set; }
    public int Stacks { get; set; } = 1;
}

public sealed class ActorState : ISpellCaster, ISpellTarget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Faction { get; set; } = "reach";
    public string Region { get; set; } = "reach";
    public string Profile { get; set; } = "";
    public string CombatProfile { get; set; } = "";
    public string TacticalIntent { get; set; } = "";
    public double NextTacticalTime { get; set; }
    public string Profession { get; set; } = "";
    public Appearance Appearance { get; set; } = new();
    public float X { get; set; }
    public float Y { get; set; }
    public float MotionX { get; set; }
    public float MotionY { get; set; }
    public float HomeX { get; set; }
    public float HomeY { get; set; }
    public float Health { get; set; } = 100;
    public float Mana { get; set; } = 60;
    public float MaxHealth { get; set; } = 100;
    public float MaxMana { get; set; } = 60;
    public float Strength { get; set; } = 6;
    public float Defence { get; set; } = 1;
    public float Speed { get; set; } = 72;
    public float Radius { get; set; } = 9;
    public float NaturalReach { get; set; }
    public bool Flies { get; set; }
    public bool Swims { get; set; }
    public bool Grasps { get; set; } = true;
    public int SkillPoints { get; set; }
    public Dictionary<string, int> SkillRanks { get; set; } = new();
    public int Experience { get; set; }
    public int Level { get; set; } = 1;
    public string Activity { get; set; } = "patrol";
    public ActionState State { get; set; }
    public float StateRemaining { get; set; }
    public float Invulnerable { get; set; }
    public List<string> Abilities { get; set; } = new();
    public Dictionary<string, float> Cooldowns { get; set; } = new();
    public List<StatusState> Statuses { get; set; } = new();
    public Dictionary<string, int> Reputation { get; set; } = new();
    public Dictionary<string, string> Knowledge { get; set; } = new();
    public Dictionary<string, float> Relationships { get; set; } = new();
    public string Target { get; set; } = "";
    public string PendingAbility { get; set; } = "";
    // Optional schema-1 field: identifies the action after impact until recovery ends.
    public string RecoveryAbility { get; set; } = "";
    // Optional schema-1 fields. The locked direction belongs to the authoritative action.
    public bool FreeSlash { get; set; }
    public float ActionElapsed { get; set; }
    public float ActionDirectionX { get; set; }
    public float ActionDirectionY { get; set; }
    // Optional schema-1 perception and tactics fields. Defaults describe an actor that
    // has noticed nothing, so older saves load as a calm world.
    public Awareness Awareness { get; set; }
    /// <summary>Detection meter, 0..1. Reaching 1 turns suspicion into alarm.</summary>
    public float Alertness { get; set; }
    /// <summary>Seconds left in the current alarm beat or search.</summary>
    public float AwarenessRemaining { get; set; }
    public float LastKnownX { get; set; }
    public float LastKnownY { get; set; }
    public double LastSeenTime { get; set; }
    /// <summary>Authoritative ground facing; zero means "not yet established" and sees all around.</summary>
    public float FacingX { get; set; }
    public float FacingY { get; set; }
    /// <summary>Remaining guard for profiles with poise; a hit that leaves some does not stagger.</summary>
    public float Poise { get; set; }
    public float PoiseDelay { get; set; }
    /// <summary>Alarmed by an ally's call rather than its own senses; such actors do not call again.</summary>
    public bool Called { get; set; }
    /// <summary>The current charge or lunge has already landed its one hit.</summary>
    public bool ActionConnected { get; set; }
    public Vector2 Position => new(X, Y);
    public Vector2 Facing => new(FacingX, FacingY);
    public Vector2 LastKnown => new(LastKnownX, LastKnownY);
    public Vector2 Home => new(HomeX, HomeY);
    public bool Alive => Health > 0;
}

public sealed record WorldEvent(string Kind, string Actor, string Target, string Value, int Count = 1);
public sealed class EventStream
{
    public event Action<WorldEvent>? Published;
    public List<WorldEvent> Recent { get; set; } = new();

    public void Publish(WorldEvent e)
    {
        Recent.Add(e);
        if (Recent.Count > 100)
            Recent.RemoveAt(0);
        Published?.Invoke(e);
    }
}
