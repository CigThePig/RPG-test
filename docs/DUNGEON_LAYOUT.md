# Dungeon layout: rooms, halls and corridors

Graph Dungeon Generator owns the mission grammar and the logical layout: which cells exist, which cells connect and which connections are locked. `DungeonLayout` (`game/world/DungeonLayout.cs`) turns that layout into terrain. Room sizes, wings, hall positions and corridor bends come from its own integer hash (`DungeonLayout.Hash`, revision `DungeonLayout.Revision`), so they never consume mission RNG draws and the same seed always produces the same ruin.

## Blocks

Each layout cell owns a `DungeonGeometry.Cell` × `Cell` block of tiles (17 × 17; tiles stay 24 ground units). The block's outer ring is solid except where a passage crosses it, so neighbouring blocks only meet at their passage. Unoccupied blocks stay solid.

## Rooms

A mission cell becomes a **room**: a 9 × 9 core plus up to three wings.

- The 9 × 9 core (216 × 216 units) is the minimum room. Its position inside the block varies. The mission feature (entrance, key, chest, relic) sits on the core center.
- A wing is a rectangle on one side of the core, at least two tiles deep and overlapping the core side by at least three tiles. Any overhang past the core is at least two tiles, so wings never leave one-tile slivers. Wings produce larger rectangles, L, T and cross shapes and alcoves.
- Some rooms stay a plain core; most gain wings. All room floor stays inside the block ring.

## Halls

Connection cells that the layout grammar adds only to route between mission rooms become **halls**: a bare 3 × 3 junction with no feature and no landmark. Corridors run into it from each connected side, so halls read as turning or branching hallways.

## Corridors and passages

Every reciprocal layout connection becomes one three-tile-wide corridor (72 units), recorded as a `DungeonPassage`.

- The corridor crosses the two ring rows between the blocks on a lane at least `CrossingMargin` (4) tiles from either block corner, so separate passages can never touch.
- On each side the corridor either runs straight from a flat three-tile door on the room edge, or turns once. A turn keeps one wall between the corridor and its room, and at least two straight tiles before the ring.
- A locked connection places its gate on the two ring rows across the full three-tile width: six `lock` tiles forming one isolated 2 × 3 gate group. One key opens the whole group.
- An unlocked connection that the layout grammar marks `LeverLock` or `PuzzleLock` records `DungeonPassage.Mechanism` (`lever` or `puzzle`) and later receives the same 2 × 3 footprint as a `portcullis` or `seal` gate; see `DUNGEON_MECHANISMS.md`.
- If a corridor cannot be routed, the two rooms it joins are simplified (plain core, then centered core) and the ruin is re-carved. Centered cores always admit a straight corridor, so carving always finishes.

Corridors and wings make the ruin larger than the old fixed grid. Every floor tile, corridor included, is reachable by the largest production footprint (radius 11).

## Saved state

`RegionState.Rooms` stores each block's kind, core origin, floor rectangles and door mask (N 1, E 2, S 4, W 8). `RegionState.Passages` stores each crossing and its grammar mechanism. `RegionState.Lit` stores burning braziers. Terrain remains the saved tile array. The region generator identity is `rpg-regions-2`; saves made with an earlier generator are rejected on load instead of being migrated.

## Landmarks and decoration

Landmark templates stay 9 × 9 and are stamped onto a room's core. `LandmarkRoom.OriginX/OriginY` is the absolute tile of the template frame (core minus one), and anchors are local to that frame. `Landmarks.RoomSafe` floods the whole room shape, wings included, with the radius-11 footprint. Halls never receive templates. Ambient rubble only appears inside rooms, away from the core's protected cross and perimeter lanes and away from room edges, so corridors and doorways stay visually clear. A corridor takes its block's room theme; halls follow the surrounding zone theme.

## Preview and verification

Print ASCII previews of generated ruins (`#` wall, `.` room, `,` corridor or hall, `=` key gate, `%` portcullis, `~` light seal, features by initial, e.g. `L` lever, `B` brazier). The header counts levers, seals and shortcuts:

```sh
godot --headless --path . -- --dump-dungeons=1,42,1337
```

`DungeonChecks.Geometry` validates every sampled ruin: passages sit on block boundaries away from corners and match room door masks; every other ring tile is solid (no accidental connections); every block's floor joins its room; room rectangles stay inside the ring; each room keeps its 9 × 9 core; halls are bare 3 × 3 junctions; interior walls only appear where landmark templates may place them; and every room passes `RoomSafe`. Across seeds 0–63 and 1337 the suite also requires varied room footprints, rooms with wings, halls, corridor turns and corridor floor.
