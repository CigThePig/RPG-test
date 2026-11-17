using Godot;
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
        check(a.Features.Keys.All(at=>at>=0 && at<a.Tiles.Length) &&
            a.Features.ContainsKey(a.Objective),"expanded world preserves feature indices");
    }
}
