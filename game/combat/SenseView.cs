using Godot;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>
/// What nearby enemies can perceive, drawn faintly on the ground while the player is still
/// undetected: the fan each one sees (stopped by walls), the distance at which it would hear
/// the player right now (which shrinks when the player slows down), and, for a hunter that has
/// lost its quarry, the spot it is going to search. A projection of world state; it changes nothing.
/// </summary>
public partial class SenseView : Node2D
{
    public const float Range = 330;
    const int Rays = 14;
    public World World { get; set; } = null!;
    public Func<bool>? ReducedMotion { get; set; }
    readonly Vector2[] fan = new Vector2[Rays + 2];
    readonly Vector2[] edge = new Vector2[Rays + 3];
    readonly Vector2[] ring = new Vector2[33];
    float clock;
    /// <summary>How many of each mark the current world state calls for.</summary>
    public int ConesDrawn { get; private set; }
    public int EarshotDrawn { get; private set; }
    public int TrailsDrawn { get; private set; }

    public override void _Process(double delta) { clock += (float)delta; Survey(); QueueRedraw(); }

    bool Near(ActorState a, ActorState player) => a.Id != "player" && a.Alive && a.Region == player.Region && World.Hostile(a, player) &&
        V.Distance(a.Position, player.Position) <= Range;
    static bool ShowsEarshot(ActorState a, CombatProfileDef p) => a.Awareness is Awareness.Unaware or Awareness.Suspicious && !(p.Ambusher && a.Awareness == Awareness.Unaware);

    /// <summary>Count what is shown, from the same rules the drawing follows.</summary>
    public void Survey()
    {
        ConesDrawn = EarshotDrawn = TrailsDrawn = 0;
        if (World == null || !World.Actors.TryGetValue("player", out var player) || !player.Alive) return;
        foreach (var a in World.Actors.Values)
        {
            if (!Near(a, player)) continue;
            var p = World.FindProfile(a.CombatProfile) ?? CombatProfileDef.Legacy;
            if (ShowsTrail(World, a)) TrailsDrawn++;
            if (!ShowsEarshot(a, p)) continue;
            EarshotDrawn++;
            if (ShowsSight(World, a, p)) ConesDrawn++;
        }
    }

    static Vector2 Project(V p) => GodotConvert.GroundToScreen(p.X, p.Y);

    /// <summary>Is this actor's sight worth showing: awake, not lying in ambush, and not yet certain of anything.</summary>
    public static bool ShowsSight(World w, ActorState a, CombatProfileDef p) =>
        a.Awareness is Awareness.Unaware or Awareness.Suspicious && !EnemyMind.Asleep(a) && !(p.Ambusher && a.Awareness == Awareness.Unaware) &&
        (a.FacingX != 0 || a.FacingY != 0);

    /// <summary>Is this actor heading for a place rather than a quarry it can see?</summary>
    public static bool ShowsTrail(World w, ActorState a) =>
        (a.Awareness == Awareness.Searching || (a.Awareness == Awareness.Engaged && a.TacticalIntent == "pursue")) && V.Distance(a.Position, a.LastKnown) > 20;

    public override void _Draw()
    {
        if (World == null || !World.Actors.TryGetValue("player", out var player) || !player.Alive || !World.RegionsById.TryGetValue(player.Region, out var region)) return;
        bool still = ReducedMotion?.Invoke() == true;
        foreach (var a in World.Actors.Values)
        {
            if (!Near(a, player)) continue;
            float distance = V.Distance(a.Position, player.Position);
            var p = World.FindProfile(a.CombatProfile) ?? CombatProfileDef.Legacy;
            float near = Math.Clamp((Range - distance) / 90, 0, 1);
            if (ShowsTrail(World, a)) DrawTrail(a, near, still);
            if (!ShowsEarshot(a, p)) continue;
            // How close the player could come, moving as they are now, before being heard.
            float earshot = EnemyMind.Earshot(World, a, player, p);
            bool danger = distance < earshot * 1.25f;
            DrawRing(a.Position, earshot, new Color(danger ? new Color(1f, .78f, .35f) : new Color(.8f, .9f, 1f), (danger ? .55f : .3f) * near));
            if (!ShowsSight(World, a, p)) continue;
            float heading = MathF.Atan2(a.FacingY, a.FacingX), half = Math.Min(p.SightHalfAngle, 170) * MathF.PI / 180;
            fan[0] = Project(a.Position);
            for (int i = 0; i <= Rays; i++)
            {
                float angle = heading - half + 2 * half * i / Rays;
                var direction = new V(MathF.Cos(angle), MathF.Sin(angle));
                // Sight stops at the first thing that blocks it.
                float reach = 12;
                for (float d = 20; d <= p.Sight; d += 8)
                {
                    if (region.BlocksSight(region.Index(a.Position + direction * d))) break;
                    reach = d;
                }
                fan[i + 1] = Project(a.Position + direction * Math.Min(reach + 4, p.Sight));
            }
            bool suspicious = a.Awareness == Awareness.Suspicious, wary = a.Cooldowns.GetValueOrDefault("wary") > 0;
            var tint = suspicious || wary ? new Color(1f, .8f, .35f) : new Color(.92f, .96f, 1f);
            float strength = suspicious ? .09f + .12f * Math.Clamp(a.Alertness, 0, 1) : wary ? .075f : .055f;
            DrawColoredPolygon(fan, new Color(tint, strength * near));
            Array.Copy(fan, edge, fan.Length); edge[^1] = fan[0];
            DrawPolyline(edge, new Color(tint, (suspicious ? .5f : .22f) * near), 1.2f, true);
        }
    }

    void DrawRing(V center, float radius, Color color)
    {
        if (radius < 4) return;
        for (int i = 0; i <= 32; i++)
        {
            float angle = i * MathF.Tau / 32;
            ring[i] = Project(center + new V(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
        // Dashed, so it never reads as a danger ring.
        for (int i = 0; i < 32; i += 2) DrawLine(ring[i], ring[i + 1], color, 1.4f, true);
    }

    // Where it thinks the quarry went: a footprint-sized mark and a faint thread leading to it.
    void DrawTrail(ActorState a, float near, bool still)
    {
        var pale = new Color(.78f, .86f, .9f);
        var from = Project(a.Position); var to = Project(a.LastKnown);
        for (int i = 1; i < 9; i += 2) DrawLine(from.Lerp(to, i / 9f), from.Lerp(to, (i + 1) / 9f), new Color(pale, .28f * near), 1.2f, true);
        float pulse = still ? .7f : .55f + .3f * MathF.Sin(clock * 4);
        DrawSetTransform(to, 0, new Vector2(1, .5f));
        DrawArc(Vector2.Zero, 9, 0, MathF.Tau, 20, new Color(.09f, .07f, .06f, .6f * near), 3.2f, true);
        DrawArc(Vector2.Zero, 9, 0, MathF.Tau, 20, new Color(pale, pulse * near), 1.5f, true);
        DrawSetTransform(Vector2.Zero);
        DrawLine(to + new Vector2(-3, -1.5f), to + new Vector2(3, 1.5f), new Color(pale, pulse * near), 1.5f, true);
        DrawLine(to + new Vector2(-3, 1.5f), to + new Vector2(3, -1.5f), new Color(pale, pulse * near), 1.5f, true);
    }
}
