using Godot;
namespace Mosslight;

/// <summary>Fixed action identities and responsive geometry. Commands remain in Foundation.</summary>
public partial class MobileCombatControls : Control
{
    // Version 3 (PR #7 redesign): round medallion actions in an arc above a large
    // Attack, pill utilities. Android smoke consumes this version's diagnostics.
    public const int LayoutVersion = 3;
    public Dictionary<string, Button> Actions { get; } = new();
    public Rect2 SafeRegion { get; private set; }
    public MobileCombatControls() { MouseFilter = MouseFilterEnum.Ignore; }
    public void Register(string id, Button button)
    {
        Actions.Add(id, button);
        button.Name = "action_" + id;
        ActionFace.Attach(button, id, id is "inventory" or "menu" ? ActionFace.Style.Pill : ActionFace.Style.Medallion);
    }
    // Android reports physical display pixels. The viewport's screen transform includes
    // canvas_items scaling and letterboxing; camera transforms do not belong here.
    public static Rect2 ToLogicalSafeArea(Rect2 viewport, Rect2 physicalSafe, Transform2D viewportToScreen)
    {
        if (!physicalSafe.HasArea()) return viewport;
        var inverse = viewportToScreen.AffineInverse();
        var a = inverse * physicalSafe.Position;
        var b = inverse * physicalSafe.End;
        var clipped = viewport.Intersection(new Rect2(a, b-a));
        return clipped.HasArea() ? clipped : viewport;
    }
    public static Dictionary<string, Rect2> Geometry(Rect2 safe)
    {
        safe = DisplayLayout.Gameplay(safe);
        // Compact fallback scales the complete arrangement, including touch gaps.
        float s = Math.Min(1, Math.Min(safe.Size.X / 900, safe.Size.Y / 560));
        var result = new Dictionary<string, Rect2>();
        // Medallion: circle of diameter d centred at the offset, plus a caption plate below.
        void Medal(string id, float right, float bottom, float d, float width = 0)
        {
            width = Math.Max(width, d);
            var centre = safe.End - new Vector2(right, bottom) * s;
            result[id] = new Rect2(centre - new Vector2(width / 2, d / 2) * s, new Vector2(width, d * ActionFace.CaptionAllowance) * s);
        }
        // Primary right-thumb actions: Attack largest, then Dodge and Interact, with
        // spells and the draught in an arch above. The lower band stays free for notices.
        Medal("attack", 96, 118, 120);
        Medal("dodge", 232, 152, 88);
        Medal("interact", 362, 152, 88, 112);
        Medal("ember", 300, 262, 72, 84);
        Medal("mend", 200, 276, 72, 84);
        Medal("potion", 100, 262, 72, 84);
        result["inventory"] = new Rect2(new Vector2(safe.End.X-262*s,safe.Position.Y+18*s), new Vector2(120,56)*s);
        result["menu"] = new Rect2(new Vector2(safe.End.X-132*s,safe.Position.Y+18*s), new Vector2(116,56)*s);
        result["stick"] = new Rect2(new Vector2(safe.Position.X+40*s,safe.End.Y-260*s),new Vector2(200,200)*s);
        return result;
    }
    public void Arrange(Rect2 safe)
    {
        safe = DisplayLayout.Gameplay(safe);
        SafeRegion = safe;
        foreach (var (id, rect) in Geometry(safe))
            if (Actions.TryGetValue(id, out var button))
            {
                button.CustomMinimumSize = Vector2.Zero;
                button.Position = rect.Position; button.Size = rect.Size;
            }
    }
}

/// <summary>
/// Painted face of a combat or utility action: medallion (touch), pill (utilities) or
/// tile (desktop hotbar). The Button keeps its text for identity and tests; the face
/// shows an icon, caption, badge and a radial cooldown sweep with the time remaining.
/// It never changes the Button's rectangle.
/// </summary>
public partial class ActionFace : Control
{
    public enum Style { Medallion, Pill, Tile }
    public const float CaptionAllowance = 1.1f;
    public Style Look { get; private set; }
    public string Glyph { get; set; } = "dot";
    public string Caption { get; set; } = "";
    public string Badge { get; set; } = "";
    public string Key { get; set; } = "";
    public Tone BadgeTone { get; set; } = Tone.Arcane;
    public string Status { get; set; } = "";
    public float Cooldown { get; set; }
    public string CooldownText { get; set; } = "";
    public bool Primary { get; set; }
    public bool Highlight { get; set; }
    Button button = null!;

    public static ActionFace Attach(Button button, string id, Style look)
    {
        var face = new ActionFace { Look = look, button = button, Name = "face", MouseFilter = MouseFilterEnum.Ignore, Primary = id == "attack" };
        face.Glyph = id switch
        {
            "attack" => "sword", "dodge" => "dodge", "ember" => "flame", "mend" => "leaf", "potion" => "flask",
            "interact" => "hand", "inventory" => "bag", "menu" => "scroll", "journal" => "book", "character" => "person", "map" => "map", _ => "dot"
        };
        button.ThemeTypeVariation = "BareButton";
        button.ClipText = true;
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color", "font_outline_color" })
            button.AddThemeColorOverride(name, Colors.Transparent);
        button.AddChild(face);
        face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        button.Draw += face.QueueRedraw;
        return face;
    }
    public static ActionFace? Of(Button button) => button.GetNodeOrNull<ActionFace>("face");

    public void Show(string caption, string badge = "", Tone tone = Tone.Arcane, string status = "", float cooldown = 0, string cooldownText = "", bool highlight = false)
    {
        if (caption == Caption && badge == Badge && tone == BadgeTone && status == Status && Math.Abs(cooldown - Cooldown) < .001f && cooldownText == CooldownText && highlight == Highlight) return;
        Caption = caption; Badge = badge; BadgeTone = tone; Status = status; Cooldown = cooldown; CooldownText = cooldownText; Highlight = highlight;
        QueueRedraw();
    }

    public override void _Draw()
    {
        bool enabled = !button.Disabled;
        bool hover = button.IsHovered() && enabled;
        var rim = Primary ? UiDesign.Gold : Highlight ? UiDesign.GoldBright : new Color("b8c4ab");
        if (!enabled) rim = new Color("6d7569");
        var ink = enabled ? (Primary ? UiDesign.GoldBright : UiDesign.Ink) : new Color("8b8f84");
        switch (Look)
        {
            case Style.Medallion: Medallion(rim, ink, enabled, hover); break;
            case Style.Pill: Pill(rim, ink, hover); break;
            default: Tile(rim, ink, enabled, hover); break;
        }
    }

    void Medallion(Color rim, Color ink, bool enabled, bool hover)
    {
        // Geometry reserves 10% of the diameter below the circle for the caption plate.
        float d = Math.Min(Size.X, Size.Y / CaptionAllowance);
        float r = d / 2;
        var c = new Vector2(Size.X / 2, r);
        // Body edges sit under the anti-aliased rim, so the fills themselves need no feathering.
        DrawCircle(c + new Vector2(0, 3), r + 1, new Color(0, 0, 0, .38f));
        var body = Primary ? new Color("3a2a14", .9f) : new Color("0d1614", .82f);
        if (hover) body = body.Lightened(.08f);
        DrawCircle(c, r - 1, body);
        DrawArc(c + new Vector2(0, -r * .06f), r * .84f, MathF.PI * 1.12f, MathF.PI * 1.88f, 16, new Color(1, 1, 1, .14f), Math.Max(1.5f, r * .05f), true);
        DrawArc(c, r - 1.5f, 0, MathF.Tau, 48, new Color(rim, enabled ? .95f : .55f), Primary ? 3f : 2f, true);
        if (Highlight && enabled) DrawArc(c, r + 3, 0, MathF.Tau, 56, new Color(UiDesign.GoldBright, .45f), 2, true);
        float g = d * (Primary ? .44f : .46f);
        GlyphPainter.Draw(this, Glyph, new Rect2(c - new Vector2(g, g) / 2, new Vector2(g, g)), Cooldown > 0 ? new Color(ink, .45f) : ink,
            Primary ? new Color("6b4b1d") : new Color("23342e"));
        if (Cooldown > 0)
        {
            float start = -MathF.PI / 2, sweep = MathF.Tau * Math.Clamp(Cooldown, 0, .999f);
            // A sliver this thin would be a degenerate polygon; the bright rim edge still shows it.
            if (sweep > MathF.Tau * .97f) DrawCircle(c, r - 2.5f, new Color(0, 0, 0, .58f), true, -1, true);
            else if (sweep > .08f)
            {
                int steps = Math.Max(3, (int)(40 * sweep / MathF.Tau)); var pie = new Vector2[steps + 2]; pie[0] = c;
                for (int i = 0; i <= steps; i++) { float a = start + sweep * i / steps; pie[i + 1] = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (r - 2.5f); }
                DrawColoredPolygon(pie, new Color(0, 0, 0, .58f));
            }
            DrawArc(c, r - 1.5f, start + sweep, start + MathF.Tau, 40, UiDesign.GoldBright, 3, true);
            if (CooldownText != "")
            {
                var font = UiDesign.BodySemi; int size = (int)Math.Max(12, d * .26f);
                var at = new Vector2(0, c.Y + (font.GetAscent(size) - font.GetDescent(size)) / 2);
                DrawStringOutline(font, at, CooldownText, HorizontalAlignment.Center, Size.X, size, 4, new Color(0, 0, 0, .9f));
                DrawString(font, at, CooldownText, HorizontalAlignment.Center, Size.X, size, UiDesign.Ink);
            }
        }
        if (Badge != "")
        {
            var font = UiDesign.BodySemi; int size = 13;
            float w = Math.Max(22, font.GetStringSize(Badge, HorizontalAlignment.Left, -1, size).X + 12);
            var at = c + new Vector2(r * .70f, -r * .74f);
            var rect = new Rect2(at - new Vector2(w / 2, 11), new Vector2(w, 22));
            rect.Position = new Vector2(Math.Min(rect.Position.X, Size.X - w), Math.Max(0, rect.Position.Y));
            var tone = UiDesign.ToneColor(BadgeTone);
            var face = new StyleBoxFlat { BgColor = new Color("0a1110", .95f), BorderColor = new Color(tone, .8f), AntiAliasing = true };
            face.SetBorderWidthAll(1); face.SetCornerRadiusAll(11);
            face.Draw(GetCanvasItem(), rect);
            DrawString(font, new Vector2(rect.Position.X, rect.Position.Y + 16), Badge, HorizontalAlignment.Center, w, size, tone);
        }
        string caption = Status != "" ? Status : Caption;
        if (caption != "")
        {
            var font = UiDesign.BodySemi; int size = Primary ? 15 : 13;
            float w = Math.Min(Size.X, font.GetStringSize(caption, HorizontalAlignment.Left, -1, size).X + 18);
            var rect = new Rect2(new Vector2((Size.X - w) / 2, Math.Min(d - 10, Size.Y - 20)), new Vector2(w, 20));
            var plate = new StyleBoxFlat { BgColor = new Color("070c0b", .92f), BorderColor = new Color(Status != "" ? UiDesign.Ember : rim, .6f), AntiAliasing = true };
            plate.SetBorderWidthAll(1); plate.SetCornerRadiusAll(10);
            plate.Draw(GetCanvasItem(), rect);
            DrawString(font, new Vector2(rect.Position.X, rect.Position.Y + 15), caption, HorizontalAlignment.Center, w, size,
                Status != "" ? UiDesign.Ember : enabled ? UiDesign.Ink : UiDesign.Faint);
        }
    }

    void Pill(Color rim, Color ink, bool hover)
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var face = new StyleBoxFlat { BgColor = hover ? new Color("17231f", .92f) : new Color("0b1311", .84f), BorderColor = new Color(rim, .55f), AntiAliasing = true,
            ShadowColor = new Color(0, 0, 0, .35f), ShadowSize = 6, ShadowOffset = new Vector2(0, 2) };
        face.SetBorderWidthAll(UiDesign.HighContrast ? 2 : 1); face.SetCornerRadiusAll((int)(Size.Y / 2));
        face.Draw(GetCanvasItem(), rect);
        float g = Size.Y * .46f;
        GlyphPainter.Draw(this, Glyph, new Rect2(new Vector2(Size.Y * .34f, (Size.Y - g) / 2), new Vector2(g, g)), UiDesign.Gold, new Color("23342e"));
        var font = UiDesign.BodySemi; int size = 17;
        float x = Size.Y * .34f + g + 8;
        DrawString(font, new Vector2(x, Size.Y / 2 + (font.GetAscent(size) - font.GetDescent(size)) / 2), Caption, HorizontalAlignment.Left, Size.X - x - 10, size, ink);
    }

    void Tile(Color rim, Color ink, bool enabled, bool hover)
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var face = new StyleBoxFlat { BgColor = hover ? new Color("1c2a25", .94f) : new Color("0e1715", .88f), BorderColor = new Color(Primary ? UiDesign.Gold : rim, enabled ? .6f : .35f), AntiAliasing = true,
            ShadowColor = new Color(0, 0, 0, .35f), ShadowSize = 6, ShadowOffset = new Vector2(0, 2) };
        face.SetBorderWidthAll(UiDesign.HighContrast ? 2 : 1); face.SetCornerRadiusAll(12);
        face.Draw(GetCanvasItem(), rect);
        float g = Math.Min(32, Size.Y - 26);
        var icon = new Rect2(new Vector2(10, (Size.Y - g) / 2 + 5), new Vector2(g, g));
        GlyphPainter.Draw(this, Glyph, icon, Cooldown > 0 ? new Color(ink, .45f) : Primary ? UiDesign.GoldBright : ink, new Color("23342e"));
        if (Cooldown > 0)
        {
            var shade = new StyleBoxFlat { BgColor = new Color(0, 0, 0, .5f), AntiAliasing = true };
            shade.SetCornerRadiusAll(12); shade.CornerRadiusTopLeft = shade.CornerRadiusTopRight = Cooldown > .9f ? 12 : 0;
            shade.Draw(GetCanvasItem(), new Rect2(new Vector2(0, Size.Y * (1 - Cooldown)), new Vector2(Size.X, Size.Y * Cooldown)));
        }
        var semi = UiDesign.BodySemi; var medium = UiDesign.BodyMedium;
        if (Key != "")
        {
            float kw = Math.Max(20, semi.GetStringSize(Key, HorizontalAlignment.Left, -1, 11).X + 10);
            var key = new StyleBoxFlat { BgColor = new Color("060a09"), BorderColor = new Color(rim, .5f), AntiAliasing = true };
            key.SetBorderWidthAll(1); key.SetCornerRadiusAll(5);
            key.Draw(GetCanvasItem(), new Rect2(new Vector2(8, 6), new Vector2(kw, 17)));
            DrawString(semi, new Vector2(8, 19), Key, HorizontalAlignment.Center, kw, 11, UiDesign.Muted);
        }
        float x = icon.End.X + 8, room = Size.X - x - 8;
        // Shrink long names (contextual Interact, ability names) to fit instead of clipping.
        int Fit(Font font, string text, int size) { while (size > 11 && font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X > room) size--; return size; }
        DrawString(semi, new Vector2(x, Size.Y / 2 + 2), Caption, HorizontalAlignment.Left, room, Fit(semi, Caption, 16), ink);
        string detail = Status != "" ? Status : CooldownText != "" ? CooldownText + "s" : Badge;
        if (detail != "")
            DrawString(medium, new Vector2(x, Size.Y / 2 + 21), detail, HorizontalAlignment.Left, room, Fit(medium, detail, 14),
                Status != "" ? UiDesign.Ember : Cooldown > 0 ? UiDesign.GoldBright : UiDesign.ToneColor(BadgeTone));
    }
}
