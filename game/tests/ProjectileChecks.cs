using V = System.Numerics.Vector2;
namespace Mosslight;
public static class ProjectileChecks
{
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World; string saved = Persistence.Capture(w, app.Narrative);
        var events = new List<CombatPresentationEvent>(); void Observe(CombatPresentationEvent e) => events.Add(e);
        w.Presentation += Observe;
        try
        {
            var r = new RegionState { Id = "projectile-lab", Width = 40, Height = 30, PlacementRevision = 1, Tiles = Enumerable.Repeat(Terrain.Floor, 1200).ToArray() }; w.RegionsById[r.Id] = r;
            foreach (var actor in w.Actors.Values) actor.Region = "reach";
            var p = w.Player; p.Region = r.Id; p.X = 300; p.Y = 300; p.Health = p.MaxHealth = 1000; p.Statuses.Clear(); p.Invulnerable = 0;
            var a = new ActorState { Id = "shooter", Region = r.Id, Faction = "beasts", X = 150, Y = 300, Speed = 0, State = ActionState.Recovery, StateRemaining = 100, Abilities = new() { "spit" } }; w.Actors[a.Id] = a;
            var straight = w.FindAbility("spit")!; var homing = w.FindAbility("fixture_homing")!; var ember = w.FindAbility("ember")!;
            ProjectileState Make(AbilityDef d) => ProjectileFlight.Release(a, p, d);
            var q = Make(straight); p.Y = 400;
            var result = ProjectileFlight.Advance(w, q, straight, r, .25f);
            check(Math.Abs(q.Y - 300) < .001f && Math.Abs(q.X - 183.75f) < .001f && result.Outcome == "", "straight projectile commits direction and speed");
            p.Y = 300; var h = Make(homing); p.Y = 500;
            ProjectileFlight.Advance(w, h, homing, r, .25f);
            float angle = MathF.Atan2(h.HeadingY, h.HeadingX) * 180 / MathF.PI;
            check(angle > 0 && angle <= 13.751f, "bounded homing obeys 55 degrees per second");
            p.Y = 300; var legacy = Make(ember); p.Y = 500;
            ProjectileFlight.Advance(w, legacy, ember, r, .25f);
            check(legacy.HeadingY > .7f, "omitted Ember mode retains target-following compatibility");
            p.Y = 300; var x = Make(straight); var y = Make(straight);
            for (int i = 0; i < 20; i++) { ProjectileFlight.Advance(w, x, straight, r, 1f/60); ProjectileFlight.Advance(w, y, straight, r, 1f/60); }
            check(x.X == y.X && x.Y == y.Y && x.HeadingX == y.HeadingX, "projectile trajectories repeat exactly");
            var lost = Make(homing); lost.Target = "missing";
            check(ProjectileFlight.Advance(w, lost, homing, r, .25f).Outcome == "" && lost.X > a.X && lost.Y == a.Y, "lost target continues last heading");
            var expired = Make(straight); expired.Remaining = .01f;
            check(ProjectileFlight.Advance(w, expired, straight, r, .25f).Outcome == "expired" && expired.X < 152, "expiration clips travel to remaining lifetime");
            // Large legal step/high speed: analytic collision, no tiny motion substeps.
            var fast = new AbilityDef { Id = "spit", ProjectileSpeed = 2000, Projectile = new() { Mode = "straight", Radius = 3 } };
            r.Tiles[12 * r.Width + 9] = Terrain.Wall;
            var wall = Make(fast);
            check(ProjectileFlight.Advance(w, wall, fast, r, .25f).Outcome == "terrain" && wall.X < 216, "sweep chooses wall before farther actor");
            r.Tiles[12 * r.Width + 9] = Terrain.Floor; r.Features[12 * r.Width + 9] = "lock";
            check(ProjectileFlight.Advance(w, Make(fast), fast, r, .25f).Outcome == "terrain", "locked door blocks swept projectile");
            r.OpenDoors.Add(12 * r.Width + 9);
            check(ProjectileFlight.Advance(w, Make(fast), fast, r, .25f).Actor == p, "open door permits small-target swept collision");
            var friendly = new ActorState { Id = "friendly", Region = r.Id, Faction = "beasts", X = 200, Y = 300 }; w.Actors[friendly.Id] = friendly;
            check(ProjectileFlight.Advance(w, Make(fast), fast, r, .25f).Actor == p, "non-hostiles neither take damage nor block projectiles");
            var candidate = new ActorState { Id = "aaa", Region = r.Id, Faction = p.Faction, X = p.X, Y = p.Y }; w.Actors[candidate.Id] = candidate;
            check(ProjectileFlight.Advance(w, Make(fast), fast, r, .25f).Actor == candidate, "equal actor collision ties resolve by ordinal ID");
            candidate.Region = "reach"; p.Y = 450;
            var boundary = Make(fast); boundary.HeadingX = -1; boundary.HeadingY = 0;
            check(ProjectileFlight.Advance(w, boundary, fast, r, .25f).Outcome == "boundary", "boundary swept collision terminates flight");
            p.Y = 300; w.Projectiles.Clear(); events.Clear();
            var contact = Make(straight); contact.X = p.X - p.Radius - 3 - 1; w.Projectiles.Add(contact);
            p.Invulnerable = 1; float health = p.Health; int statuses = p.Statuses.Count;
            w.Step(1f/60);
            check(w.Projectiles.Count == 0 && p.Health == health && p.Statuses.Count == statuses && events.Any(e => e.Kind == CombatPresentationKind.DefensiveAvoidance), "invulnerability consumes one projectile without damage/status");
            check(events.Count(e => e.Kind == CombatPresentationKind.ProjectileImpact) == 1 && !events.Any(e => e.Kind == CombatPresentationKind.Hit), "avoided actor collision has one impact observation and no hit");
            events.Clear(); p.Invulnerable = 0; p.State = ActionState.Idle; p.Cooldowns.Clear();
            check(w.TryStartPlayerDodge(V.UnitY), "projectile dodge fixture starts real roll");
            for (int i = 0; i < 8; i++) w.Step(1f/60);
            contact = Make(ember); contact.X = p.X - p.Radius - 1; contact.Y = p.Y; contact.HeadingX = 1; contact.HeadingY = 0; w.Projectiles.Add(contact);
            w.Step(1f/60);
            check(p.Health == health && p.Statuses.Count == statuses && events.Any(e => e.Kind == CombatPresentationKind.DefensiveAvoidance && e.Outcome == "dodge"), "dodge immunity rejects projectile and burn status");
            w.Cancel(p); p.Invulnerable = 0; events.Clear(); contact = Make(straight); contact.X = p.X - p.Radius - 3 - 1; contact.Y = p.Y; contact.HeadingX = 1; contact.HeadingY = 0; w.Projectiles.Add(contact);
            w.Step(1f/60); w.Step(1f/60);
            check(events.Count(e => e.Kind == CombatPresentationKind.Hit) == 1 && w.Projectiles.Count == 0, "real projectile resolves damage once through world pipeline");
            events.Clear(); var miss = Make(straight); miss.Remaining = .001f; w.Projectiles.Add(miss); w.Step(1f/60);
            check(!events.Any(e => e.Kind is CombatPresentationKind.ProjectileImpact or CombatPresentationKind.Hit), "expiration fabricates no impact");
            w.Projectiles.Clear(); p.X = 300; p.Y = 300;
            w.Projectiles.Add(new() { Source = a.Id, Target = p.Id, Ability = "ember", X = 160, Y = 300 });
            w.Projectiles.Add(Make(straight)); w.Projectiles.Add(Make(homing));
            var parsed = Persistence.Parse(Persistence.Capture(w, app.Narrative), w.Content);
            check(parsed.Schema == 1 && parsed.Projectiles.Count == 3 && parsed.Projectiles.All(p => float.IsFinite(p.HeadingX) && p.HeadingX > .99f), "schema-1 migrates legacy heading and preserves mixed projectile modes");
            var invalid = Content.Load(true); invalid.Abilities.Single(a => a.Id == "spit").Projectile!.TurnDegrees = float.NaN;
            check(invalid.Validate(new() { "potion", "iron_sword", "oak_staff", "coin", "herb", "ore", "wood" }).Any(e => e.Contains("projectile")), "content rejects nonfinite projectile configuration");
        }
        finally { w.Presentation -= Observe; Persistence.Restore(w, app.Narrative, saved); }
    }
}
