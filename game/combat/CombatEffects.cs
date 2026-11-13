using Godot;
using PixelCreatures.Runtime;
using Ground = System.Numerics.Vector2;

namespace Mosslight;

public enum CombatEffectKind { Hit, HeavyHit, Whiff, Dodge, Interrupt, ProjectileRelease, ProjectileCollision, Death, Periodic, TerrainCollision, Alert, GuardBreak, Crash, Rally, Guarded, Blocked, Splash, Ripple, Mend, Shaken, Word }

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
        public float Age, Lifetime, Size;
        public uint Seed;
        public int Priority;
        public string Text;
        public Color Tint;
    }
    readonly Effect[] effects = new Effect[Capacity];
    public int ActiveCount { get; private set; }
    public int EmittedCount { get; private set; }
    public int DroppedCount { get; private set; }
    public Vector2 LastAnchor { get; private set; }
    public CombatEffectKind LastKind { get; private set; }

    static int Priority(CombatEffectKind kind) => kind switch
    {
        CombatEffectKind.Death or CombatEffectKind.HeavyHit or CombatEffectKind.GuardBreak or CombatEffectKind.Crash or CombatEffectKind.Splash => 3,
        CombatEffectKind.Hit or CombatEffectKind.Dodge or CombatEffectKind.ProjectileCollision or CombatEffectKind.Alert or CombatEffectKind.Rally or
            CombatEffectKind.Blocked or CombatEffectKind.Word or CombatEffectKind.Mend => 2,
        _ => 1
    };
    static float Duration(CombatEffectKind kind) => kind switch
    {
        CombatEffectKind.Death => .64f,
        CombatEffectKind.HeavyHit => .46f,
        CombatEffectKind.Hit => .34f,
        CombatEffectKind.Periodic => .26f,
        CombatEffectKind.Rally => .7f,
        CombatEffectKind.GuardBreak or CombatEffectKind.Crash => .5f,
        CombatEffectKind.Alert => .4f,
        CombatEffectKind.Guarded => .22f,
        CombatEffectKind.Blocked => .3f,
        CombatEffectKind.Splash => .55f,
        CombatEffectKind.Ripple => .8f,
        CombatEffectKind.Mend => .9f,
        CombatEffectKind.Shaken => .5f,
        CombatEffectKind.Word => .95f,
        _ => .32f
    };

    /// <summary>A short call-out that rises from a point and fades: what just happened, in a word.</summary>
    public bool Say(string text, Ground position, long tick, string actorId, Color tint)
    {
        if (!Emit(CombatEffectKind.Word, position, Ground.Zero, tick, actorId, text)) return false;
        effects[lastSlot].Text = text; effects[lastSlot].Tint = tint; LastWord = text;
        return true;
    }
    public string LastWord { get; private set; } = "";
    int lastSlot;

    public bool Emit(CombatEffectKind kind, Ground position, Ground direction, long tick, string actorId, string targetId = "", float size = 0)
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
            Lifetime = Duration(kind), Seed = seed, Priority = priority, Size = size, Text = "" };
        lastSlot = slot;
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
    // Behaviour beats: the alarm, a broken guard, a charge meeting a wall, a rallying cry.
    void DrawBehaviour(Effect e, float t, float fade)
    {
        switch (e.Kind)
        {
            case CombatEffectKind.Alert:
            {
                // Sharp ticks fanning out above the head.
                var head = e.Anchor + new Vector2(0, -58);
                var c = new Color(1f, .86f, .35f, fade);
                for (int j = 0; j < 5; j++)
                {
                    float angle = -Mathf.Pi / 2 + (j - 2) * .5f;
                    var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    DrawLine(head + ray * (11 + t * 9), head + ray * (17 + t * 13), c, 2.5f);
                }
                break;
            }
            case CombatEffectKind.GuardBreak:
            {
                var at = e.Anchor + new Vector2(0, -14);
                var c = new Color(.75f, .9f, 1f, fade);
                DrawArc(at, 9 + t * 22, 0, Mathf.Tau, 20, new Color(c, fade * .7f), 3);
                for (int j = 0; j < 7; j++)
                {
                    float angle = Variation(e.Seed, j) * Mathf.Tau;
                    var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .7f);
                    var start = at + radial * (8 + t * (24 + Variation(e.Seed, j + 7) * 14));
                    // Shards: short thick slivers turning as they fly.
                    var across = new Vector2(-radial.Y, radial.X) * (3 - 2 * t);
                    DrawLine(start - across, start + across + radial * 4, c, 3);
                }
                if (t < .3f) DrawCircle(at, 7, new Color(1, 1, 1, (1 - t / .3f) * .85f));
                break;
            }
            case CombatEffectKind.Crash:
            {
                var at = e.Anchor + new Vector2(0, -10);
                var dust = new Color(.8f, .76f, .64f, fade * .8f);
                for (int j = 0; j < 6; j++)
                {
                    float angle = Variation(e.Seed, j) * Mathf.Tau;
                    var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .6f);
                    DrawCircle(at + radial * (6 + t * 22), 5 * (1 - t) + 2, dust);
                }
                var spark = new Color(1f, .95f, .7f, fade);
                for (int j = 0; j < 4; j++)
                {
                    float angle = -Mathf.Pi / 2 + (Variation(e.Seed, j + 20) - .5f) * 2.4f;
                    var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    DrawLine(at + ray * (8 + t * 14), at + ray * (13 + t * 20), spark, 2.5f);
                }
                break;
            }
            case CombatEffectKind.Rally:
            {
                // Two rings sweep out over the ground to the edge of the cry.
                var c = new Color(1f, .82f, .3f);
                DrawSetTransform(e.Anchor, 0, new Vector2(1, .5f));
                for (int ring = 0; ring < 2; ring++)
                {
                    float u = Math.Clamp(t * 1.25f - ring * .22f, 0, 1);
                    if (u <= 0) continue;
                    float eased = 1 - (1 - u) * (1 - u);
                    DrawArc(Vector2.Zero, 14 + eased * Math.Max(20, e.Size - 14), 0, Mathf.Tau, 40, new Color(c, (1 - u) * .8f), 3 - ring);
                }
                DrawSetTransform(Vector2.Zero);
                break;
            }
            case CombatEffectKind.Blocked:
            {
                // Turned on a hard front: a bright curved flash across the blow and sparks thrown back along it.
                var at = e.Anchor + new Vector2(0, -12);
                float facing = e.Direction.Angle();
                var steel = new Color(.82f, .93f, 1f, fade);
                DrawArc(at, 10 + t * 5, facing + Mathf.Pi - .9f, facing + Mathf.Pi + .9f, 10, new Color(.07f, .1f, .14f, fade), 5, true);
                DrawArc(at, 10 + t * 5, facing + Mathf.Pi - .9f, facing + Mathf.Pi + .9f, 10, steel, 2.5f, true);
                for (int j = 0; j < 5; j++)
                {
                    float angle = facing + Mathf.Pi + (Variation(e.Seed, j) - .5f) * 1.8f;
                    var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .7f);
                    DrawLine(at + ray * (9 + t * 16), at + ray * (13 + t * 24), new Color(1f, .96f, .7f, fade), 2);
                }
                break;
            }
            case CombatEffectKind.Splash:
            {
                // A lobbed glob bursting on the ground: a ring to its edge and droplets thrown up.
                var c = new Color(.72f, .95f, .3f);
                float radius = Math.Max(16, e.Size);
                float eased = 1 - (1 - t) * (1 - t);
                DrawSetTransform(e.Anchor, 0, new Vector2(1, .5f));
                DrawArc(Vector2.Zero, radius * (.35f + .65f * eased), 0, Mathf.Tau, 32, new Color(c, fade * .9f), 3.5f, true);
                if (t < .25f) DrawCircle(Vector2.Zero, radius * .6f, new Color(1, 1, .8f, (1 - t / .25f) * .5f));
                DrawSetTransform(Vector2.Zero);
                for (int j = 0; j < 7; j++)
                {
                    float angle = Variation(e.Seed, j) * Mathf.Tau, throwOut = radius * (.3f + .6f * Variation(e.Seed, j + 9)) * eased;
                    float rise = (14 + 14 * Variation(e.Seed, j + 17)) * MathF.Sin(t * Mathf.Pi);
                    DrawCircle(e.Anchor + new Vector2(MathF.Cos(angle) * throwOut, MathF.Sin(angle) * throwOut * .5f - rise), 2.6f * (1 - t) + 1, new Color(c, fade + .15f));
                }
                break;
            }
            case CombatEffectKind.Ripple:
            {
                // A noise that something heard: thin rings spreading over the ground from where it was.
                var c = new Color(.8f, .9f, 1f);
                DrawSetTransform(e.Anchor, 0, new Vector2(1, .5f));
                for (int ring = 0; ring < 3; ring++)
                {
                    float u = Math.Clamp(t * 1.3f - ring * .2f, 0, 1);
                    if (u <= 0) continue;
                    DrawArc(Vector2.Zero, 6 + u * 26, 0, Mathf.Tau, 24, new Color(c, (1 - u) * .55f), 1.6f, true);
                }
                DrawSetTransform(Vector2.Zero);
                break;
            }
            case CombatEffectKind.Mend:
            {
                // Green crosses rising off the body.
                var c = new Color(.5f, 1f, .62f);
                for (int j = 0; j < 5; j++)
                {
                    float u = Math.Clamp(t * 1.4f - Variation(e.Seed, j) * .4f, 0, 1);
                    if (u <= 0 || u >= 1) continue;
                    var at = e.Anchor + new Vector2((Variation(e.Seed, j + 5) - .5f) * 26, -8 - u * 34);
                    float arm = 3.2f * (1 - u * .4f);
                    DrawLine(at + new Vector2(-arm, 0), at + new Vector2(arm, 0), new Color(.05f, .16f, .08f, (1 - u) * .9f), 3.6f);
                    DrawLine(at + new Vector2(0, -arm), at + new Vector2(0, arm), new Color(.05f, .16f, .08f, (1 - u) * .9f), 3.6f);
                    DrawLine(at + new Vector2(-arm, 0), at + new Vector2(arm, 0), new Color(c, 1 - u), 1.8f);
                    DrawLine(at + new Vector2(0, -arm), at + new Vector2(0, arm), new Color(c, 1 - u), 1.8f);
                }
                break;
            }
            case CombatEffectKind.Shaken:
            {
                // A jagged shiver over the head.
                var c = new Color(.6f, .82f, 1f, fade);
                var head = e.Anchor + new Vector2(0, -50);
                for (int j = 0; j < 3; j++)
                {
                    float x = (j - 1) * 9;
                    DrawPolyline(new[] { head + new Vector2(x - 2, -6 - t * 8), head + new Vector2(x + 2, -2 - t * 8), head + new Vector2(x - 2, 2 - t * 8), head + new Vector2(x + 2, 6 - t * 8) }, c, 1.8f);
                }
                break;
            }
            case CombatEffectKind.Word:
            {
                if (string.IsNullOrEmpty(e.Text)) break;
                var font = ThemeDB.FallbackFont;
                const int size = 11;
                float rise = 1 - (1 - t) * (1 - t);
                float alpha = t < .7f ? 1 : 1 - (t - .7f) / .3f;
                // A quick overshoot as it appears, then it drifts up.
                float scale = t < .14f ? .6f + .6f * (t / .14f) : 1.2f - .2f * Math.Clamp((t - .14f) / .12f, 0, 1);
                float width = font.GetStringSize(e.Text, HorizontalAlignment.Left, -1, size).X;
                DrawSetTransform(e.Anchor + new Vector2(0, -62 - rise * 16), 0, new Vector2(scale, scale));
                var origin = new Vector2(-width / 2, 0);
                DrawStringOutline(font, origin, e.Text, HorizontalAlignment.Left, -1, size, 4, new Color(.06f, .05f, .05f, alpha * .9f));
                DrawString(font, origin, e.Text, HorizontalAlignment.Left, -1, size, new Color(e.Tint, alpha));
                DrawSetTransform(Vector2.Zero);
                break;
            }
            case CombatEffectKind.Guarded:
            {
                var c = new Color(.72f, .82f, .92f, fade);
                var at = e.Anchor + new Vector2(0, -12);
                for (int j = 0; j < 3; j++)
                {
                    float angle = Variation(e.Seed, j) * Mathf.Tau;
                    var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .65f);
                    DrawLine(at + radial * (4 + t * 8), at + radial * (8 + t * 12), c, 2);
                }
                break;
            }
        }
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
            if (e.Kind is CombatEffectKind.Alert or CombatEffectKind.GuardBreak or CombatEffectKind.Crash or CombatEffectKind.Rally or CombatEffectKind.Guarded or
                CombatEffectKind.Blocked or CombatEffectKind.Splash or CombatEffectKind.Ripple or CombatEffectKind.Mend or CombatEffectKind.Shaken or CombatEffectKind.Word)
            { DrawBehaviour(e, t, fade); continue; }
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
