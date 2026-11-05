using Godot;
namespace Mosslight;

/// <summary>Persistent header (emblem, engraved title, Back) and scroll surface shared by every screen.</summary>
public partial class UiScreenHost : PanelContainer
{
    public VBoxContainer Body { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    Label heading = null!, subtitle = null!;
    Glyph emblem = null!;
    public TouchScroll Scroll { get; private set; } = null!;
    MarginContainer customHost = null!;
    public string Title => heading.Text;

    static readonly Dictionary<string, (string Title, string Subtitle, string Glyph)> Titles = new()
    {
        ["THE WAYFARER'S KIT"] = ("The Wayfarer's Kit", "Equipment and belongings", "bag"),
        ["PACK & EQUIPMENT"] = ("Pack", "Craft and trade", "bag"),
        ["CHARACTER · WAYFARER"] = ("Character", "Attributes, training and abilities", "person"),
        ["QUEST JOURNAL"] = ("Journal", "Quests, places and lore", "book"),
        ["REGION MAP"] = ("Map", "Explored lands", "map"),
        ["SAVE & LOAD"] = ("Save & Load", "Your adventures", "scroll"),
        ["SETTINGS"] = ("Settings", "Comfort, display and sound", "gear"),
        ["DEVELOPER TOOLS"] = ("Developer Tools", "Testing, tuning and diagnostics", "hammer"),
        ["GAME-FEEL LAB · development tools"] = ("Game-feel Lab", "Development tools", "hammer"),
        ["PROCEDURAL CREATURE WORKSHOP"] = ("Creature Workshop", "Development tools", "hammer"),
    };

    public void Build(Action back)
    {
        ThemeTypeVariation = "ScreenPanel";
        var column = new VBoxContainer(); AddChild(column);
        column.AddThemeConstantOverride("separation", 6);
        var header = new HBoxContainer(); column.AddChild(header);
        header.AddThemeConstantOverride("separation", 14);
        var medal = new PanelContainer { ThemeTypeVariation = "Well", CustomMinimumSize = new Vector2(52, 52), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        header.AddChild(medal);
        emblem = new Glyph("dot", UiDesign.Gold, 26) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        medal.AddChild(emblem);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        titles.AddThemeConstantOverride("separation", 0);
        header.AddChild(titles);
        heading = new Label { ThemeTypeVariation = "TitleLabel", MouseFilter = MouseFilterEnum.Ignore, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        titles.AddChild(heading);
        subtitle = new Label { ThemeTypeVariation = "CaptionLabel", MouseFilter = MouseFilterEnum.Ignore, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        titles.AddChild(subtitle);
        var backButton = new RichButton("Back", 14, 6) { Name = "screen_back", CustomMinimumSize = new Vector2(124, UiDesign.Target), SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = "Back" };
        backButton.Content.Alignment = BoxContainer.AlignmentMode.Center;
        backButton.Content.AddThemeConstantOverride("separation", 8);
        backButton.Content.AddChild(new Glyph("back", UiDesign.Gold, 16));
        var label = new Label { Text = "Back", ThemeTypeVariation = "RowTitle", VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        backButton.Content.AddChild(label);
        backButton.Pressed += back;
        header.AddChild(backButton);
        BackButton = backButton;
        UiDesign.Divider(column);
        Scroll = new TouchScroll { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        Scroll.GetVScrollBar().CustomMinimumSize = new Vector2(28,0);
        column.AddChild(Scroll);
        customHost = new MarginContainer { Name = "custom_content", Visible = false,
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        customHost.AddThemeConstantOverride("margin_top", 4);
        column.AddChild(customHost);
        Body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; Scroll.AddChild(Body);
    }
    int restoreScroll = -1, restoreFrames;
    public void Present(string title, bool keepScroll = false, string? subtitleOverride = null)
    {
        // Rebuilt content is laid out next frame; restore the position once it exists.
        restoreScroll = keepScroll ? Scroll.ScrollVertical : -1; restoreFrames = 2;
        Scroll.CancelGesture(); Scroll.ScrollVertical = 0;
        ClearCustom(); customHost.Hide(); Scroll.Show();
        foreach (var child in Body.GetChildren()) { Body.RemoveChild(child); child.QueueFree(); }
        var (text, sub, glyph) = Titles.TryGetValue(title, out var known) ? known
            : (System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.ToLowerInvariant()), "", "diamond");
        heading.Text = text; subtitle.Text = subtitleOverride ?? sub; subtitle.Visible = subtitle.Text != ""; emblem.Kind = glyph;
        Show(); BackButton.GrabFocus();
    }
    public void MountCustom(Control screen)
    {
        ClearCustom();
        Scroll.CancelGesture();
        Scroll.Hide();
        customHost.Show();
        screen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        screen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        customHost.AddChild(screen);
        restoreScroll = -1;
    }
    void ClearCustom()
    {
        foreach (var child in customHost.GetChildren())
        {
            // Removing an input-processing Control immediately during a Button
            // callback can leave a native GUI event holding an out-of-tree Node.
            // Hide first, cancel owned gestures and let Godot free at frame end.
            // Release the stable screen name immediately. Godot otherwise renames
            // the replacement while this retired node awaits frame-end deletion.
            child.Name = "retired_" + child.GetInstanceId();
            if (child is Control control) control.Hide();
            foreach (var scroll in child.FindChildren("*", "ScrollContainer", true, false).OfType<TouchScroll>())
                scroll.CancelGesture();
            if (!child.IsQueuedForDeletion()) child.QueueFree();
        }
    }
    public override void _Process(double delta)
    {
        if (restoreScroll < 0 || --restoreFrames > 0) return;
        Scroll.ScrollVertical = restoreScroll; restoreScroll = -1;
    }
}
