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

## PR5 Part 1 dungeon checks

`DungeonChecks` runs within `tools/verify.py --headless`: seeds 0–63 plus 1337, exact PR4 topology signatures, deterministic terrain/features/gates, 9×9 interiors, all boundary cross-sections, isolated 2×3 gate groups, player physical reachability and canonical-seed player/NPC/prowler/brute/spitter clearance. Canonical progression uses `World.Move` and `World.Interact` through keys, required gates, relic and exit; these checks do not teleport through gates. The suite also checks generated-room rolls, enclosing-wall roll/lunge collision, gate projectile collision, eight production spawns, exact legacy/new save roundtrips and deterministic continuation. Tests capture/restore in memory and do not write user slots.

The default 45-scenario replay remains compatible. `--replay-only=dungeon-expedition` selects an additional focused production-touch fixture (precise preset): seed 1, room dodge, corner waypoint, key, required gate, objective and return. Other actors are isolated in the existing feel-lab during this traversal fixture; normal combat and tactical replays remain separate. Example after build/import:

```sh
godot --path . --audio-driver Dummy --resolution 1280x800 --fixed-fps 60 -- \
  --gameplay-check --mobile-ui --preset=precise --replay-only=dungeon-expedition \
  --evidence=build/dungeon-expedition
```

The existing `tools/gameplay-verify.py --presets baseline,precise` command also records this focused precise expedition, repeats it independently, compares authoritative digests, transcodes the actual movie and produces a contact sheet. CI runs it through the unchanged gameplay workflow. A fixture reset is explicit and occurs before traversal, never beyond a locked gate. Seed 42 legitimately allows an alternate route, so it is retained for the visual comparison but is not a mandatory-gate replay fixture.

`DungeonVisualChecks` extends desktop/mobile viewport capture with seed-42 old/new rooms and doorways at unchanged 1.5× zoom, adjacent-room movement, closed/open production gate interaction, enemy doorway placement and a mixed room. These composed screenshot fixtures freeze time for comparable captures; they are not a recording of live enemy navigation. Full gameplay replays and collision tests supply temporal evidence. Rendering reports record initial terrain draw cost and 90-frame static-room intervals on the current renderer; software results are not physical-device FPS claims.
