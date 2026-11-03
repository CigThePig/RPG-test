using Godot;
namespace Mosslight;
/// <summary>Own a swipe that starts over a menu button, so it cannot become a click.
/// Sliders, option popups, scrollbars and real mouse input retain native GUI handling.</summary>
public partial class TouchScroll : ScrollContainer
{
    int finger=-1, nativeFinger=-1;
    Vector2 start;
    int initialScroll;
    bool dragging, suppressMouse;
    int mouseReleaseGraceFrames, nativeMouseGraceFrames;
    // Godot may assign native Button focus after this scroll's _Input handler.
    // Sweep that late focus during the following frames of a touch swipe.
    int clearSwipeFocusFrames;
    // Verification counters to distinguish missing touch ownership from late focus.
    internal int DiagnosticTouches { get; private set; }
    internal int DiagnosticSwipes { get; private set; }
    internal int DiagnosticFocusReleases { get; private set; }
    Button? pressed;
    bool NativeTouchTarget(Vector2 position) =>
        GetVScrollBar().GetGlobalRect().HasPoint(position) ||
        FindChildren("*","Control",true,false).OfType<Control>().Any(c =>
            c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(position) &&
            (c is Slider || c is OptionButton));
    public void CancelGesture()
    {
        Cancel();
        clearSwipeFocusFrames = 0;
        nativeFinger=-1;nativeMouseGraceFrames=0;
    }
    void Cancel()
    {
        if(GodotObject.IsInstanceValid(pressed))pressed!.Modulate=Colors.White;
        pressed=null;finger=-1;dragging=false;
    }
    public override void _Input(InputEvent e)
    {
        if(!IsVisibleInTree()){CancelGesture();suppressMouse=false;mouseReleaseGraceFrames=0;return;}
        // Native scrollbar/slider/popup gestures keep their mouse input even when
        // their drag moves across ordinary rows.
        if(e is InputEventMouse nativeMouse && nativeMouse.Device==InputEvent.DeviceIdEmulation &&
            (nativeFinger>=0 || nativeMouseGraceFrames>0))
        {
            if(nativeFinger<0 && nativeMouse is InputEventMouseButton release && !release.Pressed)
                nativeMouseGraceFrames=0;
            return;
        }
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
            if(touch.Index==nativeFinger && (touch.Canceled || !touch.Pressed))
            {
                nativeFinger=-1;nativeMouseGraceFrames=2;
                return;
            }
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
                if(NativeTouchTarget(touch.Position))
                {
                    nativeFinger=touch.Index;nativeMouseGraceFrames=0;
                    suppressMouse=false;return;
                }
                finger=touch.Index;start=touch.Position;initialScroll=ScrollVertical;dragging=false;suppressMouse=true;mouseReleaseGraceFrames=0;
                DiagnosticTouches++;
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
                DiagnosticSwipes++;
                // A swipe is not a button selection. Clear any focus assigned by
                // the touchscreen's native/emulated GUI path, but preserve focus on
                // controls outside this scroll area (e.g. the fixed Back button).
                ClearSwipeFocus();
                // Keep guarding for late assignment by the native GUI queue,
                // including release-order races with emulated mouse events.
                clearSwipeFocusFrames = 4;
            }
            if(dragging)
            {
                if(GodotObject.IsInstanceValid(pressed))pressed!.Modulate=Colors.White;
                ClearSwipeFocus();
                ScrollVertical=(int)Math.Clamp(initialScroll+start.Y-drag.Position.Y,0,Math.Max(0,GetVScrollBar().MaxValue-GetVScrollBar().Page));
            }
            GetViewport().SetInputAsHandled();
        }
    }
    void ClearSwipeFocus()
    {
        // Never release the fixed Back button or focus elsewhere in the UI.
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus != null && IsAncestorOf(focus)) { focus.ReleaseFocus(); DiagnosticFocusReleases++; }
        if (GodotObject.IsInstanceValid(pressed) && pressed!.HasFocus()) pressed.ReleaseFocus();
    }
    public override void _Process(double delta)
    {
        if (clearSwipeFocusFrames > 0)
        {
            ClearSwipeFocus();
            --clearSwipeFocusFrames;
        }
        // Covers the release-order race without swallowing a future, unrelated
        // emulated click. Genuine desktop mouse input is never suppressed.
        if(finger<0 && mouseReleaseGraceFrames>0 && --mouseReleaseGraceFrames==0)
            suppressMouse=false;
        if(nativeFinger<0 && nativeMouseGraceFrames>0)--nativeMouseGraceFrames;
    }
    public override void _Notification(int what)
    {
        if(what==(int)NotificationApplicationPaused || what==(int)NotificationApplicationFocusOut || what==NotificationExitTree) {CancelGesture();suppressMouse=false;mouseReleaseGraceFrames=0;}
    }
}
