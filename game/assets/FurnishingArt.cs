using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Things for the insides of houses, shops, halls and workshops. Standard cell.</summary>
public static class FurnishingArt
{
    static readonly (string Name,byte[] Ramp)[] Hues={("red",A.Red),("blue",A.Blue),("green",A.Green),("violet",A.Violet)};
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("furnishings",72,104,8);
        foreach(var (name,r) in Hues)s.Add("bed-double-"+name,p=>DoubleBed(p,r));
        s.Add("bunk-bed",BunkBed).Add("cradle",Cradle).Add("wardrobe",Wardrobe).Add("drawers",Drawers);
        s.Add("chair",p=>Chair(p,false)).Add("chair-side",p=>Chair(p,true)).Add("stool",Stool).Add("armchair",Armchair)
         .Add("dining-table",DiningTable).Add("desk",Desk).Add("workbench",Workbench).Add("map-table",MapTable);
        s.Add("bar-counter",Bar).Add("keg-rack",KegRack).Add("wine-rack",WineRack).Add("cupboard",Cupboard)
         .Add("oven",Oven).Add("bread-rack",BreadRack).Add("butcher-hooks",Hooks).Add("washbasin",Washbasin);
        s.Add("cooking-pot-1",p=>CookingPot(p,0)).Add("cooking-pot-2",p=>CookingPot(p,1)).Add("cauldron-green-1",p=>Cauldron(p,A.Green,0)).Add("cauldron-green-2",p=>Cauldron(p,A.Green,1))
         .Add("cauldron-violet-1",p=>Cauldron(p,A.Violet,0)).Add("cauldron-violet-2",p=>Cauldron(p,A.Violet,1)).Add("alchemy-table",Alchemy).Add("scales",Scales);
        s.Add("fireplace-1",p=>Fireplace(p,0)).Add("fireplace-2",p=>Fireplace(p,1)).Add("fireplace-3",p=>Fireplace(p,2)).Add("fireplace-4",p=>Fireplace(p,3))
         .Add("bookcase-tall",p=>Bookcase(p,0)).Add("bookcase-scrolls",p=>Bookcase(p,1)).Add("shop-shelf-potions",p=>ShopShelf(p,0)).Add("shop-shelf-wares",p=>ShopShelf(p,1));
        s.Add("clock",Clock).Add("mirror",Mirror).Add("coat-rack",CoatRack).Add("ladder",Ladder)
         .Add("plant-fern",p=>Plant(p,0)).Add("plant-tree",p=>Plant(p,1)).Add("plant-flower",p=>Plant(p,2)).Add("vase",Vase);
        s.Add("chandelier",Chandelier).Add("candelabra",Candelabra).Add("floor-lamp",FloorLamp).Add("birdcage",Birdcage)
         .Add("piano",Piano).Add("harp",Harp).Add("globe",Globe).Add("telescope",Telescope);
        s.Add("loom",Loom).Add("spinning-wheel",SpinningWheel).Add("mannequin",Mannequin).Add("sawhorse",Sawhorse)
         .Add("bathtub",Bathtub).Add("strongbox",Strongbox).Add("toy-chest",ToyChest).Add("easel",Easel);
        s.Add("throne",Throne).Add("armour-stand-iron",p=>ArmourStand(p,A.IronT)).Add("armour-stand-gold",p=>ArmourStand(p,A.Gold)).Add("armour-stand-mythril",p=>ArmourStand(p,A.Mythril))
         .Add("trophy-antlers",Trophy).Add("shield-display",ShieldDisplay).Add("pillar",Pillar).Add("stairs-up",Stairs);
        foreach(var (name,r) in Hues)s.Add("curtain-"+name,p=>Curtain(p,r));
        foreach(var (name,r) in Hues)s.Add("carpet-"+name,p=>Carpet(p,r),true,new Vector2(36,94));
        s.Add("painting-landscape",p=>Painting(p,0)).Add("painting-portrait",p=>Painting(p,1)).Add("painting-ship",p=>Painting(p,2)).Add("tapestry",Tapestry)
         .Add("barrel-apples",p=>OpenBarrel(p,A.Red)).Add("barrel-fish",p=>OpenBarrel(p,A.Blue)).Add("crate-open",OpenCrate).Add("trapdoor",Trapdoor,true,new Vector2(36,94));
        return s;
    }

    static void Leg(Pixels p,int x,int y,int h){p.Rect(x,y,2,h,13);p.Set(x,y,14);}
    static void Candle(Pixels p,int x,int y,int tall){p.Rect(x,y-tall,2,tall,31);p.Rect(x+1,y-tall,1,tall,58);p.Rect(x,y-tall-3,2,3,46);p.Set(x,y-tall-4,47);}
    static void DoubleBed(Pixels p,byte[] r)
    {
        p.Cast(40,100,22,3);p.Rect(14,82,44,16,14);p.Rect(14,82,44,1,16);p.Rect(14,96,44,3,13);Brush.Box(p,12,70,48,13,A.Wood);p.Rect(20,73,32,6,13);
        p.Rect(16,84,40,13,r[1]);p.Rect(16,84,40,2,r[3]);p.Rect(16,84,2,13,r[2]);p.Rect(52,86,4,11,r[0]);p.Rect(16,92,40,1,r[0]);
        foreach(int x in new[]{19,38}){p.Rect(x,80,15,6,86);p.Rect(x,80,15,1,98);p.Rect(x+13,81,2,5,84);}
    }
    static void BunkBed(Pixels p)
    {
        p.Cast(40,100,18,3);foreach(int x in new[]{18,52}){p.Rect(x,48,3,52,14);p.Rect(x,48,1,52,16);}
        foreach(var (y,r) in new[]{(60,A.Blue),(86,A.Red)}){p.Rect(20,y,33,10,14);p.Rect(20,y,33,1,16);p.Rect(22,y-2,29,6,r[1]);p.Rect(22,y-2,29,1,r[3]);p.Rect(22,y-4,9,4,86);p.Rect(20,y+9,33,1,13);}
        p.Rect(46,62,2,36,13);for(int y=68;y<96;y+=6)p.Rect(46,y,6,1,15);
    }
    static void Cradle(Pixels p)
    {
        p.Cast(39,100,14,2);p.Poly(14,22,82,50,82,46,94,26,94);p.Poly(15,22,82,36,82,36,94,26,94);p.Rect(22,81,28,2,16);p.Rect(26,84,20,5,86);p.Rect(26,84,8,3,98);
        for(int i=-12;i<=12;i++)p.Set(36+i,96+(int)(3*(1-i*i/144.0)),13);p.Poly(14,20,82,24,70,30,82);p.Rect(24,70,2,3,16);
    }
    static void Wardrobe(Pixels p)
    {
        p.Cast(40,100,17,3);Brush.Box(p,20,46,32,54,A.Wood);p.Rect(18,43,36,4,15);p.Rect(18,43,36,1,16);p.Rect(35,50,2,46,12);
        foreach(int x in new[]{23,39}){p.Rect(x,52,10,40,14);p.Rect(x,52,10,1,16);p.Rect(x,52,1,40,15);p.Rect(x+9,53,1,39,13);p.Rect(x+2,56,6,14,13);p.Rect(x+2,74,6,14,13);}
        p.Rect(32,70,2,4,29);p.Rect(38,70,2,4,29);p.Rect(20,96,32,4,13);
    }
    static void Drawers(Pixels p)
    {
        p.Cast(40,100,16,3);Brush.Box(p,20,72,32,28,A.Wood);p.Rect(18,70,36,3,15);p.Rect(18,70,36,1,16);
        for(int i=0;i<3;i++){int y=75+i*8;p.Rect(22,y,28,6,14);p.Rect(22,y,28,1,16);p.Rect(22,y+5,28,1,13);p.Rect(34,y+2,4,2,29);}
        p.Rect(24,62,5,8,67);p.Rect(24,62,1,8,68);p.Oval(26,58,4,4,3);p.Set(25,56,5);p.Rect(40,66,8,4,65);p.Rect(40,66,8,1,59);
    }
    static void Chair(Pixels p,bool side)
    {
        p.Cast(38,100,8,2);
        if(side){p.Rect(30,68,3,32,14);p.Rect(30,68,1,32,16);p.Rect(30,86,14,3,15);p.Rect(30,86,14,1,16);Leg(p,42,89,11);p.Rect(31,72,2,3,13);p.Rect(31,78,2,3,13);return;}
        p.Rect(28,66,16,22,14);p.Rect(28,66,16,2,16);p.Rect(28,66,2,22,15);p.Rect(42,68,2,20,13);p.Rect(32,70,8,12,13);p.Rect(27,86,18,4,15);p.Rect(27,86,18,1,16);Leg(p,28,90,10);Leg(p,42,90,10);
    }
    static void Stool(Pixels p){p.Cast(38,100,7,2);p.Oval(36,88,8,3,15);p.Oval(36,87,8,2,16);p.Rect(28,88,16,2,14);Leg(p,30,90,10);Leg(p,40,90,10);p.Rect(31,95,10,1,13);}
    static void Armchair(Pixels p)
    {
        p.Cast(39,100,15,3);p.Rect(22,68,28,24,A.Red[1]);p.Rect(22,68,28,2,A.Red[3]);p.Rect(22,68,2,24,A.Red[2]);p.Rect(46,70,4,22,A.Red[0]);
        p.Rect(18,80,8,16,A.Red[1]);p.Rect(46,80,8,16,A.Red[0]);p.Rect(18,80,8,2,A.Red[3]);p.Rect(46,80,8,2,A.Red[2]);p.Rect(26,86,20,8,A.Red[2]);p.Rect(26,86,20,1,A.Red[3]);Leg(p,20,96,4);Leg(p,50,96,4);
        for(int y=72;y<84;y+=5)for(int x=27;x<46;x+=6)p.Set(x,y,A.Red[0]);
    }
    static void DiningTable(Pixels p)
    {
        p.Cast(40,100,26,3);p.Rect(10,82,52,5,15);p.Rect(10,82,52,1,16);p.Rect(10,86,52,1,13);foreach(int x in new[]{13,57})Leg(p,x,87,13);p.Rect(15,92,42,1,13);
        foreach(var (x,c) in new[]{(16,(byte)161),(30,(byte)161),(46,(byte)161)}){p.Oval(x+3,81,4,1,c);}p.Rect(24,77,3,5,67);p.Rect(38,76,4,6,A.Green[0]);p.Rect(39,73,2,3,A.Green[0]);
        Candle(p,34,82,5);p.Rect(52,79,4,3,165);p.Rect(52,79,4,1,166);
    }
    static void Desk(Pixels p)
    {
        p.Cast(40,100,20,3);p.Rect(16,80,40,4,15);p.Rect(16,80,40,1,16);Brush.Box(p,18,84,14,16,A.Wood);p.Rect(20,87,10,5,14);p.Rect(24,89,3,1,29);p.Rect(20,94,10,5,14);Leg(p,52,84,16);
        p.Rect(34,77,10,3,65);p.Rect(34,77,10,1,59);p.Rect(36,78,6,1,57);p.Rect(46,74,4,6,A.Dark[1]);p.Line(48,74,53,64,1,A.Pale[2]);Candle(p,20,80,5);p.Rect(26,76,6,4,A.Blue[1]);p.Rect(26,76,6,1,A.Blue[2]);
    }
    static void Workbench(Pixels p)
    {
        p.Cast(40,100,22,3);p.Rect(14,80,44,5,15);p.Rect(14,80,44,1,16);p.Rect(14,84,44,1,13);foreach(int x in new[]{16,54}){p.Rect(x,85,3,15,14);}p.Rect(18,93,36,2,13);
        p.Rect(14,58,44,2,14);p.Rect(14,58,2,22,14);p.Rect(56,58,2,22,13);
        p.Rect(20,62,2,12,15);p.Rect(18,60,6,3,A.Steel[2]);p.Rect(28,62,2,12,15);p.Poly(A.Steel[2],26,60,34,60,34,64,28,66);p.Rect(38,62,1,14,A.Steel[1]);p.Rect(36,60,5,2,15);p.Rect(46,62,2,10,15);p.Oval(47,60,3,2,A.Steel[2]);
        p.Rect(22,76,8,4,A.Steel[1]);p.Rect(22,76,8,1,A.Steel[3]);p.Rect(40,77,10,3,14);p.Rect(40,77,10,1,16);
    }
    static void MapTable(Pixels p)
    {
        p.Cast(40,100,22,3);p.Rect(14,80,44,5,15);p.Rect(14,80,44,1,16);foreach(int x in new[]{16,54})Leg(p,x,85,15);
        p.Poly(65,18,74,54,74,56,82,16,82);p.Poly(59,18,74,36,74,35,82,16,82);p.Line(22,80,30,77,1,A.Red[1]);p.Line(30,77,40,79,1,A.Red[1]);p.Rect(44,76,3,2,A.Blue[1]);p.Rect(24,76,3,1,A.Green[1]);
        p.Rect(48,72,2,4,A.Red[2]);p.Rect(36,74,2,3,A.Steel[2]);Candle(p,20,76,4);
    }
    static void Bar(Pixels p)
    {
        p.Cast(40,100,28,3);Brush.Planks(p,8,82,56,18,true,601);p.Rect(6,78,60,5,15);p.Rect(6,78,60,1,16);p.Rect(6,82,60,1,13);
        foreach(var (x,r) in new[]{(12,A.WoodT),(26,A.WoodT),(52,A.WoodT)}){p.Rect(x,70,6,8,r[1]);p.Rect(x,70,2,8,r[2]);p.Rect(x+6,72,2,4,r[1]);p.Rect(x,69,6,2,161);}
        p.Rect(38,66,4,12,A.Green[0]);p.Rect(39,62,2,4,A.Green[0]);p.Rect(44,68,4,10,A.Red[0]);p.Rect(45,65,2,3,A.Red[0]);
    }
    static void KegRack(Pixels p)
    {
        p.Cast(40,100,22,3);foreach(int x in new[]{14,56}){p.Rect(x,56,2,44,14);}
        foreach(int y in new[]{66,88})foreach(int x in new[]{26,46}){p.Oval(x,y,9,9,14);p.Oval(x,y,7,7,15);p.Oval(x,y,5,5,14);p.Oval(x-1,y-1,3,3,15);p.Rect(x-1,y+5,3,3,A.Steel[1]);p.Rect(x-9,y,18,1,42);}
        p.Rect(14,76,44,2,14);p.Rect(14,98,44,2,13);
    }
    static void WineRack(Pixels p)
    {
        p.Cast(40,100,16,3);Brush.Box(p,20,52,32,48,A.Wood);
        for(int row=0;row<5;row++)for(int col=0;col<4;col++){int x=23+col*7,y=56+row*9;p.Rect(x,y,5,6,12);if((row+col)%4!=3){p.Oval(x+2,y+3,2,2,A.Green[0]);p.Set(x+2,y+3,(row+col)%2==0?A.Red[1]:A.Yellow[1]);p.Set(x+1,y+2,A.Green[2]);}}
    }
    static void Cupboard(Pixels p)
    {
        p.Cast(40,100,17,3);Brush.Box(p,20,50,32,50,A.Wood);p.Rect(18,47,36,4,15);p.Rect(18,47,36,1,16);
        for(int i=0;i<3;i++){int y=54+i*9;p.Rect(22,y,28,8,12);p.Rect(22,y+7,28,1,15);for(int x=24;x<48;x+=6){p.Oval(x+2,y+5,2,1,161);p.Rect(x,y+2,4,3,i==1?(byte)67:(byte)160);p.Set(x,y+2,98);}}
        p.Rect(22,82,13,16,14);p.Rect(37,82,13,16,14);p.Rect(22,82,13,1,16);p.Rect(37,82,13,1,16);p.Rect(33,88,2,3,29);p.Rect(37,88,2,3,29);
    }
    static void Oven(Pixels p)
    {
        p.Cast(40,100,20,3);Brush.Masonry(p,16,70,40,30,611);for(int y=52;y<71;y++){int half=20-(70-y)*(70-y)/30;if(half<4)continue;for(int x=36-half;x<36+half;x++)p.Set(x,y,x<36-half+3?(byte)23:x>36+half-4?(byte)20:(y+x)%7==0?(byte)20:(byte)21);}
        p.Rect(26,78,20,14,A.Ink);p.Oval(36,78,10,6,A.Ink);p.Rect(28,86,16,6,44);p.Rect(30,84,12,6,45);p.Rect(33,83,6,5,46);p.Rect(16,96,40,2,22);
        p.Rect(34,40,6,14,21);p.Rect(34,40,2,14,23);p.Rect(33,38,8,3,22);p.Oval(46,92,4,2,165);p.Oval(46,91,3,1,166);
    }
    static void BreadRack(Pixels p)
    {
        p.Cast(40,100,16,3);foreach(int x in new[]{20,50}){p.Rect(x,56,2,44,14);}
        for(int i=0;i<4;i++){int y=64+i*10;p.Rect(20,y,32,2,15);p.Rect(20,y+1,32,1,13);for(int x=23;x<48;x+=8){p.Oval(x+3,y-2,4,2,i%2==0?(byte)165:(byte)164);p.Oval(x+2,y-3,2,1,166);}}
    }
    static void Hooks(Pixels p)
    {
        p.Cast(40,100,16,2);foreach(int x in new[]{18,52}){p.Rect(x,52,2,48,14);}p.Rect(16,54,40,3,14);p.Rect(16,54,40,1,16);
        foreach(var (x,len) in new[]{(24,18),(34,24),(44,16)}){p.Rect(x+2,57,1,5,42);p.Oval(x+3,62+len/2,4,len/2,73);p.Oval(x+2,61+len/2,3,len/2-1,74);p.Rect(x+2,64,2,len-6,166);p.Rect(x+1,62+len,4,3,58);}
    }
    static void Washbasin(Pixels p)
    {
        p.Cast(39,100,12,2);Brush.Box(p,26,80,20,20,A.Wood);p.Oval(36,79,11,4,161);p.Oval(36,79,9,3,142);p.Oval(34,78,5,1,143);p.Rect(46,68,3,10,A.Steel[2]);p.Rect(42,68,6,2,A.Steel[2]);p.Rect(22,74,4,6,85);p.Rect(22,74,4,1,86);
    }
    static void CookingPot(Pixels p,int frame)
    {
        p.Cast(38,100,13,2);foreach(var (x,y) in new[]{(25,98),(47,98),(36,101)})p.Blob(x,y,3,2,A.Stone8,x,0,.03f,true);Brush.Flame(p,36,97,12,10,frame);
        p.Line(20,100,36,62,2,42);p.Line(52,100,36,62,2,41);p.Rect(35,62,2,14,41);p.Blob(36,84,10,8,A.Dark8,621,0,.02f,true);p.Oval(36,77,9,3,A.Dark[1]);p.Oval(36,77,7,2,A.Orange[1]);
        p.Set(33+frame*2,74,160);p.Set(38-frame,71,160);p.Set(35,68-frame,159);
    }
    static void Cauldron(Pixels p,byte[] r,int frame)
    {
        p.Cast(39,100,15,3);p.Blob(36,88,14,11,A.Dark8,631,0,.02f,true);p.Rect(22,78,28,3,A.Dark[2]);p.Rect(22,78,14,1,A.Dark[3]);p.Oval(36,79,12,3,r[1]);p.Oval(35,78,8,1,r[2]);
        foreach(var (x,y,rr) in frame==0?new[]{(30,76,2),(40,74,3),(35,70,1)}:new[]{(32,74,3),(42,77,2),(38,69,1)}){p.Oval(x,y,rr,rr,r[2]);p.Set(x-1,y-1,r[3]);}
        Leg(p,26,97,3);Leg(p,44,97,3);p.Set(30+frame*6,64,r[3]);p.Set(44-frame*4,62,r[2]);
    }
    static void Alchemy(Pixels p)
    {
        p.Cast(40,100,22,3);p.Rect(14,80,44,5,15);p.Rect(14,80,44,1,16);foreach(int x in new[]{16,54})Leg(p,x,85,15);p.Rect(18,90,36,2,13);
        foreach(var (x,r,h) in new[]{(18,A.Red,8),(27,A.Green,11),(46,A.Blue,9)}){p.Oval(x+3,78,4,3,r[1]);p.Oval(x+2,77,2,2,r[2]);p.Rect(x+2,78-h,2,h-2,160);p.Rect(x+2,77-h,2,1,A.Leather[1]);p.Set(x+1,76,161);}
        p.Line(30,70,44,66,1,160);p.Line(44,66,46,72,1,160);p.Rect(36,72,6,8,A.Bronze[1]);p.Rect(36,72,6,1,A.Bronze[3]);p.Rect(37,80-3,4,3,46);p.Rect(50,74,6,6,A.Violet[1]);p.Rect(50,74,6,1,A.Violet[3]);p.Set(33,62,A.Green[3]);p.Set(40,58,160);
    }
    static void Scales(Pixels p)
    {
        p.Cast(38,100,10,2);p.Rect(35,70,2,28,A.Gold[1]);p.Rect(35,70,1,28,A.Gold[3]);p.Rect(29,97,14,3,A.Gold[1]);p.Rect(29,97,14,1,A.Gold[3]);p.Line(22,74,50,70,1,A.Gold[2]);p.Oval(36,69,2,2,A.Gold[2]);
        foreach(var (x,y) in new[]{(22,74),(50,70)}){p.Line(x,y,x-5,y+10,1,A.Gold[0]);p.Line(x,y,x+5,y+10,1,A.Gold[0]);p.Oval(x,y+11,6,2,A.Gold[2]);p.Oval(x-1,y+10,4,1,A.Gold[3]);}
        p.Rect(48,78,4,3,A.Red[2]);
    }
    static void Fireplace(Pixels p,int frame)
    {
        p.Cast(40,100,22,3);Brush.Masonry(p,12,56,48,44,641);Brush.Box(p,8,50,56,7,A.Stone);p.Rect(22,68,28,32,A.Ink);p.Oval(36,68,14,6,A.Ink);p.Rect(24,96,24,3,13);p.Rect(26,95,20,1,14);
        Brush.Flame(p,36,96,20,24,frame);p.Rect(14,44,4,6,67);Candle(p,24,50,5);p.Rect(40,43,10,7,13);p.Rect(41,44,8,5,A.Blue[1]);p.Rect(41,47,8,2,A.Green[1]);p.Rect(54,44,4,6,29);
    }
    static void Bookcase(Pixels p,int v)
    {
        p.Cast(40,100,17,3);p.Rect(20,36,32,64,12);p.Rect(20,36,2,64,15);p.Rect(50,36,2,64,13);p.Rect(18,33,36,4,15);p.Rect(18,33,36,1,16);p.Rect(20,96,32,4,14);
        byte[] bind={63,61,64,65,28,60,138,142,146,118};
        for(int shelf=0;shelf<5;shelf++)
        {
            int board=47+shelf*12;p.Rect(22,board,28,2,15);p.Rect(22,board+1,28,1,13);
            if(v==1&&shelf%2==0){for(int i=0;i<6;i++){p.Oval(25+i*4,board-2-(i%2)*3,1,1,65);p.Set(25+i*4,board-2-(i%2)*3,57);}continue;}
            for(int x=23;x<49;){uint h=A.Hash(x,shelf,650+v);int w=2+(int)(h%2),tall=6+(int)(h>>3)%4;if(h%9==0){x+=w;continue;}byte c=bind[(h>>6)%bind.Length];p.Rect(x,board-tall,w,tall,c);if((h>>9)%3==0)p.Rect(x,board-tall+2,w,1,29);x+=w;}
        }
    }
    static void ShopShelf(Pixels p,int v)
    {
        p.Cast(40,100,17,3);p.Rect(20,44,32,56,12);p.Rect(20,44,2,56,15);p.Rect(50,44,2,56,13);p.Rect(18,41,36,4,15);p.Rect(18,41,36,1,16);
        for(int shelf=0;shelf<4;shelf++)
        {
            int board=56+shelf*13;p.Rect(22,board,28,2,15);p.Rect(22,board+1,28,1,13);
            for(int i=0;i<4;i++)
            {
                int x=24+i*7;var r=new[]{A.Red,A.Blue,A.Green,A.Yellow,A.Violet,A.Orange}[(i+shelf*3)%6];
                if(v==0){p.Oval(x+2,board-3,3,3,r[1]);p.Oval(x+1,board-4,2,2,r[2]);p.Rect(x+1,board-9,2,3,160);p.Set(x,board-4,161);}
                else if((i+shelf)%3==0){p.Rect(x,board-7,5,7,67);p.Rect(x,board-7,1,7,68);}
                else if((i+shelf)%3==1){p.Rect(x,board-5,5,5,r[1]);p.Rect(x,board-5,5,1,r[3]);}
                else{p.Rect(x+1,board-8,2,8,15);p.Poly(A.Steel[2],x,board-8,x+5,board-8,x+4,board-5,x+1,board-5);}
            }
        }
    }
    static void Clock(Pixels p)
    {
        p.Cast(39,100,10,3);Brush.Box(p,28,38,16,62,A.Wood);p.Poly(15,26,38,36,30,46,38);p.Rect(26,38,20,2,16);p.Oval(36,50,6,6,161);p.Oval(36,50,5,5,86);p.Line(36,50,36,46,1,A.Ink);p.Line(36,50,39,51,1,A.Ink);
        p.Rect(31,62,10,26,12);p.Rect(35,62,2,16,A.Gold[1]);p.Oval(36,80,3,3,A.Gold[2]);p.Set(35,79,A.Gold[3]);p.Rect(26,96,20,4,14);
    }
    static void Mirror(Pixels p)
    {
        p.Cast(39,100,10,2);p.Oval(36,68,12,18,A.Gold[1]);p.Oval(36,68,10,16,143);p.Oval(34,64,6,10,144);p.Line(30,74,38,58,1,161);p.Line(33,78,40,66,1,161);
        p.Rect(34,86,4,10,A.Gold[1]);p.Rect(28,96,16,4,A.Gold[1]);p.Rect(28,96,16,1,A.Gold[3]);p.Oval(36,49,3,2,A.Gold[2]);
    }
    static void CoatRack(Pixels p)
    {
        p.Cast(38,100,8,2);p.Rect(35,44,2,56,14);p.Rect(35,44,1,56,16);p.Line(36,98,28,100,2,14);p.Line(36,98,44,100,2,13);foreach(var (ex,ey) in new[]{(26,46),(46,46),(28,54),(44,54)})p.Line(36,ey+4,ex,ey,1,15);
        p.Poly(A.Leather[1],26,48,34,50,33,76,25,74);p.Poly(A.Leather[2],26,48,30,49,29,75,25,74);p.Poly(A.Dark[1],40,44,50,44,48,50,42,50);p.Rect(38,49,14,2,A.Dark[2]);
    }
    static void Ladder(Pixels p){p.Cast(38,100,9,2);p.Line(28,100,32,36,2,14);p.Line(46,100,42,36,2,13);for(int y=44;y<98;y+=9){int a=28+(100-y)*4/64,b=46-(100-y)*4/64;p.Rect(a+2,y,b-a-2,2,15);p.Rect(a+2,y+1,b-a-2,1,13);}}
    static void Plant(Pixels p,int v)
    {
        p.Cast(38,100,9,2);p.Poly(67,28,86,44,86,42,100,30,100);p.Rect(27,84,18,3,68);p.Rect(41,87,3,13,66);p.Rect(28,90,14,1,29);
        if(v==0)foreach(var (ex,ey) in new[]{(18,70),(24,60),(34,54),(42,58),(52,66),(54,76)}){for(int i=0;i<=8;i++){float t=i/8f;int x=36+(int)((ex-36)*t),y=85+(int)((ey-85)*t-5*MathF.Sin(t*3.14f));p.Set(x,y,3);p.Set(x,y-1,ex<36?(byte)5:(byte)4);p.Set(x+1,y+1,2);}}
        if(v==1){p.Rect(35,60,2,26,14);p.Blob(36,54,12,10,A.Leaf8,661,0);p.Blob(30,60,6,5,A.Leaf8,662,0);p.Blob(43,61,6,5,A.Leaf8,663,-.05f);}
        if(v==2){p.Blob(36,78,10,7,A.Leaf8,664,0);foreach(var (x,y,c) in new[]{(29,72,(byte)138),(35,68,(byte)151),(41,71,(byte)122),(33,77,(byte)119),(43,78,(byte)139)}){p.Rect(x,y,3,3,c);p.Set(x+1,y+1,152);}}
    }
    static void Vase(Pixels p)
    {
        p.Cast(38,100,8,2);p.Blob(36,88,8,11,A.Blue8,671,0,.02f,true);p.Rect(32,74,8,3,A.Blue[2]);p.Rect(31,73,10,1,A.Blue[3]);p.Rect(29,86,14,1,A.Gold[2]);p.Rect(30,90,12,1,A.Gold[1]);p.Rect(32,98,8,2,A.Blue[0]);
        foreach(var (ex,ey,c) in new[]{(28,58,(byte)138),(36,52,(byte)151),(44,60,(byte)122)}){p.Line(36,74,ex,ey+3,1,3);p.Rect(ex-1,ey,3,3,c);p.Set(ex,ey+1,152);}
    }
    static void Chandelier(Pixels p)
    {
        p.Rect(35,20,2,26,A.Gold[0]);p.Oval(36,50,5,4,A.Gold[1]);p.Oval(35,49,3,2,A.Gold[3]);
        for(int i=-22;i<=22;i++)p.Set(36+i,58+(int)(6*(1-i*i/484.0)),A.Gold[i<0?2:1]);p.Rect(14,58,45,2,A.Gold[1]);p.Rect(14,58,22,1,A.Gold[3]);
        foreach(int x in new[]{15,26,36,46,57}){p.Rect(x-1,52,3,6,161);p.Rect(x,52,1,6,98);p.Rect(x-1,48,3,4,46);p.Set(x,46,47);p.Set(x,47,45);}
        foreach(int x in new[]{20,31,41,52})p.Set(x,62+x%3,56);
    }
    static void Candelabra(Pixels p)
    {
        p.Cast(38,100,8,2);p.Rect(35,62,2,36,A.Gold[1]);p.Rect(35,62,1,36,A.Gold[3]);p.Rect(29,97,14,3,A.Gold[1]);p.Rect(29,97,14,1,A.Gold[3]);
        for(int i=-10;i<=10;i++)p.Set(36+i,70-(int)(6*(1-i*i/100.0)),A.Gold[2]);p.Rect(25,62,23,2,A.Gold[2]);foreach(int x in new[]{26,36,46})Candle(p,x-1,62,7);
    }
    static void FloorLamp(Pixels p){p.Cast(38,100,7,2);p.Rect(35,56,2,42,A.Bronze[1]);p.Rect(35,56,1,42,A.Bronze[3]);p.Rect(30,97,12,3,A.Bronze[1]);p.Poly(93,28,56,44,56,40,40,32,40);p.Poly(47,28,56,36,56,35,40,32,40);p.Rect(28,55,16,2,A.Bronze[2]);p.Rect(32,39,8,2,A.Bronze[2]);}
    static void Birdcage(Pixels p)
    {
        p.Cast(38,100,8,2);p.Rect(35,54,2,44,A.Bronze[0]);p.Rect(30,97,12,3,A.Bronze[1]);p.Line(36,54,46,50,1,A.Bronze[1]);p.Rect(46,50,1,6,A.Bronze[1]);
        for(int y=56;y<80;y++){int half=y<62?(y-55)+2:8;for(int x=47-half;x<=47+half;x++)if((x-47)%3==0||y==79||y==68)p.Set(x,y,A.Gold[2]);}
        p.Oval(47,72,3,3,151);p.Oval(49,69,2,2,151);p.Set(51,69,154);p.Set(49,68,A.Ink);
    }
    static void Piano(Pixels p)
    {
        p.Cast(40,100,22,3);p.Rect(14,58,44,30,A.Dark[0]);p.Rect(14,58,44,2,A.Dark[2]);p.Rect(14,58,2,30,A.Dark[1]);p.Rect(12,78,48,4,A.Dark[1]);p.Rect(14,82,44,4,161);for(int x=15;x<58;x+=3)p.Rect(x,82,1,4,160);for(int x=17;x<56;x+=6)p.Rect(x,82,2,2,A.Dark[0]);
        Leg(p,15,86,14);Leg(p,55,86,14);p.Rect(26,64,20,10,65);p.Rect(28,66,16,1,57);p.Rect(28,69,12,1,57);p.Rect(22,92,28,3,A.Red[1]);Leg(p,24,95,5);Leg(p,46,95,5);
    }
    static void Harp(Pixels p)
    {
        p.Cast(38,100,10,2);p.Rect(26,96,20,4,A.Gold[1]);p.Line(28,96,30,40,3,A.Gold[1]);p.Line(28,96,30,40,1,A.Gold[3]);
        for(int i=0;i<=16;i++){float t=i/16f;p.Rect(30+(int)(18*t),40+(int)(8*MathF.Sin(t*3.14f))+(int)(t*4),2,2,A.Gold[2]);}p.Line(48,46,44,96,2,A.Gold[0]);
        for(int x=33;x<46;x+=2)p.Line(x,46+(x-33)/2,x-2+(x-33)/4,95,1,(x/2)%3==0?A.Red[2]:(byte)161);
    }
    static void Globe(Pixels p)
    {
        p.Cast(38,100,9,2);p.Rect(35,88,2,8,A.Bronze[1]);p.Rect(28,96,16,4,A.Bronze[1]);p.Rect(28,96,16,1,A.Bronze[3]);p.Oval(36,74,13,13,A.Bronze[0]);p.Oval(36,74,11,11,142);p.Oval(34,72,8,8,143);
        p.Poly(146,30,66,36,64,38,70,34,76,29,72);p.Poly(147,40,74,46,72,45,80,40,82);p.Rect(32,82,5,2,146);p.Line(25,80,47,68,1,A.Bronze[2]);
    }
    static void Telescope(Pixels p)
    {
        p.Cast(38,100,11,2);p.Line(36,78,26,100,2,14);p.Line(36,78,46,100,2,13);p.Line(36,78,36,100,2,14);
        p.Line(22,82,50,60,5,A.Bronze[1]);p.Line(22,81,50,59,2,A.Bronze[3]);p.Oval(52,58,4,4,A.Bronze[2]);p.Oval(52,58,2,2,143);p.Rect(18,82,5,4,A.Bronze[0]);p.Rect(34,68,5,5,A.Gold[1]);
    }
    static void Loom(Pixels p)
    {
        p.Cast(40,100,18,3);foreach(int x in new[]{18,52}){p.Rect(x,50,3,50,14);p.Rect(x,50,1,50,16);}p.Rect(16,50,40,3,15);p.Rect(16,94,40,3,14);p.Rect(18,72,36,2,14);
        for(int x=22;x<51;x+=2)p.Rect(x,53,1,41,x%4==0?(byte)161:(byte)160);for(int y=76;y<94;y+=3)p.Rect(21,y,31,2,(y/3)%3==0?A.Red[1]:(y/3)%3==1?A.Blue[1]:A.Yellow[1]);p.Rect(24,69,10,3,15);
    }
    static void SpinningWheel(Pixels p)
    {
        p.Cast(39,100,15,2);p.Oval(46,76,13,13,14);p.Oval(46,76,11,11,0);for(int i=0;i<8;i++){double a=i*.785;p.Line(46,76,46+(int)(Math.Cos(a)*11),76+(int)(Math.Sin(a)*11),1,15);}p.Oval(46,76,2,2,13);
        p.Rect(20,86,36,3,15);p.Rect(20,86,36,1,16);Leg(p,22,89,11);Leg(p,52,89,11);p.Rect(24,70,2,16,14);p.Oval(25,68,4,4,161);p.Oval(24,67,2,2,98);p.Line(28,70,36,74,1,161);
    }
    static void Mannequin(Pixels p)
    {
        p.Cast(38,100,8,2);p.Rect(35,84,2,14,14);p.Rect(29,97,14,3,14);p.Poly(A.Red[1],28,56,44,56,46,64,42,84,30,84,26,64);p.Poly(A.Red[2],28,56,36,56,35,84,30,84,26,64);p.Rect(30,70,12,2,29);p.Oval(36,50,4,5,65);p.Rect(34,54,4,3,58);
        p.Rect(28,56,16,1,A.Red[3]);for(int y=60;y<82;y+=5)p.Set(36,y,29);
    }
    static void Sawhorse(Pixels p)
    {
        p.Cast(39,100,16,2);p.Rect(16,82,40,4,15);p.Rect(16,82,40,1,16);foreach(int x in new[]{20,50}){p.Line(x,86,x-4,100,2,14);p.Line(x,86,x+4,100,2,13);}
        p.Rect(24,76,28,6,14);p.Rect(24,76,28,1,16);p.Rect(38,72,1,14,A.Steel[2]);p.Poly(A.Steel[2],32,66,46,70,46,73,32,69);p.Rect(28,64,5,5,14);
    }
    static void Bathtub(Pixels p)
    {
        p.Cast(40,100,22,3);p.Poly(160,14,78,58,78,54,96,18,96);p.Poly(161,14,78,36,78,35,96,18,96);p.Rect(12,76,48,3,98);p.Oval(36,80,20,3,143);p.Oval(32,79,12,1,144);
        foreach(int x in new[]{20,50}){p.Rect(x,96,4,4,A.Gold[1]);}p.Oval(26,76,4,3,161);p.Oval(32,74,3,3,98);p.Oval(40,76,4,2,161);p.Rect(56,66,3,10,A.Gold[1]);p.Rect(52,66,6,2,A.Gold[2]);
    }
    static void Strongbox(Pixels p)
    {
        p.Cast(39,100,14,3);Brush.Box(p,22,78,28,22,new byte[]{37,41,42,42,43,23});p.Rect(22,84,28,1,41);p.Rect(22,94,28,1,41);foreach(int x in new[]{24,46})for(int y=80;y<98;y+=6)p.Set(x,y,23);
        p.Oval(36,89,5,5,A.Gold[1]);p.Oval(36,89,3,3,A.Gold[2]);p.Line(36,89,36,86,1,A.Ink);p.Rect(44,88,3,2,A.Gold[2]);
    }
    static void ToyChest(Pixels p)
    {
        p.Cast(39,100,14,3);Brush.Box(p,22,84,28,16,A.Wood);p.Rect(22,84,28,3,A.Blue[1]);p.Rect(22,84,28,1,A.Blue[3]);p.Rect(34,90,4,4,A.Yellow[2]);
        p.Oval(28,80,4,4,A.Red[1]);p.Set(27,79,A.Red[3]);p.Rect(36,74,2,10,15);p.Poly(A.Green[2],38,74,46,77,38,80);p.Oval(44,82,3,2,A.Leather[2]);p.Oval(46,80,2,2,A.Leather[2]);p.Set(47,79,A.Ink);
    }
    static void Easel(Pixels p)
    {
        p.Cast(38,100,11,2);p.Line(36,46,26,100,2,14);p.Line(36,46,46,100,2,13);p.Line(36,50,38,98,1,13);p.Rect(22,56,28,24,161);p.Rect(22,56,28,1,98);p.Rect(22,80,28,2,15);
        p.Rect(24,70,24,8,A.Green[1]);p.Rect(24,58,24,12,143);p.Oval(42,62,3,3,151);p.Poly(A.Green[2],24,74,32,64,40,74);p.Poly(A.Stone[3],36,74,42,68,48,74);
    }
    static void Throne(Pixels p)
    {
        p.Cast(40,100,18,3);Brush.Box(p,22,40,28,52,A.Stone);p.Poly(22,22,40,28,30,36,36,44,30,50,40);p.Rect(26,46,20,34,A.Red[1]);p.Rect(26,46,20,2,A.Red[3]);p.Rect(26,46,2,34,A.Red[2]);
        Brush.Box(p,16,72,10,28,A.Stone);Brush.Box(p,46,72,10,28,A.Stone);Brush.Box(p,24,82,24,18,A.Stone);p.Rect(26,80,20,4,A.Red[2]);p.Rect(16,70,10,3,A.Gold[2]);p.Rect(46,70,10,3,A.Gold[1]);
        p.Oval(36,34,3,3,A.Gold[2]);p.Set(35,33,A.Gold[3]);p.Rect(28,30,2,2,A.Blue[2]);p.Rect(42,30,2,2,A.Blue[2]);
    }
    static void ArmourStand(Pixels p,byte[] r)
    {
        p.Cast(38,100,9,2);p.Rect(35,50,2,48,14);p.Rect(28,97,16,3,14);p.Rect(26,62,20,2,14);
        p.Poly(r[1],26,60,32,56,36,60,40,56,46,60,47,68,44,70,44,84,28,84,28,70,25,68);p.Poly(r[2],27,61,32,57,36,61,36,83,29,83,29,70,26,68);p.Rect(35,62,2,20,r[0]);p.Rect(29,74,14,1,r[0]);p.Rect(30,60,3,1,r[3]);
        p.Oval(36,48,6,6,r[1]);p.Oval(35,47,4,4,r[2]);p.Rect(31,49,10,2,A.Ink);p.Rect(35,40,2,4,A.Red[1]);p.Set(33,45,r[3]);
    }
    static void Trophy(Pixels p)
    {
        Brush.Box(p,26,66,20,18,A.Wood);p.Oval(36,66,10,5,15);p.Oval(36,74,6,6,A.Leather[2]);p.Oval(35,73,4,4,A.Leather[3]);p.Set(33,73,A.Ink);p.Set(39,73,A.Ink);p.Oval(36,78,2,1,A.Dark[0]);
        foreach(int d in new[]{-1,1}){p.Line(36+d*5,68,36+d*14,56,2,58);p.Line(36+d*10,61,36+d*10,52,1,59);p.Line(36+d*14,56,36+d*18,50,1,59);p.Line(36+d*13,58,36+d*19,58,1,58);}
    }
    static void ShieldDisplay(Pixels p)
    {
        p.Line(22,52,50,84,2,A.Steel[2]);p.Line(50,52,22,84,2,A.Steel[2]);p.Line(21,51,24,54,2,A.Gold[1]);p.Line(51,51,48,54,2,A.Gold[1]);
        p.Poly(A.Blue[1],24,56,48,56,48,72,36,88,24,72);p.Poly(A.Blue[2],25,57,36,57,36,87,25,72);p.Rect(35,57,2,30,A.Gold[2]);p.Rect(25,68,22,2,A.Gold[2]);p.Oval(36,69,3,3,A.Gold[3]);
    }
    static void Pillar(Pixels p)
    {
        p.Cast(40,100,12,3);Brush.Box(p,24,92,24,8,A.Stone);Brush.Cylinder(p,28,24,16,68,new byte[]{20,21,22,24});for(int y=28;y<90;y+=2){p.Set(33,y,20);p.Set(38,y,20);}Brush.Box(p,24,16,24,8,A.Stone);p.Rect(26,23,20,1,19);
    }
    static void Stairs(Pixels p)
    {
        p.Cast(40,100,20,3);for(int i=0;i<7;i++){int y=94-i*8;p.Rect(16+i*2,y,40-i*2,8,i%2==0?(byte)14:(byte)15);p.Rect(16+i*2,y,40-i*2,1,16);p.Rect(16+i*2,y+7,40-i*2,1,12);}
        p.Line(16,90,28,42,2,14);p.Line(16,89,28,41,1,16);for(int i=0;i<7;i++)p.Rect(17+i*2,84-i*8,1,10,13);
    }
    static void Curtain(Pixels p,byte[] r)
    {
        p.Rect(14,34,44,3,A.Gold[1]);p.Rect(14,34,44,1,A.Gold[3]);p.Oval(13,35,2,2,A.Gold[2]);p.Oval(58,35,2,2,A.Gold[2]);
        foreach(var (x,w) in new[]{(16,14),(42,14)})for(int xx=x;xx<x+w;xx++){int fold=(xx-x)%5;for(int y=37;y<94-(xx-x<3||xx-x>w-4?0:0);y++)p.Set(xx,y,fold==0?r[3]:fold==4?r[0]:fold<3?r[2]:r[1]);}
        p.Rect(16,92,14,2,A.Gold[1]);p.Rect(42,92,14,2,A.Gold[1]);p.Rect(30,40,12,30,143);p.Rect(30,40,12,2,144);p.Rect(35,40,2,30,13);p.Rect(30,54,12,2,13);p.Rect(29,39,14,1,13);p.Rect(29,70,14,2,15);
    }
    static void Carpet(Pixels p,byte[] r)
    {
        p.Rect(8,84,56,18,r[0]);p.Rect(10,85,52,16,r[1]);p.Rect(12,87,48,12,r[2]);p.Rect(14,88,44,10,r[1]);for(int x=10;x<62;x+=4){p.Set(x,85,29);p.Set(x+2,100,29);}
        p.Poly(29,36,89,46,93,36,97,26,93);p.Poly(r[3],36,91,41,93,36,95,31,93);foreach(int x in new[]{18,54}){p.Poly(29,x,90,x+3,93,x,96,x-3,93);}for(int y=84;y<102;y+=2){p.Set(7,y,r[3]);p.Set(64,y,r[3]);}
    }
    static void Painting(Pixels p,int v)
    {
        p.Rect(18,50,36,28,A.Gold[1]);p.Rect(18,50,36,2,A.Gold[3]);p.Rect(18,50,2,28,A.Gold[2]);p.Rect(22,54,28,20,143);
        if(v==0){p.Rect(22,66,28,8,A.Green[1]);p.Poly(A.Stone[3],26,66,34,56,42,66);p.Poly(161,32,59,34,56,36,59);p.Oval(44,58,2,2,151);p.Rect(38,64,3,4,A.Green[0]);}
        if(v==1){p.Rect(22,54,28,20,A.Dark[1]);p.Oval(36,62,5,6,163);p.Poly(A.Red[1],28,74,31,68,41,68,44,74);p.Set(34,61,A.Ink);p.Set(38,61,A.Ink);p.Rect(31,55,10,4,A.Leather[1]);}
        if(v==2){p.Rect(22,68,28,6,142);p.Poly(14,28,68,44,68,41,72,31,72);p.Rect(35,56,1,12,13);p.Poly(86,36,57,44,66,36,66);p.Poly(85,34,59,29,66,34,66);p.Set(26,70,144);p.Set(46,71,144);}
    }
    static void Tapestry(Pixels p)
    {
        p.Rect(20,34,32,2,A.Gold[1]);for(int x=22;x<50;x++){int tail=x<26||x>45?0:(x-22)%8<4?3:0;for(int y=36;y<86+tail;y++)p.Set(x,y,x<25?A.Violet[2]:x>46?A.Violet[0]:A.Violet[1]);}
        p.Rect(24,38,24,1,A.Gold[2]);p.Rect(24,82,24,1,A.Gold[2]);p.Poly(A.Gold[2],36,46,44,58,36,74,28,58);p.Poly(A.Violet[3],36,52,40,58,36,66,32,58);p.Oval(36,59,2,2,161);
    }
    static void OpenBarrel(Pixels p,byte[] r)
    {
        p.Cast(39,100,12,3);Brush.Cylinder(p,26,78,20,22,A.WoodT);for(int x=30;x<46;x+=4)p.Rect(x,79,1,21,13);p.Rect(26,82,20,2,42);p.Rect(26,94,20,2,42);p.Oval(36,78,10,3,12);
        foreach(var (x,y) in new[]{(30,76),(36,74),(42,76),(33,72),(39,71)}){p.Oval(x,y,3,3,r[1]);p.Set(x-1,y-1,r[3]);}
    }
    static void OpenCrate(Pixels p)
    {
        p.Cast(39,100,14,3);Brush.Planks(p,22,82,28,18,false,681);p.Rect(22,80,28,3,12);p.Blob(36,80,12,3,A.Straw8,682,0,.1f);p.Rect(28,74,5,7,67);p.Rect(28,74,1,7,68);p.Rect(38,76,6,5,A.Blue[1]);p.Rect(38,76,6,1,A.Blue[3]);
        p.Line(50,82,58,76,2,14);
    }
    static void Trapdoor(Pixels p)
    {
        p.Rect(20,86,32,14,13);for(int x=22;x<50;x+=5){p.Rect(x,87,4,12,14);p.Rect(x,87,4,1,15);}p.Rect(20,90,32,1,42);p.Rect(20,96,32,1,42);p.Oval(46,93,2,2,A.IronT[2]);p.Oval(46,93,1,1,13);
    }
}
