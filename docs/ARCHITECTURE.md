# Architecture

`Foundation` composes the application. `World` owns authoritative actors, regions, clock, actions and the short event stream. It has no renderer-owned gameplay state. Godot ticks it at 60 Hz and routes UI/input into its public commands. `ActorState` contains persistent identity, action state, attributes, experience, skills/capability lists, faction, reputation, knowledge and relationships. A separate `Appearance` stores the original genome and generator identity.

## Imported implementations and authority

Gloot owns all item instances, stacks and equipment slots. The C# `Items` façade calls the real GDScript implementation; it has no parallel collection of items. Gloot property bags preserve material, quality, condition and IDs through transfers. `ActorState` owns health and resources. SPELL executes requirements, costs and effects compiled from `AbilityDef`; the adapter starts serializable windup/recovery states. Costs and cooldowns are committed only after validation. Immediate SPELL effects are drained when a cast begins, leaving no unpersisted pending library effects.

Melee uses geometric range and line-of-sight at resolution time. Projectiles travel through traversable cells toward their target. Damage performs mitigation, recovery/invulnerability, knockback, interruption, death, XP and loot creation. Status definitions provide capped stacking, refresh, periodic damage and action-blocking hooks. Healing, items and attacks are shared player/NPC operations. Action state prevents incompatible movement/casting/use overlap. The current attacks target one actor; area/facing-dependent attacks are extension work.

QuestSystem owns quest lifecycle/pools. `EventQuest` adds event/objective counters and failure/reward metadata. Dialogue Manager compiles the ordinary upstream dialogue format; its mutations call the same narrative API. `Narrative` observes a scoped stream emitted by the current world and emits explicit reward/reputation requests back to the composition root. Completion cannot grant the same reward twice after restoration. No unrelated subsystem looks up another through an enormous global bus.

## NPC execution

The extracted Skelerealms GOAP graph plans against value-aware conditions. The production adapter supplies combat, retreat, rest, trade and patrol goals. Schedules, perception range, line-of-sight, faction relationships, health and ability constraints select current goals. Navigation routes through the authoritative region cells. Motion is continuous at the fixed tick; planning is throttled to 5 Hz. Distant/unloaded inhabitants retain schedule/state with a coarse schedule update, rather than inventing offscreen battles or economies. The initial profiles are intentionally small but execute real shared abilities and inventories.

## Procedural world

Godot FastNoiseLite generates repeatable regional fields with settlement/causeway constraints. Region tiles drive collision, water traversal, line-of-sight, paths and rendering. Graph Dungeon Generator supplies the original mission grammar and spatial layout. The adapter builds rooms/corridors, places keys/doors/rewards, corrects the upstream reversed door convention and validates actual tile-level key/lock reachability with bounded state search. Invalid layouts are rejected with bounded deterministic retries. Spawn tables select families/abilities and create stable region actor IDs. Dungeon state is retained when leaving.

## Presentation

`AnatomyAdapter` derives wings/fins/arms, collision dimensions and movement/equipment capabilities in the model layer, including actors that have never been rendered. `CharacterView` consumes that state and original PPC genomes; pixels never decide capabilities. Existing equipment is reconciled after anatomy changes and save restoration. Equipment produces a presentation-only genome variant through PPC gear genes. The base saved appearance stays separate from equipped items. PPC's factory cache and original render queue retain CPU rasterization, parallel batching, 10-fps pose presentation, offscreen culling and engine texture updates. Unloaded actors stop detailed presentation.

`RpgUi` panels are views of live state. Dialogue uses its own modal panel driven by actual dialogue resources. Region terrain is one retained draw-command node per active region, avoiding rebuilding it every frame; features/projectiles are dynamic. Camera projection matches PPC's 30-degree ground plane. Audio uses imported gdfxr synthesis and three playback channels, with deterministic parameter presets and WAV export. No remote service is required.

## Persistence

One coordinator captures actor state, equipped/bag Gloot snapshots, narrative pools and quest properties, flags, faction changes, every generated region's current tiles and mutations, projectiles and a bounded causal event history. Atomic temporary-file replacement supports three slots. Schema and generator revisions are explicit and rejected on mismatch. Snapshot validation precedes mutation; bridge staging/rollback preserves a running session if restore fails. Full region arrays are stored in v1: this is a simple, reliable baseline rather than an unimplemented claim of delta streaming. Cross-region container/actor IDs remain stable.

See the integration map for exact source origins, responsibilities and extension files. Save formats and engine pinning are implementation contracts; upgrades require deliberate migrations.
