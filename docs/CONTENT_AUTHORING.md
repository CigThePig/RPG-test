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
| Combat profiles | `foundation.json: combatProfiles` | Stable ID, minimum/preferred/approach/pursuit distances, retreat health fraction, decision seconds, reposition fraction, melee/ranged behaviour and positive per-ability priorities. |
| Factions | `foundation.json: factions` | Directed `source:target` opinion; per-actor reputation can modify it. Relations and knowledge are persistent. |
| Quests | `quests.json` | Upstream Quest extension: unique integer ID, event/value objective, count, prerequisite and reward. Objective targets use actual event values, actor/region IDs remain available in event arguments. |
| Dialogue | `warden.dialogue` | Edit in Dialogue Manager. For v4 response conditions use `[if condition /]`. Mutations call `accept_quest`; queries use `completed`/flags. |
| Regions | `Regions.Overworld` | Seed/size and generation constraints. `RegionState` owns tiles, features and persistent changes. Add region definitions through this boundary. |
| Dungeons | `third_party/graph_dungeon/grammar`, recipe/start files | Original mission and layout grammar. Run `python3 tools/pack-dungeon-rules.py` after edits; no hand-editing the packaged JSON. |
| Spawns | `spawns.json` | Count, faction and archetypes (combat profile, registered family, name, speed, health). Stable actor IDs are derived from region and spawn index. |
| Creature identity | Workshop / PPC JSON | Select any registered family/seed, preview, mutate and export original PPC genome. Full genome, not just a PNG or family label, is stored. |
| Audio | `AudioService.gd` | Original gdfxr parameter configs and editor; `export_effects()` writes reproducible WAVs. Supply AudioStreams for ambience/music. |

Validation reports unknown IDs, invalid ranges, duplicate definitions, missing rewards/dependencies/assets, bad families and inconsistent recipes. The real dialogue compiler rejects invalid scripts. For richer per-objective semantics, extend `EventQuest.update` and continue to refer to authoritative IDs/events; do not invent a parallel quest store.

## Attack geometry and warnings

Ability geometry and presentation are optional. Omit both to preserve the original target-locked SPELL behavior. For an existing direct hostile attack, `"Telegraph": { "Style": "target" }` draws an attacker ring, target ring and tracking tether during a meaningful windup. It does not imply a fixed danger area. The ordinary `bite` uses this style.

For a direct hostile melee cone, add `"Geometry": { "Shape": "cone", "Range": 56, "HalfAngleDegrees": 45 }` and optionally `"Telegraph": { "Style": "cone" }`. At activation the direction toward the designated target is normalized and committed. The sector follows the attacker's current ground position with that direction; its inclusive range and half-angle are the exact center-point checks used at impact, followed by hostility, line of sight and normal defenses. Affected actors are processed once in ordinal ID order. `Range` on the ability still governs activation; cone `Geometry.Range` governs impact and its outline. `fixture_sweep` has `VerificationOnly: true`, so it is loaded in test and replay sessions but excluded from ordinary gameplay and progression. Authored production cones omit that flag.

`Style` may be `none`, `target`, or `cone`, and must match implemented damaging mechanics; self effects and projectiles cannot claim these danger areas. The reusable circle membership primitive is available for future ground attacks, but `circle` is rejected in ability definitions until a matching impact executor exists. Values must be finite, ranges nonnegative, cone range and half-angle positive, and the half-angle no greater than 180 degrees. Unknown shapes, styles, action styles, and status references fail content validation. Windups shorter than 0.05 seconds intentionally receive no danger warning; simulation timing is unchanged. `ActionStyle` may name an existing PPC `ActionStyle` such as `Bite`, `Strike`, or `TailSwipe`. `AttackStarted` passes it to the timed PPC action; omitted styles use the generated rig default. TailSwipe/Sting require a tail, WingBuffet wings, TentacleLash tentacles and PincerSnap grippers; an incompatible request falls back to the rig default. `slash` explicitly uses `Strike`, `bite` uses `Bite`, and the verification-only sweep uses `TailSwipe`. These are cosmetic choices and do not alter damage, range or timing.

The current item material example affects mass and damage/defence/magic power. Weight-dependent movement, full durability degradation and additional construction components are extension points, not already implemented mechanics. The property and instance format supports them without replacing inventory.

## Combat event presentation

| World observation | Presentation |
|---|---|
| `AttackStarted` | One timed rig action; swing/bite cue when appropriate. |
| `AttackCommitted`, `AttackImpact` | No second animation or assumed hit. The world retains its impact boundary. |
| `Hit` | Directional victim recoil and contact burst, with one damage cue and nearby restrained camera response. Periodic damage uses a smaller separate effect. |
| `Miss` | Ordinary melee whiff except player slash: its single air cue belongs to `AttackImpact`, with no miss burst. Matching avoidance suppresses ordinary whiffs; projectile collision outcomes coalesce separately. |
| `DefensiveAvoidance` | Dodge gets its own ring and cue; generic invulnerability is not called a dodge. |
| `AttackCanceled`, `ActionCompleted` | Cancel active/pending action; interruption gets a short accent. |
| `ProjectileReleased`, `ProjectileImpact` | Distinct release and collision accents. Collision and confirmed damage share one impact burst and cue; an avoided collision uses the avoidance feedback instead. A collision without a damage outcome is flushed after the world step. |
| `Death` | One defeat accent and a transition to the rig's stable rest posture; the earlier hit is not repeated. |

Effect severity is presentation-only: physical definitions with at least 18 authored damage and cone attacks use the heavy variant. It adds no gameplay force. Effects use projected event positions and a fixed pool; abilities need no bespoke particle node. Do not infer terrain collisions, damage, or a projectile hit from release.

## Tactical combat and projectile authoring

Use the independent `ActorState.CombatProfile` ID; identity/schedules retain `Profile`. Unknown saved IDs safely fall back; authored spawn references are validated. Distances must be finite and ordered minimum ≤ preferred ≤ approach ≤ pursuit (at most 360). Retreat/reposition values are 0–1 and decision cadence 0.2–2 seconds. Priorities name existing hostile abilities and must be finite/positive. SPELL still checks actual resources, equipment and cooldowns. The three production profiles and eight ruin spawns are examples.

Optional ability `Projectile` accepts `Mode` (`legacy`, `straight`, `bounded_homing`), `TurnDegrees` (0–720/s), `Lifetime` (>0–30 s), `Radius` (0–12) and `Visual` (`ember`, `spit`). Speed remains `ProjectileSpeed`. Omission preserves legacy Ember steering and 4-second lifetime. Heading is committed at release for straight shots; lost targets continue their last heading. `InterceptHostiles: true` opts into first-hostile contact (used by Spit); omission preserves legacy target-only actor eligibility, including Ember. Friendlies pass through. Earliest swept terrain/actor collision wins, with terrain winning near ties. Terrain/boundary impacts emit explicit outcomes through the existing presentation stream; expiration emits no hit effect. Save schema remains 1. See `PR4_PART4_HANDOFF.md` for migration and test fixtures.

Enemy `slam` and `spit` set `PlayerLearnable: false`; they remain usable through NPC SPELL ability sets and are excluded from player progression. Omission retains existing player learning behaviour.

## Dungeon spaces and gates

Generated ruins use variable rooms joined by corridors; see [DUNGEON_LAYOUT.md](DUNGEON_LAYOUT.md). Every room has at least a 9×9 core (216×216 units) and may gain wings; corridors and gates are three tiles (72 units) wide; tiles remain 24 ground units. Production anatomy derives circular radii 6–11 (diameters 12–22), leaving at least 50 units of lateral width beyond the largest tested footprint. Rendered silhouette size is separate from collision radius. Test large silhouettes at normal zoom rather than shrinking them to fit.

Mission features sit on room core centers. Eight seeded inhabitants retain the prowler/brute/spitter profiles and deterministic `FindPlacement` selection, which checks reachable floor, footprints, feature clearance and actor separation. Gates are isolated connected lock components; never place independent authored gates so their cells touch. Adjacent lock cells intentionally mean one key/one gate.

## Environment art and future landmarks

Use `EnvironmentStyle` for the shared palette and cosmetic revision/hash, and `EnvironmentArtGenerator` for bounded integer-pixel floor/wall recipes. Atlases are generated once by `EnvironmentAssetCache`; do not make per-tile textures. Interactive objects remain existing `RegionState.Features` consumed by `FeatureView`. Nonblocking visual slots belong in `EnvironmentDecoration`, with stable ground-contact pivots and the actor Y-sort parent. Preserve saved terrain and gameplay RNG when extending themes. See `PR5_PART2_HANDOFF.md`; full landmark authoring remains Part 3.

## PR5 authored landmarks

Author production rooms in `game/data/landmarks/rooms.json`. [LANDMARK_AUTHORING.md](LANDMARK_AUTHORING.md) covers the complete schema, coordinates, doorway masks, safe blockers, supported assets/themes, rotation, stable selection, previews and Part 4 hooks. Templates load in ContentAudit, via packaged FileAccess. They compose new dungeons only; saved resolved manifests and terrain remain authoritative.

## Ordered adventures and exploration

`quests.json` may declare ordered `stages` with stable `id`, exact `kind`/`target`/`value` matchers and player-facing `text`. The current stage alone receives events; the adapter already filters player identity. Optional discoveries must not appear as mandatory fixed-template prerequisites. Keep rewards within existing Gloot prototypes and use confirmed delivery; never set `rewarded` before success. Warden dialogue conditions query the adapter, not independent UI flags. Fresh optional caches are resolved by Exploration into ordinary chest features before container initialization, never generated by decoration rendering. See the Part 4 handoff for persistence and migration contracts.
