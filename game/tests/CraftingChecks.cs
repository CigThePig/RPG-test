using Godot;

namespace Mosslight;

/// <summary>Exercise real Gloot constraints and transactional crafting on isolated bags.</summary>
public static class CraftingChecks
{
    public static void Run(Foundation app, Action<bool, string> check)
    {
        var world = app.World;
        var items = world.Items;
        var service = items.Service;
        string original = Persistence.Capture(world, app.Narrative);
        var draught = world.Content.Recipes.Single(x => x.Id == "draught");
        var forge = world.Content.Recipes.Single(x => x.Id == "forge_sword");

        void LimitStacks(string owner, int capacity)
        {
            var bag = service.Call("bag", owner).AsGodotObject() as Node
                ?? throw new InvalidOperationException("Gloot bag not found for crafting test");
            var constraint = GD.Load<GDScript>("res://addons/gloot/core/constraints/item_count_constraint.gd")
                .New().AsGodotObject() as Node
                ?? throw new InvalidOperationException("Gloot item count constraint not available");
            constraint.Set("capacity", capacity);
            bag.AddChild(constraint);
        }

        try
        {
            const string freedSlot = "craft-test:freed-slot";
            check(items.Add(freedSlot, "herb", 2), "craft fixture adds two recipe ingredients");
            LimitStacks(freedSlot, 1);
            check(!service.Call("has_space", freedSlot, "potion", 1).AsBool(),
                "full one-stack bag rejects an output before ingredient consumption");
            check(items.Craft(freedSlot, draught) &&
                items.Count(freedSlot, "herb") == 0 &&
                items.Count(freedSlot, "potion") == 1,
                "craft consumes the final herb stack before checking output space");

            const string remainsFull = "craft-test:remains-full";
            check(items.Add(remainsFull, "herb", 3) && items.Add(remainsFull, "iron_sword"),
                "craft fixture fills both slots while leaving one herb after consumption");
            LimitStacks(remainsFull, 2);
            string fullBefore = service.Call("export_state").AsString();
            check(!items.Craft(remainsFull, draught) &&
                service.Call("export_state").AsString() == fullBefore &&
                items.Count(remainsFull, "herb") == 3 &&
                items.Count(remainsFull, "potion") == 0,
                "craft with no post-consumption room restores exact inventory and serial");

            const string tooMany = "craft-test:batch-overflow";
            check(items.Add(tooMany, "herb", 2), "batch craft fixture receives ingredients");
            LimitStacks(tooMany, 1);
            string batchBefore = service.Call("export_state").AsString();
            var largeBatch = new RecipeDef
            {
                Id = "test-batch-overflow",
                Ingredients = new() { { "herb", 2 } },
                Output = "potion",
                Count = 21
            };
            check(!items.Craft(tooMany, largeBatch) &&
                service.Call("export_state").AsString() == batchBefore &&
                items.Count(tooMany, "herb") == 2,
                "multi-stack output that exceeds freed capacity rolls back without partial grants");

            const string equipment = "craft-test:equipment";
            check(items.Add(equipment, "ore", 3) && items.Add(equipment, "wood", 1),
                "equipment craft fixture receives both ingredient stacks");
            LimitStacks(equipment, 2);
            check(!service.Call("has_space", equipment, "iron_sword", 1).AsBool(),
                "full equipment bag has no room before consuming materials");
            check(items.Craft(equipment, forge) &&
                items.Count(equipment, "ore") == 0 &&
                items.Count(equipment, "wood") == 0 &&
                items.Count(equipment, "iron_sword") == 1,
                "material-based equipment creation uses freed ingredient slots");
        }
        finally
        {
            // The harness owns player progress; fixtures must not change downstream tests.
            Persistence.Restore(world, app.Narrative, original);
        }
    }
}
