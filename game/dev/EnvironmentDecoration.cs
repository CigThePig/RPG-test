using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;
/// <summary>Bounded, nonblocking visual slots. Never inserted into Region.Features.</summary>
public partial class EnvironmentDecoration : Node2D
{
    public const int Maximum = 96;
    public Texture2D? Texture { get; init; }
    public Vector2 Pivot { get; init; }
    public bool Rubble { get; init; }
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
                Position=GodotConvert.GroundToScreen((room.X*11+a.X+.5f)*24,(room.Y*11+a.Y+1f)*24),
                Scale=new Vector2(a.Facing is 1 or 2?-1:1,1),
                PropModule=(Array.IndexOf(Landmarks.Themes,room.Theme)*5+asset)*3+a.Variant,
                TextureFilter=TextureFilterEnum.Nearest });
        }
        if(result.Count>Maximum)throw new InvalidDataException("Saved landmark exceeds architectural node budget");
        for(int y=1;y<r.Height-1 && result.Count<Maximum;y++)
            for(int x=1;x<r.Width-1 && result.Count<Maximum;x++)
            {
                int at=y*r.Width+x;var tile=r.Tiles[at];
                uint h=EnvironmentStyle.Hash(r.Seed,x,y,99);
                if(tile is not (Terrain.Grass or Terrain.Floor) || h%29!=0)continue;
                // Keep all feature stands, doorway lanes and arrival clear of tall silhouettes.
                if(r.Features.Keys.Any(f=>Math.Abs(f%r.Width-x)<3 && Math.Abs(f/r.Width-y)<3))continue;
                if(tile==Terrain.Floor && (x%11 is 0 or 1 or 4 or 5 or 6 or 9 or 10 || y%11 is 0 or 1 or 4 or 5 or 6 or 9 or 10))continue;
                string key=h%4==0?"tree1":h%4==1?"rock1":"bush1";
                art.TryGetValue(key,out var texture);
                result.Add(new EnvironmentDecoration
                {
                    Position=GodotConvert.GroundToScreen((x+.5f)*24,(y+.5f)*24),
                    Texture=tile==Terrain.Grass?texture:null, Rubble=tile==Terrain.Floor,
                    Pivot=key=="tree1"?new Vector2(36,100):key=="rock1"?new Vector2(12,16):new Vector2(17,23),
                    TextureFilter=TextureFilterEnum.Nearest
                });
            }
        return result;
    }
    public override void _Draw()
    {
        if(PropModule>=0)DrawTextureRectRegion(EnvironmentAssetCache.Props,new Rect2(-13,-62,26,64),new Rect2(PropModule*26,0,26,64));
        if(Texture!=null)DrawTexture(Texture,-Pivot);
        if(Rubble)
        {
            DrawRect(new Rect2(-5,-1,11,3),new Color(0,0,0,.15f));
            DrawColoredPolygon(new[]{new Vector2(-4,0),new Vector2(-3,-4),new Vector2(0,-5),new Vector2(3,-2),new Vector2(3,1)},EnvironmentStyle.Stone);
            DrawRect(new Rect2(-3,-4,3,1),EnvironmentStyle.Cap);
            DrawRect(new Rect2(4,-1,3,2),EnvironmentStyle.Shade);
        }
    }
}
