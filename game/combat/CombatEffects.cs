using Godot;
using PixelCreatures.Runtime;
using Ground = System.Numerics.Vector2;

namespace Mosslight;

public enum CombatEffectKind { Hit, HeavyHit, Whiff, Dodge, Interrupt, ProjectileRelease, ProjectileCollision, Death, Periodic, TerrainCollision }

/// <summary>One bounded world-space draw layer. Fragments are derived from the event seed, not stored as nodes.</summary>
public partial class CombatEffects : Node2D
{
    public const int Capacity = 48;
    public const int MaxFragmentsPerEffect = 7;
    struct Effect
    {
        public bool Active;
        public CombatEffectKind Kind;
        public Vector2 Anchor, Direction;
        public float Age, Lifetime;
        public uint Seed;
        public int Priority;
    }
    readonly Effect[] effects = new Effect[Capacity];
    public int ActiveCount { get; private set; }
    public int EmittedCount { get; private set; }
    public int DroppedCount { get; private set; }
    public Vector2 LastAnchor { get; private set; }
    public CombatEffectKind LastKind { get; private set; }

    static int Priority(CombatEffectKind kind) => kind switch
    {
        CombatEffectKind.Death or CombatEffectKind.HeavyHit => 3,
        CombatEffectKind.Hit or CombatEffectKind.Dodge or CombatEffectKind.ProjectileCollision => 2,
        _ => 1
    };
    static float Duration(CombatEffectKind kind) => kind switch
    {
        CombatEffectKind.Death => .64f,
        CombatEffectKind.HeavyHit => .46f,
        CombatEffectKind.Hit => .34f,
        CombatEffectKind.Periodic => .26f,
        _ => .32f
    };

    public bool Emit(CombatEffectKind kind, Ground position, Ground direction, long tick, string actorId, string targetId = "")
    {
        int priority = Priority(kind), slot = -1, replacement = -1;
        float oldest = -1;
        for (int i = 0; i < effects.Length; i++)
        {
            if (!effects[i].Active) { slot = i; break; }
            if (effects[i].Priority < priority && effects[i].Age > oldest) { replacement = i; oldest = effects[i].Age; }
        }
        if (slot < 0) slot = replacement;
        if (slot < 0) { DroppedCount++; return false; }
        if (!effects[slot].Active) ActiveCount++;
        var projected = GodotConvert.GroundToScreen(position.X, position.Y);
        var d = GodotConvert.GroundToScreen(direction.X, direction.Y);
        if (d.LengthSquared() < .0001f) d = Vector2.Right;
        else d = d.Normalized();
        uint seed = unchecked((uint)World.StableId(actorId) ^ (uint)World.StableId(targetId) * 3266489917u ^ (uint)tick * 2654435761u ^ (uint)kind * 2246822519u);
        effects[slot] = new Effect { Active = true, Kind = kind, Anchor = projected, Direction = d,
            Lifetime = Duration(kind), Seed = seed, Priority = priority };
        LastAnchor = projected; LastKind = kind; EmittedCount++;
        QueueRedraw();
        return true;
    }

    public void Advance(float dt)
    {
        dt = Math.Clamp(dt, 0, .1f);
        bool changed = false;
        for (int i = 0; i < effects.Length; i++)
        {
            if (!effects[i].Active) continue;
            effects[i].Age += dt; changed = true;
            if (effects[i].Age >= effects[i].Lifetime) { effects[i].Active = false; ActiveCount--; }
        }
        if (changed) QueueRedraw();
    }

    public void Reset()
    {
        Array.Clear(effects, 0, effects.Length);
        ActiveCount = 0;
        QueueRedraw();
    }

    static float Variation(uint seed, int index)
    {
        uint n = unchecked(seed + (uint)index * 747796405u);
        n = unchecked((n ^ (n >> 16)) * 2246822519u);
        return (n & 65535) / 65535f;
    }
    public override void _Draw()
    {
        for (int i = 0; i < effects.Length; i++)
        {
            var e = effects[i];
            if (!e.Active) continue;
            float t = Math.Clamp(e.Age / e.Lifetime, 0, 1);
            float fade = (1 - t) * (1 - t);
            Vector2 at = e.Anchor + new Vector2(0, e.Kind == CombatEffectKind.Death ? -7 : -12);
            Color c = e.Kind switch
            {
                CombatEffectKind.Dodge => new Color(.54f, .9f, .91f),
                CombatEffectKind.ProjectileRelease => new Color(1f, .72f, .26f),
                CombatEffectKind.ProjectileCollision => new Color(1f, .43f, .16f),
                CombatEffectKind.TerrainCollision => new Color(.65f, .72f, .58f),
                CombatEffectKind.Death => new Color(.78f, .70f, .58f),
                CombatEffectKind.Interrupt => new Color(.95f, .75f, .48f),
                CombatEffectKind.Periodic => new Color(1f, .43f, .18f),
                _ => new Color(1f, .9f, .60f)
            };
            c.A = fade * .85f;
            if (e.Kind is CombatEffectKind.Whiff or CombatEffectKind.Dodge or CombatEffectKind.ProjectileRelease or CombatEffectKind.ProjectileCollision or CombatEffectKind.Death)
            {
                float radius = (e.Kind == CombatEffectKind.Death ? 10 : 7) + t * (e.Kind == CombatEffectKind.Death ? 24 : 17);
                if (e.Kind == CombatEffectKind.Whiff)
                    DrawArc(at, radius + 7, e.Direction.Angle() - .65f, e.Direction.Angle() + .65f, 12, c, 2.5f);
                else
                    DrawArc(at, radius, 0, Mathf.Tau, 20, c, e.Kind == CombatEffectKind.Dodge ? 3 : 2);
            }
            int fragments = e.Kind switch
            {
                CombatEffectKind.HeavyHit => 7, CombatEffectKind.Hit => 6,
                CombatEffectKind.Death => 6, CombatEffectKind.ProjectileCollision => 5,
                CombatEffectKind.Interrupt => 4, CombatEffectKind.Periodic => 3, _ => 2
            };
            for (int j = 0; j < fragments; j++)
            {
                float angle = Variation(e.Seed, j) * Mathf.Tau;
                var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .65f);
                float distance = 6 + t * (17 + Variation(e.Seed, j + 11) * 15);
                var start = at + radial * distance;
                if (e.Kind is CombatEffectKind.Hit or CombatEffectKind.HeavyHit or CombatEffectKind.ProjectileCollision)
                    DrawLine(start, start + radial * (7 + 8 * (1 - t)), c, e.Kind == CombatEffectKind.HeavyHit ? 3.5f : 2.5f);
                else
                    DrawCircle(start, 2.5f * (1 - t) + 1, c);
            }
            if (e.Kind is CombatEffectKind.Hit or CombatEffectKind.HeavyHit && t < .3f)
            {
                var flash = new Color(1, 1, .88f, (1 - t / .3f) * .8f);
                DrawCircle(at, e.Kind == CombatEffectKind.HeavyHit ? 6 : 4, flash);
            }
        }
    }
}
