using Godot;
using System.Diagnostics;
using System.Text.Json;
using PixelCreatures.Runtime;

namespace Mosslight;
public static class VisualChecks
{
    static object? telegraphPerformance;
    static object? combatPerformance;
    static object? cameraHitch;
    static object? tacticalPerformance;
    static async System.Threading.Tasks.Task Frames(Foundation app, int count = 12)
    {
        for (int i = 0; i < count; i++)
            await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    static async System.Threading.Tasks.Task Capture(Foundation app, string name)
    {
        await Frames(app);
        await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = app.GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory("docs/native-screenshots");
        image.SavePng("docs/native-screenshots/" + (app.Ui.MobileLayout ? "mobile-" : "") + name + ".png");
    }

    // No 12-frame pre-roll: capture the actual effect phases at gameplay zoom.
    static async System.Threading.Tasks.Task CaptureRollFrame(Foundation app, string name)
    {
        await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = app.GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory("docs/native-screenshots");
        image.SavePng("docs/native-screenshots/" + (app.Ui.MobileLayout ? "mobile-" : "") + name + ".png");
    }

    static async System.Threading.Tasks.Task AwaitDodgeProgress(Foundation app, float seconds)
    {
        for(int i=0;i<120;i++)
        {
            var p=app.World.Player;
            if(p.State != ActionState.Dodging)
                throw new InvalidOperationException("Dodge ended before expected dust phase " + seconds);
            if(p.ActionElapsed >= seconds)return;
            await Frames(app,1);
        }
        throw new InvalidOperationException("Visual dodge did not advance to " + seconds);
    }

    static async System.Threading.Tasks.Task Until(Foundation app, Func<bool> predicate, string message)
    {
        for (int i = 0; i < 360; i++) { if (predicate()) return; await Frames(app, 1); }
        throw new InvalidOperationException(message);
    }

    // The captured pixels come from the actual viewport. Time scale is reduced
    // here only to make screenshots reproducible on slow CI renderers; the movie
    // replay separately exercises the normal fixed-step timing and input path.
    static async System.Threading.Tasks.Task VerifyTactics(Foundation app)
    {
        var w=app.World; string snapshot=Persistence.Capture(w,app.Narrative);
        double oldScale=Engine.TimeScale;
        try
        {
            Engine.TimeScale=1;w.ProfileCombat=true;
            var course=GameplayReplay.CreateCourse();course.Id="tactical-visual";
            w.RegionsById[course.Id]=course;
            var p=w.Player;p.Region=course.Id;p.X=300;p.Y=300;p.Health=p.MaxHealth=10000;p.Invulnerable=1000;
            var enemies=w.Actors.Values.Where(a=>a.Id!="player").OrderBy(a=>a.Id,StringComparer.Ordinal).Take(8).ToArray();
            var samples=new List<object>();
            foreach(var workload in new[]{("idle",0), ("one engaged",1), ("three archetypes",3), ("four enemies",4), ("eight enemies",8), ("projectiles",3), ("projectiles and effects",4)})
            {
                w.Projectiles.Clear();
                foreach(var a in w.Actors.Values.Where(a=>a.Id!="player"))a.Region="reach";
                for(int i=0;i<Math.Min(workload.Item2,enemies.Length);i++)
                {
                    var a=enemies[i];w.Cancel(a);
                    string id=new[]{"prowler","brute","spitter"}[i%3];
                    a.CombatProfile=id;a.Region=course.Id;a.Faction="beasts";
                    a.Abilities=w.Content.CombatProfiles.Single(c=>c.Id==id).Priorities.Keys.ToList();
                    a.X=370+(i%3)*38;a.Y=255+(i/3)*32;
                    a.Health=a.MaxHealth=1000;a.Speed=id=="brute"?46:72;a.Invulnerable=0;a.Cooldowns.Clear();a.Statuses.Clear();a.NextTacticalTime=0;
                }
                app.RebuildViews();app.ResumeGame();await Frames(app,20);
                var times=new List<double>();var ai=new List<double>();var avoidance=new List<double>();var navigation=new List<double>();var projectiles=new List<double>();var simulation=new List<double>();
                long rasters=CreatureRenderQueue.TotalRenders,uploads=CreatureRenderQueue.TotalUploads,allocation=GC.GetAllocatedBytesForCurrentThread(),memory=GC.GetTotalMemory(false);
                int effectPeak=0,projectilePeak=0;
                var clock=Stopwatch.StartNew();double last=clock.Elapsed.TotalMilliseconds;
                for(int frame=0;frame<120;frame++)
                {
                    if(workload.Item1.StartsWith("projectiles") && frame%15==0)
                        for(int j=0;j<4;j++)w.Projectiles.Add(ProjectileFlight.Release(enemies[0],p,w.FindAbility(j%2==0?"spit":"ember")!));
                    if(workload.Item1=="projectiles and effects" && frame%30==0)
                        foreach(var a in enemies.Take(4)){a.Invulnerable=0;w.Damage(p,a,1,"physical",ability:"slash");}
                    await Frames(app,1);
                    double now=clock.Elapsed.TotalMilliseconds;times.Add(now-last);last=now;
                    ai.Add(w.TacticalMilliseconds);avoidance.Add(w.AvoidanceMilliseconds);navigation.Add(w.NavigationMilliseconds);projectiles.Add(w.ProjectileMilliseconds);simulation.Add(app.SimulationMs);
                    effectPeak=Math.Max(effectPeak,app.CombatEffects.ActiveCount);projectilePeak=Math.Max(projectilePeak,w.Projectiles.Count);
                    if(frame==60)await CaptureRollFrame(app,"tactical-"+workload.Item1.Replace(' ','-'));
                }
                double Q(List<double> values,double fraction){values.Sort();return values[Math.Min(values.Count-1,(int)(values.Count*fraction))];}
                samples.Add(new {scenario=workload.Item1,frames=times.Count,p50ms=Q(times,.5),p95ms=Q(times,.95),p99ms=Q(times,.99),aiP95ms=Q(ai,.95),avoidanceP95ms=Q(avoidance,.95),navigationP95ms=Q(navigation,.95),projectileP95ms=Q(projectiles,.95),simulationP95ms=Q(simulation,.95),rasterizations=CreatureRenderQueue.TotalRenders-rasters,uploads=CreatureRenderQueue.TotalUploads-uploads,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocation,managedStart=memory,managedEnd=GC.GetTotalMemory(false),effectPeak,projectilePeak});
            }
            tacticalPerformance=new{renderer=RenderingServer.GetVideoAdapterName(),note="120 normal-time rendered frames per workload; subsystem timings are last simulation tick per frame, AI includes navigation. Software GPU, not physical-phone certification.",samples};
        }
        finally{w.ProfileCombat=false;Engine.TimeScale=oldScale;Persistence.Restore(w,app.Narrative,snapshot);app.RebuildViews();await Frames(app,2);}
    }

    static async System.Threading.Tasks.Task VerifyTelegraphs(Foundation app)
    {
        string snapshot = Persistence.Capture(app.World, app.Narrative);
        double oldScale = Engine.TimeScale;
        try
        {
            Engine.TimeScale = .3;
            var w = app.World;
            var course = GameplayReplay.CreateCourse(); course.Id = "telegraph-visual";
            w.RegionsById[course.Id] = course; w.InitializeContainers(course);
            var p = w.Player;
            var enemy = w.Actors.Values.First(a => a.Profile == "beast");
            p.Region = enemy.Region = course.Id;
            foreach (var other in w.Actors.Values.Where(a => a.Id != p.Id && a.Id != enemy.Id && a.Region == course.Id)) other.Region = "reach";
            enemy.Speed = 0; enemy.Health = enemy.MaxHealth = 1000;
            enemy.State = ActionState.Recovery; enemy.StateRemaining = 100; enemy.Abilities.Clear();
            p.X = 300; p.Y = 300; enemy.X = 328; enemy.Y = 300;
            p.Health = p.MaxHealth = 1000; p.State = ActionState.Idle; p.Statuses.Clear(); p.Cooldowns.Clear(); p.Invulnerable = 0;
            app.RebuildViews(); app.ResumeGame(); await Frames(app, 12);
            var bite = w.FindAbility("bite")!; var cone = w.FindAbility("fixture_sweep")!;
            void Reset(string ability)
            {
                w.Cancel(p); w.Cancel(enemy);
                p.X=300; p.Y=300; p.Health=p.MaxHealth; p.Invulnerable=0; p.Statuses.Clear(); p.Cooldowns.Clear();
                enemy.X=328; enemy.Y=300; enemy.Health=enemy.MaxHealth; enemy.Invulnerable=0; enemy.Statuses.Clear(); enemy.Cooldowns.Clear();
                enemy.Abilities.Clear(); enemy.Abilities.Add(ability);
                app.Views[p.Id].SnapPosition(); app.Views[enemy.Id].SnapPosition();
                app.Views[p.Id].Creature.Motor!.Teleport(new PixelCreatures.Core.Vec3(p.X,0,p.Y),0);
            }
            void Start(string ability)
            {
                if(!w.Activate(enemy.Id, ability, p.Id)) throw new InvalidOperationException("Visual warning failed to activate " + ability);
                enemy.Abilities.Clear();
            }
            Reset("bite"); Start("bite");
            await CaptureRollFrame(app, "telegraph-bite-early");
            await Until(app, () => enemy.State != ActionState.Windup || enemy.StateRemaining <= bite.Windup * .55f, "Bite did not reach mid windup");
            if(!TelegraphView.IsActive(w, enemy, bite)) throw new InvalidOperationException("Bite warning disappeared before impact");
            await CaptureRollFrame(app, "telegraph-bite-middle");
            app.RebuildViews();
            if(!TelegraphView.IsActive(w, enemy, bite)) throw new InvalidOperationException("View rebuild lost live warning");
            await Until(app, () => enemy.State != ActionState.Windup, "Bite did not reach impact");
            if(TelegraphView.IsActive(w, enemy, bite) || p.Health == p.MaxHealth) throw new InvalidOperationException("Bite impact or warning removal failed");
            await CaptureRollFrame(app, "telegraph-bite-impact");

            Reset("bite"); Start("bite");
            await Until(app, () => enemy.StateRemaining <= bite.Windup * .7f, "Interrupt fixture did not wind up");
            await CaptureRollFrame(app, "telegraph-interrupt-before");
            w.Damage(p, enemy, 5, "physical");
            if(TelegraphView.IsActive(w, enemy, bite)) throw new InvalidOperationException("Interrupted warning persisted");
            await Frames(app, 2); await CaptureRollFrame(app, "telegraph-interrupt-after");
            await Frames(app, 90);
            if(p.Health != p.MaxHealth) throw new InvalidOperationException("Interrupted visual bite hit late");

            Reset("fixture_sweep"); Start("fixture_sweep");
            await Until(app, () => enemy.StateRemaining <= cone.Windup * .6f, "Cone did not reach mid windup");
            if(!TelegraphView.IsActive(w, enemy, cone)) throw new InvalidOperationException("Cone warning missing");
            await CaptureRollFrame(app, "telegraph-cone-middle");
            p.X=enemy.X; p.Y=enemy.Y+45; app.Views[p.Id].SnapPosition();
            await CaptureRollFrame(app, "telegraph-cone-outside");
            await Until(app, () => enemy.State != ActionState.Windup, "Cone did not reach impact");
            if(p.Health != p.MaxHealth || TelegraphView.IsActive(w, enemy, cone)) throw new InvalidOperationException("Cone hit outside its committed geometry");
            await CaptureRollFrame(app, "telegraph-cone-impact");

            Reset("bite"); Start("bite");
            await Until(app, () => enemy.StateRemaining <= bite.Windup * .45f, "Dodge fixture did not reach input boundary");
            if(app.Ui.MobileLayout)
            {
                var button=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith("Dodge") && b.IsVisibleInTree());
                var point=button.GetGlobalRect().GetCenter();
                Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=point});
                Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=point});
            }
            else
            {
                Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.Space,Pressed=true});
                Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.Space,Pressed=false});
            }
            Input.FlushBufferedEvents();
            await Until(app, () => p.State == ActionState.Dodging && p.ActionElapsed >= .08f, "Production dodge input did not start");
            await CaptureRollFrame(app, "telegraph-dodge-approach");
            await Until(app, () => enemy.State != ActionState.Windup, "Dodge fixture did not reach impact");
            if(p.Health != p.MaxHealth) throw new InvalidOperationException("Incoming bite damaged player during dodge immunity");
            await CaptureRollFrame(app, "telegraph-dodge-avoided");

            // Same four visible creatures and camera, first idle and then in
            // real bite windups. Slow simulation preserves the short warnings
            // across the comparable wall-clock frame samples.
            w.Cancel(p); p.X=300; p.Y=300; p.Health=p.MaxHealth; p.Invulnerable=0;
            var attackers=w.Actors.Values.Where(a=>a.Profile=="beast").Take(4).ToArray();
            if(attackers.Length!=4) throw new InvalidOperationException("Missing multiple-warning fixture actors");
            var offsets=new[] { new System.Numerics.Vector2(28,0), new System.Numerics.Vector2(-28,0), new System.Numerics.Vector2(0,28), new System.Numerics.Vector2(0,-28) };
            for(int i=0;i<attackers.Length;i++)
            {
                var a=attackers[i]; w.Cancel(a); a.Region=course.Id; a.X=p.X+offsets[i].X; a.Y=p.Y+offsets[i].Y;
                a.Health=a.MaxHealth=1000; a.Invulnerable=0; a.Speed=0; a.Statuses.Clear(); a.Cooldowns.Clear();
                a.Abilities.Clear(); a.State=ActionState.Recovery; a.StateRemaining=100;
            }
            Engine.TimeScale=.001;
            app.RebuildViews(); await Frames(app,30);
            async System.Threading.Tasks.Task<(double mean, double p95, long memory)> MeasureFrames()
            {
                var samples=new List<double>(); var clock=Stopwatch.StartNew(); double last=clock.Elapsed.TotalMilliseconds;
                for(int i=0;i<90;i++) { await Frames(app,1); double now=clock.Elapsed.TotalMilliseconds; samples.Add(now-last); last=now; }
                samples.Sort(); return (samples.Average(),samples[(int)(samples.Count*.95)],GC.GetTotalMemory(false));
            }
            var baseline=await MeasureFrames();
            foreach(var a in attackers)
            {
                a.State=ActionState.Idle; a.StateRemaining=0; a.Abilities.Add("bite");
                if(!w.Activate(a.Id,"bite",p.Id)) throw new InvalidOperationException("Multiple-warning activation failed");
                a.Abilities.Clear();
            }
            await Frames(app,10); await CaptureRollFrame(app,"telegraph-multiple");
            if(attackers.Count(a=>TelegraphView.IsActive(w,a,bite))!=4) throw new InvalidOperationException("Multiple warnings did not remain active");
            var active=await MeasureFrames();
            telegraphPerformance=new { renderer=RenderingServer.GetVideoAdapterName(), actors=4, samples=90,
                idleMeanMs=baseline.mean, idleP95Ms=baseline.p95, windupMeanMs=active.mean, windupP95Ms=active.p95,
                idleManagedBytes=baseline.memory, windupManagedBytes=active.memory,
                note="Same visible actors; active sample includes PPC action animation and four warnings. Software-renderer measurement, not a phone benchmark." };

            // Normal engine time is essential here: at the earlier .001 scale
            // the PPC fixed clock barely advances, so that sample alone cannot
            // measure the cost of the adaptive combat pose rate.
            Engine.TimeScale=1;
            foreach(var a in attackers) { w.Cancel(a); a.Abilities.Clear(); a.State=ActionState.Recovery; a.StateRemaining=100; }
            w.Cancel(p); p.X=300; p.Y=300; p.Health=p.MaxHealth=1000; p.Invulnerable=1000;
            app.RebuildViews(); await Frames(app,20);
            async System.Threading.Tasks.Task<object> Profile(string label, Action<int> drive)
            {
                var times=new List<double>(120); var rasters=new List<double>(120); var uploads=new List<double>(120);
                long renderStart=CreatureRenderQueue.TotalRenders, uploadStart=CreatureRenderQueue.TotalUploads;
                long allocated=GC.GetAllocatedBytesForCurrentThread(); long memory=GC.GetTotalMemory(false);
                int gcStart=GC.CollectionCount(0), growthStart=attackers.Sum(a=>app.Views[a.Id].Creature.CanvasGrowths);
                var spikes=new List<(double frame, double drive, double raster, double upload, int effects)>(120);
                int peakEffects=0; var clock=Stopwatch.StartNew(); double last=clock.Elapsed.TotalMilliseconds;
                long previousRenders=renderStart;
                for(int i=0;i<120;i++)
                {
                    double driveStart=clock.Elapsed.TotalMilliseconds;
                    drive(i); double driveMs=clock.Elapsed.TotalMilliseconds-driveStart;
                    await Frames(app,1);
                    double now=clock.Elapsed.TotalMilliseconds; times.Add(now-last); last=now;
                    double rasterMs=0, uploadMs=0;
                    if(CreatureRenderQueue.TotalRenders!=previousRenders)
                    {
                        rasters.Add(CreatureRenderQueue.LastRasterMs); uploads.Add(CreatureRenderQueue.LastUploadMs);
                        rasterMs=CreatureRenderQueue.LastRasterMs; uploadMs=CreatureRenderQueue.LastUploadMs;
                        previousRenders=CreatureRenderQueue.TotalRenders;
                    }
                    peakEffects=Math.Max(peakEffects,app.CombatEffects.ActiveCount);
                    spikes.Add((times[^1],driveMs,rasterMs,uploadMs,app.CombatEffects.ActiveCount));
                }
                times.Sort(); rasters.Sort(); uploads.Sort();
                double Q(List<double> values,double q) => values.Count==0 ? 0 : values[Math.Min(values.Count-1,(int)(values.Count*q))];
                return new { scenario=label, frames=times.Count, p50ms=Q(times,.50), p95ms=Q(times,.95), p99ms=Q(times,.99), meanMs=times.Average(),
                    rasterBatches=rasters.Count, rasterP50Ms=Q(rasters,.5), rasterP95Ms=Q(rasters,.95), uploadP95Ms=Q(uploads,.95),
                    rasterizations=CreatureRenderQueue.TotalRenders-renderStart, uploads=CreatureRenderQueue.TotalUploads-uploadStart,
                    allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated, managedStart=memory, managedEnd=GC.GetTotalMemory(false), peakEffects,
                    gen0Collections=GC.CollectionCount(0)-gcStart, canvasGrowths=attackers.Sum(a=>app.Views[a.Id].Creature.CanvasGrowths)-growthStart,
                    worstFrames=spikes.OrderByDescending(x=>x.frame).Take(5).Select(x=>new { frameMs=x.frame, driveMs=x.drive, rasterMs=x.raster, uploadMs=x.upload, effects=x.effects }).ToArray() };
            }
            void Prepare()
            {
                for(int index=0; index<attackers.Length; index++)
                {
                    var a=attackers[index];
                    w.Cancel(a); a.Abilities.Clear(); a.State=ActionState.Recovery; a.StateRemaining=100;
                    a.Health=a.MaxHealth=1000; a.Invulnerable=0;
                    a.X=p.X+offsets[index].X; a.Y=p.Y+offsets[index].Y; app.Views[a.Id].SnapPosition();
                }
                app.CombatPresentation?.Reset();
            }
            void StartBite(ActorState a)
            {
                if(a.State is not (ActionState.Idle or ActionState.Moving)) return;
                a.Cooldowns.Clear(); a.Abilities.Add("bite");
                if(!w.Activate(a.Id,"bite",p.Id)) throw new InvalidOperationException("Performance bite could not start");
                a.Abilities.Clear();
            }
            void Strike(ActorState a)
            {
                a.Health=1000; a.Invulnerable=0;
                if(!w.Damage(p,a,2,"physical",ability:"slash")) throw new InvalidOperationException("Performance hit could not resolve");
            }
            var scenarios=new List<object>();
            Prepare(); scenarios.Add(await Profile("idle",_=>{}));
            Prepare(); attackers[0].State=ActionState.Idle;
            scenarios.Add(await Profile("one attack",_=>StartBite(attackers[0])));
            Prepare(); scenarios.Add(await Profile("one hit reaction",i=>{ if(i%30==0) Strike(attackers[0]); }));
            Prepare(); foreach(var a in attackers) a.State=ActionState.Idle;
            scenarios.Add(await Profile("four attackers",_=>{ foreach(var a in attackers) StartBite(a); }));
            Prepare(); scenarios.Add(await Profile("four simultaneous hits",i=>{ if(i%30==0) foreach(var a in attackers) Strike(a); }));
            combatPerformance=new { renderer=RenderingServer.GetVideoAdapterName(), samplesPerScenario=120,
                note="Normal simulation time, identical four-creature fixture. Wall-frame samples include renderer and host scheduling; not phone performance.", scenarios };

            Reset("bite"); Start("bite");
            if(!TelegraphView.IsActive(w, enemy, bite)) throw new InvalidOperationException("Reset fixture has no warning");
            app.Reset();
            var layers=app.GetChildren().OfType<TelegraphView>().ToArray();
            if(layers.Length!=1 || !ReferenceEquals(layers[0].World,app.World))
                throw new InvalidOperationException("World reset retained a stale telegraph layer");
        }
        finally
        {
            Engine.TimeScale = oldScale;
            Persistence.Restore(app.World, app.Narrative, snapshot); app.RebuildViews();
            await Frames(app, 2);
        }
    }

    // Exercise the real camera and damage bridge under severe render stalls.
    // A variable-delta camera can extrapolate its smoothed center far past the
    // target when several hits move the player during a slow Android frame.
    static async System.Threading.Tasks.Task VerifyCameraHitches(Foundation app)
    {
        string snapshot=Persistence.Capture(app.World,app.Narrative);
        try
        {
            var w=app.World; var course=GameplayReplay.CreateCourse(); course.Id="camera-hitch-visual";
            w.RegionsById[course.Id]=course; w.InitializeContainers(course);
            var p=w.Player; var enemy=w.Actors.Values.First(a=>a.Profile=="beast");
            foreach(var other in w.Actors.Values.Where(a=>a.Id!=p.Id && a.Id!=enemy.Id)) other.Region="reach";
            w.Cancel(p); w.Cancel(enemy);
            // Away from the camera's edge clamps, so knockback moves its target.
            p.Region=enemy.Region=course.Id; p.X=600; p.Y=570; p.Health=p.MaxHealth=1000;
            p.Invulnerable=0; p.Statuses.Clear(); p.Cooldowns.Clear();
            enemy.X=628; enemy.Y=570; enemy.Speed=0; enemy.Abilities.Clear();
            enemy.State=ActionState.Recovery; enemy.StateRemaining=100;
            app.RebuildViews(); app.ResumeGame(); await Frames(app, 12);
            app.Camera.ResetSmoothing(); await Frames(app, 2);
            float maxError=0;
            for(int i=0;i<8;i++)
            {
                p.Invulnerable=0;
                if(!w.Damage(enemy,p,2,"physical",ability:"bite")) throw new InvalidOperationException("Camera hitch damage did not resolve");
                System.Threading.Thread.Sleep(230); // intentional test-only render stall
                await Frames(app,1);
                maxError=Math.Max(maxError,app.Camera.GetScreenCenterPosition().DistanceTo(app.Camera.Position));
            }
            await Frames(app,60);
            float settled=app.Camera.GetScreenCenterPosition().DistanceTo(app.Camera.Position);
            cameraHitch=new { stalledFrames=8, stallMs=230, maxCenterError=maxError, settledCenterError=settled,
                callback=app.Camera.ProcessCallback.ToString() };
            if(maxError>120 || settled>3) throw new InvalidOperationException($"Camera smoothing escaped its follow target under hitches: peak {maxError:0.0}, settled {settled:0.0}");
            await CaptureRollFrame(app,"combat-camera-hitch-recovered");
        }
        finally
        {
            Persistence.Restore(app.World,app.Narrative,snapshot); app.RebuildViews();
            await Frames(app,2);
        }
    }

    static async System.Threading.Tasks.Task VerifyDefeat(Foundation app)
    {
        string snapshot=Persistence.Capture(app.World,app.Narrative);
        try
        {
            var w=app.World; var course=GameplayReplay.CreateCourse(); course.Id="defeat-visual";
            w.RegionsById[course.Id]=course; w.InitializeContainers(course);
            var p=w.Player; var enemy=w.Actors.Values.First(a=>a.Profile=="beast");
            foreach(var other in w.Actors.Values.Where(a=>a.Id!=p.Id && a.Id!=enemy.Id)) other.Region="reach";
            w.Cancel(p); w.Cancel(enemy);
            p.Region=enemy.Region=course.Id; p.X=300; p.Y=300; p.Health=p.MaxHealth=1000; p.Invulnerable=0; p.Cooldowns.Clear();
            enemy.X=336; enemy.Y=300; enemy.Health=enemy.MaxHealth=1; enemy.Invulnerable=0; enemy.Speed=0;
            enemy.Abilities.Clear(); enemy.State=ActionState.Recovery; enemy.StateRemaining=100;
            w.Items.Equip(p,"iron_sword","weapon"); w.Items.Add(enemy.Id,"potion",1);
            app.RebuildViews(); app.ResumeGame();
            app.Views[p.Id].Creature.Motor!.Teleport(new PixelCreatures.Core.Vec3(p.X,0,p.Y),0);
            await Frames(app,12); app.Cast("slash");
            await Until(app,()=>!enemy.Alive,"Fatal visual strike did not resolve");
            await CaptureRollFrame(app,"combat-fatal-impact");
            await Until(app,()=>app.Views[enemy.Id].Creature.Motor!.RestProgress>.45 && app.Views[enemy.Id].LootMarkerVisible,
                "Defeat posture or loot marker did not appear");
            await CaptureRollFrame(app,"combat-fatal-corpse");
        }
        finally
        {
            Persistence.Restore(app.World,app.Narrative,snapshot); app.RebuildViews();
            await Frames(app,2);
        }
    }

    public static async System.Threading.Tasks.Task Run(Foundation app)
    {
        await Frames(app, 30);
        if (!app.Ui.MobileLayout)
        {
            // The dummy audio driver proves resource construction, not how the
            // cues sound. Publish actual synthesized WAVs as CI artifacts so
            // human review can compare bite, whiff, impact and death cues.
            string audioDirectory = ProjectSettings.GlobalizePath("res://docs/audio-samples");
            app.Audio.Call("export_effects", audioDirectory);
            foreach (string cue in new[] { "swing", "bite", "whiff", "hit", "heavy_hit",
                "player_hit", "dodge", "avoid", "release", "projectile_impact", "death" })
            {
                var sample = Path.Combine(audioDirectory, cue + ".wav");
                if (!File.Exists(sample) || new FileInfo(sample).Length < 256)
                    throw new InvalidOperationException("Missing synthesized combat audio sample: " + cue);
            }
        }
        await Capture(app, "menu");
        if (app.Ui.MobileLayout)
        {
            var scroller=app.Ui.FindChildren("*","ScrollContainer",true,false).OfType<ScrollContainer>().First(s=>s.IsVisibleInTree());
            var start=scroller.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=true,Position=start});
            await Frames(app,1);
            for(int i=1;i<=6;i++)
            {
                Input.ParseInputEvent(new InputEventScreenDrag {Index=9,Position=start-new Vector2(0,i*30),Relative=new Vector2(0,-30)});
                await Frames(app,1);
            }
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=false,Position=start-new Vector2(0,180)});
            await Frames(app,2);
            if(scroller.ScrollVertical<60 || !app.Ui.MenuOpen)throw new InvalidOperationException("Menu swipe over button rows did not scroll safely");
            scroller.ScrollVertical=0;
            await Frames(app,3);
            var enter=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Enter / Resume");
            var enterPoint=enter.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=true,Position=enterPoint});
            Input.ParseInputEvent(new InputEventScreenTouch {Index=9,Pressed=false,Canceled=true,Position=enterPoint});
            await Frames(app,2);
            if(!app.Ui.MenuOpen)throw new InvalidOperationException("Canceled menu touch activated Enter");
        }
        app.Ui.Menu();
        await Frames(app,2);
        if(app.Ui.MobileLayout)
        {
            // Menu sits above the primary action cluster. Its upper edge must still
            // suppress the OS-synthesized mouse pair after the owned touch.
            var menuButton=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Menu" && b.IsVisibleInTree());
            var menuPoint=menuButton.GlobalPosition+new Vector2(menuButton.Size.X/2,5);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=menuPoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=true,Position=menuPoint,GlobalPosition=menuPoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=menuPoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=false,Position=menuPoint,GlobalPosition=menuPoint});
            await Frames(app,2);
            if(!app.Ui.MenuOpen)throw new InvalidOperationException("Menu edge touch plus emulated mouse did not open exactly once");
            app.Ui.Menu();await Frames(app,2);
        }
        if (app.Ui.MobileLayout)
        {
            // Exercise action ownership through the actual input pipeline while a
            // different finger owns the stick, including canceled and dragged touches.
            string snapshot=Persistence.Capture(app.World,app.Narrative);
            var mobilePlayer=app.World.Player;
            var dodge=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Dodge" && b.IsVisibleInTree());
            var attack=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="Attack" && b.IsVisibleInTree());
            var dodgePoint=dodge.GetGlobalRect().GetCenter();
            var dodgeSize=dodge.Size;
            int starts=0;
            void OnCombat(string kind, System.Numerics.Vector2 direction, string context) { if(kind=="dodge_started") starts++; }
            app.World.CombatDiagnostic += OnCombat;
            var mobileStick=app.Ui.Stick;
            var stickPoint=mobileStick.GlobalPosition+mobileStick.Size/2;
            Input.ParseInputEvent(new InputEventScreenTouch {Index=7,Pressed=true,Position=stickPoint});
            Input.ParseInputEvent(new InputEventScreenDrag {Index=7,Position=stickPoint+new Vector2(72,0)});
            await Frames(app,2);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=dodgePoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Canceled=true,Position=dodgePoint});
            await Frames(app,2);
            if(starts!=0) throw new InvalidOperationException("Canceled Dodge touch activated");
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=dodgePoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenDrag {Index=8,Position=dodgePoint+new Vector2(-180,-180)});
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=dodgePoint+new Vector2(-180,-180)});
            await Frames(app,2);
            if(starts!=0) throw new InvalidOperationException("Dodge touch released outside activated");
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=true,Position=dodgePoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=true,Position=dodgePoint,GlobalPosition=dodgePoint});
            await Frames(app,1);
            Input.ParseInputEvent(new InputEventScreenTouch {Index=8,Pressed=false,Position=dodgePoint});
            Input.ParseInputEvent(new InputEventMouseButton {Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=false,Position=dodgePoint,GlobalPosition=dodgePoint});
            await Frames(app,2);
            if(starts!=1 || mobilePlayer.State!=ActionState.Dodging || mobilePlayer.ActionDirectionX<.99f || mobileStick.Finger!=7)
                throw new InvalidOperationException("Dodge multitouch, direction or emulated mouse ownership failed");
            if(dodge.Size!=dodgeSize) throw new InvalidOperationException("Dodge cooldown changed button dimensions");
            app.World.CancelPlayerDodge();
            Input.ParseInputEvent(new InputEventScreenTouch {Index=7,Pressed=false,Position=stickPoint});
            app.World.CombatDiagnostic -= OnCombat;
            Persistence.Restore(app.World,app.Narrative,snapshot); app.RebuildViews();
            await Frames(app,2);
            var mobileButtons=dodge.GetParent().GetChildren().OfType<Button>().Where(b=>b.IsVisibleInTree()).ToArray();
            for(int i=0;i<mobileButtons.Length;i++) for(int j=i+1;j<mobileButtons.Length;j++)
                if(mobileButtons[i].GetGlobalRect().Intersects(mobileButtons[j].GetGlobalRect()))
                    throw new InvalidOperationException("Mobile touch targets overlap: "+mobileButtons[i].Text+" / "+mobileButtons[j].Text);
            if(attack.GetGlobalRect().Intersects(dodge.GetGlobalRect())) throw new InvalidOperationException("Attack and Dodge touch targets overlap");
        }
        if (!app.Ui.MobileLayout)
        {
            // Verify the desktop binding via the normal unhandled-input route.
            string snapshot=Persistence.Capture(app.World,app.Narrative);
            var p=app.World.Player;
            p.Cooldowns.Remove("dodge");
            var focused=app.Ui.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.IsVisibleInTree() && b.Text.Contains("Draught"));
            int potionsBefore=app.World.Items.Count("player","potion");
            focused.GrabFocus();
            int starts=0;
            void OnDodge(string kind, System.Numerics.Vector2 direction, string context) { if(kind=="dodge_started") starts++; }
            app.World.CombatDiagnostic += OnDodge;
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.Space,Pressed=true});
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.Space,Pressed=false});
            await Frames(app,2);
            if(starts!=1 || p.State!=ActionState.Dodging || app.World.Items.Count("player","potion")!=potionsBefore)
                throw new InvalidOperationException("Focused hotbar button consumed Space or Space activated more than one action");
            focused.ReleaseFocus();
            app.World.CancelPlayerDodge(); app.World.CombatDiagnostic -= OnDodge;
            Persistence.Restore(app.World,app.Narrative,snapshot); app.RebuildViews();
            await Frames(app,2);
        }
        // Use real GUI dispatch for desktop mice; headless checks exercise raw touch.
        var draught=app.Ui.GetChildren().OfType<Control>().First().FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.Contains("Draught"));
        var point=draught.GetGlobalRect().GetCenter();
        int potions=app.World.Items.Count("player","potion");
        Input.ParseInputEvent(new InputEventMouseButton { Device=0,ButtonIndex=MouseButton.Left,Pressed=true,Position=point,GlobalPosition=point });
        await Frames(app,1);
        Input.ParseInputEvent(new InputEventMouseButton { Device=0,ButtonIndex=MouseButton.Left,Pressed=false,Position=point,GlobalPosition=point });
        await Frames(app,2);
        if(app.World.Items.Count("player","potion")!=potions-1) throw new InvalidOperationException("Real desktop mouse hotbar dispatch failed");
        app.World.Items.Add("player","potion");
        // Input flows through Godot's actual action mapping and physics process.
        float before = app.World.Player.X;
        Input.ActionPress("east");
        await Frames(app, 30);
        Input.ActionRelease("east");
        if (app.World.Player.X <= before)
            throw new InvalidOperationException("Visual movement input did not move player");
        await Capture(app, "exploration");
        // Document takeoff, travel, landing and lingering dust at normal camera
        // zoom. Previously 'Active' tests passed even when pixels were invisible.
        var dustSnapshot = Persistence.Capture(app.World, app.Narrative);
        try
        {
            app.World.Player.Cooldowns.Clear();
            if (!app.World.TryStartPlayerDodge(new System.Numerics.Vector2(1, 0)))
                throw new InvalidOperationException("Visual roll dust check could not start dodge");
            await AwaitDodgeProgress(app, .05f);
            await CaptureRollFrame(app, "roll-dust-takeoff");
            await AwaitDodgeProgress(app, .19f);
            await CaptureRollFrame(app, "roll-dust-trail");
            for(int i=0;i<120 && app.World.Player.State==ActionState.Dodging;i++)
                await Frames(app,1);
            if(app.World.Player.State==ActionState.Dodging)
                throw new InvalidOperationException("Dodge did not reach landing");
            // Check the real landing cloud exists before claiming visual coverage.
            if(app.Views["player"].RollDust is not { Active:true, HasLandingCloud:true })
                throw new InvalidOperationException("Landing dust is not visible after roll completion");
            await CaptureRollFrame(app, "roll-dust-landing");
            await Frames(app,2);
            if(app.Views["player"].RollDust is not { Active:true })
                throw new InvalidOperationException("Landing cloud did not linger after dodge");
            await CaptureRollFrame(app, "roll-dust-linger");
        }
        finally
        {
            Persistence.Restore(app.World, app.Narrative, dustSnapshot);
            app.RebuildViews();
            await Frames(app, 2);
        }
        await VerifyTelegraphs(app);
        await VerifyTactics(app);
        await VerifyCameraHitches(app);
        await VerifyDefeat(app);
        var player=app.World.Player;
        var region=app.World.RegionsById[player.Region];
        int chest=region.Features.Single(x=>x.Value=="chest").Key;
        var chestPos=region.Center(chest);
        // Walk to the chest using the same screen-space input conversion as touch.
        for(int i=0;i<240 && System.Numerics.Vector2.Distance(player.Position,chestPos)>28;i++)
        {
            var d=chestPos-player.Position;
            app.TouchMovement=PixelCreatures.Runtime.GodotConvert.GroundToScreen(d.X,d.Y).Normalized();
            await Frames(app,1);
        }
        app.Ui.Stick.Cancel();
        await Capture(app,"chest");
        app.Interact();
        if(!region.Depleted.Contains(chest)) throw new InvalidOperationException("Visual chest walk/loot failed");
        await Capture(app,"opened-chest");
        app.CollisionDebug=true;await Capture(app,"collision-overlay");app.CollisionDebug=false;
        app.Ui.Inventory();
        await Capture(app, "inventory");
        app.Ui.ClosePanels();
        app.Ui.Character();
        await Capture(app, "character");
        app.Ui.ClosePanels();
        app.Narrative.Call("talk");
        await Capture(app, "dialogue");
        // Exercise the real Dialogue Manager choice mutation, not a simulated quest acceptance.
        var choices = app.Narrative.Get("choices").AsGodotObject() as VBoxContainer ?? throw new InvalidOperationException("Missing dialogue UI");
        if (choices.GetChildren().OfType<Button>().Any(x => x.Text.Contains("[if")))
            throw new InvalidOperationException("Dialogue conditions rendered as text");
        var locked = choices.GetChildren().OfType<Button>().FirstOrDefault(x => x.Text.Contains("buried"));
        if (locked != null && !locked.Disabled)
            throw new InvalidOperationException("Dialogue prerequisite option enabled");
        var response = choices.GetChildren().OfType<Button>().First(x => x.Text.Contains("prowlers"));
        response.EmitSignal(Button.SignalName.Pressed);
        await Frames(app);
        if (!app.Narrative.Get("flags").AsGodotDictionary().ContainsKey("accepted_1"))
            throw new InvalidOperationException("Dialogue choice failed to start quest");
        // Finish the current line through its actual Continue button.
        foreach (var button in choices.GetChildren().OfType<Button>().ToArray())
            button.EmitSignal(Button.SignalName.Pressed);
        await Frames(app);
        app.Ui.Journal();
        await Capture(app, "journal");
        app.Ui.ClosePanels();
        app.World.Enter(app.World.Player, "ruin");
        await Capture(app, "dungeon");
        app.World.Enter(app.World.Player, "reach");
        if (app.Ui.MobileLayout) await Capture(app, "controls");
        app.Ui.Workshop();
        await Capture(app, "workshop");
        app.Ui.ClosePanels();
        app.Ui.FeelLab();
        await Capture(app,"feel-lab");
        app.Ui.ClosePanels();
        var frames = new List<double>();
        var timer = Stopwatch.StartNew();
        double last = timer.Elapsed.TotalMilliseconds;
        for (int i = 0; i < 180; i++)
        {
            await Frames(app, 1);
            double now = timer.Elapsed.TotalMilliseconds;
            frames.Add(now - last);
            last = now;
        }

        frames.Sort();
        var data = new
        {
            frames = frames.Count,
            p50ms = frames[frames.Count / 2],
            p95ms = frames[(int)(frames.Count * .95)],
            p99ms = frames[(int)(frames.Count * .99)],
            averageMs = frames.Average(),
            renderer = RenderingServer.GetVideoAdapterName(),
            actors = app.Views.Count,
            visibleActors = app.Views.Values.Count(v => v.Visible),
            totalRasterizations = app.Views.Values.Sum(v => v.Creature.RenderCount),
            managedBytes = GC.GetTotalMemory(false),
            cameraHitch,
            telegraphPerformance,
            combatPerformance,
            tacticalPerformance
        };
        File.WriteAllText(app.Ui.MobileLayout ? "docs/render-verification-mobile.json" : "docs/render-verification.json", JsonSerializer.Serialize(data, Content.Json));
        GD.Print("VISUAL CHECKS PASSED");
    }
}
