using System.Text.Json;
using Godot;
using V=System.Numerics.Vector2;
namespace Mosslight;
public static class LandmarkChecks
{
    public static void Run(Foundation app,Action<bool,string> check)
    {
        var library=Landmarks.Library;
        check(library.Count>=5,"packaged landmark library loads via FileAccess");
        void Invalid(Action<LandmarkTemplate> mutate,string label)
        {
            var t=JsonSerializer.Deserialize<LandmarkTemplate>(JsonSerializer.Serialize(library[0],Content.Json),Content.Json)!;mutate(t);
            try{Landmarks.Validate(t);throw new Exception("accepted malformed "+label);}
            catch(InvalidDataException e){check(e.Message.Contains(t.Id),"diagnostic template ID: "+label);}
        }
        Invalid(t=>t.Id="", "missing ID");Invalid(t=>t.Revision=0,"revision");Invalid(t=>t.Layout=new[]{"."},"dimensions");
        Invalid(t=>t.Layout[0]="?........","symbol");Invalid(t=>t.Theme="missing","material");Invalid(t=>t.Roles=new[]{"fake"},"role");
        Invalid(t=>t.Rotations=new[]{4},"rotation");Invalid(t=>t.DoorMasks=new[]{16},"doors");Invalid(t=>t.Focal=Array.Empty<int>(),"focal");
        Invalid(t=>t.ProtectedFeature=new[]{4,4},"interaction center");Invalid(t=>t.Anchors[0].X=20,"anchor bounds");
        Invalid(t=>t.Anchors[0].Asset="missing","asset");Invalid(t=>t.Anchors[0].Asset="records","flat asset cannot own invisible collision");Invalid(t=>t.Anchors[0].Weight=10,"required visual weight");
        Invalid(t=>t.Layout[3]="...#.....","blocked protected lane");
        foreach(var t in library)
        {
            Landmarks.Validate(t);
            for(int turn=0;turn<4;turn++)
            {
                check(Landmarks.RotateDoors(1,turn)==1<<turn,"cardinal rotation "+turn);
                check(Landmarks.Rotate(2,3,turn)==turn switch {0=>(2,3),1=>(7,2),2=>(8,7),_=>(3,8)},"coordinate rotation "+turn);
                foreach(int mask in Enumerable.Range(1,15))
                {
                    var fixture=Fixture();var roles=new[]{t.Roles[0]};
                    check(Landmarks.TryApply(fixture,0,0,mask,roles,t,turn),$"safe template {t.Id} orientation={turn} doors={mask}");
                    check(Landmarks.RoomSafe(fixture,0,0),"radius 11 interior sweep "+t.Id);
                    check(fixture.Landmarks.Single().Anchors.Where(a=>a.Collision=="terrain").All(a=>fixture.Tiles[a.Y*11+a.X]==Terrain.Wall),"visible structural terrain agreement "+t.Id);
                }
            }
        }
        // Exercise explicit door compatibility, rollback and safe fallback.
        var restricted=JsonSerializer.Deserialize<LandmarkTemplate>(JsonSerializer.Serialize(library[0],Content.Json),Content.Json)!;
        restricted.DoorMasks=new[]{1};check(!Landmarks.Compatible(restricted,restricted.Roles,3,0) && Landmarks.Compatible(restricted,restricted.Roles,2,1),"rotated eligibility respects actual cardinal connections");
        var conflict=Fixture();conflict.Features[2*11+2]="chest";string untouched=JsonSerializer.Serialize(conflict,Content.Json);
        check(!Landmarks.TryApply(conflict,0,0,1,new[]{library[0].Roles[0]},library[0],0) && untouched==JsonSerializer.Serialize(conflict,Content.Json),"candidate feature conflict leaves terrain untouched");
        var encountered=new HashSet<string>();var themeSet=new HashSet<string>();
        foreach(int seed in Enumerable.Range(0,64).Append(1337).Concat(Enumerable.Range(1000,32)))
        {
            var a=Regions.Dungeon(seed);var b=Regions.Dungeon(seed);
            check(JsonSerializer.Serialize(a,Content.Json)==JsonSerializer.Serialize(b,Content.Json),"independent resolved generation/save manifest repeat "+seed);
            check(a.Landmarks.Count>0 && a.Landmarks.Count(l=>l.Roles.Contains("entrance"))==1 && a.Landmarks.Count(l=>l.Roles.Contains("objective"))==1,"classified mission landmarks "+seed);
            check(a.Landmarks.SelectMany(l=>l.Anchors).Count(v=>v.Collision=="terrain")<=96,"required architectural node budget "+seed);
            check(Landmarks.LastRejections.All(message=>message.Contains("seed="+seed)),"bounded rejection diagnostics identify seed "+seed);
            foreach(var room in a.Landmarks){encountered.Add(room.Template);themeSet.Add(room.Theme);}
            Landmarks.ValidateSaved(a);
            var saved=JsonSerializer.Deserialize<RegionState>(JsonSerializer.Serialize(a,Content.Json),Content.Json)!;
            check(saved.Tiles.SequenceEqual(a.Tiles) && JsonSerializer.Serialize(saved.Landmarks,Content.Json)==JsonSerializer.Serialize(a.Landmarks,Content.Json),"resolved landmark persistence "+seed);
            var decorations=EnvironmentDecoration.Create(a,new());
            check(decorations.Count<=96 && decorations.Count(d=>d.PropModule>=0)==a.Landmarks.Sum(l=>l.Anchors.Count(v=>v.Collision=="terrain")),"required landmarks precede scatter "+seed);
            foreach(var d in decorations)d.Free();
        }
        check(library.All(t=>encountered.Contains(t.Id)),"all five landmarks occur naturally across seed sample");
        check(Landmarks.Themes.All(themeSet.Contains),"all three themes occur in production");
        foreach(int theme in Enumerable.Range(0,3))foreach(int asset in Enumerable.Range(0,5))foreach(int variant in Enumerable.Range(0,3))
            check(EnvironmentArtGenerator.PropModule(asset,variant,Landmarks.Themes[theme]).All(m=>m.Rect.Position.X>=1 && m.Rect.End.X<=25 && m.Rect.Position.Y>=0 && m.Rect.End.Y<=63),"bounded prop atlas recipe");
        var invalidSaved=Regions.Dungeon(1);
        invalidSaved.Landmarks.SelectMany(l=>l.Anchors).First(a=>a.Collision=="terrain").Asset="records";
        try{Landmarks.ValidateSaved(invalidSaved);throw new Exception("Accepted invisible saved structure");}
        catch(InvalidDataException e){check(e.Message.Contains("asset/collision"),"saved flat asset cannot own invisible collision");}
        LegacyAndProgress(app,check);
        Combat(app,check);
    }
    static void LegacyAndProgress(Foundation app,Action<bool,string> check)
    {
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);
        try
        {
            string legacy=Godot.FileAccess.GetFileAsString("res://game/tests/fixtures/pr5-part2-dungeon-save.json");
            var old=Persistence.Parse(legacy,w.Content);var tiles=old.Regions["ruin"].Tiles.ToArray();
            Persistence.Restore(w,app.Narrative,legacy);
            check(w.RegionsById["ruin"].Landmarks.Count==0 && tiles.SequenceEqual(w.RegionsById["ruin"].Tiles),"real pre-Part3 expanded dungeon retains geometry without inferred landmarks");
            string captured=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,captured);
            check(captured==Persistence.Capture(w,app.Narrative),"Part2 snapshot exact continuation roundtrip");
            Persistence.Restore(w,app.Narrative,original);
            w.RegionsById["ruin"]=Regions.Dungeon(1);w.Enter(w.Player,"ruin");var r=w.RegionsById["ruin"];
            foreach(string kind in new[]{"key","lock","chest","relic"})
            {
                if(kind=="lock")r.OpenDoors.UnionWith(r.GateGroups().First());
                else foreach(int at in r.Features.Where(f=>f.Value==kind).Select(f=>f.Key))r.Depleted.Add(at);
                captured=Persistence.Capture(w,app.Narrative);Persistence.Restore(w,app.Narrative,captured);r=w.RegionsById["ruin"];
                check(captured==Persistence.Capture(w,app.Narrative),"resolved landmark/terrain/state survives restore after "+kind);
            }
        }
        finally{Persistence.Restore(w,app.Narrative,original);}
    }
    public static RegionState Fixture()
    {
        var r=new RegionState{Id="landmark-fixture",Width=11,Height=11,Tiles=Enumerable.Repeat(Terrain.Wall,121).ToArray(),Entrance=60,Objective=60};
        for(int y=1;y<=9;y++)for(int x=1;x<=9;x++)r.Tiles[y*11+x]=Terrain.Floor;
        return r;
    }
    static void Combat(Foundation app,Action<bool,string> check)
    {
        var w=app.World;string original=Persistence.Capture(w,app.Narrative);
        try
        {
            var r=Regions.Dungeon(42);w.RegionsById["ruin"]=r;
            foreach(var actor in w.Actors.Values)actor.Region="reach";
            var p=w.Player;p.Region="ruin";var room=r.Landmarks.First(l=>l.Template=="ruined-entrance");
            var anchor=room.Anchors.First(a=>a.Collision=="terrain");int at=(room.Y*11+anchor.Y)*r.Width+room.X*11+anchor.X;
            var center=r.Center(at);
            void Reset(V point){w.Cancel(p);p.X=point.X;p.Y=point.Y;p.Health=p.MaxHealth;p.Cooldowns.Clear();p.Statuses.Clear();}
            Reset(center+new V(0,24));var start=p.Position;
            check(w.TryStartPlayerDodge(-V.UnitY),"dodge toward authored column begins");
            for(int i=0;i<40;i++)w.Step(1f/60);
            check(r.CanStand(p.Position,p) && V.Distance(start,p.Position)<30,$"production dodge stops at visible structural column at={at} center={center} start={start} end={p.Position} radius={p.Radius} clear={r.CanStand(p.Position,p)} distance={V.Distance(start,p.Position)}");
            Reset(center+new V(0,24));start=p.Position;check(w.TryStartPlayerSlash(-V.UnitY),"melee beside authored column");
            for(int i=0;i<45;i++)w.Step(1f/60);
            check(r.CanStand(p.Position,p),"melee lunge respects interior wall");
            var source=w.Actors.Values.First(a=>a.Id!="player");source.Region="ruin";source.X=center.X;source.Y=center.Y-24;
            Reset(center+new V(0,24));var spit=w.FindAbility("spit")!;
            check(ProjectileFlight.Advance(w,ProjectileFlight.Release(source,p,spit),spit,r,1).Outcome=="terrain","production projectile hits authored architecture");
            // Actual movement around the column, no teleport over collision.
            Reset(center+new V(0,24));var destination=center-new V(0,24);var path=r.Path(p.Position,destination,p);
            check(path.Count>0,"actual player route around column");
            foreach(int tile in path){var end=r.Center(tile);for(int i=0;i<200 && V.Distance(end,p.Position)>.01f;i++){var delta=end-p.Position;if(delta.Length()>3)delta=V.Normalize(delta)*3;w.Move(p,delta);}}
            check(r.Index(p.Position)==r.Index(destination) && r.CanStand(p.Position,p),"production World.Move walks around interior column");
        }
        finally{Persistence.Restore(w,app.Narrative,original);}
    }
}
