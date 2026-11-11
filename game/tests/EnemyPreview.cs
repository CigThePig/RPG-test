using Godot;
using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>
/// Fast review captures of enemy behaviour in the production renderer. Development only:
/// <c>godot --path . -- --visual-check --enemy-preview</c> stages each beat in the combat lab,
/// lets the real simulation reach it, and writes build/enemy-preview/*.png. It asserts nothing
/// and restores the session afterwards.
/// </summary>
public static class EnemyPreview
{
    const string Folder = "build/enemy-preview";

    static async System.Threading.Tasks.Task Shot(Foundation app, string name)
    {
        await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = app.GetViewport().GetTexture().GetImage();
        image.SavePng($"{Folder}/{name}.png");
        // A second, closer crop around the player makes the small marks legible in review.
        var size = image.GetSize();
        var focus = app.Views["player"].GetGlobalTransformWithCanvas().Origin * (size.X / app.GetViewportRect().Size.X);
        int left = Math.Clamp((int)focus.X - 260, 0, size.X - 520), top = Math.Clamp((int)focus.Y - 210, 0, size.Y - 380);
        using var crop = image.GetRegion(new Rect2I(left, top, 520, 380));
        crop.Resize(1040, 760, Image.Interpolation.Nearest);
        crop.SavePng($"{Folder}/{name}-close.png");
        GD.Print("ENEMY PREVIEW " + name);
    }

    static async System.Threading.Tasks.Task<bool> Until(Foundation app, Func<bool> reached, int frames = 600)
    {
        for (int i = 0; i < frames; i++)
        {
            if (reached()) return true;
            await app.ToSignal(app.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        GD.Print("ENEMY PREVIEW beat not reached");
        return false;
    }

    static async System.Threading.Tasks.Task Wait(Foundation app, int frames)
    {
        for (int i = 0; i < frames; i++) await app.ToSignal(app.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>A fresh lab holding only the named kinds, each posted relative to the player.</summary>
    static ActorState[] Stage(Foundation app, string encounter, params (float X, float Y, float FaceX)[] posts)
    {
        app.BeginCombatCourse(encounter, true);
        var w = app.World; var p = w.Player;
        var cast = w.Actors.Values.Where(a => a.Id.StartsWith("lab:")).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
        for (int i = 0; i < cast.Length && i < posts.Length; i++)
            EnemyMind.Resettle(cast[i], new V(p.X + posts[i].X, p.Y + posts[i].Y), new V(posts[i].FaceX, 0));
        foreach (var view in app.Views.Values) view.SnapPosition();
        return cast;
    }

    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        Directory.CreateDirectory(Folder);
        var w = app.World; string snapshot = Persistence.Capture(w, app.Narrative); var size = app.GetWindow().Size;
        try
        {
            app.GetWindow().Size = new Vector2I(1280, 800);
            app.ResumeGame();
            // Each stage restores the lab, which replaces the actor objects: always read the live player.
            ActorState P() => w.Player;

            // Noticing: suspicion, the alarm, and the packmate it calls.
            var cast = Stage(app, "two melee", (150, 0, -1), (175, 60, 1));
            await Until(app, () => cast.Any(a => a.Awareness == Awareness.Suspicious)); await Wait(app, 6); await Shot(app, "01-suspicious");
            await Until(app, () => cast.Any(a => a.Awareness == Awareness.Alerted)); await Wait(app, 8); await Shot(app, "02-alarm");

            // A pack: two press, the rest circle.
            cast = Stage(app, "pack", (120, -30, -1), (140, 20, -1), (150, 60, -1), (170, -60, -1));
            await Until(app, () => cast.Count(a => a.TacticalIntent == "flank") >= 2 && cast.Any(a => a.State == ActionState.Windup), 900); await Shot(app, "03-pack-flank");

            cast = Stage(app, "prowler", (78, 0, -1));
            await Until(app, () => cast[0].PendingAbility == "pounce" && cast[0].StateRemaining < .34f); await Shot(app, "04-pounce-telegraph");
            await Until(app, () => cast[0].PendingAbility == "pounce" && cast[0].StateRemaining < .1f); await Shot(app, "05-pounce-air");

            cast = Stage(app, "brute", (150, 0, -1));
            await Until(app, () => cast[0].PendingAbility == "charge" && cast[0].StateRemaining < .3f); await Shot(app, "06-charge-telegraph");
            await Until(app, () => app.Views[cast[0].Id].Charging); await Wait(app, 8); await Shot(app, "07-charge-run");

            // Guard: one blow glances, the next breaks it.
            cast = Stage(app, "brute", (34, 0, -1));
            await Until(app, () => cast[0].State == ActionState.Windup);
            w.Damage(P(), cast[0], 22, "physical", ability: "slash"); await Wait(app, 6); await Shot(app, "08-guarded");
            cast[0].Invulnerable = 0; w.Damage(P(), cast[0], 30, "physical", ability: "slash"); await Wait(app, 9); await Shot(app, "09-guard-break");
            cast[0].Health = cast[0].MaxHealth * .3f;
            await Until(app, () => cast[0].Statuses.Any(s => s.Id == "rage"), 900); await Wait(app, 30); await Shot(app, "10-enraged");

            cast = Stage(app, "stalker", (130, 20, 1));
            await Wait(app, 40); await Shot(app, "11-stalker-dormant");
            P().X += 70;
            await Until(app, () => cast[0].State == ActionState.Windup); await Shot(app, "12-stalker-sting");

            cast = Stage(app, "howler", (110, -40, -1), (90, 40, -1));
            bool rallied = false;
            void Heard(CombatPresentationEvent e) { rallied |= e.Kind == CombatPresentationKind.Rallied; }
            w.Presentation += Heard;
            try { await Until(app, () => rallied, 900); } finally { w.Presentation -= Heard; }
            await Wait(app, 14); await Shot(app, "13-howler-rally");

            cast = Stage(app, "spitter", (120, 0, -1));
            await Until(app, () => w.Projectiles.Count(x => x.Ability == "volley") == 3, 900); await Wait(app, 10); await Shot(app, "14-volley");

            // Death, mid-fall and settled.
            cast = Stage(app, "four mixed", (60, -20, -1), (70, 40, -1), (-60, 30, 1), (-70, -30, 1));
            await Wait(app, 50);
            foreach (var a in cast) { a.Invulnerable = 0; w.Damage(P(), a, 10000, "physical", ability: "slash"); }
            await Wait(app, 22); await Shot(app, "15-death-falling");
            await Wait(app, 80); await Shot(app, "16-death-settled");

            // Losing the quarry: the player vanishes beyond the lab's dividing wall.
            cast = Stage(app, "prowler", (90, 0, -1));
            await Until(app, () => cast[0].Awareness == Awareness.Engaged);
            P().X = 1000; P().Y = 300;
            await Until(app, () => cast[0].Awareness == Awareness.Searching, 900);
            // Look at the searcher, not the hidden player.
            P().X = cast[0].X + 60; P().Y = cast[0].Y + 200; P().Invulnerable = 1000; cast[0].Cooldowns["leash"] = 100;
            app.Views["player"].SnapPosition(); await Wait(app, 40); await Shot(app, "17-searching");
        }
        finally
        {
            app.GetWindow().Size = size;
            if (app.InFeelCourse) app.LeaveFeelCourse();
            Persistence.Restore(w, app.Narrative, snapshot); app.RebuildViews(); app.ResumeGame();
        }
    }
}
