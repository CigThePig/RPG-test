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
    /// <summary>High-contrast preference: opaque surfaces, brighter borders and secondary text. Read when controls are built.</summary>
    public static bool HighContrast { get; set; }
    public static Color Border => HighContrast ? Ink : Bronze;
    public static StyleBoxFlat Notification() => Box(HighContrast ? new Color("0b1312") : Surface, Border);
    public static Theme Theme(float scale)
    {
        var theme = new Theme { DefaultFontSize = (int)(20 * scale) };
        theme.SetStylebox("panel", "PanelContainer", Box(HighContrast ? new Color("0b1312") : Surface, Border));
        foreach (var type in new[] { "Label", "Button" }) theme.SetColor("font_color", type, Ink);
        theme.SetStylebox("normal", "Button", Box(HighContrast ? new Color("1a2a26") : new Color("263c36"), HighContrast ? Bronze : new Color("71816c")));
        theme.SetStylebox("hover", "Button", Box(new Color("365347"), Bronze));
        theme.SetStylebox("pressed", "Button", Box(new Color("101c1a"), Bronze));
        theme.SetStylebox("focus", "Button", Box(new Color(0,0,0,0), new Color("ffdf8c"), 3));
        theme.SetColor("font_disabled_color", "Button", new Color("b6b8b0"));
        // Disabled: flatter face and dim border; a written reason always accompanies it.
        theme.SetStylebox("disabled", "Button", Box(new Color("1c2a27"), new Color("4a5550")));
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
    /// <summary>Secondary text (reasons, hints). Lighter ivory keeps 4.5:1 on the surface; meaning is in words.</summary>
    public static Color Muted => HighContrast ? Ink : new Color("c9c6b6");
    public static Label Note(Node parent, string text)
    {
        var label = Text(text); label.AddThemeColorOverride("font_color", Muted); parent.AddChild(label); return label;
    }
    public static Label Heading(Node parent, string text)
    {
        var label = Text(text); label.AddThemeColorOverride("font_color", Bronze.Lightened(.25f)); parent.AddChild(label); return label;
    }
    /// <summary>Bordered card with a vertical column for its content.</summary>
    public static VBoxContainer Card(Node parent, string? name = null)
    {
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        if (name != null) card.Name = name;
        var style = Box(HighContrast ? new Color("0f1a18") : new Color("1d2f2b"), HighContrast ? Bronze : new Color("4f6158"));
        style.ContentMarginTop = style.ContentMarginBottom = 10; style.ContentMarginLeft = style.ContentMarginRight = 12;
        card.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(card);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        card.AddChild(column);
        return column;
    }
    /// <summary>
    /// Segmented tab row. The selected tab is marked by words ("· open") and a thick
    /// bronze outline and [brackets], not colour alone, and does not dispatch again.
    /// </summary>
    public static HBoxContainer Tabs(Node parent, IReadOnlyList<string> labels, int selected, Action<int> select, string namePrefix = "tab_")
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        for (int i = 0; i < labels.Count; i++)
        {
            int index = i;
            var tab = Action(row, labels[i], () => { if (index != selected) select(index); });
            tab.Name = namePrefix + labels[i].ToLowerInvariant();
            tab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            if (i == selected)
            {
                var face = Box(new Color("365347"), new Color("ffdf8c"), 4);
                tab.AddThemeStyleboxOverride("normal", face); tab.AddThemeStyleboxOverride("hover", face);
                tab.TooltipText = labels[i] + " (open)";
                tab.Text = "[" + labels[i] + "]";
            }
        }
        return row;
    }

    /// <summary>Distinct recessed, selected and momentary-pressed tile faces.</summary>
    public static StyleBoxFlat InventoryTile(bool selected, bool pressed = false)
    {
        var border = selected ? (HighContrast ? Ink : Bronze) : (HighContrast ? new Color("9baca0") : new Color("4b6157"));
        var face = pressed ? new Color("0c1715") :
            selected ? new Color("33463a") : new Color("12211f");
        var style = Box(face, border, selected ? 3 : 1);
        style.ContentMarginLeft = style.ContentMarginRight = 5;
        style.ContentMarginTop = style.ContentMarginBottom = 5;
        return style;
    }

    /// <summary>Reusable fixed-height RPG region with a flexible interior.</summary>
    public static VBoxContainer InventoryPanel(Node parent, string name, string heading)
    {
        var panel = new PanelContainer { Name = name, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        var style = Box(HighContrast ? new Color("0d1917") : new Color("20332e"), Border, HighContrast ? 2 : 1);
        style.ContentMarginLeft = style.ContentMarginRight = 10;
        style.ContentMarginTop = style.ContentMarginBottom = 8;
        panel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(panel);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        panel.AddChild(column);
        var title = Heading(column, heading);
        // Screen theme supplies accessible text scale; do not hard-code a smaller font.
        return column;
    }

    public static Rect2 Overlay(Rect2 safe, float width) => new(
        safe.Position + new Vector2((safe.Size.X - Math.Min(width, safe.Size.X - 32)) / 2, 16),
        new Vector2(Math.Min(width, safe.Size.X - 32), safe.Size.Y - 112));
}
