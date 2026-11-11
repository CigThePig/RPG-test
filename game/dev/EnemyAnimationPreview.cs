using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Runtime;

namespace Mosslight;

/// <summary>
/// Contact sheets of every enemy posture, reaction and attack, rendered by the production
/// motor and rasterizer with no scene or display. For looking at animation while authoring it:
/// <c>godot --headless --path . -- --enemy-animation-sheets</c> writes build/enemy-animation/.
/// It asserts nothing.
/// </summary>
public static class EnemyAnimationPreview
{
    /// <summary>One strip: how to set a fresh motor going, and how long to watch it.</summary>
    public sealed record Clip(string Name, double Seconds, Action<CreatureMotor> Start, Action<CreatureMotor, double>? Drive = null, double Lead = 0.5);

    static Action<CreatureMotor> Attack(string ability) => m =>
    {
        var def = Content.Load(true).Abilities.Single(a => a.Id == ability);
        m.Stance = StanceKind.Combat; m.SnapStance(StanceKind.Combat);
        Enum.TryParse<ActionStyle>(def.ActionStyle, out var style);
        m.TriggerGameplayAction(def.Windup, def.Charge != null ? .15 : def.Recovery, def.ActionStyle == "" ? null : style);
    };

    static double Total(string ability)
    {
        var def = Content.Load(true).Abilities.Single(a => a.Id == ability);
        return def.Windup + def.Recovery;
    }

    public static IReadOnlyList<Clip> Clips { get; } = new Clip[]
    {
        new("relaxed", 1.6, _ => { }),
        new("wary", 1.2, m => m.Stance = StanceKind.Wary, Lead: 0),
        new("startle", .6, m => { m.Stance = StanceKind.Wary; m.TriggerReaction(ReactionKind.Startle); }, Lead: 0),
        new("bark", .6, m => { m.Stance = StanceKind.Combat; m.TriggerGameplayAction(.25, .25, ActionStyle.Roar); }, Lead: 0),
        new("combat", 1.6, m => m.SnapStance(StanceKind.Combat)),
        new("strafe", 1.2, m => m.SnapStance(StanceKind.Combat), (m, t) => m.SetWorldPosition(new Vec3(0, 0, 36 * t), new Vec3(0, 0, 36))),
        new("search", 2.4, m => m.SnapStance(StanceKind.Search)),
        new("flee", 1.0, m => m.SnapStance(StanceKind.Flee), (m, t) => m.SetWorldPosition(new Vec3(80 * t, 0, 0), new Vec3(80, 0, 0))),
        new("enraged", 1.2, m => m.SnapStance(StanceKind.Enraged)),
        new("bite", Total("bite"), Attack("bite"), Lead: .2),
        new("pounce", Total("pounce"), Attack("pounce"), Lead: .2),
        new("slam", Total("slam"), Attack("slam"), Lead: .2),
        new("charge", Total("charge"), Attack("charge"), (m, t) =>
        {
            var def = Content.Load(true).Abilities.Single(a => a.Id == "charge");
            double run = Math.Clamp(t - def.Windup, 0, def.Charge!.Duration);
            bool running = t >= def.Windup && t < def.Windup + def.Charge.Duration;
            m.Stance = running ? StanceKind.Charge : StanceKind.Combat;
            m.SetWorldPosition(new Vec3(run * def.Charge.Speed, 0, 0), new Vec3(running ? def.Charge.Speed : 0, 0, 0));
        }, Lead: .2),
        new("crash", 1.2, m => { m.SnapStance(StanceKind.Combat); m.TriggerReaction(ReactionKind.Crash); }, Lead: .1),
        new("sting", Total("sting"), Attack("sting"), Lead: .2),
        new("gust", Total("gust"), Attack("gust"), Lead: .2),
        new("spit", Total("spit"), Attack("spit"), Lead: .2),
        new("screech", Total("screech"), Attack("screech"), Lead: .2),
        new("hit", .5, m => { m.SnapStance(StanceKind.Combat); m.TriggerHit(new Vec3(1, 0, .3)); }, Lead: .1),
        new("guard break", .8, m => { m.SnapStance(StanceKind.Combat); m.TriggerHit(new Vec3(1, 0, 0), 1.4); m.TriggerReaction(ReactionKind.Reel); }, Lead: .1),
        new("evade", CombatTuning.EvadeDuration + .15, m => m.SnapStance(StanceKind.Combat), (m, t) =>
        {
            double q = t / CombatTuning.EvadeDuration;
            m.DodgeProgress = q < 1 ? q : null; m.DodgeForwardComponent = -.6; m.DodgeSideComponent = .8;
            double travel = 42 * (1 - (1 - Math.Min(1, q)) * (1 - Math.Min(1, q)));
            m.SetWorldPosition(new Vec3(-.6 * travel, 0, .8 * travel), Vec3.Zero);
        }, Lead: .1),
        new("dormant", 1.0, m => m.SnapRest(true), Lead: 0),
        new("wake", 1.1, m => { m.SnapRest(true); m.Resting = false; m.Stance = StanceKind.Combat; m.TriggerReaction(ReactionKind.Startle); }, Lead: 0),
        new("death", 1.5, m => { m.SnapStance(StanceKind.Combat); m.TriggerHit(new Vec3(1, 0, .4)); m.Die(new Vec3(1, 0, .4)); }, Lead: .1),
    };

    public static readonly (string Label, string Family, ulong Seed)[] Subjects =
    {
        ("prowler", "quadruped", 42), ("brute", "reptile", 43), ("spitter", "amorphous", 44),
        ("stalker", "arthropod", 46), ("howler", "winged", 47), ("wayfarer", "biped", 42), ("serpent", "serpent", 5), ("swimmer", "aquatic", 9),
    };

    /// <summary>Simulate one clip and return the rasterized frames, evenly spaced.</summary>
    public static List<(PixelFrame Frame, double Flash, CreaturePose Pose)> Capture(CreatureModel model, Clip clip, int frames, int direction, out CanvasSpec canvas)
    {
        const int margin = 40;
        var source = model.Anatomy.Canvas;
        canvas = new CanvasSpec(source.Width + margin * 2, source.Height + margin * 2, source.OriginX + margin, source.OriginY + margin);
        var motor = new CreatureMotor(model, CreatureRuntime.Registry, new MotorOptions { Facing = FacingMode.Eight, InternalDrive = false, FaceVelocity = false });
        motor.Teleport(Vec3.Zero, PixelCamera.DirectionYaw(direction));
        motor.LockCurrentFacing(PixelCamera.DirectionYaw(direction));
        motor.SetWorldPosition(Vec3.Zero, Vec3.Zero);
        for (int i = 0; i < (int)(clip.Lead * 60); i++) motor.Step(FixedClock.DefaultStep);
        clip.Start(motor);
        var renderer = new CreatureRenderer();
        var result = new List<(PixelFrame, double, CreaturePose)>();
        int steps = Math.Max(frames, (int)Math.Round(clip.Seconds * 60)), taken = 0;
        var origin = motor.Position;
        for (int f = 0; f < frames; f++)
        {
            int until = (int)Math.Round((f + 1) / (double)frames * steps);
            for (; taken < until; taken++)
            {
                clip.Drive?.Invoke(motor, taken / 60.0);
                motor.Step(FixedClock.DefaultStep);
            }
            var frame = new PixelFrame(canvas.Width, canvas.Height) { OriginX = canvas.OriginX, OriginY = canvas.OriginY };
            var view = ViewSpec.Default(motor.Yaw);
            view.Quality = 1;
            var pose = new CreaturePose(model.Anatomy.Skeleton);
            pose.CopyFrom(motor.Pose);
            renderer.Render(model.Anatomy, model.Palette, model.Surface, pose, view, frame);
            result.Add((frame, motor.HitFlash, pose));
        }
        return result;
    }

    public static void Write(string directory, int frames = 12, int scale = 2, string? only = null)
    {
        Directory.CreateDirectory(directory);
        var ink = new Rgba(230, 226, 205, 255); var back = new Rgba(38, 44, 40, 255); var rule = new Rgba(62, 70, 62, 255);
        foreach (var (label, family, seed) in Subjects)
        {
            if (only != null && !only.Split(',').Contains(label)) continue;
            var model = CreatureRuntime.Build(CreatureRuntime.Sample(family, seed));
            var rows = Clips.Select(clip => (clip, frames: Capture(model, clip, frames, 7, out _))).ToArray();
            // Crop every cell to the union of what any frame drew, so sheets stay compact.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
            foreach (var row in rows)
                foreach (var (frame, _, _) in row.frames)
                    if (frame.ContentBounds(out int a, out int b, out int c, out int d)) { x0 = Math.Min(x0, a); y0 = Math.Min(y0, b); x1 = Math.Max(x1, c); y1 = Math.Max(y1, d); }
            int cw = (x1 - x0 + 3) * scale, ch = (y1 - y0 + 3) * scale, gutter = 78;
            var sheet = new ImageCanvas(gutter + cw * frames, 14 + ch * rows.Length, back);
            sheet.Text(4, 4, $"{label}  {family} seed {seed}  -  {frames} frames per row, facing south-east", ink);
            for (int r = 0; r < rows.Length; r++)
            {
                int top = 14 + r * ch;
                sheet.Fill(0, top, sheet.Width, 1, rule);
                sheet.Text(4, top + ch / 2 - 6, rows[r].clip.Name, ink);
                sheet.Text(4, top + ch / 2 + 2, $"{rows[r].clip.Seconds:0.00}s", rule);
                for (int f = 0; f < frames; f++)
                {
                    var (frame, flash, _) = rows[r].frames[f];
                    var lut = new byte[256 * 4];
                    model.Palette.BuildLut(lut, flash);
                    var rgba = new byte[frame.Width * frame.Height * 4];
                    frame.ToRgba(lut, rgba, model.Palette.WaterTint);
                    // Blit only the cropped window.
                    int w = x1 - x0 + 1, h = y1 - y0 + 1;
                    var crop = new byte[w * h * 4];
                    for (int y = 0; y < h; y++) Array.Copy(rgba, ((y0 + y) * frame.Width + x0) * 4, crop, y * w * 4, w * 4);
                    sheet.Blit(crop, w, h, gutter + f * cw + scale, top + scale, scale);
                }
            }
            File.WriteAllBytes(Path.Combine(directory, label + ".png"), sheet.ToPng());
            GD.Print($"ENEMY ANIMATION SHEET {label}: {rows.Length} clips x {frames} frames -> {Path.Combine(directory, label + ".png")}");
        }
    }
}
