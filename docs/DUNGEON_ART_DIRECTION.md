# The Sunken Halls — Mosslight dungeon art standard

The ruins are painted in the same manner as [the Living Reach](OVERWORLD_PART4_HANDOFF.md): shader-painted
surfaces chosen from absolute canvas pixels, props as lit volumes with one dark contour and a cast shadow,
one indexed palette, upper-left light, and a little life and motion that stops under reduced motion. A ruin
should read as a place under the same world, not a separate tileset.

This replaces the PR5 floor, wall and prop atlases and their per-theme colour tints. Geography, collision,
gates, mechanisms, landmarks, saves and world RNG are untouched; art never writes `RegionState`.

Reference captures are in `docs/dungeon-screenshots/` (`entrance`, `weathered`, `overgrown`, `ceremonial`,
`lock`, `lever`, `portcullis`, `relic`, `braziers-cold`, `braziers-burning`, `reduced-motion` and four window
sizes; the phone layout has a `mobile-` prefix). The updated captures in `docs/native-screenshots/` and
`docs/landmark-screenshots/` show the same renderer through the older suites.

## What is on screen

| Element | Treatment |
| --- | --- |
| Solid wall | Seen from above it is dark packed fill, so rooms stand out of the black. A ring of lit coping stones follows every edge, bright toward the upper-left, with a dark outline where it overhangs the floor behind |
| Wall face | Where floor lies to the south: two courses of large dressed blocks over a plinth, lit quoins where the wall ends, shade in inside corners, fallen and cracked blocks. Weathered faces run with damp, overgrown faces carry hanging ivy and climbing moss, ceremonial faces a gilt band |
| Floor | Staggered flagstones of mixed sizes with chiselled lips, chipped corners and cracked slabs; whole slabs take damp stains and wear. Corridors and halls are small cobbles, so routes read at a glance. Walls throw shade down-right onto the floor |
| Weathered halls | Blue slate, damp joints, still pools with a lit rim and slow glints |
| Overgrown halls | Moss carpets with blades that lean in a draught, small flowers, mossed coping |
| Ceremonial halls | Warm sandstone tiles in two tones inside bordered panels, gold studs and worn inlay |
| Zone seams | The theme of a block blends into its neighbour along noise instead of a straight tint edge |
| Light | Darkness is multiplied over the whole scene. Wall torches, burning braziers, the relic, the stair out, intact seals, altar candles and glowcaps open it; so does the wayfarer's own lantern. Light stops at masonry and steps down in dithered bands |
| Structural props | Column, bookcase, altar, stores and rubble heap, three wear variants in each of the three themes. Templates author one variant; coordinates pick the wear |
| Features | Chest (the Reach's, shut and open), key, relic and its empty pedestal, lever up and down, brazier with a four-frame flame (ward-blue once its seal breaks), stair and arch out, key gate staves with one lock plate, portcullis grille and raised teeth, light seal |
| Dressing | Torches and banners on straight runs of wall face; bones, pots, rubble, growth, debris and candles at the foot of the walls; dust in the lamplight |

## How it is built

- **Lookups.** `DungeonSurface` owns two textures per region: a one-third-resolution RGBA8 field
  (`(width*8+1) x (height*4+1)`: signed ground distance to the nearest wall or floor, and three noise octaves)
  and one texel per tile (theme weights and whether the tile is room floor). `dungeon_floor.gdshader` and
  `dungeon_wall.gdshader` read them through `dungeon_common.gdshaderinc` and the 36-colour `DungeonArt.Ramp`.
- **Masonry.** `DungeonArt.Masonry` is every wall tile except those holding a landmark's structural prop.
  Those tiles still block; the prop stands on open floor instead of on a block of wall.
- **Floor.** `DungeonGroundRenderer` (ground layer) draws one dark fill for the region, merged runs of open
  floor with the floor shader, and the landmark floor details as flat atlas sprites.
- **Walls.** `TerrainWallRow` is unchanged in structure and depth: one node per wall row in the actor Y-sort
  parent, anchored at the front ground edge, `Rise` 16. Each masonry tile emits a top quad; a face quad is
  added only where floor lies to the south. Vertex colour tells the shader top from face and how the face ends.
  Wall art never extends outside its own tile columns, and floor shade lives in the floor shader, so the
  doorway checks in `WallDepthChecks` still hold.
- **Atlas.** `DungeonPropArt` paints 111 fixed modules into one process-lifetime 864x1040 atlas, using the
  Reach's `OverworldPropArt.Pixels` painter, 72x104 cell and palette (indices 41-71 were added for iron,
  flame, sandstone, ward light, bone, cloth, bindings, clay and daylight). `EnvironmentDecoration` draws the
  structural props, `FeatureView` the features, `WallFixtures` the torches and banners, `DungeonClutter` the
  loose items.
- **Dressing.** `DungeonDressing.Fixtures` and `.Plan` choose placements from coordinates alone. Loose items
  stay inside rooms, clear of corridor mouths, features and the arrival tile, off the row hidden behind a south
  wall, and sort in front of the wall behind them. They never collide and are not saved.
- **Light.** `DungeonLight.Lamps` lists every lamp from saved state and the dressing plan; `Bake` writes an
  RGB8 light map at lookup resolution, stopping each lamp at masonry. `dungeon_light.gdshader` multiplies the
  finished world by ambient plus that light plus the lantern. The map is rebaked only when a brazier, relic,
  key or gate changes state.
- **One clock.** The ruins use the Reach's `ReachAmbience` clock for flames, firelight, pools, draught and
  dust. Reduced motion pins it to zero: one still frame.

## Rules for extending it

1. Read `RegionState`; never write it from art. No `Random`, clock or string hash may choose permanent pixels:
   use `EnvironmentStyle.Hash(seed,x,y,slot)`.
2. Paint new props in `DungeonPropArt` with the shared painter and palette. Keep the outer ring of the cell
   transparent, the foot at (36,100), light from the upper-left and one contour.
3. Anything that looks solid must be solid. Loose dressing stays low and small.
4. Bright amber and ward-cyan belong to things the player can use or must notice.
5. Keep wall art inside its tile columns and floor shading in the floor shader.
6. Motion goes through `ReachAmbience` so reduced motion can hold it.

## Review and verification

```sh
godot --path . -- --visual-check --mobile-ui --dungeon-preview [--preview-clean]   # build/dungeon-preview/, asserts nothing
godot --path . -- --visual-check --mobile-ui --dungeon-art-only                    # references + rendered checks
godot --path . -- --visual-check --mobile-ui --environment-only                    # doorway, depth, rebuilds, art
python3 tools/verify.py --headless --test-only=EnvironmentArtChecks,LandmarkChecks
```

`EnvironmentArtChecks` covers the atlas (every module painted, indexed, repeatable, gutter-safe), masonry and
the signed distance field, fixture and clutter placement rules, lamp bounds, a repeatable light bake that is
dark inside solid wall and bright under a torch, and that none of it changes serialized geography or the
simulation. `DungeonArtVisualChecks` renders the three themes and each mechanism, requires solid wall to be
darker than lit floor, requires burning braziers to brighten the floor from saved state alone, and requires
flames and firelight to move when motion is allowed and to be pixel-identical when it is not.

The look was reviewed in the software renderer at 1280x800 and four other window sizes. Motion was checked by
comparing frames, not watched at speed, and nothing here has been seen on the Galaxy S25.
