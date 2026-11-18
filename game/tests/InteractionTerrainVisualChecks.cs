using Godot;
using System.Text.Json;
using V = System.Numerics.Vector2;
namespace Mosslight;

/// <summary>Capture the frames immediately after the production Interact action,
/// rather than waiting for a discarded streamer to finish rebuilding.</summary>
public static class InteractionTerrainVisualChecks
{
    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        var w=app.World;string saved=Persistence.Capture(w,app.Narrative);
        var window=app.GetWindow();var size=window.Size;var zoom=app.Camera.Zoom;
        bool reduced=app.Ui.Preferences.ReducedMotion, frozen=app.FreezeSimulationForVerification;
        string folder=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--interaction-output="))?.Split('=',2)[1]??"build/interactions";
        Directory.CreateDirectory(folder);var results=new List<object>();
        async System.Threading.Tasks.Task Frame(){await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame);app.Camera.ResetSmoothing();}
        OverworldChunkStreamer Stream()=>app.GetTree().GetNodesInGroup("overworld-chunks").OfType<OverworldChunkStreamer>().Single();
        async System.Threading.Tasks.Task Settle()
        {
            for(int i=0;i<2400;i++){await Frame();if(app.GetTree().GetNodesInGroup("overworld-chunks").OfType<OverworldChunkStreamer>().SingleOrDefault()?.CoversVisible==true){await Frame();return;}}
            throw new InvalidOperationException("Interaction scenery did not settle");
        }
        async System.Threading.Tasks.Task<int> Capture(string name)
        {
            await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=window.GetTexture().GetImage();image.SavePng(folder+"/"+name+".png");
            int dark=0;
            // Interior world pixels, outside the HUD and touch-control overlays.
            for(int y=image.GetHeight()/5;y<image.GetHeight()*3/5;y+=4)
                for(int x=image.GetWidth()/5;x<image.GetWidth()*4/5;x+=4)
                {var c=image.GetPixel(x,y);if(c.R+c.G+c.B<.06f)dark++;}
            return dark;
        }
        async System.Threading.Tasks.Task Press(string label,int? resource=null)
        {
            await Settle();var stream=Stream();ulong identity=stream.GetInstanceId();
            int before=await Capture(label+"-before");app.Interact();
            var pixels=new List<int>();
            for(int frame=0;frame<4;frame++)
            {
                await Frame();int dark=await Capture(label+"-after-"+frame);pixels.Add(dark);
                if(Stream().GetInstanceId()!=identity||!Stream().CoversVisible)
                    throw new InvalidOperationException("Interact discarded visible terrain: "+label);
                if(dark>before+64)throw new InvalidOperationException("Ground flashed black: "+label);
            }
            if(resource is int at)
            {
                var view=app.FindChildren("*","",true,false).OfType<FeatureView>().Single(v=>v.GetMeta("tile").AsInt32()==at);
                if(!w.RegionsById["reach"].Depleted.Contains(at)||!view.Spent)
                    throw new InvalidOperationException("Interaction did not refresh resource visual: "+label+"; "+w.InteractionFeedback);
            }
            results.Add(new{action=label,streamer=identity,beforeDarkSamples=before,afterDarkSamples=pixels,feedback=w.InteractionFeedback});
        }
        try
        {
            app.FreezeSimulationForVerification=true;app.Ui.Preferences.ReducedMotion=true;
            // Isolate ground/resource interactions: a depleted object beside the
            // warden would otherwise make the next press start a conversation.
            foreach(string id in w.Actors.Keys.Where(id=>id.StartsWith("npc:")).ToArray())w.Actors.Remove(id);
            app.Ui.ClosePanels();app.ResumeGame();window.Size=new(2340,1080);window.Position=Vector2I.Zero;
            app.Camera.Zoom=Vector2.One*2.2f;var r=w.RegionsById["reach"];var p=w.Player;
            p.Region="reach";p.State=ActionState.Idle;p.MotionX=p.MotionY=0;
            var empty=Enumerable.Range(115,12).SelectMany(y=>Enumerable.Range(80,16).Select(x=>y*r.Width+x))
                .First(at=>r.CanStand(r.Center(at),p)&&r.Features.Keys.All(f=>V.Distance(r.Center(at),r.Center(f))>80));
            p.X=r.Center(empty).X;p.Y=r.Center(empty).Y;app.Views["player"].SnapPosition();app.RebuildViews();
            for(int i=0;i<4;i++)await Press("unavailable-"+i);
            foreach(string kind in new[]{"herb","wood","ore","chest"})
            {
                int selected=-1;
                foreach(int at in r.Features.Where(f=>f.Value==kind&&!r.Depleted.Contains(f.Key)).Select(f=>f.Key))
                {
                    foreach(var offset in new[]{new V(0,36),new V(36,0),new V(-36,0),new V(0,-36)})
                    {
                        var point=r.Center(at)+offset;if(!r.CanStand(point,p))continue;
                        p.X=point.X;p.Y=point.Y;
                        var targets=w.InteractionTargets(p).ToArray();
                        if(targets.Length==1&&targets[0].Key==at){selected=at;break;}
                    }
                    if(selected>=0)break;
                }
                if(selected<0)throw new InvalidOperationException("No reachable resource for "+kind);
                app.Views["player"].SnapPosition();await Press(kind,selected);
                await Press(kind+"-depleted");
                // Independent rendering probes must not fill the starter pack
                // before the following ore/chest interaction can succeed.
                if(kind!="chest")w.Items.Take(p.Id,kind,3);
            }
            File.WriteAllText(folder+"/report.json",JsonSerializer.Serialize(results,Content.Json));
            GD.Print("INTERACTION TERRAIN RENDERED CHECKS PASSED: no-op, resources, chest, repeated depleted interactions; 48 immediate frames");
        }
        finally
        {
            window.Size=size;app.Camera.Zoom=zoom;app.Ui.Preferences.ReducedMotion=reduced;
            app.FreezeSimulationForVerification=frozen;Persistence.Restore(w,app.Narrative,saved);app.RebuildViews();
        }
    }
}
