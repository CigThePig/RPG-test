using Godot;

namespace Mosslight;

/// <summary>How much the enemies near the player know, summed up over the player's own head.</summary>
public enum Exposure
{
    /// <summary>Nothing hostile close enough to matter.</summary>
    Clear,
    /// <summary>Something is near and has noticed nothing.</summary>
    Unseen,
    /// <summary>Something is growing suspicious.</summary>
    Suspected,
    /// <summary>Something is searching for whatever disturbed it.</summary>
    Sought,
    /// <summary>Something has recognised the player.</summary>
    Hunted
}

/// <summary>
/// A small eye above the player: shut while unseen, opening as suspicion fills, pale and
/// scanning while something searches, red for a moment when the player is recognised.
/// Reads world state only.
/// </summary>
public partial class StealthMark : Node2D
{
    public CharacterView View { get; set; } = null!;
    public Exposure Exposure { get; private set; }
    public float Suspicion { get; private set; }
    float clock, since = 10;
    Exposure shown;

    public static (Exposure Level, float Suspicion) Read(World w)
    {
        var level = Exposure.Clear; float suspicion = 0;
        if (!w.Actors.TryGetValue("player", out var player) || !player.Alive) return (level, 0);
        foreach (var a in w.Actors.Values)
        {
            if (a.Id == "player" || !a.Alive || a.Region != player.Region || !w.Hostile(a, player)) continue;
            float distance = System.Numerics.Vector2.Distance(a.Position, player.Position);
            var p = w.FindProfile(a.CombatProfile) ?? CombatProfileDef.Legacy;
            var mine = a.Awareness switch
            {
                Awareness.Alerted or Awareness.Engaged => a.Target == player.Id ? Exposure.Hunted : Exposure.Clear,
                Awareness.Searching => distance < 260 ? Exposure.Sought : Exposure.Clear,
                Awareness.Suspicious => Exposure.Suspected,
                // A lurking ambusher gives nothing away, not even that it is there.
                _ => distance < p.Sight * 1.35f && !(p.Ambusher && a.Awareness == Awareness.Unaware) ? Exposure.Unseen : Exposure.Clear
            };
            if (mine == Exposure.Suspected) suspicion = Math.Max(suspicion, a.Alertness);
            if (mine > level) level = mine;
        }
        return (level, suspicion);
    }

    public override void _Process(double delta)
    {
        if (View?.World == null || View.Creature.Paused) return;
        clock += (float)delta; since += (float)delta;
        var (level, suspicion) = Read(View.World);
        if (level != shown) { shown = level; since = 0; }
        Exposure = level; Suspicion = suspicion;
        // Once the fight is on, the eye has said its piece.
        bool visible = level != Exposure.Clear && !(level == Exposure.Hunted && since > 1.3f);
        if (visible || Visible) QueueRedraw();
        Visible = visible;
    }

    public override void _Draw()
    {
        if (View?.State == null || Exposure == Exposure.Clear) return;
        bool still = View.ReducedMotion?.Invoke() == true;
        float head = Math.Clamp(View.Creature.ContentExtent.Y * View.Creature.Scale.Y * .8f, 12, 52);
        var at = new Vector2(0, -head - 14);
        var ink = new Color(.09f, .07f, .06f, .9f);
        float open = Exposure switch { Exposure.Unseen => 0, Exposure.Suspected => .25f + .75f * Math.Clamp(Suspicion, 0, 1), _ => 1 };
        var color = Exposure switch
        {
            Exposure.Unseen => new Color(.82f, .9f, .96f, .8f),
            Exposure.Suspected => new Color(1f, .82f, .3f),
            Exposure.Sought => new Color(.78f, .86f, .9f),
            _ => new Color(1f, .4f, .22f, Math.Clamp((1.3f - since) / .3f, 0, 1))
        };
        ink.A *= color.A;
        float pop = still ? 1 : 1 + .5f * MathF.Sin(Math.Clamp(since / .25f, 0, 1) * MathF.PI);
        const float width = 9;
        // Lids: two shallow arcs that part as the eye opens. Shut, they are one line with lashes.
        float lift = 4.5f * open * pop;
        var upper = new Vector2[9]; var lower = new Vector2[9];
        for (int i = 0; i < 9; i++)
        {
            float u = i / 8f * 2 - 1, bow = 1 - u * u;
            upper[i] = at + new Vector2(u * width * pop, -lift * bow);
            lower[i] = at + new Vector2(u * width * pop, (open > 0 ? lift * .8f : 2.2f) * bow);
        }
        DrawPolyline(upper, ink, 4, true); DrawPolyline(lower, ink, 4, true);
        DrawPolyline(upper, color, 1.8f, true); DrawPolyline(lower, color, 1.8f, true);
        if (open <= 0)
        {
            for (int i = -1; i <= 1; i++) DrawLine(at + new Vector2(i * 4.5f, 2.2f - Math.Abs(i) * .7f), at + new Vector2(i * 5.5f, 5 - Math.Abs(i) * .7f), color, 1.4f, true);
            return;
        }
        float look = Exposure == Exposure.Sought && !still ? MathF.Sin(clock * 2.4f) * 3.5f : 0;
        DrawCircle(at + new Vector2(look, 0), Math.Max(1.2f, 2.6f * open), color);
    }
}
