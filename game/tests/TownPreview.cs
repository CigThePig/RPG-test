using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>A hand-laid market town of block houses on a Reach meadow, for reviewing the town asset kit in
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
            float dx=(x-55)/5f,dy=(y-9)/3.5f;
            if(dx*dx+dy*dy<1+.1f*MathF.Sin(y*1.3f))r.Tiles[y*64+x]=Terrain.Water;
            if(y is 27 or 28 && x is >=2 and <=61 || x is 31 or 32 && y is >=28 and <=46)r.Tiles[y*64+x]=Terrain.Earth;
        }
        // The houses are real wall and floor: collision, sight and pathing follow the tiles.
        Plan().Carve(r);
        return r;
    }
    public static TownPlan Plan()
    {
        // Rows are spaced so no roof oversails the house or the road behind it.
        var plan=new TownPlan().Pave(23,17,19,10).Pave(29,16,7,1);
        // North side of the square: chapel, inn and shop.
        plan.Build(HouseStyle.Chapel,16,8,7,8,3).Build(HouseStyle.Inn,27,9,11,7,5).Build(HouseStyle.Cottage,40,10,8,6,2);
        // The lane west and east.
        plan.Build(HouseStyle.Cottage,5,20,7,6,2).Build(HouseStyle.Stone,14,21,7,5,3).Build(HouseStyle.Smithy,44,21,8,5,5,2).Build(HouseStyle.Cottage,54,20,7,6,3);
        // South of the road.
        plan.Build(HouseStyle.Cottage,11,33,7,6,3).Build(HouseStyle.Stone,20,34,8,6,2).Build(HouseStyle.Cottage,38,33,7,6,4).Build(HouseStyle.Barn,47,33,9,8,3,2);
        // Inside the inn: bar, hearth, tables and a runner from the door.
        plan.Add(TownProp.Counter,29,11).Add(TownProp.Shelf,29,10).Add(TownProp.Barrels,31,10).Add(TownProp.Hearth,35,10)
            .Add(TownProp.Table,29,13).Add(TownProp.Table,35,13).Add(TownProp.Rug,32,12).Add(TownProp.Rug,32,13).Add(TownProp.Rug,32,14);
        // Chapel: pews either side of the aisle, the warden's statue at its head.
        foreach(int y in new[]{11,13}){plan.Add(TownProp.Pew,18,y).Add(TownProp.Pew,21,y);}
        for(int y=10;y<=14;y++)plan.Add(TownProp.Rug,19,y,1);
        plan.Add(TownProp.Statue,19,9);
        // Shop, smithy and homes.
        plan.Add(TownProp.Counter,44,12).Add(TownProp.Shelf,41,11).Add(TownProp.Shelf,46,11).Add(TownProp.Crates,46,13).Add(TownProp.Sacks,41,13);
        plan.Add(TownProp.Hearth,46,22).Add(TownProp.Anvil,48,23).Add(TownProp.Barrels,50,22).Add(TownProp.Crates,45,23);
        plan.Add(TownProp.Bed,6,21).Add(TownProp.Hearth,9,21).Add(TownProp.Table,9,23).Add(TownProp.Rug,7,23);
        plan.Add(TownProp.Bed,15,22,1).Add(TownProp.Hearth,18,22).Add(TownProp.Rug,17,23,1);
        plan.Add(TownProp.Shelf,55,21).Add(TownProp.Bed,59,21).Add(TownProp.Table,57,23);
        plan.Add(TownProp.Bed,12,34).Add(TownProp.Hearth,15,34).Add(TownProp.Rug,14,36);
        plan.Add(TownProp.Shelf,21,35).Add(TownProp.Hearth,23,35).Add(TownProp.Bed,26,35,1).Add(TownProp.Table,24,37);
        plan.Add(TownProp.Bed,39,34,1).Add(TownProp.Table,42,35).Add(TownProp.Rug,41,36);
        plan.Add(TownProp.Hay,48,34,1).Add(TownProp.Hay,51,34,0).Add(TownProp.Hay,54,35,0).Add(TownProp.Cart,52,37).Add(TownProp.Sacks,54,38).Add(TownProp.Crates,48,37);
        // The square itself.
        plan.Add(TownProp.Fountain,32,21).Add(TownProp.Stall,26,19,0).Add(TownProp.Stall,26,23,1).Add(TownProp.Stall,38,22,2)
            .Add(TownProp.LampPost,23,17).Add(TownProp.LampPost,41,17).Add(TownProp.LampPost,23,26).Add(TownProp.LampPost,41,26)
            .Add(TownProp.Bench,29,18).Add(TownProp.Bench,35,18).Add(TownProp.NoticeBoard,28,16)
            .Add(TownProp.Planter,31,16,0).Add(TownProp.Planter,33,16,1).Add(TownProp.TradeSign,34,16,0).Add(TownProp.BannerPole,36,16)
            .Add(TownProp.TradeSign,44,16,2).Add(TownProp.Barrels,46,16).Add(TownProp.TradeSign,47,26,1).Add(TownProp.Well,43,19);
        plan.Add(TownProp.Signpost,34,29).Add(TownProp.Woodpile,53,26).Add(TownProp.Washing,15,40).Add(TownProp.Hay,58,39,1).Add(TownProp.Bench,21,26);
        // A fenced kitchen garden west of the south cottages.
        for(int x=2;x<=8;x++){plan.Add(TownProp.FenceRun,x,31);if(x!=5)plan.Add(TownProp.FenceRun,x,35);}
        plan.Add(TownProp.Gate,5,35);
        for(int y=32;y<=35;y++){plan.Add(TownProp.FenceSide,2,y);plan.Add(TownProp.FenceSide,8,y);}
        for(int y=32;y<=34;y++)for(int x=3;x<=7;x++)if(x!=5)plan.Add(TownProp.Garden,x,y,x<5?0:1);
        // A field by the barn and a low wall along the south road.
        for(int y=43;y<=45;y++)for(int x=49;x<=56;x++)plan.Add(TownProp.Garden,x,y,0);
        plan.Add(TownProp.Scarecrow,53,43);
        for(int x=25;x<=29;x++)plan.Add(TownProp.StoneWall,x,43);
        for(int x=34;x<=38;x++)plan.Add(TownProp.StoneWall,x,46);
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
            // Outside first, then inside each kind of house, where its roof gives way.
            foreach(var (name,x,y) in new[]{("square",32f,22.5f),("west",12f,27.5f),("east",50f,27.5f),("south",24f,41.5f),
                ("inside-inn",32f,13.5f),("inside-chapel",19.5f,12.5f),("inside-cottage",8f,24.3f),("inside-smithy",48.5f,24.3f),("inside-barn",51f,39f)})
            {p.X=x*24;p.Y=y*24;w.Cancel(p);app.RebuildViews();app.ResumeGame();await Shot(app,name,folder);}
            // The whole town at once, for composition.
            p.X=32*24;p.Y=29.5f*24;app.RebuildViews();app.ResumeGame();app.Camera.Zoom=zoom*.5f;await Shot(app,"overview",folder);
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
            using(var atlas=TownAtlas.Props.GetImage())atlas.SavePng("build/town-preview/props.png");
        }
        finally{ReachAmbience.PreviewTime=-1;Engine.TimeScale=scale;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
