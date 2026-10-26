# Content authoring

All example definitions are in `game/data/`. `ContentAudit` runs at application startup; integration tests additionally build every registered creature family, validate events and generate thirty dungeons.

| Content | Source | Workflow |
|---|---|---|
| Items/equipment | `items.json` | Gloot prototype ID → property dictionary. `max_stack_size`, `slot`, damage/defence/power/material/condition. Gloot's inspector/UI addon remains available. |
| Materials/generated items | `materials.json` | Mass and physical/magical multipliers. `ItemService.generate_item(owner, archetype, material, quality)` creates a genuine unique Gloot instance. |
| Abilities | `foundation.json: abilities` | ID, target/range, cost, cooldown, windup, recovery, damage type, projectile speed, optional status, equipment requirement. SPELL executes the definition. |
| Skills/progression | `foundation.json: skills` | Stat/rank definitions, experience thresholds, skill points and ability unlock level/cost. Character UI exercises training/unlocks. |
| Statuses | `foundation.json: statuses` | Duration, tick interval, capped stacks, type and action blocking. Live state stays per actor. |
| Recipes | `foundation.json: recipes` | Ingredient ID/quantity map, output ID and count; optional material/quality constructs an instance with transformed properties. The same Gloot transaction path is available to NPCs. |
| Loot | `foundation.json: loot` | Chance and bounded quantity per actual item ID. Instances go into the defeated actor's container. |
| NPCs/schedules | `foundation.json: npcProfiles` | Family, profession/faction and ordered hourly activities. Profiles share World actions and the planner. |
| Factions | `foundation.json: factions` | Directed `source:target` opinion; per-actor reputation can modify it. Relations and knowledge are persistent. |
| Quests | `quests.json` | Upstream Quest extension: unique integer ID, event/value objective, count, prerequisite and reward. Objective targets use actual event values, actor/region IDs remain available in event arguments. |
| Dialogue | `warden.dialogue` | Edit in Dialogue Manager. For v4 response conditions use `[if condition /]`. Mutations call `accept_quest`; queries use `completed`/flags. |
| Regions | `Regions.Overworld` | Seed/size and generation constraints. `RegionState` owns tiles, features and persistent changes. Add region definitions through this boundary. |
| Dungeons | `third_party/graph_dungeon/grammar`, recipe/start files | Original mission and layout grammar. Run `python3 tools/pack-dungeon-rules.py` after edits; no hand-editing the packaged JSON. |
| Spawns | `spawns.json` | Count, registered families, faction and ability. Stable actor IDs are derived from region and spawn index. |
| Creature identity | Workshop / PPC JSON | Select any registered family/seed, preview, mutate and export original PPC genome. Full genome, not just a PNG or family label, is stored. |
| Audio | `AudioService.gd` | Original gdfxr parameter configs and editor; `export_effects()` writes reproducible WAVs. Supply AudioStreams for ambience/music. |

Validation reports unknown IDs, invalid ranges, duplicate definitions, missing rewards/dependencies/assets, bad families and inconsistent recipes. The real dialogue compiler rejects invalid scripts. For richer per-objective semantics, extend `EventQuest.update` and continue to refer to authoritative IDs/events; do not invent a parallel quest store.

The current item material example affects mass and damage/defence/magic power. Weight-dependent movement, full durability degradation and additional construction components are extension points, not already implemented mechanics. The property and instance format supports them without replacing inventory.
