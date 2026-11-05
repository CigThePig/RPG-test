using Godot;
namespace Mosslight;

/// <summary>Viewport and safe rectangles are logical canvas coordinates, never camera coordinates.</summary>
public static class DisplayLayout
{
    public const float DesignAspect = 1280f / 800;
    public static Rect2 Gameplay(Rect2 safe)
    {
        // Preserve the old height-based scale on wide phones; compact/narrow screens
        // retain the responsive fallback. Centre within usable space, not a cutout.
        float width = Math.Min(safe.Size.X, safe.Size.Y * DesignAspect);
        return new Rect2(safe.Position + new Vector2((safe.Size.X-width)/2, 0), new Vector2(width,safe.Size.Y));
    }
    public static Rect2 Overlay(Rect2 safe)
    {
        float width = Math.Min(1280, safe.Size.X);
        return new Rect2(safe.Position + new Vector2((safe.Size.X-width)/2,0), new Vector2(width,safe.Size.Y));
    }
    public static Vector2 CameraCenter(Vector2 focus, Vector2 bounds, Vector2 half) => new(
        bounds.X <= half.X*2 ? bounds.X/2 : Mathf.Clamp(focus.X,half.X,bounds.X-half.X),
        bounds.Y <= half.Y*2 ? bounds.Y/2 : Mathf.Clamp(focus.Y,half.Y,bounds.Y-half.Y));
}

/// <summary>Non-playable bedrock behind finite regions, including maps smaller than the view.
/// Canvas backing only: no generated tiles, collision, textures, or persistent state.</summary>
public partial class RegionBedrock : Control
{
    public override void _Ready() { MouseFilter=MouseFilterEnum.Ignore; Resized+=QueueRedraw; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("171e20"));
        // Quiet mineral seams distinguish intentional rock backing from undefined output.
        for(float y=0;y<Size.Y;y+=48)
            for(float x=(int)(y/48)%2*48;x<Size.X;x+=96)
                DrawLine(new Vector2(x,y),new Vector2(x+28,y+12),new Color("20292b"),1);
    }
}
