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
            foreach (var reaction in new[] { ReactionKind.Startle, ReactionKind.Reel, ReactionKind.Crash, ReactionKind.Block, ReactionKind.Cower })
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
        Senses(app, check);
    }

    // Sleep, wariness, broken nerve, shells, lobbed attacks and what the player is shown of enemy senses.
    static void Senses(Foundation app, Action<bool, string> check)
    {
        var w = app.World; string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            app.BeginCombatCourse("depths", true);
            var p = w.Player;
            ActorState Of(string profile) => w.Actors.Values.First(a => a.Id.StartsWith("lab:") && a.CombatProfile == profile);
            var alpha = Of("alpha"); var prowler = Of("prowler"); var shell = Of("shellback"); var mender = Of("mender"); var spitter = Of("spitter"); var lasher = Of("lasher");
            int post = 0;
            foreach (var a in w.Actors.Values.Where(a => a.Id.StartsWith("lab:")).OrderBy(a => a.Id, StringComparer.Ordinal))
            { EnemyMind.Resettle(a, new V(p.X + 120 + post % 3 * 40, p.Y - 60 + post / 3 * 90), new V(-1, 0)); a.NextTacticalTime = 1e9; post++; }
            void Sync() { foreach (var view in app.Views.Values) { view.SyncCombatFacing(w); view.Sync(1f / 60); view.Overlay?._Process(1.0 / 60); view.Stealth?._Process(1.0 / 60); } app.Senses.Survey(); }
            var eye = app.Views[p.Id].Stealth!;
            var view = app.Views[prowler.Id];

            // Calm and awake: sight and earshot are shown, the leader wears its crest, the player's eye is shut.
            Sync();
            check(app.Views[alpha.Id].Overlay!.LeaderVisible && !view.Overlay!.LeaderVisible, "only the pack's leader wears a crest");
            check(app.Senses.ConesDrawn == 6 && app.Senses.EarshotDrawn == 6 && app.Senses.TrailsDrawn == 0, $"each calm enemy nearby shows what it can see and hear ({app.Senses.ConesDrawn} cones, {app.Senses.EarshotDrawn} rings)");
            check(eye.Exposure == Exposure.Unseen && eye.Visible, "near enemies that have noticed nothing, the player's eye is shut");

            // Asleep: lying down under drifting letters, with no sight to show.
            prowler.Sleeper = true; Sync();
            check(EnemyMind.Asleep(prowler) && view.Creature.Resting && view.Overlay.Glyph == "sleep" && view.Stance == StanceKind.Relaxed, "a sleeper lies down under its own mark");
            check(app.Senses.ConesDrawn == 5 && app.Senses.EarshotDrawn == 6 && !SenseView.ShowsSight(w, prowler, w.FindProfile("prowler")!), "a sleeper shows how near it can be approached, and no field of view");
            float loud = EnemyMind.Earshot(w, alpha, p, w.FindProfile("alpha")!);
            p.State = ActionState.Dodging;
            float rolling = EnemyMind.Earshot(w, alpha, p, w.FindProfile("alpha")!);
            p.State = ActionState.Idle;
            check(rolling > loud * 1.9f && EnemyMind.Earshot(w, prowler, p, w.FindProfile("prowler")!) < loud, "the earshot shown shrinks for a quiet player and for a sleeping listener");
            prowler.Cooldowns["wary"] = 5; Sync();
            check(!view.Creature.Resting && view.Overlay.Glyph == "wary", "on edge after a disturbance it is up, under an open eye");
            prowler.Sleeper = false; prowler.Cooldowns.Clear();

            // Suspicion and search, as the player's own mark shows them.
            prowler.Awareness = Awareness.Suspicious; prowler.Alertness = .6f; Sync();
            check(eye.Exposure == Exposure.Suspected && Math.Abs(eye.Suspicion - .6f) < .001f, "growing suspicion opens the player's eye by as much");
            prowler.Awareness = Awareness.Searching; prowler.LastKnownX = p.X + 40; prowler.LastKnownY = p.Y; prowler.Alertness = 0; view.Overlay.Heard(); Sync();
            check(eye.Exposure == Exposure.Sought && view.Overlay.Glyph == "search" && view.Overlay.HeardVisible && app.Senses.TrailsDrawn == 1 && SenseView.ShowsTrail(w, prowler),
                "a search drawn by a noise shows sound arcs and the spot it is heading for");
            prowler.Awareness = Awareness.Engaged; prowler.Target = p.Id; Sync();
            check(eye.Exposure == Exposure.Hunted && app.Senses.ConesDrawn == 5, "recognised, the eye turns to say so and that enemy's sight is no longer drawn");
            prowler.Cooldowns["rout"] = 3; view.Overlay.Pop(); for (int i = 0; i < 60; i++) Sync();
            check(view.Overlay.Glyph == "shaken", "a broken nerve shows over the head for as long as it lasts");
            prowler.TacticalIntent = "cower"; Sync();
            check(view.Stance == StanceKind.Flee, "an enemy that has run far enough still shows its fear");
            prowler.Cooldowns.Clear(); EnemyMind.Calm(prowler);

            // The shell: raised while it can turn a blow, lowered while it recovers.
            var guard = app.Views[shell.Id];
            shell.Awareness = Awareness.Engaged; shell.Target = p.Id; Sync();
            check(guard.ShieldUp && guard.Stance == StanceKind.Guard && w.Shielded(shell, p.Position), "a shellback in a fight hunkers behind its raised front");
            shell.Invulnerable = 0; w.Damage(p, shell, 10, "physical", ability: "slash"); Sync();
            check(guard.Creature.Motor!.Reaction == ReactionKind.Block && app.CombatEffects.LastWord == "BLOCKED", "a turned blow jolts it and is called out");
            shell.State = ActionState.Recovery; shell.RecoveryAbility = "pincer"; shell.StateRemaining = .4f; Sync();
            check(!guard.ShieldUp && guard.Stance != StanceKind.Guard && !w.Shielded(shell, p.Position), "recovering from its own attack, the shell is shown open");
            w.Cancel(shell); EnemyMind.Calm(shell);
            prowler.Invulnerable = 0; prowler.Awareness = Awareness.Unaware; w.Damage(p, prowler, 1, "physical", ability: "slash");
            check(app.CombatEffects.LastWord == "AMBUSH", "an ambush is called out");
            EnemyMind.Calm(prowler);

            // A lobbed glob: warned from the first moment of the windup until it lands, then a pool.
            var glob = w.FindAbility("glob")!;
            spitter.Awareness = Awareness.Engaged; spitter.Target = p.Id; w.Cancel(spitter); spitter.Cooldowns.Clear();
            p.X = spitter.X - 140; p.Y = spitter.Y;
            check(w.Activate(spitter.Id, "glob", p.Id) && TelegraphView.IsActive(w, spitter, glob), "a lobbed attack is warned through its windup");
            float early = TelegraphView.BlastProgress(glob, 0);
            for (int i = 0; i < (int)(glob.Windup * 60) + 3; i++) w.Step(1f / 60);
            Sync();
            check(w.Hazards.Count == 1 && app.Telegraphs.HazardsDrawn == 1 && TelegraphView.BlastProgress(glob, glob.Windup) > early &&
                TelegraphView.BlastProgress(glob, glob.Windup + glob.Blast!.Delay) == 1, "the warning carries on, still filling, while it is in the air");
            app.CombatEffects.Reset();
            for (int i = 0; i < (int)(glob.Blast!.Delay * 60) + 2; i++) w.Step(1f / 60);
            Sync();
            check(w.Hazards.Count == 1 && w.Hazards[0].Landed && app.Telegraphs.HazardsDrawn == 1 && app.CombatEffects.EmittedCount > 0, "it bursts where it was warned and the pool is drawn");
            w.Hazards.Clear(); w.Cancel(spitter); EnemyMind.Calm(spitter);

            // A burst is warned all round the body.
            var coil = w.FindAbility("coil")!;
            lasher.Awareness = Awareness.Engaged; lasher.Target = p.Id; lasher.Cooldowns.Clear(); p.X = lasher.X - 34; p.Y = lasher.Y;
            check(w.Activate(lasher.Id, "coil", p.Id) && TelegraphView.IsActive(w, lasher, coil), "a burst is warned through its windup");
            w.Cancel(lasher); EnemyMind.Calm(lasher);

            // Mending shows on the mended.
            mender.Awareness = Awareness.Engaged; mender.Target = p.Id; alpha.Health = alpha.MaxHealth * .4f;
            alpha.X = mender.X + 20; alpha.Y = mender.Y + 20;
            app.CombatEffects.Reset(); w.Activate(mender.Id, "bloom", mender.Id);
            var bloom = w.FindAbility("bloom")!;
            for (int i = 0; i < (int)(bloom.Windup * 60) + 2; i++) w.Step(1f / 60);
            Sync();
            check(alpha.Statuses.Any(s => s.Id == "regrowth") && app.CombatEffects.LastKind == CombatEffectKind.Mend && CharacterView.StatusColor("regrowth") != CharacterView.StatusColor("snare"),
                "a mended ally shows it, in its own colour");

            app.CombatEffects.Reset();
            foreach (var kind in new[] { CombatEffectKind.Blocked, CombatEffectKind.Splash, CombatEffectKind.Ripple, CombatEffectKind.Mend, CombatEffectKind.Shaken })
                check(app.CombatEffects.Emit(kind, p.Position, V.UnitX, w.Tick, "a", "b", 34) && app.CombatEffects.LastKind == kind, "effect layer accepts " + kind);
            check(app.CombatEffects.Say("ROUTED", p.Position, w.Tick, "a", Colors.White) && app.CombatEffects.LastKind == CombatEffectKind.Word, "effect layer accepts a call-out");
            for (int i = 0; i < 12; i++) app.CombatEffects.Advance(.09f);
            check(app.CombatEffects.ActiveCount == 0, "the new effects expire by themselves");
            foreach (string cue in new[] { "block", "splash", "heard", "wake", "whimper", "mend", "lob" })
            {
                // Each on a free mixer: the fight above left voices sounding.
                app.Audio.Call("stop_effects");
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
