using Godot;
using System;
using System.Collections.Generic;

namespace Mosslight;

/// <summary>
/// Original 32px pixel-object illustrations. No external art, RNG, dynamic instance
/// textures or animation allocations; one shared nearest-neighbour texture per ID.
/// </summary>
public static class ItemIconAtlas
{
    static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);
    public static int CachedCount => Cache.Count;

    public static Texture2D Get(string id)
    {
        string key = id switch
        {
            "iron_sword" or "oak_staff" or "leather_coat" or "potion" or
            "herb" or "ore" or "wood" or "coin" or "relic" or "dungeon_key" => id,
            _ => "unknown"
        };
        if (Cache.TryGetValue(key, out var existing)) return existing;
        using var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        Paint(image, key);
        var texture = ImageTexture.CreateFromImage(image);
        Cache[key] = texture;
        return texture;
    }

    static void Paint(Image image, string id)
    {
        var p = new Brush(image);
        Color edge = new("101a19"), steel = new("a6bac0"), shine = new("e9e9d8");
        Color bark = new("8e6246"), wood = new("bc9568"), bronze = new("bcaa76");
        Color moss = new("6f9d78"), pale = new("b7d0a0"), ruby = new("bd5759");
        switch (id)
        {
            case "iron_sword":
                p.Line(6,26,26,6,7,edge); p.Line(9,23,25,7,4,steel);
                p.Line(12,20,25,7,1,shine);
                p.Line(7,16,16,25,5,edge); p.Line(8,17,15,24,2,bronze);
                p.Line(4,28,11,21,5,edge); p.Line(5,27,10,22,3,bark);
                p.Dot(3,29,4,bronze); break;
            case "oak_staff":
                p.Line(8,29,22,9,7,edge); p.Line(9,28,22,9,4,bark);
                p.Line(10,26,21,11,1,wood); p.Dot(22,8,9,edge);
                p.Dot(22,7,6,moss); p.Dot(21,6,3,pale);
                p.Line(19,14,26,13,2,bronze); break;
            case "leather_coat":
                p.Rect(8,7,16,23,edge); p.Rect(10,9,12,18,bark);
                p.Rect(4,10,7,13,edge); p.Rect(21,10,7,13,edge);
                p.Rect(6,12,5,10,bark); p.Rect(21,12,5,10,bark);
                p.Rect(14,9,4,19,wood); p.Line(17,13,13,26,2,edge);
                p.Dot(16,16,2,bronze); p.Dot(16,22,2,bronze); break;
            case "potion":
                p.Rect(12,2,8,8,edge); p.Rect(13,3,6,5,bronze);
                p.Rect(9,8,14,22,edge); p.Rect(11,11,10,17,steel);
                p.Rect(11,16,10,12,ruby); p.Rect(12,18,8,8,new Color("a73f4f"));
                p.Rect(12,11,2,8,shine); p.Rect(13,3,6,2,wood); break;
            case "herb":
                p.Line(15,29,16,5,3,edge); p.Line(16,28,16,6,1,pale);
                foreach (var (x,y) in new[]{(9,10),(22,8),(6,18),(23,18),(10,25)})
                {
                    p.Dot(x,y,9,edge); p.Dot(x,y,6,moss); p.Dot(x-1,y-1,3,pale);
                    p.Line(16,y+2,x,y,2,moss);
                }
                break;
            case "ore":
                p.Dot(14,18,23,edge); p.Dot(21,18,18,edge);
                p.Dot(13,16,17,new Color("697371")); p.Dot(21,18,14,steel);
                p.Dot(8,19,7,bark); p.Dot(19,10,6,bronze);
                p.Dot(24,23,5,bronze); p.Line(11,21,20,13,2,shine); break;
            case "wood":
                foreach(var y in new[]{9,17,24})
                {
                    p.Rect(4,y-4,25,8,edge); p.Rect(6,y-2,22,5,bark);
                    p.Rect(7,y-1,16,2,wood); p.Dot(7,y,5,bronze);
                    p.Dot(7,y,2,edge);
                }
                p.Line(19,4,22,28,2,bronze); break;
            case "coin":
                p.Dot(15,18,25,edge); p.Dot(15,17,21,bronze);
                p.Dot(14,16,15,new Color("e4c67c")); p.Dot(14,16,10,bronze);
                p.Line(13,11,13,21,2,shine); p.Line(13,11,18,13,2,shine);
                p.Line(13,21,19,19,2,shine); p.Dot(25,24,6,wood); break;
            case "relic":
                p.Rect(7,4,18,26,edge); p.Rect(9,6,14,22,new Color("637b74"));
                p.Rect(11,8,10,18,new Color("81948b"));
                p.Line(16,8,12,16,3,edge); p.Line(12,16,20,16,3,edge);
                p.Line(20,16,16,24,3,edge); p.Dot(16,16,3,bronze);
                p.Rect(9,26,5,2,moss); break;
            case "dungeon_key":
                p.Dot(10,10,17,edge); p.Dot(10,10,11,bronze); p.Dot(10,10,5,edge);
                p.Line(14,14,26,26,7,edge); p.Line(15,15,25,25,4,bronze);
                p.Line(22,23,28,18,5,edge); p.Line(23,23,27,19,3,bronze);
                p.Line(25,26,29,22,5,edge); p.Line(25,25,28,23,3,bronze); break;
            default:
                p.Rect(6,12,20,18,edge); p.Rect(8,14,16,14,bark);
                p.Line(9,14,12,7,5,edge); p.Line(23,14,20,7,5,edge);
                p.Line(11,9,21,9,3,bronze); p.Dot(16,21,7,bronze);
                p.Dot(16,21,3,edge); break;
        }
    }

    sealed class Brush(Image image)
    {
        public void Rect(int x, int y, int w, int h, Color color)
        {
            for (int yy=Math.Max(0,y); yy<Math.Min(32,y+h); yy++)
                for (int xx=Math.Max(0,x); xx<Math.Min(32,x+w); xx++)
                    image.SetPixel(xx, yy, color);
        }
        public void Dot(int x, int y, int size, Color color)
        {
            float r=size/2f;
            for(int yy=Math.Max(0,y-size);yy<Math.Min(32,y+size);yy++)
                for(int xx=Math.Max(0,x-size);xx<Math.Min(32,x+size);xx++)
                    if((xx-x)*(xx-x)+(yy-y)*(yy-y)<=r*r) image.SetPixel(xx,yy,color);
        }
        public void Line(int x0,int y0,int x1,int y1,int thickness,Color color)
        {
            int dx=Math.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.Abs(y1-y0),sy=y0<y1?1:-1,err=dx+dy;
            while(true)
            {
                Dot(x0,y0,thickness,color);
                if(x0==x1 && y0==y1)break;
                int e=2*err;
                if(e>=dy){err+=dy;x0+=sx;}
                if(e<=dx){err+=dx;y0+=sy;}
            }
        }
    }
}
