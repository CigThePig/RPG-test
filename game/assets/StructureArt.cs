using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Shared building blocks for the library's recipes.</summary>
public static class Brush
{
    /// <summary>A dressed block lit from the upper-left. Ramp is six tones, darkest first.</summary>
    public static void Box(Pixels p,int x,int y,int w,int h,byte[] r)
    {
        p.Rect(x,y,w,h,r[3]);p.Rect(x,y,w,1,r[5]);p.Rect(x,y+1,1,h-1,r[4]);p.Rect(x+w-2,y+1,2,h-1,r[2]);p.Rect(x,y+h-1,w,1,r[1]);
    }
    /// <summary>Coursed stonework.</summary>
    public static void Masonry(Pixels p,int x,int y,int w,int h,int slot,byte[]? ramp=null)
    {
        var r=ramp??A.Stone;
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)
        {
            int course=(yy-y)/6,cy=(yy-y)%6,bx=xx-x+(course%2)*6;uint n=A.Hash(bx/12,course,slot);
            byte c=n%5==0?r[3]:n%7==0?r[1]:r[2];
            if(cy==0)c=c==r[1]?r[2]:c==r[2]?r[3]:r[4];
            if(cy==5||bx%12==11)c=r[0];
            p.Set(xx,yy,c);
        }
        p.Rect(x,y,2,h,r[4]);for(int yy=y;yy<y+h;yy++)for(int xx=x+w-3;xx<x+w;xx++)if(p.At(xx,yy)!=r[0])p.Set(xx,yy,r[1]);
    }
    /// <summary>Boards, upright or laid across.</summary>
    public static void Planks(Pixels p,int x,int y,int w,int h,bool upright,int slot,byte[]? ramp=null)
    {
        var r=ramp??A.Wood;
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)
        {
            int along=upright?xx-x:yy-y,board=along/5;
            p.Set(xx,yy,along%5==4?r[1]:A.Hash(board,slot,(upright?yy:xx)/11)%4==0?r[3]:r[2]);
        }
        p.Rect(x,y,w,1,r[4]);p.Rect(x,y,1,h,r[4]);p.Rect(x+w-1,y,1,h,r[1]);p.Rect(x,y+h-1,w,1,r[1]);
    }
    /// <summary>An upright cylinder: lit left of centre, shaded right. Four-tone ramp.</summary>
    public static void Cylinder(Pixels p,int x,int y,int w,int h,byte[] r)
    {
        for(int yy=y;yy<y+h;yy++)for(int i=0;i<w;i++){float a=i/(float)Math.Max(1,w-1);p.Set(x+i,yy,a<.12f?r[1]:a<.3f?r[3]:a<.6f?r[2]:a<.85f?r[1]:r[0]);}
    }
    /// <summary>One frame of a flame, four frames to a loop. Four-tone ramp, coolest first.</summary>
    public static void Flame(Pixels p,int cx,int foot,int width,int height,int frame,byte[]? ramp=null)
    {
        var c=ramp??A.Flame;
        for(int layer=0;layer<4;layer++)
        {
            float half=width*(1-layer*.23f)/2,tall=height*(1-layer*.21f);
            for(int y=0;y<tall;y++)
            {
                float t=y/tall,w=half*MathF.Sqrt(1-t)*(.6f+.4f*MathF.Sin(MathF.PI*MathF.Min(1,t*1.7f+.2f)));
                float sway=MathF.Sin(t*3.4f+frame*MathF.PI/2+layer*.5f)*t*width*.2f;
                for(int x=(int)MathF.Round(cx+sway-w);x<=(int)MathF.Round(cx+sway+w);x++)p.Set(x,foot-y,c[layer]);
            }
        }
        int[] drift={-3,2,4,-1};p.Set(cx+drift[frame&3],foot-height-2-frame%2,c[2]);p.Set(cx-drift[(frame+2)&3],foot-height+2,c[1]);
    }
    /// <summary>A pitched roof seen end-on: bands of tile widening downward. Four-tone ramp.</summary>
    public static void Pitch(Pixels p,int cx,int top,int half,int height,byte[] r)
    {
        for(int row=0;row<height;row++)
        {
            int reach=Math.Max(1,half*(row+1)/height);
            for(int x=cx-reach;x<cx+reach;x++)p.Set(x,top+row,row%4==3?r[0]:x<cx-reach+2?r[3]:x>cx+reach-4?r[1]:row%4==0?r[3]:r[2]);
        }
        p.Rect(cx-half,top+height-1,half*2,1,r[0]);
    }
    /// <summary>Patchy moss over whatever stone or timber is already painted there.</summary>
    public static void Moss(Pixels p,int x,int y,int w,int h,int slot,int density=30)
    {
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)
        {
            byte c=p.At(xx,yy);if(c is 0 or 35 or 36 or 37 or (>=1 and <=6))continue;
            if(A.Hash(xx/4,yy/3,slot)%100>=density)continue;
            p.Set(xx,yy,A.Hash(xx,yy,slot+1)%5==0?(byte)5:A.Hash(xx,yy,slot+2)%3==0?(byte)2:(byte)3);
        }
    }
    /// <summary>A lit window with frame and cross bar.</summary>
    public static void Window(Pixels p,int x,int y,int w,int h,bool lit=true)
    {
        p.Rect(x-1,y-1,w+2,h+2,13);p.Rect(x,y,w,h,lit?(byte)93:(byte)94);if(lit){p.Rect(x,y,w,2,47);p.Rect(x,y+h-2,w,2,45);}else p.Set(x+1,y+1,87);
        p.Rect(x+w/2,y,1,h,13);p.Rect(x,y+h/2,w,1,13);
    }
    /// <summary>A plank door in a frame.</summary>
    public static void Door(Pixels p,int x,int y,int w,int h)
    {
        p.Rect(x-2,y-2,w+4,h+2,13);p.Rect(x,y,w,h,14);for(int b=x+3;b<x+w;b+=4)p.Rect(b,y,1,h,13);p.Rect(x,y,1,h,15);p.Rect(x,y+h/3,w,1,41);p.Rect(x,y+h*2/3,w,1,41);p.Rect(x+w-3,y+h/2,2,2,29);
    }
}

/// <summary>Things people build out in the world: camps, graves and shrines, bridges and
/// docks, defences, farm and mine gear, market tents and works of magic. Standard cell, and
/// a 160x176 cell for landmarks.</summary>
public static class StructureArt
{
    static readonly (string Name,byte[] Ramp)[] Hues={("red",A.Red),("blue",A.Blue),("green",A.Green),("violet",A.Violet)};
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("structures",72,104,8);
        foreach(var (name,r) in Hues)s.Add("tent-"+name,p=>Tent(p,r));
        s.Add("campfire-cold",p=>Campfire(p,-1));for(int f=0;f<4;f++){int k=f;s.Add($"campfire-{f+1}",p=>Campfire(p,k));}
        s.Add("bedroll",Bedroll).Add("cooking-spit",Spit).Add("lean-to",LeanTo);
        s.Add("grave-cross",p=>Grave(p,0)).Add("grave-round",p=>Grave(p,1)).Add("grave-slab",p=>Grave(p,2)).Add("grave-broken",p=>Grave(p,3))
         .Add("tomb",Tomb).Add("shrine",Shrine).Add("obelisk",Obelisk).Add("milestone",Milestone);
        foreach(var (name,r) in Hues)s.Add("waystone-"+name,p=>Waystone(p,r));
        foreach(var (name,r) in Hues)s.Add("portal-"+name,p=>Portal(p,r));
        foreach(var (name,r) in Hues)s.Add("pylon-"+name,p=>Pylon(p,r));
        foreach(var (name,r) in Hues)s.Add("rune-circle-"+name,p=>RuneCircle(p,r),true,new Vector2(36,92));
        foreach(var (name,r) in Hues)s.Add("standard-"+name,p=>Standard(p,r));
        s.Add("totem-1",p=>Totem(p,0)).Add("totem-2",p=>Totem(p,1)).Add("idol",Idol).Add("offering-bowl",OfferingBowl);
        s.Add("bridge-plank",BridgePlank).Add("bridge-post",BridgePost).Add("dock",Dock).Add("dock-post",DockPost)
         .Add("rowboat",Rowboat).Add("net-rack",NetRack).Add("crab-pot",CrabPot).Add("buoy",Buoy);
        s.Add("palisade",Palisade).Add("palisade-side",PalisadeSide).Add("palisade-gate",PalisadeGate).Add("battlement",Battlement)
         .Add("training-dummy",Dummy).Add("archery-target",Target).Add("weapon-rack",WeaponRack).Add("ballista",Ballista);
        s.Add("brazier-post-cold",p=>BrazierPost(p,-1)).Add("brazier-post-1",p=>BrazierPost(p,0)).Add("brazier-post-2",p=>BrazierPost(p,1)).Add("brazier-post-3",p=>BrazierPost(p,2))
         .Add("brazier-post-4",p=>BrazierPost(p,3)).Add("spike-barricade",Barricade).Add("sandbags",Sandbags).Add("war-drum",Drum);
        s.Add("coop",Coop).Add("doghouse",Doghouse).Add("trough",Trough).Add("plough",Plough)
         .Add("wheelbarrow",Wheelbarrow).Add("milk-churns",Churns).Add("grindstone",Grindstone).Add("water-barrel",WaterBarrel);
        s.Add("mine-cart",p=>MineCart(p,null)).Add("mine-cart-ore",p=>MineCart(p,A.Gold)).Add("rail",Rail).Add("rail-side",RailSide)
         .Add("mine-support",MineSupport).Add("ore-pile-iron",p=>OrePile(p,A.Steel)).Add("ore-pile-gold",p=>OrePile(p,A.Gold)).Add("ore-pile-mythril",p=>OrePile(p,A.Mythril));
        foreach(var (name,r) in Hues)s.Add("pavilion-"+name,p=>Pavilion(p,r));
        s.Add("wagon",Wagon).Add("fish-stand",FishStand).Add("weapon-stand",WeaponStand).Add("potion-stand",PotionStand);
        return s;
    }

    static void Tent(Pixels p,byte[] r)
    {
        p.Cast(40,100,24,3);
        p.Poly(r[1],36,58,60,100,12,100);p.Poly(r[2],36,58,36,100,12,100);p.Poly(r[0],36,58,60,100,52,100);
        p.Poly(A.Dark[0],36,72,44,100,28,100);p.Poly(r[3],36,72,30,100,28,100);p.Line(36,58,12,100,1,r[3]);p.Rect(35,54,2,6,14);
        for(int y=70;y<98;y+=8)p.Line(36-(y-58)/2,y,36-(y-58)/2+6,y,1,r[1]);p.Line(12,100,6,102,1,57);p.Line(60,100,66,102,1,57);
    }
    static void Campfire(Pixels p,int frame)
    {
        p.Cast(38,100,13,2);
        foreach(var (x,y) in new[]{(25,97),(30,100),(42,100),(47,97),(36,101)})p.Blob(x,y,3,2,A.Stone8,x,0,.03f,true);
        p.Line(28,97,44,93,2,13);p.Line(28,93,44,97,2,14);p.Line(30,96,42,94,1,15);
        if(frame<0){p.Rect(32,93,8,2,69);p.Set(35,93,44);return;}
        Brush.Flame(p,36,95,15,22,frame);
    }
    static void Bedroll(Pixels p)
    {
        p.Cast(38,100,15,2);p.Rect(20,93,30,7,A.Leather[1]);p.Rect(20,93,30,2,A.Leather[2]);p.Oval(50,96,3,4,A.Leather[2]);p.Oval(50,96,2,2,A.Leather[0]);
        p.Rect(26,93,2,7,A.Red[1]);p.Rect(40,93,2,7,A.Red[1]);p.Rect(16,95,6,5,85);p.Rect(16,95,6,1,86);
    }
    static void Spit(Pixels p)
    {
        Campfire(p,1);foreach(int x in new[]{20,51}){p.Rect(x,70,2,30,14);p.Line(x,70,x-3,66,1,14);p.Line(x+1,70,x+4,66,1,14);}
        p.Rect(18,72,36,1,42);p.Oval(36,73,8,4,73);p.Oval(35,72,6,2,74);p.Line(42,71,46,69,2,58);
    }
    static void LeanTo(Pixels p)
    {
        p.Cast(40,100,24,3);p.Poly(13,12,100,50,62,58,62,24,100);for(int i=0;i<7;i++)p.Line(14+i*2,100,52+i,62,1,i%2==0?(byte)15:(byte)14);
        p.Rect(54,62,3,38,14);p.Rect(54,62,1,38,16);Brush.Moss(p,14,62,40,36,507,22);p.Rect(30,95,18,5,A.Leather[1]);p.Rect(30,95,18,1,A.Leather[2]);
    }
    static void Grave(Pixels p,int v)
    {
        p.Cast(38,100,9,2);p.Oval(36,100,12,2,8);
        if(v==0){p.Rect(33,72,6,28,21);p.Rect(26,80,20,6,21);p.Rect(33,72,2,28,23);p.Rect(26,80,20,1,23);p.Rect(37,73,2,27,20);}
        if(v==1){p.Rect(28,82,16,18,21);p.Oval(36,82,8,8,21);p.Rect(28,82,2,18,23);p.Oval(34,80,5,5,22);p.Rect(42,82,2,18,20);p.Rect(32,86,8,1,19);p.Rect(32,89,6,1,19);}
        if(v==2){Brush.Box(p,26,84,20,16,A.Stone);p.Rect(24,82,24,3,22);p.Rect(24,82,24,1,24);p.Rect(34,88,4,8,20);p.Rect(32,90,8,2,20);}
        if(v==3){p.Rect(28,88,16,12,21);p.Poly(21,28,88,31,82,36,86,40,80,44,88);p.Rect(28,84,2,16,23);p.Rect(46,97,6,3,21);p.Rect(46,97,6,1,23);}
        p.Over(26,90,22,10,3,21,22);p.Tuft(26,100,2);p.Tuft(46,100,3);
    }
    static void Tomb(Pixels p)
    {
        p.Cast(40,100,20,3);Brush.Box(p,16,84,40,16,A.Stone);Brush.Box(p,14,78,44,7,A.Stone);p.Rect(20,88,32,8,20);
        p.Poly(22,22,78,28,70,44,70,50,78);p.Rect(28,70,16,1,24);p.Rect(34,72,4,5,29);p.Rect(22,90,4,4,29);p.Rect(46,90,4,4,29);Brush.Moss(p,14,78,44,22,506,20);
    }
    static void Shrine(Pixels p)
    {
        p.Cast(39,100,15,3);Brush.Box(p,24,94,24,6,A.Stone);p.Rect(27,66,3,28,14);p.Rect(42,66,3,28,13);p.Rect(27,66,1,28,16);
        Brush.Pitch(p,36,52,18,14,A.Tile);p.Rect(31,78,10,16,21);p.Oval(36,78,5,5,21);p.Oval(35,77,3,3,23);p.Set(34,79,A.Ink);p.Set(38,79,A.Ink);p.Rect(34,84,5,1,20);
        p.Rect(26,92,3,2,46);p.Set(27,91,47);p.Rect(43,92,3,2,46);p.Set(44,91,47);p.Rect(32,96,8,1,29);
    }
    static void Obelisk(Pixels p)
    {
        p.Cast(39,100,10,3);Brush.Box(p,27,94,18,6,A.Stone);p.Poly(21,30,94,32,40,36,32,40,40,42,94);p.Poly(23,30,94,32,40,36,32,35,94);p.Poly(20,39,40,42,94,38,94);
        foreach(int y in new[]{50,60,70,80})p.Rect(34,y,4,4,54);p.Set(34,50,56);p.Poly(29,34,36,36,32,38,36);
    }
    static void Milestone(Pixels p){p.Cast(38,100,7,2);p.Rect(31,86,10,14,21);p.Oval(36,86,5,3,22);p.Rect(31,86,2,14,23);p.Rect(39,87,2,13,20);p.Rect(34,90,4,1,19);p.Rect(34,93,3,1,19);p.Tuft(30,100,2);}
    static void Waystone(Pixels p,byte[] r)
    {
        p.Cast(39,100,11,3);p.Poly(21,28,100,29,62,34,54,42,56,45,66,44,100);p.Poly(23,28,100,29,62,34,54,35,100);p.Poly(20,41,58,45,66,44,100,40,100);
        p.Oval(36,74,4,6,r[1]);p.Oval(36,74,2,4,r[2]);p.Set(36,73,r[3]);p.Rect(35,84,2,6,r[2]);p.Rect(33,86,6,2,r[2]);p.Set(24,58,r[3]);p.Set(48,64,r[2]);p.Set(30,48,r[2]);p.Tuft(26,100,2);
    }
    static void Portal(Pixels p,byte[] r)
    {
        p.Cast(40,100,22,3);
        for(int y=50;y<98;y++)for(int x=20;x<52;x++){float dx=(x-35.5f)/14f,dy=(y-74)/23f,d=dx*dx+dy*dy;if(d>1)continue;float swirl=MathF.Sin(MathF.Atan2(dy,dx)*3+d*9);p.Set(x,y,d<.12f?r[3]:swirl>.4f?r[2]:swirl>-.3f?r[1]:r[0]);}
        for(int i=0;i<16;i++){double a=i*.3927;int x=36+(int)(Math.Cos(a)*17),y=74+(int)(Math.Sin(a)*26);p.Rect(x-3,y-3,7,7,i<8?(byte)21:(byte)22);p.Rect(x-3,y-3,7,1,24);p.Rect(x+3,y-2,1,6,20);p.Set(x,y,r[2]);}
        Brush.Box(p,18,96,36,5,A.Stone);
    }
    static void Pylon(Pixels p,byte[] r)
    {
        p.Cast(38,100,9,2);Brush.Box(p,28,92,16,8,A.Stone);Brush.Box(p,31,82,10,10,A.Stone);
        p.Poly(r[1],36,40,44,58,36,78,28,58);p.Poly(r[2],36,40,36,78,28,58);p.Poly(r[3],36,44,36,60,31,58);p.Line(28,58,44,58,1,r[0]);p.Set(33,52,161);
        foreach(var (x,y) in new[]{(24,48),(48,52),(22,66),(50,70),(36,32)})p.Set(x,y,r[3]);
    }
    static void RuneCircle(Pixels p,byte[] r)
    {
        for(int i=0;i<64;i++){double a=i*.0982;p.Set(36+(int)Math.Round(Math.Cos(a)*28),92+(int)Math.Round(Math.Sin(a)*9),i%4==0?r[3]:r[2]);p.Set(36+(int)Math.Round(Math.Cos(a)*22),92+(int)Math.Round(Math.Sin(a)*7),r[1]);}
        for(int i=0;i<5;i++){double a=i*1.2566-1.57,b=(i+2)*1.2566-1.57;p.Line(36+(int)(Math.Cos(a)*21),92+(int)(Math.Sin(a)*7),36+(int)(Math.Cos(b)*21),92+(int)(Math.Sin(b)*7),1,r[2]);}
        for(int i=0;i<8;i++){double a=i*.785+.39;p.Rect(36+(int)(Math.Cos(a)*25)-1,92+(int)(Math.Sin(a)*8),2,1,r[3]);}p.Oval(36,92,3,1,r[3]);
    }
    static void Standard(Pixels p,byte[] r)
    {
        p.Cast(38,100,6,2);p.Rect(35,36,2,64,14);p.Rect(35,36,1,64,16);p.Rect(28,40,16,2,14);p.Oval(36,35,2,2,29);Brush.Box(p,32,96,8,4,A.Stone);
        p.Poly(r[1],29,42,43,42,43,70,36,64,29,70);p.Poly(r[2],29,42,36,42,36,64,29,70);p.Rect(29,42,1,26,r[3]);p.Oval(36,52,3,3,29);p.Set(35,51,30);p.Rect(29,44,14,1,29);
    }
    static void Totem(Pixels p,int v)
    {
        p.Cast(39,100,10,3);
        for(int i=0;i<3;i++)
        {
            int y=84-i*18;Brush.Box(p,28,y,16,17,v==0?A.Wood:A.Stone);p.Rect(31,y+5,3,3,v==0?(byte)46:(byte)54);p.Rect(38,y+5,3,3,v==0?(byte)46:(byte)54);p.Rect(32,y+11,8,2,A.Ink);
            for(int t=33;t<39;t+=2)p.Set(t,y+11,59);if(i==1){p.Rect(20,y+6,8,4,v==0?(byte)14:(byte)21);p.Rect(44,y+6,8,4,v==0?(byte)13:(byte)20);}
        }
        p.Poly(v==0?(byte)138:(byte)118,28,48,36,38,44,48);p.Set(36,40,161);
    }
    static void Idol(Pixels p)
    {
        p.Cast(39,100,12,2);Brush.Box(p,26,92,20,8,A.Sand);p.Blob(36,82,8,10,A.Sand8,201,0,.02f,true);p.Blob(36,68,6,6,A.Sand8,202,0,.02f,true);
        p.Set(34,67,A.Ink);p.Set(38,67,A.Ink);p.Rect(34,71,5,1,49);p.Rect(33,80,7,2,29);p.Oval(36,86,2,2,138);p.Line(28,80,24,74,2,51);p.Line(44,80,48,74,2,50);
    }
    static void OfferingBowl(Pixels p)
    {
        p.Cast(38,100,10,2);p.Poly(A.Bronze[1],26,90,46,90,42,98,30,98);p.Rect(26,89,20,2,A.Bronze[2]);p.Rect(26,89,10,1,A.Bronze[3]);p.Rect(33,98,6,2,A.Bronze[0]);
        p.Rect(29,87,14,2,29);p.Set(31,86,30);p.Rect(36,85,3,2,138);p.Set(40,86,147);p.Set(33,82,160);p.Set(34,79,160);
    }
    static void BridgePlank(Pixels p)
    {
        // One tile of plank bridge; neighbours join end to end.
        Brush.Planks(p,24,88,24,12,true,211);p.Rect(24,82,24,2,14);p.Rect(24,82,24,1,16);foreach(int x in new[]{24,35,46}){p.Rect(x,80,2,9,13);p.Set(x,80,15);}
        p.Rect(24,100,24,2,13);
    }
    static void BridgePost(Pixels p){p.Cast(38,100,6,2);Brush.Cylinder(p,32,72,8,28,A.WoodT);p.Oval(36,72,4,2,16);p.Rect(32,78,8,2,A.Leather[2]);p.Rect(32,92,8,2,A.Leather[1]);}
    static void Dock(Pixels p)
    {
        Brush.Planks(p,24,86,24,10,false,212);foreach(int x in new[]{25,45}){p.Rect(x,94,2,8,13);p.Rect(x,101,2,1,99);}p.Rect(24,96,24,1,12);
    }
    static void DockPost(Pixels p){Brush.Cylinder(p,33,70,6,30,A.WoodT);p.Oval(36,70,3,1,16);p.Rect(33,76,6,2,57);p.Rect(33,79,6,1,58);p.Rect(31,99,10,2,101);p.Rect(33,100,6,1,99);}
    static void Rowboat(Pixels p)
    {
        p.Poly(14,10,88,62,88,56,99,16,99);p.Poly(15,10,88,36,88,34,99,16,99);p.Rect(10,87,52,2,16);p.Rect(12,96,48,3,13);
        p.Rect(22,89,3,8,13);p.Rect(46,89,3,8,13);p.Rect(20,90,32,2,15);p.Line(28,84,8,94,1,15);p.Line(44,84,64,94,1,14);p.Rect(6,94,5,3,14);p.Rect(62,94,5,3,13);p.Oval(36,100,28,2,A.Penumbra);
    }
    static void NetRack(Pixels p)
    {
        p.Cast(38,100,16,2);p.Rect(18,62,2,38,14);p.Rect(52,62,2,38,13);p.Rect(18,64,36,2,14);p.Rect(18,64,36,1,16);
        for(int x=20;x<52;x+=4)for(int y=66;y<92;y+=4){p.Line(x,y,x+4,y+4,1,57);p.Line(x+4,y,x,y+4,1,58);}
        foreach(var (x,y) in new[]{(26,80),(40,74),(46,86)}){p.Oval(x,y,2,2,154);p.Set(x-1,y-1,155);}
    }
    static void CrabPot(Pixels p)
    {
        p.Cast(38,100,11,2);p.Rect(26,86,20,14,13);for(int x=27;x<45;x+=3)p.Rect(x,87,2,12,15);p.Rect(26,86,20,1,16);p.Rect(26,92,20,1,14);p.Rect(26,99,20,1,12);
        p.Line(46,88,54,80,1,57);p.Oval(55,79,2,2,138);
    }
    static void Buoy(Pixels p){p.Oval(36,92,7,7,138);p.Oval(35,91,5,5,139);p.Rect(29,91,14,3,161);p.Rect(35,78,2,8,42);p.Poly(138,37,78,44,81,37,84);p.Oval(36,100,10,1,99);p.Set(33,88,140);}
    static void Palisade(Pixels p)
    {
        for(int i=0;i<4;i++){int x=24+i*6;Brush.Cylinder(p,x,66,6,36,A.WoodT);p.Poly(15,x,66,x+3,58,x+6,66);p.Poly(16,x,66,x+3,58,x+3,66);}
        p.Rect(24,78,24,2,A.Leather[1]);p.Rect(24,92,24,2,A.Leather[1]);
    }
    static void PalisadeSide(Pixels p)
    {
        for(int i=0;i<2;i++){int y=101-i*6;Brush.Cylinder(p,33,y-36,6,36,A.WoodT);p.Poly(15,33,y-36,36,y-44,39,y-36);p.Poly(16,33,y-36,36,y-44,36,y-36);}
    }
    static void PalisadeGate(Pixels p)
    {
        Brush.Cylinder(p,12,54,7,48,A.WoodT);Brush.Cylinder(p,53,54,7,48,A.WoodT);p.Poly(15,12,54,15,46,19,54);p.Poly(15,53,54,56,46,60,54);
        p.Rect(10,58,52,5,14);p.Rect(10,58,52,1,16);p.Rect(10,62,52,1,12);Brush.Planks(p,20,66,32,36,true,221);p.Rect(35,66,2,36,12);p.Rect(20,74,32,2,42);p.Rect(20,92,32,2,42);p.Rect(32,82,3,3,29);p.Rect(37,82,3,3,29);
    }
    static void Battlement(Pixels p)
    {
        Brush.Masonry(p,24,66,24,36,231);foreach(int x in new[]{24,37}){Brush.Box(p,x,56,11,10,A.Stone);}p.Rect(24,66,24,2,23);p.Rect(34,78,4,10,A.Ink);p.Rect(35,79,2,8,94);
    }
    static void Dummy(Pixels p)
    {
        p.Cast(38,100,9,2);p.Rect(35,70,2,30,14);p.Rect(24,76,24,2,14);p.Blob(36,82,8,9,A.Straw8,241,0,.05f,true);p.Blob(36,68,5,5,A.Straw8,242,0,.05f,true);
        p.Rect(29,84,14,2,A.Leather[1]);p.Set(34,67,A.Ink);p.Set(38,67,A.Ink);p.Line(30,74,40,90,1,138);p.Rect(30,98,12,2,13);
    }
    static void Target(Pixels p)
    {
        p.Cast(38,100,11,2);p.Line(26,100,32,70,2,14);p.Line(46,100,40,70,2,13);p.Oval(36,76,13,13,161);p.Oval(36,76,10,10,138);p.Oval(36,76,7,7,161);p.Oval(36,76,4,4,142);p.Oval(36,76,1,1,151);
        p.Line(38,74,48,64,1,15);p.Rect(47,62,3,2,138);
    }
    static void WeaponRack(Pixels p)
    {
        p.Cast(39,100,15,2);p.Rect(20,70,3,30,14);p.Rect(49,70,3,30,13);p.Rect(20,74,32,2,14);p.Rect(20,74,32,1,16);p.Rect(20,94,32,2,14);
        p.Rect(27,60,2,36,A.Steel[2]);p.Rect(26,90,4,2,A.Gold[1]);p.Rect(34,58,2,40,15);p.Poly(A.Steel[2],32,58,35,50,38,58);p.Rect(42,62,2,34,15);p.Poly(A.Steel[2],40,62,47,62,46,70,41,70);p.Rect(46,66,2,30,A.Steel[1]);
    }
    static void Ballista(Pixels p)
    {
        p.Cast(39,100,20,3);p.Rect(22,90,28,5,14);p.Rect(22,90,28,1,16);foreach(int x in new[]{24,44}){p.Oval(x,97,4,4,13);p.Oval(x,97,2,2,15);}
        p.Line(16,78,56,78,2,14);for(int i=-10;i<=10;i++)p.Set(36+i*2,78-(int)(8*Math.Sqrt(Math.Max(0,1-i*i/100.0))),15);p.Line(16,78,36,86,1,57);p.Line(56,78,36,86,1,57);
        p.Rect(35,68,2,22,13);p.Poly(A.Steel[2],34,68,36,60,38,68);
    }
    static void BrazierPost(Pixels p,int frame)
    {
        p.Cast(38,100,7,2);Brush.Cylinder(p,34,66,4,34,A.IronT);p.Rect(31,97,10,3,41);p.Poly(41,26,58,46,58,42,66,30,66);p.Rect(26,57,20,2,42);p.Rect(26,57,9,1,43);
        if(frame<0){p.Rect(29,55,14,2,69);return;}Brush.Flame(p,36,58,14,20,frame);
    }
    static void Barricade(Pixels p)
    {
        p.Cast(39,100,20,2);p.Line(14,92,58,92,3,14);p.Line(14,91,58,91,1,16);
        for(int i=0;i<5;i++){int x=18+i*9;p.Line(x,100,x+8,76,2,i%2==0?(byte)14:(byte)15);p.Line(x+8,100,x,78,2,13);p.Set(x+8,75,16);p.Set(x,77,15);}
    }
    static void Sandbags(Pixels p)
    {
        p.Cast(39,100,20,2);for(int row=0;row<3;row++)for(int i=0;i<4-row;i++){int x=18+i*11+row*5,y=95-row*6;p.Oval(x+5,y+2,6,3,57);p.Oval(x+4,y+1,5,2,58);p.Set(x+2,y,59);p.Rect(x+10,y+1,1,3,A.Leather[1]);}
    }
    static void Drum(Pixels p)
    {
        p.Cast(39,100,13,3);Brush.Cylinder(p,24,80,24,20,A.WoodT);p.Oval(36,80,12,4,59);p.Oval(35,80,9,2,161);for(int x=26;x<47;x+=5){p.Line(x,82,x+3,98,1,138);}p.Rect(24,86,24,1,29);
        p.Line(48,70,40,80,1,15);p.Oval(49,69,2,2,58);
    }
    static void Coop(Pixels p)
    {
        p.Cast(39,100,18,3);Brush.Planks(p,20,78,32,18,true,251);Brush.Pitch(p,36,62,20,16,A.Thatch);p.Rect(32,84,8,12,A.Ink);p.Line(40,96,48,100,2,14);foreach(int x in new[]{22,48})p.Rect(x,96,2,4,13);
        p.Oval(56,96,3,3,161);p.Oval(58,93,2,2,161);p.Set(60,93,154);p.Set(58,91,138);p.Rect(55,99,1,2,154);
    }
    static void Doghouse(Pixels p)
    {
        p.Cast(39,100,15,3);Brush.Planks(p,22,82,28,18,true,252);Brush.Pitch(p,36,68,18,14,A.Tile);p.Rect(30,88,12,12,A.Ink);p.Oval(36,88,6,4,A.Ink);p.Oval(33,96,3,3,A.Leather[2]);p.Set(32,95,A.Ink);p.Rect(52,98,6,2,A.Bronze[1]);
    }
    static void Trough(Pixels p)
    {
        p.Cast(39,100,18,2);p.Poly(14,16,88,56,88,52,98,20,98);p.Poly(15,16,88,36,88,36,98,20,98);p.Rect(16,87,40,2,16);p.Rect(19,89,34,3,142);p.Rect(20,89,14,1,143);foreach(int x in new[]{20,50})p.Rect(x,96,3,4,13);
    }
    static void Plough(Pixels p)
    {
        p.Cast(39,100,16,2);p.Line(16,80,50,92,2,14);p.Line(16,79,50,91,1,16);p.Line(16,80,12,72,2,14);p.Line(22,82,18,74,2,13);p.Poly(A.Steel[1],44,90,56,92,48,100);p.Poly(A.Steel[2],44,90,50,91,47,97);p.Oval(54,96,4,4,13);p.Oval(54,96,2,2,15);
    }
    static void Wheelbarrow(Pixels p)
    {
        p.Cast(39,100,16,2);p.Poly(14,20,82,48,82,44,94,26,94);p.Poly(15,20,82,34,82,33,94,26,94);p.Rect(20,81,28,2,16);p.Line(44,92,60,84,2,14);p.Oval(24,96,4,4,13);p.Oval(24,96,2,2,42);p.Rect(46,94,2,6,13);
        p.Blob(34,80,10,4,A.Straw8,253,0,.08f);
    }
    static void Churns(Pixels p)
    {
        p.Cast(39,100,13,2);foreach(var (x,h) in new[]{(24,22),(38,26)}){Brush.Cylinder(p,x,100-h,11,h,A.Steel);p.Rect(x+2,97-h,7,3,A.Steel[2]);p.Rect(x+1,95-h,9,2,A.Steel[3]);p.Rect(x,100-h+5,11,1,A.Steel[0]);}
    }
    static void Grindstone(Pixels p)
    {
        p.Cast(39,100,13,2);p.Rect(24,84,3,16,14);p.Rect(45,84,3,16,13);p.Rect(22,90,28,2,14);p.Oval(36,82,10,10,21);p.Oval(35,81,8,8,22);p.Oval(36,82,2,2,41);p.Line(46,82,54,78,1,42);p.Rect(53,76,3,3,14);p.Set(31,76,24);
    }
    static void WaterBarrel(Pixels p)
    {
        p.Cast(39,100,12,3);Brush.Cylinder(p,26,76,20,24,A.WoodT);for(int x=30;x<46;x+=4)p.Rect(x,77,1,23,13);p.Rect(26,80,20,2,42);p.Rect(26,94,20,2,42);p.Oval(36,76,10,3,15);p.Oval(36,76,8,2,142);p.Oval(34,76,4,1,143);
    }
    static void MineCart(Pixels p,byte[]? ore)
    {
        p.Cast(39,100,16,2);p.Poly(42,18,78,54,78,50,94,22,94);p.Poly(43,18,78,36,78,35,94,22,94);p.Rect(18,77,36,2,23);p.Rect(22,93,28,2,41);foreach(int x in new[]{26,46}){p.Oval(x,97,4,4,41);p.Oval(x,97,2,2,43);}
        for(int x=24;x<50;x+=6)p.Rect(x,80,1,12,41);
        if(ore!=null){p.Blob(36,75,15,5,A.Stone8,261,0,.08f,true);foreach(var (x,y) in new[]{(26,74),(34,72),(42,75),(47,73)}){p.Rect(x,y,3,2,ore[2]);p.Set(x,y,ore[3]);}}
    }
    static void Rail(Pixels p){for(int x=25;x<48;x+=6){p.Rect(x,90,3,10,14);p.Rect(x,90,3,1,15);}p.Rect(24,91,24,2,43);p.Rect(24,92,24,1,41);p.Rect(24,97,24,2,43);p.Rect(24,98,24,1,41);}
    static void RailSide(Pixels p){for(int y=90;y<102;y+=4){p.Rect(28,y,16,2,14);p.Rect(28,y,16,1,15);}p.Rect(30,89,2,13,43);p.Rect(31,89,1,13,41);p.Rect(40,89,2,13,43);p.Rect(41,89,1,13,41);}
    static void MineSupport(Pixels p)
    {
        p.Cast(39,100,20,2);Brush.Cylinder(p,16,60,7,40,A.WoodT);Brush.Cylinder(p,49,60,7,40,A.WoodT);p.Rect(12,52,48,8,14);p.Rect(12,52,48,2,16);p.Rect(12,58,48,2,12);
        p.Line(22,72,30,60,2,13);p.Line(49,72,41,60,2,13);p.Rect(34,60,3,4,42);p.Rect(33,64,5,6,93);p.Rect(33,64,5,1,47);
    }
    static void OrePile(Pixels p,byte[] r)
    {
        p.Cast(39,100,16,3);p.Blob(28,94,9,6,A.Stone8,271,0,.06f,true);p.Blob(44,95,9,5,A.Stone8,272,-.05f,.06f,true);p.Blob(36,88,9,6,A.Stone8,273,.03f,.06f,true);
        foreach(var (x,y) in new[]{(24,92),(31,95),(36,86),(41,89),(46,94),(33,90),(50,97)}){p.Rect(x,y,3,2,r[2]);p.Set(x,y,r[3]);p.Set(x+2,y+1,r[1]);}
    }
    static void Pavilion(Pixels p,byte[] r)
    {
        p.Cast(40,100,26,3);foreach(int x in new[]{12,58}){p.Rect(x,62,2,38,14);p.Set(x,62,16);}
        for(int row=0;row<26;row++){int reach=4+row;for(int x=36-reach;x<36+reach;x++){bool stripe=((x+72)/6)%2==0;p.Set(x,36+row,stripe?(x<36?r[2]:r[1]):(x<36?(byte)86:(byte)85));}}
        for(int x=8;x<64;x++){bool stripe=((x+72)/6)%2==0;p.Rect(x,62,1,4+((x+72)%6<3?1:0),stripe?r[1]:(byte)84);}
        p.Rect(35,28,2,9,14);p.Poly(r[2],37,28,46,31,37,34);Brush.Planks(p,16,86,40,14,true,281);p.Rect(16,84,40,3,15);p.Rect(16,84,40,1,16);
        foreach(var (x,c) in new[]{(20,(byte)138),(27,(byte)151),(34,(byte)146),(41,(byte)154),(48,(byte)142)}){p.Rect(x,80,5,4,c);p.Set(x,80,161);}
    }
    static void Wagon(Pixels p)
    {
        p.Cast(40,100,30,3);Brush.Planks(p,10,78,48,14,false,291);
        for(int y=50;y<79;y++)for(int x=12;x<56;x++){float dx=(x-34)/22f,dy=(y-78)/28f;if(dx*dx+dy*dy>1)continue;p.Set(x,y,(x/7)%2==0?(x<30?(byte)86:(byte)85):(byte)84);}
        p.Rect(12,77,44,2,15);p.Oval(34,66,5,9,A.Dark[0]);foreach(int x in new[]{20,48}){p.Oval(x,93,7,7,13);p.Oval(x,93,5,5,15);p.Oval(x,93,1,1,42);p.Rect(x-5,93,11,1,13);p.Rect(x,88,1,11,13);}
        p.Line(58,88,68,94,2,14);
    }
    static void Stand(Pixels p,byte[] r)
    {
        p.Cast(40,100,22,3);p.Rect(15,66,2,34,14);p.Rect(55,66,2,34,13);Brush.Planks(p,16,84,40,16,true,301);p.Rect(16,84,40,2,16);
        for(int row=0;row<13;row++)for(int x=11-row/3;x<61+row/3;x++){bool stripe=(x+40)/7%2==0;p.Set(x,54+row,row==12?(stripe?r[0]:(byte)84):stripe?(row<3?r[3]:r[2]):(row<3?(byte)86:(byte)85));}
    }
    static void FishStand(Pixels p)
    {
        Stand(p,A.Blue);foreach(var (x,y) in new[]{(20,80),(31,81),(42,80)}){p.Poly(143,x,y+2,x+3,y,x+7,y,x+9,y+2,x+7,y+4,x+3,y+4);p.Poly(142,x+9,y+2,x+11,y,x+11,y+4);p.Set(x+2,y+1,A.Ink);}
        p.Rect(18,72,1,8,57);p.Rect(52,70,1,9,57);p.Oval(18,81,2,2,143);
    }
    static void WeaponStand(Pixels p)
    {
        Stand(p,A.Red);p.Rect(20,68,2,14,A.Steel[2]);p.Rect(19,80,4,1,A.Gold[1]);p.Rect(28,66,2,17,15);p.Poly(A.Steel[2],26,66,33,66,32,72,27,72);p.Poly(A.Steel[1],38,70,46,70,46,78,42,83,38,78);p.Set(42,74,29);p.Rect(50,68,2,15,15);p.Poly(A.Steel[2],49,68,51,62,53,68);
    }
    static void PotionStand(Pixels p)
    {
        Stand(p,A.Violet);foreach(var (x,r) in new[]{(19,A.Red),(26,A.Blue),(33,A.Green),(40,A.Yellow),(47,A.Violet)}){p.Oval(x+2,80,3,3,r[1]);p.Oval(x+1,79,2,2,r[2]);p.Rect(x+1,74,2,4,160);p.Rect(x+1,73,2,1,A.Leather[1]);p.Set(x,78,161);}
        p.Rect(22,68,10,1,14);p.Rect(40,68,10,1,14);
    }

    // ---- landmarks ------------------------------------------------------------------
    public static AssetSheet Large()
    {
        var s=new AssetSheet("landmarks",160,176,4);
        s.Add("windmill-1",p=>Windmill(p,0)).Add("windmill-2",p=>Windmill(p,1)).Add("watchtower",Watchtower).Add("lighthouse",Lighthouse)
         .Add("mine-entrance",MineEntrance).Add("cave-mouth",CaveMouth).Add("great-tree",GreatTree).Add("ruined-temple",Temple)
         .Add("wizard-tower",WizardTower).Add("castle-gate",CastleGate).Add("sailing-ship",Ship).Add("war-tent",WarTent)
         .Add("stone-bridge",StoneBridge).Add("colossus-head",Colossus).Add("dragon-bones",DragonBones).Add("great-crystal",GreatCrystal);
        return s;
    }
    const int F=172,M=80;
    static void Windmill(Pixels p,int frame)
    {
        p.Cast(86,F,34,5);
        for(int y=70;y<F;y++){float t=(y-70)/102f;int half=(int)(16+12*t);for(int x=M-half;x<M+half;x++){float a=(x-M+half)/(2f*half);p.Set(x,y,(y/7)%2==0&&(x+y/7*5)%11==0?(byte)84:a<.15f?(byte)86:a<.6f?(byte)85:(byte)84);}}
        Brush.Masonry(p,M-28,150,56,22,311);Brush.Pitch(p,M,46,22,26,A.Tile);Brush.Door(p,M-7,146,14,26);Brush.Window(p,M-5,96,10,12);Brush.Window(p,M-4,122,8,10,false);
        // Four sails on a hub; the second frame is turned an eighth.
        for(int i=0;i<4;i++)
        {
            double a=i*Math.PI/2+(frame==0?.3:.3+Math.PI/4);int ex=M+(int)(Math.Cos(a)*56),ey=66+(int)(Math.Sin(a)*56);
            p.Line(M,66,ex,ey,2,14);
            for(int k=3;k<=10;k++){int bx=M+(int)(Math.Cos(a)*k*5.4),by=66+(int)(Math.Sin(a)*k*5.4);int ox=(int)(Math.Cos(a+1.57)*9),oy=(int)(Math.Sin(a+1.57)*9);p.Line(bx,by,bx+ox,by+oy,1,k%2==0?(byte)86:(byte)85);}
            p.Line(M+(int)(Math.Cos(a)*16)+(int)(Math.Cos(a+1.57)*9),66+(int)(Math.Sin(a)*16)+(int)(Math.Sin(a+1.57)*9),ex+(int)(Math.Cos(a+1.57)*9),ey+(int)(Math.Sin(a+1.57)*9),1,15);
        }
        p.Oval(M,66,4,4,42);p.Oval(M-1,65,2,2,43);
    }
    static void Watchtower(Pixels p)
    {
        p.Cast(86,F,26,5);Brush.Masonry(p,M-22,60,44,112,321);Brush.Masonry(p,M-27,46,54,16,322);
        for(int x=M-27;x<M+27;x+=13){Brush.Box(p,x,36,9,10,A.Stone);}
        Brush.Pitch(p,M,8,24,30,A.Slate);p.Rect(M-1,-2+6,2,6,41);p.Poly(138,M+1,4,M+14,8,M+1,12);
        Brush.Door(p,M-8,146,16,26);foreach(int y in new[]{76,104,128}){p.Rect(M-3,y,6,14,13);p.Rect(M-2,y+1,4,12,y==104?(byte)94:(byte)93);}
        p.Over(M-22,130,14,40,3,21,22,20);
    }
    static void Lighthouse(Pixels p)
    {
        p.Cast(86,F,26,5);
        for(int y=44;y<F;y++){float t=(y-44)/128f;int half=(int)(13+11*t);for(int x=M-half;x<M+half;x++){float a=(x-M+half)/(2f*half);bool band=((y-44)/22)%2==0;p.Set(x,y,band?(a<.2f?(byte)98:a<.65f?(byte)161:(byte)160):(a<.2f?(byte)139:a<.65f?(byte)138:(byte)137));}}
        Brush.Masonry(p,M-28,158,56,14,331);p.Rect(M-18,40,36,5,42);p.Rect(M-18,40,36,1,43);p.Rect(M-12,22,24,18,93);p.Rect(M-12,22,24,3,47);for(int x=M-12;x<M+13;x+=6)p.Rect(x,22,1,18,41);
        Brush.Pitch(p,M,8,16,14,A.Tile);p.Rect(M-1,2,2,6,41);p.Poly(152,M+12,26,M+58,14,M+58,40,M+12,36);p.Poly(47,M+12,28,M+40,24,M+40,34,M+12,34);
        Brush.Door(p,M-7,148,14,24);Brush.Window(p,M-4,84,8,10);Brush.Window(p,M-4,118,8,10,false);
    }
    static void MineEntrance(Pixels p)
    {
        p.Cast(84,F,60,5);p.Blob(M,120,66,54,A.Stone8,341,0,.05f,true);p.Blob(40,150,26,22,A.Stone8,342,-.04f,.05f,true);p.Blob(124,152,24,20,A.Stone8,343,-.08f,.05f,true);
        p.Poly(A.Ink,M-26,F,M-26,118,M-16,104,M+16,104,M+26,118,M+26,F);p.Poly(156,M-20,F,M-20,122,M-12,112,M+12,112,M+20,122,M+20,F);
        Brush.Cylinder(p,M-32,108,8,64,A.WoodT);Brush.Cylinder(p,M+24,108,8,64,A.WoodT);p.Rect(M-36,98,72,10,14);p.Rect(M-36,98,72,2,16);p.Rect(M-36,106,72,2,12);
        p.Rect(M-16,F-6,32,2,43);p.Rect(M-16,F-2,32,2,43);for(int x=M-14;x<M+16;x+=7)p.Rect(x,F-8,3,10,14);
        p.Rect(M-3,108,2,5,42);p.Rect(M-5,113,6,8,93);p.Rect(M-5,113,6,2,47);Brush.Moss(p,20,90,120,80,501,22);
    }
    static void CaveMouth(Pixels p)
    {
        p.Cast(84,F,62,5);p.Blob(M,116,68,58,A.Stone8,351,0,.06f,true);p.Blob(34,146,24,26,A.Stone8,352,.02f,.06f,true);p.Blob(128,150,22,22,A.Stone8,353,-.08f,.06f,true);
        for(int y=96;y<=F;y++)for(int x=M-34;x<M+34;x++){float dx=(x-M)/32f,dy=(y-F)/70f;float d=dx*dx+dy*dy+.08f*MathF.Sin(x*.4f);if(d<1)p.Set(x,y,d<.62f?A.Ink:(byte)156);}
        for(int x=M-26;x<M+28;x+=9){int len=6+(int)(A.Hash(x,0,11)%12);p.Poly(21,x,104+(Math.Abs(x-M)/3),x+6,104+(Math.Abs(x-M)/3),x+3,104+(Math.Abs(x-M)/3)+len);}
        Brush.Moss(p,18,60,124,112,502,26);p.Tuft(30,F,3);p.Tuft(132,F,3);
    }
    static void GreatTree(Pixels p)
    {
        p.Cast(86,F,58,6);p.Trunk(M,84,F,30,46,0,new byte[]{12,13,14,14,15,16});
        foreach(var (ex,ey) in new[]{(22,F),(40,F),(120,F),(140,F)}){p.Line(M+(ex<M?-16:16),150,ex,ey,5,13);p.Line(M+(ex<M?-16:16),149,ex,ey-1,2,15);}
        foreach(var (x,y,rx,ry,b) in new[]{(40,84,34,26,-.2f),(120,86,34,26,-.26f),(M,96,36,20,-.3f),(M,52,52,40,-.04f),(48,48,30,26,.04f),(114,54,30,26,-.06f),(M,24,34,22,.1f)})p.Blob(x,y,rx,ry,A.Leaf8,(int)(360+x+y),b);
        Brush.Door(p,M-8,146,16,26);p.Oval(M,146,8,6,13);p.Oval(M,147,6,4,14);Brush.Window(p,M-5,118,10,10);p.Rect(M+18,128,6,8,93);p.Rect(M+18,128,6,2,47);
    }
    static void Temple(Pixels p)
    {
        p.Cast(86,F,66,5);Brush.Box(p,12,160,136,12,A.Sand);Brush.Box(p,20,150,120,10,A.Sand);
        for(int i=0;i<5;i++){int x=28+i*24,top=i==3?110:i==1?84:70;Brush.Cylinder(p,x,top,12,150-top,new byte[]{49,50,52,53});for(int y=top+6;y<148;y+=3)p.Set(x+5,y,50);Brush.Box(p,x-2,144,16,6,A.Sand);if(top==70)Brush.Box(p,x-3,64,18,7,A.Sand);else{p.Poly(51,x,top,x+4,top-5,x+8,top-1,x+12,top);}}
        Brush.Box(p,22,50,60,14,A.Sand);p.Poly(51,22,50,52,30,82,50);p.Poly(53,22,50,52,30,52,50);p.Rect(22,49,60,2,53);Brush.Box(p,124,164,20,8,A.Sand);p.Poly(51,104,172,116,160,128,172);
        Brush.Moss(p,12,60,140,112,503,18);
    }
    static void WizardTower(Pixels p)
    {
        p.Cast(86,F,24,5);
        for(int y=56;y<F;y++){float t=(y-56)/116f;int half=(int)(15+7*t*t)+(y<70?4:0);for(int x=M-half;x<M+half;x++){float a=(x-M+half)/(2f*half);int course=(y-56)/6;p.Set(x,y,(y-56)%6==5||(x+course*5)%11==10?(byte)117:a<.15f?(byte)120:a<.5f?(byte)119:a<.8f?(byte)118:(byte)117);}}
        for(int row=0;row<50;row++){int reach=1+row*24/50;for(int x=M-reach;x<M+reach;x++)p.Set(x,8+row,row%5==4?(byte)76:x<M-reach+3?(byte)79:x>M+reach-5?(byte)77:(byte)78);}
        p.Oval(M,6,4,4,150);p.Oval(M-1,5,2,2,152);Brush.Door(p,M-7,148,14,24);foreach(var (x,y) in new[]{(M-4,76),(M-4,108)}){p.Rect(x-1,y-1,10,14,117);p.Rect(x,y,8,12,54);p.Rect(x,y,8,3,56);p.Oval(x+4,y,4,2,54);}
        p.Rect(M+18,92,14,3,117);p.Rect(M+20,84,10,8,119);p.Rect(M+20,84,10,1,120);p.Set(M+34,70,56);p.Set(M-30,50,55);p.Set(M+26,30,56);p.Set(M-22,100,55);
    }
    static void CastleGate(Pixels p)
    {
        p.Cast(86,F,70,5);Brush.Masonry(p,8,70,40,102,371);Brush.Masonry(p,112,70,40,102,372);Brush.Masonry(p,48,84,64,88,373);
        foreach(int bx in new[]{8,112})for(int x=bx;x<bx+40;x+=14)Brush.Box(p,x,58,10,12,A.Stone);for(int x=48;x<112;x+=13)Brush.Box(p,x,74,9,10,A.Stone);
        p.Poly(A.Ink,56,F,56,122,M,100,104,122,104,F);for(int x=60;x<104;x+=8)p.Rect(x,112+Math.Abs(x-M)/2,3,F-112,42);for(int y=124;y<F;y+=12)p.Rect(58,y,46,2,42);
        foreach(int x in new[]{24,128}){p.Rect(x,90,8,16,13);p.Rect(x+1,91,6,14,93);p.Rect(x+1,91,6,3,47);}
        foreach(int x in new[]{28,132}){p.Rect(x,26,2,34,14);p.Poly(138,x+2,26,x+18,31,x+2,38);p.Poly(139,x+2,26,x+10,29,x+2,33);}
    }
    static void Ship(Pixels p)
    {
        p.Poly(14,14,132,146,132,132,162,34,162);p.Poly(15,14,132,80,132,76,162,34,162);p.Rect(14,130,132,3,16);p.Rect(30,160,104,3,12);for(int y=138;y<160;y+=6)p.Rect(20+(y-132)/2,y,120-(y-132),1,13);
        p.Poly(15,118,132,150,118,146,132);p.Rect(78,20,4,112,14);p.Rect(78,20,1,112,16);p.Rect(44,60,3,72,14);
        p.Poly(86,84,30,136,112,84,112);p.Poly(85,84,30,136,112,108,112);p.Poly(86,76,34,32,108,76,108);p.Poly(84,76,34,76,108,60,108);p.Poly(85,46,66,20,120,46,120);
        p.Poly(138,82,14,100,18,82,24);for(int x=40;x<130;x+=18)p.Oval(x,146,3,3,A.Ink);p.Oval(80,166,70,4,99);p.Oval(74,165,50,2,101);
    }
    static void WarTent(Pixels p)
    {
        p.Cast(86,F,66,6);
        for(int y=60;y<=F;y++){float t=(y-60)/112f;int half=(int)(8+60*t);for(int x=M-half;x<M+half;x++){bool stripe=(int)MathF.Floor((x-M+half)*10f/(2*half))%2==0;p.Set(x,y,stripe?(x<M?(byte)139:(byte)138):(x<M?(byte)86:(byte)85));}}
        p.Poly(A.Ink,M,110,M+18,F,M-18,F);p.Poly(137,M,110,M-12,F,M-18,F);p.Rect(M-1,36,3,26,14);p.Poly(151,M+2,36,M+24,42,M+2,50);p.Poly(152,M+2,36,M+12,39,M+2,44);
        for(int x=M-66;x<M+66;x+=6)p.Rect(x,F-4,3,5,(x/6)%2==0?(byte)137:(byte)84);p.Line(M-60,F,M-74,F+2,1,57);p.Line(M+60,F,M+74,F+2,1,57);
    }
    static void StoneBridge(Pixels p)
    {
        p.Cast(84,F,72,4);Brush.Masonry(p,6,120,148,52,381);
        foreach(int cx in new[]{44,116})for(int y=132;y<=F;y++)for(int x=cx-24;x<cx+24;x++){float dx=(x-cx)/24f,dy=(y-F)/40f;if(dx*dx+dy*dy<1)p.Set(x,y,dy>-.25f?(byte)142:A.Ink);}
        Brush.Box(p,2,108,156,12,A.Stone);for(int x=6;x<154;x+=20)Brush.Box(p,x,98,10,10,A.Stone);p.Oval(44,170,22,2,143);p.Oval(116,170,22,2,143);Brush.Moss(p,6,98,148,72,504,16);
    }
    static void Colossus(Pixels p)
    {
        p.Cast(86,F,56,6);p.Blob(M,120,50,52,A.Stone8,391,0,.03f,true);p.Rect(46,108,22,6,19);p.Rect(92,108,22,6,19);p.Rect(48,108,18,2,54);p.Rect(94,108,18,2,54);
        p.Poly(20,74,116,80,104,86,116,84,140,76,140);p.Poly(23,74,116,80,104,80,140,76,140);p.Rect(62,150,36,4,19);p.Rect(50,72,60,6,22);p.Rect(50,72,60,1,24);
        p.Line(100,84,116,110,1,19);p.Line(116,110,110,130,1,19);Brush.Moss(p,30,66,100,106,505,24);
    }
    static void DragonBones(Pixels p)
    {
        p.Cast(84,F,70,4);p.Line(20,160,140,150,4,58);p.Line(20,159,140,149,1,59);
        for(int i=0;i<9;i++){int x=34+i*11,h=34-Math.Abs(i-4)*5;for(int k=0;k<=10;k++){double a=Math.PI*(1+k/10.0);p.Rect(x+(int)(Math.Cos(a)*5)+5,158-i+(int)(Math.Sin(a)*h),2,2,k<5?(byte)59:(byte)58);}}
        p.Blob(22,150,16,12,A.Bone8,401,0,.03f,true);p.Oval(18,148,4,3,A.Ink);p.Poly(58,6,158,24,154,22,164);for(int x=8;x<24;x+=4)p.Rect(x,160,2,4,59);p.Line(26,140,34,122,3,58);p.Line(18,140,16,124,3,59);
        p.Line(140,150,156,162,3,58);p.Line(156,162,158,168,2,57);
    }
    static void GreatCrystal(Pixels p)
    {
        p.Cast(86,F,40,5);
        foreach(var (x,top,w) in new[]{(52,96,12),(M,30,18),(108,82,13),(34,130,8),(126,128,9),(66,120,9),(96,124,8)})
        {p.Poly(131,x-w,F,x-w,top+w*2,x,top,x+w,top+w*2,x+w,F);p.Poly(132,x-w,F,x-w,top+w*2,x,top,x,F);p.Line(x-w+2,top+w*2,x-w+2,F-3,2,133);p.Line(x,top+3,x,F-2,1,131);p.Set(x-2,top+6,161);p.Set(x-2,top+7,161);}
        p.Blob(M,F-2,50,6,A.Stone8,411,0,.05f,true);foreach(var (x,y) in new[]{(30,60),(130,50),(M,14),(20,100),(140,96)})p.Set(x,y,133);
    }
}
