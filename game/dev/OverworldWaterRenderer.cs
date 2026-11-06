using Godot;
namespace Mosslight;
/// <summary>One water surface batch plus sparse glints. A single active, one-third-resolution
/// distance lookup replaces thousands of tiny triangles; it is data, not a painted region texture.
/// All native region resources are disposed on removal. No seed-keyed caches.</summary>
public partial class OverworldWaterRenderer : Node2D
{
    public RegionState Region { get; init; } = null!;
    public OverworldGroundRenderer? Ground {get;init;}
    public Func<bool> ReducedMotion { get; init; } = () => true;
    static readonly bool visualVerification=OS.GetCmdlineUserArgs().Contains("--visual-check");
    static readonly bool legacyCommand=OS.GetCmdlineUserArgs().Contains("--baseline-water");
    // Test-only A/B switch; ordinary gameplay cannot select the obsolete renderer.
    public static bool VerificationLegacy { get; set; }
    public static bool UseLivingReach => !(visualVerification && (legacyCommand || VerificationLegacy));
    public static double LastBuildMilliseconds { get; private set; }
    public static long LastVertexBytes { get; private set; }
    public static long LastLookupBytes { get; private set; }
    public static long BuildCount { get; private set; }
    readonly TerrainBatch surface=new(),glints=new(),details=new();
    ShaderMaterial? surfaceMaterial,glintMaterial;
    ImageTexture? field;
    static Shader? waterShader,glintShader;
    bool reduced;
    public override void _Ready()
    {
        AddToGroup("overworld-water");
        TextureFilter=TextureFilterEnum.Nearest;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var regionBounds=new Rect2(0,0,Region.Width*24,Region.Height*12);
        void Bank(Rect2 rectangle,Color color)
        {
            var clipped=rectangle.Intersection(regionBounds);
            if(clipped.Size.X>0 && clipped.Size.Y>0)details.Rect(clipped,color);
        }
        if(OS.GetCmdlineUserArgs().Contains("--water-static-fallback"))
        {
            foreach(var mark in OverworldWaterArt.Build(Region))surface.Rect(mark.Rect,OverworldWaterArt.Ramp[mark.Shade]);
            LastVertexBytes=surface.VertexBytes;LastLookupBytes=0;LastBuildMilliseconds=timer.Elapsed.TotalMilliseconds;BuildCount++;SetProcess(false);return;
        }
        // Three canvas pixels per distance sample. GPU interpolates the scalar field,
        // then quantizes material at integer canvas pixels: crisp art, not a blurred image.
        int width=Region.Width*8+1,height=Region.Height*4+1;
        using(var image=Image.CreateEmpty(width,height,false,OverworldWaterArt.Part1?Image.Format.Rg8:Image.Format.Rgb8))
        {
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int tx=x/8,ty=y/4;float px=x*3,py=y*6;
                float d=OverworldWaterArt.Water(Region,tx,ty)?OverworldWaterArt.Distance(Region,tx,ty,px,py):-2;
                float v=OverworldWaterArt.Variation(Region.Seed,px,py);
                float inset=OverworldWaterArt.BankInset(Region,tx,ty,px,py);
                image.SetPixel(x,y,new Color(Math.Clamp((d+8)/68,0,1),v,inset/8));
            }
            field=ImageTexture.CreateFromImage(image);
        }
        LastLookupBytes=(long)width*height*(OverworldWaterArt.Part1?2:3);
        // Exactly the authoritative water footprint. Adjacent water rows share one quad.
        for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;)
        {
            if(!OverworldWaterArt.Water(Region,x,y)){x++;continue;}
            int start=x;while(x<Region.Width && OverworldWaterArt.Water(Region,x,y))x++;
            surface.Rect(new Rect2(start*24,y*12,(x-start)*24,12),Colors.White);
        }
        waterShader ??= GD.Load<Shader>("res://game/art/overworld_water.gdshader");
        surfaceMaterial=new ShaderMaterial {Shader=waterShader};surfaceMaterial.SetShaderParameter("depth_map",field);surfaceMaterial.SetShaderParameter("palette",OverworldWaterArt.Ramp);
        surfaceMaterial.SetShaderParameter("lookup_size",new Vector2(width,height));surfaceMaterial.SetShaderParameter("cosmetic_seed",(float)(OverworldWaterArt.Hash(Region.Seed,0,0)%65536));
        surfaceMaterial.SetShaderParameter("part1",OverworldWaterArt.Part1);
        Ground?.Configure(surfaceMaterial);
        Material=surfaceMaterial;
        for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;x++)
        {
            if(!OverworldWaterArt.Water(Region,x,y))continue;
            uint bank=OverworldWaterArt.Hash(Region.Seed,x,y,9);
            if(!OverworldWaterArt.Water(Region,x,y-1) && bank%4==0)
            {
                int bx=x*24+8+(int)(bank>>8)%8,by=y*12+1;
                Bank(new Rect2(bx-2,by+1,8,2),new Color("3a5145"));
                Bank(new Rect2(bx,by-3,1,5),new Color("71875b"));
                Bank(new Rect2(bx+3,by-2,1,4),new Color("536d4b"));
                Bank(new Rect2(bx+5,by,1,2),new Color("849366"));
            }
            if(!OverworldWaterArt.Water(Region,x-1,y) && bank%9==0)
            {
                int bx=x*24+2,by=y*12+6;
                Bank(new Rect2(bx,by,5,2),new Color("354e47"));
                Bank(new Rect2(bx,by-1,4,2),new Color("68766a"));
                Bank(new Rect2(bx,by-1,2,1),new Color("89917b"));
            }
            uint h=OverworldWaterArt.Hash(Region.Seed,x,y,7);if(h%7!=0)continue;
            int px=5+(int)(h>>8)%14,py=3+(int)(h>>16)%6;
            if(OverworldWaterArt.Distance(Region,x,y,x*24+px,y*24+py*2)<7)continue;
            glints.Rect(new Rect2(x*24+px,y*12+py,3+h%3,1),new Color(.42f,.59f,.55f,.2f));
        }
        glintShader ??= new Shader {Code="shader_type canvas_item; render_mode unshaded; uniform float motion = 0.0; uniform float preview_time = -1.0; varying vec2 place; void vertex(){place=VERTEX;} void fragment(){float t=preview_time<0.0?TIME:preview_time;float wave=0.55+0.45*sin(t*0.7+place.x*0.018+place.y*0.009);COLOR.a*=mix(0.55,wave,motion);}"};
        glintMaterial=new ShaderMaterial {Shader=glintShader};reduced=ReducedMotion();glintMaterial.SetShaderParameter("motion",reduced?0f:1f);
        AddChild(new WaterGlints {Batch=details,ZIndex=1});
        AddChild(new WaterGlints {Batch=glints,Material=glintMaterial});
        LastVertexBytes=surface.VertexBytes+details.VertexBytes+glints.VertexBytes;LastBuildMilliseconds=timer.Elapsed.TotalMilliseconds;BuildCount++;
    }
    // Deterministic shader-only clock for frozen visual fixtures; negative restores engine time.
    public void SetAnimationPreviewTime(float time)=>glintMaterial?.SetShaderParameter("preview_time",time);
    public override void _Process(double delta)
    {
        bool value=ReducedMotion();if(value==reduced)return;
        reduced=value;glintMaterial?.SetShaderParameter("motion",reduced?0f:1f);
    }
    public override void _Draw()=>surface.Draw(this);
    public override void _ExitTree(){surface.Dispose();details.Dispose();glints.Dispose();surfaceMaterial?.Dispose();glintMaterial?.Dispose();field?.Dispose();}
}
public partial class WaterGlints : Node2D
{
    public TerrainBatch Batch { get; init; } = null!;
    public override void _Draw()=>Batch.Draw(this);
}
