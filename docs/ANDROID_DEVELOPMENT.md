# Play Mosslight on your Android phone

You do not need a computer, Godot, Android Studio or a cable. GitHub builds the game; your phone installs it.

## Download and install

1. Sign into GitHub in your phone browser and open [Android development APK runs](https://github.com/CigThePig/RPG-test/actions/workflows/android.yml).
2. Choose a successful run for `feat/combat-feel-enemy-ai-mobile` (PR #4), or `main` after merging. Check its source commit against the change you want to test.
3. Scroll to **Artifacts**. Download **Mosslight-android-arm64-debug-COMMIT**. Ignore `emulator-test-package` and diagnostic artifacts.
4. GitHub downloads a ZIP. Open **My Files → Downloads** on your Samsung phone, select the ZIP and choose **Extract**. Open the extracted `.apk`.
5. If Android requests it, allow **Install unknown apps** for the app opening the APK (usually My Files or your browser). Install **Mosslight Dev** and open it. Samsung Auto Blocker may also block sideloading; change that setting only if you decide to permit this development install.
6. Choose **Enter / Resume** to begin, **Load** to restore the selected save slot, or **Recover background autosave** after a previous session.

Artifacts expire after 30 days. A new branch push or PR update builds automatically. Once the workflow is on the default branch, **Run workflow** lets you select a branch manually without merging its changes. While PR #4 is open, pushes to its branch already trigger builds.

## Controls

Use landscape orientation. Drag the circular stick at the lower left to move; release to stop. A second finger can press action buttons while moving.

| Button | Action |
|---|---|
| Attack / Ember / Mend | Free-form directional sword swing / nearest eligible hostile spell / self-heal |
| Draught | Consume a healing item |
| Interact | Talk to a nearby warden, gather, loot, use keys or enter a portal |
| Pack | Inspect, equip, craft, buy and use items; scroll the panel |
| Journal / Character / Map | Quests, progression and regional map |
| Menu | Pause, save/load, sound, camera zoom, workshop and diagnostics |

Close the pack to resume exploration. Item use from the pack closes it before passing through the shared gameplay command, then reopens it. Dialogue and the pause menu block gameplay. Inventory management is allowed within the pack, while dialogue/death/pause reject mutations. Dialogue choices use large touch buttons. The pause menu scrolls to its lower options.

![Hosted preview of the shared UI](native-screenshots/mobile-controls.png)

The project uses a 1280×800 logical canvas with canvas_items stretching and safe-area-aware controls. Wide phones can have letterboxing. Device-specific cutout, gesture edge and readable-text checks are still part of physical playtesting.

## Saves, updates and signing

Three manual save slots and a separate background autosave use Godot's private writable `user://` application storage. The game runs offline. Backgrounding cancels movement, pauses the world, and saves an active session to the autosave without overwriting manual slots. Returning to the app leaves the pause menu open. Recover the autosave from the menu after process termination. Android can kill a process without delivering a final notification, so save manually before important changes.

**The initial builds use a new temporary debug signing key for each workflow run.** An APK from another run generally cannot update the installed app. Android may say the package conflicts with an existing package. Uninstalling fixes that but deletes private saves. Do not uninstall if you need that progress. Builds from the same run use the same key.

For stable development updates, a maintainer can supply these repository Actions secrets:

- `ANDROID_DEV_KEYSTORE_BASE64`: Base64 encoding of a private development keystore.
- `ANDROID_DEV_KEYSTORE_ALIAS`: Its alias.
- `ANDROID_DEV_KEYSTORE_PASSWORD`: Its key/store password (the same for both).

The workflow uses them when present, otherwise it generates a usable temporary debug key. Never put the keystore/password in source, issue comments or downloadable artifacts. Provision the persistent key before relying on long-lived progress. Switching from temporary to persistent signing requires one uninstall. Thereafter the stable package ID and increasing version code allow updates preserving data. Release signing remains a separate future decision.

## Report a problem from your phone

Include the workflow link, commit shown in its artifact filename, Samsung model/Android version, steps and what you expected. Add a screenshot or screen recording. The ZIP's `build-metadata.json` records source, engine/SDK, package, actual manifest metadata, APK bytes, SHA-256 checksum and signing certificate fingerprint.

In Menu, enable **Performance overlay** and resume. Report FPS, p50/p95/p99 frame times, visible/total actors, managed memory, raster/upload timing and last save time. Managed memory excludes native/GPU allocations. These numbers are measurements from the running device, not promised targets.

A repeatable phone test: load a fresh game, wait 30 seconds, walk and fight for two minutes, talk to a warden, open/scroll the pack and journal, enter/leave the ruin, change creature family, save/load, background for 10 seconds and resume. Repeat after five minutes to expose warm-up or thermal changes. Record minimum FPS, p95/p99 and visible stutters with the build commit. Test multi-touch movement plus attack and cancellation when opening menus.

## What cloud verification establishes

The workflow compiles desktop C#, imports from a clean checkout, runs the complete engine integration and rendered UI suites, and exports an ARM64 debug APK. Standard Android tools check the manifest/package, launcher, signing certificate and native ABI. ZIP/ELF checks require the gameplay assembly, Godot bindings, core library, Mono runtime, publish manifest and game data.

A separate x86_64 export exercises the same source on a GitHub-hosted Android emulator: installation, C# scene readiness, touchscreen Resume, gameplay ticks, screenshot and background/resume. Its result and logcat are in **android-runtime-evidence**. A passing emulator test is not evidence of ARM64 GPU performance on a Galaxy S25. Read the actual run result; package validation alone does not establish launch success.

## Toolchain and compatibility

Godot 4.6.1 .NET with matching official `.mono` templates; .NET SDK 9.0.203 (desktop framework stays net8.0; Android selects net9.0); .NET 8 runtime also installed for desktop checks; Temurin JDK 17; Android platform 35/build tools 35.0.1, NDK 28.1.13356709 and CMake 3.10.2.4988404. The prebuilt APK template uses minimum API 24 and target API 35; verification checks these actual manifest values rather than claiming unsupported overrides. Godot's prebuilt Android C# template requires net9.0. Godot's SDK obtains the Android Mono runtime packs during publish; this is not a .NET MAUI application.

Godot documents Android C# support as experimental. Desktop Linux/Windows presets remain available. Live PPC generation, animation, gear and genomes remain C#, not baked sprites. Pure CPU rasterization can use the original worker queue; texture upload happens after workers join on the Godot main thread. The project includes no remote service requirement, and requests no game network or storage permission.

Official references: [C# requirements](https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_basics.html), [Android setup](https://docs.godotengine.org/en/4.6/tutorials/export/exporting_for_android.html).

## Direct APK links after merging

Actions artifacts are ZIP archives and require GitHub sign-in. A manually initiated development prerelease can attach a validated APK for a direct `.apk` download. Only distribute a successful trusted `main` build this way; do not automatically publish PR artifacts. GitHub's release editor can attach an extracted APK from the phone. No public release is created by the build workflow.

## Playability build

See [MOBILE_PLAYABILITY.md](MOBILE_PLAYABILITY.md) for collision/placement/container changes and save handling. The in-game menu now exposes collision/navigation and performance overlays. Secondary character/journal/map screens are available from that menu on phones. The stick anchors near the initial thumb contact; drag to move and lift to stop. Opening menus/dialogue or backgrounding cancels held movement.

The runtime smoke test checks release coordinates and a real chest-depletion event after touch interaction; merely reaching the splash/menu or advancing ticks is insufficient. Download the ARM64 artifact only from the tested source commit's Android workflow, and check the separate `android-runtime` job before installing. Existing development-signing limitations still apply: changing the generated signing key can require uninstalling the previous APK, which removes local saves. Use configured persistent signing secrets for reliable in-place updates.

## Part 4 layout diagnostics

Android emits bounded `MOSSLIGHT_CONTROL version=1 id=… x=… y=…` markers on layout changes. Centers are in actual window screen pixels, calculated from production global rectangles through the viewport screen transform. `android-smoke.py` reads these for Attack, Dodge, Interact, Pack and Menu instead of historical hotbar coordinates; the layout manifest is retained in runtime evidence. Menu row and joystick route coordinates still use the documented 1280×800 test canvas. Never treat Godot controls as Android accessibility nodes.

The ARM64 download is the `Mosslight-android-arm64-debug-<sha>` Actions artifact. The separately named emulator package is x86_64 and is unsuitable for the Galaxy S25. Keep the build metadata and SHA with phone feedback. A changed ephemeral development signing key may require uninstalling the old build, which deletes local saves; export desired evidence before uninstalling.
