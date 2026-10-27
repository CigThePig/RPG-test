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
    public void Cancel() { if (finger >= 0 && OS.HasFeature("android")) GD.Print($"MOSSLIGHT_TOUCH_RELEASE x={App.World.Player.X:0.00} y={App.World.Player.Y:0.00}"); finger = -1; offset = Vector2.Zero; App.TouchMovement = Vector2.Zero; QueueRedraw(); }
    public override void _Input(InputEvent e)
    {
        if (App.GameplayBlocked) { Cancel(); return; }
        if (e is InputEventScreenTouch touch)
        {
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
        DrawCircle(Size / 2, 94, new Color(.08f,.14f,.14f,.65f));
        DrawCircle((finger < 0 ? Size / 2 : anchor) + offset, 30, new Color(.65f,.73f,.5f,.8f));
    }
}
