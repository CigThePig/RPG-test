using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Motion;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>Exercises the production event bridge, PPC motor, bounded effects and audio service.</summary>
public static class CombatPresentationChecks
{
    const float Dt = 1f / 60;
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World;
        string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            var map = GameplayReplay.CreateCourse(); map.Id = "presentation-check";
            w.RegionsById[map.Id] = map;
            var p = w.Player;
            var enemy = w.Actors.Values.First(a => a.Profile == "beast");
            foreach (var other in w.Actors.Values.Where(a => a.Id != p.Id && a.Id != enemy.Id)) other.Region = "reach";
            p.Region = enemy.Region = map.Id;
            p.X = 500; p.Y = 500; enemy.X = 528; enemy.Y = 500;
            p.Health = p.MaxHealth = enemy.Health = enemy.MaxHealth = 1000;
            p.State = enemy.State = ActionState.Idle; p.Invulnerable = enemy.Invulnerable = 0;
            p.Cooldowns.Clear(); enemy.Cooldowns.Clear(); p.Statuses.Clear(); enemy.Statuses.Clear();
            enemy.Speed = 0; enemy.Abilities.Clear(); enemy.Abilities.Add("bite");
            w.Items.Equip(p, "iron_sword", "weapon");
            app.RebuildViews();
            var playerMotor = app.Views[p.Id].Creature.Motor!;
            check(w.TryStartPlayerSlash(V.UnitX) && playerMotor.CurrentActionStyle == ActionStyle.Strike && playerMotor.ActionTriggerCount == 1,
                "player sword selects one timed Strike action");
            w.Cancel(p); p.Cooldowns.Clear(); app.CombatPresentation!.Reset();
            var view = app.Views[enemy.Id]; var motor = view.Creature.Motor!;
            var bite = w.FindAbility("bite")!;
            check(w.Activate(enemy.Id, "bite", p.Id), "presentation fixture starts real bite");
            check(motor.ActionActive && motor.CurrentActionStyle == ActionStyle.Bite && motor.ActionTriggerCount == 1,
                "authored bite starts once despite start and commitment observations");
            check(Math.Abs(motor.ActionDuration - (bite.Windup + bite.Recovery)) < .00001 && motor.CombatSampling,
                "bite pose uses authoritative timing and adaptive sampling");
            for (int i = 0; i < 5; i++) motor.Step(Dt);
            var anticipation = motor.Params;
            for (int i = 0; i < 13; i++) motor.Step(Dt);
            var strike = motor.Params;
            check(Math.Abs(anticipation.NeckPitch - strike.NeckPitch) > .03 || Math.Abs(anticipation.Jaw - strike.Jaw) > .03,
                "bite anticipation and impact use distinct head or jaw poses");
            for(int i=0;i<4;i++) w.Step(Dt);
            view.SyncCombatFacing(w);
            double expectedTime=bite.Windup-enemy.StateRemaining;
            for(int i=0;i<6;i++) motor.Step(Dt);
            check(Math.Abs(motor.ActionTime-expectedTime)<.0001, "catch-up frames retain authoritative attack pose progress");
            var defaultStyle = motor.Model.Anatomy.Rig.Action;
            check(motor.ResolveActionStyle(null) == defaultStyle &&
                motor.ResolveActionStyle((ActionStyle)255) == defaultStyle, "omitted or invalid style preserves the rig default");
            if (motor.Model.Anatomy.Rig.Wings.Count == 0)
                check(motor.ResolveActionStyle(ActionStyle.WingBuffet) == defaultStyle, "missing wings fall back to the rig action");
            if (motor.Model.Anatomy.Rig.Tail != null)
                check(motor.ResolveActionStyle(ActionStyle.TailSwipe) == ActionStyle.TailSwipe, "tailed anatomy accepts TailSwipe");
            double facing = motor.Yaw;
            var beforeContact = enemy.Position; var origin = p.Position;
            app.CombatEffects.Reset();
            check(w.Damage(p, enemy, 5, "physical", ability: "slash"), "real damage interrupts the windup");
            check(!motor.ActionActive && motor.CombatSampling && Math.Abs(DMath.WrapAngle(motor.Yaw - facing)) < .00001,
                "damage cancels the action and begins a hit reaction without rotating the victim");
            var contact = beforeContact + V.Normalize(origin - beforeContact) * Math.Min(8, enemy.Radius * .55f);
            check(app.CombatEffects.LastKind == CombatEffectKind.Hit &&
                app.CombatEffects.LastAnchor.DistanceTo(GodotConvert.GroundToScreen(contact.X, contact.Y)) < .01f && enemy.Position != beforeContact,
                "hit effect uses the captured contact before knockback");
            int afterHit = app.CombatEffects.EmittedCount;
            for (int i = 0; i < 25; i++) motor.Step(Dt);
            check(!motor.ActionActive && app.CombatEffects.EmittedCount == afterHit, "interrupted strike cannot reappear later");
            motor.SnapRest(true); motor.TriggerAction(ActionStyle.Bite); motor.CancelGameplayAction();
            for (int i = 0; i < 120; i++) motor.Step(Dt);
            check(!motor.ActionActive, "cancellation clears a pending wake-up action");

            w.Cancel(enemy); enemy.State=ActionState.Idle; enemy.Cooldowns.Clear(); enemy.Invulnerable=0;
            enemy.X=528; enemy.Y=500; enemy.Abilities.Clear(); enemy.Abilities.Add("fixture_sweep");
            check(w.Activate(enemy.Id,"fixture_sweep",p.Id), "directional presentation fixture starts through SPELL");
            check(motor.CurrentActionStyle == motor.ResolveActionStyle(ActionStyle.TailSwipe), "directional fixture selects the supported TailSwipe or rig fallback");
            w.Cancel(enemy);
            var winged = new CreatureMotor(CreatureRuntime.Build(CreatureRuntime.Sample("winged", 42)), CreatureRuntime.Registry);
            if(winged.Model.Anatomy.Rig.Wings.Count > 0)
            {
                winged.TriggerGameplayAction(.3,.4,ActionStyle.WingBuffet);
                for(int i=0;i<12;i++) winged.Step(Dt);
                check(winged.CurrentActionStyle == ActionStyle.WingBuffet && (Math.Abs(winged.Params.WingFlap)> .05 || winged.Params.WingFold < .8),
                    "winged anatomy uses a distinctive supported WingBuffet fixture");
            }

            // A restored windup resumes its elapsed pose, but never emits a second start cue.
            w.Cancel(enemy); enemy.State = ActionState.Idle; enemy.Cooldowns.Clear(); enemy.Invulnerable = 0;
            enemy.X = 528; enemy.Y = 500; p.X = 500; p.Y = 500; enemy.Abilities.Clear(); enemy.Abilities.Add("bite");
            check(w.Activate(enemy.Id, "bite", p.Id), "restore presentation fixture starts");
            for (int i = 0; i < 4; i++) w.Step(Dt);
            string windup = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, windup); p = w.Player; enemy = w.Actors[enemy.Id];
            app.RebuildViews(); motor = app.Views[enemy.Id].Creature.Motor!;
            check(motor.ActionActive && motor.CurrentActionStyle == ActionStyle.Bite && motor.ActionTriggerCount == 1 && motor.ActionTime > 0,
                "reconstructed windup resumes once at the saved elapsed time");
            for (int i = 0; i < 18; i++) w.Step(Dt);
            check(p.Health < p.MaxHealth, "restored windup still resolves on the world timeline");
            app.RebuildViews();
            check(!app.Views[enemy.Id].Creature.Motor!.ActionActive, "reconstructed recovery does not replay a completed attack");

            w.Cancel(enemy); enemy.Health = 1; enemy.Invulnerable = 0; enemy.X = 528; enemy.Y = 500;
            p.X = 500; p.Y = 500; app.CombatPresentation!.Reset();
            w.Items.Add(enemy.Id, "potion", 1);
            int fatalBefore = app.CombatEffects.EmittedCount;
            check(w.Damage(p, enemy, 10, "physical", ability: "slash") && !enemy.Alive,
                "fatal damage uses the ordinary world death path");
            check(app.CombatEffects.EmittedCount == fatalBefore + 2 && app.CombatEffects.LastKind == CombatEffectKind.Death,
                "fatal damage produces one hit and one distinct death effect");
            var defeated = app.Views[enemy.Id]; defeated.Creature.Paused = false;
            defeated.HasLoot = w.Items.HasLoot(enemy.Id);
            for (int i = 0; i < 50; i++) { defeated.Sync(Dt); defeated.Creature.Motor!.Step(Dt); }
            check(defeated.Creature.Motor!.RestProgress > .1 && defeated.LootMarkerVisible, "fatal hit transitions into a loot-marked rig defeat posture");
            string corpse = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, corpse); p = w.Player; enemy = w.Actors[enemy.Id];
            int savedEffects = app.CombatEffects.EmittedCount; app.RebuildViews();
            app.Views[enemy.Id].HasLoot=w.Items.HasLoot(enemy.Id); app.Views[enemy.Id].Sync(Dt);
            check(!enemy.Alive && app.Views[enemy.Id].Creature.Motor!.RestProgress >= .999 && app.Views[enemy.Id].LootMarkerVisible && app.CombatEffects.EmittedCount == savedEffects,
                "restored corpse is already defeated without replaying fatal feedback");
            // Restore an available actor for the routing and resource checks below.
            enemy.Health = enemy.MaxHealth; enemy.State = ActionState.Idle; enemy.Invulnerable = 0;

            // Verify outcome routing independently of wall-clock debounce.
            var controller = app.CombatPresentation!;
            var at = p.Position; var from = enemy.Position;
            var e = new CombatPresentationEvent(CombatPresentationKind.DefensiveAvoidance, enemy.Id, p.Id, "bite", w.Tick + 1, from, at, V.Zero, "dodge");
            controller.Reset(); int count = app.CombatEffects.EmittedCount;
            controller.Handle(e); controller.Handle(e with { Kind = CombatPresentationKind.Miss, Outcome = "miss" });
            check(app.CombatEffects.EmittedCount == count + 1 && app.CombatEffects.LastKind == CombatEffectKind.Dodge,
                "dodge avoidance suppresses the matching miss without a hit burst");
            controller.Reset(); count = app.CombatEffects.EmittedCount;
            controller.Handle(e with { Tick = e.Tick + 1, Outcome = "invulnerability" });
            controller.Handle(e with { Kind = CombatPresentationKind.Miss, Tick = e.Tick + 1, Outcome = "miss" });
            check(app.CombatEffects.EmittedCount == count, "ordinary invulnerability is not presented as a dodge or hit");
            controller.Reset(); count = app.CombatEffects.EmittedCount;
            controller.Handle(e with { Kind = CombatPresentationKind.ProjectileReleased, AbilityId = "ember", Tick = e.Tick + 2 });
            check(app.CombatEffects.EmittedCount == count + 1 && app.CombatEffects.LastKind == CombatEffectKind.ProjectileRelease,
                "projectile release does not present a collision");
            controller.Handle(e with { Kind = CombatPresentationKind.ProjectileImpact, AbilityId = "ember", Tick = e.Tick + 3 });
            controller.FlushPending();
            check(app.CombatEffects.LastKind == CombatEffectKind.ProjectileCollision, "projectile collision has its own effect");
            controller.Reset(); count=app.CombatEffects.EmittedCount;
            var projectile=e with { Kind=CombatPresentationKind.ProjectileImpact, AbilityId="ember", Tick=e.Tick+4 };
            controller.Handle(projectile);
            controller.Handle(projectile with { Kind=CombatPresentationKind.DefensiveAvoidance, Outcome="dodge" });
            controller.Handle(projectile with { Kind=CombatPresentationKind.Miss, Outcome="miss" });
            check(app.CombatEffects.EmittedCount == count + 1 && app.CombatEffects.LastKind == CombatEffectKind.Dodge,
                "avoided projectile has a dodge cue without a misleading collision burst");
            controller.Reset(); count=app.CombatEffects.EmittedCount;
            controller.Handle(projectile with { Tick=projectile.Tick+1 });
            controller.Handle(projectile with { Kind=CombatPresentationKind.Hit, Tick=projectile.Tick+1 });
            check(app.CombatEffects.EmittedCount == count + 1 && app.CombatEffects.LastKind == CombatEffectKind.ProjectileCollision,
                "projectile hit coalesces collision and damage into one impact burst");
            count = app.CombatEffects.EmittedCount;
            controller.Handle(e with { Kind = CombatPresentationKind.Hit, ActorId = "missing", TargetId = "missing", Tick = e.Tick + 4 });
            check(app.CombatEffects.EmittedCount == count, "missing actors and views are ignored safely");
            controller.Handle(e with { Kind = CombatPresentationKind.Hit, AbilityId = "", Outcome = "periodic", Tick = e.Tick + 5 });
            check(app.CombatEffects.LastKind == CombatEffectKind.Periodic, "unattributed periodic damage does not create a weapon swing");
            app.CombatCamera.Impact(2, w.Tick); app.RebuildViews();
            check(app.CombatEffects.ActiveCount == 0 && app.CombatCamera.Offset == Vector2.Zero,
                "view reconstruction clears transient effects and camera offset");

            // Capacity, lifetime, anchoring and overflow are independent of gameplay.
            var fx = app.CombatEffects; fx.Reset();
            var playerPosition = p.Position; float health = p.Health, mana = p.Mana;
            for (int i = 0; i < CombatEffects.Capacity; i++) fx.Emit(CombatEffectKind.Whiff, new V(i, 3), V.UnitX, i, p.Id);
            check(fx.ActiveCount == CombatEffects.Capacity && !fx.Emit(CombatEffectKind.Whiff, V.Zero, V.UnitX, 0, p.Id),
                "effects respect their fixed capacity and drop equal-priority overflow");
            check(fx.Emit(CombatEffectKind.Death, new V(20, 30), V.UnitX, 1, p.Id) && fx.ActiveCount == CombatEffects.Capacity,
                "important effects may replace a lower-priority effect without growing the pool");
            var anchor = fx.LastAnchor; p.X += 50;
            check(fx.LastAnchor == anchor && anchor.DistanceTo(GodotConvert.GroundToScreen(20, 30)) < .01f,
                "effect anchors are projected once and do not follow actors");
            for (int i = 0; i < 12; i++) fx.Advance(.1f);
            check(fx.ActiveCount == 0, "all temporary effects expire");
            p.X = playerPosition.X;
            check(p.Position == playerPosition && p.Health == health && p.Mana == mana, "effects do not mutate authoritative gameplay");

            var feedback = new CameraFeedback();
            for (int i = 0; i < 20; i++) feedback.Impact(3, i);
            check(feedback.Offset.Length() <= 3.5001f && feedback.Offset.Length() > 0, "simultaneous camera impacts are bounded");
            for (int i = 0; i < 15; i++) feedback.Advance(Dt);
            check(feedback.Offset == Vector2.Zero, "camera feedback decays completely to zero");
            feedback.Impact(2, 1); feedback.Reset();
            check(feedback.Offset == Vector2.Zero && p.Position == playerPosition, "camera lifecycle reset cannot move the player");
            feedback.Impact(3, 3);
            var edge = feedback.Confine(new Vector2(50,50),new Vector2(200,200),new Vector2(50,50));
            check(edge.X >= 0 && edge.Y >= 0 && feedback.Confine(new Vector2(50,50),new Vector2(80,80),new Vector2(50,50)) == Vector2.Zero,
                "camera nudges stay inside region boundaries");
            feedback.Strength = 0; feedback.Impact(3, 1);
            feedback.Advance(Dt);
            check(feedback.Offset == Vector2.Zero, "camera feedback can be disabled");
            check(app.Camera.PositionSmoothingEnabled && app.Camera.ProcessCallback == Camera2D.Camera2DProcessCallback.Physics,
                "camera follow smoothing uses fixed physics ticks during slow rendered frames");

            var stats = app.Audio.Call("debug_state").AsGodotDictionary();
            int resources = stats["resources"].AsInt32();
            for (int i = 0; i < 30; i++) app.Audio.Call("cue", i % 2 == 0 ? "hit" : "dodge");
            app.Audio.Call("cue", "missing-kind");
            var after = app.Audio.Call("debug_state").AsGodotDictionary();
            check(after["voices"].AsInt32() == 8 && after["playing"].AsInt32() >= 2 && after["playing"].AsInt32() <= 8 &&
                app.Audio.Call("active_count", "hit").AsInt32() <= 2 && after["resources"].AsInt32() == resources && resources >= 12,
                "audio cues overlap using bounded reusable voices and cached resources");
            bool muted = AudioServer.IsBusMute(0); AudioServer.SetBusMute(0, true);
            check(app.Audio.Call("debug_state").AsGodotDictionary()["muted"].AsBool(), "combat voices respect the existing master mute");
            AudioServer.SetBusMute(0, muted);
            app.CombatPresentation!.Reset();
            check(app.Audio.Call("debug_state").AsGodotDictionary()["playing"].AsInt32() == 0,
                "presentation reset stops transient voices without recreating resources");
            check(p.Position == playerPosition && p.Health == health && p.Mana == mana, "audio does not change world simulation");

            app.ResumeGame(); p.Health=1; p.Invulnerable=0; p.State=ActionState.Idle;
            enemy.Health=enemy.MaxHealth; enemy.State=ActionState.Idle;
            check(w.Damage(enemy,p,100,"physical",ability:"bite") && !p.Alive && app.GameplayBlocked && !app.PresentationBlocked,
                "player death blocks gameplay without pausing its presentation");
            long deathTick=w.Tick;
            for(int i=0;i<90;i++)
            {
                app._PhysicsProcess(Dt);
                app.Views[p.Id].Creature._Process(Dt);
            }
            check(w.Tick == deathTick && !app.Views[p.Id].Creature.Paused && app.Views[p.Id].Creature.Motor!.RestProgress > .1 && app.Views[enemy.Id].Creature.Paused,
                "player defeat advances while the world and living creatures remain paused");
            app.Ui.PauseForVerification();
        }
        finally
        {
            Persistence.Restore(w, app.Narrative, saved);
            app.RebuildViews();
        }
    }
}
