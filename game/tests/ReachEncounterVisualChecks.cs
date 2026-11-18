using Godot;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Rendered production encounters. Four simulation ticks per displayed frame,
/// recorded as a scripted gameplay observation, never presented as device frame pacing.</summary>
public static class ReachEncounterVisualChecks
{
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);
        var window=app.GetWindow();var size=window.Size;var zoom=app.Camera.Zoom;
        string folder="build/ecology/rendered";Directory.CreateDirectory(folder);
        var metrics=new List<object>();
        async System.Threading.Tasks.Task Frame(int n=1){for(int i=0;i<n;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}}
        async System.Threading.Tasks.Task Shot(string name){await Frame(2);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var im=window.GetTexture().GetImage();im.SavePng(folder+"/"+name+".png");}
        async System.Threading.Tasks.Task Settle(){await Frame(3);for(int i=0;i<1800;i++){var s=app.GetTree().GetNodesInGroup("overworld-chunks").OfType<OverworldChunkStreamer>().SingleOrDefault();if(s==null||s.CoversVisible){await Frame(8);return;}await Frame();}throw new InvalidOperationException("Encounter scenery never settled");}
        try
        {
            app.FreezeSimulationForVerification=true;app.ResumeGame();app.Ui.ClosePanels();window.Size=new(2340,1080);window.Position=Vector2I.Zero;app.Camera.Zoom=Vector2.One*2.2f;
            // Retain the real new-game plan, terrain, town and starter equipment.
            foreach(string id in w.Actors.Keys.Where(id=>id!="player"&&!id.StartsWith("npc:")).ToArray())w.Actors.Remove(id);
            await Settle();await Shot("00-safe-town");
            string fresh=Persistence.Capture(w,app.Narrative);
            foreach(var template in ReachEncounters.Templates)
            {
                Persistence.Restore(w,app.Narrative,fresh);var r=w.RegionsById["reach"];
                var site=r.ReachSites.First(s=>s.Template==template.Id);var mainMember=site.Members[0];var direction=new V(mainMember.FacingX,mainMember.FacingY);
                var p=w.Player;var home=r.Center(site.Tile);var approach=home+direction*300;
                if(!r.CanStand(approach,p))approach=home+direction*180;
                p.X=approach.X;p.Y=approach.Y;w.Projectiles.Clear();w.Hazards.Clear();
                for(int i=0;i<60;i++)w.Population.Update(w,true);
                app.RebuildViews();await Settle();await Shot(template.Id+"-approach");
                var foes=site.Members.Select(m=>w.Actors[m.Id]).ToArray();
                var start=home+direction*(template.Id=="rocky-guardian"?180:template.Id=="poisoned-shore"?140:template.Id=="lone-hunter"?20:100);
                if(!r.CanTravel(home,start,p))start=home+direction*48;
                p.X=start.X;p.Y=start.Y;app.Views["player"].SnapPosition();
                var seen=new HashSet<string>();var pending=new Queue<string>();var samples=new List<double>();int hits=0;int calls=0;int tick=0;
                void Observe(CombatPresentationEvent e)
                {
                    if(!site.Members.Any(m=>m.Id==e.ActorId))return;
                    if(e.Kind==CombatPresentationKind.AttackStarted&&seen.Add(e.AbilityId))pending.Enqueue(e.AbilityId);
                    if(e.Kind==CombatPresentationKind.Hit)hits++;
                    if(e.Kind==CombatPresentationKind.Alerted&&e.Outcome=="called")calls++;
                }
                w.Presentation+=Observe;app.Ui.Notify(ReachEncounters.Templates.First(t=>t.Id==site.Template).Evidence);
                try
                {
                    for(;tick<2400&&p.Alive&&foes.Any(a=>a.Alive);tick+=4)
                    {
                        var timer=System.Diagnostics.Stopwatch.StartNew();
                        for(int j=0;j<4;j++)
                        {
                            if(tick>(template.Id=="lone-hunter"?120:240))ReachEncounterChecks.DuelStep(w,foes,tick+j);
                            w.Step(1f/60);
                        }
                        samples.Add(timer.Elapsed.TotalMilliseconds);
                        await Frame();
                        if(pending.Count>0){string action=pending.Dequeue();await Shot(template.Id+"-"+action);}
                        if(tick==400 && template.Id=="coiled-threat")
                        {
                            string snapshot=Persistence.Capture(w,app.Narrative);var health=foes.Select(a=>a.Health).ToArray();
                            p.X=7200;p.Y=5300;w.Population.Update(w,true);await Frame(6);
                            Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();await Settle();
                            if(!site.Members.Select(m=>w.Actors[m.Id].Health).SequenceEqual(health))throw new InvalidOperationException("Rendered partial combat restore changed health");
                            // Restore replaces object references; continue this scene from the saved snapshot below.
                            await Shot("saved-encounter-restored");break;
                        }
                    }
                    await Shot(template.Id+"-outcome");
                }
                finally{w.Presentation-=Observe;}
                samples.Sort();double At(float q)=>samples[Math.Min(samples.Count-1,(int)(samples.Count*q))];
                metrics.Add(new{template=template.Id,simulationSeconds=tick/60f,abilities=seen,hits,calls,health=w.Player.Health,active=w.Actors.Values.Count(a=>a.EncounterSite!=""),views=app.Views.Count,simulationBatchMs=new{p50=At(.5f),p95=At(.95f),p99=At(.99f),worst=samples.Last()},activationWorstMs=w.Population.WorstActivationMilliseconds});
                if(seen.Count==0)throw new InvalidOperationException("No production attacks rendered: "+template.Id);
            }
            File.WriteAllText("build/ecology/rendered-combat.json",JsonSerializer.Serialize(metrics,Content.Json));
            GD.Print("REACH ECOLOGY RENDERED CHECKS PASSED");
        }
        finally{app.FreezeSimulationForVerification=false;window.Size=size;app.Camera.Zoom=zoom;Persistence.Restore(w,app.Narrative,original);app.RebuildViews();app.Ui.PauseForVerification();}
    }
}
