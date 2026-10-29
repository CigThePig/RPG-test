using MHLab.Spells;
using MHLab.Spells.Definitions;
using MHLab.Spells.Requirements;
using MHLab.Spells.Costs;
using MHLab.Spells.Effects;
using MHLab.Spells.Instances;

namespace Mosslight;
/// <summary>Data definitions compile into SPELL's real requirement/cost/effect execution pipeline.</summary>
public sealed class RpgAbility(World world, AbilityDef def) : ISpellDefinition, ISpellRequirement, ISpellCost, ISpellEffect
{
    public void SetMetadata(Spell spell) => spell.Cooldown = def.Cooldown;
    public void AddRequirements(SpellRequirementContainer c) => c.Add(this);
    public void AddCosts(SpellCostContainer c) => c.Add(this);
    public void AddEffects(SpellEffectContainer c) => c.Add(this);
    public CheckRequirementResult IsMet(ISpellCaster caster, IEnumerable<ISpellTarget> targets, Spell spell, SpellsContext context)
    {
        var actor = (ActorState)caster;
        var target = targets.Cast<ActorState>().SingleOrDefault();
        return new()
        {
            Result = target != null && world.CanActivate(actor, target, def),
            Error = "Invalid target, cost, equipment or actor state"
        };
    }

    public void ApplyCost(ISpellCaster caster, IEnumerable<ISpellTarget> targets, Spell spell)
    {
        var actor = (ActorState)caster;
        actor.Mana -= def.Cost;
        actor.Cooldowns[def.Id] = def.Cooldown;
    }

    public void Setup(SpellEffectInstance e)
    {
    }

    public SpellEffectContinuationState ApplyEffect(SpellEffectInstance e, float dt)
    {
        var caster = (ActorState)e.SpellInstance.Caster;
        foreach (var target in e.SpellInstance.Targets.Cast<ActorState>())
            world.BeginAbility(caster, target, def);
        return SpellEffectContinuationState.Complete;
    }

    public void CleanUp(SpellEffectInstance e)
    {
    }
}

public sealed class ProjectileState
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Ability { get; set; } = "";
    public string Region { get; set; } = "";
    public float HeadingX { get; set; }
    public float HeadingY { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Remaining { get; set; } = 4;
}
