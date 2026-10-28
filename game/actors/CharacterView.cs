using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;

namespace Mosslight;
/// <summary>Presentation consumes actor state; the PPC genome never owns health, quests or inventory.</summary>
public partial class CharacterView : Node2D
{
    public ActorState State { get; private set; } = null !;
    public CreatureActor Creature { get; private set; } = null !;

    public bool HasLoot { get; set; }
    public bool PreferMeasuredExternalVelocity { get; set; } = true;
    public Func<ActorState, Terrain>? GroundTerrain { get; set; }
    public RollDustOverlay? RollDust { get; private set; }
    LootMarker? lootMarker;
    public bool LootMarkerVisible => lootMarker?.Visible ?? false;
    Vector2 previous;
    float attackTime, attackDuration, impactTime;
    Vector2 attackDirection = Vector2.Right;
    bool attackArc;
    bool attackTimeSynchronized;
    bool previousWasDodging;
    float defeatDelay;
    public void Setup(ActorState actor)
    {
        State = actor;
        Creature = new CreatureActor
        {
            FamilyId = "",
            AutoMove = false,
            UseMeasuredExternalVelocity = true,
            SyncNodePosition = false,
            AnimationFps = 10,
            ActionAnimationFps = 30,
            Quality = 1,
            CullOffscreen = true
        };
        AddChild(Creature);
        // Render with the player without modifying PPC bones or world state.
        if (actor.Id == "player")
        {
            RollDust = new RollDustOverlay();
            AddChild(RollDust);
        }
        var result = actor.Appearance.GenomeJson == "" ? null : CreatureRuntime.Serializer.Deserialize(actor.Appearance.GenomeJson);
        if (result != null && !result.Ok)
            throw new InvalidDataException(result.Describe());
        var genome = result?.Genome ?? CreatureRuntime.Sample(actor.Appearance.Family, actor.Appearance.Seed);
        Creature.SetGenome(genome, async: false);
        float body = (float)Creature.Model!.Anatomy.Metrics.BodyLength;
        Creature.Scale = Vector2.One * Math.Clamp(actor.Radius / Math.Max(1, body * .2f), .25f, 1);
        previous = new(actor.X, actor.Y);
        if (!actor.Alive)
        {
            Creature.Resting = true;
            Creature.Motor?.SnapRest(true);
            Creature.RequestRender();
        }
        Sync(1f / 60);
    }

    public void SnapPosition() { previous=new Vector2(State.X,State.Y); Creature.Teleport(previous); Creature.MoveVelocity=Vector2.Zero; RollDust?.Reset(); }

    public void Equipment(string json)
    {
        if (State.Appearance.Family != "biped")
            return;
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        var genome = CreatureRuntime.Serializer.Deserialize(State.Appearance.GenomeJson).Genome!;
        genome = genome.WithChoice("gear.weapon", data["weapon"]).WithChoice("gear.outfit", data["outfit"]);
        Creature.SetGenome(genome, async: false);
    }

    public void Sync(float dt)
    {
        var now = new Vector2(State.X, State.Y);
        // The last dodge increment can arrive on the same frame as the transition
        // to Idle. Do not let that displacement masquerade as walking intent.
        bool dodgeDisplacement = State.State == ActionState.Dodging || previousWasDodging;
        bool combatDisplacement = dodgeDisplacement || State.FreeSlash;
        Creature.UseMeasuredExternalVelocity = combatDisplacement || PreferMeasuredExternalVelocity;
        Creature.MoveVelocity = combatDisplacement || State.State is ActionState.Staggered or ActionState.Dead ? Vector2.Zero : (now - previous) / Math.Max(dt, .001f);
        if (State.State is ActionState.Idle or ActionState.Moving && Creature.MoveVelocity.LengthSquared() > .25f) Creature.ClearFacingRequest();
        if (Creature.Motor != null)
        {
            var motor = Creature.Motor;
            motor.Options.FaceVelocity = (State.State is ActionState.Idle or ActionState.Moving) && Creature.MoveVelocity.LengthSquared() > .25f;
            motor.DodgeProgress = State.State == ActionState.Dodging ? State.ActionElapsed / CombatTuning.DodgeDuration :
                previousWasDodging && State.State == ActionState.Idle ? 1.0 : null;
            motor.DodgeBiped = State.Appearance.Family == "biped";
            if (motor.DodgeProgress.HasValue)
            {
                var facing = new Vector2((float)Math.Cos(motor.Yaw), (float)-Math.Sin(motor.Yaw));
                var travel = new Vector2(State.ActionDirectionX, State.ActionDirectionY);
                motor.DodgeForwardComponent = facing.Dot(travel);
                motor.DodgeSideComponent = facing.X * travel.Y - facing.Y * travel.X;
            }
        }
        if (!Creature.Paused && !attackTimeSynchronized) attackTime += dt;
        attackTimeSynchronized = false;
        if (State.State is ActionState.Staggered or ActionState.Dead) attackDuration = 0;
        Creature.GroundPosition = now;
        previous = now;
        previousWasDodging = State.State == ActionState.Dodging;
        // The dust's own lifetime survives the action transition, and each
        // emitted puff stays at its original ground location as the actor moves.
        RollDust?.Advance(dt, State.State == ActionState.Dodging,
            State.ActionElapsed / CombatTuning.DodgeDuration, now,
            new Vector2(State.ActionDirectionX, State.ActionDirectionY),
            GroundTerrain?.Invoke(State) ?? Terrain.Grass);
        Position = GodotConvert.GroundToScreen(State.X, State.Y);
        if (!Creature.Paused && defeatDelay > 0) defeatDelay = Math.Max(0, defeatDelay - dt);
        Creature.Resting = State.Alive ? State.Activity == "rest" && (State.State is ActionState.Idle or ActionState.Moving) : defeatDelay <= 0;
        if (State.Alive) { defeatDelay = 0; Creature.Modulate = Colors.White; }
        else
        {
            float collapse = (float)(Creature.Motor?.RestProgress ?? 1);
            Creature.Modulate = Colors.White.Lerp(new Color(.45f, .4f, .4f, .72f), collapse);
        }
        if (!State.Alive && HasLoot && lootMarker == null)
        {
            // Create at most one marker per lootable corpse, above actor art.
            lootMarker = new LootMarker { ZIndex = 1 };
            AddChild(lootMarker);
        }
        if (lootMarker != null) lootMarker.Visible = !State.Alive && HasLoot;
        QueueRedraw();
    }

    static ActionStyle? Style(AbilityDef? ability) => ability != null &&
        Enum.TryParse<ActionStyle>(ability.ActionStyle, out var style) && Enum.IsDefined(style) ? style : null;

    public void Cue(string cue, ActorState? target = null, AbilityDef? ability = null, float elapsed = 0)
    {
        if (cue is "attack" or "cast")
        {
            if (State.FreeSlash || ability?.Geometry?.Shape == "cone")
            {
                var ground = new Vector2(State.ActionDirectionX, State.ActionDirectionY);
                Creature.LockFacingYaw(PixelCamera.GroundVelocityToYaw(ground.X, ground.Y));
                attackDirection = GodotConvert.GroundToScreen(ground.X, ground.Y).Normalized();
            }
            else if (target != null && target.Id != State.Id)
            {
                var delta = target.Position - State.Position;
                if (delta.LengthSquared() > .001f)
                {
                    Creature.FaceYaw(PixelCamera.GroundVelocityToYaw(delta.X, delta.Y));
                    attackDirection = GodotConvert.GroundToScreen(delta.X, delta.Y).Normalized();
                }
            }
            if (Creature.Motor != null) Creature.Motor.Options.FaceVelocity = false;
            if (ability != null) Creature.TriggerGameplayAction(ability.Windup, ability.Recovery, Style(ability), elapsed);
            else Creature.TriggerAction();
            attackTime = elapsed;
            impactTime = ability?.Windup ?? .14f;
            attackDuration = impactTime + (ability?.Recovery ?? .24f);
            attackArc = cue == "attack" && ability?.Id == "slash";
        }
        if (cue == "dodge")
        {
            // Preserve the initial combat-facing yaw so the roll axis remains stable.
            Creature.LockFacingYaw(Creature.Motor?.Yaw ?? 0);
            if (Creature.Motor != null) Creature.Motor.Options.FaceVelocity = false;
        }
        if (cue == "hit")
        {
            if (Creature.Motor != null) Creature.Motor.Options.FaceVelocity = false;
            Creature.TriggerHit(target == null ? Vector2.Left : new Vector2(target.X - State.X, target.Y - State.Y).Normalized());
            attackDuration = 0;
        }
        if (cue is "cancel" or "completed")
        {
            Creature.CancelGameplayAction();
            attackDuration = 0;
        }
    }

    /// <summary>Use captured pre-knockback positions; never rotate the victim to face the source.</summary>
    public void ReactToHit(System.Numerics.Vector2 origin, System.Numerics.Vector2 impact, float strength)
    {
        if (Creature.Motor != null) Creature.Motor.Options.FaceVelocity = false;
        var from = new Vector2(origin.X - impact.X, origin.Y - impact.Y);
        Creature.TriggerHit(from.LengthSquared() > .001f ? from.Normalized() : Vector2.Left, strength);
        attackDuration = 0;
    }

    public void BeginDefeat() => defeatDelay = .20f;

    /// <summary>Resume only a live windup, without dispatching another start or sound.</summary>
    public void RestoreWindup(World world)
    {
        if (!State.Alive || State.State != ActionState.Windup) return;
        var ability = world.FindAbility(State.PendingAbility);
        if (ability == null) return;
        world.Actors.TryGetValue(State.Target, out var target);
        Cue(ability.Damage < 0 ? "cast" : "attack", target, ability,
            Math.Clamp(ability.Windup - State.StateRemaining, 0, ability.Windup));
    }

    // Reconstructed views derive facing from the same state as the warning. A
    // target-locked strike tracks its target; a cone keeps its committed yaw.
    public void SyncCombatFacing(World world)
    {
        if (State.State == ActionState.Recovery && State.RecoveryAbility != "")
        {
            var recovery = world.FindAbility(State.RecoveryAbility);
            if (recovery != null)
            {
                attackTime = Math.Clamp(recovery.Windup + recovery.Recovery - State.StateRemaining, 0, recovery.Windup + recovery.Recovery);
                attackTimeSynchronized = true;
                Creature.Motor?.SynchronizeGameplayAction(attackTime);
            }
            return;
        }
        if (State.State != ActionState.Windup || State.PendingAbility == "") return;
        var ability = world.FindAbility(State.PendingAbility);
        if (ability != null)
        {
            attackTime = Math.Clamp(ability.Windup - State.StateRemaining, 0, ability.Windup);
            attackTimeSynchronized = true;
            Creature.Motor?.SynchronizeGameplayAction(attackTime);
        }
        if (State.FreeSlash || ability?.Geometry?.Shape == "cone")
        {
            if (CombatGeometry.TryNormalize(new System.Numerics.Vector2(State.ActionDirectionX, State.ActionDirectionY), out var direction))
                Creature.LockFacingYaw(PixelCamera.GroundVelocityToYaw(direction.X, direction.Y));
        }
        else if (world.Actors.TryGetValue(State.Target, out var target) && target.Region == State.Region && target.Id != State.Id)
        {
            var delta = target.Position - State.Position;
            if (delta.LengthSquared() > .001f) Creature.FaceYaw(PixelCamera.GroundVelocityToYaw(delta.X, delta.Y));
        }
    }

    public override void _Draw()
    {
        if (attackArc && attackTime < attackDuration && State.Alive)
        {
            // Presentation only: the authoritative ability still owns range, damage and timing.
            float t = attackTime - impactTime;
            if (t >= -.06f && t <= .14f)
            {
                float alpha = t < 0 ? (t + .06f) / .06f : 1 - t / .14f;
                float angle = attackDirection.Angle();
                DrawArc(new Vector2(0, -14), 30, angle - 1.0f, angle + 1.0f, 20, new Color(1, .87f, .5f, alpha), 4, true);
                DrawArc(new Vector2(0, -14), 35, angle - .7f, angle + .8f, 16, new Color(1, 1, .85f, alpha * .7f), 2, true);
            }
        }
        if (!State.Alive)
        {
            return;
        }
        if (State.Health < State.MaxHealth)
        {
            DrawRect(new Rect2(-14, -50, 28, 3), new Color(.15f, .1f, .12f));
            DrawRect(new Rect2(-14, -50, 28 * State.Health / State.MaxHealth, 3), new Color(.76f, .29f, .22f));
        }

        if (State.Statuses.Count > 0)
            DrawCircle(new Vector2(0, -54), 2, new Color(1, .6f, .2f));
    }
}
