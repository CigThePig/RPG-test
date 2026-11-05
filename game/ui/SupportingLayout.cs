using Godot;
namespace Mosslight;

/// <summary>Save-slot library or grouped settings surface. All actions remain on RpgUi's existing guarded paths.</summary>
public partial class SupportingLayout : VBoxContainer
{
    readonly RpgUi ui;
    readonly bool saves;
    public SupportingLayout(RpgUi owner, bool saveScreen) { ui = owner; saves = saveScreen; Name = saves ? "save_library" : "preferences"; }
    public override void _Ready() => Rebuild();
    public void Rebuild()
    {
        if (!IsInsideTree()) return;
        foreach (var scroll in FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>()) scroll.CancelGesture();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddThemeConstantOverride("separation", 10);
        bool compact = ui.PackAvailableSize.X / ui.TextScale < 850;
        if (saves)
        {
            UiDesign.Note(this, "Choose an adventure. Background recovery is kept separately and never overwrites a slot.");
            var regions = new BoxContainer { Vertical = compact, SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
            var libraryColumn = UiDesign.InventoryPanel(regions, "save_slots", "ADVENTURES", "scroll");
            libraryColumn.GetParent<Control>().SizeFlagsStretchRatio = .42f;
            var library = JournalLayout.Scroll(libraryColumn, "save_slot_scroll");
            var grid = new GridContainer { Columns = compact ? 2 : 1, SizeFlagsHorizontal = SizeFlags.ExpandFill }; library.AddChild(grid);
            grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8);
            for (int slot = 1; slot <= 4; slot++)
            {
                int selected = slot;
                var info = Persistence.Inspect(slot == 4 ? Persistence.AutosavePath : Persistence.SlotPath(slot), ui.App.World.Content);
                string state = info.State switch { SlotState.Ready => "Saved game", SlotState.Empty => "Empty", SlotState.Incompatible => "Older version", _ => "Damaged" };
                var tone = info.State switch { SlotState.Ready => Tone.Moss, SlotState.Empty => Tone.Neutral, SlotState.Incompatible => Tone.Arcane, _ => Tone.Ember };
                string title = slot == 4 ? "Recovery" : "Slot " + slot;
                var choose = new RichButton(title + " · " + state, 10, 8) { Name = "choose_slot_" + slot, SizeFlagsHorizontal = SizeFlags.ExpandFill, MinHeight = 72 * Math.Min(ui.TextScale, 1.2f) };
                choose.Pressed += () => { ui.SaveSelection = selected; ui.SaveLoad(); };
                UiDesign.SetSelected(choose, slot == ui.SaveSelection);
                var medal = new PanelContainer { ThemeTypeVariation = "IconWell", CustomMinimumSize = new Vector2(48, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
                choose.Content.AddChild(medal);
                if (slot == 4) medal.AddChild(new Glyph("scroll", UiDesign.Gold, 24) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
                else
                {
                    var numeral = new Label { Text = slot.ToString(), ThemeTypeVariation = "DisplayValue", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                    numeral.AddThemeFontOverride("font", UiDesign.BodySemi); numeral.AddThemeFontSizeOverride("font_size", (int)(24 * Math.Min(ui.TextScale, 1.2f)));
                    medal.AddChild(numeral);
                }
                choose.Stack(title, info.State switch
                {
                    SlotState.Ready => "Level " + info.Level, SlotState.Empty => "No adventure saved yet",
                    SlotState.Incompatible => "Made by an older build", _ => "File could not be read"
                });
                if (!compact) UiDesign.Chip(choose.Content, state, tone);
                grid.AddChild(choose);
            }
            var detailColumn = UiDesign.InventoryPanel(regions, "save_detail", "DETAILS", "eye");
            detailColumn.GetParent<Control>().SizeFlagsStretchRatio = .58f;
            var details = JournalLayout.Scroll(detailColumn, "save_detail_scroll");
            ui.PopulateSaveDetails(details);
        }
        else
        {
            UiDesign.Tabs(this, new[] { "Display", "Comfort", "Audio" }, ui.SettingsPage, i => { ui.SettingsPage = i; ui.Settings(); }, "settings_");
            var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 12); AddChild(regions);
            var optionsColumn = UiDesign.InventoryPanel(regions, "settings_options", new[] { "DISPLAY", "COMFORT", "AUDIO" }[Math.Clamp(ui.SettingsPage, 0, 2)], new[] { "eye", "feather", "spark" }[Math.Clamp(ui.SettingsPage, 0, 2)]);
            optionsColumn.GetParent<Control>().SizeFlagsStretchRatio = .66f;
            var options = JournalLayout.Scroll(optionsColumn, "settings_scroll");
            options.AddThemeConstantOverride("separation", 10);
            ui.PopulateSettings(options);
            if (!compact)
            {
                var previewColumn = UiDesign.InventoryPanel(regions, "settings_summary", "PREVIEW", "book");
                previewColumn.GetParent<Control>().SizeFlagsStretchRatio = .34f;
                var preview = JournalLayout.Scroll(previewColumn, "settings_preview_scroll");
                UiDesign.Caption(preview, "Your preferences");
                var chips = new HFlowContainer { MouseFilter = MouseFilterEnum.Ignore };
                chips.AddThemeConstantOverride("h_separation", 6); chips.AddThemeConstantOverride("v_separation", 6);
                preview.AddChild(chips);
                UiDesign.Chip(chips, "Text · " + ui.Preferences.TextSizeName, Tone.Gold);
                UiDesign.Chip(chips, "Sound · " + (ui.Preferences.Muted ? "Off" : "On"), ui.Preferences.Muted ? Tone.Neutral : Tone.Moss);
                UiDesign.Chip(chips, "Reduced motion · " + (ui.Preferences.ReducedMotion ? "On" : "Off"), ui.Preferences.ReducedMotion ? Tone.Moss : Tone.Neutral);
                UiDesign.Chip(chips, "High contrast · " + (ui.Preferences.HighContrast ? "On" : "Off"), ui.Preferences.HighContrast ? Tone.Moss : Tone.Neutral);
                UiDesign.Caption(preview, "Readability preview");
                var sample = UiDesign.Card(preview, "readability_sample", "Well");
                var quest = JournalQuest.Read(ui.App.Narrative).FirstOrDefault(q => q.Status == "active");
                UiDesign.Heading(sample, (quest?.Title ?? "Your adventure").ToUpperInvariant());
                sample.AddChild(UiDesign.Text(quest?.Objective ?? "Text and contrast changes apply immediately to menus and dialogue."));
                UiDesign.Note(sample, "These choices stay on this device when you load a saved game.");
            }
        }
    }
}
