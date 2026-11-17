using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>
/// Bounded, on-demand Living Reach presentation. Authoritative tiles and world actors
/// stay in RegionState for movement, pathing, quests and backward-compatible saves.
/// Only scenery that intersects the camera (plus overscan) consumes meshes and lookup
/// textures. All lookups use world coordinates and a three-tile sampling halo.
/// </summary>
public partial class OverworldChunkStreamer : Node2D
{
    public const int ChunkTiles=32;
    public const int MaximumNewChunksPerFrame=3;
    public RegionState Region {get;init;}=null!;
    public Node2D ActorLayer {get;init;}=null!;
    public Dictionary<string,Texture2D> Art {get;init;}=null!;
    public Func<Vector2> PlayerPosition {get;init;}=()=>Vector2.Zero;
    public Func<Vector2> ViewportPixels {get;init;}=()=>new Vector2(1280,800);
    public Func<Vector2> Zoom {get;init;}=()=>Vector2.One;
    public Func<bool> ReducedMotion {get;init;}=()=>false;
    public Func<bool> ShowCharacters {get;init;}=()=>true;

    sealed record Loaded(List<Node> Ground,List<Node> Actors);
    readonly Dictionary<Vector2I,Loaded> live=new();
    Vector2 lastFocus=new(float.NaN,float.NaN);
    Vector2 lastViewport, lastZoom;
    bool incomplete=true;
    public int LoadedChunks=>live.Count;
    public int LoadedCells=>live.Keys.Sum(id=>Cells(Region,id).Size.X*Cells(Region,id).Size.Y);

    public static Rect2I Cells(RegionState region,Vector2I id)
    {
        int x=id.X*ChunkTiles,y=id.Y*ChunkTiles;
        if(x<0||y<0||x>=region.Width||y>=region.Height)throw new ArgumentOutOfRangeException(nameof(id));
        return new Rect2I(x,y,Math.Min(ChunkTiles,region.Width-x),Math.Min(ChunkTiles,region.Height-y));
    }

    /// <summary>Camera-driven visible set. 160x132 screen pixels of overscan covers raised
    /// props, camera smoothing and a normal movement step without a hard scenery edge.</summary>
    public static List<Vector2I> Required(RegionState region,Vector2 player,Vector2 viewport,Vector2 zoom)
    {
        Vector2 centre=GodotConvert.GroundToScreen(player.X,player.Y);
        Vector2 half=new(viewport.X/Math.Max(.1f,zoom.X)/2+160,
            viewport.Y/Math.Max(.1f,zoom.Y)/2+132);
        int nx=(region.Width+ChunkTiles-1)/ChunkTiles,ny=(region.Height+ChunkTiles-1)/ChunkTiles;
        int left=Math.Clamp(Mathf.FloorToInt((centre.X-half.X)/(ChunkTiles*24)),0,nx-1);
        int right=Math.Clamp(Mathf.FloorToInt((centre.X+half.X)/(ChunkTiles*24)),0,nx-1);
        int top=Math.Clamp(Mathf.FloorToInt((centre.Y-half.Y)/(ChunkTiles*12)),0,ny-1);
        int bottom=Math.Clamp(Mathf.FloorToInt((centre.Y+half.Y)/(ChunkTiles*12)),0,ny-1);
        var result=new List<Vector2I>();
        for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)result.Add(new Vector2I(x,y));
        result.Sort((a,b)=>{
            float Distance(Vector2I c) {
                var rect=Cells(region,c);
                var world=GodotConvert.GroundToScreen((rect.Position.X+rect.Size.X*.5f)*24,(rect.Position.Y+rect.Size.Y*.5f)*24);
                return world.DistanceSquaredTo(centre);
            }
            int d=Distance(a).CompareTo(Distance(b));
            return d!=0?d:a.Y!=b.Y?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X);
        });
        return result;
    }

    public override void _Ready()
    {
        AddToGroup("overworld-chunks");
        SetProcess(true);
        Refresh();
    }
    public override void _Process(double delta)=>Refresh();

    public void Refresh()
    {
        if(!IsInsideTree())return;
        Vector2 focus=PlayerPosition(),viewport=ViewportPixels(),zoom=Zoom();
        if(!incomplete && focus.DistanceSquaredTo(lastFocus)<24*24 && viewport==lastViewport && zoom==lastZoom)return;
        lastFocus=focus;lastViewport=viewport;lastZoom=zoom;
        var wanted=Required(Region,focus,viewport,zoom);
        var required=new HashSet<Vector2I>(wanted);
        foreach(var id in live.Keys.Where(id=>!required.Contains(id)).ToArray())Unload(id);
        int created=0;
        foreach(var id in wanted)
        {
            if(live.ContainsKey(id))continue;
            if(created>=MaximumNewChunksPerFrame)break;
            Load(id);created++;
        }
        incomplete=live.Count<required.Count;
    }

    void Load(Vector2I id)
    {
        Rect2I cells=Cells(Region,id);
        var ground=new List<Node>();var actors=new List<Node>();
        void Ground(Node node){AddChild(node);ground.Add(node);}
        void Actor(Node2D node){ActorLayer.AddChild(node);actors.Add(node);}
        Ground(new TerrainView {Region=Region,Art=Art,Cells=cells});
        OverworldGroundRenderer? soil=null;
        if(OverworldGroundRenderer.Enabled)
            Ground(soil=new OverworldGroundRenderer {Region=Region,Cells=cells,ZIndex=-1});
        bool anyRock=false,anyWater=false;
        for(int y=cells.Position.Y;y<cells.End.Y;y++)
            for(int x=cells.Position.X;x<cells.End.X;x++)
            {
                var tile=Region.Tiles[y*Region.Width+x];
                anyRock|=tile==Terrain.Rock;
                anyWater|=tile==Terrain.Water;
            }
        if(anyRock && OverworldRockRenderer.Enabled)
            Ground(new OverworldRockRenderer {Region=Region,Cells=cells});
        if(anyWater && OverworldWaterRenderer.UseLivingReach)
            Ground(new OverworldWaterRenderer {Region=Region,Cells=cells,Ground=soil,ZIndex=-1,ReducedMotion=ReducedMotion});
        foreach(var decor in EnvironmentDecoration.Create(Region,Art,cells))Actor(decor);
        if(OverworldFlora.Enabled)
        {
            var flora=OverworldFlora.Create(Region,cells);
            foreach(var plant in flora.Standing)Actor(plant);
            if(flora.Flat!=null)Ground(flora.Flat);
        }
        live.Add(id,new Loaded(ground,actors));
    }

    void Unload(Vector2I id)
    {
        var chunk=live[id];
        foreach(var node in chunk.Ground.Concat(chunk.Actors))
        {
            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }
        live.Remove(id);
    }
    public override void _ExitTree()
    {
        // Actor-layer children are not owned by this node and must be released as
        // soon as the overworld is replaced (ruins, load game, new game).
        foreach(var id in live.Keys.ToArray())Unload(id);
    }
}
