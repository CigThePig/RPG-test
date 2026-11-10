using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Fast art-review captures of the production overworld renderer. Development only:
/// writes build/preview/*.png, asserts nothing and restores the session afterwards.</summary>
public static class OverworldPreview
{
    static async System.Threading.Tasks.Task Shot(Foundation app,string name)
    {
        for(int i=0;i<10;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
        await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=app.GetViewport().GetTexture().GetImage();image.SavePng("build/preview/"+name+".png");
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        Directory.CreateDirectory("build/preview");
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);var size=app.GetWindow().Size;
        bool clean=OS.GetCmdlineUserArgs().Contains("--preview-clean");
        try
        {
            app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var p=w.Player;w.Cancel(p);p.Health=p.MaxHealth;p.Invulnerable=1000;
            app.GetWindow().Size=new Vector2I(1280,800);
            if(clean)app.Ui.Visible=false;
            var fixture=OverworldVisualChecks.Fixture();fixture.Id="reach-preview";w.RegionsById[fixture.Id]=fixture;
            p.Region=fixture.Id;p.X=32.5f*24;p.Y=34.5f*24;app.RebuildViews();await Shot(app,"fixture");
            ReachAmbience.PreviewTime=3;await Shot(app,"fixture-later");ReachAmbience.PreviewTime=-1;
            foreach(int seed in new[]{1,42,1337})
            {
                var r=Regions.Overworld(seed);r.Id="reach-preview-seed";w.RegionsById[r.Id]=r;p.Region=r.Id;
                var point=r.FindPlacement(r.Center(r.Entrance)+new V(72,0),p,w.Actors.Values);p.X=point.X;p.Y=point.Y;
                app.RebuildViews();await Shot(app,"seed"+seed);
                if(seed!=42)continue;
                // A second vantage with rock massifs and the region border.
                int at=Enumerable.Range(0,r.Tiles.Length).Where(n=>r.Tiles[n]==Terrain.Grass && r.Neighbours(n).Any(k=>r.Tiles[k]==Terrain.Rock) && n%r.Width is >6 and <58 && n/r.Width is >6 and <42).DefaultIfEmpty(r.Entrance).First();
                point=r.FindPlacement(r.Center(at),p,w.Actors.Values);p.X=point.X;p.Y=point.Y;app.RebuildViews();await Shot(app,"seed42-rocks");
            }
            using(var atlas=OverworldPropAtlas.Texture.GetImage())atlas.SavePng("build/preview/atlas.png");
        }
        finally{app.Ui.Visible=true;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
