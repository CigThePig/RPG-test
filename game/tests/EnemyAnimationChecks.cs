using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Motion;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>
/// The motor's posture, reaction and death layers for every body plan, and the view's reading
/// of actor state into them. Runs without a display: poses are compared, not pixels.
/// </summary>
public static class EnemyAnimationChecks
{
    static CreatureMotor Motor(string family, ulong seed)
    {
        var model = CreatureRuntime.Build(CreatureRuntime.Sample(family, seed));
        var motor = new CreatureMotor(model, CreatureRuntime.Registry, new MotorOptions { InternalDrive = false, FaceVelocity = false });
        motor.SetWorldPosition(Vec3.Zero, Vec3.Zero);
        return motor;
    }

    static void Advance(CreatureMotor motor, double seconds)
    {
        for (int i = 0; i < (int)Math.Round(seconds * 60); i++) motor.Step(FixedClock.DefaultStep);
    }

    static bool Finite(CreatureMotor motor) => motor.Pose.Skeleton.World.All(x => double.IsFinite(x.Origin.X) && double.IsFinite(x.Origin.Y) && double.IsFinite(x.Origin.Z));

    // Total distance between two poses' bone positions: zero means visually identical.
    static double Difference(Vec3[] a, CreatureMotor b) => a.Zip(b.Pose.Skeleton.World, (x, y) => (x - y.Origin).Length).Sum();
    static Vec3[] Bones(CreatureMotor motor) => motor.Pose.Skeleton.World.Select(x => x.Origin).ToArray();

    public static void Run(Foundation app, Action<bool, string> check)
    {
        foreach (var (label, family, seed) in EnemyAnimationPreview.Subjects)
        {
            // Stances: each settles to full weight, changes the pose, and relaxes back out.
            var relaxed = Motor(family, seed); Advance(relaxed, 1);
            foreach (var stance in Enum.GetValues<StanceKind>().Where(k => k != StanceKind.Relaxed))
            {
                var motor = Motor(family, seed);
                motor.Stance = stance; Advance(motor, 1);
                check(motor.StanceWeight(stance) == 1 && Finite(motor) && (Difference(Bones(relaxed), motor) > .5 || motor.Params.Mood != relaxed.Params.Mood),
                    $"{label}: {stance} stance settles into a distinct posture");
                motor.Stance = StanceKind.Relaxed; Advance(motor, 1.5);
                check(motor.StanceWeight(stance) == 0, $"{label}: {stance} stance relaxes completely");
            }
            var steady = Motor(family, seed); steady.Stance = StanceKind.Combat;
            var twin = Motor(family, seed); twin.Stance = StanceKind.Combat;
            Advance(steady, .8); Advance(twin, .8);
            check(Difference(Bones(steady), twin) == 0, label + ": the same inputs give the same pose on every run");

            // Reactions end by themselves and leave no residue.
            foreach (var reaction in new[] { ReactionKind.Startle, ReactionKind.Reel, ReactionKind.Crash })
            {
                var motor = Motor(family, seed); Advance(motor, .5);
                var before = Bones(motor); int count = motor.ReactionTriggerCount;
                motor.TriggerReaction(reaction); Advance(motor, .15);
                bool moved = Difference(before, motor) > .5 || motor.Params.Mood != EyeMood.Neutral;
                bool sampled = motor.CombatSampling;
                Advance(motor, 1.3);
                check(moved && sampled && motor.Reaction == ReactionKind.None && motor.ReactionTriggerCount == count + 1 && !motor.CombatSampling && Finite(motor),
                    $"{label}: {reaction} plays once at the action frame rate and ends");
            }

            // Death: staged, final, held, and not a sleeping pose.
            var dying = Motor(family, seed); dying.Stance = StanceKind.Combat; Advance(dying, .5);
            var standing = Bones(dying);
            dying.Die(new Vec3(1, 0, .4));
            Advance(dying, .2);
            double early = dying.DeathProgress;
            check(dying.Dead && dying.State == MotionStateKind.Dead && early > 0 && early < 1 && dying.CombatSampling, label + ": death begins a staged collapse");
            Advance(dying, 2.5);
            var fallen = Bones(dying);
            check(dying.DeathProgress == 1 && Difference(standing, dying) > 2 && Finite(dying) && dying.Params.EyeOpen == 0 && dying.RestProgress == 0 && !dying.CombatSampling,
                label + ": the collapse ends in a held pose with closed eyes, distinct from standing and from rest");
            dying.TriggerAction(); dying.TriggerReaction(ReactionKind.Startle); dying.TriggerGameplayAction(.2, .2, ActionStyle.Bite);
            Advance(dying, 1);
            double drift = Difference(fallen, dying);
            check(!dying.ActionActive && dying.Reaction == ReactionKind.None && drift < .5, $"{label}: a corpse does not act, react, breathe or fidget (drift {drift:0.###})");
            var snapped = Motor(family, seed); snapped.SnapDead();
            check(snapped.Dead && snapped.DeathProgress == 1 && Finite(snapped), label + ": a restored corpse is already down");
            snapped.Revive(); Advance(snapped, .2);
            check(!snapped.Dead && snapped.State != MotionStateKind.Dead, label + ": revival stands the body back up");

            // Every authored attack style has a resolvable gesture that returns to neutral.
            foreach (var ability in Content.Load().Abilities.Where(a => a.ActionStyle != "" && !a.PlayerLearnable))
            {
                var motor = Motor(family, seed);
                motor.TriggerGameplayAction(ability.Windup, ability.Recovery, Enum.Parse<ActionStyle>(ability.ActionStyle));
                Advance(motor, ability.Windup);
                bool acting = motor.ActionActive && Finite(motor);
                Advance(motor, ability.Recovery + .1);
                check(acting && !motor.ActionActive, $"{label}: {ability.Id} plays through its windup and recovery");
            }

            var preserved = Motor(family, seed);
            preserved.TriggerGameplayAction(.4, .3, ActionStyle.Bite);
            Advance(preserved, .1);
            double actionTime = preserved.ActionTime;
            preserved.TriggerHit(Vec3.UnitX, .45, interruptAction: false);
            Advance(preserved, .1);
            check(preserved.ActionActive && preserved.ActionTime > actionTime && preserved.ActionTriggerCount == 1,
                label + ": a non-interrupting hit layers over the running action");
            Advance(preserved, .6);
            check(!preserved.ActionActive, label + ": the preserved action finishes on its original timeline");

            // A hop for bodies that cannot roll: off the ground mid-way, back down at the end.
            var hopper = Motor(family, seed); Advance(hopper, .3);
            hopper.DodgeBiped = false; hopper.DodgeProgress = .5; Advance(hopper, 1.0 / 60);
            double peak = hopper.Params.Altitude;
            hopper.DodgeProgress = null; Advance(hopper, .3);
            check(peak > hopper.Params.Altitude + 1, label + ": an evasive hop leaves the ground");
        }

        Views(app, check);
    }

    // The production view derives posture, facing and marks from actor state alone.
    static void Views(Foundation app, Action<bool, string> check)
    {
        var w = app.World; string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            app.BeginCombatCourse("ruin", true);
            var p = w.Player;
            ActorState Of(string profile) => w.Actors.Values.First(a => a.Id.StartsWith("lab:") && a.CombatProfile == profile);
            var prowler = Of("prowler"); var brute = Of("brute"); var stalker = Of("stalker");
            foreach (var a in w.Actors.Values.Where(a => a.Id.StartsWith("lab:"))) { EnemyMind.Resettle(a, a.Position, new V(1, 0)); a.NextTacticalTime = 1e9; }
            void Sync() { foreach (var view in app.Views.Values) { view.SyncCombatFacing(w); view.Sync(1f / 60); view.Overlay?._Process(1.0 / 60); } }
            var view = app.Views[prowler.Id];
            check(view.World == w && view.Overlay != null && view.Dust != null && app.Views["player"].Overlay == null, "enemy views carry marks and dust; the player's does not");

            Sync();
            check(view.Stance == StanceKind.Relaxed && !view.Overlay!.Showing, "a calm enemy stands relaxed with nothing overhead");
            prowler.Awareness = Awareness.Suspicious; prowler.Alertness = .5f; Sync();
            check(view.Stance == StanceKind.Wary && view.Overlay.Glyph == "?" && view.Creature.Stance == StanceKind.Wary, "suspicion shows as a wary posture under a question mark");
            prowler.Awareness = Awareness.Alerted; prowler.Target = p.Id; view.Alert("sighted", p); Sync();
            check(view.Stance == StanceKind.Combat && view.Overlay.Glyph == "!" && view.Creature.Motor!.ActionActive && view.Creature.Motor.CurrentActionStyle == ActionStyle.Roar,
                "the one who sights the intruder barks under an exclamation mark");
            var called = app.Views[w.Actors.Values.First(a => a.Id.StartsWith("lab:") && a.CombatProfile == "prowler" && a.Id != prowler.Id).Id];
            called.State.Awareness = Awareness.Alerted; called.Alert("called", p); Sync();
            check(called.Creature.Motor!.Reaction == ReactionKind.Startle && !called.Creature.Motor.ActionActive, "a called packmate starts instead of barking");
            prowler.Awareness = Awareness.Engaged; prowler.TacticalIntent = "circle"; Sync();
            check(view.Stance == StanceKind.Combat && view.Creature.AnimationFps == 15, "an engaged enemy holds a combat stance at the livelier frame rate");
            prowler.TacticalIntent = "retreat"; Sync();
            check(view.Stance == StanceKind.Flee, "a routed enemy shows it");
            prowler.Awareness = Awareness.Searching; Sync();
            check(view.Stance == StanceKind.Search && view.Overlay.Glyph == "search", "a searching enemy sweeps its head under a pale question mark");
            prowler.Awareness = Awareness.Returning; Sync();
            check(view.Stance == StanceKind.Relaxed && view.Overlay.Glyph == "home", "giving up is marked briefly");
            prowler.Awareness = Awareness.Unaware; Sync();
            check(view.Creature.AnimationFps == 10 && view.Overlay.Glyph == "", "back at ease the marks clear");

            // Facing follows the simulation, including while moving another way.
            prowler.Awareness = Awareness.Engaged; prowler.TacticalIntent = "circle"; prowler.FacingX = 0; prowler.FacingY = 1;
            prowler.X += 1; Sync();
            for (int i = 0; i < 40; i++) { prowler.X += 1; Sync(); view.Creature._Process(1.0 / 60); }
            double yaw = view.Creature.Motor!.Yaw, want = PixelCamera.GroundVelocityToYaw(0, 1);
            check(Math.Abs(DMath.WrapAngle(yaw - want)) < .5 && !view.Creature.Motor.Options.FaceVelocity, "a strafing enemy keeps facing where the simulation says, not where it walks");

            // Guard and rage.
            var guard = app.Views[brute.Id];
            brute.Awareness = Awareness.Engaged; brute.Target = p.Id; Sync();
            check(guard.Overlay!.GuardVisible, "a guarded foe shows its guard once it is fighting");
            w.ApplyStatus(brute, "rage", brute.Id); Sync();
            check(guard.Stance == StanceKind.Enraged && CharacterView.StatusColor("rage") != CharacterView.StatusColor("venom"), "rage changes posture and has its own colour");
            brute.State = ActionState.Staggered; brute.StateRemaining = CombatTuning.GuardBreakStagger; Sync();
            check(guard.Overlay.DazeVisible, "a long stagger shows as a daze");
            brute.State = ActionState.Idle; brute.StateRemaining = 0;

            // Production damage events must agree with the action that survived in simulation.
            brute.Statuses.Clear(); w.Cancel(brute); brute.Invulnerable = 0;
            brute.PoiseDelay = 0; brute.Health = brute.MaxHealth;
            brute.Awareness = Awareness.Unaware; // Ambush outcome still leaves poise intact.
            w.BeginAbility(brute, p, w.FindAbility("slam")!);
            w.Damage(p, brute, 1, "physical", ability: "slash");
            check(brute.State == ActionState.Windup && guard.Creature.Motor!.ActionActive,
                "a guarded ambush hit preserves the authoritative attack animation");
            w.Damage(p, brute, 1, "fire", periodic: true);
            check(brute.State == ActionState.Windup && guard.Creature.Motor!.ActionActive,
                "periodic damage preserves a guarded enemy's animation");
            brute.Invulnerable = 0;
            w.Damage(p, brute, w.FindProfile("brute")!.Poise + w.Stat(brute, "defence"), "physical", ability: "slash");
            check(brute.State == ActionState.Staggered && !guard.Creature.Motor!.ActionActive,
                "a guard-breaking hit still cancels the attack animation");
            var playerView = app.Views[p.Id];
            w.Cancel(p); p.Health = p.MaxHealth; p.Invulnerable = 0;
            w.BeginAbility(p, brute, w.FindAbility("slash")!);
            w.Damage(brute, p, 1, "fire", periodic: true);
            check(p.State == ActionState.Windup && playerView.Creature.Motor!.ActionActive,
                "periodic damage preserves an attacking player's animation");
            w.Cancel(p); w.Cancel(brute); brute.Health = brute.MaxHealth;

            // A charge: the lane is warned during the windup and while it is being run.
            var charge = w.FindAbility("charge")!;
            brute.Statuses.Clear(); w.Cancel(brute); p.X = brute.X - 140; p.Y = brute.Y; brute.Cooldowns.Clear();
            check(w.Activate(brute.Id, "charge", p.Id) && TelegraphView.IsActive(w, brute, charge), "the charge lane is telegraphed through the windup");
            double chargeYaw = PixelCamera.GroundVelocityToYaw(brute.ActionDirectionX, brute.ActionDirectionY);
            p.Y += 100; // Sidestep while the warning and simulation direction stay fixed.
            for (int i = 0; i < 15; i++) { Sync(); guard.Creature._Process(1.0 / 60); }
            check(Math.Abs(DMath.WrapAngle(guard.Creature.Motor!.Yaw - chargeYaw)) < .001,
                "a charge windup keeps its committed facing when the target sidesteps");
            for (int i = 0; i < (int)(charge.Windup * 60) + 4; i++) { w.Step(1f / 60); brute.NextTacticalTime = 1e9; }
            Sync();
            check(guard.Charging && guard.Stance == StanceKind.Charge && TelegraphView.IsCharging(w, brute, charge) && guard.Dust!.Active,
                "the run itself is a head-down charge that kicks up dust, still warned");
            // A fresh view reconstructed during the run must also recover the lane facing.
            var restoredCharge = new CharacterView { World = w };
            app.AddChild(restoredCharge);
            try
            {
                restoredCharge.Setup(brute);
                restoredCharge.SyncCombatFacing(w);
                restoredCharge.Creature._Process(1.0 / 60);
                check(Math.Abs(DMath.WrapAngle(restoredCharge.Creature.Motor!.Yaw - chargeYaw)) < .001,
                    "a charge restored in recovery faces its committed lane");
            }
            finally { restoredCharge.Free(); }
            brute.ActionConnected = true; Sync();
            check(!guard.Charging && guard.Stance != StanceKind.Charge && !TelegraphView.IsCharging(w, brute, charge),
                "a connected charge leaves its running stance together with its warning");
            brute.ActionConnected = false;
            for (int i = 0; i < 90; i++) { w.Step(1f / 60); brute.NextTacticalTime = 1e9; }
            Sync();
            check(!guard.Charging && !TelegraphView.IsCharging(w, brute, charge) && !TelegraphView.IsActive(w, brute, charge), "the warning ends with the charge");

            // A leap's danger zone is drawn where it will land and does not travel with the body.
            var pounce = w.FindAbility("pounce")!;
            w.Cancel(prowler); prowler.Cooldowns.Clear(); prowler.Awareness = Awareness.Engaged; p.X = prowler.X + 74; p.Y = prowler.Y;
            check(w.Activate(prowler.Id, "pounce", p.Id), "pounce fixture starts");
            var landing = prowler.Position + new V(prowler.ActionDirectionX, prowler.ActionDirectionY) * TelegraphView.RemainingLunge(prowler, pounce);
            for (int i = 0; i < (int)(pounce.Windup * 60) - 2; i++) w.Step(1f / 60);
            var late = prowler.Position + new V(prowler.ActionDirectionX, prowler.ActionDirectionY) * TelegraphView.RemainingLunge(prowler, pounce);
            check(prowler.State == ActionState.Windup && V.Distance(landing, late) < 1.5f && TelegraphView.RemainingLunge(prowler, pounce) < pounce.Lunge!.Distance * .25f, "the pounce warning marks a fixed landing spot");

            // Ambusher: folded down while dormant, upright the moment it is disturbed.
            var lurk = app.Views[stalker.Id];
            EnemyMind.Resettle(stalker, new V(p.X, p.Y + 300), V.Zero); stalker.NextTacticalTime = 0;
            for (int i = 0; i < 30; i++) w.Step(1f / 60);
            Sync();
            check(stalker.Activity == "ambush" && lurk.Creature.Resting && !lurk.Overlay!.Showing, "a dormant ambusher lies folded with no mark to give it away");
            stalker.Awareness = Awareness.Alerted; Sync();
            check(!lurk.Creature.Resting, "disturbed, it rises at once");

            // Death through the production event path, then a rebuilt view.
            prowler.Invulnerable = 0; w.Cancel(prowler);
            w.Damage(p, prowler, 10000, "physical", ability: "slash"); Sync();
            check(view.Creature.Dead && !view.Creature.Resting && !view.Overlay.Showing && view.Stance == StanceKind.Relaxed, "a fatal blow starts the collapse and clears every mark");
            app.RebuildViews();
            var rebuilt = app.Views[prowler.Id];
            check(rebuilt.Creature.Dead && rebuilt.Creature.Motor!.DeathProgress == 1, "a rebuilt view finds the corpse already fallen");

            // New effects stay inside the bounded effect layer.
            app.CombatEffects.Reset();
            foreach (var kind in new[] { CombatEffectKind.Alert, CombatEffectKind.GuardBreak, CombatEffectKind.Crash, CombatEffectKind.Rally, CombatEffectKind.Guarded })
                check(app.CombatEffects.Emit(kind, p.Position, V.UnitX, w.Tick, "a", "b", 120) && app.CombatEffects.LastKind == kind, "effect layer accepts " + kind);
            app.CombatEffects.Advance(.09f);
            for (int i = 0; i < 12; i++) app.CombatEffects.Advance(.09f);
            check(app.CombatEffects.ActiveCount == 0, "behaviour effects expire by themselves");
            foreach (string cue in new[] { "alert", "roar", "charge", "guard", "guard_break", "crash" })
            {
                int before = app.Audio.Call("debug_cue_count", cue).AsInt32();
                app.Audio.Call("cue", cue);
                check(app.Audio.Call("debug_cue_count", cue).AsInt32() == before + 1, "audio service has a voice for " + cue);
            }
            app.Audio.Call("stop_effects");
        }
        finally
        {
            if (app.InFeelCourse) app.LeaveFeelCourse();
            Persistence.Restore(w, app.Narrative, saved); app.RebuildViews(); app.Ui.PauseForVerification();
        }
    }
}
