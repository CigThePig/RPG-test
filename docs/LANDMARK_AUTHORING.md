# Landmark authoring

`game/data/landmarks/rooms.json` is the production library. It is read through Godot FileAccess, included explicitly in all export presets and checked inside APKs. Unknown JSON properties, invalid fields, assets, themes and layouts fail content audit with the template ID. Restart the application after editing the process-cached library.

## Coordinates and schema

Templates are stamped onto a room's 9×9 core; rooms can be larger, with wings around the core (see [DUNGEON_LAYOUT.md](DUNGEON_LAYOUT.md)). The 9 strings describe core tiles **1..9** in an 11×11 frame. North is decreasing ground Y; frame origin (0,0) is the tile northwest of the core, stored as `LandmarkRoom.OriginX/OriginY`. Local (5,5) is the mission feature/interaction center. Tile centers are `(origin + local + .5) * 24` ground units. Rendering projects these through the existing PPC conversion; author no screen coordinates.

Each entry has a unique stable `Id`, positive `Revision`, `Name`, `Theme`, primary `Roles`, `DoorMasks`, quarter-turn `Rotations`, `Layout`, `Focal`, `ProtectedFeature` and `Anchors`. Supported roles are entrance/objective/key/chest/ordinary plus descriptive dead-end/junction/hub tags. Eligibility uses the **primary feature role**, so a dead-end key room stays a key room. Secondary tags are hooks for future content, not independent mission authority.

Example: a small ordinary chamber with one authored column and a floor mosaic:

```json
{
  "Id": "memorial-corner", "Revision": 1, "Name": "Memorial Corner",
  "Theme": "zone", "Roles": ["ordinary"],
  "DoorMasks": [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15],
  "Rotations": [0,1,2,3],
  "Layout": [".........", ".#.......", ".........", ".........", ".........", ".........", ".........", ".........", "........."],
  "Focal": [2,2], "ProtectedFeature": [5,5],
  "Anchors": [
    {"X":2,"Y":2,"Asset":"column","Category":"tall","Collision":"terrain","Priority":0,"Weight":100},
    {"X":4,"Y":4,"Asset":"mosaic","Category":"ground","Collision":"cosmetic","Priority":2,"Weight":75}
  ]
}
```

This example demonstrates the schema; it is not an additional shipped template. Add distinct spatial purpose, not just a palette variant.

## Geometry and doors

`.` means floor; `#` means an authoritative Wall. No other symbols are accepted. At most eight blockers are allowed. All blockers must be inside local 2..8 and outside **both** protected central strips X=4..6 and Y=4..6. The core perimeter X/Y=1/9 stays open, so corridors and wings joining any core side reach the center. Every blocker requires exactly one structural visual anchor. Required architectural anchors must have weight 100 and priority 0 or 1. Cosmetic assets cannot masquerade as solid furniture. Never move features to accommodate a composition.

A door bitmask uses N=1, E=2, S=4, W=8; a room's mask lists the sides with a corridor (`DungeonRoom.Doors`). A template supports a room if its canonical mask, rotated with the template, equals that room's actual mask. All shipped templates preserve the full cross and consequently support every nonempty mask. Narrower mask lists support future asymmetric authoring without changing the graph. Rotation `(x,y) → (10-y,x)` is clockwise in ground coordinates; masks, structures, anchors, focal and facing rotate together. The mission graph does not rotate. Facing is preserved as an orientation/content hook; the current square-footprint billboard artwork keeps its upper-left light fixed rather than mirroring the light ramp. Props do not create directional hitboxes.

After structural application, radius-11 circular swept movement must reach every floor center in the whole room shape, wings included, and all protected lanes. A rejected candidate rolls back; bounded selection tries remaining template/orientation candidates. The final fallback stores a `safe-open` room with unchanged terrain. Mission solvability is checked again. Structural budget admission happens in entrance → objective → key → chest → ordinary order; required structures never disappear due to later render budget pressure. Rejection logs identify seed, room, template, revision and turn. Large seed 1020 exercises budget fallback.

## Assets, themes and variation

Tall solid assets: `column`, `shelf`, `altar`, `storage`, `collapse`. Use `Collision: terrain`, `Category: tall` and one matching `#`. Their base covers the actual tile. No independent physics body is created. Existing Terrain/World governs movement, rolls, sight, melee visibility and projectile sweeps.

Cosmetic assets: `records`, `moss`, `mosaic`, `threshold`, `offering`. Use `Collision: cosmetic`, categories ground/low, and floor positions. These are flat debris, markings and minor offerings; no fake loot or interaction. Ground details render in one additional static mesh and remain beneath telegraphs. Each slot has `Weight` 1..100, `Priority` 0..4 and `Facing` 0..3. The resolved variant is 0..2. Structural nodes take precedence; optional ambient scatter fills the remaining 96-node budget. Ground slots are bounded to 24 per room and do not consume nodes.

`Theme` is weathered, overgrown, ceremonial or `zone`. Zone selects related weathered/overgrown treatment in coherent 2×2 logical-cell areas. Entrance forces weathered; shrine forces overgrown; objective forces ceremonial. Floor/wall material tints reuse the existing atlases. Floor motifs, moss and silhouettes make the variation visible beyond color. Add assets through EnvironmentArtGenerator/EnvironmentAssetCache, preserving upper-left light, integer pixels, 24-unit footprint and front ground-edge pivot. No per-room textures.

Selection uses its own integer hash/revision and stable ordinal template IDs. It consumes no mission RNG, clock, object IDs or unstable string hashes. Adding/editing a template changes only newly generated regions. Keep semantic meanings of existing asset IDs stable.

## Persistence and Part 4 hooks

`RegionState.Landmarks` contains resolved layout cell, frame origin, template ID/revision, orientation, door mask, primary/secondary roles, theme, focal and fully resolved anchors. Together with region ID this is stable identity. Terrain stays in the saved tile array, and room shapes in `RegionState.Rooms`. Saved rooms never reread current template definitions to rebuild geography or decorations. Unknown/corrupt anchors fail snapshot validation before restoration. Keep prop IDs stable when expanding the renderer.

For a landmark destination use `(room.OriginX+room.FocalX, room.OriginY+room.FocalY)`; **a focal can be a solid altar**. Obtain a nearby reachable stand through RegionState.FindPlacement/Path. Mission center features retain their existing indices and interaction ownership. Place future enemies through FindPlacement with real radii and occupancy; use normal World/QuestSystem/Gloot authority for encounters, quests and rewards. Neither a room role nor an anchor grants loot or spawns a duplicate objective.

## Verification and preview

After `dotnet build` and `python3 tools/import-project.py`:

```sh
godot --headless --path . -- --validate-landmarks
python3 tools/verify.py --headless
xvfb-run -a godot --path . --audio-driver Dummy -- --visual-check --landmarks-only --mobile-ui
xvfb-run -a godot --path . --audio-driver Dummy -- --visual-check --landmarks-only --landmark=forgotten-archive --orientation=1 --visual-seed=42
```

The focused suite captures five naturally occurring templates, all rooms of seeds 1/42/1337, adjacency, actual crossing, column front/back pixel comparisons and live combat using production views and PPC actors. Individual previews use an explicitly isolated authored fixture; do not confuse them with natural placement evidence. Snapshots are restored and no user slot is read/written. Individual previews include the player and real production prowler/brute/spitter actors. Add `--landmark-debug` to write a separate collision-overlay capture; clean acceptance screenshots remain separate. The ordinary Developer collision overlay also remains available in interactive gameplay. The full desktop/mobile suites include these checks and retain prior wall/tree tests. Gameplay replay and Android smoke remain separate required checks.
