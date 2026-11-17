using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Animated effects: eight 48x48 frames each, no contour, drawn additive or plain.
/// Module names end in the frame number, 1 to 8.</summary>
public static class EffectArt
{
    public const int Frames=8;
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("effects",48,48,8);
        void Add(string name,Action<Pixels,int,float> paint){for(int f=0;f<Frames;f++){int k=f;s.Add($"{name}-{f+1}",p=>paint(p,k,k/(float)(Frames-1)),true,new Vector2(24,24));}}
        Add("explosion",Explosion);Add("fire-burst",(p,f,t)=>Burst(p,f,t,A.Flame));Add("frost-burst",(p,f,t)=>Burst(p,f,t,new byte[]{99,100,101,98}));Add("arcane-burst",(p,f,t)=>Burst(p,f,t,A.Violet));
        Add("smoke",Smoke);Add("dust",Dust);Add("sparkle-gold",(p,f,t)=>Sparkle(p,f,A.Yellow));Add("sparkle-blue",(p,f,t)=>Sparkle(p,f,A.Blue));
        Add("slash",Slash);Add("hit-spark",HitSpark);Add("heal",Heal);Add("level-up",LevelUp);
        Add("lightning",Lightning);Add("splash",Splash);Add("ripple",Ripple);Add("poison",Poison);
        Add("portal-swirl",Swirl);Add("shield",Shield);Add("ice-shards",Shards);Add("leaves",Leaves);
        Add("missile",Missile);Add("teleport",Teleport);Add("coin-spin",Coin);Add("torch-flame",(p,f,t)=>Brush.Flame(p,24,40,14,26,f));
        Add("curse",Curse);Add("blood",(p,f,t)=>Droplets(p,f,t,A.Red));Add("embers",Embers);Add("snowfall",Snow);
        return s;
    }
    static void Disc(Pixels p,float cx,float cy,float r,byte c){for(int y=(int)(cy-r);y<=cy+r;y++)for(int x=(int)(cx-r);x<=cx+r;x++)if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r)p.Set(x,y,c);}
    static void Ring(Pixels p,float cx,float cy,float r,float thick,byte c,float squash=1){for(int y=1;y<47;y++)for(int x=1;x<47;x++){float d=MathF.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy)/(squash*squash));if(d<=r&&d>r-thick)p.Set(x,y,c);}}
    static float H(int a,int b,int slot)=>(A.Hash(a,b,slot)&1023)/1023f;
    static void Explosion(Pixels p,int f,float t)
    {
        float r=5+17*MathF.Sqrt(t);
        if(f<5){Disc(p,24,24,r,f<3?(byte)44:(byte)158);Disc(p,24,24,r*.82f,f<2?(byte)45:f<4?(byte)44:(byte)159);Disc(p,24,24,r*.58f,f<2?(byte)46:(byte)45);if(f<3)Disc(p,24,24,r*.3f,47);}
        else{for(int i=0;i<12;i++){float a=i*.5236f;Disc(p,24+MathF.Cos(a)*r*.8f,24+MathF.Sin(a)*r*.8f-(f-4)*1.5f,4.5f-(f-4),i%2==0?(byte)158:(byte)159);}}
        for(int i=0;i<10;i++){float a=i*.63f+1;float d=r+2+H(i,0,61)*6*t;p.Set((int)(24+MathF.Cos(a)*d),(int)(24+MathF.Sin(a)*d),f<5?(byte)46:(byte)158);}
    }
    static void Burst(Pixels p,int f,float t,byte[] c)
    {
        float r=4+18*t;Ring(p,24,24,r,MathF.Max(1.5f,5*(1-t)),c[1]);Ring(p,24,24,r-1,MathF.Max(1,3*(1-t)),c[2]);
        for(int i=0;i<8;i++){float a=i*.785f+.2f;float d=r*.9f;Disc(p,24+MathF.Cos(a)*d,24+MathF.Sin(a)*d,MathF.Max(.6f,3*(1-t)),c[3]);}
        if(f<3)Disc(p,24,24,6-f*2,c[3]);
    }
    static void Smoke(Pixels p,int f,float t)
    {
        for(int i=0;i<6;i++){float x=24+(H(i,0,62)-.5f)*18*(.4f+t),y=38-t*26-H(i,1,62)*8,r=3+H(i,2,62)*4*(1-t*.5f);if(r<1)continue;Disc(p,x,y,r,f<5?(byte)159:(byte)158);Disc(p,x-1,y-1,r*.6f,f<4?(byte)160:(byte)159);}
    }
    static void Dust(Pixels p,int f,float t)
    {
        for(int i=0;i<7;i++){float x=24+(H(i,0,63)-.5f)*36*(.3f+t),y=38-H(i,1,63)*6-t*4,r=(2+H(i,2,63)*3)*(1-t*.6f);Disc(p,x,y,r,i%2==0?(byte)103:(byte)102);Disc(p,x-1,y-1,r*.5f,104);}
    }
    static void Star(Pixels p,int x,int y,int size,byte[] c){p.Set(x,y,161);for(int i=1;i<=size;i++){byte k=i==size?c[1]:c[3];p.Set(x+i,y,k);p.Set(x-i,y,k);p.Set(x,y+i,k);p.Set(x,y-i,k);}if(size>1){p.Set(x+1,y+1,c[2]);p.Set(x-1,y-1,c[2]);p.Set(x+1,y-1,c[2]);p.Set(x-1,y+1,c[2]);}}
    static void Sparkle(Pixels p,int f,byte[] c){for(int i=0;i<7;i++){int phase=(f+i*3)%8;int size=phase<4?phase:7-phase;if(size<=0)continue;Star(p,8+(int)(H(i,0,64)*32),8+(int)(H(i,1,64)*32),size,c);}}
    static void Slash(Pixels p,int f,float t)
    {
        float sweep=-2.4f+t*3.2f;
        for(int i=0;i<=40;i++){float a=sweep-i*.045f;float fade=i/40f;if(fade>1-t*.3f&&f>4)continue;for(int w=0;w<(int)(5*(1-fade))+1;w++){float r=18-w;p.Set((int)(24+MathF.Cos(a)*r),(int)(26+MathF.Sin(a)*r),w==0?(byte)98:fade<.3f?(byte)161:fade<.6f?(byte)144:(byte)143);}}
    }
    static void HitSpark(Pixels p,int f,float t)
    {
        if(f>5)return;float r=3+f*3.4f;for(int i=0;i<8;i++){float a=i*.785f;for(float d=r*.4f;d<r;d+=.8f)p.Set((int)(24+MathF.Cos(a)*d),(int)(24+MathF.Sin(a)*d),d>r*.75f?(byte)151:(byte)98);}
        if(f<3)Disc(p,24,24,4-f,98);
    }
    static void Heal(Pixels p,int f,float t)
    {
        Ring(p,24,38,6+10*t,1.5f,f<6?(byte)147:(byte)146,.35f);
        for(int i=0;i<5;i++){int x=10+(int)(H(i,0,65)*28),y=40-(int)((t+H(i,1,65))%1f*34);byte c=i%2==0?(byte)148:(byte)147;p.Rect(x-2,y,5,1,c);p.Rect(x,y-2,1,5,c);p.Set(x,y,161);}
    }
    static void LevelUp(Pixels p,int f,float t)
    {
        int top=40-(int)(t*36);for(int x=14;x<34;x+=3){int h=40-top-(int)(H(x,0,66)*8);if(h>0)p.Rect(x,40-h,2,h,x%2==0?(byte)151:(byte)152);}
        Ring(p,24,40,8+8*t,2,151,.35f);Ring(p,24,40,4+8*t,1,152,.35f);for(int i=0;i<5;i++)Star(p,10+(int)(H(i,1,66)*28),top+(int)(H(i,2,66)*10),1+(f+i)%2,A.Yellow);
    }
    static void Lightning(Pixels p,int f,float t)
    {
        if(f%4==3)return;int x=24+(int)((H(f,0,67)-.5f)*8);
        for(int y=2;y<44;y+=6){int nx=24+(int)((H(f,y,67)-.5f)*22);p.Line(x,y,nx,y+6,f%2==0?3:1,143);p.Line(x,y,nx,y+6,1,98);if(H(y,f,68)>.7f)p.Line(nx,y+6,nx+(int)((H(y,f,69)-.5f)*20),y+12,1,144);x=nx;}
        Disc(p,x,44,3,144);Disc(p,x,44,1.5f,98);
    }
    static void Splash(Pixels p,int f,float t)
    {
        Ring(p,24,38,4+12*t,1.5f,143,.35f);for(int i=0;i<9;i++){float a=-MathF.PI*(.15f+.7f*i/8f);float d=20*t,g=30*t*t;int x=(int)(24+MathF.Cos(a)*d),y=(int)(38+MathF.Sin(a)*d*1.4f+g);if(y<40){p.Rect(x,y,2,2,i%2==0?(byte)144:(byte)143);p.Set(x,y,98);}}
        if(f<3)Disc(p,24,36,5-f,144);
    }
    static void Ripple(Pixels p,int f,float t){for(int k=0;k<3;k++){float r=(t+k/3f)%1f*20;Ring(p,24,24,r,1.2f,r<8?(byte)144:r<15?(byte)143:(byte)142,.45f);}}
    static void Poison(Pixels p,int f,float t)
    {
        for(int i=0;i<7;i++){float ph=(t+H(i,0,70))%1f;float x=10+H(i,1,70)*28+MathF.Sin(ph*6+i)*2,y=42-ph*36,r=1+H(i,2,70)*3;if(ph>.85f){p.Set((int)x-2,(int)y,147);p.Set((int)x+2,(int)y,147);p.Set((int)x,(int)y-2,147);continue;}Ring(p,x,y,r,1,146);p.Set((int)x-1,(int)y-1,148);}
    }
    static void Swirl(Pixels p,int f,float t)
    {
        for(int arm=0;arm<3;arm++)for(int i=0;i<40;i++){float a=arm*2.094f+i*.14f+t*MathF.PI*2,r=2+i*.5f;int x=(int)(24+MathF.Cos(a)*r),y=(int)(24+MathF.Sin(a)*r*.9f);p.Rect(x,y,2,2,i<12?(byte)120:i<26?(byte)119:(byte)118);}
        Disc(p,24,24,3,120);Disc(p,24,24,1.5f,161);
    }
    static void Shield(Pixels p,int f,float t)
    {
        float pulse=1+.06f*MathF.Sin(t*MathF.PI*2);Ring(p,24,24,19*pulse,1.6f,143);Ring(p,24,24,17.5f*pulse,1,144);
        for(int y=6;y<42;y+=6)for(int x=6+((y/6)%2)*3;x<42;x+=6){float d=MathF.Sqrt((x-24)*(x-24)+(y-24)*(y-24));if(d<17&&(x+y+f*2)%5<2)p.Set(x,y,144);}
        for(int i=0;i<10;i++){float a=-2.4f+i*.1f;p.Set((int)(24+MathF.Cos(a)*15),(int)(24+MathF.Sin(a)*15),98);}
    }
    static void Shards(Pixels p,int f,float t)
    {
        for(int i=0;i<9;i++){float a=i*.698f,d=4+18*t;int x=(int)(24+MathF.Cos(a)*d),y=(int)(24+MathF.Sin(a)*d),ex=(int)(x+MathF.Cos(a)*5*(1-t*.5f)),ey=(int)(y+MathF.Sin(a)*5*(1-t*.5f));p.Line(x,y,ex,ey,2,100);p.Line(x,y,ex,ey,1,101);p.Set(ex,ey,98);}
        if(f<2)Disc(p,24,24,5,101);
    }
    static void Leaves(Pixels p,int f,float t)
    {
        for(int i=0;i<8;i++){float a=i*.785f+t*MathF.PI*2,r=8+H(i,0,71)*10;int x=(int)(24+MathF.Cos(a)*r),y=(int)(24+MathF.Sin(a)*r*.6f-MathF.Sin(t*6+i)*3);byte c=i%3==0?(byte)110:i%3==1?(byte)4:(byte)111;p.Rect(x,y,3,2,c);p.Set(x+((f+i)%2==0?3:-1),y+1,c);}
    }
    static void Missile(Pixels p,int f,float t)
    {
        int x=6+(int)(t*34);for(int i=0;i<14;i++){int tx=x-i*2;if(tx<2)break;p.Rect(tx,24+(int)(MathF.Sin((tx+f)*.6f)*2),2,i<4?3:i<9?2:1,i<4?(byte)120:i<9?(byte)119:(byte)118);}
        Disc(p,x,24,4,119);Disc(p,x,24,2.5f,120);Disc(p,x+1,24,1,161);
    }
    static void Teleport(Pixels p,int f,float t)
    {
        float w=f<4?2+f*3:14-(f-4)*3.4f;if(w>0){p.Rect((int)(24-w/2),2,(int)w,42,55);p.Rect((int)(24-w/4),2,Math.Max(1,(int)(w/2)),42,56);}
        Ring(p,24,42,6+t*10,1.5f,54,.35f);for(int i=0;i<6;i++)p.Set(14+(int)(H(i,f,72)*20),6+(int)(H(i,f+9,72)*34),56);
    }
    static void Coin(Pixels p,int f,float t)
    {
        float w=MathF.Abs(MathF.Cos(t*MathF.PI))*9+1;for(int y=14;y<=34;y++){float half=w*MathF.Sqrt(MathF.Max(0,1-(y-24)*(y-24)/100f));for(int x=(int)(24-half);x<=(int)(24+half);x++)p.Set(x,y,x<24-half*.4f?(byte)30:x>24+half*.5f?(byte)28:(byte)29);}
        if(w>6){p.Rect(23,19,2,10,28);p.Rect(21,21,6,1,28);}p.Set(24-(int)(w*.6f),17,161);
    }
    static void Curse(Pixels p,int f,float t)
    {
        for(int i=0;i<3;i++){float a=i*2.094f+t*MathF.PI*2;int x=(int)(24+MathF.Cos(a)*13),y=(int)(24+MathF.Sin(a)*7);Disc(p,x,y,4,117);Disc(p,x,y-1,3,118);p.Set(x-1,y-1,A.Ink);p.Set(x+1,y-1,A.Ink);p.Rect(x-1,y+1,3,1,A.Ink);}
        for(int i=0;i<10;i++)p.Set(8+(int)(H(i,f,73)*32),8+(int)(H(i,f+5,73)*32),119);
    }
    static void Droplets(Pixels p,int f,float t,byte[] c)
    {
        for(int i=0;i<10;i++){float a=-MathF.PI*(.1f+.8f*H(i,0,74)),d=6+20*t*(.5f+H(i,1,74)),g=24*t*t;int x=(int)(24+MathF.Cos(a)*d),y=(int)(30+MathF.Sin(a)*d+g);if(y>45)continue;p.Rect(x,y,2,2,i%2==0?c[1]:c[2]);p.Set(x,y,c[3]);}
    }
    static void Embers(Pixels p,int f,float t)
    {
        for(int i=0;i<14;i++){float ph=(t+H(i,0,75))%1f;int x=(int)(8+H(i,1,75)*32+MathF.Sin(ph*7+i)*3),y=(int)(44-ph*40);p.Set(x,y,ph<.3f?(byte)47:ph<.6f?(byte)46:ph<.85f?(byte)45:(byte)44);if(ph<.4f)p.Set(x,y+1,45);}
    }
    static void Snow(Pixels p,int f,float t)
    {
        for(int i=0;i<16;i++){float ph=(t+H(i,0,76))%1f;int x=(int)(4+H(i,1,76)*40+MathF.Sin(ph*5+i)*3),y=(int)(2+ph*44);p.Set(x,y,98);if(i%3==0){p.Set(x+1,y,97);p.Set(x,y+1,97);}}
    }
}
