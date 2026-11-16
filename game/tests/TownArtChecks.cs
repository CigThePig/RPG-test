using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>The town kit: every furniture module painted and well-formed; block houses
/// carved as real wall and floor that the wayfarer can walk into through the doorway and
/// nowhere else; wild growth kept off town ground. Rendered: the town appears, each roof
/// hides its room from outside and gives way once the wayfarer is inside.</summary>
public static class TownArtChecks
{
    static bool Body(byte c)=>c!=0 && c!=35 && c!=36 && c!=37;
    public static void Run(Foundation app,Action<bool,string> check)
    {
        check(TownAtlas.Props.GetWidth()==TownArt.PropColumns*TownArt.Width && TownAtlas.Props.GetHeight()==TownArt.PropRows*TownArt.Height
            && ReferenceEquals(TownAtlas.Props,TownAtlas.Props) && TownAtlas.BuildCount==1,"one town atlas holds every module");
        var modules=new HashSet<int>();
        foreach(var prop in Enum.GetValues<TownProp>())for(int v=0;v<TownArt.Count(prop);v++)
        {
            var data=TownArt.Generate(prop,v);string name=prop.ToString()+v;
            check(data.Length==72*104 && data.All(n=>n<OverworldPropArt.Palette.Length) && data.Count(n=>n>0)>20,"valid indexed town art "+name);
            bool ring=true;
            for(int x=0;x<72;x++)ring&=data[x]==0 && data[103*72+x]==0;
            for(int y=0;y<104;y++)ring&=data[y*72]==0 && data[y*72+71]==0;
            check(ring,"transparent gutter round "+name);
            check(modules.Add(TownArt.Module(prop,v)) && data.SequenceEqual(TownArt.Generate(prop,v)),"distinct repeatable town prop "+name);
        }
        check(modules.Count==TownArt.Props,"every town prop module addressed");
        // Fence, wall, garden and rug modules span their whole tile so neighbours join.
        foreach(var run in new[]{TownProp.FenceRun,TownProp.StoneWall,TownProp.Garden,TownProp.Rug})
        {
            var data=TownArt.Generate(run,0);
            check(Enumerable.Range(0,TownArt.Height).Any(y=>Body(data[y*TownArt.Width+24])) && Enumerable.Range(0,TownArt.Height).Any(y=>Body(data[y*TownArt.Width+47])),run+" reaches both tile edges");
        }
        check(TownBlocks.Ramp.Length==35 && TownBlocks.Ramp.All(c=>c.A==1),"opaque block-house ramp");
        // Block houses are ordinary terrain: a wall ring, floor within, one doorway.
        var plan=TownPreview.Plan();var meadow=TownPreview.Region();var r=TownPreview.Region();
        check(Enum.GetValues<HouseStyle>().All(style=>plan.Houses.Any(h=>h.Style==style)) && plan.Pieces.Select(piece=>piece.Kind).Distinct().Count()==Enum.GetValues<TownProp>().Length-1,
            "reference town shows every house style and every placeable module");
        var player=app.World.Player;
        foreach(var house in plan.Houses)
        {
            string name=$"{house.Style} at {house.X},{house.Y}";
            bool shaped=true;int doors=0;
            for(int y=house.Y;y<house.Y+house.Height;y++)for(int x=house.X;x<house.X+house.Width;x++)
            {
                var tile=r.Tiles[y*r.Width+x];
                if(house.Doorway(x,y)){doors++;shaped&=tile==Terrain.Floor;}
                else shaped&=tile==(house.Ring(x,y)?Terrain.Wall:Terrain.Floor);
            }
            check(shaped && doors==house.DoorWidth && house.Width>=5 && house.Height>=5,"wall ring, floor and doorway of "+name);
            check(house.Wall is >=0 and <=2 && house.Roof is >=0 and <=2 && house.Floor is >=0 and <=2,"styled "+name);
            // The largest production body can stand in the middle of the room and in the doorway,
            // and walks from the street to the room: the only way in is the door.
            var inside=r.Center((house.Y+house.Height/2)*r.Width+house.X+house.Width/2);
            var street=r.Center((house.Y+house.Height+1)*r.Width+house.X+house.Door);
            check(r.CanStand(street,player) && r.Path(street,inside,player).Count>0,"the street leads into "+name);
            var sealedUp=TownPreview.Region();
            for(int x=house.X+house.Door;x<house.X+house.Door+house.DoorWidth;x++)sealedUp.Tiles[(house.Y+house.Height-1)*sealedUp.Width+x]=Terrain.Wall;
            check(sealedUp.Path(street,inside,player).Count==0,"walls of "+name+" admit no one but through the door");
            check(Enumerable.Range(house.X,house.Width).All(x=>!TownBlocks.Window(house,r.Seed,x).Window || !house.Doorway(x,house.Y+house.Height-1) && x!=house.X && x!=house.X+house.Width-1),"windows keep clear of the door and corners of "+name);
        }
        var claimed=plan.Houses.SelectMany(h=>Enumerable.Range(h.Y,h.Height).SelectMany(y=>Enumerable.Range(h.X,h.Width).Select(x=>(x,y)))).ToArray();
        check(claimed.Length==claimed.Distinct().Count(),"reference houses do not overlap");
        // Furniture stands on floor or open ground, never in a wall or the water.
        check(plan.Pieces.All(piece=>r.Tiles[piece.Y*r.Width+piece.X] is Terrain.Floor or Terrain.Grass or Terrain.Earth),"furniture stands on open tiles");
        check(plan.Pieces.Where(piece=>(TownProp)piece.Kind is TownProp.Bed or TownProp.Hearth or TownProp.Counter or TownProp.Pew or TownProp.Anvil).All(piece=>plan.HouseAt(piece.X,piece.Y)!=null),"furnishings are indoors");
        // A staged town keeps wild growth off its houses, furniture and paving.
        var art=new Dictionary<string,Texture2D>();
        int Wild(RegionState region)
        {
            var decorations=EnvironmentDecoration.Create(region,art);int count=decorations.Count+OverworldFlora.Plan(region).Count(f=>!f.Flat);
            foreach(var d in decorations)d.Free();return count;
        }
        string before=System.Text.Json.JsonSerializer.Serialize(r,Content.Json);
        int open=Wild(r);
        try
        {
            TownStage.Stage(r.Id,plan);
            check(ReferenceEquals(TownStage.For(r),plan) && Wild(r)<open,"a staged town thins wild growth");
            var decorations=EnvironmentDecoration.Create(r,art);
            check(decorations.All(d=>!plan.Occupied((int)(d.Position.X/24),(int)(d.Position.Y/12))),"no tree or shrub on town ground");
            foreach(var d in decorations)d.Free();
            var views=TownView.Create(plan);
            check(views.Count==plan.Pieces.Count(piece=>!TownArt.Flat((TownProp)piece.Kind)) && !typeof(CollisionObject2D).IsAssignableFrom(typeof(TownView)),"one nonblocking view per standing module");
            foreach(var view in views)view.Free();
            // Roofs: one per house, and each knows when the wayfarer is under it.
            var where=new Vector2(-1,-1);
            var roofs=TownRoof.Create(plan,()=>where,()=>false);
            check(roofs.Count==plan.Houses.Count && roofs.Zip(plan.Houses).All(pair=>pair.First.Position.Y>(pair.Second.Y+pair.Second.Height)*12 && pair.First.Position.Y<(pair.Second.Y+pair.Second.Height)*12+1),"a roof per house, sorted at its front edge");
            foreach(var roof in roofs)roof.Free();
            foreach(var row in TerrainWallRow.Create(r))row.Free();
        }
        finally{TownStage.Clear(r.Id);}
        check(TownStage.For(r)==null && Wild(r)==open,"clearing a staged town restores the growth round it");
        check(before==System.Text.Json.JsonSerializer.Serialize(r,Content.Json),"staging and drawing a town does not mutate serialized geography");
    }
    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);var size=app.GetWindow().Size;double scale=Engine.TimeScale;
        try
        {
            Engine.TimeScale=0;ReachAmbience.PreviewTime=0;app.ResumeGame();w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var p=w.Player;p.Health=p.MaxHealth;p.Invulnerable=1000;
            app.GetWindow().Size=new Vector2I(1280,800);app.Ui.Visible=false;
            async System.Threading.Tasks.Task<Image> Frame()
            {
                for(int i=0;i<10;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
                await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                return app.GetViewport().GetTexture().GetImage();
            }
            var plan=TownPreview.Plan();var r=TownPreview.Region();w.RegionsById[r.Id]=r;p.Region=r.Id;TownStage.Stage(r.Id,plan);
            var parent=app.Views["player"].GetParent();
            int count=0;
            foreach(var house in plan.Houses)
            {
                // Stand in the street before the door, then in the middle of the room.
                var street=r.Center((house.Y+house.Height+1)*r.Width+house.X+house.Door);
                var room=r.Center((house.Y+house.Height/2)*r.Width+house.X+house.Width/2);
                p.X=street.X;p.Y=street.Y;w.Cancel(p);app.RebuildViews();app.ResumeGame();using var outside=await Frame();
                var roof=parent.GetChildren().OfType<TownRoof>().Single(n=>n.House==house);
                if(roof.Occupied || roof.Opacity!=1)throw new InvalidOperationException($"Roof of {house.Style} is not closed from the street");
                // The move itself, through the door, on the real collision.
                w.Move(p,new V(0,room.Y-street.Y));
                if(!house.Contains((int)(p.X/24),(int)(p.Y/24)))throw new InvalidOperationException($"Could not walk into {house.Style} at {house.X},{house.Y}");
                p.X=room.X;p.Y=room.Y;app.Views["player"].SnapPosition();
                using var inside=await Frame();
                if(!roof.Occupied || roof.Opacity!=0)throw new InvalidOperationException($"Roof of {house.Style} did not open for the wayfarer");
                // With the roof open the room shows: compare the roof's own area in both frames,
                // moving the outside frame by the camera's shift between them.
                var others=parent.GetChildren().OfType<TownRoof>().Where(n=>n!=roof);
                if(others.Any(n=>n.Opacity!=1))throw new InvalidOperationException("Another house's roof opened too");
                var transform=app.GetViewport().GetCanvasTransform();
                var topLeft=transform*new Vector2(house.X*24,house.Y*12);var bottomRight=transform*new Vector2((house.X+house.Width)*24,(house.Y+house.Height-1)*12);
                int floor=0,total=0;
                var boards=new[]{TownBlocks.Ramp[29],TownBlocks.Ramp[30],TownBlocks.Ramp[31],TownBlocks.Ramp[9],TownBlocks.Ramp[10],TownBlocks.Ramp[32],TownBlocks.Ramp[33]};
                for(int y=Math.Max(0,(int)topLeft.Y);y<Math.Min(inside.GetHeight(),(int)bottomRight.Y);y++)for(int x=Math.Max(0,(int)topLeft.X);x<Math.Min(inside.GetWidth(),(int)bottomRight.X);x++)
                {
                    var c=inside.GetPixel(x,y);total++;
                    // Cloud shadow darkens the floor; match its hue within a wide band.
                    if(boards.Any(b=>MathF.Abs(c.R/MathF.Max(c.G,.01f)-b.R/b.G)<.12f && MathF.Abs(c.B/MathF.Max(c.G,.01f)-b.B/b.G)<.14f))floor++;
                }
                if(total<400 || floor<total/4)throw new InvalidOperationException($"Room of {house.Style} is not visible with the roof open: {floor}/{total}");
                count++;
            }
            app.Ui.Visible=true;
            await TownPreview.Capture(app,"docs/town-screenshots",false);
            using(var atlas=TownAtlas.Props.GetImage())atlas.SavePng("docs/town-screenshots/atlas-props.png");
            GD.Print($"TOWN ART CHECKS PASSED: walked into {count} houses, each roof closed from the street and open within");
        }
        finally{app.Ui.Visible=true;TownStage.Clear(TownPreview.Id);ReachAmbience.PreviewTime=-1;Engine.TimeScale=scale;app.GetWindow().Size=size;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
