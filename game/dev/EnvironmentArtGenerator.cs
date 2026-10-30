using Godot;
namespace Mosslight;
public readonly record struct EnvironmentMark(Rect2 Rect, Color Color);
/// <summary>Bounded reusable geometry modules, generated once. No per-cell textures.</summary>
public static class EnvironmentArtGenerator
{
    public const int Variants = 16;
    static readonly EnvironmentMark[][] floors = Enumerable.Range(0, Variants).Select(Floor).ToArray();
    public static IReadOnlyList<EnvironmentMark> FloorModule(int variant) => floors[variant & 15];
    public static int CachedModuleCount => floors.Length;
    public static int CachedMarkCount => floors.Sum(f => f.Length);
    static EnvironmentMark[] Floor(int variant)
    {
        var marks = new List<EnvironmentMark>();
        void R(float x,float y,float w,float h,Color c) => marks.Add(new(new Rect2(x,y,w,h),c));
        // Every module shares the same outer mortar; staggered interior joins never leave gaps.
        R(0,0,24,12,EnvironmentStyle.Mortar.Lerp(EnvironmentStyle.Floor,.55f));
        for(int row=0;row<2;row++)
        {
            int split=row==0?15:8;
            foreach(var span in new[]{(1,split-1),(split+1,23-split)})
            {
                uint h=EnvironmentStyle.Hash(variant,span.Item1,row);
                var c=EnvironmentStyle.Floor.Lightened((h%7)*.012f);
                R(span.Item1,row*6+1,span.Item2,5,c);
                R(span.Item1,row*6+1,span.Item2,1,c.Lightened(.055f));
                if(h%3==0)R(span.Item1+2,row*6+3,3,1,c.Darkened(.10f));
            }
        }
        if(variant%3==0){R(5,2,1,2,EnvironmentStyle.Shade);R(6,4,2,1,EnvironmentStyle.Shade);}
        if(variant%5==0){R(19,9,3,1,EnvironmentStyle.Moss);R(21,8,2,1,EnvironmentStyle.Moss);}
        return marks.ToArray();
    }
    public static void Floor(TerrainBatch batch,Vector2 p,int seed,int x,int y)
    {
        foreach(var mark in FloorModule((int)EnvironmentStyle.Hash(seed,x,y)))
            batch.Rect(new Rect2(p+mark.Rect.Position,mark.Rect.Size),mark.Color);
    }
    public static EnvironmentMark[] WallModule(int variant, bool leftEnd, bool rightEnd)
    {
        var marks=new List<EnvironmentMark>();
        void R(Rect2 rect,Color color)=>marks.Add(new(rect,color));
        var p=new Vector2(0,TerrainWallRow.Rise);int seed=variant,x=0,y=0;
        float rise=TerrainWallRow.Rise;
        R(new Rect2(p+new Vector2(0,-rise),new Vector2(24,12)),EnvironmentStyle.Cap);
        R(new Rect2(p+new Vector2(0,12-rise),new Vector2(24,rise)),EnvironmentStyle.Mortar);
        // Three staggered courses. Mortar, upper-left bevel and lower shadow give real volume.
        for(int course=0;course<3;course++)
        {
            int offset=course%2==0?0:-6;
            for(int start=offset;start<24;start+=12)
            {
                int a=Math.Max(0,start),z=Math.Min(24,start+11);
                uint h=EnvironmentStyle.Hash(seed,x*24+start,y,course);
                var c=EnvironmentStyle.Stone.Darkened(.05f+(h%5)*.018f);
                float top=12-rise+course*5;
                R(new Rect2(p+new Vector2(a,top),new Vector2(z-a,4)),c);
                R(new Rect2(p+new Vector2(a,top),new Vector2(z-a,1)),c.Lightened(.12f));
                if(h%9==0)R(new Rect2(p+new Vector2(a+2,top+1),new Vector2(1,2)),EnvironmentStyle.Shade);
            }
        }
        R(new Rect2(p+new Vector2(0,-rise),new Vector2(24,1)),EnvironmentStyle.Highlight);
        R(new Rect2(p+new Vector2(0,10-rise),new Vector2(24,2)),EnvironmentStyle.Shade);
        R(new Rect2(p+new Vector2(1,2-rise),new Vector2(21,7)),EnvironmentStyle.Cap.Darkened(.035f));
        R(new Rect2(p+new Vector2(11,2-rise),new Vector2(1,7)),EnvironmentStyle.Stone);
        uint wear=EnvironmentStyle.Hash(variant,7,11);
        if(wear%3==0){R(new Rect2(p+new Vector2(5,-rise+3),new Vector2(1,3)),EnvironmentStyle.Stone);R(new Rect2(p+new Vector2(6,-rise+6),new Vector2(3,1)),EnvironmentStyle.Stone);}
        if(wear%4==0)R(new Rect2(p+new Vector2(16,-rise+5),new Vector2(3,1)),EnvironmentStyle.Cap.Darkened(.12f));
        if(leftEnd)R(new Rect2(p+new Vector2(0,-rise+1),new Vector2(2,rise+10)),EnvironmentStyle.Highlight.Darkened(.12f));
        if(rightEnd)R(new Rect2(p+new Vector2(22,-rise+1),new Vector2(2,rise+10)),EnvironmentStyle.Shade);
        if(EnvironmentStyle.Hash(seed,x,y,7)%5==0)
            R(new Rect2(p+new Vector2(3,9),new Vector2(5,2)),EnvironmentStyle.Moss);
        return marks.ToArray();
    }

    // 26x64 cells, one transparent pixel around each 24-wide footprint.
    // All recipes use upper-left light and the same stone/wood ramps as Part 2.
    public static EnvironmentMark[] PropModule(int asset,int variant,string theme)
    {
        var marks=new List<EnvironmentMark>();
        var tint=EnvironmentThemes.Tint(theme);var accent=EnvironmentThemes.Accent(theme);
        void R(int x,int y,int w,int h,Color c)=>marks.Add(new(new Rect2(x,y,w,h),c));
        var stone=EnvironmentStyle.Stone*tint;var cap=EnvironmentStyle.Cap*tint;var shade=EnvironmentStyle.Shade*tint;
        var wood=new Color("786249");var ink=EnvironmentStyle.Mortar;
        R(1,54,24,8,shade);R(1,54,24,1,cap);R(2,61,22,1,ink);
        if(asset==0) // fluted column with carved capital and base
        {
            R(5,14,16,41,stone);R(5,14,3,40,cap);R(18,14,3,41,shade);
            for(int x=9;x<=15;x+=3)R(x,18,1,33,shade);
            R(2,8,22,8,stone);R(2,8,22,2,EnvironmentStyle.Highlight*tint);R(20,10,4,6,shade);
            R(1,5,24,4,cap);R(3,3,20,2,EnvironmentStyle.Highlight*tint);
            R(3,50,20,5,cap);R(4,52,18,1,accent);
            if(variant==1){R(13,27,2,6,shade);R(15,32,3,1,shade);}
            if(variant==2){R(5,17,8,2,accent);R(5,19,2,5,accent);}
        }
        if(asset==1) // stone-backed shelving and records; never resembles a loot chest
        {
            R(1,21,24,34,wood.Darkened(.35f));R(1,19,24,3,cap);
            R(1,22,3,33,wood.Lightened(.2f));R(22,22,3,33,wood.Darkened(.2f));
            for(int row=0;row<2;row++)
            {
                int y=25+row*14;R(4,y+10,18,3,wood.Lightened(.15f));
                for(int book=0;book<6;book++)
                {int h=6+(book+variant)%4;R(5+book*3,y+10-h,2,h,new[]{new Color("8d8b68"),new Color("788a82"),new Color("9b7654") }[(book+variant)%3]);}
            }
            if(variant==2)R(11,36,8,6,ink);
        }
        if(asset==2) // memorial altar, distinct from interactive relic pedestal
        {
            R(5,32,16,22,stone);R(18,32,3,22,shade);R(6,34,2,18,cap);
            R(1,28,24,6,cap);R(1,28,24,1,EnvironmentStyle.Highlight*tint);
            R(10,17,6,11,stone);R(8,19,10,2,accent);R(12,17,2,9,accent);
            R(9,41,8,7,shade);R(11,42,4,1,accent);
        }
        if(asset==3) // open barrel / broken supply racks, no chest clasp or lid
        {
            R(3,32,20,24,wood);R(20,34,3,22,wood.Darkened(.3f));R(3,32,3,23,wood.Lightened(.2f));
            for(int x=8;x<20;x+=5)R(x,34,1,21,wood.Darkened(.3f));
            R(2,36,22,3,shade);R(2,49,22,3,shade);R(4,29,18,4,wood.Lightened(.2f));R(6,30,14,2,ink);
            if(variant==1){R(11,27,2,8,wood);R(17,26,2,7,wood);}
        }
        if(asset==4) // rubble covers exactly its authoritative tile footprint
        {
            R(2,49,14,9,stone);R(2,47,14,3,cap);R(13,50,3,8,shade);
            R(13,43,10,11,stone);R(13,42,10,3,cap);R(21,45,3,9,shade);
            R(4,56,8,4,cap);R(16,56,8,5,shade);
        }
        if(theme=="overgrown") {R(2,55,7,2,accent);R(4,53,3,2,accent);R(21,51,2,7,accent);}
        if(theme=="ceremonial" && asset!=4) {R(4,57,18,1,accent);R(6,59,14,1,accent);}
        return marks.ToArray();
    }
    public static void GroundDetail(TerrainBatch batch,Vector2 p,LandmarkAnchor a,string theme)
    {
        var accent=EnvironmentThemes.Accent(theme);var shade=EnvironmentStyle.Shade;
        void R(int x,int y,int w,int h,Color c)=>batch.Rect(new Rect2(p+new Vector2(x,y),new Vector2(w,h)),c);
        switch(a.Asset)
        {
            case "mosaic":
                R(-11,-5,22,1,accent);R(-11,4,22,1,accent);R(-11,-4,1,8,accent);R(10,-4,1,8,accent);
                R(-3,-2,6,4,shade);R(-1,-1,2,2,accent);break;
            case "threshold":
                R(-11,-4,22,8,shade);R(-11,-4,22,1,accent);R(-11,3,22,1,accent);R(-2,-2,4,4,accent);break;
            case "records":
                R(-8,-3,7,3,new Color("a09b7b"));R(-6,-2,4,1,shade);
                R(2,1,6,3,new Color("858c76"));R(3,2,3,1,shade);break;
            case "offering":
                R(-5,-2,4,3,accent);R(2,0,4,2,new Color("999c80"));R(-4,-3,2,1,new Color("c1b38d"));break;
            case "moss":
                R(-10,-4,9,3,accent.Darkened(.12f));R(-5,-1,10,3,accent);R(3,0,7,3,accent.Darkened(.2f));break;
        }
    }
}
