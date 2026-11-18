# Living Reach world overhaul

New 320×240 worlds use geography revision 1, without changing `rpg-regions-2` or
save schema 1. A north–south valley channel has broad seeded bends, slowly varying
width, two irregular basins, an eastern tributary and a separate small pond.
Rock ridges frame the valley. Smooth canopy fields compose clearings and forest
cover, and a short local road branches into woodland trails. Saved three-tile
bridge decks allow walkers across the river and tributary while retaining water below it.

Large new towns use composition revision 2: cottages start at 10×9, stone houses
11×10, inns 17×12, smithies 13×10, chapels 15×14 and barns 16×13, with seeded
variation. Wider plots, two/three-tile doors and a return street connect buildings.
Furniture leaves an entrance apron and central circulation aisle. Existing
shader roofs, wall cutaways and Y sorting operate on the larger real footprints.

Small maps keep revision-1 towns. Saved terrain and houses are loaded verbatim;
neither town nor geography generation runs during restore. Missing optional
`GeographyRevision` and `Crossings` fields default to old-world behavior. Existing
large saves receive streaming improvements while keeping their old geography.
Start a new game in another slot to see the new terrain and town.

## Streaming

One cancellable worker prepares normalized ground/water/rock lookup bytes against
an immutable tile snapshot. No Image, texture, RenderingServer or scene-tree API
runs on that worker. Sampling uses the same global coordinates and three-tile halo
as reference rendering. Managed scalar/Color/Rect structs and pure hash helpers
are safe there; feature switches are initialized first on the main thread.

Camera-visible chunks outrank an additional movement buffer. Main-thread work
uses a 2 ms scheduling budget and at most one construction stage per frame:
terrain, ground upload/mesh, rock upload/mesh, water upload/mesh, decorations, flora.
An indivisible upload can exceed that scheduling budget; the budget is not a
promise about total frame time. Completed chunks activate together. Eight hidden
in-tree chunks retain rendering resources for reversals; distant nodes release
incrementally within the same budget. A merged low-detail terrain mesh prevents
black holes on cold startup/teleport while detailed scenery catches up. Collision
and gameplay always use fully resident RegionState terrain.

Streaming traces identify the chunk and record preparation, image copies,
texture submissions, mesh uploads, activation, deactivation and eviction separately.
Frame traces include managed memory, texture memory and resident mesh/vertex counts.
Upload timings measure the main-thread submission cost, not GPU execution time.

The Developer performance overlay includes current/worst streaming CPU time,
active/cache/prepared counts, estimated lookup bytes and reuse hits. To compare
schedules, `tools/living-reach-verify.py` runs the frozen synchronous resource path
(test-only) and staged path against identical geography and travel. It writes
real Godot screenshots for three whole worlds, river/lake/pond/confluence/town,
six exteriors and interiors, per-frame timings and a real-time desktop MP4.
The CI `living-reach-evidence` artifact contains this evidence. Desktop timing
includes software rasterization; it does not establish Galaxy S25 performance.

## Device check

Install the ARM64 development APK. In Menu → Developer, enable the performance
overlay. Start a new seed-42 world, explore east from town, cross and recross both
waterway bridges, move diagonally, return to town, enter/exit the dungeon, save/load
away from town, and repeat with Reduced motion. Record the overlay while moving;
report p50/p95/p99, worst streaming time and whether any freezes or low-detail
catch-up are visible. The engineering target is 60+ FPS and no ordinary transition
frame over 33.3 ms; only a physical device can establish that result.
