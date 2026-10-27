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
        if (recipe.Ingredients.Count == 0 || recipe.Ingredients.Any(x => x.Value < 1 || Count(owner, x.Key) < x.Value) || !service.Call("can_produce", recipe.Output, recipe.Count, recipe.Material, recipe.Quality).AsBool())
            return false;
        foreach (var x in recipe.Ingredients)
            Take(owner, x.Key, x.Value);
        if (recipe.Material != "")
        {
            for (int i = 0; i < recipe.Count; i++)
                service.Call("generate_item", owner, recipe.Output, recipe.Material, recipe.Quality);
            events.Publish(new("item_acquired", owner, "", recipe.Output, recipe.Count));
        }
        else
            Add(owner, recipe.Output, recipe.Count);
        events.Publish(new("crafted", owner, "", recipe.Id));
        return true;
    }

    public bool Trade(string buyer, string merchant, string item, int price)
    {
        if (price < 0 || Count(buyer, "coin") < price || Count(merchant, item) < 1)
            return false;
        if (!Transfer(merchant, buyer, item, 1))
            return false;
        if (price > 0)
            Transfer(buyer, merchant, "coin", price);
        events.Publish(new("traded", buyer, merchant, item));
        return true;
    }
}
