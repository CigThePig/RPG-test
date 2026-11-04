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

    public static void Card(VBoxContainer body, Foundation app, JournalQuest q)
    {
        var card = UiDesign.Card(body, "quest_" + q.Id);
        string state = q.Status switch
        {
            "active" => q.Staged ? $"Active · step {Math.Min(q.Progress + 1, q.Required)} of {q.Required}" : $"Active · {q.Progress}/{q.Required}",
            "completed" => "Complete",
            "available" => "Available",
            "locked" => "Not yet available",
            "failed" => "Failed",
            _ => q.Status
        };
        UiDesign.Heading(card, $"{q.Title.ToUpperInvariant()}  ·  {state}");
        if (q.Description != "") UiDesign.Note(card, q.Description);
        switch (q.Status)
        {
            case "available": card.AddChild(UiDesign.Text("Speak to the warden in Reach to accept this quest.")); break;
            case "locked":
                card.AddChild(UiDesign.Text(q.PrerequisiteMet ? "Already accepted earlier." : $"Requires: complete {q.PrerequisiteTitle} first.")); break;
            default:
                if (q.Staged)
                {
                    int shown = 0;
                    foreach (var stage in q.Stages)
                    {
                        // Completed and current stages only; later stages are not spoiled.
                        if (stage.Done) { card.AddChild(UiDesign.Text("Done: " + stage.Text)); shown++; }
                        else if (stage.Current) { card.AddChild(UiDesign.Text("Now: " + stage.Text)); shown++; }
                    }

                }
                else card.AddChild(UiDesign.Text($"{q.Objective} ({q.Progress}/{q.Required})"));
                break;
        }
        string reward = $"{app.World.Items.Title(q.RewardItem)} ×{q.RewardCount}";
        UiDesign.Note(card, q.PendingReward ? $"Reward waiting: {reward} · make pack space, then speak to the warden"
            : q.Rewarded ? $"Reward received: {reward}" : $"Reward: {reward}");
    }
}
