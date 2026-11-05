using Godot;
namespace Mosslight;

/// <summary>Which edge of a <see cref="SurfaceBox"/> carries its accent bar.</summary>
public enum AccentEdge { None, Left, Bottom, Top }

/// <summary>
/// Layered panel face: drop shadow or glow, fill, border, a soft top sheen, an inner
/// hairline and an optional accent bar or corner ornaments. Composed from cached
/// <see cref="StyleBoxFlat"/> layers so it stays anti-aliased and cheap to draw.
/// </summary>
public partial class SurfaceBox : StyleBox
{
    public Color Fill { get; set; } = Colors.Transparent;
    public Color Border { get; set; } = Colors.Transparent;
    public int BorderWidth { get; set; } = 1;
    public int Radius { get; set; } = 12;
    /// <summary>White overlay alpha on the upper half: reads as gentle top lighting.</summary>
    public float Sheen { get; set; }
    /// <summary>Inner hairline one pixel inside the border.</summary>
    public Color Inner { get; set; } = Colors.Transparent;
    public Color Shadow { get; set; } = Colors.Transparent;
    public int ShadowSize { get; set; }
    public Vector2 ShadowOffset { get; set; }
    public Color Accent { get; set; } = Colors.Transparent;
    public AccentEdge AccentEdge { get; set; }
    public int AccentWidth { get; set; } = 4;
    /// <summary>Gold corner brackets drawn just inside the border of ornate panels.</summary>
    public Color Corners { get; set; } = Colors.Transparent;
    /// <summary>Optional diamond bullet drawn in the left content margin (dialogue replies).</summary>
    public Color Bullet { get; set; } = Colors.Transparent;
    /// <summary>Shrinks the drawn face inside the control rectangle (left, top, right, bottom).</summary>
    public Vector4 Inset { get; set; }

    StyleBoxFlat? face, sheen, inner, accent;

    public SurfaceBox Margins(float horizontal, float vertical)
    {
        ContentMarginLeft = ContentMarginRight = horizontal;
        ContentMarginTop = ContentMarginBottom = vertical;
        return this;
    }

    /// <summary>Member-wise copy. Resource.Duplicate() would not carry these C# properties.</summary>
    public SurfaceBox Copy() => new()
    {
        Fill = Fill, Border = Border, BorderWidth = BorderWidth, Radius = Radius, Sheen = Sheen, Inner = Inner,
        Shadow = Shadow, ShadowSize = ShadowSize, ShadowOffset = ShadowOffset, Accent = Accent, AccentEdge = AccentEdge,
        AccentWidth = AccentWidth, Corners = Corners, Bullet = Bullet, Inset = Inset,
        ContentMarginLeft = ContentMarginLeft, ContentMarginTop = ContentMarginTop, ContentMarginRight = ContentMarginRight, ContentMarginBottom = ContentMarginBottom
    };

    static StyleBoxFlat Flat(Color color, int radius) => new()
    {
        BgColor = color, AntiAliasing = true,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };

    void Build()
    {
        face = Flat(Fill, Radius);
        face.BorderColor = Border;
        face.BorderWidthLeft = face.BorderWidthTop = face.BorderWidthRight = face.BorderWidthBottom = Border.A > 0 ? BorderWidth : 0;
        face.ShadowColor = Shadow; face.ShadowSize = ShadowSize; face.ShadowOffset = ShadowOffset;
        if (Sheen > 0)
        {
            int r = Math.Max(0, Radius - BorderWidth);
            sheen = Flat(new Color(1, 1, 1, Sheen), r);
            sheen.CornerRadiusBottomLeft = sheen.CornerRadiusBottomRight = 0;
        }
        if (Inner.A > 0)
        {
            inner = Flat(Colors.Transparent, Math.Max(0, Radius - BorderWidth - 1));
            inner.DrawCenter = false; inner.BorderColor = Inner;
            inner.BorderWidthLeft = inner.BorderWidthTop = inner.BorderWidthRight = inner.BorderWidthBottom = 1;
        }
        if (Accent.A > 0 && AccentEdge != AccentEdge.None) accent = Flat(Accent, Math.Max(1, AccentWidth / 2));
    }

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        if (face == null) Build();
        rect = new Rect2(rect.Position + new Vector2(Inset.X, Inset.Y), rect.Size - new Vector2(Inset.X + Inset.Z, Inset.Y + Inset.W));
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
        face!.Draw(canvasItem, rect);
        float b = Border.A > 0 ? BorderWidth : 0;
        if (sheen != null)
        {
            var top = new Rect2(rect.Position + new Vector2(b, b), new Vector2(rect.Size.X - 2 * b, (rect.Size.Y - 2 * b) * .48f));
            if (top.Size.X > 0 && top.Size.Y > 0) sheen.Draw(canvasItem, top);
        }
        if (inner != null)
        {
            var inside = rect.Grow(-b);
            if (inside.Size.X > 2 && inside.Size.Y > 2) inner.Draw(canvasItem, inside);
        }
        if (accent != null)
        {
            float w = AccentWidth, pad = Math.Max(6, Radius * .6f);
            var bar = AccentEdge switch
            {
                AccentEdge.Left => new Rect2(rect.Position + new Vector2(b + 3, pad), new Vector2(w, rect.Size.Y - 2 * pad)),
                AccentEdge.Top => new Rect2(rect.Position + new Vector2(pad, b + 3), new Vector2(rect.Size.X - 2 * pad, w)),
                _ => new Rect2(rect.Position + new Vector2(pad, rect.Size.Y - b - 3 - w), new Vector2(rect.Size.X - 2 * pad, w))
            };
            if (bar.Size.X > 0 && bar.Size.Y > 0) accent.Draw(canvasItem, bar);
        }
        if (Bullet.A > 0)
        {
            var d = new Vector2(rect.Position.X + 20, rect.Position.Y + rect.Size.Y / 2);
            RenderingServer.CanvasItemAddPolygon(canvasItem, new[] { d + new Vector2(0, -6), d + new Vector2(6, 0), d + new Vector2(0, 6), d + new Vector2(-6, 0) }, new[] { Bullet });
        }
        if (Corners.A > 0 && rect.Size.X > 80 && rect.Size.Y > 80)
        {
            float o = b + 7, l = 18;
            var rs = RenderingServer.Singleton;
            foreach (var (corner, dx, dy) in new[] { (rect.Position, 1f, 1f), (new Vector2(rect.End.X, rect.Position.Y), -1f, 1f),
                (new Vector2(rect.Position.X, rect.End.Y), 1f, -1f), (rect.End, -1f, -1f) })
            {
                var p = corner + new Vector2(o * dx, o * dy);
                RenderingServer.CanvasItemAddPolyline(canvasItem, new[] { p + new Vector2(0, l * dy), p, p + new Vector2(l * dx, 0) }, new[] { Corners }, 1.5f, true);
                var d = p + new Vector2(4 * dx, 4 * dy);
                RenderingServer.CanvasItemAddPolygon(canvasItem, new[] { d + new Vector2(0, -2.5f), d + new Vector2(2.5f, 0), d + new Vector2(0, 2.5f), d + new Vector2(-2.5f, 0) }, new[] { Corners });
            }
        }
    }
}

/// <summary>Original vector icons, drawn at any size from unit coordinates. No external art.</summary>
public static class GlyphPainter
{
    public static void Draw(CanvasItem c, string kind, Rect2 area, Color color, Color? detail = null)
    {
        float s = Math.Min(area.Size.X, area.Size.Y);
        if (s <= 1) return;
        var o = area.Position + (area.Size - new Vector2(s, s)) / 2;
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        Vector2[] Ps(params float[] xy) { var r = new Vector2[xy.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = P(xy[i * 2], xy[i * 2 + 1]); return r; }
        float w = Math.Max(1.5f, s * .095f);
        var d = detail ?? color.Darkened(.45f);
        void Line(float width, params float[] xy) => c.DrawPolyline(Ps(xy), color, width, true);
        void Fill(params float[] xy) { var pts = Ps(xy); c.DrawColoredPolygon(pts, color); c.DrawPolyline(pts.Append(pts[0]).ToArray(), color, 1, true); }
        void FillWith(Color col, params float[] xy) { var pts = Ps(xy); c.DrawColoredPolygon(pts, col); c.DrawPolyline(pts.Append(pts[0]).ToArray(), col, 1, true); }
        void Ring(float x, float y, float r, float width) => c.DrawArc(P(x, y), r * s, 0, MathF.Tau, 40, color, width, true);
        void Disc(float x, float y, float r, Color? col = null) => c.DrawCircle(P(x, y), r * s, col ?? color, true, -1, true);
        void Arc(float x, float y, float r, float from, float to, float width) => c.DrawArc(P(x, y), r * s, from, to, 32, color, width, true);
        switch (kind)
        {
            case "sword":
                Fill(.80f,.14f, .86f,.20f, .40f,.64f, .34f,.58f);
                Line(w, .22f,.54f, .46f,.78f);
                Line(w * 1.1f, .33f,.67f, .16f,.84f);
                Disc(.14f, .86f, .07f); break;
            case "staff":
                Line(w, .22f,.86f, .70f,.30f); Ring(.74f, .25f, .13f, w * .8f); Disc(.74f, .25f, .05f); break;
            case "flame":
                Fill(.50f,.08f, .62f,.28f, .76f,.44f, .80f,.62f, .72f,.80f, .58f,.90f, .42f,.90f, .28f,.80f, .20f,.62f, .26f,.44f, .36f,.34f, .40f,.46f, .46f,.30f);
                FillWith(d, .50f,.50f, .60f,.64f, .60f,.78f, .50f,.86f, .40f,.78f, .40f,.66f); break;
            case "leaf":
                Fill(.18f,.82f, .20f,.52f, .34f,.30f, .58f,.16f, .86f,.14f, .84f,.42f, .70f,.66f, .46f,.80f);
                c.DrawPolyline(Ps(.18f,.82f, .44f,.56f, .70f,.30f), d, Math.Max(1.2f, w * .55f), true);
                c.DrawPolyline(Ps(.44f,.56f, .44f,.36f), d, Math.Max(1f, w * .4f), true);
                c.DrawPolyline(Ps(.56f,.44f, .72f,.46f), d, Math.Max(1f, w * .4f), true); break;
            case "flask":
                Line(w * .9f, .38f,.12f, .62f,.12f);
                Fill(.42f,.14f, .58f,.14f, .58f,.38f, .78f,.62f, .78f,.74f, .68f,.88f, .32f,.88f, .22f,.74f, .22f,.62f, .42f,.38f);
                FillWith(d, .27f,.66f, .73f,.66f, .73f,.73f, .65f,.83f, .35f,.83f, .27f,.73f); break;
            case "spark":
                Fill(.50f,.06f, .58f,.42f, .94f,.50f, .58f,.58f, .50f,.94f, .42f,.58f, .06f,.50f, .42f,.42f);
                Disc(.80f, .20f, .06f); break;
            case "dodge":
                Line(w * 1.15f, .18f,.24f, .44f,.50f, .18f,.76f);
                Line(w * 1.15f, .48f,.24f, .74f,.50f, .48f,.76f);
                c.DrawPolyline(Ps(.78f,.30f, .90f,.50f, .78f,.70f), new Color(color, .45f), w * .8f, true); break;
            case "bag":
                Arc(.50f, .40f, .17f, MathF.PI, MathF.Tau, w * .85f);
                Fill(.18f,.44f, .82f,.44f, .86f,.80f, .80f,.88f, .20f,.88f, .14f,.80f);
                FillWith(d, .30f,.52f, .70f,.52f, .66f,.64f, .34f,.64f);
                Disc(.50f, .63f, .045f); break;
            case "person":
                Disc(.50f, .32f, .17f);
                Fill(.16f,.90f, .18f,.72f, .30f,.58f, .42f,.54f, .58f,.54f, .70f,.58f, .82f,.72f, .84f,.90f); break;
            case "book":
                Fill(.10f,.24f, .30f,.18f, .48f,.24f, .48f,.84f, .30f,.78f, .10f,.84f);
                Fill(.52f,.24f, .70f,.18f, .90f,.24f, .90f,.84f, .70f,.78f, .52f,.84f);
                c.DrawPolyline(Ps(.18f,.36f, .40f,.34f), d, Math.Max(1f, w * .45f), true);
                c.DrawPolyline(Ps(.18f,.48f, .40f,.46f), d, Math.Max(1f, w * .45f), true);
                c.DrawPolyline(Ps(.60f,.36f, .82f,.34f), d, Math.Max(1f, w * .45f), true); break;
            case "map":
                Fill(.10f,.22f, .36f,.14f, .36f,.78f, .10f,.86f);
                FillWith(color.Darkened(.18f), .38f,.14f, .62f,.22f, .62f,.86f, .38f,.78f);
                Fill(.64f,.22f, .90f,.14f, .90f,.78f, .64f,.86f);
                Disc(.50f, .44f, .07f, d); break;
            case "scroll":
                Fill(.24f,.20f, .76f,.20f, .76f,.80f, .24f,.80f);
                Disc(.24f, .20f, .08f); Disc(.76f, .80f, .08f); Disc(.24f, .80f, .08f); Disc(.76f, .20f, .08f);
                c.DrawPolyline(Ps(.36f,.38f, .64f,.38f), d, Math.Max(1f, w * .5f), true);
                c.DrawPolyline(Ps(.36f,.50f, .64f,.50f), d, Math.Max(1f, w * .5f), true);
                c.DrawPolyline(Ps(.36f,.62f, .56f,.62f), d, Math.Max(1f, w * .5f), true); break;
            case "gear":
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.Tau / 8; var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    c.DrawLine(P(.5f, .5f) + dir * s * .26f, P(.5f, .5f) + dir * s * .42f, color, s * .13f, true);
                }
                Ring(.5f, .5f, .24f, s * .12f); Disc(.5f, .5f, .08f); break;
            case "plus":
                Line(w * 1.2f, .5f,.18f, .5f,.82f); Line(w * 1.2f, .18f,.5f, .82f,.5f); break;
            case "power":
                Arc(.5f, .54f, .30f, -MathF.PI * .32f, MathF.PI * 1.32f, w);
                Line(w, .5f,.12f, .5f,.48f); break;
            case "hammer":
                Line(w * 1.1f, .30f,.82f, .62f,.40f);
                Fill(.44f,.30f, .64f,.12f, .90f,.38f, .72f,.56f); break;
            case "play": Fill(.30f,.16f, .84f,.50f, .30f,.84f); break;
            case "back": Line(w * 1.2f, .64f,.16f, .30f,.50f, .64f,.84f); break;
            case "next": Line(w * 1.2f, .36f,.16f, .70f,.50f, .36f,.84f); break;
            case "close": Line(w * 1.15f, .2f,.2f, .8f,.8f); Line(w * 1.15f, .8f,.2f, .2f,.8f); break;
            case "check": Line(w * 1.25f, .16f,.54f, .40f,.78f, .86f,.24f); break;
            case "cross": Line(w * 1.1f, .24f,.24f, .76f,.76f); Line(w * 1.1f, .76f,.24f, .24f,.76f); break;
            case "lock":
                Arc(.5f, .40f, .20f, MathF.PI, MathF.Tau, w);
                Line(w, .30f,.40f, .30f,.48f); Line(w, .70f,.40f, .70f,.48f);
                Fill(.20f,.46f, .80f,.46f, .80f,.88f, .20f,.88f); Disc(.5f, .64f, .07f, d); break;
            case "key":
                Ring(.32f, .36f, .17f, w); Line(w, .44f,.48f, .86f,.86f); Line(w, .70f,.70f, .80f,.60f); Line(w, .78f,.78f, .88f,.68f); break;
            case "diamond": Fill(.5f,.10f, .90f,.5f, .5f,.90f, .10f,.5f); break;
            case "diamond-outline": Line(w, .5f,.12f, .88f,.5f, .5f,.88f, .12f,.5f, .5f,.12f); break;
            case "dot": Disc(.5f, .5f, .22f); break;
            case "ring": Ring(.5f, .5f, .30f, w); break;
            case "coin":
                Disc(.5f, .5f, .40f); Disc(.5f, .5f, .29f, d); Disc(.5f, .5f, .22f);
                c.DrawPolyline(Ps(.5f,.36f, .5f,.64f), d, Math.Max(1.2f, w * .6f), true); break;
            case "up": Fill(.5f,.20f, .84f,.70f, .16f,.70f); break;
            case "down": Fill(.16f,.30f, .84f,.30f, .5f,.80f); break;
            case "left": Fill(.20f,.5f, .70f,.16f, .70f,.84f); break;
            case "right": Fill(.80f,.5f, .30f,.84f, .30f,.16f); break;
            case "target":
                Ring(.5f, .5f, .30f, w * .9f); Disc(.5f, .5f, .09f);
                Line(w * .9f, .5f,.04f, .5f,.18f); Line(w * .9f, .5f,.82f, .5f,.96f);
                Line(w * .9f, .04f,.5f, .18f,.5f); Line(w * .9f, .82f,.5f, .96f,.5f); break;
            case "star":
                {
                    var pts = new float[20];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = -MathF.PI / 2 + i * MathF.PI / 5, r = i % 2 == 0 ? .44f : .19f;
                        pts[i * 2] = .5f + MathF.Cos(a) * r; pts[i * 2 + 1] = .54f + MathF.Sin(a) * r;
                    }
                    Fill(pts); break;
                }
            case "shield":
                Fill(.5f,.08f, .86f,.20f, .82f,.56f, .5f,.92f, .18f,.56f, .14f,.20f);
                FillWith(d, .5f,.20f, .74f,.28f, .71f,.54f, .5f,.80f); break;
            case "bolt": Fill(.58f,.06f, .22f,.56f, .48f,.56f, .40f,.94f, .80f,.40f, .54f,.40f); break;
            case "feather":
                Fill(.16f,.86f, .30f,.56f, .52f,.30f, .86f,.12f, .74f,.46f, .50f,.70f);
                c.DrawPolyline(Ps(.12f,.90f, .62f,.40f), d, Math.Max(1.2f, w * .5f), true); break;
            case "wave":
                for (int row = 0; row < 2; row++)
                {
                    var pts = new Vector2[13];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float x = .12f + i * .76f / 12f;
                        pts[i] = P(x, .38f + row * .26f + MathF.Sin(i / 12f * MathF.Tau * 1.5f) * .07f);
                    }
                    c.DrawPolyline(pts, color, w, true);
                }
                break;
            case "hand":
                Fill(.30f,.90f, .24f,.56f, .30f,.50f, .38f,.56f, .38f,.20f, .46f,.14f, .54f,.20f, .54f,.44f, .62f,.40f, .70f,.46f, .76f,.46f, .82f,.54f, .80f,.74f, .70f,.90f);
                break;
            case "warning":
                Line(w, .5f,.12f, .90f,.84f, .10f,.84f, .5f,.12f);
                Line(w * 1.1f, .5f,.38f, .5f,.62f); Disc(.5f, .73f, .05f); break;
            case "pin":
                Fill(.5f,.92f, .26f,.54f, .24f,.38f, .32f,.20f, .5f,.10f, .68f,.20f, .76f,.38f, .74f,.54f);
                Disc(.5f, .38f, .10f, d); break;
            case "compass":
                Ring(.5f, .5f, .42f, w * .6f);
                FillWith(color, .5f,.10f, .60f,.5f, .5f,.5f, .40f,.5f);
                FillWith(d, .5f,.90f, .40f,.5f, .5f,.5f, .60f,.5f); break;
            case "lever":
                Line(w, .30f,.84f, .62f,.30f); Disc(.65f, .24f, .11f);
                Fill(.16f,.80f, .56f,.80f, .56f,.90f, .16f,.90f); break;
            case "eye":
                Line(w * .8f, .08f,.5f, .30f,.30f, .5f,.24f, .70f,.30f, .92f,.5f, .70f,.70f, .5f,.76f, .30f,.70f, .08f,.5f);
                Disc(.5f, .5f, .14f); break;
            case "chest":
                Fill(.12f,.44f, .88f,.44f, .88f,.86f, .12f,.86f);
                FillWith(color.Darkened(.2f), .12f,.40f, .18f,.20f, .82f,.20f, .88f,.40f);
                FillWith(d, .44f,.40f, .56f,.40f, .56f,.58f, .44f,.58f); break;
            default: Disc(.5f, .5f, .2f); break;
        }
    }
}

/// <summary>A non-interactive vector icon control.</summary>
public partial class Glyph : Control
{
    string kind = "dot";
    Color color = Colors.White;
    Color? detail;
    public string Kind { get => kind; set { kind = value; QueueRedraw(); } }
    public Color Tint { get => color; set { color = value; QueueRedraw(); } }
    public Color? Detail { get => detail; set { detail = value; QueueRedraw(); } }
    public Glyph() { MouseFilter = MouseFilterEnum.Ignore; }
    public Glyph(string kind, Color color, float size) : this()
    {
        this.kind = kind; this.color = color;
        CustomMinimumSize = new Vector2(size, size);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }
    public override void _Draw() => GlyphPainter.Draw(this, kind, new Rect2(Vector2.Zero, Size), color, detail);
}

/// <summary>
/// A real Button that hosts arbitrary content (icons, stacked labels, chips). Its text,
/// if any, is kept for keyboard/semantic identity but painted transparent, and its
/// height follows the content so wrapping text at large sizes never clips.
/// </summary>
public partial class RichButton : Button
{
    public MarginContainer Frame { get; }
    public HBoxContainer Content { get; }
    public float MinHeight { get; set; } = UiDesign.Target;
    public RichButton(string semantic = "", float padX = 14, float padY = 10)
    {
        Text = semantic; ClipText = true;
        MouseFilter = MouseFilterEnum.Pass; FocusMode = FocusModeEnum.All;
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color" })
            AddThemeColorOverride(name, Colors.Transparent);
        Frame = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        Frame.AddThemeConstantOverride("margin_left", (int)padX); Frame.AddThemeConstantOverride("margin_right", (int)padX);
        Frame.AddThemeConstantOverride("margin_top", (int)padY); Frame.AddThemeConstantOverride("margin_bottom", (int)padY);
        AddChild(Frame);
        Frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
        Content.AddThemeConstantOverride("separation", 12);
        Frame.AddChild(Content);
        Frame.MinimumSizeChanged += Fit;
        Resized += Fit;
    }
    public override void _Ready() { UiDesign.IgnoreMouse(Frame); Fit(); }
    void Fit()
    {
        float height = Math.Max(MinHeight, Frame.GetCombinedMinimumSize().Y);
        if (Math.Abs(CustomMinimumSize.Y - height) > .5f) CustomMinimumSize = new Vector2(CustomMinimumSize.X, height);
    }
    /// <summary>Stacked title and optional subtitle, expanding to fill the row.</summary>
    public VBoxContainer Stack(string title, string? subtitle = null, string titleVariation = "RowTitle")
    {
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 1);
        Content.AddChild(stack);
        var head = UiDesign.Text(title); head.ThemeTypeVariation = titleVariation; stack.AddChild(head);
        if (!string.IsNullOrEmpty(subtitle)) { var sub = UiDesign.Text(subtitle); sub.ThemeTypeVariation = "RowSubtitle"; stack.AddChild(sub); }
        return stack;
    }
}

/// <summary>Hairline divider with an optional centred diamond, fading at both ends.</summary>
public partial class Divider : Control
{
    public bool Ornament { get; set; } = true;
    public Divider() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(0, 14); SizeFlagsHorizontal = SizeFlags.ExpandFill; }
    public override void _Draw()
    {
        float y = Size.Y / 2, cx = Size.X / 2;
        var line = UiDesign.GoldDeep; var clear = new Color(line, 0);
        float gap = Ornament ? 12 : 0;
        DrawPolylineColors(new[] { new Vector2(0, y), new Vector2(cx - gap, y) }, new[] { clear, line }, 1, true);
        DrawPolylineColors(new[] { new Vector2(cx + gap, y), new Vector2(Size.X, y) }, new[] { line, clear }, 1, true);
        if (Ornament)
        {
            var g = UiDesign.Gold;
            DrawColoredPolygon(new[] { new Vector2(cx, y - 5), new Vector2(cx + 5, y), new Vector2(cx, y + 5), new Vector2(cx - 5, y) }, g);
            DrawCircle(new Vector2(cx - 10, y), 1.6f, g, true, -1, true); DrawCircle(new Vector2(cx + 10, y), 1.6f, g, true, -1, true);
        }
    }
}

/// <summary>
/// Dim and vignette behind modal surfaces; never takes input. Drawn as one textured pass,
/// or, behind a full-screen panel, only in the uncovered margins around <see cref="Hole"/>.
/// </summary>
public partial class Scrim : Control
{
    static GradientTexture2D? vignette, contrast;
    Rect2? hole;
    public Rect2? Hole { get => hole; set { if (hole != value) { hole = value; QueueRedraw(); } } }
    public Scrim() { MouseFilter = MouseFilterEnum.Ignore; }
    static GradientTexture2D Make(float centre, float edge) => new()
    {
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1.05f, .5f), Width = 128, Height = 128,
        Gradient = new Gradient { Colors = new[] { new Color(UiDesign.Night, centre), new Color(0, 0, 0, edge) }, Offsets = new[] { .3f, 1f } }
    };
    public override void _Draw()
    {
        var dim = new Color(UiDesign.Night, UiDesign.HighContrast ? .86f : .66f);
        if (hole is Rect2 covered)
        {
            var full = new Rect2(Vector2.Zero, Size);
            DrawRect(new Rect2(0, 0, Size.X, covered.Position.Y), dim);
            DrawRect(new Rect2(0, covered.End.Y, Size.X, Size.Y - covered.End.Y), dim);
            DrawRect(new Rect2(0, covered.Position.Y, covered.Position.X, covered.Size.Y), dim);
            DrawRect(new Rect2(covered.End.X, covered.Position.Y, full.End.X - covered.End.X, covered.Size.Y), dim);
            return;
        }
        var texture = UiDesign.HighContrast ? contrast ??= Make(.84f, .92f) : vignette ??= Make(.5f, .82f);
        DrawTextureRect(texture, new Rect2(Vector2.Zero, Size), false);
    }
    public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }
}

/// <summary>Rounded value bar with gloss, caption and numbers (never colour alone).</summary>
public partial class Meter : Control
{
    public string Caption { get; set; } = "";
    public string Value { get; set; } = "";
    public Color Fill { get; set; } = UiDesign.Gold;
    public float Ratio { get; set; }
    public bool Thin { get; set; }
    public Meter() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(0, 24); SizeFlagsHorizontal = SizeFlags.ExpandFill; }
    public override void _Draw() => DrawBar(this, new Rect2(Vector2.Zero, Size), Ratio, Fill, Caption, Value, Thin, false);

    public static void DrawBar(CanvasItem c, Rect2 area, float ratio, Color fill, string caption, string value, bool thin, bool warning, float fontScale = 1)
    {
        var font = UiDesign.BodySemi;
        int size = (int)Math.Round((thin ? 12 : Math.Clamp(area.Size.Y * .64f, 11, 16)) * fontScale);
        Rect2 bar = thin ? new Rect2(area.Position.X, area.End.Y - 7, area.Size.X, 7) : area;
        float r = bar.Size.Y / 2;
        var track = new StyleBoxFlat { BgColor = new Color("060a09", .82f), AntiAliasing = true, BorderColor = warning ? UiDesign.GoldBright : new Color(1, 1, 1, .10f) };
        track.BorderWidthLeft = track.BorderWidthTop = track.BorderWidthRight = track.BorderWidthBottom = warning ? 2 : 1;
        track.SetCornerRadiusAll((int)r);
        track.Draw(c.GetCanvasItem(), bar);
        float w = Math.Clamp(ratio, 0, 1) * (bar.Size.X - 2);
        if (w > 1)
        {
            var body = new StyleBoxFlat { BgColor = fill, AntiAliasing = true };
            body.SetCornerRadiusAll((int)Math.Max(1, r - 1));
            var fillRect = new Rect2(bar.Position + new Vector2(1, 1), new Vector2(Math.Max(w, Math.Min(bar.Size.Y - 2, bar.Size.X - 2)), bar.Size.Y - 2));
            body.Draw(c.GetCanvasItem(), fillRect);
            var gloss = new StyleBoxFlat { BgColor = new Color(1, 1, 1, .20f), AntiAliasing = true };
            gloss.SetCornerRadiusAll((int)Math.Max(1, r - 2)); gloss.CornerRadiusBottomLeft = gloss.CornerRadiusBottomRight = 0;
            gloss.Draw(c.GetCanvasItem(), new Rect2(fillRect.Position + new Vector2(1, 1), new Vector2(fillRect.Size.X - 2, Math.Max(1, fillRect.Size.Y * .42f))));
        }
        if (caption == "" && value == "") return;
        var ink = UiDesign.Ink; var outline = new Color(0, 0, 0, .9f);
        if (thin)
        {
            float baseline = bar.Position.Y - 3;
            c.DrawStringOutline(font, new Vector2(area.Position.X, baseline), caption, HorizontalAlignment.Left, -1, size, 3, outline);
            c.DrawString(font, new Vector2(area.Position.X, baseline), caption, HorizontalAlignment.Left, -1, size, UiDesign.Muted);
            c.DrawStringOutline(font, new Vector2(area.Position.X, baseline), value, HorizontalAlignment.Right, area.Size.X, size, 3, outline);
            c.DrawString(font, new Vector2(area.Position.X, baseline), value, HorizontalAlignment.Right, area.Size.X, size, ink);
            return;
        }
        float y = bar.Position.Y + (bar.Size.Y + font.GetAscent(size) - font.GetDescent(size)) / 2;
        c.DrawStringOutline(font, new Vector2(bar.Position.X + 10, y), caption, HorizontalAlignment.Left, -1, size, 4, outline);
        c.DrawString(font, new Vector2(bar.Position.X + 10, y), caption, HorizontalAlignment.Left, -1, size, ink);
        c.DrawStringOutline(font, new Vector2(bar.Position.X, y), value, HorizontalAlignment.Right, bar.Size.X - 10, size, 4, outline);
        c.DrawString(font, new Vector2(bar.Position.X, y), value, HorizontalAlignment.Right, bar.Size.X - 10, size, ink);
    }
}

/// <summary>Small radial highlight drawn behind showcased items and portraits.</summary>
public partial class Halo : Control
{
    static GradientTexture2D? glow;
    public Color Tint { get; set; } = UiDesign.Gold;
    public Halo() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        glow ??= new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f), Width = 128, Height = 128,
            Gradient = new Gradient { Colors = new[] { new Color(1, 1, 1, .5f), new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 1f } }
        };
        float s = Math.Min(Size.X, Size.Y);
        DrawTextureRect(glow, new Rect2((Size - new Vector2(s, s)) / 2, new Vector2(s, s)), false, new Color(Tint, .35f));
    }
    public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }
}

/// <summary>Painted on/off switch used inside settings rows; the state is also written beside it.</summary>
public partial class Switch : Control
{
    public bool On { get; set; }
    public Switch() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(52, 30); SizeFlagsVertical = SizeFlags.ShrinkCenter; }
    public override void _Draw()
    {
        var track = new Rect2(0, (Size.Y - 28) / 2, Size.X, 28);
        var box = new StyleBoxFlat { BgColor = On ? new Color("c9a052") : UiDesign.Inset, BorderColor = On ? UiDesign.GoldBright : UiDesign.LineStrong, AntiAliasing = true };
        box.SetBorderWidthAll(UiDesign.HighContrast ? 2 : 1); box.SetCornerRadiusAll(14);
        box.Draw(GetCanvasItem(), track);
        var knob = new Vector2(On ? track.End.X - 14 : track.Position.X + 14, track.Position.Y + 14);
        DrawCircle(knob + new Vector2(0, 1.5f), 10, new Color(0, 0, 0, .35f), true, -1, true);
        DrawCircle(knob, 10, On ? UiDesign.Ink : new Color("8f998f"), true, -1, true);
    }
}
