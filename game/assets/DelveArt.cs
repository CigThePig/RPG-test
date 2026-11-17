using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>More for the ruins and the deep places: doors and stairs, traps, treasure,
/// tombs, cages, crystals and cave growth, statues, dark altars and mana springs.</summary>
public static class DelveArt
{
    static readonly (string Name,byte[] Ramp)[] Hues={("red",A.Red),("blue",A.Blue),("green",A.Green),("violet",A.Violet)};
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("delve",72,104,8);
        s.Add("door-wood",p=>Door(p,0,false)).Add("door-wood-open",p=>Door(p,0,true)).Add("door-iron",p=>Door(p,1,false)).Add("door-iron-open",p=>Door(p,1,true))
         .Add("door-rune",p=>Door(p,2,false)).Add("door-rune-open",p=>Door(p,2,true)).Add("stairs-down",StairsDown).Add("ladder-hole",LadderHole);
        s.Add("spikes-down",p=>Spikes(p,false),true,new Vector2(36,94)).Add("spikes-up",p=>Spikes(p,true)).Add("plate-up",p=>Plate(p,false),true,new Vector2(36,94)).Add("plate-down",p=>Plate(p,true),true,new Vector2(36,94))
         .Add("dart-trap",DartTrap).Add("flame-jet-1",p=>FlameJet(p,0)).Add("flame-jet-2",p=>FlameJet(p,2)).Add("wall-lever",WallLever);
        s.Add("chest-iron",p=>Chest(p,A.Steel,false)).Add("chest-iron-open",p=>Chest(p,A.Steel,true)).Add("chest-gold",p=>Chest(p,A.Gold,false)).Add("chest-gold-open",p=>Chest(p,A.Gold,true))
         .Add("mimic",Mimic).Add("treasure-small",p=>Treasure(p,0)).Add("treasure-large",p=>Treasure(p,1)).Add("treasure-hoard",p=>Treasure(p,2));
        s.Add("sarcophagus",p=>Sarcophagus(p,false)).Add("sarcophagus-open",p=>Sarcophagus(p,true)).Add("coffin",Coffin).Add("urn-ashes",Urn)
         .Add("skeleton-sitting",Skeleton).Add("skull-pile",SkullPile).Add("bone-heap",BoneHeap).Add("ribcage-large",RibCage);
        s.Add("cage",Cage).Add("cage-hanging",HangingCage).Add("shackles",Shackles).Add("iron-maiden",IronMaiden)
         .Add("web-corner",p=>Web(p,false)).Add("web-large",p=>Web(p,true)).Add("egg-sac",EggSac).Add("nest",Nest);
        foreach(var (name,r) in Hues)s.Add("crystal-cluster-"+name,p=>Crystals(p,r));
        s.Add("stalagmite-1",p=>Stalagmite(p,0)).Add("stalagmite-2",p=>Stalagmite(p,1)).Add("stalagmite-3",p=>Stalagmite(p,2)).Add("obsidian-spire",Obsidian);
        s.Add("glowcap-giant",GlowCap).Add("cave-moss",CaveMoss).Add("root-tangle",Roots).Add("rubble-large",Rubble)
         .Add("pillar-broken-1",p=>BrokenPillar(p,0)).Add("pillar-broken-2",p=>BrokenPillar(p,1)).Add("statue-knight",p=>Statue(p,0)).Add("statue-broken",p=>Statue(p,1));
        s.Add("gargoyle",Gargoyle).Add("dark-altar",DarkAltar).Add("dark-throne",DarkThrone).Add("evil-idol",EvilIdol)
         .Add("mana-spring",p=>Spring(p,A.Blue)).Add("healing-spring",p=>Spring(p,A.Green)).Add("dark-well",DarkWell).Add("candle-stand",CandleStand);
        foreach(var (name,r) in Hues)s.Add("teleporter-"+name,p=>Teleporter(p,r),true,new Vector2(36,92));
        foreach(var (name,r) in Hues)s.Add("slime-pool-"+name,p=>Pool(p,r),true,new Vector2(36,94));
        s.Add("barrel-explosive",ExplosiveBarrel).Add("bottle-rack",BottleRack).Add("table-skull",SkullTable).Add("bookshelf-ruined",RuinedShelf)
         .Add("lava-vent-1",p=>LavaVent(p,0)).Add("lava-vent-2",p=>LavaVent(p,2)).Add("mine-timber",Timber).Add("boss-door",BossDoor);
        return s;
    }

    static void Door(Pixels p,int kind,bool open)
    {
        // A doorway in a stone frame: plank, iron-bound or rune-sealed.
        Brush.Masonry(p,14,52,10,48,701);Brush.Masonry(p,48,52,10,48,702);Brush.Box(p,12,44,48,9,A.Stone);p.Rect(24,53,24,47,A.Ink);
        if(open){p.Poly(kind==1?(byte)42:kind==2?(byte)117:(byte)14,24,54,30,58,30,100,24,100);p.Rect(24,54,1,46,kind==1?(byte)43:kind==2?(byte)119:(byte)16);p.Rect(26,100-4,22,4,156);return;}
        if(kind==0){Brush.Planks(p,24,54,24,46,true,703);p.Rect(24,62,24,2,42);p.Rect(24,88,24,2,42);p.Rect(43,76,3,3,29);}
        if(kind==1){p.Rect(24,54,24,46,42);p.Rect(24,54,24,1,43);p.Rect(24,54,1,46,43);p.Rect(45,55,3,45,41);for(int y=58;y<98;y+=10)for(int x=27;x<46;x+=6)p.Set(x,y,23);p.Rect(24,76,24,2,41);p.Rect(33,72,6,10,41);p.Oval(36,75,1,1,A.Ink);p.Rect(36,76,1,3,A.Ink);}
        if(kind==2){p.Rect(24,54,24,46,117);p.Rect(24,54,24,1,119);p.Rect(24,54,1,46,119);p.Rect(46,55,2,45,156);p.Oval(36,74,8,10,118);p.Oval(36,74,6,8,117);p.Rect(35,64,2,20,55);p.Rect(30,72,12,2,55);p.Line(31,66,41,82,1,54);p.Line(41,66,31,82,1,54);p.Set(36,74,56);}
    }
    static void StairsDown(Pixels p)
    {
        Brush.Box(p,14,84,44,16,A.Stone);p.Rect(18,86,36,14,A.Ink);for(int i=0;i<5;i++){p.Rect(20+i*3,87+i*2,32-i*6,3,i<2?(byte)22:i<4?(byte)21:(byte)20);p.Rect(20+i*3,87+i*2,32-i*6,1,i<2?(byte)24:(byte)22);}
        p.Rect(14,82,44,3,22);p.Rect(14,82,44,1,24);
    }
    static void LadderHole(Pixels p)
    {
        p.Oval(36,94,18,7,20);p.Oval(36,94,15,5,A.Ink);p.Oval(36,92,16,5,22);p.Oval(36,94,15,5,A.Ink);
        p.Rect(30,76,2,22,14);p.Rect(40,76,2,22,13);for(int y=80;y<98;y+=5)p.Rect(31,y,10,2,15);p.Set(30,76,16);
    }
    static void Spikes(Pixels p,bool up)
    {
        p.Rect(24,88,24,12,20);p.Rect(24,88,24,1,22);p.Rect(24,99,24,1,19);
        for(int row=0;row<2;row++)for(int i=0;i<4;i++)
        {
            int x=27+i*6+row*2,y=92+row*5;p.Oval(x,y,1,1,A.Ink);
            if(up){p.Poly(A.Steel[1],x-2,y,x,y-16,x+2,y);p.Line(x-1,y-1,x,y-15,1,A.Steel[3]);}
        }
    }
    static void Plate(Pixels p,bool down)
    {
        p.Rect(24,88,24,12,20);p.Rect(26,down?91:89,20,down?8:9,down?(byte)21:(byte)22);p.Rect(26,down?91:89,20,1,down?(byte)22:(byte)24);p.Rect(26,down?98:97,20,1,19);
        p.Poly(down?(byte)20:(byte)54,36,down?93:91,40,down?95:93,36,down?97:95,32,down?95:93);
    }
    static void DartTrap(Pixels p)
    {
        Brush.Masonry(p,24,60,24,40,711);p.Rect(28,72,16,10,20);foreach(int x in new[]{31,36,41}){p.Oval(x,77,1,1,A.Ink);}p.Line(36,77,52,82,1,A.Steel[3]);p.Rect(52,82,3,1,A.Red[2]);p.Set(27,66,54);p.Set(44,66,54);
    }
    static void FlameJet(Pixels p,int frame)
    {
        p.Rect(26,92,20,8,20);p.Rect(26,92,20,1,22);p.Oval(36,95,6,2,A.Ink);for(int x=30;x<43;x+=3)p.Rect(x,94,1,3,41);Brush.Flame(p,36,94,14,44,frame);
    }
    static void WallLever(Pixels p)
    {
        Brush.Box(p,28,70,16,18,A.Stone);p.Rect(34,74,4,10,A.Ink);p.Line(36,79,46,68,3,42);p.Line(36,78,45,68,1,43);p.Oval(47,66,3,3,A.Red[1]);p.Set(46,65,A.Red[3]);
    }
    static void Chest(Pixels p,byte[] r,bool open)
    {
        p.Cast(40,100,17,2);
        p.Rect(20,84,26,16,r[1]);p.Poly(r[0],46,84,52,81,52,96,46,100);p.Rect(20,84,26,1,r[3]);p.Rect(20,98,26,2,r[0]);p.Rect(24,84,3,16,A.Gold[1]);p.Rect(39,84,3,16,A.Gold[1]);p.Rect(24,84,1,16,A.Gold[3]);
        for(int y=88;y<98;y+=5)for(int x=21;x<46;x+=6)p.Set(x,y,r[3]);
        if(open)
        {
            p.Poly(r[0],20,84,26,81,52,81,46,84);p.Rect(22,82,23,2,A.Ink);p.Poly(r[1],22,66,48,66,52,70,52,81,26,81,22,78);p.Rect(24,68,22,11,r[2]);p.Rect(24,68,22,1,r[3]);
            p.Rect(24,80,20,3,A.Gold[2]);foreach(var (x,y) in new[]{(26,79),(32,78),(38,79),(42,78)}){p.Rect(x,y,3,2,A.Gold[3]);}p.Rect(30,77,2,2,A.Red[2]);p.Rect(36,77,2,2,A.Blue[2]);p.Set(28,74,161);p.Set(40,72,161);
        }
        else
        {
            p.Poly(r[2],20,84,20,78,24,74,44,74,46,78,46,84);p.Poly(r[0],46,84,46,78,44,74,50,72,52,76,52,81);p.Rect(24,74,20,1,r[3]);p.Rect(21,78,24,1,r[3]);
            p.Rect(24,74,3,10,A.Gold[1]);p.Rect(39,74,3,10,A.Gold[1]);p.Rect(30,81,6,7,A.Gold[2]);p.Rect(30,81,6,1,A.Gold[3]);p.Rect(32,84,2,2,A.Ink);
        }
    }
    static void Mimic(Pixels p)
    {
        Chest(p,A.WoodT,true);p.Rect(22,80,24,5,A.Red[0]);p.Poly(A.Red[1],28,84,40,84,38,96,30,96);p.Poly(A.Red[2],28,84,34,84,33,96,30,96);
        for(int x=22;x<46;x+=4){p.Poly(161,x,80,x+3,80,x+1,76);p.Poly(161,x,84,x+3,84,x+1,88);}p.Oval(28,72,3,3,151);p.Oval(40,72,3,3,151);p.Rect(28,71,1,3,A.Ink);p.Rect(40,71,1,3,A.Ink);
    }
    static void Treasure(Pixels p,int size)
    {
        int rx=size==0?10:size==1?17:26,ry=size==0?5:size==1?9:14;
        p.Cast(39,100,rx+2,3);p.Blob(36,100-ry,rx,ry,new byte[]{28,28,28,29,29,29,30,30},721+size,0,.08f,true);
        for(int i=0;i<10+size*14;i++){int x=36-rx+2+(int)(A.Hash(i,size,31)%(uint)(rx*2-3)),y=100-ry*2+3+(int)(A.Hash(i,size,32)%(uint)(ry*2-3));if(p.At(x,y) is not (28 or 29 or 30))continue;p.Rect(x,y,3,1,30);p.Rect(x,y+1,3,1,28);}
        foreach(var (dx,c) in new[]{(-5,A.Red),(4,A.Blue),(0,A.Green)}){int x=36+dx*(size+1)/2,y=100-ry-(size*2);p.Poly(c[1],x,y-3,x+3,y,x,y+3,x-3,y);p.Poly(c[2],x,y-3,x,y+3,x-3,y);p.Set(x-1,y-1,161);}
        if(size>0){p.Rect(44,100-ry*2-2,8,5,A.Gold[2]);p.Poly(A.Gold[2],44,100-ry*2-2,44,100-ry*2-6,47,100-ry*2-3,48,100-ry*2-7,50,100-ry*2-3,52,100-ry*2-6,52,100-ry*2-2);}
        if(size>1){p.Rect(18,84,3,14,A.Steel[2]);p.Rect(16,82,7,2,A.Gold[1]);p.Oval(56,92,5,5,A.Bronze[1]);p.Oval(55,91,3,3,A.Bronze[3]);p.Rect(26,76,6,8,A.Violet[1]);p.Rect(26,76,6,1,A.Violet[3]);}
    }
    static void Sarcophagus(Pixels p,bool open)
    {
        p.Cast(40,100,22,3);Brush.Box(p,14,84,44,16,A.Sand);for(int x=18;x<56;x+=8){p.Rect(x,88,4,8,49);p.Rect(x+1,89,2,6,29);}
        if(open){p.Rect(16,82,40,3,A.Ink);p.Poly(51,16,76,60,66,62,72,18,82);p.Poly(53,16,76,60,66,60,68,16,78);p.Oval(30,82,4,2,58);p.Set(29,82,A.Ink);return;}
        Brush.Box(p,12,76,48,9,A.Sand);p.Oval(24,78,5,3,29);p.Set(22,78,A.Ink);p.Set(26,78,A.Ink);p.Rect(30,77,24,5,51);p.Rect(34,78,2,3,142);p.Rect(40,78,2,3,138);p.Rect(46,78,2,3,142);p.Rect(12,76,48,1,53);
    }
    static void Coffin(Pixels p)
    {
        p.Cast(40,100,18,3);p.Poly(13,18,88,26,80,50,80,56,88,52,100,22,100);p.Poly(14,18,88,26,80,50,80,56,88,50,94,24,94);p.Line(26,81,50,81,1,16);p.Rect(35,82,2,10,A.Gold[1]);p.Rect(31,85,10,2,A.Gold[1]);p.Rect(22,96,30,1,12);
    }
    static void Urn(Pixels p){p.Cast(38,100,8,2);p.Blob(36,88,8,11,A.Dark8,731,0,.02f,true);p.Rect(32,74,8,3,A.Dark[2]);p.Rect(31,72,10,2,A.Dark[3]);p.Rect(29,86,14,1,A.Gold[1]);p.Rect(34,68,4,4,A.Dark[2]);p.Set(33,80,A.Dark[3]);}
    static void Skull(Pixels p,int x,int y){p.Oval(x,y-4,3,3,58);p.Rect(x-2,y-2,5,2,58);p.Rect(x-3,y-6,3,2,59);p.Set(x-1,y-3,A.Ink);p.Set(x+1,y-3,A.Ink);p.Rect(x-1,y,3,1,57);}
    static void Skeleton(Pixels p)
    {
        p.Cast(39,100,14,2);Skull(p,36,74);for(int i=0;i<4;i++){p.Rect(31,77+i*3,11,1,58);p.Set(31,77+i*3,59);}p.Rect(35,76,2,14,58);
        p.Line(31,78,24,88,1,58);p.Line(24,88,28,96,1,58);p.Line(41,78,48,86,1,57);p.Line(48,86,46,96,1,57);p.Line(33,90,26,98,2,58);p.Line(39,90,48,98,2,57);p.Rect(22,98,6,2,58);p.Rect(46,98,6,2,57);
        p.Line(50,70,54,98,1,A.Steel[2]);p.Rect(48,92,5,1,A.Gold[1]);
    }
    static void SkullPile(Pixels p){p.Cast(39,100,14,2);foreach(var (x,y) in new[]{(26,99),(34,99),(42,99),(48,99),(30,92),(38,92),(45,93),(34,85),(41,86),(37,79)})Skull(p,x,y);}
    static void BoneHeap(Pixels p)
    {
        p.Cast(39,100,16,2);for(int i=0;i<12;i++){int x=22+(int)(A.Hash(i,0,41)%26),y=88+(int)(A.Hash(i,1,41)%10),ex=x+4+(int)(A.Hash(i,2,41)%8),ey=y-3+(int)(A.Hash(i,3,41)%7);p.Line(x,y,ex,ey,1,i%2==0?(byte)58:(byte)57);p.Rect(x-1,y-1,2,2,59);p.Rect(ex,ey,2,2,59);}
        Skull(p,36,90);
    }
    static void RibCage(Pixels p)
    {
        p.Cast(39,100,20,3);p.Line(16,90,56,86,3,58);for(int i=0;i<6;i++){int x=20+i*6,h=22-Math.Abs(i-2)*3;for(int k=0;k<=12;k++){double a=Math.PI*(1+k/12.0);p.Rect(x+(int)(Math.Cos(a)*4)+4,90+(int)(Math.Sin(a)*h),2,2,k<6?(byte)59:(byte)58);}p.Line(x,90,x-1,100,1,57);p.Line(x+8,89,x+9,100,1,57);}
    }
    static void Cage(Pixels p)
    {
        p.Cast(40,100,16,3);p.Rect(20,60,32,3,42);p.Rect(20,60,32,1,43);p.Rect(20,97,32,3,41);for(int x=21;x<52;x+=5){p.Rect(x,63,2,34,42);p.Rect(x,63,1,34,43);}p.Rect(20,78,32,2,42);p.Rect(44,76,5,6,A.Gold[1]);p.Rect(46,78,1,2,A.Ink);
    }
    static void HangingCage(Pixels p)
    {
        for(int y=10;y<40;y+=3){p.Rect(35,y,2,2,42);p.Set(35,y,43);}p.Poly(42,24,48,36,38,48,48);for(int x=25;x<48;x+=4){p.Rect(x,48,2,30,42);p.Rect(x,48,1,30,43);}p.Rect(24,48,24,2,42);p.Rect(24,76,24,3,41);p.Rect(24,62,24,1,42);
        p.Oval(36,72,4,3,58);p.Set(35,71,A.Ink);p.Oval(36,100,12,2,A.Penumbra);
    }
    static void Shackles(Pixels p)
    {
        Brush.Box(p,26,56,20,8,A.Stone);foreach(int x in new[]{30,42}){p.Oval(x,60,2,2,41);for(int y=64;y<84;y+=3){p.Rect(x-1+(y/3)%2,y,2,2,42);}p.Oval(x,87,3,3,42);p.Oval(x,87,1,1,0);}
    }
    static void IronMaiden(Pixels p)
    {
        p.Cast(39,100,12,3);p.Poly(42,26,100,26,58,30,46,36,42,42,46,46,58,46,100);p.Poly(43,26,100,26,58,30,46,36,42,36,100);p.Rect(35,46,2,54,41);p.Oval(36,52,4,5,41);p.Set(34,51,A.Red[2]);p.Set(38,51,A.Red[2]);
        for(int y=62;y<98;y+=8){p.Rect(26,y,20,1,41);p.Set(30,y+3,23);p.Set(41,y+3,23);}p.Rect(44,72,3,6,A.Gold[1]);
    }
    static void Web(Pixels p,bool large)
    {
        int reach=large?30:18;
        for(int i=0;i<=6;i++){double a=i*Math.PI/12;p.Line(12,40,12+(int)(Math.Cos(a)*reach*1.6),40+(int)(Math.Sin(a)*reach*1.8),1,160);}
        for(int ring=1;ring<=(large?5:3);ring++)for(int i=0;i<6;i++){double a=i*Math.PI/12,b=(i+1)*Math.PI/12;double r=ring*reach/3.4;p.Line(12+(int)(Math.Cos(a)*r*1.6),40+(int)(Math.Sin(a)*r*1.8),12+(int)(Math.Cos(b)*r*1.5),40+(int)(Math.Sin(b)*r*1.7),1,159);}
        if(large){p.Oval(34,66,3,3,A.Dark[0]);p.Oval(34,62,2,2,A.Dark[1]);p.Set(33,61,A.Red[2]);p.Set(35,61,A.Red[2]);foreach(int d in new[]{-1,1}){p.Line(34,66,34+d*7,62,1,A.Dark[0]);p.Line(34,67,34+d*7,70,1,A.Dark[0]);}}
    }
    static void EggSac(Pixels p)
    {
        p.Cast(39,100,13,2);foreach(var (x,y,rx,ry) in new[]{(30,92,7,8),(42,93,6,7),(36,84,6,7)}){p.Blob(x,y,rx,ry,new byte[]{159,159,160,160,160,161,161,98},741+x,0,.03f,true);p.Line(x-rx+2,y,x+rx-2,y+2,1,159);}
        p.Line(24,84,30,88,1,160);p.Line(48,84,43,89,1,160);p.Line(36,72,36,78,1,160);
    }
    static void Nest(Pixels p)
    {
        p.Cast(39,100,15,2);for(int ring=0;ring<4;ring++)for(int i=0;i<24;i++){double a=i*.262+ring;int x=36+(int)(Math.Cos(a)*(14-ring*2)),y=95+(int)(Math.Sin(a)*(5-ring));p.Line(x,y,x+2,y-1,1,(i+ring)%3==0?A.Leather[3]:A.Leather[1+ring%2]);}
        foreach(int x in new[]{31,37,42}){p.Oval(x,91,3,4,A.Teal[2]);p.Oval(x-1,90,1,2,A.Teal[3]);p.Set(x+1,93,A.Teal[1]);}
    }
    static void Crystals(Pixels p,byte[] r)
    {
        p.Cast(39,100,15,2);
        foreach(var (x,top,w) in new[]{(25,82,4),(33,66,6),(42,74,5),(49,86,3),(37,88,4)})
        {p.Poly(r[1],x-w,100,x-w,top+w*2,x,top,x+w,top+w*2,x+w,100);p.Poly(r[2],x-w,100,x-w,top+w*2,x,top,x,100);p.Line(x-w+1,top+w*2,x-w+1,98,1,r[3]);p.Set(x-1,top+3,161);}
        p.Blob(36,100,16,3,A.Stone8,751,0,.05f,true);p.Set(22,70,r[3]);p.Set(52,74,r[2]);p.Set(40,58,r[3]);
    }
    static void Stalagmite(Pixels p,int v)
    {
        p.Cast(39,100,v==2?14:9,2);
        foreach(var (x,top,w) in v==0?new[]{(36,52,8)}:v==1?new[]{(30,66,6),(42,74,5)}:new[]{(26,80,5),(36,60,7),(46,76,5),(40,90,3)})
            for(int y=top;y<=100;y++){float t=(y-top)/(float)(100-top);int half=(int)(1+w*t*(.7f+.3f*t));for(int x2=x-half;x2<=x+half;x2++){float a=(x2-x+half)/(2f*half+.01f);p.Set(x2,y,(y/5)%2==0&&a>.3f&&a<.7f?(byte)21:a<.25f?(byte)23:a<.6f?(byte)22:a<.85f?(byte)21:(byte)20);}}
    }
    static void Obsidian(Pixels p)
    {
        p.Cast(39,100,12,3);foreach(var (x,top,w) in new[]{(30,62,6),(40,44,8),(48,74,4)}){p.Poly(156,x-w,100,x-w+1,top+w*3,x,top,x+w,top+w*2,x+w,100);p.Poly(157,x-w,100,x-w+1,top+w*3,x,top,x,100);p.Line(x-w+2,top+w*3,x-w+2,98,1,158);p.Line(x-1,top+4,x-3,top+w*3,1,119);}
    }
    static void GlowCap(Pixels p)
    {
        p.Cast(40,100,14,2);for(int y=60;y<=100;y++){int w=4+(y>92?(y-92)/2:0);for(int i=-w;i<=w;i++)p.Set(36+i,y,i<-w/2?(byte)133:i<w/2?(byte)132:(byte)131);}
        p.Blob(36,54,18,10,new byte[]{131,131,132,132,132,133,133,56},761,0,.04f,true);p.Rect(22,61,28,2,131);foreach(var (x,y) in new[]{(28,50),(38,47),(45,53),(32,56)})p.Rect(x,y,3,2,56);
        foreach(var (x,y) in new[]{(18,40),(54,46),(36,34),(24,66),(50,70)})p.Set(x,y,133);p.Rect(50,90,2,10,132);p.Oval(51,88,5,3,132);p.Oval(50,87,3,1,133);
    }
    static void CaveMoss(Pixels p){p.Blob(36,96,16,5,A.Swamp8,771,.05f,.1f);p.Blob(26,95,8,4,A.Swamp8,772,.05f,.1f);foreach(var (x,y) in new[]{(28,92),(36,90),(44,93)}){p.Set(x,y,133);p.Set(x+1,y-1,56);}}
    static void Roots(Pixels p)
    {
        foreach(var (sx,ex,ey) in new[]{(30,18,100),(34,28,100),(38,40,100),(42,54,100),(36,46,84),(32,24,82)}){for(int i=0;i<=20;i++){float t=i/20f;int x=sx+(int)((ex-sx)*t+3*MathF.Sin(t*6+sx)),y=40+(int)((ey-40)*t);p.Rect(x,y,t<.5f?3:2,2,t<.3f?(byte)14:(byte)13);p.Set(x,y,15);}}
        p.Rect(26,36,20,6,13);
    }
    static void Rubble(Pixels p)
    {
        p.Cast(40,100,24,3);foreach(var (x,y,rx,ry) in new[]{(24,94,10,7),(46,93,12,8),(36,84,11,8),(56,97,6,4),(16,98,5,3),(38,72,7,6)})p.Blob(x,y,rx,ry,A.Stone8,781+x,0,.06f,true);
        Brush.Box(p,44,76,12,7,A.Stone);p.Line(18,90,30,74,2,14);p.Line(18,89,30,73,1,16);
    }
    static void BrokenPillar(Pixels p,int v)
    {
        p.Cast(40,100,14,3);Brush.Box(p,24,92,24,8,A.Stone);int top=v==0?56:74;Brush.Cylinder(p,28,top,16,92-top,new byte[]{20,21,22,24});for(int y=top+2;y<90;y+=2){p.Set(33,y,20);p.Set(38,y,20);}
        for(int x=28;x<44;x++){int jag=(int)(A.Hash(x/2,v,51)%6);p.Rect(x,top-jag,1,jag,x<34?(byte)22:(byte)21);}
        if(v==1){Brush.Cylinder(p,48,88,14,12,new byte[]{20,21,22,24});p.Oval(55,88,7,3,23);p.Oval(55,88,5,2,21);}
        Brush.Moss(p,24,top,24,100-top,791,18);
    }
    static void Statue(Pixels p,int v)
    {
        p.Cast(40,100,14,3);Brush.Box(p,24,90,24,10,A.Stone);
        p.Poly(21,28,90,29,62,34,56,38,56,43,62,44,90);p.Poly(23,28,90,29,62,34,56,35,90);p.Rect(34,70,4,20,20);
        if(v==0){p.Oval(36,50,5,6,22);p.Oval(35,49,3,4,23);p.Rect(32,50,8,2,20);p.Rect(35,42,2,4,22);p.Rect(46,48,2,42,22);p.Rect(46,48,1,42,24);p.Rect(43,62,8,2,22);p.Poly(22,20,64,28,62,28,80,24,86,20,80);p.Rect(23,66,2,12,20);}
        else{p.Rect(28,56,16,4,21);for(int x=28;x<44;x++){int jag=(int)(A.Hash(x/2,0,52)%5);p.Rect(x,56-jag,1,jag,21);}p.Oval(52,96,5,5,22);p.Oval(51,95,3,3,23);p.Rect(50,96,4,1,20);p.Rect(16,97,6,3,21);}
        Brush.Moss(p,24,56,24,44,793,16);
    }
    static void Gargoyle(Pixels p)
    {
        p.Cast(40,100,14,3);Brush.Box(p,26,88,20,12,A.Stone);p.Blob(36,78,9,10,A.Stone8,801,0,.03f,true);p.Blob(36,64,6,6,A.Stone8,802,0,.03f,true);
        p.Poly(21,27,74,12,58,16,78,26,84);p.Poly(22,27,74,12,58,18,70);p.Poly(20,45,74,60,58,56,78,46,84);p.Poly(22,31,60,29,52,34,58);p.Poly(21,41,60,43,52,38,58);
        p.Set(34,63,138);p.Set(38,63,138);p.Rect(34,67,5,1,19);p.Set(34,68,24);p.Set(38,68,24);p.Rect(30,86,4,3,22);p.Rect(38,86,4,3,21);
    }
    static void DarkAltar(Pixels p)
    {
        p.Cast(40,100,20,3);Brush.Box(p,20,84,32,16,new byte[]{156,156,157,157,158,159});Brush.Box(p,16,78,40,7,new byte[]{156,156,157,157,158,159});p.Rect(30,88,12,8,117);p.Rect(35,89,2,6,119);p.Rect(32,91,8,2,119);
        foreach(int x in new[]{19,51}){p.Rect(x,70,2,8,A.Red[0]);p.Rect(x,67,2,3,46);p.Set(x,66,47);}p.Oval(36,76,7,2,A.Dark[0]);p.Oval(36,75,5,1,A.Violet[2]);p.Rect(34,66,4,9,58);p.Oval(36,64,3,3,58);p.Set(35,63,A.Ink);p.Set(37,63,A.Ink);
    }
    static void DarkThrone(Pixels p)
    {
        p.Cast(40,100,18,3);var d=new byte[]{156,156,157,157,158,159};Brush.Box(p,22,36,28,56,d);foreach(int x in new[]{22,34,46})p.Poly(157,x,36,x+2,22,x+4,36);
        p.Rect(26,44,20,34,117);p.Rect(26,44,20,2,119);Brush.Box(p,16,72,10,28,d);Brush.Box(p,46,72,10,28,d);Brush.Box(p,24,82,24,18,d);p.Rect(26,80,20,4,118);
        Skull(p,21,72);Skull(p,51,72);p.Oval(36,34,3,3,138);p.Set(35,33,140);
    }
    static void EvilIdol(Pixels p)
    {
        p.Cast(39,100,12,2);var d=new byte[]{156,156,157,157,158,159};Brush.Box(p,26,92,20,8,d);p.Blob(36,80,9,12,A.Dark8,811,0,.02f,true);p.Blob(36,64,7,7,A.Dark8,812,0,.02f,true);
        p.Rect(32,62,3,2,138);p.Rect(38,62,3,2,138);p.Set(32,62,140);p.Rect(33,68,7,2,A.Ink);for(int x=34;x<40;x+=2)p.Set(x,68,161);p.Poly(157,30,60,26,48,33,58);p.Poly(157,42,60,46,48,39,58);
        p.Line(28,78,20,70,2,157);p.Line(44,78,52,70,2,156);p.Oval(36,82,2,2,118);p.Set(36,81,120);
    }
    static void Spring(Pixels p,byte[] r)
    {
        p.Cast(39,101,22,3);p.Oval(36,93,22,7,21);p.Oval(36,92,22,6,23);p.Oval(36,92,19,5,r[1]);p.Oval(34,91,13,3,r[2]);p.Oval(32,90,6,1,r[3]);
        Brush.Box(p,32,70,8,20,A.Stone);p.Oval(36,70,6,2,23);p.Oval(36,69,4,1,r[2]);foreach(var (x,y) in new[]{(36,62),(33,66),(39,66),(30,74),(42,74),(28,82),(44,82)})p.Set(x,y,r[3]);
        foreach(var (x,y) in new[]{(20,80),(52,78),(36,52),(16,90),(56,92)})p.Set(x,y,r[2]);
    }
    static void DarkWell(Pixels p)
    {
        p.Cast(40,100,17,3);for(int y=84;y<100;y++)for(int x=22;x<50;x++){int i=x-22;p.Set(x,y,(y-84)%5==4||(i+(y-84)/5*4)%8==7?(byte)19:i<3?(byte)23:i<18?(byte)21:(byte)20);}
        p.Oval(36,84,14,4,22);p.Oval(36,84,11,3,A.Ink);p.Rect(22,84,28,1,24);for(int y=60;y<84;y+=3){p.Rect(35,y,2,2,42);}p.Rect(30,56,12,4,42);Brush.Moss(p,22,84,28,16,821,24);
        p.Set(33,83,133);p.Set(39,84,133);
    }
    static void CandleStand(Pixels p)
    {
        p.Cast(38,100,7,2);p.Rect(35,66,2,32,41);p.Rect(35,66,1,32,42);p.Rect(30,97,12,3,41);p.Poly(41,28,62,44,62,41,67,31,67);p.Rect(28,61,16,2,42);
        foreach(var (x,h) in new[]{(30,6),(35,9),(40,7)}){p.Rect(x,61-h,2,h,31);p.Rect(x,58-h,2,3,46);p.Set(x,57-h,47);}p.Rect(32,67,1,4,31);
    }
    static void Teleporter(Pixels p,byte[] r)
    {
        p.Oval(36,92,26,8,20);p.Oval(36,91,26,7,22);p.Oval(36,91,22,6,21);p.Oval(36,91,18,5,r[0]);p.Oval(36,91,14,4,r[1]);p.Oval(36,91,8,2,r[2]);p.Oval(36,91,3,1,r[3]);
        for(int i=0;i<8;i++){double a=i*.785;p.Rect(36+(int)(Math.Cos(a)*22)-1,91+(int)(Math.Sin(a)*6),3,1,r[3]);}
    }
    static void Pool(Pixels p,byte[] r)
    {
        p.Blob(36,94,20,6,new byte[]{r[0],r[0],r[1],r[1],r[1],r[2],r[2],r[3]},831,.05f,.14f);p.Blob(22,95,7,3,new byte[]{r[0],r[0],r[1],r[1],r[1],r[2],r[2],r[3]},832,.05f,.14f);
        foreach(var (x,y) in new[]{(30,92),(42,94),(36,96)}){p.Oval(x,y,2,1,r[3]);p.Set(x-1,y-1,161);}
    }
    static void ExplosiveBarrel(Pixels p)
    {
        p.Cast(39,100,12,3);Brush.Cylinder(p,26,76,20,24,A.Red);for(int x=30;x<46;x+=4)p.Rect(x,77,1,23,A.Red[0]);p.Rect(26,80,20,2,42);p.Rect(26,94,20,2,42);p.Oval(36,76,10,3,A.Red[2]);p.Oval(36,76,8,2,A.Red[0]);
        p.Poly(151,36,83,41,92,31,92);p.Rect(35,86,2,3,A.Ink);p.Set(35,90,A.Ink);p.Line(36,75,40,68,1,A.Leather[2]);p.Rect(40,66,2,2,46);
    }
    static void BottleRack(Pixels p)
    {
        p.Cast(40,100,16,3);p.Rect(20,60,32,40,12);p.Rect(20,60,2,40,15);p.Rect(50,60,2,40,13);p.Rect(18,57,36,4,15);
        for(int shelf=0;shelf<3;shelf++){int board=72+shelf*12;p.Rect(22,board,28,2,15);for(int i=0;i<5;i++){int x=23+i*5+((shelf+i)%3==0?1:0);var r=new[]{A.Red,A.Green,A.Violet,A.Blue,A.Yellow}[(i+shelf*2)%5];
            if((i+shelf)%4==3){p.Line(x,board-1,x+4,board-3,2,160);continue;}p.Rect(x,board-6,3,6,r[1]);p.Rect(x,board-6,1,6,r[2]);p.Rect(x+1,board-9,1,3,160);}}
        p.Line(22,62,28,62,1,160);p.Line(22,62,22,68,1,160);p.Line(22,66,26,62,1,160);
    }
    static void SkullTable(Pixels p)
    {
        p.Cast(40,100,18,3);p.Rect(18,82,36,4,15);p.Rect(18,82,36,1,16);p.Rect(20,86,2,14,13);p.Rect(50,86,2,14,13);Skull(p,30,81);p.Rect(38,74,2,8,31);p.Rect(38,71,2,3,46);p.Rect(44,78,6,4,117);p.Rect(44,78,6,1,119);p.Rect(22,79,5,3,65);
    }
    static void RuinedShelf(Pixels p)
    {
        p.Cast(40,100,17,3);p.Rect(20,50,32,50,12);p.Rect(20,50,2,50,15);p.Rect(50,56,2,44,13);p.Rect(18,47,30,4,15);p.Rect(46,50,6,6,0);
        p.Rect(22,62,28,2,15);p.Line(22,76,48,84,2,14);p.Rect(22,92,28,2,15);foreach(var (x,y,c) in new[]{(24,56,(byte)63),(28,55,(byte)61),(40,57,(byte)65),(26,86,(byte)64),(42,88,(byte)60)}){p.Rect(x,y,3,6,c);}
        p.Rect(28,97,7,3,63);p.Rect(38,98,6,2,61);p.Rect(54,98,6,2,65);p.Line(22,52,28,52,1,160);p.Line(22,52,22,58,1,160);Brush.Moss(p,20,88,32,12,841,20);
    }
    static void LavaVent(Pixels p,int frame)
    {
        p.Blob(36,96,16,5,A.Dark8,851,0,.1f,true);p.Oval(36,95,10,3,44);p.Oval(36,95,7,2,45);p.Oval(35,94,4,1,46);p.Oval(34,94,1,0,47);
        foreach(var (x,y) in frame==0?new[]{(30,86),(40,80),(36,72)}:new[]{(32,82),(42,86),(37,76)}){p.Rect(x,y,2,2,45);p.Set(x,y,47);}p.Line(22,96,14,99,1,44);p.Line(50,97,58,100,1,44);
    }
    static void Timber(Pixels p)
    {
        p.Cast(39,100,18,2);Brush.Cylinder(p,18,56,7,44,A.WoodT);Brush.Cylinder(p,47,56,7,44,A.WoodT);p.Rect(14,48,44,8,14);p.Rect(14,48,44,2,16);p.Rect(14,54,44,2,12);p.Line(24,68,32,56,2,13);p.Line(47,68,39,56,2,13);
    }
    static void BossDoor(Pixels p)
    {
        Brush.Masonry(p,8,36,12,64,861);Brush.Masonry(p,52,36,12,64,862);Brush.Box(p,4,26,64,11,A.Stone);p.Rect(20,37,32,63,117);p.Rect(20,37,32,2,119);p.Rect(20,37,2,63,119);p.Rect(35,37,2,63,156);
        foreach(int x in new[]{24,40}){p.Rect(x,44,8,22,118);p.Rect(x,44,8,1,120);p.Rect(x,72,8,22,118);p.Rect(x,72,8,1,120);}
        p.Oval(36,68,7,7,A.Gold[1]);p.Oval(36,68,5,5,A.Dark[0]);Skull(p,36,72);p.Oval(36,31,4,3,138);p.Set(35,30,140);foreach(int x in new[]{12,58}){p.Rect(x,44,3,6,46);p.Set(x+1,42,47);}
    }
}
