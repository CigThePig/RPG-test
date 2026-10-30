using V=System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Deterministic real-input dungeon expedition; setup never crosses a lock.</summary>
public sealed class DungeonTraversalReplay
{
    readonly World world;
    readonly RegionState region;
    readonly Action<string> tap;
    readonly Queue<int> gates;
    Queue<V> route=new();
    string action="dodge";
    int target=-1;
    bool issued;
    int delay=5;
    public bool Complete { get; private set; }
    public int Unlocks { get; private set; }
    public HashSet<int> Rooms { get; }=new();
    public DungeonTraversalReplay(World w,Action<string> send)
    {
        world=w;region=w.RegionsById["ruin"];tap=send;
        if(!DungeonValidator.TrySolve(region,out var plan))throw new InvalidOperationException("Replay mission not solvable");
        gates=new Queue<int>(plan);
        int keys=w.Items.Count("player","dungeon_key");if(keys>0)w.Items.Take("player","dungeon_key",keys);
    }
    void Aim(int at,string kind)
    {
        target=at;action=kind;issued=false;
        var p=world.Player;var path=region.Path(p.Position,region.Center(at),p);
        if(path.Count==0 && region.Index(p.Position)!=at)throw new InvalidOperationException("Dungeon replay route missing: "+kind+" "+at);
        route=new Queue<V>(path.Select(region.Center));
        // Stop at the exact interaction point, including same-tile endpoints.
        if(route.Count==0)route.Enqueue(region.Center(at));
    }
    public V Tick()
    {
        var p=world.Player;
        if(Complete)return V.Zero;
        if(p.Region=="ruin")Rooms.Add((int)(p.Y/(11*24))*1000+(int)(p.X/(11*24)));
        if(delay-->0)return V.Zero;
        if(action=="dodge")
        {
            if(!issued){tap("dodge");issued=true;delay=35;return V.Zero;}
            if(p.State==ActionState.Dodging)throw new InvalidOperationException("Dungeon roll did not finish");
            // The authored corner quadrant may contain a pillar; its outer ring
            // is a protected lane and requires a real route around architecture.
            Aim(region.Entrance+4*region.Width+4,"corner");
        }
        while(route.Count>0 && V.Distance(p.Position,route.Peek())<3)route.Dequeue();
        if(route.Count>0)return V.Normalize(route.Peek()-p.Position);
        if(action is "key" or "gate" or "relic" or "exit")
        {
            if(!issued){tap("interact");issued=true;delay=3;return V.Zero;}
            bool done=action=="gate"?region.GateAt(target).All(region.OpenDoors.Contains):action=="exit"?p.Region=="reach":region.Depleted.Contains(target);
            if(!done)throw new InvalidOperationException("Dungeon replay interaction failed: "+action+" "+world.InteractionFeedback);
            if(action=="gate"){Unlocks++;gates.Dequeue();}
            if(action=="exit"){Complete=true;return V.Zero;}
        }
        if(region.Depleted.Contains(region.Objective)){Aim(region.Entrance,"exit");return V.Zero;}
        if(gates.Count>0)
        {
            foreach(int key in region.Features.Where(x=>x.Value=="key").Select(x=>x.Key).Order())
                if(!region.Depleted.Contains(key) && region.Path(p.Position,region.Center(key),p).Count>0){Aim(key,"key");return V.Zero;}
            var gate=region.GateAt(gates.Peek());
            int stand=gate.SelectMany(region.Neighbours).Distinct().Order().First(at=>region.CanStand(region.Center(at),p)&&region.Path(p.Position,region.Center(at),p).Count>0);
            // target is a stand, while gate identity must remain its representative.
            Aim(stand,"gate");target=gates.Peek();return V.Zero;
        }
        Aim(region.Objective,"relic");return V.Zero;
    }
}
