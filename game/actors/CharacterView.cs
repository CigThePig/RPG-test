using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Motion;

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
    /// <summary>Ground dust for enemy charges, leaps and hops. Presentation only.</summary>
    public RollDustOverlay? Dust { get; private set; }
    public ActorOverlay? Overlay { get; private set; }
    /// <summary>Read-only source of ability and profile definitions for presentation.</summary>
    public World? World { get; set; }
    public Func<bool>? ReducedMotion { get; set; }
    public StanceKind Stance { get; private set; }
    LootMarker? lootMarker;
    public bool LootMarkerVisible => lootMarker?.Visible ?? false;
    Vector2 previous;
    float attackTime, attackDuration, impactTime;
    Vector2 attackDirection = Vector2.Right;
    bool attackArc;
    bool attackTimeSynchronized;
    bool previousWasDodging;
    float presentationTime;
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
        else
        {
            Dust = new RollDustOverlay();
            AddChild(Dust);
            Overlay = new ActorOverlay { View = this, ZIndex = 1 };
            AddChild(Overlay);
        }
        var result = actor.Appearance.GenomeJson == "" ? null : CreatureRuntime.Serializer.Deserialize(actor.Appearance.GenomeJson);
        if (result != null && !result.Ok)
            throw new InvalidDataException(result.Describe());
        var genome = result?.Genome ?? CreatureRuntime.Sample(actor.Appearance.Family, actor.Appearance.Seed);
        Creature.SetGenome(genome, async: false);
        float body = (float)Creature.Model!.Anatomy.Metrics.BodyLength;
        Creature.Scale = Vector2.One * Math.Clamp(actor.Radius / Math.Max(1, body * .2f), .25f, 1);
        previous = new(actor.X, actor.Y);
        // A corpse found on loading lies as it fell, without replaying the collapse.
        if (!actor.Alive) Creature.SnapDead();
        Sync(1f / 60);
    }

    public void SnapPosition() { previous=new Vector2(State.X,State.Y); Creature.Teleport(previous); Creature.MoveVelocity=Vector2.Zero; RollDust?.Reset(); Dust?.Reset(); }

    AbilityDef? CurrentAbility => World?.FindAbility(State.State == ActionState.Windup ? State.PendingAbility : State.State == ActionState.Recovery ? State.RecoveryAbility : "");

    /// <summary>Running down a committed charge lane right now.</summary>
    public bool Charging => !State.ActionConnected && State.State == ActionState.Recovery && CurrentAbility is { Charge: { } charge } ability &&
        State.ActionElapsed >= ability.Windup && State.ActionElapsed - ability.Windup < charge.Duration;

    // The posture is a pure reading of saved actor state, so a restored view shows the same mood.
    StanceKind ReadStance()
    {
        if (!State.Alive || State.Id == "player") return StanceKind.Relaxed;
        if (Charging) return StanceKind.Charge;
        switch (State.Awareness)
        {
            case Awareness.Suspicious: return StanceKind.Wary;
            case Awareness.Searching: return StanceKind.Search;
            case Awareness.Alerted: return StanceKind.Combat;
            case Awareness.Engaged:
                if (State.Statuses.Any(s => s.Id == "rage")) return StanceKind.Enraged;
                return State.TacticalIntent == "retreat" && State.State is ActionState.Idle or ActionState.Moving ? StanceKind.Flee : StanceKind.Combat;
            default: return StanceKind.Relaxed;
        }
    }

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
        // A leap is carried by its own animation, not by a running cycle in mid-air.
        bool leaping = State.State == ActionState.Windup && CurrentAbility?.Lunge != null;
        bool combatDisplacement = dodgeDisplacement || State.FreeSlash || leaping;
        presentationTime += dt;
        bool player = State.Id == "player";
        Stance = ReadStance();
        Creature.Stance = Stance;
        // Foes in a fight are sampled a little more often than a calm crowd.
        if (!player) Creature.AnimationFps = State.Alive && State.Awareness is Awareness.Alerted or Awareness.Engaged ? 15 : 10;
        Creature.UseMeasuredExternalVelocity = combatDisplacement || PreferMeasuredExternalVelocity;
        Creature.MoveVelocity = combatDisplacement || State.State is ActionState.Staggered or ActionState.Dead ? Vector2.Zero : (now - previous) / Math.Max(dt, .001f);
        bool free = State.State is ActionState.Idle or ActionState.Moving;
        // Non-player actors look where the simulation says they look: at their quarry while
        // circling or backing off, along their path otherwise. Unset facing keeps the old rule.
        bool simFacing = !player && State.Alive && (State.FacingX != 0 || State.FacingY != 0);
        if (free && simFacing) Creature.FaceYaw(PixelCamera.GroundVelocityToYaw(State.FacingX, State.FacingY));
        else if (free && Creature.MoveVelocity.LengthSquared() > .25f) Creature.ClearFacingRequest();
        if (Creature.Motor != null)
        {
            var motor = Creature.Motor;
            motor.Options.FaceVelocity = free && !simFacing && Creature.MoveVelocity.LengthSquared() > .25f;
            float dodgeDuration = player ? CombatTuning.DodgeDuration : CombatTuning.EvadeDuration;
            motor.DodgeProgress = State.State == ActionState.Dodging ? State.ActionElapsed / dodgeDuration :
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
        if (Dust != null)
        {
            // Dust rises where a charge tears along, where a leap takes off and lands, and under a hop.
            var ability = CurrentAbility;
            bool kicking = Charging || State.State == ActionState.Dodging ||
                (leaping && State.ActionElapsed >= ability!.Lunge!.Start && State.ActionElapsed < ability.Lunge.End);
            float progress = Charging ? (State.ActionElapsed - ability!.Windup) / ability.Charge!.Duration
                : State.State == ActionState.Dodging ? State.ActionElapsed / CombatTuning.EvadeDuration
                : kicking ? (State.ActionElapsed - ability!.Lunge!.Start) / (ability.Lunge.End - ability.Lunge.Start) : 0;
            Dust.Advance(dt, State.Alive && kicking, progress, now, new Vector2(State.ActionDirectionX, State.ActionDirectionY),
                GroundTerrain?.Invoke(State) ?? Terrain.Floor);
        }
        Position = GodotConvert.GroundToScreen(State.X, State.Y);
        // A merchant's "rest" schedule means off-duty, not physically slumped
        // or asleep on the path. Reserve the PPC rest pose for other creatures
        // and for an ambusher lying in wait, so starting Reach wardens stand upright.
        bool visualRest = (State.Activity == "rest" && State.Profession != "merchant") || (State.Activity == "ambush" && State.Awareness == Awareness.Unaware);
        Creature.Resting = State.Alive && visualRest && (State.State is ActionState.Idle or ActionState.Moving);
        if (State.Alive)
        {
            if (Creature.Dead) Creature.Revive();
            Creature.Modulate = Colors.White;
        }
        else
        {
            // Killed with no observed blow (a restored or scripted defeat still shown standing).
            if (!Creature.Dead) Creature.Die(Vector2.Left);
            float collapse = (float)(Creature.Motor?.DeathProgress ?? 1);
            Creature.Modulate = Colors.White.Lerp(new Color(.45f, .4f, .4f, .72f), Math.Clamp((collapse - .35f) / .65f, 0, 1));
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
            if (State.FreeSlash || (ability?.Geometry?.Shape is "cone" or "line"))
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
            // A charge's gesture is only its windup; the run that follows is real locomotion.
            if (ability != null) Creature.TriggerGameplayAction(ability.Windup, ability.Charge != null ? .15f : ability.Recovery, Style(ability), elapsed);
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
    public void ReactToHit(System.Numerics.Vector2 origin, System.Numerics.Vector2 impact, float strength, bool interruptAction = true)
    {
        if (Creature.Motor != null) Creature.Motor.Options.FaceVelocity = false;
        var from = new Vector2(origin.X - impact.X, origin.Y - impact.Y);
        Creature.TriggerHit(from.LengthSquared() > .001f ? from.Normalized() : Vector2.Left, strength, interruptAction);
        if (interruptAction) attackDuration = 0;
    }

    /// <summary>Collapse away from the blow. Captured positions; the corpse itself is never moved.</summary>
    public void BeginDefeat(System.Numerics.Vector2 origin, System.Numerics.Vector2 impact)
    {
        var from = new Vector2(origin.X - impact.X, origin.Y - impact.Y);
        Creature.Die(from.LengthSquared() > .001f ? from.Normalized() : Vector2.Left);
    }

    /// <summary>The moment of noticing: the one who sees barks, the ones called start.</summary>
    public void Alert(string cause, ActorState? quarry)
    {
        Overlay?.Pop();
        if (!State.Alive || State.State is not (ActionState.Idle or ActionState.Moving)) return;
        if (quarry != null)
        {
            var delta = quarry.Position - State.Position;
            if (delta.LengthSquared() > .001f) Creature.FaceYaw(PixelCamera.GroundVelocityToYaw(delta.X, delta.Y));
        }
        if (cause == "sighted")
        {
            float beat = Math.Max(.3f, World?.FindProfile(State.CombatProfile)?.NoticeSeconds ?? .45f);
            Creature.TriggerGameplayAction(beat * .45f, beat * .55f, ActionStyle.Roar);
        }
        else if (cause == "called") React(ReactionKind.Startle);
    }

    public void React(ReactionKind kind)
    {
        if (!State.Alive) return;
        Creature.TriggerReaction(kind);
    }

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
    // target-locked strike tracks its target; a cone or charge keeps its committed yaw.
    public void SyncCombatFacing(World world)
    {
        if (State.State == ActionState.Recovery && State.RecoveryAbility != "")
        {
            var recovery = world.FindAbility(State.RecoveryAbility);
            if (recovery != null)
            {
                if ((recovery.Geometry?.Shape is "cone" or "line") &&
                    CombatGeometry.TryNormalize(new System.Numerics.Vector2(State.ActionDirectionX, State.ActionDirectionY), out var direction))
                    Creature.LockFacingYaw(PixelCamera.GroundVelocityToYaw(direction.X, direction.Y));
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
        if (State.FreeSlash || (ability?.Geometry?.Shape is "cone" or "line"))
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
            // Air movement only. Sweep a short, translucent blade streak
            // instead of the broad gold burst that resembled a hit on empty air.
            // A confirmed World.Hit owns impact flashes independently.
            float t = attackTime - impactTime;
            if (t >= -.055f && t <= .115f)
            {
                float phase = Math.Clamp((t + .055f) / .17f, 0, 1);
                float alpha = MathF.Sin(phase * MathF.PI) * .42f;
                float angle = attackDirection.Angle() - .8f + phase * 1.6f;
                DrawArc(new Vector2(0, -14), 31, angle - .28f, angle + .28f, 8,
                    new Color(.78f, .9f, 1f, alpha), 2.2f, true);
            }
        }
        if (!State.Alive)
        {
            return;
        }
        DrawTemper();
        if (State.Health < State.MaxHealth)
        {
            DrawRect(new Rect2(-14, -50, 28, 3), new Color(.15f, .1f, .12f));
            DrawRect(new Rect2(-14, -50, 28 * State.Health / State.MaxHealth, 3), new Color(.76f, .29f, .22f));
        }

        // One pip per status, coloured by what it does.
        for (int i = 0; i < State.Statuses.Count; i++)
            DrawCircle(new Vector2((i - (State.Statuses.Count - 1) * .5f) * 6, -54), 2, StatusColor(State.Statuses[i].Id));
    }

    public static Color StatusColor(string id) => id switch
    {
        "venom" => new Color(.5f, .95f, .35f),
        "ward" => new Color(.55f, .8f, 1f),
        "rage" => new Color(1f, .3f, .25f),
        "frenzy" => new Color(1f, .85f, .3f),
        _ => new Color(1, .6f, .2f)
    };

    // Ground marks under the body: a red ring for rage, an amber one for a rallied frenzy.
    void DrawTemper()
    {
        bool rage = State.Statuses.Any(s => s.Id == "rage"), frenzy = State.Statuses.Any(s => s.Id == "frenzy");
        if (!rage && !frenzy) return;
        bool still = ReducedMotion?.Invoke() == true;
        float pulse = still ? .5f : .5f + .5f * MathF.Sin(presentationTime * (rage ? 9f : 6f));
        var color = StatusColor(rage ? "rage" : "frenzy");
        float radius = State.Radius + 7 + (still ? 0 : pulse * 2);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1, .5f));
        DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 28, new Color(.12f, .04f, .03f, .7f), 4, true);
        DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 28, new Color(color, .55f + .35f * pulse), 2, true);
        DrawSetTransform(Vector2.Zero);
        if (still) return;
        // A few motes rising off the body, phased from the actor's identity.
        int key = Mosslight.World.StableId(State.Id);
        for (int i = 0; i < 4; i++)
        {
            float phase = (presentationTime * .9f + EnemyMind.Hash01(key, i, 41)) % 1f;
            float x = (EnemyMind.Hash01(key, i, 42) - .5f) * (State.Radius * 2 + 8);
            DrawRect(new Rect2(x, -6 - phase * 30, 2, 2), new Color(color, (1 - phase) * .8f));
        }
    }
}
