# Mobile playability remediation

## Confirmed causes

- Rock and water were authoritative blocked **terrain cells**, but rock art appeared on only one in three rock cells. Flat blocked cells read like traversable ground. The map boundary had the same problem.
- Collision sampled four square corners, rejecting some valid rounded corners and missing interior blockers for unusually large bodies. Navigation checked centers without sweeping the footprint between them.
- AI replanned every twelve ticks and continued at full velocity past its waypoint, causing overshoot/oscillation. It also stopped pursuing targets inside the same cell.
- The first NPC was at entrance +100 ground units and the chest at +96: a four-unit separation. All objects used generic circles and permanent labels, drawn outside the creature depth-sorting group.
- Chest interaction transferred one hardcoded potion, independently minted five coins, and depleted the feature without checking transfer success. Re-entry treated a missing potion as permission to regenerate contents.
- The stick measured displacement from a fixed center and jumped directly from its dead zone into nonzero movement. Screen-space input was interpreted as ground-space input despite PPC's compressed vertical projection.

## Production changes

`RegionState` now defines circular footprints against every intersected tile and a sampled `CanTravel` segment using that same collision rule. BFS checks complete edges. Movement retains small substeps and axis sliding after a failed full-vector step. AI caps its velocity to the planning interval's waypoint distance and uses direct steering only across a clear swept segment. Movement uses the shared speed-stat resolver.

Fresh maps retain the existing noise-based terrain, but replace the wide rectangular clearing with an oval settlement, winding three-cell road, and feature-access clearings. Actor placement searches terrain with valid clearance, in the entrance's reachable component, away from static features and other initial actors. This is an accessibility improvement, not a new hydrology generator. Runtime creature avoidance remains future work; creatures can still pass each other.

Feature presentation uses procedural `CanvasItem` primitives, with recognizable chest/open chest, plant, ore, wood, key, relic, portal, stairs and door forms. Features and creatures share Y sorting; loot-bearing corpses receive a small satchel indicator. Static untextured terrain and obstacle geometry is batched into two retained colored triangle meshes, while textured ground stays grouped by texture. Every blocked rock/wall cell gets a raised visible surface; water edges get shoreline outlines. PPC remains live and externally driven; large-body visual scale is bounded to its gameplay footprint. No baked creature sprites were introduced.

Touch movement has a movable anchor within the existing pad, a continuous remapped dead zone, normalized analog magnitude, exclusive finger ownership and explicit OS-cancel handling. Gameplay converts screen direction through PPC's projection. Camera follow is bounded to the map with faster smoothing. Mobile secondary screens move into the menu. Keyboard bindings remain available.

Interaction chooses accessible features by distance with stable tie-breaking; nearby features take precedence over merchant dialogue. Open doors no longer mask other targets. Chests transfer their actual Gloot inventories and remain available while any contents remain. Fresh chest contents are seeded once (potions, coins and a material resource); region initialization is serialized independently of depletion. Resources deplete only after successful grants. Grant capacity is checked before adding any of a batch. Feedback distinguishes full inventory, empty chest, missing key and successful collection. Combat targeting skips targets that cannot activate the selected ability.

## Diagnostics

Menu → Collision / navigation overlay shows nearby standable/blocked cells, projected actor radii, AI movement vectors and a route toward the selected feature, interaction ranges, intended player movement and spawn/home positions. Menu → Performance overlay includes coordinates, tile, standability, input magnitude, actual velocity, last blocked movement description, simulation duration, frame percentiles, visible actor count, managed memory and PPC raster/upload costs. These views are opt-in. Android logs record touch release coordinates and successful world-feature interactions, enabling the smoke test to assert movement and actual chest depletion.

## Persistence

Schema 1 and stored region arrays remain supported. New region metadata defaults legacy containers to **already initialized**, preserving existing Gloot contents and never retroactively creating loot. Existing empty bags remain empty. An explicit placement revision repairs legacy actors with blocked footprints or NPCs on features, before mutating running state. It retains feature indices, ownership, depletion, quests and progress. Fresh saves avoid repeated migration. Unsupported generator/schema revisions are still rejected. Existing old terrain receives the clearer renderer; its geography is not regenerated.

## Verification

Run `dotnet build`, `python3 tools/import-project.py`, `python3 tools/verify.py --headless`, and both rendered modes documented in TESTING.md. `PlayabilityChecks` extends the existing production suite with actual World.Move route execution, rock/water/wall collision, capability differences, circular corner clearance, large bodies, sliding, long knockback, multiple step sizes, spawn separation, full-container failure, atomic grants, depletion, re-entry and legacy container metadata.

Overworld routes to every feature execute across seeds 1, 42, 1337 and 0–29. Canonical dungeon routes execute with radius 11 after unlocking; the existing thirty-seed key/lock validator still runs separately. Rendered tests walk toward the chest via the production screen-input conversion, empty its Gloot inventory, inspect inventory and capture an opened chest, collision overlay, dialogue and dungeon. Android CI exports/signs/verifies ARM64, exports matching x86_64, then uses actual ADB touch events to move to and empty the chest, inspect inventory and background/resume.

Screenshots and raw frame samples are workflow evidence artifacts. Original comparison: `docs/native-screenshots/exploration.png` at commit `43eb4805c241cf93ebe951af70ae77564dfe677f`. After: `mobile-chest.png`, `mobile-opened-chest.png`, `mobile-collision-overlay.png` in `native-rpg-evidence` and Android diagnostics. Actual exported Android captures live in `android-runtime-evidence`.

## Limits and next checks

Hosted Mesa/llvmpipe timings are software-renderer evidence, not S25 measurements. Physical thumb comfort, frame pacing, audio and safe areas still need device confirmation. The work does not establish advanced creature avoidance, sophisticated scenery silhouettes, fully organic terrain transitions, or a comprehensive combat redesign. Current environmental forms are deliberately simple procedural geometry. Priorities after device feedback are actor avoidance/spacing in combat, biome-edge art refinement, and encounter spacing. Avoid broadening RPG feature count before confirming these controls on the phone.

## Local measured result

The final local production suite passes **318 assertions**. Both desktop and phone-layout rendered checks pass without engine/script errors. Same-host 180-frame Mesa llvmpipe samples: original commit mean 13.46 ms, p50 12.48, p95 20.11, p99 24.03; revised scene mean 15.24 ms, p50 15.09, p95 18.70, p99 22.72. A first unbatched revision cost 20.11 ms mean, motivating the retained terrain meshes. The scenes differ in geography/art and elapsed gameplay, so these are indicative short integrated workloads, not a controlled isolated renderer benchmark. Revised managed-memory sample: 27.25 MB decimal. Texture/native memory and sustained ARM64 performance were not measured locally. CI publishes fresh measurements for its own host.

## Emulator rendering verification repair

Inspecting the earlier runtime artifact revealed a false positive: input and Gloot events worked, but screenshots were solid gray. Logcat contained SceneShaderGLES3/CanvasShaderGLES3 uniform-limit link failures on legacy SwiftShader GLES 4.0.0.1. This is a separate emulator graphics issue, not evidence that the S25 startup fix regressed. The harness now uses the supported `swangle` ANGLE/SwiftShader backend instead of deprecated `swiftshader_indirect`, waits for actual rendered Reach pixels, rejects blank/splash screenshots, and fails on Godot rendering/shader errors. Menu, exploration, chest, inventory and resumed-menu captures are checked and retained. See Android's [emulator acceleration documentation](https://developer.android.com/studio/run/emulator-acceleration) and Godot [issue 109550](https://github.com/godotengine/godot/issues/109550). A runtime pass from before this harness repair must not be described as visual validation.
