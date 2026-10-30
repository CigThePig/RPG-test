# PR #5 Part 4 — The Buried Light

## Executive summary and authority

Parts 1–3 and the variable-room overhaul supply the physical dungeon, authored landmarks and PPC-compatible artwork. Part 4 connects that foundation to an ordered QuestSystem adventure, contextual warden dialogue, transactional Gloot rewards, persistent exploration and bounded room-aware encounters. PR #5 remains draft and must not be merged before physical Galaxy S25 acceptance.

Mission graph, `DungeonLayout`, saved landmark manifests, `World`, QuestSystem, Gloot and presentation retain their existing ownership. There is no replacement inventory, quest engine, combat system or room generator. Historical Part 1–3 evidence remains historical; `DUNGEON_LAYOUT.md` supersedes their fixed-cell geometry.

## Adventure and quest stages

The existing introductory assignment still requires two player-attributed beast defeats; historical defeats reconcile at acceptance. It teaches combat before The Buried Light becomes available. Warden dialogue hides duplicate acceptance and unavailable hand-ins.

The Buried Light has stable authored stage identifiers:

1. `enter_ruin`: player `region_entered`, value `ruin`, empty target.
2. `recover_relic`: player `resource_depleted`, target `ruin`, value `relic` (only after successful production inventory acquisition).
3. `return_reach`: player `region_entered`, value `reach`, empty target.
4. `report_warden`: deliberate contextual dialogue, player `npc_report`, target `warden`, value `reach`.

Assignment acceptance belongs to the QuestSystem active pool. Successful report moves the quest to its completed pool; an unrewarded completed quest is explicitly pending. `stage_id` accompanies the ordered cursor in serialized QuestSystem properties. Definition matchers are loaded from current authored content rather than overwritten by obsolete serialized definitions. Historical completed quests stay completed and retain their reward flags.

Current possession at acceptance reconciles recovery and, if already home, return. Reporting is never retroactive. Generic item-acquired strings cannot complete this adventure. The relic is retained as a keepsake; it is not consumed during hand-in. A full inventory therefore cannot destroy the objective.

The journal reads the QuestSystem adapter and lists current/completed stages, pending rewards, actual discoveries and recorded lore. Notifications use the existing UI. No permanent combat control was added.

## Reward transaction

The former fire-and-forget reward signal was removed. `Narrative.claim_reward` validates eligibility and uses a synchronous C# callable. `Items.GrantReward` delegates to Gloot `reward_add`: validate prototype/count and capacity for the whole delivery, snapshot the bridge, deliver, and restore the exact bridge snapshot on an unexpected delivery failure. No item event is emitted on failure. Only success commits `rewarded`, then emits reputation once. A reentrancy guard covers item-acquired callbacks. Autosaves cannot interleave inside this synchronous transaction.

Completion and delivery are separate durable facts. Failure leaves a visible pending claim, retried through the warden; no equipment, stack or reputation is fabricated on retry. The normal inventory remains Gloot's only store. Ordinary collection keeps existing full-capacity/depletion semantics.

## Discovery and optional content

`Exploration.Discover` uses player ground tiles, saved `DungeonRoom.Contains` rectangles and saved manifests. A corridor in the same 17×17 block does not qualify. Stable identity is `region:x,y:template@revision:rotation`; it never depends on node IDs or the current template library. `DiscoveredLandmarks` and bounded explored map tiles are saved in RegionState. The event is `landmark_discovered`, player actor, stable identity target, saved template value. Render rebuilds emit nothing.

Archive and shrine discoveries record the light's history and purpose in the journal. The storeroom points toward expedition supplies. A fresh dungeon attempts one optional supply chest in an actually resolved storeroom/archive/shrine, prioritizing the storeroom. Selection uses a separate stable hash, valid floor and feature clearance; it never moves mission features or uses cosmetic anchor rendering as gameplay authority. Existing Gloot chest ownership, contents, transfer, partial-capacity retention and depletion apply. If optional landmarks are absent or fall back, no optional requirement blocks the main quest.

Map terrain is revealed near visited tiles, with discovered landmark markers, the entrance and player. Hidden optional landmarks are not marked. The map is drawn only as an open panel.

## Room-aware encounters

Population remains eight using existing prowler, brute and spitter profiles. A dedicated integer-hash selection stream consumes no mission RNG. Stable IDs remain `ruin:spawn:0..7`. Placement excludes the entrance room, a 240-unit entrance safety radius, hallways, mission/cache features (48-unit clearance), illegal footprints and actor overlaps. Rooms below 100 floor tiles admit one spawn; larger rooms admit two. Brutes prefer large rooms; spitters require three useful 48-unit local movement lanes. Other archetypes require two 24-unit lanes. Slot zero prioritizes the sanctuary and uses the existing brute as guardian. Unsuitable placements are skipped rather than breaking region entry. Seed sweeps require the intended budget.

`EncountersInitialized` is explicit. Re-entry never moves saved survivors or recreates an empty/defeated population. Actor corpses and loot remain the existing authoritative objects. `ExplorationInitialized` prevents optional cache initialization on historical regions. No AI, damage, collision, projectile or animation tuning was used to force the adventure test to pass.

## Save compatibility

Generator identity remains `rpg-regions-2`, schema 1. Old generator saves still require a new game and are not deleted. Manual save refuses to overwrite an incompatible-generator slot; choose another slot. Before replacing an incompatible or malformed background recovery file, autosave preserves its exact bytes under a unique `.retained-N.json` sibling; previous retained files are never overwritten. Current-generation recovery files keep the normal single autosave path. No risky geographical migration is attempted.

Missing Part 4 fields in current-generation saves default to initialized encounters/exploration, empty discovery and map history. This preserves existing actors, terrain, opened gates, relic, containers and loot. It does not grant retroactive discovery bonuses. Entering a room later records its discovery normally; discovery itself does not grant items. After unified restoration, Narrative reconciles active stages against the restored player region and Gloot possession without emitting rewards or replaying discovery events. This prevents an older active quest saved inside the ruin from waiting forever for an entry event that already happened. Active old adventure objectives use current definitions; completed old adventures retain their historical completion/reward status. New fields participate in the existing unified snapshot/rollback and background save.

## Verification and evidence

Initial branch: `a29784389169ac954fb8850f32b8ffe725760675`. A separate detached checkout supplied the pre-Part-4 comparison; it is not a new development branch.

Local .NET 9.0.203 build succeeds (58 existing third-party nullable warnings, zero errors). The expanded production-engine headless suite passed **7,915 assertions** after the autosave/dialogue correction and relic/corpse persistence additions. It includes seeds 0–63 plus 1337, repeated encounter-stream placement, actual-room occupancy, density/feature clearance, unchanged survivors, empty-population revisit, discovery/corridor distinction, old-field defaults, full/partially available reward capacity, pending reload/retry and exactly-once reputation. Existing geometry, landmarks, combat, movement, saves and quest checks remain enabled. The first published source, `a2f7072`, failed its autosave regression check because the new verification safeguard also suppressed the existing autosave fixture. The correction scopes suppression to adventure fixtures only; the local full suite then passed. Render inspection also found disabled dialogue rows overflowing the mobile viewport; unavailable responses are now omitted. Final exact-source CI status is authoritative and must be checked, not inferred from these earlier local runs.

Commands (use the pinned engine through `GODOT` if not on PATH):

```sh
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
godot --headless --path . -- --adventure-check
xvfb-run -a python3 tools/verify.py --visual
xvfb-run -a python3 tools/verify.py --visual --mobile
xvfb-run -a python3 tools/gameplay-verify.py --presets baseline,precise
xvfb-run -a python3 tools/adventure-verify.py
godot --headless --path . -- --validate-landmarks
godot --headless --path . -- --dump-dungeons=1,42,1337
```

`AdventureChecks` uses clearly identified isolated setup for capacity/migration/empty-population assertions; those setups are not combat victories. `AdventureRun` separately executes a complete fresh production-world adventure with normal health. Its API driver physically follows paths, fights through real attacks/spells/dodges, interacts with actual chests/keys/gates/relic/portals and restores snapshots after gate opening, relic recovery and completion. It never injects a relic, sets a quest-completed flag, directly opens gates, forces damage or grants invulnerability.

The first complete API run used seed 42, original starter equipment, 100 health, 60 mana and three starter potions. It completed in 10,966 ticks, received 41 damage, used 22 melee starts, 26 spell casts and two dodges, consumed zero potions and finished at 84 health. Spell/level recovery uses existing production rules, not fixture health resets. `build/adventure/normal-health.json` records the setup and stage state; later rendered runs with the real dialogue UI also travel to the giver and can have different totals.

`--adventure-visual` continuously renders that production API driver with actual Dialogue Manager choice buttons and production journal/map panels. It records up to three simulation ticks per rendered frame for accelerated playback. It is a continuous adventure, not stitched unrelated scenes, and is **not OS-touch input evidence**. `tools/adventure-verify.py` validates completion and transcodes the real Movie Maker output. Screenshots and movie metadata live in `build/adventure/`; gameplay workflow uploads them with precise-preset evidence. Existing touch replays retain independent digest verification.

## Performance

Fresh baseline desktop viewport suite passed on the same host with Godot 4.6.1 llvmpipe. Raw baseline and current `render-verification*.json`, `build/dungeon/` reports and `seed-measurements.json` are the measurement records. Generation and entry/population timings are sampled per seed. Comparisons must use serial like-for-like runs; concurrent local compilation/rendering changes host contention. No physical-device FPS claim follows from these software results.

Encounter planning runs once. Discovery scans the small saved manifest and checks one room rectangle set per simulation step; map exploration updates are bounded around a newly visited tile. UI uses no per-frame hidden-map rebuild. Actor counts, shared atlas architecture and decoration budgets are unchanged. Existing visual suites measure transitions, nodes, atlas resources and combat rendering; longer physical-device memory/performance acceptance remains required.

## Android and release boundary

The existing workflow exports the exact source's ARM64 development APK, verifies metadata/resources/signature and runs a matching x86_64 installed-emulator package. The smoke adds real OS-touch warden dialogue/intro acceptance using logged actual button rectangles, plus actual room-discovery events. Existing movement/combat/chest/pack/background/transition coverage remains. F10 region-entry fixture coverage is explicitly a developer shortcut, not proof of walking the adventure route.

**A complete installed-Android OS-touch relic/return/reward adventure is not claimed.** Desktop production API completion, rendered dialogue completion, installed Android input smoke and physical S25 acceptance are separate evidence levels. Exported resources and workflow results must be checked on the final commit. The APK and Actions URLs are supplied with delivery once available.

## Review and remaining limits

Review includes the whole PR's saved geometry, landmark authority, Gloot ownership, events, quest ordering, export resources and stale-view restoration. Part 4 also guards views removed by a snapshot restoration. Retained constraints: no historical geometry migration, no new boss system, no additional regions/classes/equipment economy. Optional content is intentionally small. Map labels and encounter quality still need subjective phone review.

Before acceptance, inspect exact-source workflow results and the final rendered evidence. Remaining blockers, if any, must be reported explicitly with failed check names. Physical Galaxy S25 testing must cover presentation, touch comfort, audio, sustained performance and longer-session stability. Keep PR #5 **open, draft and unmerged**.

Next milestones after acceptance: refine encounter/loot balance from phone observations; author a second adventure using the existing contracts; profile real-device rendering and memory before expanding population or regions.

## Local final-review observations

The corrected build passes 7,915 assertions and repeats the normal-health API completion at 10,966 ticks / 84 remaining health. The focused production-panel capture also passes:

```sh
godot --path . --audio-driver Dummy -- --visual-check --mobile-ui --adventure-panels-only
```

It first completes the real API adventure, then displays its persisted dungeon map and completed journal. Render inspection confirmed the journal scrolls, completion text is readable and the map now uses compact E/A/S/T/R markers plus a legend instead of overlapping full names. Both full visual suites include these panel captures.

Observed local measurements (software host; **not a controlled regression comparison**):

| Workload | Pre-Part-4 | Part 4 |
| --- | ---: | ---: |
| Same 30-seed generation median | 38.194 ms | 48.991 ms |
| 16-actor simulation per tick | 0.281 ms | 0.527 ms |
| 128-actor simulation per tick | 1.957 ms | 3.874 ms |
| Managed heap at 128-actor sample | 50,499,136 B | 84,528,344 B |

The Part 4 samples overlapped two software movie encoders/renderers and extra verification fixtures; the baseline did not. The nearly doubled simulation interval therefore cannot be attributed to Part 4 from these measurements. Generation code was not changed in Part 4. Raw results are preserved for investigation rather than claiming an improvement or a physical-phone regression. The 65-seed adventure sample recorded median entry/population preparation of 38.590 ms and maximum 146.254 ms. A 10,000-call probe measured already-discovered-room processing at 11.361 μs/call and the no-op quest event bridge at 34.928 μs/call under that same contention. These are bounded-operation observations, not long-session memory or S25 FPS evidence. A serial paired benchmark and sustained physical-device profiling remain limitations of this delivery if not superseded by attached final evidence.

## Serial paired benchmark (supersedes the contention comparison)

An identical additional diagnostic harness was compiled into separate detached baseline/current checkouts. It uses production World/Gloot/GOAP without presentation or narrative subscribers; no gameplay implementation was changed by the harness. Baseline ran to completion before current began. The raw canonical-seed samples, three repeated 600-tick population samples and harness are committed in `docs/adventure-evidence/`. This measures CPU work; renderer/texture/frame-pacing evidence remains in the viewport suites. The tested gameplay candidate was `a7c960d`; the subsequent recovery-file retention change is outside these benchmark operations.

| Workload (median) | Baseline | Part 4 |
| --- | ---: | ---: |
| Canonical dungeon generation | 22.181 ms | 19.600 ms |
| Region population | 32.657 ms | 10.076 ms |
| 16-actor World tick | 0.518 ms | 0.298 ms |
| 64-actor World tick | 0.964 ms | 0.879 ms |
| 128-actor World tick | 1.803 ms | 1.711 ms |

These results replace any inference from the earlier overlapping recordings. They do not establish sustained S25 performance or long-session GPU/texture memory. Three additional file-system regression assertions verify that an older recovery file is retained byte-for-byte, repeated retention cannot overwrite earlier copies, and current-generation saves do not create repeated backups. The complete local integration suite passes 7,915 assertions.
