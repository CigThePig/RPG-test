# Gameplay verification

## Running the checks

Requirements: matching Godot 4.6.1 .NET, .NET SDKs from global.json/workflows, FFmpeg, Pillow and a rendered display (Linux CI uses Xvfb).

```sh
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
xvfb-run -a python3 tools/verify.py --visual
xvfb-run -a python3 tools/verify.py --visual --mobile
xvfb-run -a python3 tools/gameplay-verify.py
xvfb-run -a python3 tools/gameplay-verify.py --presets precise --realtime --output build/gameplay-realtime
```

Routine gameplay CI records baseline and precise; manual extended verification includes baseline/precise/soft/explore plus a real-time desktop recording. Runs have bounded process and job timeouts. CI installs Pillow 11.3.0 and the imageio-ffmpeg 0.6.0 wheel with bounded pip retries, verifies libx264/X11 support, and uses the runner's existing Xvfb. This avoids an observed stall in unrelated apt repository updates. The binary is a CI utility, not an APK dependency. The existing Android workflow builds/signs ARM64, installs an x86_64 companion on an emulator and captures OS-touch gameplay video. APK signing remains unchanged.

## Production harness

`GameplayReplay` runs inside Foundation and schedules events immediately before normal physics. It creates an explicit development region; the ordinary World controller, action gates, circular terrain collision, camera, PPC, hotbar and joystick run unchanged. Inputs use `Input.ParseInputEvent` with finger 7 and secondary finger 8, plus production keyboard interaction. Injected event batches are explicitly flushed before simulation; otherwise Godot can coalesce drag input across multiple physics ticks in one render frame. It records actual dispatched raw input before UI consumption, received analog, and resulting authoritative movement.

Controlled scenarios: horizontal/vertical/diagonal, three-tick tap/stop, reversal, eight turns, analog ramp, interior camera-follow travel away from map clamps, wall slide, wall recovery, one-tile gap, menu cancellation, concurrent action touch and chest approach/interaction. Additional seeded production-world scenarios run settlement path following, reachable dungeon corridors and movement while casting around a live enemy. Fixture teleports are marked; they are not counted as travel. Region clearance is checked every scenario tick. Current coverage does not automate an entire dungeon quest or every native safe-area gesture; existing integration and physical testing remain complementary.

Every movie replay is repeated through an independent rendered Godot process and event dispatch at the same 60Hz clock and its position/input/action/collision digest must match. The trace comparison excludes presentation/frame clocks. Both runs use a real display: headless Godot reports a dummy window transform that rescales synthetic touch coordinates and is unsuitable for this full dispatch check. This establishes deterministic authoritative behavior, not pixel-identical animation across backends. Scenario failures identify the name, coordinates and blocking reason. Never run verification by loading an ordinary save: automatic replay disables background autosave and never offers ordinary save actions.

## Evidence package

Each preset produces:

- `gameplay.mp4`: H.264 of the actual running renderer. Movie Maker AVI is transcoded then removed to bound artifact size; no frame reconstruction from logs.
- `contact-sheet.jpg`: sampled actual frames with video timestamps, including the end of the clip.
- `trace.jsonl`: session, raw touch/drag, sample, render-frame, configuration and event markers.
- `scenarios.json`: actual travel/displacement, endpoints, clearance, movement and source frame per scenario.
- `report.json`: source/dirty state, renderer, timing mode, deterministic digest, speed/camera metrics and obstructed periods with frame/timestamp.
- `repeat/`: independent deterministic reproduction trace/logs; `engine.log` retains engine failures.

Automated checks reject missing movements, movement persisting after release, inconsistent directional speed, invalid footprints, missing rasterization, raster errors, frozen/blank movies, failed chest interaction and mismatched repeated states. Video checks combine decoded frame counts, scene color richness, temporal differences in a world-only crop and runtime semantic assertions. They are deliberately not a pixel-perfect artistic quality score. Camera/animation/UI readability still requires inspection. Per-backend visual tolerance does not excuse missing terrain or shader errors.

## Time bases and units

JSONL uses ground pixels, canvas projected pixels, seconds and milliseconds. `tick` is authoritative simulation ticks at 60Hz; `frame` is Godot's drawn-frame counter; `wallMs` is monotonic time since recording started. `render-frame.engineDeltaMs` is engine delta; in Movie Maker it is imposed. Real-time frame p50/p95/p99 comes from consecutive monotonic render-frame samples. `physicsMs` describes callback delta, `simulationMs` measured World work; neither is GPU time. `droppedSeconds` explicitly reports elapsed time discarded by the existing 100ms accumulator cap. That cap is retained, not hidden by accelerating simulation. Reports also expose total simulation seconds and wall seconds, making engine-level lost tick time visible without pretending the accumulator cap measures all dropped time.

In Movie Maker, frame/60 locates evidence within one render interval. Initialization and teleports are visible and marked. Real-time FFmpeg uses `captureStartUnix` and the session UTC + wallMs to align the separate recording; renderer frame IDs are not real-time video PTS. Android `recording.json` anchors screenrecord start and stores measured release positions; logcat provides coarse interaction/lifecycle synchronization. ADB video is not sub-frame latency evidence. The opt-in phone trace is much richer than the Android smoke log.

Samples contain requested/actual velocity, screen/ground input, finger/anchor/displacement/analog, stat speed/radius/capabilities, action/animation/yaw, rejected attempts/slides, camera target/center/zoom/player screen position, simulation/raster/upload costs and periodic managed memory. Allocation and file I/O are skipped when recording is disabled. Logging is bounded and buffered. `blocked` identifies terrain/action restrictions; authoritative collision candidates are visualized with the existing collision overlay. An obstruction report is a diagnostic, not automatically a bug: intentional wall pressure produces many blocked samples.

## Environment limits

Headless proves systems, hosted desktop proves a rendered application, emulator proves installed Android rendering/input, and physical S25 testing proves that specific device's experience. Keep those labels in every report. Offline movie smoothness is guaranteed by the capture clock and must never be advertised as stable physical 60FPS. Use the manual real-time capture and phone traces for pacing. No first-party MCP server is required for future coding agents to consume these files.

### Combat and menu follow-up

The core replay now includes an eighteenth scenario, `attack-readability`: three actual Attack-button touches, a labeled NPC reposition/bite command, and an assertion that the incoming hit does not reverse player facing. The fixture uses production ability timing/damage/knockback; its frozen, high-health enemy is explicitly controlled, not a test of autonomous AI. The phone visual suite also swipes over main-menu button rows and requires scrolling without activation. The preserved movement presets remain unchanged.

## Dungeon checks

`DungeonChecks` runs within `tools/verify.py --headless`: seeds 0–63 plus 1337, deterministic terrain/features/gates/rooms, the room/corridor geometry contract in [DUNGEON_LAYOUT.md](DUNGEON_LAYOUT.md) (sealed block rings, passage placement and door masks, 9×9 room cores, bare 3×3 halls, legal interior architecture), variety across the sample (room footprints, wings, halls, corridor turns), isolated 2×3 gate groups, reachability of every floor tile, player physical reachability and canonical-seed player/NPC/prowler/brute/spitter clearance. Canonical progression uses `World.Move` and `World.Interact` through keys, required gates, relic and exit; these checks do not teleport through gates. `DungeonEncounterChecks` additionally drives real Prowler/Brute/Spitter AI through horizontal and vertical corridor crossings for eight simulated seconds each, observing melee/cone starts and projectile releases. The suite also checks safe arrival clear of the exit marker, generated-room rolls, enclosing-wall roll/lunge collision, gate projectile collision, eight production spawns, exact save roundtrips and deterministic continuation. Tests capture/restore in memory and do not write user slots.

The default 45-scenario replay remains compatible. `--replay-only=dungeon-expedition` selects an additional focused production-touch fixture (precise preset): seed 1, room dodge, corner waypoint, key, required gate, objective and return. Other actors are isolated in the existing feel-lab during this traversal fixture; normal combat and tactical replays remain separate. Example after build/import:

```sh
godot --path . --audio-driver Dummy --resolution 1280x800 --fixed-fps 60 -- \
  --gameplay-check --mobile-ui --preset=precise --replay-only=dungeon-expedition \
  --evidence=build/dungeon-expedition
```

The existing `tools/gameplay-verify.py --presets baseline,precise` command also records this focused precise expedition, repeats it independently, compares authoritative digests, transcodes the actual movie and produces a contact sheet. CI runs it through the unchanged gameplay workflow. A fixture reset is explicit and occurs before traversal, never beyond a locked gate. Seed 42 legitimately allows an alternate route, so it is retained for the visual comparison but is not a mandatory-gate replay fixture.

`DungeonVisualChecks` extends desktop/mobile viewport capture with the seed-42 entrance room, a corridor crossing and the corridor beyond it at unchanged 1.5× zoom, closed/open production gate interaction, enemy doorway placement and a mixed room. These composed screenshot fixtures freeze time for comparable captures; they are not a recording of live enemy navigation. Full gameplay replays and collision tests supply temporal evidence. Rendering reports record initial terrain draw cost and 90-frame static-room intervals on the current renderer; software results are not physical-device FPS claims.

## Wall depth regression checks

`WallDepthChecks` runs in both full desktop/mobile viewport suites. It uses a real seed-42 shared wall and doorway with production RegionState, CharacterView and wall meshes. Pixel comparisons reproduce the old below-every-character wall order, then require the normal depth order to restore exact wall pixels over the lower body while keeping the upper silhouette visible. A front-side character must paint over wall pixels. A centered doorway must produce the same pixels with background/normal wall ordering (no invisible rectangle over the gap). Production World.Move crosses the opening and returns without changing collision.

`--visual-check --wall-depth-only` runs this focused scene, and `--mobile-ui` selects its mobile layout. Captures are `wall-depth-old-order-reproduction`, `wall-depth-behind`, `wall-depth-in-front` and `wall-depth-doorway`, with an optional `mobile-` prefix. The old-order reproduction is the current scene with only wall Z order temporarily forced behind actors, not an earlier APK. Normal gameplay uses ordinary ground-foot Y sorting with no per-frame layer toggles or per-tile nodes.

## PR5 Part 2 environment presentation

See [PR5_PART2_HANDOFF.md](PR5_PART2_HANDOFF.md) for the shared floor/wall atlases, cosmetic hashing, PPC natural-art pivots, bounded nonblocking decoration, production reference scenes and exact-source verification. Region/gameplay authority and Part 1 front-edge wall Y sorting remain unchanged. `--visual-check --environment-only` runs focused real-renderer references and repeated region rebuild measurements; add `--mobile-ui` for the phone layout.

The extended installed-Android smoke also uses the F10 developer region-entry shortcut for three generated-ruin Attack/Dodge/return cycles. It requires real completion events with a bounded timeout and stable wall/decoration/atlas counts. F10 delegates to the existing World.Enter/Diagnostics route and leaves the reversible combat course first. This covers exported-region rendering and repeated transitions; the full key/gate/relic expedition remains the separate production-touch movie fixture, and S25 physical acceptance remains manual.

## PR5 Part 3 landmark verification

The full integration suite now includes LandmarkChecks: malformed resource validation; every template with all 15 nonempty masks and four turns; independent resolved generation/serialization on seeds 0..63, 1337 and 1000..1031; natural template/theme coverage; authoritative collision and prop budgets; mutation-state roundtrips; actual movement/roll/melee/projectile checks beside columns. DungeonChecks checks legal interior blockers, protected cross/perimeter lanes, complete footprint reachability across whole room shapes and cardinal combat sweeps from each room core. Existing progression/AI/recent-fix checks remain.

Both full viewport suites include LandmarkVisualChecks. `--visual-check --landmarks-only` runs focused real production captures; optional `--landmark=ID --orientation=0..3 --visual-seed=N` selects a standalone authoring preview. Captures go to `docs/landmark-screenshots/`; timing/resource observations go to `build/dungeon/landmarks-{desktop,mobile}.json`. No user slots are written. Installed Android retains all earlier smoke scenarios, checks the third shared atlas and requires actual generated entrance/sanctuary metadata in exported runtime logs. See [LANDMARK_AUTHORING.md](LANDMARK_AUTHORING.md) for exact commands and data contracts.

## Complete Buried Light adventure

`godot --headless --path . -- --adventure-check` runs a fresh normal-health production API adventure; `xvfb-run -a python3 tools/adventure-verify.py` records its continuous rendered variant with real Dialogue Manager button choices and journal/map views. Playback batches up to three simulation ticks per rendered frame. Neither is claimed as Android OS-touch acceptance. Reports and frames are in `build/adventure/`, uploaded by precise gameplay CI. `AdventureChecks` adds 0–63/1337 encounter/discovery/save/capacity coverage to the headless suite. Existing foundation quest assertions now require recovery, return and deliberate report instead of completion on item pickup. The Part 4 handoff records exact scope and evidence limitations.
