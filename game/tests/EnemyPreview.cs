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

            // Senses: a den with two asleep and one on watch; the cone and earshot rings show what each can perceive.
            cast = Stage(app, "sleepers", (215, -50, -1), (110, 34, 1), (150, 72, 1));
            foreach (var a in cast.Skip(1)) a.Sleeper = true;
            cast[0].NextTacticalTime = w.Time + 5;
            await Wait(app, 70); await Shot(app, "18-sleepers");

            // A roll behind a prowler's back: it heard something and goes to look.
            cast = Stage(app, "prowler", (66, 0, 1));
            cast[0].NextTacticalTime = w.Time + .6;
            await Wait(app, 20);
            w.Cancel(P()); P().Cooldowns.Clear(); w.TryStartPlayerDodge(new V(0, 1));
            await Wait(app, 16); await Shot(app, "19-heard");

            // A slain packmate found: the finder searches and warns the others.
            cast = Stage(app, "pack", (150, 0, -1), (215, 40, 1), (90, 10, -1));
            cast[0].NextTacticalTime = cast[1].NextTacticalTime = w.Time + 3;
            cast[2].Invulnerable = 0; P().X -= 400; w.Damage(P(), cast[2], 10000, "physical", ability: "slash");
            foreach (var a in cast.Take(2)) { EnemyMind.Calm(a); a.Knowledge.Remove(cast[2].Id); a.Cooldowns.Clear(); a.NextTacticalTime = 0; }
            cast[3].Region = "reach";
            P().X += 400; P().Y += 150; app.Views["player"].SnapPosition();
            await Until(app, () => cast[0].Awareness == Awareness.Searching, 300); await Wait(app, 14); await Shot(app, "20-found-body");

            // The shell: turned from the front, open from behind.
            cast = Stage(app, "shellback", (36, 0, -1));
            await Until(app, () => cast[0].Awareness == Awareness.Engaged);
            cast[0].NextTacticalTime = w.Time + 2;
            w.Damage(P(), cast[0], 22, "physical", ability: "slash"); await Wait(app, 8); await Shot(app, "21-shell-blocked");
            P().X = cast[0].X + 34; P().Y = cast[0].Y + 6; app.Views["player"].SnapPosition(); cast[0].NextTacticalTime = w.Time + 2;
            await Wait(app, 14); await Shot(app, "22-shell-flanked");

            // A lobbed glob: warned on the ground while it flies, then a pool.
            cast = Stage(app, "spitter", (150, 0, -1));
            await Until(app, () => cast[0].Awareness == Awareness.Engaged);
            w.Cancel(cast[0]); cast[0].Cooldowns.Clear(); w.Activate(cast[0].Id, "glob", P().Id);
            await Until(app, () => w.Hazards.Count == 1 && w.Hazards[0].Fuse < .4f); await Shot(app, "23-glob-flight");
            await Until(app, () => w.Hazards.Count == 1 && w.Hazards[0].Landed); await Wait(app, 5); await Shot(app, "24-glob-splash");
            await Wait(app, 60); await Shot(app, "25-pool");

            cast = Stage(app, "lasher", (34, 0, -1));
            await Until(app, () => cast[0].PendingAbility == "coil" && cast[0].StateRemaining < .25f, 900); await Shot(app, "26-coil");

            // A mender tending a wounded ally.
            cast = Stage(app, "mender", (130, -40, -1), (60, 20, -1), (70, 60, -1));
            await Until(app, () => cast.All(a => a.Awareness == Awareness.Engaged));
            foreach (var a in cast.Where(a => a.CombatProfile == "prowler")) a.Health = a.MaxHealth * .35f;
            rallied = false; w.Presentation += Heard;
            try { await Until(app, () => rallied, 900); } finally { w.Presentation -= Heard; }
            await Wait(app, 16); await Shot(app, "27-mender-bloom");

            // The alpha falls and the pack breaks.
            cast = Stage(app, "alpha", (60, 0, -1), (80, -40, -1), (85, 40, -1), (110, 0, -1));
            await Until(app, () => cast.All(a => a.Awareness == Awareness.Engaged)); await Wait(app, 30); await Shot(app, "28-alpha-pack");
            var alpha = cast.First(a => a.CombatProfile == "alpha");
            alpha.Invulnerable = 0; w.Damage(P(), alpha, 10000, "physical", ability: "slash");
            await Wait(app, 26); await Shot(app, "29-rout");

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
