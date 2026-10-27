# Agentic gameplay development decision record

Investigated 2026-10-27 against Godot 4.6.1 .NET. This is a testing workflow, not an AI runtime dependency.

| Solution / primary source | Actual capability | CI / compatibility decision |
| --- | --- | --- |
| [Coding-Solo/godot-mcp](https://github.com/Coding-Solo/godot-mcp) (MIT) | Editor/project operations, run/debug output; screenshot capabilities vary across versions/forks. Active repository, but editor tooling alone does not establish full gameplay input or temporal quality. | Useful interactively; not added as a Node/MCP service prerequisite for Actions. No assumption that another agent has an MCP client. |
| [Godot QA](https://github.com/youichi-uda/godot-qa) (MIT) | Godot 4 unattended scenarios, input replay, node/text assertions and screenshot diffs; small community project, not a demonstrated 4.6.1 .NET/mobile certification. | Adopt its scenario/assert/evidence pattern, not a second runner and overlapping addon. Our C# authoritative World, custom raw touch ownership and Android checks require project-specific observation. |
| [GameDevBench](https://github.com/waynchi/gamedevbench) (Apache-2.0) | Evaluation tasks with engine tests and visual-feedback tooling; maintained benchmark research rather than a mobile production test SDK. | Adopt visual feedback + executable assertions + iteration. Do not import benchmark tasks or a scoring function as a definition of fun. |
| [Godot RL Agents](https://github.com/edbeeching/godot_rl_agents) | Runtime observation/actions for learning agents, with Python training infrastructure. | Far larger than necessary for deterministic joystick experiments. Autonomous training is unnecessary for reproducing a wall snag. Not integrated; no code copied. |
| [Godot Movie Maker](https://docs.godotengine.org/en/4.6/tutorials/animation/creating_movies.html) | Actual game rendering at a fixed clock, AVI/PNG output; stock engine feature. Explicitly not a real-time profiler. | Chosen for comparable actual gameplay movies; FFmpeg transcodes to H.264. Works unattended with a rendered X display. |
| [Input.parse_input_event](https://docs.godotengine.org/en/4.6/classes/class_input.html#class-input-method-parse-input-event) | Production Godot event dispatch including raw touch/drag. | Chosen for tick-scheduled replay; separate ADB tests exercise Android OS input. Not claimed equivalent to physical fingers. |
| [ADB screenrecord](https://developer.android.com/tools/adb#screenrecord) | Actual Android display video, bounded duration; timestamps/logcat available separately. | Chosen for emulator video. Does not certify physical S25 speed or thumb comfort. |
| [Native FileDialog](https://docs.godotengine.org/en/4.6/classes/class_filedialog.html#class-filedialog-property-use-native-dialog) | Android native Storage Access Framework when access is FILESYSTEM. | Chosen for phone ZIP export, using Godot FileAccess for content URIs; no broad storage permission/native sharing dependency. |

No code was imported from the investigated MCP/QA/benchmark projects. Existing PPC adaptation retains its upstream licence; changes to external-drive velocity are local host integration. FFmpeg and Pillow remain test-host utilities; they are not bundled into the APK. CI obtains its FFmpeg executable from pinned imageio-ffmpeg 0.6.0 (BSD-licensed Python wrapper, separately licensed bundled FFmpeg/GPL codec binary). The setup checks actual libx264 and X11 device support before running. No wheel or FFmpeg executable is incorporated into the game.

## Required loop for future changes

1. Read production input, actor state, collision and presentation implementations; reproduce the reported problem with an identified scenario.
2. Run `python3 tools/gameplay-verify.py --presets baseline,precise` under a rendered display (Linux: `xvfb-run -a`). Examine reports, input trace, contact sheets and the MP4. Inspect consecutive frames around a failure, not just its final screenshot.
3. Distinguish movie-clock behavior from real-time pacing. Use `--realtime --presets precise --output build/gameplay-realtime` for an FFmpeg desktop capture and monotonic render-frame samples.
4. Form one falsifiable hypothesis. Change a factor, repeat identical inputs, compare displacement, obstruction, camera and action/animation transitions. Retain failed experiments as evidence, not new production defaults.
5. Run the integration suite and rendered UI checks, including mobile layout. Inspect procedural settlement/dungeon/combat clips as well as the controlled map. Check source identity, errors and deterministic repeat digest.
6. Obtain physical phone feedback for subjective feel; attach issue markers and a locally exported ZIP. State exactly what was inspected, measured, or remains untested.

Every report must identify its commit and dirty state. CI artifacts use the exact checked-out PR head. Do not describe Movie Maker wall-time as FPS performance, headless output as rendered video, or emulator measurements as S25 results. Never synthesize footage from coordinates and label it a gameplay capture.
