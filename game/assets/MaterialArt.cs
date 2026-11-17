using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
using A=Mosslight.AssetPalette;
namespace Mosslight;

/// <summary>Seamless ground materials, 48x24: two tiles wide in the game's ground
/// projection. Every recipe wraps at both edges, so a material can be tiled or sampled by
/// a shader at absolute pixels.</summary>
public static class MaterialArt
{
    const int W=48,H=24;
    public static AssetSheet Sheet()
    {
        var s=new AssetSheet("materials",W,H,8);
        void Add(string name,Func<int,int,byte> colour)=>s.Modules.Add(new AssetModule(name,W,H,new Vector2(0,0),()=>{var data=new byte[W*H];for(int y=0;y<H;y++)for(int x=0;x<W;x++)data[y*W+x]=colour(x,y);return data;},true));
        Add("snow",(x,y)=>Blend(x,y,1,new byte[]{96,97,97,98},.5f,4));Add("ice",Ice);Add("sand",(x,y)=>Blend(x,y,2,new byte[]{102,103,103,104},.6f,3));Add("dunes",Dunes);
        Add("mud",(x,y)=>Blend(x,y,3,new byte[]{12,134,134,135},.8f,5));Add("swamp-water",(x,y)=>Water(x,y,new byte[]{113,114,115,116}));Add("shallow-water",(x,y)=>Water(x,y,new byte[]{142,143,143,144}));Add("deep-water",(x,y)=>Water(x,y,new byte[]{141,141,142,143}));
        Add("lava",Lava);Add("basalt",Basalt);Add("ash",(x,y)=>Blend(x,y,4,new byte[]{157,158,158,159},.7f,6));Add("obsidian",Obsidian);
        Add("cobble",(x,y)=>Setts(x,y,new byte[]{20,21,22,23},8,5));Add("stone-flags",(x,y)=>Setts(x,y,new byte[]{20,21,22,23},16,8));Add("sandstone-tiles",(x,y)=>Setts(x,y,new byte[]{49,51,52,53},12,6));Add("brick",(x,y)=>Setts(x,y,new byte[]{72,73,74,75},12,4));
        Add("planks",(x,y)=>Boards(x,y,false));Add("parquet",(x,y)=>Boards(x,y,true));Add("marble",Marble);Add("marble-checker",Checker);
        Add("carpet-red",(x,y)=>Carpet(x,y,A.Red));Add("carpet-blue",(x,y)=>Carpet(x,y,A.Blue));Add("straw",Straw);Add("tilled-soil",Tilled);
        Add("grass-dark",(x,y)=>Turf(x,y,new byte[]{1,2,3,4}));Add("grass-dry",(x,y)=>Turf(x,y,new byte[]{114,115,116,104}));Add("autumn-leaves",LeafLitter);Add("gravel",Gravel);
        Add("moss-stone",MossStone);Add("crystal-floor",CrystalFloor);Add("metal-grate",Grate);Add("bone-floor",BoneFloor);
        return s;
    }
    // Hash and value noise that wrap at the tile edges.
    static uint U(int x,int y,int slot)=>A.Hash(((x%W)+W)%W,((y%H)+H)%H,900+slot);
    static float R(int x,int y,int slot)=>(U(x,y,slot)&1023)/1023f;
    static float Noise(int x,int y,int cells,int slot)
    {
        float gx=x*cells/(float)W,gy=y*cells/(float)W;int ix=(int)MathF.Floor(gx),iy=(int)MathF.Floor(gy);float fx=gx-ix,fy=gy-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        int rows=Math.Max(1,cells*H/W);
        float V(int a,int b)=>(A.Hash(((a%cells)+cells)%cells,((b%rows)+rows)%rows,950+slot)&1023)/1023f;
        return Mathf.Lerp(Mathf.Lerp(V(ix,iy),V(ix+1,iy),fx),Mathf.Lerp(V(ix,iy+1),V(ix+1,iy+1),fx),fy);
    }
    static byte Blend(int x,int y,int slot,byte[] r,float grain,int cells)
    {
        float n=Noise(x,y,cells,slot)*.7f+Noise(x,y,cells*2,slot+1)*.3f;int t=n<.38f?1:n>.62f?2:(x+y)%2==0&&MathF.Abs(n-.5f)>.1f?(n<.5f?1:2):n<.5f?1:2;
        float g=R(x,y,slot+2);if(g>1-grain*.08f)t=3;else if(g<grain*.07f)t=0;return r[t];
    }
    static byte Ice(int x,int y)
    {
        float n=Noise(x,y,4,10);byte c=n<.4f?(byte)99:n>.65f?(byte)101:(byte)100;
        // Long cracks and a few bright scuffs.
        if((x+y*2+(int)(Noise(x,y,6,11)*10))%24==0)c=98;if(R(x/3,y,12)>.96f)c=98;if((x*2-y+(int)(Noise(x,y,3,13)*12)+96)%32==0)c=99;return c;
    }
    static byte Dunes(int x,int y)
    {
        float wave=MathF.Sin((y+Noise(x,y,4,20)*6)*MathF.PI*2/8f);byte c=wave>.6f?(byte)104:wave<-.6f?(byte)102:(byte)103;if(R(x,y,21)>.97f)c=102;return c;
    }
    static byte Water(int x,int y,byte[] r)
    {
        float n=Noise(x,y,4,30);int t=n<.4f?0:n>.66f?2:1;float wave=MathF.Sin((y*2+Noise(x,y,6,31)*8)*MathF.PI*2/12f+x*MathF.PI*2/W);
        if(wave>.93f&&R(x/4,y,32)>.3f)t=3;if((x+y)%2==0&&MathF.Abs(n-.4f)<.03f)t=1;return r[t];
    }
    static byte Lava(int x,int y)
    {
        float n=Noise(x,y,4,40)*.6f+Noise(x,y,8,41)*.4f;return n<.34f?(byte)157:n<.4f?(byte)44:n<.55f?(byte)45:n<.7f?(byte)46:(byte)47;
    }
    static byte Basalt(int x,int y)
    {
        float n=Noise(x,y,6,50);int cx=x/12,cy=y/8;float crack=MathF.Min(MathF.Abs((x+ (cy%2)*6)%12-6),MathF.Abs(y%8-4)*1.5f);
        byte c=n<.4f?(byte)156:n>.65f?(byte)158:(byte)157;if(crack>5f)c=R(cx,cy,51)>.6f?(byte)44:(byte)156;if(crack>5.6f&&R(cx,cy,51)>.6f)c=46;return c;
    }
    static byte Obsidian(int x,int y)
    {
        float n=Noise(x,y,4,60);byte c=n<.45f?(byte)156:(byte)157;if((x+y+(int)(n*20))%14==0)c=158;if((x-y*2+48+(int)(n*14))%22==0)c=118;if(R(x,y,61)>.985f)c=120;return c;
    }
    static byte Setts(int x,int y,byte[] r,int w,int h)
    {
        int row=y/h,qx=(x+(row%2)*(w/2))%W,col=qx/w,lx=qx%w,ly=y%h;float id=R(col,row,70+w);
        if(lx==w-1||ly==h-1)return r[0];int t=id>.72f?3:id<.25f?1:2;if(ly==0&&id>.3f)t=Math.Min(3,t+1);if(R(x,y,71)>.95f)t=Math.Max(1,t-1);return r[t];
    }
    static byte Boards(int x,int y,bool parquet)
    {
        if(parquet){int cell=(x/12+y/6)%2;int a=cell==0?x%12:y%6,b=cell==0?y%6:x%12;float id=R(x/12,y/6,80);if((cell==0?b%3:b%4)==(cell==0?2:3)||a==0)return 13;return id>.6f?(byte)16:id<.3f?(byte)14:(byte)15;}
        int row=y/4,qx=(x+(int)(R(row,0,81)*30))%W,ly=y%4;float bid=R(qx/24,row,82);if(ly==3||qx%24==23)return 13;byte c=bid>.66f?(byte)16:bid<.3f?(byte)14:(byte)15;if(ly==0&&bid>.5f)c=16;if(R(x,y,83)>.96f)c=14;return c;
    }
    static byte Marble(int x,int y)
    {
        float n=Noise(x,y,4,90);float vein=MathF.Abs(MathF.Sin((x+y*2)*MathF.PI*2/W+n*6));byte c=n<.4f?(byte)160:(byte)161;if(vein<.08f)c=159;else if(vein<.16f)c=160;if(R(x,y,91)>.98f)c=98;return c;
    }
    static byte Checker(int x,int y)
    {
        bool dark=(x/12+y/6)%2==0;byte m=Marble(x,y);if(x%12==0||y%6==0)return 158;return dark?(m==161?(byte)158:m==160?(byte)157:(byte)159):m;
    }
    static byte Carpet(int x,int y,byte[] r)
    {
        int lx=x%24,ly=y%12;if(lx==0||ly==0)return r[0];float d=MathF.Abs(lx-12)/12f+MathF.Abs(ly-6)/6f;if(MathF.Abs(d-.7f)<.07f)return 29;if(d<.3f)return r[3];if((x+y)%2==0&&d>.9f)return r[2];return r[1];
    }
    static byte Straw(int x,int y){float n=R(x/5,y,100);byte c=n>.7f?(byte)83:n<.25f?(byte)81:(byte)82;if(R(x,y/3,101)>.9f)c=80;if(R(x/3,y,102)>.94f)c=83;return c;}
    static byte Tilled(int x,int y){int ly=y%6;byte c=ly<2?(byte)135:ly<4?(byte)134:(byte)12;if(ly==0&&R(x/3,y,110)>.5f)c=136;if(R(x,y,111)>.96f)c=13;if(ly==1&&R(x/4,y/6,112)>.8f&&x%4==1)c=4;return c;}
    static byte Turf(int x,int y,byte[] r)
    {
        float n=Noise(x,y,4,120)*.6f+Noise(x,y,8,121)*.4f;int t=n<.4f?0:n>.62f?2:1;float g=R(x/2,y,122);if(g>.9f&&y%3!=0)t=Math.Min(3,t+1);if(R(x,y,123)>.97f)t=3;return r[t];
    }
    static byte LeafLitter(int x,int y)
    {
        byte c=Turf(x,y,new byte[]{1,2,3,4});float n=R(x/3,y/2,130);float k=Noise(x,y,6,131);if(k>.42f&&n>.35f)c=n>.85f?(byte)112:n>.7f?(byte)111:n>.5f?(byte)110:(byte)109;if(k>.42f&&R(x,y,132)>.93f)c=108;return c;
    }
    static byte Gravel(int x,int y){float n=R(x/2,y,140),k=R(x/3,y/2,141);byte c=k>.75f?(byte)23:k<.2f?(byte)20:(byte)22;if(n>.8f)c=21;if(R(x,y,142)>.96f)c=24;return c;}
    static byte MossStone(int x,int y){byte c=Setts(x,y,new byte[]{20,21,22,23},12,6);float k=Noise(x,y,6,150);if(k>.55f)c=c==20?(byte)1:k>.7f?(byte)3:(byte)2;if(k>.62f&&R(x,y,151)>.9f)c=5;return c;}
    static byte CrystalFloor(int x,int y)
    {
        int cx=x/8,cy=y/6;float id=R(cx,cy,160);int lx=x%8,ly=y%6;bool edge=lx==0||ly==0||(lx+ly)==6;byte c=id>.6f?(byte)132:id<.3f?(byte)131:(byte)132;if(edge)c=131;if(lx+ly<4&&!edge)c=133;if(R(x,y,161)>.985f)c=56;return c;
    }
    static byte Grate(int x,int y){int lx=x%6,ly=y%4;if(lx==0||ly==0)return lx==0&&ly==0?(byte)43:(byte)42;if(lx==1||ly==1)return 41;return 156;}
    static byte BoneFloor(int x,int y)
    {
        byte c=Blend(x,y,170,new byte[]{12,134,134,135},.6f,5);int cx=x/12,cy=y/6;float id=R(cx,cy,171);int lx=x%12,ly=y%6;
        if(id>.55f&&ly==2+(int)(id*3)%2&&lx>2&&lx<10)c=lx==3||lx==9?(byte)59:(byte)58;if(id<.15f&&(lx-6)*(lx-6)+(ly-3)*(ly-3)*3<8)c=(lx==5||lx==7)&&ly==3?A.Ink:(byte)58;return c;
    }
}
