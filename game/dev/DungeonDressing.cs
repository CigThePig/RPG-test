using Godot;
using PixelCreatures.Runtime;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Small nonblocking life in the ruins, chosen from coordinates alone: torches and
/// banners on the wall faces, and bones, pots, growth and candles along the foot of the
/// walls. Never a feature, never collision and never saved.</summary>
public static class DungeonDressing
{
    /// <summary>A wall tile whose south face carries a torch (Banner &lt; 0) or a banner.</summary>
    public readonly record struct Fixture(int X,int Y,int Banner);
    /// <summary>One loose item: its atlas module, ground point and any light it gives.</summary>
    public readonly record struct Item(int Module,V Ground,float Glow,bool Warm);
    static bool Face(RegionState r,bool[] masonry,int x,int y)=>y+1<r.Height&&DungeonArt.Solid(masonry,r,x,y)&&!DungeonArt.Solid(masonry,r,x,y+1);
    public static List<Fixture> Fixtures(RegionState r,bool[] masonry)
    {
        var result=new List<Fixture>();
        for(int y=0;y<r.Height-1;y++)
        {
            int last=-9;
            for(int x=1;x<r.Width-1;x++)
            {
                // Only on a straight run of face, never over a gate or a landmark prop.
                if(!Face(r,masonry,x,y)||!Face(r,masonry,x-1,y)||!Face(r,masonry,x+1,y))continue;
                int below=(y+1)*r.Width+x;
                if(r.Tiles[below]!=Terrain.Floor||r.Features.ContainsKey(below))continue;
                uint h=EnvironmentStyle.Hash(r.Seed,x,y,9700);
                if(h%4==0&&x-last>=4){result.Add(new(x,y,-1));last=x;}
                else if(DungeonArt.Theme(r,x,y)==2&&h%5==1&&x-last>=2)result.Add(new(x,y,(int)((h>>8)%3)));
            }
        }
        return result;
    }
    public static IEnumerable<Fixture> Torches(RegionState r,bool[] masonry)=>Fixtures(r,masonry).Where(f=>f.Banner<0);
    public static List<Item> Plan(RegionState r,bool[] masonry)
    {
        var result=new List<Item>();
        bool Open(int x,int y)=>!DungeonArt.Solid(masonry,r,x,y)&&r.Tiles[y*r.Width+x]==Terrain.Floor;
        for(int y=1;y<r.Height-1;y++)for(int x=1;x<r.Width-1;x++)
        {
            int at=y*r.Width+x;
            if(!Open(x,y)||at==r.Entrance)continue;
            var room=r.RoomAt(x,y);
            if(room==null||room.Kind!="room")continue;
            // Stay inside rooms and well clear of every corridor mouth.
            bool mouth=false;
            for(int dy=-2;dy<=2&&!mouth;dy++)for(int dx=-2;dx<=2;dx++)
            {int nx=x+dx,ny=y+dy;if(nx>=0&&ny>=0&&nx<r.Width&&ny<r.Height&&Open(nx,ny)&&!room.Contains(nx,ny)){mouth=true;break;}}
            if(mouth||!room.Contains(x,y))continue;
            if(r.Features.Keys.Any(f=>Math.Abs(f%r.Width-x)<2&&Math.Abs(f/r.Width-y)<2))continue;
            bool north=DungeonArt.Solid(masonry,r,x,y-1),side=DungeonArt.Solid(masonry,r,x-1,y)||DungeonArt.Solid(masonry,r,x+1,y),south=DungeonArt.Solid(masonry,r,x,y+1);
            uint h=EnvironmentStyle.Hash(r.Seed,x,y,9710);
            // Clutter gathers at the foot of the walls; open floor keeps only a rare scrap.
            // The row under a south wall is hidden behind that wall's raised top.
            if(south)continue;
            if(north?h%3!=0:side?h%5!=0:h%31!=0)continue;
            int theme=DungeonArt.Theme(r,x,y);uint pick=(h>>8)%12,variant=(h>>16)%4;
            DungeonProp kind=theme switch
            {
                1=>pick<6?DungeonProp.Growth:pick<9?DungeonProp.Rubble:pick<11?DungeonProp.Bones:DungeonProp.Pots,
                2=>pick<4?DungeonProp.Sacred:pick<8?DungeonProp.Pots:pick<10?DungeonProp.Rubble:DungeonProp.Bones,
                _=>pick<4?DungeonProp.Rubble:pick<7?DungeonProp.Bones:pick<9?DungeonProp.Debris:pick<11?DungeonProp.Pots:DungeonProp.Growth
            };
            // Away from the walls only low scraps lie about.
            if(!north&&!side&&kind is DungeonProp.Pots or DungeonProp.Sacred or DungeonProp.Debris)kind=DungeonProp.Rubble;
            if(kind==DungeonProp.Growth&&theme!=1)variant=3;
            float jitter=((h>>20)%9)-4f;
            // Keep the whole sprite and its shadow off the wall tiles to either side.
            bool west=DungeonArt.Solid(masonry,r,x-1,y),east=DungeonArt.Solid(masonry,r,x+1,y);
            if(west&&east)continue;
            if(west)jitter=MathF.Abs(jitter)+1;
            if(east)jitter=-MathF.Abs(jitter)-1;
            // Against a north wall an item sits just clear of the face: any nearer and the
            // wall row, which sorts at its front edge, would draw over it.
            var ground=new V((x+.5f)*24+jitter,(y+(north?.62f:.5f))*24);
            bool glowcap=kind==DungeonProp.Growth&&variant==3,candles=kind==DungeonProp.Sacred&&variant==0;
            result.Add(new(DungeonPropArt.Module(kind,(int)variant),ground,glowcap?46:candles?64:0,candles));
        }
        return result;
    }
}

/// <summary>Loose items drawn from the ruin atlas. Standing items share one Y-sorted node per
/// screen row so actors pass in front of and behind them.</summary>
public partial class DungeonClutter : Node2D
{
    readonly List<(Rect2 Destination,Rect2 Source)> sprites=new();
    public static int LastCount {get;private set;}
    public static List<DungeonClutter> Create(RegionState r,bool[] masonry)
    {
        var plan=DungeonDressing.Plan(r,masonry);LastCount=plan.Count;
        var rows=new SortedDictionary<int,DungeonClutter>();
        foreach(var item in plan)
        {
            var foot=GodotConvert.GroundToScreen(item.Ground.X,item.Ground.Y);
            // Sort by the same visible footing as a standing character; see OverworldFlora.
            int row=(int)MathF.Round(foot.Y)-OverworldFlora.ActorFootDrop;
            if(!rows.TryGetValue(row,out var node))rows[row]=node=new DungeonClutter{Position=new Vector2(0,row),TextureFilter=TextureFilterEnum.Nearest};
            node.sprites.Add((new Rect2(foot-node.Position-DungeonPropArt.Pivot,new Vector2(DungeonPropArt.Width,DungeonPropArt.Height)),DungeonPropArt.Source(item.Module)));
        }
        return rows.Values.ToList();
    }
    public override void _Ready()=>SetProcess(false);
    public override void _Draw(){foreach(var sprite in sprites)DrawTextureRectRegion(DungeonPropAtlas.Texture,sprite.Destination,sprite.Source);}
}

/// <summary>Torches and banners hung on one wall row's faces. A child of that row, so it
/// sorts and hides with the masonry it hangs on. Flames step through four frames on the
/// shared ambience clock and hold still under reduced motion.</summary>
public partial class WallFixtures : Node2D
{
    public DungeonDressing.Fixture[] Fixtures {get;init;}=Array.Empty<DungeonDressing.Fixture>();
    public int Seed {get;init;}
    int frame=-1;
    static int Frame=>(int)(ReachAmbience.Time*9);
    public override void _Ready(){TextureFilter=TextureFilterEnum.Nearest;SetProcess(Fixtures.Any(f=>f.Banner<0));}
    public override void _Process(double delta){if(Frame!=frame)QueueRedraw();}
    public override void _Draw()
    {
        frame=Frame;
        foreach(var fixture in Fixtures)
        {
            // The row's origin is the wall's front ground edge: the foot of the face.
            var foot=new Vector2(fixture.X*24+12,0);
            if(fixture.Banner>=0){DungeonPropAtlas.Draw(this,DungeonPropArt.Module(DungeonProp.Banner,fixture.Banner),foot);continue;}
            DungeonPropAtlas.Draw(this,DungeonPropArt.Module(DungeonProp.Sconce),foot);
            DungeonPropAtlas.Draw(this,DungeonPropArt.Module(DungeonProp.TorchFlame,frame+(int)(EnvironmentStyle.Hash(Seed,fixture.X,fixture.Y,9720)%4)),foot);
        }
    }
}
