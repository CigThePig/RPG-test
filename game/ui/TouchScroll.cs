using Godot;
namespace Mosslight;
/// <summary>Own a swipe that starts over a menu button, so it cannot become a click.
/// Sliders, option popups, scrollbars and real mouse input retain native GUI handling.</summary>
public partial class TouchScroll : ScrollContainer
{
    int finger=-1;
    Vector2 start;
    int initialScroll;
    bool dragging, suppressMouse;
    int mouseReleaseGraceFrames;
    Button? pressed;
    bool NativeTouchTarget(Vector2 position) =>
        GetVScrollBar().GetGlobalRect().HasPoint(position) ||
        FindChildren("*","Control",true,false).OfType<Control>().Any(c =>
            c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(position) &&
            (c is Slider || c is OptionButton));
    public void CancelGesture() => Cancel();
    void Cancel()
    {
        if(GodotObject.IsInstanceValid(pressed))pressed!.Modulate=Colors.White;
        pressed=null;finger=-1;dragging=false;
    }
    public override void _Input(InputEvent e)
    {
        if(!IsVisibleInTree()){Cancel();suppressMouse=false;return;}
        // Emulated mouse-down may precede the matching screen-touch press on
        // Android. Intercept it at the scroll surface even before finger ownership
        // is established, or Button can remain natively pressed when we later
        // consume its emulated mouse-up. Real mouse input is unaffected.
        if(e is InputEventMouse mouse && mouse.Device==InputEvent.DeviceIdEmulation &&
            (suppressMouse || (GetGlobalRect().HasPoint(mouse.Position) && !NativeTouchTarget(mouse.Position))))
        {
            // Godot can deliver the emulated mouse-up before the screen-touch release.
            // Do not let that mouse-up end suppression while the finger still owns the
            // gesture: subsequent emulated events could leave a Button visually down.
            GetViewport().SetInputAsHandled();
            if(mouse is InputEventMouseButton b && !b.Pressed && finger<0)
            {
                suppressMouse=false;
                mouseReleaseGraceFrames=0;
            }
            return;
        }
        if(e is InputEventScreenTouch touch)
        {
            if(touch.Canceled)
            {
                if(touch.Index==finger)
                {
                    Cancel(); mouseReleaseGraceFrames=2;
                    GetViewport().SetInputAsHandled();
                }
                return;
            }
            if(touch.Pressed && finger<0 && GetGlobalRect().HasPoint(touch.Position))
            {
                // The scrollbar remains directly draggable. Range controls and popup selectors
                // must receive native GUI events rather than being treated as menu buttons.
                if(NativeTouchTarget(touch.Position)) {suppressMouse=false;return;}
                finger=touch.Index;start=touch.Position;initialScroll=ScrollVertical;dragging=false;suppressMouse=true;mouseReleaseGraceFrames=0;
                pressed=FindChildren("*","Button",true,false).OfType<Button>().LastOrDefault(b=>b.IsVisibleInTree() && !b.Disabled && b.GetGlobalRect().HasPoint(touch.Position));
                if(pressed!=null)pressed.Modulate=new Color(.75f,.85f,.85f);
                GetViewport().SetInputAsHandled();
            }
            else if(touch.Index==finger && (!touch.Pressed || touch.Canceled))
            {
                var activate=!touch.Canceled && !dragging && GodotObject.IsInstanceValid(pressed) && !pressed!.Disabled && pressed.GetGlobalRect().HasPoint(touch.Position) ? pressed : null;
                Cancel(); mouseReleaseGraceFrames=2;
                GetViewport().SetInputAsHandled();
                activate?.EmitSignal(Button.SignalName.Pressed);
            }
        }
        if(e is InputEventScreenDrag drag && drag.Index==finger)
        {
            if(!dragging && Math.Abs(drag.Position.Y-start.Y)>ScrollDeadzone)
            {
                dragging=true;
                // A swipe is not a button selection. Clear any focus assigned by
                // the touchscreen's native/emulated GUI path, but preserve focus on
                // controls outside this scroll area (e.g. the fixed Back button).
                var focus=GetViewport().GuiGetFocusOwner();
                if(focus!=null && IsAncestorOf(focus))focus.ReleaseFocus();
            }
            if(dragging)
            {
                if(GodotObject.IsInstanceValid(pressed))pressed!.Modulate=Colors.White;
                ScrollVertical=(int)Math.Clamp(initialScroll+start.Y-drag.Position.Y,0,Math.Max(0,GetVScrollBar().MaxValue-GetVScrollBar().Page));
            }
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _Process(double delta)
    {
        // Covers the release-order race without swallowing a future, unrelated
        // emulated click. Genuine desktop mouse input is never suppressed.
        if(finger<0 && mouseReleaseGraceFrames>0 && --mouseReleaseGraceFrames==0)
            suppressMouse=false;
    }
    public override void _Notification(int what)
    {
        if(what==(int)NotificationApplicationPaused || what==(int)NotificationApplicationFocusOut || what==NotificationExitTree) {Cancel();suppressMouse=false;mouseReleaseGraceFrames=0;}
    }
}
