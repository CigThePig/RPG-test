using Godot;
using System.Text.Json;
namespace Mosslight;

public static class TownGenerationChecks
{
    public static void Run(Foundation app, Action<bool,string> check)
    {
        string Json(RegionState r) => JsonSerializer.Serialize(r,Content.Json);
        var layouts = new HashSet<string>();
        foreach(int seed in new[]{1,42,1337,-1,int.MinValue,int.MaxValue,2,3,4,5,6,7})
        {
            var r=Regions.Overworld(seed); var town=r.Town!; var p=TownStage.For(r)!;
            TownState.Validate(r);
            check(town!=null && p.Houses.Count==6 && p.Houses.Select(h=>h.Style).Distinct().Count()==6,"six generated town styles seed "+seed);
            layouts.Add(JsonSerializer.Serialize(town,Content.Json));
            check(Json(r)==Json(Regions.Overworld(seed)),"repeatable town and world seed "+seed);
            var player=new ActorState{Radius=11}; // Largest production footprint.
            var spawn=r.Center(r.Entrance);
            check(r.CanStand(spawn,player) && r.Path(spawn,r.Center(r.Objective),player).Count>0,"town preserves spawn and route to ruin "+seed);
            foreach(var h in p.Houses)
            {
                var inside=r.Center((h.Y+h.Height/2)*r.Width+h.X+h.Width/2);
                var street=r.Center((h.Y+h.Height)*r.Width+h.X+h.Door);
                check(r.CanStand(street,player) && r.Path(spawn,inside,player).Count>0,"spawn reaches generated "+h.Style+" seed "+seed);
                var closed=JsonSerializer.Deserialize<RegionState>(Json(r),Content.Json)!;
                for(int x=h.X+h.Door;x<h.X+h.Door+h.DoorWidth;x++)closed.Tiles[(h.Y+h.Height-1)*r.Width+x]=Terrain.Wall;
                check(closed.Path(spawn,inside,player).Count==0,"generated house walls only admit through door "+seed+" "+h.Style);
            }
            check(p.Paving.All(t=>r.Tiles[t.Y*r.Width+t.X] is Terrain.Earth or Terrain.Floor),"connected town paving stays clear of walls "+seed);
            check(r.Features.Keys.All(at=>r.CanStand(r.Center(at),player) && r.Path(spawn,r.Center(at),player).Count>0),"all overworld features remain reachable "+seed);
            foreach(var a in p.Houses) foreach(var b in p.Houses.Where(b=>b.Y>a.Y+a.Height-1))
                check(b.Y-(a.Y+a.Height)>=4,"generated roof rows have four clear tiles "+seed);
            string serialized=Json(r);
            var loaded=JsonSerializer.Deserialize<RegionState>(serialized,Content.Json)!;
            TownState.Validate(loaded);
            check(Json(loaded)==serialized && TownStage.For(loaded)!.Houses.SequenceEqual(p.Houses) && TownStage.For(loaded)!.Paving.SetEquals(p.Paving),"town composition survives serialization without staging "+seed);
            TownGenerator.Install(r);
            check(Json(r)==serialized,"town install is idempotent "+seed);
        }
        check(layouts.Count==12,"different seeds vary town plots and furnishing");
        foreach(var (w,h) in new[]{(16,16),(32,48),(48,32),(48,44),(96,64)})
        {
            var r=Regions.Overworld(42,w,h);
            check((r.Town!=null)==(w>=48&&h>=44),"town bounds and small-map fallback "+w+"x"+h);
            TownState.Validate(r);
        }
        // Full production capture/restore verifies town data through the real save parser.
        var world=app.World; string saved=Persistence.Capture(world,app.Narrative);
        try
        {
            // Preserve the live production dimensions: actors from a 25x world may be
            // outside the old 64x48 reference map during this save/load test.
            var reach=world.RegionsById["reach"];
            world.RegionsById["reach"]=Regions.Overworld(world.Seed,reach.Width,reach.Height);
            var r=world.RegionsById["reach"];string townJson=Json(r);
            string capture=Persistence.Capture(world,app.Narrative);
            Persistence.Restore(world,app.Narrative,capture);
            check(Json(world.RegionsById["reach"])==townJson,"production town save/load roundtrip");
            // Reject corrupt composition before any live state changes.
            world.RegionsById["reach"].Town!.Houses[0]=new(HouseStyle.Inn,-1,5,7,7,3);
            string bad=Persistence.Capture(world,app.Narrative);
            bool rejected=false;try{Persistence.Parse(bad,world.Content);}catch(InvalidDataException){rejected=true;}
            check(rejected,"invalid saved town rejected");
            Persistence.Restore(world,app.Narrative,capture);
            world.RegionsById["reach"].Town=null;
            string legacy=Persistence.Capture(world,app.Narrative);
            Persistence.Restore(world,app.Narrative,legacy);
            check(world.RegionsById["reach"].Town==null,"legacy saves load without retroactive town carving");
        }
        finally { Persistence.Restore(world,app.Narrative,saved); }
    }
    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        var w=app.World; string snapshot=Persistence.Capture(w,app.Narrative);
        var size=app.GetWindow().Size; var zoom=app.Camera.Zoom; double scale=Engine.TimeScale;
        const string folder="docs/generated-town-screenshots";
        Directory.CreateDirectory(folder);
        try
        {
            Engine.TimeScale=0; ReachAmbience.PreviewTime=0; app.ResumeGame(); w.Projectiles.Clear();
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="unused";
            var player=w.Player; player.Health=player.MaxHealth;player.Invulnerable=1000;
            app.GetWindow().Size=new Vector2I(2340,1080);
            async System.Threading.Tasks.Task Frame(string? name=null)
            {
                for(int i=0;i<10;i++){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
                await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                if(name!=null){using var image=app.GetViewport().GetTexture().GetImage();image.SavePng(folder+"/"+name+".png");}
            }
            foreach(int seed in new[]{1,42,1337})
            {
                var r=Regions.Overworld(seed,320,240); w.RegionsById["reach"]=r; player.Region="reach";
                var plan=TownStage.For(r)!;
                foreach(var h in plan.Houses)
                {
                    var street=r.Center((h.Y+h.Height+1)*r.Width+h.X+h.Door);
                    player.X=street.X; player.Y=street.Y; w.Cancel(player);app.RebuildViews();app.ResumeGame();
                    await Frame();
                    var roofs=app.Views["player"].GetParent().GetChildren().OfType<TownRoof>().ToArray();
                    var roof=roofs.Single(n=>n.House==h);
                    if(roof.Occupied||roof.Opacity!=1)throw new InvalidOperationException("Generated roof closed state "+seed+" "+h.Style);
                    // Move north through the actual doorway, without teleporting across a wall.
                    w.Move(player,new System.Numerics.Vector2(0,-3*24));
                    app.Views["player"].SnapPosition();
                    await Frame(seed==42 ? "inside-"+h.Style.ToString().ToLowerInvariant() : null);
                    if(!roof.Occupied||roof.Opacity!=0||roofs.Any(n=>n!=roof&&n.Opacity!=1))
                        throw new InvalidOperationException("Generated doorway or roof entry "+seed+" "+h.Style);
                    w.Move(player,new System.Numerics.Vector2(0,3*24));app.Views["player"].SnapPosition();await Frame();
                    if(roof.Occupied||roof.Opacity!=1)throw new InvalidOperationException("Generated roof exit "+seed+" "+h.Style);
                }
                var spawn=r.Center(r.Entrance);player.X=spawn.X;player.Y=spawn.Y;app.RebuildViews();app.ResumeGame();
                await Frame("seed-"+seed+"-street");
                player.X=39.5f*24;player.Y=(r.Height/2+3)*24;app.RebuildViews();app.ResumeGame();app.Camera.Zoom=zoom*.5f;
                await Frame("seed-"+seed+"-overview");app.Camera.Zoom=zoom;
            }
            GD.Print("GENERATED TOWN RENDERED CHECKS PASSED: 18 actual door entries and exits across seeds 1, 42, 1337");
        }
        finally
        {
            ReachAmbience.PreviewTime=-1;Engine.TimeScale=scale;app.Camera.Zoom=zoom;app.GetWindow().Size=size;
            Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();
        }
    }

}
