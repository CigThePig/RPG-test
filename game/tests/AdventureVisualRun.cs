using Godot;
namespace Mosslight;

/// <summary>Continuous render of the same production-API normal-health run. No scene stitching.</summary>
public static class AdventureVisualRun
{
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        string? checkpoint=null;var run=new AdventureRun(app){UseDialogue=true,Checkpoint=name=>checkpoint=name};
        var frames=new List<object>();int rendered=0;
        using var steps=run.Steps().GetEnumerator();
        Directory.CreateDirectory("build/adventure/frames");
        bool more=true;
        while(more)
        {
            more=steps.MoveNext();
            if(!more)break;
            for(int batch=1;batch<3 && checkpoint==null;batch++) { more=steps.MoveNext();if(!more)break; }
            app.Ui.HideMenu(); // Pause simulation through the dedicated verification flag, keep presentation alive.
            app.RebuildViewsIfNeededForAdventure();
            await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
            await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            rendered++;
            if(checkpoint!=null)
            {
                string name=checkpoint;checkpoint=null;
                app.GetViewport().GetTexture().GetImage().SavePng($"build/adventure/frames/{rendered:D5}-{name}.png");
                frames.Add(new{name,frame=rendered,tick=app.World.Tick,health=app.World.Player.Health});
                if(name is "assignment-accepted" or "relic-recovered" or "quest-completed-rewarded")
                {
                    app.Ui.Journal();
                    for(int i=0;i<12;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
                    await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    app.GetViewport().GetTexture().GetImage().SavePng($"build/adventure/frames/{rendered:D5}-{name}-journal.png");
                    app.Ui.ClosePanels();
                }
                if(name is "exploration-supplies" or "relic-sanctuary")
                {
                    app.Ui.Map();
                    for(int i=0;i<12;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);
                    await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    app.GetViewport().GetTexture().GetImage().SavePng($"build/adventure/frames/{rendered:D5}-map.png");
                    app.Ui.ClosePanels();
                }
            }
        }
        File.WriteAllText("build/adventure/rendered-frames.json",System.Text.Json.JsonSerializer.Serialize(new{setup="Production API driver; up to three simulation ticks per rendered frame (3x navigation playback); normal health, no OS input automation",frames},Content.Json));
        GD.Print("RENDERED ADVENTURE PASSED");
    }
}
