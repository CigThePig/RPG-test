using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>One placed town module. Buildings are placed by the centre tile of their front
/// row; street furniture by its own tile.</summary>
public readonly record struct TownPiece(bool Building,int Kind,int Variant,int X,int Y);

/// <summary>A town as a list of placed modules and paved tiles. Presentation only: it holds
/// no collision, is not saved and is never read by simulation.</summary>
public sealed class TownPlan
{
    public List<TownPiece> Pieces {get;}=new();
    public HashSet<(int X,int Y)> Paving {get;}=new();
    HashSet<(int,int)>? occupied;
    public TownPlan Add(TownBuilding building,int x,int y,int variant=0){Pieces.Add(new(true,(int)building,variant,x,y));occupied=null;return this;}
    public TownPlan Add(TownProp prop,int x,int y,int variant=0){Pieces.Add(new(false,(int)prop,variant,x,y));occupied=null;return this;}
    public TownPlan Pave(int x,int y,int width,int height){for(int yy=y;yy<y+height;yy++)for(int xx=x;xx<x+width;xx++)Paving.Add((xx,yy));occupied=null;return this;}
    /// <summary>Tiles under one building: across its front and back from it.</summary>
    public static IEnumerable<(int X,int Y)> Footprint(TownPiece piece)
    {
        if(!piece.Building){yield return (piece.X,piece.Y);yield break;}
        var (width,depth)=TownArt.Footprint((TownBuilding)piece.Kind);
        for(int y=piece.Y-depth+1;y<=piece.Y;y++)for(int x=piece.X-width/2;x<piece.X-width/2+width;x++)yield return (x,y);
    }
    /// <summary>Tiles where wild trees, shrubs and flora should not grow.</summary>
    public bool Occupied(int x,int y)
    {
        if(occupied==null)
        {
            occupied=new(Paving);
            foreach(var piece in Pieces)foreach(var tile in Footprint(piece))
                // Keep a clear tile round every building, and two before its door, so nothing
                // grows through its eaves or stands in front of it.
                for(int dy=-(piece.Building?1:0);dy<=(piece.Building?2:0);dy++)for(int dx=-(piece.Building?1:0);dx<=(piece.Building?1:0);dx++)occupied.Add((tile.X+dx,tile.Y+dy));
        }
        return occupied.Contains((x,y));
    }
}

/// <summary>Town plans staged for a region's presentation, keyed by region id. Town
/// generation does not exist yet; fixtures and previews stage plans here.</summary>
public static class TownStage
{
    static readonly Dictionary<string,TownPlan> plans=new(StringComparer.Ordinal);
    public static void Stage(string regionId,TownPlan plan)=>plans[regionId]=plan;
    public static void Clear(string regionId)=>plans.Remove(regionId);
    public static TownPlan? For(RegionState r)=>plans.GetValueOrDefault(r.Id);
    public static bool Occupied(RegionState r,int x,int y)=>plans.TryGetValue(r.Id,out var plan)&&plan.Occupied(x,y);
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
            if(!piece.Building&&TownArt.Flat((TownProp)piece.Kind))continue;
            // Buildings sort at the front of their front row; furniture at the same visible
            // footing as a standing character (see OverworldFlora.ActorFootDrop).
            var foot=piece.Building?GodotConvert.GroundToScreen((piece.X+.5f)*24,(piece.Y+1)*24):GodotConvert.GroundToScreen((piece.X+.5f)*24,(piece.Y+.5f)*24);
            var anchor=piece.Building?foot:foot-new Vector2(0,OverworldFlora.ActorFootDrop);
            result.Add(new TownView{Piece=piece,Position=anchor,offset=foot-anchor,TextureFilter=TextureFilterEnum.Nearest});
        }
        return result;
    }
    public override void _Ready()=>SetProcess(false);
    public override void _Draw()
    {
        if(Piece.Building)
            DrawTextureRectRegion(TownAtlas.Buildings,new Rect2(offset-TownArt.BuildingPivot,new Vector2(TownArt.BuildingWidth,TownArt.BuildingHeight)),TownArt.BuildingSource(TownArt.Module((TownBuilding)Piece.Kind,Piece.Variant)));
        else
            DrawTextureRectRegion(TownAtlas.Props,new Rect2(offset-TownArt.Pivot,new Vector2(TownArt.Width,TownArt.Height)),TownArt.PropSource(TownArt.Module((TownProp)Piece.Kind,Piece.Variant)));
    }
}

/// <summary>Cobbled paving and flat garden plots in the ground layer. One small region
/// lookup holds the distance to the edge of the paved set, so the cobbles break up into
/// the turf instead of ending on tile lines.</summary>
public partial class TownPaving : Node2D
{
    public RegionState Region {get;init;}=null!;
    public TownPlan Plan {get;init;}=null!;
    public static readonly Color[] Ramp={new("4b4a45"),new("76746a"),new("8f8c7f"),new("a6a393"),new("c1bda9"),new("55994a"),new("3a7a40"),new("6a675d")};
    readonly TerrainBatch batch=new(),flat=new(){Texture=TownAtlas.Props};
    static Shader? shader;
    ShaderMaterial? material;
    ImageTexture? lookup;
    public override void _Ready()
    {
        AddToGroup("town-paving");SetProcess(false);TextureFilter=TextureFilterEnum.Nearest;
        bool Paved(int x,int y)=>Plan.Paving.Contains((x,y));
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
            if(!piece.Building&&TownArt.Flat((TownProp)piece.Kind))
                flat.Sprite(new Rect2(new Vector2((piece.X+.5f)*24,(piece.Y+.5f)*12)-TownArt.FlatPivot,new Vector2(TownArt.Width,TownArt.Height)),TownArt.PropSource(TownArt.Module((TownProp)piece.Kind,piece.Variant)));
        if(flat.VertexCount>0)AddChild(new Layer{Batch=flat,TextureFilter=TextureFilterEnum.Nearest});
    }
    public override void _ExitTree(){batch.Dispose();flat.Dispose();material?.Dispose();lookup?.Dispose();}
    partial class Layer : Node2D
    {
        public TerrainBatch Batch {get;init;}=null!;
        public override void _Draw()=>Batch.Draw(this);
    }
}
