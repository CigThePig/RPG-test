using Godot;
namespace Mosslight;

/// <summary>Pure cosmetic rules for the ruins: which wall tiles are masonry, how far a
/// point is from the nearest wall or floor, and the shared ramps. Reads RegionState and
/// never writes it; nothing here is saved or consumed by simulation.</summary>
public static class DungeonArt
{
    public const int Revision=1;
    public const float Reach=48;
    public static bool Is(RegionState r)=>r.Rooms.Count>0;
    // 0 void, 1 ink; then six-tone ramps (joint, four stone tones, highlight) for
    // weathered slate 2-7, overgrown stone 8-13 and ceremonial sandstone 14-19;
    // moss 20-24, gold 25-26, standing water 27-30, blossom 31-32, deep earth 33-35.
    public static readonly Color[] Ramp={new("0b0e17"),new("171c2b"),
        new("272d3d"),new("3a4254"),new("485265"),new("566277"),new("68758a"),new("8693a2"),
        new("242f33"),new("374644"),new("445650"),new("52675c"),new("637a69"),new("83977c"),
        new("33282d"),new("54443f"),new("6f5a48"),new("8a7355"),new("a58f66"),new("c9b684"),
        new("1d4034"),new("2a5a3a"),new("3a7a40"),new("55994a"),new("8fc55a"),
        new("b9822f"),new("f0c659"),
        new("1c2740"),new("243a58"),new("4a7396"),new("b7dcea"),
        new("d98bb6"),new("f3ecd2"),
        new("10141f"),new("1b2130"),new("242b3b")};
    /// <summary>Tiles holding a landmark's structural prop. They collide as wall, but the
    /// prop stands on open floor rather than on a block of masonry.</summary>
    public static HashSet<int> PropTiles(RegionState r)=>r.Landmarks.SelectMany(room=>room.Anchors.Where(a=>a.Collision=="terrain")
        .Select(a=>(room.OriginY+a.Y)*r.Width+room.OriginX+a.X)).ToHashSet();
    /// <summary>True where built wall stands. Space outside the region is solid.</summary>
    public static bool[] Masonry(RegionState r)
    {
        var props=PropTiles(r);var result=new bool[r.Tiles.Length];
        for(int at=0;at<result.Length;at++)result[at]=r.Tiles[at]==Terrain.Wall && !props.Contains(at);
        return result;
    }
    public static bool Solid(bool[] masonry,RegionState r,int x,int y)=>x<0||y<0||x>=r.Width||y>=r.Height||masonry[y*r.Width+x];
    /// <summary>Ground units from a point to the nearest surface of the other kind: positive
    /// on open floor (distance to masonry), negative inside masonry (distance to floor).</summary>
    public static float Distance(bool[] masonry,RegionState r,float px,float py)
    {
        int tx=Math.Clamp((int)MathF.Floor(px/24),0,r.Width-1),ty=Math.Clamp((int)MathF.Floor(py/24),0,r.Height-1);
        bool inside=Solid(masonry,r,tx,ty);float best=Reach;
        for(int y=ty-2;y<=ty+2;y++)for(int x=tx-2;x<=tx+2;x++)
        {
            if(Solid(masonry,r,x,y)==inside)continue;
            float dx=Math.Max(Math.Max(x*24-px,px-(x+1)*24),0),dy=Math.Max(Math.Max(y*24-py,py-(y+1)*24),0);
            best=Math.Min(best,MathF.Sqrt(dx*dx+dy*dy));
        }
        return inside?-best:best;
    }
    public static int Theme(RegionState r,int x,int y)=>Math.Max(0,Array.IndexOf(Landmarks.Themes,EnvironmentThemes.At(r,x,y)));
}

/// <summary>One region's dungeon lookups and the two materials that read them. Owned and
/// disposed by the ground renderer; wall rows only borrow the wall material.</summary>
public sealed class DungeonSurface : IDisposable
{
    public RegionState Region {get;}
    public bool[] Masonry {get;}
    public ShaderMaterial Floor {get;}
    public ShaderMaterial Wall {get;}
    readonly ImageTexture map,tiles;
    static Shader? floorShader,wallShader;
    public DungeonSurface(RegionState r)
    {
        Region=r;Masonry=DungeonArt.Masonry(r);
        int width=r.Width*8+1,height=r.Height*4+1;
        using(var image=Image.CreateEmpty(width,height,false,Image.Format.Rgba8))
        {
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float px=x*3,py=y*6;
                image.SetPixel(x,y,new Color((DungeonArt.Distance(Masonry,r,px,py)+DungeonArt.Reach)/(DungeonArt.Reach*2),
                    OverworldGroundArt.Noise(r.Seed,px/120f,py/120f,31),OverworldGroundArt.Noise(r.Seed,px/34f,py/34f,32),OverworldGroundArt.Noise(r.Seed,px/15f,py/15f,33)));
            }
            map=ImageTexture.CreateFromImage(image);
        }
        // One texel per tile: theme weights, and whether the tile is room floor (not corridor).
        using(var image=Image.CreateEmpty(r.Width,r.Height,false,Image.Format.Rgba8))
        {
            float Roomy(int x,int y)=>x>=0&&y>=0&&x<r.Width&&y<r.Height&&!Masonry[y*r.Width+x]&&r.RoomAt(x,y) is {Kind:"room"} room&&room.Contains(x,y)?1:0;
            for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)
            {
                int theme=DungeonArt.Theme(r,x,y);float room=Roomy(x,y);
                // Masonry borrows its neighbours' value so the filtered edge does not fade.
                if(Masonry[y*r.Width+x])room=Math.Max(Math.Max(Roomy(x-1,y),Roomy(x+1,y)),Math.Max(Roomy(x,y-1),Roomy(x,y+1)));
                image.SetPixel(x,y,new Color(theme==0?1:0,theme==1?1:0,theme==2?1:0,room));
            }
            tiles=ImageTexture.CreateFromImage(image);
        }
        floorShader??=GD.Load<Shader>("res://game/art/dungeon_floor.gdshader");
        wallShader??=GD.Load<Shader>("res://game/art/dungeon_wall.gdshader");
        Floor=Configure(floorShader,width,height);Wall=Configure(wallShader,width,height);
    }
    ShaderMaterial Configure(Shader shader,int width,int height)
    {
        var material=new ShaderMaterial{Shader=shader};
        material.SetShaderParameter("dungeon_map",map);material.SetShaderParameter("dungeon_tiles",tiles);
        material.SetShaderParameter("dungeon_map_size",new Vector2(width,height));
        material.SetShaderParameter("dungeon_tile_size",new Vector2(Region.Width,Region.Height));
        material.SetShaderParameter("dungeon_palette",DungeonArt.Ramp);
        material.SetShaderParameter("dungeon_seed",(int)(EnvironmentStyle.Hash(Region.Seed,0,0,9600+DungeonArt.Revision)%4096));
        ReachAmbience.Register(material);
        return material;
    }
    public void Dispose()
    {
        ReachAmbience.Unregister(Floor);ReachAmbience.Unregister(Wall);
        Floor.Dispose();Wall.Dispose();map.Dispose();tiles.Dispose();
    }
}

/// <summary>The ruin's ground layer: one dark fill for the whole region and merged runs of
/// open floor painted by the floor shader. Walls live beside the actors in TerrainWallRow.</summary>
public partial class DungeonGroundRenderer : Node2D
{
    public DungeonSurface Surface {get;init;}=null!;
    readonly TerrainBatch floor=new(),fill=new(),details=new(){Texture=DungeonPropAtlas.Texture};
    public override void _Ready()
    {
        AddToGroup("dungeon-ground");SetProcess(false);TextureFilter=TextureFilterEnum.Nearest;
        var r=Surface.Region;
        fill.Rect(new Rect2(0,-TerrainWallRow.Rise,r.Width*24,r.Height*12+TerrainWallRow.Rise),DungeonArt.Ramp[0]);
        for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;)
        {
            if(Surface.Masonry[y*r.Width+x]){x++;continue;}
            int start=x;while(x<r.Width&&!Surface.Masonry[y*r.Width+x])x++;
            floor.Rect(new Rect2(start*24,y*12,(x-start)*24,12),Colors.White);
        }
        AddChild(new Layer{Batch=floor,Material=Surface.Floor,TextureFilter=TextureFilterEnum.Nearest});
        // Landmark floor details: mosaics, thresholds, strewn records, offerings and moss.
        foreach(var room in r.Landmarks)foreach(var anchor in room.Anchors.Where(a=>a.Collision=="cosmetic"))
        {
            int theme=Math.Max(0,Array.IndexOf(Landmarks.Themes,room.Theme)),x=room.OriginX+anchor.X,y=room.OriginY+anchor.Y;
            int variant=(int)(EnvironmentStyle.Hash(r.Seed,x,y,9740)%2);
            int module=anchor.Asset switch
            {
                "mosaic"=>DungeonPropArt.Module(DungeonProp.Mosaic,theme),"threshold"=>DungeonPropArt.Module(DungeonProp.Threshold,theme),
                "records"=>DungeonPropArt.Module(DungeonProp.Records,variant),"offering"=>DungeonPropArt.Module(DungeonProp.Offering,variant),
                "moss"=>DungeonPropArt.Module(DungeonProp.MossPatch,variant),_=>-1
            };
            if(module<0)continue;
            details.Sprite(new Rect2(new Vector2((x+.5f)*24,(y+.5f)*12)-DungeonPropArt.FlatPivot,new Vector2(DungeonPropArt.Width,DungeonPropArt.Height)),DungeonPropArt.Source(module));
        }
        if(details.VertexCount>0)AddChild(new Layer{Batch=details,TextureFilter=TextureFilterEnum.Nearest});
    }
    public override void _Draw()=>fill.Draw(this);
    public override void _ExitTree(){floor.Dispose();fill.Dispose();details.Dispose();Surface.Dispose();}
    partial class Layer : Node2D
    {
        public TerrainBatch Batch {get;init;}=null!;
        public override void _Draw()=>Batch.Draw(this);
    }
}
