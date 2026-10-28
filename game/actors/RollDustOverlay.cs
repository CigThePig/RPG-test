using Godot;

namespace Mosslight;

/// <summary>
/// Bounded procedural cover for the shoulder roll. The gameplay state owns
/// timing, direction and collision; this child only draws translucent particles.
/// </summary>
public partial class RollDustOverlay : Node2D
{
    public bool Active { get; private set; }
    public float Progress { get; private set; }
    public Terrain Surface { get; private set; } = Terrain.Grass;
    Vector2 direction = Vector2.Right;

    public void SetRoll(float progress, Vector2 projectedDirection, Terrain surface)
    {
        Active = true;
        Progress = Math.Clamp(progress, 0, 1);
        Surface = surface;
        if (projectedDirection.LengthSquared() > .00001f) direction = projectedDirection.Normalized();
        Visible = true;
        QueueRedraw();
    }

    public void EndRoll()
    {
        if (!Active) return;
        Active = false;
        Progress = 0;
        Visible = false;
    }

    static float Smooth(float a, float b, float value)
    {
        float t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    static Color Tint(Terrain surface, float alpha, int index)
    {
        Color baseColor = surface switch
        {
            Terrain.Earth => new Color(.64f, .48f, .32f),
            Terrain.Floor => new Color(.57f, .58f, .57f),
            Terrain.Rock or Terrain.Wall => new Color(.56f, .56f, .53f),
            Terrain.Water => new Color(.50f, .73f, .78f),
            _ => new Color(.64f, .64f, .46f)
        };
        float shade = index % 3 == 0 ? 1.13f : index % 3 == 1 ? .90f : 1f;
        return new Color(Math.Min(1, baseColor.R * shade),
            Math.Min(1, baseColor.G * shade), Math.Min(1, baseColor.B * shade), alpha);
    }

    public override void _Draw()
    {
        if (!Active) return;
        float p = Progress;
        Vector2 side = new(-direction.Y, direction.X);
        // A small stream travels behind the feet, not through walls or world space.
        // Particle identities are fixed: no RNG, allocations, textures or physics.
        for (int i = 0; i < 16; i++)
        {
            float age = p - i * .052f;
            if (age < 0 || age >= .32f) continue;
            float life = age / .32f;
            float fade = (1 - life) * (1 - life);
            float scatter = MathF.Sin(i * 2.37f) * (4f + life * 10f);
            Vector2 place = -direction * (4 + life * 17) + side * scatter
                + new Vector2(0, -9f - life * 9f);
            float radius = 2.6f + (i % 4) * .65f + life * 3f;
            DrawCircle(place, radius, Tint(Surface, .43f * fade, i));
        }

        // Takeoff and landing have distinct, short radial puffs; the middle
        // stream softens unstable silhouettes without hiding combat completely.
        float takeoff = 1 - Smooth(.02f, .23f, p);
        float landing = Smooth(.73f, .85f, p) * (1 - Smooth(.94f, 1f, p));
        float burst = Math.Max(takeoff * .34f, landing * .55f);
        for (int i = 0; i < 7 && burst > .001f; i++)
        {
            float angle = i * (MathF.PI * 2 / 7);
            Vector2 offset = new(MathF.Cos(angle), MathF.Sin(angle) * .55f);
            DrawCircle(offset * (6 + 7 * burst) + new Vector2(0, -4), 2.6f + burst * 3,
                Tint(Surface, burst, i + 1));
        }
    }
}
