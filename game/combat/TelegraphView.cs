using Godot;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>One lightweight ground layer; all warnings are projections of current world state.</summary>
public partial class TelegraphView : Node2D
{
    public const float MinimumWindup = .05f;
    public World World { get; set; } = null!;
    public Func<bool>? ReducedMotion { get; set; }
    float clock;
    readonly Vector2[] sector = new Vector2[26];
    readonly Vector2[] outline = new Vector2[27];
    readonly Vector2[] ring = new Vector2[33];
    readonly HashSet<string> markedTargets = new();
    public override void _Process(double delta) { clock += (float)delta; QueueRedraw(); }

    public static bool IsActive(World world, ActorState actor, AbilityDef? ability)
    {
        if (actor.Id == "player" || !actor.Alive || actor.Region != world.Player.Region ||
            actor.State != ActionState.Windup || ability == null || ability.Id != actor.PendingAbility ||
            ability.Windup < MinimumWindup || ability.Telegraph?.Style is not ("target" or "cone" or "line" or "blast" or "burst")) return false;
        // A ring round the body, or round a point already fixed on the ground: nothing more to resolve.
        if (ability.Telegraph.Style is "blast" or "burst") return true;
        if (ability.Telegraph.Style == "target")
            return world.Actors.TryGetValue(actor.Target, out var target) && target.Alive && target.Region == actor.Region;
        return World.Directed(ability) && CombatGeometry.TryNormalize(new V(actor.ActionDirectionX, actor.ActionDirectionY), out _);
    }

    /// <summary>A charge still tearing down its lane: the warning stays until it lands or stops.</summary>
    public static bool IsCharging(World world, ActorState actor, AbilityDef? ability) =>
        actor.Id != "player" && actor.Alive && actor.Region == world.Player.Region && actor.State == ActionState.Recovery &&
        ability?.Charge is { } charge && ability.Id == actor.RecoveryAbility && !actor.ActionConnected &&
        actor.ActionElapsed - ability.Windup < charge.Duration;

    /// <summary>Ground still to be covered by a leap, so its danger zone is drawn where it lands.</summary>
    public static float RemainingLunge(ActorState actor, AbilityDef ability)
    {
        if (ability.Lunge is not { } lunge) return 0;
        float u = Math.Clamp((actor.ActionElapsed - lunge.Start) / (lunge.End - lunge.Start), 0, 1);
        return lunge.Distance * (1 - u * u * (3 - 2 * u));
    }

    public override void _Draw()
    {
        if (World == null || !World.Actors.ContainsKey("player")) return;
        markedTargets.Clear();
        DrawHazards();
        foreach (var actor in World.Actors.Values)
        {
            if (actor.Id == "player") continue;
            if (actor.State == ActionState.Recovery && World.FindAbility(actor.RecoveryAbility) is { } running && IsCharging(World, actor, running))
            { DrawLane(actor, running, 1, (actor.ActionElapsed - running.Windup) * running.Charge!.Speed); continue; }
            if (actor.State != ActionState.Windup || actor.PendingAbility == "") continue;
            var ability = World.FindAbility(actor.PendingAbility);
            if (!IsActive(World, actor, ability)) continue;
            float progress = Math.Clamp(1 - actor.StateRemaining / ability!.Windup, 0, 1);
            if (ability.Telegraph!.Style == "blast")
                DrawRing(actor.Aim, ability.Geometry!.Range, BlastProgress(ability, ability.Windup - actor.StateRemaining), true);
            else if (ability.Telegraph!.Style == "burst") DrawRing(actor.Position, ability.Geometry!.Range, progress, false);
            else if (ability.Telegraph!.Style == "line") DrawLane(actor, ability, progress, 0);
            else if (ability.Telegraph!.Style == "cone") DrawCone(actor, ability.Geometry!, progress, RemainingLunge(actor, ability));
            else DrawTarget(actor, World.Actors[actor.Target], progress, markedTargets.Add(actor.Target));
        }
    }

    static Vector2 Project(V p) => GodotConvert.GroundToScreen(p.X, p.Y);

    /// <summary>How far a lobbed attack is through its whole warning: the windup, then the flight.</summary>
    public static float BlastProgress(AbilityDef ability, float elapsed) =>
        Math.Clamp(elapsed / Math.Max(.01f, ability.Windup + (ability.Blast?.Delay ?? 0)), 0, 1);

    /// <summary>Hazards in the player's region that are warned or lying on the ground right now.</summary>
    public int HazardsDrawn => World == null ? 0 : World.Hazards.Count(h => h.Region == World.Player.Region && World.FindAbility(h.Ability)?.Blast != null);

    // A danger ring on the ground: the full edge from the first moment, a disc that fills it
    // as the moment of impact approaches. A lobbed one also carries a small cross at its centre.
    void DrawRing(V center, float radius, float progress, bool mark)
    {
        for (int i = 0; i <= 32; i++)
        {
            float angle = i * MathF.Tau / 32;
            ring[i] = Project(center + new V(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
        DrawSetTransform(Project(center), 0, new Vector2(1, .5f));
        DrawCircle(Vector2.Zero, radius, new Color(1f, .25f, .12f, .10f + progress * .10f));
        if (progress > .02f) DrawCircle(Vector2.Zero, radius * progress, new Color(1f, .42f, .16f, .16f + progress * .14f));
        DrawSetTransform(Vector2.Zero);
        DrawPolyline(ring, new Color(.18f, .04f, .03f, .9f), 4, true);
        DrawPolyline(ring, new Color(1f, .64f + .2f * progress, .28f, .95f), 2, true);
        if (!mark) return;
        var c = Project(center); var bright = new Color(1, .92f, .58f, .9f);
        DrawLine(c + new Vector2(-5, 0), c + new Vector2(5, 0), bright, 2, true);
        DrawLine(c + new Vector2(0, -2.5f), c + new Vector2(0, 2.5f), bright, 2, true);
    }

    static Color PoolColor(string type) => type switch
    {
        "fire" => new Color(1f, .5f, .18f),
        "poison" => new Color(.6f, .4f, .85f),
        _ => new Color(.62f, .9f, .25f)
    };

    // Lobbed attacks: the landing ring and the thing in the air while the fuse runs, then the pool.
    void DrawHazards()
    {
        string here = World.Player.Region;
        bool still = ReducedMotion?.Invoke() == true;
        foreach (var h in World.Hazards)
        {
            if (h.Region != here || World.FindAbility(h.Ability) is not { Blast: { } blast } ability) continue;
            var color = PoolColor(ability.Type);
            if (!h.Landed)
            {
                DrawRing(h.Position, blast.Radius, BlastProgress(ability, ability.Windup + blast.Delay - h.Fuse), true);
                // The glob itself: a rising and falling arc over its ground shadow.
                float t = Math.Clamp(1 - h.Fuse / Math.Max(.01f, blast.Delay), 0, 1);
                var ground = Project(V.Lerp(new V(h.OriginX, h.OriginY), h.Position, t));
                float height = 46 * 4 * t * (1 - t) + 10 * (1 - t);
                DrawSetTransform(ground, 0, new Vector2(1, .5f));
                DrawCircle(Vector2.Zero, 4, new Color(0, 0, 0, .28f));
                DrawSetTransform(Vector2.Zero);
                DrawCircle(ground + new Vector2(0, -height), 5.5f, new Color(.1f, .14f, .05f, .9f));
                DrawCircle(ground + new Vector2(0, -height), 4, color);
                DrawCircle(ground + new Vector2(-1.2f, -height - 1.2f), 1.4f, new Color(1, 1, .85f, .8f));
                continue;
            }
            if (ability.Pool is not { } pool) continue;
            // A pool: an uneven puddle that bubbles, thins as it dries, and is edged so it reads on any floor.
            float life = Math.Clamp(h.Remaining / .7f, 0, 1) * Math.Clamp((pool.Duration - h.Remaining) / .18f, .35f, 1);
            int key = World.StableId(h.Ability) ^ (int)(h.X * 31) ^ (int)(h.Y * 17);
            DrawSetTransform(Project(h.Position), 0, new Vector2(1, .5f));
            DrawArc(Vector2.Zero, blast.Radius, 0, MathF.Tau, 32, new Color(.08f, .12f, .04f, .7f * life), 3.5f, true);
            DrawCircle(Vector2.Zero, blast.Radius, new Color(color, .3f * life));
            for (int i = 0; i < 5; i++)
            {
                float angle = EnemyMind.Hash01(key, i, 61) * MathF.Tau, reach = blast.Radius * (.25f + .45f * EnemyMind.Hash01(key, i, 62));
                DrawCircle(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach, blast.Radius * (.28f + .2f * EnemyMind.Hash01(key, i, 63)), new Color(color, .22f * life));
            }
            DrawArc(Vector2.Zero, blast.Radius, 0, MathF.Tau, 32, new Color(color, .8f * life), 1.6f, true);
            for (int i = 0; i < 6; i++)
            {
                // Bubbles swell and burst out of step with each other.
                float phase = still ? EnemyMind.Hash01(key, i, 64) : (clock * (.7f + .5f * EnemyMind.Hash01(key, i, 65)) + EnemyMind.Hash01(key, i, 64)) % 1f;
                float angle = EnemyMind.Hash01(key, i, 66) * MathF.Tau, reach = blast.Radius * .8f * MathF.Sqrt(EnemyMind.Hash01(key, i, 67));
                DrawArc(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach, 1 + phase * 3, 0, MathF.Tau, 8, new Color(1, 1, .8f, (1 - phase) * .8f * life), 1.2f, true);
            }
            DrawSetTransform(Vector2.Zero);
        }
    }

    readonly Vector2[] lane = new Vector2[4];
    readonly Vector2[] laneOutline = new Vector2[5];

    // The committed lane of a charge: as wide as the body that will come down it, as long
    // as the run, cut short where terrain will stop it.
    void DrawLane(ActorState actor, AbilityDef ability, float progress, float covered)
    {
        var charge = ability.Charge!;
        var direction = new V(actor.ActionDirectionX, actor.ActionDirectionY);
        var side = new V(-direction.Y, direction.X);
        float length = Math.Max(0, charge.Distance - covered);
        if (World.RegionsById.TryGetValue(actor.Region, out var region))
            for (float d = 8; d <= length; d += 8)
                if (!region.CanStand(actor.Position + direction * d, actor)) { length = Math.Max(0, d - 8); break; }
        if (length < 4) return;
        float half = actor.Radius + charge.Reach + 4;
        var near = actor.Position; var far = actor.Position + direction * (length + actor.Radius);
        lane[0] = Project(near + side * half); lane[1] = Project(far + side * half);
        lane[2] = Project(far - side * half); lane[3] = Project(near - side * half);
        Array.Copy(lane, laneOutline, 4); laneOutline[4] = lane[0];
        DrawColoredPolygon(lane, new Color(1f, .25f, .12f, .12f + progress * .16f));
        DrawPolyline(laneOutline, new Color(.18f, .04f, .03f, .9f), 4, true);
        DrawPolyline(laneOutline, new Color(1f, .64f + .2f * progress, .28f, .95f), 2, true);
        // Chevrons point the way it will come; more of them light as the windup completes.
        int chevrons = Math.Max(1, (int)(length / 34));
        for (int i = 0; i < chevrons; i++)
        {
            float at = (i + .6f) / chevrons * length;
            if ((i + 1f) / chevrons > progress + .001f && covered == 0) break;
            var tip = near + direction * (at + 8);
            var bright = new Color(1, .92f, .58f, .9f);
            DrawLine(Project(tip), Project(tip - direction * 9 + side * half * .55f), bright, 2, true);
            DrawLine(Project(tip), Project(tip - direction * 9 - side * half * .55f), bright, 2, true);
        }
    }

    void DrawCone(ActorState actor, AttackGeometryDef geometry, float progress, float ahead = 0)
    {
        // A leap is drawn at its landing spot, which stays put while the body flies to it.
        var origin = actor.Position + new V(actor.ActionDirectionX, actor.ActionDirectionY) * ahead;
        if (ahead > 2)
            for (int i = 0; i < 4; i += 2)
                DrawLine(Project(V.Lerp(actor.Position, origin, i / 4f)), Project(V.Lerp(actor.Position, origin, (i + 1) / 4f)), new Color(1f, .7f, .3f, .8f), 2, true);
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
