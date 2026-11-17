using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Item icons, 24x24, lit from the upper-left with a dark contour. Families are
/// parametric: one recipe per shape, repeated across material tiers and colours.</summary>
public static class ItemIconArt
{
    static readonly (string Name,byte[] Ramp)[] Metals={("bronze",A.Bronze),("iron",A.IronT),("steel",A.Steel),("gold",A.Gold),("mythril",A.Mythril)};
    static readonly (string Name,byte[] Ramp)[] Hues={("red",A.Red),("blue",A.Blue),("green",A.Green),("yellow",A.Yellow),("orange",A.Orange),("violet",A.Violet),("pink",A.Pink),("teal",A.Teal)};
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("items",24,24,16);
        foreach(var (name,r) in Metals)
        {
            s.Add("sword-"+name,p=>Sword(p,r,13)).Add("dagger-"+name,p=>Sword(p,r,8)).Add("axe-"+name,p=>Axe(p,r,false)).Add("battleaxe-"+name,p=>Axe(p,r,true))
             .Add("mace-"+name,p=>Mace(p,r)).Add("spear-"+name,p=>Spear(p,r)).Add("hammer-"+name,p=>Hammer(p,r)).Add("shield-"+name,p=>Shield(p,r))
             .Add("helmet-"+name,p=>Helmet(p,r)).Add("cuirass-"+name,p=>Cuirass(p,r)).Add("boots-"+name,p=>Boots(p,r)).Add("gauntlets-"+name,p=>Gauntlets(p,r))
             .Add("ingot-"+name,p=>Ingot(p,r)).Add("ore-"+name,p=>Ore(p,r)).Add("key-"+name,p=>Key(p,r)).Add("ring-"+name,p=>Ring(p,r,GemFor(name)));
        }
        foreach(var (name,r) in Hues)
        {
            s.Add("potion-"+name,p=>Potion(p,r,0)).Add("flask-"+name,p=>Potion(p,r,1)).Add("vial-"+name,p=>Potion(p,r,2)).Add("gem-"+name,p=>Gem(p,r,false))
             .Add("jewel-"+name,p=>Gem(p,r,true)).Add("essence-"+name,p=>Essence(p,r)).Add("staff-"+name,p=>Staff(p,r,false)).Add("wand-"+name,p=>Staff(p,r,true))
             .Add("amulet-"+name,p=>Amulet(p,r)).Add("book-"+name,p=>Book(p,r)).Add("scroll-"+name,p=>Scroll(p,r)).Add("cloth-"+name,p=>Cloth(p,r))
             .Add("herb-"+name,p=>Herb(p,r)).Add("crystal-"+name,p=>Crystal(p,r)).Add("slime-"+name,p=>Slime(p,r)).Add("banner-"+name,p=>Banner(p,r));
        }
        // Bows, tools and adventuring gear.
        s.Add("bow-short",p=>Bow(p,A.WoodT,false)).Add("bow-long",p=>Bow(p,A.Leather,true)).Add("arrow",Arrow).Add("quiver",Quiver)
         .Add("pickaxe",p=>Tool(p,0)).Add("hatchet",p=>Tool(p,1)).Add("shovel",p=>Tool(p,2)).Add("hoe",p=>Tool(p,3))
         .Add("sickle",p=>Tool(p,4)).Add("fishing-rod",p=>Tool(p,5)).Add("torch",Torch).Add("lantern",Lantern)
         .Add("bomb",Bomb).Add("rope",Rope).Add("map",Map).Add("compass",Compass);
        // Food and drink.
        s.Add("apple",p=>Fruit(p,A.Red)).Add("pear",p=>Fruit(p,A.Green)).Add("orange",p=>Fruit(p,A.Orange)).Add("berries",Berries)
         .Add("bread",Bread).Add("cheese",Cheese).Add("meat",Meat).Add("fish",Fish)
         .Add("mushroom",Mushroom).Add("carrot",Carrot).Add("egg",Egg).Add("pie",Pie)
         .Add("stew",Stew).Add("wine",p=>Bottle(p,A.Red)).Add("ale",Ale).Add("honey",p=>Jar(p,A.Yellow));
        // Materials and trophies.
        s.Add("log",Log).Add("plank",Plank).Add("leather",Hide).Add("thread",Thread)
         .Add("bone",Bone).Add("feather",Feather).Add("coal",p=>Lump(p,A.Dark)).Add("stone",p=>Lump(p,new byte[]{20,21,22,24}))
         .Add("fang",Fang).Add("claw",Claw).Add("eye",Eye).Add("wing",Wing)
         .Add("shell",Shell).Add("pearl",Pearl).Add("scale",Scale).Add("horn",Horn);
        // Valuables and oddments.
        s.Add("coin",p=>Coins(p,1)).Add("coins",p=>Coins(p,3)).Add("pouch",Pouch).Add("crown",Crown)
         .Add("heart",p=>Heart(p,A.Red)).Add("star",Star).Add("skull",Skull).Add("candle",Candle)
         .Add("bell",Bell).Add("hourglass",Hourglass).Add("letter",Letter).Add("quill",Quill)
         .Add("lute",Lute).Add("dice",Dice).Add("bucket",Bucket).Add("chest-key-ring",KeyRing);
        return s;
    }

    static void Sword(Pixels p,byte[] r,int reach)
    {
        int tx=7+reach,ty=17-reach;
        p.Line(8,15,tx-1,ty+1,2,r[2]);p.Line(8,14,tx-1,ty,1,r[3]);p.Line(10,16,tx,ty+2,1,r[1]);p.Set(tx,ty,r[3]);p.Set(tx+1,ty-1,r[2]);
        p.Line(5,13,10,18,2,A.Gold[1]);p.Line(5,13,9,17,1,A.Gold[2]);p.Line(4,19,6,17,2,A.Leather[1]);p.Set(5,17,A.Leather[2]);p.Rect(2,20,2,2,A.Gold[2]);p.Set(2,20,A.Gold[3]);
    }
    static void Axe(Pixels p,byte[] r,bool twin)
    {
        p.Line(5,20,15,6,2,A.WoodT[1]);p.Line(5,19,14,6,1,A.WoodT[2]);
        p.Poly(r[2],13,4,19,3,21,8,18,12,14,9);p.Poly(r[1],18,12,21,8,20,11);p.Line(14,4,19,3,1,r[3]);p.Line(19,4,20,8,1,r[3]);
        if(twin){p.Poly(r[2],12,8,8,4,5,7,7,12,11,11);p.Line(8,4,5,7,1,r[3]);p.Line(7,12,11,11,1,r[0]);}
        p.Rect(4,20,2,2,r[1]);
    }
    static void Mace(Pixels p,byte[] r)
    {
        p.Line(5,20,13,10,2,A.WoodT[1]);p.Line(5,19,12,10,1,A.WoodT[2]);p.Rect(4,20,2,2,r[1]);
        p.Oval(15,8,4,4,r[1]);p.Oval(14,7,3,3,r[2]);p.Set(13,6,r[3]);p.Set(12,6,r[3]);
        foreach(var (x,y) in new[]{(15,2),(20,5),(21,10),(17,13),(10,5),(11,11)})p.Rect(x,y,2,2,r[2]);p.Set(15,2,r[3]);p.Set(10,5,r[3]);
    }
    static void Spear(Pixels p,byte[] r)
    {
        p.Line(3,21,16,8,2,A.WoodT[1]);p.Line(3,20,15,8,1,A.WoodT[2]);
        p.Poly(r[2],15,9,14,5,21,2,18,9);p.Line(15,6,20,3,1,r[3]);p.Line(17,9,20,4,1,r[0]);p.Rect(13,9,3,2,A.Red[1]);
    }
    static void Hammer(Pixels p,byte[] r)
    {
        p.Line(5,20,14,9,2,A.WoodT[1]);p.Line(5,19,13,9,1,A.WoodT[2]);
        p.Poly(r[1],9,6,15,2,21,8,15,12);p.Poly(r[2],9,6,15,2,18,5,12,9);p.Line(10,6,15,3,1,r[3]);p.Rect(4,20,2,2,r[1]);
    }
    static void Shield(Pixels p,byte[] r)
    {
        p.Poly(r[1],4,3,20,3,20,12,12,22,4,12);p.Poly(r[2],5,4,12,4,12,21,5,12);p.Rect(5,4,14,1,r[3]);p.Rect(5,4,1,8,r[3]);
        p.Rect(11,4,2,17,r[0]);p.Rect(5,10,14,2,r[0]);p.Oval(12,11,2,2,A.Gold[2]);p.Set(11,10,A.Gold[3]);
    }
    static void Helmet(Pixels p,byte[] r)
    {
        p.Oval(12,11,8,8,r[1]);p.Oval(11,10,6,6,r[2]);p.Rect(4,12,16,8,r[1]);p.Rect(5,12,8,7,r[2]);p.Rect(6,6,3,2,r[3]);p.Rect(5,9,1,5,r[3]);
        p.Rect(7,13,10,2,A.Dark[0]);p.Rect(11,13,2,7,r[0]);p.Rect(4,19,16,1,r[0]);p.Rect(11,2,2,4,A.Red[1]);p.Set(11,2,A.Red[2]);
    }
    static void Cuirass(Pixels p,byte[] r)
    {
        p.Poly(r[1],4,4,9,3,12,6,15,3,20,4,21,10,18,11,18,21,6,21,6,11,3,10);p.Poly(r[2],5,5,9,4,12,7,12,20,7,20,7,11,4,9);
        p.Rect(7,6,3,1,r[3]);p.Rect(7,12,1,7,r[3]);p.Rect(11,8,2,12,r[0]);p.Rect(7,15,10,1,r[0]);p.Rect(6,20,12,1,A.Leather[1]);p.Rect(11,20,2,1,A.Gold[2]);
    }
    static void Boots(Pixels p,byte[] r)
    {
        foreach(int x in new[]{3,13})
        {
            p.Rect(x+1,4,5,12,r[1]);p.Rect(x+1,4,2,12,r[2]);p.Rect(x,15,9,5,r[1]);p.Rect(x,15,5,3,r[2]);p.Rect(x,20,9,1,A.Leather[0]);
            p.Rect(x+1,4,5,2,A.Leather[2]);p.Set(x+1,6,r[3]);p.Set(x+1,16,r[3]);
        }
    }
    static void Gauntlets(Pixels p,byte[] r)
    {
        foreach(int x in new[]{2,13})
        {
            p.Rect(x+1,11,7,9,r[1]);p.Rect(x+1,11,3,9,r[2]);p.Rect(x,17,9,4,A.Leather[1]);p.Rect(x,17,9,1,A.Leather[2]);
            for(int f=0;f<4;f++){p.Rect(x+1+f*2,5+f%2,1,7,r[1]);p.Set(x+1+f*2,5+f%2,r[3]);}
            p.Rect(x+8,12,2,4,r[1]);p.Set(x+1,12,r[3]);
        }
    }
    static void Ingot(Pixels p,byte[] r)
    {
        p.Poly(r[1],3,14,7,9,20,9,21,14,21,18,3,18);p.Poly(r[2],7,9,20,9,18,13,5,13);p.Poly(r[0],18,13,21,14,21,18,18,18);
        p.Rect(7,9,12,1,r[3]);p.Rect(4,14,14,1,r[3]);p.Rect(3,18,18,1,r[0]);
    }
    static void Ore(Pixels p,byte[] r)
    {
        p.Blob(12,13,9,7,A.Stone8,(int)r[1],0,.08f,true);
        foreach(var (x,y,w) in new[]{(6,10,3),(11,8,4),(15,13,3),(9,15,3),(13,17,2)}){p.Rect(x,y,w,2,r[2]);p.Set(x,y,r[3]);p.Set(x+w-1,y+1,r[1]);}
    }
    static void Key(Pixels p,byte[] r)
    {
        p.Oval(7,7,4,4,r[2]);p.Oval(7,7,1,1,0);p.Set(5,5,r[3]);p.Set(6,4,r[3]);p.Rect(9,9,2,2,r[1]);
        p.Line(10,10,19,19,2,r[2]);p.Line(10,10,18,18,1,r[3]);p.Rect(16,20,2,2,r[1]);p.Rect(19,17,2,2,r[1]);p.Rect(13,17,2,2,r[1]);
    }
    static byte[] GemFor(string metal)=>metal switch{"bronze"=>A.Green,"iron"=>A.Red,"steel"=>A.Blue,"gold"=>A.Violet,_=>A.Yellow};
    static void Ring(Pixels p,byte[] r,byte[] gem)
    {
        p.Oval(12,14,7,7,r[1]);p.Oval(12,14,4,4,0);p.Oval(11,13,6,6,r[2]);p.Oval(12,14,4,4,0);p.Set(6,11,r[3]);p.Set(7,9,r[3]);
        p.Poly(gem[1],9,6,12,3,15,6,12,9);p.Poly(gem[2],9,6,12,3,12,9);p.Set(11,5,gem[3]);
    }
    static void Glass(Pixels p,int x,int y){p.Set(x,y,161);p.Set(x,y+1,161);p.Set(x+1,y,160);}
    static void Potion(Pixels p,byte[] r,int shape)
    {
        if(shape==0){p.Oval(12,15,7,6,160);p.Oval(12,16,6,5,r[1]);p.Oval(11,15,4,3,r[2]);p.Rect(6,12,13,1,r[3]);p.Rect(10,5,5,6,160);p.Rect(10,5,2,6,161);}
        if(shape==1){p.Poly(160,9,9,15,9,20,21,4,21);p.Poly(r[1],8,13,16,13,19,20,5,20);p.Poly(r[2],8,13,12,13,10,20,5,20);p.Rect(8,13,8,1,r[3]);p.Rect(10,4,4,6,160);p.Rect(10,4,1,6,161);}
        if(shape==2){p.Rect(9,7,6,14,160);p.Rect(10,11,4,9,r[1]);p.Rect(10,11,2,9,r[2]);p.Rect(10,11,4,1,r[3]);p.Rect(9,7,1,14,161);p.Rect(9,20,6,1,159);}
        p.Rect(shape==2?10:10,shape==2?4:2,shape==2?4:5,3,A.Leather[1]);p.Rect(10,shape==2?4:2,2,1,A.Leather[2]);
        Glass(p,shape==2?13:15,shape==2?13:14);p.Set(shape==0?9:11,shape==0?18:17,r[3]);
    }
    static void Gem(Pixels p,byte[] r,bool round)
    {
        if(round){p.Oval(12,12,8,8,r[1]);p.Oval(11,11,6,6,r[2]);p.Poly(r[3],7,9,10,5,13,7,9,11);p.Poly(r[0],13,19,19,13,17,17);p.Set(8,7,161);p.Set(9,6,161);}
        else{p.Poly(r[1],4,9,8,4,16,4,20,9,12,21);p.Poly(r[2],4,9,8,4,12,4,12,21);p.Poly(r[3],8,4,12,4,10,9,6,9);p.Poly(r[0],16,9,20,9,12,21);p.Line(4,9,20,9,1,r[3]);p.Set(7,6,161);}
    }
    static void Essence(Pixels p,byte[] r)
    {
        p.Oval(12,13,7,7,r[0]);p.Oval(12,13,5,5,r[1]);p.Oval(11,12,3,3,r[2]);p.Oval(10,11,1,1,r[3]);
        foreach(var (x,y) in new[]{(4,5),(19,6),(20,18),(5,19),(12,3)})p.Set(x,y,r[3]);p.Set(18,5,r[2]);p.Set(6,20,r[2]);
    }
    static void Staff(Pixels p,byte[] r,bool wand)
    {
        if(wand){p.Line(6,19,15,10,2,A.Dark[1]);p.Line(6,18,14,10,1,A.Dark[2]);p.Rect(5,19,2,2,A.Gold[2]);p.Oval(17,7,3,3,r[1]);p.Oval(16,6,2,2,r[2]);p.Set(15,5,r[3]);p.Set(21,3,r[3]);p.Set(12,4,r[2]);p.Set(20,11,r[2]);return;}
        p.Line(4,21,15,8,2,A.WoodT[1]);p.Line(4,20,14,8,1,A.WoodT[2]);
        p.Line(14,9,13,4,1,A.WoodT[1]);p.Line(15,9,20,8,1,A.WoodT[1]);p.Line(13,4,17,2,1,A.WoodT[2]);p.Line(20,8,21,4,1,A.WoodT[1]);
        p.Oval(17,5,3,3,r[1]);p.Oval(16,4,2,2,r[2]);p.Set(15,3,r[3]);
    }
    static void Amulet(Pixels p,byte[] r)
    {
        for(int i=0;i<=10;i++){double a=Math.PI*(1+i/10.0);int x=12+(int)Math.Round(Math.Cos(a)*8),y=11+(int)Math.Round(Math.Sin(a)*8);p.Set(x,y,i%2==0?A.Gold[2]:A.Gold[1]);}
        p.Line(4,11,10,15,1,A.Gold[1]);p.Line(20,11,14,15,1,A.Gold[1]);
        p.Poly(A.Gold[1],8,16,12,13,16,16,12,22);p.Poly(r[1],10,16,12,14,14,16,12,20);p.Poly(r[2],10,16,12,14,12,20);p.Set(11,16,r[3]);
    }
    static void Book(Pixels p,byte[] r)
    {
        p.Rect(5,4,14,17,r[1]);p.Rect(5,4,3,17,r[0]);p.Rect(8,4,11,1,r[2]);p.Rect(8,5,1,15,r[2]);p.Rect(8,20,11,1,161);p.Rect(18,5,1,15,160);
        p.Rect(10,7,7,1,A.Gold[2]);p.Rect(10,17,7,1,A.Gold[2]);p.Oval(13,12,2,2,A.Gold[2]);p.Set(12,11,A.Gold[3]);p.Rect(15,2,2,6,A.Red[1]);
    }
    static void Scroll(Pixels p,byte[] r)
    {
        p.Rect(6,5,12,14,65);p.Rect(6,5,2,14,59);p.Rect(16,5,2,14,57);p.Rect(5,3,14,3,58);p.Rect(5,3,14,1,59);p.Rect(5,18,14,3,58);p.Rect(5,20,14,1,57);
        p.Rect(4,3,1,3,57);p.Rect(19,3,1,3,57);p.Rect(4,18,1,3,57);p.Rect(19,18,1,3,57);
        for(int y=8;y<16;y+=2)p.Rect(9,y,6-(y%4),1,A.Dark[2]);p.Oval(12,19,2,2,r[1]);p.Set(11,18,r[3]);
    }
    static void Cloth(Pixels p,byte[] r)
    {
        p.Rect(4,7,16,12,r[1]);p.Rect(4,7,16,2,r[2]);p.Rect(4,7,2,12,r[2]);p.Rect(18,9,2,10,r[0]);p.Rect(4,18,16,1,r[0]);
        p.Oval(4,13,2,6,r[1]);p.Oval(4,13,1,4,r[0]);p.Rect(8,10,10,1,r[3]);p.Rect(8,14,10,1,r[0]);p.Poly(r[2],14,19,20,19,20,22,17,21);
    }
    static void Herb(Pixels p,byte[] r)
    {
        p.Line(12,21,12,9,1,A.Green[0]);p.Line(12,16,7,12,1,A.Green[0]);p.Line(12,14,17,10,1,A.Green[0]);
        foreach(var (x,y) in new[]{(12,6),(6,10),(18,8)}){p.Oval(x,y,3,3,r[1]);p.Oval(x-1,y-1,2,2,r[2]);p.Set(x-1,y-2,r[3]);}
        p.Poly(A.Green[1],12,20,7,18,9,15);p.Poly(A.Green[2],12,20,17,18,15,15);
    }
    static void Crystal(Pixels p,byte[] r)
    {
        foreach(var (x,top,w) in new[]{(7,9,3),(12,3,4),(17,8,3)})
        {p.Poly(r[1],x-w,20,x-w,top+4,x,top,x+w,top+4,x+w,20);p.Poly(r[2],x-w,20,x-w,top+4,x,top,x,20);p.Line(x-w+1,top+5,x-w+1,18,1,r[3]);p.Set(x,top+1,161);}
        p.Rect(3,19,18,2,20);p.Rect(3,19,18,1,22);
    }
    static void Slime(Pixels p,byte[] r)
    {
        p.Poly(r[1],3,20,4,13,8,8,12,6,16,8,20,13,21,20);p.Poly(r[2],4,19,5,13,9,9,12,8,12,19);p.Oval(9,11,2,2,r[3]);p.Set(8,10,161);
        p.Rect(8,15,2,2,A.Dark[0]);p.Rect(14,15,2,2,A.Dark[0]);p.Set(8,15,161);p.Set(14,15,161);p.Rect(3,20,18,1,r[0]);
    }
    static void Banner(Pixels p,byte[] r)
    {
        p.Rect(4,3,16,2,A.WoodT[1]);p.Rect(4,3,16,1,A.WoodT[3]);p.Set(3,3,A.Gold[2]);p.Set(20,3,A.Gold[2]);
        p.Poly(r[1],6,5,18,5,18,18,12,14,6,18);p.Poly(r[2],6,5,12,5,12,14,6,18);p.Rect(6,5,1,12,r[3]);p.Oval(12,9,2,2,A.Gold[2]);p.Set(11,8,A.Gold[3]);
    }
    static void Bow(Pixels p,byte[] r,bool tall)
    {
        int reach=tall?10:8;
        for(int i=-reach;i<=reach;i++){int x=6+(int)Math.Round(8*Math.Sqrt(Math.Max(0,1-i*i/(double)(reach*reach))));p.Rect(x,12+i,2,1,r[1]);p.Set(x,12+i,r[2]);}
        p.Line(6,12-reach,6,12+reach,1,160);p.Rect(13,11,3,3,A.Leather[1]);p.Set(13,11,A.Leather[2]);if(tall){p.Set(6,2,A.Gold[2]);p.Set(6,22,A.Gold[2]);}
    }
    static void Arrow(Pixels p)
    {
        p.Line(5,19,17,7,1,A.WoodT[2]);p.Poly(A.Steel[2],16,8,15,4,20,3,19,8);p.Line(16,5,19,4,1,A.Steel[3]);
        p.Poly(A.Red[1],3,21,3,17,7,17);p.Poly(A.Red[2],3,21,7,21,7,17);p.Line(4,20,6,18,1,161);
    }
    static void Quiver(Pixels p)
    {
        p.Poly(A.Leather[1],7,8,16,8,18,21,9,21);p.Poly(A.Leather[2],7,8,11,8,12,21,9,21);p.Rect(7,8,9,2,A.Leather[0]);p.Rect(8,14,9,1,A.Gold[1]);
        foreach(int x in new[]{8,11,14}){p.Line(x,8,x+1,3,1,A.WoodT[2]);p.Rect(x,2,2,2,x==11?A.Red[2]:A.Pale[2]);}
        p.Line(16,9,20,20,1,A.Leather[0]);
    }
    static void Tool(Pixels p,int kind)
    {
        p.Line(5,20,16,7,2,A.WoodT[1]);p.Line(5,19,15,7,1,A.WoodT[2]);
        var r=A.Steel;
        if(kind==0){p.Poly(r[2],8,6,13,3,19,4,21,9,18,7,14,7,10,9);p.Line(9,6,13,4,1,r[3]);p.Line(18,5,20,8,1,r[1]);}
        if(kind==1){p.Poly(r[2],13,4,19,4,21,9,17,11,14,8);p.Line(14,4,19,4,1,r[3]);p.Line(17,11,21,9,1,r[1]);}
        if(kind==2){p.Poly(r[2],14,3,20,2,21,9,16,10);p.Line(15,3,20,3,1,r[3]);p.Line(16,10,21,9,1,r[1]);}
        if(kind==3){p.Poly(r[2],13,5,20,5,20,8,15,9);p.Line(13,5,20,5,1,r[3]);}
        if(kind==4){for(int i=0;i<=8;i++){double a=-Math.PI*.9+i*.32;int x=15+(int)Math.Round(Math.Cos(a)*6),y=9+(int)Math.Round(Math.Sin(a)*6);p.Rect(x,y,2,2,r[2]);p.Set(x,y,r[3]);}}
        if(kind==5){p.Line(16,7,21,3,1,A.WoodT[2]);p.Line(21,3,21,14,1,160);p.Rect(20,14,2,2,A.Red[2]);p.Rect(9,14,3,3,r[1]);}
    }
    static void Torch(Pixels p)
    {
        p.Line(8,21,13,11,2,A.WoodT[1]);p.Line(8,20,12,11,1,A.WoodT[2]);p.Rect(11,9,5,3,A.Dark[1]);
        p.Poly(44,10,10,11,5,14,2,17,6,17,10);p.Poly(45,11,10,12,6,14,4,16,7,16,10);p.Poly(46,12,10,13,7,14,6,15,10);p.Set(14,8,47);p.Set(18,3,45);
    }
    static void Lantern(Pixels p)
    {
        p.Rect(8,7,9,13,A.IronT[0]);p.Rect(9,9,7,9,93);p.Rect(9,9,7,2,47);p.Rect(9,16,7,2,45);p.Rect(12,9,1,9,A.IronT[0]);
        p.Rect(7,6,11,2,A.IronT[1]);p.Rect(7,6,11,1,A.IronT[2]);p.Rect(7,19,11,2,A.IronT[1]);p.Rect(10,4,5,2,A.IronT[0]);
        for(int i=0;i<=6;i++){double a=Math.PI*(1+i/6.0);p.Set(12+(int)Math.Round(Math.Cos(a)*3),4+(int)Math.Round(Math.Sin(a)*2),A.IronT[2]);}
    }
    static void Bomb(Pixels p)
    {
        p.Oval(11,14,7,7,A.Dark[0]);p.Oval(10,13,5,5,A.Dark[1]);p.Oval(8,11,2,2,A.Dark[2]);p.Set(7,10,161);p.Rect(10,5,4,3,A.Dark[2]);
        p.Line(12,5,16,3,1,A.Leather[2]);p.Line(16,3,19,5,1,A.Leather[2]);p.Rect(19,4,2,2,46);p.Set(21,3,47);p.Set(20,7,44);p.Set(18,2,45);
    }
    static void Rope(Pixels p)
    {
        for(int ring=0;ring<3;ring++){p.Oval(12,13,8-ring*2,7-ring*2,ring%2==0?A.Leather[1]:A.Leather[2]);}
        p.Oval(12,13,2,1,0);for(int i=0;i<10;i++){double a=i*.63;p.Set(12+(int)Math.Round(Math.Cos(a)*7),13+(int)Math.Round(Math.Sin(a)*6),A.Leather[3]);}
        p.Line(18,8,21,4,2,A.Leather[1]);
    }
    static void Map(Pixels p)
    {
        p.Poly(65,3,6,9,4,15,6,21,4,21,18,15,20,9,18,3,20);p.Poly(59,3,6,9,4,9,18,3,20);p.Poly(57,15,6,21,4,21,18,15,20);
        p.Line(5,15,8,11,1,A.Red[1]);p.Line(8,11,13,13,1,A.Red[1]);p.Line(13,13,17,9,1,A.Red[1]);p.Rect(17,7,2,2,A.Red[2]);p.Rect(5,8,2,1,A.Green[1]);p.Rect(11,8,3,1,A.Blue[1]);
    }
    static void Compass(Pixels p)
    {
        p.Oval(12,13,8,8,A.Gold[1]);p.Oval(12,13,6,6,161);p.Oval(11,12,7,7,A.Gold[2]);p.Oval(12,13,6,6,161);
        p.Poly(A.Red[1],12,8,14,13,10,13);p.Poly(A.Steel[1],12,18,14,13,10,13);p.Set(12,13,A.Dark[0]);p.Rect(11,3,3,2,A.Gold[2]);p.Set(6,8,A.Gold[3]);
    }
    static void Fruit(Pixels p,byte[] r)
    {
        p.Oval(12,14,7,7,r[1]);p.Oval(11,13,5,5,r[2]);p.Oval(9,11,2,2,r[3]);p.Set(8,10,161);p.Oval(15,18,3,2,r[0]);
        p.Line(12,7,13,3,1,A.Leather[1]);p.Poly(A.Green[2],13,5,18,3,17,7);p.Set(16,4,A.Green[3]);
    }
    static void Berries(Pixels p)
    {
        foreach(var (x,y) in new[]{(8,14),(14,12),(11,18),(17,17)}){p.Oval(x,y,3,3,A.Violet[1]);p.Oval(x-1,y-1,2,2,A.Violet[2]);p.Set(x-1,y-2,161);}
        p.Line(11,9,13,4,1,A.Green[0]);p.Poly(A.Green[2],13,5,19,4,16,9);p.Poly(A.Green[1],12,6,6,4,8,9);
    }
    static void Bread(Pixels p)
    {
        p.Oval(12,14,9,6,164);p.Oval(12,13,8,5,165);p.Oval(11,12,6,3,166);foreach(int x in new[]{7,11,15})p.Line(x,10,x+2,14,1,164);p.Rect(4,19,16,1,164);
    }
    static void Cheese(Pixels p)
    {
        p.Poly(A.Yellow[1],3,17,13,7,21,12,21,19,3,19);p.Poly(A.Yellow[2],3,17,13,7,21,12,12,15);p.Line(3,17,12,15,1,A.Yellow[3]);p.Line(12,15,21,12,1,A.Yellow[3]);
        foreach(var (x,y) in new[]{(8,17),(15,16),(11,12),(17,13)})p.Oval(x,y,1,1,A.Yellow[0]);
    }
    static void Meat(Pixels p)
    {
        p.Oval(10,11,7,6,73);p.Oval(9,10,5,4,74);p.Oval(8,9,2,2,75);p.Line(14,15,19,20,2,58);p.Line(14,14,18,18,1,59);p.Oval(20,20,2,2,59);p.Oval(18,21,1,1,58);p.Oval(10,12,2,2,166);
    }
    static void Fish(Pixels p)
    {
        p.Poly(A.Blue[1],3,12,8,7,15,7,19,11,15,16,8,16);p.Poly(A.Blue[2],3,12,8,7,15,7,17,10,6,12);p.Poly(A.Blue[1],18,11,22,6,22,17);p.Poly(160,5,13,9,15,15,15,17,12);
        p.Set(6,10,A.Dark[0]);p.Set(6,9,161);p.Line(10,8,10,14,1,A.Blue[0]);p.Poly(A.Blue[0],10,7,13,4,14,7);
    }
    static void Mushroom(Pixels p)
    {
        p.Rect(10,12,5,9,58);p.Rect(10,12,2,9,59);p.Rect(13,13,2,8,57);p.Poly(A.Red[1],3,13,6,7,12,4,18,7,21,13);p.Poly(A.Red[2],3,13,6,7,12,4,12,13);
        foreach(var (x,y) in new[]{(7,9),(12,7),(16,10),(10,11)})p.Rect(x,y,2,2,161);p.Rect(3,13,18,1,A.Red[0]);
    }
    static void Carrot(Pixels p)
    {
        p.Poly(A.Orange[1],8,8,14,8,12,21,10,21);p.Poly(A.Orange[2],8,8,11,8,11,21,10,21);p.Line(9,11,11,11,1,A.Orange[0]);p.Line(10,15,12,15,1,A.Orange[0]);
        p.Poly(A.Green[1],11,8,7,2,9,7);p.Poly(A.Green[2],11,8,11,1,13,7);p.Poly(A.Green[1],11,8,16,3,13,8);
    }
    static void Egg(Pixels p){p.Oval(12,13,6,8,160);p.Oval(11,12,5,6,161);p.Oval(9,9,1,2,98);p.Oval(14,18,3,2,159);}
    static void Pie(Pixels p)
    {
        p.Oval(12,14,9,5,164);p.Oval(12,13,9,4,165);p.Oval(12,12,7,3,166);p.Line(8,12,16,12,1,164);p.Line(12,10,12,14,1,164);
        p.Oval(12,12,1,1,A.Red[1]);for(int x=4;x<21;x+=3)p.Set(x,16,164);p.Rect(4,18,16,1,A.Steel[1]);
    }
    static void Stew(Pixels p)
    {
        p.Poly(A.WoodT[1],3,11,21,11,18,20,6,20);p.Poly(A.WoodT[2],3,11,10,11,9,20,6,20);p.Oval(12,11,9,3,A.WoodT[3]);p.Oval(12,11,7,2,A.Orange[1]);
        p.Rect(8,10,2,2,A.Green[2]);p.Rect(13,11,2,1,A.Orange[2]);p.Rect(15,10,2,1,166);p.Line(10,7,11,4,1,160);p.Line(14,7,15,3,1,160);
    }
    static void Bottle(Pixels p,byte[] r)
    {
        p.Rect(8,10,8,11,A.Green[0]);p.Rect(9,13,6,7,r[1]);p.Rect(9,13,2,7,r[2]);p.Rect(8,10,1,11,A.Green[2]);p.Rect(10,3,4,8,A.Green[0]);p.Rect(10,3,1,8,A.Green[2]);
        p.Rect(10,2,4,2,A.Leather[2]);p.Rect(9,15,6,3,65);p.Rect(10,16,4,1,r[1]);
    }
    static void Ale(Pixels p)
    {
        p.Rect(5,8,11,13,A.WoodT[1]);p.Rect(5,8,3,13,A.WoodT[2]);p.Rect(5,11,11,1,A.IronT[1]);p.Rect(5,18,11,1,A.IronT[1]);
        p.Rect(16,10,4,2,A.WoodT[1]);p.Rect(18,12,2,5,A.WoodT[1]);p.Rect(16,17,4,2,A.WoodT[0]);p.Oval(10,7,6,3,161);p.Oval(8,6,3,2,98);p.Rect(13,9,2,3,161);
    }
    static void Jar(Pixels p,byte[] r)
    {
        p.Rect(6,8,12,13,160);p.Rect(7,10,10,10,r[1]);p.Rect(7,10,4,10,r[2]);p.Rect(7,10,10,1,r[3]);p.Rect(6,8,1,13,161);p.Rect(5,5,14,3,65);p.Rect(5,5,14,1,59);p.Rect(7,7,10,1,A.Leather[1]);Glass(p,15,12);
    }
    static void Log(Pixels p)
    {
        p.Poly(A.WoodT[1],4,9,18,6,20,14,6,18);p.Poly(A.WoodT[2],4,9,18,6,19,9,5,13);p.Oval(5,14,3,5,A.WoodT[3]);p.Oval(5,14,2,3,A.WoodT[2]);p.Set(5,14,A.WoodT[0]);
        p.Line(9,9,16,8,1,A.WoodT[0]);p.Line(9,14,17,12,1,A.WoodT[0]);
    }
    static void Plank(Pixels p)
    {
        foreach(var (y,d) in new[]{(5,0),(11,2),(17,1)}){p.Rect(3+d,y,17,5,A.WoodT[1]);p.Rect(3+d,y,17,1,A.WoodT[3]);p.Rect(3+d,y+4,17,1,A.WoodT[0]);p.Rect(8+d*2,y+2,5,1,A.WoodT[0]);}
    }
    static void Hide(Pixels p)
    {
        p.Poly(A.Leather[1],5,3,9,5,15,5,19,3,18,9,21,13,17,15,18,21,12,18,6,21,7,15,3,13,6,9);p.Poly(A.Leather[2],6,5,9,6,12,6,12,17,7,19,8,15,5,13,7,9);
        p.Line(8,8,11,8,1,A.Leather[3]);p.Line(14,10,17,12,1,A.Leather[0]);
    }
    static void Thread(Pixels p)
    {
        p.Rect(9,4,6,16,A.WoodT[2]);p.Rect(7,3,10,2,A.WoodT[1]);p.Rect(7,19,10,2,A.WoodT[1]);p.Rect(7,3,10,1,A.WoodT[3]);
        p.Rect(7,6,10,12,A.Pale[1]);p.Rect(7,6,3,12,A.Pale[2]);for(int y=7;y<18;y+=2)p.Rect(7,y,10,1,A.Pale[0]);p.Line(16,12,20,20,1,A.Pale[2]);
    }
    static void Bone(Pixels p)
    {
        p.Line(7,17,17,7,3,58);p.Line(7,16,16,7,1,59);foreach(var (x,y) in new[]{(5,16),(7,19),(16,5),(19,7)})p.Oval(x,y,2,2,59);p.Oval(8,20,1,1,57);p.Oval(20,8,1,1,57);
    }
    static void Feather(Pixels p)
    {
        p.Poly(A.Pale[1],5,19,8,10,14,4,19,3,18,9,13,15);p.Poly(A.Pale[2],5,19,8,10,14,4,17,4,9,14);p.Line(4,21,18,4,1,A.Pale[0]);
        p.Line(10,10,12,12,1,A.Pale[0]);p.Line(13,7,15,9,1,A.Pale[0]);p.Set(18,3,A.Blue[2]);p.Set(17,5,A.Blue[1]);
    }
    static void Lump(Pixels p,byte[] r)
    {
        p.Poly(r[1],4,16,6,9,12,6,18,8,21,15,17,20,8,20);p.Poly(r[2],4,16,6,9,12,6,13,12,8,15);p.Poly(r[0],13,12,21,15,17,20,12,19);p.Line(7,9,12,7,1,r[3]);p.Set(15,10,r[3]);
    }
    static void Fang(Pixels p){p.Poly(58,7,4,15,3,18,8,14,21,9,13);p.Poly(59,7,4,12,4,12,18,9,13);p.Rect(7,4,9,2,57);p.Set(9,7,98);}
    static void Claw(Pixels p)
    {
        for(int i=0;i<3;i++){int x=5+i*5;p.Poly(A.Dark[1],x,6,x+4,5,x+5,12,x+2,21);p.Poly(A.Dark[2],x,6,x+2,6,x+2,18);p.Set(x+1,8,A.Dark[3]);}
        p.Rect(4,3,16,4,A.Leather[1]);p.Rect(4,3,16,1,A.Leather[2]);
    }
    static void Eye(Pixels p)
    {
        p.Oval(12,12,9,7,161);p.Oval(12,13,8,5,160);p.Oval(12,12,4,4,A.Green[1]);p.Oval(12,12,2,2,A.Dark[0]);p.Set(10,10,98);p.Line(4,9,7,7,1,A.Red[2]);p.Line(18,16,20,14,1,A.Red[2]);
    }
    static void Wing(Pixels p)
    {
        p.Poly(A.Dark[1],3,5,10,3,20,6,21,12,17,11,18,17,13,14,13,20,8,15,5,12);p.Poly(A.Dark[2],3,5,10,3,18,6,10,8,6,11);
        p.Line(4,6,17,11,1,A.Dark[0]);p.Line(6,8,13,14,1,A.Dark[0]);p.Line(7,10,9,15,1,A.Dark[0]);p.Set(5,5,A.Dark[3]);
    }
    static void Shell(Pixels p)
    {
        p.Poly(A.Pink[1],12,20,3,11,6,5,12,3,18,5,21,11);p.Poly(A.Pink[2],12,20,3,11,6,5,12,3);foreach(int x in new[]{6,9,12,15,18})p.Line(12,20,x,5,1,A.Pink[0]);
        p.Rect(9,20,6,2,A.Pink[1]);p.Set(7,7,161);
    }
    static void Pearl(Pixels p){p.Oval(12,13,7,7,160);p.Oval(11,12,5,5,161);p.Oval(9,10,2,2,98);p.Oval(15,17,2,2,A.Pink[2]);p.Set(13,15,A.Blue[3]);}
    static void Scale(Pixels p)
    {
        p.Poly(A.Teal[1],12,3,20,9,18,18,12,21,6,18,4,9);p.Poly(A.Teal[2],12,3,12,21,6,18,4,9);p.Line(12,4,12,20,1,A.Teal[3]);p.Line(6,10,11,14,1,A.Teal[0]);p.Line(18,10,13,14,1,A.Teal[0]);p.Set(8,8,161);
    }
    static void Horn(Pixels p)
    {
        p.Poly(58,4,18,6,11,12,6,20,3,17,9,13,14,10,20);p.Poly(59,4,18,6,11,12,6,18,4,10,11,7,18);p.Line(8,12,11,15,1,57);p.Line(12,8,15,11,1,57);p.Rect(4,18,7,3,A.Gold[1]);p.Rect(4,18,7,1,A.Gold[3]);
    }
    static void Coins(Pixels p,int stack)
    {
        for(int i=0;i<stack;i++){int y=17-i*4;p.Oval(11+i,y,7,3,A.Gold[0]);p.Oval(11+i,y-1,7,3,A.Gold[2]);p.Oval(10+i,y-2,4,1,A.Gold[3]);p.Rect(10+i,y-1,3,1,A.Gold[1]);}
        if(stack==1){p.Oval(12,12,8,8,A.Gold[1]);p.Oval(11,11,6,6,A.Gold[2]);p.Rect(11,8,2,8,A.Gold[1]);p.Rect(9,10,6,1,A.Gold[1]);p.Set(7,8,A.Gold[3]);p.Set(8,7,A.Gold[3]);}
    }
    static void Pouch(Pixels p)
    {
        p.Poly(A.Leather[1],6,9,18,9,21,17,18,21,6,21,3,17);p.Poly(A.Leather[2],6,9,12,9,11,21,6,21,3,17);p.Rect(8,7,8,3,A.Leather[0]);p.Rect(7,8,10,1,A.Gold[2]);
        p.Poly(A.Leather[1],8,7,6,3,11,5);p.Poly(A.Leather[1],16,7,18,3,13,5);p.Oval(12,15,2,2,A.Gold[2]);p.Set(11,14,A.Gold[3]);
    }
    static void Crown(Pixels p)
    {
        p.Poly(A.Gold[1],4,19,3,7,8,12,12,5,16,12,21,7,20,19);p.Poly(A.Gold[2],4,19,3,7,8,12,12,5,12,19);p.Rect(4,17,16,3,A.Gold[2]);p.Rect(4,17,16,1,A.Gold[3]);p.Rect(4,20,16,1,A.Gold[0]);
        p.Rect(11,14,2,2,A.Red[2]);p.Rect(6,15,2,2,A.Blue[2]);p.Rect(16,15,2,2,A.Green[2]);p.Set(12,5,A.Gold[3]);p.Set(3,7,A.Gold[3]);
    }
    static void Heart(Pixels p,byte[] r)
    {
        p.Poly(r[1],12,21,3,11,3,7,7,4,12,8,17,4,21,7,21,11);p.Poly(r[2],12,20,4,11,4,7,7,5,12,9);p.Oval(7,8,2,2,r[3]);p.Set(6,7,161);
    }
    static void Star(Pixels p)
    {
        p.Poly(A.Yellow[1],12,2,15,9,22,9,16,14,19,21,12,17,5,21,8,14,2,9,9,9);p.Poly(A.Yellow[2],12,2,12,17,5,21,8,14,2,9,9,9);p.Set(11,6,A.Yellow[3]);p.Set(10,9,A.Yellow[3]);
    }
    static void Skull(Pixels p)
    {
        p.Oval(12,10,8,7,58);p.Oval(11,9,6,5,59);p.Rect(7,14,10,6,58);p.Rect(7,14,5,5,59);p.Oval(8,11,2,2,A.Dark[0]);p.Oval(15,11,2,2,A.Dark[0]);
        p.Poly(A.Dark[0],12,13,10,16,13,16);for(int x=8;x<16;x+=2)p.Rect(x,18,1,2,A.Dark[1]);
    }
    static void Candle(Pixels p)
    {
        p.Rect(9,10,6,10,161);p.Rect(13,10,2,10,160);p.Rect(9,10,6,1,160);p.Set(10,13,160);p.Rect(6,19,12,2,A.Bronze[2]);p.Rect(6,19,12,1,A.Bronze[3]);p.Rect(11,8,1,2,A.Dark[0]);
        p.Poly(45,9,8,12,2,15,8);p.Poly(46,10,8,12,4,14,8);p.Set(12,7,47);
    }
    static void Bell(Pixels p)
    {
        p.Poly(A.Gold[1],5,17,7,9,10,5,14,5,17,9,19,17);p.Poly(A.Gold[2],5,17,7,9,10,5,12,5,11,17);p.Rect(4,17,16,2,A.Gold[2]);p.Rect(4,17,16,1,A.Gold[3]);
        p.Rect(11,2,2,4,A.Gold[1]);p.Oval(12,20,2,1,A.Gold[0]);p.Set(8,9,A.Gold[3]);
    }
    static void Hourglass(Pixels p)
    {
        p.Rect(5,3,14,2,A.WoodT[1]);p.Rect(5,19,14,2,A.WoodT[1]);p.Rect(5,3,14,1,A.WoodT[3]);p.Poly(161,7,5,17,5,12,12);p.Poly(161,7,19,17,19,12,12);
        p.Poly(A.Yellow[2],9,7,15,7,12,11);p.Poly(A.Yellow[2],8,19,16,19,12,15);p.Rect(12,12,1,3,A.Yellow[2]);p.Rect(6,5,1,14,A.WoodT[2]);p.Rect(17,5,1,14,A.WoodT[0]);
    }
    static void Letter(Pixels p)
    {
        p.Rect(3,6,18,13,65);p.Rect(3,6,18,1,59);p.Rect(3,18,18,1,57);p.Line(3,6,12,13,1,57);p.Line(21,6,12,13,1,57);p.Oval(12,14,2,2,A.Red[1]);p.Set(11,13,A.Red[3]);
    }
    static void Quill(Pixels p)
    {
        p.Poly(A.Pale[1],6,17,10,8,16,3,20,3,18,9,12,15);p.Poly(A.Pale[2],6,17,10,8,16,3,18,4,10,13);p.Line(4,21,18,5,1,A.Pale[0]);p.Rect(3,20,2,2,A.Dark[0]);
        p.Rect(15,16,6,5,A.Dark[1]);p.Rect(15,16,6,1,A.Dark[3]);p.Rect(16,14,4,2,A.Dark[2]);
    }
    static void Lute(Pixels p)
    {
        p.Oval(9,15,6,6,A.WoodT[1]);p.Oval(8,14,5,5,A.WoodT[2]);p.Oval(9,15,2,2,A.Dark[0]);p.Line(12,11,19,4,2,A.WoodT[0]);p.Line(12,11,18,4,1,A.WoodT[2]);
        p.Rect(18,2,3,3,A.WoodT[1]);p.Line(7,17,18,5,1,160);p.Set(6,12,A.WoodT[3]);
    }
    static void Dice(Pixels p)
    {
        foreach(var (x,y,r) in new[]{(3,9,A.Pale),(12,5,A.Red)})
        {p.Rect(x,y,9,9,r[2]);p.Rect(x,y,9,1,r[3]);p.Rect(x+7,y+1,2,8,r[1]);p.Rect(x,y+8,9,1,r[0]);
         foreach(var (dx,dy) in new[]{(2,2),(6,6),(4,4)})p.Set(x+dx,y+dy,r==A.Pale?A.Dark[0]:(byte)161);}
    }
    static void Bucket(Pixels p)
    {
        p.Poly(A.WoodT[1],5,9,19,9,17,21,7,21);p.Poly(A.WoodT[2],5,9,11,9,11,21,7,21);p.Rect(5,11,14,1,A.IronT[1]);p.Rect(6,18,12,1,A.IronT[1]);p.Oval(12,9,7,2,A.Blue[2]);p.Oval(11,9,4,1,A.Blue[3]);
        for(int i=0;i<=8;i++){double a=Math.PI*(1+i/8.0);p.Set(12+(int)Math.Round(Math.Cos(a)*7),9+(int)Math.Round(Math.Sin(a)*6),A.IronT[2]);}
    }
    static void KeyRing(Pixels p)
    {
        p.Oval(12,8,6,6,A.IronT[1]);p.Oval(12,8,4,4,0);p.Oval(11,7,5,5,A.IronT[2]);p.Oval(12,8,4,4,0);
        foreach(var (x,r) in new[]{(7,A.Gold),(12,A.Bronze),(17,A.Steel)}){p.Rect(x,13,2,8,r[2]);p.Rect(x,13,1,8,r[3]);p.Rect(x+2,17,2,1,r[1]);p.Rect(x+2,19,2,1,r[1]);p.Oval(x,12,1,1,r[1]);}
    }
}
