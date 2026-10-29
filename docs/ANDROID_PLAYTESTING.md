# Android playtesting

See [Android development](ANDROID_DEVELOPMENT.md) for installation, signing and APK downloads. PR #4 remains unmerged. Install the ARM64 development APK, not the x86_64 emulator package. Ephemeral debug keys may require uninstalling an older run, deleting its saves; persistent private development signing remains optional and unchanged.

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

The combat follow-up keeps the soft/explore settings unchanged. Attack is now the large lower-right target, with Ember/Mend/Draught/Interact around it and Pack/Menu above. Touch-down highlights the owned action target; release outside it or cancellation does not activate it. Cooldown text occupies a second line. Swipe vertically anywhere over menu button rows to scroll, or drag the wider scrollbar. Receiving knockback should no longer turn your character away from its current facing; deliberate movement still changes facing. Sword attacks now have a stronger rig pose and a brief directional trail synchronized to the combat windup.

## Part 4 Galaxy S25 acceptance (phone only)

Physical-device touch comfort, subjective combat feel, audio quality, and sustained ARM64 performance remain pending user acceptance before merge.

1. Download the final source-identified ARM64 ZIP from the linked Actions run on your phone, extract and install its APK. Save the workflow URL, full SHA and build-metadata.json in your notes. No development computer is needed.
2. In ordinary exploration, hold movement and tap Attack/Dodge separately and in quick succession. Test the right thumb's reach to each secondary ability. Check accidental touches, cooldown text, HUD visibility, camera cutout and Android gesture edges. Swipe menu lists starting over button rows; test Pack, equipment, Journal, Character, Map and dialogue.
3. Menu → Developer diagnostics exposes each Prowler/Brute/Spitter, two melee, mixed, four mixed, projectile and eight-enemy crowd fixture. Choose **normal health** to assess pressure; protected observation is separate. Reset by selecting the fixture again. Return from combat lab restores ordinary progress; saving inside a lab is blocked.
4. For each archetype try movement, a deliberate dodge and a counterattack. Check the Brute's committed cone and recovery, Spit contrast on grass/stone and flight avoidance, player tracking in crowds, sword miss whoosh versus confirmed impact, roll/dust and hit reactions. Eight enemies are a stress fixture, not a promised comfortable encounter density.
5. Play for 15–20 minutes, including repeated mixed fights. Note visible stutters, frame pacing, camera stability, warmth/throttling and audio harshness/stacking. The Performance overlay shows p50/p95/p99 and managed memory; screenshots are useful, but this does not measure total GPU/native memory.
6. Background while exploring, holding movement, winding up, rolling and with projectiles active. Repeat at least five times. The resumed menu and gameplay must render; movement/actions must be released. A single black surface is a failure to report even if another resume fixes it. Save/load and exit/re-enter the ruin outside the lab afterward.
7. Start local recording through Game-feel lab; use Samsung Screen recorder and screenshots. Tap Mark issue at the live problem. Stop and export the trace ZIP to Files. Share the ZIP, recording, screenshots, workflow URL, SHA, build metadata, preset, fixture name and any overlay statistics. Describe expected/actual behaviour and video time. Test Android Files export itself, including cancellation.

Emulator passes establish repeatable software checks. They do not complete any of the subjective or sustained phone checks above.
