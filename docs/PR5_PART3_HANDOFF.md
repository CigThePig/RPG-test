# PR5 Part 3 — Authored places in procedural dungeons

> **Superseded geometry and saves.** Part 3's templates still apply, now stamped onto each room's 9×9 core. Its statements that the 11-cell geometry and older saves are retained no longer apply. The current room/corridor layout, saved fields and checks are in [DUNGEON_LAYOUT.md](DUNGEON_LAYOUT.md). Earlier saves are no longer loaded.

## Implementation summary

**Final runtime/source: `488174f7a0835584cf5059cb65e9e9b3c87be37e`**, tree `0e5b85d731d13d2bcd37f229a1ccfd081bebefbf`. Later commits add evidence/documentation only.

Continues pre-Part3 branch `a080e538c080be8204b8d4ecb86205324ab5e1a5`, preserving its gameplay/CI corrections. The existing PR #5 remains draft and unmerged. Five JSON-authored room compositions now appear in production new dungeons: Ruined Entrance, Forgotten Archive, Abandoned Shrine, Collapsed Storeroom and Relic Sanctuary. Weathered, overgrown and ceremonial materials share the Part 2 renderer. Authored arrangements mix with independent seeded orientations, prop variants, optional ground slots, coherent environmental zones and existing ambient scatter.

## Architecture and procedural integration

`Landmarks` owns library parsing, static validation, feature-derived classification, cardinal mask compatibility, rotation, independent stable hashing, candidate application, radius-11 room validation and bounded safe-open fallback. The original graph/layout/grammar and its Random stream are unchanged. Compose runs only after the original layout passes solvability and before the new region is returned. Selection prioritizes entrance/objective/key/chest before ordinary rooms, preserving required art under the 96-node budget. No feature moves, actors shrink or combat/movement timings change. RegionState owns resolved `Landmarks` alongside authoritative terrain. Presentation reads that manifest without rerunning selection.

The primary role comes from the existing center feature; dead-end/junction/hub are secondary topology descriptors. Masks use N/E/S/W bits 1/2/4/8 and rotate with local geometry/anchors/focal/facing. Every shipped template supports all nonempty masks by preserving full cross lanes. Role eligibility prevents optional rooms replacing mission entrance/objective landmarks.

## Template authoring

[LANDMARK_AUTHORING.md](LANDMARK_AUTHORING.md) documents the schema with a complete practical example, coordinates, supported symbols/assets/categories/themes, masks, rotations, protected lanes/features, seeds, previews, validation commands and revision policy. ContentAudit loads the packaged library at startup; JSON typos, missing resources and malformed entries fail clearly. Individual production-renderer previews accept template ID, quarter-turn and deterministic seed. No new editor framework is required.

## Structural safety

Templates fill each room's 9×9 core; rooms may extend beyond it (see DUNGEON_LAYOUT.md). Blocking elements are existing Wall terrain, with one matching visual each; floor motifs and scattered records have no collision. All central three-tile cross lanes and the perimeter ring remain open, with radius-11 swept footprint connectivity across every floor cell. Critical feature centers retain at least 60 units of structural separation. Boundary/gate cells are untouched. Candidate failure rolls back before trying the next bounded option; safe-open preserves otherwise valid generated rooms. Large sampled dungeons can exhaust the architectural budget and deliberately receive optional open fallback rooms; diagnostics identify seed, coordinates, template/revision/turn and reason. Required architecture is admitted first.

New column tests use actual World.Move, dodge, slash and projectile flight. Existing canonical-seed footprint checks cover player, biped NPC, prowler, brute and spitter; mission topology signatures, grouped gate progression and live generated-doorway encounters remain. The touch expedition now targets the protected perimeter instead of assuming an authored corner tile is floor. Its actual path must go around new blockers.

The original wall-depth fixture now explicitly selects a shared boundary row, because an interior two-tile rubble composition can also satisfy its former generic wall predicate. Foreground reference pixels are isolated to that boundary and ambient tall props are hidden in this wall-only probe. Separate new column pixel checks retain real actor/prop foreground/background verification. No runtime sorting rule was weakened.

## Environmental art

The original 384×12 floor and 1664×30 wall atlases are retained. One 1170×64 RGBA8 atlas adds five structural silhouettes × three variants × three themes, with gutters. Added payload: 299,520 bytes; total three-atlas payload: 517,632 bytes. No room/seed texture keys. Floor/wall themes use existing atlas vertex tints. Flat records, moss, thresholds, mosaics and offerings share one retained ground mesh below telegraphs. Column/shelf/altar/open-storage/collapse props use front ground-edge pivots in the existing actor Y-sort layer. Solid-looking furniture always has authoritative terrain. No fake decorative loot chests, external asset dependencies or alternate renderer.

## Saved landmarks

Saves store resolved room/template/revision/orientation/theme/focal/anchors, not a live pointer into a mutable template library. Future template edits affect only newly generated regions. Keep saved asset ID meanings stable. Snapshot validation checks anchor bounds, asset IDs, budgets and terrain agreement before restore.

The final review guards reject cosmetic artwork owning structural collision, both in authored content and saved manifests. Prop billboard light ramps stay fixed at world upper-left while their room positions rotate. Saved facing remains an orientation/content hook; no reflected lighting is introduced.

## Verification

Baseline pinned build/import and **4,609 assertions** passed on a080e538. Final local integration passes **6,210 assertions** with the existing 58 nullable warnings. The standalone authoring validation, rotated multi-actor preview with separate collision image, and focused final-source landmark/mobile/column suite also pass.

| Final-source verification | Result / evidence |
| --- | --- |
| Build, grammar packaging, fresh import, production integration | Passed — [native workflow](https://github.com/CigThePig/RPG-test/actions/runs/41036935137) |
| Full desktop/mobile views, all landmark/canonical-seed/column/wall/tree checks | Passed — same workflow and native artifact below |
| Preserved reference art regeneration/tests/build | Passed — same workflow |
| All 45 baseline scenarios and independent rendered repeat | Passed — [movies/repeats](https://github.com/CigThePig/RPG-test/actions/runs/41036934915) |
| All 45 precise scenarios plus focused key/gate/relic/return expedition and repeats | Passed — same workflow |
| ARM64 export/package/source/resources/signature | Passed — [Android workflow](https://github.com/CigThePig/RPG-test/actions/runs/41036934785) |
| Installed x86_64 Android touch/combat/lifecycle and three authored-dungeon roundtrips | Passed — same Android workflow |
| Physical Galaxy S25 presentation, audio, comfort and sustained performance | Not performed; user playtest required |

Exact commands:

```sh
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
xvfb-run -a python3 tools/verify.py --visual
xvfb-run -a python3 tools/verify.py --visual --mobile
xvfb-run -a python3 tools/gameplay-verify.py --presets baseline,precise
godot --headless --path . -- --validate-landmarks
xvfb-run -a godot --path . --audio-driver Dummy -- --visual-check --landmarks-only --mobile-ui
xvfb-run -a godot --path . --audio-driver Dummy -- --visual-check --landmarks-only --landmark=forgotten-archive --orientation=1 --visual-seed=42 --landmark-debug
```

Tests cover all templates × four rotations × all 15 nonempty masks; malformed dimensions/symbols/assets/materials/roles/rotation/focal/feature lanes and structural/cosmetic mismatches; 97 independently repeated seeds (0..63, 1337, 1000..1031); natural template/theme coverage; complete terrain/feature/manifest repeat equality; budgets; resolved snapshot mutations; and actual movement/roll/melee/projectile collision beside new structures. Canonical progression/AI/actor-family clearance, recent gameplay fixes, quests, inventory, combat status and invulnerability tests remain.

All three initial acf01e05 workflows passed before final guard/light refinements. Those historical results and the initial APK are not claimed as final acceptance. Final artifact links and source metadata follow below. The later evidence commit changes no runtime/test/workflow source.

## Visual evidence


Actual production Godot frames are captured under `docs/landmark-screenshots/` by LandmarkVisualChecks, using real TerrainView, Part 2 atlases, PPC actors, normal camera and ordinary Y sorting. Five named landmark captures, canonical-seed room captures, coherent adjacency, physical crossing, column front/back and live combat are produced for desktop and mobile layouts. Individual preview fixtures are explicitly separate from natural-generation acceptance.

| Production scene | Desktop | Mobile layout |
| --- | --- | --- |
| Ruined Entrance / weathered | [Frame](landmark-screenshots/ruined-entrance.png) | [Frame](landmark-screenshots/mobile-ruined-entrance.png) |
| Forgotten Archive | [Frame](landmark-screenshots/forgotten-archive.png) | [Frame](landmark-screenshots/mobile-forgotten-archive.png) |
| Abandoned Shrine / overgrown | [Frame](landmark-screenshots/abandoned-shrine.png) | [Frame](landmark-screenshots/mobile-abandoned-shrine.png) |
| Collapsed Storeroom | [Frame](landmark-screenshots/collapsed-storeroom.png) | [Frame](landmark-screenshots/mobile-collapsed-storeroom.png) |
| Relic Sanctuary / ceremonial | [Frame](landmark-screenshots/relic-sanctuary.png) | [Frame](landmark-screenshots/mobile-relic-sanctuary.png) |
| Coherent adjacent rooms | [Frame](landmark-screenshots/adjacent-rooms.png) | [Frame](landmark-screenshots/mobile-adjacent-rooms.png) |
| Actual architecture crossing | [Frame](landmark-screenshots/architecture-crossing.png) | [Frame](landmark-screenshots/mobile-architecture-crossing.png) |
| Behind structural column | [Frame](landmark-screenshots/column-behind.png) | [Frame](landmark-screenshots/mobile-column-behind.png) |
| In front of structural column | [Frame](landmark-screenshots/column-front.png) | [Frame](landmark-screenshots/mobile-column-front.png) |
| Live production archetypes | [Frame](landmark-screenshots/live-combat.png) | [Frame](landmark-screenshots/mobile-live-combat.png) |

[Contact sheet](landmark-screenshots/contact-sheet.jpg). Canonical-seed captures of every composed room are in the native CI artifact. The normal-zoom frames were inspected: shelving reads as records storage, irregular open containers/rubble differentiate storage, altar/moss and mosaics distinguish sacred rooms, the portal remains clear, and ceremonial columns frame the existing relic. Pillars/props are grounded and the player and live archetypes remain readable. Room silhouettes and motifs matter more than subtle tint; shared boundary materials remain continuous. Map-edge landmarks can sit near the HUD under the existing confined camera; these captures do not change zoom or camera policy to hide that limitation.

## Performance


The existing environment fixture is used for a same-host pre-Part3/current comparison; observations are software-renderer results, not Galaxy S25 FPS. Generation/selection/structural-validation timing, mesh construction, frame intervals, node/texture/buffer counts and six transition observations are recorded by existing and new fixtures. No performance improvement is claimed from historical workers.

Three alternating same-host baseline/current runs use the existing `--environment-only --mobile-ui` fixture, 1280×800 at unchanged normal zoom, Compatibility + llvmpipe. Each process contributes the median of three equivalent dungeon entries, then the table takes the median across the three paired processes. Native compilation/testing was not run concurrently with these measurements; hosted workflows run elsewhere.

| Local observation | a080e538 | 488174f |
| --- | ---: | ---: |
| Static room p50 interval, ms | 10.978 | 11.876 |
| Static room p95 interval, ms | 12.915 | 15.695 |
| Combined terrain/wall preparation, ms | 1.588 | 1.989 |
| Synchronous view rebuild, ms | 4.183 | 4.164 |
| Actor-layer nodes / decoration nodes | 59 / 5 | 114 / 60 |
| Engine-wide texture memory, bytes | 10,457,689 | 10,857,041 |
| Engine-wide buffer memory, bytes | 6,934,092 | 6,961,760 |

Paired p95 increases **2.779 ms (21.52%)** in this short software fixture; p50 increases about 0.898 ms. This is an observed cost, not a physical-device FPS result. The almost equal rebuild times are noisy and are not an optimization claim. [Paired summary and method](landmark-evidence/paired-summary.json); all six raw process samples are beside it. Equal-region texture/buffer/node counts repeat exactly across each process's transitions. Additional engine texture allocation differs from raw atlas payload because driver allocation, alignment and PPC resources are included. Three shared atlases and 96 decoration nodes remain hard bounds; ground marks add one static mesh, not nodes per tile.

Warm generation samples (five per seed, baseline a080e538 and final 488174f on this host in separate integration runs; timing differences include shared-host variation):

| Seed | Baseline generation, ms | Part3 generation, ms | Composition, ms | Selection, ms | Structural sweep validation, ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 5.289 | 2.763 | 0.513 | 0.032 | 0.309 |
| 42 | 3.922 | 5.059 | 0.640 | 0.042 | 0.377 |
| 1337 | 13.745 | 15.347 | 0.679 | 0.051 | 0.270 |

[Baseline generation](landmark-evidence/baseline-generation.json) / [Part3 generation](landmark-evidence/part3-generation.json). These separately scheduled generation samples do not isolate a speedup/regression; composition's own timers quantify its work directly. Region build and frame/resource observations also appear in [desktop](landmark-evidence/local-desktop.json) / [mobile](landmark-evidence/local-mobile.json). Atlas build timing includes CPU rasterization and texture-upload submission, not GPU completion. View-build timing excludes deferred first draw; readiness includes frame scheduling. Repeated transitions show bounded cleanup, not proof of long-session device stability.


The first implementation measurements are retained under `landmark-evidence/initial-acf01/`; they are not substituted for final-source results. Their smaller p95 delta illustrates shared-host sampling variation. The final paired observation above is the reported cost.

## Android delivery

Final ARM64 development APK: **Mosslight-android-arm64-debug-488174f7a083.apk**, source **488174f7a0835584cf5059cb65e9e9b3c87be37e**. Package `com.cigthepig.mosslight.dev`; Godot 4.6.1 .NET, SDK 9.0.203/net9.0. Size **104,484,374 bytes**; SHA-256 **d8dc44e1d2b8df44f45365065c8adfc0b5e86e0655f8be796229c8ecde77f16a**. [Full package metadata](landmark-evidence/android-package.json) records packaged resources, ABI and signature verification.

[Direct ARM64 APK](https://chatgpt.com/library?file_id=libfile_e7b47ea807988191be1faab2acb1ee6e&account_id=personal) / [GitHub artifact ZIP](https://github.com/CigThePig/RPG-test/actions/runs/41036934785/artifacts/12968089588). Install the ARM64 APK; the x86_64 package is only for emulator verification.

All export presets explicitly include `game/data/landmarks/*.json`; package verification requires this resource. Installed smoke retains ordinary touch/chest/inventory/damage/spell/projectile/lifecycle checks, verifies the third shared atlas, requires generated entrance/sanctuary/structural metadata, measures actual dungeon joystick displacement and clear actor footprints, performs Attack/Dodge, returns/re-enters three times, and checks stable wall/decoration/atlas counts. Final installed-runtime [result](landmark-evidence/android-result.txt), [region counts](landmark-evidence/android-regions.json) and [lifecycle evidence](landmark-evidence/android-lifecycle.json) pass. Each ruin visit retains 55 wall rows, 53 decoration nodes and three atlases; Reach returns retain 0/43/3. All three lifecycle resumes preserve the process identity and cancel active input.

The build uses an ephemeral debug signature. Installing over a differently signed build can require uninstalling, which deletes Android private saves. Preserve valuable progress before replacing an installed development build. Start a new game after installing a build with a new region generator; earlier saves are rejected.

This environment has no attached physical S25. The installed smoke is a hosted x86_64 emulator test, not an ARM64 physical-device performance claim. A complete installed-Android key/gate/relic expedition is not automated here; the protected desktop/mobile-layout production-touch movie covers that path, while real combat is verified separately. Intermediate mutation-state restore checks cover metadata/terrain persistence separately from actual interaction progression. No ordinary user save slot is read or written by fixtures.

## Part 4 integration contract

Use region ID + layout cell X/Y + template ID/revision for stable identity; place by frame origin plus local focal. The saved focal and roles are content hooks, not a second mission system. A focal may be a solid altar: obtain nearby reachable interaction/encounter stands through existing FindPlacement/Path with real radii/occupancy. Use existing World events, QuestSystem, Gloot, Feature state, encounter profiles and interaction handling for quest destinations, rewards, loot and narrative context. Preserve room-core-center mission-feature indices; no duplicated relic/portal/key authority. Enemies and rewards must not be emitted from cosmetic anchors. Resolved anchors/theme remain stable on revisits/save/load. Optional scatter may change presentation density without altering structure or gameplay. Part4 retains adventure pacing, quest integration, reward loops and final PR readiness review.

## Final artifacts and visual inspection

- [Final native screenshots, seed captures and reports](https://github.com/CigThePig/RPG-test/actions/runs/41036935137/artifacts/12968712647)
- [Installed Android recording, screenshots and logs](https://github.com/CigThePig/RPG-test/actions/runs/41036934785/artifacts/12969105865)
- [Baseline gameplay recording and repeat](https://github.com/CigThePig/RPG-test/actions/runs/41036934915/artifacts/12968194184)
- [Precise gameplay and dungeon expedition recordings/repeats](https://github.com/CigThePig/RPG-test/actions/runs/41036934915/artifacts/12969945166)

Final expedition state digest: `0349224f8354c82bb95f2b52b9dbd61ac1f7cd93d66f846d6cb879a57a593021`, independently repeated. [Baseline report](landmark-evidence/ci-replay-baseline.json), [precise report](landmark-evidence/ci-replay-precise.json), [expedition report](landmark-evidence/ci-replay-dungeon-expedition.json). Replay reports mark their checkout dirty solely for Godot-generated untracked UID/import sidecars; the tested tracked source is the stated commit. Movie Maker wall pacing is capture cost, not gameplay FPS.

Raw final native observations are [desktop](landmark-evidence/ci-desktop.json), [mobile](landmark-evidence/ci-mobile.json), [generation](landmark-evidence/ci-generation.json) and [integration](landmark-evidence/ci-verification.json). The committed normal-zoom gallery is drawn from the final native artifact. Earlier Part1/Part2 screenshots and evidence remain unchanged. CI software frame timings belong to a separate allocation and are not mixed with the same-host paired table as evidence of speedup.
