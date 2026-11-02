using Godot;
using System.Text.Json;

namespace Mosslight;

/// <summary>Exercise real Gloot constraints and transactional crafting on isolated bags.</summary>
public static class CraftingChecks
{
    // JSON object property order is not gameplay state: Gloot restores a parsed
    // dictionary and may serialize its keys in a different insertion order.
    private static bool SameJson(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                if (a.EnumerateObject().Count() != b.EnumerateObject().Count()) return false;
                foreach (var property in a.EnumerateObject())
                    if (!b.TryGetProperty(property.Name, out var other) || !SameJson(property.Value, other))
                        return false;
                return true;
            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength()) return false;
                for (int i = 0; i < a.GetArrayLength(); i++)
                    if (!SameJson(a[i], b[i])) return false;
                return true;
            case JsonValueKind.String:
                return a.GetString() == b.GetString();
            case JsonValueKind.Number:
                return a.GetDouble().Equals(b.GetDouble());
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;
            default:
                return a.GetRawText() == b.GetRawText();
        }
    }

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
            bool fullCrafted = items.Craft(remainsFull, draught);
            string fullAfter = service.Call("export_state").AsString();
            check(!fullCrafted, "craft with no post-consumption room is rejected");
            check(items.Count(remainsFull, "herb") == 3 &&
                items.Count(remainsFull, "potion") == 0 &&
                items.Count(remainsFull, "iron_sword") == 1,
                "rejected craft leaves its ingredient and other item stacks intact");
            using (var beforeDoc = JsonDocument.Parse(fullBefore))
            using (var afterDoc = JsonDocument.Parse(fullAfter))
            {
                bool same = SameJson(beforeDoc.RootElement, afterDoc.RootElement);
                if (!same)
                {
                    var b = beforeDoc.RootElement;
                    var a = afterDoc.RootElement;
                    GD.Print("CRAFT_ROLLBACK_DIAGNOSTIC serial_before=", b.GetProperty("serial").GetRawText(),
                        " serial_after=", a.GetProperty("serial").GetRawText(),
                        " bag_before=", b.GetProperty("bags").GetProperty(remainsFull).GetRawText(),
                        " bag_after=", a.GetProperty("bags").GetProperty(remainsFull).GetRawText());
                }
                check(same, "failed craft restores the full semantic Gloot snapshot including serial");
            }

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
            bool oversizedCrafted = items.Craft(tooMany, largeBatch);
            string batchAfter = service.Call("export_state").AsString();
            check(!oversizedCrafted && items.Count(tooMany, "herb") == 2 &&
                items.Count(tooMany, "potion") == 0,
                "multi-stack output exceeding freed capacity rejects any partial grant");
            using (var beforeDoc = JsonDocument.Parse(batchBefore))
            using (var afterDoc = JsonDocument.Parse(batchAfter))
                check(SameJson(beforeDoc.RootElement, afterDoc.RootElement),
                    "oversized output rollback restores full Gloot state and serial");

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
