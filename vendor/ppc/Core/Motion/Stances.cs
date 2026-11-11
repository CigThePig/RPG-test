// Mosslight addition to Procedural Pixel Creatures - temperament layers on top of locomotion.
//
// Stances are persistent postures (wary, ready to fight, searching, routed, enraged, charging)
// that blend in and out; reactions are short one-shots that play over whatever else the body
// is doing (a startled hop, reeling from a broken guard, the daze after hitting a wall); death
// is a staged collapse that ends in a held pose. All of them speak through PoseParams, so they
// work for every body plan, and all are driven by the motor's own fixed-step clock.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public enum StanceKind
    {
        Relaxed,
        /// <summary>Something caught its attention: frozen, head high, ears forward.</summary>
        Wary,
        /// <summary>Squared up to a foe: low, coiled, tail lashing.</summary>
        Combat,
        /// <summary>Lost its quarry: sweeping the head, sniffing.</summary>
        Search,
        /// <summary>Nerve broken: ears flat, tail tucked.</summary>
        Flee,
        /// <summary>Combat, bristling and shaking with fury.</summary>
        Enraged,
        /// <summary>Head down for a flat-out run.</summary>
        Charge,
    }

    public sealed class StanceLayer
    {
        private static readonly int Count = Enum.GetValues(typeof(StanceKind)).Length;
        private readonly double[] _w = new double[Count];
        public StanceKind Target { get; set; }
        public double Weight(StanceKind kind) => _w[(int)kind];

        public void Step(double dt)
        {
            for (int i = 1; i < Count; i++)
            {
                double goal = i == (int)Target ? 1 : 0;
                // Postures tense quickly and relax a little more slowly.
                _w[i] += (goal - _w[i]) * DMath.DampFactor(goal > _w[i] ? 0.09 : 0.16, dt);
                if (Math.Abs(goal - _w[i]) < 5e-3) _w[i] = goal;
            }
        }

        public void Snap(StanceKind kind)
        {
            Target = kind;
            for (int i = 1; i < Count; i++) _w[i] = i == (int)kind ? 1 : 0;
        }

        public void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            double live = (1 - ctx.RestWeight) * (1 - 0.65 * ctx.ActionWeight) * (1 - ctx.DeathWeight);
            if (live <= 1e-3) return;
            double t = ctx.Time;
            double energy = DMath.Lerp(0.85, 1.2, ctx.Traits.Energy);
            double phase = ctx.Hash(21, 0) * 6.0;
            double L = Math.Max(4, ctx.Anatomy.Metrics.BodyLength);
            double leg = Math.Max(3, ctx.Anatomy.Metrics.LegLength);
            double still = 1 - ctx.MoveWeight;

            double wary = _w[(int)StanceKind.Wary] * live;
            if (wary > 1e-3)
            {
                // Drawn up tall and still, weight back, ready to bolt or spring.
                p.NeckPitch += 0.42 * wary;
                p.HeadPitch += 0.12 * wary;
                p.BodyPitch += 0.07 * wary;
                p.BodyOffset = p.BodyOffset + new Vec3(-0.03 * L * wary, 0.05 * leg * wary, 0);
                p.EarPitch += 0.95 * wary;
                p.TailPitch += 0.5 * wary;
                p.FrontLift += 0.08 * wary;
                p.Breath *= 1 - 0.6 * wary;
                p.WingFold = Math.Min(p.WingFold, 1 - 0.18 * wary);
                if (wary > 0.5 && p.Mood == EyeMood.Neutral) p.Mood = EyeMood.Alert;
            }

            double combat = _w[(int)StanceKind.Combat] * live;
            double rage = _w[(int)StanceKind.Enraged] * live;
            double ready = Math.Min(1, combat + rage);
            if (ready > 1e-3)
            {
                // A slow weight shift from foot to foot while standing ready.
                double bounce = DMath.Sin(DMath.TwoPi * 1.7 * energy * t + phase);
                p.Crouch += (0.24 + 0.05 * bounce * still) * ready;
                p.BodyOffset = p.BodyOffset + new Vec3(0.035 * L * ready, 0, 0);
                p.BodyPitch += -0.05 * ready;
                p.NeckPitch += -0.3 * ready;
                p.HeadPitch += -0.08 * ready;
                p.EarPitch += -0.55 * ready;
                p.Jaw = Math.Max(p.Jaw, (0.16 + 0.06 * DMath.Sin(DMath.TwoPi * 0.8 * t + phase)) * ready);
                p.TailPitch += 0.18 * ready;
                p.TailYaw += DMath.Sin(DMath.TwoPi * 1.25 * energy * t + phase) * 0.42 * ready;
                p.ArmRaise += 0.28 * ready;
                p.ArmReach += 0.12 * ready;
                p.FrontReach += 0.04 * ready;
                p.Grip = Math.Max(p.Grip, 0.35 * ready);
                p.WingFold = Math.Min(p.WingFold, 1 - 0.3 * ready);
                p.TentacleAmp += 0.55 * ready;
                p.Breath *= 1 + 0.35 * ready;
                if (ready > 0.5 && p.Mood is EyeMood.Neutral or EyeMood.Alert) p.Mood = EyeMood.Angry;
            }
            if (rage > 1e-3)
            {
                // Bristling: puffed up and trembling.
                p.Squash += -0.05 * rage;
                p.Crouch += -0.04 * rage;
                p.HeadRoll += DMath.Sin(DMath.TwoPi * 9 * t) * 0.045 * rage;
                p.BodyRoll += DMath.Sin(DMath.TwoPi * 7 * t + 1.3) * 0.018 * rage;
                p.Jaw = Math.Max(p.Jaw, 0.34 * rage);
                p.EarPitch += -0.4 * rage;
                p.TailPitch += 0.2 * rage;
                p.WingFold = Math.Min(p.WingFold, 1 - 0.55 * rage);
                p.Breath *= 1 + 0.5 * rage;
            }

            double search = _w[(int)StanceKind.Search] * live;
            if (search > 1e-3)
            {
                double sweep = DMath.Sin(DMath.TwoPi * 0.42 * t + phase);
                double sniff = DMath.Pulse(DMath.Frac(0.3 * t + phase), 0.0, 0.15, 0.35, 0.5);
                p.HeadYaw += 0.6 * sweep * search;
                p.NeckYaw += 0.32 * sweep * search;
                p.LookX += sweep * search;
                p.NeckPitch += (0.1 - 0.4 * sniff) * search;
                p.HeadPitch += -0.25 * sniff * search;
                p.EarPitch += 0.55 * search;
                p.TailPitch += 0.1 * search;
                if (search > 0.5 && p.Mood == EyeMood.Neutral) p.Mood = EyeMood.Alert;
            }

            double flee = _w[(int)StanceKind.Flee] * live;
            if (flee > 1e-3)
            {
                p.EarPitch += -1.0 * flee;
                p.TailPitch += -0.55 * flee;
                p.TailCurl += 0.4 * flee;
                p.Crouch += 0.1 * flee;
                p.NeckPitch += -0.12 * flee;
                p.WingFold = Math.Max(p.WingFold, 0.9 * flee);
                if (flee > 0.5 && p.Mood == EyeMood.Neutral) p.Mood = EyeMood.Alert;
            }

            double charge = _w[(int)StanceKind.Charge] * (1 - ctx.RestWeight) * (1 - ctx.DeathWeight);
            if (charge > 1e-3)
            {
                p.BodyPitch += -0.15 * charge;
                p.NeckPitch += -0.5 * charge;
                p.HeadPitch += -0.3 * charge;
                p.EarPitch += -1.0 * charge;
                p.TailPitch += 0.45 * charge;
                p.Jaw = Math.Max(p.Jaw, 0.12 * charge);
                p.WingFold = Math.Max(p.WingFold, charge);
                if (charge > 0.4) p.Mood = EyeMood.Angry;
            }
        }
    }

    public enum ReactionKind
    {
        None,
        /// <summary>A startled hop on noticing something.</summary>
        Startle,
        /// <summary>Guard broken: thrown back on its heels, knees buckling.</summary>
        Reel,
        /// <summary>Ran headlong into something solid: flattened, then swaying.</summary>
        Crash,
    }

    /// <summary>Short additive one-shots that do not interrupt locomotion or facing.</summary>
    public sealed class ReactionPlayer
    {
        public ReactionKind Kind { get; private set; }
        public double Time { get; private set; }
        public double Duration { get; private set; }
        public bool Active => Kind != ReactionKind.None;
        public int TriggerCount { get; private set; }

        public void Trigger(MotorContext ctx, ReactionKind kind, double duration = 0)
        {
            Kind = kind;
            Time = 0;
            TriggerCount++;
            Duration = duration > 0 ? duration : kind switch
            {
                ReactionKind.Startle => 0.42,
                ReactionKind.Reel => 0.7,
                ReactionKind.Crash => 1.1,
                _ => 0,
            };
            if (Duration <= 0) Kind = ReactionKind.None;
        }

        public void Cancel() { Kind = ReactionKind.None; }

        public void Step(double dt)
        {
            if (!Active) return;
            Time += dt;
            if (Time >= Duration) Kind = ReactionKind.None;
        }

        public void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            if (!Active) return;
            double u = DMath.Saturate(Time / Duration);
            double L = Math.Max(4, ctx.Anatomy.Metrics.BodyLength);
            double leg = Math.Max(3, ctx.Anatomy.Metrics.LegLength);
            switch (Kind)
            {
                case ReactionKind.Startle:
                {
                    double hop = u < 0.6 ? DMath.Sin(DMath.Pi * u / 0.6) : 0;
                    double land = DMath.Pulse(u, 0.56, 0.62, 0.68, 0.95);
                    double up = DMath.Pulse(u, 0.0, 0.12, 0.6, 1.0);
                    p.Altitude += hop * leg * 0.3;
                    p.Squash += -0.13 * hop + 0.14 * land;
                    p.LegTuck = Math.Max(p.LegTuck, 0.3 * hop);
                    p.NeckPitch += 0.38 * up;
                    p.HeadPitch += 0.12 * up;
                    p.EarPitch += 1.1 * up;
                    p.TailPitch += 0.55 * up;
                    p.BodyPitch += 0.07 * hop;
                    p.WingFold = Math.Min(p.WingFold, 1 - 0.6 * hop);
                    p.EyeOpen = Math.Max(p.EyeOpen, up);
                    if (u < 0.85) p.Mood = EyeMood.Alert;
                    break;
                }
                case ReactionKind.Reel:
                {
                    double back = DMath.Pulse(u, 0.0, 0.12, 0.45, 0.9);
                    double buckle = DMath.Pulse(u, 0.25, 0.5, 0.8, 1.0);
                    p.BodyOffset = p.BodyOffset + new Vec3(-0.09 * L * back, 0, 0);
                    p.BodyPitch += 0.24 * back;
                    p.NeckPitch += 0.3 * back - 0.4 * buckle;
                    p.HeadPitch += -0.2 * buckle;
                    p.HeadRoll += Curves.Shake(u, 0.05, 0.95, 5) * 0.34;
                    p.BodyRoll += Curves.Shake(u, 0.1, 0.9, 3) * 0.08;
                    p.Crouch += 0.3 * buckle;
                    p.FrontLift += 0.3 * DMath.Pulse(u, 0.0, 0.1, 0.2, 0.4);
                    p.ArmRaise += 0.5 * back - 0.3 * buckle;
                    p.EarPitch += -0.9 * back;
                    p.TailPitch += -0.3 * buckle;
                    p.WingFold = Math.Min(p.WingFold, 1 - 0.7 * back);
                    p.WingFlap += Curves.Shake(u, 0.0, 0.6, 6) * 0.6;
                    p.Jaw = Math.Max(p.Jaw, 0.5 * back);
                    p.Mood = EyeMood.Pain;
                    break;
                }
                case ReactionKind.Crash:
                {
                    double flat = DMath.Pulse(u, 0.0, 0.04, 0.1, 0.3);
                    double sway = DMath.Pulse(u, 0.12, 0.3, 0.8, 1.0);
                    p.Squash += 0.3 * flat;
                    p.BodyOffset = p.BodyOffset + new Vec3((0.05 * flat - 0.07 * sway) * L, 0, 0);
                    p.Crouch += 0.45 * sway;
                    p.HeadRoll += DMath.Sin(DMath.TwoPi * 2.6 * Time) * 0.32 * sway;
                    p.HeadYaw += DMath.Sin(DMath.TwoPi * 1.3 * Time + 1) * 0.3 * sway;
                    p.NeckPitch += -0.3 * sway + 0.2 * flat;
                    p.BodyRoll += DMath.Sin(DMath.TwoPi * 1.3 * Time) * 0.07 * sway;
                    p.EarPitch += -0.8 * sway;
                    p.TailPitch += -0.4 * sway;
                    p.Jaw = Math.Max(p.Jaw, 0.3 * sway);
                    // Dizzy: eyes drift half shut and blink out of time.
                    p.EyeOpen *= 1 - sway * (0.45 + 0.4 * (DMath.Sin(DMath.TwoPi * 3.1 * Time) > 0.2 ? 1 : 0));
                    p.Mood = EyeMood.Pain;
                    break;
                }
            }
        }
    }

    /// <summary>A staged collapse chosen by body plan; holds its last pose until revived.</summary>
    public sealed class DeathPlayer
    {
        public bool Active { get; private set; }
        public double Progress { get; private set; }
        /// <summary>How far the body has come down to the ground (hovering bodies lose their lift with it).</summary>
        public double Grounded { get; private set; }
        private double _duration = 0.9;
        private double _side = 1;
        private Vec3 _push = new Vec3(-1, 0, 0);

        public void Begin(MotorContext ctx, Vec3 fromWorld)
        {
            if (Active) return;
            Active = true;
            Progress = 0;
            Grounded = 0;
            _duration = 0.9 * ctx.Traits.TimeScale * DMath.Lerp(0.9, 1.2, ctx.Traits.Weight);
            // Falls away from the blow; with no clear side, the creature's own seed picks one.
            _push = ctx.WorldDirToCreature(new Vec3(-fromWorld.X, 0, -fromWorld.Z)).NormalizedOr(new Vec3(-1, 0, 0));
            _side = Math.Abs(_push.Z) > 0.25 ? Math.Sign(_push.Z) : (ctx.Hash(31, 0) < 0.5 ? -1 : 1);
        }

        public void Snap(MotorContext ctx)
        {
            Begin(ctx, Vec3.UnitX);
            Progress = 1;
            Grounded = 1;
        }

        public void Revive() { Active = false; Progress = 0; Grounded = 0; }

        public void Step(double dt)
        {
            if (!Active || Progress >= 1) return;
            Progress = Math.Min(1, Progress + dt / Math.Max(0.1, _duration));
        }

        public void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            if (!Active) return;
            double u = Progress;
            var rig = ctx.Rig;
            double L = Math.Max(4, ctx.Anatomy.Metrics.BodyLength);
            double leg = Math.Max(3, ctx.Anatomy.Metrics.LegLength);
            double recoil = DMath.Pulse(u, 0.0, 0.07, 0.14, 0.3);
            double shut = DMath.SmoothStep(0.3, 0.72, u);
            double fall, impact;

            p.BodyOffset = p.BodyOffset + _push * (0.05 * L * DMath.SmoothStep(0.0, 0.3, u));
            p.Jaw = Math.Max(p.Jaw, 0.6 * recoil + 0.18 * shut);
            p.EarPitch += -1.0 * DMath.SmoothStep(0.05, 0.4, u);
            p.Breath *= 1 - DMath.SmoothStep(0.2, 0.8, u);
            p.Mood = EyeMood.Pain;

            if (rig.Upright)
            {
                // Knees give way, then the body pitches forward onto the ground.
                double knees = DMath.SmoothStep(0.0, 0.38, u);
                fall = DMath.EaseInCubic(DMath.Saturate((u - 0.3) / 0.38));
                impact = DMath.Pulse(u, 0.66, 0.69, 0.73, 0.86);
                p.Crouch = DMath.Lerp(p.Crouch, 1.0, knees);
                p.LegTuck = Math.Max(p.LegTuck, 0.85 * knees);
                p.BodyPitch += 0.14 * recoil - 1.32 * fall;
                p.BodyOffset = p.BodyOffset + new Vec3(-0.1 * L * fall, -0.12 * leg * fall, 0);
                p.NeckPitch += 0.3 * recoil + 0.35 * fall;
                p.HeadYaw += _side * 0.5 * fall;
                p.ArmRaise += 0.5 * recoil - 0.3 * fall;
                p.ArmReach += 0.5 * fall;
                p.LegLimp = Math.Max(p.LegLimp, fall);
                p.Squash += 0.1 * impact;
                Grounded = fall;
            }
            else switch (rig.Locomotion)
            {
                case LocomotionKind.Hop:
                case LocomotionKind.Float:
                {
                    // Soft bodies shudder, lose their shape and spread out.
                    fall = DMath.SmoothStep(0.12, 0.8, u);
                    impact = 0;
                    p.Squash += Curves.Shake(u, 0.0, 0.5, 6) * 0.14 + 0.43 * fall;
                    p.Extra0 = Math.Max(p.Extra0, DMath.SmoothStep(0.25, 0.95, u));
                    p.Altitude *= 1 - DMath.SmoothStep(0.0, 0.45, u);
                    p.TentacleAmp *= 1 - fall;
                    p.NeckPitch += -0.25 * fall;
                    Grounded = DMath.SmoothStep(0.0, 0.45, u);
                    break;
                }
                case LocomotionKind.Fly:
                {
                    // A last flail, then it drops and crumples on one wing.
                    double drop = DMath.EaseInCubic(DMath.Saturate(u / 0.5));
                    fall = DMath.SmoothStep(0.3, 0.62, u);
                    impact = DMath.Pulse(u, 0.48, 0.52, 0.57, 0.75);
                    p.WingFold = DMath.Lerp(Math.Min(p.WingFold, 1 - 0.8 * recoil), 0.72, DMath.SmoothStep(0.35, 0.8, u));
                    p.WingFlap += Curves.Shake(u, 0.02, 0.42, 7) * 0.9;
                    p.WingTwist += _side * 0.5 * fall;
                    p.BodyRoll += _side * 0.95 * fall;
                    p.BodyPitch += -0.25 * drop * (1 - fall) + 0.1 * recoil;
                    p.Crouch = DMath.Lerp(p.Crouch, 1.0, fall);
                    p.LegTuck = Math.Max(p.LegTuck, 0.5 * fall);
                    p.LegLimp = Math.Max(p.LegLimp, fall);
                    p.NeckPitch += -0.5 * fall;
                    p.HeadPitch += -0.3 * fall;
                    p.TailPitch += -0.45 * fall;
                    p.Squash += 0.16 * impact;
                    p.Altitude *= 1 - drop;
                    Grounded = drop;
                    break;
                }
                case LocomotionKind.Swim:
                {
                    fall = DMath.SmoothStep(0.15, 0.8, u);
                    impact = 0;
                    p.BodyRoll += _side * 2.9 * fall;
                    p.FinAmp *= 1 - fall;
                    p.TailYaw += Curves.Shake(u, 0.0, 0.5, 5) * 0.8;
                    p.SpineYaw += _side * 0.25 * fall;
                    Grounded = fall;
                    break;
                }
                case LocomotionKind.Serpentine:
                {
                    fall = DMath.SmoothStep(0.15, 0.75, u);
                    impact = 0;
                    p.NeckPitch += 0.4 * recoil - 0.45 * fall;
                    p.HeadPitch += -0.3 * fall;
                    p.HeadRoll += _side * 0.9 * fall;
                    p.BodyRoll += _side * 0.5 * fall;
                    p.Squash += 0.12 * fall;
                    p.TailCurl += 0.5 * fall;
                    Grounded = fall;
                    break;
                }
                default:
                {
                    // Four or more legs: a stagger, then over onto its side.
                    fall = DMath.EaseInOutCubic(DMath.Saturate((u - 0.16) / 0.46));
                    impact = DMath.Pulse(u, 0.58, 0.62, 0.66, 0.8);
                    p.Altitude += recoil * leg * 0.14;
                    p.BodyPitch += 0.12 * recoil;
                    p.BodyRoll += _side * 1.36 * fall;
                    p.Crouch = DMath.Lerp(p.Crouch, 1.0, fall);
                    p.LegLimp = Math.Max(p.LegLimp, fall);
                    p.NeckPitch += 0.25 * recoil - 0.45 * fall;
                    p.HeadPitch += -0.3 * fall;
                    p.HeadRoll += _side * 0.35 * fall;
                    p.WingFold = Math.Max(p.WingFold, fall);
                    p.Squash += 0.14 * impact;
                    Grounded = fall;
                    break;
                }
            }
            // Whatever still swayed with life goes slack.
            double slack = 1 - DMath.SmoothStep(0.3, 0.9, u);
            p.TentacleAmp *= slack;
            p.FinAmp *= slack;
            p.EyeOpen *= 1 - shut;
            if (shut >= 1) p.Mood = EyeMood.Sleep;
            p.LookX *= 1 - shut;
            p.LookY *= 1 - shut;
        }
    }
}
