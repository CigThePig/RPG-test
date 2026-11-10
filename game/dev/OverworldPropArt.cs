using Godot;
namespace Mosslight;

public enum ReachProp { Tree, Rock, Shrub, Herb, Ore, Chest, Gateway, Stump, OreSpent, HerbSpent, ChestOpen, WaterFlora, GroundFlora }

/// <summary>Fixed, procedurally painted woodland modules: lit volumes, one dark contour and
/// a cast shadow, all from an indexed palette. Pure cosmetic recipes; no world RNG,
/// simulation state, external textures or textures keyed by seed.</summary>
public static class OverworldPropArt
{
    public const int Revision=4, Width=72, Height=104, Variants=4, Columns=8, Rows=7;
    public static readonly int Modules=Enum.GetValues<ReachProp>().Length*Variants;
    public static readonly Vector2 Pivot=new(36,100);
    // 1-6 broadleaf, 7-10 conifer, 11 pale leaf tip, 12-16 bark, 17-18 birch bark,
    // 19-24 stone, 25-27 crystal, 28-30 gold, 31-34 blossom, 35-36 shadow, 37 contour.
    public static readonly Color[] Palette={Colors.Transparent,
        new("1d4034"),new("2a5a3a"),new("3a7a40"),new("55994a"),new("7ab852"),new("a6d463"),
        new("173a3a"),new("1f5045"),new("2d6b4c"),new("448653"),new("c8e07a"),
        new("33241f"),new("4d3528"),new("6d4b33"),new("8f6842"),new("b38a58"),
        new("e3dfcc"),new("aaa99a"),
        new("262b3a"),new("414a59"),new("5d6873"),new("7f8a92"),new("a3adae"),new("c9cfc6"),
        new("2e8f96"),new("5cc7c0"),new("c4f3e2"),
        new("b9822f"),new("f0c659"),new("fff0b0"),
        new("f3ecd2"),new("d98bb6"),new("c9483e"),new("7d7fd6"),
        new Color(.04f,.08f,.16f,.30f),new Color(.04f,.08f,.16f,.15f),new("172428")};
    const byte Ink=37,Shadow=35,Penumbra=36;
    static readonly byte[] Leaf={1,2,2,3,3,4,5,6},Needle={7,7,8,8,9,9,10,4},Airy={2,3,4,4,5,5,6,11},
        Stone={19,20,20,21,21,22,23,24},Bark={12,13,14,14,15,16};
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
        switch(asset)
        {
            case ReachProp.Tree: Tree(p,v);break;
            case ReachProp.Rock: Boulder(p,v,0);break;
            case ReachProp.Shrub: Shrub(p,v);break;
            case ReachProp.Herb: Herb(p,v,false);break;
            case ReachProp.Ore: Boulder(p,v,1);break;
            case ReachProp.Chest: Chest(p,v,false);break;
            case ReachProp.ChestOpen: Chest(p,v,true);break;
            case ReachProp.Gateway: Gateway(p,v);break;
            case ReachProp.Stump: Stump(p,v);break;
            case ReachProp.OreSpent: Boulder(p,v,2);break;
            case ReachProp.HerbSpent: Herb(p,v,true);break;
            case ReachProp.WaterFlora: WaterFlora(p,v);break;
            case ReachProp.GroundFlora: GroundFlora(p,v);break;
        }
        // Flat pads and loose blades read better without a hard edge.
        p.Contour(asset==ReachProp.WaterFlora&&v<2||asset==ReachProp.GroundFlora&&v==0);
        return p.Data;
    }
    static void Tree(Pixels p,int v)
    {
        if(v==0)
        {
            // Spreading oak: heavy low crown on a short forked bole.
            p.Cast(40,99,23,3);p.Trunk(36,60,100,7,11,0,Bark);
            p.Line(34,66,22,54,3,13);p.Line(38,66,50,54,3,13);p.Line(34,65,23,54,1,15);
            p.Blob(22,53,15,12,Leaf,11,-.20f);p.Blob(51,54,15,12,Leaf,12,-.24f);p.Blob(36,59,17,10,Leaf,13,-.30f);
            p.Blob(36,39,22,19,Leaf,14,-.04f);p.Blob(24,37,14,12,Leaf,15,.04f);p.Blob(48,40,14,12,Leaf,16,-.04f);
            p.Blob(35,25,15,11,Leaf,17,.08f);
        }
        if(v==1)
        {
            // Tall elm: stacked crown, long clear trunk.
            p.Cast(39,99,17,3);p.Trunk(36,50,100,6,9,-1,Bark);
            p.Line(35,60,27,50,2,13);p.Line(37,58,45,48,2,13);
            p.Blob(36,55,15,9,Leaf,21,-.30f);p.Blob(26,46,12,12,Leaf,22,-.14f);p.Blob(47,47,12,12,Leaf,23,-.20f);
            p.Blob(36,33,16,15,Leaf,24,-.02f);p.Blob(30,21,12,11,Leaf,25,.06f);p.Blob(42,15,10,9,Leaf,26,.04f);
            p.Blob(35,10,8,6,Leaf,27,.10f);
        }
        if(v==2)
        {
            // Dark fir: drooping tiers narrowing to a leader.
            p.Cast(39,99,16,3);p.Trunk(36,80,100,5,7,0,Bark);
            p.Tier(36,60,90,21,31);p.Tier(36,44,74,18,32);p.Tier(36,29,58,14,33);p.Tier(36,16,42,10,34);p.Tier(36,5,27,6,35);
        }
        if(v==3)
        {
            // Birch: pale leaning stem under a light, open crown.
            p.Cast(39,99,16,3);p.Trunk(36,36,100,4,6,2,new byte[]{18,17,17,17,17,18});
            for(int y=44;y<98;y+=7){int x=35+(100-y)/32;p.Rect(x-(y%2),y,3,1,12);p.Rect(x+1,y+3,2,1,20);}
            p.Line(38,56,46,48,1,18);p.Line(37,62,29,54,1,18);
            p.Blob(29,54,9,7,Airy,41,-.22f);p.Blob(47,50,10,8,Airy,42,-.24f);p.Blob(38,44,13,10,Airy,43,-.12f);
            p.Blob(28,38,10,9,Airy,44,-.02f);p.Blob(47,35,10,9,Airy,45,-.06f);p.Blob(37,27,12,10,Airy,46,.04f);
            p.Blob(33,16,9,8,Airy,47,.08f);p.Blob(43,19,7,6,Airy,48,.02f);
        }
        // Grass gathers round the foot.
        p.Tuft(29,100,3);p.Tuft(43,100,4);
    }
    static void Stump(Pixels p,int v)
    {
        p.Cast(39,99,13,2);p.Trunk(36,89,100,11,15,0,Bark);
        p.Oval(36,89,6,2,16);p.Oval(36,89,4,1,15);p.Rect(35,89,2,1,14);p.Rect(31,88,3,1,24);
        p.Rect(41,92,1,5,12);p.Rect(32,93,1,4,12);
        if(v%2==0){p.Rect(44,95,3,2,33);p.Rect(45,97,1,2,31);p.Rect(45,95,1,1,31);}
        else{p.Line(30,90,27,85,1,4);p.Rect(25,84,3,2,5);p.Rect(28,86,2,1,4);}
        p.Tuft(27,100,2);p.Tuft(45,100,3);
    }
    static void Boulder(Pixels p,int v,int ore)
    {
        // Variants change mass and stacking; ore and its spent remnant share each silhouette.
        int lift=ore==1?3:0;
        p.Cast(39,99,v==1?17:14,2);
        if(v==0){p.Blob(36,91-lift,13,9+lift,Stone,61,0,.05f,true);}
        if(v==1){p.Blob(30,92-lift,10,8+lift,Stone,62,0,.05f,true);p.Blob(44,94,9,6,Stone,63,-.06f,.05f,true);}
        if(v==2){p.Blob(37,90-lift,11,10+lift,Stone,64,0,.06f,true);p.Blob(27,96,6,4,Stone,65,-.05f,.05f,true);}
        if(v==3){p.Blob(35,93-lift,15,7+lift,Stone,66,.02f,.04f,true);p.Blob(45,96,6,4,Stone,67,-.08f,.05f,true);}
        // A crack and a cap of moss tie the stone to the meadow.
        p.Over(33+v,88-lift,1,5,20,21,22);p.Over(34+v,92-lift,3,1,20,21,22);
        if(ore==0){p.Over(28+v*2,84,9,3,4,22,23,24);p.Over(30+v*2,83,5,1,5,23,24);p.Over(27+v*2,86,4,2,3,21,22);}
        p.Tuft(24,100,2);p.Tuft(47,100,3);
        if(ore==1)
        {
            Crystal(p,29,84-lift,6,25,26);Crystal(p,37,78-lift,8,25,26);Crystal(p,44,86-lift,5,25,26);
            p.Rect(33,93,2,2,29);p.Rect(41,95,2,1,29);p.Rect(26,94,1,1,30);
        }
        if(ore==2)
        {
            // Sockets and spall where the crystal was cut away.
            p.Over(29,86,4,3,19,20,21,22,23);p.Over(38,84,5,3,19,21,22,23,24);p.Rect(31,87,1,1,25);p.Rect(40,85,2,1,25);
            p.Rect(48,98,3,2,22);p.Rect(22,98,2,1,23);p.Rect(49,98,1,1,24);
        }
    }
    static void Crystal(Pixels p,int x,int y,int size,byte dark,byte lit)
    {
        p.Poly(dark,x-3,y+size,x-3,y-size/2,x,y-size,x+3,y-size/2,x+3,y+size);
        p.Poly(lit,x-3,y+size,x-3,y-size/2,x,y-size,x,y+size);
        p.Line(x-1,y-size+2,x-1,y+size-3,1,27);p.Rect(x,y-size+1,1,1,27);
    }
    static void Shrub(Pixels p,int v)
    {
        p.Cast(39,99,14,2);
        p.Blob(28,92,9,7,Leaf,71+v,-.16f);p.Blob(44,92,9,7,Leaf,75+v,-.22f);p.Blob(36,88-v%2*2,11,9,Leaf,79+v,.02f);
        // Two variants carry berries or blossom so thickets do not repeat.
        if(v==1)foreach(var (x,y) in new[]{(30,89),(38,84),(43,91),(34,93),(47,88)}){p.Rect(x,y,2,2,33);p.Rect(x,y,1,1,32);}
        if(v==3)foreach(var (x,y) in new[]{(29,87),(37,82),(44,89),(33,92),(41,86)}){p.Rect(x,y,2,2,31);p.Rect(x+1,y+1,1,1,29);}
        p.Tuft(26,100,2);p.Tuft(46,100,2);
    }
    static void Herb(Pixels p,int v,bool spent)
    {
        p.Cast(38,99,8,2);
        int lean=v%2==0?-1:1;
        // Broad basal leaves stay after gathering.
        p.Poly(3,36,99,25,95,23,90,30,91,36,96);p.Poly(4,36,98,27,94,26,91,31,92);
        p.Poly(2,36,99,47,95,49,90,42,91,36,96);p.Poly(3,37,98,45,94,46,91,41,92);
        p.Line(28,93,34,97,1,5);
        if(spent){p.Rect(35,93,2,6,3);p.Rect(35,92,2,1,11);p.Rect(39,95,1,3,3);p.Rect(39,94,1,1,11);return;}
        p.Line(36,98,36+lean,74,2,3);p.Line(36,98,36+lean,74,1,4);
        p.Line(36,88,30,82,1,3);p.Line(37,84,43,78,1,3);
        // Three luminous bells: the gatherable part.
        foreach(var (x,y) in new[]{(28,79),(42,75),(35+lean,69)})
        {p.Rect(x,y,4,4,32);p.Rect(x,y,3,2,31);p.Rect(x+1,y+4,2,1,29);p.Rect(x+3,y+3,1,1,34);}
    }
    static void Chest(Pixels p,int v,bool open)
    {
        p.Cast(40,100,17,2);
        // Body: plank front, shaded end, iron feet.
        p.Rect(20,84,26,16,14);p.Poly(13,46,84,52,81,52,96,46,100);
        for(int x=26;x<46;x+=7)p.Rect(x,85,1,15,13);
        p.Rect(20,84,26,1,16);p.Rect(20,98,26,2,13);p.Rect(20,99,3,2,20);p.Rect(43,99,3,2,20);p.Rect(50,96,2,2,19);
        p.Rect(24,84,3,16,28);p.Rect(39,84,3,16,28);p.Rect(24,84,1,16,29);p.Rect(39,84,1,16,29);
        if(open)
        {
            // Lid thrown back, dark hollow, a glint left on the rim.
            p.Poly(13,20,84,26,81,52,81,46,84);p.Rect(22,82,23,2,12);
            p.Poly(14,22,66,48,66,52,70,52,81,26,81,22,78);p.Rect(24,68,22,11,15);p.Rect(24,68,22,1,16);
            p.Rect(26,66,3,15,28);p.Rect(41,66,3,15,28);p.Rect(26,66,1,15,29);
            p.Rect(30+v,82,2,1,29);
        }
        else
        {
            p.Poly(15,20,84,20,78,24,74,44,74,46,78,46,84);p.Poly(13,46,84,46,78,44,74,50,72,52,76,52,81);
            p.Rect(24,74,20,1,16);p.Rect(21,78,24,1,16);
            p.Rect(24,74,3,10,28);p.Rect(39,74,3,10,28);p.Rect(24,74,1,10,29);p.Rect(39,74,1,10,29);
            p.Rect(30,81,6,7,28);p.Rect(30,81,6,1,30);p.Rect(31,82,1,5,29);p.Rect(32,84,2,2,19);
        }
        p.Tuft(18,101,2);
    }
    static void Gateway(Pixels p,int v)
    {
        p.Cast(40,100,27,2);
        // Dressed-stone arch around a standing pool of light.
        for(int y=44;y<100;y++)for(int x=22;x<50;x++)
        {
            float dx=(x-35.5f)/13f,dy=(y-60)/15f;if(y<60&&dx*dx+dy*dy>1)continue;
            float glow=1-MathF.Min(1,MathF.Sqrt(dx*dx+(y-72)*(y-72)/900f));
            p.Set(x,y,glow>.78f?(byte)27:glow>.5f||((x+y)%2==0&&glow>.4f)?(byte)26:(byte)25);
        }
        p.Rect(12,56,11,45,21);p.Rect(49,56,11,45,21);p.Rect(12,56,3,45,23);p.Rect(57,56,3,45,20);p.Rect(20,56,3,45,20);p.Rect(49,56,2,45,20);
        for(int i=0;i<9;i++)
        {
            float a=MathF.PI*(i+.5f)/9;int x=(int)(36-MathF.Cos(a)*19),y=(int)(60-MathF.Sin(a)*20);
            p.Rect(x-4,y-4,8,8,i<4?(byte)22:(byte)21);p.Rect(x-4,y-4,8,1,i<5?(byte)24:(byte)22);p.Rect(x+3,y-3,1,7,20);
        }
        for(int y=64;y<98;y+=9){p.Rect(12,y,11,1,19);p.Rect(49,y,11,1,19);}
        p.Rect(32,36,8,8,23);p.Rect(32,36,8,1,24);p.Rect(35,38,2,4,26);p.Rect(34,39,4,2,26);
        // Moss on the weather side; variants move the growth, never the opening.
        p.Over(12,56+v*6,5,7,3,21,23);p.Over(13,58+v*6,3,3,4,3);p.Over(52,90-v*4,7,6,2,20,21);p.Over(24+v,41,9,3,4,22,24);
        p.Rect(24,98,24,3,22);p.Rect(24,98,24,1,24);p.Tuft(11,101,3);p.Tuft(60,101,3);
    }
    static void WaterFlora(Pixels p,int v)
    {
        if(v<2)
        {
            // Lily pads lie flat: no cast shadow, no contour above the water.
            foreach(var (x,y,r) in v==0?new[]{(28,94,8),(44,97,6),(40,88,5)}:new[]{(31,96,7),(45,92,5)})
            {
                p.Oval(x+1,y+1,r,r/2,Penumbra);p.Oval(x,y,r,r/2,2);p.Oval(x,y,r-1,r/2-1,4);p.Oval(x-1,y-1,r-3,Math.Max(1,r/2-2),5);
                // The notch that makes a disc a lily pad.
                p.Rect(x,y,r+1,1,2);p.Rect(x+r/2,y+1,r/2,1,2);
            }
            if(v==0){p.Rect(43,95,4,3,32);p.Rect(44,94,2,1,31);p.Rect(44,96,2,1,29);}
            return;
        }
        // Reedmace standing in the shallows.
        foreach(var (x,top,head) in v==2?new[]{(31,86,true),(35,82,true),(39,89,false),(42,85,true)}:new[]{(32,89,false),(36,84,true),(40,90,false)})
        {
            p.Line(x,99,x+(x%2==0?1:-1),top,1,3);p.Line(x+1,99,x+3,top+6,1,4);
            if(head){p.Rect(x-1+(x%2==0?1:-1),top-4,2,5,14);p.Rect(x-1+(x%2==0?1:-1),top-4,1,4,15);p.Rect(x+(x%2==0?1:-1),top-6,1,2,4);}
        }
    }
    static void GroundFlora(Pixels p,int v)
    {
        if(v==0)
        {
            // Meadow clump: long blades of uneven height, palest at the tips.
            foreach(var (x,top,tilt) in new[]{(29,93,-2),(32,89,-1),(35,86,0),(38,88,1),(41,91,2),(44,94,3)})
            {p.Line(x,99,x+tilt,top,1,3);p.Line(x+1,99,x+tilt+1,top+2,1,4);p.Set(x+tilt,top,5);p.Set(x+tilt,top+1,5);if(x%2==1)p.Set(x+tilt,top-1,6);}
        }
        if(v==1)
        {
            // Scarlet caps.
            p.Cast(38,100,8,1);
            foreach(var (x,y,w) in new[]{(31,94,7),(41,96,5),(37,98,3)})
            {p.Rect(x+w/2-1,y+1,2,100-y-1,31);p.Rect(x,y-1,w,2,33);p.Rect(x+1,y-2,w-2,1,33);p.Rect(x+1,y-2,1,1,31);p.Rect(x+w-2,y-1,1,1,31);}
        }
        if(v==2)
        {
            // Mossed windfall.
            p.Cast(38,100,16,2);
            p.Rect(23,93,26,7,14);p.Rect(23,93,26,2,15);p.Rect(23,98,26,2,13);p.Oval(49,96,2,3,16);p.Oval(49,96,1,2,15);
            for(int x=27;x<47;x+=6)p.Rect(x,95,2,1,13);
            p.Over(26,92,10,2,4,14,15);p.Over(28,91,5,1,5,0,14);p.Over(38,93,6,1,3,15);p.Line(31,93,29,88,1,13);
        }
        if(v==3)
        {
            // Bluebell clump.
            foreach(var (x,top) in new[]{(31,88),(36,84),(41,89)})
            {p.Line(x,99,x,top,1,3);p.Rect(x-1,top,3,3,34);p.Rect(x-1,top,2,1,31);p.Rect(x+1,top+4,2,2,34);p.Rect(x-3,96,3,1,4);}
        }
        p.Tuft(30,100,2);p.Tuft(42,100,2);
    }
    sealed class Pixels
    {
        public readonly byte[] Data=new byte[Width*Height];
        public byte At(int x,int y)=>x>=0&&x<Width&&y>=0&&y<Height?Data[y*Width+x]:(byte)0;
        // The outermost ring stays transparent so neighbouring atlas cells never bleed.
        public void Set(int x,int y,byte c){if(x>0&&x<Width-1&&y>0&&y<Height-1)Data[y*Width+x]=c;}
        public void Rect(int x,int y,int w,int h,byte c){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)Set(xx,yy,c);}
        /// <summary>Recolour only pixels that already hold one of the listed materials.</summary>
        public void Over(int x,int y,int w,int h,byte c,params byte[] onto){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)if(Array.IndexOf(onto,At(xx,yy))>=0)Set(xx,yy,c);}
        public void Oval(int x,int y,int rx,int ry,byte c){for(int yy=-ry;yy<=ry;yy++)for(int xx=-rx;xx<=rx;xx++)if(xx*xx*ry*ry+yy*yy*rx*rx<=rx*rx*ry*ry)Set(x+xx,y+yy,c);}
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
                if(inside)Set(x,y,c);
            }
        }
        static float Grain(int x,int y,int slot)=>(EnvironmentStyle.Hash(Revision,x,y,18100+slot)&1023)/1023f;
        static float Cloud(float x,float y,int slot)
        {
            int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float fx=x-ix,fy=y-iy;
            return Mathf.Lerp(Mathf.Lerp(Grain(ix,iy,slot),Grain(ix+1,iy,slot),fx),Mathf.Lerp(Grain(ix,iy+1,slot),Grain(ix+1,iy+1,slot),fx),fy);
        }
        /// <summary>Stepped ground shadow, thrown slightly down-right of the foot.</summary>
        public void Cast(int x,int y,int rx,int ry){Oval(x,y,rx,ry,Penumbra);Oval(x-1,y,rx-3,Math.Max(1,ry-1),Shadow);}
        /// <summary>A lit ellipsoid with a scalloped rim, clustered tone breaks and a dark underside.</summary>
        public void Blob(float cx,float cy,float rx,float ry,byte[] ramp,int slot,float bias=0,float rough=.11f,bool hard=false)
        {
            for(int y=(int)(cy-ry-3);y<=cy+ry+3;y++)for(int x=(int)(cx-rx-3);x<=cx+rx+3;x++)
            {
                float nx=(x+.5f-cx)/rx,ny=(y+.5f-cy)/ry,angle=MathF.Atan2(ny,nx);
                float rim=1+rough*(MathF.Sin(angle*5+slot)+.6f*MathF.Sin(angle*9+slot*2.3f));
                float r=MathF.Sqrt(nx*nx+ny*ny)/rim;if(r>1)continue;
                float nz=MathF.Sqrt(1-r*r);
                float light=.5f+.5f*(-nx*.50f-ny*.66f+nz*.56f)+bias;
                // Foliage breaks into soft clumps; stone into broad planes with fine grit.
                light+=hard?(Cloud(x/7f,y/5f,slot)-.5f)*.34f+(Grain(x/2,y,slot)-.5f)*.06f
                           :(Cloud(x/3.2f,y/2.6f,slot)-.5f)*.46f+(Grain(x,y,slot)-.5f)*.10f;
                int tone=Math.Clamp((int)(light*ramp.Length),1,ramp.Length-1);
                if(r>.80f&&nx*.55f+ny*.83f>.25f)tone=Math.Min(tone,r>.9f?0:1);
                Set(x,y,ramp[tone]);
            }
        }
        /// <summary>Tapered, side-lit stem with flared roots and broken bark.</summary>
        public void Trunk(int x,int top,int bottom,int topWidth,int bottomWidth,int lean,byte[] ramp)
        {
            for(int y=top;y<=bottom;y++)
            {
                float t=(y-top)/(float)Math.Max(1,bottom-top);
                float width=Mathf.Lerp(topWidth,bottomWidth,t*t)+(bottom-y<3?3-(bottom-y):0);
                int centre=x+(int)MathF.Round(lean*(1-t)),left=centre-(int)MathF.Round(width/2);
                for(int i=0;i<(int)MathF.Round(width);i++)
                {
                    float across=i/MathF.Max(1,width-1);
                    int tone=across<.2f?4:across<.52f?3:across<.8f?2:1;
                    uint h=EnvironmentStyle.Hash(Revision,left+i,y/3,18300);
                    if(h%7==0)tone=Math.Max(1,tone-1);if(h%23==0)tone=Math.Min(ramp.Length-1,tone+1);
                    Set(left+i,y,ramp[tone]);
                }
            }
        }
        /// <summary>One drooping conifer skirt: lit left, shaded right, ragged hem.</summary>
        public void Tier(int x,int top,int bottom,int half,int slot)
        {
            for(int y=top;y<=bottom;y++)
            {
                float t=(y-top)/(float)(bottom-top);float reach=1.5f+half*MathF.Pow(t,.82f);
                for(int dx=-(int)reach;dx<=(int)reach;dx++)
                {
                    // Hem: each bough ends at a different height.
                    int hem=bottom-(int)(Grain((x+dx)/3,slot,7)*5)-(Math.Abs(dx)%4==3?2:0);
                    if(y>hem)continue;
                    float across=(dx+reach)/(2*reach);
                    float light=.78f-across*.5f-(hem-y<3?.34f:0)+(Cloud((x+dx)/2.6f,y/2.2f,slot)-.5f)*.36f-t*.06f;
                    Set(x+dx,y,Needle[Math.Clamp((int)(light*Needle.Length),0,Needle.Length-1)]);
                }
            }
        }
        /// <summary>A few blades over the base so props sit in the turf rather than on it.</summary>
        public void Tuft(int x,int y,int blades)
        {for(int i=0;i<blades;i++){int bx=x+i*2-blades;Set(bx,y-1,3);Set(bx,y-2,i%2==0?(byte)5:(byte)4);if(i%2==0)Set(bx+1,y-3,5);}}
        /// <summary>One-pixel dark contour round every opaque form; shadows stay soft.</summary>
        public void Contour(bool flat)
        {
            if(flat)return;
            bool Solid(int x,int y){byte c=At(x,y);return c!=0&&c!=Shadow&&c!=Penumbra&&c!=Ink;}
            var edge=new List<int>();
            for(int y=1;y<Height-1;y++)for(int x=1;x<Width-1;x++)
                if(!Solid(x,y)&&At(x,y)!=Ink&&(Solid(x-1,y)||Solid(x+1,y)||Solid(x,y-1)||Solid(x,y+1)))edge.Add(y*Width+x);
            foreach(int at in edge)Data[at]=Ink;
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
