using Godot;

namespace Mosslight;

/// <summary>
/// Marks above a non-player actor that say what it knows and how it stands: a filling "?"
/// while suspicious, a "!" as it raises the alarm, a pale "?" while it searches (with sound
/// arcs when a noise drew it), drifting "z"s while it sleeps, an open eye while it is still on
/// edge, falling drops while its nerve is broken, a guard bar for foes that shrug off blows,
/// circling stars while dazed and a small crest over a pack's leader. Reads actor state only.
/// </summary>
public partial class ActorOverlay : Node2D
{
    public CharacterView View { get; set; } = null!;
    float clock, pop = 10, lost = 10, guardFlash = 10, heard = 10;
    Awareness shown;

    /// <summary>The alarm was just raised: the mark jumps before settling.</summary>
    public void Pop() { pop = 0; QueueRedraw(); }
    /// <summary>A noise just drew its attention: sound arcs beside the mark for a moment.</summary>
    public void Heard() { heard = 0; QueueRedraw(); }
    /// <summary>The guard was just broken: the bar flashes empty.</summary>
    public void GuardBroken() { guardFlash = 0; QueueRedraw(); }

    public bool Showing { get; private set; }
    /// <summary>Which mark is up, for verification: "", "?", "!", "search", "home", "sleep", "wary", "shaken".</summary>
    public string Glyph { get; private set; } = "";
    public bool LeaderVisible { get; private set; }
    public bool HeardVisible => Glyph == "search" && heard < 1.2f;
    public bool GuardVisible { get; private set; }
    public bool DazeVisible { get; private set; }

    public override void _Process(double delta)
    {
        if (View?.State == null || View.Creature.Paused) return;
        float dt = (float)delta;
        clock += dt; pop += dt; lost += dt; guardFlash += dt; heard += dt;
        var a = View.State;
        if (a.Awareness != shown)
        {
            if (a.Awareness == Awareness.Returning) lost = 0;
            shown = a.Awareness;
        }
        string glyph = !a.Alive ? "" : a.Awareness switch
        {
            Awareness.Suspicious => "?",
            Awareness.Alerted => "!",
            Awareness.Engaged => pop < .9f ? "!" : a.Cooldowns.GetValueOrDefault("rout") > 0 ? "shaken" : "",
            Awareness.Searching => "search",
            Awareness.Returning => lost < 1.4f ? "home" : "",
            _ => EnemyMind.Asleep(a) ? "sleep" : a.Cooldowns.GetValueOrDefault("wary") > 0 && a.CombatProfile != "" ? "wary" : ""
        };
        var profile = View.World?.FindProfile(a.CombatProfile);
        bool leader = a.Alive && profile is { Leader: true };
        bool guard = a.Alive && profile is { Poise: > 0 } && a.Awareness is Awareness.Alerted or Awareness.Engaged;
        bool daze = a.Alive && a.State == ActionState.Staggered && a.StateRemaining > .3f;
        bool showing = glyph != "" || guard || daze || leader;
        // Redraw while anything is up, and once more to clear it.
        if (showing || Showing) QueueRedraw();
        Showing = showing; Glyph = glyph; GuardVisible = guard; DazeVisible = daze; LeaderVisible = leader;
        Visible = showing;
    }

    static readonly Color Ink = new(.09f, .07f, .06f, .92f);

    void Question(Vector2 at, Color color, float scale)
    {
        var ink = new Color(Ink, Ink.A * color.A);
        // Hook, stem and dot, drawn twice: a dark underlay then the colour.
        for (int pass = 0; pass < 2; pass++)
        {
            var c = pass == 0 ? ink : color; float w = (pass == 0 ? 4.5f : 2.2f) * scale;
            DrawArc(at + new Vector2(0, -6) * scale, 3.4f * scale, -Mathf.Pi * .95f, Mathf.Pi * .45f, 10, c, w, true);
            DrawLine(at + new Vector2(.8f, -3.4f) * scale, at + new Vector2(0, -.6f) * scale, c, w, true);
            DrawCircle(at + new Vector2(0, 3) * scale, (pass == 0 ? 2.4f : 1.4f) * scale, c);
        }
    }

    void Exclamation(Vector2 at, Color color, float scale)
    {
        var ink = new Color(Ink, Ink.A * color.A);
        for (int pass = 0; pass < 2; pass++)
        {
            var c = pass == 0 ? ink : color; float grow = pass == 0 ? 1.2f : 0;
            DrawRect(new Rect2(at + new Vector2(-1.5f - grow, -10 - grow) * scale, new Vector2(3 + 2 * grow, 9 + 2 * grow) * scale), c);
            DrawCircle(at + new Vector2(0, 3) * scale, (1.7f + grow) * scale, c);
        }
    }

    public override void _Draw()
    {
        if (View?.State == null) return;
        var a = View.State;
        bool still = View.ReducedMotion?.Invoke() == true;
        // Sit just above this particular body, whatever its size.
        float head = Math.Clamp(View.Creature.ContentExtent.Y * View.Creature.Scale.Y * .8f, 12, 52);
        var at = new Vector2(0, -head - 12);
        switch (Glyph)
        {
            case "?":
            {
                // The ring fills as suspicion hardens into certainty.
                var amber = new Color(1f, .82f, .3f);
                DrawArc(at + new Vector2(0, -2), 9, 0, Mathf.Tau, 24, new Color(Ink, .6f), 3.5f, true);
                DrawArc(at + new Vector2(0, -2), 9, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Math.Clamp(a.Alertness, 0, 1), 24, amber, 2, true);
                Question(at, amber, 1);
                break;
            }
            case "!":
            {
                // Overshoot and settle; held still when motion is reduced.
                float t = Math.Clamp(pop / .28f, 0, 1);
                float scale = still ? 1.15f : 1.15f + .75f * MathF.Sin(t * Mathf.Pi) * (1 - t * .4f);
                float lift = still ? 0 : -4 * MathF.Sin(Math.Clamp(pop / .4f, 0, 1) * Mathf.Pi);
                float fade = a.Awareness == Awareness.Engaged ? Math.Clamp((.9f - pop) / .25f, 0, 1) : 1;
                Exclamation(at + new Vector2(0, lift), new Color(1f, .42f, .2f, fade), scale);
                break;
            }
            case "search":
            {
                float bob = still ? 0 : MathF.Sin(clock * 3.2f) * 1.5f;
                var pale = new Color(.78f, .86f, .9f, .9f);
                Question(at + new Vector2(0, bob), pale, .9f);
                if (HeardVisible)
                {
                    // It heard something: arcs of sound spreading from beside the mark.
                    float fade = Math.Clamp(1 - heard / 1.2f, 0, 1);
                    for (int i = 0; i < 3; i++)
                    {
                        float reach = 6 + i * 4 + (still ? 0 : Math.Min(1, heard / .4f) * 2);
                        DrawArc(at + new Vector2(7, -3 + bob), reach, -.75f, .75f, 8, new Color(Ink, fade), 3, true);
                        DrawArc(at + new Vector2(7, -3 + bob), reach, -.75f, .75f, 8, new Color(pale, fade * (1 - i * .25f)), 1.5f, true);
                    }
                }
                break;
            }
            case "sleep":
            {
                // Three letters drifting up and away, each smaller than the last.
                var dusk = new Color(.72f, .8f, 1f);
                for (int i = 0; i < 3; i++)
                {
                    float phase = still ? i / 3f : (clock * .45f + i / 3f) % 1f;
                    float size = 2.2f + 2.6f * (1 - phase);
                    var c = at + new Vector2(4 + phase * 12, 4 - phase * 18);
                    float alpha = MathF.Sin(phase * MathF.PI) * .95f;
                    var points = new[] { c + new Vector2(-size, -size), c + new Vector2(size, -size), c + new Vector2(-size, size), c + new Vector2(size, size) };
                    DrawPolyline(points, new Color(Ink, alpha), 3.2f, true);
                    DrawPolyline(points, new Color(dusk, alpha), 1.5f, true);
                }
                break;
            }
            case "wary":
            {
                // Still on edge: a small open eye.
                var amber = new Color(1f, .82f, .3f, .85f);
                var eye = at + new Vector2(0, -1);
                DrawArc(eye + new Vector2(0, 5), 8, -Mathf.Pi * .78f, -Mathf.Pi * .22f, 8, new Color(Ink, .8f), 3.4f, true);
                DrawArc(eye + new Vector2(0, -5), 8, Mathf.Pi * .22f, Mathf.Pi * .78f, 8, new Color(Ink, .8f), 3.4f, true);
                DrawArc(eye + new Vector2(0, 5), 8, -Mathf.Pi * .78f, -Mathf.Pi * .22f, 8, amber, 1.5f, true);
                DrawArc(eye + new Vector2(0, -5), 8, Mathf.Pi * .22f, Mathf.Pi * .78f, 8, amber, 1.5f, true);
                float look = still ? 0 : MathF.Sin(clock * 1.7f) * 2;
                DrawCircle(eye + new Vector2(look, 0), 1.7f, amber);
                break;
            }
            case "shaken":
            {
                // Nerve gone: drops flying off the head.
                var sweat = new Color(.6f, .82f, 1f);
                for (int i = 0; i < 3; i++)
                {
                    float phase = still ? .3f + i * .2f : (clock * 1.6f + i / 3f) % 1f;
                    var drop = at + new Vector2((i - 1) * 8 + (i - 1) * phase * 5, 2 - MathF.Sin(phase * MathF.PI) * 7 + phase * 6);
                    DrawCircle(drop, 2.6f, new Color(Ink, 1 - phase));
                    DrawCircle(drop, 1.6f, new Color(sweat, 1 - phase));
                }
                break;
            }
            case "home":
            {
                // Giving up: three dots that fade.
                float fade = Math.Clamp(1 - lost / 1.4f, 0, 1);
                for (int i = -1; i <= 1; i++)
                {
                    DrawCircle(at + new Vector2(i * 5, -2), 2.4f, new Color(Ink, fade));
                    DrawCircle(at + new Vector2(i * 5, -2), 1.4f, new Color(.78f, .86f, .9f, fade));
                }
                break;
            }
        }
        if (LeaderVisible)
        {
            // A small three-pointed crest: this one leads.
            var gold = new Color(1f, .8f, .32f);
            var c = new Vector2(-15, -head - 8);
            var crest = new[] { c + new Vector2(-6, 0), c + new Vector2(-6, -5), c + new Vector2(-3, -2), c + new Vector2(0, -6), c + new Vector2(3, -2), c + new Vector2(6, -5), c + new Vector2(6, 0), c + new Vector2(-6, 0) };
            DrawPolyline(crest, Ink, 3.2f, true);
            DrawPolyline(crest, gold, 1.4f, true);
        }
        if (GuardVisible)
        {
            var profile = View.World!.FindProfile(a.CombatProfile)!;
            float fill = a.PoiseDelay <= 0 ? 1 : Math.Clamp(a.Poise / profile.Poise, 0, 1);
            bool flash = guardFlash < .35f;
            DrawRect(new Rect2(-15, -46, 30, 4), Ink);
            DrawRect(new Rect2(-14, -45, 28 * (flash ? 1 : fill), 2), flash ? new Color(1, 1, 1, 1 - guardFlash / .35f) : new Color(.62f, .8f, .95f));
        }
        if (DazeVisible)
        {
            // Small stars circling the head.
            for (int i = 0; i < 3; i++)
            {
                float angle = (still ? 0 : clock * 5f) + i * Mathf.Tau / 3;
                var star = new Vector2(MathF.Cos(angle) * 10, -54 + MathF.Sin(angle) * 3.5f);
                var c = new Color(1f, .93f, .55f, MathF.Sin(angle) > -.2f ? 1 : .45f);
                DrawLine(star + new Vector2(-2.5f, 0), star + new Vector2(2.5f, 0), Ink, 3);
                DrawLine(star + new Vector2(0, -2.5f), star + new Vector2(0, 2.5f), Ink, 3);
                DrawLine(star + new Vector2(-2, 0), star + new Vector2(2, 0), c, 1.4f);
                DrawLine(star + new Vector2(0, -2), star + new Vector2(0, 2), c, 1.4f);
            }
        }
    }
}
