using Godot;
using System.Text.Json;
namespace Mosslight;

/// <summary>Repeatable normal-health observation, not a scripted win-rate target.</summary>
public static class EncounterChecks
{
    public static void Run(Foundation app, Action<bool,string> check)
    {
        var w=app.World;
        string before=Persistence.Capture(w,app.Narrative);
        var rows=new List<object>();
        try
        {
            foreach(ulong seed in new ulong[]{1,42,1337})
            foreach(string scenario in new[]{"prowler","brute","spitter","two melee","mixed","four mixed","crowd"})
            {
                app.BeginCombatCourse(scenario,false,seed);
                check(w.Player.MaxHealth<10000 && w.Player.Invulnerable==0,"normal-health lab: "+scenario+"/"+seed);
                int starts=0,hits=0,misses=0,avoided=0,launches=0,collisions=0,deaths=0;
                void Observe(CombatPresentationEvent e)
                {
                    if(e.Kind==CombatPresentationKind.AttackStarted)starts++;
                    if(e.Kind==CombatPresentationKind.Hit)hits++;
                    if(e.Kind==CombatPresentationKind.Miss)misses++;
                    if(e.Kind==CombatPresentationKind.DefensiveAvoidance)avoided++;
                    if(e.Kind==CombatPresentationKind.ProjectileReleased)launches++;
                    if(e.Kind==CombatPresentationKind.ProjectileImpact)collisions++;
                    if(e.Kind==CombatPresentationKind.Death)deaths++;
                }
                int received=0,dealt=0;double spacing=0;int samples=0;
                var intentions=new Dictionary<string,int>();
                void Event(WorldEvent e) { if(e.Kind=="damage") { if(e.Target=="player")received+=e.Count; if(e.Actor=="player")dealt+=e.Count; } }
                w.Presentation+=Observe;w.Events.Published+=Event;
                try
                {
                    // Stationary exposure measures pressure without pretending to represent skilled play.
                    for(int tick=0;tick<720;tick++)
                    {
                        w.Step(1f/60);
                        var enemies=w.Actors.Values.Where(a=>a.Alive && a.Region==w.Player.Region && a.Id!="player").ToArray();
                        foreach(var a in enemies)intentions[a.TacticalIntent]=intentions.GetValueOrDefault(a.TacticalIntent)+1;
                        if(enemies.Length>0){spacing+=enemies.Min(a=>System.Numerics.Vector2.Distance(a.Position,w.Player.Position));samples++;}
                        if(!float.IsFinite(w.Player.X) || enemies.Any(a=>!float.IsFinite(a.X)||!float.IsFinite(a.Y)))throw new InvalidOperationException("Nonfinite encounter position");
                    }
                }
                finally{w.Presentation-=Observe;w.Events.Published-=Event;}
                rows.Add(new{seed,scenario,duration=12,policy="stationary normal-health exposure",starts,hits,misses,avoided,launches,collisions,deaths,received,dealt,playerHealth=w.Player.Health,nearestMean=samples>0?spacing/samples:0,intentionTicks=intentions});
                app.LeaveFeelCourse();
                check(Persistence.Capture(w,app.Narrative)==before,"encounter restore: "+scenario+"/"+seed);
            }
            Directory.CreateDirectory("build");
            File.WriteAllText("build/encounter-measurements.json",JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{if(app.InFeelCourse)app.LeaveFeelCourse();app.Ui.PauseForVerification();}
    }
}
