using Godot;
using PixelCreatures.Runtime;
using V = System.Numerics.Vector2;
namespace Mosslight;
/// <summary>Actual viewport pixels, not just Y-sort flags: a foreground wall must
/// restore its exact pixels over the actor, and a front actor must cover the wall.</summary>
public static class WallDepthChecks
{
    static async System.Threading.Tasks.Task Frames(Foundation app, int count=4)
    {
        for(int i=0;i<count;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    static async System.Threading.Tasks.Task<Image> Image(Foundation app, string? name=null)
    {
        await Frames(app);await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=app.GetViewport().GetTexture().GetImage();
        if(name!=null)
        {
            Directory.CreateDirectory("docs/native-screenshots");
            image.SavePng("docs/native-screenshots/"+(app.Ui.MobileLayout?"mobile-":"")+"wall-depth-"+name+".png");
        }
        return image;
    }
    static bool Different(Color a,Color b) => Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>.015f;
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;
        try
        {
            Engine.TimeScale=0;w.Projectiles.Clear();w.Cancel(w.Player);
            var r=Regions.Dungeon(42);r.Id="wall-depth";r.Features.Clear();r.OpenDoors.Clear();
            w.RegionsById[r.Id]=r;var p=w.Player;p.Region=r.Id;
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            // A real two-row shared boundary with safe floor on either side.
            int wall=Enumerable.Range(r.Width*2,r.Tiles.Length-r.Width*4).First(at=>
                at%r.Width%11==8 && r.Tiles[at]==Terrain.Wall && r.Tiles[at+r.Width]==Terrain.Wall &&
                r.Tiles[at-r.Width]==Terrain.Floor && r.Tiles[at+2*r.Width]==Terrain.Floor &&
                r.CanStand(new V(r.Center(at).X,at/r.Width*24-p.Radius-1),p) &&
                r.CanStand(new V(r.Center(at).X,(at/r.Width+2)*24+p.Radius+1),p));
            float x=r.Center(wall).X,y=wall/r.Width*24;
            void Place(V point){p.X=point.X;p.Y=point.Y;app.Views["player"].SnapPosition();}
            p.X=x;p.Y=y-p.Radius-1;app.RebuildViews();app.ResumeGame();await Frames(app,16);
            var rows=app.Views["player"].GetParent().GetChildren().OfType<TerrainWallRow>().ToArray();
            if(rows.Length>r.Height || rows.Sum(row=>row.Columns.Length)!=r.Tiles.Count(t=>t==Terrain.Wall) || !rows.All(row=>row.ZIndex==0))
                throw new InvalidOperationException("Walls must be complete bounded row meshes in the shared actor depth layer");
            var parent=(Node2D)app.Views["player"].GetParent();
            if(!parent.YSortEnabled || rows.Any(row=>row.GetParent()!=parent))throw new InvalidOperationException("Walls and characters do not share Y sorting");
            app.ShowCharacters(false);
            var wallOnly=await Image(app);
            foreach(var row in rows)row.Visible=false;
            var floorOnly=await Image(app);
            foreach(var row in rows){row.Visible=true;row.ZIndex=-1;}
            app.ShowCharacters(true);
            var broken=await Image(app,"old-order-reproduction");
            foreach(var row in rows)row.ZIndex=0;
            var behind=await Image(app,"behind");
            var view=app.Views["player"];
            var screen=view.GetGlobalTransformWithCanvas().Origin;
            var zoom=app.GetViewport().GetCamera2D().Zoom;
            var bounds=new Rect2I((int)(screen.X-48*zoom.X),(int)(screen.Y-70*zoom.Y),(int)(96*zoom.X),(int)(110*zoom.Y));
            int occluded=0,visibleAbove=0;
            void Pixels(Action<int,int> visit)
            {
                for(int py=Math.Max(0,bounds.Position.Y);py<Math.Min(behind.GetHeight(),bounds.End.Y);py++)
                    for(int px=Math.Max(0,bounds.Position.X);px<Math.Min(behind.GetWidth(),bounds.End.X);px++)visit(px,py);
            }
            Pixels((px,py)=>
            {
                bool wallPixel=Different(wallOnly.GetPixel(px,py),floorOnly.GetPixel(px,py));
                bool actorPixel=Different(broken.GetPixel(px,py),wallOnly.GetPixel(px,py));
                if(wallPixel&&actorPixel)
                {
                    if(Different(behind.GetPixel(px,py),wallOnly.GetPixel(px,py)))throw new InvalidOperationException("Foreground wall failed to cover actor pixel");
                    occluded++;
                }
                if(!wallPixel&&Different(behind.GetPixel(px,py),wallOnly.GetPixel(px,py)))visibleAbove++;
            });
            if(occluded<20 || visibleAbove<20)throw new InvalidOperationException($"Missing partial wall occlusion: hidden={occluded} visible={visibleAbove}");
            // Actor standing south/in front must paint over the same wall art.
            Place(new V(x,y+48+p.Radius+1));await Frames(app,8);
            var front=await Image(app,"in-front");
            foreach(var row in rows)row.ZIndex=1;
            var incorrectlyAbove=await Image(app);
            int frontPixels=0;Pixels((px,py)=>{if(Different(front.GetPixel(px,py),incorrectlyAbove.GetPixel(px,py)))frontPixels++;});
            if(frontPixels<20)throw new InvalidOperationException("Actor in front was incorrectly hidden by the wall");
            foreach(var row in rows)row.ZIndex=0;
            // Same shared boundary's centered three-tile doorway stays visibly open.
            float doorX=(wall%r.Width/11*11+5.5f)*24;
            var doorway=new V(doorX,y+24);
            if(!r.CanStand(doorway,p))throw new InvalidOperationException("Depth fixture doorway is not traversable");
            Place(doorway);await Frames(app,8);
            var doorwayFixed=await Image(app,"doorway");
            foreach(var row in rows)row.ZIndex=-1;
            var doorwayLegacy=await Image(app);
            int doorwayChanges=0;
            for(int py=0;py<doorwayFixed.GetHeight();py++)for(int px=0;px<doorwayFixed.GetWidth();px++)
                if(Different(doorwayFixed.GetPixel(px,py),doorwayLegacy.GetPixel(px,py)))doorwayChanges++;
            if(doorwayChanges!=0)throw new InvalidOperationException("Wall row paints over clear doorway: "+doorwayChanges);
            foreach(var row in rows)row.ZIndex=0;
            // Actual collision-safe movement through the opening and back.
            Place(new V(doorX,y-48));
            var start=p.Position;w.Move(p,new V(0,144));w.Move(p,new V(0,-144));
            if(!r.CanStand(p.Position,p)||V.Distance(start,p.Position)>.1f)throw new InvalidOperationException("Depth fix changed doorway traversal");
            GD.Print($"WALL DEPTH CHECKS PASSED: foreground pixels={occluded}, visible silhouette={visibleAbove}, front pixels={frontPixels}, doorway changes={doorwayChanges}, row nodes={rows.Length}");
        }
        finally{app.ShowCharacters(true);Engine.TimeScale=scale;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
