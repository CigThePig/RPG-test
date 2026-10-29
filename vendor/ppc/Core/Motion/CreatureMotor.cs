// Procedural Pixel Creatures - the creature motor: fixed-step simulation of one creature instance.
//
// Usage: create with a CreatureModel, feed inputs (desired velocity, facing, commands), advance with
// Step(FixedStep) from a FixedClock, and read Pose. The simulation depends only on the inputs and
// the fixed step count, never on the render frame rate: the same input history reproduces the same
// poses on every machine.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class MotorOptions
    {
        public FacingMode Facing = FacingMode.Eight;
        /// <summary>Motor integrates the position from DesiredVelocity (true) or follows SetWorldPosition (false).</summary>
        public bool InternalDrive = true;
        public bool FaceVelocity = true;
        /// <summary>Loopable output for exports: no random gestures, periodic blink/breathing (see MotorContext.ExportLoop).</summary>
        public bool ExportMode;
        public double ExportLoop = 2.0;
    }

    /// <summary>Fixed step accumulator. Keeps simulation time independent of render time.</summary>
    public sealed class FixedClock
    {
        public const double DefaultStep = 1.0 / 60.0;
        public double StepSize { get; }
        public int MaxStepsPerFrame { get; set; } = 8;
        private double _acc;

        public FixedClock(double step = DefaultStep) { StepSize = step; }

        /// <summary>Returns how many fixed steps to run for a frame delta (already scaled by time scale).</summary>
        public int Advance(double delta)
        {
            if (delta < 0 || double.IsNaN(delta)) delta = 0;
            _acc += delta;
            int n = (int)Math.Floor(_acc / StepSize + 1e-9);
            if (n > MaxStepsPerFrame)
            {
                n = MaxStepsPerFrame;
                _acc = 0;
            }
            else _acc -= n * StepSize;
            return n;
        }

        /// <summary>Interpolation factor between the last two steps (for optional pose interpolation).</summary>
        public double Alpha => DMath.Saturate(_acc / StepSize);

        public void Reset() { _acc = 0; }
    }

    public sealed class CreatureMotor
    {
        private readonly MotorContext _ctx;
        private readonly ILocomotionModule _loco;
        private readonly RigDriver _driver;
        private readonly IdleLife _idle;
        private readonly ActionPlayer _action = new ActionPlayer();
        private readonly HitReaction _hit = new HitReaction();
        private readonly RestPlayer _rest = new RestPlayer();
        private readonly CreaturePose _pose;
        private PoseParams _params = PoseParams.Neutral;
        private Smooth1 _moveW, _runW;
        private Vec3 _externalPos;
        private bool _externalSet;
        private Vec3 _prevVelocity;
        private bool _pendingAction;
        private ActionStyle _pendingStyle;
        private long _steps;
        private bool _wasRolling;
        private double _hitSamplingUntil;
        private double? _gameplayActionTime;

        public MotorOptions Options { get; }
        public CreatureModel Model { get; }
        public MotorContext Context => _ctx;
        public CreaturePose Pose => _pose;
        public PoseParams Params => _params;
        public ILocomotionModule Locomotion => _loco;
        public double CycleFrequency => _loco.CycleFrequency(_ctx);
        public long StepCount => _steps;

        /// <summary>Desired ground velocity in pixels/second (x east, z south).</summary>
        public Vec3 DesiredVelocity { get; set; }
        /// <summary>Yaw to face while standing (null = keep).</summary>
        public double? FacingOverride { get; set; }
        // Combat can freeze the exact current yaw, including between quantized directions.
        public bool FacingOverrideExact { get; set; }
        // Presentation only: host supplies normalized authoritative dodge progress.
        public double? DodgeProgress { get; set; }
        public bool DodgeBiped { get; set; }
        public double DodgeForwardComponent { get; set; } = 1;
        public double DodgeSideComponent { get; set; }
        public void LockCurrentFacing(double yaw)
        {
            _ctx.Yaw = _ctx.TargetYaw = DMath.WrapAngle(yaw);
            _ctx.YawRate = 0;
            FacingOverride = _ctx.Yaw;
            FacingOverrideExact = true;
        }
        public bool Resting { get => _rest.Requested; set => _rest.Requested = value; }

        public Vec3 Position => _ctx.Position;
        public double Yaw => _ctx.Yaw;
        public double Speed => _ctx.Speed;
        public double Time => _ctx.Time;
        public double Altitude => _ctx.Altitude;
        public double HitFlash => _hit.Flash;
        public double RestProgress => _rest.Progress;
        public bool ActionActive => _action.Active;
        public ActionStyle CurrentActionStyle => _action.Style;
        public double ActionDuration => _action.Duration;
        public double ActionTime => _action.Time;
        public int ActionTriggerCount => _action.TriggerCount;
        // Do not keep a crowd at the combat rate for the whole spring tail.
        public bool CombatSampling => _action.Active || _ctx.Time < _hitSamplingUntil;

        public CreatureMotor(CreatureModel model, CreatureRegistry registry, MotorOptions? options = null)
        {
            Model = model;
            Options = options ?? new MotorOptions();
            _ctx = new MotorContext(model) { ExportMode = Options.ExportMode, ExportLoop = Options.ExportLoop };
            string moduleId = string.IsNullOrEmpty(model.Anatomy.Rig.LocomotionModule) ? "legged" : model.Anatomy.Rig.LocomotionModule;
            _loco = registry.CreateLocomotion(moduleId);
            _driver = new RigDriver(model.Anatomy, model.Motion);
            _idle = new IdleLife(_ctx);
            _pose = new CreaturePose(model.Anatomy.Skeleton);
            _loco.Initialize(_ctx);
            ComputePose(0, initialize: true);
        }

        // ------------------------------------------------------------------ commands

        public void TriggerGameplayAction(double windup, double recovery) =>
            TriggerGameplayAction(windup, recovery, null);

        /// <summary>Starts immediately on the world's timeline, even when waking from rest.</summary>
        public void TriggerGameplayAction(double windup, double recovery, ActionStyle? style, double elapsed = 0)
        {
            _rest.Requested = false;
            _pendingAction = false;
            _gameplayActionTime = null;
            _action.Trigger(_ctx, ResolveActionStyle(style));
            _action.MatchCombat(windup, recovery);
            if (elapsed > 0) _action.Seek(elapsed);
        }

        /// <summary>Unsupported appendage-specific actions fall back to this rig's own action.</summary>
        public ActionStyle ResolveActionStyle(ActionStyle? requested)
        {
            if (!requested.HasValue || !Enum.IsDefined(typeof(ActionStyle), requested.Value)) return _ctx.Rig.Action;
            var rig = _ctx.Rig;
            bool supported = requested.Value switch
            {
                ActionStyle.TailSwipe or ActionStyle.Sting => rig.Tail != null,
                ActionStyle.WingBuffet => rig.Wings.Count > 0,
                ActionStyle.TentacleLash => rig.Tentacles.Count > 0,
                ActionStyle.PincerSnap => rig.Grippers.Count > 0,
                _ => true
            };
            return supported ? requested.Value : rig.Action;
        }

        public void CancelGameplayAction()
        {
            _pendingAction = false;
            _gameplayActionTime = null;
            _action.Cancel();
        }

        /// <summary>Optional host timeline: pose progress follows the authoritative action state.</summary>
        public void SynchronizeGameplayAction(double elapsed)
        {
            if (_action.Active && double.IsFinite(elapsed))
                _gameplayActionTime = DMath.Clamp(elapsed, 0, _action.Duration);
        }

        public void TriggerAction() => TriggerAction(_ctx.Rig.Action);

        public void TriggerAction(ActionStyle style)
        {
            _gameplayActionTime = null;
            if (_rest.Progress > 0.01)
            {
                // wake up first, then act
                _rest.Requested = false;
                _pendingAction = true;
                _pendingStyle = style;
                return;
            }
            _action.Trigger(_ctx, style);
        }

        /// <summary>Hit from a world direction (vector pointing from the attacker to the creature is derived internally).</summary>
        public void TriggerHit(Vec3 fromDirectionWorld, double strength = 1.0)
        {
            if (_rest.Progress > 0.01) _rest.Requested = false;
            _pendingAction = false;
            _gameplayActionTime = null;
            _action.Cancel();
            _hitSamplingUntil = _ctx.Time + .38;
            _hit.Trigger(_ctx, fromDirectionWorld, strength);
            _driver.Tail?.Impulse(new Vec3(-fromDirectionWorld.X, 0.5, -fromDirectionWorld.Z).NormalizedOr(Vec3.UnitY) * 40, FixedClock.DefaultStep);
        }

        /// <summary>Moves the creature instantly (resets feet and chains).</summary>
        public void Teleport(Vec3 position, double yaw)
        {
            _ctx.Position = new Vec3(position.X, 0, position.Z);
            _ctx.Yaw = yaw;
            _ctx.TargetYaw = yaw;
            _ctx.YawRate = 0;
            _ctx.Velocity = Vec3.Zero;
            _externalPos = _ctx.Position;
            _loco.Initialize(_ctx);
            ComputePose(0, initialize: true);
        }

        /// <summary>External drive: the game sets the world position every frame; velocity is derived.</summary>
        // Host-provided measured velocity avoids deriving a spike then zeros when
        // several motor ticks consume one external physics position after a long frame.
        private Vec3? _externalVelocity;
        public void SetWorldPosition(Vec3 position, Vec3? measuredVelocity = null)
        {
            _externalVelocity = measuredVelocity;
            _externalPos = new Vec3(position.X, 0, position.Z);
            _externalSet = true;
        }

        /// <summary>Instantly sets rest state (used when spawning sleeping creatures).</summary>
        public void SnapRest(bool resting)
        {
            _rest.Snap(resting);
            ComputePose(0, initialize: true);
        }

        public MotionStateKind State
        {
            get
            {
                if (_action.Active) return MotionStateKind.Action;
                if (_hit.Active && _hit.Flash > 0) return MotionStateKind.Hit;
                if (_rest.Progress > 0.001) return _rest.Progress >= 0.999 && _rest.Requested ? MotionStateKind.Rest : MotionStateKind.Transition;
                if (_ctx.MoveWeight > 0.5) return _ctx.RunBlend > 0.5 ? MotionStateKind.Run : MotionStateKind.Walk;
                return MotionStateKind.Idle;
            }
        }

        // ------------------------------------------------------------------ simulation

        public void Step(double dt)
        {
            if (dt <= 0) return;
            _steps++;
            var ctx = _ctx;
            ctx.Time += dt;
            var traits = ctx.Traits;

            // ---- drive
            Vec3 oldPos = ctx.Position;
            if (Options.InternalDrive)
            {
                bool blocked = _rest.Progress > 0.01 || _action.Envelope > 0.5;
                Vec3 desired = blocked ? Vec3.Zero : new Vec3(DesiredVelocity.X, 0, DesiredVelocity.Z);
                double maxSpeed = traits.RunSpeed * 1.25;
                if (desired.Length > maxSpeed) desired = desired.Normalized() * maxSpeed;
                double accel = traits.RunSpeed * DMath.Lerp(5.0, 2.2, traits.Weight);
                Vec3 dv = desired - ctx.Velocity;
                double maxDv = accel * dt;
                if (dv.Length > maxDv) dv = dv.Normalized() * maxDv;
                ctx.Velocity = ctx.Velocity + dv;
                ctx.Position = ctx.Position + ctx.Velocity * dt;
            }
            else if (_externalSet)
            {
                Vec3 v = _externalVelocity ?? ((_externalPos - ctx.Position) / dt);
                ctx.Velocity = _externalVelocity.HasValue ? v : Vec3.Lerp(ctx.Velocity, v, DMath.DampFactor(0.05, dt));
                ctx.Position = _externalPos;
            }
            ctx.Acceleration = Vec3.Lerp(ctx.Acceleration, (ctx.Velocity - _prevVelocity) / dt, DMath.DampFactor(0.08, dt));
            _prevVelocity = ctx.Velocity;
            ctx.Speed = new Vec2(ctx.Velocity.X, ctx.Velocity.Z).Length;
            ctx.ForwardSpeed = Vec3.Dot(ctx.Velocity, ctx.Forward);

            // ---- facing
            UpdateFacing(dt);

            // ---- blends
            double walk = traits.WalkSpeed, run = traits.RunSpeed;
            _moveW.Step(DMath.SmoothStep(walk * 0.06, walk * 0.3, ctx.Speed), 0.07, dt);
            _runW.Step(DMath.SmoothStep(walk * 1.1, run * 0.85, ctx.Speed), 0.12, dt);
            ctx.MoveWeight = _moveW.X;
            ctx.RunBlend = _runW.X;

            _rest.Step(ctx, dt);
            if (_gameplayActionTime.HasValue && _action.Active) _action.Seek(_gameplayActionTime.Value);
            else _action.Step(dt);
            _hit.Step(ctx, dt);
            _idle.Step(ctx, dt);
            ctx.RestWeight = _rest.Progress;
            ctx.RestExiting = _rest.Exiting;
            ctx.ActionWeight = _action.Envelope;
            if (_pendingAction && _rest.Progress <= 0.001)
            {
                _pendingAction = false;
                _action.Trigger(ctx, _pendingStyle);
            }

            bool rolling = DodgeProgress.HasValue && DodgeBiped && _loco is LeggedLocomotion;
            if (_loco is LeggedLocomotion legs)
            {
                legs.RollActive = rolling;
                if (!rolling && _wasRolling) legs.ResetPlants(ctx);
            }
            _wasRolling = rolling;
            _loco.Step(ctx, dt);
            ComputePose(dt, initialize: false);
            _ = oldPos;
        }

        private void UpdateFacing(double dt)
        {
            var ctx = _ctx;
            double walk = ctx.Traits.WalkSpeed;
            double? desired = null;
            if (Options.FaceVelocity && ctx.Speed > walk * 0.15) desired = PixelCamera.GroundVelocityToYaw(ctx.Velocity.X, ctx.Velocity.Z);
            else if (FacingOverride.HasValue) desired = FacingOverride.Value;
            if (desired.HasValue && _rest.Progress < 0.01)
            {
                double target = FacingOverrideExact && !Options.FaceVelocity ? DMath.WrapAngle(desired.Value) : Quantize(desired.Value);
                // hysteresis: keep the current target unless the new direction is clearly different
                double step = Options.Facing == FacingMode.Four ? DMath.HalfPi : (Options.Facing == FacingMode.Eight ? DMath.Pi / 4 : 0);
                if (!FacingOverrideExact && step > 0 && Math.Abs(DMath.WrapAngle(desired.Value - ctx.TargetYaw)) < step * 0.5 + 0.14) target = ctx.TargetYaw;
                ctx.TargetYaw = target;
            }
            double delta = DMath.WrapAngle(ctx.TargetYaw - ctx.Yaw);
            // At the exact antipode either route is equally short. Pick one once;
            // after the first step the ordinary shortest-angle sign keeps it stable.
            // Rechoosing a preferred midpoint on every step can steer away from the
            // target and then flip back, trapping south-to-north turns in oscillation.
            if (Math.Abs(Math.Abs(delta) - DMath.Pi) < 1e-9) delta = DMath.Pi;
            double size = DMath.Saturate(ctx.Anatomy.Metrics.BodyLength / 120.0);
            double maxRate = DMath.Lerp(9.0, 3.6, size) * DMath.Lerp(0.85, 1.2, ctx.Traits.Energy);
            double desiredRate = DMath.Clamp(delta * 9.0, -maxRate, maxRate);
            ctx.YawRate += (desiredRate - ctx.YawRate) * DMath.DampFactor(0.045, dt);
            ctx.Yaw = DMath.WrapAngle(ctx.Yaw + ctx.YawRate * dt);
            if (Math.Abs(DMath.WrapAngle(ctx.TargetYaw - ctx.Yaw)) < 0.004 && Math.Abs(ctx.YawRate) < 0.25)
            {
                ctx.Yaw = ctx.TargetYaw;
                ctx.YawRate = 0;
            }
        }

        private double Quantize(double yaw)
        {
            switch (Options.Facing)
            {
                case FacingMode.Four: return PixelCamera.DirectionYaw(PixelCamera.YawToDirection4(yaw));
                case FacingMode.Eight: return PixelCamera.DirectionYaw(PixelCamera.YawToDirection8(yaw));
                default: return DMath.WrapAngle(yaw);
            }
        }

        private void ComputePose(double dt, bool initialize)
        {
            var ctx = _ctx;
            var p = PoseParams.Neutral;
            double idle = (1 - ctx.MoveWeight) * (1 - ctx.RestWeight) * (1 - ctx.ActionWeight);
            _idle.Evaluate(ctx, ref p, idle);
            _loco.Evaluate(ctx, ref p);

            // head leads turns and looks where it is going
            double turn = DMath.WrapAngle(ctx.TargetYaw - ctx.Yaw);
            p.HeadYaw += DMath.Clamp(turn * 0.55, -0.7, 0.7) * (1 - ctx.RestWeight);
            p.NeckYaw += DMath.Clamp(turn * 0.35, -0.45, 0.45) * (1 - ctx.RestWeight);
            p.LookX += DMath.Clamp(turn * 1.5, -1, 1);

            _rest.Evaluate(ctx, ref p);
            _action.Evaluate(ctx, ref p);
            _hit.Evaluate(ctx, ref p);
            bool exclusiveRoll = DodgeProgress.HasValue && DodgeBiped && _loco is LeggedLocomotion;
            if (exclusiveRoll)
            {
                // Replace locomotion/action pose rather than adding a spin to it.
                p = RollMotion.Evaluate(ctx, DodgeProgress!.Value, DodgeForwardComponent, DodgeSideComponent);
            }
            else if (DodgeProgress.HasValue)
            {
                // Families without biped roll anatomy retain a small non-spinning evasion.
                double tuck = Math.Sin(Math.PI * DMath.Clamp(DodgeProgress.Value, 0, 1));
                p.BodyPitch += .45 * tuck;
                p.Crouch += .35 * tuck;
            }
            p.Altitude += _loco.Altitude(ctx) * (1 - ctx.RestWeight);
            p.EyeOpen = DMath.Saturate(p.EyeOpen);
            ctx.Altitude = p.Altitude;
            _params = p;

            var sk = _pose.Skeleton;
            sk.Reset();
            _driver.ApplyBody(p, sk);
            sk.Solve();
            if (exclusiveRoll && DodgeProgress.HasValue)
            {
                var support = RollMotion.CorrectGroundContact(ctx, sk, DodgeProgress.Value, ref p);
                if (Math.Abs(support) > 1e-6)
                {
                    sk.Reset();
                    _driver.ApplyBody(p, sk);
                    sk.Solve();
                }
            }
            _pose.Contacts.Clear();
            if (exclusiveRoll && _loco is LeggedLocomotion rollLegs)
                rollLegs.ApplyRollLimbs(ctx, p, sk, _pose, DodgeProgress!.Value);
            else
                _loco.ApplyLimbs(ctx, p, sk, _pose);
            _params = p;
            if (initialize) _driver.ResetChains(ctx, p, sk);
            else _driver.StepChains(ctx, p, sk, dt);
            _driver.ApplyChains(ctx, sk);
            sk.Solve();
            _pose.Expression = new ExpressionState { EyeOpen = p.EyeOpen, Mood = p.Mood, LookX = p.LookX, LookY = p.LookY, Flash = _hit.Flash };
            _pose.Altitude = p.Altitude;
        }
    }
}
