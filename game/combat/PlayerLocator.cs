using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>A small ground locator only during local crowding; never affects visibility or targeting.</summary>
public partial class PlayerLocator : Node2D
{
    public Foundation App { get; set; } = null!;
    World World => App.World;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if(App.World == null)return;
        var p=World.Player;
        if(!p.Alive)return;
        int nearby=0;
        foreach(var a in World.Actors.Values)
            if(a.Id!=p.Id && a.Alive && a.Region==p.Region && System.Numerics.Vector2.DistanceSquared(a.Position,p.Position)<48*48 && World.Hostile(p,a))
                if(++nearby>=2)break;
        if(nearby<2)return;
        var at=GodotConvert.GroundToScreen(p.X,p.Y);
        var ink=new Color(.05f,.10f,.10f,.85f);
        var edge=new Color(.85f,.83f,.58f,.8f);
        DrawArc(at,12,0,Mathf.Tau,24,ink,4,true);
        DrawArc(at,12,0,Mathf.Tau,24,edge,1.5f,true);
        DrawLine(at+new Vector2(0,-17),at+new Vector2(0,-12),edge,2,true);
    }
}
