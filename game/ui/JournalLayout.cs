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
        entries.Clear(); quests = JournalQuest.Read(ui.App.Narrative);
        compact = ui.PackAvailableSize.X / ui.TextScale < 1000;
        if (!quests.Any(q => q.Id == ui.JournalSelection))
            ui.JournalSelection = quests.FirstOrDefault(q => q.Status == "active")?.Id ?? quests.FirstOrDefault()?.Id;
        UiDesign.Note(this, "YOUR JOURNEY · " + quests.Count(q => q.Status == "active") + " active · " + quests.Count(q => q.Status == "completed") + " completed");
        if (compact)
            UiDesign.Tabs(this, new[] { "Quests", "Reading" }, ui.JournalReading ? 1 : 0,
                i => { ui.JournalReading = i == 1; UpdateVisibility(); }, "journal_");
        var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 16); AddChild(regions);
        var index = Scroll(regions, "quest_index_scroll"); indexPane = index.GetParent<Control>(); indexPane.SizeFlagsStretchRatio = .34f;
        reading = Scroll(regions, "quest_reading_scroll"); detailPane = reading.GetParent<Control>(); detailPane.SizeFlagsStretchRatio = .66f;
        foreach (var (status, heading) in JournalScreen.Sections)
        {
            var group = quests.Where(q => q.Status == status).ToList(); if (group.Count == 0) continue;
            UiDesign.Heading(index, heading);
            foreach (var q in group)
            {
                var button = UiDesign.Action(index, q.Title + "\n" + Status(q), () =>
                {
                    ui.JournalSelection = q.Id; ui.JournalReading = true; ReadSelected(); UpdateVisibility();
                    reading.GetParent<TouchScroll>().ScrollVertical = 0;
                });
                button.Name = "quest_" + q.Id; entries[q.Id] = button;
            }
        }
        if (quests.Count == 0) UiDesign.Note(index, "No quests yet. Speak to the warden in Reach.");
        ReadSelected(); UpdateVisibility();
    }
    public static string Status(JournalQuest q) => q.PendingReward ? "Complete · reward waiting" : q.Status switch
    { "active" => "Active", "available" => "Available from the warden", "locked" => "Not yet available", "completed" => "Complete", "failed" => "Failed", _ => q.Status };
    void ReadSelected()
    {
        if (reading == null) return;
        foreach (var child in reading.GetChildren()) { reading.RemoveChild(child); child.QueueFree(); }
        foreach (var (id, button) in entries)
        {
            var face = UiDesign.InventoryTile(id == ui.JournalSelection);
            button.AddThemeStyleboxOverride("normal", face); button.AddThemeStyleboxOverride("hover", face);
            button.Text = (id == ui.JournalSelection ? "[Selected] " : "") + quests.First(q => q.Id == id).Title + "\n" + Status(quests.First(q => q.Id == id));
        }
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
        if (extras.Discoveries.Count > 0)
        { UiDesign.Heading(reading, "DISCOVERED PLACES"); UiDesign.Note(reading, string.Join(" · ", extras.Discoveries)); }
        if (extras.Shortcuts + extras.Seals > 0)
            UiDesign.Note(reading, $"Shortcuts raised: {extras.Shortcuts} · Light seals broken: {extras.Seals}");
        if (extras.Lore.Count > 0)
        { UiDesign.Heading(reading, "GATHERED LORE"); foreach (var entry in extras.Lore) UiDesign.Note(reading, entry); }
    }
    void UpdateVisibility()
    {
        if (indexPane == null || detailPane == null) return;
        if (compact)
            foreach (var (name, text, selected) in new[] { ("journal_quests", "Quests", !ui.JournalReading), ("journal_reading", "Reading", ui.JournalReading) })
                if (FindChild(name, true, false) is Button tab)
                {
                    tab.Text = selected ? "[" + text + "]" : text;
                    var face = UiDesign.InventoryTile(selected); tab.AddThemeStyleboxOverride("normal", face); tab.AddThemeStyleboxOverride("hover", face);
                }
        // A second finger may switch pages while a scroll still owns a touch.
        // Cancel the hidden pane's gesture before it can activate a hidden row.
        if (compact && ui.JournalReading && indexPane.Visible && indexPane is TouchScroll indexScroll) indexScroll.CancelGesture();
        if (compact && !ui.JournalReading && detailPane.Visible && detailPane is TouchScroll readingScroll) readingScroll.CancelGesture();
        indexPane.Visible = !compact || !ui.JournalReading; detailPane.Visible = !compact || ui.JournalReading;
    }
    public bool DismissReading()
    {
        if (!compact || !ui.JournalReading) return false;
        ui.JournalReading = false; UpdateVisibility(); return true;
    }
}
