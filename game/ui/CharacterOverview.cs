using Godot;
namespace Mosslight;

/// <summary>Read-only production character view plus confirmed progression actions. No cached or parallel gameplay state.</summary>
public static class CharacterOverview
{
    public static void Populate(VBoxContainer body, Foundation app) => Populate(body, app, null);

    public static void Populate(VBoxContainer body, Foundation app, RpgUi? ui)
    {
        var actor = app.World.Player;
        var world = app.World;
        body.AddChild(UiDesign.Text($"{actor.Name} · Level {actor.Level}\n{actor.Region} · {actor.State}"));
        void Section(string title, string text)
        {
            var column = UiDesign.Card(body, "section_" + title.ToLowerInvariant().Split(' ')[0]);
            UiDesign.Heading(column, title); column.AddChild(UiDesign.Text(text));
        }
        Section("VITALS", $"Health  {actor.Health:0} / {actor.MaxHealth}\nMana  {actor.Mana:0} / {actor.MaxMana}\nExperience  {actor.Experience} / {actor.Level * 100}\nUnspent skill points  {actor.SkillPoints}");
        Section("COMBAT & EQUIPMENT", string.Join("\n", new[] { "strength", "defence", "power" }.Select(stat => Breakdown(world, actor, stat)))
            + $"\nWeapon damage bonus  {world.Items.Bonus(actor, "damage"):0.##}");
        Section("ABILITIES", string.Join("\n", world.Content.Abilities.Where(a => actor.Abilities.Contains(a.Id)).Select(a => a.Name)));
        Section("TRAINING", string.Join("\n", world.Content.Skills.Select(s => $"{s.Id}  {actor.SkillRanks.GetValueOrDefault(s.Id)} / {s.MaxRank}")));
        Section("APPEARANCE", $"Family  {actor.Appearance.Family} · seed {actor.Appearance.Seed}");
        Section("EXPLORATION", $"Flight: {(actor.Flies ? "Yes" : "No")} · Swimming: {(actor.Swims ? "Yes" : "No")}\nGrasping: {(actor.Grasps ? "Yes" : "No")}\nWarden reputation  {actor.Reputation.GetValueOrDefault("wardens")}");
        if (ui != null) Progression(body, app, ui);
    }

    /// <summary>"Strength 12 (base 10 · training +2)" from the same sources World.Stat sums.</summary>
    public static string Breakdown(World world, ActorState actor, string stat)
    {
        float total = world.Stat(actor, stat);
        float baseValue = stat switch { "strength" => actor.Strength, "defence" => actor.Defence, _ => 0 };
        float equipment = world.Items.Bonus(actor, stat);
        float training = world.Content.Skills.Where(s => s.Stat == stat).Sum(s => actor.SkillRanks.GetValueOrDefault(s.Id) * s.PerRank);
        float effects = total - baseValue - equipment - training;
        var parts = new List<string> { $"base {baseValue:0.##}" };
        if (Math.Abs(equipment) > .001f) parts.Add($"equipment {equipment:+0.##;-0.##}");
        if (Math.Abs(training) > .001f) parts.Add($"training {training:+0.##;-0.##}");
        if (Math.Abs(effects) > .001f) parts.Add($"effects {effects:+0.##;-0.##}");
        return $"{char.ToUpperInvariant(stat[0])}{stat[1..]}  {total:0.##}  ({string.Join(" · ", parts)})";
    }

    /// <summary>Why an ability cannot be learned now, or null. Mirrors Progression.Unlock.</summary>
    public static string? LearnBlocker(ActorState actor, AbilityDef ability)
    {
        if (actor.Abilities.Contains(ability.Id)) return "Already learned";
        if (actor.Id == "player" && !ability.PlayerLearnable) return "Cannot be learned";
        if (actor.Level < ability.UnlockLevel) return $"Requires level {ability.UnlockLevel} · you are level {actor.Level}";
        if (actor.SkillPoints < ability.UnlockCost) return $"Costs {ability.UnlockCost} skill point{(ability.UnlockCost == 1 ? "" : "s")} · you have {actor.SkillPoints}";
        return null;
    }

    /// <summary>Why a skill cannot be trained now, or null. Mirrors Progression.Train.</summary>
    public static string? TrainBlocker(ActorState actor, SkillDef skill)
    {
        if (actor.SkillRanks.GetValueOrDefault(skill.Id) >= skill.MaxRank) return "Fully trained";
        if (actor.SkillPoints < 1) return $"Costs 1 skill point · you have {actor.SkillPoints}";
        return null;
    }

    public static void Progression(VBoxContainer body, Foundation app, RpgUi ui, bool abilities = true, bool skills = true)
    {
        var actor = app.World.Player;
        UiDesign.Heading(body, $"PROGRESSION · {actor.SkillPoints} unspent skill point{(actor.SkillPoints == 1 ? "" : "s")}");
        UiDesign.Note(body, "Spending skill points is permanent. Each choice asks for confirmation. Level up by defeating foes.");
        if (abilities) foreach (var ability in app.World.Content.Abilities.Where(a => a.PlayerLearnable || actor.Abilities.Contains(a.Id)))
        {
            var card = UiDesign.Card(body, "ability_" + ability.Id);
            bool learned = actor.Abilities.Contains(ability.Id);
            UiDesign.Heading(card, $"{ability.Name.ToUpperInvariant()}  ·  {(learned ? "Learned" : LearnBlocker(actor, ability) == null ? "Available" : "Locked")}");
            card.AddChild(UiDesign.Text($"Mana {ability.Cost:0} · range {ability.Range:0} · recovers in {ability.Cooldown:0.#}s" + (learned ? "" : $"\nRequires level {ability.UnlockLevel} · costs {ability.UnlockCost} skill point{(ability.UnlockCost == 1 ? "" : "s")}")));
            if (learned) continue;
            var blocker = LearnBlocker(actor, ability);
            var learn = UiDesign.Action(card, "Learn " + ability.Name, () => ui.Confirm(
                $"Learn {ability.Name}? This spends {ability.UnlockCost} skill point{(ability.UnlockCost == 1 ? "" : "s")} and cannot be undone.",
                "Learn " + ability.Name, () =>
                {
                    bool ok = app.ManageItem(() => Mosslight.Progression.Unlock(app.World, actor, ability.Id));
                    ui.Notify(ok ? "Learned " + ability.Name : LearnBlocker(actor, ability) ?? "Cannot learn " + ability.Name + " right now");
                }));
            learn.Name = "learn_" + ability.Id;
            if (blocker != null) { learn.Disabled = true; UiDesign.Note(card, blocker); }
        }
        if (skills) foreach (var skill in app.World.Content.Skills)
        {
            var card = UiDesign.Card(body, "skill_" + skill.Id);
            int rank = actor.SkillRanks.GetValueOrDefault(skill.Id);
            string name = char.ToUpperInvariant(skill.Id[0]) + skill.Id[1..];
            UiDesign.Heading(card, $"{name.ToUpperInvariant()} · { (rank >= skill.MaxRank ? "Mastered" : TrainBlocker(actor, skill) == null ? "Available" : "Blocked")} · rank {rank} of {skill.MaxRank}");
            card.AddChild(UiDesign.Text($"Each rank adds {skill.PerRank:+0.##;-0.##;0} {skill.Stat} · costs 1 skill point\n" + Breakdown(app.World, actor, skill.Stat) + $"\nAfter training: {app.World.Stat(actor, skill.Stat) + (rank < skill.MaxRank ? skill.PerRank : 0):0.##} {skill.Stat}"));
            var blocker = TrainBlocker(actor, skill);
            var train = UiDesign.Action(card, $"Train {name} to rank {Math.Min(rank + 1, skill.MaxRank)}", () => ui.Confirm(
                $"Train {name} to rank {rank + 1}? This spends 1 skill point and cannot be undone.", "Train " + name, () =>
                {
                    bool ok = app.ManageItem(() => actor == app.World.Player && actor.SkillRanks.GetValueOrDefault(skill.Id) == rank && Mosslight.Progression.Train(app.World, actor, skill.Id));
                    ui.Notify(ok ? $"{name} trained to rank {actor.SkillRanks.GetValueOrDefault(skill.Id)}" : TrainBlocker(actor, skill) ?? "Cannot train right now");
                }));
            train.Name = "train_" + skill.Id;
            if (blocker != null) { train.Disabled = true; UiDesign.Note(card, blocker); }
        }
    }
}
