using Godot;
using V = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>
/// A scripted duelist fights each combat-lab encounter at normal health through the production
/// world step, and the outcomes are tabulated. For comparing encounters with each other and a
/// change against the run before it; the duelist is competent, not clever, so its numbers say
/// how fights compare, not how they feel in the hand.
/// <c>godot --headless --path . -- --enemy-encounter-report[=alpha,pack]</c> writes
/// build/enemy-encounters.json and prints the table. It asserts nothing.
/// </summary>
public static class EnemyEncounterReport
{
    public static readonly string[] Encounters =
        { "prowler", "brute", "spitter", "stalker", "lasher", "shellback", "howler", "mender", "sleepers", "pack", "alpha", "four mixed", "ruin", "depths" };
    public static readonly ulong[] Seeds = { 42, 43, 44 };
    const float Dt = 1f / 60;
    const int Limit = 150 * 60;

    public sealed record Result(string Encounter, ulong Seed, string Outcome, float Seconds, int Enemies, int Fallen, float DamageTaken, float HealthLeft, int Potions,
        int Swings, int SwingsLanded, int Ambushes, int Blocked, int Rolls, Dictionary<string, int> HitsTaken);

    /// <summary>One fight. The duelist uses only what a player has: movement, sword, roll, ember, mend and potions.</summary>
    public static Result Fight(Foundation app, string encounter, ulong seed)
    {
        var w = app.World;
        app.BeginCombatCourse(encounter, false, seed);
        var p = w.Player;
        var foes = w.Actors.Values.Where(a => a.Id.StartsWith("lab:")).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
        float taken = 0; int swings = 0, landed = 0, ambushes = 0, blocked = 0, rolls = 0, potions = 0;
        var hits = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Observe(CombatPresentationEvent e)
        {
            if (e.Kind != CombatPresentationKind.Hit) return;
            if (e.TargetId == "player")
            {
                string cause = e.Outcome == "periodic" && e.AbilityId == "" ? "status" : e.AbilityId == "" ? "other" : e.AbilityId + (e.Outcome == "periodic" ? " pool" : "");
                hits[cause] = hits.GetValueOrDefault(cause) + 1;
            }
            else if (e.ActorId == "player" && e.AbilityId == "slash")
            {
                landed++;
                if (e.Outcome == "ambush") ambushes++;
                if (e.Outcome == "blocked") blocked++;
            }
        }
        w.Presentation += Observe;
        int tick = 0; string outcome = "timeout";
        try
        {
            float last = p.Health;
            for (; tick < Limit; tick++)
            {
                if (!p.Alive) { outcome = "died"; break; }
                if (foes.All(a => !a.Alive)) { outcome = "won"; break; }
                if (p.Health < last) taken += last - p.Health;
                last = p.Health;
                if (p.Health < p.MaxHealth * .45f && w.UsePotion(p)) { potions++; last = p.Health; }
                if (p.Health < p.MaxHealth * .65f && w.Activate(p.Id, "mend", p.Id)) last = p.Health;
                var target = foes.Where(a => a.Alive).OrderBy(a => w.LineOfSight(p, a) ? 0 : 1).ThenBy(a => V.DistanceSquared(a.Position, p.Position)).ThenBy(a => a.Id, StringComparer.Ordinal).First();
                var offset = target.Position - p.Position; float distance = offset.Length();
                var toward = distance > 1 ? offset / distance : V.UnitX;
                var side = new V(-toward.Y, toward.X);
                var move = distance > 36 ? toward : V.Zero;
                // Off anything warned or lying on the ground.
                foreach (var h in w.Hazards)
                    if (w.FindAbility(h.Ability)?.Blast is { } blast && h.Region == p.Region && V.Distance(h.Position, p.Position) < blast.Radius + 14)
                        move = CombatGeometry.TryNormalize(p.Position - h.Position, out var away) ? away + side * .4f : side;
                // Out of the way of whatever is being wound up at close quarters.
                var threat = foes.FirstOrDefault(a => a.Alive && a.State == ActionState.Windup && w.FindAbility(a.PendingAbility) is { } pending &&
                    (pending.Blast != null ? V.Distance(a.Aim, p.Position) < pending.Blast.Radius + 10 : V.Distance(a.Position, p.Position) < 95 + (pending.Charge?.Distance ?? 0)));
                if (threat != null && w.FindAbility(threat.PendingAbility) is { } danger)
                {
                    var from = danger.Blast != null ? threat.Aim : threat.Position;
                    var clear = CombatGeometry.TryNormalize(p.Position - from, out var back) ? back : -toward;
                    // Sideways out of lanes and cones, straight back out of rings.
                    var roll = danger.Geometry?.Shape is "burst" or "blast" ? clear : side;
                    if (threat.StateRemaining < .3f && w.TryStartPlayerDodge(roll)) rolls++;
                    else move = roll;
                }
                // Round a raised shell to its back; otherwise strike when in reach.
                else if (w.Shielded(target, p.Position) && distance < 70) move = side + toward * (distance > 34 ? .5f : -.2f);
                else if (distance < 58 && w.TryStartPlayerSlash(toward)) swings++;
                else if (distance > 70) w.Activate(p.Id, "ember", target.Id);
                w.PlayerMove(move, Dt);
                w.Step(Dt);
            }
        }
        finally { w.Presentation -= Observe; }
        return new(encounter, seed, outcome, tick * Dt, foes.Length, foes.Count(a => !a.Alive), taken, p.Alive ? p.Health : 0, potions, swings, landed, ambushes, blocked, rolls, new(hits));
    }

    public static void Write(Foundation app, string directory, string? only)
    {
        Directory.CreateDirectory(directory);
        var w = app.World; string snapshot = Persistence.Capture(w, app.Narrative);
        var results = new List<Result>();
        try
        {
            foreach (string encounter in Encounters)
            {
                if (only != null && !only.Split(',').Contains(encounter)) continue;
                foreach (ulong seed in Seeds) results.Add(Fight(app, encounter, seed));
            }
        }
        finally
        {
            if (app.InFeelCourse) app.LeaveFeelCourse();
            Persistence.Restore(w, app.Narrative, snapshot); app.RebuildViews();
        }
        File.WriteAllText(Path.Combine(directory, "enemy-encounters.json"), System.Text.Json.JsonSerializer.Serialize(results, Content.Json));
        GD.Print("ENEMY ENCOUNTER REPORT  (scripted duelist, normal health, seeds " + string.Join("/", Seeds) + ")");
        GD.Print($"{"encounter",-12} {"won",5} {"seconds",8} {"damage",7} {"left",5} {"potions",7} {"landed",7} {"blocked",7} {"rolls",5}  most hurt by");
        foreach (var group in results.GroupBy(x => x.Encounter))
        {
            var worst = group.SelectMany(x => x.HitsTaken).GroupBy(x => x.Key).Select(x => (x.Key, Count: x.Sum(y => y.Value))).OrderByDescending(x => x.Count).ThenBy(x => x.Key, StringComparer.Ordinal).Take(3);
            GD.Print($"{group.Key,-12} {group.Count(x => x.Outcome == "won") + "/" + group.Count(),5} {group.Average(x => x.Seconds),8:0.0} {group.Average(x => x.DamageTaken),7:0} {group.Average(x => x.HealthLeft),5:0} " +
                $"{group.Average(x => x.Potions),7:0.0} {group.Sum(x => x.SwingsLanded) + "/" + group.Sum(x => x.Swings),7} {group.Sum(x => x.Blocked),7} {group.Sum(x => x.Rolls),5}  {string.Join(", ", worst.Select(x => x.Key + " x" + x.Count))}");
        }
        GD.Print("ENEMY ENCOUNTER REPORT WRITTEN " + Path.Combine(directory, "enemy-encounters.json"));
    }
}
