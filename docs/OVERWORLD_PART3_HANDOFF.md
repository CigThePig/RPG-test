# Living Reach — Part 3 handoff

Verified implementation source: **`fec28d79c099fecd3931184a8ea752669f52a764`**.

Part 3 implements the environmental object family described in [the design](OVERWORLD_PART3_DESIGN.md). It is layered over Part 2 (`acf694d312f0da1cf9d26c68e23525e8fe639e19`) on `feat/overworld-visual-overhaul`. Keep PR #8 draft and unmerged; Part 4 remains open.

## Implementation

- `OverworldPropArt` rasterizes fixed indexed-pixel recipes for four tree silhouettes, natural stone, shrubs, herbs, ore, timber/iron chests and the portal. Harvested trees leave stumps, mined ore leaves low stone, gathered herbs leave clipped stems, and looted chests open.
- All overdrawing is cosmetic. Region features, depletion, inventories, navigation, collision, item rewards, world generation and saved data retain authority. No simulation or character presentation source changed.
- A single immutable 576×624 RGBA8 atlas is reused by features, decoration and batched terrain stone. Pixel payload: **1,437,696 bytes**. There are no per-instance textures, additional animation loops or growing seed caches. Original PPC art and the three dungeon atlases are unchanged.
- Decoration positions/counts use the existing rules and hashes. Variant selection uses higher hash bits independently of the low-bit object-type choice; three canonical-seed checks guard against a forest of identical crowns. Tall objects retain the root pivot/Y-sort contract; natural stone retains its ground-layer ordering above water. Dungeon `FeatureView` artwork is selected independently.
- The dedicated CI check uses `tools/verify.py` so engine/script errors fail the job even if an outer check reaches its success marker.

## Local verification

Runtime: Godot **4.6.1 .NET**, .NET SDK **9.0.203**, GL Compatibility, Debian 13 / Mesa llvmpipe. Final focused visual run used `DOTNET_ROLL_FORWARD=LatestPatch` and .NET 8.0.15. Measurements are software-renderer observations, not Android/S25 FPS.

Commands (with the matching Godot/.NET binaries on PATH):

```sh
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
xvfb-run -a -s '-screen 0 2560x1440x24' python3 tools/verify.py --visual --mobile --overworld-props-only
```

The local X server used TCP display 34067 in this restricted environment; CI uses its normal `xvfb-run` command above.

- Build: **0 errors**, 58 existing warnings in third-party/generated dialogue and PPC code.
- Headless integration: **13,891 assertions passed**, including deterministic recipes, all 44 module slots, transparent gutters, authoritative state/continuation, resource depletion and bounded atlas reuse.
- Production references captured the same Part 2 composition before/after, six viewport resolutions, seeds 1/42/1337, resource states and reduced motion. Reviewed actual reference, state gallery and atlas rasters.
- Actual new-tree depth comparison: **133 actor pixels covered from behind / 1,104 pixels revealed in front**. Static prop scene pixels were identical with reduced motion toggled.
- Six dungeon→overworld roundtrips: **one atlas build**, 96 decoration nodes (the pre-existing cap), **16,379,535 texture bytes / 6,852,764 buffer bytes** at every final sample. Environment construction ranged **60.08–81.01 ms**. Atlas creation took **14.52 ms** once. Raw data are in `docs/overworld-evidence/part3/`.
- Existing water regression suite also passed with the new natural stone: **225 raised-rock pixels restored**, **232 animated / 0 reduced-motion water pixels**. This retains Part 1/2 shoreline layering and animation behavior.
- A single matched Part 2/3 frame sample observed p95 **25.91 / 22.50 ms**. This is descriptive, not a statistically robust performance improvement claim; startup ordering differs, and the process had already initialized the shared atlas before the comparison.

An initial version of the visual fixture tried to visit an uncreated `ruin` region. The resulting engine errors invalidated that attempt. The fixture now explicitly supplies a production-generated dungeon and its entrance position; the final focused run completed with no engine/script errors. The CI wrapper also catches such errors independently of the test marker. Local display initialization failures before the TCP X server setup were environment failures, not game passes.

## Hosted verification and phone acceptance

All hosted workflows passed at **`fec28d79c099fecd3931184a8ea752669f52a764`**:

- [Native verification](https://github.com/CigThePig/RPG-test/actions/runs/42175180513): build/import, **13,891 assertions**, normal-health adventures, desktop/mobile rendering, widescreen, existing water/ground references and the new prop/depletion/depth suite. Hosted tree-depth counts also measured **133 / 1,104**.
- [Gameplay verification](https://github.com/CigThePig/RPG-test/actions/runs/42175180567): baseline and precise deterministic replays, rendered movies and the complete precise normal-health adventure/dialogue flow.
- [Android packaging/runtime](https://github.com/CigThePig/RPG-test/actions/runs/42175180590): signed ARM64 export and package validation, plus both installed x86_64 layouts (**1280×800 and 1560×720**) passed on their first attempts at this revision. Checks include three nonblank background/resume cycles, combat/input cancellation and dungeon entry/return cycles. This verifies the tested revision; it does not establish elimination of the earlier intermittent platform lifecycle issue.
- [Signed ARM64 test APK](https://github.com/CigThePig/RPG-test/actions/runs/42175180590/artifacts/13483795596): `Mosslight-android-arm64-debug-fec28d79c099` (extract the ZIP and install the APK).
- [Hosted native evidence](https://github.com/CigThePig/RPG-test/actions/runs/42175180513/artifacts/13483621517), [standard Android evidence](https://github.com/CigThePig/RPG-test/actions/runs/42175180590/artifacts/13482924038), [wide Android evidence](https://github.com/CigThePig/RPG-test/actions/runs/42175180590/artifacts/13482879367).

The installed wide exploration capture was also inspected: it shows the new props, open chest and unchanged PPC actors/UI in the exported Android application. The Android OS-touch suite does not cover the complete relic/return/reward adventure; that full flow passed the rendered desktop gameplay suite.

The final handoff/capture update is documentation/evidence only and leaves that verified game/test implementation unchanged. Its fresh CI may be running; the source and APK above identify the tested runtime exactly. Part 3 implementation and automated verification are complete. Physical Galaxy S25 installation, subjective art/touch acceptance, sustained pacing and long-session behavior remain unverified. New art has no dependency on physical-device acceptance; do not describe emulator results as S25 results.

## Review captures

`docs/overworld-screenshots/part3-before-reference.png` and `part3-after-reference.png` are matched production compositions. Additional `part3-*` files cover the atlas, resource states, all six resolutions, canonical seeds and tree occlusion. CI publishes these and current raw `build/overworld/part3-*.json` with native evidence.
