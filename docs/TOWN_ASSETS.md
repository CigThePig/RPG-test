# Town asset kit

Buildings, street furniture and cobbled paving for towns in the Living Reach, painted with the Reach's own
painter and palette so a town sits in the meadow instead of on top of it. This is an asset kit and a way to
stage it; town generation, collision, interiors and inhabitants do not exist yet.

Reference captures of a hand-laid market town are in `docs/town-screenshots/` (`square`, `west`, `east`,
`south-west`, `south-east`, `gate`, `overview`, and both atlases).

## Modules

| Buildings (192x168 cell, foot at 96,160) | Front x depth in tiles |
| --- | --- |
| Cottage: thatch and limewash, thatch and plank, tile and timber | 4 x 3 |
| Stone house: clay tile or slate, with a dormer and shutters | 4 x 3 |
| Inn: stone ground floor, jettied timber upper storey, sign and lanterns | 6 x 4 |
| Smithy: stone shop with an open forge lean-to, anvil and quench tub | 5 x 3 |
| Chapel: gable front, rose window, bell-cote | 3 x 4 |
| Barn: plank gable, great doors, hay loft | 4 x 4 |
| Shop: timber front with a glazed window under a striped awning | 4 x 3 |
| Watchtower: parapet, slate cap and pennant | 2 x 2 |

Street furniture (standard 72x104 cell, foot at 36,100): well, market stall (produce, cloth, pots), lamp post,
signpost, notice board, fence run, fence side, gate, low stone wall, hay cart, hay bale and stack, bench,
barrels, crates, sacks, planters, woodpile, fountain, statue, washing line, scarecrow, banner pole, and flat
garden plots (tilled rows, flower bed).

Fence runs, walls and garden plots fill their tile edge to edge, so neighbours join into one run.

## Using it

```csharp
var plan=new TownPlan()
    .Pave(23,20,19,7)                        // cobbles over a tile rectangle
    .Add(TownBuilding.Inn,32,18)             // centre tile of the building's front row
    .Add(TownProp.Stall,26,22,variant:1);    // its own tile
TownStage.Stage(region.Id,plan);             // then rebuild the region's views
```

- `TownView` puts each standing module in the actor Y-sort layer at its ground contact, so characters pass in
  front of and behind it. `TownPaving` draws cobbles and flat plots in the ground layer; the cobbles break up
  into loose setts and turf at the edge of the paved set.
- A staged plan keeps wild trees, shrubs and flora off its paving, furniture and buildings, with a clear tile
  round each building and two before its door.
- `TownPlan.Footprint` and `TownArt.Footprint` give the tiles a module stands on, for whoever adds collision.

A plan is presentation only. It is not saved, has no collision and is never read by the simulation. `TownStage`
is a stand-in until regions can carry a town; nothing in the shipped game stages one.

## Extending it

Paint new modules in `TownArt` with `OverworldPropArt.Pixels`: light from the upper-left, one contour, a ground
shadow thrown down-right, the outer ring of the cell left transparent. Palette indices 72-94 hold the roof
ramps (clay tile, slate, thatch), limewash, awning colours and window glass. Keep a building's walls inside the
front width it declares in `Footprint`.

## Review and verification

```sh
godot --path . -- --visual-check --mobile-ui --town-preview [--preview-clean]   # build/town-preview/, asserts nothing
godot --path . -- --visual-check --mobile-ui --town-only                        # rendered check, rewrites docs/town-screenshots/
python3 tools/verify.py --headless --test-only=TownArtChecks
```

`TownArtChecks` covers every module (painted, indexed, repeatable, gutter-safe), building footprints and
light direction, edge-to-edge runs, the reference town (dry ground, no overlapping buildings, every module
kind shown), wild growth staying off town ground, and that staging changes no serialized geography. The
rendered check requires the staged town to appear and the meadow to come back when it is cleared.
