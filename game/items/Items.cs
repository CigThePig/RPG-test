using Godot;
using System.Text.Json;

namespace Mosslight;
/// <summary>No competing C# item store: all operations delegate to the actual Gloot service.</summary>
public sealed class Items(Node service, EventStream events)
{
    public Node Service => service;

    public bool Add(string owner, string item, int count = 1)
    {
        bool ok = service.Call("add", owner, item, count).AsBool();
        if (ok)
            events.Publish(new("item_acquired", owner, "", item, count));
        return ok;
    }

    public bool GrantReward(string item, int count)
    {
        bool ok = service.Call("reward_add", "player", item, count).AsBool();
        if (ok) events.Publish(new("item_acquired", "player", "quest_reward", item, count));
        return ok;
    }

    public int Count(string owner, string item) => service.Call("count_item", owner, item).AsInt32();
    public bool Take(string owner, string item, int count) => service.Call("take", owner, item, count).AsBool();
    public bool Transfer(string from, string to, string item, int count)
    {
        bool ok = service.Call("transfer", from, to, item, count).AsBool();
        if (ok)
            events.Publish(new("item_acquired", to, from, item, count));
        return ok;
    }

    public bool Equip(ActorState actor, string item, string slot)
    {
        bool ok = service.Call("equip", actor.Id, item, slot, actor.Grasps).AsBool();
        if (ok)
            events.Publish(new("item_equipped", actor.Id, "", item));
        return ok;
    }

    /// <summary>Returns a slot's item to the bag. Fails, changing nothing, if the bag cannot take it.</summary>
    public bool Unequip(ActorState actor, string slot)
    {
        string selector = EquippedSelector(actor.Id, slot);
        bool ok = selector != "" && service.Call("unequip", actor.Id, slot).AsBool();
        if (ok)
            events.Publish(new("item_unequipped", actor.Id, slot, selector));
        return ok;
    }
    public string EquippedSelector(string owner, string slot) => service.Call("equipped_selector", owner, slot).AsString();
    public bool CanUnequip(string owner, string slot) => service.Call("can_unequip", owner, slot).AsBool();
    public bool HasSpace(string owner, string item, int count = 1) => service.Call("has_space", owner, item, count).AsBool();

    /// <summary>Read-only structured rows for bag stacks and equipped items, straight from Gloot.</summary>
    public IReadOnlyList<ItemView> View(string owner)
    {
        using var doc = JsonDocument.Parse(service.Call("inventory_view", owner).AsString());
        var rows = new List<ItemView>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var props = new Dictionary<string, string>();
            foreach (var p in row.GetProperty("properties").EnumerateObject())
                props[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDouble().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : p.Value.ToString();
            rows.Add(new(row.GetProperty("selector").GetString()!, row.GetProperty("instance").GetString()!, row.GetProperty("id").GetString()!,
                row.GetProperty("title").GetString()!, row.GetProperty("count").GetInt32(), row.GetProperty("max_stack").GetInt32(),
                row.GetProperty("slot").GetString()!, row.GetProperty("equipped").GetBoolean(), row.GetProperty("equipped_slot").GetString()!, props));
        }
        return rows;
    }

    public string Title(string prototype) { string t = service.Call("prototype_title", prototype).AsString(); return t == "" ? prototype : t; }

    /// <summary>Merchant price. A single source shared by the trade screen and the transaction.</summary>
    public static int Price(string item) => 5;

    /// <summary>Why the last <see cref="Craft"/> call failed; None after success.</summary>
    public CraftFailure LastCraftFailure { get; private set; }
    /// <summary>Why the last <see cref="Trade"/> call failed; None after success.</summary>
    public TradeFailure LastTradeFailure { get; private set; }

    /// <summary>Read-only preflight of a recipe. Capacity is only known for certain after ingredients are taken.</summary>
    public CraftFailure CraftPreview(string owner, RecipeDef recipe)
    {
        if (recipe.Ingredients.Count == 0 || recipe.Ingredients.Any(x => x.Value < 1) ||
            !service.Call("can_produce", recipe.Output, recipe.Count, recipe.Material, recipe.Quality).AsBool())
            return CraftFailure.CannotMake;
        if (recipe.Ingredients.Any(x => Count(owner, x.Key) < x.Value)) return CraftFailure.MissingIngredients;
        return CraftFailure.None;
    }

    public TradeFailure TradePreview(string buyer, string merchant, string item, int price)
    {
        if (price < 0) return TradeFailure.Invalid;
        if (Count(merchant, item) < 1) return TradeFailure.OutOfStock;
        if (Count(buyer, "coin") < price) return TradeFailure.InsufficientFunds;
        if (!HasSpace(buyer, item)) return TradeFailure.PackFull;
        return TradeFailure.None;
    }

    public void Reconcile(ActorState actor) => service.Call("reconcile_equipment", actor.Id, actor.Grasps);
    public bool HasLoot(string owner) => service.Call("has_items", owner).AsBool();
    public bool LootAll(string source, string target)
    {
        bool changed = false;
        using var catalog = JsonDocument.Parse(service.Call("catalog", source).AsString());
        foreach (var row in catalog.RootElement.EnumerateArray())
        {
            string item = row.GetProperty("id").GetString()!;
            int count = row.GetProperty("count").GetInt32();
            if (service.Call("transfer_stack", source, target, row.GetProperty("selector").GetString()!, count).AsBool())
            {
                events.Publish(new("item_acquired", target, source, item, count));
                changed = true;
            }
        }
        return changed;
    }

    public float Bonus(ActorState actor, string property) => service.Call("bonus", actor.Id, property).AsSingle();
    public string Describe(string owner) => service.Call("describe", owner).AsString();
    public bool Craft(string owner, RecipeDef recipe)
    {
        LastCraftFailure = CraftPreview(owner, recipe);
        if (LastCraftFailure != CraftFailure.None) return false;
        LastCraftFailure = CraftFailure.Failed;
        // Ingredient removal may free the very slot the output needs. Validate the
        // recipe and ingredient quantities first, then check capacity after removal.
        // All Gloot mutations are synchronous, and no events escape until commit.
        if (recipe.Ingredients.Count == 0 ||
            recipe.Ingredients.Any(x => x.Value < 1 || Count(owner, x.Key) < x.Value) ||
            !service.Call("can_produce", recipe.Output, recipe.Count, recipe.Material, recipe.Quality).AsBool())
            return false;

        // Capture the entire authoritative Gloot state, including equipment, item
        // properties and instance IDs, rather than trying to reconstruct ingredients.
        string before = service.Call("export_state").AsString();
        bool produced = false;
        Exception? failure = null;
        try
        {
            bool consumed = true;
            foreach (var ingredient in recipe.Ingredients)
            {
                if (service.Call("take", owner, ingredient.Key, ingredient.Value).AsBool())
                    continue;
                consumed = false;
                break;
            }

            bool space = consumed && service.Call("has_space", owner, recipe.Output, recipe.Count).AsBool();
            if (consumed && !space) LastCraftFailure = CraftFailure.PackFull;
            if (space)
            {
                if (recipe.Material == "")
                {
                    produced = service.Call("add", owner, recipe.Output, recipe.Count).AsBool();
                }
                else
                {
                    produced = true;
                    for (int i = 0; i < recipe.Count; i++)
                    {
                        if (service.Call("generate_item", owner, recipe.Output, recipe.Material, recipe.Quality).AsBool())
                            continue;
                        produced = false;
                        break;
                    }
                }
            }
        }
        catch (Exception error)
        {
            failure = error;
        }

        // An exception after some generated items were added is never a success,
        // even if the loop had not yet cleared its produced flag.
        if (!produced || failure != null)
        {
            // Failed delivery, including a partially completed generated-item
            // batch, restores every affected stack and the original serial.
            // Never report a crafted item or quest progress on failure.
            if (!service.Call("import_state", before).AsBool())
                throw new InvalidOperationException("Crafting rollback failed; Gloot inventory state may be inconsistent.", failure);
            if (failure != null)
                GD.PushWarning("Crafting failed and was rolled back: " + failure);
            return false;
        }

        LastCraftFailure = CraftFailure.None;
        events.Publish(new("item_acquired", owner, "", recipe.Output, recipe.Count));
        events.Publish(new("crafted", owner, "", recipe.Id));
        return true;
    }

    public bool Trade(string buyer, string merchant, string item, int price)
    {
        LastTradeFailure = price < 0 ? TradeFailure.Invalid : Count(merchant, item) < 1 ? TradeFailure.OutOfStock : Count(buyer, "coin") < price ? TradeFailure.InsufficientFunds : TradeFailure.None;
        if (LastTradeFailure != TradeFailure.None)
            return false;
        LastTradeFailure = TradeFailure.Failed;
        // Take payment first and refund it if the goods cannot be delivered, so a
        // failed transfer can never hand over an item for free.
        if (price > 0 && !service.Call("transfer", buyer, merchant, "coin", price).AsBool())
            return false;
        if (!HasSpace(buyer, item)) LastTradeFailure = TradeFailure.PackFull;
        if (!Transfer(merchant, buyer, item, 1))
        {
            if (price > 0)
                service.Call("transfer", merchant, buyer, "coin", price);
            return false;
        }
        if (price > 0)
            events.Publish(new("item_acquired", merchant, buyer, "coin", price));
        LastTradeFailure = TradeFailure.None;
        events.Publish(new("traded", buyer, merchant, item));
        return true;
    }
}

public enum CraftFailure { None, MissingIngredients, CannotMake, PackFull, Failed }
public enum TradeFailure { None, Invalid, OutOfStock, InsufficientFunds, PackFull, Failed }

/// <summary>Presentation copy of one Gloot stack or equipped item. Not an item store.</summary>
public sealed record ItemView(string Selector, string Instance, string Id, string Title, int Count, int MaxStack,
    string Slot, bool Equipped, string EquippedSlot, IReadOnlyDictionary<string, string> Properties)
{
    public float Stat(string name) => Properties.TryGetValue(name, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0;
    public bool Usable => Stat("healing") > 0;
}
