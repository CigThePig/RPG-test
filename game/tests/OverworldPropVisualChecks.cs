using Godot;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Paired production captures, real tree/actor depth and repeated region ownership.</summary>
public static class OverworldPropVisualChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app,int n=8)
    {for(int i=0;i<n;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}}
    static async System.Threading.Tasks.Task<Image> Capture(Foundation app,string? name=null)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=app.GetViewport().GetTexture().GetImage();
        if(image.IsEmpty() || image.GetWidth()<800)throw new InvalidOperationException("Missing prop reference raster");
        if(name!=null)image.SavePng("docs/overworld-screenshots/part4-"+name+".png");return image;
    }
    static bool Different(Color a,Color b)=>Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>.015f;
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        Directory.CreateDirectory("docs/overworld-screenshots");Directory.CreateDirectory("build/overworld");
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;
        var size=app.GetWindow().Size;bool previous=OverworldPropAtlas.VerificationPrevious;var prefs=app.Ui.Preferences.Copy();
        try
        {
            Engine.TimeScale=0;app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var p=w.Player;w.Cancel(p);p.Health=p.MaxHealth;p.Invulnerable=1000;
            var fixture=OverworldVisualChecks.Fixture();fixture.Id="reach-prop-reference";
            w.RegionsById[fixture.Id]=fixture;p.Region=fixture.Id;p.X=32.5f*24;p.Y=34.5f*24;
            app.GetWindow().Size=new Vector2I(1280,800);
            OverworldPropAtlas.VerificationPrevious=false;app.RebuildViews();
            using(var reference=await Capture(app,"after-reference")){}
            foreach(var resolution in WidescreenChecks.Sizes)
            {app.GetWindow().Size=resolution;using var capture=await Capture(app,"after-"+resolution.X+"x"+resolution.Y);}
            app.GetWindow().Size=new Vector2I(1280,800);
            foreach(int seed in new[]{1,42,1337})
            {
                var r=Regions.Overworld(seed);r.Id="reach-prop-seed";w.RegionsById[r.Id]=r;p.Region=r.Id;
                var point=r.FindPlacement(r.Center(r.Entrance)+new V(72,0),p,w.Actors.Values);p.X=point.X;p.Y=point.Y;
                app.RebuildViews();using var capture=await Capture(app,"seed"+seed);
            }
            // The real feature lifecycle selects remnant/open atlas modules from saved state.
            var gallery=OverworldVisualChecks.Fixture();gallery.Id="reach-prop-states";Array.Fill(gallery.Tiles,Terrain.Grass);gallery.Features.Clear();
            foreach(var entry in new[]{(24,28,"wood"),(29,28,"wood"),(34,28,"wood"),(39,28,"wood"),
                (24,36,"ore"),(29,36,"herb"),(34,36,"chest"),(40,36,"portal"),
                (24,43,"wood"),(29,43,"ore"),(34,43,"herb"),(39,43,"chest")})
            {int at=entry.Item2*64+entry.Item1;gallery.Features[at]=entry.Item3;if(entry.Item2==43)gallery.Depleted.Add(at);}
            w.RegionsById[gallery.Id]=gallery;p.Region=gallery.Id;p.X=32*24;p.Y=35*24;
            app.RebuildViews();using(var capture=await Capture(app,"resource-states")){}
            using(var atlas=OverworldPropAtlas.Texture.GetImage())atlas.SavePng("docs/overworld-screenshots/part4-atlas.png");
            // An overworld tree must cover an actor behind it and reveal one in front.
            gallery.Features.Clear();int treeAt=34*64+32;gallery.Features[treeAt]="wood";gallery.Depleted.Clear();
            var root=gallery.Center(treeAt);p.X=root.X;p.Y=root.Y-12;app.RebuildViews();await Frames(app);
            var view=app.Views["player"];var parent=view.GetParent();
            foreach(var decoration in parent.GetChildren().OfType<EnvironmentDecoration>())decoration.Visible=false;
            var tree=parent.GetChildren().OfType<FeatureView>().Single();
            // ShowCharacters also hides features: keep the tree so this really is the tree alone.
            app.ShowCharacters(false);tree.Visible=true;using var treeOnly=await Capture(app);
            app.ShowCharacters(true);tree.ZIndex=-1;using var broken=await Capture(app);
            tree.ZIndex=0;using var behind=await Capture(app,"tree-behind");int covered=0;
            for(int y=0;y<behind.GetHeight();y++)for(int x=0;x<behind.GetWidth();x++)
                if(Different(broken.GetPixel(x,y),treeOnly.GetPixel(x,y)) && !Different(behind.GetPixel(x,y),treeOnly.GetPixel(x,y)))covered++;
            if(covered<20)throw new InvalidOperationException("Reach tree does not cover behind actor: "+covered);
            p.Y=root.Y+12;view.SnapPosition();await Frames(app);
            using var front=await Capture(app,"tree-front");tree.ZIndex=1;using var wrong=await Capture(app);int revealed=0;tree.ZIndex=0;
            for(int y=0;y<front.GetHeight();y++)for(int x=0;x<front.GetWidth();x++)if(Different(front.GetPixel(x,y),wrong.GetPixel(x,y)))revealed++;
            if(revealed<20)throw new InvalidOperationException("Reach tree hides front actor: "+revealed);
            File.WriteAllText("build/overworld/part4-depth.json",JsonSerializer.Serialize(new {covered,revealed},Content.Json));
            // Static artwork is exactly identical when reduced motion changes.
            // Reduced motion shows the ambience frame at clock zero; hold the running clock there too.
            ReachAmbience.PreviewTime=0;
            app.Ui.Preferences.ReducedMotion=false;using var normal=await Capture(app);
            app.Ui.Preferences.ReducedMotion=true;using var reduced=await Capture(app,"reduced-motion");
            if(!normal.GetData().SequenceEqual(reduced.GetData()))throw new InvalidOperationException("Static Reach props changed with reduced motion");
            // Atmosphere must move when allowed and hold perfectly still when not.
            ReachAmbience.PreviewTime=30;using var held=await Capture(app);
            if(!held.GetData().SequenceEqual(reduced.GetData()))throw new InvalidOperationException("Reduced motion did not freeze the Reach atmosphere");
            app.Ui.Preferences.ReducedMotion=false;using var drifted=await Capture(app);
            if(drifted.GetData().SequenceEqual(normal.GetData()))throw new InvalidOperationException("Reach atmosphere never moves");
            ReachAmbience.PreviewTime=-1;
            // Alternate real dungeon and overworld, then sample the same overworld totals.
            var dungeon=Regions.Dungeon(42);dungeon.Id="reach-prop-dungeon";w.RegionsById[dungeon.Id]=dungeon;
            string frozen=Persistence.Capture(w,app.Narrative);int builds=OverworldPropAtlas.BuildCount;
            for(int iteration=0;iteration<6;iteration++)
            {
                p.Region=dungeon.Id;p.X=dungeon.Center(dungeon.Entrance).X;p.Y=dungeon.Center(dungeon.Entrance).Y;app.RebuildViews();await Frames(app,12);
                p.Region=fixture.Id;p.X=32.5f*24;p.Y=34.5f*24;
                app.RebuildViews();await Frames(app,24);
            }
            if(OverworldPropAtlas.BuildCount!=builds)throw new InvalidOperationException("Prop atlas grew during region changes");
            // Restore fixture position before comparing authoritative state.
            p.Region=gallery.Id;p.X=root.X;p.Y=root.Y+12;
            if(Persistence.Capture(w,app.Narrative)!=frozen)throw new InvalidOperationException("Prop region rebuild changed saved state");
            GD.Print($"OVERWORLD PROP CHECKS PASSED: six sizes, seeds, resource states, depth={covered}/{revealed}, six dungeon roundtrips");
        }
        finally{ReachAmbience.PreviewTime=-1;OverworldPropAtlas.VerificationPrevious=previous;Engine.TimeScale=scale;app.GetWindow().Size=size;app.ShowCharacters(true);Persistence.Restore(w,app.Narrative,snapshot);app.Ui.ApplyPreferences(prefs,false);app.RebuildViews();app.ResumeGame();}
    }
}
