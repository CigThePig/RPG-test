using Godot;
using Ground = System.Numerics.Vector2;

namespace Mosslight;

/// <summary>Consumes world observations. This coordinator never changes simulation state.</summary>
public sealed class CombatPresentationController : IDisposable
{
    readonly World world;
    readonly Dictionary<string, CharacterView> views;
    readonly CombatEffects effects;
    readonly CameraFeedback camera;
    readonly Node audio;
    (string Actor, string Target, string Ability)? avoided;
    long avoidanceTick = long.MinValue, routTick = long.MinValue;
    CombatPresentationEvent? pendingProjectileImpact;

    public CombatPresentationController(World world, Dictionary<string, CharacterView> views,
        CombatEffects effects, CameraFeedback camera, Node audio)
    {
        this.world = world; this.views = views; this.effects = effects; this.camera = camera; this.audio = audio;
        world.Presentation += Handle;
    }
    public void Dispose() => world.Presentation -= Handle;
    public void Reset()
    {
        avoided = null; avoidanceTick = routTick = long.MinValue; pendingProjectileImpact = null; effects.Reset(); camera.Reset();
        if (GodotObject.IsInstanceValid(audio)) audio.Call("stop_effects");
    }
    bool Nearby(Ground at, float range) => world.Actors.TryGetValue("player", out var player) &&
        Ground.DistanceSquared(player.Position, at) <= range * range;
    void Sound(string kind, Ground at) { if (Nearby(at, 500) && GodotObject.IsInstanceValid(audio)) audio.Call("cue", kind); }
    bool Visible(string id) => world.Actors.TryGetValue(id, out var actor) &&
        world.Actors.TryGetValue("player", out var player) && actor.Region == player.Region;
    CharacterView? View(string id) => views.TryGetValue(id, out var view) && GodotObject.IsInstanceValid(view) ? view : null;
    static Ground Normalize(Ground v) => v.LengthSquared() > .000001f ? Ground.Normalize(v) : Ground.Zero;
    Ground Direction(CombatPresentationEvent e)
    {
        var dir = Normalize(e.Direction);
        if (dir != Ground.Zero) return dir;
        dir = Normalize(e.Position - e.Origin);
        if (dir != Ground.Zero) return dir;
        var motor = View(e.ActorId)?.Creature.Motor;
        return motor == null ? Ground.UnitX : new Ground((float)Math.Cos(motor.Yaw), (float)-Math.Sin(motor.Yaw));
    }
    Ground Contact(CombatPresentationEvent e)
    {
        var towardSource = Normalize(e.Origin - e.Position);
        float radius = world.Actors.TryGetValue(e.TargetId, out var target) ? Math.Min(8, target.Radius * .55f) : 0;
        return e.Position + towardSource * radius;
    }
    bool Heavy(AbilityDef? ability) => ability != null && ability.Type == "physical" &&
        (ability.Damage >= 18 || ability.Geometry?.Shape == "cone");
    void Emit(CombatEffectKind kind, CombatPresentationEvent e, Ground position) =>
        effects.Emit(kind, position, Direction(e), e.Tick, e.ActorId, e.TargetId);

    static bool SameImpact(CombatPresentationEvent impact, CombatPresentationEvent e) =>
        impact.Tick == e.Tick && impact.ActorId == e.ActorId && impact.TargetId == e.TargetId && impact.AbilityId == e.AbilityId;
    static bool ImpactFollowup(CombatPresentationEvent impact, CombatPresentationEvent e) =>
        (SameImpact(impact,e) && (e.Kind is CombatPresentationKind.Hit or CombatPresentationKind.Miss or CombatPresentationKind.DefensiveAvoidance)) ||
        (impact.Tick == e.Tick && e.ActorId == impact.TargetId && (e.Kind is CombatPresentationKind.AttackCanceled or CombatPresentationKind.DodgeCanceled));

    /// <summary>Present a collision with no damage outcome, after the world has finished the current step.</summary>
    public void FlushPending()
    {
        if (pendingProjectileImpact is not { } impact) return;
        pendingProjectileImpact = null;
        Emit(impact.Outcome is "terrain" or "boundary" ? CombatEffectKind.TerrainCollision : CombatEffectKind.ProjectileCollision, impact, impact.Position);
        Sound("projectile_impact", impact.Position);
    }

    public void Handle(CombatPresentationEvent e)
    {
        if (pendingProjectileImpact is { } pending && !ImpactFollowup(pending,e)) FlushPending();
        if (avoidanceTick != e.Tick) { avoided = null; avoidanceTick = e.Tick; }
        string subject = e.Kind is CombatPresentationKind.Hit or CombatPresentationKind.Death or CombatPresentationKind.GuardBroken ? e.TargetId : e.ActorId;
        // A glob lands where it was thrown, whether or not its thrower is still about.
        if (e.Kind == CombatPresentationKind.BlastLanded)
        {
            if (world.Actors.TryGetValue("player", out var here) && world.Hazards.Any(h => h.Region == here.Region && h.Ability == e.AbilityId && h.Position == e.Position) ||
                Visible(e.ActorId))
            {
                effects.Emit(CombatEffectKind.Splash, e.Position, Ground.Zero, e.Tick, e.ActorId, e.AbilityId, world.FindAbility(e.AbilityId)?.Blast?.Radius ?? 30);
                Sound("splash", e.Position);
                if (Nearby(e.Position, 120)) camera.Impact(1.4f, e.Tick);
            }
            return;
        }
        if (!Visible(subject)) return;
        var ability = world.FindAbility(e.AbilityId);
        var view = View(subject);
        switch (e.Kind)
        {
            case CombatPresentationKind.AttackStarted:
                if (ability == null) break;
                world.Actors.TryGetValue(e.TargetId, out var target);
                view?.Cue(ability.Damage < 0 ? "cast" : "attack", target, ability);
                if (ability.ProjectileSpeed > 0) break; // release has its own cue
                // Sword windup is silent. The air-only whoosh belongs to the
                // authoritative strike boundary, whether it hits or misses.
                if (e.AbilityId != "slash")
                    Sound(ability.Type == "healing" ? "cast" : ability.ActionStyle == "Roar" ? "roar" : ability.ActionStyle == "Bite" ? "bite" : "attack", e.Origin);
                break;
            case CombatPresentationKind.AttackCommitted:
                // Commitment starts no second animation or audio cue.
                break;
            case CombatPresentationKind.AttackImpact:
                // This is an attempted strike, not confirmation of a hit.
                if (e.ActorId == "player" && e.AbilityId == "slash")
                    Sound("swing", e.Origin);
                break;
            case CombatPresentationKind.AttackCanceled:
                view?.Cue("cancel");
                Emit(CombatEffectKind.Interrupt, e, e.Origin);
                break;
            case CombatPresentationKind.ActionCompleted:
                view?.Cue("completed");
                break;
            case CombatPresentationKind.DodgeStarted:
                view?.Cue("dodge"); Sound("dodge", e.Origin);
                break;
            case CombatPresentationKind.DodgeCanceled:
                break;
            case CombatPresentationKind.DefensiveAvoidance:
                // World resolves avoidance immediately before its corresponding miss.
                avoided = (e.ActorId, e.TargetId, e.AbilityId);
                if (e.Outcome == "dodge" && Visible(e.TargetId))
                {
                    Emit(CombatEffectKind.Dodge, e, e.Position); Sound("avoid", e.Position);
                }
                break;
            case CombatPresentationKind.Miss:
                bool wasAvoided = avoided == (e.ActorId, e.TargetId, e.AbilityId);
                avoided = null;
                if (pendingProjectileImpact is { } missedImpact && SameImpact(missedImpact,e))
                {
                    if (wasAvoided) pendingProjectileImpact = null;
                    else FlushPending();
                    break;
                }
                if (wasAvoided) break;
                if (ability?.ProjectileSpeed > 0) break;
                // The sword's faint blade trail and air whoosh already say
                // "swing". A second ring and noise on a miss read as a hit.
                if (e.ActorId == "player" && e.AbilityId == "slash") break;
                var dir = Direction(e);
                float reach = Math.Clamp(ability?.Range ?? 24, 12, 45);
                var offset = e.Position - e.Origin;
                var at = offset.LengthSquared() > .01f ? e.Origin + Normalize(offset) * Math.Min(offset.Length(), reach) : e.Origin + dir * reach;
                Emit(CombatEffectKind.Whiff, e, at); Sound("whiff", at);
                break;
            case CombatPresentationKind.ProjectileReleased:
                Emit(CombatEffectKind.ProjectileRelease, e, e.Position); Sound("release", e.Position);
                break;
            case CombatPresentationKind.ProjectileImpact:
                // World immediately reports Hit, avoidance or Miss. Coalesce the
                // collision and damage into one burst, or suppress it for avoidance.
                pendingProjectileImpact = e;
                break;
            case CombatPresentationKind.Hit:
                bool periodic = e.Outcome == "periodic";
                bool heavy = Heavy(ability);
                bool blocked = e.Outcome == "blocked";
                bool guarded = e.Outcome == "guarded" || blocked;
                if (e.Outcome == "ambush") heavy = true;
                // Read the resolved state: ambush/periodic outcomes can also leave a guarded action running.
                bool actionSurvives = world.Actors.TryGetValue(e.TargetId, out var hitActor) &&
                    hitActor.State is ActionState.Windup or ActionState.Recovery or ActionState.Dodging;
                view?.ReactToHit(e.Origin, e.Position, guarded ? .45f : heavy ? 1.35f : periodic ? .55f : 1f, !actionSurvives);
                if (pendingProjectileImpact is { } hitImpact && SameImpact(hitImpact,e))
                {
                    pendingProjectileImpact = null;
                    Emit(CombatEffectKind.ProjectileCollision, hitImpact, hitImpact.Position);
                }
                else Emit(periodic ? CombatEffectKind.Periodic : blocked ? CombatEffectKind.Blocked : guarded ? CombatEffectKind.Guarded : heavy ? CombatEffectKind.HeavyHit : CombatEffectKind.Hit, e, Contact(e));
                if (blocked) view?.React(PixelCreatures.Core.Motion.ReactionKind.Block);
                // Say the things a player needs to learn from: why that hit was big, or why it did nothing.
                if (e.ActorId == "player" && e.TargetId != "player")
                {
                    if (e.Outcome == "ambush") effects.Say("AMBUSH", e.Position, e.Tick, e.TargetId, new Color(1f, .86f, .4f));
                    else if (blocked) effects.Say("BLOCKED", e.Position, e.Tick, e.TargetId, new Color(.78f, .9f, 1f));
                }
                if (!periodic)
                {
                    Sound(e.TargetId == "player" ? "player_hit" : blocked ? "block" : guarded ? "guard" : ability?.ProjectileSpeed > 0 ? "projectile_impact" : heavy ? "heavy_hit" : "hit", e.Position);
                    if (Nearby(e.Position, 240)) camera.Impact(e.TargetId == "player" ? 2.2f : heavy ? 1.8f : 1.1f, e.Tick);
                }
                break;
            case CombatPresentationKind.Death:
                view?.BeginDefeat(e.Origin, e.Position);
                Emit(CombatEffectKind.Death, e, e.Position); Sound("death", e.Position);
                break;
            case CombatPresentationKind.Alerted:
                world.Actors.TryGetValue(e.TargetId, out var quarry);
                view?.Alert(e.Outcome, quarry);
                // Only the one who actually spots the intruder cries out; the called simply start.
                if (e.Outcome != "called") { Emit(CombatEffectKind.Alert, e, e.Origin); Sound("alert", e.Origin); }
                break;
            case CombatPresentationKind.AwarenessChanged:
                // The overhead mark reads actor state directly; nothing to trigger here.
                break;
            case CombatPresentationKind.GuardBroken:
            {
                var broken = View(e.TargetId);
                broken?.React(PixelCreatures.Core.Motion.ReactionKind.Reel);
                broken?.Overlay?.GuardBroken();
                if (Visible(e.TargetId))
                {
                    effects.Say("GUARD BROKEN", e.Position, e.Tick, e.TargetId, new Color(.8f, .93f, 1f));
                    Emit(CombatEffectKind.GuardBreak, e, e.Position); Sound("guard_break", e.Position);
                    if (Nearby(e.Position, 240)) camera.Impact(2f, e.Tick);
                }
                break;
            }
            case CombatPresentationKind.ChargeStarted:
                Sound("charge", e.Origin);
                break;
            case CombatPresentationKind.ChargeCrashed:
                view?.React(PixelCreatures.Core.Motion.ReactionKind.Crash);
                Emit(CombatEffectKind.Crash, e, e.Origin + Direction(e) * 10); Sound("crash", e.Origin);
                if (Nearby(e.Origin, 240)) camera.Impact(2.4f, e.Tick);
                break;
            case CombatPresentationKind.Rallied:
            {
                bool mends = ability != null && world.Content.Statuses.Any(s => s.Id == ability.Status && s.Heal > 0);
                effects.Emit(CombatEffectKind.Rally, e.Origin, Direction(e), e.Tick, e.ActorId, e.TargetId, ability?.AllyRadius ?? 120);
                if (!mends) break;
                // Everyone it reached shows it.
                foreach (var mended in world.Actors.Values)
                    if (mended.Alive && Visible(mended.Id) && mended.Statuses.Any(s => s.Id == ability!.Status && s.Source == e.ActorId) && Ground.Distance(mended.Position, e.Origin) <= ability!.AllyRadius)
                        effects.Emit(CombatEffectKind.Mend, mended.Position, Ground.Zero, e.Tick, e.ActorId, mended.Id);
                Sound("mend", e.Origin);
                break;
            }
            case CombatPresentationKind.Heard:
                view?.Overlay?.Heard();
                effects.Emit(CombatEffectKind.Ripple, e.Position, Ground.Zero, e.Tick, e.ActorId, "heard");
                Sound("heard", e.Origin);
                break;
            case CombatPresentationKind.Disturbed:
                view?.Overlay?.Heard();
                if (e.Outcome == "body") { view?.React(PixelCreatures.Core.Motion.ReactionKind.Startle); Sound("heard", e.Origin); }
                break;
            case CombatPresentationKind.Woke:
                view?.React(PixelCreatures.Core.Motion.ReactionKind.Startle);
                Sound("wake", e.Origin);
                break;
            case CombatPresentationKind.Shaken:
                view?.React(PixelCreatures.Core.Motion.ReactionKind.Cower);
                Emit(CombatEffectKind.Shaken, e, e.Origin);
                // One word for the whole pack, over the first of them.
                if (e.Outcome == "leader" && routTick != e.Tick) { routTick = e.Tick; effects.Say("ROUTED", e.Origin, e.Tick, e.ActorId, new Color(.66f, .85f, 1f)); }
                Sound("whimper", e.Origin);
                break;
            case CombatPresentationKind.BlastLaunched:
                Emit(CombatEffectKind.ProjectileRelease, e, e.Origin); Sound("lob", e.Origin);
                break;
        }
    }
}
