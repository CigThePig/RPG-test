using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
namespace Mosslight;

public enum TownBuilding { Cottage, StoneHouse, Inn, Smithy, Chapel, Barn, Shop, Watchtower }
public enum TownProp
{
    Well, Stall, LampPost, Signpost, NoticeBoard, FenceRun, FenceSide, Gate, StoneWall, Cart, Hay, Bench,
    Barrels, Crates, Sacks, Planter, Woodpile, Fountain, Statue, Washing, Scarecrow, Garden, BannerPole
}

/// <summary>Fixed, procedurally painted town modules in the Living Reach's manner and palette:
/// buildings in a 192x168 cell, street furniture in the standard 72x104 cell. Lit from the
/// upper-left, one dark contour, a cast shadow. Pure cosmetic recipes; no world RNG,
/// simulation state or external textures.</summary>
public static class TownArt
{
    public const int Revision=1,BuildingWidth=192,BuildingHeight=168,BuildingColumns=4,PropColumns=8;
    public const int Width=OverworldPropArt.Width,Height=OverworldPropArt.Height;
    public static readonly Vector2 BuildingPivot=new(96,160),Pivot=OverworldPropArt.Pivot,FlatPivot=new(36,94);
    static readonly int[] BuildingCounts={3,2,1,1,1,1,1,1};
    static readonly int[] PropCounts={1,3,1,1,1,1,1,1,1,1,2,1, 1,1,1,2,1,1,1,1,1,2,1};
    static readonly int[] BuildingOffsets=BuildingCounts.Select((_,i)=>BuildingCounts.Take(i).Sum()).ToArray();
    static readonly int[] PropOffsets=PropCounts.Select((_,i)=>PropCounts.Take(i).Sum()).ToArray();
    public static readonly int Buildings=BuildingCounts.Sum(),Props=PropCounts.Sum();
    public static readonly int BuildingRows=(Buildings+BuildingColumns-1)/BuildingColumns,PropRows=(Props+PropColumns-1)/PropColumns;
    public static int Count(TownBuilding b)=>BuildingCounts[(int)b];
    public static int Count(TownProp p)=>PropCounts[(int)p];
    public static int Module(TownBuilding b,int variant=0)=>BuildingOffsets[(int)b]+(int)((uint)variant%(uint)BuildingCounts[(int)b]);
    public static int Module(TownProp p,int variant=0)=>PropOffsets[(int)p]+(int)((uint)variant%(uint)PropCounts[(int)p]);
    public static Rect2 BuildingSource(int module)=>new(module%BuildingColumns*BuildingWidth,module/BuildingColumns*BuildingHeight,BuildingWidth,BuildingHeight);
    public static Rect2 PropSource(int module)=>new(module%PropColumns*Width,module/PropColumns*Height,Width,Height);
    public static bool Flat(TownProp p)=>p==TownProp.Garden;
    /// <summary>Tiles a building stands on: width across its front, depth back from it.</summary>
    public static (int Width,int Depth) Footprint(TownBuilding b)=>b switch
    {
        TownBuilding.Cottage=>(4,3),TownBuilding.StoneHouse=>(4,3),TownBuilding.Inn=>(6,4),TownBuilding.Smithy=>(5,3),
        TownBuilding.Chapel=>(3,4),TownBuilding.Barn=>(4,4),TownBuilding.Shop=>(4,3),_=>(2,2)
    };

    const byte Ink=37,Shadow=35,Penumbra=36;
    const int Base=160,Mid=96;
    // Four-tone roof ramps, darkest first.
    static readonly byte[] Tile={72,73,74,75},Slate={76,77,78,79},Thatch={80,81,82,83},
        Stone={19,20,21,22,23,24},Leaf8={1,2,2,3,3,4,5,6},Stone8={19,20,20,21,21,22,23,24},Straw8={80,80,81,81,82,82,83,83};
    static uint Hash(int x,int y,int slot)=>EnvironmentStyle.Hash(Revision,x,y,21000+slot);

    public static byte[] Generate(TownBuilding building,int variant)
    {
        var p=new Pixels(BuildingWidth,BuildingHeight);
        switch(building)
        {
            case TownBuilding.Cottage: Cottage(p,variant%3);break;
            case TownBuilding.StoneHouse: StoneHouse(p,variant%2);break;
            case TownBuilding.Inn: Inn(p);break;
            case TownBuilding.Smithy: Smithy(p);break;
            case TownBuilding.Chapel: Chapel(p);break;
            case TownBuilding.Barn: Barn(p);break;
            case TownBuilding.Shop: Shop(p);break;
            case TownBuilding.Watchtower: Watchtower(p);break;
        }
        p.Contour(false);return p.Data;
    }

    // ---- building parts -------------------------------------------------------------
    static void GroundShadow(Pixels p,int x,int w,int rise)
    {
        // Thrown down-right: a strip under the sill and a wedge beside the east wall.
        p.Rect(x+3,Base,w+6,3,Penumbra);p.Rect(x+5,Base,w,2,Shadow);
        p.Poly(Penumbra,x+w,Base+2,x+w+13,Base-3,x+w+13,Base-rise,x+w,Base-rise+7);
    }
    static void Footing(Pixels p,int x,int w)
    {
        p.Rect(x,Base-5,w,5,21);p.Rect(x,Base-5,w,1,23);p.Rect(x,Base-1,w,1,19);
        for(int i=x+(int)(Hash(x,0,1)%5);i<x+w;i+=9)p.Rect(i,Base-4,1,3,20);
    }
    static void Timber(Pixels p,int x,int y,int w,int h,int slot,bool footing=true)
    {
        // Limewashed panels in an oak frame.
        p.Rect(x,y,w,h,85);p.Rect(x,y,w,4,84);p.Rect(x+2,y+4,2,h-4,86);p.Rect(x+w-5,y,5,h,84);
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)if(Hash(xx/2,yy/2,slot)%23==0)p.Set(xx,yy,84);
        p.Rect(x,y,w,2,13);p.Rect(x,y+h-2,w,2,13);p.Rect(x,y,2,h,14);p.Rect(x+w-2,y,2,h,12);
        p.Rect(x,y+h/2-1,w,2,13);p.Rect(x,y+h/2-1,w,1,14);
        for(int stud=x+14;stud<x+w-8;stud+=15){p.Rect(stud,y,2,h,13);p.Rect(stud,y,1,h,14);}
        p.Line(x+2,y+h/2-2,x+12,y+2,1,13);p.Line(x+w-3,y+h/2-2,x+w-13,y+2,1,12);
        if(footing)Footing(p,x,w);
    }
    static void Masonry(Pixels p,int x,int y,int w,int h,int slot)
    {
        p.Rect(x,y,w,h,21);
        for(int yy=y;yy<y+h;yy++)
        {
            int course=(yy-y)/6,cy=(yy-y)%6;
            for(int xx=x;xx<x+w;xx++)
            {
                int bx=xx-x+(course%2==0?0:7),block=bx/14,l=bx%14;uint n=Hash(block,course,slot);
                byte c=n%5==0?(byte)22:n%7==0?(byte)20:(byte)21;
                if(cy==0)c=c==20?(byte)21:c==21?(byte)22:(byte)23;
                if(cy==5||l==13)c=19;
                if(Hash(xx/2,yy,slot+1)%29==0&&c!=19)c=20;
                p.Set(xx,yy,c);
            }
        }
        p.Over(x,y,w,3,20,21,22,23);p.Rect(x,y+3,2,h-3,23);p.Over(x+w-4,y,4,h,20,21,22);p.Rect(x,y+h-1,w,1,19);
        // Quoins at both corners.
        for(int q=y+2;q<y+h-4;q+=10){p.Rect(x,q,6,5,23);p.Rect(x,q+4,6,1,20);p.Rect(x+w-6,q+5,6,5,21);p.Rect(x+w-6,q+9,6,1,19);}
    }
    static void Planks(Pixels p,int x,int y,int w,int h,int slot)
    {
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)
        {
            int board=(xx-x)/6;uint n=Hash(board,yy/9,slot);
            byte c=(xx-x)%6==5?(byte)12:n%4==0?(byte)15:(byte)14;
            if(yy-y<4)c=c==12?(byte)12:(byte)13;
            p.Set(xx,yy,c);
        }
        p.Rect(x,y+3,2,h-3,16);p.Rect(x+w-3,y,3,h,13);p.Rect(x,y+h-2,w,2,13);
    }
    /// <summary>Roof with its ridge across the screen: a trapezoid of courses, lit on the
    /// left, shaded down the right hip, overhanging the wall at the eaves.</summary>
    static void Roof(Pixels p,int x,int y,int w,int h,byte[] r,int slant,int slot)
    {
        bool thatch=r==Thatch;
        for(int row=0;row<h;row++)
        {
            float t=row/(float)(h-1);int inset=(int)MathF.Round(slant*(1-t))-3;
            for(int xx=x+inset;xx<x+w-inset;xx++)
            {
                int hem=thatch?(int)(Hash(xx/2,0,slot)%3):0;
                if(row>=h-hem)continue;
                float across=(xx-x-inset)/(float)(w-inset*2);
                byte c=r[2];
                if(thatch)
                {
                    uint n=Hash(xx,row/3,slot+1);
                    c=n%5==0?r[1]:n%7==0?r[3]:r[2];
                    if(row%7==6)c=r[1];
                }
                else
                {
                    int course=row/5,l=(xx+(course%2)*4)%8;
                    if(row%5==0)c=r[3];
                    if(row%5==4)c=r[0];
                    else if(l==7)c=r[1];
                    else if(Hash((xx+(course%2)*4)/8,course,slot)%6==0)c=row%5==0?r[2]:r[1];
                }
                if(across<.06f&&c!=r[0])c=r[3];
                if(across>.88f)c=c==r[3]?r[2]:c==r[2]?r[1]:r[0];
                if(row<2)c=row==0?r[3]:r[1];
                if(row>=h-2-hem&&!thatch)c=r[0];
                p.Set(xx,y+row,c);
            }
        }
        if(thatch)for(int xx=x+slant-2;xx<x+w-slant+2;xx+=5){p.Rect(xx,y-1,3,3,r[3]);p.Set(xx+1,y+1,r[1]);}
    }
    /// <summary>A gable end facing the viewer: the wall rises to a point between two roof edges.</summary>
    static void Gable(Pixels p,int half,int wallTop,int rise,byte wall,byte wallShade,byte[] r)
    {
        for(int row=0;row<rise;row++)
        {
            int reach=(int)MathF.Round(half*(row+1)/(float)rise);
            for(int xx=Mid-reach;xx<Mid+reach;xx++)p.Set(xx,wallTop-rise+row,xx>Mid+reach-5?wallShade:wall);
        }
        // Barge boards and the roof edge beyond them.
        for(int row=-4;row<rise+4;row++)
        {
            int reach=(int)MathF.Round(half*(row+1)/(float)rise);
            for(int k=0;k<7;k++)
            {
                int y=wallTop-rise+row;
                if(reach+k<0)continue;
                p.Set(Mid-reach-k-1,y,k<2?r[1]:k==6?r[0]:k%2==0?r[3]:r[2]);
                p.Set(Mid+reach+k,y,k<2?r[0]:k==6?r[0]:r[1]);
            }
        }
        p.Rect(Mid-3,wallTop-rise-6,6,4,r[3]);p.Rect(Mid,wallTop-rise-6,3,4,r[1]);
    }
    static void Door(Pixels p,int x,int w,int h,byte dark,byte mid,byte lit,bool arched=false)
    {
        int y=Base-h;
        p.Rect(x-2,y-2,w+4,h+2,13);p.Rect(x-2,y-2,w+4,1,15);p.Rect(x,y,w,h,mid);
        for(int board=x+3;board<x+w;board+=4)p.Rect(board,y,1,h,dark);
        p.Rect(x,y,1,h,lit);p.Rect(x,y,w,2,dark);p.Rect(x,y+h/3,w,1,41);p.Rect(x,y+h*2/3,w,1,41);
        p.Rect(x+w-3,y+h/2,2,2,29);p.Set(x+w-3,y+h/2,30);
        if(arched){p.Rect(x-2,y-2,3,3,13);p.Rect(x+w-1,y-2,3,3,13);p.Set(x,y,13);p.Set(x+w-1,y,13);p.Rect(x+1,y-4,w-2,2,13);p.Rect(x+2,y-3,w-4,2,mid);}
        p.Rect(x-3,Base-2,w+6,3,22);p.Rect(x-3,Base-2,w+6,1,24);
    }
    static void Window(Pixels p,int x,int y,int w,int h,bool lit,bool shutters=false,bool box=false)
    {
        p.Rect(x-1,y-1,w+2,h+2,13);p.Rect(x-1,y-1,w+2,1,15);
        p.Rect(x,y,w,h,lit?(byte)93:(byte)94);
        if(lit){p.Rect(x,y,w,2,47);p.Rect(x,y+h-2,w,2,45);}else{p.Line(x+1,y+h-2,x+3,y+1,1,63);p.Set(x+w-2,y+1,87);}
        p.Rect(x+w/2,y,1,h,13);p.Rect(x,y+h/2,w,1,13);
        p.Rect(x-2,y+h+1,w+4,2,15);p.Rect(x-2,y+h+2,w+4,1,13);
        if(shutters){p.Rect(x-6,y-1,4,h+2,91);p.Rect(x-6,y-1,1,h+2,92);p.Rect(x-6,y+h/2,4,1,90);p.Rect(x+w+2,y-1,4,h+2,90);p.Rect(x+w+2,y-1,1,h+2,91);}
        if(box)
        {
            p.Rect(x-2,y+h+1,w+4,4,14);p.Rect(x-2,y+h+1,w+4,1,3);
            for(int f=x-1;f<x+w+2;f+=3){p.Set(f,y+h,4);p.Set(f+1,y+h-1,(f/3)%2==0?(byte)33:(byte)32);p.Set(f+1,y+h,5);}
        }
    }
    static void Chimney(Pixels p,int x,int top,int w,int bottom)
    {
        p.Rect(x,top,w,bottom-top,21);p.Rect(x,top,2,bottom-top,23);p.Rect(x+w-3,top,3,bottom-top,20);
        for(int y=top+5;y<bottom;y+=5)p.Rect(x,y,w,1,20);
        p.Rect(x-1,top-2,w+2,3,22);p.Rect(x-1,top-2,w+2,1,24);p.Rect(x+2,top-4,w-4,2,19);
    }
    static void HangingSign(Pixels p,int x,int y,byte field,int emblem)
    {
        p.Rect(x-2,y-6,14,2,41);p.Rect(x-2,y-6,14,1,42);p.Rect(x-2,y-6,2,9,41);p.Rect(x+2,y-4,1,3,41);p.Rect(x+9,y-4,1,3,41);
        p.Rect(x,y-1,12,11,14);p.Rect(x,y-1,12,1,16);p.Rect(x+1,y,10,9,field);p.Rect(x+1,y,10,1,86);
        if(emblem==0){p.Rect(x+3,y+3,5,5,29);p.Rect(x+3,y+3,5,1,31);p.Rect(x+8,y+4,2,3,29);p.Set(x+9,y+5,field);}   // tankard
        if(emblem==1){p.Rect(x+3,y+2,1,6,43);p.Rect(x+2,y+2,3,2,42);p.Rect(x+6,y+5,4,2,43);p.Rect(x+7,y+7,2,1,42);} // hammer and anvil
        if(emblem==2){p.Oval(x+6,y+5,3,3,29);p.Set(x+5,y+4,30);p.Rect(x+5,y+1,2,1,3);}                               // goods
    }
    static void Shrub(Pixels p,int x,int y,int rx,int slot,bool flowers)
    {
        p.Blob(x,y-rx*.6f,rx,rx*.7f,Leaf8,slot,-.1f);
        if(flowers)for(int i=0;i<4;i++){int fx=x-rx+2+(int)(Hash(i,slot,3)%(uint)(rx*2-3)),fy=y-2-(int)(Hash(i,slot,4)%(uint)rx);p.Set(fx,fy,i%2==0?(byte)32:(byte)31);}
    }

    // ---- buildings ------------------------------------------------------------------
    static void Cottage(Pixels p,int v)
    {
        int x=54,w=84,wallTop=Base-30;bool flip=v==1;
        GroundShadow(p,x,w,30);
        if(v==1)Planks(p,x,wallTop,w,30,11);else Timber(p,x,wallTop,w,30,12);
        if(v==1)Footing(p,x,w);
        Chimney(p,flip?x+12:x+w-24,wallTop-42,10,wallTop-8);
        Roof(p,x-4,wallTop-36,w+8,38,v==2?Tile:Thatch,16,13+v);
        int door=flip?x+52:x+18;
        Door(p,door,13,22,13,v==2?(byte)91:(byte)14,v==2?(byte)92:(byte)15);
        Window(p,flip?x+16:x+48,wallTop+8,12,10,v!=1,v==2,v!=1);
        if(v!=1)Window(p,flip?x+34:x+68,wallTop+8,9,10,false);
        Shrub(p,flip?x+78:x+6,Base+1,7,31+v,v==0);
        if(v==0){p.Rect(x+w-10,Base-9,8,9,14);p.Rect(x+w-10,Base-9,8,1,16);p.Rect(x+w-10,Base-6,8,1,42);p.Rect(x+w-10,Base-2,8,1,42);}
        if(v==2){p.Rect(door+16,Base-5,7,5,67);p.Rect(door+16,Base-5,1,5,68);p.Blob(door+19,Base-9,4,4,Leaf8,41);p.Set(door+18,Base-11,33);p.Set(door+21,Base-9,32);}
    }
    static void StoneHouse(Pixels p,int v)
    {
        int x=48,w=96,wallTop=Base-36;
        GroundShadow(p,x,w,36);
        Masonry(p,x,wallTop,w,36,21+v);
        Chimney(p,v==0?x+70:x+14,wallTop-50,12,wallTop-10);
        Roof(p,x-4,wallTop-42,w+8,44,v==0?Tile:Slate,20,23+v);
        // Dormer breaking the eaves.
        int dormer=v==0?x+24:x+58;
        p.Rect(dormer,wallTop-22,18,16,85);p.Rect(dormer,wallTop-22,18,2,84);p.Rect(dormer+14,wallTop-22,4,16,84);
        for(int k=0;k<8;k++){p.Rect(dormer-2+k,wallTop-23-k,22-k*2,1,(v==0?Tile:Slate)[k%2==0?2:1]);}
        p.Rect(dormer-3,wallTop-22,24,1,(v==0?Tile:Slate)[0]);
        Window(p,dormer+5,wallTop-18,8,8,v==0);
        Door(p,x+42,14,25,v==0?(byte)87:(byte)13,v==0?(byte)88:(byte)14,v==0?(byte)89:(byte)15);
        Window(p,x+14,wallTop+10,13,12,true,true);Window(p,x+70,wallTop+10,13,12,v==1,true,v==0);
        Shrub(p,x+w-7,Base+1,8,51+v,v==1);
        p.Rect(x+34,Base-14,3,2,41);p.Rect(x+34,Base-12,4,5,93);p.Rect(x+34,Base-12,4,1,47);p.Rect(x+34,Base-7,4,1,41);
    }
    static void Inn(Pixels p)
    {
        int x=30,w=132,ground=30,upper=28,lowerTop=Base-ground,upperTop=lowerTop-upper;
        GroundShadow(p,x,w,58);
        Masonry(p,x,lowerTop,w,ground,61);
        // Jettied upper storey on a row of beam ends.
        Timber(p,x-3,upperTop,w+6,upper,62,false);
        for(int beam=x-1;beam<x+w+3;beam+=8){p.Rect(beam,lowerTop-1,4,3,13);p.Rect(beam,lowerTop-1,4,1,15);}
        Chimney(p,x+16,upperTop-52,12,upperTop-12);Chimney(p,x+w-30,upperTop-46,11,upperTop-10);
        Roof(p,x-8,upperTop-44,w+16,46,Slate,24,63);
        for(int i=0;i<4;i++)Window(p,x+10+i*33,upperTop+9,13,11,i!=1,true,i==0||i==3);
        Door(p,x+56,20,26,12,14,15,true);p.Rect(x+65,Base-26,2,26,12);
        Window(p,x+14,lowerTop+8,22,13,true);Window(p,x+96,lowerTop+8,22,13,true);
        // Lanterns either side of the door and the sign of the house.
        foreach(int lx in new[]{x+49,x+79}){p.Rect(lx,lowerTop+5,3,2,41);p.Rect(lx,lowerTop+7,4,6,93);p.Rect(lx,lowerTop+7,4,1,47);p.Rect(lx,lowerTop+13,4,1,41);}
        HangingSign(p,x-14,upperTop+18,61,0);
        // Bench and barrels by the wall.
        p.Rect(x+w-36,Base-8,22,3,15);p.Rect(x+w-36,Base-8,22,1,16);p.Rect(x+w-34,Base-5,2,5,13);p.Rect(x+w-18,Base-5,2,5,13);
        p.Rect(x+4,Base-12,9,12,14);p.Rect(x+4,Base-12,2,12,16);p.Rect(x+4,Base-9,9,1,42);p.Rect(x+4,Base-3,9,1,42);p.Rect(x+5,Base-13,7,1,15);
    }
    static void Smithy(Pixels p)
    {
        int x=36,w=66,wallTop=Base-34;
        GroundShadow(p,x,w+52,34);
        // Open forge lean-to on posts beside the stone shop.
        int lean=x+w,leanW=52;
        p.Rect(lean,wallTop+8,leanW,26,12);p.Rect(lean+2,wallTop+10,leanW-6,22,19);
        Masonry(p,lean+8,Base-18,26,18,72);p.Rect(lean+12,Base-15,18,9,37);
        p.Rect(lean+13,Base-13,16,7,44);p.Rect(lean+15,Base-12,12,5,45);p.Rect(lean+17,Base-11,8,3,46);p.Rect(lean+19,Base-10,4,1,47);
        p.Rect(lean+16,wallTop+10,10,16,20);p.Rect(lean+16,wallTop+10,2,16,21);
        p.Rect(lean+leanW-5,wallTop+8,4,26,14);p.Rect(lean+leanW-5,wallTop+8,1,26,16);p.Rect(lean+leanW-2,wallTop+8,1,26,12);
        Masonry(p,x,wallTop,w,34,71);
        Chimney(p,lean+14,wallTop-40,14,wallTop+6);p.Set(lean+19,wallTop-46,45);p.Set(lean+22,wallTop-49,46);p.Set(lean+17,wallTop-51,44);
        Roof(p,x-4,wallTop-34,w+8,36,Tile,16,73);
        for(int row=0;row<14;row++){int y=wallTop-4+row;p.Rect(lean-2,y,leanW+6,1,row%4==3?(byte)12:row%4==0?(byte)15:(byte)14);}
        p.Rect(lean-2,wallTop+10,leanW+6,2,12);
        Door(p,x+12,14,24,12,14,15);Window(p,x+40,wallTop+10,13,11,true);
        HangingSign(p,x-13,wallTop+8,20,1);
        // Anvil on its block, quench tub and tools.
        p.Rect(lean+38,Base-6,8,6,14);p.Rect(lean+38,Base-6,8,1,16);p.Rect(lean+35,Base-11,14,3,42);p.Rect(lean+35,Base-11,14,1,43);p.Rect(lean+39,Base-8,6,2,41);p.Rect(lean+33,Base-10,3,1,43);
        p.Rect(lean-10,Base-8,9,8,14);p.Rect(lean-10,Base-8,9,1,88);p.Rect(lean-10,Base-5,9,1,42);
    }
    static void Chapel(Pixels p)
    {
        int half=34,x=Mid-half,wallTop=Base-44;
        GroundShadow(p,x,half*2,44);
        Masonry(p,x,wallTop,half*2,44,81);
        Gable(p,half,wallTop,30,21,20,Slate);
        for(int row=4;row<30;row+=6)p.Over(Mid-half,wallTop-30+row,half*2,1,20,21);
        // Bell-cote on the ridge.
        p.Rect(Mid-7,wallTop-52,14,18,21);p.Rect(Mid-7,wallTop-52,2,18,23);p.Rect(Mid+4,wallTop-52,3,18,20);
        p.Rect(Mid-4,wallTop-48,8,10,37);p.Rect(Mid-2,wallTop-46,4,5,29);p.Rect(Mid-2,wallTop-46,1,5,30);p.Rect(Mid-3,wallTop-41,6,1,28);
        for(int k=0;k<7;k++)p.Rect(Mid-9+k,wallTop-53-k,18-k*2,1,Slate[k%2==0?2:1]);
        p.Rect(Mid-1,wallTop-66,2,7,29);p.Rect(Mid-3,wallTop-64,6,2,29);
        // Rose window, arched door, buttresses.
        p.Oval(Mid,wallTop-12,7,7,13);p.Oval(Mid,wallTop-12,5,5,88);p.Rect(Mid-5,wallTop-12,11,1,13);p.Rect(Mid,wallTop-17,1,11,13);
        p.Set(Mid-3,wallTop-15,89);p.Set(Mid+2,wallTop-10,61);p.Set(Mid+2,wallTop-15,29);p.Set(Mid-3,wallTop-10,91);
        Door(p,Mid-9,18,28,12,14,15,true);
        foreach(int wx in new[]{x+7,x+half*2-15}){p.Rect(wx,wallTop+10,8,18,13);p.Rect(wx+1,wallTop+11,6,16,94);p.Rect(wx+1,wallTop+11,6,3,88);p.Rect(wx+4,wallTop+11,1,16,13);p.Set(wx+2,wallTop+15,89);}
        foreach(int bx in new[]{x-5,x+half*2-1}){p.Rect(bx,wallTop+14,6,30,21);p.Rect(bx,wallTop+14,6,1,23);p.Rect(bx,wallTop+14,1,30,23);p.Rect(bx+4,wallTop+14,2,30,20);p.Rect(bx,Base-1,6,1,19);}
    }
    static void Barn(Pixels p)
    {
        int half=48,x=Mid-half,wallTop=Base-38;
        GroundShadow(p,x,half*2,38);
        Planks(p,x,wallTop,half*2,38,91);Footing(p,x,half*2);
        Gable(p,half,wallTop,36,14,13,Tile);
        for(int board=Mid-half+5;board<Mid+half;board+=6)for(int y=wallTop-36;y<wallTop;y++)if(p.At(board,y)==14)p.Set(board,y,12);
        // Hay loft and great doors.
        p.Rect(Mid-8,wallTop-22,16,14,37);p.Rect(Mid-9,wallTop-23,18,1,16);p.Blob(Mid,wallTop-11,7,4,Straw8,92,0,.08f);p.Rect(Mid-10,wallTop-8,20,2,15);
        p.Rect(Mid-1,wallTop-30,2,6,41);p.Rect(Mid-3,wallTop-25,6,2,42);
        p.Rect(Mid-20,Base-32,40,32,13);
        foreach(int dx in new[]{Mid-19,Mid+1})
        {
            p.Rect(dx,Base-31,18,31,73);p.Rect(dx,Base-31,18,1,75);p.Rect(dx,Base-31,1,31,74);
            for(int board=dx+4;board<dx+18;board+=5)p.Rect(board,Base-30,1,30,72);
            p.Line(dx+1,Base-30,dx+16,Base-2,2,86);p.Line(dx+16,Base-30,dx+1,Base-2,2,85);p.Rect(dx,Base-31,18,2,86);p.Rect(dx,Base-2,18,2,84);
        }
        p.Rect(Mid-2,Base-18,4,3,42);
        p.Blob(x+half*2-4,Base-5,9,6,Straw8,93,0,.08f);p.Line(x+6,Base,x+10,Base-22,1,14);p.Rect(x+8,Base-25,5,1,42);p.Rect(x+8,Base-25,1,5,42);p.Rect(x+12,Base-25,1,5,42);
    }
    static void Shop(Pixels p)
    {
        int x=50,w=92,wallTop=Base-34;
        GroundShadow(p,x,w,34);
        Timber(p,x,wallTop,w,34,101);
        Chimney(p,x+w-22,wallTop-44,10,wallTop-8);
        Roof(p,x-4,wallTop-38,w+8,40,Tile,18,102);
        Door(p,x+10,14,24,90,91,92);
        // Shop front under a striped awning, wares on the sill.
        p.Rect(x+32,wallTop+12,50,16,13);p.Rect(x+34,wallTop+14,46,12,93);p.Rect(x+34,wallTop+14,46,2,47);p.Rect(x+34,wallTop+24,46,2,45);
        for(int m=x+45;m<x+80;m+=12)p.Rect(m,wallTop+14,1,12,13);
        p.Rect(x+30,wallTop+28,54,3,15);p.Rect(x+30,wallTop+30,54,1,13);
        foreach(var (gx,c) in new[]{(x+36,(byte)33),(x+42,(byte)29),(x+50,(byte)67),(x+58,(byte)4),(x+66,(byte)62),(x+74,(byte)31)}){p.Rect(gx,wallTop+25,4,3,c);p.Set(gx,wallTop+25,86);}
        for(int row=0;row<9;row++)for(int xx=x+28-row/2;xx<x+86+row/2;xx++)
        {
            bool stripe=(xx-x)/6%2==0;
            p.Set(xx,wallTop+3+row,row==8?(stripe?(byte)60:(byte)84):stripe?(row<2?(byte)62:(byte)61):(row<2?(byte)86:(byte)85));
        }
        HangingSign(p,x+w+2,wallTop+6,90,2);
        p.Rect(x+w-9,Base-7,8,7,14);p.Rect(x+w-9,Base-7,8,1,16);p.Rect(x+w-8,Base-10,3,3,33);p.Rect(x+w-5,Base-9,3,2,29);
    }
    static void Watchtower(Pixels p)
    {
        int x=Mid-22,w=44,wallTop=Base-104;
        GroundShadow(p,x,w,70);
        Masonry(p,x,wallTop,w,104,111);
        // Corbelled parapet with merlons under a slate cap.
        Masonry(p,x-4,wallTop-12,w+8,14,112);
        p.Rect(x-4,wallTop-18,w+8,6,19);
        for(int m=x-4;m<x+w+4;m+=13){p.Rect(m,wallTop-18,8,6,22);p.Rect(m,wallTop-18,8,1,24);p.Rect(m+6,wallTop-18,2,6,20);}
        for(int k=0;k<20;k++)p.Rect(Mid-20+k,wallTop-19-k,40-k*2,1,Slate[k%4==3?0:k%4==0?3:k<4?1:2]);
        p.Rect(Mid-1,wallTop-52,2,14,41);p.Poly(61,Mid+1,wallTop-52,Mid+15,wallTop-48,Mid+1,wallTop-43);p.Poly(62,Mid+1,wallTop-52,Mid+9,wallTop-50,Mid+1,wallTop-47);
        foreach(var (sy,lit) in new[]{(wallTop+14,true),(wallTop+44,false),(wallTop+72,true)}){p.Rect(Mid-3,sy,6,14,13);p.Rect(Mid-2,sy+1,4,12,lit?(byte)93:(byte)94);if(lit)p.Rect(Mid-2,sy+1,4,2,47);}
        Door(p,Mid-8,16,24,12,14,15,true);
        p.Over(x,Base-30,10,26,3,21,22,20);p.Over(x+2,Base-38,5,10,4,21,22);p.Over(x+w-8,wallTop+30,6,18,2,21,20);
    }

    // ---- street furniture -----------------------------------------------------------
    public static byte[] Generate(TownProp prop,int variant)
    {
        var p=new Pixels();int v=variant;
        switch(prop)
        {
            case TownProp.Well:
                p.Cast(40,100,17,3);
                for(int y=84;y<100;y++)for(int x=22;x<50;x++){int i=x-22;p.Set(x,y,(y-84)%5==4||(i+(y-84)/5*4)%8==7?(byte)19:i<3?(byte)23:i<18?(byte)21:(byte)20);}
                p.Oval(36,84,14,4,22);p.Oval(36,84,11,3,87);p.Oval(35,84,7,2,88);p.Rect(31,83,3,1,89);p.Rect(22,84,28,1,24);
                p.Rect(23,56,3,30,14);p.Rect(23,56,1,30,16);p.Rect(46,56,3,30,13);p.Rect(22,66,28,2,14);p.Rect(22,66,28,1,15);
                for(int k=0;k<10;k++)p.Rect(18+k*2,57-k,36-k*4,1,Tile[k%3==0?3:k%3==1?2:1]);
                p.Rect(18,58,36,1,Tile[0]);p.Rect(35,68,1,9,57);p.Rect(32,77,7,6,14);p.Rect(32,77,7,1,16);p.Rect(32,80,7,1,42);
                break;
            case TownProp.Stall:
            {
                byte dark=v==0?(byte)60:v==1?(byte)87:(byte)90,mid=v==0?(byte)61:v==1?(byte)88:(byte)91,lit=v==0?(byte)62:v==1?(byte)89:(byte)92;
                p.Cast(40,100,22,3);
                p.Rect(15,66,2,34,14);p.Rect(55,66,2,34,13);p.Rect(15,66,1,34,16);
                p.Rect(16,84,40,16,14);p.Rect(16,84,40,2,16);p.Rect(16,98,40,2,13);for(int b=22;b<56;b+=7)p.Rect(b,86,1,12,13);
                for(int row=0;row<13;row++)for(int x=11-row/3;x<61+row/3;x++)
                {
                    bool stripe=(x+40)/7%2==0;
                    p.Set(x,54+row,row==12?(stripe?dark:(byte)84):stripe?(row<3?lit:mid):(row<3?(byte)86:(byte)85));
                }
                for(int x=11;x<61;x+=7)p.Rect(x-4,67,3,2,(x/7)%2==0?mid:(byte)85);
                // Wares differ by trade: produce, cloth, pots.
                if(v==0)foreach(var (x,c) in new[]{(19,(byte)33),(24,(byte)33),(30,(byte)29),(35,(byte)4),(41,(byte)4),(47,(byte)67),(52,(byte)29)}){p.Rect(x,80,4,4,c);p.Set(x,80,86);p.Set(x+3,83,13);}
                if(v==1)foreach(var (x,c) in new[]{(19,(byte)61),(27,(byte)88),(35,(byte)82),(43,(byte)91),(50,(byte)85)}){p.Rect(x,79,6,5,c);p.Rect(x,79,6,1,86);p.Rect(x+5,80,1,4,13);}
                if(v==2)foreach(var (x,h) in new[]{(20,6),(27,8),(34,5),(41,9),(49,6)}){p.Rect(x,84-h,5,h,67);p.Rect(x,84-h,1,h,68);p.Rect(x+1,83-h,3,1,66);}
                break;
            }
            case TownProp.LampPost:
                p.Cast(38,100,6,2);
                p.Rect(35,62,2,38,41);p.Rect(35,62,1,38,42);p.Rect(33,97,6,3,41);p.Rect(33,97,6,1,43);p.Rect(34,60,4,2,42);
                p.Rect(32,50,8,10,41);p.Rect(33,51,6,8,93);p.Rect(33,51,6,2,47);p.Rect(33,57,6,2,45);p.Rect(36,51,1,8,41);
                p.Rect(31,48,10,2,42);p.Rect(33,46,6,2,41);p.Rect(35,44,2,2,43);
                break;
            case TownProp.Signpost:
                p.Cast(38,100,6,2);
                p.Rect(35,64,3,36,14);p.Rect(35,64,1,36,16);p.Rect(34,62,5,2,13);
                p.Poly(15,38,68,54,68,58,72,54,76,38,76);p.Rect(38,68,16,1,16);p.Rect(41,71,10,1,13);p.Rect(41,73,7,1,13);
                p.Poly(14,35,80,19,80,15,84,19,88,35,88);p.Rect(19,80,16,1,15);p.Rect(22,83,9,1,13);
                break;
            case TownProp.NoticeBoard:
                p.Cast(39,100,13,2);
                p.Rect(22,70,3,30,14);p.Rect(47,70,3,30,13);p.Rect(22,70,1,30,16);
                p.Rect(20,66,32,22,13);p.Rect(22,68,28,18,15);p.Rect(22,68,28,1,16);
                for(int k=0;k<5;k++)p.Rect(19+k,65-k,34-k*2,1,Tile[k%2==0?2:1]);
                foreach(var (x,y,w,h) in new[]{(24,70,8,7),(34,71,6,9),(42,70,6,6),(26,79,7,5),(41,78,7,6)}){p.Rect(x,y,w,h,65);p.Rect(x,y,w,1,59);p.Rect(x+1,y+2,w-2,1,57);if(h>6)p.Rect(x+1,y+4,w-3,1,57);p.Set(x+w/2,y,33);}
                break;
            case TownProp.FenceRun:
                // One tile of fence along a row; neighbours join into a run.
                p.Rect(24,91,24,2,15);p.Rect(24,91,24,1,16);p.Rect(24,96,24,2,14);p.Rect(24,97,24,1,13);
                foreach(int x in new[]{25,35,45}){p.Rect(x,86,3,15,14);p.Rect(x,86,1,15,16);p.Rect(x+2,87,1,14,13);p.Set(x+1,85,15);}
                break;
            case TownProp.FenceSide:
                // One tile of fence running away from the viewer.
                for(int y=89;y<=101;y+=6){p.Rect(35,y-12,3,13,14);p.Rect(35,y-12,1,13,16);p.Rect(37,y-11,1,12,13);p.Set(36,y-13,15);}
                p.Rect(36,80,1,20,15);p.Rect(37,84,1,16,13);
                break;
            case TownProp.Gate:
                p.Rect(24,84,3,17,14);p.Rect(24,84,1,17,16);p.Rect(45,84,3,17,13);p.Rect(45,84,1,17,15);p.Set(25,83,16);p.Set(46,83,15);
                p.Rect(27,88,18,2,15);p.Rect(27,97,18,2,14);p.Line(27,97,44,89,1,13);for(int x=30;x<45;x+=5)p.Rect(x,88,2,11,14);p.Rect(43,92,2,2,42);
                break;
            case TownProp.StoneWall:
                for(int y=90;y<101;y++)for(int x=24;x<48;x++){int course=(y-90)/4,l=(x+course*4)%8;p.Set(x,y,(y-90)%4==3||l==7?(byte)19:y==90?(byte)23:Hash(x/8+course,course,5)%3==0?(byte)22:(byte)21);}
                p.Rect(24,88,24,2,23);p.Rect(24,88,24,1,24);p.Over(26,88,6,2,4,23,24);p.Over(39,90,5,3,3,21,22);
                break;
            case TownProp.Cart:
                p.Cast(40,100,22,3);
                p.Rect(18,80,34,10,14);p.Rect(18,80,34,1,16);p.Rect(18,89,34,1,12);for(int x=24;x<52;x+=7)p.Rect(x,81,1,8,13);
                p.Rect(16,76,2,14,14);p.Rect(52,76,2,14,13);p.Line(54,88,64,94,2,14);
                p.Blob(35,76,15,7,Straw8,7,0,.1f);
                foreach(int wx in new[]{26,46}){p.Oval(wx,93,6,6,13);p.Oval(wx,93,4,4,15);p.Oval(wx,93,1,1,42);p.Rect(wx-4,93,9,1,13);p.Rect(wx,89,1,9,13);}
                break;
            case TownProp.Hay:
                p.Cast(39,100,v==0?13:17,3);
                if(v==0){p.Rect(25,86,22,14,82);p.Rect(25,86,22,2,83);p.Rect(44,88,3,12,81);p.Rect(25,98,22,2,80);for(int x=28;x<45;x+=3)p.Rect(x,89,1,8,81);p.Rect(25,90,22,1,13);p.Rect(25,95,22,1,13);}
                else{p.Blob(36,86,16,14,Straw8,8,0,.09f);p.Rect(35,68,2,6,14);for(int i=0;i<9;i++)p.Set(24+(int)(Hash(i,0,9)%24),78+(int)(Hash(i,1,9)%18),83);}
                break;
            case TownProp.Bench:
                p.Cast(39,100,14,2);
                p.Rect(22,92,28,3,15);p.Rect(22,92,28,1,16);p.Rect(22,94,28,1,13);p.Rect(24,95,2,6,13);p.Rect(46,95,2,6,13);
                p.Rect(22,84,28,2,15);p.Rect(22,87,28,2,14);p.Rect(22,84,2,9,14);p.Rect(48,84,2,9,13);
                break;
            case TownProp.Barrels:
                p.Cast(40,100,15,3);
                foreach(var (x,y,w,h) in new[]{(24,82,13,18),(38,86,12,14)})
                {
                    for(int yy=0;yy<h;yy++)for(int xx=yy<2||yy>=h-2?1:0;xx<w-(yy<2||yy>=h-2?1:0);xx++){float a=xx/(float)(w-1);p.Set(x+xx,y+yy,xx%4==3?(byte)13:a<.2f?(byte)16:a<.5f?(byte)15:a<.8f?(byte)14:(byte)13);}
                    p.Rect(x,y+3,w,2,42);p.Rect(x,y+3,w/3,1,43);p.Rect(x,y+h-5,w,2,42);p.Oval(x+w/2,y,w/2-1,2,15);p.Oval(x+w/2,y,w/2-3,1,13);
                }
                break;
            case TownProp.Crates:
                p.Cast(40,100,15,3);
                foreach(var (x,y,w,h) in new[]{(23,84,16,16),(40,88,12,12),(28,72,13,12)})
                {p.Rect(x,y,w,h,14);p.Rect(x,y,w,2,16);p.Rect(x,y+h-2,w,2,13);p.Rect(x,y,2,h,15);p.Rect(x+w-2,y,2,h,13);p.Line(x+2,y+2,x+w-3,y+h-3,1,13);p.Set(x+1,y+1,43);p.Set(x+w-2,y+h-2,41);}
                break;
            case TownProp.Sacks:
                p.Cast(39,100,13,2);
                p.Blob(30,93,7,7,new byte[]{13,57,57,58,58,65,65,59},11,0,.05f,true);p.Blob(42,94,6,6,new byte[]{13,57,57,58,58,65,65,59},12,-.05f,.05f,true);
                p.Blob(36,85,6,5,new byte[]{13,57,57,58,58,65,65,59},13,.04f,.05f,true);p.Rect(29,85,3,2,14);p.Rect(41,87,3,2,14);p.Rect(35,79,3,2,14);p.Line(27,92,33,95,1,57);p.Line(40,93,45,96,1,57);
                break;
            case TownProp.Planter:
                p.Cast(38,100,10,2);
                if(v==0){p.Rect(27,91,18,9,14);p.Rect(27,91,18,1,16);p.Rect(27,94,18,1,42);p.Rect(43,92,2,8,13);}
                else{p.Poly(67,26,90,46,90,43,100,29,100);p.Rect(26,89,20,2,68);p.Rect(42,91,3,9,66);}
                p.Blob(36,85,10,6,Leaf8,14+v,-.05f);
                foreach(var (x,y) in new[]{(29,84),(33,80),(38,82),(42,85),(36,86)}){p.Rect(x,y,2,2,v==0?(byte)33:(byte)34);p.Set(x,y,v==0?(byte)32:(byte)31);}
                break;
            case TownProp.Woodpile:
                p.Cast(39,100,14,2);
                for(int row=0;row<3;row++)for(int i=0;i<4-row;i++){int x=26+i*7+row*3,y=97-row*6;p.Oval(x,y,3,3,13);p.Oval(x,y,2,2,15);p.Set(x,y,14);p.Set(x-1,y-1,16);}
                p.Rect(22,84,2,16,13);p.Rect(49,84,2,16,13);p.Line(52,100,58,90,2,14);p.Rect(56,88,5,3,42);p.Rect(56,88,5,1,43);
                break;
            case TownProp.Fountain:
                p.Cast(39,101,26,3);
                p.Oval(36,93,26,8,21);p.Oval(36,92,26,7,23);p.Oval(36,92,23,6,87);p.Oval(35,92,18,4,88);
                for(int x=11;x<62;x++)if(p.At(x,97)!=0||p.At(x,99)!=0)for(int y=95;y<101;y++)if(p.At(x,y)==21)p.Set(x,y,(x/6)%2==0?(byte)21:(byte)20);
                p.Rect(32,72,8,20,22);p.Rect(32,72,2,20,24);p.Rect(38,72,2,20,20);p.Oval(36,72,10,3,22);p.Oval(36,71,10,2,24);p.Oval(36,71,7,1,88);
                p.Rect(34,60,4,11,22);p.Rect(34,60,1,11,24);p.Oval(36,60,4,2,23);
                foreach(var (x,y) in new[]{(36,56),(33,58),(39,58),(31,62),(41,62),(28,76),(44,76),(26,82),(46,82)})p.Set(x,y,89);
                p.Rect(22,90,5,1,89);p.Rect(44,93,6,1,89);p.Rect(30,95,4,1,89);
                break;
            case TownProp.Statue:
                p.Cast(39,100,13,2);
                p.Rect(26,90,20,10,21);p.Rect(26,90,20,1,24);p.Rect(26,90,2,10,23);p.Rect(43,90,3,10,20);p.Rect(24,98,24,2,20);p.Rect(31,93,10,3,29);
                // Cloaked warden with a raised lantern.
                p.Poly(22,30,90,31,66,35,62,40,66,42,90);p.Rect(30,70,3,20,23);p.Rect(39,68,3,22,20);p.Oval(36,60,3,4,22);p.Rect(34,57,2,3,24);
                p.Line(40,68,47,58,2,21);p.Rect(46,52,4,5,22);p.Rect(47,53,2,3,93);p.Over(30,80,6,10,4,22,23);
                break;
            case TownProp.Washing:
                p.Cast(39,100,20,2);
                p.Rect(16,70,2,30,14);p.Rect(54,70,2,30,13);p.Rect(16,70,1,30,16);
                for(int x=18;x<54;x++)p.Set(x,73+(int)MathF.Round(3*MathF.Sin((x-18)/36f*MathF.PI)),57);
                foreach(var (x,w,h,c,l) in new[]{(21,7,10,(byte)85,(byte)86),(30,5,8,(byte)61,(byte)62),(37,8,11,(byte)88,(byte)89),(47,5,7,(byte)85,(byte)86)})
                {int top=74+(int)MathF.Round(3*MathF.Sin((x-18)/36f*MathF.PI));p.Rect(x,top,w,h,c);p.Rect(x,top,1,h,l);p.Rect(x+w-1,top+1,1,h-1,84);p.Set(x,top-1,13);p.Set(x+w-1,top-1,13);}
                break;
            case TownProp.Scarecrow:
                p.Cast(38,100,8,2);
                p.Rect(35,70,2,30,14);p.Rect(24,76,24,2,14);p.Rect(24,76,24,1,15);
                p.Poly(61,29,76,43,76,41,90,31,90);p.Rect(29,76,2,10,62);p.Rect(31,88,10,2,60);p.Rect(22,77,3,3,82);p.Rect(47,77,3,3,82);
                p.Oval(36,69,4,4,65);p.Set(34,68,37);p.Set(37,68,37);p.Rect(34,71,4,1,13);p.Poly(81,29,66,43,66,39,60,33,60);p.Rect(28,66,16,1,80);
                p.Rect(32,90,2,4,82);p.Rect(38,90,2,5,82);
                break;
            case TownProp.Garden:
                // Flat: tilled rows (0) or a flower bed (1) filling one tile.
                p.Rect(24,89,24,10,13);
                for(int row=0;row<3;row++)
                {
                    int y=90+row*3;p.Rect(25,y,22,2,14);p.Rect(25,y,22,1,15);
                    for(int x=26;x<46;x+=4){int j=(int)(Hash(x,row,15+v)%2);p.Set(x+j,y-1,v==0?(byte)4:(byte)3);p.Set(x+j,y,v==0?(byte)5:row%2==0?(byte)33:(byte)34);if(v==0)p.Set(x+j+1,y,3);else p.Set(x+j+1,y-1,(byte)(x%8==2?32:31));}
                }
                break;
            case TownProp.BannerPole:
                p.Cast(38,100,6,2);
                p.Rect(35,44,2,56,14);p.Rect(35,44,1,56,16);p.Rect(33,97,6,3,21);p.Rect(33,97,6,1,23);p.Oval(36,43,2,2,29);
                p.Poly(61,37,47,53,49,50,56,53,63,37,65);p.Poly(62,37,47,47,48,45,55,37,57);p.Rect(41,53,5,5,29);p.Set(41,53,30);
                break;
        }
        p.Contour(Flat(prop));return p.Data;
    }
}

/// <summary>Two fixed process-lifetime town atlases: buildings and street furniture.</summary>
public static class TownAtlas
{
    static ImageTexture? buildings,props;
    public static int BuildCount {get;private set;}
    public static Texture2D Buildings=>buildings??=Build(true);
    public static Texture2D Props=>props??=Build(false);
    static ImageTexture Build(bool building)
    {
        int w=building?TownArt.BuildingWidth:TownArt.Width,h=building?TownArt.BuildingHeight:TownArt.Height;
        int columns=building?TownArt.BuildingColumns:TownArt.PropColumns,rows=building?TownArt.BuildingRows:TownArt.PropRows;
        using var image=Image.CreateEmpty(columns*w,rows*h,false,Image.Format.Rgba8);image.Fill(Colors.Transparent);
        void Blit(byte[] data,Rect2 rect)
        {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(data[y*w+x]!=0)image.SetPixel((int)rect.Position.X+x,(int)rect.Position.Y+y,OverworldPropArt.Palette[data[y*w+x]]);
        }
        if(building)foreach(var b in Enum.GetValues<TownBuilding>())for(int v=0;v<TownArt.Count(b);v++)Blit(TownArt.Generate(b,v),TownArt.BuildingSource(TownArt.Module(b,v)));
        else foreach(var p in Enum.GetValues<TownProp>())for(int v=0;v<TownArt.Count(p);v++)Blit(TownArt.Generate(p,v),TownArt.PropSource(TownArt.Module(p,v)));
        BuildCount++;return ImageTexture.CreateFromImage(image);
    }
}
