# Living Reach — Part 3 handoff

Part 3 implements the environmental object family described in [the design](OVERWORLD_PART3_DESIGN.md). It is layered over Part 2 (`acf694d312f0da1cf9d26c68e23525e8fe639e19`) on `feat/overworld-visual-overhaul`. Keep PR #8 draft and unmerged; Part 4 remains open.

## Implementation

- `OverworldPropArt` rasterizes fixed indexed-pixel recipes for four tree silhouettes, natural stone, shrubs, herbs, ore, timber/iron chests and the portal. Harvested trees leave stumps, mined ore leaves low stone, gathered herbs leave clipped stems, and looted chests open.
- All overdrawing is cosmetic. Region features, depletion, inventories, navigation, collision, item rewards, world generation and saved data retain authority. No simulation or character presentation source changed.
- A single immutable 576×624 RGBA8 atlas is reused by features, decoration and batched terrain stone. Pixel payload: **1,437,696 bytes**. There are no per-instance textures, additional animation loops or growing seed caches. Original PPC art and the three dungeon atlases are unchanged.
- Decoration positions/counts use the existing rules and hashes. Tall objects retain the root pivot/Y-sort contract; natural stone retains its ground-layer ordering above water. Dungeon `FeatureView` artwork is selected independently.
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
- Headless integration: **13,888 assertions passed**, including deterministic recipes, all 44 module slots, transparent gutters, authoritative state/continuation, resource depletion and bounded atlas reuse.
- Production references captured the same Part 2 composition before/after, six viewport resolutions, seeds 1/42/1337, resource states and reduced motion. Reviewed actual reference, state gallery and atlas rasters.
- Actual new-tree depth comparison: **133 actor pixels covered from behind / 1,104 pixels revealed in front**. Static prop scene pixels were identical with reduced motion toggled.
- Six dungeon→overworld roundtrips: **one atlas build**, 96 decoration nodes (the pre-existing cap), **16,379,535 texture bytes / 6,852,764 buffer bytes** at every final sample. Environment construction ranged **58.94–71.36 ms**. Atlas creation took **12.69 ms** once. Raw data are in `docs/overworld-evidence/part3/`.
- A single matched Part 2/3 frame sample observed p95 **20.18 / 18.39 ms**. This is descriptive, not a statistically robust performance improvement claim; startup ordering differs, and the process had already initialized the shared atlas before the comparison.

An initial version of the visual fixture tried to visit an uncreated `ruin` region. The resulting engine errors invalidated that attempt. The fixture now explicitly supplies a production-generated dungeon and its entrance position; the final focused run completed with no engine/script errors. The CI wrapper also catches such errors independently of the test marker. Local display initialization failures before the TCP X server setup were environment failures, not game passes.

## Hosted verification and phone acceptance

Hosted native, gameplay and Android results will be recorded here after this implementation commit completes CI. Physical Galaxy S25 installation, subjective art/touch acceptance, sustained pacing and long-session behavior remain unverified. New art has no dependency on physical-device acceptance; do not describe emulator results as S25 results.

## Review captures

`docs/overworld-screenshots/part3-before-reference.png` and `part3-after-reference.png` are matched production compositions. Additional `part3-*` files cover the atlas, resource states, all six resolutions, canonical seeds and tree occlusion. CI publishes these and current raw `build/overworld/part3-*.json` with native evidence.
