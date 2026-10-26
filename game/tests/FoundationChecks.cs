using Godot;
using System.Diagnostics;
using System.Text.Json;
using PixelCreatures.Runtime;
using PixelCreatures.Core.Genetics;
using V = System.Numerics.Vector2;

namespace Mosslight;
/// <summary>Runs production systems in the engine, including cross-language addons.</summary>
public static class FoundationChecks
{
    static int assertions;
    static void Check(bool value, string name)
    {
        assertions++;
        if (!value)
            throw new InvalidOperationException("CHECK FAILED: " + name);
        GD.Print("PASS " + name);
    }

    static void Advance(World w, float seconds)
    {
        for (int i = 0; i < (int)(seconds * 60); i++)
            w.Step(1f / 60);
    }

    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        app.Ui.PauseForVerification();
        var measurements = new Dictionary<string, object>();
        var w = app.World;
        string original = Persistence.Capture(w, app.Narrative);
        var watch = Stopwatch.StartNew();
        try
        {
            // Exercise nonlinear progression using actual world defeat events before acceptance.
            var early = w.Player;
            var victims = w.Actors.Values.Where(a => a.Profile == "beast").Take(2).ToArray();
            foreach (var victim in victims) w.Damage(early, victim, 10000, "magic");
            Check(!app.Narrative.Call("completed", 1).AsBool(), "historical defeats do not auto-accept quest");
            string beforeAcceptance = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, beforeAcceptance);
            int beforeReward = w.Items.Count("player", "coin");
            Check(app.Narrative.Call("accept_quest", 1).AsBool() && app.Narrative.Call("completed", 1).AsBool(), "pre-acceptance defeats survive save and complete on acceptance");
            Check(w.Items.Count("player", "coin") == beforeReward + 15, "historical achievement grants one reward");
            string rewardedSave = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, rewardedSave);
            Check(!app.Narrative.Call("accept_quest", 1).AsBool() && w.Items.Count("player", "coin") == beforeReward + 15, "load and repeated acceptance cannot duplicate historical reward");
            Persistence.Restore(w, app.Narrative, original);
            var bag = w.Items.Service.Call("bag", "capacity-test").AsGodotObject() as Node;
            w.Items.Add("capacity-test", "iron_sword");
            var capacity = GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd").New().AsGodotObject() as Node;
            bag!.AddChild(capacity!);
            w.Items.Add("loot-test", "potion", 3);
            w.Items.Add("loot-test", "iron_sword");
            string beforeFailure = w.Items.Service.Call("export_state").AsString();
            Check(!w.Items.LootAll("loot-test", "capacity-test"), "full destination rejects loot without reporting success");
            Check(w.Items.Service.Call("export_state").AsString() == beforeFailure, "failed loot retains stacks and stable instance IDs");
            Check(!w.Items.LootAll("empty-container", "player"), "empty container never reports successful loot");
            capacity!.QueueFree();
            await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(w.Items.Transfer("loot-test", "player", "potion", 1) && w.Items.Count("loot-test", "potion") == 2, "partial loot leaves remaining stack in corpse");
            Check(w.Items.LootAll("loot-test", "player") && !w.Items.HasLoot("loot-test"), "remaining loot moves once and empties container");
            Check(!w.Items.LootAll("loot-test", "player"), "emptied container does not report repeated success");
            Persistence.Restore(w, app.Narrative, original);
            // Raw touch movement shares modal/focus authority with desktop.
            var stick = app.Ui.Stick;
            app.Ui.Menu(); // Resume.
            stick._Input(new InputEventScreenTouch { Index = 7, Pressed = true, Position = stick.GlobalPosition + new Vector2(180, 100) });
            Check(app.TouchMovement.X > .8f, "touch stick owns movement finger");
            stick._Input(new InputEventScreenTouch { Index = 8, Pressed = false });
            Check(app.TouchMovement.X > .8f, "other finger release does not cancel movement");
            app.Ui.PauseForVerification();
            app.Ui.Update();
            Check(app.TouchMovement == Vector2.Zero, "modal opening cancels held touch movement");
            stick._Input(new InputEventScreenDrag { Index = 7, Position = stick.GlobalPosition + new Vector2(180,100) });
            Check(app.TouchMovement == Vector2.Zero, "drag cannot restart movement under modal");
            bool ran = false;
            Check(!app.ManageItem(() => { ran = true; return true; }) && !ran, "menu rejects programmatic item mutations");
            app.Ui.Menu();
            app.Narrative.Set("speaking", true);
            Check(!app.UseQuickItem("potion") && !app.ManageItem(() => true), "dialogue blocks hotbar and inventory mutations");
            app.Narrative.Set("speaking", false);
            w.Player.Health = 0;
            Check(app.GameplayBlocked && !app.UseQuickItem("potion") && !app.ManageItem(() => true), "death blocks player gameplay and equipment commands");
            Persistence.Restore(w, app.Narrative, original);
            app.Ui.PauseForVerification();
            var itemDefs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Godot.FileAccess.GetFileAsString("res://game/data/items.json"))!;
            Check(w.Content.Validate(itemDefs.Keys.ToHashSet()).Count == 0, "content definitions and references");
            foreach (var family in CreatureRuntime.Registry.Families)
            {
                var genome = CreatureRuntime.Sample(family.Id, 42);
                var a = CreatureRuntime.Build(genome);
                var repeated = CreatureRuntime.Build(CreatureRuntime.Sample(family.Id, 42));
                Check(a.Anatomy.AnatomyKey == repeated.Anatomy.AnatomyKey, "deterministic anatomy " + family.Id);
                string json = CreatureRuntime.Serializer.Serialize(genome);
                var loaded = CreatureRuntime.Serializer.Deserialize(json);
                Check(loaded.Ok && CreatureRuntime.Build(loaded.Genome!).Anatomy.AnatomyKey == a.Anatomy.AnatomyKey, "genome roundtrip " + family.Id);
                var view = new CharacterView();
                app.AddChild(view);
                view.Setup(new() { Id = "test:" + family.Id, Appearance = new() { Family = family.Id, Seed = 42 } });
                view.Creature.TriggerAction();
                view.Creature.TriggerHit(Vector2.Left);
                for (int i = 0; i < 3; i++)
                    await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(view.Creature.LastError == "" && view.Creature.Model != null, "live actor setup/action " + family.Id);
                view.QueueFree();
            }

            var p = w.Player;
            float healthBeforeModal = p.Health;
            int potionBeforeModal = w.Items.Count(p.Id, "potion");
            p.Health = 25;
            Check(app.GameplayBlocked && !app.UseQuickItem("potion") && p.Health == 25 && w.Items.Count(p.Id, "potion") == potionBeforeModal, "modal hotbar item use cannot mutate a paused world");
            p.Health = healthBeforeModal;
            Check(!Progression.Unlock(w, p, "ward"), "ability unlock enforces level/skill cost");
            Progression.Award(w, p, 100);
            Check(p.Level == 2 && p.SkillPoints == 1, "experience grants level and persistent skill point");
            Check(Progression.Unlock(w, p, "ward"), "ability progression unlocks actual definition");
            float defence = w.Stat(p, "defence");
            w.ApplyStatus(p, "ward", p.Id);
            Check(w.Stat(p, "defence") == defence + 8, "temporary modifiers share stat resolution");
            p.Statuses.Clear();
            Progression.Award(w, p, 200);
            Check(Progression.Train(w, p, "blades") && w.Stat(p, "strength") == p.Strength + 2, "trained skills modify shared combat attributes");
            Check(w.Items.Add(p.Id, "herb", 60), "Gloot accepts stacked commodities");
            Check(w.Items.Count(p.Id, "herb") == 64, "Gloot stack counts across limits");
            Check(!w.Items.Add(p.Id, "missing", 1), "unknown item rejected");
            Check(!w.Items.Take(p.Id, "herb", 1000), "failed removal preserves inventory");
            Check(w.Items.Transfer(p.Id, "test:container", "herb", 13) && w.Items.Count("test:container", "herb") == 13, "Gloot split/transfer to container");
            Check(w.Items.Service.Call("generate_item", p.Id, "oak_staff", "silver", 2).AsBool(), "procedural material/quality item composed in Gloot");
            Check(w.Items.Equip(p, "oak_staff", "weapon") && w.Items.Bonus(p, "power") > 6, "procedural material affects magical equipment power");
            Check(w.Items.Equip(p, "iron_sword", "weapon"), "replaced equipment returns to backpack");
            w.Items.Service.Call("generate_item", p.Id, "oak_staff", "oak", 3);
            using (var rows = JsonDocument.Parse(w.Items.Service.Call("catalog", p.Id).AsString()))
            {
                string selector = rows.RootElement.EnumerateArray().Last(row => row.GetProperty("id").GetString() == "oak_staff").GetProperty("selector").GetString()!;
                Check(w.Items.Equip(p, selector, "weapon") && Math.Abs(w.Items.Bonus(p, "power") - 9.36f) < .01f, "equipment selection preserves unique generated instance identity");
            }

            w.Items.Equip(p, "iron_sword", "weapon");
            Check(w.Items.Equip(p, "leather_coat", "body") && w.Items.Bonus(p, "defence") == 4, "equipment alters authoritative defence");
            var originalAppearance = p.Appearance;
            app.ChangeAppearance("quadruped", 42);
            Check(!p.Grasps && w.Items.Bonus(p, "damage") == 0 && w.Items.Count(p.Id, "iron_sword") >= 1, "anatomy change returns incompatible equipment to inventory");
            var capabilityTarget = w.Actors["npc:2"];
            float savedTargetX = capabilityTarget.X, savedTargetY = capabilityTarget.Y;
            capabilityTarget.X = p.X + 20;
            capabilityTarget.Y = p.Y;
            w.Items.Service.Call("equip", p.Id, "iron_sword", "weapon", true);
            Check(!w.Activate(p.Id, "slash", capabilityTarget.Id), "weapon abilities reject current incompatible anatomy even with corrupt equipment");
            w.Items.Reconcile(p);
            w.SetAppearance(p, originalAppearance);
            w.Items.Equip(p, "iron_sword", "weapon");
            app.RebuildViews();
            capabilityTarget.X = savedTargetX;
            capabilityTarget.Y = savedTargetY;
            var beast = w.Actors["npc:2"];
            Check(!w.Items.Equip(beast, "iron_sword", "weapon"), "anatomy prevents incompatible equipment");
            var recipe = w.Content.Recipes.Single(x => x.Id == "draught");
            int potions = w.Items.Count(p.Id, "potion");
            app.Narrative.Call("accept_quest", 3);
            Check(w.Items.Craft(p.Id, recipe) && w.Items.Count(p.Id, "potion") == potions + 1, "crafting consumes and produces using Gloot");
            Check(app.Narrative.Call("completed", 3).AsBool(), "craft event completes actual QuestSystem quest");
            Check(!w.Items.Craft(p.Id, w.Content.Recipes.Single(x => x.Id == "forge_sword")), "invalid recipe transaction is atomic");
            w.Items.Add(p.Id, "ore", 3);
            w.Items.Add(p.Id, "wood", 1);
            Check(w.Items.Craft(p.Id, w.Content.Recipes.Single(x => x.Id == "forge_sword")), "crafting constructs material/quality equipment");
            Check(!w.Items.Service.Call("generate_item", p.Id, "iron_sword", "unknown_material", 2).AsBool(), "generated item rejects unknown material");
            int herbsBeforeInvalid = w.Items.Count(p.Id, "herb");
            Check(!w.Items.Craft(p.Id, new RecipeDef { Id = "invalid", Ingredients = new() { { "herb", 1 } }, Output = "unknown", Count = 1 }) && w.Items.Count(p.Id, "herb") == herbsBeforeInvalid, "invalid crafting output preserves ingredients");
            Check(w.Items.Trade(p.Id, "npc:0", "potion", 5), "merchant transfers existing stock for coins");
            Check(!w.Items.Trade(p.Id, "npc:0", "potion", 9999), "barter enforces funds");
            Check(app.Narrative.Call("accept_quest", 1).AsBool(), "quest acceptance");
            Check(!app.Narrative.Call("accept_quest", 2).AsBool(), "quest dependencies enforced");
            foreach (var a in w.Actors.Values.Where(a => a.Id != "player"))
                a.X += 1000;
            beast.X = p.X + 25;
            beast.Y = p.Y;
            beast.Health = 25;
            beast.Defence = 0;
            Check(!w.Activate(p.Id, "slash", "npc:0"), "friendly-fire target rejected");
            Check(w.Activate(p.Id, "slash", beast.Id), "SPELL melee cast accepted");
            Check(!w.Activate(p.Id, "slash", beast.Id), "cooldown/action exclusion enforced");
            Advance(w, .8f);
            Check(!beast.Alive, "windup damage mitigation death pipeline");
            int coins = w.Items.Count(beast.Id, "coin");
            Check(coins >= 2, "combat generates loot in victim inventory");
            Check(w.Interact(p) && w.Items.Count(p.Id, "coin") >= coins, "corpse loot transfer");
            var b = w.Actors["npc:3"];
            b.X = p.X + 25;
            b.Y = p.Y;
            b.Health = 10;
            w.Damage(p, b, 100, "physical");
            Check(app.Narrative.Call("completed", 1).AsBool(), "defeat events complete kill quest");
            Check(p.Reputation.GetValueOrDefault("wardens") >= 10, "quest rewards change reputation");
            int secondCorpseLoot = w.Items.Count(b.Id, "coin");
            int playerCoinsBeforeSecond = w.Items.Count(p.Id, "coin");
            Check(w.Interact(p) && w.Items.Count(b.Id, "coin") == 0 && w.Items.Count(p.Id, "coin") == playerCoinsBeforeSecond + secondCorpseLoot, "empty overlapping corpse does not hide another corpse's loot");
            string relicQuestAvailable = app.Narrative.Call("export_state").AsString();
            Check(app.Narrative.Call("accept_quest", 2).AsBool(), "quest unlocked after prerequisite");
            b.Health = 100;
            b.Invulnerable = 0;
            b.State = ActionState.Idle;
            b.Statuses.Clear();
            p.State = ActionState.Idle;
            p.Mana = 0;
            Check(!w.Activate(p.Id, "ember", b.Id), "ability resource cost validation");
            p.Mana = 60;
            Check(w.Activate(p.Id, "ember", b.Id), "ranged SPELL cast accepted");
            Advance(w, 1.2f);
            Check(b.Health < 100, "projectile hit resolves damage");
            w.ApplyStatus(b, "burn", p.Id);
            w.ApplyStatus(b, "burn", p.Id);
            w.ApplyStatus(b, "burn", p.Id);
            w.ApplyStatus(b, "burn", p.Id);
            Check(b.Statuses.Single(x => x.Id == "burn").Stacks == 3, "status stack cap and refresh");
            float hp = b.Health;
            Advance(w, 1.2f);
            Check(b.Health < hp, "periodic status damage");
            p.State = ActionState.Idle;
            p.Cooldowns.Clear();
            b.Statuses.Clear();
            b.Health = 100;
            b.Invulnerable = 0;
            Check(w.Activate(p.Id, "slash", b.Id), "cancellable ability starts");
            w.Step(1f / 60);
            w.Cancel(p);
            float before = b.Health;
            Advance(w, .4f);
            Check(b.Health == before, "cancel prevents windup hit");
            p.Health = 30;
            p.State = ActionState.Idle;
            Check(w.UsePotion(p) && p.Health == 65, "consumable updates shared health");
            var testRegion = new RegionState
            {
                Id = "geometry",
                Width = 7,
                Height = 5,
                Tiles = Enumerable.Repeat(Terrain.Floor, 35).ToArray(),
                Entrance = 15,
                Objective = 19
            };
            testRegion.Features[17] = "herb";
            testRegion.Tiles[16] = Terrain.Wall;
            w.RegionsById[testRegion.Id] = testRegion;
            string oldRegion = p.Region;
            float oldX = p.X, oldY = p.Y;
            p.Region = testRegion.Id;
            p.X = testRegion.Center(15).X + 2;
            p.Y = testRegion.Center(15).Y;
            p.State = ActionState.Idle;
            Check(!w.Interact(p), "resource interaction cannot cross a wall");
            testRegion.Tiles[16] = Terrain.Water;
            Check(w.VisibleTo(p, testRegion.Center(17)), "water does not block sight or projectile targeting");
            int herbsBeforeModalInteraction = w.Items.Count(p.Id, "herb");
            app.Interact();
            Check(w.Items.Count(p.Id, "herb") == herbsBeforeModalInteraction && !testRegion.Depleted.Contains(17), "modal hotbar interaction cannot gather while simulation is paused");
            p.Region = oldRegion;
            p.X = oldX;
            p.Y = oldY;
            w.RegionsById.Remove(testRegion.Id);
            Check(app.World.Planner!.Call("choose", "safe", true).AsString() == "retreat", "adapted Skelerealms GOAP selects retreat");
            Check(app.World.Planner.Call("choose", "defeated", false).AsString() == "wait", "planner value preconditions reject unseen target");
            var generation = new List<double>();
            var dungeonStats = new List<object>();
            for (int seed = 0; seed < 30; seed++)
            {
                var timer = Stopwatch.StartNew();
                var r = Regions.Overworld(seed);
                generation.Add(timer.Elapsed.TotalMilliseconds);
                Check(r.Tiles.SequenceEqual(Regions.Overworld(seed).Tiles), "region deterministic " + seed);
                var actor = new ActorState();
                Check(r.Path(r.Center(r.Entrance), r.Center(r.Objective), actor).Count > 0, "causeway connected " + seed);
                timer.Restart();
                var dungeon = Regions.Dungeon(seed);
                double milliseconds = timer.Elapsed.TotalMilliseconds;
                Check(dungeon.Tiles.SequenceEqual(Regions.Dungeon(seed).Tiles) && dungeon.Features.SequenceEqual(Regions.Dungeon(seed).Features), "dungeon deterministic " + seed);
                if (!DungeonValidator.Solvable(dungeon))
                {
                    File.WriteAllText("/tmp/rpg-dungeon-failure.json", JsonSerializer.Serialize(dungeon, Content.Json));
                    GD.Print("DUNGEON features " + string.Join(",", dungeon.Features.Select(x => $"{x.Key}:{x.Value}")));
                }

                Check(DungeonValidator.Solvable(dungeon), "runtime keys/locks/objective reachable " + seed);
                dungeonStats.Add(new { seed, milliseconds, tiles = dungeon.Tiles.Length });
            }

            foreach (var(width, height)in new[]
            {
                (32, 24),
                (64, 48),
                (128, 96)
            }

            )
            {
                var timer = Stopwatch.StartNew();
                var region = Regions.Overworld(42, width, height);
                double ms = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
                var path = region.Path(region.Center(region.Entrance), region.Center(region.Objective), new ActorState());
                Check(path.Count > 0, $"world-size navigation {width}x{height}");
                measurements[$"world{width}x{height}"] = new
                {
                    generationMs = ms,
                    navigationMs = timer.Elapsed.TotalMilliseconds,
                    cells = region.Tiles.Length
                };
            }

            measurements["regionGenerationMs"] = generation;
            measurements["dungeons"] = dungeonStats;
            p.Region = "reach";
            p.X = w.RegionsById["reach"].Center(w.RegionsById["reach"].Entrance).X;
            p.Y = w.RegionsById["reach"].Center(w.RegionsById["reach"].Entrance).Y;
            var r0 = w.RegionsById["reach"];
            var wall = r0.Tiles.Select((t, i) => (t, i)).First(x => x.t == Terrain.Rock);
            Check(!r0.CanStand(r0.Center(wall.i), p), "terrain blocks actors");
            w.Enter(p, "ruin");
            Check(p.Region == "ruin" && r0 != w.RegionsById[p.Region], "persistent region transition");
            w.Items.Add(p.Id, "relic");
            Check(app.Narrative.Call("completed", 2).AsBool(), "dungeon item acquisition completes quest");
            string completedNarrative = app.Narrative.Call("export_state").AsString(), completedItems = w.Items.Service.Call("export_state").AsString();
            app.Narrative.Call("import_state", relicQuestAvailable);
            int potionsBeforeReconcile = w.Items.Count(p.Id, "potion");
            Check(app.Narrative.Call("accept_quest", 2).AsBool() && app.Narrative.Call("completed", 2).AsBool() && w.Items.Count(p.Id, "potion") == potionsBeforeReconcile + 3, "quest acceptance reconciles a relic collected before acceptance");
            Check(!app.Narrative.Call("accept_quest", 2).AsBool() && w.Items.Count(p.Id, "potion") == potionsBeforeReconcile + 3, "repeated early-item quest acceptance cannot repeat rewards");
            app.Narrative.Call("import_state", completedNarrative);
            w.Items.Service.Call("import_state", completedItems);
            w.Enter(p, "reach");
            // Restore valid NPC positions before persistence validation.
            foreach (var a in w.Actors.Values.Where(a => a.Id != "player"))
            {
                a.X = a.HomeX;
                a.Y = a.HomeY;
            }

            string save = Persistence.Capture(w, app.Narrative);
            Advance(w, 1);
            Persistence.Restore(w, app.Narrative, save);
            File.WriteAllText("/tmp/rpg-before.json", save);
            File.WriteAllText("/tmp/rpg-after.json", Persistence.Capture(w, app.Narrative));
            Check(Persistence.Capture(w, app.Narrative) == save, "unified actor/item/quest/equipment save roundtrip");
            Advance(w, 1);
            string continuation = Persistence.Capture(w, app.Narrative);
            Persistence.Restore(w, app.Narrative, save);
            Advance(w, 1);
            Check(Persistence.Capture(w, app.Narrative) == continuation, "deterministic continuation across load");
            string itemSave = w.Items.Service.Call("export_state").AsString();
            Check(!w.Items.Service.Call("import_state", itemSave.Replace("iron_sword", "unknown_item")).AsBool(), "invalid item snapshot rejected before mutation");
            Check(w.Items.Service.Call("export_state").AsString() == itemSave, "failed item restore is atomic");
            bool rejected = false;
            try
            {
                Persistence.Restore(w, app.Narrative, save.Replace("rpg-regions-1", "incompatible"));
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Check(rejected, "incompatible generator rejected");
            foreach (int count in new[]
            {
                16,
                64,
                128
            }

            )
            {
                Persistence.Restore(w, app.Narrative, original);
                while (w.Actors.Count < count)
                {
                    int i = w.Actors.Count;
                    w.Actors["bench:" + i] = new()
                    {
                        Id = "bench:" + i,
                        X = w.Player.X + 80 + (i % 8) * 24,
                        Y = w.Player.Y,
                        HomeX = w.Player.X + 80,
                        HomeY = w.Player.Y,
                        Faction = "wardens",
                        Profile = "warden",
                        Profession = "merchant"
                    };
                }

                var timer = Stopwatch.StartNew();
                for (int i = 0; i < 600; i++)
                    w.Step(1f / 60);
                double ms = timer.Elapsed.TotalMilliseconds;
                measurements["simulation" + count] = new
                {
                    ticks = 600,
                    milliseconds = ms,
                    msPerTick = ms / 600,
                    managedBytes = GC.GetTotalMemory(false),
                    saveBytes = System.Text.Encoding.UTF8.GetByteCount(Persistence.Capture(w, app.Narrative))
                };
            }

            measurements["assertions"] = assertions;
            measurements["elapsedSeconds"] = watch.Elapsed.TotalSeconds;
            measurements["environment"] = $"{OS.GetName()} {OS.GetProcessorName()} · Godot {Engine.GetVersionInfo()["string"]}";
            var outPath = ProjectSettings.GlobalizePath("res://docs/verification.json");
            File.WriteAllText(outPath, JsonSerializer.Serialize(measurements, Content.Json));
            GD.Print($"FOUNDATION CHECKS PASSED: {assertions}");
        }
        finally
        {
            Persistence.Restore(w, app.Narrative, original);
            app.RebuildViews();
        }
    }
}
