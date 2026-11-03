using Godot;
namespace Mosslight;

/// <summary>Persistent header and scroll surface shared by incrementally migrated screens.</summary>
public partial class UiScreenHost : PanelContainer
{
    public VBoxContainer Body { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    Label heading = null!;
    public TouchScroll Scroll { get; private set; } = null!;
    MarginContainer customHost = null!;
    public void Build(Action back)
    {
        var column = new VBoxContainer(); AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        heading = UiDesign.Text(""); header.AddChild(heading);
        BackButton = UiDesign.Action(header, "Back", back);
        BackButton.CustomMinimumSize = new Vector2(112, UiDesign.Target);
        Scroll = new TouchScroll { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollDeadzone = 12 };
        Scroll.GetVScrollBar().CustomMinimumSize = new Vector2(28,0);
        column.AddChild(Scroll);
        customHost = new MarginContainer { Name = "custom_content", Visible = false,
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(customHost);
        Body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; Scroll.AddChild(Body);
    }
    int restoreScroll = -1, restoreFrames;
    public void Present(string title, bool keepScroll = false)
    {
        // Rebuilt content is laid out next frame; restore the position once it exists.
        restoreScroll = keepScroll ? Scroll.ScrollVertical : -1; restoreFrames = 2;
        Scroll.CancelGesture(); Scroll.ScrollVertical = 0;
        ClearCustom(); customHost.Hide(); Scroll.Show();
        foreach (var child in Body.GetChildren()) { Body.RemoveChild(child); child.QueueFree(); }
        heading.Text = title; Show(); BackButton.GrabFocus();
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
