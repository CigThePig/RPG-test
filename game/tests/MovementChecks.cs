using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Core;
using PixelCreatures.Core.Motion;
using V=System.Numerics.Vector2;
namespace Mosslight;
public static class MovementChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        var w=app.World;var saved=Persistence.Capture(w,app.Narrative);
        try
        {
            var r=new RegionState {Id="feel-regression",Width=64,Height=48,Tiles=Enumerable.Repeat(Terrain.Grass,64*48).ToArray()};
            w.RegionsById[r.Id]=r;var p=w.Player;p.Region=r.Id;
            float baseSpeed=w.Stat(p,"speed");
            foreach(var feel in MovementFeel.Presets)
            {
                var speeds=new List<float>();
                for(int direction=0;direction<8;direction++)
                {
                    p.X=500;p.Y=500;p.State=ActionState.Idle;
                    var input=feel.Convert(Vector2.Right.Rotated(direction*Mathf.Pi/4));
                    w.PlayerMove(new V(input.X,input.Y),1f/60,feel.Scale,feel.ScreenUniform);
                    speeds.Add(new V(w.ActualVelocity.X,w.ActualVelocity.Y*.5f).Length());
                }
                check(feel.Id=="baseline" ? speeds.Max()/speeds.Min()>1.99 : speeds.Max()/speeds.Min()<1.001,"projected directional speed contract "+feel.Id);
            }
            foreach(int hz in new[]{30,60,120})
            {
                p.X=500;p.Y=500;p.State=ActionState.Idle;
                for(int i=0;i<hz;i++)w.PlayerMove(new V(1,0),1f/hz,2.4f,true);
                check(Math.Abs(p.X-500-baseSpeed*2.4f)<.05,"elapsed-time independent displacement "+hz+"Hz");
            }
            foreach(var state in new[]{ActionState.Windup,ActionState.Recovery,ActionState.Staggered,ActionState.Dead})
            {
                p.State=state;var before=p.Position;w.PlayerMove(new V(1,0),1f/60,2.4f,true);
                check(p.Position==before && w.ActualVelocity==V.Zero && w.RequestedVelocity==V.Zero,"action gate clears stale telemetry "+state);
            }
            p.State=ActionState.Idle;p.X=500;p.Y=500;
            var start=p.Position;
            w.PlayerMove(new V(float.NaN,0),1f/60);w.PlayerMove(new V(1,0),float.NaN);
            check(p.Position==start,"invalid movement cannot mutate position");
            w.PlayerMove(new V(1,0),1f/60,2.4f,true);w.PlayerMove(V.Zero,1f/60,2.4f,true);
            check(w.ActualVelocity==V.Zero && p.State==ActionState.Idle,"release stops immediately with no stale velocity");
            // Full footprint through a one-tile passage, then reject the same gap for a larger body.
            for(int y=0;y<48;y++)if(y!=24)r.Tiles[y*64+30]=Terrain.Rock;
            foreach(float radius in new[]{6f,9f,11f})
            {
                p.Radius=radius;p.X=680;p.Y=588;p.State=ActionState.Idle;bool clear=true;
                for(int i=0;i<120;i++){w.PlayerMove(new V(1,0),1f/60,3.2f,true);clear &=r.CanStand(p.Position,p);}
                check(clear && p.X>744+radius,"production player crosses narrow gap radius "+radius);
                p.X=680;p.Y=300;
                for(int i=0;i<60;i++)w.PlayerMove(new V(.7071f,1.4142f),1f/60,3.2f,true);
                check(r.CanStand(p.Position,p) && p.X<=720-radius+.01 && p.Y>400,"high speed diagonal wall slide retains tangent radius "+radius);
                float x=p.X;w.PlayerMove(new V(-1,0),1f/60,3.2f,true);
                check(p.X<x,"contact permits immediate reversal radius "+radius);
            }
            p.Radius=13;p.X=680;p.Y=588;
            for(int i=0;i<120;i++)w.PlayerMove(new V(1,0),1f/60,3.2f,true);
            check(p.X<720 && r.CanStand(p.Position,p),"oversize body cannot exploit one-tile gap");
            var model=CreatureRuntime.Build(CreatureRuntime.Sample("biped",42));
            var motor=new CreatureMotor(model,CreatureRuntime.Registry,new MotorOptions {InternalDrive=false});
            motor.Teleport(Vec3.Zero,0);
            for(int i=0;i<4;i++){motor.SetWorldPosition(new Vec3(8,0,0),new Vec3(120,0,0));motor.Step(1.0/60);}
            check(Math.Abs(motor.Context.Speed-120)<.001,"animation retains measured velocity across motor catch-up steps");
            motor.SetWorldPosition(new Vec3(8,0,0),Vec3.Zero);motor.Step(1.0/60);
            check(motor.Context.Speed==0,"animation velocity stops with external displacement");
            motor.Teleport(Vec3.Zero,0);
            motor.Options.FaceVelocity=false;
            for(int i=0;i<15;i++){motor.SetWorldPosition(new Vec3(-15,0,0),new Vec3(-900,0,0));motor.Step(1.0/60);}
            check(Math.Abs(motor.Yaw)<.001,"forced knockback velocity cannot turn a facing-locked creature");
            motor.Options.FaceVelocity=true;
            for(int i=0;i<60;i++){motor.SetWorldPosition(new Vec3(-15,0,0),new Vec3(-100,0,0));motor.Step(1.0/60);}
            check(Math.Abs(motor.Yaw)>1,"intentional movement restores velocity facing after stagger");
            motor.TriggerGameplayAction(.14,.24);
            for(int i=0;i<22;i++)motor.Step(1.0/60);
            check(motor.ActionActive,"combat pose spans windup and recovery");
            motor.Step(1.0/60);
            check(!motor.ActionActive,"combat pose ends with 380ms gameplay action, not species one-second animation");
            motor.TriggerGameplayAction(.14,.24);
            motor.TriggerHit(Vec3.UnitX);
            check(!motor.ActionActive,"incoming hit cancels interrupted attack pose instead of completing a ghost swing");
            app.RebuildViews(); // Bind presentation to the current restored authoritative actors.
            var view=app.Views["player"];
            var viewMotor=view.Creature.Motor!;
            view.SnapPosition();view.Creature.ClearFacingRequest();
            viewMotor.Teleport(new Vec3(p.X,0,p.Y),0);
            p.State=ActionState.Staggered;p.X-=15;view.Cue("hit");view.Sync(1f/60);
            for(int i=0;i<20;i++)
            {
                if(i==14)p.State=ActionState.Idle;
                view.Sync(1f/60);
                // Legacy baseline derives and smooths velocity from external positions.
                viewMotor.SetWorldPosition(new Vec3(p.X,0,p.Y));viewMotor.Step(1.0/60);
            }
            check(Math.Abs(viewMotor.Yaw)<.001,"stationary post-stagger view ignores residual baseline motor velocity");
            Persistence.Restore(w,app.Narrative,saved);
            string beforeCourse=Persistence.Capture(w,app.Narrative);
            var autosave=File.Exists(Persistence.AutosavePath) ? File.ReadAllBytes(Persistence.AutosavePath) : null;
            app.BeginFeelCourse();
            check(app.InFeelCourse && w.Player.Region=="feel-lab" && w.Player.X==300,"phone controlled course uses reproducible production starting state");
            w.Items.Add("player","potion");w.Player.X=500;
            app._Notification((int)Node.NotificationApplicationPaused);
            app._Notification((int)Node.NotificationApplicationResumed);
            check(autosave==null ? !File.Exists(Persistence.AutosavePath) : File.ReadAllBytes(Persistence.AutosavePath).SequenceEqual(autosave),"controlled course cannot overwrite normal background autosave");
            app.BeginFeelCourse();
            check(w.Player.X==300,"course reset restores the comparison start");
            app.LeaveFeelCourse();
            check(!app.InFeelCourse && Persistence.Capture(w,app.Narrative)==beforeCourse,"course return restores normal world and inventory exactly");
            app.Ui.PauseForVerification();
        }
        finally {Persistence.Restore(w,app.Narrative,saved);}
    }
}
