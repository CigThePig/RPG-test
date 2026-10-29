using V = System.Numerics.Vector2;
namespace Mosslight;
public static class CombatAiChecks
{
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World; string saved = Persistence.Capture(w, app.Narrative);
        try
        {
            var r = new RegionState { Id = "tactics", Width = 40, Height = 30, PlacementRevision = 1, Tiles = Enumerable.Repeat(Terrain.Floor, 1200).ToArray() };
            w.RegionsById[r.Id] = r;
            foreach (var actor in w.Actors.Values) actor.Region = "reach";
            var p = w.Player; p.Region = r.Id; p.X = 500; p.Y = 350;
            var a = new ActorState { Id = "ai", Region = r.Id, Faction = "beasts", X = 400, Y = 350, HomeX = 400, HomeY = 350, Abilities = new() { "bite", "slam", "spit" } };
            AnatomyAdapter.Apply(a);
            w.Actors[a.Id] = a;
            var melee = w.Content.CombatProfiles.Single(x => x.Id == "prowler");
            var ranged = w.Content.CombatProfiles.Single(x => x.Id == "spitter");
            check(CombatBrain.Select(w, a, p, melee, false).Ability == "spit", "tactics uses authoritative available attack in range");
            a.Abilities = new() { "bite" };
            check(CombatBrain.Select(w, a, p, melee, false).Kind == "approach", "melee closes when no usable attack");
            a.Abilities = new() { "spit", "bite" }; a.Cooldowns["spit"] = 4;
            check(CombatBrain.Select(w, a, p, ranged, false).Kind != "ability", "cooldown is checked by SPELL eligibility");
            a.Cooldowns.Clear(); a.Mana = 0;
            check(CombatBrain.Select(w, a, p, ranged, false).Kind != "ability", "insufficient mana does not repeatedly request unavailable spit");
            a.Mana = 60; a.X = 460;
            check(CombatBrain.Select(w, a, p, ranged, false).Kind == "retreat", "spitter retreats when crowded");
            a.TacticalIntent = "retreat"; a.X = 424;
            check(CombatBrain.Select(w, a, p, ranged, false).Kind == "retreat", "retreat hysteresis persists above entry boundary");
            a.TacticalIntent = ""; a.X = 475;
            check(CombatBrain.Select(w, a, p, melee, false).Ability == "bite", "prowler prefers quick close attack");
            var brute = w.Content.CombatProfiles.Single(x => x.Id == "brute"); a.Abilities.Add("slam");
            check(CombatBrain.Select(w, a, p, brute, false).Ability == "slam", "brute prefers committed heavy attack");
            var companion=new ActorState{Id="nearby-ally",Faction=a.Faction,Region=r.Id,X=a.X+4,Y=a.Y};w.Actors[companion.Id]=companion;
            var uncrowd=CombatBrain.Select(w,a,p,melee,false);
            check(uncrowd.Kind=="circle" && r.CanTravel(a.Position,uncrowd.Destination,a),"crowded melee chooses a valid actor-phased approach position instead of piling into a strike");
            w.Actors.Remove(companion.Id);
            foreach (var state in new[] { ActionState.Windup, ActionState.Recovery, ActionState.Staggered, ActionState.Dead })
            { a.State = state; check(CombatBrain.Select(w, a, p, melee, false).Kind == "wait", "tactics respects " + state); }
            a.State = ActionState.Idle; a.Faction = p.Faction;
            check(CombatBrain.Select(w, a, p, melee, false).Kind == "wait", "tactics rejects friendly target"); a.Faction = "beasts";
            var first = CombatBrain.Select(w, a, p, brute, false);
            check(first == CombatBrain.Select(w, a, p, brute, false), "identical tactical state produces identical intention");
            check(CombatBrain.Select(w, a, p, null, false).Kind == "ability", "legacy missing profile safely uses real eligible abilities");
            // Wall and corner retreat candidates remain terrain checked.
            for (int y = 0; y < r.Height; y++) r.Tiles[y * r.Width + 18] = Terrain.Wall;
            a.X = 18 * 24 + 24 + a.Radius; a.Y = 350;
            var escape = CombatBrain.Select(w, a, p, ranged, true);
            check(escape.Destination == a.Position || r.CanTravel(a.Position, escape.Destination, a), "retreat chooses bounded valid local route near wall");
            // Production navigation, no stand-in movement executor.
            a.X = 400; a.Y = 250; a.Target = ""; a.CombatProfile = "prowler"; a.NextTacticalTime = 0;
            var dest = new V(400, 400); w.Follow(a, dest, .2f);
            check(a.MotionY > 0 && float.IsFinite(a.MotionX), "production follow finds clear corridor waypoint");
            r.Tiles=Enumerable.Repeat(Terrain.Floor,1200).ToArray();
            a.X=400;a.Y=350;a.State=ActionState.Idle;a.CombatProfile="prowler";a.Abilities=new(){"bite"};a.Cooldowns.Clear();a.Mana=60;a.NextTacticalTime=0;a.Target="";
            p.X=500;p.Y=350;p.Invulnerable=1000;p.Health=p.MaxHealth=10000;
            string fixture=Persistence.Capture(w,app.Narrative);
            string Run()
            {
                for(int i=0;i<240;i++)w.Step(1f/60);
                var actor=w.Actors["ai"];
                return System.Text.Json.JsonSerializer.Serialize(new{actor.X,actor.Y,actor.TacticalIntent,actor.State,actor.PendingAbility,actor.Cooldowns},Content.Json);
            }
            string firstRun=Run();
            check(w.Actors["ai"].X>440 && w.Events.Recent.Any(e=>e.Kind=="ability_started" && e.Actor=="ai"),"production tactical loop approaches and attacks with authored profile");
            Persistence.Restore(w,app.Narrative,fixture);
            check(firstRun==Run(),"fixed-step tactical movement and attack sequence repeats across restore");
            foreach(var state in new[]{ActionState.Windup,ActionState.Recovery,ActionState.Staggered,ActionState.Dead})
            {
                var actor=w.Actors["ai"];actor.State=state;actor.StateRemaining=.5f;actor.Health=state==ActionState.Dead?0:100;
                actor.PendingAbility=state==ActionState.Windup?"bite":"";actor.RecoveryAbility=state==ActionState.Recovery?"bite":"";
                var restored=Persistence.Parse(Persistence.Capture(w,app.Narrative),w.Content).Actors["ai"];
                check(restored.State==state && restored.CombatProfile=="prowler", "combat profile/state restoration: "+state);
            }
            var invalid=Content.Load(true);invalid.CombatProfiles[0].MinimumDistance=1000;
            check(invalid.Validate(new(){"potion","iron_sword","oak_staff","coin","herb","ore","wood"}).Any(e=>e.Contains("combat profile")),"contradictory combat distances rejected");
            w.Player.Level=100;w.Player.SkillPoints=20;
            check(!Progression.Unlock(w,w.Player,"slam") && !Progression.Unlock(w,w.Player,"spit") && w.Player.SkillPoints==20,"enemy-only content does not introduce player unlocks/actions");
            foreach (int seed in new[] { 1, 42, 1337 })
            {
                Persistence.Restore(w, app.Narrative, saved);
                w.Seed = seed;
                foreach (string id in w.Actors.Keys.Where(x => x.StartsWith("ruin:spawn:")).ToArray()) w.Actors.Remove(id);
                w.Enter(w.Player, "ruin");
                var spawned = w.Actors.Values.Where(x => x.Id.StartsWith("ruin:spawn:")).OrderBy(x => x.Id).ToArray();
                check(spawned.Length == 8 && new[] { "prowler", "brute", "spitter" }.All(id => spawned.Any(a => a.CombatProfile == id)), "seed " + seed + " spawns all archetypes");
                check(spawned.All(a => w.RegionsById[a.Region].CanStand(a.Position, a) && a.Abilities.SequenceEqual(w.Content.CombatProfiles.Single(x => x.Id == a.CombatProfile).Priorities.Keys.OrderBy(x => x, StringComparer.Ordinal))), "seed " + seed + " preserves spawn terrain and authored abilities");
            }
        }
        finally { Persistence.Restore(w, app.Narrative, saved); app.RebuildViews(); }
    }
}
