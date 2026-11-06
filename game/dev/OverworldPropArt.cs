using Godot;
namespace Mosslight;

public enum ReachProp { Tree, Rock, Shrub, Herb, Ore, Chest, Gateway, Stump, OreSpent, HerbSpent, ChestOpen }

/// <summary>Fixed, pixel-authored woodland modules. Pure cosmetic recipes; no world RNG,
/// simulation state, external textures or textures keyed by seed.</summary>
public static class OverworldPropArt
{
    public const int Revision=3, Width=72, Height=104, Variants=4, Columns=8, Rows=6;
    public static readonly Vector2 Pivot=new(36,100);
    public static readonly Color[] Palette={Colors.Transparent,
        new("243e35"),new("304c3b"),new("3e6045"),new("52764f"),new("6c8c59"),new("8ba66c"),
        new("393d32"),new("514638"),new("70553d"),new("91734e"),new("b09667"),
        new("354642"),new("4c5d56"),new("6a7970"),new("89968a"),new("a6b09a"),
        new("303e3c"),new("58645b"),new("8b936f"),new("b69b60"),new("d2ba7a"),
        new("6b8d88"),new("95b7a4"),new("c3d4b3"),new("c2b597"),
        new Color(.08f,.14f,.11f,.24f),new Color(.08f,.14f,.11f,.12f)};
    public static int Module(ReachProp asset,uint seed)=>(int)asset*Variants+(int)(seed%Variants);
    public static Rect2 Source(int module)=>new(module%Columns*Width,module/Columns*Height,Width,Height);
    public static bool TryFeature(string kind,bool spent,out ReachProp asset)
    {
        asset=kind switch {"wood"=>spent?ReachProp.Stump:ReachProp.Tree,"ore"=>spent?ReachProp.OreSpent:ReachProp.Ore,
            "herb"=>spent?ReachProp.HerbSpent:ReachProp.Herb,"chest"=>spent?ReachProp.ChestOpen:ReachProp.Chest,
            "portal"=>ReachProp.Gateway,_=>ReachProp.Rock};
        return kind is "wood" or "ore" or "herb" or "chest" or "portal";
    }
    public static byte[] Generate(ReachProp asset,int variant)
    {
        var p=new Pixels();int v=variant%Variants;
        // Soft, stepped contact shadows are baked once, beneath opaque silhouettes.
        p.Oval(36,100,asset==ReachProp.Tree?19:15,2,27);p.Oval(36,100,asset==ReachProp.Tree?13:10,1,26);
        switch(asset)
        {
            case ReachProp.Tree: Tree(p,v);break;
            case ReachProp.Rock: Stone(p,v,false);break;
            case ReachProp.Shrub: Shrub(p,v);break;
            case ReachProp.Herb: Herb(p,v,false);break;
            case ReachProp.Ore: Stone(p,v,true);break;
            case ReachProp.Chest: Chest(p,v,false);break;
            case ReachProp.ChestOpen: Chest(p,v,true);break;
            case ReachProp.Gateway: Gateway(p,v);break;
            case ReachProp.Stump:
                p.Poly(8,25,100,29,87,41,85,46,100);p.Rect(29,90,13,9,9);p.Rect(39,89,4,10,8);
                p.Oval(35,87,9,3,11);p.Oval(35,87,5,2,9);p.Rect(33,86,4,1,10);p.Rect(27,98,5,2,3);break;
            case ReachProp.OreSpent: Stone(p,v,false);p.Rect(31,88,3,2,22);p.Rect(40,93,2,1,23);break;
            case ReachProp.HerbSpent: Herb(p,v,true);break;
        }
        return p.Data;
    }
    static void Tree(Pixels p,int v)
    {
        int lean=new[]{0,-5,5,-2}[v];
        // Root flare and visibly forked timber, with light consistently above-left.
        p.Poly(7,25,101,31,92,32+lean,63,39+lean,63,42,94,48,101,39,99,35,100);
        p.Poly(9,29,98,34+lean,62,37+lean,62,39,96,42,99,36,97);
        p.Line(34+lean,72,24+lean,58,4,8);p.Line(37+lean,76,49+lean,56,4,8);
        p.Line(34+lean,72,25+lean,58,2,10);p.Line(37+lean,75,48+lean,56,1,10);
        p.Line(33,95,35+lean,75,1,10);p.Rect(36,86,2,7,8);p.Rect(32,96,3,2,11);
        // Four authored crown arrangements, selected by a stable coordinate hash.
        if(v==0){Crown(p,35,43,27,25,0);Crown(p,22,57,20,17,1);Crown(p,48,58,18,19,2);Crown(p,33,32,21,18,3);}
        if(v==1){Crown(p,30,29,17,21,4);Crown(p,25,49,21,23,5);Crown(p,43,53,19,22,6);Crown(p,30,65,19,14,7);}
        if(v==2){Crown(p,24,45,20,24,8);Crown(p,48,35,19,23,9);Crown(p,44,58,22,20,10);Crown(p,24,61,17,15,11);}
        if(v==3){Crown(p,20,53,17,18,12);Crown(p,42,50,25,23,13);Crown(p,32,33,20,19,14);Crown(p,49,64,17,13,15);}
        p.Rect(30,97,3,2,3);p.Rect(41,99,5,1,2);
    }
    static void Crown(Pixels p,int x,int y,int rx,int ry,int slot)
    {
        // Overlapping angular lobes have dark undersides, open breaks and quiet leaf clusters.
        p.Poly(1,x-rx,y+2,x-rx+3,y-ry/2,x-rx/2,y-ry+2,x+2,y-ry,x+rx-5,y-ry/2,x+rx,y+2,x+rx-4,y+ry-4,x+rx/3,y+ry,x-rx/2,y+ry-2);
        p.Poly(3,x-rx+2,y,x-rx+5,y-ry/2,x-rx/2,y-ry+3,x+2,y-ry+2,x+rx-6,y-ry/2,x+rx-2,y,x+rx-7,y+ry/2,x+1,y+ry-3,x-rx+6,y+ry/2);
        p.Poly(4,x-rx+5,y-3,x-rx+7,y-ry/2,x-rx/2,y-ry+4,x+1,y-ry+3,x+rx-8,y-ry/2,x+rx-8,y+2,x+1,y+ry/2,x-rx+7,y+ry/3);
        p.Poly(5,x-rx+8,y-ry/2,x-rx/2,y-ry+5,x,y-ry+4,x+rx/2,y-ry/2,x+rx/3,y-3,x-4,y,x-rx+9,y-4);
        for(int i=0;i<18;i++)
        {
            uint h=EnvironmentStyle.Hash(Revision,slot,i,18003);
            int px=x-rx+5+(int)(h%Math.Max(1,rx*2-10)),py=y-ry+5+(int)((h>>8)%Math.Max(1,ry*2-10));
            byte at=p.At(px,py);if(at is <3 or >5)continue;
            byte color=at==5?(byte)6:at==4?(byte)5:(byte)2;
            p.Clipped(px,py,3+(int)(h%3),1,color,at);p.Clipped(px+1,py+1,2,1,color,at);
        }
    }
    static void Stone(Pixels p,int v,bool ore)
    {
        int lift=ore?8:0,dx=new[]{0,1,-1,0}[v];
        p.Poly(12,23,97,24+dx,87-lift,32,81-lift,42+dx,83-lift,48,91-lift/2,49,98,40,101,28,100);
        p.Poly(14,24,94,26+dx,87-lift,32,82-lift,42+dx,84-lift,43,92,34,97);
        p.Poly(15,26+dx,87-lift,32,82-lift,42+dx,84-lift,36,88-lift/2,30,90);
        p.Poly(13,43,86-lift,48,91-lift/2,48,98,39,100,40,94);
        p.Line(32,83-lift,30,89-lift/2,1,16);p.Line(37,91,40,94,1,12);
        p.Poly(2,25,94,31,92,35,94,33,98,25,99);p.Rect(26,94,4,1,4);p.Rect(29,96,3,1,3);
        if(!ore)return;
        Crystal(p,31,85,5,23);Crystal(p,40,80,6,20);Crystal(p,44,91,4,22);
        p.Rect(34,93,2,1,24);p.Rect(39,95,3,1,19);
    }
    static void Crystal(Pixels p,int x,int y,int size,byte c)
    {p.Poly(17,x-3,y+size,x-3,y-size/2,x,y-size,x+3,y-size/2,x+3,y+size);p.Poly(c,x-2,y+size-1,x-2,y-size/2,x,y-size+1,x+2,y-size/2,x+2,y+size-1);p.Line(x-1,y-size+2,x-1,y+1,1,24);}
    static void Shrub(Pixels p,int v)
    {
        int dx=v%2*3;
        p.Line(35,98,30,87,2,8);p.Line(36,98,43,87,2,8);
        Crown(p,29,87,12,10,20+v);Crown(p,42,85-dx,11,12,24+v);Crown(p,37,94,14,6,28+v);
        p.Rect(25,90,2,1,6);if(v==2){p.Rect(38,83,2,2,20);p.Rect(43,89,2,2,20);}
    }
    static void Herb(Pixels p,int v,bool spent)
    {
        int top=spent?92:72-v%2*3;
        p.Line(35,99,35,top,2,3);p.Line(36,98,42,spent?94:80,1,4);
        p.Poly(3,35,96,25,92,24,87,31,89);p.Poly(5,35,94,25,89,27,89,33,91);
        if(spent){p.Rect(34,91,3,1,10);return;}
        p.Poly(3,36,90,43,82,48,82,46,88,39,92);p.Line(38,89,44,84,1,5);
        p.Poly(4,35,84,27,80,26,75,33,77);p.Line(29,78,34,81,1,6);
        // Pale seedhead distinguishes a harvestable plant from decorative shrubs.
        p.Rect(32,top-3,6,5,19);p.Rect(33,top-5,4,3,25);p.Rect(34,top-6,2,2,24);
        p.Rect(40,77,4,4,25);p.Rect(41,75,2,3,24);
    }
    static void Chest(Pixels p,int v,bool open)
    {
        p.Poly(7,18,80,45,80,53,86,53,100,45,103,18,99);
        p.Rect(20,84,25,15,9);p.Poly(8,45,84,51,87,51,99,45,101);
        for(int x=23;x<45;x+=6)p.Rect(x,86,1,12,8);
        p.Rect(20,84,25,2,10);p.Rect(20,97,25,2,8);p.Rect(20,99,3,2,7);p.Rect(47,99,3,2,7);
        if(open)
        {
            p.Rect(18,67,28,15,7);p.Rect(20,69,24,10,9);p.Rect(21,70,22,2,10);
            p.Rect(20,82,25,7,7);p.Rect(23,83,18,4,8);p.Rect(23,68,2,12,18);p.Rect(39,68,2,12,18);
        }
        else
        {
            p.Poly(8,18,81,23,75,44,75,51,81,46,87,18,87);
            p.Poly(10,20,80,24,77,43,77,48,81,44,83,20,83);p.Rect(20,83,25,3,9);
            p.Rect(23,77,3,22,18);p.Rect(39,77,3,22,18);p.Rect(23,78,1,20,19);p.Rect(39,78,1,20,19);
            p.Rect(31,85,7,8,20);p.Rect(32,86,5,1,21);p.Rect(34,88,2,3,7);
        }
        p.Rect(27+v,92,3,1,10);p.Rect(46,90,3,1,7);
    }
    static void Gateway(Pixels p,int v)
    {
        p.Rect(13,48,11,53,12);p.Rect(48,48,11,53,12);
        p.Rect(15,49,7,50,14);p.Rect(49,49,7,50,13);
        for(int y=51;y<97;y+=10){p.Rect(15,y,7,1,12);p.Rect(49,y,7,1,12);p.Rect(15,y+1,1,8,15);}
        p.Poly(12,12,50,16,41,55,41,60,50);p.Poly(14,14,47,18,42,54,42,58,47);p.Rect(18,42,36,2,16);
        p.Rect(18,41,7,2,3);p.Rect(50,48,6,3,2);p.Rect(15,88,4,9,3);p.Rect(16,88,2,3,4);
        // Restricted teal belongs to this real portal, never to ordinary scenery.
        p.Poly(22,30,62,36,56,42,62,36,69);p.Poly(24,33,62,36,59,39,62,36,66);
        p.Rect(24,97,24,3,13);p.Rect(27,94,18,3,14);p.Rect(30,91,12,3,15);
    }
    sealed class Pixels
    {
        public readonly byte[] Data=new byte[Width*Height];
        public byte At(int x,int y)=>x>=0&&x<Width&&y>=0&&y<Height?Data[y*Width+x]:(byte)0;
        public void Rect(int x,int y,int w,int h,byte c){for(int yy=Math.Max(0,y);yy<Math.Min(Height,y+h);yy++)for(int xx=Math.Max(0,x);xx<Math.Min(Width,x+w);xx++)Data[yy*Width+xx]=c;}
        public void Clipped(int x,int y,int w,int h,byte c,byte onto){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)if(At(xx,yy)==onto)Rect(xx,yy,1,1,c);}
        public void Oval(int x,int y,int rx,int ry,byte c){for(int yy=-ry;yy<=ry;yy++)for(int xx=-rx;xx<=rx;xx++)if(xx*xx*ry*ry+yy*yy*rx*rx<=rx*rx*ry*ry)Rect(x+xx,y+yy,1,1,c);}
        public void Line(int x,int y,int ex,int ey,int width,byte c){int dx=Math.Abs(ex-x),sx=x<ex?1:-1,dy=-Math.Abs(ey-y),sy=y<ey?1:-1,err=dx+dy;while(true){Rect(x,y,width,width,c);if(x==ex&&y==ey)break;int e=err*2;if(e>=dy){err+=dy;x+=sx;}if(e<=dx){err+=dx;y+=sy;}}}
        public void Poly(byte c,params int[] xy)
        {
            int minX=Width,maxX=0,minY=Height,maxY=0;
            for(int i=0;i<xy.Length;i+=2){minX=Math.Min(minX,xy[i]);maxX=Math.Max(maxX,xy[i]);minY=Math.Min(minY,xy[i+1]);maxY=Math.Max(maxY,xy[i+1]);}
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                bool inside=false;
                for(int i=0,j=xy.Length-2;i<xy.Length;j=i,i+=2)
                    if((xy[i+1]>y+.5f)!=(xy[j+1]>y+.5f) && x+.5f<(xy[j]-xy[i])*(y+.5f-xy[i+1])/(xy[j+1]-xy[i+1])+xy[i])inside=!inside;
                if(inside)Rect(x,y,1,1,c);
            }
        }
    }
}

/// <summary>One fixed process-lifetime atlas, shared by terrain, decorations and features.</summary>
public static class OverworldPropAtlas
{
    static ImageTexture? texture;
    static readonly bool verification=OS.GetCmdlineUserArgs().Contains("--visual-check");
    static readonly bool previousCommand=OS.GetCmdlineUserArgs().Contains("--part2-props");
    public static bool VerificationPrevious {get;set;}
    public static bool Enabled=>!(verification && (VerificationPrevious || previousCommand));
    public static long PixelBytes=>texture==null?0:OverworldPropArt.Columns*OverworldPropArt.Width*OverworldPropArt.Rows*OverworldPropArt.Height*4L;
    public static int BuildCount {get;private set;}
    public static double BuildMilliseconds {get;private set;}
    public static Texture2D Texture=>texture??=Build();
    static ImageTexture Build()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        using var image=Image.CreateEmpty(OverworldPropArt.Columns*OverworldPropArt.Width,OverworldPropArt.Rows*OverworldPropArt.Height,false,Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        foreach(var asset in Enum.GetValues<ReachProp>())for(int v=0;v<OverworldPropArt.Variants;v++)
        {
            var data=OverworldPropArt.Generate(asset,v);var rect=OverworldPropArt.Source(OverworldPropArt.Module(asset,(uint)v));
            for(int y=0;y<OverworldPropArt.Height;y++)for(int x=0;x<OverworldPropArt.Width;x++)
                if(data[y*OverworldPropArt.Width+x]!=0)image.SetPixel((int)rect.Position.X+x,(int)rect.Position.Y+y,OverworldPropArt.Palette[data[y*OverworldPropArt.Width+x]]);
        }
        var result=ImageTexture.CreateFromImage(image);BuildCount++;BuildMilliseconds=timer.Elapsed.TotalMilliseconds;return result;
    }
    public static void Draw(CanvasItem canvas,int module,Vector2 foot)
        =>canvas.DrawTextureRectRegion(Texture,new Rect2(foot-OverworldPropArt.Pivot,new Vector2(OverworldPropArt.Width,OverworldPropArt.Height)),OverworldPropArt.Source(module));
}
