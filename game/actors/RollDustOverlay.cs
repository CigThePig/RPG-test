using Godot;

namespace Mosslight;

/// <summary>
/// Screen-space drawing of world-anchored procedural dust. This is presentation
/// only: roll distance, timing, hit checks and collision stay in World.
/// </summary>
public partial class RollDustOverlay : Node2D
{
    const float TrailSpacing = 11f; // Ground pixels, independent of frame rate
    const int MaximumPuffs = 64;
    sealed class Puff
    {
        public Vector2 Anchor;
        public Vector2 Drift;
        public float Age, Lifetime, Radius, Opacity;
        public int Shape;
        public Terrain Surface;
    }

    readonly List<Puff> puffs = new(MaximumPuffs);
    Vector2 currentScreen, previousGround;
    Vector2 direction = Vector2.Right;
    float trailRemainder, previousProgress;
    int emitted;
    bool wasRolling;

    public bool Active => puffs.Count > 0;
    public bool IsRolling => wasRolling;
    public int PuffCount => puffs.Count;
    public float Progress { get; private set; }
    public Terrain Surface { get; private set; } = Terrain.Grass;
    public Vector2? OldestPuffScreenPosition => Active ? puffs[0].Anchor : null;
    public bool HasLandingCloud { get; private set; }

    public override void _Ready() => Visible = false;

    public void Reset()
    {
        puffs.Clear();
        wasRolling = false;
        trailRemainder = 0;
        emitted = 0;
        Progress = previousProgress = 0;
        HasLandingCloud = false;
        Visible = false;
    }

    /// <summary>Update each visual sync, including during post-roll dissipation.</summary>
    public void Advance(float dt, bool rolling, float progress, Vector2 groundPosition,
        Vector2 groundDirection, Terrain terrain)
    {
        if (!float.IsFinite(dt) || dt < 0) dt = 0;
        dt = Math.Min(dt, .1f);
        currentScreen = PixelCreatures.Runtime.GodotConvert.GroundToScreen(groundPosition.X, groundPosition.Y);
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            puffs[i].Age += dt;
            if (puffs[i].Age >= puffs[i].Lifetime) puffs.RemoveAt(i);
        }

        if (rolling)
        {
            Surface = terrain;
            Progress = Math.Clamp(progress, 0, 1);
            if (groundDirection.LengthSquared() > .001f)
                direction = PixelCreatures.Runtime.GodotConvert.GroundToScreen(
                    groundDirection.X, groundDirection.Y).Normalized();
            if (!wasRolling)
            {
                emitted = 0;
                trailRemainder = 0;
                HasLandingCloud = false;
                previousGround = groundPosition;
                Emit(currentScreen, terrain, 7, 14f, .55f, .56f, 0);
            }
            else
            {
                float distance = groundPosition.DistanceTo(previousGround);
                // Don't stretch clouds across teleports or restored locations.
                if (distance > 0 && distance < 32f)
                {
                    float cursor = TrailSpacing - trailRemainder;
                    while (cursor <= distance)
                    {
                        Vector2 at = previousGround.Lerp(groundPosition, cursor / distance);
                        Emit(PixelCreatures.Runtime.GodotConvert.GroundToScreen(at.X, at.Y),
                            terrain, 3, 12.5f, .48f, .50f, 1);
                        cursor += TrailSpacing;
                    }
                    trailRemainder = (trailRemainder + distance) % TrailSpacing;
                }
                else if (distance >= 32f) trailRemainder = 0;
            }
            previousGround = groundPosition;
            previousProgress = Progress;
        }
        else if (wasRolling)
        {
            // A completed 24-tick roll has last-seen progress about 23/24.
            // An interrupted early/late roll must not produce a fake landing.
            if (previousProgress >= .9f)
            {
                Emit(currentScreen, Surface, 9, 17f, .59f, .64f, 2);
                HasLandingCloud = true;
            }
            previousProgress = Progress = 0;
        }
        wasRolling = rolling;
        Visible = Active;
        if (Active) QueueRedraw();
    }

    void Emit(Vector2 at, Terrain terrain, int count, float size,
        float lifetime, float opacity, int type)
    {
        for (int i = 0; i < count; i++)
        {
            if (puffs.Count >= MaximumPuffs) puffs.RemoveAt(0);
            // Deterministic irregular clusters, not glitter or per-frame RNG.
            int index = emitted++;
            float phase = index * 2.3999632f;
            float radial = (i % 4 + 1) / 4f;
            Vector2 side = new(-direction.Y, direction.X);
            float across = MathF.Sin(phase) * (type == 1 ? 12f : 18f) * radial;
            float backward = (type == 1 ? -11f : 0f) + MathF.Cos(phase) * (5 + 9 * radial);
            Vector2 origin = at + side * across + direction * backward
                + new Vector2(0, -14f + MathF.Sin(phase * .7f) * 5);
            puffs.Add(new Puff
            {
                Anchor = origin,
                Drift = side * (MathF.Sin(phase * 1.13f) * (8 + 7 * radial))
                    - direction * (6 + 8 * radial) + new Vector2(0, -7f),
                Lifetime = Math.Min(.66f, lifetime + (index % 3) * .035f),
                Radius = size * (.68f + (i % 5) * .14f),
                Opacity = opacity * (.75f + (i % 3) * .125f),
                Shape = index,
                Surface = terrain
            });
        }
    }

    static float Fade(float age)
    {
        float t = Math.Clamp(age, 0, 1);
        if (t < .18f) return 1f;
        float f = 1 - (t - .18f) / .82f;
        return f * f * (3 - 2 * f);
    }

    static Color DustColor(Terrain terrain, int variation, float opacity)
    {
        // Brighter earth tones that read against noisy green grass on a phone.
        Color color = terrain switch
        {
            Terrain.Grass => new Color(.78f, .75f, .59f),
            Terrain.Earth => new Color(.86f, .71f, .52f),
            Terrain.Floor => new Color(.77f, .76f, .68f),
            Terrain.Rock or Terrain.Wall => new Color(.79f, .77f, .72f),
            Terrain.Water => new Color(.66f, .84f, .91f),
            _ => new Color(.78f, .75f, .59f)
        };
        float shade = variation % 3 == 0 ? 1.05f : variation % 3 == 1 ? .93f : 1f;
        return new Color(Math.Min(1f, color.R * shade), Math.Min(1f, color.G * shade),
            Math.Min(1f, color.B * shade), opacity);
    }

    public override void _Draw()
    {
        // Large overlapping silhouettes convey a dust cloud, with a soft edge
        // and occasional sizable debris. Every anchor stays in world space.
        foreach (var puff in puffs)
        {
            float age = puff.Age / puff.Lifetime;
            float fade = Fade(age);
            if (fade <= .002f) continue;
            Vector2 pos = puff.Anchor - currentScreen + puff.Drift * age;
            float radius = puff.Radius * (1f + .4f * age);
            float alpha = puff.Opacity * fade;
            DrawCircle(pos, radius * 1.32f, DustColor(puff.Surface, puff.Shape, alpha * .19f));
            DrawCircle(pos, radius * .93f, DustColor(puff.Surface, puff.Shape + 1, alpha * .45f));
            DrawCircle(pos + new Vector2(-radius * .2f, -radius * .16f),
                radius * .62f, DustColor(puff.Surface, puff.Shape + 2, alpha * .3f));
            if (puff.Shape % 3 == 0 && age < .65f)
                DrawCircle(pos + new Vector2(radius * .9f, -radius * .45f),
                    2.8f + age * 1.2f, DustColor(puff.Surface, puff.Shape, alpha * .6f));
        }
    }
}
