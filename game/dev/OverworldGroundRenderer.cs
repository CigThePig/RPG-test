using Godot;
namespace Mosslight;

/// <summary>One region lookup and merged dry runs. Continuous world-space material
/// clusters cross cells; no tiled noise overlays, per-cell nodes or growing caches.</summary>
public partial class OverworldGroundRenderer : Node2D
{
    public RegionState Region {get;init;}=null!;
    public Rect2I? Cells {get;init;}
    Vector2 lookupOrigin;
    static readonly bool visualVerification=OS.GetCmdlineUserArgs().Contains("--visual-check");
    static readonly bool previousCommand=OS.GetCmdlineUserArgs().Contains("--part1-ground");
    public static bool VerificationPrevious {get;set;}
    public static bool Enabled=>OverworldWaterRenderer.UseLivingReach && !(visualVerification && (VerificationPrevious || previousCommand));
    public static long LastLookupBytes {get;private set;}
    public static long LastVertexBytes {get;private set;}
    public static double LastBuildMilliseconds {get;private set;}
    public static long BuildCount {get;private set;}
    readonly TerrainBatch batch=new();
    static Shader? shader;
    ShaderMaterial? material;
    ImageTexture? lookup;
    public override void _Ready()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        AddToGroup("overworld-ground");SetProcess(false);TextureFilter=TextureFilterEnum.Nearest;
        var cells=Cells??new Rect2I(0,0,Region.Width,Region.Height);
        int sx=Math.Max(0,cells.Position.X-3),sy=Math.Max(0,cells.Position.Y-3);
        int ex=Math.Min(Region.Width,cells.End.X+3),ey=Math.Min(Region.Height,cells.End.Y+3);
        int width=(ex-sx)*8+1,height=(ey-sy)*4+1;
        lookupOrigin=new Vector2(sx*8,sy*4);
        using(var image=Image.CreateEmpty(width,height,false,Image.Format.Rgba8))
        {
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int gx=x+sx*8,gy=y+sy*4;
                var s=OverworldGroundArt.Sample(Region,gx*3,gy*6);
                image.SetPixel(x,y,new Color((s.Soil+48)/96,s.Wet/48,OverworldGroundArt.Noise(Region.Seed,gx*3/110f,gy*6/110f,0),OverworldGroundArt.Noise(Region.Seed,gx*3/22f,gy*6/22f,1)));
            }
            lookup=ImageTexture.CreateFromImage(image);
        }
        for(int y=cells.Position.Y;y<cells.End.Y;y++)for(int x=cells.Position.X;x<cells.End.X;)
        {
            if(!OverworldGroundArt.Covered(Region,x,y)){x++;continue;}
            int start=x;while(x<cells.End.X&&OverworldGroundArt.Covered(Region,x,y))x++;
            batch.Rect(new Rect2(start*24,y*12,(x-start)*24,12),Colors.White);
        }
        shader??=GD.Load<Shader>("res://game/art/overworld_ground.gdshader");
        material=new ShaderMaterial{Shader=shader};Configure(material);ReachAmbience.Register(material);
        Material=material;LastLookupBytes=(long)width*height*4;LastVertexBytes=batch.VertexBytes;
        LastBuildMilliseconds=timer.Elapsed.TotalMilliseconds;BuildCount++;
    }
    public void Configure(ShaderMaterial target)
    {
        target.SetShaderParameter("ground_map",lookup!);
        target.SetShaderParameter("ground_lookup_size",lookup!.GetSize());
        target.SetShaderParameter("ground_lookup_origin",lookupOrigin);
        target.SetShaderParameter("ground_palette",OverworldGroundArt.Ramp);
        target.SetShaderParameter("ground_seed",(int)(OverworldWaterArt.Hash(Region.Seed,0,0,OverworldGroundArt.Revision)%4096));
    }
    public override void _Draw()=>batch.Draw(this);
    public override void _ExitTree(){ReachAmbience.Unregister(material);batch.Dispose();material?.Dispose();lookup?.Dispose();}
}
