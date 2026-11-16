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
        // One ruin atlas for the process: every module painted, indexed and gutter-safe.
        var atlas=DungeonPropAtlas.Texture;
        check(ReferenceEquals(atlas,DungeonPropAtlas.Texture) && DungeonPropAtlas.BuildCount==1,"one ruin atlas reused across regions");
        check(atlas.GetWidth()==DungeonPropArt.Columns*72 && atlas.GetHeight()==DungeonPropArt.Rows*104 && DungeonPropArt.Rows*DungeonPropArt.Columns>=DungeonPropArt.Modules,"ruin atlas holds every module");
        var seen=new HashSet<int>();
        foreach(var prop in Enum.GetValues<DungeonProp>())for(int v=0;v<DungeonPropArt.Count(prop);v++)
        {
            int module=DungeonPropArt.Module(prop,v);var data=DungeonPropArt.Generate(prop,v);
            check(seen.Add(module) && module<DungeonPropArt.Modules && DungeonPropArt.Kind(module)==prop,"distinct ruin module "+prop+v);
            check(data.Length==72*104 && data.All(n=>n<OverworldPropArt.Palette.Length) && data.Count(n=>n>0)>12,"valid indexed ruin art "+prop+v);
            bool ring=true;
            for(int x=0;x<72;x++)ring&=data[x]==0 && data[103*72+x]==0;
            for(int y=0;y<104;y++)ring&=data[y*72]==0 && data[y*72+71]==0;
            check(ring,"transparent gutter round "+prop+v);
            check(data.SequenceEqual(DungeonPropArt.Generate(prop,v)),"repeatable ruin art "+prop+v);
        }
        check(DungeonArt.Ramp.Length==36 && DungeonArt.Ramp.All(c=>c.A==1),"opaque ruin surface ramp");
        var digest=new List<uint>();
        foreach(int seed in new[]{1,42,1337})
        {
            foreach(int x in new[]{int.MinValue,-1,0,1,int.MaxValue})
            {
                uint h=EnvironmentStyle.Hash(seed,x,-x,17);digest.Add(h);
                check(h==EnvironmentStyle.Hash(seed,x,-x,17),"cosmetic hash repeat "+seed+":"+x);
            }
            var r=Regions.Dungeon(seed);
            string before=System.Text.Json.JsonSerializer.Serialize(r,Content.Json);
            var masonry=DungeonArt.Masonry(r);var props=DungeonArt.PropTiles(r);
            check(Enumerable.Range(0,r.Tiles.Length).All(at=>masonry[at]==(r.Tiles[at]==Terrain.Wall && !props.Contains(at))),"masonry is every wall tile without a structural prop "+seed);
            check(props.Count==r.Landmarks.Sum(l=>l.Anchors.Count(a=>a.Collision=="terrain")) && props.All(at=>r.Tiles[at]==Terrain.Wall),"structural props stand on their own blocking tiles "+seed);
            // The distance field agrees with the masonry it was measured from.
            bool signed=true,bounded=true;
            for(int at=0;at<r.Tiles.Length;at++)
            {
                float d=DungeonArt.Distance(masonry,r,(at%r.Width+.5f)*24,(at/r.Width+.5f)*24);
                signed&=masonry[at]?d<0:d>0;bounded&=MathF.Abs(d)>=12 && MathF.Abs(d)<=DungeonArt.Reach;
            }
            check(signed && bounded,"signed bounded wall distance "+seed);
            // Torches and banners hang only on a straight run of visible face above clear floor.
            var fixtures=DungeonDressing.Fixtures(r,masonry);
            check(fixtures.SequenceEqual(DungeonDressing.Fixtures(r,masonry)) && fixtures.Count(f=>f.Banner<0)>=4,"stable wall fixtures "+seed);
            check(fixtures.All(f=>masonry[f.Y*r.Width+f.X] && masonry[f.Y*r.Width+f.X-1] && masonry[f.Y*r.Width+f.X+1] && r.Tiles[(f.Y+1)*r.Width+f.X]==Terrain.Floor
                && !props.Contains((f.Y+1)*r.Width+f.X) && !r.Features.ContainsKey((f.Y+1)*r.Width+f.X)),"fixtures hang on open wall faces "+seed);
            check(fixtures.Where(f=>f.Banner<0).GroupBy(f=>f.Y).All(row=>row.Zip(row.Skip(1),(a,b)=>b.X-a.X).All(gap=>gap>=4)),"torches keep their spacing "+seed);
            // Loose clutter stays on open room floor, clear of features, props and arrival.
            var plan=DungeonDressing.Plan(r,masonry);
            check(plan.SequenceEqual(DungeonDressing.Plan(r,masonry)) && plan.Count is >0 and <400,"stable bounded clutter "+seed);
            check(plan.All(item=>
            {
                int x=(int)(item.Ground.X/24),y=(int)(item.Ground.Y/24),at=y*r.Width+x;
                var kind=DungeonPropArt.Kind(item.Module);
                return r.Tiles[at]==Terrain.Floor && at!=r.Entrance && r.RoomAt(x,y) is {Kind:"room"} room && room.Contains(x,y)
                    && kind is >=DungeonProp.Rubble and <=DungeonProp.Sacred && !r.Features.Keys.Any(f=>Math.Abs(f%r.Width-x)<2 && Math.Abs(f/r.Width-y)<2);
            }),"clutter lies on clear room floor "+seed);
            // An item at the foot of a north wall must sort after that wall's row, or the face hides it.
            check(plan.All(item=>
            {
                int x=(int)(item.Ground.X/24),y=(int)(item.Ground.Y/24);
                return !masonry[(y-1)*r.Width+x] || MathF.Round(item.Ground.Y/2)-OverworldFlora.ActorFootDrop>y*12;
            }),"clutter stands in front of the wall behind it "+seed);
            // Light: every torch is a lamp, the bake repeats, and masonry stops it.
            var lamps=DungeonLight.Lamps(r,masonry);
            check(lamps.Count>=fixtures.Count(f=>f.Banner<0) && lamps.All(l=>l.Radius>0 && l.X>=0 && l.Y>=0 && l.X<=r.Width*24 && l.Y<=r.Height*24),"bounded lamps "+seed);
            using(var first=DungeonLight.Bake(r,masonry))using(var second=DungeonLight.Bake(r,masonry))
            {
                check(first.GetWidth()==r.Width*8+1 && first.GetHeight()==r.Height*4+1 && first.GetData().AsSpan().SequenceEqual(second.GetData()),"repeatable light map "+seed);
                var torch=fixtures.First(f=>f.Banner<0);
                check(first.GetPixel(torch.X*8+4,(torch.Y+1)*4+2).R>.8f,"torchlight reaches the floor under it "+seed);
                bool dark=true;
                for(int at=0;at<r.Tiles.Length;at++)
                {
                    int x=at%r.Width,y=at/r.Width;
                    bool buried=x>1 && y>1 && x<r.Width-2 && y<r.Height-2 && Enumerable.Range(-2,5).All(dy=>Enumerable.Range(-2,5).All(dx=>masonry[(y+dy)*r.Width+x+dx]));
                    if(buried)dark&=first.GetPixel(x*8+4,y*4+2).R==0;
                }
                check(dark,"no light inside solid masonry "+seed);
                int lit=r.Features.First(f=>f.Value!="lock").Key;
                r.Lit.Add(lit);r.Features[lit]="brazier";
                using var kindled=DungeonLight.Bake(r,masonry);
                check(kindled.GetPixel(lit%r.Width*8+4,lit/r.Width*4+2).R>.9f,"a burning brazier lights its own tile "+seed);
            }
            r=Regions.Dungeon(seed);
            var one=EnvironmentDecoration.Create(r,art);var two=EnvironmentDecoration.Create(r,art);
            check(one.Count<=EnvironmentDecoration.Maximum && one.Select(d=>(d.Position,d.PropModule)).SequenceEqual(two.Select(d=>(d.Position,d.PropModule))),"bounded deterministic decoration "+seed);
            check(one.All(d=>d.PropModule>=0 && DungeonPropArt.Kind(d.PropModule)<=DungeonProp.Collapse),"ruin decorations are structural props "+seed);
            check(!typeof(CollisionObject2D).IsAssignableFrom(typeof(EnvironmentDecoration)) && !typeof(CollisionObject2D).IsAssignableFrom(typeof(DungeonClutter)),"decorations are nonblocking presentation");
            foreach(var d in one.Concat(two))d.Free();
            foreach(var row in TerrainWallRow.Create(r))row.Free();
            foreach(var row in DungeonClutter.Create(r,masonry))row.Free();
            check(before==System.Text.Json.JsonSerializer.Serialize(r,Content.Json),"art does not mutate serialized geography "+seed);
        }
        check(digest.Distinct().Count()==digest.Count,"controlled cosmetic seed variation");
        // Run identical real simulation inputs before and after cosmetic work.
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);
        try
        {
            string Run(bool render)
            {
                Persistence.Restore(w,app.Narrative,original);
                var ruin=Regions.Dungeon(42);var walls=DungeonArt.Masonry(ruin);
                for(int tick=0;tick<120;tick++)
                {
                    if(render && tick%40==0)
                    {
                        _=DungeonDressing.Plan(ruin,walls);_=DungeonDressing.Fixtures(ruin,walls);
                        using var light=DungeonLight.Bake(ruin,walls);
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
