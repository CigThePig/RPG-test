using Godot;
namespace Mosslight;

/// <summary>The town asset kit: every module painted and well-formed, buildings standing
/// inside their stated footprints, staged towns keeping wild growth clear, and none of it
/// touching saved geography. Rendered: the staged town appears and is removed cleanly.</summary>
public static class TownArtChecks
{
    static bool Body(byte c)=>c!=0 && c!=35 && c!=36 && c!=37;
    public static void Run(Foundation app,Action<bool,string> check)
    {
        check(TownAtlas.Buildings.GetWidth()==TownArt.BuildingColumns*TownArt.BuildingWidth && TownAtlas.Buildings.GetHeight()==TownArt.BuildingRows*TownArt.BuildingHeight
            && TownAtlas.Props.GetWidth()==TownArt.PropColumns*TownArt.Width && TownAtlas.Props.GetHeight()==TownArt.PropRows*TownArt.Height,"town atlases hold every module");
        check(ReferenceEquals(TownAtlas.Buildings,TownAtlas.Buildings) && TownAtlas.BuildCount==2,"town atlases built once");
        void Wellformed(byte[] data,int w,int h,string name,int least)
        {
            check(data.Length==w*h && data.All(n=>n<OverworldPropArt.Palette.Length) && data.Count(n=>n>0)>least,"valid indexed town art "+name);
            bool ring=true;
            for(int x=0;x<w;x++)ring&=data[x]==0 && data[(h-1)*w+x]==0;
            for(int y=0;y<h;y++)ring&=data[y*w]==0 && data[y*w+w-1]==0;
            check(ring,"transparent gutter round "+name);
        }
        var modules=new HashSet<int>();
        foreach(var building in Enum.GetValues<TownBuilding>())for(int v=0;v<TownArt.Count(building);v++)
        {
            var data=TownArt.Generate(building,v);string name=building.ToString()+v;
            Wellformed(data,TownArt.BuildingWidth,TownArt.BuildingHeight,name,1500);
            check(modules.Add(TownArt.Module(building,v)) && data.SequenceEqual(TownArt.Generate(building,v)),"distinct repeatable building "+name);
            // The walls meet the ground inside the footprint the building claims.
            var foot=Enumerable.Range(0,TownArt.BuildingWidth).Where(x=>Enumerable.Range(154,5).Any(y=>Body(data[y*TownArt.BuildingWidth+x]))).ToArray();
            int width=TownArt.Footprint(building).Width*24;
            check(foot.Length>=24 && foot.Min()>=96-width/2-14 && foot.Max()<=96+width/2+14,$"{name} stands within its {width}-unit front: {foot.Min()}..{foot.Max()}");
            // Lit from the upper-left: the left half of the sprite is brighter than the right.
            float Luma(int from,int to)
            {
                float sum=0;int count=0;
                for(int y=0;y<TownArt.BuildingHeight;y++)for(int x=from;x<to;x++){byte c=data[y*TownArt.BuildingWidth+x];if(!Body(c))continue;var colour=OverworldPropArt.Palette[c];sum+=colour.R*.3f+colour.G*.59f+colour.B*.11f;count++;}
                return count==0?0:sum/count;
            }
            check(Luma(foot.Min(),foot.Min()+8)>Luma(foot.Max()-7,foot.Max()+1),"upper-left light on "+name);
        }
        check(modules.Count==TownArt.Buildings,"every building module addressed");
        modules.Clear();
        foreach(var prop in Enum.GetValues<TownProp>())for(int v=0;v<TownArt.Count(prop);v++)
        {
            var data=TownArt.Generate(prop,v);string name=prop.ToString()+v;
            Wellformed(data,TownArt.Width,TownArt.Height,name,20);
            check(modules.Add(TownArt.Module(prop,v)) && data.SequenceEqual(TownArt.Generate(prop,v)),"distinct repeatable town prop "+name);
        }
        check(modules.Count==TownArt.Props,"every town prop module addressed");
        // Fence, wall and garden modules span their whole tile so neighbours join.
        foreach(var run in new[]{TownProp.FenceRun,TownProp.StoneWall,TownProp.Garden})
        {
            var data=TownArt.Generate(run,0);
            check(Enumerable.Range(0,TownArt.Height).Any(y=>Body(data[y*TownArt.Width+24])) && Enumerable.Range(0,TownArt.Height).Any(y=>Body(data[y*TownArt.Width+47])),run+" reaches both tile edges");
        }
        // A staged town keeps wild growth off its buildings, furniture and paving, and is presentation only.
        var r=TownPreview.Region();var plan=TownPreview.Plan();
        string before=System.Text.Json.JsonSerializer.Serialize(r,Content.Json);
        var art=new Dictionary<string,Texture2D>();
        int Wild()
        {
            var decorations=EnvironmentDecoration.Create(r,art);int count=decorations.Count+OverworldFlora.Plan(r).Count(f=>!f.Flat);
            foreach(var d in decorations)d.Free();return count;
        }
        int open=Wild();
        try
        {
            TownStage.Stage(r.Id,plan);
            check(ReferenceEquals(TownStage.For(r),plan) && Wild()<open,"a staged town thins wild growth");
            var decorations=EnvironmentDecoration.Create(r,art);
            check(decorations.All(d=>{var g=d.Position;return !plan.Occupied((int)(g.X/24),(int)(g.Y/12));}),"no tree or shrub on town ground");
            foreach(var d in decorations)d.Free();
            check(plan.Pieces.All(piece=>TownPlan.Footprint(piece).All(t=>t.X>=0 && t.Y>=0 && t.X<r.Width && t.Y<r.Height && r.Tiles[t.Y*r.Width+t.X] is Terrain.Grass or Terrain.Earth)),"reference town stands on dry ground");
            var buildings=plan.Pieces.Where(piece=>piece.Building).Select(piece=>TownPlan.Footprint(piece).ToHashSet()).ToArray();
            check(buildings.SelectMany(f=>f).Count()==buildings.SelectMany(f=>f).Distinct().Count(),"reference buildings do not overlap");
            check(plan.Pieces.Where(piece=>piece.Building).Select(piece=>piece.Kind).Distinct().Count()==Enum.GetValues<TownBuilding>().Length
                && plan.Pieces.Where(piece=>!piece.Building).Select(piece=>piece.Kind).Distinct().Count()==Enum.GetValues<TownProp>().Length,"reference town shows every module kind");
            var views=TownView.Create(plan);
            check(views.Count==plan.Pieces.Count(piece=>piece.Building||!TownArt.Flat((TownProp)piece.Kind)) && !typeof(CollisionObject2D).IsAssignableFrom(typeof(TownView)),"one nonblocking view per standing module");
            foreach(var view in views)view.Free();
        }
        finally{TownStage.Clear(r.Id);}
        check(TownStage.For(r)==null && Wild()==open,"clearing a staged town restores the meadow");
        check(before==System.Text.Json.JsonSerializer.Serialize(r,Content.Json),"town art does not mutate serialized geography");
    }
    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);var size=app.GetWindow().Size;double scale=Engine.TimeScale;
        try
        {
            Engine.TimeScale=0;ReachAmbience.PreviewTime=0;app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var p=w.Player;p.Health=p.MaxHealth;p.Invulnerable=1000;
            app.GetWindow().Size=new Vector2I(1280,800);
            async System.Threading.Tasks.Task<Image> Frame()
            {
                for(int i=0;i<10;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
                await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                return app.GetViewport().GetTexture().GetImage();
            }
            // The same meadow before, with and after the town.
            var r=TownPreview.Region();w.RegionsById[r.Id]=r;p.Region=r.Id;p.X=32*24;p.Y=25.5f*24;w.Cancel(p);
            app.RebuildViews();app.ResumeGame();using var meadow=await Frame();
            TownStage.Stage(r.Id,TownPreview.Plan());app.RebuildViews();app.ResumeGame();using var town=await Frame();
            int changed=0;
            for(int y=0;y<town.GetHeight();y++)for(int x=0;x<town.GetWidth();x++)if(town.GetPixel(x,y)!=meadow.GetPixel(x,y))changed++;
            if(changed<town.GetWidth()*town.GetHeight()/6)throw new InvalidOperationException("Staged town is not visible: "+changed);
            int nodes=app.Views["player"].GetParent().GetChildren().OfType<TownView>().Count();
            if(nodes==0 || app.GetTree().GetNodesInGroup("town-paving").Count!=1)throw new InvalidOperationException("Staged town built no views");
            TownStage.Clear(r.Id);app.RebuildViews();app.ResumeGame();using var cleared=await Frame();
            // The interface may tick between captures; the world beneath it must be the meadow again.
            int left=0;
            for(int y=0;y<cleared.GetHeight();y++)for(int x=0;x<cleared.GetWidth();x++)if(cleared.GetPixel(x,y)!=meadow.GetPixel(x,y))left++;
            if(left>changed/200)throw new InvalidOperationException($"Clearing the town did not restore the meadow: {left} of {changed} pixels remain");
            await TownPreview.Capture(app,"docs/town-screenshots",false);
            using(var atlas=TownAtlas.Buildings.GetImage())atlas.SavePng("docs/town-screenshots/atlas-buildings.png");
            using(var atlas=TownAtlas.Props.GetImage())atlas.SavePng("docs/town-screenshots/atlas-props.png");
            GD.Print($"TOWN ART CHECKS PASSED: {nodes} standing modules, {changed} town pixels, {left} pixels differ after removal");
        }
        finally{TownStage.Clear(TownPreview.Id);ReachAmbience.PreviewTime=-1;Engine.TimeScale=scale;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
