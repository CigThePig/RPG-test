// Mosslight action-pose extension for Procedural Pixel Creatures (MIT).
// The world owns displacement, damage and collision; this controller owns only skeleton presentation.
using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    /// <summary>Deterministic contact-aware shoulder roll for generated upright bipeds.</summary>
    public static class RollMotion
    {
        static double Ease(double a, double b, double u) => DMath.SmoothStep(a, b, u);

        // Keep the angle unwrapped: interpolating identity -> identity as quaternions would erase a full turn.
        public static double RollAngle(double u)
        {
            u = DMath.Clamp(u, 0, 1);
            if (u <= .12) return 0;
            if (u < .30) return .30 * DMath.TwoPi * Ease(.12, .30, u);
            if (u < .73) return DMath.TwoPi * (.30 + .54 * Ease(.30, .73, u));
            if (u < .90) return DMath.TwoPi * (.84 + .16 * Ease(.73, .90, u));
            return DMath.TwoPi;
        }

        public static PoseParams Evaluate(MotorContext ctx, double u, double forward, double side)
        {
            u = DMath.Clamp(u, 0, 1);
            // Forward/right are relative to the *locked* presentation yaw. Y is always world up.
            var direction = new Vec3(forward, 0, side).NormalizedOr(Vec3.UnitX);
            // Y cross forward gives the forward-tumble axis; continuous axis also supports diagonals.
            var axis = Vec3.Cross(Vec3.UnitY, direction).NormalizedOr(new Vec3(0, 0, -1));
            double entry = Ease(0, .18, u);
            double exit = 1 - Ease(.79, 1, u);
            double compact = entry * exit;
            double deep = Ease(.14, .33, u) * (1 - Ease(.72, .96, u));
            double rollAngle = RollAngle(u);
            double shoulderBias = .24 * Math.Sin(Math.PI * Ease(.10, .90, u)) * compact;
            var p = PoseParams.Neutral;
            p.OverrideBodyRotation = true;
            // Small diagonal shoulder lead, fully recovered at both endpoints.
            p.BodyRotation = Quat.AxisAngle(direction, shoulderBias) * Quat.AxisAngle(axis, rollAngle);
            p.RollDirection = direction;
            p.Crouch = .82 * compact;
            p.LegTuck = .96 * deep;
            p.ArmRaise = .42 * compact;
            p.SpinePitch = -.23 * compact;
            p.NeckPitch = -.30 * compact;
            p.HeadPitch = -.35 * compact;
            p.EyeOpen = 1;
            // Approach the floor as we enter the tumble. Contact correction below fixes anatomy-specific clearance.
            p.BodyOffset = new Vec3(0, -ctx.Anatomy.Metrics.LegLength * .17 * compact, 0);
            return p;
        }

        /// <summary>
        /// A cheap first-pass support solver. Bone origins approximate the centres of the
        /// head/chest/pelvis/shoulder masses. Correct their minimum vertical extent to the ground,
        /// then re-solve the skeleton before IK. Never modifies authoritative world position.
        /// </summary>
        public static double CorrectGroundContact(MotorContext ctx, SkeletonPose sk, double u, ref PoseParams p)
        {
            double weight = Ease(.11, .29, u) * (1 - Ease(.76, .94, u));
            if (weight <= 0) return 0;
            var rig = ctx.Rig;
            double leg = Math.Max(4, ctx.Anatomy.Metrics.LegLength);
            double low = double.PositiveInfinity;
            void Check(int index, double radius)
            {
                if (index < 0 || index >= sk.World.Length) return;
                low = Math.Min(low, sk.World[index].Origin.Y - radius);
            }
            Check(rig.Pelvis, leg * .13);
            Check(rig.Chest, leg * .20);
            Check(rig.Head, leg * .16);
            foreach (var arm in rig.Arms) Check(arm.RootBone, leg * .09);
            if (!double.IsFinite(low)) return 0;
            double correction = DMath.Clamp(-low, -leg * .65, leg * 1.5) * weight;
            p.BodyOffset = p.BodyOffset + new Vec3(0, correction, 0);
            return correction;
        }
    }
}
