using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>A hand-laid market town on a Reach meadow, for reviewing the town asset kit in
/// the production renderer. Development only: stages a plan, writes build/town-preview/*.png
/// and both atlases, asserts nothing and restores the session afterwards.</summary>
public static class TownPreview
{
    public const string Id="town-reference";
    public static RegionState Region()
    {
        var r=new RegionState{Id=Id,Seed=42,Width=64,Height=48,Tiles=Enumerable.Repeat(Terrain.Grass,64*48).ToArray(),Entrance=28*64+32};
        for(int y=0;y<48;y++)for(int x=0;x<64;x++)
        {
            float dx=(x-54)/6f,dy=(y-12)/4f;
            if(dx*dx+dy*dy<1+.1f*MathF.Sin(y*1.3f))r.Tiles[y*64+x]=Terrain.Water;
            if(y is 27 or 28 && x is >=3 and <=60 || x is 31 or 32 && y is >=28 and <=46)r.Tiles[y*64+x]=Terrain.Earth;
        }
        return r;
    }
    public static TownPlan Plan()
    {
        var plan=new TownPlan().Pave(23,20,19,7).Pave(29,19,7,1);
        // North side of the square.
        plan.Add(TownBuilding.Inn,32,18).Add(TownBuilding.Chapel,21,18).Add(TownBuilding.Shop,43,18);
        // The lane east and west.
        plan.Add(TownBuilding.StoneHouse,17,25,0).Add(TownBuilding.Cottage,10,25,0).Add(TownBuilding.Smithy,47,25).Add(TownBuilding.Cottage,56,25,2);
        // South of the road.
        plan.Add(TownBuilding.Cottage,13,35,1).Add(TownBuilding.StoneHouse,22,35,1).Add(TownBuilding.Cottage,41,35,2).Add(TownBuilding.Barn,51,36).Add(TownBuilding.Watchtower,36,43);
        // The square itself.
        plan.Add(TownProp.Fountain,32,23).Add(TownProp.Stall,26,22,0).Add(TownProp.Stall,26,25,1).Add(TownProp.Stall,38,24,2)
            .Add(TownProp.LampPost,23,20).Add(TownProp.LampPost,41,20).Add(TownProp.LampPost,23,26).Add(TownProp.LampPost,41,26)
            .Add(TownProp.Bench,29,21).Add(TownProp.Bench,35,21).Add(TownProp.Statue,37,21).Add(TownProp.NoticeBoard,28,19)
            .Add(TownProp.Planter,30,19,0).Add(TownProp.Planter,34,19,1).Add(TownProp.BannerPole,36,19)
            .Add(TownProp.Barrels,40,19).Add(TownProp.Crates,46,19).Add(TownProp.Sacks,47,19).Add(TownProp.Well,45,21);
        plan.Add(TownProp.Signpost,34,29).Add(TownProp.Woodpile,53,24).Add(TownProp.Washing,17,36).Add(TownProp.Cart,46,37).Add(TownProp.Hay,47,39,0).Add(TownProp.Hay,49,39,1).Add(TownProp.Hay,55,37,0);
        // A fenced kitchen garden behind the western cottage.
        for(int x=6;x<=14;x++){plan.Add(TownProp.FenceRun,x,29);if(x!=10)plan.Add(TownProp.FenceRun,x,33);}
        plan.Add(TownProp.Gate,10,33);
        for(int y=30;y<=33;y++){plan.Add(TownProp.FenceSide,6,y);plan.Add(TownProp.FenceSide,14,y);}
        for(int y=30;y<=32;y++)for(int x=7;x<=13;x++)if(x!=10)plan.Add(TownProp.Garden,x,y,x<10?0:1);
        // A field by the barn and a low wall along the south road.
        for(int y=41;y<=43;y++)for(int x=50;x<=57;x++)plan.Add(TownProp.Garden,x,y,0);
        plan.Add(TownProp.Scarecrow,54,41);
        for(int x=26;x<=30;x++)plan.Add(TownProp.StoneWall,x,41);
        for(int x=38;x<=42;x++)plan.Add(TownProp.StoneWall,x,45);
        return plan;
    }
    static async System.Threading.Tasks.Task Shot(Foundation app,string name,string folder)
    {
        for(int i=0;i<10;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
        await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=app.GetViewport().GetTexture().GetImage();image.SavePng(folder+"/"+name+".png");
    }
    /// <summary>Stage the town, stand the wayfarer at each vantage and capture it.</summary>
    public static async System.Threading.Tasks.Task Capture(Foundation app,string folder,bool clean)
    {
        Directory.CreateDirectory(folder);
        var w=app.World;var p=w.Player;var zoom=app.Camera.Zoom;
        try
        {
            var r=Region();w.RegionsById[r.Id]=r;TownStage.Stage(r.Id,Plan());p.Region=r.Id;
            if(clean)app.Ui.Visible=false;
            foreach(var (name,x,y) in new[]{("square",32f,25.5f),("west",14f,28f),("east",49f,28f),("south-west",17f,38f),("south-east",48f,39.5f),("gate",34f,44f)})
            {p.X=x*24;p.Y=y*24;w.Cancel(p);app.RebuildViews();app.ResumeGame();await Shot(app,name,folder);}
            // The whole town at once, for composition.
            p.X=32*24;p.Y=31*24;app.RebuildViews();app.ResumeGame();app.Camera.Zoom=zoom*.5f;await Shot(app,"overview",folder);
        }
        finally{app.Camera.Zoom=zoom;app.Ui.Visible=true;TownStage.Clear(Id);}
    }
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);var size=app.GetWindow().Size;double scale=Engine.TimeScale;
        try
        {
            Engine.TimeScale=0;ReachAmbience.PreviewTime=0;app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            w.Player.Health=w.Player.MaxHealth;w.Player.Invulnerable=1000;
            app.GetWindow().Size=new Vector2I(1280,800);
            await Capture(app,"build/town-preview",OS.GetCmdlineUserArgs().Contains("--preview-clean"));
            using(var atlas=TownAtlas.Buildings.GetImage())atlas.SavePng("build/town-preview/buildings.png");
            using(var atlas=TownAtlas.Props.GetImage())atlas.SavePng("build/town-preview/props.png");
        }
        finally{ReachAmbience.PreviewTime=-1;Engine.TimeScale=scale;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
