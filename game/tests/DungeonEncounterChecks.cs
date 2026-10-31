using V=System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Live production archetypes inside generated rooms and through actual doorways.</summary>
public static class DungeonEncounterChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);
        try
        {
            w.RegionsById["ruin"]=Regions.Dungeon(42);w.Enter(w.Player,"ruin");
            var r=w.RegionsById["ruin"];var p=w.Player;
            r.OpenDoors.UnionWith(r.Features.Where(x=>x.Value=="lock").Select(x=>x.Key));
            var enemies=w.Actors.Values.Where(a=>a.Id.StartsWith("ruin:spawn:")).Take(3).ToArray();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            foreach(var direction in new[]{V.UnitX,V.UnitY})
            {
                // A real corridor crossing: its straight run reaches past either side.
                var passage=r.Passages.FirstOrDefault(q=>q.Dx==(int)direction.X && q.Dy==(int)direction.Y &&
                    enemies.All(e=>r.CanTravel(r.Center(q.Y*r.Width+q.X)+direction*12-direction*70,r.Center(q.Y*r.Width+q.X)+direction*12+direction*70,e)));
                check(passage!=null,"generated encounter has cardinal corridor crossing "+direction);
                if(passage==null)continue;
                var center=r.Center(passage.Y*r.Width+passage.X)+direction*12;
                foreach(var enemy in enemies)
                {
                    foreach(var a in enemies)a.Region="reach";
                    enemy.Region="ruin";w.Cancel(enemy);enemy.Health=enemy.MaxHealth;enemy.Statuses.Clear();enemy.Cooldowns.Clear();enemy.NextTacticalTime=0;
                    var from=center-direction*70;enemy.X=from.X;enemy.Y=from.Y;
                    var target=center+direction*70;p.X=target.X;p.Y=target.Y;p.Invulnerable=1000;w.Cancel(p);
                    bool attack=false,released=false,crossed=false;float minDistance=V.Distance(enemy.Position,p.Position);
                    void Observe(CombatPresentationEvent e)
                    {
                        if(e.ActorId!=enemy.Id)return;
                        if(e.Kind==CombatPresentationKind.AttackStarted)attack=true;
                        if(e.Kind==CombatPresentationKind.ProjectileReleased)released=true;
                    }
                    w.Presentation+=Observe;
                    try
                    {
                        for(int tick=0;tick<480;tick++)
                        {
                            w.Step(1f/60);
                            if(!r.CanStand(enemy.Position,enemy))throw new InvalidOperationException("Generated encounter lost enemy clearance: "+enemy.CombatProfile);
                            minDistance=Math.Min(minDistance,V.Distance(enemy.Position,p.Position));
                            crossed|=V.Dot(enemy.Position-center,direction)>0;
                        }
                    }
                    finally{w.Presentation-=Observe;}
                    if(enemy.CombatProfile=="prowler")check(crossed&&attack,"live prowler crosses doorway and bites "+direction);
                    if(enemy.CombatProfile=="brute")check(minDistance<100&&attack,"live brute repositions and starts production cone "+direction);
                    if(enemy.CombatProfile=="spitter")check(released,"live spitter maintains range and releases projectile through doorway "+direction);
                }
            }
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);}
    }
}
