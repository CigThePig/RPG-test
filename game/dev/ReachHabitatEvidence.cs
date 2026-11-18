using Godot;
using PixelCreatures.Runtime;
namespace Mosslight;

/// <summary>Non-colliding ground signs. One draw list near the camera, no new asset pipeline.</summary>
public partial class ReachHabitatEvidence : Node2D
{
    public World World {get;set;}=null!;
    double elapsed;
    public override void _Process(double delta){elapsed+=delta;if(elapsed<.5)return;elapsed=0;QueueRedraw();}
    public override void _Draw()
    {
        if(!World.RegionsById.TryGetValue("reach",out var r))return;
        foreach(var s in r.ReachSites)
        {
            var center=r.Center(s.Tile);
            if(System.Numerics.Vector2.DistanceSquared(center,World.Player.Position)>1200*1200)continue;
            var p=GodotConvert.GroundToScreen(center.X,center.Y);
            bool acid=s.Template=="poisoned-shore";
            for(int i=0;i<7;i++)
            {
                float angle=i*Mathf.Tau/7;var offset=new Vector2(Mathf.Cos(angle)*43,Mathf.Sin(angle)*22);
                var at=p+offset;
                DrawEllipse(at,new Vector2(10,3),acid?new Color("39473c"):new Color("60513d"));
                if(s.Template is "rocky-guardian" or "defended-refuge")
                {DrawCircle(at+new Vector2(2,-2),3,new Color("77817b"));DrawLine(at+new Vector2(0,-4),at+new Vector2(3,-4),new Color("a0aaa0"));}
                else if(s.Template is "woodland-ambush" or "pack-leader" or "hunting-pack")
                {DrawLine(at+new Vector2(-5,-2),at+new Vector2(5,1),new Color("957e57"),1);DrawLine(at+new Vector2(-2,2),at+new Vector2(3,-2),new Color("78664a"),1);}
            }
        }
    }
    void DrawEllipse(Vector2 at,Vector2 scale,Color color)
    {
        var points=Enumerable.Range(0,12).Select(i=>at+new Vector2(Mathf.Cos(i*Mathf.Tau/12)*scale.X,Mathf.Sin(i*Mathf.Tau/12)*scale.Y)).ToArray();
        DrawColoredPolygon(points,color with {A=.65f});
    }
}
