using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Fast art-review captures of the production dungeon renderer. Development only:
/// writes build/dungeon-preview/*.png, asserts nothing and restores the session afterwards.</summary>
public static class DungeonPreview
{
    static async System.Threading.Tasks.Task Shot(Foundation app,string name)
    {
        for(int i=0;i<10;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
        await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=app.GetViewport().GetTexture().GetImage();image.SavePng("build/dungeon-preview/"+name+".png");
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        Directory.CreateDirectory("build/dungeon-preview");
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);var size=app.GetWindow().Size;
        bool clean=OS.GetCmdlineUserArgs().Contains("--preview-clean");
        try
        {
            app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var p=w.Player;w.Cancel(p);p.Health=p.MaxHealth;p.Invulnerable=1000;
            app.GetWindow().Size=new Vector2I(1280,800);
            if(clean)app.Ui.Visible=false;
            void Stand(RegionState r,V point)
            {
                point=r.FindPlacement(point,p,w.Actors.Values);p.X=point.X;p.Y=point.Y;w.Cancel(p);app.RebuildViews();app.ResumeGame();
            }
            foreach(int seed in new[]{42,7,1337})
            {
                var r=Regions.Dungeon(seed);r.Id="ruin-preview";w.RegionsById[r.Id]=r;p.Region=r.Id;
                Stand(r,r.Center(r.Entrance)+new V(0,36));await Shot(app,$"seed{seed}-entrance");
                // One room of every theme, then the objective, a corridor and each mechanism.
                foreach(string theme in Landmarks.Themes)
                {
                    var room=r.Landmarks.FirstOrDefault(l=>l.Theme==theme && r.Entrance!=(l.OriginY+5)*r.Width+l.OriginX+5);
                    if(room==null)continue;
                    Stand(r,r.Center((room.OriginY+5)*r.Width+room.OriginX+5)+new V(30,30));await Shot(app,$"seed{seed}-{theme}");
                }
                int relic=r.Features.Where(f=>f.Value=="relic").Select(f=>f.Key).DefaultIfEmpty(-1).First();
                if(relic>=0){Stand(r,r.Center(relic)+new V(0,40));await Shot(app,$"seed{seed}-relic");}
                var hall=r.Rooms.FirstOrDefault(c=>c.Kind=="hall");
                if(hall!=null){Stand(r,r.Center(hall.CenterY*r.Width+hall.CenterX));await Shot(app,$"seed{seed}-hall");}
                foreach(string kind in new[]{"lock","portcullis","seal","lever","brazier","chest","key"})
                {
                    int at=r.Features.Where(f=>f.Value==kind).Select(f=>f.Key).DefaultIfEmpty(-1).First();
                    if(at<0)continue;
                    Stand(r,r.Center(at)+new V(kind is "lock" or "portcullis" or "seal"?0:26,40));
                    if(kind=="brazier")foreach(int b in r.Features.Where(f=>f.Value=="brazier").Select(f=>f.Key).Take(2))r.Lit.Add(b);
                    app.RebuildViews();await Shot(app,$"seed{seed}-{kind}");
                }
            }
            DungeonPreviewAtlas();
        }
        finally{app.Ui.Visible=true;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
    static void DungeonPreviewAtlas()
    {
        using(var atlas=DungeonPropAtlas.Texture.GetImage())atlas.SavePng("build/dungeon-preview/atlas.png");
    }
}
