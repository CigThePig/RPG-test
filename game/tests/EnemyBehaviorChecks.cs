using System.Text.Json.Nodes;
using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>
/// Perception, pack behaviour and the authored attack kinds, driven through the production
/// world step on an open arena. Fixtures place actors; nothing here stands in for the brain.
/// </summary>
public static class EnemyBehaviorChecks
{
    const float Dt = 1f / 60;
    static World w = null!;
    static RegionState r = null!;
    static ActorState p = null!;
    static readonly List<CombatPresentationEvent> seen = new();

    static ActorState Spawn(string id, string profile, float x, float y, V facing)
    {
        var a = EnemyRoster.Create(w, id, profile, r.Id, new V(x, y), facing);
        w.Actors[a.Id] = a;
        return a;
    }

    /// <summary>Empty the arena and stand a full-health, unprotected player at a point.</summary>
    static void Arena(float x = 480, float y = 360)
    {
        foreach (string id in w.Actors.Keys.Where(k => k.StartsWith("b:")).ToArray()) w.Actors.Remove(id);
        Array.Fill(r.Tiles, Terrain.Floor);
        w.Projectiles.Clear();
        w.Hazards.Clear();
        w.ClearPlayerMotion();
        w.Cancel(p);
        p.X = x; p.Y = y; p.Health = p.MaxHealth = 10000; p.Mana = p.MaxMana; p.Invulnerable = 0;
        p.Statuses.Clear(); p.Cooldowns.Clear();
        seen.Clear();
    }

    static void Run(int ticks, Action<int>? each = null)
    {
        for (int i = 0; i < ticks; i++) { w.Step(Dt); each?.Invoke(i); }
    }

    static bool Saw(CombatPresentationKind kind, string actor = "", string outcome = "") =>
        seen.Any(e => e.Kind == kind && (actor == "" || e.ActorId == actor) && (outcome == "" || e.Outcome == outcome));

    static void Engage(ActorState a)
    {
        a.Awareness = Awareness.Engaged; a.Target = p.Id; a.Alertness = 1;
        a.LastKnownX = p.X; a.LastKnownY = p.Y; a.LastSeenTime = w.Time;
        a.FacingX = MathF.Sign(p.X - a.X); a.FacingY = 0;
    }

    public static void Run(Foundation app, Action<bool, string> check)
    {
        w = app.World;
        string saved = Persistence.Capture(w, app.Narrative);
        void Observe(CombatPresentationEvent e) => seen.Add(e);
        w.Presentation += Observe;
        try
        {
            r = new RegionState { Id = "behaviour", Width = 40, Height = 30, PlacementRevision = 1, Tiles = Enumerable.Repeat(Terrain.Floor, 1200).ToArray() };
            w.RegionsById[r.Id] = r;
            foreach (var actor in w.Actors.Values) actor.Region = "reach";
            p = w.Player; p.Region = r.Id;

            Senses(check);
            PackCall(check);
            LosingTheQuarry(check);
            MeleeTokens(check);
            Pounce(check);
            Charge(check);
            Guard(check);
            SelfBuffMemory(app, check);
            Evasion(check);
            Stalker(check);
            Howler(check);
            Volley(check);
            Roaming(check);
            Sound(check);
            Sleepers(app, check);
            Evidence(check);
            Morale(check);
            Shell(check);
            Blast(app, check);
            Burst(check);
            Mender(check);
            Saving(app, check);
            Authoring(check);
        }
        finally
        {
            w.Presentation -= Observe;
            Persistence.Restore(w, app.Narrative, saved);
            app.RebuildViews();
        }
    }

    static void Senses(Action<bool, string> check)
    {
        Arena();
        var sight = w.FindProfile("prowler")!;
        // Facing away, outside earshot: a careful approach from behind goes unnoticed.
        var a = Spawn("b:back", "prowler", 480 - 120, 360, -V.UnitX);
        check(!EnemyMind.Notices(w, a, p, sight), "an intruder behind and out of earshot is not noticed");
        // The whole test must fit inside one roaming glance, so pin the facing each tick.
        Run(120, _ => { a.FacingX = -1; a.FacingY = 0; });
        check(a.Awareness == Awareness.Unaware && a.Alertness == 0 && !Saw(CombatPresentationKind.Alerted), "two seconds behind an unaware prowler raises no alarm");
        // Creeping, even inside earshot, is still not heard; a careless stride is.
        p.X = a.X + sight.HearingDistance - 6;
        Run(30, _ => { a.FacingX = -1; a.FacingY = 0; w.PlayerMove(new V(-.4f, 0), Dt); });
        check(a.Awareness == Awareness.Unaware && a.Alertness == 0 && p.X - a.X > sight.HearingDistance * CombatTuning.QuietHearing && p.X - a.X < sight.HearingDistance,
            "a slow step inside earshot is too quiet to hear");
        p.X = a.X + sight.HearingDistance - 6;
        // Pace back and forth at full stride without closing the distance.
        Run(40, i => { a.FacingX = -1; a.FacingY = 0; w.PlayerMove(new V(i / 6 % 2 == 0 ? 1 : -1, 0), Dt); });
        check(a.Awareness is Awareness.Alerted or Awareness.Engaged && Saw(CombatPresentationKind.Alerted, a.Id, "sighted"), "a careless stride inside earshot is heard from any side");
        w.ClearPlayerMotion();

        Arena();
        a = Spawn("b:front", "prowler", 480 - 150, 360, V.UnitX);
        var stages = new List<Awareness>();
        Run(150, _ => { if (stages.Count == 0 || stages[^1] != a.Awareness) stages.Add(a.Awareness); });
        check(stages.SequenceEqual(new[] { Awareness.Unaware, Awareness.Suspicious, Awareness.Alerted, Awareness.Engaged }), "a distant sighting grows from suspicion to alarm to engagement: " + string.Join(">", stages));
        check(a.Target == p.Id && a.Knowledge.GetValueOrDefault(p.Id) == "seen", "the alarm fixes the quarry");
        int alarms = seen.Count(e => e.Kind == CombatPresentationKind.Alerted && e.ActorId == a.Id);
        check(alarms == 1, "one sighting raises exactly one alarm");

        Arena();
        a = Spawn("b:wall", "prowler", 480 - 110, 360, V.UnitX);
        for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 17] = Terrain.Wall;
        Run(120, _ => { a.FacingX = 1; a.FacingY = 0; });
        check(a.Awareness == Awareness.Unaware, "a wall hides the intruder even when faced");

        // Striking an unaware foe: bonus damage once, an alarm, and no second bonus.
        Arena();
        a = Spawn("b:ambush", "prowler", 480 + 30, 360, V.UnitX);
        a.Health = a.MaxHealth = 1000;
        float plain = Math.Max(1, 20 - w.Stat(a, "defence"));
        check(w.Damage(p, a, 20, "physical") && Math.Abs(1000 - a.Health - Math.Max(1, 20 * CombatTuning.AmbushMultiplier - w.Stat(a, "defence"))) < .01f &&
            w.Events.Recent.Last().Kind == "ambush", "a blow on an unaware foe lands for ambush damage");
        check(a.Awareness == Awareness.Engaged && a.Target == p.Id && Saw(CombatPresentationKind.Alerted, a.Id, "hurt"), "being struck engages the victim at once");
        float before = a.Health; a.Invulnerable = 0;
        check(w.Damage(p, a, 20, "physical") && Math.Abs(before - a.Health - plain) < .01f, "an engaged foe takes ordinary damage");
    }

    static void PackCall(Action<bool, string> check)
    {
        Arena();
        var caller = Spawn("b:caller", "prowler", 480 - 150, 360, V.UnitX);
        var near = Spawn("b:near", "prowler", 480 - 230, 330, -V.UnitX);
        var also = Spawn("b:also", "prowler", 480 - 250, 400, -V.UnitX);
        var far = Spawn("b:far", "prowler", 480 - 150 - 300, 360, -V.UnitX);
        Run(100, _ => { foreach (var b in new[] { near, also, far }) if (b.Awareness == Awareness.Unaware) { b.FacingX = -1; b.FacingY = 0; } });
        check(Saw(CombatPresentationKind.Alerted, caller.Id, "sighted") && Saw(CombatPresentationKind.Alerted, near.Id, "called") && Saw(CombatPresentationKind.Alerted, also.Id, "called"),
            "a sighting calls packmates in earshot");
        check(near.Called && also.Called && !caller.Called && near.Target == p.Id, "called allies take the caller's quarry and do not call in turn");
        check(far.Awareness == Awareness.Unaware && !Saw(CombatPresentationKind.Alerted, far.Id), "a packmate beyond the call hears nothing");
        check(seen.Count(e => e.Kind == CombatPresentationKind.Alerted) == 3, "the call does not echo through the pack");
    }

    static void LosingTheQuarry(Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:hunter", "prowler", 480 - 90, 360, V.UnitX);
        Run(90);
        check(a.Awareness == Awareness.Engaged, "hunter engages before the quarry vanishes");
        a.Health = a.MaxHealth * .5f;
        // The quarry slips behind a long wall and far out of reach.
        for (int x = 2; x < 38; x++) r.Tiles[6 * r.Width + x] = Terrain.Wall;
        var last = p.Position; p.X = 480; p.Y = 60;
        var profile = w.FindProfile("prowler")!;
        var stages = new List<Awareness> { a.Awareness };
        bool searchedNearLast = false, moved = false;
        float healthWhenReturning = -1, homecoming = -1;
        Run((int)((profile.MemorySeconds + profile.SearchSeconds + 12) * 60), _ =>
        {
            if (stages[^1] != a.Awareness)
            {
                stages.Add(a.Awareness);
                if (a.Awareness == Awareness.Returning) healthWhenReturning = a.Health;
                if (a.Awareness == Awareness.Unaware) homecoming = V.Distance(a.Position, a.Home);
            }
            if (a.Awareness == Awareness.Searching) { searchedNearLast |= V.Distance(a.Position, last) < 90; moved |= a.TacticalIntent == "search" && (a.MotionX != 0 || a.MotionY != 0); }
        });
        check(stages.SequenceEqual(new[] { Awareness.Engaged, Awareness.Searching, Awareness.Returning, Awareness.Unaware }), "a lost quarry is searched for, then abandoned: " + string.Join(">", stages));
        check(searchedNearLast && moved, "the search works the ground around where the quarry was last seen");
        check(homecoming >= 0 && homecoming < 14 && a.Target == "" && a.Alertness == 0, "the hunter walks back to its post and forgets");
        check(a.Health > healthWhenReturning && healthWhenReturning >= 0, "wounds mend on the walk home");

        // The leash: a quarry that runs far from the post is let go.
        Arena(480, 360);
        a = Spawn("b:leash", "prowler", 480 - 80, 360, V.UnitX);
        a.HomeX = 220; a.HomeY = 260;
        Run(60);
        check(a.Awareness == Awareness.Engaged, "leashed hunter engages");
        bool returned = false;
        Run(900, _ =>
        {
            // Keep running away along the far wall.
            p.X = Math.Min(900, p.X + 1.6f); p.Y = Math.Min(640, p.Y + 1.2f);
            returned |= a.Awareness == Awareness.Returning;
        });
        check(returned && V.Distance(a.Position, a.Home) < profile.LeashDistance + 40, "a hunter drawn past its leash breaks off");
    }

    static void MeleeTokens(Action<bool, string> check)
    {
        Arena();
        var pack = Enumerable.Range(0, 5).Select(i => Spawn("b:pack" + i, "prowler", 480 - 120 - (i % 2) * 26, 300 + i * 30, V.UnitX)).ToArray();
        foreach (var a in pack) Engage(a);
        int most = 0, flankTicks = 0;
        var attackers = new HashSet<string>();
        float widest = 0;
        Run(1200, _ =>
        {
            int committed = pack.Count(a => a.State is ActionState.Windup or ActionState.Recovery);
            most = Math.Max(most, committed);
            foreach (var a in pack) if (a.State == ActionState.Windup) attackers.Add(a.Id);
            flankTicks += pack.Count(a => a.TacticalIntent == "flank");
            var angles = pack.Select(a => MathF.Atan2(a.Y - p.Y, a.X - p.X)).OrderBy(x => x).ToArray();
            // Largest empty arc around the player: small means surrounded.
            float gap = 0;
            for (int i = 0; i < angles.Length; i++) gap = Math.Max(gap, i == angles.Length - 1 ? angles[0] + MathF.Tau - angles[i] : angles[i + 1] - angles[i]);
            widest = Math.Max(widest, MathF.Tau - gap);
            if (pack.Any(a => !r.CanStand(a.Position, a) || !float.IsFinite(a.X))) throw new InvalidOperationException("Pack member left the arena");
        });
        check(most > 0 && most <= CombatTuning.MeleeTokens, $"no more than {CombatTuning.MeleeTokens} of a pack press one target at once (saw {most})");
        check(attackers.Count >= 4, "the pack takes turns pressing: " + attackers.Count + " of 5 attacked");
        check(flankTicks > 300, "waiting packmates circle to the flanks");
        check(widest > MathF.PI * 1.1f, "the pack spreads around its quarry rather than queueing on one side");
        // A lone melee enemy always holds a token.
        Arena();
        var solo = Spawn("b:solo", "prowler", 480 - 30, 360, V.UnitX); Engage(solo);
        check(CombatBrain.HasToken(w, solo, p), "a lone fighter is never made to wait");
    }

    static void Pounce(Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:pouncer", "prowler", 480 - 74, 360, V.UnitX); Engage(a);
        var def = w.FindAbility("pounce")!;
        float start = a.X, health = p.Health;
        int started = -1, landed = -1;
        Run(120, i =>
        {
            if (started < 0 && a.PendingAbility == "pounce") started = i;
            if (started >= 0 && landed < 0 && a.State == ActionState.Recovery) landed = i;
        });
        check(started >= 0 && started < 30, "a prowler at leaping distance pounces rather than walking in");
        check(landed - started == (int)MathF.Round(def.Windup * 60) && a.X - start > def.Lunge!.Distance * .8f, "the leap covers its distance inside the windup");
        check(p.Health < health && Saw(CombatPresentationKind.Hit, a.Id), "a pounce onto a standing target connects");

        Arena();
        a = Spawn("b:pouncer", "prowler", 480 - 74, 360, V.UnitX); Engage(a);
        health = p.Health; bool stepped = false; float landing = float.NaN;
        Run(120, _ =>
        {
            if (a.PendingAbility == "pounce" && !stepped) { p.Y += 70; stepped = true; }
            if (stepped && float.IsNaN(landing) && a.State == ActionState.Recovery) landing = a.Y;
        });
        check(stepped && Saw(CombatPresentationKind.Miss, a.Id) && !seen.Any(e => e.Kind == CombatPresentationKind.Hit && e.AbilityId == "pounce"), "stepping out of the committed line makes the pounce miss");
        check(MathF.Abs(landing - 360) < 2, "a pounce does not steer after its target");

        // A stationary quarry must remain in front of the landing cone.
        foreach (float distance in new[] { 50f, 54f, 57f, 59f, 60f, 65f, 74f })
        {
            Arena();
            a = Spawn("b:pounce-range", "prowler", p.X - distance, p.Y, V.UnitX); Engage(a);
            var rangeIntent = CombatBrain.Select(w, a, p, w.FindProfile("prowler"), false);
            if (distance < 60)
            {
                check(rangeIntent.Ability != "pounce" && !w.CanActivate(a, p, def),
                    $"pounce rejects a target inside its safe landing distance at {distance} pixels");
                continue;
            }
            check(rangeIntent.Ability == "pounce" && w.Activate(a.Id, "pounce", p.Id),
                $"pounce accepts a stationary target at {distance} pixels");
            health = p.Health;
            for (int i = 0; i < (int)MathF.Ceiling(def.Windup * 60) + 2; i++)
            { a.NextTacticalTime = w.Time + 1; w.Step(Dt); }
            check(p.Health < health && Saw(CombatPresentationKind.Hit, a.Id),
                $"the selected pounce hits its stationary target at {distance} pixels");
        }

        // No leap into a wall.
        Arena();
        a = Spawn("b:pouncer", "prowler", 480 - 74, 360, V.UnitX); Engage(a);
        for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 18] = Terrain.Wall;
        var intent = CombatBrain.Select(w, a, p, w.FindProfile("prowler"), false);
        check(intent.Ability != "pounce", "a blocked run is not chosen for a pounce");
    }

    static void Charge(Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:charger", "brute", 480 - 140, 360, V.UnitX); Engage(a);
        var def = w.FindAbility("charge")!;
        float health = p.Health, px = p.X;
        bool started = false;
        Run(200, _ => started |= a.RecoveryAbility == "charge");
        check(Saw(CombatPresentationKind.ChargeStarted, a.Id) && started, "a brute with a clear lane charges");
        check(p.Health < health && p.X - px > def.Knockback * .6f, "a charge that connects hurts and throws its target back");
        check(seen.Count(e => e.Kind == CombatPresentationKind.Hit && e.ActorId == a.Id && e.AbilityId == "charge") == 1, "one charge lands one hit");

        // Sidestep with a wall behind: the brute runs into it and is dazed.
        Arena(480, 360);
        a = Spawn("b:charger", "brute", 480 - 110, 360, V.UnitX); Engage(a);
        for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 22] = Terrain.Wall;
        health = p.Health; bool dodged = false; float dazed = 0;
        Run(200, _ =>
        {
            if (a.PendingAbility == "charge" && !dodged) { p.Y += 80; dodged = true; }
            if (a.State == ActionState.Staggered) dazed = Math.Max(dazed, a.StateRemaining);
        });
        check(dodged && p.Health == health && Saw(CombatPresentationKind.ChargeCrashed, a.Id), "a charge that meets a wall crashes");
        check(dazed > def.Charge!.CrashStagger - .1f && r.CanStand(a.Position, a), "the crash leaves the brute dazed and inside the arena");

        // Diagonal contact must not take the ordinary movement tangent along a wall.
        foreach (float sign in new[] { -1f, 1f })
        {
            Arena(490, 360 + sign * 80);
            a = Spawn("b:diagonal", "brute", 410, 360, V.UnitX); Engage(a);
            check(w.Activate(a.Id, "charge", p.Id), "diagonal charge fixture starts");
            var origin = a.Position;
            var direction = new V(a.ActionDirectionX, a.ActionDirectionY);
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 22] = Terrain.Wall;
            p.X = 200; p.Y = 200;
            float drift = 0;
            for (int i = 0; i < 100 && !Saw(CombatPresentationKind.ChargeCrashed, a.Id); i++)
            {
                a.NextTacticalTime = w.Time + 1;
                w.Step(Dt);
                var offset = a.Position - origin;
                drift = Math.Max(drift, MathF.Abs(offset.X * direction.Y - offset.Y * direction.X));
            }
            check(Saw(CombatPresentationKind.ChargeCrashed, a.Id) && a.State == ActionState.Staggered && drift < .01f,
                "an oblique charge stops on its lane at the first wall contact");
        }

        // Landing ends the run, including movement after its warning disappears.
        Arena();
        a = Spawn("b:contact-stop", "brute", 370, 360, V.UnitX); Engage(a);
        check(w.Activate(a.Id, "charge", p.Id), "contact-stop fixture starts");
        for (int i = 0; i < 100 && !a.ActionConnected; i++) { a.NextTacticalTime = w.Time + 1; w.Step(Dt); }
        var contact = a.Position;
        bool landed = Saw(CombatPresentationKind.Hit, a.Id);
        Run(8, _ => a.NextTacticalTime = w.Time + 1);
        check(landed && a.Position == contact && !TelegraphView.IsCharging(w, a, def), "a landed charge stops moving when its warning ends");

        // A wider authored reach must still respect cover at a wall corner.
        Arena();
        a = Spawn("b:covered", "brute", 370, 360, V.UnitX); Engage(a);
        w.BeginAbility(a, p, def);
        a.State = ActionState.Recovery; a.PendingAbility = ""; a.RecoveryAbility = def.Id;
        a.ActionElapsed = def.Windup; a.StateRemaining = def.Recovery;
        a.X = 480 - a.Radius - 1; a.Y = 360 + a.Radius + 6;
        p.X = 480 + p.Radius + 6; p.Y = 360 - p.Radius - 1;
        r.Tiles[15 * r.Width + 20] = Terrain.Wall;
        float reach = def.Charge!.Reach;
        try
        {
            def.Charge.Reach = 24; // Valid authoring limit; the victim is close but occluded.
            check(r.CanStand(a.Position, a) && r.CanStand(p.Position, p) && !w.LineOfSight(a, p) &&
                V.Distance(a.Position, p.Position) <= a.Radius + p.Radius + def.Charge.Reach,
                "corner fixture has two legal bodies within charge reach separated by cover");
            health = p.Health;
            a.NextTacticalTime = w.Time + 1;
            w.Step(Dt);
            check(p.Health == health && !Saw(CombatPresentationKind.Hit, a.Id), "charge reach cannot damage a body through cover");
        }
        finally { def.Charge.Reach = reach; }

        // A missed charge in the open ends as a miss, not a crash.
        Arena(480, 360);
        a = Spawn("b:charger", "brute", 480 - 110, 360, V.UnitX); Engage(a);
        dodged = false;
        Run(200, _ => { if (a.PendingAbility == "charge" && !dodged) { p.Y += 90; dodged = true; } });
        check(dodged && Saw(CombatPresentationKind.Miss, a.Id) && !Saw(CombatPresentationKind.ChargeCrashed), "a charge through open ground simply misses");
    }

    static void Guard(Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:guard", "brute", 480 - 34, 360, V.UnitX); Engage(a);
        var profile = w.FindProfile("brute")!;
        Run(40);
        check(a.State == ActionState.Windup && a.PendingAbility == "slam", "brute winds up its sweep at close range");
        float health = a.Health;
        check(w.Damage(p, a, profile.Poise * .5f + w.Stat(a, "defence"), "physical") && a.State == ActionState.Windup && a.Health < health &&
            seen.Last(e => e.Kind == CombatPresentationKind.Hit).Outcome == "guarded", "a single blow hurts a guarded brute without interrupting it");
        a.Invulnerable = 0;
        check(w.Damage(p, a, profile.Poise * .6f + w.Stat(a, "defence"), "physical") && a.State == ActionState.Staggered &&
            Math.Abs(a.StateRemaining - CombatTuning.GuardBreakStagger) < .01f && Saw(CombatPresentationKind.GuardBroken, p.Id), "spending its poise breaks the guard and staggers for longer");
        check(a.Poise == profile.Poise, "a broken guard is restored for the next exchange");
        a.Invulnerable = 0; w.Cancel(a);
        w.Damage(p, a, 10 + w.Stat(a, "defence"), "physical");
        float worn = a.Poise;
        Run((int)(CombatTuning.PoiseRecoverySeconds * 60) + 2, _ => { a.NextTacticalTime = w.Time + 1; });
        a.Invulnerable = 0;
        w.Damage(p, a, 1 + w.Stat(a, "defence"), "physical");
        check(worn < profile.Poise && Math.Abs(a.Poise - (profile.Poise - 1)) < .01f, "poise recovers after a few seconds without being hit");
        // Burning does not chip the guard or interrupt.
        a.State = ActionState.Windup; a.PendingAbility = "slam"; a.StateRemaining = .5f; float poise = a.Poise;
        w.Damage(p, a, 3, "fire", periodic: true);
        check(a.State == ActionState.Windup && a.Poise == poise, "damage over time neither breaks nor wears the guard");

        // Enrage below the authored threshold.
        Arena();
        a = Spawn("b:rage", "brute", 480 - 120, 420, V.UnitX); Engage(a);
        a.Cooldowns["charge"] = 30;
        a.Health = a.MaxHealth * (profile.EnrageThreshold - .05f);
        float calmSpeed = w.Stat(a, "speed"), calmStrength = w.Stat(a, "strength");
        Run(90, _ => a.Cooldowns["charge"] = 30);
        check(a.Statuses.Any(s => s.Id == "rage") && w.Stat(a, "speed") > calmSpeed && w.Stat(a, "strength") > calmStrength, "a badly hurt brute bellows itself into a rage");
        int bellows = w.Events.Recent.Count(e => e.Kind == "ability_started" && e.Actor == a.Id && e.Value == "bellow");
        Run(240, _ => a.Cooldowns["charge"] = 30);
        check(bellows == 1 && w.Events.Recent.Count(e => e.Kind == "ability_started" && e.Actor == a.Id && e.Value == "bellow") == 1, "it does not bellow again while the rage lasts");
    }

    static void SelfBuffMemory(Foundation app, Action<bool, string> check)
    {
        foreach (var (profile, ability, status) in new[] { ("brute", "bellow", "rage"), ("howler", "screech", "frenzy") })
        {
            Arena();
            var a = Spawn("b:buff-memory", profile, 380, 360, V.UnitX); Engage(a);
            var d = w.FindAbility(ability)!;
            check(w.Activate(a.Id, ability, a.Id) && a.Target == p.Id, ability + " preserves the engaged quarry during its windup");
            var parsed = Persistence.Parse(Persistence.Capture(w, app.Narrative), w.Content);
            check(parsed.Actors[a.Id].Target == p.Id && parsed.Actors[a.Id].PendingAbility == ability,
                ability + " saves its quarry and self recipient independently");
            // Resume the cast itself, not just its parsed fields.
            Persistence.Restore(w, app.Narrative, Persistence.Capture(w, app.Narrative));
            a = w.Actors[a.Id]; p = w.Player; r = w.RegionsById[r.Id];
            // The player disappears while the creature is busy casting.
            p.X = 600;
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 22] = Terrain.Wall;
            for (int i = 0; i < (int)MathF.Ceiling((d.Windup + d.Recovery) * 60) + 3; i++)
            { a.NextTacticalTime = w.Time + 1; w.Step(Dt); }
            EnemyMind.Think(w, a, w.FindProfile(profile), .2f);
            check(a.Statuses.Any(s => s.Id == status) && a.Target == p.Id && a.Awareness == Awareness.Engaged && a.TacticalIntent == "pursue",
                ability + " buffs the caster then pursues the hidden quarry from memory");
        }
    }

    static void Evasion(Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:evader", "prowler", 480 + 34, 360, -V.UnitX); Engage(a);
        a.Health = a.MaxHealth = 100000;
        var profile = w.FindProfile("prowler")!;
        int hops = 0, swings = 0, hits = 0;
        float lastHop = -100;
        bool spacingHeld = true;
        void Count(CombatPresentationEvent e)
        {
            if (e.Kind == CombatPresentationKind.DodgeStarted && e.ActorId == a.Id)
            { hops++; spacingHeld &= w.Time - lastHop >= profile.EvadeCooldown - .05; lastHop = (float)w.Time; }
            if (e.Kind == CombatPresentationKind.Hit && e.TargetId == a.Id) hits++;
        }
        w.Presentation += Count;
        try
        {
            Run(3600, _ =>
            {
                // Hold the pair together and keep the prowler from attacking: this measures evasion alone.
                if (p.State is ActionState.Idle or ActionState.Moving) { p.X = 480; p.Y = 360; }
                if (a.State is ActionState.Idle or ActionState.Moving) { a.X = p.X + 34; a.Y = p.Y; a.NextTacticalTime = w.Time + 1; a.MotionX = a.MotionY = 0; }
                if (w.TryStartPlayerSlash(V.UnitX)) swings++;
                if (!r.CanStand(a.Position, a)) throw new InvalidOperationException("Evade left the arena");
            });
        }
        finally { w.Presentation -= Count; }
        check(swings > 60 && hops >= 3 && hops <= swings * profile.EvadeChance + 6, $"a prowler sometimes hops clear of a swing ({hops} hops in {swings} swings)");
        check(hits == swings - hops && hops > 0, $"every hop makes its swing miss and every other swing lands (hits {hits}, hops {hops}, swings {swings})");
        check(spacingHeld, "hops respect the evasion cooldown");
        // Unaware foes never dodge: an ambush always lands.
        Arena();
        a = Spawn("b:evader", "stalker", 480 + 34, 360, V.UnitX);
        a.Health = a.MaxHealth = 100000; hops = 0;
        for (int i = 0; i < 40; i++)
        {
            EnemyMind.Calm(a); a.Cooldowns.Clear(); a.X = p.X + 34; a.Y = p.Y; a.Invulnerable = 0; w.Cancel(a);
            seen.Clear(); p.Cooldowns.Clear(); w.Cancel(p);
            w.TryStartPlayerSlash(V.UnitX);
            if (Saw(CombatPresentationKind.DodgeStarted, a.Id)) hops++;
            a.NextTacticalTime = w.Time + 10;
            Run(30, _ => a.NextTacticalTime = w.Time + 10);
        }
        check(hops == 0, "an unaware foe never dodges the opening blow");
    }

    static void Stalker(Action<bool, string> check)
    {
        Arena(480, 360);
        var a = Spawn("b:stalker", "stalker", 480 - 140, 360, -V.UnitX);
        var profile = w.FindProfile("stalker")!;
        Run(120);
        check(a.Awareness == Awareness.Unaware && a.Activity == "ambush" && a.TacticalIntent == "lurk" && a.Position == a.Home, "a stalker lies dormant on its spot");
        p.X = a.X + profile.Sight - 12;
        bool venom = false; float farthest = 0; int stings = 0; bool withdrew = false;
        Run(420, _ =>
        {
            venom |= p.Statuses.Any(s => s.Id == "venom");
            if (a.State == ActionState.Windup && a.StateRemaining > w.FindAbility("sting")!.Windup - Dt * 1.5f) stings++;
            if (stings > 0 && a.State is ActionState.Idle or ActionState.Moving) { farthest = Math.Max(farthest, V.Distance(a.Position, p.Position)); withdrew |= a.TacticalIntent == "withdraw"; }
        });
        check(a.Awareness == Awareness.Engaged && a.Activity != "ambush" && Saw(CombatPresentationKind.Alerted, a.Id), "it wakes when something comes close, from any side");
        check(stings >= 2 && venom, "it stings and its venom takes hold");
        check(withdrew && farthest > profile.PreferredDistance - 16, "between stings it backs off to a wary distance");
        float health = p.Health; p.Statuses.Clear();
        w.ApplyStatus(p, "venom", a.Id); a.Region = "reach";
        w.Cancel(p); w.TryStartPlayerDodge(V.UnitX);
        Run(70);
        check(p.Health < health && p.State is ActionState.Idle or ActionState.Moving, "venom wears the player down without interrupting their actions");
    }

    static void Howler(Action<bool, string> check)
    {
        Arena();
        var h = Spawn("b:howler", "howler", 480 - 110, 300, V.UnitX); Engage(h);
        var ally = Spawn("b:ally", "prowler", 480 - 90, 400, V.UnitX); Engage(ally);
        var far = Spawn("b:faraway", "prowler", 480 - 420, 360, -V.UnitX);
        far.NextTacticalTime = 1e9;
        var profile = w.FindProfile("howler")!;
        float calm = w.Stat(ally, "speed");
        float nearest = float.PositiveInfinity, band = 0; int samples = 0;
        bool frenzied = false;
        Run(420, i =>
        {
            frenzied |= ally.Statuses.Any(s => s.Id == "frenzy") && w.Stat(ally, "speed") > calm;
            nearest = Math.Min(nearest, V.Distance(h.Position, p.Position));
            if (i > 120 && h.State is ActionState.Idle or ActionState.Moving) { band += V.Distance(h.Position, p.Position); samples++; }
        });
        check(Saw(CombatPresentationKind.Rallied, h.Id) && frenzied, "a howler's screech sends a fighting ally into a frenzy");
        check(!far.Statuses.Any(s => s.Id == "frenzy"), "the screech reaches only allies in earshot");
        check(samples > 0 && band / samples > profile.MinimumDistance + 10 && band / samples < profile.ApproachDistance + 30, $"it hovers at its distance rather than closing in (mean {band / Math.Max(1, samples):0})");
        // Crowded, it buffets the intruder away.
        Arena();
        h = Spawn("b:howler", "howler", 480 - 50, 360, V.UnitX); Engage(h);
        h.Cooldowns["screech"] = 30; float px = p.X;
        Run(120);
        check(Saw(CombatPresentationKind.Hit, h.Id) && p.X - px > 30, "a crowded howler throws the intruder back with its wings");
    }

    static void Volley(Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:spitter", "spitter", 480 - 120, 360, V.UnitX); Engage(a);
        p.Invulnerable = 1000;
        int most = 0; var headings = new HashSet<float>();
        Run(240, _ =>
        {
            var fan = w.Projectiles.Where(x => x.Ability == "volley").ToArray();
            most = Math.Max(most, fan.Length);
            foreach (var shot in fan) headings.Add(MathF.Round(MathF.Atan2(shot.HeadingY, shot.HeadingX), 3));
        });
        var def = w.FindAbility("volley")!.Projectile!;
        check(most == def.Count && headings.Count == def.Count, "a volley releases its whole fan at once");
        check(Math.Abs((headings.Max() - headings.Min()) * 180 / MathF.PI - def.SpreadDegrees) < 1.5f, "the fan spans its authored spread");
        check(seen.Count(e => e.Kind == CombatPresentationKind.ProjectileReleased && e.AbilityId == "volley") == 1, "one volley is one release");
    }

    static void Roaming(Action<bool, string> check)
    {
        Arena(900, 660);
        var a = Spawn("b:roamer", "mire", 300, 300, V.UnitX);
        var profile = w.FindProfile("mire")!;
        // The player is far beyond sight but near enough for the world to simulate the roamer.
        float farthest = 0, travelled = 0; int still = 0, moving = 0; var last = a.Position;
        var facings = new HashSet<(int, int)>();
        Run(3600, _ =>
        {
            farthest = Math.Max(farthest, V.Distance(a.Position, a.Home));
            travelled += V.Distance(a.Position, last); last = a.Position;
            if (a.MotionX == 0 && a.MotionY == 0) { still++; facings.Add(((int)MathF.Round(a.FacingX * 4), (int)MathF.Round(a.FacingY * 4))); } else moving++;
        });
        check(a.Awareness == Awareness.Unaware && farthest <= profile.RoamDistance + 8 && farthest > 12, $"an undisturbed prowler wanders near its post (farthest {farthest:0})");
        check(still > 600 && moving > 450 && travelled > 150, $"wandering alternates unhurried legs with pauses (still {still}, moving {moving}, travelled {travelled:0})");
        check(facings.Count >= 4, "while paused it glances around");
        float pace = travelled / (moving * Dt);
        check(pace < w.Stat(a, "speed") * .6f, $"it wanders at a walk, not a run ({pace:0})");
        // Out of range of the player the hunt is simply dropped.
        Engage(a); p.X = 300; p.Y = 300; a.X = 300 + 30;
        var room = new RegionState { Id = "elsewhere", Width = 8, Height = 8, PlacementRevision = 1, Tiles = Enumerable.Repeat(Terrain.Floor, 64).ToArray() };
        w.RegionsById[room.Id] = room; p.Region = room.Id; p.X = p.Y = 96;
        Run(2);
        check(a.Awareness == Awareness.Unaware && a.Target == "", "a hunt ends when the quarry leaves the region");
        p.Region = r.Id; w.RegionsById.Remove(room.Id);
    }

    static void Wall(int column, int from = 0, int to = int.MaxValue)
    {
        for (int y = Math.Max(0, from); y < Math.Min(r.Height, to); y++) r.Tiles[y * r.Width + column] = Terrain.Wall;
    }

    static void Sound(Action<bool, string> check)
    {
        // A noise is somewhere to go and look, not someone to chase.
        Arena(900, 660);
        var a = Spawn("b:listener", "prowler", 300, 360, -V.UnitX);
        var point = new V(380, 360);
        EnemyMind.Noise(w, p, point, 100);
        check(a.Awareness == Awareness.Searching && a.Target == "" && Saw(CombatPresentationKind.Heard, a.Id, "heard") && V.Distance(a.LastKnown, point) < .01f,
            "a noise in earshot sends a calm enemy to look, without telling it who made it");
        float nearest = float.PositiveInfinity; var stages = new List<Awareness> { a.Awareness };
        Run(900, _ => { nearest = Math.Min(nearest, V.Distance(a.Position, point)); if (stages[^1] != a.Awareness) stages.Add(a.Awareness); });
        check(nearest < 14 && stages.SequenceEqual(new[] { Awareness.Searching, Awareness.Returning, Awareness.Unaware }),
            $"it walks to the sound, searches there, and goes home (nearest {nearest:0}, {string.Join(">", stages)})");
        check(a.Cooldowns.GetValueOrDefault("wary") > 0 && seen.Count(e => e.Kind == CombatPresentationKind.Heard) == 1, "one noise is one investigation, and leaves the listener wary");

        // Walls muffle; distance ends it.
        Arena(900, 660);
        var open = Spawn("b:open", "prowler", 300, 300, -V.UnitX);
        var behind = Spawn("b:behind", "prowler", 300, 420, -V.UnitX);
        var close = Spawn("b:close", "prowler", 340, 420, -V.UnitX);
        var far = Spawn("b:far", "prowler", 150, 300, -V.UnitX);
        for (int x = 0; x < r.Width; x++) r.Tiles[15 * r.Width + x] = Terrain.Wall;
        EnemyMind.Noise(w, p, new V(380, 300), 100);
        check(open.Awareness == Awareness.Searching && far.Awareness == Awareness.Unaware, "a noise carries its radius in the open and no further");
        check(behind.Awareness == Awareness.Unaware, "a wall muffles a noise that would otherwise carry");
        EnemyMind.Noise(w, p, new V(380, 420), 100);
        check(close.Awareness == Awareness.Searching, "the same noise on its own side of the wall is heard");

        // Those already fighting, and an ambusher lying in wait, do not go to look.
        Arena(900, 660);
        var fighter = Spawn("b:fighter", "prowler", 300, 300, V.UnitX); Engage(fighter);
        var lurker = Spawn("b:lurker", "stalker", 300, 400, V.UnitX);
        EnemyMind.Noise(w, p, new V(340, 350), 150);
        check(fighter.Awareness == Awareness.Engaged && lurker.Awareness == Awareness.Unaware && !Saw(CombatPresentationKind.Heard), "fighters and lurking ambushers ignore a noise");

        // A fight is heard through a wall by a packmate who cannot see it and is not called.
        Arena(480, 360);
        var victim = Spawn("b:victim", "prowler", 510, 360, V.UnitX);
        var mate = Spawn("b:mate", "prowler", 510, 450, V.UnitX);
        for (int x = 0; x < r.Width; x++) r.Tiles[17 * r.Width + x] = Terrain.Wall;
        victim.Health = victim.MaxHealth = 1000;
        w.Damage(p, victim, 20, "physical");
        check(!Saw(CombatPresentationKind.Alerted, mate.Id) && mate.Awareness == Awareness.Searching && mate.Target == "" && Saw(CombatPresentationKind.Heard, mate.Id),
            "the sound of a fight draws a packmate who was not called and cannot see it");

        // The player's own actions: a roll is heard nearby, a swing only very close.
        Arena(480, 360);
        a = Spawn("b:ear", "prowler", 480 - 62, 360, -V.UnitX);
        Run(30, _ => { a.FacingX = -1; a.FacingY = 0; a.NextTacticalTime = w.Time + 1; });
        check(a.Awareness == Awareness.Unaware, "standing still behind it, just outside earshot, goes unnoticed");
        check(w.TryStartPlayerSlash(V.UnitX) && a.Awareness == Awareness.Unaware, "a sword swing is too soft to carry that far");
        Run(60, _ => { a.FacingX = -1; a.FacingY = 0; a.NextTacticalTime = w.Time + 1; });
        p.X = 480; // The swing carried the player a step forward.
        check(w.TryStartPlayerDodge(V.UnitY) && a.Awareness == Awareness.Searching && Saw(CombatPresentationKind.Heard, a.Id), "a roll is heard");
    }

    static void Sleepers(Foundation app, Action<bool, string> check)
    {
        Arena();
        var a = Spawn("b:sleeper", "prowler", 480 - 100, 360, V.UnitX); a.Sleeper = true;
        Run(120);
        check(EnemyMind.Asleep(a) && a.Awareness == Awareness.Unaware && a.Activity == "sleep" && a.TacticalIntent == "sleep" && a.Alertness == 0 && a.Position == a.Home,
            "a sleeper dozes at its post with an intruder in plain view");
        // Creeping right up to it.
        p.X = a.X + 70;
        Run(240, _ => { if (V.Distance(a.Position, p.Position) > 30) w.PlayerMove(new V(-.5f, 0), Dt); else w.ClearPlayerMotion(); });
        check(EnemyMind.Asleep(a) && V.Distance(a.Position, p.Position) < 34, $"a careful approach does not wake it ({V.Distance(a.Position, p.Position):0} away)");
        a.Health = a.MaxHealth = 1000;
        check(w.Damage(p, a, 20, "physical") && Math.Abs(1000 - a.Health - Math.Max(1, 20 * CombatTuning.SleepingMultiplier - w.Stat(a, "defence"))) < .01f &&
            a.Awareness == Awareness.Engaged && !EnemyMind.Asleep(a), "a blow on a sleeper lands for double and wakes it into the fight");

        // A careless approach rouses it, slowly.
        Arena();
        a = Spawn("b:sleeper", "prowler", 480 - 80, 360, V.UnitX); a.Sleeper = true;
        int woke = -1, engaged = -1;
        Run(200, i =>
        {
            if (a.Awareness == Awareness.Unaware) w.PlayerMove(new V(-1, 0), Dt);
            if (woke < 0 && a.Awareness == Awareness.Alerted) woke = i;
            if (engaged < 0 && a.Awareness == Awareness.Engaged) engaged = i;
        });
        check(woke >= 0 && Saw(CombatPresentationKind.Woke, a.Id) && Saw(CombatPresentationKind.Alerted, a.Id, "sighted"), "a careless approach wakes it");
        check(engaged - woke >= (int)(CombatTuning.WakeSeconds * 60) - 13, $"it takes a groggy moment to get up ({engaged - woke} ticks)");

        // A packmate's call wakes a sleeper too.
        Arena();
        var caller = Spawn("b:caller", "prowler", 480 - 150, 360, V.UnitX);
        a = Spawn("b:sleeper", "prowler", 480 - 230, 380, -V.UnitX); a.Sleeper = true;
        Run(100);
        check(Saw(CombatPresentationKind.Woke, a.Id) && Saw(CombatPresentationKind.Alerted, a.Id, "called") && caller.Awareness == Awareness.Engaged, "a call wakes a sleeping packmate");

        // After a hunt it stays up for a while, then settles down again.
        Arena();
        a = Spawn("b:sleeper", "prowler", 480 - 60, 360, V.UnitX); a.Sleeper = true;
        w.Damage(p, a, 1, "physical");
        for (int x = 2; x < 38; x++) r.Tiles[6 * r.Width + x] = Terrain.Wall;
        p.X = 480; p.Y = 60;
        var profile = w.FindProfile("prowler")!;
        bool home = false; double homeAt = 0; bool upWhileWary = true, settled = false; double settledAt = 0;
        Run((int)((profile.MemorySeconds + profile.SearchSeconds + 14 + CombatTuning.WarySeconds) * 60), _ =>
        {
            if (!home && a.Awareness == Awareness.Unaware) { home = true; homeAt = w.Time; }
            if (home && a.Cooldowns.GetValueOrDefault("wary") > 0) upWhileWary &= !EnemyMind.Asleep(a) && a.Activity != "sleep";
            if (home && !settled && EnemyMind.Asleep(a)) { settled = true; settledAt = w.Time; }
        });
        check(home && upWhileWary, "back from a hunt, a sleeper stays up and watchful");
        check(settled && a.Activity == "sleep" && settledAt - homeAt > 5, $"once the alarm has worn off it lies down again ({settledAt - homeAt:0.0}s after coming home)");
        var parsed = Persistence.Parse(Persistence.Capture(w, app.Narrative), w.Content);
        check(parsed.Actors[a.Id].Sleeper, "being a sleeper is saved");
    }

    static ActorState Corpse(string id, float x, float y, bool slain = true)
    {
        var body = Spawn(id, "prowler", x, y, V.UnitX);
        body.Health = 0; body.State = ActionState.Dead;
        if (slain) body.Knowledge[p.Id] = "attacker";
        return body;
    }

    static void Evidence(Action<bool, string> check)
    {
        Arena(900, 660);
        var finder = Spawn("b:finder", "prowler", 300, 360, V.UnitX);
        var mate = Spawn("b:mate", "prowler", 250, 400, -V.UnitX);
        var deaf = Spawn("b:deaf", "prowler", 300, 620, -V.UnitX);
        var body = Corpse("b:body", 400, 360);
        Run(60, _ => { finder.FacingX = 1; finder.FacingY = 0; foreach (var b in new[] { mate, deaf }) if (b.Awareness == Awareness.Unaware) { b.FacingX = -1; b.FacingY = 0; } });
        check(Saw(CombatPresentationKind.Disturbed, finder.Id, "body") && finder.Awareness == Awareness.Searching && V.Distance(finder.LastKnown, body.Position) < .01f,
            "an enemy that sees a slain packmate goes to search around the body");
        check(Saw(CombatPresentationKind.Disturbed, mate.Id, "warned") && mate.Awareness == Awareness.Searching && !Saw(CombatPresentationKind.Disturbed, deaf.Id) && deaf.Awareness == Awareness.Unaware,
            "it warns packmates in earshot, who search too; one out of earshot carries on");
        check(finder.Knowledge.GetValueOrDefault(body.Id) == "found" && mate.Knowledge.GetValueOrDefault(body.Id) == "found" && finder.Target == "" && !Saw(CombatPresentationKind.Alerted),
            "a body raises wariness, not an alarm");
        Run(1500);
        check(seen.Count(e => e.Kind == CombatPresentationKind.Disturbed && e.ActorId == finder.Id) == 1 && finder.Awareness == Awareness.Unaware,
            "the same body does not disturb it twice");
        float distance = 150;
        check(finder.Cooldowns.GetValueOrDefault("wary") > 0, "having found a body it stays on edge");
        // On edge, it recognises an intruder sooner than a calm packmate at the same distance.
        int[] noticed = new int[2];
        for (int trial = 0; trial < 2; trial++)
        {
            Arena(480, 360);
            var watcher = Spawn("b:watcher", "prowler", 480 - distance, 360, V.UnitX);
            if (trial == 1) watcher.Cooldowns["wary"] = CombatTuning.WarySeconds;
            noticed[trial] = -1;
            Run(120, i => { if (noticed[trial] < 0 && watcher.Awareness == Awareness.Alerted) noticed[trial] = i; });
        }
        check(noticed[1] >= 0 && noticed[0] > noticed[1], $"a wary enemy notices sooner (wary {noticed[1]}, calm {noticed[0]})");

        // A body nothing hostile left there, and one seen falling, are not news.
        Arena(900, 660);
        finder = Spawn("b:finder", "prowler", 300, 360, V.UnitX);
        Corpse("b:old", 400, 360, slain: false);
        Run(60, _ => { finder.FacingX = 1; finder.FacingY = 0; });
        check(finder.Awareness == Awareness.Unaware && !Saw(CombatPresentationKind.Disturbed), "a body with no hostile hand in it is ignored");
    }

    static void Morale(Action<bool, string> check)
    {
        // The leader's fall breaks the pack.
        Arena();
        var alpha = Spawn("b:alpha", "alpha", 480 - 40, 360, V.UnitX); Engage(alpha);
        var pack = Enumerable.Range(0, 3).Select(i => Spawn("b:follower" + i, "prowler", 480 - 70, 310 + i * 50, V.UnitX)).ToArray();
        var brute = Spawn("b:steady", "brute", 480 - 110, 360, V.UnitX); Engage(brute);
        foreach (var a in pack) Engage(a);
        float[] before = pack.Select(a => V.Distance(a.Position, p.Position)).ToArray();
        check(w.FindProfile("alpha")!.Leader && w.Damage(p, alpha, 100000, "physical") && !alpha.Alive, "the alpha falls");
        check(pack.All(a => Saw(CombatPresentationKind.Shaken, a.Id, "leader") && a.Cooldowns.GetValueOrDefault("rout") > CombatTuning.RoutSeconds),
            "every follower that sees its leader fall loses its nerve");
        check(!Saw(CombatPresentationKind.Shaken, brute.Id) && brute.Cooldowns.GetValueOrDefault("rout") <= 0, "a creature of steady nerve does not");
        bool allFled = false; int fleeing = 0;
        Run(90, _ => { fleeing = pack.Count(a => a.TacticalIntent == "retreat"); allFled |= fleeing == pack.Length; foreach (var a in pack) a.Cooldowns["pounce"] = 5; });
        check(allFled && pack.Zip(before, (a, d) => V.Distance(a.Position, p.Position) > d + 20).All(x => x), "the routed pack runs from the fight");
        Run((int)(CombatTuning.LeaderRoutSeconds * 60) + 60);
        check(pack.All(a => a.Cooldowns.GetValueOrDefault("rout") <= 0 && a.Awareness == Awareness.Engaged && a.TacticalIntent != "retreat"), "its nerve returns and it comes back to fight");
        check(pack.All(a => a.Knowledge.GetValueOrDefault(alpha.Id) == "found") && !Saw(CombatPresentationKind.Disturbed), "a death they watched is not discovered again later");

        // An ordinary loss shakes only those whose nerve fails; morale is the chance of holding.
        var prowler = w.FindProfile("prowler")!; float morale = prowler.Morale;
        try
        {
            foreach (var (value, expected) in new[] { (0f, 3), (1f, 0) })
            {
                Arena();
                prowler.Morale = value;
                var fallen = Spawn("b:fallen", "prowler", 480 - 40, 360, V.UnitX); Engage(fallen);
                pack = Enumerable.Range(0, 3).Select(i => Spawn("b:witness" + i, "prowler", 480 - 80, 310 + i * 50, V.UnitX)).ToArray();
                foreach (var a in pack) Engage(a);
                var unseen = Spawn("b:unseen", "prowler", 480 - 80, 600, V.UnitX); Engage(unseen);
                for (int x = 0; x < r.Width; x++) r.Tiles[22 * r.Width + x] = Terrain.Wall;
                w.Damage(p, fallen, 100000, "physical");
                check(seen.Count(e => e.Kind == CombatPresentationKind.Shaken && e.Outcome == "loss") == expected && !Saw(CombatPresentationKind.Shaken, unseen.Id),
                    $"with morale {value:0} a packmate's fall shakes {expected} of 3 witnesses, and never one that did not see it");
            }
        }
        finally { prowler.Morale = morale; }

        // A runner brings in allies who had not noticed the fight.
        Arena();
        var runner = Spawn("b:runner", "prowler", 480 - 40, 360, V.UnitX); Engage(runner);
        runner.Cooldowns["rout"] = 6;
        var idle = Spawn("b:idle", "prowler", 480 - 230, 360, -V.UnitX);
        Run(150, _ => { if (idle.Awareness == Awareness.Unaware) { idle.FacingX = -1; idle.FacingY = 0; } runner.Cooldowns["pounce"] = 5; });
        check(Saw(CombatPresentationKind.Alerted, idle.Id, "called") && idle.Target == p.Id && runner.Cooldowns.GetValueOrDefault("fetch") > 0,
            "a routed enemy that reaches an unaware ally brings it into the fight");
        check(seen.Count(e => e.Kind == CombatPresentationKind.Alerted && e.ActorId == idle.Id) == 1, "and fetches it once");

        // The alpha's howl puts the pack in a frenzy.
        Arena();
        alpha = Spawn("b:alpha", "alpha", 480 - 120, 330, V.UnitX); Engage(alpha);
        var follower = Spawn("b:follower", "prowler", 480 - 120, 400, V.UnitX); Engage(follower);
        bool frenzy = false;
        Run(180, _ => frenzy |= follower.Statuses.Any(st => st.Id == "frenzy"));
        check(frenzy && Saw(CombatPresentationKind.Rallied, alpha.Id), "an alpha howls its pack into a frenzy");
    }

    static float Turned(V from, V to) => MathF.Abs(MathF.Atan2(from.X * to.Y - from.Y * to.X, V.Dot(from, to))) * 180 / MathF.PI;

    static void Shell(Action<bool, string> check)
    {
        var profile = w.FindProfile("shellback")!;
        Arena();
        var a = Spawn("b:shell", "shellback", 480 - 34, 360, V.UnitX); Engage(a);
        a.Health = a.MaxHealth = 1000; a.NextTacticalTime = 1e9;
        float defence = w.Stat(a, "defence");
        check(w.Shielded(a, p.Position) && w.Damage(p, a, 40, "physical") && Math.Abs(1000 - a.Health - Math.Max(1, 40 * profile.ShieldFactor - defence)) < .01f &&
            a.State is ActionState.Idle or ActionState.Moving && seen.Last(e => e.Kind == CombatPresentationKind.Hit).Outcome == "blocked",
            "a blow on a shellback's front is turned: little damage, no stagger");
        // From behind there is nothing to turn it.
        a.Invulnerable = 0; a.Health = 1000; a.X = 480 + 34; a.FacingX = 1; a.FacingY = 0;
        check(!w.Shielded(a, p.Position) && w.Damage(p, a, 40, "physical") && Math.Abs(1000 - a.Health - (40 - defence)) < .01f && a.State == ActionState.Staggered &&
            seen.Last(e => e.Kind == CombatPresentationKind.Hit).Outcome == "hit", "a blow from behind lands in full and staggers");
        // Its own attack opens it: the windup is still covered, the recovery is not.
        a.Invulnerable = 0; a.Health = 1000; w.Cancel(a); a.X = 480 - 34; a.FacingX = 1; a.FacingY = 0;
        a.State = ActionState.Windup; a.PendingAbility = "pincer"; a.StateRemaining = .4f;
        bool covered = w.Shielded(a, p.Position);
        a.State = ActionState.Recovery; a.PendingAbility = ""; a.RecoveryAbility = "pincer"; a.StateRemaining = .4f;
        check(covered && !w.Shielded(a, p.Position) && w.Damage(p, a, 40, "physical") && Math.Abs(1000 - a.Health - (40 - defence)) < .01f,
            "the shell is up through its windup and open while it recovers from its own attack");
        // Unnoticed, it has not raised its guard at all.
        Arena();
        a = Spawn("b:shell", "shellback", 480 - 34, 360, V.UnitX); a.Health = a.MaxHealth = 1000; a.NextTacticalTime = 1e9;
        check(!w.Shielded(a, p.Position) && w.Damage(p, a, 40, "physical") && Math.Abs(1000 - a.Health - (40 * CombatTuning.AmbushMultiplier - defence)) < .01f,
            "an unaware shellback takes the whole ambush");
        // Burning ignores the shell.
        Arena();
        a = Spawn("b:shell", "shellback", 480 - 34, 360, V.UnitX); Engage(a); a.Health = a.MaxHealth = 1000; a.NextTacticalTime = 1e9;
        w.Damage(p, a, 3, "fire", periodic: true);
        check(Math.Abs(1000 - a.Health - 3) < .01f, "damage over time is not turned by a shell");

        // It comes round slowly, and only strikes at what is in front of it.
        Arena();
        a = Spawn("b:shell", "shellback", 480 + 30, 360, V.UnitX); Engage(a);
        a.FacingX = 1; a.FacingY = 0; // Back to the player.
        float fastest = 0, offAxisAtStart = -1; var last = a.Facing; int startedAt = -1; bool hitFromBehind = false;
        Run(240, i =>
        {
            if (startedAt < 0 && a.State == ActionState.Windup)
            {
                startedAt = i;
                offAxisAtStart = Turned(last, V.Normalize(p.Position - a.Position));
            }
            if (startedAt < 0) fastest = Math.Max(fastest, Turned(last, a.Facing));
            last = a.Facing;
            hitFromBehind |= startedAt < 0 && i == 20 && !w.Shielded(a, p.Position);
        });
        check(fastest <= profile.TurnDegrees * Dt + .05f && fastest > 0, $"a shellback turns no faster than its rate ({fastest / Dt:0} of {profile.TurnDegrees:0} degrees a second)");
        check(hitFromBehind, "which leaves its back open to whoever gets round it");
        check(startedAt > (int)(60 * (180 - CombatTuning.TurnAttackHalfAngle) / profile.TurnDegrees) - 14 && offAxisAtStart >= 0 && offAxisAtStart <= CombatTuning.TurnAttackHalfAngle + 1,
            $"it starts its attack only once its quarry is in front of it (tick {startedAt}, {offAxisAtStart:0} degrees off)");
        check(p.Statuses.Any(st => st.Id == "snare") && w.Stat(p, "speed") < p.Speed - 20, "its pincer snares what it catches");
        p.Statuses.Clear();
    }

    static void Blast(Foundation app, Action<bool, string> check)
    {
        var def = w.FindAbility("glob")!;
        Arena();
        var a = Spawn("b:lobber", "spitter", 480 - 140, 360, V.UnitX); Engage(a); a.NextTacticalTime = 1e9;
        var mark = p.Position;
        check(w.Activate(a.Id, "glob", p.Id) && a.Aim == mark && w.Hazards.Count == 0, "a lobbed attack commits to where its target stands when it starts");
        float health = p.Health;
        Run((int)MathF.Round(def.Windup * 60) + 1, _ => a.NextTacticalTime = 1e9);
        check(w.Hazards.Count == 1 && !w.Hazards[0].Landed && w.Hazards[0].Position == mark && Saw(CombatPresentationKind.BlastLaunched, a.Id) && p.Health == health,
            "after the windup it is in the air, and nothing has been hurt yet");
        Run((int)MathF.Round(def.Blast!.Delay * 60) + 1, _ => a.NextTacticalTime = 1e9);
        check(Saw(CombatPresentationKind.BlastLanded, a.Id) && seen.Count(e => e.Kind == CombatPresentationKind.Hit && e.AbilityId == "glob" && e.Outcome == "hit") == 1 && p.Health < health,
            "it lands on a target that stayed put");
        check(w.Hazards.Count == 1 && w.Hazards[0].Landed && Math.Abs(w.Hazards[0].Remaining - def.Pool!.Duration) < .05f, "and leaves a pool behind");
        // Let the stagger of the landing itself pass, then stand in the pool and act.
        Run(20, _ => a.NextTacticalTime = 1e9);
        health = p.Health; bool interrupted = !w.TryStartPlayerSlash(V.UnitX);
        Run((int)(def.Pool.Duration * 60) + 5, _ => { a.NextTacticalTime = 1e9; interrupted |= p.State == ActionState.Staggered; });
        int burns = seen.Count(e => e.Kind == CombatPresentationKind.Hit && e.AbilityId == "glob" && e.Outcome == "periodic");
        check(burns >= (int)(def.Pool.Duration / def.Pool.Interval) - 1 && burns <= (int)(def.Pool.Duration / def.Pool.Interval) + 1 && p.Health < health && !interrupted,
            $"standing in the pool burns at each interval without interrupting ({burns} ticks)");
        check(w.Hazards.Count == 0, "the pool dries up");

        // Stepping off the mark: the landing misses, the pool stays where it was thrown.
        Arena();
        a = Spawn("b:lobber", "spitter", 480 - 140, 360, V.UnitX); Engage(a);
        w.Activate(a.Id, "glob", p.Id); mark = p.Position; health = p.Health;
        Run((int)MathF.Round(def.Windup * 60) + 2, _ => a.NextTacticalTime = 1e9);
        p.Y += 80;
        Run((int)((def.Blast.Delay + 2) * 60), _ => a.NextTacticalTime = 1e9);
        check(p.Health == health && !Saw(CombatPresentationKind.Hit) && w.Hazards.Count == 1 && w.Hazards[0].Position == mark, "stepping off the mark avoids both the landing and the pool");

        // Interrupting the throw: nothing leaves the hand.
        Arena();
        a = Spawn("b:lobber", "spitter", 480 - 140, 360, V.UnitX); Engage(a);
        w.Activate(a.Id, "glob", p.Id);
        Run(10, _ => a.NextTacticalTime = 1e9);
        w.Damage(p, a, 5, "physical");
        Run(120, _ => a.NextTacticalTime = 1e9);
        check(w.Hazards.Count == 0 && !Saw(CombatPresentationKind.BlastLaunched), "a throw interrupted in its windup never leaves the hand");

        // The thrower dies while it is in the air: it still lands and pools, but the landing itself is spent.
        Arena();
        a = Spawn("b:lobber", "spitter", 480 - 140, 360, V.UnitX); Engage(a);
        w.Activate(a.Id, "glob", p.Id);
        Run((int)MathF.Round(def.Windup * 60) + 2, _ => a.NextTacticalTime = 1e9);
        a.Invulnerable = 0; w.Damage(p, a, 100000, "physical"); seen.Clear();
        Run((int)MathF.Round(def.Blast.Delay * 60) + 2);
        check(!a.Alive && Saw(CombatPresentationKind.BlastLanded) && !seen.Any(e => e.Kind == CombatPresentationKind.Hit && e.Outcome == "hit") && w.Hazards.Count == 1 && w.Hazards[0].Landed,
            "a glob outlives its thrower: it lands without a blow and still pools");

        // Left to itself a spitter lobs from long range, and the flight survives a save exactly.
        Arena();
        a = Spawn("b:lobber", "spitter", 480 - 150, 360, V.UnitX); Engage(a);
        p.Invulnerable = 1000;
        bool lobbed = false; string snapshot = "", flight = "";
        Run(600, _ =>
        {
            a.Cooldowns["volley"] = 30;
            if (!lobbed && w.Hazards.Count == 1 && !w.Hazards[0].Landed && w.Hazards[0].Fuse < def.Blast.Delay * .6f) { lobbed = true; snapshot = Persistence.Capture(w, app.Narrative); flight = System.Text.Json.JsonSerializer.Serialize(w.Hazards); }
        });
        check(lobbed, "a spitter at long range lobs a glob of its own accord");
        if (lobbed)
        {
            Persistence.Restore(w, app.Narrative, snapshot);
            p = w.Player; r = w.RegionsById[r.Id];
            check(w.Hazards.Count == 1 && !w.Hazards[0].Landed && System.Text.Json.JsonSerializer.Serialize(w.Hazards) == flight,
                "a glob in the air survives a save exactly");
            seen.Clear();
            Run((int)(def.Blast.Delay * 60) + 2);
            check(Saw(CombatPresentationKind.BlastLanded) && w.Hazards.Count == 1 && w.Hazards[0].Landed, "and lands after loading");
            var node = JsonNode.Parse(snapshot)!; node.AsObject().Remove("Hazards");
            Persistence.Restore(w, app.Narrative, node.ToJsonString());
            p = w.Player; r = w.RegionsById[r.Id];
            check(w.Hazards.Count == 0, "a save from before hazards existed loads with clear ground");
        }
    }

    static void Burst(Action<bool, string> check)
    {
        var coil = w.FindAbility("coil")!;
        // All round the body: a quarry behind it is caught as surely as one in front.
        foreach (float side in new[] { 1f, -1f })
        {
            Arena();
            var a = Spawn("b:lasher", "lasher", 480 - 34 * side, 360, V.UnitX); Engage(a);
            a.FacingX = 1; a.FacingY = 0; a.NextTacticalTime = 1e9;
            float px = p.X, health = p.Health;
            check(w.Activate(a.Id, "coil", p.Id), "coil fixture starts");
            Run((int)MathF.Round(coil.Windup * 60) + 2, _ => a.NextTacticalTime = 1e9);
            check(p.Health < health && (p.X - px) * side > coil.Knockback * .6f, "a coiling lash catches and throws back a quarry " + (side > 0 ? "in front" : "behind"));
        }
        Arena();
        var lasher = Spawn("b:lasher", "lasher", 480 - 34, 360, V.UnitX); Engage(lasher); lasher.NextTacticalTime = 1e9;
        w.Activate(lasher.Id, "coil", p.Id);
        Run(8, _ => lasher.NextTacticalTime = 1e9);
        p.X += 40; float unhurt = p.Health;
        Run((int)MathF.Round(coil.Windup * 60), _ => lasher.NextTacticalTime = 1e9);
        check(p.Health == unhurt && Saw(CombatPresentationKind.Miss, lasher.Id), "stepping out of the ring makes it miss");

        // Choice by range: the fang lunge from a distance, the coil up close.
        Arena();
        lasher = Spawn("b:lasher", "lasher", 480 - 70, 360, V.UnitX); Engage(lasher);
        check(CombatBrain.Select(w, lasher, p, w.FindProfile("lasher"), false).Ability == "fang", "from a few lengths away a lasher lunges with its fangs");
        lasher.X = 480 - 34;
        check(CombatBrain.Select(w, lasher, p, w.FindProfile("lasher"), false).Ability == "coil", "up close it coils and lashes");
        lasher.X = 480 - 70;
        bool venom = false;
        Run(90, _ => venom |= p.Statuses.Any(st => st.Id == "venom"));
        check(venom && Saw(CombatPresentationKind.Hit, lasher.Id), "its fangs carry venom");
        p.Statuses.Clear();
    }

    static void Mender(Action<bool, string> check)
    {
        var status = w.Content.Statuses.Single(st => st.Id == "regrowth");
        int Blooms() => w.Events.Recent.Count(e => e.Kind == "ability_started" && e.Value == "bloom");
        // Everyone hale: nothing to mend.
        Arena();
        var m = Spawn("b:mender", "mender", 480 - 130, 320, V.UnitX); Engage(m);
        var ally = Spawn("b:patient", "brute", 480 - 110, 400, V.UnitX); Engage(ally);
        ally.NextTacticalTime = 1e9;
        w.Events.Recent.Clear();
        Run(150, _ => ally.NextTacticalTime = 1e9);
        check(Blooms() == 0, "a mender does not spend its bloom while everyone is hale");
        // A wounded ally in reach.
        ally.Health = ally.MaxHealth * .4f; float low = ally.Health;
        bool mending = false;
        Run(120, _ => { ally.NextTacticalTime = 1e9; mending |= ally.Statuses.Any(st => st.Id == "regrowth"); });
        check(Blooms() == 1 && mending && Saw(CombatPresentationKind.Rallied, m.Id), "it blooms when an ally in reach is wounded");
        Run((int)(status.Duration * 60), _ => { ally.NextTacticalTime = 1e9; m.Cooldowns["bloom"] = 30; });
        float gained = ally.Health - low;
        check(Math.Abs(gained - status.Heal * MathF.Floor(status.Duration / status.Interval)) <= status.Heal + .01f && !ally.Statuses.Any(st => st.Id == "regrowth"),
            $"regrowth restores health at each interval, then ends ({gained:0} health)");
        ally.Health = ally.MaxHealth - 1; w.ApplyStatus(ally, "regrowth", m.Id);
        Run(90, _ => { ally.NextTacticalTime = 1e9; m.Cooldowns["bloom"] = 30; });
        check(ally.Health == ally.MaxHealth, "mending never exceeds full health");
        // Hurt itself, with nobody else about, it tends its own wounds, and keeps away.
        Arena();
        m = Spawn("b:mender", "mender", 480 - 130, 360, V.UnitX); Engage(m);
        m.Health = m.MaxHealth * .7f; w.Events.Recent.Clear();
        float closest = float.PositiveInfinity;
        Run(240, _ => closest = Math.Min(closest, V.Distance(m.Position, p.Position)));
        check(Blooms() >= 1 && m.Statuses.Any(st => st.Id == "regrowth") || m.Health > m.MaxHealth * .7f, "a wounded mender tends itself");
        check(closest > w.FindProfile("mender")!.MinimumDistance - 12, $"and keeps its distance rather than closing in ({closest:0})");
    }

    static void Saving(Foundation app, Action<bool, string> check)
    {
        Arena();
        Spawn("b:s0", "prowler", 480 - 150, 330, V.UnitX);
        Spawn("b:s1", "prowler", 480 - 200, 400, -V.UnitX);
        Spawn("b:s2", "brute", 480 - 130, 250, V.UnitX);
        Spawn("b:s3", "spitter", 480 + 160, 300, -V.UnitX);
        Spawn("b:s4", "stalker", 480 + 60, 440, V.UnitX);
        Spawn("b:s5", "howler", 480 - 120, 470, V.UnitX);
        Spawn("b:s6", "alpha", 480 - 180, 300, V.UnitX);
        Spawn("b:s7", "shellback", 480 + 120, 420, -V.UnitX);
        Spawn("b:s8", "lasher", 480 + 150, 240, -V.UnitX);
        Spawn("b:s9", "mender", 480 - 210, 440, V.UnitX).Sleeper = true;
        string Digest() => System.Text.Json.JsonSerializer.Serialize(w.Actors.Values.Where(a => a.Id.StartsWith("b:") || a.Id == "player").OrderBy(a => a.Id, StringComparer.Ordinal)
            .Select(a => new { a.Id, a.X, a.Y, a.Health, a.Awareness, a.Alertness, a.TacticalIntent, a.State, a.PendingAbility, a.Poise, a.FacingX, a.FacingY, a.Target, a.Sleeper, a.AimX, a.AimY })) +
            System.Text.Json.JsonSerializer.Serialize(w.Hazards);
        // Save in the middle of the alarm, and again in the thick of the fight.
        foreach (int lead in new[] { 25, 150, 400 })
        {
            Run(lead);
            string snapshot = Persistence.Capture(w, app.Narrative);
            Run(360);
            string first = Digest();
            Persistence.Restore(w, app.Narrative, snapshot);
            p = w.Player; r = w.RegionsById[r.Id];
            check(Persistence.Capture(w, app.Narrative) == snapshot, "awareness, facing and guard survive a save exactly (tick " + lead + ")");
            Run(360);
            check(first == Digest(), "the fight after loading repeats the fight before it (tick " + lead + ")");
            Persistence.Restore(w, app.Narrative, snapshot);
            p = w.Player; r = w.RegionsById[r.Id];
        }
        check(w.Actors.Values.Any(a => a.Id.StartsWith("b:") && a.Awareness == Awareness.Engaged), "the saved fight was a real engagement");

        // A save written before perception existed has none of the new fields.
        var node = JsonNode.Parse(Persistence.Capture(w, app.Narrative))!;
        string[] added = { "Awareness", "Alertness", "AwarenessRemaining", "LastKnownX", "LastKnownY", "LastSeenTime", "FacingX", "FacingY", "Poise", "PoiseDelay", "Called", "ActionConnected", "Sleeper", "AimX", "AimY" };
        node.AsObject().Remove("Hazards");
        foreach (var actor in node["Actors"]!.AsObject())
        {
            foreach (string field in added) actor.Value!.AsObject().Remove(field);
            if (actor.Key.StartsWith("b:")) actor.Value!["CombatProfile"] = actor.Key == "b:s1" ? "" : actor.Value["CombatProfile"]!.GetValue<string>();
        }
        Persistence.Restore(w, app.Narrative, node.ToJsonString());
        p = w.Player; r = w.RegionsById[r.Id];
        check(w.Actors.Values.Where(a => a.Id.StartsWith("b:")).All(a => a.Awareness == Awareness.Unaware && a.Facing == V.Zero && a.Poise == 0 && !a.Sleeper) && w.Hazards.Count == 0,
            "an older save loads with every actor calm, awake and on clear ground");
        Run(600);
        check(w.Actors["b:s1"].CombatProfile == "" && w.Actors.Values.Count(a => a.Id.StartsWith("b:") && a.Awareness == Awareness.Engaged) >= 3 &&
            w.Actors.Values.All(a => float.IsFinite(a.X) && float.IsFinite(a.Y)), "actors from an older save notice and fight with the new senses, profiled or not");
    }

    static void Authoring(Action<bool, string> check)
    {
        var items = new HashSet<string> { "potion", "iron_sword", "oak_staff", "coin", "herb", "ore", "wood" };
        bool Rejected(Action<Content> spoil, string expected)
        {
            var content = Content.Load(true);
            spoil(content);
            return content.Validate(items).Any(e => e.Contains(expected));
        }
        check(Content.Load(true).Validate(items).Count == 0, "authored enemy content validates");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "pounce").Lunge!.End = 9, "invalid lunge"), "a lunge outlasting its action is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "charge").Geometry!.Range = 10, "line attack"), "a charge whose telegraph disagrees with its run is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "charge").Charge = null, "line attack"), "a line attack without a charge is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "volley").Projectile!.Count = 0, "invalid volley"), "an empty volley is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "screech").Self = false, "ally radius"), "an ally radius on a hostile ability is rejected");
        check(Rejected(c => c.CombatProfiles.Single(x => x.Id == "brute").EnrageAbility = "", "half authored"), "an enrage threshold without its ability is rejected");
        check(Rejected(c => c.CombatProfiles.Single(x => x.Id == "howler").RallyAbility = "bite", "not a self ability"), "a rally that is not a self ability is rejected");
        check(Rejected(c => c.CombatProfiles.Single(x => x.Id == "prowler").LeashDistance = 10, "invalid senses"), "a leash shorter than the pursuit is rejected");
        check(Rejected(c => c.CombatProfiles.Single(x => x.Id == "prowler").EvadeCooldown = 0, "invalid senses"), "evasion without a cooldown is rejected");
        check(Rejected(c => c.Statuses.Single(x => x.Id == "rage").Modifiers["luck"] = 1, "status rage"), "a status modifier on an unknown stat is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "glob").Blast!.Radius = 20, "blast attack"), "a blast whose warning disagrees with its radius is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "glob").Blast = null, "blast attack"), "a blast shape without a blast is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "spit").Pool = new() { Duration = 2, Damage = 1 }, "invalid pool"), "a pool on an attack that is not lobbed is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "glob").Pool!.Damage = 0, "invalid pool"), "a pool that does nothing is rejected");
        check(Rejected(c => c.Abilities.Single(a => a.Id == "coil").Geometry!.Range = 10, "invalid burst"), "a burst that cannot reach what it is aimed at is rejected");
        check(Rejected(c => c.CombatProfiles.Single(x => x.Id == "shellback").TurnDegrees = 0, "shield, turning or morale"), "a shield that could never be got round is rejected");
        check(Rejected(c => c.CombatProfiles.Single(x => x.Id == "alpha").Morale = .5f, "shield, turning or morale"), "a leader whose own nerve can break is rejected");
        check(Rejected(c => c.Statuses.Single(x => x.Id == "regrowth").Damage = 1, "status regrowth"), "a status that both mends and harms is rejected");
        check(EnemyRoster.Archetypes.Keys.OrderBy(x => x).SequenceEqual(Content.Load().CombatProfiles.Select(x => x.Id).OrderBy(x => x)),
            "every combat profile has an authored body and every body a profile");
        var profiles = Content.Load().CombatProfiles;
        check(profiles.All(x => x.Loadout().SequenceEqual(x.Loadout().Distinct().OrderBy(id => id, StringComparer.Ordinal))) &&
            profiles.Single(x => x.Id == "brute").Loadout().Contains("bellow") && profiles.Single(x => x.Id == "howler").Loadout().Contains("screech"),
            "a loadout carries the profile's attacks and its own abilities in stable order");
    }
}
