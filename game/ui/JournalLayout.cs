using Godot;
namespace Mosslight;

/// <summary>Quest index and selected reading page. Presentation selection only; Narrative owns all quest state.</summary>
public partial class JournalLayout : VBoxContainer
{
    readonly RpgUi ui;
    bool compact;
    Control? indexPane, detailPane;
    VBoxContainer? reading;
    readonly Dictionary<int, Button> entries = new();
    IReadOnlyList<JournalQuest> quests = Array.Empty<JournalQuest>();
    public JournalLayout(RpgUi owner) { ui = owner; Name = "quest_journal"; }
    public override void _Ready() => Rebuild();
    internal static VBoxContainer Scroll(Node parent, string name)
    {
        var scroll = new TouchScroll { Name = name, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        parent.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(body); return body;
    }
    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddThemeConstantOverride("separation", 10);
        entries.Clear(); quests = JournalQuest.Read(ui.App.Narrative);
        compact = ui.PackAvailableSize.X / ui.TextScale < 1000;
        if (!quests.Any(q => q.Id == ui.JournalSelection))
            ui.JournalSelection = quests.FirstOrDefault(q => q.Status == "active")?.Id ?? quests.FirstOrDefault()?.Id;
        var summary = new HBoxContainer { Name = "journey_summary", MouseFilter = MouseFilterEnum.Ignore };
        summary.AddThemeConstantOverride("separation", 8);
        AddChild(summary);
        UiDesign.Caption(summary, "Your journey");
        UiDesign.Chip(summary, quests.Count(q => q.Status == "active") + " active", Tone.Gold, "diamond");
        UiDesign.Chip(summary, quests.Count(q => q.Status == "completed") + " completed", Tone.Moss, "check");
        int offered = quests.Count(q => q.Status == "available");
        if (offered > 0) UiDesign.Chip(summary, offered + " available", Tone.Arcane, "spark");
        if (compact)
            UiDesign.Tabs(this, new[] { "Quests", "Reading" }, ui.JournalReading ? 1 : 0,
                i => { ui.JournalReading = i == 1; UpdateVisibility(); }, "journal_");
        var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
        var indexColumn = UiDesign.InventoryPanel(regions, "quest_index", "QUESTS", "book");
        indexPane = indexColumn.GetParent<Control>(); indexPane.SizeFlagsStretchRatio = .36f;
        var index = Scroll(indexColumn, "quest_index_scroll");
        var readingColumn = new PanelContainer { Name = "quest_page", ThemeTypeVariation = "Pane", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .64f };
        regions.AddChild(readingColumn);
        detailPane = readingColumn;
        reading = Scroll(readingColumn, "quest_reading_scroll");
        reading.AddThemeConstantOverride("separation", 12);
        foreach (var (status, heading) in JournalScreen.Sections)
        {
            var group = quests.Where(q => q.Status == status).ToList(); if (group.Count == 0) continue;
            UiDesign.Caption(index, heading);
            foreach (var q in group)
            {
                var button = new RichButton(q.Title, 10, 8) { Name = "quest_" + q.Id, SizeFlagsHorizontal = SizeFlags.ExpandFill, MinHeight = 64 * Math.Min(ui.TextScale, 1.2f) };
                button.Pressed += () =>
                {
                    ui.JournalSelection = q.Id; ui.JournalReading = true; ReadSelected(); UpdateVisibility();
                    reading.GetParent<TouchScroll>().ScrollVertical = 0;
                };
                var (glyph, tone) = JournalScreen.Marker(q);
                var medal = new PanelContainer { ThemeTypeVariation = "IconWell", CustomMinimumSize = new Vector2(40, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
                button.Content.AddChild(medal);
                medal.AddChild(new Glyph(glyph, UiDesign.ToneColor(tone), 20) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
                button.Stack(q.Title, Status(q));
                index.AddChild(button);
                entries[q.Id] = button;
            }
        }
        if (quests.Count == 0) UiDesign.Note(index, "No quests yet. Speak to the warden in Reach.");
        ReadSelected(); UpdateVisibility();
    }
    public static string Status(JournalQuest q) => q.PendingReward ? "Complete · reward waiting" : q.Status switch
    { "active" => q.Staged ? $"Active · step {Math.Min(q.Progress + 1, q.Required)} of {q.Required}" : $"Active · {q.Progress} of {q.Required}", "available" => "Available from the warden", "locked" => "Not yet available", "completed" => "Complete", "failed" => "Failed", _ => q.Status };
    void ReadSelected()
    {
        if (reading == null) return;
        foreach (var child in reading.GetChildren()) { reading.RemoveChild(child); child.QueueFree(); }
        foreach (var (id, button) in entries) UiDesign.SetSelected(button, id == ui.JournalSelection);
        var q = quests.FirstOrDefault(q => q.Id == ui.JournalSelection);
        if (q == null) UiDesign.Note(reading, "Select a quest to read your journal.");
        else
        {
            // Keep index IDs stable; selected detail has a separate name.
            var detail = new VBoxContainer { Name = "selected_quest", SizeFlagsHorizontal = SizeFlags.ExpandFill }; reading.AddChild(detail);
            JournalScreen.Card(detail, ui.App, q);
            detail.GetChild(0).Name = "quest_detail_" + q.Id;
        }
        var extras = JournalExtras.Read(ui.App.Narrative);
        if (extras.Discoveries.Count > 0 || extras.Shortcuts + extras.Seals > 0)
        {
            UiDesign.Divider(reading);
            UiDesign.Heading(reading, "DISCOVERED PLACES");
            var places = new HFlowContainer { Name = "discovered_places", MouseFilter = MouseFilterEnum.Ignore };
            places.AddThemeConstantOverride("h_separation", 6); places.AddThemeConstantOverride("v_separation", 6);
            reading.AddChild(places);
            foreach (var place in extras.Discoveries) UiDesign.Chip(places, place, Tone.Neutral, "pin");
            if (extras.Shortcuts + extras.Seals > 0)
                UiDesign.Note(reading, $"Shortcuts raised: {extras.Shortcuts} · Light seals broken: {extras.Seals}");
        }
        if (extras.Lore.Count > 0)
        {
            UiDesign.Heading(reading, "GATHERED LORE");
            foreach (var entry in extras.Lore) UiDesign.Note(UiDesign.Card(reading, null, "Quote"), entry);
        }
    }
    void UpdateVisibility()
    {
        if (indexPane == null || detailPane == null) return;
        if (compact)
            foreach (var (name, text, selected) in new[] { ("journal_quests", "Quests", !ui.JournalReading), ("journal_reading", "Reading", ui.JournalReading) })
                if (FindChild(name, true, false) is Button tab)
                    UiDesign.SetSelected(tab, selected, "TabButton", "TabSelected");
        // A second finger may switch pages while a scroll still owns a touch.
        // Cancel the hidden pane's gesture before it can activate a hidden row.
        void CancelScrolls(Control pane)
        {
            foreach (var scroll in pane.FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>())
                scroll.CancelGesture();
        }
        if (compact && ui.JournalReading && indexPane.Visible) CancelScrolls(indexPane);
        if (compact && !ui.JournalReading && detailPane.Visible) CancelScrolls(detailPane);
        indexPane.Visible = !compact || !ui.JournalReading; detailPane.Visible = !compact || ui.JournalReading;
    }
    public bool DismissReading()
    {
        if (!compact || !ui.JournalReading) return false;
        ui.JournalReading = false; UpdateVisibility(); return true;
    }
}
