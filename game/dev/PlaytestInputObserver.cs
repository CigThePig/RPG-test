using Godot;
namespace Mosslight;
/// <summary>Last child receives input before controls can consume it. Observation never handles events.</summary>
public partial class PlaytestInputObserver : Node
{
    public PlaytestTrace Trace { get; set; } = null!;
    public override void _Input(InputEvent e) => Trace.Input(e);
}
