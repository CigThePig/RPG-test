using Godot;
using System.Threading;
namespace Mosslight;

/// <summary>Managed bytes only. Build may run on a worker against an immutable tile
/// snapshot; Upload, materials, meshes and all scene nodes stay on the main thread.</summary>
public sealed record ChunkLookup(int Width,int Height,byte[] Ground,byte[]? Water,byte[]? Rock,double PrepareMs)
{
    public static double ImageCopyMilliseconds {get;private set;}
    public static double TextureUploadMilliseconds {get;private set;}
    public long Bytes=>Ground.LongLength+(Water?.LongLength??0)+(Rock?.LongLength??0);
    public static ChunkLookup Build(RegionState r,Rect2I cells,CancellationToken cancellation=default)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        int sx=Math.Max(0,cells.Position.X-3),sy=Math.Max(0,cells.Position.Y-3);
        int ex=Math.Min(r.Width,cells.End.X+3),ey=Math.Min(r.Height,cells.End.Y+3);
        int width=(ex-sx)*8+1,height=(ey-sy)*4+1;
        bool water=false,rock=false;
        for(int y=cells.Position.Y;y<cells.End.Y;y++)for(int x=cells.Position.X;x<cells.End.X;x++)
        {water|=r.Tiles[y*r.Width+x]==Terrain.Water;rock|=r.Tiles[y*r.Width+x]==Terrain.Rock;}
        // Raised rock tops and shadows spill into neighbouring cells. A chunk
        // with no rock in its core still needs the outcrop layer at that edge.
        for(int y=sy;y<ey&&!rock;y++)for(int x=sx;x<ex&&!rock;x++)rock|=r.Tiles[y*r.Width+x]==Terrain.Rock;
        var ground=new byte[width*height*4];var wet=water?new byte[width*height*3]:null;var stone=rock?new byte[width*height*3]:null;
        byte Byte(float value)=>(byte)(Math.Clamp(value,0,1)*255); // Matches Image's normalized byte conversion.
        for(int y=0;y<height;y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for(int x=0;x<width;x++)
            {
                int gx=x+sx*8,gy=y+sy*4,i=y*width+x,tx=gx/8,ty=gy/4;float px=gx*3,py=gy*6;
                var s=OverworldGroundArt.Sample(r,px,py);
                ground[i*4]=Byte((s.Soil+48)/96);ground[i*4+1]=Byte(s.Wet/48);
                ground[i*4+2]=Byte(OverworldGroundArt.Noise(r.Seed,px/110f,py/110f,0));ground[i*4+3]=Byte(OverworldGroundArt.Noise(r.Seed,px/22f,py/22f,1));
                if(wet!=null)
                {
                    float d=OverworldWaterArt.Water(r,tx,ty)?OverworldWaterArt.Distance(r,tx,ty,px,py):-2;
                    wet[i*3]=Byte((d+8)/68);wet[i*3+1]=Byte(OverworldWaterArt.Variation(r.Seed,px,py));wet[i*3+2]=Byte(OverworldWaterArt.BankInset(r,tx,ty,px,py)/8);
                }
                if(stone!=null)
                {
                    int rx=Math.Min(tx,r.Width-1),ry=Math.Min(ty,r.Height-1);
                    float d=OverworldRockRenderer.Rock(r,rx,ry)?OverworldRockRenderer.Distance(r,rx,ry,px,py):-2;
                    stone[i*3]=Byte((d+8)/68);stone[i*3+1]=Byte(OverworldGroundArt.Noise(r.Seed,px/46f,py/46f,22));stone[i*3+2]=Byte(OverworldRockRenderer.Inset(r,px,py)/8);
                }
            }
        }
        return new(width,height,ground,wet,stone,timer.Elapsed.TotalMilliseconds);
    }
    public ImageTexture Upload(byte[] bytes,Image.Format format)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        using var image=Image.CreateFromData(Width,Height,false,format,bytes);
        ImageCopyMilliseconds+=timer.Elapsed.TotalMilliseconds;timer.Restart();
        var texture=ImageTexture.CreateFromImage(image);
        TextureUploadMilliseconds+=timer.Elapsed.TotalMilliseconds;return texture;
    }
}
