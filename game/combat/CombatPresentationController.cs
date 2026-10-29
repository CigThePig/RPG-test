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
    long avoidanceTick = long.MinValue;
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
        avoided = null; avoidanceTick = long.MinValue; pendingProjectileImpact = null; effects.Reset(); camera.Reset();
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
        Emit(CombatEffectKind.ProjectileCollision, impact, impact.Position);
        Sound("projectile_impact", impact.Position);
    }

    public void Handle(CombatPresentationEvent e)
    {
        if (pendingProjectileImpact is { } pending && !ImpactFollowup(pending,e)) FlushPending();
        if (avoidanceTick != e.Tick) { avoided = null; avoidanceTick = e.Tick; }
        string subject = e.Kind is CombatPresentationKind.Hit or CombatPresentationKind.Death ? e.TargetId : e.ActorId;
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
                    Sound(ability.Type == "healing" ? "cast" : ability.ActionStyle == "Bite" ? "bite" : "attack", e.Origin);
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
                view?.ReactToHit(e.Origin, e.Position, heavy ? 1.35f : periodic ? .55f : 1f);
                if (pendingProjectileImpact is { } hitImpact && SameImpact(hitImpact,e))
                {
                    pendingProjectileImpact = null;
                    Emit(CombatEffectKind.ProjectileCollision, hitImpact, hitImpact.Position);
                }
                else Emit(periodic ? CombatEffectKind.Periodic : heavy ? CombatEffectKind.HeavyHit : CombatEffectKind.Hit, e, Contact(e));
                if (!periodic)
                {
                    Sound(e.TargetId == "player" ? "player_hit" : ability?.ProjectileSpeed > 0 ? "projectile_impact" : heavy ? "heavy_hit" : "hit", e.Position);
                    if (Nearby(e.Position, 240)) camera.Impact(e.TargetId == "player" ? 2.2f : heavy ? 1.8f : 1.1f, e.Tick);
                }
                break;
            case CombatPresentationKind.Death:
                view?.BeginDefeat();
                Emit(CombatEffectKind.Death, e, e.Position); Sound("death", e.Position);
                break;
        }
    }
}
