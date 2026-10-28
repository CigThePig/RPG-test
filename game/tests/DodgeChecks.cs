using V = System.Numerics.Vector2;

namespace Mosslight;
/// <summary>Exercises authoritative action boundaries at the production 60 Hz step.</summary>
public static class DodgeChecks
{
    const float Dt = 1f / 60;
    public static void Run(Foundation app, Action<bool,string> check)
    {
        var w = app.World;
        string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            var r = new RegionState { Id = "dodge-regression", Width = 64, Height = 48, Tiles = Enumerable.Repeat(Terrain.Grass, 64 * 48).ToArray() };
            w.RegionsById[r.Id] = r;
            var p = w.Player; p.Region = r.Id; p.X = 500; p.Y = 500;
            p.State = ActionState.Idle; p.Health = p.MaxHealth; p.Cooldowns.Clear(); p.Statuses.Clear(); p.Invulnerable = 0;
            var enemy = w.Actors.Values.First(a => a.Profile == "beast");
            enemy.Region = r.Id; enemy.X = 600; enemy.Y = 500; enemy.Health = enemy.MaxHealth;
            enemy.State = ActionState.Recovery; enemy.StateRemaining = 100; enemy.MotionX = enemy.MotionY = 0;
            check(w.TryStartPlayerDodge(V.UnitX), "idle dodge starts");
            check(!w.TryStartPlayerDodge(V.UnitY) && Math.Abs(p.Cooldowns["dodge"] - .7f) < .0001f, "repeated dodge cannot restart or reset cooldown");
            for (int i = 0; i < 5; i++) w.Step(Dt);
            check(!World.DodgeImmune(p), "dodge remains vulnerable through tick five");
            w.Step(Dt);
            check(World.DodgeImmune(p), "dodge immunity starts on tick six");
            float hp = p.Health; var pos = p.Position;
            check(!w.Damage(enemy, p, 10, "physical") && p.Health == hp && p.Position == pos && p.State == ActionState.Dodging, "immune direct hit causes no damage, knockback or stagger");
            var bite = w.Content.Abilities.Single(a => a.Id == "bite");
            float biteWindup = bite.Windup; string biteStatus = bite.Status;
            try
            {
                // Drive the actual targeted non-projectile resolution while immune.
                bite.Windup = .016f; bite.Status = "burn";
                enemy.State = ActionState.Idle; enemy.StateRemaining = 0; enemy.Cooldowns.Clear();
                enemy.X = p.X + 20; enemy.Y = p.Y;
                check(w.Activate(enemy.Id, "bite", p.Id), "targeted enemy bite starts during dodge");
                w.Step(Dt);
                check(p.Health == hp && !p.Statuses.Any(x => x.Id == "burn") && p.State == ActionState.Dodging, "dodged direct ability applies no damage or on-hit status");
            }
            finally { bite.Windup = biteWindup; bite.Status = biteStatus; }
            enemy.State = ActionState.Recovery; enemy.StateRemaining = 100;
            w.Projectiles.Add(new ProjectileState { Source = enemy.Id, Target = p.Id, Ability = "ember", X = p.X, Y = p.Y });
            w.Step(Dt);
            check(p.Health == hp && !p.Statuses.Any(x => x.Id == "burn"), "dodged projectile applies neither damage nor on-hit burn");
            for (int i = 8; i < 17; i++) w.Step(Dt);
            check(World.DodgeImmune(p), "dodge immunity remains active through tick seventeen");
            w.Step(Dt);
            check(!World.DodgeImmune(p), "dodge immunity ends on tick eighteen");
            for (int i = 18; i < 24; i++) w.Step(Dt);
            check(p.State == ActionState.Idle && Math.Abs(p.X - 590) < .05f && Math.Abs(p.Y - 500) < .05f, "dodge travels ninety ground units and completes on tick twenty-four");
            check(p.Cooldowns["dodge"] > .28f && p.Cooldowns["dodge"] < .32f, "cooldown began on initiation");
            p.Cooldowns.Clear(); p.State = ActionState.Moving;
            check(w.TryStartPlayerDodge(V.UnitY), "dodge starts from moving");
            var start = p.Position;
            w.PlayerMove(V.UnitX, Dt, 3.2f, true);
            for (int i = 0; i < 24; i++) { w.PlayerMove(V.UnitX, Dt, 3.2f, true); w.Step(Dt); }
            check(Math.Abs(p.X - start.X) < .05f && Math.Abs(p.Y - start.Y - 90) < .05f, "normal movement cannot steer locked dodge");
            foreach (var state in new[] { ActionState.Windup, ActionState.Recovery, ActionState.Staggered, ActionState.Dead, ActionState.Dodging })
            {
                p.State = state; p.Cooldowns.Clear();
                check(!w.TryStartPlayerDodge(V.UnitX) && !p.Cooldowns.ContainsKey("dodge"), "dodge rejected without cost during " + state);
            }
            p.State = ActionState.Idle; p.Cooldowns.Clear(); p.Health = p.MaxHealth; p.Invulnerable = 0; p.X = 500; p.Y = 500;
            check(w.TryStartPlayerDodge(V.UnitX), "dodge starts before periodic damage test");
            for (int i = 0; i < 6; i++) w.Step(Dt);
            check(w.Damage(enemy, p, 5, "fire", periodic: true) && p.State == ActionState.Staggered && !World.DodgeImmune(p), "periodic damage retains existing effect and interrupts dodge");
            check(p.Cooldowns["dodge"] > 0, "interrupted dodge keeps cooldown");
            p.State = ActionState.Idle; p.StateRemaining = 0; p.Invulnerable = 0; p.Cooldowns.Clear(); p.X = 520; p.Y = 500;
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 23] = Terrain.Rock;
            check(w.TryStartPlayerDodge(V.UnitX), "dodge starts toward a wall");
            for (int i = 0; i < 24; i++) w.Step(Dt);
            check(p.X <= 552 - p.Radius + .05f && r.CanStand(p.Position, p), "dodge collision stops at wall without tunnelling");
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 23] = Terrain.Grass;
            p.State = ActionState.Idle; p.StateRemaining = 0; p.Invulnerable = 0; p.Cooldowns.Clear(); p.X = 500; p.Y = 500;
            check(w.TryStartPlayerDodge(V.UnitX), "dodge starts while vulnerable");
            check(w.Damage(enemy, p, 5, "physical") && p.State == ActionState.Staggered && !World.DodgeImmune(p), "hit before immunity interrupts dodge normally");
            p.State = ActionState.Idle; p.StateRemaining = 0; p.Invulnerable = 0; p.Cooldowns.Clear();
            check(w.TryStartPlayerDodge(V.UnitX), "dodge starts before save");
            for (int i = 0; i < 8; i++) w.Step(Dt);
            var savedPos = p.Position; float cooldown = p.Cooldowns["dodge"];
            string during = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, during); p = w.Player;
            check(p.State == ActionState.Idle && p.Position == savedPos && Math.Abs(p.Cooldowns["dodge"] - cooldown) < .0001f && !World.DodgeImmune(p), "loading a dodge retains position and cooldown but clears transient state");
            p.Cooldowns.Clear(); check(w.TryStartPlayerDodge(V.UnitX), "dodge starts before lifecycle cancellation");
            var beforePause = p.Position;
            var oldAutosave = File.Exists(Persistence.AutosavePath) ? File.ReadAllBytes(Persistence.AutosavePath) : null;
            try
            {
                // Exercise the actual application lifecycle path, while preserving
                // any pre-existing local autosave if this suite is run by a player.
                app._Notification((int)Godot.Node.NotificationApplicationPaused);
                app._Notification((int)Godot.Node.NotificationApplicationResumed);
                for (int i = 0; i < 10; i++) w.Step(Dt);
                check(p.State == ActionState.Idle && p.Position == beforePause && p.Cooldowns["dodge"] > 0 && !World.DodgeImmune(p), "background and resume cancel dodge movement and retain cooldown");
            }
            finally
            {
                app._Notification((int)Godot.Node.NotificationApplicationResumed);
                if (oldAutosave == null) File.Delete(Persistence.AutosavePath);
                else File.WriteAllBytes(Persistence.AutosavePath, oldAutosave);
            }
        }
        finally { Persistence.Restore(w, app.Narrative, saved); app.RebuildViews(); }
    }
}
