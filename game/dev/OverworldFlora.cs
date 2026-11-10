using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>Small nonblocking growth chosen from coordinates alone: ferns, toadstools,
/// windfall and bluebells on turf, reedmace at the banks and lily pads on open water.
/// Standing plants share one Y-sorted node per cell row so actors pass correctly in
/// front of and behind them; flat pads lie in the ground layer. Never a feature,
/// never collision and never saved.</summary>
public partial class OverworldFlora : Node2D
{
    public readonly record struct Placement(int Module,Vector2 Foot,bool Flat);
    readonly List<(Rect2 Destination,Rect2 Source)> sprites=new();
    public static bool Enabled=>OverworldRockRenderer.Enabled;
    public static int LastCount {get;private set;}
    public static List<Placement> Plan(RegionState r)
    {
        var result=new List<Placement>();
        bool Is(int x,int y,Terrain t)=>x>=0&&y>=0&&x<r.Width&&y<r.Height&&r.Tiles[y*r.Width+x]==t;
        for(int y=1;y<r.Height-1;y++)for(int x=1;x<r.Width-1;x++)
        {
            int at=y*r.Width+x;uint h=EnvironmentStyle.Hash(r.Seed,x,y,9500);
            float jitter=((h>>20)%9)-4f;
            if(r.Tiles[at]==Terrain.Water)
            {
                bool north=!Is(x,y-1,Terrain.Water),south=!Is(x,y+1,Terrain.Water),west=!Is(x-1,y,Terrain.Water),east=!Is(x+1,y,Terrain.Water);
                if((north||south||west||east)&&h%5==0)
                {
                    // Reeds root in the shallows just off whichever bank is nearest.
                    float gx=(x+.5f)*24+(west?-4:east?4:jitter),gy=(y+.5f)*24+(north?-3:south?5:0);
                    result.Add(new(OverworldPropArt.Module(ReachProp.WaterFlora,2+(h>>8)%2),GodotConvert.GroundToScreen(gx,gy),false));
                }
                else if(!north&&!south&&!west&&!east&&h%11==0)
                    result.Add(new(OverworldPropArt.Module(ReachProp.WaterFlora,(h>>8)%2),GodotConvert.GroundToScreen((x+.5f)*24+jitter,(y+.5f)*24),true));
                continue;
            }
            if(r.Tiles[at]!=Terrain.Grass||h%17!=0||at==r.Entrance)continue;
            // Leave interactables, their approaches and the taller decoration slots clear.
            if(EnvironmentStyle.Hash(r.Seed,x,y,99)%29==0)continue;
            if(r.Features.Keys.Any(f=>Math.Abs(f%r.Width-x)<2&&Math.Abs(f/r.Width-y)<2))continue;
            bool damp=false;for(int dy=-2;dy<=2&&!damp;dy++)for(int dx=-2;dx<=2;dx++)if(Is(x+dx,y+dy,Terrain.Water)){damp=true;break;}
            uint pick=(h>>8)%10;
            // Ferns favour damp ground; windfall is rare; flowers and toadstools fill the rest.
            uint kind=damp?(pick<6?0u:3u):(pick<3?0u:pick<8?3u:pick<9?1u:2u);
            result.Add(new(OverworldPropArt.Module(ReachProp.GroundFlora,kind),GodotConvert.GroundToScreen((x+.5f)*24+jitter,(y+.5f)*24),false));
        }
        return result;
    }
    /// <summary>Row nodes for the Y-sorted actor layer plus one flat node for the ground layer.</summary>
    public static (List<OverworldFlora> Standing,OverworldFlora? Flat) Create(RegionState r)
    {
        var plan=Plan(r);LastCount=plan.Count;
        var rows=new SortedDictionary<int,OverworldFlora>();OverworldFlora? flat=null;
        foreach(var item in plan)
        {
            OverworldFlora node;
            if(item.Flat)node=flat??=new OverworldFlora{TextureFilter=TextureFilterEnum.Nearest};
            else
            {
                int row=(int)MathF.Round(item.Foot.Y);
                if(!rows.TryGetValue(row,out node!))rows[row]=node=new OverworldFlora{Position=new Vector2(0,row),TextureFilter=TextureFilterEnum.Nearest};
            }
            node.sprites.Add((new Rect2(item.Foot-node.Position-OverworldPropArt.Pivot,new Vector2(OverworldPropArt.Width,OverworldPropArt.Height)),OverworldPropArt.Source(item.Module)));
        }
        return (rows.Values.ToList(),flat);
    }
    public override void _Ready()=>SetProcess(false);
    // Plain atlas rectangles: the canvas batches them with the neighbouring props.
    public override void _Draw(){foreach(var sprite in sprites)DrawTextureRectRegion(OverworldPropAtlas.Texture,sprite.Destination,sprite.Source);}
}
