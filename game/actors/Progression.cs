namespace Mosslight;
public static class Progression
{
    public static void Award(World world, ActorState actor, int experience)
    {
        if (experience < 0)
            throw new ArgumentOutOfRangeException(nameof(experience));
        actor.Experience += experience;
        while (actor.Experience >= actor.Level * 100)
        {
            actor.Experience -= actor.Level * 100;
            actor.Level++;
            actor.SkillPoints++;
            actor.MaxHealth += 5;
            actor.Strength += 1;
            world.Events.Publish(new("level_gained", actor.Id, "", actor.Level.ToString()));
        }
    }

    public static bool Unlock(World world, ActorState actor, string id)
    {
        var definition = world.Content.Abilities.Find(a => a.Id == id);
        if (definition == null || (actor.Id == "player" && !definition.PlayerLearnable) || actor.Abilities.Contains(id) || actor.Level < definition.UnlockLevel || actor.SkillPoints < definition.UnlockCost)
            return false;
        actor.SkillPoints -= definition.UnlockCost;
        actor.Abilities.Add(id);
        world.Events.Publish(new("ability_unlocked", actor.Id, "", id));
        return true;
    }

    public static bool Train(World world, ActorState actor, string id)
    {
        var skill = world.Content.Skills.Find(s => s.Id == id);
        if (skill == null || actor.SkillPoints < 1 || actor.SkillRanks.GetValueOrDefault(id) >= skill.MaxRank)
            return false;
        actor.SkillPoints--;
        actor.SkillRanks[id] = actor.SkillRanks.GetValueOrDefault(id) + 1;
        world.Events.Publish(new("skill_trained", actor.Id, "", id));
        return true;
    }
}

public sealed class SkillDef
{
    public string Id { get; set; } = "";
    public string Stat { get; set; } = "strength";
    public float PerRank { get; set; } = 1;
    public int MaxRank { get; set; } = 5;
}
