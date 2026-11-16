using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

public enum HouseStyle { Cottage, Stone, Inn, Smithy, Chapel, Barn }

/// <summary>A house built of blocks: a ring of wall tiles round a floor, with a doorway in
/// its south wall. (X, Y) is the north-west wall tile; Door is the first doorway tile's
/// offset along the south wall and DoorWidth how many tiles it spans.</summary>
public readonly record struct TownHouse(HouseStyle Style,int X,int Y,int Width,int Height,int Door,int DoorWidth=1)
{
    public bool Contains(int x,int y)=>x>=X&&y>=Y&&x<X+Width&&y<Y+Height;
    public bool Ring(int x,int y)=>Contains(x,y)&&(x==X||y==Y||x==X+Width-1||y==Y+Height-1);
    public bool Doorway(int x,int y)=>y==Y+Height-1&&x>=X+Door&&x<X+Door+DoorWidth;
    /// <summary>0 limewashed timber frame, 1 stone, 2 plank.</summary>
    public int Wall=>Style switch{HouseStyle.Cottage or HouseStyle.Inn=>0,HouseStyle.Barn=>2,_=>1};
    /// <summary>0 thatch, 1 clay tile, 2 slate.</summary>
    public int Roof=>Style switch{HouseStyle.Cottage or HouseStyle.Barn=>0,HouseStyle.Stone or HouseStyle.Smithy=>1,_=>2};
    /// <summary>0 boards, 1 flagstones, 2 packed earth and straw.</summary>
    public int Floor=>Style switch{HouseStyle.Smithy or HouseStyle.Chapel=>1,HouseStyle.Barn=>2,_=>0};
}

/// <summary>One placed furnishing or piece of street furniture, by tile.</summary>
public readonly record struct TownPiece(int Kind,int Variant,int X,int Y);

/// <summary>A town: block houses, placed furniture and paved tiles. Carve writes the houses
/// into a region's terrain as ordinary wall and floor, so collision, sight and pathing need
/// no town rules of their own. Generated composition is saved by TownState.</summary>
public sealed class TownPlan
{
    public List<TownHouse> Houses {get;}=new();
    public List<TownPiece> Pieces {get;}=new();
    public HashSet<(int X,int Y)> Paving {get;}=new();
    HashSet<(int,int)>? occupied;
    public TownPlan Build(HouseStyle style,int x,int y,int width,int height,int door,int doorWidth=1){Houses.Add(new(style,x,y,width,height,door,doorWidth));occupied=null;return this;}
    public TownPlan Add(TownProp prop,int x,int y,int variant=0){Pieces.Add(new((int)prop,variant,x,y));occupied=null;return this;}
    public TownPlan Pave(int x,int y,int width,int height){for(int yy=y;yy<y+height;yy++)for(int xx=x;xx<x+width;xx++)Paving.Add((xx,yy));occupied=null;return this;}
    public TownHouse? HouseAt(int x,int y){foreach(var house in Houses)if(house.Contains(x,y))return house;return null;}
    /// <summary>Write every house into the region: wall ring, floor within, open doorway.</summary>
    public void Carve(RegionState r)
    {
        foreach(var house in Houses)for(int y=house.Y;y<house.Y+house.Height;y++)for(int x=house.X;x<house.X+house.Width;x++)
            r.Tiles[y*r.Width+x]=house.Ring(x,y)&&!house.Doorway(x,y)?Terrain.Wall:Terrain.Floor;
    }
    /// <summary>Tiles where wild trees, shrubs and flora should not grow.</summary>
    public bool Occupied(int x,int y)
    {
        if(occupied==null)
        {
            occupied=new(Paving);
            foreach(var piece in Pieces)occupied.Add((piece.X,piece.Y));
            // A clear tile round every house, and two before its door, so nothing grows
            // through the eaves or stands in the way in.
            foreach(var house in Houses)for(int ty=house.Y-2;ty<house.Y+house.Height+2;ty++)for(int tx=house.X-1;tx<=house.X+house.Width;tx++)occupied.Add((tx,ty));
        }
        return occupied.Contains((x,y));
    }
}

/// <summary>Reference-scene overrides, falling back to the region's saved town composition.</summary>
public static class TownStage
{
    static readonly Dictionary<string,TownPlan> plans=new(StringComparer.Ordinal);
    public static void Stage(string regionId,TownPlan plan)=>plans[regionId]=plan;
    public static void Clear(string regionId)=>plans.Remove(regionId);
    public static TownPlan? For(RegionState r)=>plans.GetValueOrDefault(r.Id) ?? r.Town?.Plan(r.Width);
    public static bool Occupied(RegionState r,int x,int y)=>For(r)?.Occupied(x,y) ?? false;
}

/// <summary>Shared ramp and materials for block houses.</summary>
public static class TownBlocks
{
    // 0 ink, 1-4 oak, 5-7 limewash, 8-12 stone, 13-15 window glass, 16-19 thatch,
    // 20-23 clay tile, 24-27 slate, 28-31 floorboards, 32-33 packed earth, 34 straw.
    public static readonly Color[] Ramp={new("172428"),
        new("4d3528"),new("6d4b33"),new("8f6842"),new("b38a58"),
        new("b9ae92"),new("ddd3b8"),new("f6efd9"),
        new("414a59"),new("5d6873"),new("7f8a92"),new("a3adae"),new("c9cfc6"),
        new("ffcf6b"),new("fff6c8"),new("1f2a44"),
        new("6e5328"),new("9a7a3a"),new("c4a455"),new("e6d188"),
        new("5c2428"),new("8f3a34"),new("bd5a42"),new("e08a5e"),
        new("26344f"),new("3b5274"),new("5b7a9e"),new("8fabc4"),
        new("4a3426"),new("6a4c34"),new("82613f"),new("9a784f"),
        new("5f4f38"),new("786447"),new("c4a455")};
    /// <summary>Height of a house wall on screen: a little taller than a standing character.</summary>
    public const int Rise=44;
    static ShaderMaterial? wall,floor;
    static Shader? roof;
    static ShaderMaterial Shared(string path)
    {
        var material=new ShaderMaterial{Shader=GD.Load<Shader>(path)};material.SetShaderParameter("town_palette",Ramp);return material;
    }
    public static ShaderMaterial Wall=>wall??=Shared("res://game/art/town_wall.gdshader");
    public static ShaderMaterial Floor=>floor??=Shared("res://game/art/town_floor.gdshader");
    /// <summary>A wall material of its own, for a facade that fades.</summary>
    public static ShaderMaterial Facade()=>Shared("res://game/art/town_wall.gdshader");
    public static ShaderMaterial Roof()
    {
        roof??=GD.Load<Shader>("res://game/art/town_roof.gdshader");
        var material=new ShaderMaterial{Shader=roof};material.SetShaderParameter("town_palette",Ramp);return material;
    }
    /// <summary>Whether a south-facing wall tile carries a window, and whether it is lit.</summary>
    public static (bool Window,bool Lit) Window(TownHouse house,int seed,int x)
    {
        bool beside=x+1>=house.X+house.Door&&x-1<house.X+house.Door+house.DoorWidth;
        bool corner=x==house.X||x==house.X+house.Width-1;
        return (!beside&&!corner&&house.Style!=HouseStyle.Barn&&(x-house.X)%2==1,EnvironmentStyle.Hash(seed,x,house.Y,9810)%3!=0);
    }
}

/// <summary>One placed module in the Y-sorted actor layer, anchored at its ground contact.</summary>
public partial class TownView : Node2D
{
    public TownPiece Piece {get;init;}
    Vector2 offset;
    public static List<TownView> Create(TownPlan plan)
    {
        var result=new List<TownView>();
        foreach(var piece in plan.Pieces)
        {
            if(TownArt.Flat((TownProp)piece.Kind))continue;
            // Sort at the same visible footing as a standing character (see OverworldFlora.ActorFootDrop).
            var foot=GodotConvert.GroundToScreen((piece.X+.5f)*24,(piece.Y+.5f)*24);
            var anchor=foot-new Vector2(0,OverworldFlora.ActorFootDrop);
            result.Add(new TownView{Piece=piece,Position=anchor,offset=foot-anchor,TextureFilter=TextureFilterEnum.Nearest});
        }
        return result;
    }
    public override void _Ready()=>SetProcess(false);
    public override void _Draw()=>DrawTextureRectRegion(TownAtlas.Props,new Rect2(offset-TownArt.Pivot,new Vector2(TownArt.Width,TownArt.Height)),TownArt.PropSource(TownArt.Module((TownProp)Piece.Kind,Piece.Variant)));
}

/// <summary>A house's roof, in the actor layer at the house's front edge: characters in
/// front stand before it, everything within is under it. It fades out while the wayfarer
/// is inside the house and returns when they leave. The full-height front wall goes with
/// it, leaving the low inner wall, so the room is seen as a cutaway.</summary>
public partial class TownRoof : Node2D
{
    public const float FadeSeconds=.22f;
    /// <summary>How far the roof stands above the wall heads; furniture within stays under it.</summary>
    public const int Loft=10;
    /// <summary>Height of the doorway opening under its transom.</summary>
    public const int DoorHeight=34;
    public TownHouse House {get;init;}
    public int Seed {get;init;}
    /// <summary>The wayfarer's ground position.</summary>
    public Func<Vector2> Wayfarer {get;init;}=()=>new Vector2(-1,-1);
    public Func<bool> ReducedMotion {get;init;}=()=>false;
    public float Opacity {get;private set;}=1;
    public bool Occupied {get;private set;}
    readonly TerrainBatch batch=new(),front=new();
    ShaderMaterial? material,facade;
    Rect2 area;
    public static List<TownRoof> Create(TownPlan plan,int seed,Func<Vector2> wayfarer,Func<bool> reducedMotion)
        =>plan.Houses.Select(house=>new TownRoof{House=house,Seed=seed,Wayfarer=wayfarer,ReducedMotion=reducedMotion,
            // A hair past the south wall's own row, so the roof always draws over it.
            Position=new Vector2(0,(house.Y+house.Height)*12+.5f),TextureFilter=TextureFilterEnum.Nearest}).ToList();
    public override void _Ready()
    {
        // The roof oversails the walls and stops just over the top of the south face.
        float left=House.X*24-4,right=(House.X+House.Width)*24+4,top=House.Y*12-TownBlocks.Rise-Loft,bottom=(House.Y+House.Height)*12-TownBlocks.Rise+4;
        area=new Rect2(left,top-Position.Y,right-left,bottom-top);
        batch.Quad(area,new Rect2(0,0,area.Size.X,area.Size.Y),Colors.White);
        material=TownBlocks.Roof();material.SetShaderParameter("roof_size",area.Size);material.SetShaderParameter("roof_style",House.Roof);
        // The front of the house: a full-height facade over the low inner wall, with a
        // transom over each doorway. It goes with the roof.
        float sill=(House.Y+House.Height)*12-Position.Y;int south=House.Y+House.Height-1;
        for(int x=House.X;x<House.X+House.Width;x++)
        {
            bool door=House.Doorway(x,south);
            float End(int nx)=>nx<House.X||nx>=House.X+House.Width||House.Doorway(nx,south)?.5f:0;
            var (window,lit)=TownBlocks.Window(House,Seed,x);
            int rise=door?TownBlocks.Rise-DoorHeight:TownBlocks.Rise;
            front.Quad(new Rect2(x*24,sill-TownBlocks.Rise,24,rise),new Rect2(0,0,24,rise),new Color(1,door?0:End(x-1),door?0:End(x+1),(House.Wall+(window&&!door?4:0)+(window&&lit&&!door?8:0))/16f));
        }
        facade=TownBlocks.Facade();
        AddChild(new Cover{Batch=front,Material=facade,TextureFilter=TextureFilterEnum.Nearest,ShowBehindParent=true});
        AddChild(new Cover{Batch=batch,Material=material,TextureFilter=TextureFilterEnum.Nearest,ShowBehindParent=true});
        Step(0,true);
    }
    void Step(double delta,bool snap)
    {
        var at=Wayfarer();
        Occupied=House.Contains((int)MathF.Floor(at.X/24),(int)MathF.Floor(at.Y/24));
        float target=Occupied?0:1,before=Opacity;
        // Frozen time and reduced motion both get the end state at once.
        Opacity=snap||delta<=0||ReducedMotion()?target:Mathf.MoveToward(Opacity,target,(float)delta/FadeSeconds);
        if(Opacity==before&&!snap)return;
        material!.SetShaderParameter("roof_alpha",Opacity);facade!.SetShaderParameter("wall_alpha",Opacity);QueueRedraw();
    }
    public override void _Process(double delta)=>Step(delta,false);
    public override void _Draw()
    {
        // From outside the doorway is a dark opening under an oak lintel, with the door
        // standing open against the wall. It clears with the roof.
        float sill=(House.Y+House.Height)*12-Position.Y,head=sill-DoorHeight;
        for(int i=0;i<House.DoorWidth;i++)
        {
            float x=(House.X+House.Door+i)*24;
            DrawRect(new Rect2(x,head,24,sill-head-2),new Color(.09f,.08f,.10f,Opacity*.92f));
            DrawRect(new Rect2(x,head,24,2),new Color(TownBlocks.Ramp[2],Opacity));
            if(i==0){DrawRect(new Rect2(x,head+2,5,sill-head-4),new Color(TownBlocks.Ramp[3],Opacity));DrawRect(new Rect2(x+4,head+2,1,sill-head-4),new Color(TownBlocks.Ramp[1],Opacity));DrawRect(new Rect2(x+2,head+6,1,2),new Color(TownBlocks.Ramp[13],Opacity));}
            if(i==House.DoorWidth-1&&House.DoorWidth>1){DrawRect(new Rect2(x+19,head+2,5,sill-head-4),new Color(TownBlocks.Ramp[2],Opacity));DrawRect(new Rect2(x+19,head+2,1,sill-head-4),new Color(TownBlocks.Ramp[1],Opacity));}
        }
        // The chimney stands on the ridge and goes with the roof.
        if(House.Style is HouseStyle.Barn or HouseStyle.Chapel)return;
        var foot=new Vector2(area.Position.X+area.Size.X*(House.X%2==0?.74f:.26f),area.Position.Y+area.Size.Y*.5f+2).Floor();
        DrawTextureRectRegion(TownAtlas.Props,new Rect2(foot-TownArt.Pivot,new Vector2(TownArt.Width,TownArt.Height)),TownArt.PropSource(TownArt.Module(TownProp.Chimney)),new Color(1,1,1,Opacity));
    }
    public override void _ExitTree(){batch.Dispose();front.Dispose();material?.Dispose();facade?.Dispose();}
    partial class Cover : Node2D
    {
        public TerrainBatch Batch {get;init;}=null!;
        public override void _Draw()=>Batch.Draw(this);
    }
}

/// <summary>Town ground: house floors, cobbled paving and flat plots and rugs. One small
/// region lookup holds the distance to the edge of the paved set, so the cobbles break up
/// into the turf instead of ending on tile lines.</summary>
public partial class TownPaving : Node2D
{
    public RegionState Region {get;init;}=null!;
    public TownPlan Plan {get;init;}=null!;
    public static readonly Color[] Ramp={new("4b4a45"),new("76746a"),new("8f8c7f"),new("a6a393"),new("c1bda9"),new("55994a"),new("3a7a40"),new("6a675d")};
    readonly TerrainBatch batch=new(),floors=new(),flat=new(){Texture=TownAtlas.Props};
    static Shader? shader;
    ShaderMaterial? material;
    ImageTexture? lookup;
    public override void _Ready()
    {
        AddToGroup("town-paving");SetProcess(false);TextureFilter=TextureFilterEnum.Nearest;
        bool Paved(int x,int y)=>Plan.Paving.Contains((x,y));
        bool Wall(int x,int y)=>x>=0&&y>=0&&x<Region.Width&&y<Region.Height&&Region.Tiles[y*Region.Width+x]==Terrain.Wall;
        // House floors: one quad per tile. r floor style, g the walls that shade it
        // (north 1, west 2), b a stone threshold in the doorway. Wall tiles get the same
        // floor beneath them so nothing shows through at their feet.
        foreach(var house in Plan.Houses)for(int y=house.Y;y<house.Y+house.Height;y++)for(int x=house.X;x<house.X+house.Width;x++)
        {
            int shade=Wall(x,y)?0:(Wall(x,y-1)?1:0)|(Wall(x-1,y)?2:0);
            floors.Rect(new Rect2(x*24,y*12,24,12),new Color(house.Floor/4f,shade/4f,house.Doorway(x,y)?1:0));
        }
        if(floors.VertexCount>0)AddChild(new Layer{Batch=floors,Material=TownBlocks.Floor,TextureFilter=TextureFilterEnum.Nearest});
        if(Plan.Paving.Count>0)
        {
            int width=Region.Width*8+1,height=Region.Height*4+1;
            using(var image=Image.CreateEmpty(width,height,false,Image.Format.R8))
            {
                foreach(var (tx,ty) in Plan.Paving)
                    for(int y=ty*4;y<=ty*4+4;y++)for(int x=tx*8;x<=tx*8+8;x++)
                    {
                        if(x>=width||y>=height)continue;
                        int cx=Math.Min(x/8,Region.Width-1),cy=Math.Min(y/4,Region.Height-1);
                        float d=Paved(cx,cy)?ReachField.Inside(Paved,cx,cy,x*3,y*6,10,48):0;
                        if(d>image.GetPixel(x,y).R*48)image.SetPixel(x,y,new Color(d/48,0,0));
                    }
                lookup=ImageTexture.CreateFromImage(image);
            }
            foreach(var (x,y) in Plan.Paving)batch.Rect(new Rect2(x*24,y*12,24,12),Colors.White);
            shader??=GD.Load<Shader>("res://game/art/town_paving.gdshader");
            material=new ShaderMaterial{Shader=shader};
            material.SetShaderParameter("paving_map",lookup);material.SetShaderParameter("paving_map_size",new Vector2(width,height));
            material.SetShaderParameter("paving_palette",Ramp);material.SetShaderParameter("paving_seed",(int)(EnvironmentStyle.Hash(Region.Seed,0,0,9800)%4096));
            AddChild(new Layer{Batch=batch,Material=material,TextureFilter=TextureFilterEnum.Nearest});
        }
        foreach(var piece in Plan.Pieces)
            if(TownArt.Flat((TownProp)piece.Kind))
                flat.Sprite(new Rect2(new Vector2((piece.X+.5f)*24,(piece.Y+.5f)*12)-TownArt.FlatPivot,new Vector2(TownArt.Width,TownArt.Height)),TownArt.PropSource(TownArt.Module((TownProp)piece.Kind,piece.Variant)));
        if(flat.VertexCount>0)AddChild(new Layer{Batch=flat,TextureFilter=TextureFilterEnum.Nearest});
    }
    public override void _ExitTree(){batch.Dispose();floors.Dispose();flat.Dispose();material?.Dispose();lookup?.Dispose();}
    partial class Layer : Node2D
    {
        public TerrainBatch Batch {get;init;}=null!;
        public override void _Draw()=>Batch.Draw(this);
    }
}
