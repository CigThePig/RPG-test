using V = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>Fixed-step checks of geometry, authoritative lifecycle and presentation observations.</summary>
public static class CombatTelegraphChecks
{
    const float Dt = 1f / 60;
    public static void Run(Foundation app, Action<bool, string> check)
    {
        static V At(float degrees, float distance) => new(MathF.Cos(degrees * MathF.PI / 180) * distance, MathF.Sin(degrees * MathF.PI / 180) * distance);
        check(CombatGeometry.InCone(V.Zero, new V(10, 0), V.UnitX, 10, 45), "cone includes centerline and maximum range");
        check(CombatGeometry.InCone(V.Zero, At(45, 9), V.UnitX, 10, 45), "cone includes angular boundary");
        check(CombatGeometry.InCone(V.Zero, At(44.9f, 9), V.UnitX, 10, 45) && !CombatGeometry.InCone(V.Zero, At(45.1f, 9), V.UnitX, 10, 45), "cone distinguishes just inside and outside");
        check(!CombatGeometry.InCone(V.Zero, new V(10.01f, 0), V.UnitX, 10, 45) && CombatGeometry.InCone(V.Zero, V.Zero, V.UnitX, 10, 45), "cone excludes beyond reach and includes apex");
        check(!CombatGeometry.InCone(V.Zero, V.Zero, V.Zero, 10, 45) && !CombatGeometry.TryNormalize(new V(float.NaN, 0), out _) && !CombatGeometry.InCircle(V.Zero, new V(float.PositiveInfinity, 0), 10), "invalid vectors never become valid geometry");
        check(CombatGeometry.InCircle(new V(-10, 0), new V(0, 0), 10) && !CombatGeometry.InCircle(V.Zero, new V(0, -10.01f), 10), "circle includes boundary across world axes");
        check(CombatGeometry.InCone(V.Zero, new V(0, -5), -V.UnitY, 10, 30) && CombatGeometry.InCone(V.Zero, new V(-5, 0), -V.UnitX, 10, 30), "cone handles negative axes");

        var w = app.World;
        string saved = Persistence.Capture(w, app.Narrative);
        var events = new List<CombatPresentationEvent>();
        void Observe(CombatPresentationEvent e) => events.Add(e);
        w.Presentation += Observe;
        var bite = w.Content.Abilities.Single(a => a.Id == "bite");
        var sweep = w.Content.Abilities.Single(a => a.Id == "fixture_sweep");
        float originalWindup = bite.Windup;
        try
        {
            var r = new RegionState { Id = "telegraph-test", Width = 64, Height = 48, PlacementRevision = 1, Tiles = Enumerable.Repeat(Terrain.Grass, 64 * 48).ToArray() };
            w.RegionsById[r.Id] = r;
            var p = w.Player;
            var enemy = w.Actors.Values.First(a => a.Profile == "beast");
            foreach (var actor in w.Actors.Values.Where(a => a.Id != p.Id && a.Id != enemy.Id)) actor.Region = "reach";
            void Reset(string ability = "bite")
            {
                w.Cancel(p); w.Cancel(enemy); events.Clear();
                p.Region = enemy.Region = r.Id; p.X = 500; p.Y = 500; enemy.X = 528; enemy.Y = 500;
                p.Health = p.MaxHealth = 1000; p.Mana = p.MaxMana; p.Invulnerable = 0; p.Statuses.Clear(); p.Cooldowns.Clear();
                enemy.Health = enemy.MaxHealth = 1000; enemy.Invulnerable = 0; enemy.Statuses.Clear(); enemy.Cooldowns.Clear();
                p.State = enemy.State = ActionState.Idle; p.StateRemaining = enemy.StateRemaining = 0;
                enemy.Speed = 0; enemy.MotionX = enemy.MotionY = 0; enemy.Abilities.Clear(); enemy.Abilities.Add(ability);
            }
            void Step(int count) { for (int i = 0; i < count; i++) w.Step(Dt); }
            bool Warning(ActorState a, AbilityDef d) => TelegraphView.IsActive(w, a, d);
            Reset();
            float before = p.Health;
            check(w.Activate(enemy.Id, "bite", p.Id) && enemy.Target == p.Id && Warning(enemy, bite), "bite starts as target-locked windup with warning");
            check(events.Select(e => e.Kind).Take(2).SequenceEqual(new[] { CombatPresentationKind.AttackStarted, CombatPresentationKind.AttackCommitted }) && events[0].AbilityId == "bite", "typed start and commitment carry ability identity");
            float remaining = bite.Windup; int boundary = 0;
            do { remaining -= Dt; boundary++; } while (remaining > 0);
            Step(boundary - 1);
            check(p.Health == before && enemy.State == ActionState.Windup && Warning(enemy, bite), "0.30-second bite cannot damage before fixed-step impact");
            var contact = p.Position;
            Step(1);
            check(p.Health < before && enemy.State == ActionState.Recovery && !Warning(enemy, bite), "bite resolves at authoritative boundary and removes warning");
            check(events.Count(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == "bite") == 1 &&
                events.Any(e => e.Kind == CombatPresentationKind.Hit && e.AbilityId == "bite" && e.Position == contact), "one impact and pre-knockback hit coordinates are observed");
            float hitHealth = p.Health; Step(12);
            check(p.Health == hitHealth && events.Count(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == "bite") == 1, "recovery cannot repeat impact");
            Step(18);
            check(events.Count(e => e.Kind == CombatPresentationKind.ActionCompleted && e.AbilityId == "bite") == 1, "recovery publishes one completed action with its ability ID");

            Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "target-locked range fixture starts"); p.X = 700; Step(boundary);
            check(p.Health == p.MaxHealth && events.Any(e => e.Kind == CombatPresentationKind.Miss), "target-locked bite misses after target leaves range");
            Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "line-of-sight fixture starts");
            r.Tiles[20 * r.Width + 21] = Terrain.Rock; Step(boundary); r.Tiles[20 * r.Width + 21] = Terrain.Grass;
            check(p.Health == p.MaxHealth, "target-locked bite respects impact line of sight");
            Reset(); p.Invulnerable = 2; check(w.Activate(enemy.Id, "bite", p.Id), "invulnerability fixture starts"); Step(boundary);
            check(p.Health == p.MaxHealth && events.Any(e => e.Kind == CombatPresentationKind.DefensiveAvoidance && e.Outcome == "invulnerability"), "target-locked bite reports invulnerability without damage");
            Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "dodge fixture starts"); Step(boundary - 7);
            check(w.TryStartPlayerDodge(V.UnitX), "dodge starts during incoming bite"); Step(7);
            check(p.Health == p.MaxHealth && events.Any(e => e.Kind == CombatPresentationKind.DefensiveAvoidance && e.AbilityId == "bite"), "authoritative dodge immunity avoids bite");

            Reset("ember");
            check(w.Activate(enemy.Id, "ember", p.Id), "projectile fixture starts through SPELL");
            float projectileRemaining = w.FindAbility("ember")!.Windup; int projectileBoundary = 0;
            do { projectileRemaining -= Dt; projectileBoundary++; } while (projectileRemaining > 0);
            Step(projectileBoundary);
            check(events.Count(e => e.Kind == CombatPresentationKind.ProjectileReleased && e.AbilityId == "ember") == 1 &&
                !events.Any(e => e.Kind is CombatPresentationKind.ProjectileImpact or CombatPresentationKind.Hit), "projectile release is not a fabricated impact");
            Step(12);
            check(events.Count(e => e.Kind == CombatPresentationKind.ProjectileImpact && e.AbilityId == "ember") == 1 &&
                events.Any(e => e.Kind == CombatPresentationKind.Hit && e.AbilityId == "ember"), "projectile collision and hit occur after release");

            Reset("fixture_sweep");
            check(w.Activate(enemy.Id, "fixture_sweep", p.Id) && Warning(enemy, sweep) && enemy.ActionDirectionX < -.99f, "cone captures direction at initiation");
            p.X = 528; p.Y = 540; Step(boundary);
            check(p.Health == p.MaxHealth && enemy.ActionDirectionX < -.99f && events.Count(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == sweep.Id) == 1, "committed cone does not follow moved target");
            Reset("fixture_sweep");
            var other = w.Actors.Values.First(a => a.Id != p.Id && a.Id != enemy.Id);
            other.Region = r.Id; other.Faction = p.Faction; other.X = 500; other.Y = 510; other.Health = other.MaxHealth = 1000; other.Invulnerable = 0; other.State = ActionState.Recovery; other.StateRemaining = 100;
            check(w.Activate(enemy.Id, "fixture_sweep", p.Id), "multi-target cone starts"); Step(boundary);
            var hits = events.Where(e => e.Kind == CombatPresentationKind.Hit && e.AbilityId == sweep.Id).Select(e => e.TargetId).ToArray();
            check(hits.Length == 2 && hits.SequenceEqual(hits.OrderBy(x => x, StringComparer.Ordinal)) && hits.Distinct().Count() == 2, "cone damages each eligible target once in stable ID order");
            other.Region = "reach";
            Reset("fixture_sweep"); check(w.Activate(enemy.Id, "fixture_sweep", p.Id), "cone line-of-sight fixture starts");
            r.Tiles[20 * r.Width + 21] = Terrain.Rock; Step(boundary); r.Tiles[20 * r.Width + 21] = Terrain.Grass;
            check(p.Health == p.MaxHealth, "cone respects terrain line of sight");

            foreach (string cancellation in new[] { "explicit", "damage", "death", "status", "region" })
            {
                Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "cancellation fixture starts: " + cancellation);
                if (cancellation == "explicit") w.Cancel(enemy);
                if (cancellation == "damage") w.Damage(p, enemy, 1, "physical");
                if (cancellation == "death") w.Damage(p, enemy, 10000, "physical");
                if (cancellation == "status") w.ApplyStatus(enemy, "stagger", p.Id);
                if (cancellation == "region") w.Enter(enemy, "reach");
                enemy.Abilities.Clear();
                check(!Warning(enemy, bite) && events.Any(e => e.Kind == CombatPresentationKind.AttackCanceled), "cancellation removes warning: " + cancellation);
                Step(boundary + 2);
                check(p.Health == p.MaxHealth && !events.Any(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == "bite"), "canceled action cannot resolve: " + cancellation);
            }
            foreach (float duration in new[] { 0f, .001f })
            {
                Reset(); bite.Windup = duration;
                check(w.Activate(enemy.Id, "bite", p.Id) && !Warning(enemy, bite), "short windup has no misleading warning: " + duration);
                check(p.Health == p.MaxHealth, "short windup does not damage at activation"); Step(1);
                check(p.Health < p.MaxHealth && enemy.State == ActionState.Recovery, "short windup resolves once on next step"); Step(2);
                check(events.Count(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == "bite") == 1, "short windup does not repeat");
            }
            bite.Windup = originalWindup;
            foreach (string id in new[] { "bite", "fixture_sweep" })
            {
                Reset(id); check(w.Activate(enemy.Id, id, p.Id), "save windup starts: " + id); Step(4);
                string during = Persistence.Capture(w, app.Narrative);
                Persistence.Restore(w, app.Narrative, during); p = w.Player; enemy = w.Actors[enemy.Id];
                var definition = id == "bite" ? bite : sweep;
                check(Warning(enemy, definition) && float.IsFinite(enemy.ActionDirectionX), "schema-1 restore reconstructs warning: " + id);
                events.Clear(); Step(boundary + 1);
                check(events.Count(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == id) == 1 && !Warning(enemy, definition), "restored windup resolves once: " + id);
                string recovery = Persistence.Capture(w, app.Narrative);
                Persistence.Restore(w, app.Narrative, recovery); p = w.Player; enemy = w.Actors[enemy.Id]; events.Clear(); Step(30);
                check(!events.Any(e => e.Kind == CombatPresentationKind.AttackImpact && e.AbilityId == id) && !Warning(enemy, definition), "restored recovery has no repeated impact: " + id);
            }
            Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "interrupted save fixture starts"); w.Cancel(enemy);
            string canceled = Persistence.Capture(w, app.Narrative); Persistence.Restore(w, app.Narrative, canceled); p = w.Player; enemy = w.Actors[enemy.Id];
            enemy.Abilities.Clear(); events.Clear(); Step(boundary + 2);
            check(!Warning(enemy, bite) && !events.Any(e => e.Kind == CombatPresentationKind.AttackImpact), "interrupted schema-1 save cannot restore a warning or impact");

            Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "blocked saved windup fixture starts");
            // Simulate inconsistent legacy/external state without invoking the
            // normal status hook, which would already cancel the attack.
            enemy.Statuses.Add(new StatusState { Id="stagger", Source=p.Id, Remaining=.5f, UntilTick=1, Stacks=1 });
            string blocked = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, blocked); p=w.Player; enemy=w.Actors[enemy.Id];
            check(enemy.State == ActionState.Staggered && enemy.StateRemaining > 0 && enemy.PendingAbility == "" && !Warning(enemy,bite), "restore cancels blocked windup and preserves action-blocking status");

            Reset("fixture_sweep"); check(w.Activate(enemy.Id, "fixture_sweep", p.Id), "invalid direction save fixture starts");
            enemy.ActionDirectionX = enemy.ActionDirectionY = 0;
            string noDirection = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, noDirection); p=w.Player; enemy=w.Actors[enemy.Id];
            check(enemy.State == ActionState.Idle && enemy.PendingAbility == "" && !Warning(enemy,sweep), "restore cancels a cone with no committed direction");

            Reset(); check(w.Activate(enemy.Id, "bite", p.Id), "legacy schema-1 fixture starts");
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(Persistence.Capture(w, app.Narrative))!;
            foreach (var entry in legacy["Actors"]!.AsObject()) entry.Value!.AsObject().Remove("RecoveryAbility");
            Persistence.Restore(w, app.Narrative, legacy.ToJsonString()); p=w.Player; enemy=w.Actors[enemy.Id];
            check(Warning(enemy,bite) && enemy.RecoveryAbility == "", "schema-1 save without new field restores target windup");

            var invalid = new Content { Abilities = new() { new AbilityDef { Id = "invalid", Damage = 5, Geometry = new() { Shape = "circle", Range = 10 }, Telegraph = new() { Style = "cone" }, ActionStyle = "NotAStyle", Windup = float.NaN } } };
            check(invalid.Validate(new HashSet<string>()).Count >= 3, "validation rejects unsupported geometry, mismatched telegraph, action style and nonfinite timing");
        }
        finally
        {
            bite.Windup = originalWindup;
            w.Presentation -= Observe;
            Persistence.Restore(w, app.Narrative, saved);
            app.RebuildViews();
        }
    }
}
