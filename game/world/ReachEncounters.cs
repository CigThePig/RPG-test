using V = System.Numerics.Vector2;
using System.Text.Json.Serialization;
namespace Mosslight;

public sealed class ReachMember
{
    public string Id { get; set; } = "";
    public string Profile { get; set; } = "";
    public int Tile { get; set; }
    public float FacingX { get; set; }
    public float FacingY { get; set; }
    public bool Sleeper { get; set; }
}
public sealed class ReachSite
{
    public string Id { get; set; } = "";
    public string Template { get; set; } = "";
    public string Habitat { get; set; } = "";
    public int Tile { get; set; }
    public int Danger { get; set; }
    public bool Discovered { get; set; }
    public List<ReachMember> Members { get; set; } = new();
}
public sealed record ReachTemplate(string Id, string[] Profiles, int Danger, string Evidence);

/// <summary>Frozen, independent placement policy. Saved plans never depend on render chunks.</summary>
public static class ReachEncounters
{
    public const int Revision = 1;
    public const int TargetSites = 42;
    public const float Spacing = 600;
    public const float TerritoryRadius = 192;
    public static readonly ReachTemplate[] Templates = {
        new("lone-hunter", new[]{"mire"},1,"Resting hollow"),
        new("hunting-pack", new[]{"prowler","prowler","prowler"},2,"Trampled hunting ground"),
        new("rocky-guardian", new[]{"brute"},2,"Scattered stones"),
        new("poisoned-shore", new[]{"spitter","mire"},2,"Acid-scarred bank"),
        new("woodland-ambush", new[]{"stalker"},2,"Disturbed undergrowth"),
        new("alarmed-ground", new[]{"howler","prowler","prowler"},3,"Lookout roost"),
        new("defended-refuge", new[]{"shellback","mender"},3,"Overgrown refuge"),
        new("coiled-threat", new[]{"lasher"},2,"Shed scales"),
        new("pack-leader", new[]{"alpha","prowler","prowler","prowler"},4,"Remote den")
    };
    public sealed record Candidate(int Tile,int Water,int Rock,int Road,int Lanes,int LongLane,float Forest,float Distance,int[] Spawns);
    public sealed class Diagnostics
    {
        public int Candidates {get;set;}
        public Dictionary<string,int> Rejected {get;set;} = new();
        public double Milliseconds {get;set;}
        public void Reject(string reason)=>Rejected[reason]=Rejected.GetValueOrDefault(reason)+1;
    }
    static readonly V[] Directions = Enumerable.Range(0,8).Select(i=>new V(MathF.Cos(i*MathF.PI/4),MathF.Sin(i*MathF.PI/4))).ToArray();
    // Conservative ground-only footprint covers every authored PPC body (radius <= 11).
    static ActorState Probe()=>new(){Radius=11};
    static int[] Distances(RegionState r,Func<int,bool> source)
    {
        var d=Enumerable.Repeat(9999,r.Tiles.Length).ToArray(); var q=new Queue<int>();
        for(int i=0;i<d.Length;i++)if(source(i)){d[i]=0;q.Enqueue(i);}
        while(q.Count>0){int at=q.Dequeue();foreach(int n in r.Neighbours(at))if(d[n]>d[at]+1){d[n]=d[at]+1;q.Enqueue(n);}}
        return d;
    }
    public static bool Protected(RegionState r,V p,float margin=300)
    {
        if(V.Distance(p,r.Center(r.Entrance))<720)return true;
        if(r.Features.Keys.Any(i=>V.Distance(p,r.Center(i))<240))return true;
        return r.Town?.Houses.Any(h=>p.X>h.X*24-margin && p.X<(h.X+h.Width)*24+margin && p.Y>h.Y*24-margin && p.Y<(h.Y+h.Height)*24+margin)==true;
    }
    public static bool ObscuresCombat(RegionState r,int x,int y,string prop)
    {
        if(prop=="rock1")return false;
        var at=new V((x+.5f)*24,(y+.5f)*24);
        foreach(var s in r.ReachSites)
        {
            if(V.DistanceSquared(at,r.Center(s.Tile))>500*500)continue;
            bool Covers(V point)=>MathF.Abs(at.X-point.X)<48 && at.Y>point.Y-24 && at.Y<point.Y+156;
            if(s.Members.Any(m=>Covers(r.Center(m.Tile))))return true;
            if(s.Template is "rocky-guardian" or "poisoned-shore")
            {
                var m=s.Members[0];var home=r.Center(s.Tile);var d=new V(m.FacingX,m.FacingY);
                for(int step=1;step<=5;step++)if(Covers(home+d*(step*48)))return true;
            }
        }
        return false;
    }
    public static void ValidateSaved(SaveData data)
    {
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in data.Regions.Values)
        {
            if(r.ReachRevision is <0 or >Revision || r.ReachSites==null || r.ReachSites.Count>64 || r.ReachRevision==0&&r.ReachSites.Count>0)
                throw new InvalidDataException("Invalid encounter plan");
            foreach(var s in r.ReachSites)
            {
                var template=Templates.FirstOrDefault(t=>t.Id==s.Template);
                if(r.Id!="reach" || template==null || s.Tile<0 || s.Tile>=r.Tiles.Length || s.Id!=$"reach:ecology:{Revision}:{s.Tile}" ||
                    !identities.Add(s.Id) || s.Danger!=template.Danger || s.Members==null || s.Members.Count!=template.Profiles.Length)
                    throw new InvalidDataException("Invalid encounter site");
                for(int i=0;i<s.Members.Count;i++)
                {
                    var m=s.Members[i];
                    if(m.Id!=s.Id+":"+i || !identities.Add(m.Id) || m.Profile!=template.Profiles[i] || m.Tile<0 || m.Tile>=r.Tiles.Length ||
                        !float.IsFinite(m.FacingX)||!float.IsFinite(m.FacingY)||new V(m.FacingX,m.FacingY).LengthSquared()<.9f ||
                        !r.CanStand(r.Center(m.Tile),Probe()) || Protected(r,r.Center(m.Tile)))throw new InvalidDataException("Invalid encounter home");
                    if(data.Actors.TryGetValue(m.Id,out var a) && (a.EncounterSite!=s.Id || a.Region!=r.Id || a.CombatProfile!=m.Profile))
                        throw new InvalidDataException("Encounter identity mismatch");
                    // Discovery is saved before first activation: missing slots are legitimately still unborn.
                }
            }
        }
        foreach(var a in data.Actors.Values.Where(a=>a.EncounterSite!=""))
            if(!identities.Contains(a.Id)||!identities.Contains(a.EncounterSite))throw new InvalidDataException("Orphan encounter actor");
    }
    public static List<ReachSite> Plan(RegionState r,out Diagnostics report,int target=TargetSites)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew(); report=new(); var actor=Probe();
        var water=Distances(r,i=>r.Tiles[i]==Terrain.Water);
        var rock=Distances(r,i=>r.Tiles[i]==Terrain.Rock);
        // Road-like earth has a long narrow run; broad bare clearings alone don't qualify.
        var road=Distances(r,i=>r.Tiles[i]==Terrain.Earth && r.Neighbours(i).Count(n=>r.Tiles[n]==Terrain.Earth)>=3);
        var connected=new HashSet<int>{r.Entrance};var queue=new Queue<int>();queue.Enqueue(r.Entrance);
        while(queue.Count>0){int at=queue.Dequeue();foreach(int n in r.Neighbours(at))if(!connected.Contains(n)&&r.CanTravel(r.Center(at),r.Center(n),actor)){connected.Add(n);queue.Enqueue(n);}}
        var candidates=new List<Candidate>();
        for(int y=8;y<r.Height-8;y+=4)for(int x=8;x<r.Width-8;x+=4)
        {
            report.Candidates++;int at=y*r.Width+x;var p=r.Center(at);
            if(!connected.Contains(at)||!r.CanStand(p,actor)){report.Reject("unreachable-or-footprint");continue;}
            if(Protected(r,p)){report.Reject("settlement-or-objective");continue;}
            int lanes=Directions.Count(d=>r.CanTravel(p,p+d*96,actor));
            int longLane=Array.FindIndex(Directions,d=>r.CanTravel(p,p+d*240,actor)&&r.CanTravel(p,p-d*96,actor)&&r.CanTravel(p,p+new V(-d.Y,d.X)*72,actor)&&r.CanTravel(p,p-new V(-d.Y,d.X)*72,actor));
            if(lanes<5){report.Reject("insufficient-escape-space");continue;}
            // Homes must share clear ground, not simply belong to the same large flood component.
            var slots=Directions.Select(d=>r.Index(p+d*60)).Distinct().Where(n=>n>=0&&connected.Contains(n)&&!Protected(r,r.Center(n))&&r.CanTravel(p,r.Center(n),actor)&&Directions.Count(d=>r.CanTravel(r.Center(n),r.Center(n)+d*48,actor))>=4).ToArray();
            if(slots.Length<4){report.Reject("group-homes");continue;}
            candidates.Add(new(at,water[at],rock[at],road[at],lanes,longLane,LivingReachGeography.Forest(r.Seed,x,y),V.Distance(p,r.Center(r.Entrance)),slots));
        }
        var result=new List<ReachSite>();
        float Score(Candidate c,ReachTemplate t)
        {
            // Complexity grows with travel distance, without altering health or damage.
            if(t.Danger>1&&c.Distance<1350 || t.Danger>2&&c.Distance<2200 || t.Danger>3&&c.Distance<3500)return -1;
            return t.Id switch {
                "lone-hunter"=> c.Distance<1800?8-c.Distance/600:1,
                "hunting-pack"=>c.Forest>.35f?5+c.Lanes*.2f:-1,
                "rocky-guardian"=>c.LongLane>=0&&c.Rock<=10?10-c.Rock*.5f:-1,
                "poisoned-shore"=>c.Water is >=2 and <=7?12-c.Water:-1,
                "woodland-ambush"=>c.Forest>.5f&&c.Road<=8?7+c.Forest:-1,
                "alarmed-ground"=>c.Forest>.25f&&c.Lanes>=6?5+c.Forest:-1,
                "defended-refuge"=>c.Forest>.4f&&c.Rock<=14?8-c.Rock*.2f:-1,
                "coiled-threat"=>c.LongLane>=0&&(c.Water<10||c.Rock<10)?6:-1,
                "pack-leader"=>c.Forest>.45f&&c.Road>5?7+c.Forest:-1,
                _=>-1};
        }
        foreach(var t in Templates)report.Rejected["unsuitable/"+t.Id]=candidates.Count(c=>Score(c,t)<0);
        // Reserve diversity first; subsequent rounds favor the least represented suitable template.
        for(int round=0;round<target;round++)
        {
            var ordered=Templates.OrderBy(t=>result.Count(s=>s.Template==t.Id)).ThenBy(t=>Array.IndexOf(Templates,t));
            bool placed=false;
            foreach(var t in ordered)
            {
                var best=candidates.Where(c=>Score(c,t)>=0&&result.All(s=>V.Distance(r.Center(s.Tile),r.Center(c.Tile))>=Spacing))
                    .OrderByDescending(c=>Score(c,t)+EnemyMind.Hash01(r.Seed,c.Tile,World.StableId(t.Id))*.8f).ThenBy(c=>c.Tile).FirstOrDefault();
                if(best==null)continue;
                string id=$"reach:ecology:{Revision}:{best.Tile}";
                int lane=best.LongLane>=0?best.LongLane:0;var facing=Directions[lane];
                var site=new ReachSite{Id=id,Tile=best.Tile,Template=t.Id,Danger=t.Danger,Habitat=best.Water<=7?"shore":best.Rock<=14?"rocky fringe":best.Forest>.5f?"woodland":"clearing"};
                for(int i=0;i<t.Profiles.Length;i++)
                {
                    // Main creature owns the tested lane. Support stands behind/to one side.
                    int tile=i==0?best.Tile:best.Spawns[(lane+3+i*2)%best.Spawns.Length];
                    if(site.Members.Any(m=>m.Tile==tile))tile=best.Spawns.First(n=>site.Members.All(m=>m.Tile!=n));
                    site.Members.Add(new(){Id=id+":"+i,Profile=t.Profiles[i],Tile=tile,FacingX=facing.X,FacingY=facing.Y,Sleeper=t.Id=="lone-hunter"||t.Id=="hunting-pack"&&i==2});
                }
                result.Add(site);placed=true;break;
            }
            if(!placed){report.Reject("spacing-or-template-exhaustion");break;}
        }
        report.Milliseconds=timer.Elapsed.TotalMilliseconds;return result;
    }
}

/// <summary>Small spatial index, staggered activation, exact dormant actor snapshots. No respawn rule.</summary>
public sealed class ReachPopulation
{
    public const int MaximumActors=24;
    public const float Activation=900, Deactivation=1250;
    public Dictionary<string,ActorState> Dormant {get;set;}=new();
    public double LastUpdateMilliseconds {get;private set;}
    public double WorstActivationMilliseconds {get;private set;}
    RegionState? indexed;
    readonly Dictionary<(int,int),List<ReachSite>> buckets=new();
    public void ResetIndex(){indexed=null;buckets.Clear();}
    IEnumerable<ReachSite> Nearby(RegionState r,V at)
    {
        if(!ReferenceEquals(indexed,r))
        {
            indexed=r;buckets.Clear();foreach(var s in r.ReachSites){var p=r.Center(s.Tile);var key=((int)p.X/600,(int)p.Y/600);if(!buckets.TryGetValue(key,out var b))buckets[key]=b=new();b.Add(s);}
        }
        int x=(int)at.X/600,y=(int)at.Y/600;
        for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)if(buckets.TryGetValue((x+dx,y+dy),out var b))foreach(var s in b)yield return s;
    }
    public void Update(World w,bool force=false)
    {
        if(!force&&w.Tick%6!=0)return;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        bool Pinned(ActorState a)=>a.Alive&&(a.Awareness is Awareness.Alerted or Awareness.Engaged or Awareness.Searching || a.State is ActionState.Windup or ActionState.Recovery or ActionState.Dodging || a.Statuses.Count>0)
            ||w.Actors.Values.Any(b=>b.Alive&&b.Id!=a.Id&&b.Target==a.Id)
            ||w.Actors.Values.Any(b=>b.Statuses.Any(s=>s.Source==a.Id))
            ||w.Projectiles.Any(p=>p.Source==a.Id||p.Target==a.Id)||w.Hazards.Any(h=>h.Source==a.Id);
        foreach(var a in w.Actors.Values.Where(a=>a.EncounterSite!="").ToArray())
            if((a.Region!=w.Player.Region||V.Distance(a.Position,w.Player.Position)>Deactivation)&&!Pinned(a))
            {Dormant[a.Id]=a;w.Actors.Remove(a.Id);}
        if(w.Player.Region=="reach"&&w.RegionsById.TryGetValue("reach",out var r)&&r.ReachRevision>0)
        {
            int active=w.Actors.Values.Count(a=>a.EncounterSite!="");
            foreach(var s in Nearby(r,w.Player.Position).Where(s=>V.Distance(r.Center(s.Tile),w.Player.Position)<Activation).OrderBy(s=>V.DistanceSquared(r.Center(s.Tile),w.Player.Position)).ThenBy(s=>s.Id,StringComparer.Ordinal))
            {
                int missing=s.Members.Count(m=>!w.Actors.ContainsKey(m.Id));
                if(active+missing>MaximumActors)continue;
                s.Discovered=true;
                foreach(var m in s.Members)
                {
                    if(w.Actors.ContainsKey(m.Id))continue;
                    ActorState a;
                    if(Dormant.Remove(m.Id,out var saved))a=saved;
                    else
                    {
                        a=EnemyRoster.Create(w,m.Id,m.Profile,"reach",r.Center(m.Tile),new(m.FacingX,m.FacingY),(ulong)(uint)World.StableId(m.Id)^((ulong)(uint)w.Seed<<32));
                        a.EncounterSite=s.Id;a.Sleeper=m.Sleeper;
                        a.Name=EnemyRoster.Archetypes[m.Profile].Name.Replace("Ruin ","Reach ");
                    }
                    w.Actors.Add(a.Id,a);active++;
                    LastUpdateMilliseconds=timer.Elapsed.TotalMilliseconds;
                    WorstActivationMilliseconds=Math.Max(WorstActivationMilliseconds,LastUpdateMilliseconds);
                    return; // At most one PPC body initialized per six fixed ticks.
                }
            }
        }
        LastUpdateMilliseconds=timer.Elapsed.TotalMilliseconds;
    }
}
