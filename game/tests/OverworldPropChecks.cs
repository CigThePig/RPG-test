using Godot;
using System.Text.Json;
namespace Mosslight;

public static class OverworldPropChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        foreach(var asset in Enum.GetValues<ReachProp>())for(int variant=0;variant<4;variant++)
        {
            var a=OverworldPropArt.Generate(asset,variant);
            check(a.SequenceEqual(OverworldPropArt.Generate(asset,variant)),"stable Reach prop "+asset+variant);
            check(a.Length==72*104 && a.All(n=>n<OverworldPropArt.Palette.Length) && a.Count(n=>n>0)>40,"valid indexed Reach prop "+asset+variant);
            check(Enumerable.Range(0,72).All(x=>a[x]==0 && a[103*72+x]==0) && Enumerable.Range(0,104).All(y=>a[y*72]==0 && a[y*72+71]==0),"transparent atlas gutter "+asset+variant);
        }
        foreach(string kind in new[]{"wood","ore","herb","chest"})
        {
            OverworldPropArt.TryFeature(kind,false,out var ready);OverworldPropArt.TryFeature(kind,true,out var spent);
            check(!OverworldPropArt.Generate(ready,0).SequenceEqual(OverworldPropArt.Generate(spent,0)),"distinct resource depletion "+kind);
        }
        var atlas=OverworldPropAtlas.Texture;
        check(ReferenceEquals(atlas,OverworldPropAtlas.Texture) && OverworldPropAtlas.BuildCount==1 && OverworldPropAtlas.PixelBytes==OverworldPropArt.Columns*72L*OverworldPropArt.Rows*104*4 && OverworldPropArt.Rows*OverworldPropArt.Columns>=OverworldPropArt.Modules,"one fixed Reach atlas across all regions");
        var art=new Dictionary<string,Texture2D>();
        foreach(int seed in new[]{1,42,1337})
        {
            var r=Regions.Overworld(seed);string before=JsonSerializer.Serialize(r,Content.Json);
            var first=EnvironmentDecoration.Create(r,art);var second=EnvironmentDecoration.Create(r,art);
            check(first.Count<=EnvironmentDecoration.Maximum && first.Select(d=>(d.Position,d.ReachModule)).SequenceEqual(second.Select(d=>(d.Position,d.ReachModule))),"bounded stable Reach placement "+seed);
            check(first.All(d=>d.ReachModule>=0 && d.ReachModule<OverworldPropArt.Modules),"overworld decorations use fixed modules "+seed);
            check(first.Where(d=>d.ReachModule/4==(int)ReachProp.Tree).Select(d=>d.ReachModule%4).Distinct().Count()>1,
                "decorative trees use independent crown variants "+seed);
            foreach(var d in first.Concat(second))d.Free();
            // Part 4 growth, outcrops and atmosphere are equally pure functions of the region.
            var growth=OverworldFlora.Plan(r);
            check(growth.SequenceEqual(OverworldFlora.Plan(r)) && growth.Count<r.Tiles.Length/6,"bounded stable Reach flora "+seed);
            check(growth.All(g=>g.Module>=(int)ReachProp.WaterFlora*4 && g.Module<OverworldPropArt.Modules),"flora uses its own fixed modules "+seed);
            check(growth.All(g=>{int x=(int)(g.Foot.X/24),y=(int)(g.Foot.Y/12);return !r.Features.ContainsKey(y*r.Width+x) && r.Tiles[y*r.Width+x] is Terrain.Grass or Terrain.Water;}),"flora keeps features and bare ground clear "+seed);
            var reeds=growth.Where(g=>g.Module/4==(int)ReachProp.WaterFlora && !g.Flat).ToList();
            check(reeds.Count>0 && reeds.All(g=>{int x=(int)(g.Foot.X/24),y=(int)(g.Foot.Y/12);float gx=g.Foot.X,gy=g.Foot.Y*2;
                float edge=OverworldWaterArt.Distance(r,x,y,gx,gy)-OverworldWaterArt.BankInset(r,x,y,gx,gy);
                return r.Tiles[y*r.Width+x]==Terrain.Water && edge is >=4.9f and <=10.1f;}),"reeds root in the visible shallows "+seed);
            check(growth.Where(g=>g.Flat).All(g=>r.Tiles[(int)(g.Foot.Y/12)*r.Width+(int)(g.Foot.X/24)]==Terrain.Water),"only lily pads lie in the ground layer "+seed);
            for(int y=0;y<r.Height;y+=5)for(int x=0;x<r.Width;x+=5)
            {
                float d=OverworldRockRenderer.Distance(r,x,y,x*24+12,y*24+12);
                check(d is >=0 and <=60 && OverworldRockRenderer.Inset(r,x*24,y*24) is >=0 and <=8,"bounded outcrop field "+seed);
            }
            check(Enumerable.Range(0,r.Tiles.Length).All(n=>!(OverworldRockRenderer.Lone(r,n%r.Width,n/r.Width)&&OverworldRockRenderer.Rock(r,n%r.Width,n/r.Width))),"a rock cell is a boulder or an outcrop, never both "+seed);
            check(before==JsonSerializer.Serialize(r,Content.Json),"Reach props preserve saved geography "+seed);
        }
        var crag=new RegionState{Width=5,Height=3,Tiles=Enumerable.Repeat(Terrain.Grass,15).ToArray()};
        crag.Tiles[5]=crag.Tiles[6]=crag.Tiles[9]=Terrain.Rock;
        check(OverworldRockRenderer.Rock(crag,0,1)&&OverworldRockRenderer.Rock(crag,1,1)&&!OverworldRockRenderer.Lone(crag,1,1),"adjacent rock cells join one outcrop");
        check(OverworldRockRenderer.Rock(crag,4,1)&&OverworldRockRenderer.Rock(crag,-1,1),"boundary rock continues beyond the region");
        crag.Tiles[9]=Terrain.Grass;crag.Tiles[8]=Terrain.Rock;
        check(OverworldRockRenderer.Lone(crag,3,1)&&!OverworldRockRenderer.Rock(crag,3,1),"an isolated rock cell is a lone boulder");
        check(Enumerable.Range(0,OverworldAtmosphere.CloudSize).All(n=>OverworldAtmosphere.Cloud(n,7) is >=0 and <=1 && OverworldAtmosphere.Cloud(n,7)==OverworldAtmosphere.Cloud(n,7)),"bounded stable cloud field");
        // The shared presentation clock: frozen by reduced motion, overridable for fixtures.
        float held=ReachAmbience.PreviewTime;
        try
        {
            ReachAmbience.PreviewTime=-1;ReachAmbience.Advance(.5,false);float running=ReachAmbience.Time;
            ReachAmbience.Advance(.25,false);check(ReachAmbience.Time>running,"ambience follows scaled process time");
            ReachAmbience.Advance(.25,true);check(ReachAmbience.Time==0,"reduced motion pins every Reach shader to one frame");
            ReachAmbience.PreviewTime=4;check(ReachAmbience.Time==0,"reduced motion outranks a preview clock");
            ReachAmbience.Advance(0,false);check(ReachAmbience.Time==4,"fixtures can freeze the ambience clock");
        }
        finally{ReachAmbience.PreviewTime=held;}
        var dungeon=EnvironmentDecoration.Create(Regions.Dungeon(42),art);
        check(dungeon.All(d=>d.ReachModule<0),"dungeon keeps its existing prop renderer");foreach(var d in dungeon)d.Free();
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);
        try
        {
            string Simulate(bool render)
            {
                Persistence.Restore(w,app.Narrative,snapshot);
                if(render)foreach(var asset in Enum.GetValues<ReachProp>())_=OverworldPropArt.Generate(asset,2);
                for(int tick=0;tick<180;tick++){w.Move(w.Player,new System.Numerics.Vector2(.3f,.1f));w.Step(1f/60);}
                return Persistence.Capture(w,app.Narrative);
            }
            check(Simulate(false)==Simulate(true),"Reach props leave gameplay RNG movement AI combat and saves identical");
        }
        finally{Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();}
    }
}
