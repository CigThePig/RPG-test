using Godot;
namespace Mosslight;

/// <summary>Read-only production character view. No cached or parallel gameplay state.</summary>
public static class CharacterOverview
{
    public static void Populate(VBoxContainer body, Foundation app)
    {
        var actor = app.World.Player;
        body.AddChild(UiDesign.Text($"{actor.Name} · Level {actor.Level}\n{actor.Region} · {actor.State}"));
        void Section(string title, string text)
        {
            var card = new PanelContainer(); body.AddChild(card);
            var column = new VBoxContainer(); card.AddChild(column);
            column.AddChild(UiDesign.Text(title)); column.AddChild(UiDesign.Text(text));
        }
        Section("VITALS", $"Health  {actor.Health:0} / {actor.MaxHealth}\nMana  {actor.Mana:0} / {actor.MaxMana}\nExperience  {actor.Experience} / {actor.Level * 100}\nUnspent skill points  {actor.SkillPoints}");
        Section("COMBAT & EQUIPMENT", $"Strength  {app.World.Stat(actor, "strength")}\nDefence  {app.World.Stat(actor, "defence")}\nWeapon damage bonus  {app.World.Items.Bonus(actor, "damage")}");
        Section("ABILITIES", string.Join("\n", app.World.Content.Abilities.Where(a => actor.Abilities.Contains(a.Id)).Select(a => a.Name)));
        Section("TRAINING", string.Join("\n", app.World.Content.Skills.Select(s => $"{s.Id}  {actor.SkillRanks.GetValueOrDefault(s.Id)} / {s.MaxRank}")));
        Section("APPEARANCE", $"Family  {actor.Appearance.Family} · seed {actor.Appearance.Seed}");
        Section("EXPLORATION", $"Flight: {(actor.Flies ? "Yes" : "No")} · Swimming: {(actor.Swims ? "Yes" : "No")}\nGrasping: {(actor.Grasps ? "Yes" : "No")}\nWarden reputation  {actor.Reputation.GetValueOrDefault("wardens")}");
    }
}
