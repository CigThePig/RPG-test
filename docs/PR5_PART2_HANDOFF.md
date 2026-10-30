# PR5 Part 2 — Procedural environmental art

> **Superseded geometry and saves.** Part 2's renderer still applies; its room screenshots show the earlier fixed 11×11 grid, and its legacy-save notes no longer apply. The current room/corridor layout, saved fields and checks are in [DUNGEON_LAYOUT.md](DUNGEON_LAYOUT.md). Earlier saves are no longer loaded.

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

Part 2 changed no region geometry, mission graph, simulation code or authoritative save fields. Gate progression and exact save roundtrips remain in the full integration suite. `EnvironmentArtChecks` adds bounded recipes, resource availability, cache reuse, boundary-coordinate hashing, decoration stability and a repeated real World input simulation with/without cosmetic work, requiring identical full serialized outcomes.

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
| Same generated room, new art | [After](environment-screenshots/dungeon-after-room.png) | [After](environment-screenshots/mobile-dungeon-after-room.png) |
| Reference room and objects | [Room](environment-screenshots/environment-dungeon-reference.png) | [Room](environment-screenshots/mobile-environment-dungeon-reference.png) |
| Behind foreground wall | [Behind](environment-screenshots/wall-depth-behind.png) | [Behind](environment-screenshots/mobile-wall-depth-behind.png) |
| In front of wall | [Front](environment-screenshots/wall-depth-in-front.png) | [Front](environment-screenshots/mobile-wall-depth-in-front.png) |
| Traversable doorway | [Doorway](environment-screenshots/wall-depth-doorway.png) | [Doorway](environment-screenshots/mobile-wall-depth-doorway.png) |
| Closed/open grouped gate | [Closed](environment-screenshots/dungeon-gate-closed.png) / [Open](environment-screenshots/dungeon-gate-open.png) | [Closed](environment-screenshots/mobile-dungeon-gate-closed.png) / [Open](environment-screenshots/mobile-dungeon-gate-open.png) |
| Overworld | [Reach](environment-screenshots/environment-overworld.png) | [Reach](environment-screenshots/mobile-environment-overworld.png) |
| Combat | [Live](environment-screenshots/environment-dungeon-combat-live.png) | [Live](environment-screenshots/mobile-environment-dungeon-combat-live.png) |

All images are real viewport captures of the fixed-grid geometry of the time; the explicitly named `environment-part1-*` images are from an untouched Part 1 checkout on this runner. Enlarged crops are supplemental only.

## Verification and exact source

Final runtime: **`b3abe42075b14ed73ca8aeffefd2ea9ecf9fc04e`**, tree `311ca18c64a9be96f35870f7f47258d190482048`. The later documentation/evidence commit changes no production code. Initial environment runtime `c80b3a4d38ff` passed all suites; the final follow-up adds installed-Android dungeon entry/return coverage and keeps object reference captures at normal zoom. Only final-source results are counted below.

Local `dotnet build RpgTest.sln`, `python3 tools/import-project.py`, `python3 tools/verify.py --headless`, `python3 tools/verify.py --visual` and `python3 tools/verify.py --visual --mobile` all passed. Existing 58 nullable warnings remain. The editor-only shutdown diagnostic remains confined to import; strict runtime checks reject engine errors. Original PPC grass/dirt/water/tree1/rock1/bush1 regenerate pixel-identically with `node tools/export-art.mjs /tmp/catalog --family=biped --seed=42`. Dungeon-rule packaging passed.

| Final-source verification | Result | Evidence |
| --- | --- | --- |
| Build, import, production integration | Passed; **4,603 assertions** | [Native CI](https://github.com/CigThePig/RPG-test/actions/runs/41021407565) |
| Desktop/mobile real viewport, wall/tree depth and region checks | Both passed; captures inspected | [Native artifact](https://github.com/CigThePig/RPG-test/actions/runs/41021407565/artifacts/12961668262) |
| Preserved original reference art/tests/build | Passed | Same native workflow |
| Baseline touch movie + independent repeat | All 45 scenarios passed; old/new authoritative digest matches | [Baseline artifact](https://github.com/CigThePig/RPG-test/actions/runs/41021407593/artifacts/12961773480) |
| Precise touch movie + independent repeat | All 45 scenarios passed | [Precise artifact](https://github.com/CigThePig/RPG-test/actions/runs/41021407593/artifacts/12962407165) |
| Focused seed-1 dungeon expedition + independent repeat | Passed; key, required gate, relic, return and matching digest | Same precise artifact, `dungeon-expedition/`; [inspected frame sheet](environment-screenshots/dungeon-expedition-contact-sheet.jpg) |
| ARM64 package/source/resources/signature | Passed | [Android CI](https://github.com/CigThePig/RPG-test/actions/runs/41021407595) |
| Installed x86_64 Android touch/combat/lifecycle/three dungeon roundtrips | Passed; captures inspected | [Runtime artifact](https://github.com/CigThePig/RPG-test/actions/runs/41021407595/artifacts/12962062709) |
| Physical Galaxy S25 presentation/audio/sustained performance | **Not performed**; no physical device attached | User playtest remains |

All three final workflows completed successfully on the source above. No unresolved code/test failure remains; physical-device acceptance and the documented software-renderer cost remain limitations. Native screenshots in this handoff come from the final source artifact. Original Part 1 evidence is kept unchanged under `native-screenshots/`; Part 2 evidence is separate under `environment-screenshots/`.

Wall checks restore 762 foreground-wall pixels, retain 1,072 upper-silhouette and 1,446 front-side comparison pixels; the clear vertical doorway has zero ordering changes. Horizontal doorway ground-strip comparison and physical crossing pass. A production tree restores 2,416 covered pixels behind it and 1,332 front-side pixels. Both viewport layouts exercise the actual F10 developer region-entry route.

`tools/gameplay-verify.py --presets baseline,precise` also records the focused precise `dungeon-expedition`, independently repeats it and checks the authoritative digest. All normal replay scenarios are retained. Movies are actual Godot frames, not reconstructions. Offline capture cadence is not a physical-device FPS claim.

An independent Part 1 versus initial Part 2 rendered baseline comparison matches **all 5,890 authoritative samples across 45 scenarios**: received analog, position, actions and collision. SHA-256 `8505cd21cbbc6375b14d0c70b162da44d7d8022072304d8ef6683cb3e8b36d45`. The final-source hosted baseline digest is checked against this value. See [renderer-independence.json](environment-evidence/renderer-independence.json). Frame/render clocks are deliberately excluded. Full integration also preserves existing interaction, quest, gate, save roundtrip and legacy-fixture checks.

## Performance and memory

| Measurement | Paired Part 1 median | Paired Part 2 median | Final hosted Part 2 |
| --- | ---: | ---: | ---: |
| Mobile-layout terrain + wall build, ms | 3.263 | 4.143 | 1.521 |
| Mobile-layout static p50 interval, ms | 10.756 | 11.511 | 10.939 |
| Mobile-layout static p95 interval, ms | 12.852 | 16.563 | 12.047 |
| Desktop terrain + wall build, ms | not sampled | not sampled | 1.069 |
| Desktop static p95 interval, ms | not sampled | not sampled | 12.426 |
| Three live archetypes p95, desktop/mobile, ms | not sampled | not sampled | 13.929 / 13.592 |

Local Part 1 and Part 2 comparisons use two checkouts of the same expanded seed-42 geography on this runner, 1.5× zoom and llvmpipe. These are short observations, not controlled hardware benchmarks. Hosted columns are a separate CI allocation and must not be compared with local values as a same-machine speedup. Historical Part 1 CI reported combined mesh construction 2.406/2.384 ms and static p95 18.408/18.341 ms; host scheduling, actor work, software rendering and sample size limit comparisons. No sustained Galaxy S25 60-FPS claim is made.

The first all-geometry prototype cost 9.06 ms combined construction and p95 27.85 ms. It was replaced with shared atlases before delivery. Warm atlas mesh preparation is near the old construction range; local mobile construction increased about 0.9 ms while frame intervals varied in both directions. `environment-evidence/part1-*.json`, `local-*.json` and `ci-*.json` retain raw data. Construction timers measure CPU command/mesh preparation, not GPU completion. Synchronous `RebuildViews` excludes deferred first draw; the 12-frame readiness value includes frame scheduling rather than just preparation CPU. Cold generation, upload and JIT are not individually isolated by these timers.

Final hosted synchronous view rebuild cost is **2.24–2.88 ms**. Typical warm dungeon terrain/wall CPU preparation is about **1.1–1.6 ms**; early terrain samples reached **7.80 ms** under the full suite. The two-atlas approach bounds preparation but does not remove cold/JIT/GC/scheduling spikes.

Across six alternating entries, engine-wide texture memory stays exactly **10,326,617 bytes desktop / 10,588,761 mobile layout** (all game textures, not only environment art). Buffer memory returns to the same region-specific totals. Actor-layer nodes return to **59 dungeon / 65 Reach**, with **5 / 43 decorations** and **33 / 0 wall rows**. After warm-up, managed/Godot memory returns within roughly 1 KiB between equivalent entries. This bounded observation shows no growing texture cache, node population or native buffer allocation.

Three alternating paired runs on this local host gave median mobile-layout p95 **12.852 ms Part 1 → 16.563 ms Part 2 (+3.710 ms, about 29%)**, with median p50 **10.756 → 11.511 ms**. Raw samples and limitations are in [paired-summary.json](environment-evidence/paired-summary.json). This is a measurable software-renderer regression; the single earlier local comparison is not used to claim an improvement. The old checkout differs only in a verification-dispatch hook exposing the existing fixture; its production renderer remains Part 1. Static preparation is near the old range but has occasional GC/scheduling outliers in both versions.

The renderer adds a floor texture pass and textured wall/natural-art sampling in exchange for the visual detail. Construction remains a few milliseconds; software frame intervals increased relative to historical hosted runs. A likely contributor is the additional texture/overdraw work on llvmpipe, but these samples do not isolate that cost from host scheduling and PPC animation. Physical-device profiling remains necessary; the art is not used to excuse or conceal the measured cost.

The two immutable atlas payloads contain **218,112 bytes** before engine/driver overhead. Terrain vertex payload is 450,648 bytes for the reference dungeon and 660,264 for Reach, excluding walls, native mesh overhead and staging lists. No textures are keyed by region seed. New visuals consume existing saved coordinates; static art is never rasterized per frame. Repeated entries are a bounded leak/cleanup check, not proof of long-session stability. Working-set inspection is unavailable in the restricted local host; managed/Godot/GPU resource observations are labeled separately.

## Visual evaluation

Real normal-zoom desktop/mobile captures were inspected. Walls now show stone material and separated upper/front faces, door gaps remain open, gold keys/relics/chests remain identifiable, natural assets share upper-left shading, and combat/telegraphs remain distinct. Large solid wall masses still repeat their cap modules visibly; this is one coherent material set, not multiple handcrafted architectural themes. The reference fixture intentionally clusters three PPC actors for stress coverage. More curated compositions and landmark silhouettes remain Part 3 work.

[Desktop object crop](environment-screenshots/environment-objects-crop.png) and [mobile object crop](environment-screenshots/mobile-environment-objects-crop.png) are explicitly nearest-neighbour enlarged crops of actual normal-zoom reference captures. The final fixture no longer changes zoom to make these probes. [Contact sheet](environment-screenshots/environment-contact-sheet.jpg) is composed only from actual viewport frames. No synthesized or reconstructed gameplay imagery is used.

## Android package and installed runtime

[Final Android workflow](https://github.com/CigThePig/RPG-test/actions/runs/41021407595).

**[Download ARM64 development APK ZIP — b3abe42075b1](https://github.com/CigThePig/RPG-test/actions/runs/41021407595/artifacts/12962110914)**. Extract/install `Mosslight-android-arm64-debug-b3abe42075b1.apk`, not the x86_64 emulator package. Source, ARM64 ABI/runtime/resources and signature validation passed. APK bytes: 104,438,891; SHA-256 `0dd7b454a77f5a012236f9f832bf6c156f5d6dbf32b1513abfb1b2cc5e9175f4`. Metadata: [android-package.json](environment-evidence/android-package.json). GitHub retains this artifact for 30 days from the build unless retention changes.

The installed **x86_64 emulator companion**, built from the same source, passed the extended smoke. [Actual Android evidence](https://github.com/CigThePig/RPG-test/actions/runs/41021407595/artifacts/12962062709) includes captures/logs/video and [regions.json](environment-evidence/android-regions.json). Three cycles each entered the saved/generated ruin, completed an Attack and Dodge, and returned to Reach; wall/decoration/atlas counts stayed **55/3/2 in the ruin and 0/43/2 in Reach**. Startup, nonblank world/menu/pack, measured touch movement, Gloot chest depletion, real damage/spell/projectile presentation and three same-process background/resume cycles also passed. [Actual third-cycle dungeon combat capture](environment-screenshots/android-ruin-combat.png) was inspected. This is installed Android runtime evidence, not an ARM64 physical-device benchmark.

The superseded `bb24fc8` smoke failed a new fixed one-second roll-completion check. Its log showed that the real roll completed at roughly 1.2 seconds. The final test waits for the actual completion event, bounded to 15 seconds, without changing gameplay timings or repeating the input. The final-source run passes; no production behavior was weakened to satisfy the check.

The smoke uses the existing developer entry/return actions via F10: it first leaves the reversible combat fixture, calls authoritative `World.Enter`, builds production terrain/features/actors, and runs ordinary touch attacks and rolls. It does not directly change serialized tiles, replace the renderer or teleport through mission gates. Region screenshots, `regions.json` and the stable wall/decoration/atlas counts verify three entry/return cycles in the installed exported app. The earlier touch movement/chest/damage/spell/projectile and three background/resume checks remain intact.

This is not a complete Android key/gate/relic adventure. The full protected production-touch expedition is verified in desktop/mobile-layout movies; physical Galaxy S25 sustained performance, subjective presentation, touch comfort and audio remain user playtesting. The established ephemeral development signature may require uninstalling a differently signed installed build, deleting its private saves. Preserve valuable progress first. Save-format compatibility itself requires no new game or regeneration of existing ruins.

## Part 3 readiness

The procedural material/cache/batch and ground-contact prop APIs are implemented and integrated. Use the contracts above for handcrafted landmarks, environmental themes and bounded decoration. Mission generation, terrain dimensions, collision, combat timing, inventory, quests and schema 1 remain authoritative and unchanged. PR #5 remains open, draft and unmerged; Parts 3 and 4 are not implemented here.
