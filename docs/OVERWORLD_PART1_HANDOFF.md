# Living Reach — Part 1 handoff

Status: implementation integrated; final verification and performance review in progress. This is one draft PR for four parts. Do not merge. Parts 2–4 are not implemented. Physical Galaxy S25 acceptance has not been performed.

Base: `ca60f3eda8153c0f7864703e52a1b09f5228f290` (latest inspected main; merged PR7). Runtime commit: see the PR head; a later evidence-only commit will record the verified source.

## Baseline audit

Foundation owns ground at Z=-10, with actors/features/PPC decorations in a separate Y-sorted parent; wall rows retain their front-edge Y ordering. `TerrainView` originally draws per-cell base rectangles, PPC ground textures at 32% alpha and continuous pale cardinal water edges. Projection is X unchanged, ground Y halved: 24×24 -> 24×12. PR7 settings remain canvas_items/expand at reference 1280×800; camera presets, bounds and centred mobile controls are unchanged. Texture sampling is nearest. PPC character presentation remains untouched.

`RegionState.Passable` allows water only for swimming/flying actors; circular collision uses the unchanged tile array. Overworld noise thresholds, features, navigation, saves and simulation RNG remain authoritative. `EnvironmentStyle` supplies explicit unsigned cosmetic hashing. Existing environment cache has **three** atlases, 517,632 pixel bytes (the prompt's older two-atlas description no longer applies). Rebuilds remove old ground nodes and free deferred scene/native resources. Existing textures are shared for process lifetime. No real S25 measurement is available.

## Implementation

- `OverworldWaterArt`: pure, versioned coordinate hash; exhaustive eight-neighbour mask; bounded distance to land/outside; convex corner rounding; discrete shallow/intermediate/deep palette; continuous coordinate variation and sparse sediment/reflection clusters. Water is never painted over dry cells by the material mesh.
- `OverworldWaterRenderer`: one merged horizontal-run water mesh, a sparse bank-detail mesh and sparse glint mesh. One active RG8 distance/variation lookup at one-third canvas resolution replaces the initially expensive palette tessellation. The static shader samples scalar data once, then quantizes colors at integer canvas pixels using the shared palette. No per-cell nodes/textures, seed-keyed native cache or full-resolution painted region texture. A second shared GL Compatibility canvas shader modulates glint opacity slowly; it never distorts UVs, topology or simulation. Nonessential glints remain static under reduced motion, with full static depth/bank artwork below them.
- Sparse short reeds and bank stones add upper-left lit shoreline detail. They are nonblocking ground artwork, not saved features or redesigned environmental assets.
- Old water edges/texture are bypassed for the overworld; dungeon terrain rendering remains unchanged. The old renderer is available **only** under `--visual-check --baseline-water` for paired comparisons.
- Region meshes/materials/lookup texture are disposed on removal. `TerrainBatch.Draw` now disposes any replaced native mesh before building another. Static geometry does not redraw every frame. The only ordinary per-frame water work is checking whether reduced motion changed.
- `OverworldVisualChecks`: reversible actual renderer/World/PPC/UI fixture; restores full snapshot, window and preferences. Includes pond, island, channel, grass, earth path, trees, shrubs, rocks, chest and three PPC actors; six native window sizes; normal seeds 1/42/1337; eight synthetic shape families and repeated rebuild intervals.
- `OverworldWaterChecks`: all 256 masks, complete opaque base coverage and at most one transition overlay over every synthetic pixel, bounded region-edge geometry, deterministic recipe/state checks, full save roundtrip and real identical movement/AI/combat continuation with/without art work.

## Visual review and evidence

The initial real-renderer prototype was rejected for overly regular shallow bands and a continuous dark rim. The refinement rounds corners, breaks bank material and adds sparse reeds/stones. An excessively tessellated broad-tone prototype was replaced by a low-resolution scalar lookup and palette shader after measured software-renderer regression. The close-up fixture was corrected to keep the player on land. All screenshots are actual Godot viewport images at unchanged gameplay zoom; no generated mockups or reconstructed game frames.

| Composition | Baseline | Living Reach |
| --- | --- | --- |
| Canonical composition | [Before](overworld-screenshots/baseline-reference.png) | [After](overworld-screenshots/final-reference.png) |
| Shoreline | [Before](overworld-screenshots/baseline-shoreline.png) | [After](overworld-screenshots/final-shoreline.png) |
| S25 surface (desktop rendering) | [Before](overworld-screenshots/baseline-2340x1080.png) | [After](overworld-screenshots/final-2340x1080.png) |
| Reduced motion | [Before](overworld-screenshots/baseline-reduced-motion.png) | [After](overworld-screenshots/final-reduced-motion.png) |
| Edge cases | [Before](overworld-screenshots/baseline-edge-cases.png) | [After](overworld-screenshots/final-edge-cases.png) |
| Production seed 42 | [Before](overworld-screenshots/baseline-exploration-seed42.png) | [After](overworld-screenshots/final-exploration-seed42.png) |

The folder also includes 1280×800, 1280×720, 1920×864, 1024×768, 900×560, seeds 1/1337 and multiple-creature compositions. The reference is a test-only arrangement, not new production geography. Raw existing terrain silhouettes constrain water boundaries; art must preserve narrow land paths and small islands rather than smooth away authoritative connectivity.

## Reproduction

Use the existing commands in `GAMEPLAY_VERIFICATION.md`: `dotnet build`, `python3 tools/import-project.py`, `python3 tools/verify.py --headless`, rendered desktop/mobile and `tools/gameplay-verify.py`. Focused captures:

```sh
xvfb-run -a -s '-screen 0 2560x1440x24' godot --path . --audio-driver Dummy -- --visual-check --mobile-ui --overworld-only
# Same fixture and camera with the old production rendering path:
xvfb-run -a -s '-screen 0 2560x1440x24' godot --path . --audio-driver Dummy -- --visual-check --mobile-ui --overworld-only --baseline-water
```

Native CI now runs the focused water fixture and uploads its screenshots/measurements. Existing native, gameplay and installed Android workflows remain intact. Offline/screenshot rendering is not proof of physical-device 60 FPS.

## Verification status

Native build passes (58 existing nullable warnings). The first integrated headless run passes **10,367 assertions**. Focused mobile-layout reference rendering passes. Final-source full desktop/mobile/gameplay/Android and paired performance results are pending and must not be marked complete yet. Import exits successfully with the engine's existing editor-only `export/android/shutdown_adb_on_exit` shutdown diagnostic; runtime checks must remain free of engine errors.

## Part 2 interface

Follow [OVERWORLD_ART_DIRECTION.md](OVERWORLD_ART_DIRECTION.md). Extend authoritative category -> cosmetic material variant -> neighbour transition -> static detail -> optional overlay. Reuse `OverworldWaterArt.Water`, `Mask`, `Distance`, versioned `Hash`, `Mark`/palette recipes and `TerrainBatch`. Keep ground under actors, pixel density/projection/lighting fixed, simulation independent and native caches bounded. Part 2 owns broad grass/earth/road/moss changes; Part 3 owns significant prop redesign; Part 4 owns broader composition/atmosphere.
