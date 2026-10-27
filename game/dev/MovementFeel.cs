using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;
/// <summary>Player-only control policy. Actor stats/statuses and World collision remain authoritative.</summary>
public sealed record MovementFeel(string Id, float Scale, bool ScreenUniform, float Response, float Deadzone, float Curve, float CameraFollow, float Zoom)
{
    public static readonly MovementFeel[] Presets = {
        new("baseline",1,false,0,.1f,1,12,1.5f),
        new("precise",2.4f,true,0,.08f,.8f,20,1.5f),
        new("soft",2.4f,true,.045f,.08f,.8f,14,1.5f),
        new("explore",3.2f,true,.07f,.08f,.75f,18,1.35f)
    };
    public Vector2 Convert(Vector2 screen)
    {
        screen=screen.LimitLength();
        var ground=GodotConvert.ScreenToGround(screen);
        return ScreenUniform ? ground : ground.Normalized()*screen.Length();
    }
}
public static class BuildIdentity
{
    public static string Source => Godot.FileAccess.FileExists("res://game/data/build-source.txt") ? Godot.FileAccess.GetFileAsString("res://game/data/build-source.txt").Trim() : "local-uncommitted";
}
