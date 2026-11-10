using Godot;
namespace Mosslight;

/// <summary>Authoritative rock cells drawn as connected outcrops rather than one boulder
/// per cell. One one-third-resolution lookup and merged quads per region; collision,
/// generation and saves are untouched. Outside the region counts as rock so the
/// boundary wall continues off the map.</summary>
public partial class OverworldRockRenderer : Node2D
{
    public const int Lift=10;
    public RegionState Region {get;init;}=null!;
    public static bool Enabled=>OverworldGroundRenderer.Enabled && OverworldPropAtlas.Enabled;
    public static long LastLookupBytes {get;private set;}
    public static long LastVertexBytes {get;private set;}
    public static double LastBuildMilliseconds {get;private set;}
    public static readonly Color[] Ramp={new("262b3a"),new("363d4c"),new("4a5361"),new("5d6873"),new("727e86"),
        new("86929a"),new("a3adae"),new("c2c9c0"),new("3c7340"),new("57903f"),new("7fb04d"),new("141a2a")};
    readonly TerrainBatch batch=new(),boulders=new(){Texture=OverworldPropAtlas.Texture};
    static Shader? shader;
    ShaderMaterial? material;
    ImageTexture? lookup;
    static bool Stone(RegionState r,int x,int y)=>x<0||y<0||x>=r.Width||y>=r.Height||r.Tiles[y*r.Width+x]==Terrain.Rock;
    /// <summary>A rock cell with no orthogonal rock neighbour stands alone as a boulder.</summary>
    public static bool Lone(RegionState r,int x,int y)=>x>=0&&y>=0&&x<r.Width&&y<r.Height&&r.Tiles[y*r.Width+x]==Terrain.Rock
        &&!Stone(r,x-1,y)&&!Stone(r,x+1,y)&&!Stone(r,x,y-1)&&!Stone(r,x,y+1);
    /// <summary>Cells belonging to a connected outcrop.</summary>
    public static bool Rock(RegionState r,int x,int y)=>Stone(r,x,y)&&!Lone(r,x,y);
    public static float Distance(RegionState r,int tx,int ty,float px,float py)=>ReachField.Inside((x,y)=>Rock(r,x,y),tx,ty,px,py,13);
    public static float Inset(RegionState r,float px,float py)=>.4f+6.8f*OverworldGroundArt.Noise(r.Seed,px/21f,py/27f,21);
    public override void _Ready()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        AddToGroup("overworld-rock");SetProcess(false);TextureFilter=TextureFilterEnum.Nearest;
        int width=Region.Width*8+1,height=Region.Height*4+1;
        using(var image=Image.CreateEmpty(width,height,false,Image.Format.Rgb8))
        {
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int tx=Math.Min(x/8,Region.Width-1),ty=Math.Min(y/4,Region.Height-1);float px=x*3,py=y*6;
                float d=Rock(Region,tx,ty)?Distance(Region,tx,ty,px,py):-2;
                image.SetPixel(x,y,new Color(Math.Clamp((d+8)/68,0,1),OverworldGroundArt.Noise(Region.Seed,px/46f,py/46f,22),Inset(Region,px,py)/8));
            }
            lookup=ImageTexture.CreateFromImage(image);
        }
        // Every cell an outcrop can paint: its own footprint, the lifted top to the
        // north and the cast shadow around it. Cells are disjoint, so nothing blends twice.
        bool Near(int x,int y)
        {
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {int nx=x+dx,ny=y+dy;if(nx>=0&&ny>=0&&nx<Region.Width&&ny<Region.Height&&Rock(Region,nx,ny))return true;}
            return false;
        }
        for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;)
        {
            if(!Near(x,y)){x++;continue;}
            int start=x;while(x<Region.Width&&Near(x,y))x++;
            batch.Rect(new Rect2(start*24,y*12,(x-start)*24,12),Colors.White);
        }
        for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;x++)
            if(Lone(Region,x,y))
                boulders.Sprite(new Rect2(new Vector2(x*24+12,y*12+11)-OverworldPropArt.Pivot,new Vector2(OverworldPropArt.Width,OverworldPropArt.Height)),
                    OverworldPropArt.Source(OverworldPropArt.Module(ReachProp.Rock,EnvironmentStyle.Hash(Region.Seed,x,y))));
        if(boulders.VertexCount>0)AddChild(new WaterGlints {Batch=boulders,TextureFilter=TextureFilterEnum.Nearest});
        shader??=GD.Load<Shader>("res://game/art/overworld_rock.gdshader");
        material=new ShaderMaterial{Shader=shader};
        material.SetShaderParameter("rock_map",lookup);material.SetShaderParameter("rock_lookup_size",new Vector2(width,height));
        material.SetShaderParameter("rock_palette",Ramp);material.SetShaderParameter("rock_seed",(float)(EnvironmentStyle.Hash(Region.Seed,0,0,9300)%97));
        OverworldAtmosphere.Configure(material,Region);ReachAmbience.Register(material);
        Material=material;LastLookupBytes=(long)width*height*3;LastVertexBytes=batch.VertexBytes+boulders.VertexBytes;LastBuildMilliseconds=timer.Elapsed.TotalMilliseconds;
    }
    public override void _Draw()=>batch.Draw(this);
    public override void _ExitTree(){ReachAmbience.Unregister(material);batch.Dispose();boulders.Dispose();material?.Dispose();lookup?.Dispose();}
}
