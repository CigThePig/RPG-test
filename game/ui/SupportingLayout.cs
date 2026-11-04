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
        bool compact = ui.PackAvailableSize.X / ui.TextScale < 850;
        if (saves)
        {
            UiDesign.Note(this, "Choose a saved adventure · background recovery is kept separately.");
            var regions = new BoxContainer { Vertical = compact, SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 16); AddChild(regions);
            var library = JournalLayout.Scroll(regions, "save_slot_scroll"); library.GetParent<Control>().SizeFlagsStretchRatio = .42f;
            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; library.AddChild(grid);
            grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8);
            for (int slot = 1; slot <= 4; slot++)
            {
                int selected = slot;
                var info = Persistence.Inspect(slot == 4 ? Persistence.AutosavePath : Persistence.SlotPath(slot), ui.App.World.Content);
                string state = info.State switch { SlotState.Ready => "Saved game", SlotState.Empty => "Empty", SlotState.Incompatible => "Older version", _ => "Damaged" };
                var choose = UiDesign.Action(grid, (slot == ui.SaveSelection ? "[Selected] " : "") + (slot == 4 ? "Recovery" : "Slot " + slot) + "\n" + state +
                    (info.State == SlotState.Ready ? "\nLevel " + info.Level : ""), () => { ui.SaveSelection = selected; ui.SaveLoad(); });
                choose.Name = "choose_slot_" + slot; choose.SizeFlagsHorizontal = SizeFlags.ExpandFill; choose.CustomMinimumSize = new Vector2(0, 112);
                var face = UiDesign.InventoryTile(slot == ui.SaveSelection); choose.AddThemeStyleboxOverride("normal", face); choose.AddThemeStyleboxOverride("hover", face);
            }
            var details = JournalLayout.Scroll(regions, "save_detail_scroll"); details.GetParent<Control>().SizeFlagsStretchRatio = .58f;
            ui.PopulateSaveDetails(details);
        }
        else
        {
            UiDesign.Tabs(this, new[] { "Display", "Comfort", "Audio" }, ui.SettingsPage, i => { ui.SettingsPage = i; ui.Settings(); }, "settings_");
            var regions = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; regions.AddThemeConstantOverride("separation", 16); AddChild(regions);
            var options = JournalLayout.Scroll(regions, "settings_scroll"); options.GetParent<Control>().SizeFlagsStretchRatio = .68f;
            ui.PopulateSettings(options);
            if (!compact)
            {
                var preview = JournalLayout.Scroll(regions, "settings_preview_scroll"); preview.GetParent<Control>().SizeFlagsStretchRatio = .32f;
                UiDesign.Heading(preview, "YOUR PREFERENCES");
                UiDesign.Note(preview, $"Text: {ui.Preferences.TextSizeName}\nSound: {(ui.Preferences.Muted ? "Off" : "On")}\nReduced motion: {(ui.Preferences.ReducedMotion ? "On" : "Off")}\nHigh contrast: {(ui.Preferences.HighContrast ? "On" : "Off")}");
                UiDesign.Heading(preview, "READABILITY PREVIEW");
                var quest = JournalQuest.Read(ui.App.Narrative).FirstOrDefault(q => q.Status == "active");
                UiDesign.Note(preview, quest?.Title ?? "Your adventure");
                UiDesign.Note(preview, quest?.Objective ?? "Text and contrast changes apply immediately to menus and dialogue.");
                UiDesign.Note(preview, "These choices stay on this device when you load a saved game.");
            }
        }
    }
}
