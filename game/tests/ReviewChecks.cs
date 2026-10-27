using Godot;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Regressions for Codex review 5661327776: body clearance and all session-entry routes.</summary>
public static class ReviewChecks
{
    public static void Run(Foundation app, Action<bool,string> check)
    {
        var w=app.World;
        string original=Persistence.Capture(w,app.Narrative);
        string autosave=Persistence.AutosavePath;
        byte[]? priorAutosave=File.Exists(autosave) ? File.ReadAllBytes(autosave) : null;
        bool initialSession=app.SessionStarted;
        try
        {
            // Exercise the real pause/autosave path while preserving any user's prior autosave.
            if(!initialSession)
            {
                File.Delete(autosave);
                app._Notification((int)Node.NotificationApplicationPaused);
                check(!File.Exists(autosave),"untouched initial menu does not overwrite a recovery save");
                app._Notification((int)Node.NotificationApplicationResumed);
            }
            foreach(string route in new[]{"Escape","Pack","Journal","Character","Map"})
            {
                app.Ui.PauseForVerification();app.Ui.Update();
                if(route=="Escape") app._UnhandledInput(new InputEventAction { Action="menu",Pressed=true });
                else
                {
                    var button=app.Ui.GetChildren().OfType<Control>().First().FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==route && b.IsVisibleInTree());
                    button.EmitSignal(Button.SignalName.Pressed);
                    app.Ui.ClosePanels();
                }
                check(!app.GameplayBlocked,"initial-menu route enters gameplay: "+route);
                long before=w.Tick;
                app._PhysicsProcess(1.0/60);
                check(w.Tick>before && app.SessionStarted,"first simulation tick establishes recoverable session: "+route);
                File.Delete(autosave);
                app._Notification((int)Node.NotificationApplicationPaused);
                check(File.Exists(autosave) && Persistence.Parse(File.ReadAllText(autosave),w.Content).Tick==w.Tick,"background autosave captures progress after "+route);
                app._Notification((int)Node.NotificationApplicationResumed);
                Persistence.Restore(w,app.Narrative,original);
            }

            var r=new RegionState { Id="review-clearance",Width=12,Height=12,PlacementRevision=1,Tiles=Enumerable.Repeat(Terrain.Grass,144).ToArray(),Entrance=26 };
            w.RegionsById[r.Id]=r;
            var p=w.Player;p.Region=r.Id;
            var water=r.Center(78);r.Tiles[78]=Terrain.Water;
            p.X=p.HomeX=water.X;p.Y=p.HomeY=water.Y;
            check(w.SetAppearance(p,new Appearance { Family="winged",Seed=42 }) && p.Flies && p.Position==water,"flying body can occupy water before workshop change");
            check(w.SetAppearance(p,new Appearance { Family="quadruped",Seed=42 }) && !p.Flies && !p.Swims && p.Position!=water && r.CanStand(p.Position,p),"losing flight over water relocates to clear land");
            check(r.CanStand(new V(p.HomeX,p.HomeY),p),"anatomy relocation also repairs an invalid patrol home");
            var next=r.Neighbours(r.Index(p.Position)).Select(r.Center).First(q=>r.CanTravel(p.Position,q,p));
            check(w.Move(p,V.Normalize(next-p.Position)*2),"relocated ground creature can actually move");

            var bodies=CreatureRuntime.Registry.Families.Select(f=>new ActorState { Appearance=new Appearance { Family=f.Id,Seed=42 } }).ToArray();
            foreach(var body in bodies) AnatomyAdapter.Apply(body);
            var small=bodies.MinBy(a=>a.Radius)!;var large=bodies.MaxBy(a=>a.Radius)!;
            check(large.Radius>small.Radius+.5f,"radius-growth regression uses genuinely different PPC footprints");
            w.SetAppearance(p,small.Appearance);
            r.Tiles[78]=Terrain.Rock;
            p.X=144-small.Radius-.25f;p.Y=156;
            var beside=p.Position;
            check(r.CanStand(beside,p),"small footprint fits beside obstacle before anatomy growth");
            check(w.SetAppearance(p,large.Appearance) && p.Position!=beside && r.CanStand(p.Position,p),"larger anatomy beside rock relocates without getting stuck");

            Array.Fill(r.Tiles,Terrain.Water);
            check(w.SetAppearance(p,new Appearance { Family="winged",Seed=42 }),"water-only fixture starts with a viable flying body");
            string beforeRejected=Persistence.Capture(w,app.Narrative);
            var rejected=new Appearance { Family="quadruped",Seed=1337 };
            check(!w.SetAppearance(p,rejected) && Persistence.Capture(w,app.Narrative)==beforeRejected && rejected.GenomeJson=="","impossible anatomy change rejects atomically without mutating caller, actor, equipment or events");
            app.ChangeAppearance("quadruped",1337);
            check(Persistence.Capture(w,app.Narrative)==beforeRejected,"workshop presents safe rejection instead of committing an invalid body");
        }
        finally
        {
            app._Notification((int)Node.NotificationApplicationResumed);
            Persistence.Restore(w,app.Narrative,original);
            if(priorAutosave==null) File.Delete(autosave); else File.WriteAllBytes(autosave,priorAutosave);
            app.Ui.PauseForVerification();
        }
    }
}
