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
}
