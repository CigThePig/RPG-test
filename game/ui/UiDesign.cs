using Godot;
namespace Mosslight;

/// <summary>Semantic colour family for chips, accents and status.</summary>
public enum Tone { Neutral, Gold, Moss, Ember, Arcane }

/// <summary>
/// Mosslight visual identity: deep moss-black glass surfaces, lantern gold accents,
/// Cinzel display titles and Fira Sans body text. Every screen builds from these
/// tokens and theme variations instead of per-control colour overrides.
/// Logical pixels are not Android density-independent pixels.
/// </summary>
public static class UiDesign
{
    public const int Gap = 12, Target = 56;

    /// <summary>High-contrast preference: opaque surfaces, ivory borders and brighter secondary text. Read when controls are built.</summary>
    public static bool HighContrast { get; set; }

    // ---- Palette ----
    public static Color Night => new("050908");
    public static Color Surface => HighContrast ? new("040706") : new("0f1715");
    public static Color SurfaceHigh => HighContrast ? new("070c0b") : new("131d1a");
    public static Color Raised => HighContrast ? new("0b1210") : new("1a2723");
    public static Color RaisedHigh => HighContrast ? new("121c19") : new("23342e");
    public static Color Inset => HighContrast ? new("000000") : new("090f0e");
    public static Color Line => HighContrast ? new("8f9a92") : new("243530");
    public static Color LineStrong => HighContrast ? new("d9d3bd") : new("374c44");
    public static Color Ink => new("f3edd9");
    public static Color Muted => HighContrast ? Ink : new("c2bda8");
    public static Color Faint => HighContrast ? new("e1dbc5") : new("a6a18c");
    public static Color Gold => new("e3b965");
    public static Color GoldBright => new("f6dc9c");
    public static Color GoldDeep => HighContrast ? new("e3b965") : new("8d6c35");
    public static Color GoldInk => new("1d1507");
    public static Color Moss => new("93d08f");
    public static Color MossDeep => new("2f5537");
    public static Color Ember => new("ec806a");
    public static Color EmberDeep => new("6e271d");
    public static Color Arcane => new("7fb6f0");
    public static Color ArcaneDeep => new("244572");
    public static Color Health => new("b33a2b");
    public static Color Mana => new("2c62ad");
    public static Color Experience => new("c39538");

    /// <summary>Legacy token names kept for existing callers.</summary>
    public static Color Bronze => Gold;
    public static Color Border => HighContrast ? Ink : GoldDeep;

    /// <summary>Every foreground/background pair the theme uses for text (WCAG 4.5:1).</summary>
    public static IEnumerable<(Color Fg, Color Bg, string Name)> ContrastPairs()
    {
        yield return (Ink, Surface, "ink on surface");
        yield return (Ink, Raised, "ink on raised");
        yield return (Ink, RaisedHigh, "ink on hover");
        yield return (Muted, Raised, "secondary on raised");
        yield return (Muted, Surface, "secondary on surface");
        yield return (Faint, Raised, "caption on raised");
        yield return (Gold, Raised, "heading on raised");
        yield return (GoldBright, Surface, "title on surface");
        yield return (GoldInk, new Color("c9a052"), "primary button text");
        yield return (Faint, new Color("121a18"), "disabled text");
        yield return (new Color("f7d0c4"), new Color("3b1c17"), "danger button text");
        yield return (Ink, Health, "HP bar text");
        yield return (Ink, Mana, "MP bar text");
        yield return (Faint, new Color("233630"), "caption on selected row");
        yield return (Moss, Inset, "positive delta");
        yield return (Ember, Inset, "negative delta");
        yield return (GoldBright, new Color("1c2a25"), "dialogue speaker");
    }

    // ---- Typography ----
    static Font? display, body, bodyMedium, bodySemi;
    static Font LoadFont(string file)
    {
        var font = ResourceLoader.Exists("res://game/ui/fonts/" + file) ? GD.Load<FontFile>("res://game/ui/fonts/" + file) : null;
        if (font == null) return ThemeDB.FallbackFont;
        if (font.Fallbacks.Count == 0) font.Fallbacks = new Godot.Collections.Array<Font> { ThemeDB.FallbackFont };
        return font;
    }
    /// <summary>Cinzel display face for titles and section headings (renders as engraved small capitals).</summary>
    public static Font Display => display ??= new FontVariation
    {
        BaseFont = LoadFont("Cinzel-Variable.ttf"), SpacingGlyph = 1,
        VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("wght"), 700 } }
    };
    public static Font Body => body ??= LoadFont("FiraSans-Regular.ttf");
    public static Font BodyMedium => bodyMedium ??= LoadFont("FiraSans-Medium.ttf");
    public static Font BodySemi => bodySemi ??= LoadFont("FiraSans-SemiBold.ttf");

    // ---- Surfaces ----
    public static SurfaceBox Surfaced(Color fill, Color border, int radius = 12, float sheen = 0, int width = 1) =>
        new() { Fill = fill, Border = border, Radius = radius, Sheen = sheen, BorderWidth = width };

    public static Color ToneColor(Tone tone) => tone switch
    {
        Tone.Gold => Gold, Tone.Moss => Moss, Tone.Ember => Ember, Tone.Arcane => Arcane, _ => Muted
    };
    static Color ToneDeep(Tone tone) => tone switch
    {
        Tone.Gold => new Color("3a2c12"), Tone.Moss => new Color("17301d"), Tone.Ember => new Color("3a1712"), Tone.Arcane => new Color("14263f"), _ => new Color("1c2824")
    };

    static readonly Dictionary<(int, bool), Theme> themes = new();

    public static Theme Theme(float scale)
    {
        var key = ((int)Math.Round(scale * 100), HighContrast);
        if (themes.TryGetValue(key, out var cached)) return cached;
        int F(float size) => (int)Math.Round(size * scale);
        var theme = new Theme { DefaultFont = Body, DefaultFontSize = F(19) };
        bool hc = HighContrast;

        // Modal panels: deep glass with a gold hairline and engraved corners.
        var modal = new SurfaceBox { Fill = Surface, Border = hc ? Ink : GoldDeep, BorderWidth = hc ? 2 : 1, Radius = 18,
            Inner = new Color(1, 1, 1, .035f), Shadow = new Color(0, 0, 0, .55f), ShadowSize = 26, ShadowOffset = new Vector2(0, 10),
            Corners = new Color(Gold, hc ? .9f : .55f) }.Margins(22, 18);
        theme.SetStylebox("panel", "PanelContainer", modal);
        // Full-screen screens sit on the scrim, so they skip the large (costly) drop shadow.
        var screen = modal.Copy(); screen.Shadow = Colors.Transparent; screen.ShadowSize = 0;
        theme.SetTypeVariation("ScreenPanel", "PanelContainer"); theme.SetStylebox("panel", "ScreenPanel", screen);
        void PanelVariation(string name, SurfaceBox box) { theme.SetTypeVariation(name, "PanelContainer"); theme.SetStylebox("panel", name, box); }
        PanelVariation("Card", new SurfaceBox { Fill = Raised, Border = hc ? LineStrong : Line, BorderWidth = hc ? 2 : 1, Radius = 14 }.Margins(14, 12));
        PanelVariation("Well", new SurfaceBox { Fill = Inset, Border = hc ? LineStrong : Line, BorderWidth = hc ? 2 : 1, Radius = 14 }.Margins(12, 10));
        PanelVariation("IconWell", new SurfaceBox { Fill = Inset, Border = hc ? LineStrong : Line, BorderWidth = hc ? 2 : 1, Radius = 12 }.Margins(3, 3));
        PanelVariation("Pane", new SurfaceBox { Fill = SurfaceHigh, Border = hc ? LineStrong : Line, BorderWidth = hc ? 2 : 1, Radius = 16 }.Margins(14, 12));
        PanelVariation("TabTrack", new SurfaceBox { Fill = Inset, Border = hc ? LineStrong : Line, BorderWidth = hc ? 2 : 1, Radius = 14 }.Margins(4, 4));
        PanelVariation("Toast", new SurfaceBox { Fill = new Color("0b1311", hc ? 1 : .95f), Border = hc ? Ink : GoldDeep, BorderWidth = hc ? 2 : 1, Radius = 14,
            Accent = Gold, AccentEdge = AccentEdge.Left, AccentWidth = 3, Shadow = new Color(0, 0, 0, .45f), ShadowSize = 14, ShadowOffset = new Vector2(0, 4) }.Margins(20, 10));
        PanelVariation("Plate", new SurfaceBox { Fill = new Color("0a1110", hc ? 1 : .72f), Border = hc ? Ink : new Color(1, 1, 1, .09f), BorderWidth = hc ? 2 : 1, Radius = 14 }.Margins(12, 8));
        PanelVariation("Speaker", new SurfaceBox { Fill = new Color("1c2a25"), Border = Gold, Radius = 10, Sheen = .06f, Shadow = new Color(0, 0, 0, .4f), ShadowSize = 6 }.Margins(16, 4));
        PanelVariation("Quote", new SurfaceBox { Fill = Raised, Radius = 10, Accent = GoldDeep, AccentEdge = AccentEdge.Left, AccentWidth = 3 }.Margins(18, 10));
        foreach (Tone tone in Enum.GetValues<Tone>())
        {
            var c = ToneColor(tone);
            PanelVariation("Chip" + tone, new SurfaceBox { Fill = hc ? Colors.Black : ToneDeep(tone), Border = new Color(c, hc ? 1 : .55f), Radius = 99 }.Margins(10, 2));
            string label = "Chip" + tone + "Label";
            theme.SetTypeVariation(label, "Label");
            theme.SetFont("font", label, BodySemi); theme.SetFontSize("font_size", label, F(13));
            theme.SetColor("font_color", label, tone == Tone.Neutral ? Muted : c);
        }

        // Labels
        theme.SetColor("font_color", "Label", Ink);
        void LabelVariation(string name, Font font, int size, Color color)
        {
            theme.SetTypeVariation(name, "Label");
            theme.SetFont("font", name, font); theme.SetFontSize("font_size", name, size); theme.SetColor("font_color", name, color);
        }
        LabelVariation("TitleLabel", Display, F(26), GoldBright);
        LabelVariation("HeadingLabel", Display, F(15), Gold);
        LabelVariation("NoteLabel", Body, F(17), Muted);
        LabelVariation("CaptionLabel", BodyMedium, F(13), Faint);
        LabelVariation("RowTitle", BodyMedium, F(18), Ink);
        LabelVariation("RowSubtitle", Body, F(15), Muted);
        LabelVariation("ValueLabel", BodySemi, F(18), Ink);
        LabelVariation("BigValue", BodySemi, F(28), Ink);
        LabelVariation("DisplayValue", Display, F(30), GoldBright);
        LabelVariation("PositiveLabel", BodySemi, F(16), Moss);
        LabelVariation("NegativeLabel", BodySemi, F(16), Ember);
        LabelVariation("DialogueLine", Body, F(23), Ink);
        LabelVariation("DialogueSpeaker", Display, F(18), GoldBright);
        theme.SetConstant("line_spacing", "DialogueLine", F(4));

        // Buttons
        var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = GoldBright, AntiAliasing = true,
            ExpandMarginLeft = 3, ExpandMarginRight = 3, ExpandMarginTop = 3, ExpandMarginBottom = 3 };
        focus.SetBorderWidthAll(2); focus.SetCornerRadiusAll(15);
        void ButtonStyles(string type, SurfaceBox normal, SurfaceBox hover, SurfaceBox pressed, SurfaceBox disabled, Color font, Color hoverFont, Font face, int size)
        {
            if (type != "Button") theme.SetTypeVariation(type, "Button");
            theme.SetStylebox("normal", type, normal); theme.SetStylebox("hover", type, hover);
            theme.SetStylebox("pressed", type, pressed); theme.SetStylebox("hover_pressed", type, pressed);
            theme.SetStylebox("disabled", type, disabled); theme.SetStylebox("focus", type, focus);
            theme.SetColor("font_color", type, font); theme.SetColor("font_hover_color", type, hoverFont);
            theme.SetColor("font_pressed_color", type, hoverFont); theme.SetColor("font_hover_pressed_color", type, hoverFont);
            theme.SetColor("font_focus_color", type, hoverFont); theme.SetColor("font_disabled_color", type, Faint);
            theme.SetFont("font", type, face); theme.SetFontSize("font_size", type, size);
        }
        SurfaceBox Face(Color fill, Color border, int radius = 12, float sheen = .045f, int width = 1, float inner = 0, Color? accent = null, AccentEdge edge = AccentEdge.None, Color? glow = null)
        {
            var box = new SurfaceBox { Fill = fill, Border = border, Radius = radius, Sheen = sheen, BorderWidth = hc ? Math.Max(2, width) : width,
                Inner = new Color(1, 1, 1, inner), Accent = accent ?? Colors.Transparent, AccentEdge = edge, AccentWidth = 4 };
            if (glow is Color g) { box.Shadow = g; box.ShadowSize = 8; }
            return box.Margins(16, 10);
        }
        var disabledFace = Face(hc ? Colors.Black : new Color("121a18"), hc ? new Color("6b746d") : Line, sheen: 0, inner: 0);
        ButtonStyles("Button",
            Face(Raised, hc ? LineStrong : LineStrong), Face(RaisedHigh, hc ? Ink : GoldDeep),
            Face(hc ? Colors.Black : new Color("101916"), Gold, sheen: 0), disabledFace, Ink, GoldBright, BodyMedium, F(18));
        var gold = new Color("c9a052");
        ButtonStyles("PrimaryButton",
            Face(gold, GoldBright, sheen: .16f, inner: .18f, glow: new Color(Gold, .22f)), Face(new Color("d9b467"), new Color("fff0c4"), sheen: .2f, inner: .22f, glow: new Color(Gold, .34f)),
            Face(new Color("a8833e"), GoldBright, sheen: 0, inner: .1f), disabledFace, GoldInk, GoldInk, BodySemi, F(18));
        ButtonStyles("DangerButton",
            Face(new Color("3b1c17"), new Color("8e3d2d")), Face(new Color("4c241d"), Ember),
            Face(new Color("2a1310"), Ember, sheen: 0), disabledFace, new Color("f7d0c4"), new Color("ffe2d9"), BodyMedium, F(18));
        var clear = new Color(0, 0, 0, 0);
        ButtonStyles("QuietButton",
            Face(clear, hc ? LineStrong : clear, sheen: 0, inner: 0), Face(new Color(1, 1, 1, .05f), hc ? Ink : Line, sheen: 0, inner: 0),
            Face(new Color(0, 0, 0, .2f), GoldDeep, sheen: 0, inner: 0), Face(clear, clear, sheen: 0, inner: 0), Muted, Ink, BodyMedium, F(17));
        ButtonStyles("TabButton",
            Face(clear, hc ? Line : clear, radius: 10, sheen: 0, inner: 0), Face(new Color(1, 1, 1, .05f), hc ? Ink : clear, radius: 10, sheen: 0, inner: 0),
            Face(new Color(0, 0, 0, .25f), clear, radius: 10, sheen: 0, inner: 0), Face(clear, clear, radius: 10, sheen: 0, inner: 0), Muted, Ink, BodyMedium, F(17));
        var selectedTab = Face(RaisedHigh, hc ? Ink : GoldDeep, radius: 10, sheen: .05f, accent: Gold, edge: AccentEdge.Bottom);
        selectedTab.AccentWidth = 3;
        ButtonStyles("TabSelected", selectedTab, selectedTab, selectedTab, selectedTab, GoldBright, GoldBright, BodySemi, F(17));
        ButtonStyles("RowButton",
            Face(Raised, hc ? LineStrong : Line, sheen: .025f, inner: .03f), Face(RaisedHigh, hc ? Ink : GoldDeep, sheen: .03f, inner: .04f),
            Face(hc ? Colors.Black : new Color("111a17"), Gold, sheen: 0, inner: 0), disabledFace, Ink, Ink, BodyMedium, F(18));
        var rowSelected = Face(hc ? new Color("1a2622") : new Color("233630"), Gold, sheen: .035f, inner: .05f, accent: Gold, edge: AccentEdge.Left, glow: new Color(Gold, hc ? 0 : .16f));
        ButtonStyles("RowSelected", rowSelected, rowSelected, rowSelected, rowSelected, Ink, Ink, BodyMedium, F(18));
        ButtonStyles("SlotButton",
            Face(Inset, hc ? LineStrong : LineStrong, radius: 12, sheen: 0, inner: .03f), Face(new Color("111b18"), hc ? Ink : GoldDeep, radius: 12, sheen: 0, inner: .04f),
            Face(Colors.Black, Gold, radius: 12, sheen: 0, inner: 0), disabledFace, Ink, Ink, BodyMedium, F(14));
        var slotSelected = Face(hc ? new Color("1a2622") : new Color("1b2a24"), Gold, radius: 12, sheen: .03f, width: 2, inner: .06f, glow: new Color(Gold, hc ? 0 : .3f));
        ButtonStyles("SlotSelected", slotSelected, slotSelected, slotSelected, slotSelected, Ink, Ink, BodyMedium, F(14));
        SurfaceBox Reply(Color fill, Color border, Color bullet, bool accent)
        {
            var box = Face(fill, border, sheen: .03f, inner: .04f, accent: accent ? Gold : null, edge: accent ? AccentEdge.Left : AccentEdge.None);
            box.Bullet = bullet; box.ContentMarginLeft = 42; box.ContentMarginRight = 18; box.ContentMarginTop = box.ContentMarginBottom = 12;
            return box;
        }
        ButtonStyles("DialogueChoice",
            Reply(Raised, hc ? LineStrong : Line, GoldDeep.Lightened(.1f), false), Reply(RaisedHigh, Gold, Gold, true),
            Reply(hc ? Colors.Black : new Color("111a17"), GoldBright, GoldBright, true), Reply(new Color("121a18"), Line, Line, false), Ink, GoldBright, BodyMedium, F(19));
        var empty = new StyleBoxEmpty();
        theme.SetTypeVariation("BareButton", "Button");
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }) theme.SetStylebox(state, "BareButton", empty);
        theme.SetStylebox("focus", "BareButton", focus);

        // Inputs, scrollbars and popups
        var well = new SurfaceBox { Fill = Inset, Border = LineStrong, Radius = 10 }.Margins(12, 8);
        theme.SetStylebox("normal", "LineEdit", well); theme.SetStylebox("focus", "LineEdit", focus);
        theme.SetColor("font_color", "LineEdit", Ink);
        var track = new SurfaceBox { Fill = new Color(1, 1, 1, hc ? .12f : .035f), Radius = 3, Inset = new Vector4(12, 2, 12, 2) };
        track.ContentMarginLeft = track.ContentMarginRight = 14; track.ContentMarginTop = track.ContentMarginBottom = 2;
        theme.SetStylebox("scroll", "VScrollBar", track); theme.SetStylebox("scroll_focus", "VScrollBar", track);
        foreach (var (state, color) in new[] { ("grabber", hc ? Ink : LineStrong.Lightened(.15f)), ("grabber_highlight", GoldDeep.Lightened(.2f)), ("grabber_pressed", Gold) })
            theme.SetStylebox(state, "VScrollBar", new SurfaceBox { Fill = color, Radius = 4, Inset = new Vector4(10, 0, 10, 0) });
        var tooltip = new SurfaceBox { Fill = new Color("0b1311"), Border = GoldDeep, Radius = 8 }.Margins(10, 6);
        theme.SetStylebox("panel", "TooltipPanel", tooltip);
        theme.SetColor("font_color", "TooltipLabel", Ink); theme.SetFontSize("font_size", "TooltipLabel", F(15));
        theme.SetStylebox("panel", "PopupMenu", new SurfaceBox { Fill = Surface, Border = GoldDeep, Radius = 10 }.Margins(8, 8));
        theme.SetStylebox("background", "ProgressBar", new SurfaceBox { Fill = Inset, Border = Line, Radius = 8 });
        theme.SetStylebox("fill", "ProgressBar", new SurfaceBox { Fill = Experience, Radius = 8, Sheen = .18f });
        theme.SetStylebox("slider", "HSlider", new SurfaceBox { Fill = Inset, Border = LineStrong, Radius = 6 }.Margins(0, 4));
        theme.SetStylebox("grabber_area", "HSlider", new SurfaceBox { Fill = GoldDeep, Radius = 6 }.Margins(0, 4));
        theme.SetStylebox("grabber_area_highlight", "HSlider", new SurfaceBox { Fill = Gold, Radius = 6 }.Margins(0, 4));

        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 10);
        theme.SetConstant("h_separation", "GridContainer", 10);
        theme.SetConstant("v_separation", "GridContainer", 10);
        themes[key] = theme;
        return theme;
    }

    /// <summary>Floating notice surface (also used by the HUD tracker in high contrast).</summary>
    public static SurfaceBox Notification() => (SurfaceBox)Theme(1).GetStylebox("panel", "Toast");

    // ---- Components ----
    public static Label Text(string text) => new()
    {
        Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
    };
    static Label Styled(Node parent, string text, string variation)
    {
        var label = Text(text); label.ThemeTypeVariation = variation; parent.AddChild(label); return label;
    }
    /// <summary>Secondary text (reasons, hints). Meaning is in words, never colour alone.</summary>
    public static Label Note(Node parent, string text) => Styled(parent, text, "NoteLabel");
    /// <summary>Engraved section heading in the display face.</summary>
    public static Label Heading(Node parent, string text) => Styled(parent, text, "HeadingLabel");
    public static Label Title(Node parent, string text) => Styled(parent, text, "TitleLabel");
    public static Label Caption(Node parent, string text) => Styled(parent, text.ToUpperInvariant(), "CaptionLabel");
    /// <summary>Single-line figure (never wraps), vertically centred in its row.</summary>
    public static Label Value(Node parent, string text, string variation = "ValueLabel")
    {
        var label = new Label { Text = text, ThemeTypeVariation = variation, MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        parent.AddChild(label); return label;
    }

    public static Button Action(Node parent, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, Target),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Pass };
        button.Pressed += action; parent.AddChild(button); return button;
    }
    public static T Primary<T>(T button) where T : Button { button.ThemeTypeVariation = "PrimaryButton"; return button; }
    public static T Danger<T>(T button) where T : Button { button.ThemeTypeVariation = "DangerButton"; return button; }
    public static T Quiet<T>(T button) where T : Button { button.ThemeTypeVariation = "QuietButton"; return button; }

    /// <summary>Marks a tab, row or slot as the current selection: a raised face, gold edge and accent bar, plus a stable flag.</summary>
    public static void SetSelected(Button button, bool selected, string normal = "RowButton", string chosen = "RowSelected")
    {
        button.ThemeTypeVariation = selected ? chosen : normal;
        button.SetMeta("selected", selected);
    }
    public static bool IsSelected(Control control) => control.HasMeta("selected") && control.GetMeta("selected").AsBool();

    /// <summary>Raised card with a vertical column for its content.</summary>
    public static VBoxContainer Card(Node parent, string? name = null, string variation = "Card")
    {
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass, ThemeTypeVariation = variation, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (name != null) card.Name = name;
        parent.AddChild(card);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        card.AddChild(column);
        return column;
    }
    public static VBoxContainer Well(Node parent, string? name = null) => Card(parent, name, "Well");

    /// <summary>Pill-shaped status chip. The words carry the meaning; tone only reinforces it.</summary>
    public static PanelContainer Chip(Node parent, string text, Tone tone = Tone.Neutral, string? glyph = null)
    {
        var chip = new PanelContainer { ThemeTypeVariation = "Chip" + tone, MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        parent.AddChild(chip);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 5);
        chip.AddChild(row);
        if (glyph != null) row.AddChild(new Glyph(glyph, ToneColor(tone), 12));
        row.AddChild(new Label { Text = text, ThemeTypeVariation = "Chip" + tone + "Label", MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center });
        return chip;
    }

    public static Divider Divider(Node parent, bool ornament = true)
    {
        var divider = new Divider { Ornament = ornament }; parent.AddChild(divider); return divider;
    }

    /// <summary>Framed icon showcase with a soft halo.</summary>
    public static PanelContainer IconWell(Node parent, Texture2D? texture, float size, bool halo = true, string? name = null)
    {
        var well = new PanelContainer { ThemeTypeVariation = "IconWell", MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(size, size), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        if (name != null) well.Name = name;
        parent.AddChild(well);
        var stack = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        well.AddChild(stack);
        if (halo) { var glow = new Halo(); stack.AddChild(glow); glow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); }
        if (texture != null)
        {
            var icon = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            stack.AddChild(icon); icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = icon.OffsetTop = size * .08f; icon.OffsetRight = icon.OffsetBottom = -size * .08f;
        }
        return well;
    }

    /// <summary>Label · value line used by stat sheets and inspectors.</summary>
    public static HBoxContainer Stat(Node parent, string glyph, string label, string value, string? delta = null, bool positive = true, Tone tone = Tone.Gold)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);
        row.AddChild(new Glyph(glyph, ToneColor(tone), 18));
        var name = Text(label); name.ThemeTypeVariation = "NoteLabel"; row.AddChild(name);
        if (delta != null)
        {
            var change = new Label { Text = delta, ThemeTypeVariation = positive ? "PositiveLabel" : "NegativeLabel", MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
            row.AddChild(change);
        }
        row.AddChild(new Label { Text = value, ThemeTypeVariation = "ValueLabel", MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    public static void IgnoreMouse(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
            IgnoreMouse(child);
        }
    }

    /// <summary>
    /// Segmented control. The selected segment is raised with a gold underline and
    /// flagged via <see cref="IsSelected"/>; selecting it again does not dispatch.
    /// Returns the segment row (its parent is the recessed track).
    /// </summary>
    public static HBoxContainer Tabs(Node parent, IReadOnlyList<string> labels, int selected, Action<int> select, string namePrefix = "tab_")
    {
        var track = new PanelContainer { ThemeTypeVariation = "TabTrack", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        parent.AddChild(track);
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 4);
        track.AddChild(row);
        for (int i = 0; i < labels.Count; i++)
        {
            int index = i;
            // Pages such as Journal switch in place and update the selection flag.
            // The constructor's selected index becomes stale after that first switch.
            var tab = Action(row, labels[i], () =>
            {
                if (row.GetChild<Control>(index) is Button current && !IsSelected(current)) select(index);
            });
            tab.Name = namePrefix + labels[i].ToLowerInvariant();
            tab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            tab.CustomMinimumSize = new Vector2(0, Target - 8);
            SetSelected(tab, i == selected, "TabButton", "TabSelected");
            if (i == selected) tab.TooltipText = labels[i] + " (open)";
        }
        return row;
    }

    /// <summary>Recessed, hovered and selected inventory faces (kept for callers that style buttons directly).</summary>
    public static StyleBox InventoryTile(bool selected, bool pressed = false) =>
        Theme(1).GetStylebox(pressed ? "pressed" : "normal", selected ? "SlotSelected" : "SlotButton");

    /// <summary>Named region with an engraved heading and a flexible interior.</summary>
    public static VBoxContainer InventoryPanel(Node parent, string name, string heading, string? glyph = null)
    {
        var panel = new PanelContainer { Name = name, ThemeTypeVariation = "Pane", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        parent.AddChild(panel);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);
        var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 8);
        column.AddChild(header);
        if (glyph != null) header.AddChild(new Glyph(glyph, Gold, 16));
        Heading(header, heading);
        return column;
    }

    public static Rect2 Overlay(Rect2 safe, float width) => new(
        safe.Position + new Vector2((safe.Size.X - Math.Min(width, safe.Size.X - 32)) / 2, 16),
        new Vector2(Math.Min(width, safe.Size.X - 32), safe.Size.Y - 112));
}
