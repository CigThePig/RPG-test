# Android playtesting

See [Android development](ANDROID_DEVELOPMENT.md) for installation, signing and APK downloads. PR #2 stays open. Install the ARM64 development APK, not the x86_64 emulator package. Ephemeral debug keys may require uninstalling an older run, deleting its saves; persistent private development signing remains optional and unchanged.

## Compare movement on the phone

Open Menu → Game-feel lab / record playtest. Select baseline, precise, soft or explore. The game resumes and the lab overlay identifies the active preset, actual speed and input magnitude. No reinstall is needed. Repeat the same settlement chest → road → ruin route, trying slow drags, short taps, reversals, wall contact and movement plus actions. Sliders allow speed, response, dead zone, curve, camera follow and zoom; custom configurations are included in telemetry. Release always stops immediately, so a separate deceleration slider is deliberately omitted.

Use Start / reset controlled obstacle course for the same authored wall, gap and feature layout used in replay tests. Each reset returns to the same position; Return from course restores the pre-test world snapshot. Saving/loading is blocked in the course, and background autosave is suppressed there. Ordinary regions and inventory are restored on return.

Baseline restores original input/speed/camera settings, not an older executable; shared bug fixes remain active. Presets are session-local. Actor genetics, inventory and ordinary saves do not store test tuning.

## Capture and export

1. In the lab, choose Start local recording. Close returns to gameplay. Recording is opt-in and never uploads.
2. Optionally use Samsung/Android Screen recorder for actual physical-device video. The game does not request screen-capture permission or secretly capture the display.
3. Tap Mark issue during a problem. A timestamped marker is written immediately. The lab also provides named feedback markers; these are stamped when selected, so mark the live moment first if menu navigation takes time.
4. Return to the lab and Stop, or choose Stop and export playtest ZIP to Files…. Android's native Save dialog lets you choose Downloads or another destination. Share the resulting ZIP through the phone's Files app alongside the screen recording. The exporter uses Godot FileAccess for Android content URIs, not System.IO paths.

The ZIP contains JSONL diagnostics and a README, not your save or inventory snapshot. Recording includes position, inputs, configuration, action/animation, camera and performance; no account identifiers or automatic network transfer. One local session is overwritten when starting another; export before replacing it. Logging is bounded to 15 minutes or 120,000 records and flushes on lifecycle pause. A terminated process may lose the last buffered records; backgrounding flushes them. Source identity is embedded by the build workflow.

Native export is implemented against Godot's supported Android Storage Access Framework. Confirm provider selection and the exported ZIP on the physical S25; hosted input/video verification alone cannot certify every Android document provider. Canceling the picker leaves the local recording intact. Failure is reported on screen.

## What feedback helps

Identify the preset, route/location, finger motion, expected response and observed problem. Share an issue marker time and video time when possible. Distinguish slow travel, slow input response, collision restriction, camera lag and animation stutter. No telemetry score determines whether you enjoy the controls.
