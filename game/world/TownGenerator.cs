namespace Mosslight;

/// <summary>Saved town composition. Terrain remains the movement and sight authority.</summary>
public sealed class TownState
{
    public int Revision { get; set; } = 1;
    public List<TownHouse> Houses { get; set; } = new();
    public List<TownPiece> Pieces { get; set; } = new();
    public List<int> Paving { get; set; } = new();
    TownPlan? plan;
    public TownPlan Plan(int width)
    {
        if (plan != null) return plan;
        plan = new TownPlan();
        plan.Houses.AddRange(Houses); plan.Pieces.AddRange(Pieces);
        foreach (int at in Paving) plan.Pave(at % width, at / width, 1, 1);
        return plan;
    }
    public static TownState From(TownPlan plan, int width) => new()
    {
        Houses = new(plan.Houses), Pieces = new(plan.Pieces),
        Paving = plan.Paving.Select(p => p.Y * width + p.X).Order().ToList()
    };

    public static void Validate(RegionState r)
    {
        var t = r.Town;
        if (t == null) return; // Older worlds keep their original geography.
        void Require(bool valid) { if (!valid) throw new InvalidDataException("Invalid saved town in " + r.Id); }
        Require(r.Id != "ruin" && t.Revision is 1 or 2 && t.Houses != null && t.Pieces != null && t.Paving != null);
        Require(t.Houses!.Count is > 0 and <= 64 && t.Pieces!.Count <= 2048 && t.Paving!.Count <= r.Tiles.Length);
        var occupied = new HashSet<int>();
        foreach (var h in t.Houses)
        {
            Require(Enum.IsDefined(h.Style) && h.Width >= 5 && h.Height >= 5 && h.Width <= r.Width && h.Height <= r.Height &&
                h.X >= 1 && h.Y >= 3 && h.X <= r.Width - h.Width - 1 && h.Y <= r.Height - h.Height - 2 &&
                h.DoorWidth is >= 1 and <= 3 && h.Door >= 1 && h.Door <= h.Width - h.DoorWidth - 1);
            for (int y = h.Y; y < h.Y + h.Height; y++) for (int x = h.X; x < h.X + h.Width; x++)
            {
                int at = y * r.Width + x;
                Require(occupied.Add(at) && !r.Features.ContainsKey(at) && at != r.Entrance && at != r.Objective &&
                    r.Tiles[at] == (h.Ring(x,y) && !h.Doorway(x,y) ? Terrain.Wall : Terrain.Floor));
            }
        }
        foreach (int at in t.Paving!) Require(at >= 0 && at < r.Tiles.Length && r.Tiles[at] is Terrain.Grass or Terrain.Earth or Terrain.Floor);
        foreach (var p in t.Pieces!)
        {
            Require(Enum.IsDefined((TownProp)p.Kind) && p.X > 0 && p.Y > 0 && p.X < r.Width - 1 && p.Y < r.Height - 1);
            Require(p.Variant >= 0 && p.Variant < TownArt.Count((TownProp)p.Kind) &&
                r.Tiles[p.Y * r.Width + p.X] is Terrain.Grass or Terrain.Earth or Terrain.Floor);
        }
    }
}

/// <summary>Seeded roadside settlement. Layout RNG is private; generation never stages
/// presentation or draws from the world's combat, loot or creature streams.</summary>
public static class TownGenerator
{
    // Frozen town-revision-1 seed mixer: art revisions must never move real walls.
    static int LayoutSeed(int seed)
    {
        unchecked
        {
            uint Mix(uint n) { n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; return n ^ (n >> 16); }
            uint h=Mix((uint)seed ^ 0x6d6f7373u ^ 0x9e3779b9u);
            return (int)Mix(Mix(Mix(h)) ^ 0x546Fu);
        }
    }

    public static TownPlan? Generate(RegionState r)
    {
        // Keep a real wilderness and the portal east of the town. Small custom maps
        // retain the old roadside clearing rather than squeezing in broken houses.
        if (r.Id != "reach" || r.Width < 48 || r.Height < 44) return null;
        if(r.Width>=128 && r.Height>=128)return GenerousTown(r);
        var rng = new Random(LayoutSeed(r.Seed));
        int mid = r.Height / 2;
        var plan = new TownPlan();
        var styles = new[] { HouseStyle.Chapel, HouseStyle.Inn, HouseStyle.Cottage,
            HouseStyle.Stone, HouseStyle.Smithy, HouseStyle.Barn };
        // Swap the side plots, retaining the inn overlooking the market square.
        if (rng.Next(2) == 0) (styles[0], styles[2]) = (styles[2], styles[0]);
        if (rng.Next(2) == 0) (styles[3], styles[5]) = (styles[5], styles[3]);
        for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
        {
            int width = rng.Next(7,10), height = rng.Next(6,9);
            int x = 4 + col * 11 + rng.Next(2);
            int y = row == 0 ? mid - 7 - height - rng.Next(2) : mid + 7 + rng.Next(2);
            int doorWidth = styles[row * 3 + col] is HouseStyle.Smithy or HouseStyle.Barn ? 2 : 1;
            int door = rng.Next(2, width - doorWidth - 1);
            plan.Build(styles[row * 3 + col],x,y,width,height,door,doorWidth);
        }
        // The central street keeps the original spawn, chest and resource stands.
        // A southern lane returns round the outer plots; every door meets a lane.
        plan.Pave(2,mid-2,37,5).Pave(2,mid-6,37,3).Pave(2,mid+17,37,3)
            .Pave(2,mid-6,2,26).Pave(37,mid-6,2,26);
        foreach (var h in plan.Houses)
        {
            int front = h.Y + h.Height;
            int lane = h.Y < mid ? mid - 5 : mid + 18;
            plan.Pave(h.X+h.Door-1,front,h.DoorWidth+2,lane-front+1);
            Furnish(plan,h,rng);
        }
        // A little market on the north side; the centre of the street stays clear.
        plan.Add(TownProp.Well,20,mid-4).Add(TownProp.Stall,10,mid-4,rng.Next(3))
            .Add(TownProp.Stall,29,mid-4,rng.Next(3)).Add(TownProp.NoticeBoard,5,mid-3)
            .Add(TownProp.Signpost,37,mid-1).Add(TownProp.Bench,23,mid-4)
            .Add(TownProp.LampPost,3,mid-3).Add(TownProp.LampPost,36,mid-3)
            .Add(TownProp.LampPost,3,mid+17).Add(TownProp.Cart,35,mid+17)
            .Add(TownProp.Garden,27,mid+18).Add(TownProp.Garden,28,mid+18)
            .Add(TownProp.Scarecrow,29,mid+18);
        // Reject the complete layout before touching terrain if a custom feature
        // conflicts. Standard overworld feature positions all lie in the street.
        bool Protected(int x,int y) => r.Features.ContainsKey(y*r.Width+x) || y*r.Width+x == r.Entrance || y*r.Width+x == r.Objective;
        if (plan.Houses.Any(h => Enumerable.Range(h.Y,h.Height).Any(y => Enumerable.Range(h.X,h.Width).Any(x => Protected(x,y))))) return null;
        plan.Pieces.RemoveAll(p => Protected(p.X,p.Y));
        return plan;
    }

    public static void Install(RegionState r)
    {
        if (r.Town != null) return;
        var plan = Generate(r);
        if (plan == null) return;
        // Clear only the plots and their verges, leaving the surrounding terrain.
        foreach (var h in plan.Houses)
            for (int y=h.Y-2;y<h.Y+h.Height+2;y++) for (int x=h.X-1;x<=h.X+h.Width;x++)
                r.Tiles[y*r.Width+x]=Terrain.Grass;
        foreach (var p in plan.Paving) r.Tiles[p.Y*r.Width+p.X]=Terrain.Earth;
        foreach (var p in plan.Pieces) if (plan.HouseAt(p.X,p.Y)==null) r.Tiles[p.Y*r.Width+p.X]=Terrain.Earth;
        plan.Carve(r);
        r.Town = TownState.From(plan,r.Width);
        if(r.Width>=128 && r.Height>=128)r.Town.Revision=2;
    }

    static TownPlan? GenerousTown(RegionState r)
    {
        var rng=new Random(LayoutSeed(r.Seed));var p=new TownPlan();int mid=r.Height/2;
        var styles=new[]{HouseStyle.Cottage,HouseStyle.Inn,HouseStyle.Chapel,HouseStyle.Stone,HouseStyle.Smithy,HouseStyle.Barn};
        if(rng.Next(2)==0)(styles[0],styles[2])=(styles[2],styles[0]);
        for(int row=0;row<2;row++)for(int col=0;col<3;col++)
        {
            var style=styles[row*3+col];
            var (minW,minH)=style switch {HouseStyle.Cottage=>(10,9),HouseStyle.Stone=>(11,10),HouseStyle.Inn=>(17,12),HouseStyle.Smithy=>(13,10),HouseStyle.Chapel=>(15,14),_=>(16,13)};
            int w=minW+rng.Next(3),h=minH+rng.Next(3),dw=style==HouseStyle.Barn?3:2;
            int x=4+col*24+rng.Next(3),y=row==0?mid-8-h:mid+9+rng.Next(2);
            p.Build(style,x,y,w,h,(w-dw)/2,dw);
        }
        p.Pave(2,mid-2,74,5).Pave(2,mid-6,74,3).Pave(2,mid+29,74,3)
            .Pave(2,mid-6,2,38).Pave(74,mid-6,2,38);
        foreach(var h in p.Houses)
        {
            int front=h.Y+h.Height,lane=h.Y<mid?mid-5:mid+30;
            p.Pave(h.X+h.Door-1,front,h.DoorWidth+2,lane-front+1);
            SpaciousInterior(p,h,rng);
        }
        p.Add(TownProp.Well,26,mid-4).Add(TownProp.Stall,18,mid-4,rng.Next(3))
            .Add(TownProp.Stall,38,mid-4,rng.Next(3)).Add(TownProp.Bench,30,mid-4)
            .Add(TownProp.NoticeBoard,5,mid-3).Add(TownProp.Signpost,73,mid-1)
            .Add(TownProp.LampPost,3,mid-3).Add(TownProp.LampPost,72,mid-3)
            .Add(TownProp.Cart,70,mid+29).Add(TownProp.Garden,24,mid+30);
        bool Protected(int x,int y)=>r.Features.ContainsKey(y*r.Width+x)||y*r.Width+x==r.Entrance||y*r.Width+x==r.Objective;
        if(p.Houses.Any(h=>Enumerable.Range(h.Y,h.Height).Any(y=>Enumerable.Range(h.X,h.Width).Any(x=>Protected(x,y)))))return null;
        p.Pieces.RemoveAll(a=>Protected(a.X,a.Y));return p;
    }

    static void SpaciousInterior(TownPlan p,TownHouse h,Random rng)
    {
        int l=h.X+2,r=h.X+h.Width-3,b=h.Y+2,f=h.Y+h.Height-3,c=h.X+h.Door;
        // Keep a three-cell central aisle and the entire entrance apron empty.
        switch(h.Style)
        {
            case HouseStyle.Inn:
                p.Add(TownProp.Counter,l+1,b+1).Add(TownProp.Shelf,l,b).Add(TownProp.Hearth,r,b);
                foreach(int y in new[]{b+4,f-1}){p.Add(TownProp.Table,l+1,y).Add(TownProp.Table,r-1,y);}
                break;
            case HouseStyle.Chapel:
                p.Add(TownProp.Statue,c,b);
                for(int y=b+4;y<=f-1;y+=3)p.Add(TownProp.Pew,l+1,y).Add(TownProp.Pew,r-1,y);
                break;
            case HouseStyle.Smithy:
                p.Add(TownProp.Hearth,l,b).Add(TownProp.Anvil,l,b+3).Add(TownProp.Barrels,r,b).Add(TownProp.Crates,r,f);break;
            case HouseStyle.Barn:
                for(int y=b;y<f;y+=3)p.Add(TownProp.Hay,l,y,y%2).Add(TownProp.Sacks,r,y);
                p.Add(TownProp.Crates,l+2,b);break;
            default:
                p.Add(TownProp.Bed,l,b,rng.Next(2)).Add(TownProp.Hearth,r,b).Add(TownProp.Table,r-1,b+4).Add(TownProp.Shelf,l,f);break;
        }
        p.Add(TownProp.Rug,c,f+1,rng.Next(2)).Add(TownProp.Planter,h.X,h.Y+h.Height+1,rng.Next(2));
        if(h.Style is HouseStyle.Inn or HouseStyle.Smithy)p.Add(TownProp.TradeSign,r,h.Y+h.Height,h.Style==HouseStyle.Inn?0:1);
    }

    static void Furnish(TownPlan p, TownHouse h, Random rng)
    {
        int left=h.X+1,right=h.X+h.Width-2,back=h.Y+1,front=h.Y+h.Height-2;
        switch(h.Style)
        {
            case HouseStyle.Inn:
                p.Add(TownProp.Counter,left,back+1).Add(TownProp.Shelf,left,back)
                    .Add(TownProp.Hearth,right,back).Add(TownProp.Table,left+1,front-1).Add(TownProp.Table,right,front-1);
                break;
            case HouseStyle.Chapel:
                p.Add(TownProp.Statue,left+2,back).Add(TownProp.Pew,left,front-1).Add(TownProp.Pew,right,front-1);
                break;
            case HouseStyle.Smithy:
                p.Add(TownProp.Hearth,left,back).Add(TownProp.Anvil,left+2,back+2).Add(TownProp.Barrels,right,back).Add(TownProp.Crates,right,front-1);
                break;
            case HouseStyle.Barn:
                p.Add(TownProp.Hay,left,back,1).Add(TownProp.Hay,left+2,back).Add(TownProp.Sacks,right,back).Add(TownProp.Crates,left,front-1);
                break;
            default:
                p.Add(TownProp.Bed,left,back,rng.Next(2)).Add(TownProp.Hearth,right,back).Add(TownProp.Table,right,front-1).Add(TownProp.Shelf,left,front-1);
                break;
        }
        // Keep the doorway and the aisle immediately inside visibly empty.
        p.Add(TownProp.Rug,h.X+h.Door,front,rng.Next(2));
        p.Add(TownProp.Planter,h.X, h.Y+h.Height+1,rng.Next(2));
        if(h.Style is HouseStyle.Inn or HouseStyle.Smithy)
            p.Add(TownProp.TradeSign,right,h.Y+h.Height,(h.Style==HouseStyle.Inn ? 0 : 1));
    }
}
