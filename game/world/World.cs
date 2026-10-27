using System.Numerics;
using MHLab.Spells;
using MHLab.Spells.Definitions;

namespace Mosslight;
public sealed class World
{
    public int Seed { get; set; }
    public long Tick { get; set; }
    public double Time { get; set; }
    public Dictionary<string, ActorState> Actors { get; set; } = new();
    public Dictionary<string, RegionState> RegionsById { get; set; } = new();
    public List<ProjectileState> Projectiles { get; set; } = new();
    public Content Content { get; }
    public Items Items { get; }
    public EventStream Events { get; } = new();
    public string InteractionFeedback { get; private set; } = "Approach an object or fallen foe";
    public string LastBlockedReason { get; private set; } = "";
    public Vector2 RequestedVelocity { get; private set; }
    public int CollisionAttempts { get; private set; }
    public int CollisionSlides { get; private set; }
    public Vector2 LastInput { get; private set; }
    public Vector2 ActualVelocity { get; private set; }
    public ActorState Player => Actors["player"];
    public SpellsContext Spells { get; private set; } = new();

    readonly Dictionary<string, Spell> spells = new();
    public Godot.Node? Planner { get; set; }

    public event Action<string, string, string>? Presentation;
    // Separate from narrative events: opt-in, bounded by the existing trace writer.
    public event Action<string, Vector2, string>? CombatDiagnostic;
    public World(Content content, Godot.Node itemService, int seed = 42)
    {
        Content = content;
        Items = new(itemService, Events);
        Seed = seed;
        RebuildSpells();
    }

    public void RebuildSpells()
    {
        Spells = new();
        spells.Clear();
        foreach (var d in Content.Abilities)
            spells[d.Id] = new Spell(new RpgAbility(this, d));
    }

    public float Stat(ActorState actor, string stat)
    {
        float value = stat switch
        {
            "strength" => actor.Strength,
            "defence" => actor.Defence,
            "speed" => actor.Speed,
            _ => 0
        };
        value += Items.Bonus(actor, stat);
        foreach (var skill in Content.Skills.Where(s => s.Stat == stat))
            value += actor.SkillRanks.GetValueOrDefault(skill.Id) * skill.PerRank;
        foreach (var status in actor.Statuses)
        {
            var definition = Content.Statuses.Single(d => d.Id == status.Id);
            if (definition.ModifierStat == stat)
                value += definition.Modifier * status.Stacks;
        }

        return value;
    }

    public bool Hostile(ActorState a, ActorState b) => a.Id != b.Id && (Content.Factions.GetValueOrDefault(a.Faction + ":" + b.Faction) + a.Reputation.GetValueOrDefault(b.Faction)) < 0;
    public bool CanActivate(ActorState a, ActorState b, AbilityDef d) => a.Alive && b.Alive && a.Region == b.Region && a.Abilities.Contains(d.Id) && a.State is ActionState.Idle or ActionState.Moving && a.Mana >= d.Cost && a.Cooldowns.GetValueOrDefault(d.Id) <= 0 && (!d.RequiresWeapon || (a.Grasps && Items.Bonus(a, "damage") > 0)) && (d.Self ? a == b : Hostile(a, b)) && (d.Self || Vector2.Distance(a.Position, b.Position) <= d.Range + a.NaturalReach) && LineOfSight(a, b);
    public bool Activate(string id, string ability, string target)
    {
        if (!Actors.TryGetValue(id, out var actor) || !Actors.TryGetValue(target, out var victim) || !spells.TryGetValue(ability, out var spell))
            return false;
        bool ok = Spells.CasterSystem.Cast(actor, new[] { victim }, spell, out _).State == SpellCastState.Success;
        if (ok)
            Spells.Update(0);
        return ok;
    }

    static bool TryDirection(Vector2 value, out Vector2 direction)
    {
        direction = Vector2.Zero;
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.LengthSquared() < .000001f || !float.IsFinite(value.LengthSquared())) return false;
        direction = Vector2.Normalize(value);
        return true;
    }

    /// <summary>Starts only the player sword swing; target selection occurs at impact.</summary>
    public bool TryStartPlayerSlash(Vector2 direction)
    {
        var a = Player;
        var d = Content.Abilities.Single(x => x.Id == "slash");
        if (!TryDirection(direction, out var facing) || !a.Alive || !a.Abilities.Contains(d.Id) ||
            a.State is not (ActionState.Idle or ActionState.Moving) || a.Mana < d.Cost ||
            a.Cooldowns.GetValueOrDefault(d.Id) > 0 ||
            (d.RequiresWeapon && (!a.Grasps || Items.Bonus(a, "damage") <= 0))) return false;
        a.Mana -= d.Cost;
        a.Cooldowns[d.Id] = d.Cooldown;
        a.MotionX = a.MotionY = 0;
        a.State = ActionState.Windup;
        a.StateRemaining = d.Windup;
        a.PendingAbility = d.Id;
        a.Target = "";
        a.FreeSlash = true;
        a.ActionElapsed = 0;
        a.ActionDirectionX = facing.X; a.ActionDirectionY = facing.Y;
        Presentation?.Invoke(a.Id, "attack", "");
        Events.Publish(new("ability_started", a.Id, "", d.Id));
        CombatDiagnostic?.Invoke("attack_started", facing, "slash");
        return true;
    }

    public bool TryStartPlayerDodge(Vector2 direction)
    {
        var a = Player;
        if (!TryDirection(direction, out var facing) || !a.Alive ||
            a.State is not (ActionState.Idle or ActionState.Moving) || a.Cooldowns.GetValueOrDefault("dodge") > 0) return false;
        a.MotionX = a.MotionY = 0;
        a.FreeSlash = false;
        a.ActionElapsed = 0;
        a.ActionDirectionX = facing.X; a.ActionDirectionY = facing.Y;
        a.State = ActionState.Dodging;
        a.StateRemaining = CombatTuning.DodgeDuration;
        a.Cooldowns["dodge"] = CombatTuning.DodgeCooldown;
        ClearPlayerMotion();
        Presentation?.Invoke(a.Id, "dodge", "");
        CombatDiagnostic?.Invoke("dodge_started", facing, "dodge");
        return true;
    }

    public static bool DodgeImmune(ActorState a) => a.State == ActionState.Dodging &&
        a.ActionElapsed >= CombatTuning.DodgeInvulnerableStart &&
        a.ActionElapsed < CombatTuning.DodgeInvulnerableEnd;

    public void CancelPlayerDodge()
    {
        if (Player.State != ActionState.Dodging) return;
        Cancel(Player);
        ClearPlayerMotion();
    }

    void AdvanceDodge(ActorState a, float dt)
    {
        float old = Math.Clamp(a.ActionElapsed / CombatTuning.DodgeDuration, 0, 1);
        a.ActionElapsed = Math.Min(CombatTuning.DodgeDuration, a.ActionElapsed + dt);
        float now = Math.Clamp(a.ActionElapsed / CombatTuning.DodgeDuration, 0, 1);
        float distance = CombatTuning.DodgeDistance * ((1 - (1 - now) * (1 - now)) - (1 - (1 - old) * (1 - old)));
        Move(a, new Vector2(a.ActionDirectionX, a.ActionDirectionY) * distance);
        a.StateRemaining = Math.Max(0, CombatTuning.DodgeDuration - a.ActionElapsed);
    }

    ActorState? PlayerSlashTarget(ActorState attacker, AbilityDef ability)
    {
        var direction = new Vector2(attacker.ActionDirectionX, attacker.ActionDirectionY);
        float maxRange = ability.Range + attacker.NaturalReach + CombatTuning.SlashImpactTolerance;
        float maxDistanceSquared = maxRange * maxRange;
        float minimumDot = MathF.Cos(CombatTuning.SlashArcHalfDegrees * MathF.PI / 180);
        ActorState? best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var candidate in Actors.Values)
        {
            if (!candidate.Alive || candidate.Invulnerable > 0 || DodgeImmune(candidate) || candidate.Region != attacker.Region || !Hostile(attacker, candidate)) continue;
            var offset = candidate.Position - attacker.Position;
            float distance = offset.LengthSquared();
            if (distance > maxDistanceSquared || (distance > .000001f && Vector2.Dot(offset / MathF.Sqrt(distance), direction) < minimumDot) || !LineOfSight(attacker, candidate)) continue;
            if (distance < bestDistance || (distance == bestDistance && best != null && StringComparer.Ordinal.Compare(candidate.Id, best.Id) < 0))
            { best = candidate; bestDistance = distance; }
        }
        return best;
    }

    static float SmoothStep(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    void AdvanceSlashLunge(ActorState a, float dt)
    {
        float previous = a.ActionElapsed;
        a.ActionElapsed += dt;
        float span = CombatTuning.SlashLungeEnd - CombatTuning.SlashLungeStart;
        float distance = CombatTuning.SlashLungeDistance * (SmoothStep((a.ActionElapsed - CombatTuning.SlashLungeStart) / span) - SmoothStep((previous - CombatTuning.SlashLungeStart) / span));
        if (distance <= 0) return;
        var before = a.Position;
        int attempts = CollisionAttempts;
        Move(a, new Vector2(a.ActionDirectionX, a.ActionDirectionY) * distance);
        if (CollisionAttempts != attempts) CombatDiagnostic?.Invoke("attack_lunge_collision", a.Position - before, LastBlockedReason);
    }

    public void BeginAbility(ActorState a, ActorState b, AbilityDef d)
    {
        a.MotionX = 0;
        a.MotionY = 0;
        a.State = ActionState.Windup;
        a.StateRemaining = d.Windup;
        a.PendingAbility = d.Id;
        a.Target = b.Id;
        Presentation?.Invoke(a.Id, d.Damage < 0 ? "cast" : "attack", b.Id);
        Events.Publish(new("ability_started", a.Id, b.Id, d.Id));
    }

    public void Cancel(ActorState a)
    {
        if (a.State == ActionState.Dodging)
        {
            CombatDiagnostic?.Invoke("dodge_interrupted", new Vector2(a.ActionDirectionX, a.ActionDirectionY), "cancel");
            Presentation?.Invoke(a.Id, "dodge_cancel", "");
        }
        a.PendingAbility = "";
        a.FreeSlash = false;
        a.ActionElapsed = 0;
        a.ActionDirectionX = a.ActionDirectionY = 0;
        a.State = a.Alive ? ActionState.Idle : ActionState.Dead;
        a.StateRemaining = 0;
    }

    public bool Damage(ActorState source, ActorState target, float amount, string type, bool periodic = false)
    {
        if (!target.Alive || (!periodic && target.Invulnerable > 0) || (!periodic && source.Region != target.Region) || !Hostile(source, target))
            return false;
        if (!periodic && DodgeImmune(target))
        {
            CombatDiagnostic?.Invoke("direct_hit_dodged", new Vector2(target.ActionDirectionX, target.ActionDirectionY), source.Id);
            return false;
        }
        float resistance = type == "physical" ? Stat(target, "defence") : 0;
        float actual = Math.Max(1, amount - resistance);
        target.Health = Math.Max(0, target.Health - actual);
        target.Invulnerable = .12f;
        Cancel(target);
        target.State = target.Alive ? ActionState.Staggered : ActionState.Dead;
        target.StateRemaining = .22f;
        var away = target.Position - source.Position;
        if (!periodic && away.LengthSquared() > 0)
            Move(target, Vector2.Normalize(away) * 15);
        Presentation?.Invoke(target.Id, "hit", source.Id);
        Events.Publish(new("damage", source.Id, target.Id, type, (int)actual));
        target.Knowledge[source.Id] = "attacker";
        target.Relationships[source.Id] = target.Relationships.GetValueOrDefault(source.Id) - actual;
        if (!target.Alive)
        {
            Items.Service.Call("unequip_all", target.Id);
            target.PendingAbility = "";
            Presentation?.Invoke(target.Id, "dead", source.Id);
            Events.Publish(new("defeated", source.Id, target.Id, target.Profile));
            Progression.Award(this, source, 25);
            var random = new Random(unchecked(Seed + (int)Tick + StableId(target.Id)));
            foreach (var l in Content.Loot)
                if (random.NextDouble() <= l.Chance)
                    Items.Add(target.Id, l.Item, random.Next(l.Min, l.Max + 1));
        }
        return true;
    }

    public static int StableId(string text)
    {
        unchecked
        {
            int hash = 216613626;
            foreach (char c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }

    public void ApplyStatus(ActorState a, string id, string source)
    {
        var d = Content.Statuses.Single(x => x.Id == id);
        var s = a.Statuses.Find(x => x.Id == id);
        if (s == null)
            a.Statuses.Add(new() { Id = id, Source = source, Remaining = d.Duration, UntilTick = d.Interval });
        else
        {
            s.Stacks = Math.Min(d.MaxStacks, s.Stacks + 1);
            s.Remaining = d.Duration;
            s.Source = source;
        }

        if (d.BlocksActions)
        {
            Cancel(a);
            a.State = ActionState.Staggered;
            a.StateRemaining = d.Duration;
        }
    }

    bool Resolve(ActorState a, ActorState b, AbilityDef d, bool projectile = false)
    {
        if (!a.Alive || !b.Alive || a.Region != b.Region)
            return false;
        if (d.RequiresWeapon && (!a.Grasps || Items.Bonus(a, "damage") <= 0))
            return false;
        if (d.Self)
        {
            a.Health = Math.Min(a.MaxHealth, a.Health - d.Damage);
            if (d.Status != "")
                ApplyStatus(a, d.Status, a.Id);
            return true;
        }

        if (!projectile && (Vector2.Distance(a.Position, b.Position) > d.Range + a.NaturalReach + 8 || !LineOfSight(a, b)))
            return false;
        // Intentionally non-damaging hostile effects retain their status-only behavior.
        bool connected = d.Damage <= 0 ? Hostile(a, b) && !DodgeImmune(b) : Damage(a, b, d.Damage + Stat(a, "strength") + Stat(a, d.Type == "physical" ? "damage" : "power"), d.Type);
        if (connected && b.Alive && d.Status != "")
            ApplyStatus(b, d.Status, a.Id);
        return connected;
    }

    public void Step(float dt)
    {
        if (dt <= 0 || dt > .25f || !float.IsFinite(dt))
            throw new ArgumentOutOfRangeException(nameof(dt));
        Tick++;
        Time += dt;
        Spells.Update(dt);
        // Advance before any actor resolves an attack so protection is independent
        // of dictionary iteration order at the fixed-step boundary.
        if (Player.Alive && Player.State == ActionState.Dodging) AdvanceDodge(Player, dt);
        foreach (var a in Actors.Values)
        {
            foreach (var key in a.Cooldowns.Keys.ToArray())
                a.Cooldowns[key] = Math.Max(0, a.Cooldowns[key] - dt);
            a.Invulnerable = Math.Max(0, a.Invulnerable - dt);
            if (!a.Alive)
                continue;
            a.Mana = Math.Min(a.MaxMana, a.Mana + dt * 2);
            foreach (var s in a.Statuses.ToArray())
            {
                var d = Content.Statuses.Single(x => x.Id == s.Id);
                s.Remaining -= dt;
                s.UntilTick -= dt;
                if (s.UntilTick <= 0 && d.Damage > 0 && Actors.TryGetValue(s.Source, out var source))
                {
                    Damage(source, a, d.Damage * s.Stacks, d.Type, periodic: true);
                    s.UntilTick += d.Interval;
                }

                if (s.Remaining <= 0)
                    a.Statuses.Remove(s);
            }

            if (!a.Alive)
                continue;
            if (a.FreeSlash && a.State is ActionState.Windup or ActionState.Recovery)
                AdvanceSlashLunge(a, dt);
            if (a.State == ActionState.Dodging && a.ActionElapsed >= CombatTuning.DodgeDuration)
            {
                CombatDiagnostic?.Invoke("dodge_completed", new Vector2(a.ActionDirectionX, a.ActionDirectionY), "dodge");
                a.State = ActionState.Idle;
                a.StateRemaining = 0;
                a.ActionElapsed = 0;
                a.ActionDirectionX = a.ActionDirectionY = 0;
            }
            if (a.State != ActionState.Dodging && a.StateRemaining > 0)
            {
                a.StateRemaining -= dt;
                if (a.StateRemaining <= 0)
                {
                    if (a.State == ActionState.Windup && a.PendingAbility != "")
                    {
                        var d = Content.Abilities.Single(x => x.Id == a.PendingAbility);
                        if (a.Id == "player" && a.FreeSlash && d.Id == "slash")
                        {
                            var target = PlayerSlashTarget(a, d);
                            bool hit = target != null && Resolve(a, target, d);
                            CombatDiagnostic?.Invoke("attack_impact", new Vector2(a.ActionDirectionX, a.ActionDirectionY), hit ? "hit/" + target!.Id : "miss");
                        }
                        else if (Actors.TryGetValue(a.Target, out var t))
                        {
                            if (d.ProjectileSpeed > 0)
                                Projectiles.Add(new() { Source = a.Id, Target = t.Id, Ability = d.Id, X = a.X, Y = a.Y });
                            else
                                Resolve(a, t, d);
                        }

                        a.PendingAbility = "";
                        a.State = ActionState.Recovery;
                        a.StateRemaining = d.Recovery;
                    }
                    else
                    {
                        if (a.FreeSlash) CombatDiagnostic?.Invoke("attack_completed", new Vector2(a.ActionDirectionX, a.ActionDirectionY), "slash");
                        a.State = ActionState.Idle;
                        a.FreeSlash = false;
                        a.ActionElapsed = 0;
                    }
                }
            }

            if (a.Id != "player")
            {
                bool near = a.Region == Player.Region && Vector2.DistanceSquared(a.Position, Player.Position) < 800 * 800;
                if (near)
                {
                    if (Tick % 12 == 0)
                        Think(a, .2f);
                    if (a.State is ActionState.Idle or ActionState.Moving)
                        Move(a, new Vector2(a.MotionX, a.MotionY) * dt);
                }
                else
                {
                    a.MotionX = 0;
                    a.MotionY = 0;
                    if (Tick % 600 == 0)
                        UpdateSchedule(a);
                }
            }
        }

        foreach (var p in Projectiles.ToArray())
        {
            var d = Content.Abilities.Single(x => x.Id == p.Ability);
            p.Remaining -= dt;
            if (!Actors.TryGetValue(p.Source, out var a) || !Actors.TryGetValue(p.Target, out var b) || !a.Alive || !b.Alive || a.Region != b.Region || p.Remaining <= 0)
            {
                Projectiles.Remove(p);
                continue;
            }

            var at = new Vector2(p.X, p.Y);
            var delta = b.Position - at;
            float distance = d.ProjectileSpeed * dt;
            if (delta.Length() <= distance + b.Radius)
            {
                Resolve(a, b, d, projectile: true);
                Projectiles.Remove(p);
                continue;
            }

            at += Vector2.Normalize(delta) * distance;
            if (RegionsById[a.Region].BlocksSight(RegionsById[a.Region].Index(at)))
            {
                Projectiles.Remove(p);
                continue;
            }

            p.X = at.X;
            p.Y = at.Y;
        }
    }

    public bool VisibleTo(ActorState actor, Vector2 point, bool allowBlockedTarget = false)
    {
        var region = RegionsById[actor.Region];
        int target = region.Index(point);
        int steps = (int)(Vector2.Distance(actor.Position, point) / 4) + 1;
        for (int i = 1; i <= steps; i++)
        {
            int tile = region.Index(Vector2.Lerp(actor.Position, point, (float)i / steps));
            if (allowBlockedTarget && tile == target)
                continue;
            if (region.BlocksSight(tile))
                return false;
        }

        return true;
    }

    public bool LineOfSight(ActorState a, ActorState b) => a.Region == b.Region && VisibleTo(a, b.Position);
    public bool Move(ActorState a, Vector2 delta)
    {
        if (!a.Alive || !float.IsFinite(delta.X) || !float.IsFinite(delta.Y))
            return false;
        var r = RegionsById[a.Region];
        bool moved = false;
        // Small substeps prevent wall tunnelling, including knockback and large frame deltas.
        int steps = Math.Max(1, (int)Math.Ceiling(delta.Length() / Math.Min(2, a.Radius / 2)));
        delta /= steps;
        for (int n = 0; n < steps; n++)
        {
            if (r.CanStand(a.Position + delta, a))
            {
                a.X += delta.X; a.Y += delta.Y;
                moved |= delta.LengthSquared() > 0;
                continue;
            }
            if (a.Id == "player")
            {
                CollisionAttempts++;
                int at = r.BlockingCell(a.Position + delta,a);
                LastBlockedReason = at < 0 ? "Map boundary" : r.Features.GetValueOrDefault(at) == "lock" ? "Locked door" : $"Terrain / footprint: {r.Tiles[at]} at {at%r.Width},{at/r.Width}";
            }
            // Advance to contact before sliding. Previously an entire (up to 2px)
            // substep vanished at contact; deterministic bisection retains the valid
            // part and makes wall clearance less sensitive to approach speed.
            float lo=0,hi=1;
            for(int k=0;k<6;k++) { float mid=(lo+hi)*.5f; if(r.CanStand(a.Position+delta*mid,a))lo=mid;else hi=mid; }
            var accepted=delta*lo;
            a.X+=accepted.X;a.Y+=accepted.Y;moved |= accepted.LengthSquared()>0;
            var remainder=delta*(1-lo);
            // Prefer the larger tangent request rather than always preferring X.
            bool xFirst=Math.Abs(remainder.X)>=Math.Abs(remainder.Y);
            for(int axis=0;axis<2;axis++)
            {
                bool horizontal=(axis==0)==xFirst;
                var slide=horizontal ? new Vector2(remainder.X,0) : new Vector2(0,remainder.Y);
                if(slide.LengthSquared()==0 || !r.CanStand(a.Position+slide,a))continue;
                a.X+=slide.X;a.Y+=slide.Y;moved=true;
                if(a.Id=="player")CollisionSlides++;
            }
        }

        return moved;
    }

    public void ClearPlayerMotion() { ActualVelocity=RequestedVelocity=Vector2.Zero; LastInput=Vector2.Zero; LastBlockedReason=""; CollisionAttempts=CollisionSlides=0; }

    public bool PlayerMove(Vector2 direction, float dt, float speedScale=1, bool projected=false)
    {
        var a = Player;
        ActualVelocity=RequestedVelocity=Vector2.Zero;
        CollisionAttempts=CollisionSlides=0;
        LastInput=float.IsFinite(direction.X) && float.IsFinite(direction.Y) ? direction : Vector2.Zero; LastBlockedReason="";
        if (a.State is not (ActionState.Idle or ActionState.Moving)) { LastBlockedReason="Action: "+a.State; return false; }
        if(!float.IsFinite(dt)||dt<=0||!float.IsFinite(speedScale)||speedScale<0||!float.IsFinite(direction.X)||!float.IsFinite(direction.Y))return false;
        float magnitude=projected ? new Vector2(direction.X,direction.Y*.5f).Length() : direction.Length();
        if(magnitude>1)direction/=magnitude;
        LastInput = direction;
        LastBlockedReason = "";
        var before = a.Position;
        RequestedVelocity=direction * Math.Max(0, Stat(a, "speed")) * speedScale;
        bool moved = Move(a, RequestedVelocity * dt);
        ActualVelocity = dt > 0 ? (a.Position - before) / dt : Vector2.Zero;
        a.State = moved ? ActionState.Moving : ActionState.Idle;
        return moved;
    }

    void UpdateSchedule(ActorState a)
    {
        var profile = Content.NpcProfiles.Find(p => p.Id == a.Profile);
        a.Activity = profile?.Schedule.Where(s => s.Hour <= ((Time / 60) % 24)).OrderBy(s => s.Hour).LastOrDefault()?.Activity ?? "patrol";
    }

    public bool Rest(ActorState a, float dt)
    {
        if (!a.Alive || a.State is not (ActionState.Idle or ActionState.Moving))
            return false;
        a.Health = Math.Min(a.MaxHealth, a.Health + dt);
        return true;
    }

    void Think(ActorState a, float dt)
    {
        if (a.State is not (ActionState.Idle or ActionState.Moving))
            return;
        a.MotionX = 0;
        a.MotionY = 0;
        UpdateSchedule(a);
        var target = Actors.Values.Where(b => b.Alive && b.Region == a.Region && Hostile(a, b) && Vector2.Distance(a.Position, b.Position) < 180 && LineOfSight(a, b)).OrderBy(b => Vector2.DistanceSquared(a.Position, b.Position)).FirstOrDefault();
        string goal = target != null ? (a.Health < a.MaxHealth * .2 ? "safe" : "defeated") : a.Activity;
        string action = Planner?.Call("choose", goal, target != null).AsString() ?? (target != null ? "attack" : a.Activity);
        if (target != null)
        {
            a.Knowledge[target.Id] = "seen";
            a.Target = target.Id;
            if (action == "retreat")
            {
                var away = Vector2.Normalize(a.Position - target.Position) * a.Speed;
                a.MotionX = away.X;
                a.MotionY = away.Y;
                return;
            }

            var ability = Content.Abilities.Where(d => a.Abilities.Contains(d.Id) && !d.Self).OrderByDescending(d => d.Range).FirstOrDefault();
            if (ability != null && Activate(a.Id, ability.Id, target.Id))
                return;
            Follow(a, target.Position, dt);
        }
        else if (action == "patrol")
        {
            var dest = new Vector2(a.HomeX + (float)Math.Sin(Time / 9 + StableId(a.Id) % 9) * 64, a.HomeY + (float)Math.Cos(Time / 13) * 40);
            Follow(a, dest, dt);
        }
        else if (action == "rest")
            Rest(a, dt);
    }

    public void Follow(ActorState a, Vector2 target, float dt)
    {
        a.MotionX = a.MotionY = 0;
        var r = RegionsById[a.Region];
        Vector2 waypoint = target;
        if (!r.CanTravel(a.Position, target, a))
        {
            var path = r.Path(a.Position, target, a);
            if (path.Count == 0) return;
            waypoint = r.Center(path[0]);
        }
        var delta = waypoint - a.Position;
        if (delta.Length() <= .1f) return;
        // Planning happens every .2s. Stop exactly at the waypoint instead of
        // overshooting it and alternating direction on subsequent plans.
        var velocity = Vector2.Normalize(delta) * Math.Min(Math.Max(0, Stat(a, "speed")), delta.Length() / Math.Max(.001f, dt));
        a.MotionX = velocity.X; a.MotionY = velocity.Y;
        a.State = ActionState.Moving;
    }

    public IEnumerable<KeyValuePair<int, string>> InteractionTargets(ActorState a)
    {
        var r = RegionsById[a.Region];
        return r.Features.Where(x => !r.Depleted.Contains(x.Key) && !r.OpenDoors.Contains(x.Key) && Vector2.Distance(r.Center(x.Key), a.Position) < 48 && VisibleTo(a, r.Center(x.Key), allowBlockedTarget: x.Value == "lock"))
            .OrderBy(x => Vector2.DistanceSquared(r.Center(x.Key), a.Position)).ThenBy(x => x.Key);
    }

    public bool Interact(ActorState a)
    {
        if (!a.Alive || a.State is not (ActionState.Idle or ActionState.Moving))
            return false;
        var r = RegionsById[a.Region];
        InteractionFeedback = "No accessible object nearby";
        var nearby = InteractionTargets(a).ToArray();
        foreach (var(at, kind)in nearby)
        {
            if (kind == "lock")
            {
                if (r.OpenDoors.Contains(at))
                    continue;
                if (!Items.Take(a.Id, "dungeon_key", 1))
                { InteractionFeedback = "A ruin key is required"; return false; }
                InteractionFeedback = "Door unlocked";
                r.OpenDoors.Add(at);
                foreach (var adjacent in r.Neighbours(at))
                    if (r.Features.GetValueOrDefault(adjacent) == "lock")
                        r.OpenDoors.Add(adjacent);
                Events.Publish(new("door_unlocked", a.Id, a.Region, at.ToString()));
                return true;
            }

            if (kind is "portal" or "exit")
            {
                InteractionFeedback = kind == "portal" ? "Entered the ruin" : "Returned to the Reach";
                Enter(a, kind == "portal" ? "ruin" : "reach");
                return true;
            }

            if (kind == "chest")
            {
                string owner = a.Region + ":" + at;
                bool changed = Items.LootAll(owner, a.Id);
                if (Items.HasLoot(owner))
                { InteractionFeedback = changed ? "Some loot collected; make room for the rest" : "Inventory full"; return changed; }
                InteractionFeedback = changed ? "Chest emptied — loot added to pack" : "Chest is empty";
            }
            else if (kind is "herb" or "ore" or "wood" or "relic" or "key")
            {
                if (!Items.Add(a.Id, kind == "key" ? "dungeon_key" : kind, kind is "relic" or "key" ? 1 : 3))
                { InteractionFeedback = "Inventory full"; return false; }
                InteractionFeedback = "Collected " + kind;
            }
            else
                continue;
            r.Depleted.Add(at);
            Events.Publish(new("resource_depleted", a.Id, a.Region, kind));
            return true;
        }

        var corpses = Actors.Values.Where(b => !b.Alive && b.Region == a.Region && Vector2.Distance(a.Position, b.Position) < 48 && LineOfSight(a, b) && Items.HasLoot(b.Id)).OrderBy(b => Vector2.DistanceSquared(a.Position, b.Position)).ThenBy(b => b.Id, StringComparer.Ordinal);
        foreach (var corpse in corpses)
            if (Items.LootAll(corpse.Id, a.Id)) { InteractionFeedback = "Loot collected"; return true; }
        if (r.Features.Any(x => x.Value == "chest" && r.Depleted.Contains(x.Key) && Vector2.Distance(a.Position,r.Center(x.Key)) < 48 && VisibleTo(a,r.Center(x.Key))))
            InteractionFeedback = "Chest already empty";
        return false;
    }

    public void Enter(ActorState a, string region)
    {
        Cancel(a);
        if (!RegionsById.ContainsKey(region))
            RegionsById[region] = Mosslight.Regions.Dungeon(Seed ^ 1337);
        a.Region = region;
        var r = RegionsById[region];
        var pos = r.Center(r.Entrance);
        a.X = pos.X;
        a.Y = pos.Y;
        InitializeContainers(r);
        PopulateRegion(region);
        Events.Publish(new("region_entered", a.Id, "", region));
    }

    void PopulateRegion(string region)
    {
        if (region != "ruin" || Actors.Keys.Any(id => id.StartsWith("ruin:spawn:")))
            return;
        using var json = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://game/data/spawns.json"));
        var def = json.RootElement.GetProperty(region);
        int count = def.GetProperty("count").GetInt32();
        var families = def.GetProperty("families").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var r = RegionsById[region];
        var random = new Random(Seed ^ 773);
        var valid = Enumerable.Range(0, r.Tiles.Length).Where(at => r.Tiles[at] == Terrain.Floor && !r.Features.ContainsKey(at) && Vector2.Distance(r.Center(at), r.Center(r.Entrance)) > 100).OrderBy(_ => random.Next()).Take(count).ToArray();
        for (int i = 0; i < valid.Length; i++)
        {
            var pos = r.Center(valid[i]);
            var actor = new ActorState
            {
                Id = "ruin:spawn:" + i,
                Name = "Ruin dweller",
                Region = region,
                Profile = "beast",
                Faction = def.GetProperty("faction").GetString()!,
                Appearance = new()
                {
                    Family = families[i % families.Length],
                    Seed = (ulong)(uint)(Seed + i * 271)
                },
                X = pos.X,
                Y = pos.Y,
                HomeX = pos.X,
                HomeY = pos.Y,
                Grasps = false,
                Abilities = new()
                {
                    def.GetProperty("ability").GetString()!
                }
            };
            AnatomyAdapter.Apply(actor);
            var placed = r.FindPlacement(actor.Position, actor, Actors.Values);
            actor.X = actor.HomeX = placed.X; actor.Y = actor.HomeY = placed.Y;
            Actors[actor.Id] = actor;
        }
    }

    public bool SetAppearance(ActorState actor, Appearance appearance)
    {
        // Stage the body and a safe location before changing live state or equipment.
        // A loss of flight/swimming or a larger radius must not strand the actor.
        var staged = new ActorState { Id = actor.Id, Region = actor.Region, Appearance = new Appearance
        {
            Family = appearance.Family, Seed = appearance.Seed,
            GenomeJson = appearance.GenomeJson, Generator = appearance.Generator
        }, Speed = actor.Speed };
        AnatomyAdapter.Apply(staged);
        var region = RegionsById[actor.Region];
        var position = actor.Position;
        if (!region.CanStand(position, staged))
        {
            try { position = region.FindPlacement(position, staged, Actors.Values); }
            catch (InvalidOperationException) { return false; }
        }
        var home = new Vector2(actor.HomeX, actor.HomeY);
        if (!region.CanStand(home, staged)) home = position;

        Cancel(actor);
        actor.Appearance = staged.Appearance;
        actor.Flies = staged.Flies; actor.Swims = staged.Swims; actor.Grasps = staged.Grasps;
        actor.Radius = staged.Radius; actor.NaturalReach = staged.NaturalReach; actor.Speed = staged.Speed;
        actor.X = position.X; actor.Y = position.Y;
        actor.HomeX = home.X; actor.HomeY = home.Y;
        actor.MotionX = actor.MotionY = 0;
        Items.Reconcile(actor);
        Events.Publish(new("appearance_changed", actor.Id, "", appearance.Family));
        return true;
    }

    public bool UsePotion(ActorState actor) => UseItem(actor, "potion");
    public bool UseItem(ActorState actor, string selector)
    {
        if (!actor.Alive || actor.State is not (ActionState.Idle or ActionState.Moving))
            return false;
        float healing = Items.Service.Call("consume", actor.Id, selector).AsSingle();
        if (healing <= 0)
            return false;
        actor.Health = Math.Min(actor.MaxHealth, actor.Health + healing);
        Events.Publish(new("item_used", actor.Id, "", selector));
        return true;
    }

    public void InitializeContainers(RegionState r)
    {
        if (r.ContainersInitialized) return;
        foreach (var (at, kind) in r.Features.OrderBy(x => x.Key))
        {
            if (kind != "chest" || r.Depleted.Contains(at)) continue;
            string owner = r.Id + ":" + at;
            var random = new Random(unchecked(r.Seed ^ at * 7919));
            Items.Add(owner, "potion", 1 + random.Next(2));
            Items.Add(owner, "coin", 4 + random.Next(9));
            Items.Add(owner, new[] { "herb", "ore", "wood" }[random.Next(3)], 2 + random.Next(3));
        }
        r.ContainersInitialized = true;
    }

    public void NewGame()
    {
        RegionsById["reach"] = Mosslight.Regions.Overworld(Seed);
        var r = RegionsById["reach"];
        var pos = r.Center(r.Entrance);
        var player = new ActorState
        {
            Id = "player",
            Name = "Wayfarer",
            X = pos.X,
            Y = pos.Y,
            HomeX = pos.X,
            HomeY = pos.Y,
            Abilities = new()
            {
                "slash",
                "ember",
                "mend"
            }
        };
        AnatomyAdapter.Apply(player);
        Actors.Add(player.Id, player);
        for (int i = 0; i < 8; i++)
        {
            var p = Content.NpcProfiles[i < 2 ? 0 : 1];
            var a = new ActorState
            {
                Id = "npc:" + i,
                Name = i < 2 ? "Reach warden" : "Mire prowler",
                Profile = p.Id,
                Profession = p.Profession,
                Faction = p.Faction,
                X = pos.X + 100 + i * 68,
                Y = pos.Y + (i % 2) * 48,
                Appearance = new()
                {
                    Family = p.Family,
                    Seed = (ulong)(Seed + i * 17)
                },
                Grasps = p.Family == "biped",
                Abilities = new()
                {
                    i < 2 ? "slash" : "bite"
                }
            };
            a.HomeX = a.X;
            a.HomeY = a.Y;
            AnatomyAdapter.Apply(a);
            var placed = r.FindPlacement(a.Position, a, Actors.Values);
            a.X = a.HomeX = placed.X; a.Y = a.HomeY = placed.Y;
            Actors.Add(a.Id, a);
            if (i < 2)
            {
                Items.Add(a.Id, "iron_sword");
                Items.Equip(a, "iron_sword", "weapon");
                Items.Add(a.Id, "potion", 4);
            }
        }

        Items.Add("player", "iron_sword");
        Items.Equip(player, "iron_sword", "weapon");
        Items.Add("player", "leather_coat");
        Items.Add("player", "potion", 3);
        Items.Add("player", "coin", 20);
        Items.Add("player", "herb", 4);
        InitializeContainers(r);
    }
}
