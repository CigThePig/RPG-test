using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>One cached neutral, south-facing PPC portrait, independent of the gameplay motor and texture.</summary>
public static class HeroPortrait
{
    static CreatureModel? cachedModel;
    static Texture2D? cached;
    public static Texture2D? Get(CreatureModel? model)
    {
        if (model == null) return null;
        if (ReferenceEquals(model, cachedModel)) return cached;
        var motor = new CreatureMotor(model, CreatureRuntime.Registry, new MotorOptions { Facing = FacingMode.Free, ExportMode = true });
        double yaw = PixelCamera.DirectionYaw(6); // South: towards the camera.
        motor.Teleport(Vec3.Zero, yaw);
        var c = model.Anatomy.Canvas;
        PixelFrame? frame = null;
        var view = ViewSpec.Default(yaw); view.Quality = 1; view.DrawShadow = false;
        var renderer = new CreatureRenderer();
        for (int padding = 64; padding <= 256; padding *= 2)
        {
            frame = new PixelFrame(c.Width + padding * 2, c.Height + padding * 2) { OriginX = c.OriginX + padding, OriginY = c.OriginY + padding };
            var stats = renderer.Render(model.Anatomy, model.Palette, model.Surface, motor.Pose, view, frame);
            if (!stats.Overflow) break;
            if (padding == 256) return null; // Written fallback rather than a cropped or unbounded portrait.
        }
        if (frame == null) return null;
        var lut = new byte[1024]; model.Palette.BuildLut(lut, 0);
        var rgba = new byte[frame.Width * frame.Height * 4]; frame.ToRgba(lut, rgba, model.Palette.WaterTint);
        using var image = Image.CreateFromData(frame.Width, frame.Height, false, Image.Format.Rgba8, rgba);
        var bounds = image.GetUsedRect();
        if (!bounds.HasArea()) return null;
        using var crop = image.GetRegion(bounds);
        cachedModel = model; cached = ImageTexture.CreateFromImage(crop);
        return cached;
    }
}
