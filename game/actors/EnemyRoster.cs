using System.Numerics;
using System.Text.Json;

namespace Mosslight;

/// <summary>
/// The authored bodies behind each combat profile (spawns.json), so the combat lab, the
/// previews and the checks stand up the same creatures the ruin does.
/// </summary>
public static class EnemyRoster
{
    public sealed record Archetype(string Profile, string Name, string Family, float Speed, float Health);

    static Dictionary<string, Archetype>? archetypes;

    public static IReadOnlyDictionary<string, Archetype> Archetypes
    {
        get
        {
            if (archetypes != null) return archetypes;
            using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://game/data/spawns.json"));
            var found = new Dictionary<string, Archetype>();
            foreach (var region in json.RootElement.EnumerateObject())
                foreach (var x in region.Value.GetProperty("archetypes").EnumerateArray())
                {
                    var archetype = new Archetype(x.GetProperty("profile").GetString()!, x.GetProperty("name").GetString()!, x.GetProperty("family").GetString()!,
                        x.GetProperty("speed").GetSingle(), x.GetProperty("health").GetSingle());
                    found.TryAdd(archetype.Profile, archetype);
                }
            // The Reach's prowlers share the ruin prowler's body.
            if (found.TryGetValue("prowler", out var prowler)) found.TryAdd("mire", prowler with { Profile = "mire", Name = "Mire prowler" });
            return archetypes = found;
        }
    }

    /// <summary>A calm, full-health enemy of that kind posted at a point. The caller adds it to the world.</summary>
    public static ActorState Create(World w, string id, string profile, string region, Vector2 at, Vector2 facing, ulong seed = 42)
    {
        var def = w.FindProfile(profile) ?? throw new InvalidOperationException("Missing combat profile " + profile);
        var archetype = Archetypes.GetValueOrDefault(profile) ?? throw new InvalidOperationException("No authored body for " + profile);
        var a = new ActorState
        {
            Id = id, Name = archetype.Name, Region = region, Faction = "beasts", Profile = "beast", CombatProfile = profile,
            Appearance = new() { Family = archetype.Family, Seed = seed }, Abilities = def.Loadout(),
            Health = archetype.Health, MaxHealth = archetype.Health
        };
        AnatomyAdapter.Apply(a);
        a.Speed = archetype.Speed;
        EnemyMind.Resettle(a, at, facing);
        return a;
    }
}
