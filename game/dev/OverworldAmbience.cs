using Godot;
namespace Mosslight;

/// <summary>One presentation clock for every Living Reach shader. It advances with scaled
/// process time, is pinned to zero under reduced motion and can be frozen for fixtures.
/// Never read by simulation; nothing here is saved.</summary>
public static class ReachAmbience
{
    static readonly List<ShaderMaterial> materials=new();
    static double clock;
    static float applied=float.NaN,preview=-1;
    static bool reduced;
    /// <summary>Non-negative values replace the running clock; negative restores it.</summary>
    public static float PreviewTime {get=>preview;set{preview=value;Apply();}}
    public static float Time=>reduced?0:preview>=0?preview:(float)clock;
    public static void Register(ShaderMaterial material){materials.Add(material);material.SetShaderParameter("reach_time",Time);}
    public static void Unregister(ShaderMaterial? material){if(material!=null)materials.Remove(material);}
    public static void Advance(double delta,bool reducedMotion)
    {
        // Wrap well inside single-precision range; every consumer is periodic or slow.
        clock=(clock+delta)%7200;reduced=reducedMotion;Apply();
    }
    static void Apply()
    {
        float time=Time;if(time==applied)return;
        applied=time;foreach(var material in materials)material.SetShaderParameter("reach_time",time);
    }
}

/// <summary>Pure proximity helpers shared by the water, ground and rock lookups.</summary>
public static class ReachField
{
    /// <summary>Ground-unit distance from a point inside a cell set to its nearest edge,
    /// with convex corners rounded by <paramref name="radius"/>.</summary>
    public static float Inside(Func<int,int,bool> member,int tx,int ty,float px,float py,float radius,float limit=60)
    {
        float distance=limit;
        for(int y=ty-2;y<=ty+2;y++)for(int x=tx-2;x<=tx+2;x++)
        {
            if(member(x,y))continue;
            float dx=Math.Max(Math.Max(x*24-px,px-(x+1)*24),0),dy=Math.Max(Math.Max(y*24-py,py-(y+1)*24),0);
            distance=Math.Min(distance,MathF.Sqrt(dx*dx+dy*dy));
        }
        for(int cy=0;cy<2;cy++)for(int cx=0;cx<2;cx++)
            if(!member(tx+(cx==0?-1:1),ty) && !member(tx,ty+(cy==0?-1:1)))
            {
                float dx=cx==0?px-tx*24:(tx+1)*24-px,dy=cy==0?py-ty*24:(ty+1)*24-py;
                if(dx<radius && dy<radius)distance=Math.Min(distance,radius-MathF.Sqrt((radius-dx)*(radius-dx)+(radius-dy)*(radius-dy)));
            }
        return distance;
    }
}

/// <summary>Advances the shared clock while an overworld region is shown.</summary>
public partial class ReachAmbienceDriver : Node
{
    public Func<bool> ReducedMotion {get;init;}=()=>true;
    public override void _Ready()=>ReachAmbience.Advance(0,ReducedMotion());
    public override void _Process(double delta)=>ReachAmbience.Advance(delta,ReducedMotion());
}

/// <summary>The Reach's air, above the world and beneath the interface: drifting cloud
/// shadow multiplied over terrain, props and actors from one small process-lifetime
/// field, and a few motes of pollen. No particles or per-frame allocation.</summary>
public partial class OverworldAtmosphere : Node2D
{
    public const int CloudSize=128,CloudCells=8;
    static readonly Vector2 Cell=new(110,74);
    public RegionState Region {get;init;}=null!;
    public static bool Enabled=>OverworldRockRenderer.Enabled;
    static ImageTexture? clouds;
    static Shader? shader;
    ShaderMaterial? material;
    float drawn=float.NaN;
    Vector2 centre;
    /// <summary>Two octaves of periodic value noise, 0..1, identical in every process.</summary>
    public static float Cloud(int x,int y)
    {
        float Octave(int cells,int slot)
        {
            float gx=x*cells/(float)CloudSize,gy=y*cells/(float)CloudSize;int ix=(int)gx,iy=(int)gy;float fx=gx-ix,fy=gy-iy;
            fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
            float H(int a,int b)=>(EnvironmentStyle.Hash(0,a%cells,b%cells,9410+slot)&65535)/65535f;
            return Mathf.Lerp(Mathf.Lerp(H(ix,iy),H(ix+1,iy),fx),Mathf.Lerp(H(ix,iy+1),H(ix+1,iy+1),fx),fy);
        }
        return Octave(CloudCells,0)*.65f+Octave(CloudCells*2+2,1)*.35f;
    }
    static void Configure(ShaderMaterial material,RegionState region)
    {
        if(clouds==null)
        {
            using var image=Image.CreateEmpty(CloudSize,CloudSize,false,Image.Format.R8);
            for(int y=0;y<CloudSize;y++)for(int x=0;x<CloudSize;x++)image.SetPixel(x,y,new Color(Cloud(x,y),0,0));
            clouds=ImageTexture.CreateFromImage(image);
        }
        material.SetShaderParameter("cloud_map",clouds);
        material.SetShaderParameter("cloud_seed",(float)(EnvironmentStyle.Hash(region.Seed,0,0,9400)%512));
    }
    /// <summary>Where one cell's mote is at a given moment, and how brightly it shows.</summary>
    public static (Vector2 Position,float Glow) Mote(int seed,int cx,int cy,float time)
    {
        uint n=EnvironmentStyle.Hash(seed,cx,cy,9420);float h=(n&1023)/1023f,k=((n>>10)&1023)/1023f;
        if(h<.45f)return (default,0);
        var home=new Vector2((cx+.2f+.6f*h)*Cell.X,(cy+.2f+.6f*k)*Cell.Y);
        var drift=new Vector2(MathF.Sin(time*.31f+h*6.28f)*22,MathF.Cos(time*.23f+k*6.28f)*13-(time*2+h*40)%40*.4f);
        return ((home+drift).Floor(),.5f+.5f*MathF.Sin(time*1.4f+k*30));
    }
    public override void _Ready()
    {
        AddToGroup("overworld-atmosphere");
        shader??=GD.Load<Shader>("res://game/art/overworld_atmosphere.gdshader");
        material=new ShaderMaterial{Shader=shader};Configure(material,Region);ReachAmbience.Register(material);
        // The shadow is its own item so the pollen above it keeps ordinary blending.
        AddChild(new CloudShadow{Size=new Vector2(Region.Width*24,Region.Height*12),Material=material,ShowBehindParent=true});
    }
    public override void _ExitTree(){ReachAmbience.Unregister(material);material?.Dispose();}
    partial class CloudShadow : Node2D
    {
        public Vector2 Size {get;init;}
        public override void _Draw()=>DrawRect(new Rect2(Vector2.Zero,Size),Colors.White);
    }
    public override void _Process(double delta)
    {
        // Redraw only when the clock moved or the camera uncovered other cells.
        var view=GetViewport().GetCamera2D()?.GetScreenCenterPosition()??Vector2.Zero;
        if(ReachAmbience.Time==drawn && view.DistanceSquaredTo(centre)<Cell.X*Cell.X)return;
        QueueRedraw();
    }
    public override void _Draw()
    {
        drawn=ReachAmbience.Time;
        var camera=GetViewport().GetCamera2D();centre=camera?.GetScreenCenterPosition()??Vector2.Zero;
        var half=camera==null?new Vector2(Region.Width*24,Region.Height*12):GetViewportRect().Size/camera.Zoom/2+Cell*2;
        int left=Math.Max(0,(int)((centre.X-half.X)/Cell.X)),right=Math.Min((int)(Region.Width*24/Cell.X),(int)((centre.X+half.X)/Cell.X));
        int top=Math.Max(0,(int)((centre.Y-half.Y)/Cell.Y)),bottom=Math.Min((int)(Region.Height*12/Cell.Y),(int)((centre.Y+half.Y)/Cell.Y));
        for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
        {
            var mote=Mote(Region.Seed,x,y,drawn);
            if(mote.Glow>.05f)DrawRect(new Rect2(mote.Position,Vector2.One),new Color(1,.95f,.68f,mote.Glow*.75f));
        }
    }
}
