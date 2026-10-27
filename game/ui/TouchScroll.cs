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
    Button? pressed;
    void Cancel()
    {
        if(GodotObject.IsInstanceValid(pressed))pressed!.Modulate=Colors.White;
        pressed=null;finger=-1;dragging=false;
    }
    public override void _Input(InputEvent e)
    {
        if(!IsVisibleInTree()){Cancel();suppressMouse=false;return;}
        if(e is InputEventMouse mouse && mouse.Device==InputEvent.DeviceIdEmulation && suppressMouse)
        {
            GetViewport().SetInputAsHandled();
            if(mouse is InputEventMouseButton b && !b.Pressed)suppressMouse=false;
            return;
        }
        if(e is InputEventScreenTouch touch)
        {
            if(touch.Pressed && finger<0 && GetGlobalRect().HasPoint(touch.Position))
            {
                // The scrollbar remains directly draggable. Range controls and popup selectors
                // must receive native GUI events rather than being treated as menu buttons.
                if(GetVScrollBar().GetGlobalRect().HasPoint(touch.Position)) {suppressMouse=false;return;}
                if(FindChildren("*","Control",true,false).OfType<Control>().Any(c=>c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(touch.Position) && (c is Slider || c is OptionButton))) {suppressMouse=false;return;}
                finger=touch.Index;start=touch.Position;initialScroll=ScrollVertical;dragging=false;suppressMouse=true;
                pressed=FindChildren("*","Button",true,false).OfType<Button>().LastOrDefault(b=>b.IsVisibleInTree() && !b.Disabled && b.GetGlobalRect().HasPoint(touch.Position));
                if(pressed!=null)pressed.Modulate=new Color(.75f,.85f,.85f);
                GetViewport().SetInputAsHandled();
            }
            else if(touch.Index==finger && (!touch.Pressed || touch.Canceled))
            {
                var activate=!touch.Canceled && !dragging && GodotObject.IsInstanceValid(pressed) && !pressed!.Disabled && pressed.GetGlobalRect().HasPoint(touch.Position) ? pressed : null;
                Cancel();GetViewport().SetInputAsHandled();
                activate?.EmitSignal(Button.SignalName.Pressed);
            }
        }
        if(e is InputEventScreenDrag drag && drag.Index==finger)
        {
            if(Math.Abs(drag.Position.Y-start.Y)>ScrollDeadzone)dragging=true;
            if(dragging)
            {
                if(GodotObject.IsInstanceValid(pressed))pressed!.Modulate=Colors.White;
                ScrollVertical=(int)Math.Clamp(initialScroll+start.Y-drag.Position.Y,0,Math.Max(0,GetVScrollBar().MaxValue-GetVScrollBar().Page));
            }
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _Notification(int what)
    {
        if(what==(int)NotificationApplicationPaused || what==NotificationExitTree) {Cancel();suppressMouse=false;}
    }
}
