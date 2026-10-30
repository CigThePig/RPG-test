# PR5 Part 1 — Dungeon scale and traversal

Latest wall-depth correction: **`59fc7ec3c9c9db97a7cecfda177cc247bc31257e`**. [Download the corrected ARM64 APK ZIP](https://github.com/CigThePig/RPG-test/actions/runs/40998033592/artifacts/12951683875). The wall-depth follow-up at the end records its verification; earlier Part 1 measurements and APKs below are historical baselines.

## Implemented spatial foundation

Base: merged PR4 `9eace0f1db3e4f2c7e5cc94cd2474eaa612cfe1b`, verified as main at checkout. Branch: `feat/dungeon-exploration`. Part 1 only; do not merge this draft PR.

The old adapter used 7×7 cells, 5×5 interiors and one-tile (24-unit) openings. Circular actor collision could technically fit while large PPC silhouettes and combat movement felt cramped. The upstream mission graph was not the cause.

Final geometry exactly matches the starting target: 11×11 cells, 9×9 interiors (216×216 units), three-tile passages (72 units), center coordinate 5, one-tile enclosing boundary and unchanged 24-unit tiles. Each valid reciprocal cardinal graph edge carves three parallel floor lanes from its center through its own boundary. Adjacent cells carve the other half. Unoccupied cells, corners and unconnected boundaries remain blocked. No RNG consumption, mission grammar or logical topology changes are introduced.

The target leaves considerably more interior and lateral space at modest absolute map sizes; no further enlargement was justified by footprint checks or the inspected normal-zoom renders.

## Locks, interaction and mission integrity

Each generated gate is one isolated cardinally connected component, normally a six-cell 2×3 rectangle spanning both boundary rows. Sorted `RegionState.GateGroups()` is shared by runtime, validator and presentation. Different generated gates are separated by solid corners. Runtime consumes one key and opens the entire group, including far cells the old immediate-neighbour unlock missed. Repeated interaction cannot spend a second key on that opened group.

Movement, sight and projectiles still consult each actual lock cell. One grouped FeatureView draws the connected bars and a single lock plate; nearest-cell interaction selects the whole visual and opening updates it. This is a modest blocking-state presentation, not the Part 2 environment pass.

The bounded mission solver retains its search and now exposes a deterministic gate-plan witness. It respects opened groups and depleted keys, with an optional starting key count. Alternate graph routes remain valid: seed 42 can reach its objective without spending a key, whereas seeds 1 and 1337 exercise required unlocking.

## Actor, feature and spawn clearance

Anatomy-derived collision radii remain 6–11 units. Canonical checks use the actual player, an ordinary biped NPC and production archetype families (quadruped prowler, reptile brute, amorphous spitter). Largest tested diameter is 22 against 72 nominal passage width; no radius/speed/timing changes compensate for geometry.

Opened-map footprint floods reach every feature and gate, covering horizontal/vertical entries, exits and return paths. Interior corner sweeps retain clearance. Production movement/interaction walks acquire keys, open required gates, collect the relic and use the exit. Generated-room tests use the real 90-unit roll, wall-stopped roll, approved sword lunge, full-width closed gate and production projectile sweep.

Mission features stay at room centers. Existing interaction range and visibility remain unchanged. Arrival now uses the existing deterministic `FindPlacement` on entry to the ruin, preferring a point two tiles beside and one tile below the return marker. This avoids both physical crowding and spawning behind the tall exit artwork; surviving inhabitants are excluded by their real footprints. Restoring a saved snapshot never takes this entry path or relocates its player. Eight seeded inhabitants retain their profiles/counts. Production FindPlacement still uses actual footprints, reachable floor, 40-unit feature exclusion and radius-based actor separation; canonical checks verify these and entrance safety. No encounter rebalance, alternate navigation system or spawn retry loop was added.

## Persistence

Schema 1 and `rpg-regions-1` are unchanged. Previously generated ruins retain their complete serialized tiles, dimensions, indices, locks, depleted features, actors, loot, inventory/equipment and quests. They remain narrow intentionally. Saves without a generated ruin keep their progress and create the expanded ruin on first entry. New ruins restore exactly and continue deterministically.

The old executable produced `game/tests/fixtures/pr4-dungeon-save.json` before generator edits. It includes collected key/opened lock/depleted chest, a dead enemy and survivors, equipment/inventory and quest progress. Capture provenance and the synthetic chest detail are documented in the fixture README. Exact roundtrip and continued-save tests pass without writing user slots. No production persistence changes were required.

## Verification and exact source

Local build uses SDK 9.0.203, Godot 4.6.1 .NET and .NET 8.0.15 host. `dotnet build` passes with the existing 58 nullable warnings. `tools/import-project.py` succeeds; the existing editor-shutdown EditorSettings diagnostic remains confined to import and runtime verification rejects engine errors.

- `python3 tools/verify.py --headless`: **4,521 assertions passed**.
- `python3 tools/verify.py --visual`: passed; actual viewport images inspected.
- `python3 tools/verify.py --visual --mobile`: passed; actual mobile-layout images inspected.
- Geometry seeds: **0–63 plus 1337** (includes 1 and 42), repeated generation, exact PR4 logical topology/mission-role comparison, bounds, cross-sections, determinism, solvability and physical player routes. All five representative footprints on 1/42/1337. Failed seeds raise with dimensions/features/groups and reason.
- Required-gate runtime progression: seeds 1 and 1337; seed 42 validates its legitimate alternate route. No failed seed is suppressed.
- Local baseline and precise: all 45 production scenarios and independent authoritative repeat digests passed. The precise local MP4 was incomplete during post-processing; re-encoding the retained original AVI and rerunning artifact checks succeeded. No gameplay checks were loosened. Focused seed-1 touch expedition movie, independent rendered repeat and matching authoritative digest also passed, and contact sheets were inspected. Final exact-source CI results are recorded below.
- Live generated-doorway encounter checks exercise both cardinal orientations: Prowler crosses and bites, Brute repositions and commits its cone, Spitter maintains range and releases a projectile. These use production seeded inhabitants, World AI and 480 simulation steps per observation.
- **Final tested runtime:** `c949175fd6e6144bb3d04a52824d647e26890d9e`. Native CI and ARM64 package build/validation have succeeded; final emulator and movie workflow status will be appended below.

## Actual rendered evidence

All comparisons use logical seed 42, 1.5× gameplay zoom and the actual viewport. Old 28×21 terrain (588 tiles) comes from the original generator fixture; new 44×33 terrain (1,452 tiles) comes from production Regions.Dungeon. Both use the current terrain/character renderer, so the comparison isolates geometry. Old gate art in this comparison uses the current grouped rendering rather than the historical lock glyph.

| Scenario | Desktop | Mobile layout |
|---|---|---|
| Old room | [Before](native-screenshots/dungeon-before-room.png) | [Before](native-screenshots/mobile-dungeon-before-room.png) |
| New room | [After](native-screenshots/dungeon-after-room.png) | [After](native-screenshots/mobile-dungeon-after-room.png) |
| Old doorway | [Before](native-screenshots/dungeon-before-doorway.png) | [Before](native-screenshots/mobile-dungeon-before-doorway.png) |
| New doorway | [After](native-screenshots/dungeon-after-doorway.png) | [After](native-screenshots/mobile-dungeon-after-doorway.png) |
| Adjacent room | [View](native-screenshots/dungeon-adjacent-room.png) | [View](native-screenshots/mobile-dungeon-adjacent-room.png) |
| Closed gate | [View](native-screenshots/dungeon-gate-closed.png) | [View](native-screenshots/mobile-dungeon-gate-closed.png) |
| Open gate | [View](native-screenshots/dungeon-gate-open.png) | [View](native-screenshots/mobile-dungeon-gate-open.png) |
| Enemy doorway | [View](native-screenshots/dungeon-enemy-doorway.png) | [View](native-screenshots/mobile-dungeon-enemy-doorway.png) |
| Mixed room | [View](native-screenshots/dungeon-mixed-room.png) | [View](native-screenshots/mobile-dungeon-mixed-room.png) |

[Side-by-side mobile doorway comparison](native-screenshots/dungeon-comparison.png). [Actual corrected ruin arrival](native-screenshots/mobile-dungeon-arrival.png).

The player has visible breathing room at the opening centerline. Some silhouette/wall depth overlap near walls remains possible under the existing projection; collision authority and visual sprite bounds are distinct. The frozen mixed/enemy screenshots use production dungeon inhabitants and establish composition/placement. Separate live generated-doorway integration checks verify tactical behaviour.

## Performance and memory

Raw data: [baseline](dungeon-evidence/baseline.json), [expanded](dungeon-evidence/expanded.json), [desktop render](dungeon-evidence/render-desktop.json), [mobile layout render](dungeon-evidence/render-mobile.json). Five warm-run samples per canonical seed; one initial warm-up precedes measurement in each process. Values are local host observations, not controlled hardware benchmarks. Processes and neighbouring test workloads differ, so small speedups are noise, not optimization claims.

The cell tile budget rises by 121/49 ≈ 2.47×. Exact tile arrays use four bytes per tile, excluding dictionary/actor/managed overhead. Recorded generation allocation is cumulative allocated bytes (including upstream graph work), not retained region memory.

| Seed | Old → new dimensions | Tiles | Floor tiles | Tile bytes | Generation median ms | Validation median ms | Path median ms | Placement median ms |
|---|---|---|---|---|---|---|---|---|
| 1 | 28×28 → 44×44 | 784 → 1936 | 241 → 777 | 3,136 → 7,744 | 4.559 → 5.671 | 0.097 → 0.246 | 0.424 → 1.589 | 0.482 → 1.882 |
| 42 | 28×21 → 44×33 | 588 → 1452 | 297 → 957 | 2,352 → 5,808 | 7.704 → 3.762 | 0.073 → 0.088 | 0.241 → 0.252 | 0.566 → 0.737 |
| 1337 | 42×28 → 66×44 | 1176 → 2904 | 241 → 777 | 4,704 → 11,616 | 16.936 → 13.741 | 0.092 → 0.175 | 0.403 → 0.410 | 0.491 → 0.888 |

Seed-42 terrain command construction: desktop **0.708 → 3.308 ms**, mobile-layout **0.731 → 1.802 ms**. Static-room 90-frame desktop p50/p95: **7.99/13.12 → 11.83/18.53 ms**; mobile layout **7.88/15.11 → 10.79/17.58 ms**. Both use llvmpipe, a frozen no-enemy room and the same process/zoom; legacy runs first, and these samples do not establish live combat performance or sustained FPS. Terrain construction is measured inside `_Draw`, not GPU upload/completion. Complete dungeon-entry wall-clock cost, retained region heap size and sustained ARM64 GPU/frame measurements remain unavailable.

Final-source hosted native measurements are retained separately in [CI expanded](dungeon-evidence/ci-expanded.json), [CI desktop](dungeon-evidence/ci-render-desktop.json) and [CI mobile layout](dungeon-evidence/ci-render-mobile.json). These must not be mixed with the local old-generator generation baseline. In the same CI rendering process, old/new terrain construction was **0.832 → 1.487 ms desktop** and **1.113 → 1.696 ms mobile layout**. Static-room p95 frame intervals were **13.53 → 20.92 ms desktop** and **14.07 → 21.34 ms mobile layout**. This is a measurable software-renderer regression, not a 60-FPS result. The expanded absolute tile counts remain below the existing 64×48 overworld, but real Android entry/live encounter timing still needs device profiling. Final committed screenshots come from the exact `c949175fd6e6` native CI artifact, downloaded and visually inspected.

## Android and phone acceptance

The established GitHub Actions Android pipeline exported ARM64 and its matching x86_64 emulator package and passed source/package/resources/signature validation. Native CI independently passed the full build/import/4,521-assertion/desktop/mobile suites. Installed Android emulator verification also passed: launch/rendering, measured OS-touch movement, chest/inventory, four Attack and three Dodge touches, real damage, all three archetypes/Spit, and three nonblank same-process mixed-combat resumes with canceled input. The final Android screenshots/contact sheet were downloaded and inspected. This smoke covers ordinary gameplay and the tactical fixture; it does not time a complete generated-dungeon expedition on Android. Exact-source movie results are recorded below. Desktop/mobile-layout rendering alone is not Android runtime evidence.

For the phone: use **Menu → New game** to start a fresh world (do not load a slot containing a generated ruin), then enter the ruin from the overworld portal. If retaining progress, load a save from before its first ruin visit. Existing generated dungeons intentionally retain their original layout. Back up valuable saves before replacing a development build: the established ephemeral debug signature can require uninstalling the prior APK, deleting its private data.

Physical S25 acceptance remains **pending user playtest**: install the ARM64 APK, inspect spacious rooms/openings and silhouette clearance, walk through doors in both directions, approach enemies, dodge/reposition, collect keys/chests/relic, unlock gates, return to Reach and save/load. Check for invisible walls, entry stalls, responsiveness, subjective touch comfort, audio and sustained performance during a normal session. No claim of physical-device acceptance or 60 FPS is made.

## Limitations and Part 2 integration

Part 1 changes space, not environmental artistry. Floors and walls remain visually repetitive; wall sprites can still overlap silhouettes at contact. Gate bars are intentionally simple and cover their actual blocking footprint; Part 2 can improve depth, orientation and art without changing key authority. Terrain construction/frame cost increased, so retain batched terrain and profile representative live Android encounters before adding detail. No per-tile nodes, streaming system, alternate physics/navigation or handcrafted-template framework was introduced.

Later authored rooms must preserve reciprocal boundary semantics and sufficient practical/visual clearance. Connected lock cells mean one logical gate; independent gates must stay disconnected or receive explicit compatible IDs later. Keep feature interaction stands, entrance separation, eight production inhabitants, collision radii, deterministic RNG ordering and saved region authority. Do not regenerate old ruins to apply future templates.

Parts 2–4 remain unimplemented. The draft PR must remain unmerged.

## Original Part 1 package identity

ARM64 APK source: `c949175fd6e6144bb3d04a52824d647e26890d9e`. APK SHA-256: `b0f5d1c0fca4e0cb43efddee4bf7e13f7879de78966fe4f0e7044968554a0b2d`. Signing mode reported by package validation: `ephemeral-debug-key-uninstall-required-between-runs`. Preserve/export valuable progress before uninstalling a differently signed prior build; format compatibility does not preserve Android private storage across uninstall.

## Original Part 1 CI and downloads (before wall-depth correction)

The original Part 1 verification and APK in this section use **`c949175fd6e6144bb3d04a52824d647e26890d9e`**; the corrected wall-depth runtime and replacement APK are recorded below. The final handoff/screenshot update changes documentation and evidence only; it does not change that tested runtime.

| Verification | Final result | Evidence |
|---|---|---|
| Build, import, grammar packaging, production integration | Passed, 4,521 assertions | [Native CI](https://github.com/CigThePig/RPG-test/actions/runs/40950726079) |
| Desktop and mobile-layout rendered suites | Both passed; exact-source screenshots inspected | [Native artifact](https://github.com/CigThePig/RPG-test/actions/runs/40950726079/artifacts/12930358763) |
| Preserved reference generation/tests/build | Passed | Same native workflow |
| Baseline touch movie + independent rendered repeat | All 45 production scenarios passed | [Baseline artifact](https://github.com/CigThePig/RPG-test/actions/runs/40950726085/artifacts/12930769074) |
| Precise touch movie + independent rendered repeat | All 45 production scenarios passed | [Precise artifact](https://github.com/CigThePig/RPG-test/actions/runs/40950726085/artifacts/12930494945) |
| Focused dungeon expedition movie + independent repeat | Passed, identical authoritative digest; seed 1, real Dodge/Interact/joystick, key/gate/relic/return | Included in precise artifact, `dungeon-expedition/`; [inspected frame sheet](native-screenshots/dungeon-expedition-contact-sheet.jpg) |
| ARM64 export, SDK/ABI/runtime/resources/source/signature | Passed | [Android workflow](https://github.com/CigThePig/RPG-test/actions/runs/40950726160) |
| Installed x86_64 Android gameplay and three mixed-combat lifecycle cycles | Passed; actual captures inspected | [Android runtime evidence](https://github.com/CigThePig/RPG-test/actions/runs/40950726160/artifacts/12930479375) |
| Physical Galaxy S25 ruin comfort, audio and sustained performance | **Not performed: no physical device connected to this environment** | User playtest required |

**Download:** [Verified ARM64 development APK ZIP — c949175fd6e6](https://github.com/CigThePig/RPG-test/actions/runs/40950726160/artifacts/12932066317). Extract and install the `.apk`, not the x86_64 emulator package. The artifact also includes metadata/checksums and is retained until November 28, 2026.

[Draft PR #5](https://github.com/CigThePig/RPG-test/pull/5) targets main on `feat/dungeon-exploration`. Parts 2–4 remain future work; do not merge. No outstanding inline review threads were present at the final review check.

## Follow-up: wall depth sorting from phone feedback

The phone screenshot revealed a real draw-order defect: all raised wall geometry was in the background terrain mesh at Z=-10, so every character painted over it. The spacious geometry did not correct this. Raised wall rows now share the existing actor/feature Y-sort layer, anchored at their front ground edge. Opaque foreground walls cover the lower character silhouette; actors standing in front cover the wall. Doorway gaps contain no occluding geometry. Floors stay in their background batch, and natural rocks retain their existing rendering.

There is one mesh/node per wall-containing row (33 in the seed-42 fixture), never one per tile. No geometry, collision, movement, combat or saved-state rules change. Both newly generated and previously saved ruins use corrected depth rendering without regenerating their layouts.

The full headless suite still passes 4,521 assertions. Desktop and mobile rendered suites now also pass actual pixel checks: 694 formerly overlapping pixels correctly covered by foreground wall, 1,140 visible upper-silhouette pixels, 1,446 pixels establishing front-side visibility, and zero pixel differences across a clear doorway. These counts describe this fixture, not all possible silhouettes. The wall-only/background references and old-order reproduction use the real viewport. The fix is actor-agnostic because all CharacterViews share the same ground-depth container.

The corrected runtime is `59fc7ec3c9c9db97a7cecfda177cc247bc31257e`. Earlier APK c949175fd6e6 and its evidence above remain the historical Part 1 baseline and do not contain this correction. Latest verification and replacement download links are recorded below.

### Corrected wall-depth evidence

[Before/behind/in-front viewport crops](native-screenshots/wall-depth-comparison.png), enlarged 2× with nearest-neighbour scaling. The left panel reproduces the old depth order in the same scene. Full normal-zoom captures:

| Position | Desktop | Mobile layout |
|---|---|---|
| Old-order reproduction | [View](native-screenshots/wall-depth-old-order-reproduction.png) | [View](native-screenshots/mobile-wall-depth-old-order-reproduction.png) |
| Behind foreground wall | [View](native-screenshots/wall-depth-behind.png) | [View](native-screenshots/mobile-wall-depth-behind.png) |
| In front of wall | [View](native-screenshots/wall-depth-in-front.png) | [View](native-screenshots/mobile-wall-depth-in-front.png) |
| Clear doorway | [View](native-screenshots/wall-depth-doorway.png) | [View](native-screenshots/mobile-wall-depth-doorway.png) |

These images were downloaded from the exact-source native workflow and inspected. Current renderer measurements are [desktop](dungeon-evidence/wall-depth-render-desktop.json) and [mobile layout](dungeon-evidence/wall-depth-render-mobile.json). For the expanded seed-42 map, combined floor + wall-row mesh construction measured **2.406 ms desktop / 2.384 ms mobile layout**; static-room p95 intervals were **18.408 / 18.341 ms** on CI software rendering. The wall-row portion was 1.656 / 1.744 ms. These include deferred row mesh construction; they do not measure full region-entry latency, and comparison with earlier runs on other hosts is not a controlled speedup claim. Physical S25 performance remains unverified.

### Wall-depth correction: final verification and replacement APK

All correction results below use **`59fc7ec3c9c9db97a7cecfda177cc247bc31257e`**. Final screenshots/documentation are a separate evidence-only commit.

| Check | Result | Link |
|---|---|---|
| Build/import and integration | Passed, 4,521 assertions | [Native CI](https://github.com/CigThePig/RPG-test/actions/runs/40998033597) |
| Desktop/mobile viewport suites | Both passed, including foreground/front-side/doorway pixel checks | [Exact-source native evidence](https://github.com/CigThePig/RPG-test/actions/runs/40998033597/artifacts/12952248049) |
| Baseline / precise replay | 45 scenarios plus matching independent rendered repeat per preset | [Movie workflow](https://github.com/CigThePig/RPG-test/actions/runs/40998033567) |
| Focused dungeon expedition | Recorded Dodge/corner/key/gate/relic/return and deterministic repeat passed | [Precise + dungeon evidence](https://github.com/CigThePig/RPG-test/actions/runs/40998033567/artifacts/12953085390) |
| ARM64 package | Export, source, ABI, runtime, resources and signature passed | [Android workflow](https://github.com/CigThePig/RPG-test/actions/runs/40998033592) |
| Installed Android emulator | Gameplay, OS touch and three nonblank mixed-combat lifecycle cycles passed | [Android runtime evidence](https://github.com/CigThePig/RPG-test/actions/runs/40998033592/artifacts/12951769223) |

**Replacement download:** [ARM64 development APK ZIP — 59fc7ec3c9c9](https://github.com/CigThePig/RPG-test/actions/runs/40998033592/artifacts/12951683875). APK SHA-256: `c62ff056fa16e7121b4129f7a4329822ef4dff54afe1ff55dc8c0c77899fb113`. Package signing remains ephemeral debug signing. Replacing a differently signed installation can require uninstalling it and deleting private saves.

The rendering correction applies to existing saved ruins as well as new ones; a new game is not required by the fix. Saved tiles, actors, progression and collisions remain unchanged. Physical S25 wall presentation and sustained performance still need user acceptance; the emulator smoke checks normal gameplay and the tactical fixture, while wall pixel checks run in real desktop/mobile-layout viewports. No physical-device or sustained-60-FPS claim is made.

PR #5 remains draft and unmerged. This is the narrow rendering correction prompted by Part 1 phone feedback, not implementation of the broader Part 2 environment-art milestone.
