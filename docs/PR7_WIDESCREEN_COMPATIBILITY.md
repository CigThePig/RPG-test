# PR #7: full-screen rendering and widescreen compatibility

## Baseline and contract

Inspected PR head `1e73955586782fd3378efd1b6ff388c9a23998d4` before editing. All baseline workflows passed: [native/reference](https://github.com/CigThePig/RPG-test/actions/runs/41979357694), [gameplay](https://github.com/CigThePig/RPG-test/actions/runs/41979357594), [Android](https://github.com/CigThePig/RPG-test/actions/runs/41979357603). The project used `canvas_items` with implicit Keep aspect, a 1280×800 design resolution, and landscape orientation. Mobile control geometry and HUD used the safe viewport edges. Overlay layout and camera geometry also read the viewport. The camera already centred undersized regions but exposed the default clear background there. Android CI covered only 1280×800.

Acceptance invariants:

- Expand the actual root viewport with `display/window/stretch/aspect="expand"`; retain `canvas_items`, design resolution, camera zoom and gameplay authority.
- Physical Android pixels enter logical canvas space through `Viewport.GetScreenTransform().AffineInverse()`. Never use a camera transform for UI or raw screen pixels for logical hit tests.
- Derive a centred gameplay reference from the logical safe viewport: width = min(safe width, safe height × 1.6). It is contained by, and centred in, the actual safe area, including asymmetric insets. Compact geometry retains its existing scale and spacing rules.
- At equal physical height, gameplay control centres and sizes must differ by no more than **2 physical pixels**, including HUD and joystick. This detects the approximately 306-pixel outward migration an edge-anchored S25 layout would produce.
- World and scrim use the entire drawable viewport. Overlays independently use the safe viewport with a 1280-logical-unit maximum width; dialogue keeps its existing 1100-unit maximum.
- Follow targets clamp using the expanded viewport and unchanged zoom. Smoothing resets only when viewport, region bounds or zoom changes. Camera impact confinement uses the actual smoothed centre. Smaller regions remain centred over intentional, non-playable bedrock backing, without adding tiles or collision.
- Viewport changes cannot change serialized world state or the result of fixed simulation inputs. Canonical seeds remain 1, 42 and 1337.

## Implementation

`DisplayLayout` owns the reference/overlay calculations and camera-centre calculation. `MobileCombatControls.Geometry` and `Arrange` apply the reference region consistently. `RpgUi` places joystick, buttons, notices and HUD in that region, while keeping a full-viewport scrim and independent safe overlays. Android safe-area changes are polled twice per second; a full layout happens only if they change, or through existing resize/navigation/preference paths. No per-frame terrain rebuild or texture creation is introduced.

`Foundation` retains its original movement, simulation and camera zoom. A backdrop CanvasLayer paints bedrock below finite map terrain. The test-only simulation freeze is internal, gated by verification mode, and never serialized. Terrain batching, content, asset caches, save schemas and authoritative systems are unchanged.

Android layout diagnostics include physical display, drawable and logical viewport dimensions, logical safe and gameplay rectangles, transform scale/origin, world span, zoom, and physical rectangles for controls, joystick and HUD. Existing stable action IDs and layout-version protocol are retained.

## Verification and evidence

Geometry-unit coverage is explicitly separate from actual rendered surface coverage. `WidescreenChecks` resizes the real Godot window at 1280×800, 1280×720, 1920×864, 2340×1080, 1024×768 and 900×560, and checks the resulting logical size and screen transform. It compares the old Keep presentation against Expand at the same 2340×1080 native window. Both modes are warmed before measuring performance.

Rendered checks cover uniform scale, full-surface mapping, physical control position/size invariance, scenery in the outer strips of the known Reach fixture, actual smoothed camera edges/corners and bounded impact feedback at five zooms, and real action-input dispatch. The edge check accepts valid scenery regardless of whether it is green, water or stone; it rejects undefined output and bedrock in the known terrain-filled Reach strips. It also tests safe overlays, confirmations and commerce at all six sizes and text scales 1.0, 1.3 and 1.4, including high contrast and asymmetric insets. Dialogue and dungeon captures use the actual wide surface.

A fixed snapshot is unchanged by resizing. For each canonical seed, six different viewport dimensions receive the same save and 120 fixed simulation inputs; resulting serialized state must match exactly. Existing full desktop/mobile suites retain wall-depth, landmark, projectile, camera-hitch, dialogue, inventory, scrolling and navigation assertions.

UI input fixtures now explicitly convert logical points through the viewport screen transform before `Input.ParseInputEvent`, preserving native emulated mouse handling. Fixtures that already converted their points retain their original input path. On taller logical 4:3 views, the scroll test deliberately overflows otherwise-fitting content before retaining its existing scrollbar/Back assertions.

The dedicated native CI step writes `build/widescreen/measurements.json` and `docs/native-screenshots/widescreen-*.png`. Screenshots of the old framing capture the native X11 window, since the old viewport texture itself excludes pillarboxes. Before/after Reach captures reproduce Keep/Expand in the same build and frozen world; they are not claimed to be two separate APK builds. The JSON includes per-control physical rectangles, camera span and performance samples. Standard workflows publish them as artifacts.

Android runtime CI retains **1280×800** and adds **1560×720**, the exact S25 aspect ratio at two-thirds native resolution. Each profile installs the matching exported C# build and runs the existing full OS-touch/lifecycle/dungeon route. Diagnostics assert both the app drawable and logical viewport match the effective Android aspect ratio. Joystick gestures derive their centre and displacement from production diagnostics. Dialogue coordinates use the actual screen transform. The immersive prompt is dismissed through native XML bounds instead of a guessed coordinate. Output folders and artifacts identify the profile separately.

## Acceptance status

The implementation must remain in `feat/mobile-ui-ux-overhaul`, PR #7, open and unmerged. See the PR's widescreen section for exact-source CI and APK status. Local verification, measured values, and pending device gates are recorded below after the final run.

Physical Galaxy S25 acceptance remains mandatory: full-width rendering and cutout placement, thumb comfort, multi-finger input, menu/scrolling/dialogue, Android Back, active-touch background/resume, existing-save continuation, normal-health adventure, and device frame pacing. Linux software rendering and hosted x86 Android are not ARM64 phone performance certification.

## Local verification record (5 November 2026)

- .NET SDK 9.0.203 / Godot 4.6.1 mono: build passes (58 existing addon/vendor nullable warnings; no errors).
- Fresh import passes. This host logs the existing editor shutdown warning about `export/android/shutdown_adb_on_exit`; it is not a game runtime failure.
- `python3 tools/verify.py --headless`: **9,567 assertions pass**.
- `python3 tools/verify.py --visual` and `--visual --mobile`: **both complete suites pass**, including normal-health adventure, environment and landmark checks.
- Dedicated `--visual-check --mobile-ui --widescreen-only`: **passes** all actual surface, input, camera, overlay and canonical-seed comparisons.
- Python Android script compilation and all four existing Android native-window unit tests pass.
- Local X11 uses Xvfb over TCP because this host blocks Unix sockets. CI retains normal `xvfb-run`; no project workaround is needed.
- Android SDK/emulator are unavailable locally. Exact-source Android export, signing, package inspection, runtime OS touches and deterministic gameplay video jobs run on GitHub Actions. Do not interpret local Linux results as Android evidence.

### Measured S25-sized native surface

| Measurement | Old Keep presentation | Expanded presentation |
| --- | ---: | ---: |
| Native window | 2340×1080 | 2340×1080 |
| Rendered game texture | 1728×1080 | 2340×1080 |
| Logical viewport | 1280×800 | 1733×800 |
| Native side bar per side | 306 px | 0 px |
| Gameplay UI rectangle (logical) | (0,0), 1280×800 | (226.5,0), 1280×800 |
| Vertical logical world view | 800 / unchanged zoom | 800 / unchanged zoom |
| Horizontal logical world view | 1280 / unchanged zoom | 1733 / unchanged zoom |

Godot rounds the expanded logical width to an integer. The resulting X/Y scale difference is about 0.019%; the test permits that subpixel rounding while rejecting meaningful stretching. Physical control centres below come from actual `GetScreenTransform` measurements, not expected geometry alone.

| Control | Before centre (physical px) | After centre (physical px) |
| --- | ---: | ---: |
| attack | (1904.40, 928.80) | (1904.54, 928.80) |
| dodge | (1720.80, 880.74) | (1720.91, 880.74) |
| ember | (1629.00, 731.16) | (1629.09, 731.16) |
| mend | (1764.00, 712.26) | (1764.11, 712.26) |
| potion | (1899.00, 731.16) | (1899.14, 731.16) |
| interact | (1545.30, 880.74) | (1545.37, 880.74) |
| inventory | (1761.30, 62.10) | (1761.41, 62.10) |
| menu | (1934.10, 62.10) | (1934.25, 62.10) |
| stick | (495.00, 864.00) | (494.87, 864.00) |
| hud | (603.00, 151.20) | (602.89, 151.20) |

All centres and sizes meet the documented 2-pixel tolerance; maximum observed centre movement is below 0.15 pixels.

### Visual evidence

- [Old 16:10 framing in the actual 2340×1080 native window](native-screenshots/widescreen-before-reach.png)
- [Expanded Reach with preserved HUD/control placement](native-screenshots/widescreen-after-reach.png)
- [Wide dungeon](native-screenshots/widescreen-dungeon.png)
- [Bounded Pack and full-width dimming scrim](native-screenshots/widescreen-pack.png)
- [Wide dialogue](native-screenshots/widescreen-dialogue.png)
- [Structured local measurements](widescreen-measurements.json)

### Available performance comparison

This is a warmed, 60-frame sample on shared-host llvmpipe software rendering at 2340×1080, with other verification processes active. It is evidence of workload and allocations, not a stable phone frame-time benchmark. CI produces its own exact-source measurement JSON; physical S25 profiling remains open.

| Metric | Keep | Expand |
| --- | ---: | ---: |
| Median frame (ms) | 37.31 | 45.83 |
| P95 frame (ms) | 47.80 | 59.83 |
| Draw calls | 286.00 | 294.00 |
| Visible renderer objects | 3763.00 | 3941.00 |
| Primitives | 27785.00 | 27971.00 |
| Static memory (bytes) | 74220548.00 | 74917300.00 |
| Texture memory (bytes) | 22104185.00 | 27391865.00 |
| Terrain vertex bytes | 660264.00 | 660264.00 |

The expanded surface renders 35.4% more game pixels and a modest number of additional visible objects/draw calls. Terrain vertex allocation remains identical. Additional render-target texture memory is expected at the larger rendered surface. No quality reduction or content culling was added. The dedicated test also asserts zero full UI layouts and zero terrain rebuilds during each steady 60-frame sample.

### Files changed

- `project.godot`: enable expanding aspect while retaining canvas-items scaling.
- `game/ui/DisplayLayout.cs` (+ UID): centred gameplay/overlay geometry, pure camera clamp and finite-region bedrock backing.
- `game/ui/MobileCombatControls.cs`: use centred safe gameplay geometry without changing button IDs, sizes, spacing or touch ownership.
- `game/ui/RpgUi.cs`: distinct gameplay and overlay regions; Android safe-area polling; physical geometry diagnostics and layout-pass counter.
- `game/dev/Foundation.cs`: geometry-change smoothing reset, confinement at the rendered centre, bedrock layer, verification-only freeze and terrain-build counter.
- `game/tests/WidescreenChecks.cs` (+ UID): real native surface matrix, comparisons, camera edges, serialization/simulation independence, overlays, input and performance evidence; explicit logical-input fixture helper.
- `game/tests/MobileCombatControlsChecks.cs`: register geometry coverage and inject correctly transformed touches at expanded sizes.
- `game/tests/UiNavigationChecks.cs`: explicit input coordinates and intentional overflow for the existing scroll test on taller views.
- `game/tests/AccessibilityChecks.cs`, `HudChecks.cs`, `VisualChecks.cs`: explicit fixture coordinates; dedicated widescreen entry point. Assertions remain intact.
- `tools/android-smoke.py`: real geometry-based gestures, display/aspect assertions, native immersive prompt dismissal and profile-specific artifacts.
- `.github/workflows/android.yml`: baseline plus S25-aspect runtime matrix.
- `.github/workflows/ci.yml`: native widescreen verification and artifact collection.
- This document, five native screenshots and `docs/widescreen-measurements.json`: reviewable evidence.

## Android runtime follow-up

The first wide emulator attempt hit the existing 20-second cold-install limit before launch. A same-source retry installed and launched successfully, then exposed Android's observed truncation of Godot log messages at roughly 1024 bytes: fractional wide control coordinates made the combined JSON exceed that limit. The drawable and logical size values already showed the expected 1560×720 and 1733×800, but a truncated record cannot be accepted as verification.

Display metadata and each physical control rectangle now use separate short records. `tools/android_layout.py` validates complete display metadata and all ten physical target rectangles; `tools/test_android_layout.py` covers fractional records below the limit, rejection of a truncated newest record, latest target ownership and missing-target rejection. All existing display/aspect/touch assertions remain. Only package installation gets a 60-second cold-start allowance; other ADB operations retain their 20-second bounds and installation is never blindly repeated. The verification-only freeze condition also short-circuits on its false flag before querying command-line arguments during normal gameplay.

Follow-up files: `game/ui/RpgUi.cs`, `game/dev/Foundation.cs`, `tools/android-smoke.py`, `tools/android_layout.py`, `tools/test_android_layout.py`, `.github/workflows/android.yml`, and this record. See the PR for final exact-source workflow results.
