using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Ability icons, 32x32 on a framed medallion in their school's colour.</summary>
public static class AbilityArt
{
    static readonly (string School,byte[] Ramp)[] Schools={("fire",A.Orange),("frost",A.Blue),("nature",A.Green),("arcane",A.Violet),("holy",A.Yellow),("shadow",A.Dark),("blood",A.Red),("steel",A.Steel)};
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("abilities",32,32,8);
        void Add(string name,int school,Action<Pixels,byte[]> glyph){var r=Schools[school].Ramp;s.Add(name,p=>{Medallion(p,r);glyph(p,r);},false,new Vector2(16,16));}
        // Fire
        Add("fireball",0,(p,r)=>{Ball(p,18,14,6,A.Flame);Tail(p,A.Flame);});Add("flame-wave",0,(p,r)=>Waves(p,A.Flame));Add("ignite",0,(p,r)=>Brush.Flame(p,16,25,12,17,1));Add("meteor",0,(p,r)=>{Ball(p,20,12,5,new byte[]{156,157,44,45});Tail(p,A.Flame);});
        // Frost
        Add("ice-lance",1,(p,r)=>Lance(p,new byte[]{99,100,101,98}));Add("frost-nova",1,(p,r)=>Nova(p,new byte[]{99,100,101,98}));Add("blizzard",1,(p,r)=>Flakes(p));Add("ice-wall",1,(p,r)=>Wall(p,new byte[]{99,100,101,98}));
        // Nature
        Add("thorns",2,(p,r)=>Thorns(p));Add("regrowth",2,(p,r)=>Leaf(p));Add("entangle",2,(p,r)=>Vines(p));Add("summon-wolf",2,(p,r)=>Paw(p,A.Leather));
        // Arcane
        Add("arcane-missile",3,(p,r)=>{Ball(p,18,14,5,A.Violet);Tail(p,A.Violet);});Add("blink",3,(p,r)=>Blink(p));Add("mana-shield",3,(p,r)=>Shield(p,A.Violet));Add("rune",3,(p,r)=>Rune(p,A.Violet));
        // Holy
        Add("heal",4,(p,r)=>Cross(p,A.Yellow));Add("smite",4,(p,r)=>Bolt(p,A.Yellow));Add("bless",4,(p,r)=>Wings(p));Add("sanctuary",4,(p,r)=>Shield(p,A.Yellow));
        // Shadow
        Add("shadow-bolt",5,(p,r)=>{Ball(p,18,14,5,A.Violet);Tail(p,new byte[]{156,157,117,118});});Add("curse",5,(p,r)=>Eye(p));Add("stealth",5,(p,r)=>Hood(p));Add("fear",5,(p,r)=>SkullGlyph(p));
        // Blood
        Add("bleed",6,(p,r)=>Drops(p));Add("drain-life",6,(p,r)=>Heart(p,true));Add("frenzy",6,(p,r)=>Claws(p));Add("vitality",6,(p,r)=>Heart(p,false));
        // Steel
        Add("strike",7,(p,r)=>Sword(p,A.Steel));Add("cleave",7,(p,r)=>Axe(p));Add("guard",7,(p,r)=>Shield(p,A.Steel));Add("dash",7,(p,r)=>Dash(p));
        Add("whirlwind",7,(p,r)=>Whirl(p));Add("shield-bash",7,(p,r)=>{Shield(p,A.Steel);Lines(p);});Add("taunt",7,(p,r)=>Shout(p));Add("war-cry",6,(p,r)=>Shout(p));
        return s;
    }
    static void Medallion(Pixels p,byte[] r)
    {
        for(int y=1;y<31;y++)for(int x=1;x<31;x++){float d=MathF.Sqrt((x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f));if(d>14.5f)continue;
            p.Set(x,y,d>13.2f?A.Gold[1]:d>12.2f?(x+y<30?A.Gold[3]:A.Gold[2]):d<12.2f&&x+y<26?r[1]:r[0]);}
        for(int y=4;y<14;y++)for(int x=4;x<14;x++){float d=MathF.Sqrt((x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f));if(d<12&&d>9&&(x+y)%2==0)p.Set(x,y,r[2]);}
    }
    static void Ball(Pixels p,int x,int y,int rad,byte[] c){p.Oval(x,y,rad,rad,c[1]);p.Oval(x-1,y-1,rad-2,rad-2,c[2]);p.Oval(x-2,y-2,Math.Max(1,rad-4),Math.Max(1,rad-4),c[3]);}
    static void Tail(Pixels p,byte[] c){for(int i=0;i<8;i++){p.Line(13-i,19+i,9-i,17+i,1,i<3?c[2]:c[1]);}p.Line(14,20,7,27,2,c[1]);}
    static void Waves(Pixels p,byte[] c){for(int k=0;k<3;k++)for(int x=6;x<26;x++){int y=9+k*6+(int)MathF.Round(2*MathF.Sin(x*.6f+k));p.Rect(x,y,1,2,k==1?c[3]:c[2]);}}
    static void Lance(Pixels p,byte[] c){p.Line(7,25,23,9,3,c[1]);p.Line(7,24,22,9,1,c[3]);p.Poly(c[2],21,7,26,6,25,11);p.Set(23,8,98);}
    static void Nova(Pixels p,byte[] c){for(int i=0;i<8;i++){double a=i*.785;p.Line(16,16,16+(int)(Math.Cos(a)*10),16+(int)(Math.Sin(a)*10),i%2==0?2:1,c[2]);}p.Oval(16,16,3,3,c[3]);}
    static void Flakes(Pixels p){foreach(var (x,y,s) in new[]{(11,11,4),(21,13,3),(15,22,3),(23,22,2)}){for(int i=0;i<3;i++){double a=i*1.047;p.Line(x-(int)(Math.Cos(a)*s),y-(int)(Math.Sin(a)*s),x+(int)(Math.Cos(a)*s),y+(int)(Math.Sin(a)*s),1,98);}}}
    static void Wall(Pixels p,byte[] c){foreach(var (x,top,w) in new[]{(10,10,3),(16,6,4),(22,11,3)}){p.Poly(c[1],x-w,25,x-w,top+w,x,top,x+w,top+w,x+w,25);p.Poly(c[2],x-w,25,x-w,top+w,x,top,x,25);p.Set(x-1,top+2,98);}}
    static void Thorns(Pixels p){for(int i=0;i<3;i++){int y=10+i*6;p.Line(6,y+4,26,y,1,A.Green[0]);for(int x=8;x<26;x+=4)p.Poly(A.Green[2],x,y+3-(x-6)/5,x+2,y-2-(x-6)/5,x+3,y+2-(x-6)/5);}}
    static void Leaf(Pixels p){p.Poly(A.Green[1],8,24,10,12,18,6,25,7,23,15,15,22);p.Poly(A.Green[2],8,24,10,12,18,6,21,8);p.Line(8,24,22,9,1,A.Green[3]);p.Rect(20,18,5,1,98);p.Rect(22,16,1,5,98);}
    static void Vines(Pixels p){for(int k=0;k<3;k++)for(int y=6;y<27;y++){int x=9+k*7+(int)MathF.Round(3*MathF.Sin(y*.5f+k*2));p.Set(x,y,A.Green[2]);if(y%5==k){p.Rect(x+1,y,2,2,A.Green[3]);}}}
    static void Paw(Pixels p,byte[] c){p.Oval(16,20,5,4,c[2]);p.Oval(15,19,3,2,c[3]);foreach(var (x,y) in new[]{(9,13),(13,9),(19,9),(23,13)})p.Oval(x,y,2,2,c[2]);}
    static void Blink(Pixels p){for(int k=0;k<3;k++){int x=8+k*6;p.Oval(x,16,3,6,k==2?A.Violet[3]:A.Violet[1+k%2]);}p.Poly(A.Violet[3],22,10,27,16,22,22);}
    static void Shield(Pixels p,byte[] c){p.Poly(c[1],8,7,24,7,24,16,16,26,8,16);p.Poly(c[2],9,8,16,8,16,25,9,16);p.Rect(15,8,2,17,c[0]);p.Rect(9,13,15,2,c[0]);p.Rect(9,8,7,1,c[3]);}
    static void Rune(Pixels p,byte[] c){p.Rect(15,6,2,20,c[3]);p.Line(9,10,23,22,1,c[2]);p.Line(23,10,9,22,1,c[2]);p.Oval(16,16,4,4,c[3]);p.Oval(16,16,2,2,c[0]);}
    static void Cross(Pixels p,byte[] c){p.Rect(13,6,6,20,c[2]);p.Rect(6,13,20,6,c[2]);p.Rect(13,6,2,20,c[3]);p.Rect(6,13,20,2,c[3]);p.Rect(15,15,2,2,98);}
    static void Bolt(Pixels p,byte[] c){p.Poly(c[2],18,4,9,18,15,18,12,28,23,12,17,12,21,4);p.Poly(c[3],18,4,12,16,15,16,17,6);}
    static void Wings(Pixels p){foreach(int d in new[]{-1,1})for(int i=0;i<5;i++)p.Line(16+d*2,14+i,16+d*(12-i),8+i*3,1,i%2==0?(byte)98:A.Yellow[3]);p.Oval(16,13,2,2,A.Yellow[2]);}
    static void Eye(Pixels p){p.Oval(16,16,10,6,161);p.Oval(16,16,5,5,A.Violet[1]);p.Oval(16,16,2,2,A.Ink);p.Set(14,14,98);p.Line(6,16,4,12,1,A.Violet[2]);p.Line(26,16,28,12,1,A.Violet[2]);}
    static void Hood(Pixels p){p.Poly(A.Dark[2],16,5,25,13,25,27,7,27,7,13);p.Poly(A.Dark[3],16,5,7,13,7,27,12,27);p.Oval(16,18,5,6,A.Ink);p.Set(14,17,A.Violet[3]);p.Set(18,17,A.Violet[3]);}
    static void SkullGlyph(Pixels p){p.Oval(16,13,8,7,58);p.Oval(15,12,6,5,59);p.Rect(11,18,10,6,58);p.Oval(12,14,2,2,A.Ink);p.Oval(20,14,2,2,A.Ink);p.Poly(A.Ink,16,17,14,20,18,20);for(int x=12;x<21;x+=2)p.Rect(x,22,1,2,57);}
    static void Drops(Pixels p){foreach(var (x,y,s) in new[]{(11,12,4),(20,10,3),(18,21,4)}){p.Poly(A.Red[1],x,y-s-2,x+s,y+1,x,y+s,x-s,y+1);p.Oval(x,y+1,s,s,A.Red[1]);p.Oval(x-1,y,s/2,s/2,A.Red[3]);}}
    static void Heart(Pixels p,bool drain){p.Poly(A.Red[1],16,26,6,15,6,10,10,7,16,11,22,7,26,10,26,15);p.Poly(A.Red[2],16,25,7,15,7,10,10,8,16,12);p.Oval(10,11,2,2,A.Red[3]);if(drain)for(int i=0;i<4;i++)p.Set(16+(i%2==0?-1:1),3+i*2,A.Red[3]);else{p.Rect(19,14,5,1,98);p.Rect(21,12,1,5,98);}}
    static void Claws(Pixels p){for(int i=0;i<3;i++){p.Line(9+i*6,6,13+i*6,26,2,98);p.Line(10+i*6,6,14+i*6,26,1,A.Red[3]);}}
    static void Sword(Pixels p,byte[] r){p.Line(9,23,23,9,2,r[2]);p.Line(9,22,22,9,1,r[3]);p.Line(7,19,13,25,2,A.Gold[2]);p.Line(5,27,8,24,2,A.Leather[1]);}
    static void Axe(Pixels p){p.Line(8,26,20,10,2,A.WoodT[2]);p.Poly(A.Steel[2],17,6,26,5,27,14,22,18,18,12);p.Line(18,6,26,5,1,A.Steel[3]);p.Line(22,18,27,14,1,A.Steel[0]);}
    static void Dash(Pixels p){p.Poly(A.Steel[3],22,16,14,8,14,13,6,13,6,19,14,19,14,24);for(int i=0;i<3;i++)p.Rect(4-i,10+i*5,3,1,A.Steel[2]);}
    static void Whirl(Pixels p){for(int i=0;i<36;i++){double a=i*.2;int r=3+i/4;p.Rect(16+(int)(Math.Cos(a)*r),16+(int)(Math.Sin(a)*r),2,2,i<12?A.Steel[3]:A.Steel[2]);}}
    static void Lines(Pixels p){for(int i=0;i<3;i++)p.Line(26,10+i*5,29,9+i*5,1,98);}
    static void Shout(Pixels p){p.Oval(11,16,5,6,163);p.Oval(13,18,2,2,A.Ink);for(int k=0;k<3;k++)for(int i=-4;i<=4;i++)p.Set(19+k*3+(int)(Math.Sqrt(16-i*i)*.5),16+i*(1+k/2),98);}
}

/// <summary>Interface pieces: nine-slice panel frames, buttons, bars, slots, cursors and
/// small status glyphs. 48x48 cells; nine-slice frames use a 12-pixel border.</summary>
public static class InterfaceArt
{
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("interface",48,48,8);
        void Add(string name,Action<Pixels> paint)=>s.Modules.Add(new AssetModule(name,48,48,new Vector2(24,24),()=>{var p=new Pixels(48,48);paint(p);return p.Data;},true));
        Add("panel-wood",p=>Frame(p,A.Wood,new byte[]{14,15},A.Gold));Add("panel-stone",p=>Frame(p,A.Stone,new byte[]{21,22},A.Steel));Add("panel-parchment",p=>Frame(p,new byte[]{57,58,58,59,65,65},new byte[]{65,59},A.Leather));Add("panel-dark",p=>Frame(p,new byte[]{156,156,157,157,158,159},new byte[]{156,157},A.Gold));
        Add("panel-royal",p=>Frame(p,new byte[]{60,61,61,62,62,62},new byte[]{60,61},A.Gold));Add("panel-frost",p=>Frame(p,new byte[]{99,99,100,100,101,98},new byte[]{99,100},A.Steel));Add("panel-leaf",p=>Frame(p,new byte[]{1,2,3,3,4,5},new byte[]{2,3},A.WoodT));Add("panel-arcane",p=>Frame(p,new byte[]{117,117,118,118,119,120},new byte[]{117,118},A.Gold));
        foreach(var (name,r) in new[]{("red",A.Red),("blue",A.Blue),("green",A.Green),("gold",A.Gold)}){Add("button-"+name,p=>Button(p,r,false));Add("button-"+name+"-pressed",p=>Button(p,r,true));}
        Add("slot",p=>Slot(p,null));Add("slot-selected",p=>Slot(p,A.Gold));Add("slot-locked",p=>{Slot(p,null);Lock(p);});Add("slot-rare",p=>Slot(p,A.Blue));Add("slot-epic",p=>Slot(p,A.Violet));Add("slot-legendary",p=>Slot(p,A.Orange));Add("slot-cursed",p=>Slot(p,A.Red));Add("slot-round",RoundSlot);
        foreach(var (name,r) in new[]{("health",A.Red),("mana",A.Blue),("stamina",A.Green),("xp",A.Yellow)})Add("bar-"+name,p=>Bar(p,r));
        Add("bar-empty",p=>Bar(p,null));Add("divider",Divider);Add("corner-ornament",Corner);Add("tab",Tab);
        Add("cursor-hand",Hand);Add("cursor-sword",p=>{p.Line(10,10,30,30,3,A.Steel[2]);p.Line(10,10,29,29,1,A.Steel[3]);p.Line(26,32,32,26,2,A.Gold[2]);p.Line(31,31,36,36,2,A.Leather[1]);});Add("cursor-target",Target);Add("cursor-grab",Grab);
        Add("arrow-up",p=>Arrow(p,0));Add("arrow-right",p=>Arrow(p,1));Add("arrow-down",p=>Arrow(p,2));Add("arrow-left",p=>Arrow(p,3));
        Add("speech-bubble",Bubble);Add("thought-bubble",Thought);Add("exclaim",p=>Mark(p,true));Add("question",p=>Mark(p,false));
        Add("status-poison",p=>Status(p,A.Green,0));Add("status-burn",p=>Status(p,A.Orange,1));Add("status-freeze",p=>Status(p,A.Blue,2));Add("status-stun",p=>Status(p,A.Yellow,3));
        Add("status-sleep",p=>Status(p,A.Violet,4));Add("status-bleed",p=>Status(p,A.Red,5));Add("status-regen",p=>Status(p,A.Green,6));Add("status-shield",p=>Status(p,A.Steel,7));
        Add("map-pin",Pin);Add("map-flag",Flag);Add("map-x",p=>{p.Line(14,14,34,34,4,A.Red[1]);p.Line(34,14,14,34,4,A.Red[1]);p.Line(14,14,34,34,1,A.Red[3]);});Add("map-star",p=>{p.Poly(A.Yellow[2],24,8,28,19,40,19,30,26,34,38,24,31,14,38,18,26,8,19,20,19);p.Poly(A.Yellow[3],24,8,24,31,14,38,18,26,8,19,20,19);});
        return s;
    }
    static void Frame(Pixels p,byte[] r,byte[] fill,byte[] trim)
    {
        p.Rect(2,2,44,44,fill[0]);for(int y=12;y<36;y++)for(int x=12;x<36;x++)p.Set(x,y,((x+y)/3)%2==0?fill[0]:fill[1]);
        for(int i=0;i<10;i++){byte c=i<1?A.Ink:i<3?r[5]:i<6?r[3]:i<8?r[2]:r[1];for(int k=2+i;k<46-i;k++){p.Set(k,2+i,c);p.Set(2+i,k,c);p.Set(k,45-i,i<3?r[1]:c);p.Set(45-i,k,i<3?r[1]:c);}}
        foreach(var (x,y) in new[]{(4,4),(40,4),(4,40),(40,40)}){p.Rect(x,y,4,4,trim[2]);p.Rect(x,y,2,2,trim[3]);p.Set(x+3,y+3,trim[0]);}
        p.Rect(2,2,44,1,A.Ink);p.Rect(2,45,44,1,A.Ink);p.Rect(2,2,1,44,A.Ink);p.Rect(45,2,1,44,A.Ink);
    }
    static void Button(Pixels p,byte[] r,bool pressed)
    {
        int lift=pressed?2:0;p.Rect(4,14,40,22,A.Ink);p.Rect(5,32,38,3,r[0]);p.Rect(5,15+lift,38,17-lift+(pressed?0:0),r[1]);p.Rect(6,16+lift,36,7,r[2]);p.Rect(6,16+lift,36,2,r[3]);
        if(!pressed){p.Rect(5,31,38,1,r[0]);}p.Set(8,18+lift,161);p.Set(9,18+lift,161);
    }
    static void Slot(Pixels p,byte[]? glow)
    {
        p.Rect(6,6,36,36,A.Ink);p.Rect(7,7,34,34,glow==null?(byte)157:glow[0]);p.Rect(9,9,30,30,156);p.Rect(9,9,30,2,A.Ink);p.Rect(9,9,2,30,A.Ink);p.Rect(9,37,30,2,158);p.Rect(37,9,2,30,158);
        if(glow!=null){p.Rect(7,7,34,1,glow[3]);p.Rect(7,7,1,34,glow[2]);p.Rect(7,40,34,1,glow[1]);p.Rect(40,7,1,34,glow[1]);foreach(var (x,y) in new[]{(7,7),(39,7),(7,39),(39,39)})p.Rect(x,y,2,2,glow[3]);}
    }
    static void Lock(Pixels p){p.Rect(19,24,10,9,A.Gold[2]);p.Rect(19,24,10,1,A.Gold[3]);for(int i=0;i<=6;i++){double a=Math.PI*(1+i/6.0);p.Rect(24+(int)Math.Round(Math.Cos(a)*4)-1,24+(int)Math.Round(Math.Sin(a)*5),2,2,A.Gold[1]);}p.Rect(23,27,2,3,A.Ink);}
    static void RoundSlot(Pixels p){p.Oval(24,24,19,19,A.Ink);p.Oval(24,24,18,18,A.Gold[1]);p.Oval(23,23,16,16,A.Gold[3]);p.Oval(24,24,15,15,A.Gold[1]);p.Oval(24,24,14,14,156);p.Oval(25,25,12,12,157);}
    static void Bar(Pixels p,byte[]? r)
    {
        p.Rect(2,18,44,12,A.Ink);p.Rect(3,19,42,10,A.Gold[1]);p.Rect(3,19,42,1,A.Gold[3]);p.Rect(5,21,38,6,156);
        if(r!=null){p.Rect(5,21,38,6,r[1]);p.Rect(5,21,38,2,r[2]);p.Rect(5,21,38,1,r[3]);p.Rect(5,26,38,1,r[0]);for(int x=11;x<43;x+=8)p.Rect(x,21,1,6,r[0]);}
    }
    static void Divider(Pixels p){p.Rect(2,23,44,2,A.Gold[1]);p.Rect(2,23,44,1,A.Gold[3]);p.Poly(A.Gold[2],24,18,29,24,24,30,19,24);p.Poly(A.Gold[3],24,18,24,30,19,24);p.Oval(24,24,1,1,A.Red[2]);}
    static void Corner(Pixels p){for(int i=0;i<14;i++){p.Set(6+i,6,A.Gold[2]);p.Set(6,6+i,A.Gold[2]);}for(int i=0;i<=10;i++){double a=i*.157;p.Set(8+(int)(Math.Cos(a)*12),8+(int)(Math.Sin(a)*12),A.Gold[3]);}p.Oval(9,9,3,3,A.Gold[1]);p.Oval(9,9,1,1,A.Red[2]);p.Set(21,8,A.Gold[3]);p.Set(8,21,A.Gold[3]);}
    static void Tab(Pixels p){p.Poly(A.Ink,4,44,8,10,40,10,44,44);p.Poly(A.Wood[3],6,44,10,12,38,12,42,44);p.Rect(10,12,28,2,A.Wood[5]);p.Rect(7,30,2,14,A.Wood[4]);p.Rect(39,30,2,14,A.Wood[2]);}
    static void Hand(Pixels p)
    {
        p.Poly(A.Ink,14,8,20,8,20,20,34,22,36,26,34,40,20,42,12,34,10,24,14,22);p.Poly(163,15,9,19,9,19,22,33,23,35,26,33,39,20,41,13,34,11,24,15,23);p.Poly(162,26,24,33,24,32,38,24,38);
        foreach(int x in new[]{23,28})p.Rect(x,22,1,6,162);p.Rect(15,10,3,2,98);
    }
    static void Target(Pixels p){p.Oval(24,24,14,14,A.Red[1]);p.Oval(24,24,12,12,0);p.Oval(24,24,5,5,A.Red[2]);p.Oval(24,24,3,3,0);foreach(var (x,y,w,h) in new[]{(23,4,2,8),(23,36,2,8),(4,23,8,2),(36,23,8,2)})p.Rect(x,y,w,h,A.Red[3]);p.Set(24,24,98);}
    static void Grab(Pixels p){p.Oval(24,28,11,10,A.Ink);p.Oval(24,28,10,9,163);for(int x=15;x<34;x+=5){p.Oval(x,18,2,3,A.Ink);p.Oval(x,18,2,2,163);}p.Oval(21,26,4,3,162);}
    static void Arrow(Pixels p,int dir)
    {
        int[] pts={24,8,38,24,30,24,30,40,18,40,18,24,10,24};var q=new int[pts.Length];
        for(int i=0;i<pts.Length;i+=2){int x=pts[i]-24,y=pts[i+1]-24;for(int k=0;k<dir;k++)(x,y)=(-y,x);q[i]=x+24;q[i+1]=y+24;}
        p.Poly(A.Gold[1],q);var l=new int[q.Length];Array.Copy(q,l,q.Length);p.Poly(A.Gold[2],l[0],l[1],l[10],l[11],l[12],l[13]);
    }
    static void Bubble(Pixels p){p.Oval(24,20,20,14,A.Ink);p.Oval(24,20,19,13,161);p.Poly(A.Ink,14,30,10,44,26,32);p.Poly(161,15,31,12,41,24,32);p.Oval(20,15,6,3,98);foreach(int x in new[]{16,24,32})p.Oval(x,21,2,2,A.Dark[2]);}
    static void Thought(Pixels p){p.Oval(26,18,18,12,A.Ink);p.Oval(26,18,17,11,161);foreach(var (x,y,r) in new[]{(12,34,4),(7,41,2)}){p.Oval(x,y,r+1,r+1,A.Ink);p.Oval(x,y,r,r,161);}p.Oval(22,14,5,3,98);}
    static void Mark(Pixels p,bool exclaim)
    {
        var r=exclaim?A.Yellow:A.Blue;p.Oval(24,24,15,15,A.Ink);p.Oval(24,24,14,14,r[1]);p.Oval(22,22,10,10,r[2]);
        if(exclaim){p.Rect(21,12,6,15,A.Ink);p.Rect(22,13,4,13,98);p.Rect(21,30,6,6,A.Ink);p.Rect(22,31,4,4,98);}
        else{for(int i=0;i<=8;i++){double a=Math.PI*(1+i/8.0)-.3;p.Rect(24+(int)(Math.Cos(a)*6)-1,18+(int)(Math.Sin(a)*6)-1,4,4,A.Ink);}p.Rect(23,22,4,7,A.Ink);p.Rect(22,31,6,6,A.Ink);for(int i=0;i<=8;i++){double a=Math.PI*(1+i/8.0)-.3;p.Rect(24+(int)(Math.Cos(a)*6),18+(int)(Math.Sin(a)*6),2,2,98);}p.Rect(24,23,2,5,98);p.Rect(23,32,4,4,98);}
    }
    static void Status(Pixels p,byte[] r,int kind)
    {
        p.Oval(24,24,15,15,A.Ink);p.Oval(24,24,14,14,r[0]);p.Oval(23,23,12,12,r[1]);
        switch(kind)
        {
            case 0:foreach(var (x,y,s) in new[]{(19,28,5),(28,20,4),(30,31,3)}){p.Oval(x,y,s,s,r[2]);p.Oval(x-1,y-1,s/2,s/2,r[3]);}break;
            case 1:Brush.Flame(p,24,36,14,22,1);break;
            case 2:for(int i=0;i<3;i++){double a=i*1.047;p.Line(24-(int)(Math.Cos(a)*11),24-(int)(Math.Sin(a)*11),24+(int)(Math.Cos(a)*11),24+(int)(Math.Sin(a)*11),2,98);}break;
            case 3:for(int i=0;i<5;i++){double a=i*1.2566-1.57;Star(p,24+(int)(Math.Cos(a)*9),24+(int)(Math.Sin(a)*5));}break;
            case 4:foreach(var (x,y,s) in new[]{(18,30,5),(26,22,4),(32,14,3)}){p.Rect(x,y,s*2,1,98);p.Line(x+s*2,y,x,y+s*2,1,98);p.Rect(x,y+s*2,s*2,1,98);}break;
            case 5:foreach(var (x,y) in new[]{(18,18),(28,22),(22,30)}){p.Poly(r[3],x,y-5,x+3,y,x,y+4,x-3,y);p.Oval(x,y+1,3,3,r[3]);}break;
            case 6:p.Rect(21,12,6,24,98);p.Rect(12,21,24,6,98);p.Rect(21,12,2,24,r[3]);break;
            case 7:p.Poly(98,15,14,33,14,33,24,24,36,15,24);p.Poly(r[3],17,16,24,16,24,33,17,24);break;
        }
    }
    static void Star(Pixels p,int x,int y){p.Set(x,y,98);p.Set(x+1,y,151);p.Set(x-1,y,151);p.Set(x,y+1,151);p.Set(x,y-1,151);}
    static void Pin(Pixels p){p.Oval(24,18,10,10,A.Ink);p.Oval(24,18,9,9,A.Red[1]);p.Oval(22,16,6,6,A.Red[2]);p.Poly(A.Red[1],16,24,32,24,24,42);p.Oval(24,18,4,4,161);p.Oval(21,14,2,2,A.Red[3]);}
    static void Flag(Pixels p){p.Rect(14,6,3,38,A.WoodT[1]);p.Rect(14,6,1,38,A.WoodT[3]);p.Poly(A.Green[1],17,8,38,13,17,26);p.Poly(A.Green[2],17,8,27,10,17,18);p.Oval(15,44,6,2,A.Penumbra);}
}
