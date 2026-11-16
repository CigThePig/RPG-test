using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
namespace Mosslight;

public enum TownProp
{
    Well, Stall, LampPost, Signpost, NoticeBoard, FenceRun, FenceSide, Gate, StoneWall, Cart, Hay, Bench,
    Barrels, Crates, Sacks, Planter, Woodpile, Fountain, Statue, Washing, Scarecrow, Garden, BannerPole,
    // Indoors, and on the houses themselves.
    Bed, Table, Hearth, Counter, Rug, Anvil, Pew, Shelf, TradeSign, Chimney
}

/// <summary>Fixed, procedurally painted town modules in the Living Reach's manner and palette:
/// street furniture and house furnishings in the standard 72x104 cell. Lit from the
/// upper-left, one dark contour, a cast shadow. Pure cosmetic recipes; no world RNG,
/// simulation state or external textures.</summary>
public static class TownArt
{
    public const int Revision=2,PropColumns=8;
    public const int Width=OverworldPropArt.Width,Height=OverworldPropArt.Height;
    public static readonly Vector2 Pivot=OverworldPropArt.Pivot,FlatPivot=new(36,94);
    static readonly int[] PropCounts={1,3,1,1,1,1,1,1,1,1,2,1, 1,1,1,2,1,1,1,1,1,2,1, 2,1,1,1,2,1,1,1,3,1};
    static readonly int[] PropOffsets=PropCounts.Select((_,i)=>PropCounts.Take(i).Sum()).ToArray();
    public static readonly int Props=PropCounts.Sum();
    public static readonly int PropRows=(Props+PropColumns-1)/PropColumns;
    public static int Count(TownProp p)=>PropCounts[(int)p];
    public static int Module(TownProp p,int variant=0)=>PropOffsets[(int)p]+(int)((uint)variant%(uint)PropCounts[(int)p]);
    public static Rect2 PropSource(int module)=>new(module%PropColumns*Width,module/PropColumns*Height,Width,Height);
    public static bool Flat(TownProp p)=>p is TownProp.Garden or TownProp.Rug;

    const byte Ink=37,Shadow=35,Penumbra=36;
    // Four-tone roof ramps, darkest first.
    static readonly byte[] Tile={72,73,74,75},Slate={76,77,78,79},Thatch={80,81,82,83},
        Stone={19,20,21,22,23,24},Leaf8={1,2,2,3,3,4,5,6},Stone8={19,20,20,21,21,22,23,24},Straw8={80,80,81,81,82,82,83,83};
    static uint Hash(int x,int y,int slot)=>EnvironmentStyle.Hash(Revision,x,y,21000+slot);

    // ---- street furniture -----------------------------------------------------------
    public static byte[] Generate(TownProp prop,int variant)
    {
        var p=new Pixels();int v=variant;
        switch(prop)
        {
            case TownProp.Well:
                p.Cast(40,100,17,3);
                for(int y=84;y<100;y++)for(int x=22;x<50;x++){int i=x-22;p.Set(x,y,(y-84)%5==4||(i+(y-84)/5*4)%8==7?(byte)19:i<3?(byte)23:i<18?(byte)21:(byte)20);}
                p.Oval(36,84,14,4,22);p.Oval(36,84,11,3,87);p.Oval(35,84,7,2,88);p.Rect(31,83,3,1,89);p.Rect(22,84,28,1,24);
                p.Rect(23,56,3,30,14);p.Rect(23,56,1,30,16);p.Rect(46,56,3,30,13);p.Rect(22,66,28,2,14);p.Rect(22,66,28,1,15);
                for(int k=0;k<10;k++)p.Rect(18+k*2,57-k,36-k*4,1,Tile[k%3==0?3:k%3==1?2:1]);
                p.Rect(18,58,36,1,Tile[0]);p.Rect(35,68,1,9,57);p.Rect(32,77,7,6,14);p.Rect(32,77,7,1,16);p.Rect(32,80,7,1,42);
                break;
            case TownProp.Stall:
            {
                byte dark=v==0?(byte)60:v==1?(byte)87:(byte)90,mid=v==0?(byte)61:v==1?(byte)88:(byte)91,lit=v==0?(byte)62:v==1?(byte)89:(byte)92;
                p.Cast(40,100,22,3);
                p.Rect(15,66,2,34,14);p.Rect(55,66,2,34,13);p.Rect(15,66,1,34,16);
                p.Rect(16,84,40,16,14);p.Rect(16,84,40,2,16);p.Rect(16,98,40,2,13);for(int b=22;b<56;b+=7)p.Rect(b,86,1,12,13);
                for(int row=0;row<13;row++)for(int x=11-row/3;x<61+row/3;x++)
                {
                    bool stripe=(x+40)/7%2==0;
                    p.Set(x,54+row,row==12?(stripe?dark:(byte)84):stripe?(row<3?lit:mid):(row<3?(byte)86:(byte)85));
                }
                for(int x=11;x<61;x+=7)p.Rect(x-4,67,3,2,(x/7)%2==0?mid:(byte)85);
                // Wares differ by trade: produce, cloth, pots.
                if(v==0)foreach(var (x,c) in new[]{(19,(byte)33),(24,(byte)33),(30,(byte)29),(35,(byte)4),(41,(byte)4),(47,(byte)67),(52,(byte)29)}){p.Rect(x,80,4,4,c);p.Set(x,80,86);p.Set(x+3,83,13);}
                if(v==1)foreach(var (x,c) in new[]{(19,(byte)61),(27,(byte)88),(35,(byte)82),(43,(byte)91),(50,(byte)85)}){p.Rect(x,79,6,5,c);p.Rect(x,79,6,1,86);p.Rect(x+5,80,1,4,13);}
                if(v==2)foreach(var (x,h) in new[]{(20,6),(27,8),(34,5),(41,9),(49,6)}){p.Rect(x,84-h,5,h,67);p.Rect(x,84-h,1,h,68);p.Rect(x+1,83-h,3,1,66);}
                break;
            }
            case TownProp.LampPost:
                p.Cast(38,100,6,2);
                p.Rect(35,62,2,38,41);p.Rect(35,62,1,38,42);p.Rect(33,97,6,3,41);p.Rect(33,97,6,1,43);p.Rect(34,60,4,2,42);
                p.Rect(32,50,8,10,41);p.Rect(33,51,6,8,93);p.Rect(33,51,6,2,47);p.Rect(33,57,6,2,45);p.Rect(36,51,1,8,41);
                p.Rect(31,48,10,2,42);p.Rect(33,46,6,2,41);p.Rect(35,44,2,2,43);
                break;
            case TownProp.Signpost:
                p.Cast(38,100,6,2);
                p.Rect(35,64,3,36,14);p.Rect(35,64,1,36,16);p.Rect(34,62,5,2,13);
                p.Poly(15,38,68,54,68,58,72,54,76,38,76);p.Rect(38,68,16,1,16);p.Rect(41,71,10,1,13);p.Rect(41,73,7,1,13);
                p.Poly(14,35,80,19,80,15,84,19,88,35,88);p.Rect(19,80,16,1,15);p.Rect(22,83,9,1,13);
                break;
            case TownProp.NoticeBoard:
                p.Cast(39,100,13,2);
                p.Rect(22,70,3,30,14);p.Rect(47,70,3,30,13);p.Rect(22,70,1,30,16);
                p.Rect(20,66,32,22,13);p.Rect(22,68,28,18,15);p.Rect(22,68,28,1,16);
                for(int k=0;k<5;k++)p.Rect(19+k,65-k,34-k*2,1,Tile[k%2==0?2:1]);
                foreach(var (x,y,w,h) in new[]{(24,70,8,7),(34,71,6,9),(42,70,6,6),(26,79,7,5),(41,78,7,6)}){p.Rect(x,y,w,h,65);p.Rect(x,y,w,1,59);p.Rect(x+1,y+2,w-2,1,57);if(h>6)p.Rect(x+1,y+4,w-3,1,57);p.Set(x+w/2,y,33);}
                break;
            case TownProp.FenceRun:
                // One tile of fence along a row; neighbours join into a run.
                p.Rect(24,91,24,2,15);p.Rect(24,91,24,1,16);p.Rect(24,96,24,2,14);p.Rect(24,97,24,1,13);
                foreach(int x in new[]{25,35,45}){p.Rect(x,86,3,15,14);p.Rect(x,86,1,15,16);p.Rect(x+2,87,1,14,13);p.Set(x+1,85,15);}
                break;
            case TownProp.FenceSide:
                // One tile of fence running away from the viewer.
                for(int y=89;y<=101;y+=6){p.Rect(35,y-12,3,13,14);p.Rect(35,y-12,1,13,16);p.Rect(37,y-11,1,12,13);p.Set(36,y-13,15);}
                p.Rect(36,80,1,20,15);p.Rect(37,84,1,16,13);
                break;
            case TownProp.Gate:
                p.Rect(24,84,3,17,14);p.Rect(24,84,1,17,16);p.Rect(45,84,3,17,13);p.Rect(45,84,1,17,15);p.Set(25,83,16);p.Set(46,83,15);
                p.Rect(27,88,18,2,15);p.Rect(27,97,18,2,14);p.Line(27,97,44,89,1,13);for(int x=30;x<45;x+=5)p.Rect(x,88,2,11,14);p.Rect(43,92,2,2,42);
                break;
            case TownProp.StoneWall:
                for(int y=90;y<101;y++)for(int x=24;x<48;x++){int course=(y-90)/4,l=(x+course*4)%8;p.Set(x,y,(y-90)%4==3||l==7?(byte)19:y==90?(byte)23:Hash(x/8+course,course,5)%3==0?(byte)22:(byte)21);}
                p.Rect(24,88,24,2,23);p.Rect(24,88,24,1,24);p.Over(26,88,6,2,4,23,24);p.Over(39,90,5,3,3,21,22);
                break;
            case TownProp.Cart:
                p.Cast(40,100,22,3);
                p.Rect(18,80,34,10,14);p.Rect(18,80,34,1,16);p.Rect(18,89,34,1,12);for(int x=24;x<52;x+=7)p.Rect(x,81,1,8,13);
                p.Rect(16,76,2,14,14);p.Rect(52,76,2,14,13);p.Line(54,88,64,94,2,14);
                p.Blob(35,76,15,7,Straw8,7,0,.1f);
                foreach(int wx in new[]{26,46}){p.Oval(wx,93,6,6,13);p.Oval(wx,93,4,4,15);p.Oval(wx,93,1,1,42);p.Rect(wx-4,93,9,1,13);p.Rect(wx,89,1,9,13);}
                break;
            case TownProp.Hay:
                p.Cast(39,100,v==0?13:17,3);
                if(v==0){p.Rect(25,86,22,14,82);p.Rect(25,86,22,2,83);p.Rect(44,88,3,12,81);p.Rect(25,98,22,2,80);for(int x=28;x<45;x+=3)p.Rect(x,89,1,8,81);p.Rect(25,90,22,1,13);p.Rect(25,95,22,1,13);}
                else{p.Blob(36,86,16,14,Straw8,8,0,.09f);p.Rect(35,68,2,6,14);for(int i=0;i<9;i++)p.Set(24+(int)(Hash(i,0,9)%24),78+(int)(Hash(i,1,9)%18),83);}
                break;
            case TownProp.Bench:
                p.Cast(39,100,14,2);
                p.Rect(22,92,28,3,15);p.Rect(22,92,28,1,16);p.Rect(22,94,28,1,13);p.Rect(24,95,2,6,13);p.Rect(46,95,2,6,13);
                p.Rect(22,84,28,2,15);p.Rect(22,87,28,2,14);p.Rect(22,84,2,9,14);p.Rect(48,84,2,9,13);
                break;
            case TownProp.Barrels:
                p.Cast(40,100,15,3);
                foreach(var (x,y,w,h) in new[]{(24,82,13,18),(38,86,12,14)})
                {
                    for(int yy=0;yy<h;yy++)for(int xx=yy<2||yy>=h-2?1:0;xx<w-(yy<2||yy>=h-2?1:0);xx++){float a=xx/(float)(w-1);p.Set(x+xx,y+yy,xx%4==3?(byte)13:a<.2f?(byte)16:a<.5f?(byte)15:a<.8f?(byte)14:(byte)13);}
                    p.Rect(x,y+3,w,2,42);p.Rect(x,y+3,w/3,1,43);p.Rect(x,y+h-5,w,2,42);p.Oval(x+w/2,y,w/2-1,2,15);p.Oval(x+w/2,y,w/2-3,1,13);
                }
                break;
            case TownProp.Crates:
                p.Cast(40,100,15,3);
                foreach(var (x,y,w,h) in new[]{(23,84,16,16),(40,88,12,12),(28,72,13,12)})
                {p.Rect(x,y,w,h,14);p.Rect(x,y,w,2,16);p.Rect(x,y+h-2,w,2,13);p.Rect(x,y,2,h,15);p.Rect(x+w-2,y,2,h,13);p.Line(x+2,y+2,x+w-3,y+h-3,1,13);p.Set(x+1,y+1,43);p.Set(x+w-2,y+h-2,41);}
                break;
            case TownProp.Sacks:
                p.Cast(39,100,13,2);
                p.Blob(30,93,7,7,new byte[]{13,57,57,58,58,65,65,59},11,0,.05f,true);p.Blob(42,94,6,6,new byte[]{13,57,57,58,58,65,65,59},12,-.05f,.05f,true);
                p.Blob(36,85,6,5,new byte[]{13,57,57,58,58,65,65,59},13,.04f,.05f,true);p.Rect(29,85,3,2,14);p.Rect(41,87,3,2,14);p.Rect(35,79,3,2,14);p.Line(27,92,33,95,1,57);p.Line(40,93,45,96,1,57);
                break;
            case TownProp.Planter:
                p.Cast(38,100,10,2);
                if(v==0){p.Rect(27,91,18,9,14);p.Rect(27,91,18,1,16);p.Rect(27,94,18,1,42);p.Rect(43,92,2,8,13);}
                else{p.Poly(67,26,90,46,90,43,100,29,100);p.Rect(26,89,20,2,68);p.Rect(42,91,3,9,66);}
                p.Blob(36,85,10,6,Leaf8,14+v,-.05f);
                foreach(var (x,y) in new[]{(29,84),(33,80),(38,82),(42,85),(36,86)}){p.Rect(x,y,2,2,v==0?(byte)33:(byte)34);p.Set(x,y,v==0?(byte)32:(byte)31);}
                break;
            case TownProp.Woodpile:
                p.Cast(39,100,14,2);
                for(int row=0;row<3;row++)for(int i=0;i<4-row;i++){int x=26+i*7+row*3,y=97-row*6;p.Oval(x,y,3,3,13);p.Oval(x,y,2,2,15);p.Set(x,y,14);p.Set(x-1,y-1,16);}
                p.Rect(22,84,2,16,13);p.Rect(49,84,2,16,13);p.Line(52,100,58,90,2,14);p.Rect(56,88,5,3,42);p.Rect(56,88,5,1,43);
                break;
            case TownProp.Fountain:
                p.Cast(39,101,26,3);
                p.Oval(36,93,26,8,21);p.Oval(36,92,26,7,23);p.Oval(36,92,23,6,87);p.Oval(35,92,18,4,88);
                for(int x=11;x<62;x++)if(p.At(x,97)!=0||p.At(x,99)!=0)for(int y=95;y<101;y++)if(p.At(x,y)==21)p.Set(x,y,(x/6)%2==0?(byte)21:(byte)20);
                p.Rect(32,72,8,20,22);p.Rect(32,72,2,20,24);p.Rect(38,72,2,20,20);p.Oval(36,72,10,3,22);p.Oval(36,71,10,2,24);p.Oval(36,71,7,1,88);
                p.Rect(34,60,4,11,22);p.Rect(34,60,1,11,24);p.Oval(36,60,4,2,23);
                foreach(var (x,y) in new[]{(36,56),(33,58),(39,58),(31,62),(41,62),(28,76),(44,76),(26,82),(46,82)})p.Set(x,y,89);
                p.Rect(22,90,5,1,89);p.Rect(44,93,6,1,89);p.Rect(30,95,4,1,89);
                break;
            case TownProp.Statue:
                p.Cast(39,100,13,2);
                p.Rect(26,90,20,10,21);p.Rect(26,90,20,1,24);p.Rect(26,90,2,10,23);p.Rect(43,90,3,10,20);p.Rect(24,98,24,2,20);p.Rect(31,93,10,3,29);
                // Cloaked warden with a raised lantern.
                p.Poly(22,30,90,31,66,35,62,40,66,42,90);p.Rect(30,70,3,20,23);p.Rect(39,68,3,22,20);p.Oval(36,60,3,4,22);p.Rect(34,57,2,3,24);
                p.Line(40,68,47,58,2,21);p.Rect(46,52,4,5,22);p.Rect(47,53,2,3,93);p.Over(30,80,6,10,4,22,23);
                break;
            case TownProp.Washing:
                p.Cast(39,100,20,2);
                p.Rect(16,70,2,30,14);p.Rect(54,70,2,30,13);p.Rect(16,70,1,30,16);
                for(int x=18;x<54;x++)p.Set(x,73+(int)MathF.Round(3*MathF.Sin((x-18)/36f*MathF.PI)),57);
                foreach(var (x,w,h,c,l) in new[]{(21,7,10,(byte)85,(byte)86),(30,5,8,(byte)61,(byte)62),(37,8,11,(byte)88,(byte)89),(47,5,7,(byte)85,(byte)86)})
                {int top=74+(int)MathF.Round(3*MathF.Sin((x-18)/36f*MathF.PI));p.Rect(x,top,w,h,c);p.Rect(x,top,1,h,l);p.Rect(x+w-1,top+1,1,h-1,84);p.Set(x,top-1,13);p.Set(x+w-1,top-1,13);}
                break;
            case TownProp.Scarecrow:
                p.Cast(38,100,8,2);
                p.Rect(35,70,2,30,14);p.Rect(24,76,24,2,14);p.Rect(24,76,24,1,15);
                p.Poly(61,29,76,43,76,41,90,31,90);p.Rect(29,76,2,10,62);p.Rect(31,88,10,2,60);p.Rect(22,77,3,3,82);p.Rect(47,77,3,3,82);
                p.Oval(36,69,4,4,65);p.Set(34,68,37);p.Set(37,68,37);p.Rect(34,71,4,1,13);p.Poly(81,29,66,43,66,39,60,33,60);p.Rect(28,66,16,1,80);
                p.Rect(32,90,2,4,82);p.Rect(38,90,2,5,82);
                break;
            case TownProp.Garden:
                // Flat: tilled rows (0) or a flower bed (1) filling one tile.
                p.Rect(24,89,24,10,13);
                for(int row=0;row<3;row++)
                {
                    int y=90+row*3;p.Rect(25,y,22,2,14);p.Rect(25,y,22,1,15);
                    for(int x=26;x<46;x+=4){int j=(int)(Hash(x,row,15+v)%2);p.Set(x+j,y-1,v==0?(byte)4:(byte)3);p.Set(x+j,y,v==0?(byte)5:row%2==0?(byte)33:(byte)34);if(v==0)p.Set(x+j+1,y,3);else p.Set(x+j+1,y-1,(byte)(x%8==2?32:31));}
                }
                break;
            case TownProp.Bed:
            {
                byte dark=v==0?(byte)60:(byte)87,mid=v==0?(byte)61:(byte)88,lit=v==0?(byte)62:(byte)89;
                p.Cast(39,100,15,2);
                p.Rect(22,84,28,14,14);p.Rect(22,84,28,1,16);p.Rect(22,97,28,2,13);p.Rect(22,78,3,22,14);p.Rect(22,78,1,22,16);p.Rect(47,86,3,14,13);
                p.Rect(25,82,22,13,mid);p.Rect(25,82,22,2,lit);p.Rect(44,84,3,11,dark);p.Rect(25,90,22,1,dark);
                p.Rect(26,79,9,5,86);p.Rect(26,79,9,1,31);p.Rect(33,81,2,3,84);
                break;
            }
            case TownProp.Table:
                p.Cast(39,100,16,3);
                p.Rect(22,86,28,4,15);p.Rect(22,86,28,1,16);p.Rect(22,89,28,1,13);p.Rect(24,90,2,10,13);p.Rect(46,90,2,10,13);p.Rect(24,90,1,10,14);
                p.Rect(27,82,4,4,67);p.Rect(27,82,1,4,68);p.Rect(34,83,6,2,65);p.Rect(34,83,6,1,59);p.Rect(42,81,2,5,31);p.Rect(42,79,2,2,46);p.Set(42,78,47);
                // A stool at each end.
                foreach(int sx in new[]{14,52}){p.Rect(sx,92,7,2,15);p.Rect(sx,92,7,1,16);p.Rect(sx+1,94,1,6,13);p.Rect(sx+5,94,1,6,13);}
                break;
            case TownProp.Hearth:
                p.Cast(39,100,16,3);
                for(int y=70;y<100;y++)for(int x=22;x<50;x++){int course=(y-70)/5,l=(x+course*5)%10;p.Set(x,y,(y-70)%5==4||l==9?(byte)19:x<24?(byte)23:x>46?(byte)20:Hash(x/10+course,course,31)%3==0?(byte)22:(byte)21);}
                p.Rect(20,68,32,3,22);p.Rect(20,68,32,1,24);p.Rect(27,80,18,20,37);p.Rect(26,79,20,2,20);
                p.Rect(29,96,14,3,13);p.Rect(30,95,12,1,14);
                p.Rect(31,89,10,7,44);p.Rect(32,87,8,8,45);p.Rect(34,86,4,8,46);p.Rect(35,89,2,5,47);p.Set(33,84,45);p.Set(38,83,46);
                p.Rect(24,64,4,4,67);p.Rect(44,63,3,5,29);p.Rect(33,65,6,3,65);
                break;
            case TownProp.Counter:
                p.Cast(39,100,18,2);
                p.Rect(18,86,36,14,14);p.Rect(18,84,36,3,15);p.Rect(18,84,36,1,16);p.Rect(18,98,36,2,13);p.Rect(51,87,3,13,13);
                for(int x=24;x<52;x+=7)p.Rect(x,88,1,10,13);
                p.Rect(21,79,5,5,67);p.Rect(21,79,1,5,68);p.Rect(29,80,4,4,29);p.Set(29,80,30);p.Rect(36,81,7,3,65);p.Rect(36,81,7,1,59);p.Rect(46,78,3,6,88);p.Rect(46,78,1,6,89);
                break;
            case TownProp.Rug:
            {
                // Flat: a woven rug filling one tile; neighbours join.
                byte field=v==0?(byte)61:(byte)88,edge=v==0?(byte)29:(byte)85,dark=v==0?(byte)60:(byte)87;
                p.Rect(24,89,24,10,dark);p.Rect(25,90,22,8,field);p.Rect(25,90,22,1,edge);p.Rect(25,97,22,1,edge);
                p.Poly(edge,36,91,41,94,36,97,31,94);p.Rect(35,93,2,2,field);p.Set(27,94,edge);p.Set(44,94,edge);
                break;
            }
            case TownProp.Anvil:
                p.Cast(39,100,12,2);
                p.Rect(30,90,12,10,14);p.Rect(30,90,12,1,16);p.Rect(40,91,2,9,13);p.Rect(30,93,12,1,42);
                p.Rect(26,82,20,4,42);p.Rect(26,82,20,1,43);p.Rect(22,83,5,2,43);p.Rect(31,86,10,4,41);p.Rect(31,86,2,4,42);
                p.Line(44,80,50,74,1,14);p.Rect(49,71,4,3,42);p.Set(49,71,43);
                break;
            case TownProp.Pew:
                p.Cast(39,100,16,2);
                p.Rect(20,92,32,3,15);p.Rect(20,92,32,1,16);p.Rect(20,95,2,5,13);p.Rect(50,95,2,5,13);
                p.Rect(20,82,32,3,15);p.Rect(20,82,32,1,16);p.Rect(20,86,32,2,14);p.Rect(20,80,2,14,14);p.Rect(50,80,2,14,13);p.Rect(20,80,1,14,16);
                break;
            case TownProp.Shelf:
                // The archive's bookcase, in its gilded trim.
                Array.Copy(DungeonPropArt.Generate(DungeonProp.Shelf,6),p.Data,p.Data.Length);return p.Data;
            case TownProp.TradeSign:
            {
                // A painted board on a post: tankard, hammer and anvil, or goods.
                byte field=v==0?(byte)61:v==1?(byte)20:(byte)90;
                p.Cast(38,100,6,2);
                p.Rect(35,60,3,40,14);p.Rect(35,60,1,40,16);p.Rect(34,58,5,2,13);p.Rect(38,64,14,2,41);p.Rect(38,64,14,1,42);
                p.Rect(41,66,1,3,41);p.Rect(49,66,1,3,41);p.Rect(39,69,13,12,14);p.Rect(39,69,13,1,16);p.Rect(40,70,11,10,field);p.Rect(40,70,11,1,86);
                if(v==0){p.Rect(42,73,5,5,29);p.Rect(42,73,5,1,31);p.Rect(47,74,2,3,29);}
                if(v==1){p.Rect(42,72,1,6,43);p.Rect(41,72,3,2,42);p.Rect(45,75,4,2,43);p.Rect(46,77,2,1,42);}
                if(v==2){p.Oval(45,75,3,3,29);p.Set(44,74,30);p.Rect(44,71,2,1,3);}
                break;
            }
            case TownProp.Chimney:
                // Stands on a roof: no ground shadow.
                p.Rect(31,84,10,16,21);p.Rect(31,84,2,16,23);p.Rect(38,84,3,16,20);for(int y=89;y<100;y+=5)p.Rect(31,y,10,1,20);
                p.Rect(30,82,12,3,22);p.Rect(30,82,12,1,24);p.Rect(33,80,6,2,19);
                break;
            case TownProp.BannerPole:
                p.Cast(38,100,6,2);
                p.Rect(35,44,2,56,14);p.Rect(35,44,1,56,16);p.Rect(33,97,6,3,21);p.Rect(33,97,6,1,23);p.Oval(36,43,2,2,29);
                p.Poly(61,37,47,53,49,50,56,53,63,37,65);p.Poly(62,37,47,47,48,45,55,37,57);p.Rect(41,53,5,5,29);p.Set(41,53,30);
                break;
        }
        p.Contour(Flat(prop));return p.Data;
    }
}

/// <summary>One fixed process-lifetime atlas of street furniture and furnishings.</summary>
public static class TownAtlas
{
    static ImageTexture? props;
    public static int BuildCount {get;private set;}
    public static Texture2D Props=>props??=Build();
    static ImageTexture Build()
    {
        int w=TownArt.Width,h=TownArt.Height;
        using var image=Image.CreateEmpty(TownArt.PropColumns*w,TownArt.PropRows*h,false,Image.Format.Rgba8);image.Fill(Colors.Transparent);
        foreach(var p in Enum.GetValues<TownProp>())for(int v=0;v<TownArt.Count(p);v++)
        {
            var data=TownArt.Generate(p,v);var rect=TownArt.PropSource(TownArt.Module(p,v));
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(data[y*w+x]!=0)image.SetPixel((int)rect.Position.X+x,(int)rect.Position.Y+y,OverworldPropArt.Palette[data[y*w+x]]);
        }
        BuildCount++;return ImageTexture.CreateFromImage(image);
    }
}
