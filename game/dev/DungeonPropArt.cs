using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
namespace Mosslight;

public enum DungeonProp
{
    Column, Shelf, Altar, Storage, Collapse,
    Chest, ChestOpen, Key, Relic, RelicTaken, LeverUp, LeverDown, Brazier, Flame, WardFlame, Exit,
    Stave, StaveOpen, LockPlate, Portcullis, PortcullisRaised,
    Sconce, TorchFlame, Banner,
    Rubble, Bones, Pots, Growth, Debris, Sacred,
    Mosaic, Threshold, Records, Offering, MossPatch
}

/// <summary>Fixed, procedurally painted ruin modules in the Living Reach's manner: lit
/// volumes, one dark contour and a cast shadow, from the same indexed palette and the same
/// 72x104 cell. Pure cosmetic recipes; no world RNG, simulation state or external textures.</summary>
public static class DungeonPropArt
{
    public const int Revision=1,Width=OverworldPropArt.Width,Height=OverworldPropArt.Height,Columns=12,Themes=3,Variants=3;
    // Structural props carry one module per theme and variant; the rest are plain variants.
    static readonly int[] Counts={9,9,9,9,9, 1,1,1,1,1,1,1,1,4,4,1, 1,1,1,1,1, 1,4,3, 4,4,4,4,4,4, 3,3,2,2,2};
    static readonly int[] Offsets=Counts.Select((_,i)=>Counts.Take(i).Sum()).ToArray();
    public static readonly int Modules=Counts.Sum();
    public static readonly int Rows=(Modules+Columns-1)/Columns;
    public static readonly Vector2 Pivot=OverworldPropArt.Pivot,FlatPivot=new(36,94);
    public static int Count(DungeonProp prop)=>Counts[(int)prop];
    public static int Module(DungeonProp prop,int variant=0)=>Offsets[(int)prop]+(int)((uint)variant%(uint)Counts[(int)prop]);
    public static int Structural(DungeonProp prop,int theme,int variant)=>Module(prop,theme%Themes*Variants+variant%Variants);
    public static Rect2 Source(int module)=>new(module%Columns*Width,module/Columns*Height,Width,Height);
    public static DungeonProp Kind(int module){int at=Array.FindLastIndex(Offsets,o=>o<=module);return (DungeonProp)at;}
    public static bool Flat(DungeonProp prop)=>prop is DungeonProp.Flame or DungeonProp.WardFlame or DungeonProp.TorchFlame or >=DungeonProp.Mosaic;
    /// <summary>Feature kinds the ruin atlas paints. The seal is drawn live as light.</summary>
    public static bool Handles(string kind)=>kind is "chest" or "key" or "relic" or "lever" or "brazier" or "exit" or "lock" or "portcullis" or "seal";

    const byte Ink=37;
    static readonly byte[] Slate={19,20,21,22,23,24},Sand={48,49,50,51,52,53},Wood={12,13,14,15,16,16},Iron={37,41,42,42,43,23},
        Slate8={19,20,20,21,21,22,23,24},Sand8={48,49,49,50,51,52,52,53},Clay8={66,66,67,67,67,68,68,65},Bone8={57,57,58,58,58,59,59,59},
        Sack8={13,57,57,58,58,65,65,59},Leaf8={1,2,2,3,3,4,5,6};
    static byte[] Stone(int theme)=>theme==2?Sand:Slate;
    static byte[] Stone8(int theme)=>theme==2?Sand8:Slate8;
    static uint Hash(int x,int y,int slot)=>EnvironmentStyle.Hash(Revision,x,y,19000+slot);

    public static byte[] Generate(DungeonProp prop,int variant)
    {
        var p=new Pixels();int theme=variant/Variants%Themes,v=variant%Variants;
        switch(prop)
        {
            case DungeonProp.Column: Column(p,theme,v);break;
            case DungeonProp.Shelf: Shelf(p,theme,v);break;
            case DungeonProp.Altar: Altar(p,theme,v);break;
            case DungeonProp.Storage: Storage(p,theme,v);break;
            case DungeonProp.Collapse: Collapse(p,theme,v);break;
            case DungeonProp.Chest: case DungeonProp.ChestOpen:
                // The Reach's banded chest, without the turf round its foot.
                Array.Copy(OverworldPropArt.Generate(prop==DungeonProp.Chest?ReachProp.Chest:ReachProp.ChestOpen,0),p.Data,p.Data.Length);
                p.Rect(13,95,7,7,0);return p.Data;
            case DungeonProp.Key: Key(p);break;
            case DungeonProp.Relic: Relic(p,false);break;
            case DungeonProp.RelicTaken: Relic(p,true);break;
            case DungeonProp.LeverUp: Lever(p,false);break;
            case DungeonProp.LeverDown: Lever(p,true);break;
            case DungeonProp.Brazier: Brazier(p);break;
            case DungeonProp.Flame: Fire(p,36,77,17,24,variant,false);break;
            case DungeonProp.WardFlame: Fire(p,36,77,17,24,variant,true);break;
            case DungeonProp.Exit: Exit(p);break;
            case DungeonProp.Stave: Stave(p,false);break;
            case DungeonProp.StaveOpen: Stave(p,true);break;
            case DungeonProp.LockPlate: LockPlate(p);break;
            case DungeonProp.Portcullis: Portcullis(p,false);break;
            case DungeonProp.PortcullisRaised: Portcullis(p,true);break;
            case DungeonProp.Sconce: Sconce(p);break;
            case DungeonProp.TorchFlame: Fire(p,36,87,7,10,variant,false);break;
            case DungeonProp.Banner: Banner(p,variant);break;
            case DungeonProp.Rubble: Rubble(p,variant);break;
            case DungeonProp.Bones: Bones(p,variant);break;
            case DungeonProp.Pots: Pots(p,variant);break;
            case DungeonProp.Growth: Growth(p,variant);break;
            case DungeonProp.Debris: Debris(p,variant);break;
            case DungeonProp.Sacred: Sacred(p,variant);break;
            case DungeonProp.Mosaic: Mosaic(p,variant);break;
            case DungeonProp.Threshold: Threshold(p,variant);break;
            case DungeonProp.Records: Records(p,variant);break;
            case DungeonProp.Offering: Offering(p,variant);break;
            case DungeonProp.MossPatch: MossPatch(p,variant);break;
        }
        p.Contour(Flat(prop));
        return p.Data;
    }

    /// <summary>A dressed block lit from the upper-left: bright top edge, lit left arris,
    /// shaded right return and a dark foot.</summary>
    static void Box(Pixels p,int x,int y,int w,int h,byte[] r)
    {
        p.Rect(x,y,w,h,r[3]);p.Rect(x,y,w,1,r[5]);p.Rect(x,y+1,1,h-1,r[4]);
        p.Rect(x+w-2,y+1,2,h-1,r[2]);p.Rect(x,y+h-1,w,1,r[1]);
    }
    /// <summary>Moss and ivy over whatever stone or timber is already painted there.</summary>
    static void Overgrow(Pixels p,int x,int y,int w,int h,int slot,int density)
    {
        for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)
        {
            byte c=p.At(xx,yy);if(c==0||c==Ink||c==35||c==36)continue;
            uint n=Hash(xx/3,yy/2,slot),g=Hash(xx,yy,slot+1);
            if(n%100>=density)continue;
            p.Set(xx,yy,g%5==0?(byte)5:g%3==0?(byte)2:c is 23 or 24 or 53 or 16?(byte)4:(byte)3);
        }
    }
    static void Column(Pixels p,int theme,int v)
    {
        var r=Stone(theme);int top=v==2?76:54;
        p.Cast(41,99,15,3);
        Box(p,24,92,24,8,r);Box(p,26,88,20,4,r);
        // Fluted shaft: a highlight left of centre, falling to shade on the right.
        for(int y=v==2?top:top+8;y<88;y++)for(int x=28;x<44;x++)
        {
            int i=x-28;byte c=i<1?r[2]:i<3?r[4]:i<4?r[5]:i<9?r[3]:i<13?r[2]:r[1];
            if((i==6||i==10)&&y<86)c=i==6?r[2]:r[1];
            if(Hash(x/2,y/3,1)%19==0)c=c==r[5]?r[4]:c==r[3]?r[2]:c;
            p.Set(x,y,c);
        }
        if(v==2)
        {
            // Snapped: a ragged break, and the fallen drum beside the plinth.
            for(int x=28;x<44;x++){int jag=(int)(Hash(x/2,0,2)%5)+(x<33?3:0);p.Rect(x,top-jag,1,jag,x<32?r[4]:x<40?r[3]:r[1]);p.Set(x,top-jag,r[5]);}
            Box(p,47,91,11,9,r);p.Oval(52,91,5,2,r[4]);p.Oval(52,91,3,1,r[2]);p.Rect(18,97,4,3,r[3]);p.Rect(18,97,4,1,r[5]);
        }
        else
        {
            Box(p,26,top+4,20,4,r);Box(p,23,top-2,26,6,r);p.Rect(24,top+3,24,1,r[1]);
        }
        if(v==1)
        {
            p.Line(33,64,36,70,1,r[1]);p.Line(36,70,34,77,1,r[1]);p.Line(34,77,37,83,1,r[1]);
            p.Rect(44,top-2,5,3,0);p.Rect(46,top+1,3,2,0);p.Rect(50,98,3,2,r[3]);p.Rect(50,98,3,1,r[5]);
        }
        if(theme==1)
        {
            // Ivy winds up the shaft and settles on the plinth.
            for(int y=top+6;y<92;y++){int x=28+(y*3/2)%20;if(x<44){p.Over(x,y,3,2,(byte)(y%3==0?5:3),r);p.Over(x+1,y,1,1,4,r);}}
            Overgrow(p,24,86,24,14,3,45);if(v!=2)Overgrow(p,23,top-2,26,6,4,35);
        }
        if(theme==2)
        {
            p.Over(28,v==2?84:top+9,16,1,29,r);p.Over(28,85,16,1,28,r);p.Over(28,v==2?84:top+10,16,1,28,r);
            p.Over(25,95,22,1,29,r);if(v!=2)p.Over(24,top,24,1,29,r);
        }
    }
    static void Shelf(Pixels p,int theme,int v)
    {
        p.Cast(41,99,15,3);
        // Carcase: dark back, lit left upright, shaded right, moulded top.
        p.Rect(24,52,24,48,12);p.Rect(24,52,2,48,15);p.Rect(46,52,2,48,13);
        p.Rect(23,49,26,3,15);p.Rect(23,49,26,1,16);p.Rect(23,51,26,1,13);p.Rect(24,96,24,4,14);p.Rect(24,96,24,1,15);p.Rect(24,99,24,1,13);
        byte[] bindings={63,61,64,65,28,60,14,63};
        for(int shelf=0;shelf<3;shelf++)
        {
            int board=64+shelf*11;
            p.Rect(26,board,20,2,shelf==2&&v==2?(byte)0:(byte)15);if(!(shelf==2&&v==2))p.Rect(26,board+1,20,1,13);
            if(v==1&&shelf==1)
            {
                // Scroll rack and a sealed jar.
                for(int i=0;i<4;i++){p.Oval(29+i*3,board-2-(i%2)*3,1,1,65);p.Set(29+i*3,board-2-(i%2)*3,57);}
                p.Rect(40,board-7,4,7,67);p.Rect(40,board-7,1,7,68);p.Rect(41,board-8,2,1,66);continue;
            }
            if(v==2&&shelf==2)
            {
                // The lowest board has dropped; its books lie where they fell.
                p.Line(26,board-2,44,board+6,2,14);
                p.Rect(29,board+7,6,2,63);p.Rect(36,board+8,5,2,61);p.Rect(31,board+5,5,2,65);continue;
            }
            for(int x=27;x<45;)
            {
                uint h=Hash(x,shelf,10+v);int w=2+(int)(h%2),tall=6+(int)(h>>3)%4;
                if(h%7==0){x+=w;continue;}
                byte c=bindings[(h>>6)%bindings.Length];
                p.Rect(x,board-tall,w,tall,c);p.Rect(x,board-tall,1,tall,c==65?(byte)59:c==28?(byte)29:c);
                if((h>>9)%3==0)p.Rect(x,board-tall+2,w,1,29);
                x+=w+(int)((h>>11)%5==0?1:0);
            }
        }
        if(v==2){p.Rect(44,49,5,4,0);p.Rect(46,53,2,3,0);}
        if(theme==0){p.Line(26,53,31,53,1,18);p.Line(26,53,26,58,1,18);p.Line(26,56,29,53,1,18);p.Set(28,55,18);}
        if(theme==1){Overgrow(p,23,49,26,5,5,60);p.Line(46,52,42,66,1,3);p.Line(42,66,45,78,1,2);p.Rect(41,60,2,2,5);p.Rect(44,72,2,2,4);p.Rect(43,84,2,2,5);Overgrow(p,24,92,24,8,6,40);}
        if(theme==2){p.Rect(23,49,26,1,29);p.Rect(23,51,26,1,28);p.Rect(24,96,24,1,28);p.Rect(22,46,3,3,29);p.Rect(47,46,3,3,28);p.Set(22,46,30);}
    }
    static void Candle(Pixels p,int x,int y,int tall)
    {
        p.Rect(x,y-tall,2,tall,31);p.Rect(x+1,y-tall,1,tall,58);
        p.Rect(x,y-tall-3,2,3,46);p.Set(x,y-tall-4,47);p.Set(x,y-tall-1,45);
    }
    static void Altar(Pixels p,int theme,int v)
    {
        var r=Stone(theme);
        p.Cast(41,99,18,3);
        Box(p,26,84,20,16,r);Box(p,23,97,26,3,r);Box(p,22,78,28,7,r);
        p.Rect(31,88,10,8,r[2]);p.Rect(31,88,10,1,r[1]);p.Rect(31,88,1,8,r[1]);
        if(v==0)
        {
            // Runner laid across the slab, hanging down the front.
            p.Rect(30,78,12,2,62);p.Rect(30,80,12,13,61);p.Rect(30,80,1,13,62);p.Rect(41,80,1,13,60);p.Rect(30,92,12,1,29);
            p.Rect(35,83,2,6,29);p.Rect(33,85,6,2,29);p.Set(35,83,30);
            Candle(p,25,78,6);Candle(p,45,78,8);
        }
        if(v==1)
        {
            // Standing stele with a ward rune.
            Box(p,31,58,10,20,r);p.Rect(32,56,8,2,r[4]);p.Rect(33,55,6,1,r[5]);
            p.Rect(35,62,2,11,54);p.Rect(33,65,6,2,54);p.Set(35,62,55);p.Set(33,65,55);p.Rect(34,70,4,1,54);
            p.Rect(34,90,4,4,29);p.Set(34,90,30);
        }
        if(v==2)
        {
            // Offering bowl between two candles.
            p.Poly(41,29,71,43,71,40,78,32,78);p.Rect(29,71,14,1,43);p.Rect(31,69,10,2,29);p.Rect(33,68,5,1,30);p.Set(31,69,30);
            Candle(p,24,78,8);Candle(p,46,78,5);p.Rect(34,90,4,4,29);p.Set(34,90,30);
        }
        if(theme==1){Overgrow(p,22,78,28,4,7,55);Overgrow(p,23,90,26,10,8,45);}
        if(theme==2){p.Over(22,84,28,1,29,r);p.Over(26,98,20,1,28,r);}
    }
    static void Barrel(Pixels p,int x,int y,int w,int h)
    {
        // Staved, hooped and slightly bellied.
        for(int yy=0;yy<h;yy++)
        {
            int inset=yy<2||yy>=h-2?1:0;
            for(int xx=inset;xx<w-inset;xx++)
            {
                float across=xx/(float)(w-1);
                byte c=across<.14f?(byte)16:across<.45f?(byte)15:across<.78f?(byte)14:(byte)13;
                if(xx%4==3)c=13;
                p.Set(x+xx,y+yy,c);
            }
        }
        foreach(int band in new[]{3,h-5}){p.Rect(x,y+band,w,2,42);p.Rect(x,y+band,w/3,1,43);p.Rect(x+w-3,y+band,3,2,41);}
        p.Oval(x+w/2,y,w/2-1,2,15);p.Oval(x+w/2,y,w/2-3,1,13);p.Rect(x+2,y-1,w/2,1,16);
    }
    static void Crate(Pixels p,int x,int y,int w,int h)
    {
        p.Rect(x,y,w,h,14);p.Rect(x,y,w,2,16);p.Rect(x,y+h-2,w,2,13);p.Rect(x,y,2,h,15);p.Rect(x+w-2,y,2,h,13);
        p.Line(x+2,y+2,x+w-3,y+h-3,1,13);p.Line(x+2,y+h-3,x+w-3,y+2,1,15);
        p.Set(x+1,y+1,43);p.Set(x+w-2,y+1,42);p.Set(x+1,y+h-2,42);p.Set(x+w-2,y+h-2,41);
    }
    static void Urn(Pixels p,int x,int foot,int rx,int tall,bool gilt)
    {
        p.Blob(x,foot-tall/2f,rx,tall/2f,Clay8,40+x,0,.02f,true);
        p.Rect(x-rx/2,foot-tall-2,rx,3,67);p.Rect(x-rx/2-1,foot-tall-3,rx+2,1,68);p.Rect(x-rx/2,foot-tall-2,1,3,68);
        p.Rect(x-rx/2,foot-1,rx,1,66);
        if(gilt){p.Over(x-rx,foot-tall/2-1,rx*2+1,1,29,Clay8);p.Over(x-rx,foot-tall/2,rx*2+1,1,28,Clay8);}
    }
    static void Storage(Pixels p,int theme,int v)
    {
        p.Cast(41,99,16,3);
        if(theme==2)
        {
            // The sanctuaries keep their stores in tall sealed urns.
            if(v==0){Urn(p,36,100,9,22,true);}
            if(v==1){Urn(p,30,100,7,18,true);Urn(p,43,100,6,13,false);}
            if(v==2){Urn(p,31,100,6,14,false);Urn(p,42,100,8,24,true);p.Rect(40,72,5,2,60);p.Rect(40,72,5,1,61);}
            return;
        }
        if(v==0){Barrel(p,26,74,20,26);}
        if(v==1){Crate(p,24,82,22,18);Crate(p,29,68,15,14);p.Rect(46,92,3,8,13);}
        if(v==2)
        {
            Barrel(p,32,78,16,22);
            p.Blob(28,93,7,7,Sack8,31,0,.05f,true);p.Rect(27,85,3,2,13);p.Rect(26,84,5,1,57);
        }
        if(theme==1)Overgrow(p,22,66,28,34,9,22);
        else{p.Line(24,v==1?68:74,28,v==1?68:74,1,18);p.Set(26,v==1?70:76,18);}
    }
    static void Collapse(Pixels p,int theme,int v)
    {
        var r8=Stone8(theme);var r=Stone(theme);
        p.Cast(40,99,17,3);
        if(v==2){p.Line(23,95,47,76,3,14);p.Line(23,95,47,76,1,15);p.Line(25,97,49,78,1,13);}
        p.Blob(30,91,9,8,r8,51,0,.06f,true);p.Blob(43,93,8,6,r8,52,-.05f,.06f,true);p.Blob(37,85,7,6,r8,53,.03f,.05f,true);
        p.Blob(25,97,4,3,r8,54,0,.04f,true);p.Blob(48,97,4,3,r8,55,-.06f,.04f,true);
        if(v==1)
        {
            // A fallen drum of column lies across the heap.
            Box(p,39,80,12,9,r);p.Oval(39,84,2,4,r[4]);p.Oval(39,84,1,2,r[2]);p.Rect(43,81,1,7,r[2]);p.Rect(47,81,1,7,r[2]);
        }
        p.Rect(20,99,3,2,r[3]);p.Rect(20,99,3,1,r[5]);p.Rect(52,98,2,2,r[2]);
        if(theme==1)Overgrow(p,20,78,34,24,11,38);
        if(theme==2){p.Rect(33,94,2,1,29);p.Rect(44,90,1,1,29);}
    }
    static void Key(Pixels p)
    {
        // An old iron-bound key, standing on a worn stub so it reads from across a hall.
        p.Cast(38,100,9,2);
        Box(p,31,95,11,5,Slate);
        p.Oval(36,78,5,5,29);p.Oval(36,78,2,2,0);p.Rect(32,75,2,5,30);p.Rect(39,77,2,5,28);
        p.Rect(35,83,3,12,29);p.Rect(35,83,1,12,30);p.Rect(37,84,1,11,28);
        p.Rect(38,89,4,2,29);p.Rect(38,92,3,2,29);p.Rect(38,90,4,1,28);p.Rect(38,93,3,1,28);
        p.Set(29,71,30);p.Set(43,74,30);p.Set(28,82,29);p.Set(44,84,30);p.Rect(30,72,1,1,29);
    }
    static void Relic(Pixels p,bool taken)
    {
        p.Cast(40,100,17,3);
        Box(p,22,94,28,6,Slate);Box(p,25,88,22,6,Slate);Box(p,28,80,16,8,Slate);
        p.Rect(31,83,10,1,29);p.Rect(31,85,10,1,28);p.Rect(22,94,28,1,24);
        if(taken){p.Rect(33,79,6,2,19);p.Rect(34,79,4,1,37);return;}
        // The relic: a cut gem in a gold cradle, hanging in its own light.
        p.Rect(31,77,10,3,28);p.Rect(31,77,10,1,29);p.Rect(30,74,2,4,29);p.Rect(40,74,2,4,28);
        p.Poly(25,36,48,45,60,36,74,27,60);p.Poly(26,36,48,36,74,27,60);p.Poly(27,36,50,36,62,30,60);
        p.Line(36,48,36,73,1,26);p.Line(28,60,44,60,1,25);p.Set(33,56,56);p.Set(32,58,56);p.Set(34,54,56);
        p.Set(36,43,55);p.Set(24,56,55);p.Set(48,58,55);p.Set(27,47,56);p.Set(46,49,55);p.Set(22,64,54);p.Set(50,66,54);
    }
    static void Lever(Pixels p,bool pulled)
    {
        p.Cast(40,100,14,2);
        Box(p,25,90,22,10,Slate);Box(p,23,98,26,2,Slate);
        p.Rect(31,90,10,3,41);p.Rect(32,91,8,1,37);
        // The handle stands up before the pull and lies over after it.
        int tx=pulled?49:27,ty=pulled?80:70;
        p.Line(35,90,tx,ty,3,42);p.Line(35,90,tx,ty,1,43);
        p.Oval(tx+1,ty,3,3,pulled?(byte)28:(byte)29);p.Set(tx,ty-1,pulled?(byte)29:(byte)30);
        p.Rect(34,89,4,3,41);p.Set(35,89,43);
        if(!pulled){p.Rect(27,95,2,1,29);p.Rect(43,95,2,1,29);}
    }
    static void Brazier(Pixels p)
    {
        p.Cast(40,100,13,2);
        Box(p,31,84,10,16,Slate);Box(p,28,96,16,4,Slate);
        // Iron bowl on its stem, ash banked inside.
        p.Poly(41,23,76,49,76,43,85,29,85);p.Rect(23,75,26,2,42);p.Rect(23,75,12,1,43);p.Rect(25,78,2,4,42);
        p.Rect(26,73,20,2,69);p.Rect(28,72,14,1,57);p.Set(38,73,44);p.Set(31,73,44);
    }
    static void Fire(Pixels p,int cx,int foot,int width,int height,int frame,bool ward)
    {
        byte[] c=ward?new byte[]{54,54,55,56}:new byte[]{44,45,46,47};
        for(int layer=0;layer<4;layer++)
        {
            float half=width*(1-layer*.23f)/2,tall=height*(1-layer*.21f);
            for(int y=0;y<tall;y++)
            {
                float t=y/tall;
                float w=half*MathF.Sqrt(1-t)*(.6f+.4f*MathF.Sin(MathF.PI*MathF.Min(1,t*1.7f+.2f)));
                float sway=MathF.Sin(t*3.4f+frame*MathF.PI/2+layer*.5f)*t*width*.2f;
                // The body splits into two tongues near the tip on alternate frames.
                bool fork=layer==0&&t>.55f&&frame%2==1;
                for(int x=(int)MathF.Round(cx+sway-w);x<=(int)MathF.Round(cx+sway+w);x++)
                    if(!fork||Math.Abs(x-(cx+sway))>=.6f)p.Set(x,foot-y,c[layer]);
            }
        }
        int[] drift={-3,2,4,-1};
        p.Set(cx+drift[frame],foot-height-2-frame%2,c[2]);p.Set(cx-drift[(frame+2)%4],foot-height+2,c[1]);
        if(width>10)p.Set(cx+drift[(frame+1)%4]*2,foot-height-5+frame,c[1]);
    }
    static void Exit(Pixels p)
    {
        p.Cast(40,100,27,2);
        // Daylight falls down a stair through a dressed-stone arch.
        for(int y=44;y<100;y++)for(int x=22;x<50;x++)
        {
            float dx=(x-35.5f)/13f,dy=(y-60)/15f;if(y<60&&dx*dx+dy*dy>1)continue;
            float glow=1-MathF.Min(1,MathF.Sqrt(dx*dx*.7f+(y-58)*(y-58)/1500f));
            p.Set(x,y,glow>.72f?(byte)70:glow>.5f||((x+y)%2==0&&glow>.42f)?(byte)71:(byte)22);
        }
        for(int step=0;step<5;step++)
        {
            int y=96-step*6,inset=step*2;
            p.Rect(23+inset,y,26-inset*2,4,step<2?(byte)22:(byte)23);p.Rect(23+inset,y,26-inset*2,1,24);p.Rect(23+inset,y+3,26-inset*2,1,step<3?(byte)20:(byte)21);
        }
        p.Rect(12,56,11,45,21);p.Rect(49,56,11,45,21);p.Rect(12,56,3,45,23);p.Rect(57,56,3,45,20);p.Rect(20,56,3,45,20);p.Rect(49,56,2,45,20);
        for(int i=0;i<9;i++)
        {
            float a=MathF.PI*(i+.5f)/9;int x=(int)(36-MathF.Cos(a)*19),y=(int)(60-MathF.Sin(a)*20);
            p.Rect(x-4,y-4,8,8,i<4?(byte)22:(byte)21);p.Rect(x-4,y-4,8,1,i<5?(byte)24:(byte)22);p.Rect(x+3,y-3,1,7,20);
        }
        for(int y=64;y<98;y+=9){p.Rect(12,y,11,1,19);p.Rect(49,y,11,1,19);}
        p.Rect(32,36,8,8,23);p.Rect(32,36,8,1,24);p.Rect(35,38,2,4,29);p.Rect(34,39,4,2,29);
        p.Over(12,58,5,7,3,21,23);p.Over(13,60,3,3,4,3);p.Over(52,88,7,6,2,20,21);p.Over(25,41,9,3,4,22,24);
    }
    static void Stave(Pixels p,bool open)
    {
        // One iron stave in its stone socket; a gate is a row of them.
        Box(p,30,97,12,5,Slate);p.Rect(33,97,6,2,37);
        if(open){p.Rect(34,95,4,3,42);p.Rect(34,95,2,1,43);return;}
        p.Rect(34,66,4,32,42);p.Rect(34,66,1,32,43);p.Rect(37,67,1,31,41);
        p.Poly(42,36,58,40,66,32,66);p.Line(36,59,34,65,1,43);
        // Rails run the full cell so neighbouring staves join into one gate.
        foreach(int y in new[]{76,89}){p.Rect(24,y,24,2,42);p.Rect(24,y,24,1,43);}
        p.Rect(33,70,6,2,28);p.Rect(33,70,6,1,29);
    }
    static void LockPlate(Pixels p)
    {
        p.Rect(29,74,14,15,28);p.Rect(29,74,14,1,30);p.Rect(29,75,1,14,29);p.Rect(41,75,2,14,28);p.Rect(29,88,14,1,41);
        p.Rect(31,76,10,11,29);p.Oval(36,80,2,2,37);p.Rect(35,81,2,5,37);
        p.Set(30,75,30);p.Set(41,75,30);p.Set(30,87,28);p.Set(41,87,28);
    }
    static void Portcullis(Pixels p,bool raised)
    {
        // Iron grille on one gate cell; raised, only the teeth show under the lintel.
        Box(p,24,60,24,5,Slate);p.Rect(24,64,24,1,19);
        for(int bar=0;bar<4;bar++)
        {
            int x=26+bar*6;
            if(raised){p.Rect(x,65,3,6,42);p.Rect(x,65,1,6,43);p.Poly(42,x,71,x+3,71,x+1,75);continue;}
            p.Rect(x,65,3,35,42);p.Rect(x,65,1,35,43);p.Rect(x+2,66,1,34,41);p.Poly(42,x,99,x+3,99,x+1,102);
        }
        if(raised)return;
        foreach(int y in new[]{74,87}){p.Rect(24,y,24,3,42);p.Rect(24,y,24,1,43);p.Rect(24,y+2,24,1,41);for(int bar=0;bar<4;bar++)p.Set(27+bar*6,y+1,23);}
    }
    static void Sconce(Pixels p)
    {
        // Iron bracket and a pitch-bound brand; the flame is a separate animated module.
        p.Rect(33,93,6,2,42);p.Rect(33,93,6,1,43);p.Rect(35,95,2,4,41);p.Rect(32,91,8,2,41);p.Rect(32,91,8,1,42);
        p.Rect(35,86,2,6,14);p.Rect(35,86,1,6,15);p.Rect(34,85,4,2,12);
    }
    static void Banner(Pixels p,int v)
    {
        // Hung from an iron rod against the masonry: crimson and gold, or faded and torn.
        byte dark=v==0?(byte)60:v==1?(byte)63:(byte)2,mid=v==0?(byte)61:v==1?(byte)63:(byte)3,lit=v==0?(byte)62:v==1?(byte)64:(byte)4;
        p.Rect(29,86,14,1,42);p.Set(28,86,43);p.Set(43,86,41);
        for(int x=30;x<42;x++)
        {
            int tall=11-(Math.Abs(x-36)<2?0:Math.Abs(x-36)<4?1:2)-(v==2&&x%3==0?2:0);
            for(int y=0;y<tall;y++)p.Set(x,87+y,x<32?lit:x>39?dark:mid);
        }
        if(v!=2){p.Rect(35,89,2,5,29);p.Rect(34,90,4,2,29);p.Set(35,89,30);p.Rect(30,87,12,1,v==0?(byte)28:(byte)65);}
    }
    static void Rubble(Pixels p,int v)
    {
        p.Cast(38,100,v==3?6:9,2);
        if(v==0){p.Blob(34,96,5,4,Slate8,61,0,.05f,true);p.Blob(41,98,3,2,Slate8,62,0,.04f,true);}
        if(v==1){p.Blob(32,97,4,3,Slate8,63,0,.05f,true);p.Blob(39,95,5,4,Slate8,64,-.04f,.05f,true);p.Rect(44,99,2,1,22);}
        if(v==2){Box(p,31,94,8,6,Slate);p.Blob(42,97,3,3,Slate8,65,0,.04f,true);p.Rect(28,99,2,1,23);}
        if(v==3){p.Blob(36,97,4,3,Slate8,66,0,.05f,true);p.Rect(42,99,2,1,22);p.Rect(30,99,1,1,23);}
    }
    static void Skull(Pixels p,int x,int y)
    {
        p.Oval(x,y-4,3,3,58);p.Rect(x-2,y-2,5,2,58);p.Rect(x-3,y-6,3,2,59);p.Rect(x+2,y-4,2,3,57);
        p.Set(x-1,y-3,37);p.Set(x+1,y-3,37);p.Set(x,y-1,57);p.Rect(x-1,y,3,1,57);
    }
    static void Bones(Pixels p,int v)
    {
        p.Cast(38,100,9,2);
        if(v==0){Skull(p,36,99);p.Line(29,98,33,96,1,58);p.Set(29,97,59);}
        if(v==1)
        {
            // Rib cage half sunk in the dust.
            for(int i=0;i<4;i++){p.Line(31+i*3,99,32+i*3,93,1,58);p.Set(32+i*3,92,59);p.Set(33+i*3,93,57);}
            p.Line(30,96,44,96,1,57);
        }
        if(v==2){p.Line(29,99,37,95,1,58);p.Rect(28,98,2,2,59);p.Rect(37,94,2,2,59);p.Line(36,100,44,98,1,58);p.Rect(44,97,2,2,59);p.Set(41,95,57);}
        if(v==3){Skull(p,33,99);p.Line(37,99,45,96,1,58);p.Line(38,96,44,100,1,57);p.Rect(45,95,2,2,59);p.Set(40,94,58);}
    }
    static void Pots(Pixels p,int v)
    {
        p.Cast(38,100,9,2);
        if(v==0)Urn(p,36,100,5,10,false);
        if(v==1){Urn(p,32,100,4,8,false);Urn(p,41,100,3,6,false);}
        if(v==2)
        {
            // Broken: the foot still stands among its shards.
            p.Rect(32,96,8,4,67);p.Rect(32,96,1,4,68);p.Rect(38,97,2,3,66);p.Set(33,95,68);p.Set(36,94,67);p.Set(38,95,66);p.Rect(34,96,4,1,66);
            p.Rect(27,99,3,1,68);p.Rect(43,98,2,2,67);p.Set(46,100,66);p.Set(30,97,67);
        }
        if(v==3){Urn(p,36,100,4,13,true);p.Rect(30,99,2,1,67);}
    }
    static void Growth(Pixels p,int v)
    {
        if(v==0)
        {
            // Fern pushing up between the slabs.
            foreach(var (x,top,tilt) in new[]{(31,92,-4),(34,88,-2),(36,85,0),(38,88,2),(41,92,4)})
            {p.Line(36,99,x+tilt,top,1,3);p.Set(x+tilt,top,5);p.Set(x+tilt-Math.Sign(tilt),top+2,4);p.Set(x+tilt+1,top+3,2);}
        }
        if(v==1){p.Tuft(33,100,3);p.Tuft(39,100,4);p.Tuft(36,99,2);}
        if(v==2)
        {
            p.Cast(38,100,7,1);
            foreach(var (x,y,w) in new[]{(31,94,7),(41,96,5)})
            {p.Rect(x+w/2-1,y+1,2,100-y-1,31);p.Rect(x,y-1,w,2,33);p.Rect(x+1,y-2,w-2,1,33);p.Rect(x+1,y-2,1,1,31);p.Rect(x+w-2,y-1,1,1,31);}
        }
        if(v==3)
        {
            // Pale glowcaps: the only light some halls have.
            foreach(var (x,y,w) in new[]{(30,95,5),(37,92,7),(44,96,4)})
            {p.Rect(x+w/2,y+1,1,100-y-1,26);p.Rect(x,y-1,w,2,26);p.Rect(x+1,y-2,w-2,1,27);p.Set(x+1,y-1,27);p.Set(x+w-1,y,25);}
            p.Set(34,89,27);p.Set(42,88,26);
        }
    }
    static void Debris(Pixels p,int v)
    {
        p.Cast(38,100,10,2);
        if(v==0){p.Line(28,99,42,95,2,14);p.Line(28,99,42,95,1,15);p.Line(33,100,46,99,2,13);p.Set(46,98,15);}
        if(v==1)
        {
            // Tipped bucket.
            p.Rect(31,93,10,7,14);p.Rect(31,93,10,1,16);p.Rect(31,95,10,1,42);p.Rect(31,98,10,1,42);p.Oval(41,96,2,3,12);p.Rect(31,94,1,6,15);
        }
        if(v==2)
        {
            // Coil of chain.
            for(int i=0;i<7;i++){float a=i*.9f;int x=36+(int)(MathF.Cos(a)*6),y=97+(int)(MathF.Sin(a)*2);p.Rect(x,y,2,2,42);p.Set(x,y,43);}
            p.Line(43,97,48,100,1,42);
        }
        if(v==3){p.Rect(30,96,13,4,14);p.Rect(30,96,13,1,16);p.Rect(30,99,13,1,13);p.Rect(34,96,1,4,13);p.Rect(39,96,1,4,13);p.Line(43,95,47,99,1,14);}
    }
    static void Sacred(Pixels p,int v)
    {
        p.Cast(38,100,8,2);
        if(v==0){Candle(p,31,100,4);Candle(p,35,100,7);Candle(p,40,100,5);p.Rect(30,99,13,1,58);}
        if(v==1)
        {
            // Incense bowl on three feet.
            p.Poly(41,30,94,43,94,40,99,33,99);p.Rect(30,93,13,1,43);p.Rect(33,99,1,1,41);p.Rect(39,99,1,1,41);
            p.Rect(34,92,5,1,44);p.Set(36,89,18);p.Set(37,86,18);p.Set(35,83,18);
        }
        if(v==2){p.Oval(36,98,7,2,28);p.Oval(36,97,6,1,29);p.Rect(33,95,2,2,29);p.Rect(37,94,3,3,30);p.Set(35,94,26);p.Set(41,96,29);}
        if(v==3){Box(p,32,90,8,10,Sand);p.Rect(34,86,4,4,52);p.Rect(34,86,4,1,53);p.Set(35,88,37);p.Set(37,88,37);p.Rect(33,94,6,1,29);}
    }
    // Flat floor modules are centred on FlatPivot and fill at most one 24x12 tile.
    static void Mosaic(Pixels p,int theme)
    {
        byte dark=theme==2?(byte)49:(byte)20,line=theme==2?(byte)29:theme==1?(byte)4:(byte)23,fill=theme==2?(byte)51:(byte)21,gem=theme==2?(byte)61:theme==1?(byte)26:(byte)63;
        p.Rect(25,89,22,10,fill);p.Rect(25,89,22,1,line);p.Rect(25,98,22,1,dark);p.Rect(25,89,1,10,line);p.Rect(46,89,1,10,dark);
        p.Poly(dark,36,90,45,94,36,98,27,94);p.Poly(gem,36,92,41,94,36,96,31,94);p.Rect(35,93,2,2,line);
        foreach(var (x,y) in new[]{(27,90),(44,90),(27,97),(44,97)})p.Set(x,y,line);
    }
    static void Threshold(Pixels p,int theme)
    {
        // A worn runner of carved slabs; adjacent tiles join into one band.
        byte body=theme==2?(byte)61:theme==1?(byte)2:(byte)20,edge=theme==2?(byte)29:theme==1?(byte)4:(byte)22,dark=theme==2?(byte)60:theme==1?(byte)1:(byte)19;
        // Framed the same on every side, so a run reads as one band in either direction.
        p.Rect(24,89,24,10,dark);p.Rect(25,90,22,8,body);
        p.Poly(edge,36,91,41,94,36,97,31,94);p.Rect(35,93,2,2,body);p.Set(27,94,edge);p.Set(44,94,edge);
        if(theme!=2){p.Rect(29,96,3,1,dark);p.Rect(41,91,2,1,dark);}
    }
    static void Records(Pixels p,int v)
    {
        if(v==0){p.Rect(27,91,7,4,65);p.Rect(27,91,7,1,59);p.Rect(28,93,4,1,57);p.Rect(38,94,6,4,58);p.Rect(39,95,3,1,57);p.Rect(33,96,3,2,63);}
        else{p.Rect(29,92,8,2,65);p.Oval(29,93,1,1,59);p.Oval(37,93,1,1,57);p.Rect(39,95,5,3,61);p.Rect(39,95,5,1,62);p.Rect(40,96,1,1,29);p.Rect(27,96,5,2,58);p.Rect(28,97,3,1,57);}
    }
    static void Offering(Pixels p,int v)
    {
        if(v==0){p.Oval(34,94,4,2,28);p.Oval(34,93,3,1,29);p.Set(33,93,30);p.Rect(40,95,2,1,29);p.Set(43,93,29);p.Set(29,96,28);p.Rect(41,91,2,2,26);}
        else{p.Rect(31,92,4,3,29);p.Rect(31,92,4,1,30);p.Rect(38,94,3,2,58);p.Set(42,96,29);p.Set(30,97,29);p.Rect(36,91,1,2,31);p.Set(36,90,46);}
    }
    static void MossPatch(Pixels p,int v)
    {
        // Cushion moss with a few fronds, lying flat between the slabs.
        foreach(var (x,y,rx,ry) in v==0?new[]{(31,94,6,3),(40,95,5,2),(36,92,4,2)}:new[]{(33,95,7,2),(41,92,4,2)})
        {p.Oval(x,y,rx,ry,2);p.Oval(x-1,y-1,rx-2,Math.Max(1,ry-1),3);p.Set(x-2,y-1,5);p.Set(x+1,y,4);}
        p.Set(29,91,5);p.Set(43,96,4);p.Set(37,97,2);
    }
}

/// <summary>One fixed process-lifetime atlas shared by every ruin: props, features, wall
/// dressing and floor details.</summary>
public static class DungeonPropAtlas
{
    static ImageTexture? texture;
    public static int BuildCount {get;private set;}
    public static Texture2D Texture=>texture??=Build();
    static ImageTexture Build()
    {
        using var image=Image.CreateEmpty(DungeonPropArt.Columns*DungeonPropArt.Width,DungeonPropArt.Rows*DungeonPropArt.Height,false,Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        foreach(var prop in Enum.GetValues<DungeonProp>())for(int v=0;v<DungeonPropArt.Count(prop);v++)
        {
            var data=DungeonPropArt.Generate(prop,v);var rect=DungeonPropArt.Source(DungeonPropArt.Module(prop,v));
            for(int y=0;y<DungeonPropArt.Height;y++)for(int x=0;x<DungeonPropArt.Width;x++)
                if(data[y*DungeonPropArt.Width+x]!=0)image.SetPixel((int)rect.Position.X+x,(int)rect.Position.Y+y,OverworldPropArt.Palette[data[y*DungeonPropArt.Width+x]]);
        }
        BuildCount++;return ImageTexture.CreateFromImage(image);
    }
    public static void Draw(CanvasItem canvas,int module,Vector2 foot)
        =>canvas.DrawTextureRectRegion(Texture,new Rect2(foot-DungeonPropArt.Pivot,new Vector2(DungeonPropArt.Width,DungeonPropArt.Height)),DungeonPropArt.Source(module));
}
