using Godot;
namespace Mosslight;
/// <summary>One finger owns movement. Other fingers remain available for action buttons.</summary>
public partial class TouchStick : Control
{
    public Foundation App { get; set; } = null!;
    int finger = -1;
    Vector2 offset;
    public void Cancel() { finger = -1; offset = Vector2.Zero; App.TouchMovement = Vector2.Zero; QueueRedraw(); }
    public override void _Input(InputEvent e)
    {
        if (App.GameplayBlocked) { Cancel(); return; }
        if (e is InputEventScreenTouch touch)
        {
            if (!touch.Pressed && touch.Index == finger) { Cancel(); return; }
            if (touch.Pressed && finger == -1 && new Rect2(GlobalPosition, Size).HasPoint(touch.Position))
            { finger = touch.Index; Move(touch.Position); GetViewport().SetInputAsHandled(); }
        }
        if (e is InputEventScreenDrag drag && drag.Index == finger)
        { Move(drag.Position); GetViewport().SetInputAsHandled(); }
    }
    void Move(Vector2 point)
    {
        offset = (point - GlobalPosition - Size / 2).LimitLength(80);
        var movement = offset / 80;
        App.TouchMovement = movement.Length() < .12f ? Vector2.Zero : movement;
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawCircle(Size / 2, 94, new Color(.08f,.14f,.14f,.65f));
        DrawCircle(Size / 2 + offset, 34, new Color(.65f,.73f,.5f,.8f));
    }
}
