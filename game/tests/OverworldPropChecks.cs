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
        check(ReferenceEquals(atlas,OverworldPropAtlas.Texture) && OverworldPropAtlas.BuildCount==1 && OverworldPropAtlas.PixelBytes==1437696,"one fixed Reach atlas across all regions");
        var art=new Dictionary<string,Texture2D>();
        foreach(int seed in new[]{1,42,1337})
        {
            var r=Regions.Overworld(seed);string before=JsonSerializer.Serialize(r,Content.Json);
            var first=EnvironmentDecoration.Create(r,art);var second=EnvironmentDecoration.Create(r,art);
            check(first.Count<=EnvironmentDecoration.Maximum && first.Select(d=>(d.Position,d.ReachModule)).SequenceEqual(second.Select(d=>(d.Position,d.ReachModule))),"bounded stable Reach placement "+seed);
            check(first.All(d=>d.ReachModule is >=0 and <44),"overworld decorations use fixed modules "+seed);
            foreach(var d in first.Concat(second))d.Free();
            check(before==JsonSerializer.Serialize(r,Content.Json),"Reach props preserve saved geography "+seed);
        }
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
