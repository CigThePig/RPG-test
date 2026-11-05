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

    static string AbilityGlyph(string id) => id switch { "slash" => "sword", "ember" => "flame", "mend" => "leaf", "dodge" => "dodge", _ => "spark" };
    static string SkillGlyph(string stat) => stat switch { "strength" => "sword", "defence" => "shield", "power" => "bolt", _ => "star" };

    /// <summary>Card header: icon medallion, title, state chip.</summary>
    static VBoxContainer CardHeader(VBoxContainer card, string glyph, Tone tone, string title, string state, Tone stateTone, string? stateGlyph)
    {
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; head.AddThemeConstantOverride("separation", 12);
        card.AddChild(head);
        var badge = new PanelContainer { ThemeTypeVariation = "IconWell", CustomMinimumSize = new Vector2(46, 46), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddChild(badge);
        badge.AddChild(new Glyph(glyph, UiDesign.ToneColor(tone), 24) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 4);
        head.AddChild(text);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 8);
        text.AddChild(row);
        var name = UiDesign.Heading(row, title.ToUpperInvariant());
        name.AddThemeFontSizeOverride("font_size", (int)(name.GetThemeFontSize("font_size") * 1.15f));
        name.AddThemeColorOverride("font_color", UiDesign.GoldBright);
        UiDesign.Chip(row, state, stateTone, stateGlyph);
        return text;
    }

    public static void Progression(VBoxContainer body, Foundation app, RpgUi ui, bool abilities = true, bool skills = true)
    {
        var actor = app.World.Player;
        var banner = UiDesign.Card(body, "progression_banner", "Quote");
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; line.AddThemeConstantOverride("separation", 10);
        banner.AddChild(line);
        line.AddChild(new Glyph("star", actor.SkillPoints > 0 ? UiDesign.Gold : UiDesign.Faint, 22));
        UiDesign.Heading(line, $"PROGRESSION · {actor.SkillPoints} unspent skill point{(actor.SkillPoints == 1 ? "" : "s")}");
        UiDesign.Note(banner, "Spending skill points is permanent. Each choice asks for confirmation. Level up by defeating foes.");
        if (abilities) foreach (var ability in app.World.Content.Abilities.Where(a => a.PlayerLearnable || actor.Abilities.Contains(a.Id)))
        {
            var card = UiDesign.Card(body, "ability_" + ability.Id);
            bool learned = actor.Abilities.Contains(ability.Id);
            var blocker = LearnBlocker(actor, ability);
            string state = learned ? "Learned" : blocker == null ? "Available" : "Locked";
            var text = CardHeader(card, AbilityGlyph(ability.Id), learned ? Tone.Gold : Tone.Arcane, ability.Name, state,
                learned ? Tone.Moss : blocker == null ? Tone.Gold : Tone.Neutral, learned ? "check" : blocker == null ? null : "lock");
            var facts = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            facts.AddThemeConstantOverride("h_separation", 6); facts.AddThemeConstantOverride("v_separation", 4);
            text.AddChild(facts);
            UiDesign.Chip(facts, $"Mana {ability.Cost:0}", Tone.Arcane);
            UiDesign.Chip(facts, $"Range {ability.Range:0}", Tone.Neutral);
            UiDesign.Chip(facts, $"Recovers {ability.Cooldown:0.#}s", Tone.Neutral);
            if (!learned) UiDesign.Note(text, $"Requires level {ability.UnlockLevel} · costs {ability.UnlockCost} skill point{(ability.UnlockCost == 1 ? "" : "s")}");
            if (learned) continue;
            var learn = UiDesign.Primary(UiDesign.Action(card, "Learn " + ability.Name, () => ui.Confirm(
                $"Learn {ability.Name}? This spends {ability.UnlockCost} skill point{(ability.UnlockCost == 1 ? "" : "s")} and cannot be undone.",
                "Learn " + ability.Name, () =>
                {
                    bool ok = app.ManageItem(() => Mosslight.Progression.Unlock(app.World, actor, ability.Id));
                    ui.Notify(ok ? "Learned " + ability.Name : LearnBlocker(actor, ability) ?? "Cannot learn " + ability.Name + " right now");
                })));
            learn.Name = "learn_" + ability.Id;
            if (blocker != null) { learn.Disabled = true; UiDesign.Note(card, blocker); }
        }
        if (skills) foreach (var skill in app.World.Content.Skills)
        {
            var card = UiDesign.Card(body, "skill_" + skill.Id);
            int rank = actor.SkillRanks.GetValueOrDefault(skill.Id);
            string name = char.ToUpperInvariant(skill.Id[0]) + skill.Id[1..];
            var blocker = TrainBlocker(actor, skill);
            string state = rank >= skill.MaxRank ? "Mastered" : blocker == null ? "Available" : "Blocked";
            var text = CardHeader(card, SkillGlyph(skill.Stat), Tone.Gold, name, state,
                rank >= skill.MaxRank ? Tone.Moss : blocker == null ? Tone.Gold : Tone.Neutral, rank >= skill.MaxRank ? "check" : null);
            var pips = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Name = "rank_pips" }; pips.AddThemeConstantOverride("separation", 4);
            text.AddChild(pips);
            for (int i = 0; i < skill.MaxRank; i++) pips.AddChild(new Glyph(i < rank ? "diamond" : "diamond-outline", i < rank ? UiDesign.Gold : UiDesign.Faint, 14));
            UiDesign.Caption(pips, $"  Rank {rank} of {skill.MaxRank}");
            UiDesign.Note(text, $"Each rank adds {skill.PerRank:+0.##;-0.##;0} {skill.Stat} · costs 1 skill point");
            float now = app.World.Stat(actor, skill.Stat), next = now + (rank < skill.MaxRank ? skill.PerRank : 0);
            var projection = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; projection.AddThemeConstantOverride("separation", 10);
            text.AddChild(projection);
            projection.AddChild(new Glyph(SkillGlyph(skill.Stat), UiDesign.Faint, 16));
            var label = UiDesign.Value(projection, char.ToUpperInvariant(skill.Stat[0]) + skill.Stat[1..], "NoteLabel");
            UiDesign.Value(projection, now.ToString("0.##")).SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            if (rank < skill.MaxRank)
            {
                projection.AddChild(new Glyph("next", UiDesign.Gold, 14));
                UiDesign.Value(projection, next.ToString("0.##"), "PositiveLabel").SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            }
            card.TooltipText = Breakdown(app.World, actor, skill.Stat) + $" · After training: {next:0.##} {skill.Stat}";
            var train = UiDesign.Primary(UiDesign.Action(card, $"Train {name} to rank {Math.Min(rank + 1, skill.MaxRank)}", () => ui.Confirm(
                $"Train {name} to rank {rank + 1}? This spends 1 skill point and cannot be undone.", "Train " + name, () =>
                {
                    bool ok = app.ManageItem(() => actor == app.World.Player && actor.SkillRanks.GetValueOrDefault(skill.Id) == rank && Mosslight.Progression.Train(app.World, actor, skill.Id));
                    ui.Notify(ok ? $"{name} trained to rank {actor.SkillRanks.GetValueOrDefault(skill.Id)}" : TrainBlocker(actor, skill) ?? "Cannot train right now");
                })));
            train.Name = "train_" + skill.Id;
            if (blocker != null) { train.Disabled = true; UiDesign.Note(card, blocker); }
        }
    }
}
