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
    // Optional schema-1 fields. The locked direction belongs to the authoritative action.
    public bool FreeSlash { get; set; }
    public float ActionElapsed { get; set; }
    public float ActionDirectionX { get; set; }
    public float ActionDirectionY { get; set; }
    public Vector2 Position => new(X, Y);
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
