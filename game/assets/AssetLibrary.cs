using Godot;
using Pixels=Mosslight.OverworldPropArt.Pixels;
namespace Mosslight;

/// <summary>Named ramps over the shared indexed palette, darkest first. Four tones for
/// small things, six for dressed volumes, eight for lit blobs.</summary>
public static class AssetPalette
{
    public const byte Ink=37,Shadow=35,Penumbra=36;
    public static readonly byte[]
        Wood={12,13,14,15,16,16},Stone={19,20,21,22,23,24},Sand={48,49,50,51,52,53},Iron={37,41,42,42,43,23},
        Leaf8={1,2,2,3,3,4,5,6},Pine8={7,7,8,8,9,9,10,4},Stone8={19,20,20,21,21,22,23,24},Sand8={48,49,49,50,51,52,52,53},
        Autumn8={108,108,109,109,110,110,111,112},Gold8={108,109,110,111,111,112,112,152},Blossom8={121,121,122,122,122,123,123,161},
        Snow8={95,95,96,96,97,97,98,98},Ice8={99,99,100,100,100,101,101,98},Cactus8={105,105,106,106,106,107,107,148},
        Swamp8={113,113,114,114,115,115,116,116},Violet8={117,117,118,118,119,119,120,120},Dune8={51,102,102,103,103,104,104,104},
        Straw8={80,80,81,81,82,82,83,83},Clay8={66,66,67,67,67,68,68,65},Bone8={57,57,58,58,58,59,59,59},Dark8={156,156,157,157,158,158,159,159},
        Red8={137,137,138,138,138,139,139,140},Blue8={141,141,142,142,142,143,143,144},Green8={145,145,146,146,146,147,147,148},
        Bronze={124,124,125,126},Steel={127,128,129,130},Gold={28,28,29,30},Mythril={131,131,132,133},IronT={41,42,43,23},WoodT={13,14,15,16},
        Leather={134,134,135,136},Red={137,138,139,140},Blue={141,142,143,144},Green={145,146,147,148},Yellow={149,150,151,152},
        Orange={153,154,155,152},Violet={117,118,119,120},Pink={121,122,123,161},Teal={131,132,133,56},Pale={159,160,161,98},Dark={156,157,158,159},
        Tile={72,73,74,75},Slate={76,77,78,79},Thatch={80,81,82,83},Snow={95,96,97,98},Ice={99,100,101,98},Flame={44,45,46,47},Ward={54,54,55,56};
    public static uint Hash(int x,int y,int slot)=>EnvironmentStyle.Hash(7,x,y,30000+slot);
}

/// <summary>One named piece of art: its cell, where its ground contact is, and its recipe.</summary>
public sealed record AssetModule(string Name,int Width,int Height,Vector2 Pivot,Func<byte[]> Paint,bool Flat=false);

/// <summary>A named sheet of equally sized cells.</summary>
public sealed class AssetSheet
{
    public string Name {get;}
    public int CellWidth {get;}
    public int CellHeight {get;}
    public int Columns {get;}
    public List<AssetModule> Modules {get;}=new();
    public AssetSheet(string name,int cellWidth,int cellHeight,int columns){Name=name;CellWidth=cellWidth;CellHeight=cellHeight;Columns=columns;}
    public int Rows=>(Modules.Count+Columns-1)/Columns;
    public Rect2 Source(int index)=>new(index%Columns*CellWidth,index/Columns*CellHeight,CellWidth,CellHeight);
    /// <summary>Add a module painted on a fresh canvas of this sheet's cell size.</summary>
    public AssetSheet Add(string name,Action<Pixels> paint,bool flat=false,Vector2? pivot=null)
    {
        int w=CellWidth,h=CellHeight;
        Modules.Add(new AssetModule(name,w,h,pivot??new Vector2(w/2,h-4),()=>{var p=new Pixels(w,h);paint(p);p.Contour(flat);return p.Data;},flat));
        return this;
    }
}

/// <summary>The asset library: every sheet, a name index, on-demand textures for the game
/// and a PNG + JSON export for use outside it. Painted procedurally with the Living
/// Reach's painter and palette; nothing here is read by the simulation or saved.</summary>
public static class AssetLibrary
{
    static List<AssetSheet>? sheets;
    static Dictionary<string,(AssetSheet Sheet,int Index)>? index;
    static readonly Dictionary<string,ImageTexture> textures=new();
    public static IReadOnlyList<AssetSheet> Sheets=>sheets??=new(){ItemIconArt.Sheet(),BiomeArt.Sheet(),StructureArt.Sheet(),StructureArt.Large(),FurnishingArt.Sheet(),DelveArt.Sheet(),EffectArt.Sheet(),MaterialArt.Sheet()};
    public static bool TryFind(string name,out AssetSheet sheet,out int at)
    {
        index??=Sheets.SelectMany(s=>s.Modules.Select((m,i)=>(m.Name,s,i))).ToDictionary(e=>e.Name,e=>(e.s,e.i),StringComparer.Ordinal);
        bool found=index.TryGetValue(name,out var entry);sheet=entry.Sheet;at=entry.Index;return found;
    }
    public static Image Render(AssetSheet sheet)
    {
        var image=Image.CreateEmpty(sheet.Columns*sheet.CellWidth,Math.Max(1,sheet.Rows)*sheet.CellHeight,false,Image.Format.Rgba8);image.Fill(Colors.Transparent);
        for(int i=0;i<sheet.Modules.Count;i++)
        {
            var data=sheet.Modules[i].Paint();var rect=sheet.Source(i);
            for(int y=0;y<sheet.CellHeight;y++)for(int x=0;x<sheet.CellWidth;x++)
                if(data[y*sheet.CellWidth+x]!=0)image.SetPixel((int)rect.Position.X+x,(int)rect.Position.Y+y,OverworldPropArt.Palette[data[y*sheet.CellWidth+x]]);
        }
        return image;
    }
    /// <summary>One process-lifetime texture per sheet, built the first time it is asked for.</summary>
    public static Texture2D Texture(AssetSheet sheet)
    {
        if(textures.TryGetValue(sheet.Name,out var texture))return texture;
        using var image=Render(sheet);return textures[sheet.Name]=ImageTexture.CreateFromImage(image);
    }
    /// <summary>Draw a named module with its ground contact at <paramref name="foot"/>.</summary>
    public static bool Draw(CanvasItem canvas,string name,Vector2 foot)
    {
        if(!TryFind(name,out var sheet,out int at))return false;
        var module=sheet.Modules[at];
        canvas.DrawTextureRectRegion(Texture(sheet),new Rect2(foot-module.Pivot,new Vector2(module.Width,module.Height)),sheet.Source(at));
        return true;
    }
    /// <summary>Write every sheet as a PNG, a labelled contact sheet on a neutral ground, and
    /// one manifest with each module's rectangle and pivot.</summary>
    public static int Export(string directory)
    {
        Directory.CreateDirectory(directory);
        var manifest=new List<object>();int count=0;
        foreach(var sheet in Sheets)
        {
            using var image=Render(sheet);image.SavePng(Path.Combine(directory,sheet.Name+".png"));
            using var preview=Image.CreateEmpty(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8);
            preview.Fill(sheet.Name is "effects"?new Color("1c2030"):sheet.Name is "delve"?new Color("3a4254"):sheet.Name is "items"?new Color("2b3140"):new Color("55994a"));
            preview.BlendRect(image,new Rect2I(0,0,image.GetWidth(),image.GetHeight()),Vector2I.Zero);
            preview.Resize(image.GetWidth()*2,image.GetHeight()*2,Image.Interpolation.Nearest);
            preview.SavePng(Path.Combine(directory,sheet.Name+"-preview.png"));
            for(int i=0;i<sheet.Modules.Count;i++)
            {
                var m=sheet.Modules[i];var r=sheet.Source(i);count++;
                manifest.Add(new{name=m.Name,sheet=sheet.Name+".png",x=(int)r.Position.X,y=(int)r.Position.Y,width=m.Width,height=m.Height,pivotX=(int)m.Pivot.X,pivotY=(int)m.Pivot.Y,flat=m.Flat});
            }
        }
        File.WriteAllText(Path.Combine(directory,"manifest.json"),System.Text.Json.JsonSerializer.Serialize(manifest,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        var palette=OverworldPropArt.Palette.Select((c,i)=>new{index=i,hex=c.ToHtml(c.A<1)}).ToArray();
        File.WriteAllText(Path.Combine(directory,"palette.json"),System.Text.Json.JsonSerializer.Serialize(palette));
        return count;
    }
}
