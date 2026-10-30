# PR5 Part 2 — Procedural environmental art

## Implementation and approach

Continues `42b74baf210a6a7eb5ac99af86a3347cfe4e5a06` on the existing draft `feat/dungeon-exploration` branch. Keep PR #5 draft and unmerged.

Selected a native hybrid: original exported PPC vegetation and rocks plus project-owned procedural architectural modules. PPC's upper-left illumination, restrained ramps, integer pixels and ground pivots guide the new art. A fully 3D renderer would replace the established projection/depth contracts. A development-time model-to-sprite pipeline is optional future work: KayKit/Quaternius/Kenney assets were **not downloaded, licensed for inclusion, or evaluated as production art**. They bring conversion/camera/palette work without solving a dependency the current generator cannot handle. No new external assets or dependencies ship.

Floors now show staggered stone joints, worn slab tones, restrained cracks and moss. Walls use capped stone, shaded staggered courses, bevels, end caps and deterministic wear. Door openings retain their full physical and visual width; end caps frame them without a spanning arch that could hide the passage. Grouped gates retain their cell membership and one-key authority, with iron staves and sockets. Chests have shaded box sides, planks, metalwork and open lids; resource ore uses PPC rock art with readable minerals; wood uses the original shaded PPC tree; portal/exit architecture and relic pedestals share the stone palette. Existing herb/key silhouettes remain deliberately readable. Cosmetic natural vegetation and small dungeon rubble add restrained variation.

## Responsibilities and Part 3 contract

- `EnvironmentStyle`: revision, shared material palette and explicitly unchecked unsigned cosmetic hash. Inputs are region seed, integer coordinates and slot. No `Random`, string hashes, gameplay RNG, time or scheduling.
- `EnvironmentArtGenerator`: 16 cached 24×12 floor recipes and bounded wall recipes. Marks are integer rectangles and colors. Extend recipes here, keeping modules within their declared bounds.
- `EnvironmentAssetCache`: two immutable process-lifetime RGBA8 atlases, initialized/uploaded on the Godot main thread. Floors: 384×12; walls: 1664×30 (64 modules: 16 variants × four end-cap states). Combined CPU pixel payload is **218,112 bytes**; GPU/engine overhead is measured separately. No texture cache keyed by region or per-cell textures. Wall modules have transparent one-pixel gutters; nearest filtering, integer projection and opaque common floor borders prevent atlas bleed/gaps.
- `TerrainBatch`: reusable colored or textured triangle mesh construction, six vertices per atlas quad. Native mesh resources explicitly disposed when replaced/removed. Static surfaces are not rebuilt per frame.
- `TerrainView`: consumes serialized region tiles; builds ground/floor/natural-rock meshes. Original PPC ground textures remain grouped by material.
- `TerrainWallRow`: one mesh per occupied row, still in the actor Y-sort parent and anchored at the **front ground edge**. Rise increases from 14 to 16 screen units, without changing the ground projection or footprint. End caps and all atlas pixels remain inside each tile's 24-pixel horizontal bounds.
- `FeatureView`: existing interaction-state view; receives the already-loaded PPC texture dictionary and stable cosmetic seed. Authority, reach, positions, Gloot, depleted/open/selection states and grouped gate behavior stay in the existing systems. Tree pivot is (36,100); rock is (12,16); bush is (17,23), from the exporter manifest.
- `EnvironmentDecoration`: at most 96 nonblocking nodes per active region, fixed scan/slot hash. Natural props share ground Y sorting; dungeon rubble avoids central passage lanes and feature stands. No physics nodes, feature entries or saved cosmetic state. Views are removed/freed on region rebuild.

Part 3 should consume these presentation APIs and current authoritative region/features. Do not regenerate saved geography for visual themes. Add bounded recipes/style choices without consuming gameplay RNG. Tall authored props need a ground-contact pivot and actor-layer depth; collision/interactive props need explicit existing-world authority. Do not insert one node per ground tile or extend arches into traversable doorways without new pixel verification. Fully realized themes, landmarks and an adventure remain later milestones.

## PPC reuse and licensing

Original `vendor/ppc/EnvironmentArt.cs` remains untouched and MIT notices remain in `vendor/ppc/LICENSE`, `vendor/ppc/THIRD_PARTY_NOTICES.md` and root `THIRD_PARTY_NOTICES.md`. Existing native `tree1`, `rock1`, `bush1`, grass, dirt and water PNGs come from pinned PPC `a2207c4bd6f1be51d426684cf58f11668def7579`. Previously tree/rock/bush textures were loaded by Foundation but never supplied to FeatureView or decorative views. They now render in the production world; rock terrain is batched. No duplicate upstream generator was copied.

Regenerate PPC originals with `node tools/export-art.mjs /tmp/catalog --family=biped --seed=42`; environment outputs are included. Copy matching PNGs only after checking manifest pivots. The architecture atlases regenerate directly from version-controlled C# at startup; no PNG or download step is required. Determinism is integer hashing/module selection plus fixed color/rectangle rasterization; platform-independent authoritative simulation never depends on pixels.

## Saves and gameplay

Schema 1, full saved terrain arrays, generator identity, region geometry, mission graph and all simulation code remain unchanged. No new authoritative save fields. Legacy narrow dungeons receive the same upgraded material renderer on their saved cells. Existing Part 1 legacy fixtures, gate progression and exact save roundtrips remain in the full integration suite. `EnvironmentArtChecks` adds bounded recipes, resource availability, cache reuse, boundary-coordinate hashing, decoration stability and a repeated real World input simulation with/without cosmetic work, requiring identical full serialized outcomes.

## Reproduction and evidence

Run the existing build/import/headless/desktop/mobile/gameplay commands from `docs/GAMEPLAY_VERIFICATION.md`. Focused production-renderer references:

```
godot --path . --audio-driver Dummy -- --visual-check --environment-only
godot --path . --audio-driver Dummy -- --visual-check --environment-only --mobile-ui
```

Use a real display (Xvfb on Linux CI). This fixture includes existing generated rooms/gates, wall-depth pixels, a normal-zoom room with PPC actors/chest/ore/herb/relic, live combat, the overworld, and six alternating region rebuilds. The fixture temporarily inserts features/actors and restores the original snapshot; it does not add content to user saves.

| Evidence | Desktop | Mobile layout |
| --- | --- | --- |
| Actual Part 1 expanded room | [Before](native-screenshots/environment-part1-dungeon.png) | [Before](native-screenshots/mobile-environment-part1-dungeon.png) |
| Same generated room, new art | [After](native-screenshots/dungeon-after-room.png) | [After](native-screenshots/mobile-dungeon-after-room.png) |
| Reference room and objects | [Room](native-screenshots/environment-dungeon-reference.png) | [Room](native-screenshots/mobile-environment-dungeon-reference.png) |
| Behind foreground wall | [Behind](native-screenshots/wall-depth-behind.png) | [Behind](native-screenshots/mobile-wall-depth-behind.png) |
| In front of wall | [Front](native-screenshots/wall-depth-in-front.png) | [Front](native-screenshots/mobile-wall-depth-in-front.png) |
| Traversable doorway | [Doorway](native-screenshots/wall-depth-doorway.png) | [Doorway](native-screenshots/mobile-wall-depth-doorway.png) |
| Closed/open grouped gate | [Closed](native-screenshots/dungeon-gate-closed.png) / [Open](native-screenshots/dungeon-gate-open.png) | [Closed](native-screenshots/mobile-dungeon-gate-closed.png) / [Open](native-screenshots/mobile-dungeon-gate-open.png) |
| Overworld | [Reach](native-screenshots/environment-overworld.png) | [Reach](native-screenshots/mobile-environment-overworld.png) |
| Combat | [Live](native-screenshots/environment-dungeon-combat-live.png) | [Live](native-screenshots/mobile-environment-dungeon-combat-live.png) |

All images are real viewport captures. The existing `dungeon-before-*` fixtures compare **legacy geometry using the current renderer**, not historical artwork; the explicitly named `environment-part1-*` images are from an untouched Part 1 checkout on this runner. Enlarged crops are supplemental only.

## Verification and performance status

Final commit, exact-source CI, measurements and Android download are appended after verification. Do not treat this status paragraph as a completion claim.

The first all-geometry art prototype cost 9.06 ms combined mesh construction on expanded seed 42 and p95 27.85 ms on llvmpipe. It was replaced with shared atlases before delivery. Baseline and final same-runner measurements are retained in `docs/environment-evidence/`; `build/dungeon/environment-*.json` contains terrain/wall cost, synchronous rebuild cost, 12-frame readiness wall time, frame p50/p95, node counts, managed/Godot memory and atlas/cache counts over repeated transitions. Readiness includes waiting rendered frames and is **not** pure preparation CPU cost. Working-set inspection is unavailable in this restricted local host, so Godot static memory and managed memory are reported instead. Atlas payload excludes driver/resource overhead.

Physical Samsung S25 sustained FPS, subjective art acceptance, touch comfort and audio require user playtesting. Neither software desktop timing nor installed emulator verification establishes physical-device acceptance. Existing development signing may require uninstalling a differently signed APK, which deletes private saves; preserve valuable progress first.
