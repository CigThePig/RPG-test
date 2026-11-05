using Godot;
namespace Mosslight;
/// <summary>One finger owns movement. Other fingers remain available for action buttons.</summary>
public partial class TouchStick : Control
{
    public Foundation App { get; set; } = null!;
    int finger = -1;
    Vector2 offset;
    Vector2 anchor;
    public int Finger => finger;
    public Vector2 Anchor => anchor;
    public Vector2 Displacement => offset;
    public static Vector2 Analog(Vector2 displacement, float deadzone=.1f, float curve=1)
    {
        float length = Math.Min(1, displacement.Length() / 72);
        return length <= deadzone ? Vector2.Zero : displacement.Normalized() * MathF.Pow((length-deadzone)/(1-deadzone),curve);
    }
    public void Cancel() { if (finger >= 0 && OS.HasFeature("android")) GD.Print($"MOSSLIGHT_TOUCH_RELEASE x={App.World.Player.X:0.00} y={App.World.Player.Y:0.00}"); if(finger>=0 && OS.HasFeature("android") && App.World.RegionsById[App.World.Player.Region].Landmarks.Count>0) GD.Print($"MOSSLIGHT_LANDMARK_MOVEMENT clear={App.World.RegionsById[App.World.Player.Region].CanStand(App.World.Player.Position,App.World.Player)} x={App.World.Player.X:0.00} y={App.World.Player.Y:0.00}"); finger = -1; offset = Vector2.Zero; App.TouchMovement = Vector2.Zero; QueueRedraw(); }
    public override void _Input(InputEvent e)
    {
        if (App.GameplayBlocked) { Cancel(); return; }
        if (e is InputEventScreenTouch touch)
        {
            if (touch.Canceled && touch.Index != finger)return;
            if ((touch.Canceled || !touch.Pressed) && touch.Index == finger) { Cancel(); return; }
            if (touch.Pressed && finger == -1 && new Rect2(GlobalPosition, Size).HasPoint(touch.Position))
            {
                finger = touch.Index;
                // Anchor can move within the pad, so an off-centre thumb starts at rest.
                anchor = (touch.Position - GlobalPosition).Clamp(new Vector2(55,55), Size - new Vector2(55,55));
                Move(touch.Position); GetViewport().SetInputAsHandled();
            }
        }
        if (e is InputEventScreenDrag drag && drag.Index == finger)
        { Move(drag.Position); GetViewport().SetInputAsHandled(); }
    }
    void Move(Vector2 point)
    {
        offset = (point - GlobalPosition - anchor).LimitLength(72);
        App.TouchMovement = Analog(offset, App.Feel.Deadzone, App.Feel.Curve);
        QueueRedraw();
    }
    public override void _Draw()
    {
        float scale = Math.Min(Size.X, Size.Y) / 200;
        var centre = Size / 2;
        bool active = finger >= 0;
        // Recessed base: soft shadow, glass disc, engraved rings and compass ticks.
        DrawCircle(centre, 94 * scale, new Color(.03f, .06f, .055f, active ? .62f : .46f));
        DrawArc(centre, 93 * scale, 0, MathF.Tau, 64, new Color(UiDesign.Gold, active ? .55f : .3f), 2 * scale, true);
        DrawArc(centre, 72 * scale, 0, MathF.Tau, 64, new Color(1, 1, 1, .07f), 1.5f * scale, true);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2 - MathF.PI / 2;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var tip = centre + dir * 86 * scale; var side = new Vector2(-dir.Y, dir.X) * 6 * scale;
            DrawColoredPolygon(new[] { tip, tip - dir * 9 * scale + side, tip - dir * 9 * scale - side }, new Color(UiDesign.GoldBright, active ? .5f : .28f));
        }
        var knob = (finger < 0 ? centre : anchor) + offset;
        if (active && offset.Length() > 4)
        {
            // Direction glow along the rim shows where the stick is pushed.
            float a = offset.Angle();
            DrawArc(centre, 93 * scale, a - .45f, a + .45f, 16, new Color(UiDesign.GoldBright, .85f), 4 * scale, true);
        }
        DrawCircle(knob + new Vector2(0, 4 * scale), 32 * scale, new Color(0, 0, 0, .35f));
        DrawCircle(knob, 30 * scale, new Color("2a3b34"));
        DrawCircle(knob, 25 * scale, new Color("3d5348"), true, -1, true);
        DrawArc(knob, 31 * scale, 0, MathF.Tau, 48, new Color(UiDesign.Gold, active ? .95f : .7f), 2 * scale, true);
    }
}
