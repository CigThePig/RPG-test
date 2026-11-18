using Godot;
namespace Mosslight;
/// <summary>Saved bridge cells keep continuous water below a three-tile timber deck.
/// A single small static mesh uses the town's wood ramp and upper-left lighting.</summary>
public partial class ReachBridge : Node2D
{
    public RegionState Region {get;init;}=null!;
    readonly TerrainBatch batch=new();
    public override void _Ready()
    {
        // Span both banks with a rectangular deck. The diagonal shoreline must
        // not turn a vertical bridge into a stair-shaped patch of floating planks.
        var remaining=new HashSet<int>(Region.Crossings);var deck=new HashSet<int>();
        while(remaining.Count>0)
        {
            int first=remaining.First();remaining.Remove(first);var group=new List<int>{first};var queue=new Queue<int>();queue.Enqueue(first);
            while(queue.Count>0)foreach(int n in Region.Neighbours(queue.Dequeue()))
                if(remaining.Remove(n)){group.Add(n);queue.Enqueue(n);}
            int left=group.Min(i=>i%Region.Width),right=group.Max(i=>i%Region.Width);
            int top=group.Min(i=>i/Region.Width),bottom=group.Max(i=>i/Region.Width);
            if(right-left>=bottom-top){left=Math.Max(0,left-1);right=Math.Min(Region.Width-1,right+1);}
            else{top=Math.Max(0,top-1);bottom=Math.Min(Region.Height-1,bottom+1);}
            batch.Rect(new Rect2(left*24+2,top*12+3,(right-left+1)*24,(bottom-top+1)*12),new Color(0,0,0,.2f));
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)deck.Add(y*Region.Width+x);
        }
        foreach(int i in deck.Order())
        {
            int x=i%Region.Width*24,y=i/Region.Width*12;
            batch.Rect(new Rect2(x,y,24,12),new Color("4d3528"));
            for(int dx=0;dx<24;dx+=4)
            {
                batch.Rect(new Rect2(x+dx,y,3,11),new Color("8f6842"));
                batch.Rect(new Rect2(x+dx,y,1,11),new Color("b38a58"));
            }
            if(!deck.Contains(i-Region.Width))batch.Rect(new Rect2(x,y-2,24,2),new Color("b38a58"));
            if(!deck.Contains(i-1))batch.Rect(new Rect2(x-1,y,2,12),new Color("b38a58"));
            if(!deck.Contains(i+1))batch.Rect(new Rect2(x+23,y,2,12),new Color("4d3528"));
            if(!deck.Contains(i+Region.Width))batch.Rect(new Rect2(x,y+11,24,2),new Color("4d3528"));
        }
        batch.Upload();SetProcess(false);
    }
    public override void _Draw()=>batch.Draw(this);
    public override void _ExitTree()=>batch.Dispose();
}
