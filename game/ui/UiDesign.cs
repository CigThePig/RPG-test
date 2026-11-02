using Godot;
namespace Mosslight;

/// <summary>Shared presentation tokens. Logical pixels are not Android density-independent pixels.</summary>
public static class UiDesign
{
    public const int Gap = 12, Target = 56;
    public static readonly Color Ink = new("eee9d6"), Surface = new("172523"), Bronze = new("bcaa76");
    static StyleBoxFlat Box(Color color, Color border, int width = 2) => new()
    {
        BgColor = color, BorderColor = border,
        BorderWidthBottom = width, BorderWidthTop = width, BorderWidthLeft = width, BorderWidthRight = width,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12
    };
    public static StyleBoxFlat Notification() => Box(Surface, Bronze);
    public static Theme Theme(float scale)
    {
        var theme = new Theme { DefaultFontSize = (int)(20 * scale) };
        theme.SetStylebox("panel", "PanelContainer", Box(Surface, Bronze));
        foreach (var type in new[] { "Label", "Button" }) theme.SetColor("font_color", type, Ink);
        theme.SetStylebox("normal", "Button", Box(new Color("263c36"), new Color("71816c")));
        theme.SetStylebox("hover", "Button", Box(new Color("365347"), Bronze));
        theme.SetStylebox("pressed", "Button", Box(new Color("101c1a"), Bronze));
        theme.SetStylebox("focus", "Button", Box(new Color(0,0,0,0), new Color("ffdf8c"), 3));
        theme.SetColor("font_disabled_color", "Button", new Color("b6b8b0"));
        theme.SetConstant("separation", "VBoxContainer", Gap);
        // ScrollContainer reserves the scrollbar's theme minimum width, not its
        // CustomMinimumSize. Give the track a real 28px minimum so content is laid
        // out beside the touch-draggable scrollbar instead of underneath it.
        var track = new StyleBoxFlat { BgColor = new Color(0, 0, 0, .25f), ContentMarginLeft = 14, ContentMarginRight = 14,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
        theme.SetStylebox("scroll", "VScrollBar", track);
        theme.SetStylebox("scroll_focus", "VScrollBar", track);
        foreach (var (state, color) in new[] { ("grabber", new Color("71816c")), ("grabber_highlight", Bronze), ("grabber_pressed", Bronze) })
            theme.SetStylebox(state, "VScrollBar", new StyleBoxFlat { BgColor = color,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 });
        return theme;
    }
    public static Label Text(string text) => new()
    {
        Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
    };
    public static Button Action(Node parent, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, Target),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Pass };
        button.Pressed += action; parent.AddChild(button); return button;
    }
    public static Rect2 Overlay(Rect2 safe, float width) => new(
        safe.Position + new Vector2((safe.Size.X - Math.Min(width, safe.Size.X - 32)) / 2, 16),
        new Vector2(Math.Min(width, safe.Size.X - 32), safe.Size.Y - 112));
}
