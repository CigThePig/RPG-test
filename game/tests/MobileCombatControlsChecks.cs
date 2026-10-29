using Godot;
namespace Mosslight;

public static class MobileCombatControlsChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(var size in new[]{new Vector2(1280,800),new Vector2(1280,720),new Vector2(1920,864),new Vector2(1024,768),new Vector2(900,560)})
        foreach(var insets in new[]{Vector2.Zero,new Vector2(60,28)})
        {
            var safe=new Rect2(insets,size-insets*2);
            var geometry=MobileCombatControls.Geometry(safe).ToArray();
            foreach(var (id,rect) in geometry)
                check(safe.Encloses(rect),$"{size}/{insets}: {id} inside safe bounds");
            for(int i=0;i<geometry.Length;i++)for(int j=i+1;j<geometry.Length;j++)
                check(!geometry[i].Value.Intersects(geometry[j].Value),$"{size}/{insets}: {geometry[i].Key}/{geometry[j].Key} separated");
        }
        var viewport=new Rect2(0,0,1280,800);
        var screen=new Transform2D(new Vector2(2,0),new Vector2(0,2),new Vector2(40,20));
        var converted=MobileCombatControls.ToLogicalSafeArea(viewport,new Rect2(140,60,2360,1480),screen);
        check(converted.IsEqualApprox(new Rect2(50,20,1180,740)),"safe pixels transformed through scale and letterbox offset");
        check(MobileCombatControls.ToLogicalSafeArea(viewport,new Rect2(),screen)==viewport,"missing platform safe area uses viewport");
    }
    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        if(!app.Ui.MobileLayout)return;
        var window=app.GetWindow();var original=window.Size;
        try
        {
            foreach(var size in new[]{new Vector2I(1280,800),new Vector2I(1280,720),new Vector2I(1920,864),new Vector2I(1024,768)})
            {
                window.Size=size;
                for(int i=0;i<4;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
                var logical=app.GetViewport().GetVisibleRect();
                app.Ui.VerificationSafeArea=new Rect2(logical.Position+new Vector2(50,24),logical.Size-new Vector2(100,48));
                app.Ui.Layout();
                var controls=app.Ui.CombatControls!;
                var buttons=controls.Actions.Values.ToArray();
                foreach(var b in buttons)
                    if(!controls.SafeRegion.Encloses(b.GetGlobalRect()))throw new InvalidOperationException("Actual touch rect outside safe area: "+b.Name);
                for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)
                    if(buttons[i].GetGlobalRect().Intersects(buttons[j].GetGlobalRect()))throw new InvalidOperationException("Actual mobile rect overlap");
                var attack=app.Ui.ActionControl("attack");var rect=attack.GetGlobalRect();
                attack.Text="Attack\n999.9s";
                await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
                if(attack.GetGlobalRect()!=rect)throw new InvalidOperationException("Cooldown changed touch rectangle");
            }
        }
        finally{app.Ui.VerificationSafeArea=null;window.Size=original;app.Ui.Layout();}
    }
    public static async System.Threading.Tasks.Task InputPipeline(Foundation app)
    {
        if(!app.Ui.MobileLayout)return;
        int attacks=0,dodges=0;
        void Diagnostic(string kind,System.Numerics.Vector2 direction,string context)
        {if(kind=="attack_started")attacks++;if(kind=="dodge_started")dodges++;}
        async System.Threading.Tasks.Task Frame(int n=2)
        {for(int i=0;i<n;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);}
        void Touch(int finger,bool pressed,Vector2 at,bool canceled=false) => Input.ParseInputEvent(new InputEventScreenTouch{Index=finger,Pressed=pressed,Position=at,Canceled=canceled});
        void Tap(string id)
        {var at=app.Ui.ActionControl(id).GetGlobalRect().GetCenter();Touch(8,true,at);Touch(8,false,at);}
        void Require(bool value,string name){if(!value)throw new InvalidOperationException("Mobile pipeline: "+name);}
        app.BeginFeelCourse();
        app.World.CombatDiagnostic+=Diagnostic;
        try
        {
            foreach(var a in app.World.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            var p=app.World.Player;p.Invulnerable=1000;
            await Frame();
            var attack=app.Ui.ActionControl("attack").GetGlobalRect().GetCenter();
            var dodge=app.Ui.ActionControl("dodge").GetGlobalRect().GetCenter();
            var outside=new Vector2(700,200);
            Touch(8,true,outside);Touch(8,false,attack);await Frame();
            Require(attacks==0,"outside press / inside release");
            Touch(8,true,attack);
            Input.ParseInputEvent(new InputEventScreenDrag{Index=8,Position=dodge});
            Touch(8,false,dodge);await Frame();
            Require(attacks==0 && dodges==0,"drag across actions");
            Touch(8,true,attack);Touch(8,false,attack,true);await Frame();
            Require(attacks==0,"cancellation");
            var stick=app.Ui.Stick.GetGlobalRect().GetCenter();
            Touch(7,true,stick);
            Input.ParseInputEvent(new InputEventScreenDrag{Index=7,Position=stick+Vector2.Right*72});
            Tap("attack");await Frame();
            Require(attacks==1 && app.Ui.Stick.Finger==7,"movement and free attack");
            Tap("attack");await Frame();Require(attacks==1,"cooldown rejection");
            app.World.Cancel(p);p.Cooldowns.Clear();await Frame();
            // Two independent action fingers must never transfer ownership to the pad.
            Touch(8,true,attack);Touch(9,true,dodge);Touch(9,false,dodge);Touch(8,false,attack);await Frame();
            Require(dodges==1 && app.Ui.Stick.Finger==7,"separate action fingers retain movement ownership");
            app.World.CancelPlayerDodge();app.World.Cancel(p);p.Cooldowns.Clear();await Frame();
            p.Health=20;p.Mana=p.MaxMana;
            float mana=p.Mana;
            Tap("mend");await Frame();
            Require(p.Mana<mana && app.Ui.Stick.Finger==7,"movement plus Mend dispatch");
            app.World.Cancel(p);p.Cooldowns.Clear();p.Mana=p.MaxMana;
            var rangedTarget=app.World.Actors.Values.First(a=>a.Profile=="beast");
            rangedTarget.Region=p.Region;rangedTarget.X=p.X+150;rangedTarget.Y=p.Y;rangedTarget.Health=rangedTarget.MaxHealth;
            await Frame();Tap("ember");await Frame();
            Require(p.Cooldowns.GetValueOrDefault("ember")>0 && app.Ui.Stick.Finger==7,"movement plus Ember dispatch");
            rangedTarget.Region="reach";
            app.World.Cancel(p);p.Cooldowns.Clear();
            p.X=432;p.Y=300;
            await Frame();Tap("interact");await Frame();
            Require(app.World.RegionsById[p.Region].Depleted.Contains(12*64+18) && app.Ui.Stick.Finger==7,"movement plus production chest interaction");
            app.World.Cancel(p);p.Cooldowns.Clear();p.Mana=0;await Frame();
            Tap("mend");await Frame();Require(!p.Cooldowns.ContainsKey("mend") && p.Mana<1,"disabled spell rejects touch");
            p.Health=20;int potions=app.World.Items.Count("player","potion");
            var potion=app.Ui.ActionControl("potion").GetGlobalRect().GetCenter();
            Touch(8,true,potion);
            Input.ParseInputEvent(new InputEventMouseButton{Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=true,Position=potion,GlobalPosition=potion});
            Touch(8,false,potion);
            Input.ParseInputEvent(new InputEventMouseButton{Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=false,Position=potion,GlobalPosition=potion});
            await Frame();Require(app.World.Items.Count("player","potion")==potions-1,"one physical item tap plus emulated mouse");
            Touch(8,true,attack);Tap("menu");await Frame();
            Require(app.Ui.MenuOpen && app.TouchMovement==Vector2.Zero && app.Ui.Stick.Finger<0,"menu cancels movement and pending action");
            int prior=attacks;Touch(8,false,attack);app.ResumeGame();await Frame();
            Touch(8,false,attack);await Frame();Require(attacks==prior,"hidden control release remains canceled after resume");
            Touch(8,true,attack);await Frame();
            app._Notification((int)Node.NotificationApplicationFocusOut);
            app._Notification((int)Node.NotificationApplicationFocusIn);app.ResumeGame();await Frame();
            Touch(8,false,attack);await Frame();Require(attacks==prior && app.TouchMovement==Vector2.Zero,$"focus loss clears held touches (attacks {attacks}/{prior}, movement {app.TouchMovement})");
            Tap("inventory");await Frame();Require(app.Ui.BlocksInput,"stable Pack action opens panel");
        }
        finally{app.World.CombatDiagnostic-=Diagnostic;app.LeaveFeelCourse();app.Ui.PauseForVerification();}
    }

}
