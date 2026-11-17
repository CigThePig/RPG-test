using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Nature for lands beyond the Reach: autumn wood, blossom grove, snowfield, desert,
/// swamp, mystic forest, coast and farmland. Standard 72x104 cell, foot at (36,100).</summary>
public static class BiomeArt
{
    static readonly byte[] Bark={12,13,14,14,15,16},Pale={18,17,17,17,17,18},Grey={19,20,21,21,22,23},Dusk={156,157,158,158,159,160};
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("biomes",72,104,8);
        // Broadleaf trees in every season and mood.
        foreach(var (name,r,bark) in new[]{("autumn",A.Autumn8,Bark),("golden",A.Gold8,Pale),("blossom",A.Blossom8,Bark),("mystic",A.Violet8,Dusk),("swamp",A.Swamp8,Grey),("frost",A.Snow8,Grey)})
            for(int v=0;v<3;v++){int k=v;s.Add($"tree-{name}-{v+1}",p=>Broadleaf(p,r,bark,k));}
        s.Add("pine-snow-1",p=>Pine(p,0,true)).Add("pine-snow-2",p=>Pine(p,1,true)).Add("pine-snow-3",p=>Pine(p,2,true)).Add("pine-tall",p=>Pine(p,1,false)).Add("pine-young",p=>Pine(p,2,false))
         .Add("tree-dead-1",p=>Dead(p,0,Grey)).Add("tree-dead-2",p=>Dead(p,1,Bark)).Add("tree-dead-snow",p=>{Dead(p,0,Grey);Snowcap(p,10,20,52,70,60);})
         .Add("palm-1",p=>Palm(p,0)).Add("palm-2",p=>Palm(p,1)).Add("willow-1",p=>Willow(p,0)).Add("willow-2",p=>Willow(p,1))
         .Add("bamboo-1",p=>Bamboo(p,0)).Add("bamboo-2",p=>Bamboo(p,1)).Add("cypress",Cypress).Add("fruit-tree",FruitTree);
        // Desert.
        s.Add("cactus-saguaro",p=>Cactus(p,0)).Add("cactus-twin",p=>Cactus(p,1)).Add("cactus-barrel",p=>Cactus(p,2)).Add("cactus-pear",p=>Cactus(p,3))
         .Add("rock-desert-1",p=>Rock(p,A.Sand8,0)).Add("rock-desert-2",p=>Rock(p,A.Sand8,1)).Add("rock-mesa",p=>Rock(p,A.Sand8,2)).Add("bush-dry",DryBush)
         .Add("tumbleweed",Tumbleweed).Add("beast-skull",BeastSkull).Add("ribs",Ribs).Add("dune-grass",DuneGrass);
        // Snow and ice.
        s.Add("rock-snow-1",p=>{Rock(p,A.Stone8,0);Snowcap(p,14,60,44,36,80);}).Add("rock-snow-2",p=>{Rock(p,A.Stone8,1);Snowcap(p,14,60,44,36,81);})
         .Add("ice-crystal-1",p=>IceCrystal(p,0)).Add("ice-crystal-2",p=>IceCrystal(p,1)).Add("snowman",Snowman).Add("snow-drift",SnowDrift)
         .Add("bush-snow",p=>{Bush(p,A.Pine8,0);Snowcap(p,22,78,30,10,82);}).Add("frozen-reeds",p=>Reeds(p,A.Snow,true));
        // Stone and standing things.
        s.Add("rock-moss",p=>{Rock(p,A.Stone8,2);Moss(p,16,62,40,22,90);}).Add("boulder-pile",p=>Rock(p,A.Stone8,3)).Add("standing-stone-1",p=>Menhir(p,0)).Add("standing-stone-2",p=>Menhir(p,1))
         .Add("standing-stone-3",p=>Menhir(p,2)).Add("stone-arch",StoneArch).Add("cairn",Cairn).Add("stump-mossy",Stump);
        // Bushes, flowers and fungi.
        s.Add("bush-autumn",p=>Bush(p,A.Autumn8,1)).Add("bush-berry-red",p=>{Bush(p,A.Leaf8,0);Dots(p,A.Red,91);}).Add("bush-berry-blue",p=>{Bush(p,A.Leaf8,1);Dots(p,A.Blue,92);}).Add("bush-rose",p=>{Bush(p,A.Leaf8,0);Dots(p,A.Pink,93);})
         .Add("flowers-red",p=>Flowers(p,A.Red)).Add("flowers-blue",p=>Flowers(p,A.Blue)).Add("flowers-yellow",p=>Flowers(p,A.Yellow)).Add("flowers-white",p=>Flowers(p,A.Pale))
         .Add("mushroom-giant-red",p=>GiantCap(p,A.Red,0)).Add("mushroom-giant-violet",p=>GiantCap(p,A.Violet,1)).Add("mushroom-giant-teal",p=>GiantCap(p,A.Teal,2)).Add("mushroom-ring",MushroomRing)
         .Add("fern-large",Fern).Add("reeds",p=>Reeds(p,A.Green,false)).Add("vine-post",VinePost).Add("log-hollow",HollowLog);
        // Swamp and coast.
        s.Add("swamp-root",SwampRoot).Add("lily-giant",GiantLily).Add("coral-red",p=>Coral(p,A.Red)).Add("coral-violet",p=>Coral(p,A.Violet))
         .Add("driftwood",Driftwood).Add("shell-pile",ShellPile).Add("seaweed",Seaweed).Add("tide-rock",p=>{Rock(p,A.Stone8,0);p.Over(18,92,36,6,99,19,20,21,22);});
        // Farmland: crops by growth stage.
        for(int stage=0;stage<3;stage++){int k=stage;s.Add($"wheat-{stage+1}",p=>Wheat(p,k));}
        for(int stage=0;stage<3;stage++){int k=stage;s.Add($"corn-{stage+1}",p=>Corn(p,k));}
        s.Add("pumpkin",Pumpkin).Add("cabbage",Cabbage).Add("sunflower",Sunflower).Add("tomato-vine",Tomato)
         .Add("grape-vine",Grapes).Add("beehive",Beehive).Add("haystack-large",Haystack).Add("turnips",Turnips);
        return s;
    }

    static void Broadleaf(Pixels p,byte[] r,byte[] bark,int v)
    {
        if(v==0)
        {
            p.Cast(40,99,23,3);p.Trunk(36,60,100,7,11,0,bark);p.Line(34,66,22,54,3,bark[1]);p.Line(38,66,50,54,3,bark[1]);
            p.Blob(22,53,15,12,r,11,-.20f);p.Blob(51,54,15,12,r,12,-.24f);p.Blob(36,59,17,10,r,13,-.30f);
            p.Blob(36,39,22,19,r,14,-.04f);p.Blob(24,37,14,12,r,15,.04f);p.Blob(48,40,14,12,r,16,-.04f);p.Blob(35,25,15,11,r,17,.08f);
        }
        if(v==1)
        {
            p.Cast(39,99,17,3);p.Trunk(36,50,100,6,9,-1,bark);p.Line(35,60,27,50,2,bark[1]);p.Line(37,58,45,48,2,bark[1]);
            p.Blob(36,55,15,9,r,21,-.30f);p.Blob(26,46,12,12,r,22,-.14f);p.Blob(47,47,12,12,r,23,-.20f);
            p.Blob(36,33,16,15,r,24,-.02f);p.Blob(30,21,12,11,r,25,.06f);p.Blob(42,15,10,9,r,26,.04f);p.Blob(35,10,8,6,r,27,.10f);
        }
        if(v==2)
        {
            // Round young tree on a slim stem.
            p.Cast(39,99,14,2);p.Trunk(36,62,100,4,6,1,bark);
            p.Blob(36,58,14,9,r,31,-.28f);p.Blob(36,46,17,14,r,32,-.02f);p.Blob(28,42,9,8,r,33,.06f);p.Blob(44,44,9,8,r,34,-.06f);p.Blob(36,34,10,8,r,35,.1f);
        }
        // A few fallen leaves at the foot.
        for(int i=0;i<5;i++)p.Set(22+(int)(A.Hash(i,v,1)%30),98+(int)(A.Hash(i,v,2)%3),r[4+i%3]);
    }
    static void Snowcap(Pixels p,int x,int y,int w,int h,int slot)
    {
        // Snow lies on whatever faces up: any painted pixel with open air above it.
        for(int xx=x;xx<x+w;xx++)for(int yy=y;yy<y+h;yy++)
        {
            byte c=p.At(xx,yy);if(c==0||c==A.Shadow||c==A.Penumbra||c>=95&&c<=98)continue;
            if(p.At(xx,yy-1)!=0&&p.At(xx,yy-1)<95)continue;
            int depth=3+(int)(A.Hash(xx/3,slot,3)%3);
            for(int d=0;d<depth;d++)if(p.At(xx,yy+d)!=0)p.Set(xx,yy+d,d==0?(byte)98:d==depth-1?(byte)96:(byte)97);
            break;
        }
    }
    static void Moss(Pixels p,int x,int y,int w,int h,int slot)
    {
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)
        {byte c=p.At(xx,yy);if(c is not (>=19 and <=24))continue;if(A.Hash(xx/3,yy/2,slot)%100<45)p.Set(xx,yy,A.Hash(xx,yy,slot+1)%4==0?(byte)5:c>=23?(byte)4:(byte)3);}
    }
    static void Pine(Pixels p,int v,bool snow)
    {
        if(v==0){p.Cast(39,99,16,3);p.Trunk(36,80,100,5,7,0,Bark);p.Tier(36,60,90,21,31);p.Tier(36,44,74,18,32);p.Tier(36,29,58,14,33);p.Tier(36,16,42,10,34);p.Tier(36,5,27,6,35);}
        if(v==1){p.Cast(39,99,13,3);p.Trunk(36,84,100,4,6,0,Bark);p.Tier(36,66,92,15,41);p.Tier(36,52,78,13,42);p.Tier(36,38,64,11,43);p.Tier(36,25,50,9,44);p.Tier(36,13,37,7,45);p.Tier(36,3,24,4,46);}
        if(v==2){p.Cast(38,99,11,2);p.Trunk(36,88,100,3,5,0,Bark);p.Tier(36,70,94,13,51);p.Tier(36,56,80,10,52);p.Tier(36,44,66,7,53);p.Tier(36,35,54,4,54);}
        if(!snow)return;
        // Snow sits on the upper side of each bough and round the foot.
        for(int y=3;y<96;y++)for(int x=10;x<62;x++)
        {
            byte c=p.At(x,y);if(c is not (4 or >=7 and <=10))continue;
            bool lit=c is 9 or 10 or 4;uint n=A.Hash(x/2,y/3,60+v);
            if(lit&&n%3!=0)p.Set(x,y,n%5==0?(byte)96:c==4||c==10?(byte)98:(byte)97);
        }
        p.Oval(36,100,14,2,97);p.Oval(34,99,9,1,98);
    }
    static void Dead(Pixels p,int v,byte[] bark)
    {
        p.Cast(39,99,12,2);p.Trunk(36,44,100,5,9,v==0?-2:2,bark);
        foreach(var (x,y,ex,ey,w) in v==0?new[]{(34,60,20,44,2),(20,44,14,30,1),(22,46,12,48,1),(37,52,52,38,2),(52,38,58,24,1),(50,40,60,42,1),(34,46,30,26,2),(30,26,24,16,1),(31,30,38,18,1)}
                                      :new[]{(38,62,52,50,2),(52,50,60,36,1),(54,48,62,52,1),(36,54,22,40,2),(22,40,16,26,1),(24,42,14,44,1),(38,46,42,24,2),(42,24,48,14,1),(41,30,34,20,1)})
        {p.Line(x,y,ex,ey,w,bark[2]);p.Line(x,y,ex,ey,1,bark[4]);}
        p.Oval(37,70,2,3,bark[0]);p.Rect(30,99,4,1,bark[2]);
    }
    static void Palm(Pixels p,int v)
    {
        p.Cast(40,99,16,3);
        int lean=v==0?6:-8;
        for(int y=40;y<=100;y++){float t=(y-40)/60f;int x=36+(int)MathF.Round(lean*(1-t)*(1-t));int w=y>94?6:4;p.Rect(x-w/2,y,w,1,(y/4)%2==0?A.Leather[1]:A.Leather[2]);p.Set(x-w/2,y,A.Leather[3]);p.Set(x+w/2-1,y,A.Leather[0]);}
        int cx=36+lean,cy=40;
        foreach(var (dx,dy) in new[]{(-24,6),(-20,-10),(-6,-18),(10,-18),(22,-8),(25,8),(-12,14),(14,14)})
        {
            // Each frond arcs out and droops; leaflets hang from its spine.
            for(int i=0;i<=12;i++){float t=i/12f;int x=cx+(int)(dx*t),y=cy+(int)(dy*t+10*t*t);p.Rect(x,y,2,2,dx<0?(byte)5:(byte)3);p.Rect(x,y+2,1,2+(int)(3*MathF.Sin(t*3.1f)),dx<0?(byte)4:(byte)2);p.Set(x,y,dx<0?(byte)6:(byte)4);}
        }
        p.Oval(cx,cy+2,3,3,A.Leather[1]);p.Oval(cx-3,cy+4,2,2,A.Orange[1]);p.Oval(cx+3,cy+5,2,2,A.Orange[2]);
    }
    static void Willow(Pixels p,int v)
    {
        p.Cast(40,99,22,3);p.Trunk(36,52,100,7,11,v==0?-2:2,Grey);
        p.Blob(36,36,24,16,A.Swamp8,61+v,.02f);p.Blob(22,42,12,10,A.Swamp8,63+v,0);p.Blob(50,42,12,10,A.Swamp8,65+v,-.06f);
        // Hanging curtains of leaves.
        for(int x=12;x<60;x+=2)
        {
            int top=44+(int)(A.Hash(x,v,5)%6),len=18+(int)(A.Hash(x,v,6)%22);
            if(x>31&&x<41)len=Math.Min(len,10);
            for(int y=top;y<top+len;y++)p.Set(x,y,(y+x)%5==0?(byte)116:x<30?(byte)115:(byte)114);
            p.Set(x,top+len,116);
        }
    }
    static void Bamboo(Pixels p,int v)
    {
        p.Cast(38,99,12,2);
        foreach(var (x,top,lean) in v==0?new[]{(28,22,-2),(35,10,0),(42,26,3),(47,44,4)}:new[]{(30,30,-3),(38,14,1),(45,34,2)})
        {
            for(int y=top;y<=100;y++){int xx=x+(int)MathF.Round(lean*(100-y)/80f);p.Rect(xx,y,3,1,146);p.Set(xx,y,147);p.Set(xx+2,y,145);if((y-top)%11==10)p.Rect(xx,y,3,1,145);}
            for(int y=top+4;y<top+40;y+=9){int xx=x+(int)MathF.Round(lean*(100-y)/80f);int dir=(y/9)%2==0?1:-1;p.Line(xx+1,y,xx+1+dir*9,y-5,1,146);p.Poly(147,xx+1+dir*6,y-4,xx+1+dir*13,y-9,xx+1+dir*10,y-2);p.Poly(146,xx+1+dir*4,y-1,xx+1+dir*11,y+2,xx+1+dir*6,y+2);}
        }
    }
    static void Cypress(Pixels p)
    {
        p.Cast(38,99,10,2);p.Trunk(36,88,100,4,6,0,Bark);
        for(int y=8;y<92;y++){float t=(y-8)/84f;float half=2+9*MathF.Sin(MathF.Min(1,t*1.15f)*MathF.PI*.62f);for(int x=(int)(36-half);x<=(int)(36+half);x++){float a=(x-36+half)/(2*half);uint n=A.Hash(x/2,y/3,70);p.Set(x,y,a<.2f?(byte)10:a<.55f?(n%3==0?(byte)10:(byte)9):a<.82f?(byte)8:(byte)7);}}
    }
    static void FruitTree(Pixels p)
    {
        Broadleaf(p,A.Leaf8,Bark,2);
        foreach(var (x,y) in new[]{(27,44),(33,38),(41,42),(46,48),(30,54),(38,52),(44,36)}){p.Oval(x,y,2,2,138);p.Set(x-1,y-1,140);}
    }
    static void Cactus(Pixels p,int v)
    {
        void Arm(int x,int top,int bottom,int w)
        {
            for(int y=top;y<=bottom;y++)for(int i=0;i<w;i++){float a=i/(float)(w-1);p.Set(x+i,y,i%3==1?(byte)105:a<.3f?(byte)107:a<.7f?(byte)106:(byte)105);}
            p.Oval(x+w/2,top,w/2,2,107);for(int y=top+3;y<bottom;y+=5){p.Set(x-1,y,161);p.Set(x+w,y+2,161);}
        }
        p.Cast(39,99,v==2?12:14,2);
        if(v==0){Arm(31,40,100,10);Arm(19,58,76,7);p.Rect(22,74,10,6,106);p.Rect(22,74,10,1,107);Arm(46,50,70,7);p.Rect(41,66,8,6,106);p.Rect(34,36,4,3,122);p.Set(35,36,123);}
        if(v==1){Arm(24,54,100,9);Arm(40,44,100,9);Arm(52,60,78,6);p.Rect(48,76,6,5,106);p.Rect(27,51,3,3,151);p.Rect(43,41,3,3,122);}
        if(v==2){p.Blob(36,90,12,10,A.Cactus8,71,0,.03f,true);for(int x=26;x<47;x+=4)p.Line(x,82,x+(x<36?-1:1),98,1,105);p.Rect(33,78,6,3,122);p.Rect(34,77,4,1,123);p.Set(36,79,151);}
        if(v==3){foreach(var (x,y,rx,ry) in new[]{(34,92,9,8),(46,82,8,7),(26,78,8,7),(38,68,7,6),(52,70,5,5)}){p.Blob(x,y,rx,ry,A.Cactus8,72+x,0,.03f,true);p.Set(x-2,y-2,161);p.Set(x+3,y+1,161);p.Set(x,y+3,161);}
            p.Oval(38,62,2,2,138);p.Oval(52,65,2,2,138);p.Oval(24,71,2,2,122);}
    }
    static void Rock(Pixels p,byte[] r,int v)
    {
        p.Cast(39,99,v==2?20:16,3);
        if(v==0){p.Blob(36,90,14,10,r,81,0,.05f,true);p.Blob(26,96,6,4,r,82,-.05f,.05f,true);}
        if(v==1){p.Blob(30,91,11,9,r,83,0,.05f,true);p.Blob(45,93,10,7,r,84,-.06f,.05f,true);p.Blob(38,84,8,6,r,85,.04f,.05f,true);}
        if(v==2){p.Blob(36,80,18,20,r,86,0,.04f,true);for(int y=70;y<96;y+=8)p.Over(18,y,36,1,r[1],r[2],r[3],r[4],r[5],r[6]);p.Blob(52,95,7,5,r,87,-.06f,.05f,true);}
        if(v==3){p.Blob(27,93,10,7,r,88,0,.05f,true);p.Blob(45,92,11,8,r,89,-.05f,.05f,true);p.Blob(36,82,10,8,r,90,.03f,.05f,true);p.Blob(36,70,7,6,r,91,.06f,.05f,true);}
        p.Tuft(22,100,2);p.Tuft(50,100,2);
    }
    static void DryBush(Pixels p)
    {
        p.Cast(38,100,11,2);
        for(int i=0;i<11;i++){double a=Math.PI*(1.05+i*.09);int ex=36+(int)(Math.Cos(a)*(13+i%3*2)),ey=98+(int)(Math.Sin(a)*(14+i%4*2));p.Line(36,98,ex,ey,1,i%2==0?A.Leather[2]:A.Leather[1]);p.Set(ex,ey,A.Leather[3]);if(i%3==0)p.Set(ex+1,ey+1,116);}
    }
    static void Tumbleweed(Pixels p)
    {
        p.Cast(38,100,10,2);
        for(int ring=0;ring<4;ring++)for(int i=0;i<16;i++){double a=i*.39+ring;int x=36+(int)(Math.Cos(a)*(9-ring*2)),y=90+(int)(Math.Sin(a)*(9-ring*2));p.Set(x,y,(i+ring)%3==0?A.Leather[3]:A.Leather[2]);}
        p.Line(28,86,44,94,1,A.Leather[1]);p.Line(30,95,43,85,1,A.Leather[1]);
    }
    static void BeastSkull(Pixels p)
    {
        p.Cast(39,100,14,2);p.Blob(36,92,9,7,A.Bone8,92,0,.03f,true);p.Rect(31,96,10,4,58);p.Oval(32,91,2,2,A.Ink);p.Oval(40,91,2,2,A.Ink);p.Rect(35,95,2,3,57);
        p.Line(28,88,18,80,2,58);p.Line(18,80,16,72,2,59);p.Line(44,88,54,80,2,58);p.Line(54,80,56,72,2,57);for(int x=32;x<41;x+=2)p.Rect(x,99,1,2,57);
    }
    static void Ribs(Pixels p)
    {
        p.Cast(39,100,18,2);p.Line(18,96,54,92,2,58);
        for(int i=0;i<6;i++){int x=22+i*6;for(int k=0;k<=8;k++){double a=Math.PI*(1+k/8.0);p.Set(x+(int)(Math.Cos(a)*3)+3,96-i%2+(int)(Math.Sin(a)*(14-Math.Abs(i-2)*2)),k<4?(byte)59:(byte)58);}}
        p.Oval(16,96,3,3,58);p.Set(15,95,A.Ink);
    }
    static void DuneGrass(Pixels p)
    {
        foreach(var (x,top,tilt) in new[]{(28,88,-4),(31,82,-2),(34,78,-1),(37,80,1),(40,84,3),(43,90,5),(46,93,6)}){p.Line(x,99,x+tilt,top,1,115);p.Set(x+tilt,top,116);p.Set(x+tilt,top+1,104);}
        p.Oval(36,100,12,1,103);
    }
    static void IceCrystal(Pixels p,int v)
    {
        p.Cast(39,100,14,2);
        foreach(var (x,top,w) in v==0?new[]{(26,78,5),(36,58,7),(46,74,5),(31,88,4)}:new[]{(30,66,6),(42,52,7),(50,80,4),(22,86,4),(37,84,5)})
        {p.Poly(99,x-w,100,x-w,top+w*2,x,top,x+w,top+w*2,x+w,100);p.Poly(100,x-w,100,x-w,top+w*2,x,top,x,100);p.Line(x-w+1,top+w*2,x-w+1,98,1,101);p.Line(x,top+2,x,97,1,99);p.Set(x-1,top+3,98);p.Set(x-1,top+4,98);}
        p.Oval(36,100,16,2,97);
    }
    static void Snowman(Pixels p)
    {
        p.Cast(40,100,14,2);p.Blob(36,90,12,10,A.Snow8,95,0,.02f,true);p.Blob(36,74,9,8,A.Snow8,96,0,.02f,true);p.Blob(36,62,6,6,A.Snow8,97,0,.02f,true);
        p.Set(34,61,A.Ink);p.Set(38,61,A.Ink);p.Rect(36,63,4,1,154);p.Rect(36,72,1,1,A.Ink);p.Rect(36,76,1,1,A.Ink);p.Rect(36,80,1,1,A.Ink);
        p.Rect(31,67,11,2,138);p.Rect(40,69,2,5,138);p.Rect(31,54,10,3,A.Dark[1]);p.Rect(33,48,6,6,A.Dark[1]);p.Rect(33,53,6,1,138);
        p.Line(27,74,16,66,1,13);p.Line(45,74,56,68,1,13);p.Set(15,64,13);p.Set(57,66,13);
    }
    static void SnowDrift(Pixels p){p.Blob(36,97,20,5,A.Snow8,98,.1f,.04f,true);p.Blob(26,96,9,4,A.Snow8,99,.1f,.04f,true);p.Set(44,94,98);p.Set(30,93,98);}
    static void Bush(Pixels p,byte[] r,int v)
    {
        p.Cast(39,99,14,2);p.Blob(28,92,9,7,r,71+v,-.16f);p.Blob(44,92,9,7,r,75+v,-.22f);p.Blob(36,88-v*2,11,9,r,79+v,.02f);
    }
    static void Dots(Pixels p,byte[] r,int slot)
    {
        for(int i=0;i<9;i++){int x=24+(int)(A.Hash(i,slot,1)%25),y=82+(int)(A.Hash(i,slot,2)%13);if(p.At(x,y) is 0 or A.Ink)continue;p.Rect(x,y,2,2,r[2]);p.Set(x,y,r[3]);p.Set(x+1,y+1,r[1]);}
    }
    static void Flowers(Pixels p,byte[] r)
    {
        foreach(var (x,top) in new[]{(28,90),(32,84),(36,88),(40,82),(44,89),(34,93),(42,94)})
        {p.Line(x,99,x,top+2,1,3);p.Rect(x-1,top,3,3,r[2]);p.Set(x,top+1,151);p.Set(x-1,top,r[3]);p.Set(x-2,97,4);}
    }
    static void GiantCap(Pixels p,byte[] r,int v)
    {
        p.Cast(40,100,16,3);
        int top=v==0?44:v==1?34:54;
        for(int y=top+10;y<=100;y++){int w=5+(y>92?(y-92)/2:0);for(int i=-w;i<=w;i++)p.Set(36+i,y,i<-w/2?(byte)59:i<w/2?(byte)58:(byte)57);}
        p.Blob(36,top+8,v==2?16:20,v==1?12:10,new[]{r[0],r[0],r[1],r[1],r[2],r[2],r[3],r[3]},101+v,0,.04f,true);
        p.Rect(22,top+15,28,2,57);for(int x=24;x<49;x+=3)p.Rect(x,top+15,1,3,58);
        foreach(var (x,y) in new[]{(28,top+4),(38,top+1),(46,top+7),(33,top+9),(22,top+10)})p.Rect(x,y,3,2,161);
        if(v==2){p.Set(20,top-4,133);p.Set(52,top+2,133);p.Set(36,top-8,56);}
        // A child of the same cap at its foot.
        p.Rect(52,92,3,8,58);p.Blob(53,90,6,4,new[]{r[0],r[0],r[1],r[1],r[2],r[2],r[3],r[3]},105+v,0,.04f,true);
    }
    static void MushroomRing(Pixels p)
    {
        for(int i=0;i<9;i++){double a=i*.7;int x=36+(int)(Math.Cos(a)*16),y=95+(int)(Math.Sin(a)*4);p.Rect(x,y-2,1,3,58);p.Rect(x-2,y-4,5,2,i%2==0?(byte)138:(byte)118);p.Rect(x-1,y-5,3,1,i%2==0?(byte)139:(byte)119);p.Set(x-1,y-4,161);}
        p.Tuft(36,99,3);
    }
    static void Fern(Pixels p)
    {
        foreach(var (ex,ey) in new[]{(14,84),(20,74),(30,66),(42,66),(52,74),(58,84)})
        {for(int i=0;i<=10;i++){float t=i/10f;int x=36+(int)((ex-36)*t),y=99+(int)((ey-99)*t-8*MathF.Sin(t*3.14f));p.Set(x,y,3);if(i>1){p.Set(x-1,y+1+i%2,ex<36?(byte)5:(byte)2);p.Set(x+1,y-1-i%2,ex<36?(byte)4:(byte)3);p.Set(x,y-2,ex<36?(byte)6:(byte)4);}}}
    }
    static void Reeds(Pixels p,byte[] r,bool frozen)
    {
        foreach(var (x,top,head) in new[]{(27,80,true),(31,72,true),(35,84,false),(39,68,true),(43,78,true),(47,86,false)})
        {p.Line(x,100,x+(x%2==0?1:-1),top,1,frozen?(byte)96:(byte)3);p.Line(x+1,100,x+3,top+10,1,frozen?(byte)97:(byte)4);
         if(head){p.Rect(x+(x%2==0?0:-2),top-6,2,7,frozen?(byte)98:(byte)14);p.Rect(x+(x%2==0?0:-2),top-6,1,6,frozen?(byte)97:(byte)15);}}
    }
    static void VinePost(Pixels p)
    {
        p.Cast(38,100,7,2);p.Rect(34,50,4,50,14);p.Rect(34,50,1,50,16);p.Rect(37,51,1,49,13);p.Rect(26,54,20,3,14);p.Rect(26,54,20,1,16);
        for(int y=52;y<98;y++){int x=33+(int)MathF.Round(3*MathF.Sin(y*.4f));p.Set(x,y,3);if(y%4==0){p.Rect(x-2,y,2,2,5);p.Rect(x+2,y+1,2,2,4);}if(y%9==0)p.Set(x+1,y,34);}
        for(int x=26;x<46;x+=3){p.Rect(x,56,2,3+x%4,x%2==0?(byte)4:(byte)3);}
    }
    static void HollowLog(Pixels p)
    {
        p.Cast(39,100,20,3);p.Rect(18,86,36,13,14);p.Rect(18,86,36,2,16);p.Rect(18,97,36,2,13);for(int x=24;x<52;x+=7)p.Rect(x,89,3,1,13);
        p.Oval(54,92,4,7,15);p.Oval(54,92,3,5,12);p.Oval(54,93,2,3,A.Ink);p.Oval(18,92,3,7,13);Moss(p,18,84,36,6,110);p.Over(20,85,14,2,4,14,16);p.Over(34,86,8,1,5,16);
        p.Rect(28,82,2,4,58);p.Rect(26,80,6,2,138);p.Set(27,80,161);p.Tuft(20,100,2);p.Tuft(50,100,3);
    }
    static void Menhir(Pixels p,int v)
    {
        p.Cast(39,100,v==2?16:11,3);
        if(v==0){p.Poly(21,28,100,27,56,33,46,42,48,45,62,44,100);p.Poly(23,28,100,27,56,33,46,35,48,33,100);p.Poly(20,41,50,45,62,44,100,40,100);}
        if(v==1){p.Poly(21,29,100,31,60,36,52,43,58,44,100);p.Poly(23,29,100,31,60,36,52,35,100);p.Poly(20,40,58,44,100,39,100);p.Rect(33,68,6,2,54);p.Rect(35,64,2,10,54);p.Rect(33,78,6,1,54);p.Set(35,64,56);}
        if(v==2){foreach(int x in new[]{22,44}){p.Rect(x,62,8,38,21);p.Rect(x,62,2,38,23);p.Rect(x+6,62,2,38,20);}p.Rect(18,54,38,9,22);p.Rect(18,54,38,2,24);p.Rect(18,61,38,2,20);p.Rect(52,56,4,7,20);}
        for(int y=56;y<98;y+=9)p.Over(22,y,26,1,20,21,22,23);Moss(p,20,84,34,16,120+v);p.Tuft(24,100,2);p.Tuft(48,100,3);
    }
    static void StoneArch(Pixels p)
    {
        p.Cast(40,100,24,3);p.Rect(14,58,11,42,21);p.Rect(47,58,11,42,21);p.Rect(14,58,3,42,23);p.Rect(55,58,3,42,20);p.Rect(22,58,3,42,20);
        for(int i=0;i<9;i++){float a=MathF.PI*(i+.5f)/9;int x=(int)(36-MathF.Cos(a)*18),y=(int)(60-MathF.Sin(a)*18);p.Rect(x-4,y-4,8,8,i<4?(byte)22:(byte)21);p.Rect(x-4,y-4,8,1,i<5?(byte)24:(byte)22);p.Rect(x+3,y-3,1,7,20);}
        for(int y=66;y<98;y+=9){p.Rect(14,y,11,1,19);p.Rect(47,y,11,1,19);}Moss(p,12,40,48,60,125);
    }
    static void Cairn(Pixels p)
    {
        p.Cast(39,100,12,2);foreach(var (y,rx,ry,slot) in new[]{(95,11,5,1),(86,9,4,2),(79,7,4,3),(72,5,3,4),(67,3,2,5)})p.Blob(36+(slot%2==0?1:-1),y,rx,ry,A.Stone8,130+slot,0,.04f,true);
    }
    static void Stump(Pixels p)
    {
        p.Cast(39,99,14,2);p.Trunk(36,84,100,13,17,0,Bark);p.Oval(36,84,7,3,16);p.Oval(36,84,5,2,15);p.Oval(36,84,2,1,14);Moss(p,26,82,22,8,131);p.Over(28,83,8,3,4,14,15,16);
        p.Rect(45,93,2,5,58);p.Rect(43,91,6,2,138);p.Set(44,91,161);p.Rect(27,95,2,4,58);p.Rect(25,93,6,2,165);p.Tuft(25,100,2);p.Tuft(48,100,3);
    }
    static void SwampRoot(Pixels p)
    {
        p.Cast(40,100,22,3);p.Trunk(36,40,84,9,13,0,Grey);
        foreach(var (ex,ey) in new[]{(14,99),(22,100),(30,100),(44,100),(52,100),(60,98)}){p.Line(36,82,ex,ey,2,Grey[2]);p.Line(36,82,ex,ey-1,1,Grey[4]);}
        p.Blob(36,34,20,12,A.Swamp8,141,0);p.Blob(24,40,10,8,A.Swamp8,142,0);p.Blob(49,40,10,8,A.Swamp8,143,-.06f);
        for(int x=20;x<54;x+=3){int len=6+(int)(A.Hash(x,0,7)%10);for(int y=46;y<46+len;y++)p.Set(x,y,114);}
    }
    static void GiantLily(Pixels p)
    {
        p.Oval(36,95,20,6,145);p.Oval(36,94,19,5,146);p.Oval(33,93,13,3,147);p.Poly(0,36,95,58,90,58,100);for(int i=0;i<6;i++)p.Line(36,95,20+i*6,90+(i%2)*8,1,145);
        p.Blob(30,88,6,5,A.Blossom8,145,.1f,.05f);p.Set(30,87,151);p.Set(31,88,151);
    }
    static void Coral(Pixels p,byte[] r)
    {
        p.Cast(38,100,12,2);
        void Branch(int x,int y,int len,double a,int depth){int ex=x+(int)(Math.Cos(a)*len),ey=y+(int)(Math.Sin(a)*len);p.Line(x,y,ex,ey,depth>1?3:2,r[1]);p.Line(x,y,ex,ey,1,r[2]);p.Set(ex,ey,r[3]);if(depth>0){Branch(ex,ey,len*3/4,a-.5,depth-1);Branch(ex,ey,len*3/4,a+.45,depth-1);}}
        Branch(36,99,12,-Math.PI/2,3);
    }
    static void Driftwood(Pixels p)
    {
        p.Cast(39,100,20,2);p.Line(16,97,52,88,4,57);p.Line(16,96,52,87,2,58);p.Line(18,95,50,87,1,59);p.Line(38,91,46,80,2,57);p.Line(46,80,50,76,1,58);p.Line(28,93,24,84,2,57);p.Oval(16,97,2,3,57);
    }
    static void ShellPile(Pixels p)
    {
        p.Cast(38,100,11,2);foreach(var (x,y,r) in new[]{(30,96,A.Pink),(38,94,A.Pale),(44,97,A.Orange),(35,99,A.Blue)}){p.Poly(r[1],x,y+3,x-4,y-1,x,y-4,x+4,y-1);p.Poly(r[2],x,y+3,x-4,y-1,x,y-4);p.Line(x,y+2,x-2,y-2,1,r[0]);p.Set(x-2,y-2,161);}
    }
    static void Seaweed(Pixels p)
    {
        foreach(var (x,top) in new[]{(28,78),(33,66),(38,72),(43,82)})for(int y=top;y<=100;y++){int xx=x+(int)MathF.Round(3*MathF.Sin(y*.35f+x));p.Rect(xx,y,2,1,y%6<3?(byte)146:(byte)145);p.Set(xx,y,147);}
    }
    static void Wheat(Pixels p,int stage)
    {
        int tall=stage==0?8:stage==1?18:26;
        for(int x=24;x<48;x+=2){int h=tall-(int)(A.Hash(x,stage,8)%4),lean=(x/2)%3-1;p.Line(x,100,x+lean,100-h,1,stage==2?(byte)82:(byte)4);
            if(stage>0){p.Rect(x+lean,100-h-4,1,5,stage==2?(byte)83:(byte)5);p.Set(x+lean-1,100-h-2,stage==2?(byte)82:(byte)4);p.Set(x+lean+1,100-h-1,stage==2?(byte)81:(byte)3);}}
        p.Rect(24,100,24,1,14);
    }
    static void Corn(Pixels p,int stage)
    {
        int tall=stage==0?12:stage==1?28:42;
        foreach(int x in new[]{28,36,44})
        {
            p.Rect(x,100-tall,2,tall,146);p.Set(x,100-tall,147);
            for(int y=100-tall+5;y<98;y+=7){int dir=(y/7+x)%2==0?1:-1;p.Line(x,y,x+dir*8,y-4,1,146);p.Line(x+dir*8,y-4,x+dir*11,y,1,147);}
            if(stage==2){p.Rect(x-2,100-tall+14,3,8,151);p.Rect(x-2,100-tall+14,1,8,152);p.Rect(x+1,100-tall+15,1,6,149);p.Rect(x,100-tall-4,2,5,82);}
        }
        p.Rect(24,100,24,1,14);
    }
    static void Pumpkin(Pixels p)
    {
        p.Cast(39,100,15,2);p.Blob(36,91,13,9,new byte[]{153,153,154,154,154,155,155,152},151,0,.02f,true);foreach(int x in new[]{28,33,39,44})p.Line(x,84,x+(x<36?-1:1),98,1,153);
        p.Rect(35,79,3,4,146);p.Set(35,79,147);p.Line(38,81,48,84,1,146);p.Poly(147,46,82,54,80,52,87);p.Blob(53,96,5,4,new byte[]{153,153,154,154,154,155,155,152},152,0,.02f,true);
    }
    static void Cabbage(Pixels p)
    {
        p.Cast(38,100,12,2);foreach(int x in new[]{28,44}){p.Blob(x,94,8,6,A.Green8,153+x,0,.08f);p.Oval(x,93,4,3,148);p.Oval(x-1,92,2,1,161);}
        p.Blob(36,96,7,4,A.Green8,155,0,.08f);p.Oval(36,95,3,2,148);
    }
    static void Sunflower(Pixels p)
    {
        p.Cast(38,100,8,2);
        foreach(var (x,top) in new[]{(30,62),(41,50)})
        {p.Line(x,100,x,top+6,2,146);p.Poly(147,x,84,x-9,80,x-5,88);p.Poly(146,x+1,76,x+10,72,x+6,80);
         for(int i=0;i<12;i++){double a=i*.5236;p.Rect(x+(int)(Math.Cos(a)*8),top+(int)(Math.Sin(a)*8),2,2,i%2==0?(byte)151:(byte)150);}p.Oval(x,top,5,5,A.Leather[1]);p.Oval(x-1,top-1,3,3,A.Leather[2]);p.Set(x-2,top-2,A.Leather[3]);}
    }
    static void Tomato(Pixels p)
    {
        p.Rect(35,60,2,40,14);p.Rect(35,60,1,40,16);for(int y=62;y<98;y++){int x=35+(int)MathF.Round(4*MathF.Sin(y*.5f));p.Set(x,y,146);if(y%5==0){p.Rect(x-4,y,4,2,147);p.Rect(x+2,y+1,3,2,146);}}
        foreach(var (x,y) in new[]{(29,74),(42,70),(31,86),(41,84),(36,66)}){p.Oval(x,y,2,2,138);p.Set(x-1,y-1,140);}
    }
    static void Grapes(Pixels p)
    {
        p.Rect(22,62,2,38,14);p.Rect(48,62,2,38,13);p.Rect(22,64,28,2,14);p.Rect(22,64,28,1,16);p.Rect(22,80,28,1,14);
        for(int x=24;x<48;x+=2){p.Rect(x,60+(int)(A.Hash(x,0,9)%4),3,6,x%4==0?(byte)147:(byte)146);}
        foreach(var (x,y) in new[]{(28,70),(36,72),(44,69),(32,84),(41,85)})for(int i=0;i<6;i++){int gx=x+(i%3)-1+(i/3)%2,gy=y+i/2*2;p.Rect(gx,gy,2,2,118);p.Set(gx,gy,119);}
    }
    static void Beehive(Pixels p)
    {
        p.Cast(39,100,11,2);p.Rect(28,94,16,6,14);p.Rect(28,94,16,1,16);p.Rect(30,96,2,4,13);p.Rect(40,96,2,4,13);
        for(int band=0;band<5;band++){int w=13-band*2,y=90-band*5;p.Oval(36,y,w,3,band%2==0?(byte)150:(byte)151);p.Rect(36-w,y+2,w*2,1,149);p.Set(36-w+2,y-1,152);}
        p.Oval(36,90,2,2,A.Ink);p.Set(48,78,151);p.Set(49,78,A.Ink);p.Set(22,84,151);p.Set(23,84,A.Ink);p.Set(44,66,151);
    }
    static void Haystack(Pixels p)
    {
        p.Cast(40,100,22,3);p.Blob(36,82,21,19,A.Straw8,161,0,.07f);p.Rect(35,56,2,8,14);for(int i=0;i<16;i++)p.Set(18+(int)(A.Hash(i,0,10)%36),70+(int)(A.Hash(i,1,10)%28),83);
        p.Line(52,100,58,76,1,14);p.Rect(56,72,5,1,42);p.Rect(56,72,1,5,42);p.Rect(58,72,1,5,42);p.Rect(60,72,1,5,42);
    }
    static void Turnips(Pixels p)
    {
        foreach(int x in new[]{26,34,42}){p.Oval(x+1,97,3,3,x==34?(byte)122:(byte)161);p.Set(x,96,98);p.Line(x+1,94,x-2,86,1,146);p.Line(x+1,94,x+1,84,1,147);p.Line(x+1,94,x+4,87,1,146);}
        p.Rect(22,100,26,1,14);
    }
}
