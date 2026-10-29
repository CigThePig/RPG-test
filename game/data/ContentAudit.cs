using System.Text.Json;
using PixelCreatures.Runtime;

namespace Mosslight;
public static class ContentAudit
{
    public static void Validate(Content content)
    {
        var items = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Godot.FileAccess.GetFileAsString("res://game/data/items.json"))!;
        var errors = content.Validate(items.Keys.ToHashSet());
        using var materials = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://game/data/materials.json"));
        foreach (var recipe in content.Recipes)
            if (recipe.Material != "" && (!materials.RootElement.TryGetProperty(recipe.Material, out _) || recipe.Quality < 1 || recipe.Quality > 3 || items[recipe.Output].GetProperty("max_stack_size").GetInt32() != 1))
                errors.Add($"recipe {recipe.Id}: invalid generated output material/quality");
        using var spawns = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://game/data/spawns.json"));
        foreach (var region in spawns.RootElement.EnumerateObject())
        {
            if (region.Value.GetProperty("count").GetInt32() is < 3 or > 32)
                errors.Add($"spawn {region.Name}: invalid count");
            foreach (var archetype in region.Value.GetProperty("archetypes").EnumerateArray())
            {
                if (!CreatureRuntime.Registry.TryGetFamily(archetype.GetProperty("family").GetString()!, out _))
                    errors.Add($"spawn {region.Name}: unknown family");
                var profile = content.CombatProfiles.FirstOrDefault(p => p.Id == archetype.GetProperty("profile").GetString());
                if (profile == null || profile.Priorities.Keys.Any(id => content.Abilities.Any(a => a.Id == id && a.RequiresWeapon)))
                    errors.Add($"spawn {region.Name}: unknown profile or unsupported weapon ability");
                float speed = archetype.GetProperty("speed").GetSingle(), health = archetype.GetProperty("health").GetSingle();
                if (!float.IsFinite(speed) || speed <= 0 || !float.IsFinite(health) || health <= 0)
                    errors.Add($"spawn {region.Name}: invalid attributes");
            }
        }

        using var quests = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://game/data/quests.json"));
        var ids = quests.RootElement.EnumerateArray().Select(q => q.GetProperty("id").GetInt32()).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            errors.Add("quests: duplicate IDs");
        foreach (var quest in quests.RootElement.EnumerateArray())
        {
            if (!items.ContainsKey(quest.GetProperty("reward_item").GetString()!))
                errors.Add("quest: unknown reward item");
            if (quest.TryGetProperty("prerequisite", out var dependency) && !ids.Contains(dependency.GetInt32()))
                errors.Add("quest: unknown prerequisite");
        }

        foreach (var item in items)
            if (!item.Value.TryGetProperty("max_stack_size", out var count) || count.GetInt32() < 1)
                errors.Add($"item {item.Key}: invalid stack size");
        // JSON and grammar files are consumed as raw text and intentionally copied into PCKs.
        if (!Godot.FileAccess.FileExists("res://game/data/dungeon_rules.json"))
            errors.Add("Missing asset: game/data/dungeon_rules.json");

        // Dialogue Manager and texture importers replace original source files on export.
        // FileAccess.FileExists is false for those source paths in an Android APK even when
        // the compiled resources are present and loadable through Godot's resource remaps.
        foreach (var file in new[] { "game/data/warden.dialogue", "game/art/grass.png", "game/art/water.png" })
            if (!Godot.ResourceLoader.Exists("res://" + file))
                errors.Add("Missing imported resource: " + file);
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join("\n", errors));
    }
}
