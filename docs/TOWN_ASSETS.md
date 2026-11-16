# Town kit: block houses and street furniture

Towns for the Living Reach, built so the player walks straight into a house on the same map. A house is made
of blocks: a ring of ordinary wall tiles round a floor, with a doorway. Its roof covers the room from outside
and fades away while the wayfarer is inside. Street furniture, furnishings and cobbled paving are painted with
the Reach's own painter and palette.

This is a kit and a way to stage it. Town generation, saved towns and inhabitants do not exist yet, and
nothing in the shipped game places a town.

Reference captures of a hand-laid market town are in `docs/town-screenshots/`: `square`, `west`, `east`,
`south`, `overview`, and `inside-inn`, `inside-chapel`, `inside-cottage`, `inside-smithy`, `inside-barn` with
the roof open, plus the furniture atlas.

## Block houses

| Style | Walls | Roof | Floor |
| --- | --- | --- | --- |
| Cottage | Limewashed timber frame | Thatch | Boards |
| Stone | Stone | Clay tile | Boards |
| Inn | Limewashed timber frame | Slate | Boards |
| Smithy | Stone | Clay tile | Flagstones |
| Chapel | Stone | Slate | Flagstones |
| Barn | Plank | Thatch | Packed earth and straw |

A house is `TownHouse(Style, X, Y, Width, Height, Door, DoorWidth)`: any rectangle of at least 5 x 5 tiles,
with the doorway in its south wall. `TownPlan.Carve` writes it into the region as `Terrain.Wall` and
`Terrain.Floor`, so collision, line of sight and pathing work with no town-specific rules: walls block, the
doorway is the only way in.

- **Walls** reuse `TerrainWallRow` (the dungeon's Y-sorted wall rows): a wall head and, where open ground lies
  to the south, a 16-pixel face. South faces of the outer wall carry windows, some lit.
- **Floors** are drawn per tile in the ground layer, shaded under the north and west walls, with a stone
  threshold in the doorway.
- **Roof.** `TownRoof` is a hipped roof seen from above, standing `Loft` (20) pixels over the wall heads. It
  sits in the actor layer at the house's front edge: characters in front of the house stand before it, and
  everything inside is under it. From outside the doorway shows as a dark opening with the door standing open.
- **Entering.** When the wayfarer's tile is inside the house rectangle (doorway included) that house's roof,
  chimney and doorway shadow fade out over about a fifth of a second, and fade back on leaving. Other roofs
  stay shut. Reduced motion switches at once.

Leave four clear rows between the back of one house and the front of the next, or the nearer roof will stand
over the further house's front wall.

## Furniture

Street: well, market stall (produce, cloth, pots), lamp post, signpost, trade sign (inn, smith, shop), notice
board, fence run, fence side, gate, low stone wall, hay cart, hay bale and stack, bench, barrels, crates,
sacks, planters, woodpile, fountain, statue, washing line, scarecrow, banner pole, flat garden plots.

Indoors: bed (two colours), table with stools, hearth with a fire, shop counter, bookcase, anvil, pew, flat
rugs (two colours). Furniture does not collide.

Fence runs, walls, garden plots and rugs fill their tile edge to edge, so neighbours join into one run.

## Using it

```csharp
var plan=new TownPlan()
    .Pave(23,17,19,10)                               // cobbles over a tile rectangle
    .Build(HouseStyle.Inn,27,9,11,7,door:5)          // north-west wall tile, size, door offset along the south wall
    .Add(TownProp.Hearth,35,10)                      // furniture by tile, indoors or out
    .Add(TownProp.Stall,26,19,variant:1);
plan.Carve(region);                                  // houses become wall and floor tiles
TownStage.Stage(region.Id,plan);                     // then rebuild the region's views
```

Carving changes the region's tiles, which are saved like any other terrain. The plan itself (styles, roofs,
furniture, paving) is presentation: it is not saved and is never read by the simulation. `TownStage` is a
stand-in until regions can carry a town. A staged plan keeps wild trees, shrubs and flora off its paving and
furniture, with a clear tile round each house and two before its door.

## Extending it

Paint new furniture in `TownArt` with `OverworldPropArt.Pixels`: light from the upper-left, one contour, the
outer ring of the cell left transparent. New wall, roof or floor materials go in `town_wall.gdshader`,
`town_roof.gdshader` and `town_floor.gdshader` with their colours in `TownBlocks.Ramp`.

## Review and verification

```sh
godot --path . -- --visual-check --mobile-ui --town-preview [--preview-clean]   # build/town-preview/, asserts nothing
godot --path . -- --visual-check --mobile-ui --town-only                        # rendered check, rewrites docs/town-screenshots/
python3 tools/verify.py --headless --test-only=TownArtChecks
```

`TownArtChecks` covers every furniture module, the shape of every carved house, that the largest production
body can path from the street into each room and cannot once the doorway is walled up, window placement,
furniture standing on open tiles, wild growth staying off town ground, and that staging and drawing a town
changes no serialized geography. The rendered check walks the wayfarer through each of the eleven reference
doors on the real collision and requires that house's roof to be shut from the street, open within, the room
visible, and every other roof still shut.

The fade itself was checked only at its two ends; time is frozen in the checks, so the transition has not been
watched in motion.
