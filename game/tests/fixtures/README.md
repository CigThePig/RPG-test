# PR4 compatibility fixtures

Source: `9eace0f1db3e4f2c7e5cc94cd2474eaa612cfe1b` (unmodified production code in a detached worktree).
`docs/dungeon-evidence/baseline-capture.cs.txt` records the temporary test instrumentation used to create these fixtures. No user slot was loaded or written.

- `pr4-dungeon-save.json`: full schema-1 production snapshot, seed-42 legacy ruin, collected key, fully opened old gate, depleted chest, living and defeated seeded inhabitants, player inventory/equipment and accepted quest progress. Seed 42 has no puzzle chest, so the capture adds a chest on an existing interior tile, initializes it through production container setup and loots it. PR4's own restore/capture normalizes Godot numeric JSON formatting before saving the fixture. It is a synthetic test save made by the old executable, not an actual user's save.
- `pr4-seed42-region.json`: exact original generator output, without the fixture mutations; used for equal-zoom rendered comparisons.
- `pr4-topology.json`: old cell dimensions, occupied cells, mission-role centers and four cardinal boundary states for seeds 0–63 and 1337. Expanded geometry must reproduce this logical signature exactly. This catches accidental graph connections as well as missing connections.

`pr5-part2-dungeon-save.json` was captured on 2026-10-30 from pre-Part3 commit a080e538c080be8204b8d4ecb86205324ab5e1a5 using its existing DungeonChecks.SaveChecks fresh expanded-dungeon snapshot. Only a test-only File.WriteAllText was added in a detached checkout to export the existing captured string; production code was unchanged. The detached build and all 4,609 baseline assertions passed. This verifies pre-landmark 11-cell terrain and state without new metadata; Part1/Part2 share this authoritative region schema. PR4's separate original narrow-dungeon fixture remains unchanged.
