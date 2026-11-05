# The Living Reach — Mosslight environmental art standard

Part 1 establishes the water/material foundation; Parts 2–4 extend it. Authority remains in `RegionState`, `World` and persistence. Art never changes saved geography or collision. Reference base: `ca60f3eda8153c0f7864703e52a1b09f5228f290`.

## Physical space and hierarchy

This is dimensional pixel art in a compressed ground projection, not diamond isometric art. `GodotConvert.GroundToScreen` preserves X and halves Y: 24×24 ground units occupy 24×12 canvas units. Integer canvas pixels and nearest sampling match PPC's logical density. Do not change actor scale, pivots, camera presets or PR7's 1280×800 canvas_items/expand surface.

Foundation owns a ground parent beneath the Y-sorted actor/feature/decoration parent. Water and banks are flat ground; never put them in actor Y sorting. Trees use the PPC (36,100) pivot, rocks (12,16), shrubs (17,23). Upper-left light defines highlights; lower/right faces and contact points are darker. A surface ripple is not a new light source.

Actors, interaction cues and combat telegraphs take priority over materials. Keep broad surfaces quiet, with clustered sediment and sparse broken reflections. Bright amber belongs to meaningful interactables; unusual magic belongs to meaningful locations. Ground should harmonize with moss-black and lantern-gold UI without taking over its contrast.

## Shared ramps

These are controlled material families, ordered shadow to restricted highlight. Part 2 must tune its existing dry materials against the water reference before replacing them globally; Part 3 must retain coherent PPC lighting.

| Material | Darkest | Shadow | Base | Lit | Restricted highlight |
| --- | --- | --- | --- | --- | --- |
| Vegetation | #243E35 | #365540 | #4E7B54 | #72935E | #8BAE70 |
| Earth | #403E32 | #5C4B3B | #80634B | #9B7C58 | #B29970 |
| Stone | #384A47 | #56645E | #7A8681 | #949E90 | #A9B09D |
| Water | #244F60 | #305D66 | #3B6A6D | #487975 | #568780 |
| Bank | #34443A | #514F3D | #65745A | #709078 | #70968A |
| Amber accents | #665539 | #92754A | #B89760 | #D1AE70 | #E2CA92 |

Water uses discrete depth bands sampled in ground space, never one gradient rectangle per cell. Deep centres occur only when distance from land supports them. Narrow channels and isolated pools remain shallow. Reflections occupy a small fraction of the surface; no white/cyan pond outline, photographic noise, blurry filtering or independently flowing tile textures.

## Water construction and material contract

`OverworldWaterArt` supplies pure distance/topology and `Mark(Rect2, Shade)` fallback recipes; `OverworldWaterRenderer` owns native region resources. Eight-neighbour `Mask` classification includes diagonals; bounded distance sampling includes all surrounding land within two cells, treating out-of-region space as bank. Convex corners round within twelve ground units. Low-amplitude, continuous coordinate noise irregularizes the bank within the water footprint. Dry cells retain their complete walkable footprint. The residual damp rim occupies impassable water and cannot create a visual crossing. Small islands remain present; narrow land corridors are never covered by tall water decoration.

The extension contract is:

1. **Authoritative category:** read `RegionState.Tiles`; never write it from art.
2. **Cosmetic variant:** unsigned `OverworldWaterArt.Hash(seed,x,y,slot)` and a cosmetic revision, independent of simulation RNG.
3. **Neighbour transition:** `Water`, `Mask`, `Distance` supply region-safe topology and proximity. Dry-side recipes in Part 2 should consume these same coordinates/proximity, without introducing a second shore classifier.
4. **Surface detail:** production uses one active RG8 distance/variation lookup, sampled every three canvas pixels and interpolated as scalar data. `overworld_water.gdshader` quantizes the data into the shared discrete palette at integer canvas coordinates. `Shade`/`Mark` recipes provide the shader-free fallback. Part 2 can extend recipes behind the same ground-layer contract.
5. **Optional animation:** a separate sparse region batch, never the static material or topology. Reduced motion selects static opacity immediately; shader failure can omit this optional overlay without losing bank/depth artwork.

No full-resolution painted region texture, per-cell node, seed-keyed atlas cache or simulation depth field is introduced. Each active region owns one one-third-resolution RG8 scalar lookup: `(width*8+1)*(height*4+1)*2` bytes, 263,682 bytes for the 64×64 reference. The shader reads one filtered data sample and emits an opaque palette color; filtering the scalar field does not blur rendered pixels. Water geometry merges horizontal cell runs into quads. Native meshes, lookup and materials must be disposed on removal. Two shared shaders live for the process. `--water-static-fallback` uses merged palette recipes without either shader. Profile before extending material families.

## Determinism and review

Coordinates, seed, material slot and art revision select permanent art. No `Random`, string hashes, clock, mutable world fields or scheduling may select permanent pixels. Time may affect only optional overlay opacity. Do not serialize cosmetic data or require save migration. Keep dungeon rendering independent.

Review real production viewport captures at unchanged gameplay zoom. Compare the same fixture, resolution and camera before/after. Reject pale continuous borders, cyan ribbons, per-tile sawtooth patterns, rectangular depth seams, excessive shimmer, lost islands, misleading banks, obstructed paths and mismatched pixel density. Automated topology/state checks complement visual review; passing them does not certify art.

## Acceptance references and four-part scope

`docs/overworld-screenshots/` contains baseline/final reference, shoreline, six viewport sizes, reduced motion, production seeds 1/42/1337 and synthetic edge cases. `OverworldVisualChecks.Fixture` is the canonical reversible production composition; it uses actual PPC actors, props and PR7 UI. It does not load ordinary saves. See `OVERWORLD_PART1_HANDOFF.md` for measured results and incomplete gates.

Part 2 extends ground ramps/transitions; Part 3 reworks environmental objects; Part 4 refines composition/atmosphere. None is implemented merely by establishing this standard.
