using Godot;
namespace Mosslight;
public static class EnvironmentArtChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        var art=new Dictionary<string,Texture2D>();
        foreach(var key in new[]{"tree1","rock1","bush1"})
        {
            check(ResourceLoader.Exists("res://game/art/"+key+".png"),"PPC environment resource "+key);
            art[key]=GD.Load<Texture2D>("res://game/art/"+key+".png");
            check(art[key].GetWidth()>0 && art[key].GetHeight()>0,"valid PPC texture "+key);
        }
        var floors=EnvironmentAssetCache.Floors;var walls=EnvironmentAssetCache.Walls;
        check(ReferenceEquals(floors,EnvironmentAssetCache.Floors) && ReferenceEquals(walls,EnvironmentAssetCache.Walls),"two atlases reused across regions");
        check(EnvironmentAssetCache.TextureCount==2 && EnvironmentAssetCache.PixelBytes<220000,"bounded environment texture memory");
        using(var pixels=floors.GetImage())
            check(pixels.GetWidth()==384 && pixels.GetHeight()==12 && pixels.GetPixel(0,0).A==1,"valid opaque floor atlas");
        foreach(int variant in Enumerable.Range(0,16))
            check(EnvironmentArtGenerator.WallModule(variant,true,true).All(m=>m.Rect.Position.X>=0 && m.Rect.Position.Y>=0 && m.Rect.End.X<=24 && m.Rect.End.Y<=28),"valid wall atlas bounds "+variant);
        var digest=new List<uint>();
        foreach(int seed in new[]{1,42,1337})
        {
            foreach(int x in new[]{int.MinValue,-1,0,1,int.MaxValue})
            {
                uint h=EnvironmentStyle.Hash(seed,x,-x,17);digest.Add(h);
                check(h==EnvironmentStyle.Hash(seed,x,-x,17),"cosmetic hash repeat "+seed+":"+x);
                var a=EnvironmentArtGenerator.FloorModule((int)h);
                check(ReferenceEquals(a,EnvironmentArtGenerator.FloorModule((int)h)),"surface module cache reuse");
                check(a.Count>8 && a.All(m=>m.Rect.Size.X>0 && m.Rect.Size.Y>0 && m.Rect.Position.X>=0 && m.Rect.Position.Y>=0 && m.Rect.End.X<=24 && m.Rect.End.Y<=12 && m.Color.A==1),"opaque bounded floor module");
            }
            var r=Regions.Dungeon(seed);
            string before=System.Text.Json.JsonSerializer.Serialize(r,Content.Json);
            var first=EnvironmentDecoration.Create(r,art);var second=EnvironmentDecoration.Create(r,art);
            check(first.Count<=EnvironmentDecoration.Maximum && first.Select(d=>d.Position).SequenceEqual(second.Select(d=>d.Position)),"bounded deterministic decoration "+seed);
            check(!typeof(CollisionObject2D).IsAssignableFrom(typeof(EnvironmentDecoration)),"decorations are nonblocking presentation");
            foreach(var d in first.Concat(second))d.Free();
            foreach(var row in TerrainWallRow.Create(r))row.Free();
            check(before==System.Text.Json.JsonSerializer.Serialize(r,Content.Json),"art does not mutate serialized geography "+seed);
        }
        check(digest.Distinct().Count()==digest.Count,"controlled cosmetic seed variation");
        check(EnvironmentArtGenerator.CachedModuleCount==16,"bounded 16-module cache");
        // Run identical real simulation inputs before and after cosmetic work.
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);
        try
        {
            string Run(bool render)
            {
                Persistence.Restore(w,app.Narrative,original);
                for(int tick=0;tick<120;tick++)
                {
                    if(render)
                    {
                        _=EnvironmentArtGenerator.FloorModule((int)EnvironmentStyle.Hash(w.RegionsById[w.Player.Region].Seed,tick,-tick));
                    }
                    w.Move(w.Player,new System.Numerics.Vector2(.3f,0));w.Step(1f/60);
                }
                return Persistence.Capture(w,app.Narrative);
            }
            check(Run(false)==Run(true),"cosmetic work leaves authoritative movement AI combat progression identical");
        }
        finally { Persistence.Restore(w,app.Narrative,original);app.RebuildViews(); }
    }
}
