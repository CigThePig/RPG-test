using System.Text.Json;
using System.Diagnostics;
using V = System.Numerics.Vector2;
namespace Mosslight;

public sealed class LandmarkAnchor
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Asset { get; set; } = "";
    public string Category { get; set; } = "";
    public string Collision { get; set; } = "cosmetic";
    public int Priority { get; set; } = 3;
    public int Weight { get; set; } = 100;
    public int Variant { get; set; }
    public int Facing { get; set; }
}
public sealed class LandmarkTemplate
{
    public string Id { get; set; } = "";
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public string Theme { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
    // Bitmask N=1 E=2 S=4 W=8; canonical masks rotate with the template.
    public int[] DoorMasks { get; set; } = Array.Empty<int>();
    public int[] Rotations { get; set; } = Array.Empty<int>();
    public string[] Layout { get; set; } = Array.Empty<string>();
    public int[] Focal { get; set; } = Array.Empty<int>();
    public int[] ProtectedFeature { get; set; } = Array.Empty<int>();
    public LandmarkAnchor[] Anchors { get; set; } = Array.Empty<LandmarkAnchor>();
}
// Resolved manifest, not a pointer back into mutable template content. Terrain is
// always the saved tile array.
public sealed class LandmarkRoom
{
    // Layout cell, plus the absolute tile origin of the template frame (core minus one).
    public int X { get; set; }
    public int Y { get; set; }
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public string Template { get; set; } = "";
    public int Revision { get; set; }
    public int Rotation { get; set; }
    public int Doors { get; set; }
    public string Theme { get; set; } = "weathered";
    public string[] Roles { get; set; } = Array.Empty<string>();
    public int FocalX { get; set; } = 5;
    public int FocalY { get; set; } = 5;
    public List<LandmarkAnchor> Anchors { get; set; } = new();
}
public static class Landmarks
{
    public const int SelectionRevision = 1;
    public static readonly string[] Themes = { "weathered", "overgrown", "ceremonial" };
    public static readonly string[] Assets = { "column", "shelf", "altar", "storage", "collapse", "records", "moss", "mosaic", "threshold", "offering" };
    public static readonly string[] Categories = { "ground", "low", "tall", "architecture" };
    public static readonly string[] Roles = { "entrance", "objective", "key", "chest", "ordinary", "dead-end", "junction", "hub" };
    static LandmarkTemplate[]? templates;
    public static IReadOnlyList<LandmarkTemplate> Library => templates ??= Parse(Godot.FileAccess.GetFileAsString("res://game/data/landmarks/rooms.json"));
    public static double LastComposeMilliseconds { get; private set; }
    public static double LastValidationMilliseconds { get; private set; }
    public static double LastSelectionMilliseconds { get; private set; }
    public static string LastCandidateFailure { get; private set; } = "";
    public static IReadOnlyList<string> LastRejections => rejections;
    static readonly List<string> rejections = new();
    public static LandmarkTemplate[] Parse(string json)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<LandmarkTemplate[]>(json, new JsonSerializerOptions(Content.Json){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow}) ?? throw new InvalidDataException("null library");
            if(entries.Length == 0 || entries.Length > 128) throw new InvalidDataException("library must contain 1..128 templates");
            foreach(var t in entries) { if(t==null)throw new InvalidDataException("Landmark library contains null template");Validate(t); }
            if(entries.Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=entries.Length)throw new InvalidDataException("duplicate template ID");
            return entries.OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray();
        }
        catch(JsonException e){throw new InvalidDataException("Landmark JSON: "+e.Message,e);}
    }
    public static (int X,int Y) Rotate(int x,int y,int turn)
    {
        for(int i=0;i<turn;i++)(x,y)=(10-y,x);
        return (x,y);
    }
    public static int RotateDoors(int mask,int turn) => ((mask<<turn)|(mask>>(4-turn)))&15;
    public static bool Protected(int x,int y) => x is >=4 and <=6 || y is >=4 and <=6;
    public static void Validate(LandmarkTemplate t)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException($"Landmark '{t.Id}': {why}");}
        if(t.Theme==null || t.Roles==null || t.DoorMasks==null || t.Rotations==null || t.Layout==null || t.Layout.Any(s=>s==null) || t.Focal==null || t.ProtectedFeature==null || t.Anchors==null || t.Anchors.Any(a=>a==null))throw new InvalidDataException($"Landmark '{t.Id}': null required field");
        Need(!string.IsNullOrWhiteSpace(t.Id) && t.Id.Length<=64 && t.Revision>0 && !string.IsNullOrWhiteSpace(t.Name),"required ID/revision/name");
        Need(t.Theme=="zone" || Themes.Contains(t.Theme),"unknown material/theme "+t.Theme);
        Need(t.Roles.Length>0 && t.Roles.All(Roles.Contains),"unknown or missing room roles");
        Need(t.DoorMasks.Length>0 && t.DoorMasks.All(m=>m is >=1 and <=15),"door masks must be 1..15");
        Need(t.Rotations.Length>0 && t.Rotations.All(n=>n is >=0 and <=3) && t.Rotations.Distinct().Count()==t.Rotations.Length,"invalid rotation declarations");
        Need(t.Layout.Length==9 && t.Layout.All(row=>row.Length==9),"layout must be 9x9");
        Need(t.Focal.Length==2 && t.Focal.All(n=>n is >=1 and <=9),"missing/out-of-bounds focal anchor");
        Need(t.ProtectedFeature.SequenceEqual(new[]{5,5}),"protected interaction position must be room center [5,5]");
        int blockers=0;
        for(int y=1;y<=9;y++)for(int x=1;x<=9;x++)
        {
            char c=t.Layout[y-1][x-1];Need(c is '.' or '#', $"unknown symbol '{c}' at {x},{y}");
            if(c!='#')continue;
            blockers++;Need(x is >=2 and <=8 && y is >=2 and <=8 && !Protected(x,y),$"blocker in protected doorway/feature/perimeter lane at {x},{y}");
            Need(t.Anchors.Count(a=>a.X==x && a.Y==y && a.Collision=="terrain")==1,$"blocker needs exactly one visible structural prop at {x},{y}");
        }
        Need(blockers<=8,"at most eight structural tiles per room");
        Need(t.Anchors.Length>0 && t.Anchors.Length<=24,"1..24 anchors required");
        foreach(var a in t.Anchors)
        {
            Need(a.X is >=1 and <=9 && a.Y is >=1 and <=9,$"anchor outside room at {a.X},{a.Y}");
            Need(Assets.Contains(a.Asset) && Categories.Contains(a.Category),$"unknown asset/category at {a.X},{a.Y}: {a.Asset}/{a.Category}");
            Need(a.Collision is "terrain" or "cosmetic" && a.Priority is >=0 and <=4 && a.Weight is >=1 and <=100 && a.Facing is >=0 and <=3,"invalid anchor collision/priority/weight/facing");
            Need(a.Collision!="terrain" || (t.Layout[a.Y-1][a.X-1]=='#' && a.Priority<=1 && a.Weight==100),$"structural prop requires permanent visible terrain at {a.X},{a.Y}");
            Need(a.Category!="tall" || a.Collision=="terrain","solid-looking tall props require terrain");
            Need(a.Collision!="terrain" || new[]{"column","shelf","altar","storage","collapse"}.Contains(a.Asset),"terrain collision requires a structural asset");
            Need(a.Collision!="terrain" || a.Category=="tall","structural props must use tall category");
            Need(a.Collision!="cosmetic" || a.Category is "ground" or "low","cosmetic slots must be ground/low");
            Need(a.Collision!="cosmetic" || new[]{"records","moss","mosaic","threshold","offering"}.Contains(a.Asset),"solid-looking prop cannot be cosmetic");
            Need(a.Collision!="cosmetic" || t.Layout[a.Y-1][a.X-1]=='.',"cosmetic anchor on blocker");
        }
    }
    public static string[] Classify(RegionState r,DungeonRoom room)
    {
        string feature=r.Features.GetValueOrDefault(room.CenterY*r.Width+room.CenterX,"");
        string primary=feature switch {"exit"=>"entrance","relic"=>"objective","key"=>"key","chest"=>"chest",_=>"ordinary"};
        int count=Enumerable.Range(0,4).Count(i=>(room.Doors&(1<<i))!=0);
        return new[]{primary,count==1?"dead-end":count==4?"hub":count==3?"junction":"ordinary"}.Distinct().ToArray();
    }
    public static uint Hash(int seed,int x,int y,int slot=0)
    {
        // Selection owns its revision: presentation revisions cannot reroll geography.
        unchecked { uint n=(uint)seed ^ (uint)x*0x9e3779b9u ^ (uint)y*0x85ebca6bu ^ (uint)slot*0xc2b2ae35u ^ SelectionRevision;
            n^=n>>16;n*=0x7feb352du;n^=n>>15;n*=0x846ca68bu;return n^(n>>16); }
    }
    static uint IdHash(string id){unchecked{uint h=2166136261;foreach(char c in id){h^=c;h*=16777619;}return h;}}
    public static bool Compatible(LandmarkTemplate t,string[] roles,int doors,int rotation) => t.Roles.Contains(roles[0]) && t.DoorMasks.Any(m=>RotateDoors(m,rotation)==doors);
    public static void Compose(RegionState r)
    {
        var timer=Stopwatch.StartNew();LastValidationMilliseconds=LastSelectionMilliseconds=0;rejections.Clear();
        if(r.Landmarks.Count!=0)throw new InvalidOperationException("Never recompose an existing region");
        // Admit mission landmarks before optional rooms can consume structural budget.
        // Halls are corridor junctions and never receive templates.
        var cells=r.Rooms.Where(c=>c.Kind=="room")
            .OrderBy(c=>r.Features.GetValueOrDefault(c.CenterY*r.Width+c.CenterX,"") switch {"exit"=>0,"relic"=>1,"key"=>2,"chest"=>3,_=>4}).ThenBy(c=>c.Y).ThenBy(c=>c.X);
        foreach(var cell in cells)
        {
            int x=cell.X,y=cell.Y;
            int doors=cell.Doors;var roles=Classify(r,cell);
            var selectionTimer=Stopwatch.StartNew();
            var options=Library.SelectMany(t=>t.Rotations.Where(turn=>Compatible(t,roles,doors,turn)).Select(turn=>(Template:t,Turn:turn)))
                .OrderBy(o=>Hash(r.Seed,x,y,unchecked((int)IdHash(o.Template.Id)+o.Turn*97))).ThenBy(o=>o.Template.Id,StringComparer.Ordinal).ThenBy(o=>o.Turn).ToArray();
            LastSelectionMilliseconds+=selectionTimer.Elapsed.TotalMilliseconds;
            bool applied=false;
            foreach(var option in options)
            {
                if(TryApply(r,cell,doors,roles,option.Template,option.Turn)){applied=true;break;}
                string reason=$"seed={r.Seed} room={x},{y} template={option.Template.Id}@{option.Template.Revision} rotation={option.Turn}: {LastCandidateFailure}";
                rejections.Add(reason);Godot.GD.Print("LANDMARK CANDIDATE REJECTED: "+reason);
            }
            if(!applied)r.Landmarks.Add(new LandmarkRoom{X=x,Y=y,OriginX=cell.CoreX-1,OriginY=cell.CoreY-1,Template="safe-open",Revision=1,Doors=doors,Roles=roles,Theme=Zone(r.Seed,x,y)});
        }
        if(!DungeonValidator.Solvable(r))throw new InvalidOperationException("Landmark composition violated mission integrity for seed "+r.Seed);
        LastComposeMilliseconds=timer.Elapsed.TotalMilliseconds;
    }
    public static string Zone(int seed,int x,int y) => Hash(seed,x/2,y/2,31)%3==0?"overgrown":"weathered";
    public static bool TryApply(RegionState r,DungeonRoom cell,int doors,string[] roles,LandmarkTemplate t,int rotation)
    {
        int x=cell.X,y=cell.Y,ox=cell.CoreX-1,oy=cell.CoreY-1;
        LastCandidateFailure="role/doorway mismatch";
        if(!Compatible(t,roles,doors,rotation))return false;
        LastCandidateFailure="structural node budget exhausted";
        if(r.Landmarks.Sum(l=>l.Anchors.Count(a=>a.Collision=="terrain"))+t.Anchors.Count(a=>a.Collision=="terrain")>96)return false;
        var changed=new List<int>();
        for(int ly=1;ly<=9;ly++)for(int lx=1;lx<=9;lx++)if(t.Layout[ly-1][lx-1]=='#')
        {
            var p=Rotate(lx,ly,rotation);int at=(oy+p.Y)*r.Width+ox+p.X;
            LastCandidateFailure=$"terrain/feature conflict at local {p.X},{p.Y}";
            if(r.Tiles[at]!=Terrain.Floor || r.Features.Any(f=>f.Value!="lock" && V.Distance(r.Center(f.Key),r.Center(at))<60))return false;
            changed.Add(at);
        }
        foreach(int at in changed)r.Tiles[at]=Terrain.Wall;
        var timer=Stopwatch.StartNew();bool safe=RoomSafe(r,cell);LastValidationMilliseconds+=timer.Elapsed.TotalMilliseconds;
        LastCandidateFailure="radius-11 footprint or protected lane disconnected";
        if(!safe){foreach(int at in changed)r.Tiles[at]=Terrain.Floor;return false;}
        var room=new LandmarkRoom{X=x,Y=y,OriginX=ox,OriginY=oy,Template=t.Id,Revision=t.Revision,Rotation=rotation,Doors=doors,Roles=roles,Theme=t.Theme=="zone"?Zone(r.Seed,x,y):t.Theme};
        var focal=Rotate(t.Focal[0],t.Focal[1],rotation);room.FocalX=focal.X;room.FocalY=focal.Y;
        int slot=0;
        foreach(var a in t.Anchors)
        {
            uint h=Hash(r.Seed,x,y,unchecked((int)IdHash(t.Id)+100+slot++));if(h%100>=a.Weight)continue;
            var p=Rotate(a.X,a.Y,rotation);
            room.Anchors.Add(new LandmarkAnchor{X=p.X,Y=p.Y,Asset=a.Asset,Category=a.Category,Collision=a.Collision,Priority=a.Priority,Weight=100,Variant=(int)(h%3),Facing=(a.Facing+rotation)%4});
        }
        r.Landmarks.Add(room);LastCandidateFailure="";return true;
    }
    public static bool RoomSafe(RegionState r,DungeonRoom cell)
    {
        // Largest production radius is 11. Flood actual swept circular footprints across
        // the whole room shape, not only point connectivity. No doors are opened/mutated.
        var actor=new ActorState{Radius=11};int mid=cell.CenterY*r.Width+cell.CenterX;
        var seen=new HashSet<int>{mid};var queue=new Queue<int>();queue.Enqueue(mid);
        while(queue.Count>0)
        {
            int at=queue.Dequeue();
            foreach(int n in r.Neighbours(at))
                if(cell.Contains(n%r.Width,n/r.Width) && !seen.Contains(n) && r.CanTravel(r.Center(at),r.Center(n),actor,true)){seen.Add(n);queue.Enqueue(n);}
        }
        foreach(var (tx,ty) in cell.Tiles())
        {
            int at=ty*r.Width+tx,lx=tx-cell.CoreX+1,ly=ty-cell.CoreY+1;
            if(r.Tiles[at]==Terrain.Floor && !seen.Contains(at))return false;
            if(lx is >=1 and <=9 && ly is >=1 and <=9 && Protected(lx,ly) && r.Tiles[at]!=Terrain.Floor)return false;
        }
        return true;
    }
    public static void ValidateSaved(RegionState r)
    {
        if(r.Landmarks!=null && r.Landmarks.All(l=>l!=null && l.Anchors!=null) && r.Landmarks.Sum(l=>l.Anchors.Count(a=>a!=null && a.Collision=="terrain"))>96)throw new InvalidDataException("Saved landmark node budget exceeded");
        if(r.Landmarks==null || r.Landmarks.Any(l=>l==null || l.Anchors==null || l.Roles==null || l.Template==null) || r.Landmarks.Count>r.Tiles.Length/121 || r.Landmarks.Select(l=>(l.X,l.Y)).Distinct().Count()!=r.Landmarks.Count)throw new InvalidDataException("Invalid landmark room count/identity");
        if(r.Rooms==null || r.Passages==null || r.Rooms.Any(c=>c==null || c.Rects==null || c.Rects.Count==0 || c.Kind is not ("room" or "hall") || c.Rects.Any(q=>q==null || q.Length!=4 || q[0]<1 || q[1]<1 || q[2]<1 || q[3]<1 || q[0]+q[2]>=r.Width || q[1]+q[3]>=r.Height)) || r.Passages.Any(p=>p==null || p.X<1 || p.Y<1 || p.X+2>=r.Width || p.Y+2>=r.Height || p.Dx+p.Dy!=1 || p.Dx*p.Dy!=0))
            throw new InvalidDataException("Invalid saved dungeon rooms/passages");
        foreach(var room in r.Landmarks)
        {
            if(room.X<0 || room.Y<0 || room.OriginX<0 || room.OriginY<0 || room.OriginX+10>=r.Width || room.OriginY+10>=r.Height || room.Revision<1 || room.FocalX is <1 or >9 || room.FocalY is <1 or >9 || room.Rotation is <0 or >3 || !Themes.Contains(room.Theme) || room.Anchors.Count>24 || room.Doors is <1 or >15 || room.Template.Length==0 || room.Roles.Length==0 || room.Roles.Any(role=>!Roles.Contains(role)))throw new InvalidDataException("Invalid saved landmark "+room.Template);
            foreach(var a in room.Anchors)
            {
                if(a==null)throw new InvalidDataException("Null saved landmark anchor");
                if(a.X is <1 or >9 || a.Y is <1 or >9 || !Assets.Contains(a.Asset) || !Categories.Contains(a.Category) || a.Collision is not ("terrain" or "cosmetic") || a.Priority is <0 or >4 || a.Facing is <0 or >3 || a.Variant is <0 or >2)throw new InvalidDataException("Invalid saved landmark anchor "+room.Template);
                bool structuralAsset=new[]{"column","shelf","altar","storage","collapse"}.Contains(a.Asset);
                if((a.Collision=="terrain")!=structuralAsset || (a.Collision=="terrain" && a.Category!="tall") || (a.Collision=="cosmetic" && a.Category is not ("ground" or "low")))throw new InvalidDataException("Saved landmark asset/collision mismatch "+room.Template);
                int at=(room.OriginY+a.Y)*r.Width+room.OriginX+a.X;
                if(a.Collision=="terrain" && r.Tiles[at]!=Terrain.Wall)throw new InvalidDataException("Saved landmark/terrain disagreement "+room.Template);
            }
        }
    }
}
