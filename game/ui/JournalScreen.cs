using Godot;
namespace Mosslight;

public sealed record JournalStage(string Id, string Text, bool Done, bool Current);

/// <summary>Read-only journal row from <c>Narrative.journal_rows()</c>. Never parsed from journal().</summary>
public sealed record JournalQuest(int Id, string Title, string Description, string Status, string Objective, bool Staged,
    IReadOnlyList<JournalStage> Stages, int Progress, int Required, int Prerequisite, string PrerequisiteTitle,
    bool PrerequisiteMet, string RewardItem, int RewardCount, bool Rewarded, bool PendingReward)
{
    public static IReadOnlyList<JournalQuest> Read(Node narrative)
    {
        var rows = new List<JournalQuest>();
        foreach (var value in narrative.Call("journal_rows").AsGodotArray())
        {
            var row = value.AsGodotDictionary();
            var stages = row["stages"].AsGodotArray().Select(s => s.AsGodotDictionary())
                .Select(s => new JournalStage(s["id"].AsString(), s["text"].AsString(), s["done"].AsBool(), s["current"].AsBool())).ToList();
            rows.Add(new(row["id"].AsInt32(), row["title"].AsString(), row["description"].AsString(), row["status"].AsString(),
                row["objective"].AsString(), row["staged"].AsBool(), stages, row["progress"].AsInt32(), row["required"].AsInt32(),
                row["prerequisite"].AsInt32(), row["prerequisite_title"].AsString(), row["prerequisite_met"].AsBool(),
                row["reward_item"].AsString(), row["reward_count"].AsInt32(), row["rewarded"].AsBool(), row["pending_reward"].AsBool()));
        }
        return rows;
    }
}

public sealed record JournalExtras(IReadOnlyList<string> Discoveries, IReadOnlyList<string> Lore, int Shortcuts, int Seals)
{
    public static JournalExtras Read(Node narrative)
    {
        var d = narrative.Call("journal_extras").AsGodotDictionary();
        return new(d["discoveries"].AsGodotArray().Select(v => v.AsString()).ToList(), d["lore"].AsGodotArray().Select(v => v.AsString()).ToList(),
            d["shortcuts"].AsInt32(), d["seals"].AsInt32());
    }
}

/// <summary>Journal screen (PR #7 Part 3). Presents QuestSystem/Narrative state; never changes it.</summary>
public static class JournalScreen
{
    public static readonly (string Status, string Heading)[] Sections =
    {
        ("active", "ACTIVE"), ("available", "AVAILABLE FROM THE WARDEN"), ("completed", "COMPLETED"), ("locked", "NOT YET AVAILABLE"), ("failed", "FAILED")
    };

    public static void Populate(VBoxContainer body, Foundation app)
    {
        var quests = JournalQuest.Read(app.Narrative);
        foreach (var (status, heading) in Sections)
        {
            var group = quests.Where(q => q.Status == status).ToList();
            if (group.Count == 0) continue;
            UiDesign.Heading(body, heading);
            foreach (var quest in group) Card(body, app, quest);
        }
        if (quests.Count == 0) UiDesign.Note(body, "No quests yet. Speak to the warden in Reach.");

        var extras = JournalExtras.Read(app.Narrative);
        if (extras.Discoveries.Count > 0 || extras.Shortcuts + extras.Seals > 0)
        {
            var places = UiDesign.Card(body, "discoveries");
            UiDesign.Heading(places, "DISCOVERIES");
            if (extras.Discoveries.Count > 0) places.AddChild(UiDesign.Text(string.Join(", ", extras.Discoveries)));
            if (extras.Shortcuts + extras.Seals > 0)
                places.AddChild(UiDesign.Text($"Ruin mechanisms: {extras.Shortcuts} shortcut{(extras.Shortcuts == 1 ? "" : "s")} raised · {extras.Seals} light seal{(extras.Seals == 1 ? "" : "s")} broken"));
        }
        if (extras.Lore.Count > 0)
        {
            var lore = UiDesign.Card(body, "lore");
            UiDesign.Heading(lore, "LORE");
            foreach (var entry in extras.Lore) lore.AddChild(UiDesign.Text(entry));
        }
    }

    /// <summary>Index medallion for a quest: shape and words carry the state, tone reinforces it.</summary>
    public static (string Glyph, Tone Tone) Marker(JournalQuest q) => q.PendingReward ? ("star", Tone.Gold) : q.Status switch
    {
        "active" => ("diamond", Tone.Gold), "available" => ("spark", Tone.Arcane), "completed" => ("check", Tone.Moss),
        "locked" => ("lock", Tone.Neutral), "failed" => ("cross", Tone.Ember), _ => ("dot", Tone.Neutral)
    };

    public static void Card(VBoxContainer body, Foundation app, JournalQuest q)
    {
        var card = UiDesign.Card(body, "quest_" + q.Id, "Well");
        card.AddThemeConstantOverride("separation", 10);
        string state = q.Status switch
        {
            "active" => q.Staged ? $"Active · step {Math.Min(q.Progress + 1, q.Required)} of {q.Required}" : $"Active · {q.Progress}/{q.Required}",
            "completed" => "Complete",
            "available" => "Available",
            "locked" => "Not yet available",
            "failed" => "Failed",
            _ => q.Status
        };
        var (glyph, tone) = Marker(q);
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; head.AddThemeConstantOverride("separation", 12);
        card.AddChild(head);
        head.AddChild(new Glyph(glyph, UiDesign.ToneColor(tone), 26));
        var title = UiDesign.Title(head, q.Title);
        title.AddThemeFontSizeOverride("font_size", (int)(title.GetThemeFontSize("font_size") * .9f));
        UiDesign.Chip(head, state, tone);
        if (q.Description != "") UiDesign.Note(UiDesign.Card(card, null, "Quote"), q.Description);
        void Step(string mark, Color colour, string text, bool current)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 12);
            card.AddChild(row);
            row.AddChild(new Glyph(mark, colour, 18) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin });
            var label = UiDesign.Text(text); label.ThemeTypeVariation = current ? "RowTitle" : "RowSubtitle"; row.AddChild(label);
        }
        switch (q.Status)
        {
            case "available": Step("spark", UiDesign.Arcane, "Speak to the warden in Reach to accept this quest.", true); break;
            case "locked":
                Step("lock", UiDesign.Faint, q.PrerequisiteMet ? "Already accepted earlier." : $"Requires: complete {q.PrerequisiteTitle} first.", true); break;
            default:
                if (q.Staged)
                {
                    UiDesign.Caption(card, "The path so far");
                    foreach (var stage in q.Stages)
                    {
                        // Completed and current stages only; later stages are not spoiled.
                        if (stage.Done) Step("check", UiDesign.Moss, "Done: " + stage.Text, false);
                        else if (stage.Current) Step("diamond", UiDesign.Gold, "Now: " + stage.Text, true);
                    }
                }
                else
                {
                    Step(q.Status == "completed" ? "check" : "diamond", q.Status == "completed" ? UiDesign.Moss : UiDesign.Gold, $"{q.Objective} ({q.Progress}/{q.Required})", q.Status != "completed");
                    card.AddChild(new Meter { Name = "quest_progress", Caption = "Progress", Value = $"{q.Progress} / {q.Required}", Fill = UiDesign.Experience,
                        Ratio = q.Required == 0 ? 1 : (float)q.Progress / q.Required, CustomMinimumSize = new Vector2(0, 22) });
                }
                break;
        }
        string reward = $"{app.World.Items.Title(q.RewardItem)} ×{q.RewardCount}";
        var prize = new HBoxContainer { Name = "quest_reward_row", MouseFilter = Control.MouseFilterEnum.Ignore }; prize.AddThemeConstantOverride("separation", 10);
        card.AddChild(prize);
        UiDesign.IconWell(prize, ItemIconAtlas.Get(q.RewardItem), 40, !q.Rewarded);
        var line = UiDesign.Note(prize, q.PendingReward ? $"Reward waiting: {reward} · make pack space, then speak to the warden"
            : q.Rewarded ? $"Reward received: {reward}" : $"Reward: {reward}");
        line.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }
}
