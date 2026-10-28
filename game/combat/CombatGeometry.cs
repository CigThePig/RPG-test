using System.Numerics;

namespace Mosslight;

/// <summary>Ground-space geometry. Boundaries and the origin are included.</summary>
public static class CombatGeometry
{
    public static bool TryNormalize(Vector2 value, out Vector2 direction)
    {
        direction = Vector2.Zero;
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) return false;
        double length = Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y);
        if (length < .001 || !double.IsFinite(length)) return false;
        direction = new((float)(value.X / length), (float)(value.Y / length));
        return true;
    }

    public static bool InCircle(Vector2 origin, Vector2 point, float radius)
    {
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(point.X) ||
            !float.IsFinite(point.Y) || !float.IsFinite(radius) || radius < 0) return false;
        double x = (double)point.X - origin.X, y = (double)point.Y - origin.Y;
        return x * x + y * y <= (double)radius * radius;
    }

    public static bool InCone(Vector2 origin, Vector2 point, Vector2 direction, float radius, float halfAngleDegrees)
    {
        if (!InCircle(origin, point, radius) || !float.IsFinite(halfAngleDegrees) ||
            halfAngleDegrees < 0 || halfAngleDegrees > 180 || !TryNormalize(direction, out var facing)) return false;
        // The point at the apex has no angle, and is included like the player slash.
        double x = (double)point.X - origin.X, y = (double)point.Y - origin.Y;
        double length = Math.Sqrt(x * x + y * y);
        if (length < .000001) return true;
        double dot = (x * facing.X + y * facing.Y) / length;
        return dot >= Math.Cos(halfAngleDegrees * Math.PI / 180.0) - 1e-7;
    }
}
