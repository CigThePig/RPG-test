# Living Reach — Part 3: environmental objects

## Objective and boundaries

Replace the overworld's mismatched scenery with a coherent woodland object family at normal gameplay zoom. Part 2's ground and banks remain the visual foundation. This part changes presentation only: no geography, object coordinates, collision, navigation, rewards, harvesting rules, regeneration, save schema, actors, combat, camera or mobile layout changes. Dungeon architecture and its existing feature illustrations remain independent. Part 4 owns atmospheric overlays, regional composition changes and final global grading.

## Visual design

Use the Living Reach palette with upper-left illumination, stepped integer-pixel silhouettes, restrained highlights and small flattened contact shadows. The 2.5D projection compresses the ground plane, not the vertical objects. Leave broad surfaces quiet enough for PPC characters and combat telegraphs to stand out. Amber marks a useful chest clasp or ore vein; teal belongs to the real portal. Ordinary scenery has neither emissive rims nor selection-like accents.

| Object | Shape and material | State/readability |
| --- | --- | --- |
| Trees | Four authored arrangements of overlapping crown masses; forked timber, visible trunk, root flare; brighter upper-left leaf clusters and dark undersides | Full tree when harvestable; low cut stump after harvesting. Same ground-contact pivot, no camera-facing or actor changes |
| Natural stone | Low faceted boulder, grey-green top, dark right face, moss at the foot | Retains terrain footprint and existing ground-layer rock ordering; decorative rocks remain nonblocking |
| Shrubs | Low, uneven leaf clumps with woody stems and sparse seed/berry detail | Lower visual priority than a harvestable resource; same existing cosmetic slots |
| Herb | Open stem-and-leaf silhouette with pale seedheads | Distinct from a shrub; clipped stems after gathering |
| Ore | Taller faceted stone with pale mineral shards and a restricted ochre vein | Recognizable at phone zoom without a giant floating diamond; mined remnant loses the tall shards |
| Chest | Timber planks, bevelled lid and right side, muted iron bands and small amber clasp | Closed and visibly open/empty states follow authoritative depletion |
| Portal | Weathered stone posts and lintel, moss, three shallow steps and a small teal inset | Landmark silhouette; no new lighting pass or animation |

The tree family retains approximately the existing 72×104 source bounds and (36,100) root pivot. Terrain boulders occupy the existing 24×19 raised-rock envelope. Resources occupy similar local ground extents to their predecessors. The new art is code-native pixel rasterization rather than generated photographic imagery or a new external asset dependency.

## Implementation contract

`OverworldPropArt` contains bounded indexed-pixel recipes. Shapes use integer rectangles, scan-converted polygons, short lines and clipped clusters. Four variants per family are selected by the existing unsigned coordinate hash. `EnvironmentStyle.Revision` stays unchanged so materials, dungeons and decoration placements do not reshuffle.

`OverworldPropAtlas` builds one immutable 576×624 RGBA8 atlas once per process: **1,437,696 pixel bytes**. Eleven families with four slots each fit in an 8×6 module layout. Some small state/architectural variants intentionally share major shapes. Transparent module borders and nearest filtering prevent neighbour bleed. No per-object image, seed-keyed texture dictionary, frame-driven regeneration, additional shader or animated scenery node is introduced. Region views share the atlas; it lives for the process like the existing environment atlases.

`FeatureView` selects a module from its real kind/depletion state only for overworld regions. Existing selection rings, interaction targeting and authoritative containers remain in charge. `EnvironmentDecoration` uses its existing bounded slots, count and feature-clearance rules. `TerrainView` batches natural stone sprites beneath actors and above the water's Z=-1 surface, preserving raised shoreline rock tips. Tall objects and features remain siblings of PPC actors in the Y-sorted layer.

The visual-verification-only `--part2-props` / paired switch retains the previous props for matched comparisons. Ordinary gameplay uses the new art. Original PPC source art and dungeon atlases are retained unchanged.

## Required verification

1. Engine build and full headless integration, including all indexed modules, transparent gutters, state differences, bounded atlas reuse, deterministic decoration placement, serialized geography and identical real movement/AI/combat continuation.
2. Production viewport comparisons of the same Part 2 ground/water fixture before/after; all six supported aspect ratios, canonical overworld seeds 1/42/1337, resource states, and reduced motion.
3. Actual pixel occlusion: a tree covers the actor standing behind its root and the actor covers the tree when standing in front. Retain existing dungeon wall/prop and raised shore-rock checks.
4. Repeated dungeon/overworld rebuilds still produce correct, unchanged scenery.
5. Existing gameplay replay/adventure CI, signed ARM64 packaging validation and both installed Android emulator layouts. Physical S25 acceptance: the owner ran the PR #8 ARM64 APK on the physical Galaxy S25 and accepted its appearance, touch controls and performance.

Keep PR #8 draft and unmerged. Record verified source, exact commands, evidence and any failed attempt in the Part 3 handoff before calling automated delivery complete.
