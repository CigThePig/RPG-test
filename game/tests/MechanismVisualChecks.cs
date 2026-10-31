using Godot;
using System.Text.Json;
using V=System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Rendered lever/portcullis and brazier/seal states through production interaction.</summary>
public static class MechanismVisualChecks
{
    static async System.Threading.Tasks.Task Capture(Foundation app,string name)
    {
        for(int i=0;i<12;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
        await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        app.GetViewport().GetTexture().GetImage().SavePng("docs/native-screenshots/"+(app.Ui.MobileLayout?"mobile-":"")+"mechanism-"+name+".png");
    }
    static void Stand(Foundation app,RegionState r,V point){var p=app.World.Player;p.X=point.X;p.Y=point.Y;app.World.Cancel(p);app.RebuildViews();app.ResumeGame();}
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string snapshot=Persistence.Capture(w,app.Narrative);double scale=Engine.TimeScale;
        var evidence=new List<object>();
        try
        {
            Engine.TimeScale=0;
            foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
            var p=w.Player;p.Health=p.MaxHealth;p.Invulnerable=1000;
            // Seed 7: a brazier ring whose light seal guards the route to the relic.
            {
                var r=Regions.Dungeon(7);w.RegionsById["ruin"]=r;p.Region="ruin";
                var ring=DungeonMechanisms.Controls(r).First(c=>c.Kind=="puzzle");
                var centre=(r.Center(ring.Controllers[1])+r.Center(ring.Controllers[3]))/2;
                Stand(app,r,centre+new V(0,30));
                await Capture(app,"braziers-cold");
                var answer=DungeonMechanisms.Solution(r,ring.Controllers);
                foreach(int brazier in answer)
                {
                    Stand(app,r,r.Center(brazier));
                    if(!w.Interact(p))throw new InvalidOperationException("Visual brazier interaction failed: "+w.InteractionFeedback);
                }
                if(!ring.Gates.All(r.OpenDoors.Contains))throw new InvalidOperationException("Visual seal did not break");
                Stand(app,r,centre+new V(0,30));
                await Capture(app,"braziers-burning");
                var seal=ring.Gates;var sealCentre=seal.Select(r.Center).Aggregate(V.Zero,(a,b)=>a+b)/seal.Length;
                evidence.Add(new{seed=7,kind="puzzle",lightings=answer.Length,sealCells=seal.Length,sealCentre=new[]{sealCentre.X,sealCentre.Y}});
                r.OpenDoors.ExceptWith(seal);foreach(int at in ring.Controllers)r.Depleted.Remove(at);app.RebuildViews();
                int stand=seal.SelectMany(r.Neighbours).Distinct().Order().First(at=>!seal.Contains(at)&&r.CanStand(r.Center(at),p));
                Stand(app,r,r.Center(stand));await Capture(app,"seal-closed");
                r.OpenDoors.UnionWith(seal);app.RebuildViews();await Capture(app,"seal-broken");
                r.ExploredTiles.UnionWith(Enumerable.Range(0,r.Tiles.Length));
                app.Ui.Map();await Capture(app,"map");app.Ui.ClosePanels();
            }
            // Seed 42: a lever beside the sanctuary raises a portcullis shortcut.
            {
                var r=Regions.Dungeon(42);w.RegionsById["ruin"]=r;p.Region="ruin";
                var lever=DungeonMechanisms.Controls(r).First(c=>c.Kind=="lever");
                var gate=lever.Gates;
                int stand=gate.SelectMany(r.Neighbours).Distinct().Order().First(at=>!gate.Contains(at)&&r.CanStand(r.Center(at),p));
                Stand(app,r,r.Center(stand));await Capture(app,"portcullis-lowered");
                Stand(app,r,r.Center(lever.Controllers[0])+new V(0,24));await Capture(app,"lever-ready");
                if(!w.Interact(p)||!gate.All(r.OpenDoors.Contains))throw new InvalidOperationException("Visual lever interaction failed: "+w.InteractionFeedback);
                await Capture(app,"lever-pulled");
                Stand(app,r,r.Center(stand));await Capture(app,"portcullis-raised");
                evidence.Add(new{seed=42,kind="lever",portcullisCells=gate.Length});
            }
            Directory.CreateDirectory("build/dungeon");
            File.WriteAllText("build/dungeon/mechanism-render-"+(app.Ui.MobileLayout?"mobile":"desktop")+".json",JsonSerializer.Serialize(evidence,Content.Json));
        }
        finally{Engine.TimeScale=scale;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();app.ResumeGame();}
    }
}
