# Town kit: block houses and street furniture

Towns for the Living Reach, built so the player walks straight into a house on the same map. A house is made
of blocks: a ring of ordinary wall tiles round a floor, with a doorway. Its roof covers the room from outside
and fades away while the wayfarer is inside. Street furniture, furnishings and cobbled paving are painted with
the Reach's own painter and palette.

The kit now has a seeded town generator and saved composition, described below. New standard Reach
worlds contain a furnished roadside town; no new inhabitant or shop systems are added.

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

Carving changes the region's tiles, which are saved like any other terrain. Generated plans (styles, roofs, furniture and paving) are saved through `RegionState.Town`;
hand-authored previews can still override them through `TownStage`. A staged plan keeps wild trees, shrubs and flora off its paving and
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

## Seeded towns (generator follow-up)

New standard Reach worlds now call `TownGenerator.Install` after placing the road and its interaction
features. A private, hashed seed stream varies house widths, depths, plot offsets, door positions, side-plot
styles and furniture variants. Six plots provide a cottage, stone house, inn, smithy, chapel and barn. The
inn overlooks a small market with stalls, a well, notice board, benches and lamps. Furnishings reflect each
building's purpose. Three-tile approaches and a southern return lane join every south-facing door to the
central street. House rows leave at least four clear tiles for the roof overhang. Town furniture retains the
kit's noncolliding behaviour; these buildings do not add shop or inn gameplay services.

Only house parcels, their verges and streets are cleared. The original spawn, chest, harvest nodes and ruin
portal stay in place; the original route continues east into wilderness. Wardens begin by the street, while
prowlers begin beyond the eastern plots. Custom maps smaller than 48 by 44 tiles retain the original
roadside clearing. Generation declines a layout if a custom feature occupies a house footprint.

`RegionState.Town` holds a `TownState` with the houses, furniture and paved tile indices. `TownStage.For`
uses a staged reference plan when present, otherwise this saved composition. Saved terrain remains the
collision and sight authority: loading never re-carves a town. Missing town data is valid, so existing saves
keep their old geography; start a new game to see a generated town. Invalid town bounds, styles, furniture,
paving or disagreement with saved house terrain are rejected before live state changes. Installing twice is
a no-op. Town RNG is independent of simulation RNG.

`TownGenerationChecks` covers repeated seeds, varied layouts, small and larger maps, reachable interiors,
sealed-door exclusion, feature access, roof-row spacing, idempotent installation, production save/load,
invalid saves and the legacy no-town case. `--town-only` also walks through every generated door and back
out on seeds 1, 42 and 1337, checking that exactly the entered roof opens. It writes phone-sized street,
overview and interior captures to `docs/generated-town-screenshots/`; CI retains those with the town kit
captures.

Generator verification on Godot 4.6.1 .NET: `dotnet build` succeeded; the full headless suite passed
16,207 assertions; focused town suites passed 545 assertions with the maximum production radius; the
rendered town suite passed all 11 reference houses and 18 generated door entries/exits; the normal-health
adventure replay completed both quests (12,698 ticks, 68 health remaining). Review images:
[generated town overview](generated-town-screenshots/seed-42-overview.jpg) and
[inside the inn](generated-town-screenshots/inside-inn.jpg). Full-resolution PNGs are emitted by the rendered
check and retained in CI's evidence artifact. The new town has not been tested on the physical S25.
