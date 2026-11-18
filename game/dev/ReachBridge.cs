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
        foreach(int i in Region.Crossings.Order())
        {
            int x=i%Region.Width*24,y=i/Region.Width*12;
            batch.Rect(new Rect2(x,y,24,12),new Color("4d3528"));
            for(int dx=0;dx<24;dx+=4)
            {
                batch.Rect(new Rect2(x+dx,y,3,11),new Color("8f6842"));
                batch.Rect(new Rect2(x+dx,y,1,11),new Color("b38a58"));
            }
            if(!Region.Crossings.Contains(i-Region.Width))batch.Rect(new Rect2(x,y-2,24,2),new Color("b38a58"));
            if(!Region.Crossings.Contains(i+Region.Width))batch.Rect(new Rect2(x,y+11,24,2),new Color("4d3528"));
        }
        batch.Upload();SetProcess(false);
    }
    public override void _Draw()=>batch.Draw(this);
    public override void _ExitTree()=>batch.Dispose();
}
