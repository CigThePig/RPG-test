using System.Text.Json;
using System.Text.Json.Nodes;
using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Lever shortcuts and brazier-ring light seals: generation, rules, interaction, saves.</summary>
public static class MechanismChecks
{
    // Tile steps from a tile to the entrance, ignoring footprints; gates closed unless listed.
    static int Distance(RegionState r, int from, ISet<int> open)
    {
        var seen = new Dictionary<int, int> { [from] = 0 }; var queue = new Queue<int>(); queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int at = queue.Dequeue();
            if (at == r.Entrance) return seen[at];
            foreach (int n in r.Neighbours(at))
                if (!seen.ContainsKey(n) && r.Tiles[n] == Terrain.Floor && (!RegionState.IsGate(r.Features.GetValueOrDefault(n)) || open.Contains(n)))
                { seen[n] = seen[at] + 1; queue.Enqueue(n); }
        }
        return int.MaxValue;
    }

    public static void Run(Foundation app, Action<bool, string> check)
    {
        var w = app.World; var n = app.Narrative; string original = Persistence.Capture(w, n);
        try
        {
            Generation(check);
            Rules(check);
            Persistence.Restore(w, n, original);
            Interaction(app, check);
            Persistence.Restore(w, n, original);
            Saves(app, check);
        }
        finally { Persistence.Restore(w, n, original); }
    }

    static void Generation(Action<bool, string> check)
    {
        int leverSeeds = 0, puzzleSeeds = 0, shorter = 0, guarding = 0, rings = 0;
        var solutionLengths = new HashSet<int>();
        foreach (int seed in Enumerable.Range(0, 64).Append(1337))
        {
            var r = Regions.Dungeon(seed); var repeat = Regions.Dungeon(seed);
            check(r.Lit.SetEquals(repeat.Lit) && JsonSerializer.Serialize(r.Passages) == JsonSerializer.Serialize(repeat.Passages), "mechanisms are deterministic " + seed);
            var controls = DungeonMechanisms.Controls(r);
            check(controls.All(c => c.Gates.Length > 0 && c.Gates.Length % 6 == 0), "every mechanism controls whole gates " + seed);
            check(r.Passages.All(p => p.Mechanism == "" || !p.Locked), "key locks dominate mechanism doors " + seed);
            check(r.Features.Where(f => f.Value is "portcullis" or "seal").All(f => controls.Any(c => c.Gates.Contains(f.Key))), "no orphan mechanism gate " + seed);
            check(r.Lit.All(at => r.Features.GetValueOrDefault(at) == "brazier"), "only braziers burn " + seed);
            var reach = DungeonValidator.Reach(r, allKeyGatesOpen: true);
            check(controls.All(c => c.Controllers.Any(reach.Contains)), "every lever and brazier ring is reachable " + seed);
            check(DungeonValidator.Solvable(r), "mission solvable with closed mechanism gates " + seed);
            if (controls.Any(c => c.Kind == "lever")) leverSeeds++;
            if (controls.Any(c => c.Kind == "puzzle")) puzzleSeeds++;
            foreach (var c in controls)
            {
                if (c.Kind == "lever")
                {
                    check(r.Features[c.Controllers[0]] == "lever" && !r.Depleted.Contains(c.Controllers[0]), "fresh lever is unpulled " + seed);
                    var open = r.Features.Where(f => f.Value is "lock" or "seal").Select(f => f.Key).ToHashSet();
                    int closed = Distance(r, c.Controllers[0], open); open.UnionWith(c.Gates);
                    int raised = Distance(r, c.Controllers[0], open);
                    check(raised <= closed && raised < int.MaxValue, "a raised portcullis never lengthens the way out " + seed);
                    // Lever side to entrance with every key gate still locked: a raised portcullis may bypass them.
                    var seals = r.Features.Where(f => f.Value == "seal").Select(f => f.Key).ToHashSet();
                    bool bypass = Distance(r, c.Controllers[0], seals) == int.MaxValue && Distance(r, c.Controllers[0], seals.Concat(c.Gates).ToHashSet()) < int.MaxValue;
                    if (raised < closed || bypass) shorter++;
                }
                else
                {
                    rings++;
                    check(c.Controllers.Length == DungeonMechanisms.RingSize && !DungeonMechanisms.Solved(r, c.Controllers), "fresh brazier ring of four starts unsolved " + seed);
                    var answer = DungeonMechanisms.Solution(r, c.Controllers);
                    check(answer.Length >= 2, "brazier ring needs at least two lightings " + seed); solutionLengths.Add(answer.Length);
                    var room = r.Rooms.Single(q => q.X == c.Controllers[0] % r.Width / DungeonGeometry.Cell && q.Y == c.Controllers[0] / r.Width / DungeonGeometry.Cell);
                    check(c.Controllers.All(at => room.Contains(at % r.Width, at / r.Width) && r.Tiles[at] == Terrain.Floor), "braziers stand on the puzzle room floor " + seed);
                    // Seals are genuine gates: without the ring, the relic or a key route may close.
                    var copy = JsonSerializer.Deserialize<RegionState>(JsonSerializer.Serialize(r, Content.Json), Content.Json)!;
                    foreach (int at in c.Controllers) copy.Features.Remove(at);
                    copy.Lit.Clear();
                    if (!DungeonValidator.Solvable(copy)) guarding++;
                }
            }
        }
        check(leverSeeds >= 16 && puzzleSeeds >= 16, $"grammar levers/puzzles become mechanisms across seeds (levers {leverSeeds}, puzzles {puzzleSeeds})");
        check(shorter * 3 >= leverSeeds * 2, $"most lever portcullises shorten the way out or bypass locked gates ({shorter}/{leverSeeds})");
        check(guarding >= 1, $"some light seals guard the mission itself ({guarding}/{rings})");
        check(solutionLengths.Count >= 2, "brazier rings vary in difficulty");
    }

    static void Rules(Action<bool, string> check)
    {
        var r = Regions.Dungeon(7);
        var ring = DungeonMechanisms.Controls(r).First(c => c.Kind == "puzzle");
        var b = ring.Controllers;
        // Every one of the sixteen ring states has exactly one minimal answer that solves it.
        for (int state = 0; state < 16; state++)
        {
            r.Lit.Clear(); r.Depleted.ExceptWith(b); r.OpenDoors.ExceptWith(ring.Gates);
            for (int i = 0; i < 4; i++) if ((state & (1 << i)) != 0) r.Lit.Add(b[i]);
            var answer = DungeonMechanisms.Solution(r, b);
            check(state == 15 ? answer.Length == 0 : answer.Length > 0, "ring state has an answer " + state);
            foreach (int at in answer) DungeonMechanisms.Kindle(r, at, out _);
            check(state == 15 || (DungeonMechanisms.Solved(r, b) && ring.Gates.All(r.OpenDoors.Contains) && b.All(r.Depleted.Contains)), "answer lights the whole ring and breaks its seal " + state);
        }
        // Lighting flips exactly the brazier and its two ring neighbours.
        r.Lit.Clear(); r.Depleted.ExceptWith(b); r.OpenDoors.ExceptWith(ring.Gates);
        check(DungeonMechanisms.Kindle(r, b[0], out bool solved) && !solved && r.Lit.SetEquals(new[] { b[3], b[0], b[1] }), "kindling flips self and both neighbours");
        // One dark brazier opposite b[0] needs the three others lit in turn: no single flip fixes it.
        check(DungeonMechanisms.Kindle(r, b[1], out solved) && !solved && DungeonMechanisms.Kindle(r, b[2], out solved) && !solved, "partial answers keep the ring unsolved");
        check(DungeonMechanisms.Kindle(r, b[3], out solved) && solved && ring.Gates.All(r.OpenDoors.Contains), "third lighting completes the ring");
        check(!DungeonMechanisms.Kindle(r, b[1], out _), "solved braziers are inert");
        // Closed mechanism gates block movement, sight and enemy routing.
        var actor = new ActorState { Id = "probe", Region = "ruin", Radius = 11 };
        r.OpenDoors.ExceptWith(ring.Gates);
        check(ring.Gates.All(at => !r.Passable(at, actor) && r.BlocksSight(at) && r.ClosedGate(at)), "closed seal blocks footprint and sight");
        check(ring.Gates.All(at => r.Passable(at, actor, gatesOpen: true)), "placement queries may treat seals as open");
        var lever = Regions.Dungeon(42); var control = DungeonMechanisms.Controls(lever).Single(c => c.Kind == "lever");
        check(control.Gates.All(at => !lever.Passable(at, actor)), "lowered portcullis blocks movement");
        var raised = DungeonMechanisms.Pull(lever, control.Controllers[0]);
        check(raised.SequenceEqual(control.Gates) && control.Gates.All(at => lever.Passable(at, actor) && !lever.BlocksSight(at)), "lever raises exactly its portcullis");
        check(DungeonMechanisms.Pull(lever, control.Controllers[0]).Length == 0, "a pulled lever cannot pull again");
    }

    static void Interaction(Foundation app, Action<bool, string> check)
    {
        var w = app.World; var n = app.Narrative; var p = w.Player;
        foreach (var a in w.Actors.Values.Where(a => a.Id != "player")) a.Region = "reach";
        var events = new List<WorldEvent>(); void Observe(WorldEvent e) => events.Add(e);
        w.Events.Published += Observe;
        try
        {
            // Lever shortcut: the far side cannot open it by hand.
            var r = Regions.Dungeon(42); w.RegionsById["ruin"] = r; p.Region = "ruin"; w.Cancel(p);
            var control = DungeonMechanisms.Controls(r).Single(c => c.Kind == "lever");
            int lever = control.Controllers[0];
            var leverBlock = (X: lever % r.Width / DungeonGeometry.Cell, Y: lever / r.Width / DungeonGeometry.Cell);
            int far = control.Gates.SelectMany(r.Neighbours).Distinct().Order().First(at => !control.Gates.Contains(at) && r.CanStand(r.Center(at), p) &&
                (at % r.Width / DungeonGeometry.Cell, at / r.Width / DungeonGeometry.Cell) != leverBlock);
            p.X = r.Center(far).X; p.Y = r.Center(far).Y;
            check(w.InteractionTargets(p).Any(t => t.Value == "portcullis") && !w.Interact(p) && w.InteractionFeedback.Contains("lever") && !control.Gates.Any(r.OpenDoors.Contains), "far-side portcullis explains its lever and stays shut");
            p.X = r.Center(lever).X; p.Y = r.Center(lever).Y; events.Clear();
            check(w.Interact(p) && control.Gates.All(r.OpenDoors.Contains) && events.Count(e => e.Kind == "lever_pulled") == 1, "production lever pull raises the shortcut once");
            check(!w.InteractionTargets(p).Any(t => t.Key == lever), "pulled lever leaves the interaction list");
            check(n.Call("journal").AsString().Contains("1 shortcut raised") && n.Call("journal").AsString().Contains("portcullis shortcuts"), "journal records the shortcut and its lore");

            // Brazier ring: seal hint, partial feedback, final break event.
            r = Regions.Dungeon(7); w.RegionsById["ruin"] = r;
            var ring = DungeonMechanisms.Controls(r).First(c => c.Kind == "puzzle");
            int stand = ring.Gates.SelectMany(r.Neighbours).Distinct().Order().First(at => !ring.Gates.Contains(at) && r.CanStand(r.Center(at), p));
            p.X = r.Center(stand).X; p.Y = r.Center(stand).Y;
            check(!w.Interact(p) && w.InteractionFeedback.Contains("brazier"), "light seal points to the braziers");
            var answer = DungeonMechanisms.Solution(r, ring.Controllers); events.Clear();
            for (int i = 0; i < answer.Length; i++)
            {
                p.X = r.Center(answer[i]).X; p.Y = r.Center(answer[i]).Y;
                check(w.Interact(p), "production brazier interaction " + i);
                check(i == answer.Length - 1 ? w.InteractionFeedback.Contains("seal breaks") : w.InteractionFeedback.Contains("braziers burn"), "brazier feedback reports progress " + i);
            }
            check(events.Count(e => e.Kind == "brazier_kindled") == answer.Length && events.Count(e => e.Kind == "seal_broken") == 1, "brazier events and one seal break");
            check(ring.Gates.All(r.OpenDoors.Contains) && n.Call("journal").AsString().Contains("1 light seal broken") && n.Call("journal").AsString().Contains("stirs the two beside it"), "journal records the broken seal and the brazier rule");
            // Enemies route around closed seals, and through them once broken.
            r.OpenDoors.ExceptWith(ring.Gates);
            var probe = new ActorState { Id = "probe", Region = "ruin", Radius = 11 };
            int across = ring.Gates.SelectMany(r.Neighbours).Distinct().Where(at => !ring.Gates.Contains(at) && r.CanStand(r.Center(at), probe)).OrderByDescending(at => Math.Abs(at % r.Width - stand % r.Width) + Math.Abs(at / r.Width - stand / r.Width)).First();
            var path = r.Path(r.Center(stand), r.Center(across), probe);
            check(path.All(at => !ring.Gates.Contains(at)), "closed seal excluded from routes");
            r.OpenDoors.UnionWith(ring.Gates);
            check(r.Path(r.Center(stand), r.Center(across), probe).Count > 0, "broken seal admits routes");
        }
        finally { w.Events.Published -= Observe; }
    }

    static void Saves(Foundation app, Action<bool, string> check)
    {
        var w = app.World; var n = app.Narrative; var p = w.Player;
        var r = Regions.Dungeon(7); w.RegionsById["ruin"] = r; p.Region = "ruin";
        var ring = DungeonMechanisms.Controls(r).First(c => c.Kind == "puzzle");
        DungeonMechanisms.Kindle(r, DungeonMechanisms.Solution(r, ring.Controllers)[0], out _);
        var lit = r.Lit.Order().ToArray();
        string save = Persistence.Capture(w, n); Persistence.Restore(w, n, save);
        var restored = w.RegionsById["ruin"];
        check(restored.Lit.Order().SequenceEqual(lit) && restored.Passages.Count(q => q.Mechanism == "puzzle") == r.Passages.Count(q => q.Mechanism == "puzzle"), "partial brazier ring and passage mechanisms survive save/load");
        check(DungeonMechanisms.Solution(restored, DungeonMechanisms.Controls(restored).First(c => c.Kind == "puzzle").Controllers).Length > 0, "restored ring keeps its remaining answer");
        // Saves from before mechanisms lack Lit and Mechanism; they load with no mechanisms.
        var json = JsonNode.Parse(save)!; var region = json["Regions"]?["ruin"] ?? json["regions"]?["ruin"];
        check(region != null, "save exposes ruin region");
        if (region != null)
        {
            var obj = region.AsObject();
            foreach (var key in obj.Select(kv => kv.Key).Where(k => k is "Lit" or "lit").ToArray()) obj.Remove(key);
            foreach (var q in (obj["Passages"] ?? obj["passages"])!.AsArray()) foreach (var key in q!.AsObject().Select(kv => kv.Key).Where(k => k is "Mechanism" or "mechanism").ToArray()) q.AsObject().Remove(key);
            var features = (obj["Features"] ?? obj["features"])!.AsObject();
            foreach (var key in features.Where(kv => kv.Value!.GetValue<string>() is "brazier" or "seal" or "lever" or "portcullis").Select(kv => kv.Key).ToArray()) features.Remove(key);
            var depleted = (obj["Depleted"] ?? obj["depleted"])!.AsArray();
            foreach (var item in depleted.Where(x => !features.ContainsKey(x!.ToString())).ToArray()) depleted.Remove(item);
            Persistence.Restore(w, n, json.ToJsonString());
            var legacy = w.RegionsById["ruin"];
            check(legacy.Lit.Count == 0 && legacy.Passages.All(q => q.Mechanism == "") && DungeonMechanisms.Controls(legacy).Count == 0 && DungeonValidator.Solvable(legacy), "pre-mechanism ruin saves load unchanged and solvable");
            // A burning tile that is not a brazier is rejected before mutation.
            var bad = JsonNode.Parse(save)!; var badRegion = (bad["Regions"]?["ruin"] ?? bad["regions"]?["ruin"])!.AsObject();
            var litKey = badRegion.ContainsKey("Lit") ? "Lit" : "lit";
            badRegion[litKey] = new JsonArray(restored.Entrance);
            bool rejected = false;
            try { Persistence.Restore(w, n, bad.ToJsonString()); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "save with a burning non-brazier tile is rejected");
        }
    }
}
