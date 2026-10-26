using System.Text.Json;
using Godot;
using PixelCreatures.Runtime;

namespace Mosslight;
public sealed class SaveData
{
    public int Schema { get; set; } = 1;
    public string Generator { get; set; } = Mosslight.Regions.Version;
    public int Seed { get; set; }
    public long Tick { get; set; }
    public double Time { get; set; }
    public Dictionary<string, ActorState> Actors { get; set; } = new();
    public Dictionary<string, RegionState> Regions { get; set; } = new();
    public List<ProjectileState> Projectiles { get; set; } = new();
    public List<WorldEvent> Events { get; set; } = new();
    public string Items { get; set; } = "";
    public string Narrative { get; set; } = "";
}

public static class Persistence
{
    public static string Capture(World w, Node narrative) => JsonSerializer.Serialize(new SaveData { Seed = w.Seed, Tick = w.Tick, Time = w.Time, Actors = w.Actors, Regions = w.RegionsById, Projectiles = w.Projectiles, Events = w.Events.Recent, Items = w.Items.Service.Call("export_state").AsString(), Narrative = narrative.Call("export_state").AsString() }, Content.Json);
    public static SaveData Parse(string json, Content content)
    {
        var s = JsonSerializer.Deserialize<SaveData>(json, Content.Json) ?? throw new InvalidDataException("Empty save");
        if (s.Schema != 1 || s.Generator != Mosslight.Regions.Version)
            throw new InvalidDataException("Incompatible save schema or region generator; retain the older executable to read this slot");
        if (!s.Actors.ContainsKey("player") || s.Time < 0 || !double.IsFinite(s.Time) || s.Tick < 0 || s.Events.Count > 100)
            throw new InvalidDataException("Invalid world state");
        foreach (var(id, r)in s.Regions)
            if (r.Id != id || r.Width < 1 || r.Height < 1 || r.Width > 512 || r.Height > 512 || r.Tiles.Length != r.Width * r.Height || r.Features.Keys.Any(k => k < 0 || k >= r.Tiles.Length) || r.Tiles.Any(t => !Enum.IsDefined(t)) || r.OpenDoors.Any(k => !r.Features.ContainsKey(k)) || r.Depleted.Any(k => !r.Features.ContainsKey(k)))
                throw new InvalidDataException($"Invalid region {id}");
        foreach (var(id, a)in s.Actors)
        {
            if (a.Id != id || !s.Regions.TryGetValue(a.Region, out var r) || !float.IsFinite(a.X) || !float.IsFinite(a.Y) || a.X < 0 || a.Y < 0 || a.X >= r.Width * 24 || a.Y >= r.Height * 24 || a.Health < 0 || a.Health > a.MaxHealth || !float.IsFinite(a.Health) || !float.IsFinite(a.Mana) || !float.IsFinite(a.MaxMana) || !float.IsFinite(a.MaxHealth) || !float.IsFinite(a.Strength) || !float.IsFinite(a.Defence) || !float.IsFinite(a.Speed) || !float.IsFinite(a.Radius) || !float.IsFinite(a.MotionX) || !float.IsFinite(a.MotionY) || !float.IsFinite(a.StateRemaining) || a.MaxHealth <= 0 || a.Radius <= 0 || a.Speed < 0 || !Enum.IsDefined(a.State) || a.Mana < 0 || a.Mana > a.MaxMana || a.Level < 1 || a.Experience < 0 || a.SkillPoints < 0 || a.SkillRanks.Any(x => !content.Skills.Any(d => d.Id == x.Key) || x.Value < 0) || a.Abilities.Any(k => !content.Abilities.Any(d => d.Id == k)) || a.Statuses.Any(x => !content.Statuses.Any(d => d.Id == x.Id) || x.Stacks < 1 || !float.IsFinite(x.Remaining)) || a.Cooldowns.Any(x => !float.IsFinite(x.Value) || x.Value < 0))
                throw new InvalidDataException($"Invalid actor {id}");
            if (a.Appearance.Generator != "ppc-a2207c4" || !CreatureRuntime.Registry.TryGetFamily(a.Appearance.Family, out _) || (a.Appearance.GenomeJson != "" && !CreatureRuntime.Serializer.Deserialize(a.Appearance.GenomeJson).Ok) || a.PendingAbility != "" && !content.Abilities.Any(d => d.Id == a.PendingAbility))
                throw new InvalidDataException($"Invalid appearance or pending action {id}");
        }

        foreach (var p in s.Projectiles)
            if (!s.Actors.ContainsKey(p.Source) || !s.Actors.ContainsKey(p.Target) || !content.Abilities.Any(a => a.Id == p.Ability) || !float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Remaining))
                throw new InvalidDataException("Invalid projectile");
        return s;
    }

    public static void Restore(World w, Node narrative, string json)
    {
        var s = Parse(json, w.Content);
        // Each bridge stages its state. Roll back both if either adapter rejects the snapshot.
        string oldItems = w.Items.Service.Call("export_state").AsString(), oldNarrative = narrative.Call("export_state").AsString();
        try
        {
            if (!w.Items.Service.Call("import_state", s.Items).AsBool() || !narrative.Call("import_state", s.Narrative).AsBool())
                throw new InvalidDataException("Subsystem snapshot rejected");
        }
        catch
        {
            w.Items.Service.Call("import_state", oldItems);
            narrative.Call("import_state", oldNarrative);
            throw;
        }

        w.Seed = s.Seed;
        w.Tick = s.Tick;
        w.Time = s.Time;
        w.Actors = s.Actors;
        w.RegionsById = s.Regions;
        w.Projectiles = s.Projectiles;
        w.Events.Recent = s.Events;
        w.RebuildSpells();
    }

    public static string SlotPath(int slot)
    {
        if (slot < 1 || slot > 3)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return ProjectSettings.GlobalizePath($"user://slot-{slot}.json");
    }

    public static void Save(World w, Node narrative, int slot)
    {
        string path = SlotPath(slot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, Capture(w, narrative));
        File.Move(temporary, path, true);
    }

    public static void Load(World w, Node narrative, int slot) => Restore(w, narrative, File.ReadAllText(SlotPath(slot)));
}
