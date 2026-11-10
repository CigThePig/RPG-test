# Living Reach — Part 1 handoff

Runtime: **`5a67da01be3d583699c658db33f3e1dda398a7df`**. Base: **`ca60f3eda8153c0f7864703e52a1b09f5228f290`**, the inspected main containing merged PR7. Branch: `feat/overworld-visual-overhaul`. [Draft PR #8](https://github.com/CigThePig/RPG-test/pull/8). Do not merge; Parts 2–4 remain unimplemented. Game rendering code is unchanged after this runtime. Verification-only Python changes end at `3b54709ab40234b3ac4f9be0749d6507464efdee`; the final delivery commit contains documentation/evidence only (see this file’s commit history and PR head).

Implementation and native verification are complete. **Native and baseline/precise gameplay CI passed on the game runtime above. Both installed Android matrix jobs passed, with the standard job passing on an unchanged rerun after an intermittent blank resume. Physical Galaxy S25 acceptance: the owner ran the PR #8 ARM64 APK on the physical Galaxy S25 and accepted its appearance, touch controls and performance. Part 1 is not marked fully accepted while this lifecycle gap remains.**

## Baseline audit and scope

Foundation owns a ground parent at Z=-10 and a separate Y-sorted actor/feature/decoration parent. Terrain originally painted solid per-cell bases, PPC grass/dirt/water at 32% alpha, and continuous pale cardinal water borders. Natural terrain rocks were drawn after water in TerrainView and extended seven canvas pixels above their cells. Preserving this ordering matters: placing the new water above TerrainView clips those rock tips.

Projection remains `GodotConvert.GroundToScreen`: X unchanged, Y halved; 24×24 ground units become 24×12 canvas units. PR7's 1280×800 canvas_items/expand surface, camera presets/bounds, safe areas and mobile controls remain unchanged. PPC art uses nearest sampling. Existing `EnvironmentAssetCache` has **three**, not two, immutable atlases: 517,632 pixel bytes. No upstream PPC generator was copied or modified, and no external art/dependencies were added.

`RegionState.Passable` still permits water only for swimmers/fliers. Circular collision, navigation, FastNoiseLite geography, features, world RNG, quests, inventory, saves, character presentation and dungeon generation remain authoritative and unchanged. No save fields/version/migration were added. Rendering reads serialized region data; the custom fixture never loads or writes ordinary user slots.

## Rendering implementation

- `OverworldWaterArt`: pure eight-neighbour mask, bounded distance to land/outside, convex rounding, shared palette and explicitly unsigned seeded coordinate hashing. Cosmetic revision is separate from saved generation version. No `Random`, string hash or clock selects permanent artwork.
- `OverworldWaterRenderer`: merged horizontal water runs in one surface mesh, one small bank-detail mesh and one sparse glint mesh. No nodes/textures per water cell and no seed-keyed cache.
- One active RG8 scalar lookup is sampled every three canvas pixels. R encodes `(distance + 8) / 68`; G carries stable variation. Dimensions are `(width*8+1) × (height*4+1)`: **198,018 bytes** for normal 64×48 Reach, **263,682 bytes** for the 64×64 fixture. This is distance/variation data, not a full-resolution painted region texture. It is disposed on region removal.
- `overworld_water.gdshader` reads the scalar data once and chooses discrete shared palette colors at integer canvas pixels. Linear filtering interpolates scalar distance, not artwork colors. No atlas bleeding, UV distortion, screen-space reflection or smooth color gradient is introduced.
- Water uses damp soil, sediment, shallow, intermediate and deep colors. Tiny pools have no forced deep centre. Sparse short reeds, stones and restrained broken highlights integrate banks without replacing grass/earth materials. Detail rectangles are clipped to region bounds.
- A separate glint shader changes only faint overlay opacity, with a slow coherent phase. Static topology/material never rebuild per frame. Reduced motion selects fixed opacity and retains all depth/bank detail. `--water-static-fallback` uses palette geometry and the default canvas material, without either custom water shader.
- Two shared shader resources live for the process. Active meshes, two materials and the lookup are released on region exit. `TerrainBatch.Draw` now disposes an old native mesh before replacement. Dry regions allocate no water lookup.

### Depth contract

| Layer | Effective Z | Rule |
| --- | ---: | --- |
| Water surface and glints | -11 | Water renderer at -1 beneath ground parent -10 |
| Flat ground and raised terrain-rock artwork | -10 | TerrainView; new mode leaves water cells unfilled |
| Bank details | -10 | Water child +1; draws after flat ground |
| Existing combat telegraphs | -1 | Unchanged |
| Y-sorted actors, features, wall rows | 0 | Unchanged |
| PR7 interface | Separate UI canvas | Unchanged |

The opaque new water owns its complete footprint. **Do not restore a water base fill above it.** This both removes redundant overdraw and lets raised PPC terrain rocks appear above water. The rendered depth test restores **12 rock-tip pixels** when the deliberately reproduced bad ordering is corrected. Its enlarged comparison is a labelled nearest-neighbour crop of actual frames, not new artwork.

## Fixture, determinism and evidence

`OverworldVisualChecks.Fixture` is the canonical production-renderer composition: large pond, shallow channel, grass island, raised rock, grass/earth path, PPC trees/shrubs, rocks, chest, player and three existing creatures. Creatures use normal `FindPlacement` clearance. World/narrative snapshot, preferences, window and engine scale are restored in finally.

All six native sizes are captured: 1280×800, 1280×720, 1920×864, 2340×1080, 1024×768 and 900×560. Other captures cover production seeds 1/42/1337, eight synthetic shape families, explicit water at the world boundary, reduced motion, close shoreline and multiple creatures.

`--compare-water` alternates the old/new renderer through twelve 120-frame intervals in one frozen process. **Every interval requires identical complete serialized world/narrative state.** The static water build counter must remain unchanged within each interval. The old renderer is selectable only during `--visual-check`; ordinary gameplay always uses Living Reach.

`OverworldWaterChecks` adds all 256 neighbour masks, complete bounded pixel coverage, boundary pools, deterministic recipes for all three seeds, unchanged serialized terrain, full save roundtrip and identical real movement/AI/combat continuation with and without cosmetic work. The existing simulation/quest/combat/dungeon/UI/touch/save checks remain intact.

| Composition | Original renderer | Living Reach |
| --- | --- | --- |
| Canonical reference | [Before](overworld-screenshots/baseline-reference.png) | [After](overworld-screenshots/final-reference.png) |
| Close shoreline | [Before](overworld-screenshots/baseline-shoreline.png) | [After](overworld-screenshots/final-shoreline.png) |
| 2340×1080 surface | [Before](overworld-screenshots/baseline-2340x1080.png) | [After](overworld-screenshots/final-2340x1080.png) |
| Reduced motion | [Before](overworld-screenshots/baseline-reduced-motion.png) | [After](overworld-screenshots/final-reduced-motion.png) |
| Edge cases | [Before](overworld-screenshots/baseline-edge-cases.png) | [After](overworld-screenshots/final-edge-cases.png) |
| Region boundary | [Before](overworld-screenshots/baseline-region-boundary.png) | [After](overworld-screenshots/final-region-boundary.png) |
| Production seed 42 | [Before](overworld-screenshots/baseline-exploration-seed42.png) | [After](overworld-screenshots/final-exploration-seed42.png) |

[Rock-depth comparison](overworld-screenshots/final-rock-depth-detail.png) · [Full corrected frame](overworld-screenshots/final-rock-correct-order.png) · [Default-material fallback](overworld-screenshots/fallback-reference.png).

`baseline-*` uses the original production rendering path on the same fixture. `final-rock-old-order-reproduction` is the **new** water temporarily raised to reproduce the fixed depth defect, not untouched main. All full frames are actual Godot captures. Existing grass/earth and major props deliberately remain for Parts 2/3.

Visual review rejected an early regular-band/dark-rim prototype, refined corners/banks and sparse detail, replaced dense pixel tessellation after measured cost, removed redundant fill, corrected rock depth, and verified boundary clipping. The final review found connected surfaces, restrained depth, clear small pools/channels/islands and unchanged UI placement. Authoritative tile geography still constrains broad boundaries; art never erases land corridors.

## Commands and results

Use the pinned Godot 4.6.1 .NET and SDKs 8.0.408/9.0.203. Local rendered commands use an Xorg dummy display because Xvfb is not installed; CI uses the documented xvfb-run wrapper.

| Command/check | Result on runtime above |
| --- | --- |
| `dotnet build` | Passed; zero errors, 58 existing nullable warnings |
| `python3 tools/import-project.py` | Passed in fresh native CI |
| `python3 tools/verify.py --headless` | Passed locally and CI: **10,367 assertions** |
| `xvfb-run -a python3 tools/verify.py --visual` | Passed in final-source CI |
| `xvfb-run -a python3 tools/verify.py --visual --mobile` | Passed in final-source CI |
| Native widescreen and original reference tests | Passed in final-source CI |
| `--visual-check --mobile-ui --overworld-only` | Passed locally and CI |
| Same plus `--compare-water` | Passed locally; twelve intervals and exact world-state equality |
| Animated/reduced-motion pixel check | Passed: 168 changed pixels across controlled phases, **0** in reduced motion |
| Rock depth | Passed: 12 restored pixels |
| `--water-static-fallback --quit-after 90` | Actual reference rendered and inspected; this bounded capture is not a full fixture-suite run |
| `tools/gameplay-verify.py --presets baseline` and `--presets precise` | Passed in hosted CI, including independent repeats, precise dungeon expedition and rendered normal-health adventure |
| Signed ARM64 package and installed Android runtime matrix | Signed ARM64 validation passed; x86_64 installed runtime passed at 1560×720 and 1280×800 (standard layout required one unchanged rerun; intermittent resume defect remains documented) |
| Physical Samsung Galaxy S25 | **Accepted by the owner** (appearance, touch controls, performance) on the PR #8 APK |

[Native CI](https://github.com/CigThePig/RPG-test/actions/runs/42142826134) · [Native evidence](https://github.com/CigThePig/RPG-test/actions/runs/42142826134/artifacts/13468893022) · [Gameplay CI](https://github.com/CigThePig/RPG-test/actions/runs/42142826131) · [Android CI](https://github.com/CigThePig/RPG-test/actions/runs/42142826132).

The native workflow includes full normal-health adventure checks, original reference tests, wall/prop depth, landmarks, input/UI and widescreen checks. Existing Android packaging/signature/ABI/resource and emulator touch/combat/lifecycle/region checks are retained. The optional redundant local four-preset movie capture was interrupted; it is not claimed passed. The repository's hosted baseline/precise commands provide routine replay validation.

Import has the pre-existing editor-only `export/android/shutdown_adb_on_exit` shutdown diagnostic; runtime logs must remain free of errors. An early local import lacked the pinned .NET 8 SDK and an early headless attempt overlapped first import; neither is counted as accepted verification. The accepted runs use both pinned SDKs and an imported project.

## Android harness follow-up

The first installed run on game source `5a67da0` failed verification rather than package validation. The standard layout lost its initially validated control records after noisy Android log-buffer rollover. The wide layout rendered correctly through movement, chest loot, dialogue and damage, but its host-attached screenrecord process exited almost immediately and left an empty MP4. Actual Android screenshots were inspected; water and the full-width surface were present.

Verification revision `3b54709ab40234b3ac4f9be0749d6507464efdee` changes Python harness code only. `LogHistory` retains genuine app diagnostics, critical errors and recorder logs across buffer rollover, preserves newer targets, and tracks duplicate-line multiplicity so genuine duplicate dispatch is not hidden. Nine layout/history and four native-window unit checks pass (`cd tools && python3 -m unittest test_android_layout.py test_android_window.py`). No coordinates or events are fabricated. All existing action, lifecycle, region and video assertions remain.

Recording now runs in its own device session using `setsid`, writes its actual PID, closes inherited standard streams, and flushes on SIGINT before the MP4 is pulled. AOSP screenrecord installs its own [SIGINT/SIGHUP handlers](https://android.googlesource.com/platform/frameworks/av/+/master/cmds/screenrecord/screenrecord.cpp), so nohup alone would not remove the terminal-lifetime dependency. Native video must still exceed 10 KB, decode more than five frames and show temporal changes. The wide installed run on this revision passed every assertion, including three background/resume cycles, three dungeon-entry/return cycles and decoded nonempty native video. [Android exploration](overworld-screenshots/android-wide-exploration.png) and [third resumed combat](overworld-screenshots/android-wide-resumed-combat-3.png) are actual installed Android captures; they were visually inspected. Raw [result](overworld-evidence/android-wide-result.txt), [lifecycle](overworld-evidence/android-wide-lifecycle.json), [regions](overworld-evidence/android-wide-regions.json) and [display](overworld-evidence/android-wide-display-geometry.json) are retained.

The standard layout's first attempt failed at its first resumed menu: an actual blank framebuffer accompanied an abandoned BufferQueue and `EGL_BAD_SURFACE` in Android's GLSurfaceView. This occurred in the dry `feel-lab` combat course, which contains only grass/rock and has no water renderer. The process remained alive. [Selected actual failure log](overworld-evidence/android-standard-resume-failure.log); [complete failed-attempt evidence](https://github.com/CigThePig/RPG-test/actions/runs/42142826132/artifacts/13468484586). The unchanged rerun (job `127652879542`) passed all assertions, including all three lifecycle cycles. The overall Android workflow is green on attempt 2. [Successful standard evidence](https://github.com/CigThePig/RPG-test/actions/runs/42142826132/artifacts/13469517705) and its raw [result](overworld-evidence/android-standard-result.txt), [lifecycle](overworld-evidence/android-standard-lifecycle.json) and [region counts](overworld-evidence/android-standard-regions.json) are retained; its third resumed combat frame was also visually inspected. This is an intermittent native surface/lifecycle failure, not evidence that the frame was acceptable; the successful rerun does not establish that the intermittent issue is eliminated. No automatic retry or relaxation was added to lifecycle assertions.

## Performance and memory

[Final alternating observations](overworld-evidence/final-paired.json) · [First-process baseline](overworld-evidence/final-baseline.json) · [Summary/method](overworld-evidence/final-performance-summary.json).

Debian 13, Godot 4.6.1 .NET, GL Compatibility, llvmpipe LLVM 19.1.7, 1280×800 mobile layout and unchanged 1.5× zoom. Twelve alternating 120-frame intervals; the table is the median of six interval statistics per style, **not a pooled p95**. No concurrent local build/test ran during the accepted comparison. This is software rendering, not emulator or physical-device performance.

| Measurement | Original | Living Reach |
| --- | ---: | ---: |
| Frame p50, ms | 31.3772 | 30.8057 |
| Frame p95, ms | 46.6785 | 44.8835 |
| Terrain CPU preparation, ms | 3.6914 | 2.6720 |
| Additional water preparation, ms | 0 | 13.2184 |
| Ready including 12 process frames, ms | 417.0096 | 434.6272 |
| Batched terrain/water vertices | 25,254 | 23,544 |
| Batched staging vertex payload, bytes | 707,160 | 659,280 |
| Active reference lookup pixel bytes | 0 | 263,682 |
| Engine-wide texture bytes | 12,438,991 | 12,790,055 |
| Engine-wide buffer bytes | 7,247,420 | 7,220,060 |
| Managed bytes | 17,503,196 | 17,300,700 |
| Godot static bytes | 46,067,537 | 46,089,633 |

Final median p95 is **3.85% lower** in this alternating observation, with no observed regression against the approximately 10% review threshold. This short, noisy host test is not a hardware speedup guarantee. Both versions exceed 16.7 ms here; **no S25 60-FPS claim is made**.

Process-first synchronous environment construction was **34.3153 → 72.3932 ms**; separate first terrain command preparation was **31.6748 → 19.5111 ms**. These include process JIT/lazy loading and use normal seed-42 Reach; they exclude deferred GPU completion and do not describe a cold machine. The added distance field has a measurable entry/build cost, isolated from steady rendering.

The final vertex figures count TerrainBatch attributes and exclude unchanged immediate grass/dirt texture quads, actors and UI. Staging payload uses the helper's 28/36-byte attribute estimator; engine-wide native buffer memory is reported separately. Texture counters include all resources/driver estimates, not only the RG8 pixel payload.

Every occurrence of each style returns to exactly the same texture and buffer totals across six switches. Managed memory varies with GC; no long-session plateau is claimed. Existing full environment verification also alternates six real region entries. No GPU timer-query attribution, isolated first GPU completion, process working-set or physical S25 measurement was available.

Earlier dense-mesh and independent-process observations are retained in this evidence folder, including regressions. An early paired p95 rose about 45%; that prompted investigation, removal of redundant fill and alternating comparisons. The dense prototype had about 1.19 MB of water vertices; the final water batch has **18,648 bytes**. Subsequent alternating tests improved p95 in both preceding and final fixtures; host variability remains a limitation. No unfavorable observation was used to claim a device result.

## Ownership and Part 2 readiness

- `Foundation.cs`: isolated renderer insertion, water cutout/layer ordering, process-first timing; gameplay methods unchanged.
- `OverworldWaterArt.cs`, `OverworldWaterRenderer.cs`, `overworld_water.gdshader`: topology/recipes, active resources and palette material.
- `TerrainBatch.cs`: replacement mesh disposal.
- `RpgUi.cs`: existing reduced-motion option description only.
- `OverworldWaterChecks.cs`, `OverworldVisualChecks.cs`, existing test dispatchers: integrated coverage and reversible fixtures.
- `.github/workflows/ci.yml`: focused water references and uploaded evidence. Existing jobs/checks remain.
- Art direction, this handoff, screenshots and raw measurements: durable standards for Parts 2–4.

Follow [OVERWORLD_ART_DIRECTION.md](OVERWORLD_ART_DIRECTION.md). Reuse authoritative category → cosmetic variant → neighbour transition → static detail → optional overlay. `Water` and `Mask` work for both bank sides; `Distance` is wet-side distance to land in ground units. Extend dry-side recipes using the same topology rules. Reuse the versioned `Hash`, shared `Ramp`, fallback `Mark` recipes and TerrainBatch lifetimes. Preserve the depth contract above, projection/pixel density, clear traversal boundaries and simulation independence.

Part 2 owns broad grass/earth/road/moss replacement. The old grass/dirt overlays are **not** the quality target: build against PPC creatures and the accepted water reference rather than copying their noise/grid. Part 3 owns significant prop redesign; update the depth probe if prop ownership changes, retaining occlusion coverage. Part 4 owns broader composition and atmosphere. Physical S25 installation, subjective appearance, touch comfort, audio, sustained pacing and long-session resource behavior remain user validation.

## Acceptance disposition

Game implementation, native checks, gameplay checks, signed APK validation and both installed emulator jobs have executed successfully. Native run `42142826134`, gameplay run `42142826131`, and Android run `42142826132` (attempt 2) are green on verification revision `3b54709`. The earlier game-only revision has independent native/gameplay passes as well. Final delivery changes are documentation/evidence only; their new CI run may still be pending when the PR is updated.

Part 1's overall checkbox remains open: the intermittent standard-layout blank resume was observed and has not been fixed or proven eliminated. Its occurrence in the dry combat course bounds its relationship to the water renderer but does not make the lifecycle defect acceptable. Gate 7 lifecycle stability and physical S25 acceptance require follow-up; no engine fork or test relaxation was introduced to hide it. All later implementation parts remain unimplemented. The next session can build Part 2 on this foundation while retaining this explicit acceptance debt.
