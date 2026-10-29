using Godot;

namespace Mosslight;

/// <summary>Persistent, presentation-only marker for a lootable corpse.</summary>
public partial class LootMarker : Node2D
{
    public override void _Draw()
    {
        var at = new Vector2(10, -32);
        DrawCircle(at, 10, new Color(.12f, .10f, .09f, .86f));
        DrawRect(new Rect2(at + new Vector2(-7, -3), new Vector2(14, 9)), new Color("b49364"));
        DrawRect(new Rect2(at + new Vector2(-6, -6), new Vector2(12, 4)), new Color("e1c786"));
        DrawLine(at + new Vector2(-6, -1), at + new Vector2(6, -1), new Color("5b4936"), 2);
        DrawRect(new Rect2(at + new Vector2(-1, -1), new Vector2(2, 3)), new Color("fff0b2"));
    }
}
