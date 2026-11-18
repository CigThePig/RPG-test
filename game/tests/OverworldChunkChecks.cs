using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>Expanded authored-world dimensions and chunk planning regressions.
/// A chunk is presentation only: region tiles, world RNG and saves stay authoritative.</summary>
public static class OverworldChunkChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        const int width=320,height=240;
        var a=Regions.Overworld(42,width,height);
        var b=Regions.Overworld(42,width,height);
        check(a.Tiles.Length==width*height && a.Tiles.Length>=20*64*48,
            "new Reach has at least twenty times the original playable area");
        check(a.Tiles.SequenceEqual(b.Tiles) && a.Entrance==b.Entrance && a.Objective==b.Objective,
            "expanded world terrain and features are deterministic");
        check(a.Town!=null && a.Town.Houses.Count==6,"expanded Reach retains six walkable town buildings");
        check(a.Objective%width==56 && a.Entrance%width==8,
            "quest portal and player start stay near original town");
        var original=Regions.Overworld(42);
        check(original.Width==64 && original.Height==48 && original.Objective%original.Width==56,
            "reference world dimensions remain unchanged");
        int nx=(width+OverworldChunkStreamer.ChunkTiles-1)/OverworldChunkStreamer.ChunkTiles;
        int ny=(height+OverworldChunkStreamer.ChunkTiles-1)/OverworldChunkStreamer.ChunkTiles;
        int sum=0;
        for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
        {
            var box=OverworldChunkStreamer.Cells(a,new Vector2I(x,y));
            sum+=box.Size.X*box.Size.Y;
            check(box.Size.X>0 && box.Size.Y>0 && box.Size.X<=32 && box.Size.Y<=32,
                "bounded chunk geometry "+x+":"+y);
        }
        check(sum==a.Tiles.Length && OverworldChunkStreamer.Cells(a,new Vector2I(nx-1,ny-1)).Size.Y==16,
            "partial edge chunks tile the full world without overlap");
        var town=a.Center(a.Entrance);
        var near=OverworldChunkStreamer.Required(a,new Vector2(town.X,town.Y),new Vector2(2340,1080),new Vector2(1.25f,1.25f));
        var far=OverworldChunkStreamer.Required(a,new Vector2((width-4)*24,(height-4)*24),new Vector2(2340,1080),new Vector2(1.25f,1.25f));
        check(near.Count>1 && near.Count<40 && far.Count<40,
            "phone viewport requests bounded scenery chunks instead of the whole world");
        check(near.Distinct().Count()==near.Count && far.Distinct().Count()==far.Count &&
            near.All(id=>id.X>=0 && id.Y>=0 && id.X<nx && id.Y<ny) &&
            far.All(id=>id.X>=0 && id.Y>=0 && id.X<nx && id.Y<ny),
            "chunk planning is unique and clips to world edges");
        check(!near.SequenceEqual(far) && near.Any(id=>id.X==0) && far.Any(id=>id.X==nx-1),
            "moving across the Reach changes the required loaded area");
        // These are viewport corners after real camera clamping, not a rectangle
        // centred on an off-centre player near the map boundary.
        foreach(float z in new[]{.75f,1f,1.25f,2.75f})
        foreach(var focus in new[]{Vector2.Zero,new Vector2(width*24-1,height*24-1),new Vector2(8*24,120*24)})
        {
            var viewport=new Vector2(2340f/1080*800,800);var half=viewport/z/2;
            var centre=DisplayLayout.CameraCenter(GodotConvert.GroundToScreen(focus.X,focus.Y),new Vector2(width*24,height*12),half);
            var required=OverworldChunkStreamer.Required(a,focus,viewport,Vector2.One*z,false).ToHashSet();
            foreach(var point in new[]{centre-half,centre+half-new Vector2(.01f,.01f)})
            {
                int cx=Math.Clamp((int)MathF.Floor(point.X/(32*24)),0,nx-1),cy=Math.Clamp((int)MathF.Floor(point.Y/(32*12)),0,ny-1);
                check(required.Contains(new(cx,cy)),"clamped viewport corner has detailed chunk at zoom "+z);
            }
        }
        check(a.Features.Keys.All(at=>at>=0 && at<a.Tiles.Length) &&
            a.Features.ContainsKey(a.Objective),"expanded world preserves feature indices");
    }
}
