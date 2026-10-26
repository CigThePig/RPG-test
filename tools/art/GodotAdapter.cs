// Only the value/image API used by upstream EnvironmentArt. No Godot runtime required.
namespace Godot;
public readonly record struct Vector2I(int X, int Y);
public readonly record struct Color(float R, float G, float B, float A = 1) {
 public int R8 => (int)System.Math.Round(R*255); public int G8 => (int)System.Math.Round(G*255); public int B8 => (int)System.Math.Round(B*255); public int A8 => (int)System.Math.Round(A*255);
 public static Color Color8(byte r,byte g,byte b,byte a=255)=>new(r/255f,g/255f,b/255f,a/255f);
 public Color Lightened(float t)=>Lerp(new Color(1,1,1,A),t);
 public Color Darkened(float t)=>Lerp(new Color(0,0,0,A),t);
 public Color Lerp(Color c,float t)=>new(R+(c.R-R)*t,G+(c.G-G)*t,B+(c.B-B)*t,A+(c.A-A)*t);
}
public sealed record Image(int Width,int Height,byte[] Data) {
 public enum Format { Rgba8 }
 public static Image CreateFromData(int w,int h,bool mip,Format f,byte[] data)=>new(w,h,data);
}
