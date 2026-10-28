using System.Text.Json;

namespace Mosslight;
public sealed class AbilityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "physical";
    public string Status { get; set; } = "";
    public float Range { get; set; }
    public float Damage { get; set; }
    public float Cost { get; set; }
    public float Cooldown { get; set; }
    public float Windup { get; set; }
    public float Recovery { get; set; }
    public float ProjectileSpeed { get; set; }
    public int UnlockLevel { get; set; } = 1;
    public int UnlockCost { get; set; } = 1;
    public bool Self { get; set; }
    public bool RequiresWeapon { get; set; }
    // Test fixtures are excluded from normal content, including progression UI.
    public bool VerificationOnly { get; set; }
    // Omitted geometry preserves the original target-locked SPELL behavior.
    public AttackGeometryDef? Geometry { get; set; }
    public TelegraphDef? Telegraph { get; set; }
    // Optional PPC action; an omitted or anatomy-incompatible style uses the rig default.
    public string ActionStyle { get; set; } = "";
}

public sealed class AttackGeometryDef
{
    public string Shape { get; set; } = "target";
    public float Range { get; set; }
    public float HalfAngleDegrees { get; set; }
}

public sealed class TelegraphDef
{
    public string Style { get; set; } = "none";
}

public sealed class StatusDef
{
    public string ModifierStat { get; set; } = "";
    public float Modifier { get; set; }
    public string Id { get; set; } = "";
    public float Duration { get; set; }
    public float Interval { get; set; }
    public float Damage { get; set; }
    public int MaxStacks { get; set; }
    public string Type { get; set; } = "physical";
    public bool BlocksActions { get; set; }
}

public sealed class RecipeDef
{
    public string Material { get; set; } = "";
    public int Quality { get; set; } = 1;
    public string Id { get; set; } = "";
    public Dictionary<string, int> Ingredients { get; set; } = new();
    public string Output { get; set; } = "";
    public int Count { get; set; }
}

public sealed class LootDef
{
    public string Item { get; set; } = "";
    public int Min { get; set; }
    public int Max { get; set; }
    public double Chance { get; set; }
}

public sealed class ScheduleEntry
{
    public int Hour { get; set; }
    public string Activity { get; set; } = "";
}

public sealed class NpcProfile
{
    public string Id { get; set; } = "";
    public string Family { get; set; } = "";
    public string Faction { get; set; } = "";
    public string Profession { get; set; } = "";
    public List<ScheduleEntry> Schedule { get; set; } = new();
}

public sealed class Content
{
    public List<SkillDef> Skills { get; set; } = new();
    public List<AbilityDef> Abilities { get; set; } = new();
    public List<StatusDef> Statuses { get; set; } = new();
    public List<RecipeDef> Recipes { get; set; } = new();
    public List<LootDef> Loot { get; set; } = new();
    public List<NpcProfile> NpcProfiles { get; set; } = new();
    public Dictionary<string, int> Factions { get; set; } = new();

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    public static Content Load(bool includeVerification = false)
    {
        var content = JsonSerializer.Deserialize<Content>(Godot.FileAccess.GetFileAsString("res://game/data/foundation.json"), Json)!;
        if (!includeVerification) content.Abilities.RemoveAll(a => a.VerificationOnly);
        return content;
    }
    public List<string> Validate(HashSet<string> items)
    {
        var errors = new List<string>();
        void Unique(IEnumerable<string> ids, string domain)
        {
            foreach (var g in ids.GroupBy(x => x))
                if (string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1)
                    errors.Add($"{domain}: invalid/duplicate id '{g.Key}'");
        }

        Unique(Skills.Select(x => x.Id), "skill");
        foreach (var skill in Skills)
            if (skill.MaxRank < 1 || !float.IsFinite(skill.PerRank))
                errors.Add($"skill {skill.Id}: invalid rank/modifier");
        Unique(Abilities.Select(x => x.Id), "ability");
        Unique(Statuses.Select(x => x.Id), "status");
        Unique(Recipes.Select(x => x.Id), "recipe");
        Unique(NpcProfiles.Select(x => x.Id), "npc");
        foreach (var a in Abilities)
        {
            static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0;
            if (!Nonnegative(a.Range) || !Nonnegative(a.Cost) || !Nonnegative(a.Cooldown) ||
                !Nonnegative(a.Windup) || !Nonnegative(a.Recovery) || !Nonnegative(a.ProjectileSpeed) ||
                !float.IsFinite(a.Damage) || (!string.IsNullOrEmpty(a.Status) && !Statuses.Any(s => s.Id == a.Status)))
                errors.Add($"ability {a.Id}: invalid timing, cost, damage or status reference");
            string? shape = a.Geometry == null ? "target" : a.Geometry.Shape;
            string? style = a.Telegraph == null ? "none" : a.Telegraph.Style;
            if (shape is not ("target" or "cone"))
                errors.Add($"ability {a.Id}: unsupported attack shape '{shape}'");
            if (a.Geometry != null && (!Nonnegative(a.Geometry.Range) || !Nonnegative(a.Geometry.HalfAngleDegrees) ||
                a.Geometry.HalfAngleDegrees > 180 || (shape == "cone" && (a.Geometry.Range <= 0 || a.Geometry.HalfAngleDegrees <= 0)) ||
                (shape == "target" && (a.Geometry.Range != 0 || a.Geometry.HalfAngleDegrees != 0))))
                errors.Add($"ability {a.Id}: invalid attack geometry");
            if (shape == "cone" && (a.Self || a.ProjectileSpeed > 0 || a.Damage <= 0 || a.Id == "slash"))
                errors.Add($"ability {a.Id}: cone requires a direct hostile damaging ability");
            if (style is not ("none" or "target" or "cone") ||
                (style != "none" && (style != shape || a.Self || a.Damage <= 0 || a.ProjectileSpeed > 0)))
                errors.Add($"ability {a.Id}: telegraph does not match implemented danger");
            if (a.ActionStyle != "" && (!Enum.TryParse<PixelCreatures.Core.Anatomy.ActionStyle>(a.ActionStyle, out var actionStyle) || !Enum.IsDefined(actionStyle) || actionStyle.ToString() != a.ActionStyle))
                errors.Add($"ability {a.Id}: unknown procedural action style");
        }
        foreach (var s in Statuses)
            if (s.Duration <= 0 || s.Interval <= 0 || s.MaxStacks < 1)
                errors.Add($"status {s.Id}: invalid stacking/timing");
        foreach (var r in Recipes)
            if (!items.Contains(r.Output) || r.Count < 1 || r.Ingredients.Count == 0 || r.Ingredients.Any(i => !items.Contains(i.Key) || i.Value < 1))
                errors.Add($"recipe {r.Id}: invalid item or quantity");
        foreach (var l in Loot)
            if (!items.Contains(l.Item) || l.Min < 1 || l.Max < l.Min || l.Chance < 0 || l.Chance > 1)
                errors.Add($"loot {l.Item}: invalid reference/range");
        return errors;
    }
}
