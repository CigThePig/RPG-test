using Godot;

namespace Mosslight;

/// <summary>Additive camera offset only; never moves an actor or resets camera smoothing.</summary>
public sealed class CameraFeedback
{
    public float Strength { get; set; } = 1f;
    public Vector2 Offset { get; private set; }
    float remaining;
    const float MaximumOffset = 3.5f;

    public void Impact(float amount, long tick)
    {
        float strength = Math.Clamp(Strength, 0, 1) * Math.Clamp(amount, 0, MaximumOffset);
        // A single decaying nudge, with no rapid oscillation. Accumulation is bounded.
        float angle = (float)((tick * 2.399963229728653) % (Math.PI * 2));
        var impulse = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .55f) * strength;
        Offset = (Offset + impulse).LimitLength(MaximumOffset);
        remaining = Math.Max(remaining, .22f);
    }

    public void Advance(float dt)
    {
        if (remaining <= 0 || Strength <= 0) { Reset(); return; }
        dt = Math.Clamp(dt, 0, .1f);
        remaining = Math.Max(0, remaining - dt);
        Offset *= MathF.Exp(-15f * dt);
        if (remaining <= 0) Offset = Vector2.Zero;
    }

    /// <summary>Keep the additive nudge inside the same projected region bounds as the follow target.</summary>
    public Vector2 Confine(Vector2 center, Vector2 bounds, Vector2 halfViewport)
    {
        float x = bounds.X <= 2 * halfViewport.X ? 0 :
            Mathf.Clamp(center.X + Offset.X, halfViewport.X, bounds.X - halfViewport.X) - center.X;
        float y = bounds.Y <= 2 * halfViewport.Y ? 0 :
            Mathf.Clamp(center.Y + Offset.Y, halfViewport.Y, bounds.Y - halfViewport.Y) - center.Y;
        return new Vector2(x, y);
    }

    public void Reset() { remaining = 0; Offset = Vector2.Zero; }
}
