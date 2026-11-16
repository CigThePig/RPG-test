using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;
/// <summary>Bounded, nonblocking visual slots. Never inserted into Region.Features.</summary>
public partial class EnvironmentDecoration : Node2D
{
    public const int Maximum = 96;
    public Texture2D? Texture { get; init; }
    public Vector2 Pivot { get; init; }
    public int ReachModule {get;init;}=-1;
    public int PropModule { get; init; } = -1;
    public static List<EnvironmentDecoration> Create(RegionState r, Dictionary<string,Texture2D> art)
    {
        var result=new List<EnvironmentDecoration>();
        // Every structural anchor is required and admitted by generation's budget.
        // Ground anchors are batched in TerrainView, using no decoration nodes.
        foreach(var entry in r.Landmarks.SelectMany(room=>room.Anchors.Where(a=>a.Collision=="terrain").Select(a=>(Room:room,Anchor:a)))
            .OrderBy(e=>e.Anchor.Priority).ThenBy(e=>e.Room.Y).ThenBy(e=>e.Room.X).ThenBy(e=>e.Anchor.Y).ThenBy(e=>e.Anchor.X))
        {
            var a=entry.Anchor;var room=entry.Room;
            int asset=Array.IndexOf(new[]{"column","shelf","altar","storage","collapse"},a.Asset);
            if(asset<0)continue;
            result.Add(new EnvironmentDecoration {
                Position=GodotConvert.GroundToScreen((room.OriginX+a.X+.5f)*24,(room.OriginY+a.Y+1f)*24),
                // Templates author one variant; coordinates pick the wear so rooms do not repeat.
                PropModule=DungeonPropArt.Structural((DungeonProp)asset,Array.IndexOf(Landmarks.Themes,room.Theme),
                    a.Variant+(int)(EnvironmentStyle.Hash(r.Seed,room.OriginX+a.X,room.OriginY+a.Y,9730)%3)),
                TextureFilter=TextureFilterEnum.Nearest });
        }
        if(result.Count>Maximum)throw new InvalidDataException("Saved landmark exceeds architectural node budget");
        for(int y=1;y<r.Height-1 && result.Count<Maximum;y++)
            for(int x=1;x<r.Width-1 && result.Count<Maximum;x++)
            {
                int at=y*r.Width+x;var tile=r.Tiles[at];
                uint h=EnvironmentStyle.Hash(r.Seed,x,y,99);
                // Ruins scatter their own clutter; see DungeonDressing.
                if(tile!=Terrain.Grass || h%29!=0)continue;
                // Keep all feature stands, doorway lanes and arrival clear of tall silhouettes.
                if(r.Features.Keys.Any(f=>Math.Abs(f%r.Width-x)<3 && Math.Abs(f/r.Width-y)<3))continue;
                // The Reach leans on its trees; the frozen pre-Part-3 comparison keeps its old mix.
                bool reach=tile==Terrain.Grass && r.Rooms.Count==0 && OverworldPropAtlas.Enabled;
                uint pick=reach?(h>>3)%8:99;
                string key=reach?(pick<3?"tree1":pick==3?"rock1":"bush1"):h%4==0?"tree1":h%4==1?"rock1":"bush1";
                art.TryGetValue(key,out var texture);
                result.Add(new EnvironmentDecoration
                {
                    Position=GodotConvert.GroundToScreen((x+.5f)*24,(y+.5f)*24),
                    ReachModule=tile==Terrain.Grass && r.Rooms.Count==0 && OverworldPropAtlas.Enabled
                        ?OverworldPropArt.Module(key=="tree1"?ReachProp.Tree:key=="rock1"?ReachProp.Rock:ReachProp.Shrub,h>>8):-1,
                    Texture=tile==Terrain.Grass && !(r.Rooms.Count==0 && OverworldPropAtlas.Enabled)?texture:null,
                    Pivot=key=="tree1"?new Vector2(36,100):key=="rock1"?new Vector2(12,16):new Vector2(17,23),
                    TextureFilter=TextureFilterEnum.Nearest
                });
            }
        return result;
    }
    public override void _Draw()
    {
        if(PropModule>=0)DungeonPropAtlas.Draw(this,PropModule,new Vector2(0,-1));
        if(ReachModule>=0)OverworldPropAtlas.Draw(this,ReachModule,Vector2.Zero);
        if(Texture!=null)DrawTexture(Texture,-Pivot);
    }
}
