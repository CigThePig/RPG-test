using Godot;
namespace Mosslight;

/// <summary>Fixed action identities and responsive geometry. Commands remain in Foundation.</summary>
public partial class MobileCombatControls : Control
{
    public const int LayoutVersion = 1;
    public Dictionary<string, Button> Actions { get; } = new();
    public Rect2 SafeRegion { get; private set; }
    public MobileCombatControls() { MouseFilter = MouseFilterEnum.Ignore; }
    public void Register(string id, Button button)
    {
        Actions.Add(id, button);
        button.Name = "action_" + id;
        button.ClipText = true;
        button.AddThemeFontSizeOverride("font_size", id == "attack" ? 23 : 18);
        var face = new StyleBoxFlat {
            BgColor = new Color(id == "attack" ? "795433" : id == "dodge" ? "355b56" : "263f43"),
            BorderColor = new Color("b0af7e"), BorderWidthLeft=2, BorderWidthRight=2, BorderWidthTop=2, BorderWidthBottom=2,
            CornerRadiusTopLeft=24, CornerRadiusTopRight=24, CornerRadiusBottomLeft=24, CornerRadiusBottomRight=24 };
        button.AddThemeStyleboxOverride("normal", face);
        foreach(string state in new[]{"hover","pressed","disabled"})
        {
            var variant=(StyleBoxFlat)face.Duplicate();
            variant.BgColor=state=="hover" ? face.BgColor.Lightened(.12f) : state=="pressed" ? face.BgColor.Darkened(.2f) : face.BgColor.Darkened(.35f);
            if(state=="disabled")variant.BorderColor=face.BorderColor.Darkened(.4f);
            button.AddThemeStyleboxOverride(state,variant);
        }
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
        // Compact fallback scales the complete arrangement, including touch gaps.
        float s = Math.Min(1, Math.Min(safe.Size.X / 900, safe.Size.Y / 560));
        var result = new Dictionary<string, Rect2>();
        void At(string id, float right, float bottom, float width, float height) =>
            result[id] = new Rect2(safe.End - new Vector2(right, bottom)*s, new Vector2(width,height)*s);
        At("attack",140,164,116,112);
        At("dodge",266,208,108,104);
        At("ember",130,292,100,72);
        At("mend",242,292,100,72);
        At("potion",354,292,100,72);
        At("interact",378,180,100,72);
        result["inventory"] = new Rect2(new Vector2(safe.End.X-224*s,safe.Position.Y+20*s), new Vector2(96,60)*s);
        result["menu"] = new Rect2(new Vector2(safe.End.X-116*s,safe.Position.Y+20*s), new Vector2(96,60)*s);
        result["stick"] = new Rect2(new Vector2(safe.Position.X+40*s,safe.End.Y-260*s),new Vector2(200,200)*s);
        return result;
    }
    public void Arrange(Rect2 safe)
    {
        SafeRegion = safe;
        foreach (var (id, rect) in Geometry(safe))
            if (Actions.TryGetValue(id, out var button))
            {
                button.CustomMinimumSize = Vector2.Zero;
                button.Position = rect.Position; button.Size = rect.Size;
            }
    }
}
