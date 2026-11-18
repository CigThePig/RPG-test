using Godot;
using PixelCreatures.Runtime;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
    public const double MainThreadBudgetMilliseconds=2;
    public const int MaximumCachedChunks=8;
    public RegionState Region {get;init;}=null!;
    public Node2D ActorLayer {get;init;}=null!;
    public Dictionary<string,Texture2D> Art {get;init;}=null!;
    public Func<Vector2> PlayerPosition {get;init;}=()=>Vector2.Zero;
    public Func<Vector2> ViewportPixels {get;init;}=()=>new Vector2(1280,800);
    public Func<Vector2> Zoom {get;init;}=()=>Vector2.One;
    public Func<bool> ReducedMotion {get;init;}=()=>false;
    public Func<bool> ShowCharacters {get;init;}=()=>true;

    sealed class Loaded
    {
        public readonly List<Node> Ground=new(),Actors=new();
        public int Stage;
        public ChunkLookup? Lookup;
        public OverworldGroundRenderer? Soil;
        public long Bytes;
    }
    readonly Dictionary<Vector2I,Loaded> live=new(),cache=new();
    readonly Dictionary<Vector2I,Task<ChunkLookup>> preparing=new();
    readonly Queue<Node> release=new();
    readonly CancellationTokenSource cancellation=new();
    static readonly bool baseline=OS.GetCmdlineUserArgs().Contains("--visual-check")&&OS.GetCmdlineUserArgs().Contains("--stream-baseline");
    RegionState snapshot=null!;
    Loaded? building;
    Vector2I buildingId;
    public int LoadedChunks=>live.Count;
    public bool Settled=>building==null&&preparing.Count==0;
    public bool CoversVisible=>Required(Region,PlayerPosition(),ViewportPixels(),Zoom(),false).All(live.ContainsKey);
    public int CachedChunks=>cache.Count;
    public int PreparedChunks=>preparing.Count(p=>p.Value.IsCompletedSuccessfully);
    public int PendingDisposals=>release.Count;
    public int CacheHits {get;private set;}
    public int CacheMisses {get;private set;}
    public long LookupBytes=>live.Values.Concat(cache.Values).Sum(c=>c.Bytes)+(building?.Bytes??0);
    public int LoadedCells=>live.Keys.Sum(id=>Cells(Region,id).Size.X*Cells(Region,id).Size.Y);
    public readonly record struct Sample(string Stage,double Ms,int Active,int Cached,long LookupBytes,string Chunk);
    public readonly List<Sample> Measurements=new();
    public double LastMainThreadMs {get;private set;}
    public double WorstMainThreadMs {get;private set;}
    void Record(string stage,double ms,Vector2I? id=null)
    {
        if(Measurements.Count>=4096)Measurements.RemoveRange(0,1024);
        Measurements.Add(new(stage,ms,live.Count,cache.Count,LookupBytes,(id??(building!=null?buildingId:new Vector2I(-1,-1))).ToString()));
    }

    public static Rect2I Cells(RegionState region,Vector2I id)
    {
        int x=id.X*ChunkTiles,y=id.Y*ChunkTiles;
        if(x<0||y<0||x>=region.Width||y>=region.Height)throw new ArgumentOutOfRangeException(nameof(id));
        return new Rect2I(x,y,Math.Min(ChunkTiles,region.Width-x),Math.Min(ChunkTiles,region.Height-y));
    }

    /// <summary>Camera-driven visible set. 160x132 screen pixels of overscan covers raised
    /// props, camera smoothing and a normal movement step without a hard scenery edge.</summary>
    public static List<Vector2I> Required(RegionState region,Vector2 player,Vector2 viewport,Vector2 zoom,bool overscan=true)
    {
        Vector2 visibleHalf=new(viewport.X/Math.Max(.1f,zoom.X)/2,viewport.Y/Math.Max(.1f,zoom.Y)/2);
        // At world edges the camera moves away from the player to fill the viewport.
        // Plan from that clamped centre so the far side is fully detailed when zoomed out.
        Vector2 centre=DisplayLayout.CameraCenter(GodotConvert.GroundToScreen(player.X,player.Y),
            GodotConvert.GroundToScreen(region.Width*24,region.Height*24),visibleHalf);
        Vector2 half=visibleHalf+new Vector2(overscan?160:0,overscan?132:0);
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
        // Resolve command-line/static feature flags on the main thread before workers.
        _=OverworldGroundRenderer.Enabled;_=OverworldWaterArt.Part1;_=OverworldRockRenderer.Enabled;
        snapshot=new RegionState {Seed=Region.Seed,Width=Region.Width,Height=Region.Height,Tiles=(Terrain[])Region.Tiles.Clone()};
        // A small merged color mesh supplies terrain immediately after teleports and
        // during cold startup. Detailed layers cover it as complete chunks activate.
        if(Region.Crossings.Count>0)AddChild(new ReachBridge {Region=Region,ZIndex=1});
        AddChild(new OverworldDistantTerrain {Region=Region,ZIndex=-3});
        Refresh();
    }
    public override void _Process(double delta)=>Refresh();

    static void Visible(Loaded c,bool value)
    {foreach(var node in c.Ground.Concat(c.Actors).OfType<CanvasItem>())node.Visible=value;}
    void Retire(Loaded c)
    {Visible(c,false);foreach(var node in c.Ground.Concat(c.Actors))release.Enqueue(node);}

    public void Refresh()
    {
        if(!IsInsideTree())return;
        if(baseline){RefreshBaseline();return;}
        var timer=Stopwatch.StartNew();
        Vector2 focus=PlayerPosition(),viewport=ViewportPixels(),zoom=Zoom();
        // Visible chunks come first; another camera-sized margin prefetches normal
        // travel and absorbs shader/upload latency without a fixed world radius.
        var visible=Required(Region,focus,viewport,zoom);
        var wanted=visible.Concat(Required(Region,focus,viewport+new Vector2(768,768)*zoom,zoom)).Distinct().ToList();
        var required=wanted.ToHashSet();Record("plan",timer.Elapsed.TotalMilliseconds);
        foreach(var id in live.Keys.Where(id=>!required.Contains(id)).ToArray())
        {var hide=Stopwatch.StartNew();var c=live[id];live.Remove(id);Visible(c,false);cache.Add(id,c);Record("deactivate",hide.Elapsed.TotalMilliseconds,id);}
        foreach(var id in wanted)
            if(cache.Remove(id,out var c)){live.Add(id,c);Visible(c,true);CacheHits++;}
        while(cache.Count>MaximumCachedChunks)
        {
            // Keep the closest chunks for a direction reversal.
            var id=cache.Keys.OrderByDescending(k=>new Vector2(k.X*32*24,k.Y*32*24).DistanceSquaredTo(focus)).First();
            var evict=Stopwatch.StartNew();var c=cache[id];cache.Remove(id);Retire(c);Record("evict",evict.Elapsed.TotalMilliseconds,id);
        }
        if(building!=null&&!required.Contains(buildingId)){Retire(building);building=null;}
        foreach(var id in preparing.Keys.Where(id=>!required.Contains(id)&&preparing[id].IsCompleted).ToArray())
        {if(preparing[id].IsFaulted)GD.PushError(preparing[id].Exception!.ToString());preparing.Remove(id);}
        // At most two workers, including obsolete jobs still finishing; never
        // flood the thread pool when the camera is repeatedly teleported.
        foreach(var id in wanted)
        {
            if(preparing.Count>=1)break;
            if(live.ContainsKey(id)||preparing.ContainsKey(id)||(building!=null&&buildingId==id))continue;
            var cells=Cells(snapshot,id);var token=cancellation.Token;
            preparing.Add(id,Task.Run(()=>ChunkLookup.Build(snapshot,cells,token),token));CacheMisses++;
        }
        if(building==null)
            foreach(var id in wanted)
            {
                if(!preparing.TryGetValue(id,out var task)||!task.IsCompleted)continue;
                if(task.IsFaulted){GD.PushError(task.Exception!.ToString());preparing.Remove(id);continue;}
                if(task.IsCanceled){preparing.Remove(id);continue;}
                var data=task.Result;preparing.Remove(id);buildingId=id;building=new Loaded{Lookup=data,Bytes=data.Bytes};Record("prepare-worker",data.PrepareMs);break;
            }
        // Each step is independently bounded (one lookup upload or one row of
        // scenery creation). Never construct several complete chunks in a frame.
        if(building!=null&&timer.Elapsed.TotalMilliseconds<MainThreadBudgetMilliseconds)
        {
            double imageBefore=ChunkLookup.ImageCopyMilliseconds,textureBefore=ChunkLookup.TextureUploadMilliseconds,meshBefore=TerrainBatch.UploadMilliseconds;
            var step=Stopwatch.StartNew();string stage=BuildStep(buildingId,building);
            Record(stage,step.Elapsed.TotalMilliseconds);
            if(ChunkLookup.ImageCopyMilliseconds>imageBefore)Record("image-copy",ChunkLookup.ImageCopyMilliseconds-imageBefore);
            if(ChunkLookup.TextureUploadMilliseconds>textureBefore)Record("texture-upload",ChunkLookup.TextureUploadMilliseconds-textureBefore);
            if(TerrainBatch.UploadMilliseconds>meshBefore)Record("mesh-upload",TerrainBatch.UploadMilliseconds-meshBefore);
            if(building.Stage==6){var activate=Stopwatch.StartNew();Visible(building,true);building.Lookup=null;live.Add(buildingId,building);building=null;Record("activate",activate.Elapsed.TotalMilliseconds,buildingId);}
        }
        // Release only one node per frame; cached resources remain alive in-tree,
        // since detaching them would run _ExitTree and dispose their materials.
        while(release.Count>0&&timer.Elapsed.TotalMilliseconds<MainThreadBudgetMilliseconds)
        {var step=Stopwatch.StartNew();var node=release.Dequeue();node.GetParent()?.RemoveChild(node);node.QueueFree();Record("release",step.Elapsed.TotalMilliseconds);}
        LastMainThreadMs=timer.Elapsed.TotalMilliseconds;WorstMainThreadMs=Math.Max(WorstMainThreadMs,LastMainThreadMs);
    }

    // Test-only frozen scheduling/resource path for comparable A/B recordings.
    // Ordinary play cannot select it. Geography, view and travel remain identical.
    void RefreshBaseline()
    {
        var timer=Stopwatch.StartNew();var wanted=Required(Region,PlayerPosition(),ViewportPixels(),Zoom());
        foreach(var id in live.Keys.Where(id=>!wanted.Contains(id)).ToArray())
        {
            var c=live[id];foreach(var n in c.Ground.Concat(c.Actors)){n.GetParent()?.RemoveChild(n);n.QueueFree();}live.Remove(id);
        }
        int count=0;
        foreach(var id in wanted)
        {
            if(live.ContainsKey(id))continue;if(count++>=3)break;
            var cells=Cells(Region,id);bool water=false,rock=false;
            for(int y=cells.Position.Y;y<cells.End.Y;y++)for(int x=cells.Position.X;x<cells.End.X;x++)
            {water|=Region.Tiles[y*Region.Width+x]==Terrain.Water;rock|=Region.Tiles[y*Region.Width+x]==Terrain.Rock;}
            var c=new Loaded{Lookup=new ChunkLookup(0,0,Array.Empty<byte>(),water?Array.Empty<byte>():null,rock?Array.Empty<byte>():null,0)};
            for(int step=0;step<6;step++){var watch=Stopwatch.StartNew();string stage=BuildStep(id,c);Record(stage,watch.Elapsed.TotalMilliseconds);}
            Visible(c,true);c.Lookup=null;live.Add(id,c);
        }
        LastMainThreadMs=timer.Elapsed.TotalMilliseconds;WorstMainThreadMs=Math.Max(WorstMainThreadMs,LastMainThreadMs);
    }

    string BuildStep(Vector2I id,Loaded c)
    {
        var cells=Cells(Region,id);
        void Ground(Node2D n){n.Visible=false;AddChild(n);c.Ground.Add(n);}
        void Actor(Node2D n){n.Visible=false;ActorLayer.AddChild(n);c.Actors.Add(n);}
        int stage=c.Stage++;
        switch(stage)
        {
            case 0:Ground(new TerrainView {Region=Region,Art=Art,Cells=cells});return "terrain";
            case 1:
                if(OverworldGroundRenderer.Enabled)Ground(c.Soil=new OverworldGroundRenderer {Region=Region,Cells=cells,Prepared=baseline?null:c.Lookup,ZIndex=-1});return "ground-upload-mesh";
            case 2:
                if(c.Lookup!.Rock!=null&&OverworldRockRenderer.Enabled)Ground(new OverworldRockRenderer {Region=Region,Cells=cells,Prepared=baseline?null:c.Lookup});return "rock-upload-mesh";
            case 3:
                if(c.Lookup!.Water!=null&&OverworldWaterRenderer.UseLivingReach)Ground(new OverworldWaterRenderer {Region=Region,Cells=cells,Prepared=baseline?null:c.Lookup,Ground=c.Soil,ZIndex=-1,ReducedMotion=ReducedMotion});return "water-upload-mesh";
            case 4:foreach(var n in EnvironmentDecoration.Create(Region,Art,cells))Actor(n);return "decoration-nodes";
            default:
                if(OverworldFlora.Enabled)
                {var flora=OverworldFlora.Create(Region,cells);foreach(var n in flora.Standing)Actor(n);if(flora.Flat!=null)Ground(flora.Flat);}
                return "flora-nodes";
        }
    }
    public override void _ExitTree()
    {
        cancellation.Cancel();cancellation.Dispose();
        // Observe worker faults even when a region transition discards the streamer.
        foreach(var task in preparing.Values)_=task.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        foreach(var c in live.Values.Concat(cache.Values).Concat(building==null?Array.Empty<Loaded>():new[]{building}))
            foreach(var node in c.Actors){node.GetParent()?.RemoveChild(node);node.QueueFree();}
        foreach(var node in release)
            if(node.GetParent()==ActorLayer){ActorLayer.RemoveChild(node);node.QueueFree();}
        live.Clear();cache.Clear();preparing.Clear();release.Clear();building=null;
    }
}

/// <summary>Low-cost authoritative silhouette beneath the detailed streaming layers.
/// Complete merged rows ensure cold teleports never expose black/empty scenery.</summary>
public partial class OverworldDistantTerrain : Node2D
{
    public RegionState Region {get;init;}=null!;
    readonly TerrainBatch batch=new();
    public override void _Ready()
    {
        for(int y=0;y<Region.Height;y++)for(int x=0;x<Region.Width;)
        {
            int start=x;var t=Region.Tiles[y*Region.Width+x];while(x<Region.Width&&Region.Tiles[y*Region.Width+x]==t)x++;
            var color=t switch {Terrain.Water=>new Color("21557e"),Terrain.Rock or Terrain.Wall=>new Color("414a59"),Terrain.Earth=>new Color("80634b"),Terrain.Floor=>new Color("6a4c34"),_=>new Color("376b44")};
            batch.Rect(new Rect2(start*24,y*12,(x-start)*24,12),color);
        }
        SetProcess(false);
    }
    public override void _Draw()=>batch.Draw(this);
    public override void _ExitTree()=>batch.Dispose();
}
