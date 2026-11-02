using Godot;
namespace Mosslight;

public static class UiNavigationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var nav = new UiNavigation();
        nav.Open("character"); check(nav.Blocked && !nav.Menu, "screen opened from menu owns input");
        nav.Back(false,true); check(nav.Menu && nav.Screen == null, "screen Back returns to invoking menu");
        nav.Back(false,true); check(!nav.Blocked, "menu Back resumes living player");
        nav.Open("character"); nav.Back(false,true); check(!nav.Blocked, "direct screen Back returns to gameplay");
        nav.Pause(); nav.Confirm(); nav.Back(false,true); check(nav.Menu && !nav.Confirmation, "Back cancels destructive confirmation only");
        nav.Open("journal"); nav.Back(true,true); check(nav.Screen == "journal", "dialogue locks lower navigation");
        nav.Pause(); nav.Back(false,false); check(nav.Menu, "defeat Back retains recovery menu");
        nav.Resume(); nav.Open("map"); nav.Pause(); check(nav.Menu && nav.Screen == null, "background pause clears screen return path");
        foreach(var size in new[]{new Vector2(1280,800),new Vector2(1280,720),new Vector2(1920,864),new Vector2(1024,768),new Vector2(900,560)})
        {
            var safe = new Rect2(new Vector2(60,28), size-new Vector2(120,56));
            check(safe.Encloses(UiDesign.Overlay(safe,740)), $"{size} overlay within safe bounds");
        }
    }
    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        async System.Threading.Tasks.Task Frames(int count = 4)
        { for (int i=0;i<count;i++) await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame); }
        void Require(bool value, string text) { if(!value) throw new InvalidOperationException("UI navigation: "+text); }
        void Tap(Button button)
        {
            var at = button.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch { Index=14, Pressed=true, Position=at });
            Input.ParseInputEvent(new InputEventScreenTouch { Index=14, Pressed=false, Position=at });
        }
        var window = app.GetWindow(); var original = window.Size;
        try
        {
            app.Ui.PauseForVerification(); await Frames();
            var character = app.Ui.FindChildren("*", "Button", true, false).OfType<Button>().First(b=>b.Text=="Character");
            Tap(character); await Frames();
            Require(!app.Ui.MenuOpen && app.Ui.Navigation.Screen != null && app.GameplayBlocked, "menu to Character atomic input block");
            Require(app.GetViewport().GuiGetFocusOwner()==app.Ui.ScreenBack, "screen focus starts on persistent Back");
            Tap(app.Ui.ScreenBack); await Frames(); Require(app.Ui.MenuOpen,"touch Back returns to menu");
            app.Ui.Inventory(); await Frames();
            int potions = app.World.Items.Count("player", "potion");
            var use = app.Ui.FindChildren("*", "Button", true, false).OfType<Button>().First(b=>b.Text.StartsWith("Use · "));
            use.EmitSignal(Button.SignalName.Pressed); await Frames();
            Require(app.World.Items.Count("player", "potion") == potions - 1, "inventory use dispatches production item action");
            app.Ui.Back(); await Frames(); Require(app.Ui.MenuOpen, "inventory refresh after Use preserves menu caller");
            app.World.Items.Add("player", "potion");
            app.Ui.Back(); await Frames(); Require(!app.Ui.BlocksInput,"menu Back resumes");
            app.Ui.Character(); await Frames();
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.Escape, Pressed=true }); await Frames();
            Require(!app.Ui.BlocksInput,"Escape closes directly opened screen");
            app.Ui.Character(); app._Notification((int)Node.NotificationWMGoBackRequest); await Frames();
            Require(!app.Ui.BlocksInput,"Android Back follows same direct screen path");
            app.Ui.PauseForVerification(); await Frames();
            var newGame = app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="New game");
            Tap(newGame); await Frames(); Require(app.Ui.Navigation.Confirmation && !app.Ui.MenuOpen,"confirmation hides lower menu");
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.Back, Pressed=true });
            app._Notification((int)Node.NotificationWMGoBackRequest);
            await Frames(); Require(app.Ui.MenuOpen && !app.Ui.Navigation.Confirmation,"Android key plus window notification cancels confirmation exactly once");
            app.Ui.Character();
            app._Notification((int)Node.NotificationApplicationFocusOut);
            app._Notification((int)Node.NotificationApplicationFocusIn); await Frames();
            Require(app.Ui.MenuOpen && app.TouchMovement == Vector2.Zero,"focus restore retains safe pause");
            // Real production screen at compact and wide viewports, including large text.
            foreach(var size in new[]{new Vector2I(1280,800),new Vector2I(1280,720),new Vector2I(1920,864),new Vector2I(1024,768),new Vector2I(900,560)})
            {
                window.Size = size; await Frames();
                var viewport = app.GetViewport().GetVisibleRect();
                var safe = new Rect2(viewport.Position+new Vector2(60,28),viewport.Size-new Vector2(120,56));
                app.Ui.VerificationSafeArea = safe;
                app.Ui.SetTextScale(1.3f); app.Ui.Character(); await Frames(8);
                Require(safe.Encloses(app.Ui.ScreenRect), $"{size} screen safe bounds");
                Require(app.Ui.ScreenRect.Encloses(app.Ui.ScreenBack.GetGlobalRect()), $"{size} persistent Back inside panel");
                Require(app.Ui.ScreenBack.Size.Y >= UiDesign.Target, "Back target minimum");
                var scroll = app.Ui.FindChildren("*","ScrollContainer",true,false).OfType<TouchScroll>().First(s=>s.IsVisibleInTree());
                Require(scroll.GetVScrollBar().MaxValue > scroll.GetVScrollBar().Page,"large text pilot scrolls");
                var bar = scroll.GetVScrollBar().GetGlobalRect();
                Require(scroll.GetChild<Control>(0).GetGlobalRect().End.X <= bar.Position.X + 1,
                    $"{size} scroll content is laid out beside, not under, the touch scrollbar");
                string prefix = app.Ui.MobileLayout ? "mobile-" : "";
                Directory.CreateDirectory("docs/native-screenshots");
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                app.GetViewport().GetTexture().GetImage().SavePng($"docs/native-screenshots/{prefix}ui-character-{size.X}x{size.Y}-large.png");
                scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue; await Frames();
                Require(app.Ui.ScreenRect.Encloses(app.Ui.ScreenBack.GetGlobalRect()), "Back stays reachable after scrolling");
                app.Ui.Back();
            }
            // Notices must not cover gameplay controls at any supported viewport.
            foreach(var size in new[]{new Vector2I(1280,800),new Vector2I(900,560)})
            {
                window.Size = size; await Frames();
                var viewport = app.GetViewport().GetVisibleRect();
                app.Ui.VerificationSafeArea = new Rect2(viewport.Position+new Vector2(60,28),viewport.Size-new Vector2(120,56));
                app.ResumeGame(); app.Ui.Layout(); app.Ui.Notify("Layout notice"); await Frames();
                Require(app.Ui.NoticeVisible, "notice is shown");
                var notice = app.Ui.NoticeRect;
                foreach (var control in app.Ui.HotbarControls.Where(c => c.IsVisibleInTree()))
                    Require(!notice.Intersects(control.GetGlobalRect()), $"{size} notice clears control {control.Name}");
                app.Ui.Notify("");
            }
        }
        finally
        {
            app.Ui.VerificationSafeArea=null; window.Size=original; app.Ui.SetTextScale(1); app.Ui.PauseForVerification(); await Frames();
        }
    }
}
