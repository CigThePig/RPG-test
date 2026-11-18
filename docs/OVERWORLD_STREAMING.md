# Streaming the expanded Living Reach (PR follow-up)

A new standard game now uses a **320 × 240 tile Reach**, 25 times the area of the
original 64 × 48 world. The generated six-house town, spawn, resource stands and
ruin entrance remain close to their original location. Reference and custom maps
can still call `Regions.Overworld(seed, 64, 48)`; existing saves retain their
saved width, height, tiles, features, houses and explored state.

## What is (and isn't) streamed

`RegionState` still generates its **76,800 authoritative terrain tiles upfront**.
This keeps collision, sight, pathfinding, dungeon transitions, deterministic
generation, and the existing JSON save schema unchanged. Tiles are compact and
do not create scene nodes.

`OverworldChunkStreamer` builds only nearby **32 × 32 tile presentation
chunks** and evicts those outside the camera plus an overscan margin. The
ground/water/rock lookups include a three-tile sample halo, but draw only their
own core rectangle. Shader sampling uses absolute world coordinates with
per-chunk lookup origins so procedural materials line up across boundaries.
Decoration and flora are planned per chunk from stable world hashes, not from
gameplay RNG. House roofs, paving and landmarks continue using the existing
shared actor sorting and save state.

The streamer prepares lookup bytes on a managed worker and schedules one upload or
construction stage per frame under a measured main-thread budget. Eight hidden
chunks are retained for reversals; eviction releases nodes incrementally. See
[LIVING_REACH_OVERHAUL.md](LIVING_REACH_OVERHAUL.md) for the current pipeline. Distant scenery remains
unloaded, including its textures, meshes, decoration nodes and plant rows.
Full-region collision and the eight authored starting NPCs remain active.
This is **presentation streaming**, not full simulation or per-chunk save
streaming; those would be a separate architecture milestone.

## Acceptance and device review

- `--test-only=OverworldChunkChecks`: size, determinism, town, near-world
  portal, full chunk coverage, partial edge, viewport bounds and movement
- Run headless, rendered/mobile, both adventure runs and Android smoke via CI
- On Galaxy S25: walk across several boundaries and revisit the town, checking
  shoreline seams, shadows, roof fade, enemy interactions, save/load and reduced
  motion. Monitor p95 frame time and chunk-creation spikes, not just FPS.
- The chunk streamer is intentionally not used for small reference maps or
  dungeons. No legacy save conversion is performed.

Known future work: stream authoritative terrain and NPC simulation, add more
settlements/landmarks throughout the new wilderness, and use compact chunk
delta saves if the terrain or gameplay eventually becomes much larger.
