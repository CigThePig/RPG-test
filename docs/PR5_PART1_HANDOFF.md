# PR5 Part 1 — Dungeon scale and traversal

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

Mission features stay at room centers. Existing interaction range and visibility remain unchanged. Arrival now uses the existing deterministic `FindPlacement` on entry to the ruin, avoiding overlap with the return marker or surviving inhabitants. Restoring a saved snapshot never takes this entry path or relocates its player. Eight seeded inhabitants retain their profiles/counts. Production FindPlacement still uses actual footprints, reachable floor, 40-unit feature exclusion and radius-based actor separation; canonical checks verify these and entrance safety. No encounter rebalance, alternate navigation system or spawn retry loop was added.

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
- Baseline movie and independent deterministic repeat: passed. Precise replay is completing. Focused seed-1 touch expedition movie, independent rendered repeat and matching authoritative digest: passed and contact sheet inspected.
- Live generated-doorway encounter checks exercise both cardinal orientations: Prowler crosses and bites, Brute repositions and commits its cone, Spitter maintains range and releases a projectile. These use production seeded inhabitants, World AI and 480 simulation steps per observation.
- CI, tested runtime SHA and ARM64 APK: pending publication/build; no Android success claimed yet.

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

## Android and phone acceptance

The established GitHub Actions Android pipeline will export ARM64 and its matching x86_64 emulator package, validate source/package/resources/signature and run the existing installed-emulator gameplay/lifecycle checks. Exact run/artifact links will be appended after successful completion. Desktop/mobile-layout rendering is not Android runtime evidence.

For the phone: use **Menu → New game** to start a fresh world (do not load a slot containing a generated ruin), then enter the ruin from the overworld portal. If retaining progress, load a save from before its first ruin visit. Existing generated dungeons intentionally retain their original layout. Back up valuable saves before replacing a development build: the established ephemeral debug signature can require uninstalling the prior APK, deleting its private data.

Physical S25 acceptance remains **pending user playtest**: install the ARM64 APK, inspect spacious rooms/openings and silhouette clearance, walk through doors in both directions, approach enemies, dodge/reposition, collect keys/chests/relic, unlock gates, return to Reach and save/load. Check for invisible walls, entry stalls, responsiveness, subjective touch comfort, audio and sustained performance during a normal session. No claim of physical-device acceptance or 60 FPS is made.

## Limitations and Part 2 integration

Part 1 changes space, not environmental artistry. Floors and walls remain visually repetitive; wall sprites can still overlap silhouettes at contact. Gate bars are intentionally simple and cover their actual blocking footprint; Part 2 can improve depth, orientation and art without changing key authority. Terrain construction/frame cost increased, so retain batched terrain and profile representative live Android encounters before adding detail. No per-tile nodes, streaming system, alternate physics/navigation or handcrafted-template framework was introduced.

Later authored rooms must preserve reciprocal boundary semantics and sufficient practical/visual clearance. Connected lock cells mean one logical gate; independent gates must stay disconnected or receive explicit compatible IDs later. Keep feature interaction stands, entrance separation, eight production inhabitants, collision radii, deterministic RNG ordering and saved region authority. Do not regenerate old ruins to apply future templates.

Parts 2–4 remain unimplemented. The draft PR must remain unmerged.
