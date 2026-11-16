using Godot;
using PixelCreatures.Runtime;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Rendered ruin art through the production renderer: reference captures of every
/// theme, light that follows saved brazier state, darkness inside solid wall, and ambience
/// that moves only when motion is allowed. Saved state must come back untouched.</summary>
public static class DungeonArtVisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app,int count=10)
    {for(int i=0;i<count;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}}
    static async System.Threading.Tasks.Task<Image> Capture(Foundation app,string? name=null)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=app.GetViewport().GetTexture().GetImage();
        if(name!=null){Directory.CreateDirectory("docs/dungeon-screenshots");image.SavePng("docs/dungeon-screenshots/"+(app.Ui.MobileLayout?"mobile-":"")+name+".png");}
        return image;
    }
    static float Luma(Image image,Vector2 centre,int half)
    {
        float sum=0;int count=0;
        for(int y=(int)centre.Y-half;y<=(int)centre.Y+half;y++)for(int x=(int)centre.X-half;x<=(int)centre.X+half;x++)
        {if(x<0||y<0||x>=image.GetWidth()||y>=image.GetHeight())continue;var c=image.GetPixel(x,y);sum+=c.R*.3f+c.G*.59f+c.B*.11f;count++;}
        return count==0?0:sum/count;
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;var size=app.GetWindow().Size;
        var prefs=app.Ui.Preferences.Copy();
        try
        {
            Engine.TimeScale=0;app.GetWindow().Size=new Vector2I(1280,800);w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            var p=w.Player;w.Cancel(p);p.Health=p.MaxHealth;p.Invulnerable=1000;
            ReachAmbience.PreviewTime=0;app.Ui.Preferences.ReducedMotion=false;
            void Stand(RegionState r,V point){point=r.FindPlacement(point,p,w.Actors.Values);p.X=point.X;p.Y=point.Y;w.Cancel(p);app.RebuildViews();app.ResumeGame();}
            Vector2 Screen(RegionState r,V ground)=>app.GetViewport().GetCanvasTransform()*GodotConvert.GroundToScreen(ground.X,ground.Y);
            // Seed 42 holds all three themes, a key gate, a lever and its portcullis.
            var ruin=Regions.Dungeon(42);ruin.Id="ruin-art";w.RegionsById[ruin.Id]=ruin;p.Region=ruin.Id;
            // Everything the art reads: geography, features, landmarks and mechanism state.
            string State(RegionState r)=>System.Text.Json.JsonSerializer.Serialize(new{r.Tiles,r.Features,r.Landmarks,r.Rooms,r.Passages,r.Lit,r.Depleted,r.OpenDoors},Content.Json);
            string frozen=State(ruin);
            Stand(ruin,ruin.Center(ruin.Entrance)+new V(0,36));using(await Capture(app,"entrance")){}
            foreach(string theme in Landmarks.Themes)
            {
                var room=ruin.Landmarks.First(l=>l.Theme==theme && ruin.Entrance!=(l.OriginY+5)*ruin.Width+l.OriginX+5);
                Stand(ruin,ruin.Center((room.OriginY+5)*ruin.Width+room.OriginX+5)+new V(30,30));using(await Capture(app,theme)){}
            }
            foreach(string kind in new[]{"lock","lever","portcullis","relic"})
            {
                int at=ruin.Features.First(f=>f.Value==kind).Key;
                Stand(ruin,ruin.Center(at)+new V(kind=="lever"||kind=="relic"?26:0,40));using(await Capture(app,kind)){}
            }
            // Solid wall far from any lamp is darker than the floor the wayfarer stands on.
            var masonry=DungeonArt.Masonry(ruin);
            int buried=Enumerable.Range(0,ruin.Tiles.Length).Where(at=>
            {
                int x=at%ruin.Width,y=at/ruin.Width;
                return x>3&&y>3&&x<ruin.Width-4&&y<ruin.Height-4&&Enumerable.Range(-3,7).All(dy=>Enumerable.Range(-3,7).All(dx=>masonry[(y+dy)*ruin.Width+x+dx]));
            }).OrderBy(at=>V.Distance(ruin.Center(at),ruin.Center(ruin.Entrance))).First();
            Stand(ruin,ruin.Center(ruin.Entrance)+new V(0,36));
            // Park the camera on the wayfarer's room, then measure both spots in that one frame.
            using(var view=await Capture(app))
            {
                float floor=Luma(view,Screen(ruin,p.Position+new V(40,0)),4),wall=Luma(view,Screen(ruin,ruin.Center(buried)),4);
                var spot=Screen(ruin,ruin.Center(buried));
                bool visible=spot.X>8&&spot.Y>8&&spot.X<view.GetWidth()-8&&spot.Y<view.GetHeight()-8;
                if(visible && wall>=floor*.6f)throw new InvalidOperationException($"Solid wall is not darker than lit floor: wall={wall:0.000} floor={floor:0.000}");
            }
            if(State(ruin)!=frozen)throw new InvalidOperationException("Ruin art captures changed saved geography or mechanisms");
            // Seed 7: a brazier ring. Lighting one must brighten the floor beside it, from saved state alone.
            var ring=Regions.Dungeon(7);ring.Id="ruin-art-ring";w.RegionsById[ring.Id]=ring;p.Region=ring.Id;
            var braziers=DungeonMechanisms.Controls(ring).First(c=>c.Kind=="puzzle").Controllers;
            var centre=(ring.Center(braziers[1])+ring.Center(braziers[3]))/2;
            // Measure the clear floor tile the braziers should brighten most, from far enough
            // away that the lantern does not reach it, with the interface out of the way.
            var ringMasonry=DungeonArt.Masonry(ring);V probe;
            using(var dark=DungeonLight.Bake(ring,ringMasonry))
            {
                foreach(int brazier in braziers)ring.Lit.Add(brazier);
                using var bright=DungeonLight.Bake(ring,ringMasonry);
                foreach(int brazier in braziers)ring.Lit.Remove(brazier);
                probe=Enumerable.Range(0,ring.Tiles.Length).Where(at=>ring.Tiles[at]==Terrain.Floor && V.Distance(ring.Center(at),centre) is >40 and <110
                        && !ring.Features.Keys.Any(f=>Math.Abs(f%ring.Width-at%ring.Width)<2 && Math.Abs(f/ring.Width-at/ring.Width)<2))
                    .OrderByDescending(at=>bright.GetPixel(at%ring.Width*8+4,at/ring.Width*4+2).R-dark.GetPixel(at%ring.Width*8+4,at/ring.Width*4+2).R).Select(ring.Center).First();
            }
            var away=Enumerable.Range(0,ring.Tiles.Length).Where(at=>ring.Tiles[at]==Terrain.Floor).Select(ring.Center)
                .Where(c=>V.Distance(c,probe) is >260 and <340 && ring.CanStand(c,p)).OrderBy(c=>V.Distance(c,probe)).First();
            Stand(ring,away);app.Ui.Visible=false;
            float before,after;
            using(var cold=await Capture(app))before=Luma(cold,Screen(ring,probe),4);
            foreach(int brazier in braziers)ring.Lit.Add(brazier);
            using(var kindled=await Capture(app))after=Luma(kindled,Screen(ring,probe),4);
            app.Ui.Visible=true;
            if(after<before*1.12f)throw new InvalidOperationException($"Burning brazier did not light the floor: {before:0.000} -> {after:0.000}");
            foreach(int brazier in braziers)ring.Lit.Remove(brazier);
            Stand(ring,centre+new V(0,30));using(await Capture(app,"braziers-cold")){}
            foreach(int brazier in braziers)ring.Lit.Add(brazier);
            using(await Capture(app,"braziers-burning")){}
            // Reduced motion shows the ambience frame at clock zero, exactly.
            using var normal=await Capture(app);
            app.Ui.Preferences.ReducedMotion=true;using var reduced=await Capture(app,"reduced-motion");
            if(!normal.GetData().AsSpan().SequenceEqual(reduced.GetData()))throw new InvalidOperationException("Static ruin art changed with reduced motion");
            ReachAmbience.PreviewTime=30.37f;using var held=await Capture(app);
            if(!held.GetData().AsSpan().SequenceEqual(reduced.GetData()))throw new InvalidOperationException("Reduced motion did not freeze flames and firelight");
            app.Ui.Preferences.ReducedMotion=false;using var moved=await Capture(app);
            int changed=0;
            for(int y=0;y<moved.GetHeight();y++)for(int x=0;x<moved.GetWidth();x++)if(moved.GetPixel(x,y)!=normal.GetPixel(x,y))changed++;
            if(changed<40)throw new InvalidOperationException("Ruin flames and firelight never move: "+changed);
            ReachAmbience.PreviewTime=0;
            foreach(var (width,height) in new[]{(900,560),(1280,720),(1920,864),(2340,1080)})
            {
                app.GetWindow().Size=new Vector2I(width,height);await Frames(app,6);
                Stand(ring,centre+new V(0,30));using(await Capture(app,$"{width}x{height}")){}
            }
            Directory.CreateDirectory("build/dungeon");
            File.WriteAllText("build/dungeon/art-"+(app.Ui.MobileLayout?"mobile":"desktop")+".json",System.Text.Json.JsonSerializer.Serialize(
                new{brazierFloorBefore=before,brazierFloorAfter=after,animatedPixels=changed,clutter=DungeonClutter.LastCount,modules=DungeonPropArt.Modules},Content.Json));
            GD.Print($"DUNGEON ART CHECKS PASSED: three themes, light {before:0.000}->{after:0.000}, animated pixels={changed}, still under reduced motion");
        }
        finally{app.Ui.Visible=true;ReachAmbience.PreviewTime=-1;Engine.TimeScale=scale;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.Ui.ApplyPreferences(prefs,false);app.RebuildViews();app.ResumeGame();}
    }
}
