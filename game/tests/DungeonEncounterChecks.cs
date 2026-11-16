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
            r.OpenDoors.UnionWith(r.Features.Where(x=>RegionState.IsGate(x.Value)).Select(x=>x.Key));
            // One live inhabitant of every authored kind.
            var enemies=new[]{"prowler","brute","spitter","stalker","howler"}.Select(id=>w.Actors.Values.First(a=>a.Id.StartsWith("ruin:spawn:")&&a.CombatProfile==id)).ToArray();
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
                    enemy.Region="ruin";w.Cancel(enemy);enemy.Health=enemy.MaxHealth;enemy.Statuses.Clear();
                    // Posted at the far side of the doorway, looking through it: noticing is production perception.
                    var from=center-direction*70;EnemyMind.Resettle(enemy,from,direction);
                    var target=center+direction*70;p.X=target.X;p.Y=target.Y;p.Invulnerable=1000;w.Cancel(p);
                    bool attack=false,released=false,crossed=false,alerted=false;float minDistance=V.Distance(enemy.Position,p.Position);
                    void Observe(CombatPresentationEvent e)
                    {
                        if(e.ActorId!=enemy.Id)return;
                        if(e.Kind==CombatPresentationKind.AttackStarted)attack=true;
                        if(e.Kind==CombatPresentationKind.Alerted)alerted=true;
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
                            // A dormant stalker ignores a distant intruder; walk the player in after two seconds.
                            if(enemy.CombatProfile=="stalker"&&tick==120)
                            {
                                check(!alerted&&enemy.Awareness==Awareness.Unaware&&enemy.Activity=="ambush","dormant stalker ignores a distant intruder "+direction);
                                var near=center+direction*8;p.X=near.X;p.Y=near.Y;
                            }
                        }
                    }
                    finally{w.Presentation-=Observe;}
                    if(enemy.CombatProfile=="prowler")check(crossed&&attack,"live prowler crosses doorway and bites "+direction);
                    if(enemy.CombatProfile=="brute")check(minDistance<100&&attack,"live brute repositions and starts production cone "+direction);
                    if(enemy.CombatProfile=="spitter")check(released,"live spitter maintains range and releases projectile through doorway "+direction);
                    if(enemy.CombatProfile=="stalker")check(alerted&&attack,"live stalker bursts from ambush and stings "+direction);
                    if(enemy.CombatProfile=="howler")check(alerted&&minDistance<135&&minDistance>30,"live howler closes to its hovering band without crowding "+direction);
                    check(alerted,"live "+enemy.CombatProfile+" notices an intruder through the doorway "+direction);
                }
            }
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);}
    }
}
