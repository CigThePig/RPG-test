using Godot;
namespace Mosslight;
/// <summary>Two immutable process-lifetime atlases. Generation and upload occur once,
/// on the main thread. All regions reuse them; no textures keyed by region seed.</summary>
public static class EnvironmentAssetCache
{
    static ImageTexture? floors,walls;
    public static int TextureCount => (floors==null?0:1)+(walls==null?0:1);
    public static long PixelBytes => (floors==null?0:384*12*4)+(walls==null?0:1664*30*4);
    public static Texture2D Floors => floors ??= Build(false);
    public static Texture2D Walls => walls ??= Build(true);
    static ImageTexture Build(bool wall)
    {
        int count=wall?64:16, stride=wall?26:24, height=wall?30:12;
        using var image=Image.CreateEmpty(count*stride,height,false,Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        for(int module=0;module<count;module++)
        {
            var marks=wall?EnvironmentArtGenerator.WallModule(module%16,(module/16&1)!=0,(module/16&2)!=0):EnvironmentArtGenerator.FloorModule(module);
            foreach(var mark in marks)
            {
                int padding=wall?1:0;
                for(int y=(int)mark.Rect.Position.Y;y<(int)mark.Rect.End.Y;y++)
                    for(int x=(int)mark.Rect.Position.X;x<(int)mark.Rect.End.X;x++)
                        image.SetPixel(module*stride+padding+x,padding+y,mark.Color);
            }
            // Transparent wall gutters prevent atlas neighbour bleeding. Floors abut
            // with identical mortar; nearest filtering and integer UVs avoid seams.
        }
        return ImageTexture.CreateFromImage(image);
    }
}
