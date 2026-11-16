using Godot;
namespace Mosslight;

/// <summary>A point of light on the ruin's floor, in ground units.</summary>
public readonly record struct DungeonLamp(float X,float Y,float Radius,Color Colour);

/// <summary>The ruin's air, above the world and beneath the interface: darkness multiplied
/// over terrain, props and actors, opened by the light of torches, burning braziers,
/// relics and the wayfarer's lantern, plus a little drifting dust. One small light map
/// per region, rebuilt only when a light changes. Presentation only; never saved.</summary>
public partial class DungeonLight : Node2D
{
    public static readonly Color Ambient=new(.60f,.65f,.83f);
    public static readonly Color Fire=new(1f,.80f,.52f),Ward=new(.55f,.95f,1f),Day=new(.92f,.97f,1f),Glow=new(.45f,.95f,.85f);
    public const float LanternRadius=170;
    public DungeonSurface Surface {get;init;}=null!;
    /// <summary>The wayfarer's position on the canvas.</summary>
    public Func<Vector2> Lantern {get;init;}=()=>Vector2.Zero;
    static Shader? shader;
    ShaderMaterial? material;
    ImageTexture? map;
    int signature=-1;
    float drawn=float.NaN;
    Vector2 centre;
    /// <summary>Every light in the ruin, from saved state and the cosmetic plans alone.</summary>
    public static List<DungeonLamp> Lamps(RegionState r,bool[] masonry)
    {
        var lamps=new List<DungeonLamp>();
        foreach(var torch in DungeonDressing.Torches(r,masonry))lamps.Add(new((torch.X+.5f)*24,(torch.Y+1)*24+8,165,Fire));
        foreach(var (at,kind) in r.Features)
        {
            var c=r.Center(at);bool spent=r.Depleted.Contains(at)||r.OpenDoors.Contains(at);
            if(kind=="brazier"&&r.Lit.Contains(at))lamps.Add(new(c.X,c.Y,170,spent?Ward:Fire));
            if(kind=="relic"&&!spent)lamps.Add(new(c.X,c.Y,110,Ward));
            if(kind=="exit")lamps.Add(new(c.X,c.Y,130,Day));
            if(kind=="seal"&&!spent)lamps.Add(new(c.X,c.Y,60,Ward));
            if(kind=="key"&&!spent)lamps.Add(new(c.X,c.Y,50,Fire));
        }
        // Altars keep their candles burning.
        foreach(var room in r.Landmarks)foreach(var anchor in room.Anchors.Where(a=>a.Asset=="altar"))
            lamps.Add(new((room.OriginX+anchor.X+.5f)*24,(room.OriginY+anchor.Y+1.2f)*24,80,Fire));
        foreach(var item in DungeonDressing.Plan(r,masonry))if(item.Glow>0)lamps.Add(new(item.Ground.X,item.Ground.Y,item.Glow,item.Warm?Fire:Glow));
        return lamps;
    }
    /// <summary>Light reaching a canvas point: each lamp falls off with distance and stops at
    /// masonry, though it still lands on the face of the wall it strikes.</summary>
    public static Image Bake(RegionState r,bool[] masonry)
    {
        int width=r.Width*8+1,height=r.Height*4+1;
        var light=new float[width*height*3];
        foreach(var lamp in Lamps(r,masonry))
        {
            float cx=lamp.X,cy=lamp.Y/2,radius=lamp.Radius;
            int lx=(int)MathF.Floor(lamp.X/24),ly=(int)MathF.Floor(lamp.Y/24);
            for(int y=Math.Max(0,(int)((cy-radius/1.6f)/3));y<=Math.Min(height-1,(int)((cy+radius/1.6f)/3)+1);y++)
                for(int x=Math.Max(0,(int)((cx-radius)/3));x<=Math.Min(width-1,(int)((cx+radius)/3)+1);x++)
                {
                    float dx=x*3-cx,dy=(y*3-cy)*1.6f,k=1-MathF.Sqrt(dx*dx+dy*dy)/radius;
                    if(k<=0)continue;
                    int tx=Math.Min(x*3/24,r.Width-1),ty=Math.Min(y*3/12,r.Height-1);
                    if(!Sees(r,masonry,lx,ly,tx,ty))continue;
                    // Light lands on the face and coping it strikes but dies within the wall.
                    if(DungeonArt.Solid(masonry,r,tx,ty))k*=Math.Clamp(1+DungeonArt.Distance(masonry,r,x*3,y*6)/20,0,1);
                    k=k*(2-k);int at=(y*width+x)*3;
                    light[at]=1-(1-light[at])*(1-lamp.Colour.R*k);light[at+1]=1-(1-light[at+1])*(1-lamp.Colour.G*k);light[at+2]=1-(1-light[at+2])*(1-lamp.Colour.B*k);
                }
        }
        var image=Image.CreateEmpty(width,height,false,Image.Format.Rgb8);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){int at=(y*width+x)*3;image.SetPixel(x,y,new Color(light[at],light[at+1],light[at+2]));}
        return image;
    }
    // Tile walk from the lamp to the target: every tile before the last must be open.
    static bool Sees(RegionState r,bool[] masonry,int x,int y,int ex,int ey)
    {
        int dx=Math.Abs(ex-x),sx=x<ex?1:-1,dy=-Math.Abs(ey-y),sy=y<ey?1:-1,err=dx+dy;
        while(x!=ex||y!=ey)
        {
            if(DungeonArt.Solid(masonry,r,x,y))return false;
            int e=err*2;if(e>=dy){err+=dy;x+=sx;}if(e<=dx){err+=dx;y+=sy;}
        }
        return true;
    }
    static int Signature(RegionState r)=>HashCode.Combine(r.Lit.Count,r.Depleted.Count,r.OpenDoors.Count);
    public override void _Ready()
    {
        AddToGroup("dungeon-light");
        shader??=GD.Load<Shader>("res://game/art/dungeon_light.gdshader");
        material=new ShaderMaterial{Shader=shader};
        var r=Surface.Region;
        material.SetShaderParameter("light_map_size",new Vector2(r.Width*8+1,r.Height*4+1));
        material.SetShaderParameter("ambient",new Vector3(Ambient.R,Ambient.G,Ambient.B));
        material.SetShaderParameter("lantern_radius",LanternRadius);
        ReachAmbience.Register(material);Refresh();
        // The darkness is its own item so the dust above it keeps ordinary blending.
        AddChild(new Darkness{Area=new Rect2(0,-TerrainWallRow.Rise,r.Width*24,r.Height*12+TerrainWallRow.Rise),Material=material,ShowBehindParent=true});
    }
    void Refresh()
    {
        var r=Surface.Region;int now=Signature(r);
        if(now!=signature)
        {
            signature=now;
            using var image=Bake(r,Surface.Masonry);
            if(map==null){map=ImageTexture.CreateFromImage(image);material!.SetShaderParameter("light_map",map);}
            else map.Update(image);
        }
        material!.SetShaderParameter("lantern",Lantern());
    }
    public override void _ExitTree(){ReachAmbience.Unregister(material);material?.Dispose();map?.Dispose();}
    partial class Darkness : Node2D
    {
        public Rect2 Area {get;init;}
        public override void _Draw()=>DrawRect(Area,Colors.White);
    }
    public override void _Process(double delta)
    {
        Refresh();
        var view=GetViewport().GetCamera2D()?.GetScreenCenterPosition()??Vector2.Zero;
        if(ReachAmbience.Time==drawn && view.DistanceSquaredTo(centre)<110*110)return;
        QueueRedraw();
    }
    public override void _Draw()
    {
        // Dust turns slowly in the lamplight.
        var r=Surface.Region;drawn=ReachAmbience.Time;
        var camera=GetViewport().GetCamera2D();centre=camera?.GetScreenCenterPosition()??Vector2.Zero;
        var half=camera==null?new Vector2(r.Width*24,r.Height*12):GetViewportRect().Size/camera.Zoom/2+new Vector2(220,148);
        int left=Math.Max(0,(int)((centre.X-half.X)/110)),right=Math.Min(r.Width*24/110,(int)((centre.X+half.X)/110));
        int top=Math.Max(0,(int)((centre.Y-half.Y)/74)),bottom=Math.Min(r.Height*12/74,(int)((centre.Y+half.Y)/74));
        for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
        {
            var mote=OverworldAtmosphere.Mote(r.Seed,x,y,drawn);
            if(mote.Glow<=.05f)continue;
            int tx=(int)(mote.Position.X/24),ty=(int)(mote.Position.Y/12);
            if(DungeonArt.Solid(Surface.Masonry,r,tx,ty))continue;
            DrawRect(new Rect2(mote.Position,Vector2.One),new Color(.86f,.84f,.72f,mote.Glow*.45f));
        }
    }
}
