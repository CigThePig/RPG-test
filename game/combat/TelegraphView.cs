using Godot;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>One lightweight ground layer; all warnings are projections of current world state.</summary>
public partial class TelegraphView : Node2D
{
    public const float MinimumWindup = .05f;
    public World World { get; set; } = null!;
    readonly Vector2[] sector = new Vector2[26];
    readonly Vector2[] outline = new Vector2[27];
    readonly Vector2[] ring = new Vector2[33];
    readonly HashSet<string> markedTargets = new();
    public override void _Process(double delta) => QueueRedraw();

    public static bool IsActive(World world, ActorState actor, AbilityDef? ability)
    {
        if (actor.Id == "player" || !actor.Alive || actor.Region != world.Player.Region ||
            actor.State != ActionState.Windup || ability == null || ability.Id != actor.PendingAbility ||
            ability.Windup < MinimumWindup || ability.Telegraph?.Style is not ("target" or "cone")) return false;
        if (ability.Telegraph.Style == "target")
            return world.Actors.TryGetValue(actor.Target, out var target) && target.Alive && target.Region == actor.Region;
        return ability.Geometry?.Shape == "cone" && CombatGeometry.TryNormalize(new V(actor.ActionDirectionX, actor.ActionDirectionY), out _);
    }

    public override void _Draw()
    {
        if (World == null || !World.Actors.ContainsKey("player")) return;
        markedTargets.Clear();
        foreach (var actor in World.Actors.Values)
        {
            if (actor.Id == "player" || actor.State != ActionState.Windup || actor.PendingAbility == "") continue;
            var ability = World.FindAbility(actor.PendingAbility);
            if (!IsActive(World, actor, ability)) continue;
            float progress = Math.Clamp(1 - actor.StateRemaining / ability!.Windup, 0, 1);
            if (ability.Telegraph!.Style == "cone") DrawCone(actor, ability.Geometry!, progress);
            else DrawTarget(actor, World.Actors[actor.Target], progress, markedTargets.Add(actor.Target));
        }
    }

    static Vector2 Project(V p) => GodotConvert.GroundToScreen(p.X, p.Y);

    void DrawCone(ActorState actor, AttackGeometryDef geometry, float progress)
    {
        var origin = actor.Position;
        float heading = MathF.Atan2(actor.ActionDirectionY, actor.ActionDirectionX);
        float half = geometry.HalfAngleDegrees * MathF.PI / 180;
        sector[0] = Project(origin);
        for (int i = 0; i <= 24; i++)
        {
            float angle = heading - half + 2 * half * i / 24;
            sector[i + 1] = Project(origin + new V(MathF.Cos(angle), MathF.Sin(angle)) * geometry.Range);
        }
        Array.Copy(sector, outline, sector.Length);
        outline[26] = sector[0];
        DrawColoredPolygon(sector, new Color(1f, .25f, .12f, .12f + progress * .16f));
        DrawPolyline(outline, new Color(.18f, .04f, .03f, .9f), 4, true);
        DrawPolyline(outline, new Color(1f, .64f + .2f * progress, .28f, .95f), 2, true);
        // The full edge stays visible; an inner radial stroke grows toward impact.
        var tip = origin + new V(MathF.Cos(heading), MathF.Sin(heading)) * geometry.Range * progress;
        DrawLine(Project(origin), Project(tip), new Color(1, .92f, .58f, .9f), 2, true);
    }

    void DrawGroundRing(V center, float radius, Color dark, Color bright)
    {
        for (int i = 0; i <= 32; i++)
        {
            float angle = i * MathF.Tau / 32;
            ring[i] = Project(center + new V(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
        DrawPolyline(ring, dark, 4, true);
        DrawPolyline(ring, bright, 2, true);
    }

    void DrawTarget(ActorState actor, ActorState target, float progress, bool drawTarget)
    {
        var dark = new Color(.15f, .04f, .03f, .9f);
        var bright = new Color(1f, .65f + .2f * progress, .3f, .8f + .2f * progress);
        DrawGroundRing(actor.Position, actor.Radius + 8, dark, bright);
        if (drawTarget) DrawGroundRing(target.Position, target.Radius + 8, dark, bright);
        // A dashed tether and tracking marker communicate a designated target,
        // not a static area that can be escaped merely by crossing a line.
        var a = Project(actor.Position); var b = Project(target.Position);
        // The overhead accent remains readable when close bodies hide the tether.
        var alert = a + new Vector2(0, -45);
        DrawCircle(alert, 6, dark);
        DrawLine(alert + new Vector2(0, -4), alert + new Vector2(0, 1), bright, 2, true);
        DrawCircle(alert + new Vector2(0, 4), 1.5f, bright);
        for (int i = 0; i < 6; i += 2)
            DrawLine(a.Lerp(b, i / 6f), a.Lerp(b, (i + 1) / 6f), bright, 2, true);
        if (drawTarget)
        {
            float tick = 3 + 5 * progress;
            DrawLine(b + new Vector2(-tick, 0), b + new Vector2(tick, 0), bright, 2, true);
            DrawLine(b + new Vector2(0, -tick * .5f), b + new Vector2(0, tick * .5f), bright, 2, true);
        }
    }
}
