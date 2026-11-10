# Living Reach — Part 4: a brighter, deeper overworld

Part 4 is an art-led pass over everything Parts 1–3 put on screen. It deliberately departs from
the muted ramps in [the original standard](OVERWORLD_ART_DIRECTION.md): the Reach is now a
sunlit woodland with saturated turf, clear blue water, cool stone and warm timber. All of the
engineering contracts from the earlier parts are kept.

Compare the committed Part 3 captures (`docs/overworld-screenshots/part3-after-*.png`) with the
Part 4 captures beside them (`part4-after-*.png`, `part4-seed*.png`, `part4-resource-states.png`,
`part4-atlas.png`).

## What changed on screen

| Area | Part 3 | Part 4 |
| --- | --- | --- |
| Rock terrain | One identical boulder sprite per cell, reading as a grid | Connected outcrops: a fractured, mossed top lifted 10 px, a stratified south face, a bevel lit from the upper-left and a cast shadow. Isolated cells stay single boulders |
| Water | Hard stepped contour bands, static glints | Dithered depth, a raised soil bank on northern shores with its shadow on the water, lapping foam on low shores, drifting swell lines and sparkle |
| Ground | Two flat tones with sparse flecks | Three-tone turf with dithered seams, blade tufts that lean in passing gusts, wildflower drifts, a lit turf lip over shaded soil, pebbles and scuffs on bare earth, pale strand at the waterline |
| Trees | Four angular crowns of one species | Oak, elm, fir and birch, painted as lit volumes with a dark contour and cast shadow |
| Other props | Flat-shaded | Repainted boulders, crystal ore and its cut remnant, bell-flowered herb and clipped stems, banded chest (shut and open), arched portal with a pool of light, stumps |
| New growth | None | Reedmace at the banks, lily pads on open water, meadow clumps, bluebells, toadstools and mossed windfall |
| Atmosphere | None | Slow cloud shadow in stepped bands and a few drifting pollen motes |

## How it is built

- **Shaders, not textures.** `overworld_ground_common.gdshaderinc`, `overworld_water.gdshader`
  and the new `overworld_rock.gdshader` paint from absolute canvas pixels, the existing
  one-third-resolution region lookups and a cosmetic seed. Nothing is keyed to the camera, so
  the art is stable under movement and at every zoom preset.
- **Outcrops.** `OverworldRockRenderer` owns one RGB8 lookup per region
  (`(width*8+1)*(height*4+1)*3` bytes; 297,027 for 64×48) and merged quads over rock cells and
  their neighbours. Space outside the region counts as rock, so the boundary wall runs off the
  map. It is added before the water among the ground siblings: raised stone still draws above
  the surface, and the existing rock-depth pixel check still guards that.
- **Props.** `OverworldPropArt` keeps its fixed indexed atlas and gains two flora rows:
  576×728 RGBA8, **1,677,312 bytes** (was 1,437,696). Still one build per process.
- **Flora.** `OverworldFlora.Plan` chooses placements from coordinates alone. Standing plants
  share one Y-sorted node per cell row, so actors pass correctly in front of and behind them;
  lily pads lie in the ground layer. Flora is never a feature, never collides and is not saved.
  Interactables, their neighbouring cells, the entrance and the decoration slots stay clear.
- **One clock.** `ReachAmbience` feeds `reach_time` to every Reach shader. It advances with
  scaled process time, so it stops when the game is frozen; reduced motion pins it to zero, which
  makes water, turf, cloud and pollen a single still frame; fixtures can hold it with
  `ReachAmbience.PreviewTime`.
- **Atmosphere.** Cloud shadow is one 128×128 R8 tileable field (16,384 bytes, built once per
  process) multiplied over the finished world by `overworld_atmosphere.gdshader`, so it crosses
  terrain, props and actors alike. `OverworldAtmosphere` also draws the pollen as a few
  one-pixel rectangles over the cells near the camera.

Unchanged: geography, collision, world RNG, movement, combat, rewards, save data, PPC
characters, dungeon artwork, decoration positions and the widescreen/mobile layout. Decoration
slots keep their positions; the mix per slot now favours trees (three in eight) over shrubs.

The reversible verification switches still work. The frozen Part 1 water moved to
`overworld_water_part1.gdshader`; when an older layer is selected the outcrops, flora and
atmosphere switch off with it.

## Verification

Godot 4.6.1 .NET, SDK 9.0.203, GL Compatibility on Mesa llvmpipe. These are software-renderer
observations, not Android frame rates.

```sh
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
xvfb-run -a -s '-screen 0 2560x1440x24' python3 tools/verify.py --visual --mobile --overworld-props-only
xvfb-run -a -s '-screen 0 2560x1440x24' godot --path . --audio-driver Dummy -- --visual-check --mobile-ui --overworld-only
```

Local results on the final source:

| Check | Result |
| --- | --- |
| Build | 0 errors |
| Headless integration | **14,328 assertions** passed (13,891 at Part 3) |
| Overworld props, depletion and depth | Passed; tree depth **1,854 covered / 1,312 revealed** actor pixels; one atlas build across six dungeon round trips |
| Overworld water references | Passed; **188** raised-rock pixels restored over water; **16,191** animated pixels, **0** under reduced motion |
| Part 2 ground references | Passed |
| Widescreen matrix | Passed |
| Desktop rendered UI suite | Passed, 212 s (182 s on the Part 3 head, same machine) |
| Phone rendered UI suite | Passed, 213 s |
| Normal-health adventure | Passed, 10,966 ticks, 84 health |

Cloud shadow was then restored as a whole-scene multiply pass and every suite above was run
again on that source: all pass, with 14,328 headless assertions, tree depth 1,853 / 1,312, and
209 s and 216 s for the desktop and phone UI suites. Software rendering is slower with Part 4,
so `tools/verify.py` now allows 480 s per suite (was 360 s) and the native CI job 25 minutes
(was 20).

New and changed checks:

- Flora plans are stable, bounded, use only their own atlas modules and avoid features.
- Rock cells are classified as outcrop or lone boulder, never both; the outcrop field is bounded.
- The ambience clock runs, freezes under reduced motion and obeys a preview time.
- The rendered suite now proves the atmosphere moves when motion is allowed and is pixel-identical
  when it is not.
- **The tree-depth check was not measuring what it claimed.** `ShowCharacters(false)` hides
  features too, so its "tree only" capture was bare ground; it passed in Part 3 only where leaf
  colours happened to match the old turf. The capture now keeps the tree visible and counts real
  occlusion.

For quick art review, `--visual-check --mobile-ui --overworld-preview [--preview-clean]` writes
five production captures and the atlas to `build/preview/` in about twenty seconds and asserts
nothing.

## Review fixes: bank shadow, reed footing and flora depth

- **Bank shadow on water.** The shader decided "under a raised bank" from the local slope of the
  shoreline distance field over three pixels. The irregular bank inset changes that slope, so the
  shadow vanished along stretches of north bank, leaked onto west/east shores, and was cut off
  diagonally at corners (foam showed where the shadow belonged). It now measures the vertical drop
  to the visible shoreline straight above each pixel and shades only within that band.
- **Reeds.** Roots were fixed offsets from the cell centre, which ignored the visible
  shoreline (distance less inset), so clumps stood on the strand or bank. Roots are now searched
  inward from the bank to the first point 5-10 ground units out from the drawn waterline, and the
  stems end in a broken foam ring with tinted submerged stems instead of an ink floor line.
  `OverworldPropChecks` asserts every reed root lies in that band.
- **Flora depth.** A standing character's drawn feet sit 6 screen pixels below its ground point,
  but flora rows sorted at the stems' exact base, so a character standing just in front of a
  plant drew behind it. Standing flora rows now sort `OverworldFlora.ActorFootDrop` higher.

## Not verified

Performance is accepted: the owner ran the PR #8 ARM64 APK on the physical Galaxy S25 and reports a
stable frame rate. Physical Galaxy S25 appearance and touch comfort remain open, as does the intermittent Android resume issue recorded in the
Part 1 handoff. The Android and gameplay-replay workflows were not run locally for this part.
