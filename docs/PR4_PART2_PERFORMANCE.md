# Part 2 local rendering comparison

These are short development measurements on an Intel Xeon Platinum 8573C using Godot 4.6.1 .NET and Mesa llvmpipe, not Android or physical-phone timings. Both desktop runs used the same normal-time, 120-rendered-frame fixture with the same four visible creatures. The baseline was a detached Part 1 checkout at `c8d85c8a73a59874b922381700519cb67837606d`, with only the same profiling harness added for measurement. The Part 2 sample was taken from the working implementation before its final commit. Frame times include host scheduling and rendering, and 120 frames can cover different amounts of wall time in different runs.

| Workload | Part 1 p50 / p95 / p99 ms | Part 2 p50 / p95 / p99 ms | PPC uploads, Part 1 → Part 2 | PPC raster-batch p95 ms, Part 1 → Part 2 |
|---|---:|---:|---:|---:|
| Idle | 27.74 / 42.08 / 50.09 | 26.89 / 40.12 / 43.44 | 192 → 183 | 4.77 → 4.93 |
| One attack | 27.01 / 43.72 / 47.79 | 27.18 / 44.27 / 51.19 | 190 → 244 | 5.52 → 4.35 |
| One repeated hit reaction | 26.12 / 40.03 / 47.58 | 27.22 / 165.23 / 201.38 | 196 → 237 | 6.79 → 5.65 |
| Four attackers | 24.98 / 34.74 / 35.72 | 29.77 / 55.39 / 83.71 | 172 → 447 | 4.23 → 7.82 |
| Four simultaneous hits | 25.78 / 39.28 / 42.98 | 25.80 / 43.87 / 52.68 | 232 → 303 | 4.93 → 4.50 |

The four-attacker sample shows the expected cost of sampling active poses more frequently. Main-thread allocated bytes over the five Part 1 samples were approximately 1.51, 0.87, 0.86, 2.21 and 0.97 MB; Part 2 recorded 0.86, 0.97, 2.59, 1.20 and 1.07 MB. These totals include the harness and runtime activity. Managed memory at the start/end of the Part 1 samples ranged from 25.6 to 34.2 MB; Part 2 ranged from 38.1 to 46.4 MB. These are live managed-heap observations without a controlled collection or native texture accounting, not a leak or total-memory measurement.

The unusually high Part 2 desktop hit-reaction tail was not reproduced in a later mobile-layout run: that one-hit sample was 23.42 / 32.64 / 38.39 ms, with a 3.46 ms raster-batch p95, no observed canvas growth, and at most one active effect. Its five slowest frames were 32.8–39.5 ms; the measured test-drive work on those frames was at most 0.2 ms. A four-hit sample in that run peaked at four active effects, with a 5.37 ms raster-batch p95 and one 100.6 ms frame outside a sampled raster/upload batch. The cause of the earlier desktop outliers is not established. These short and variable software-renderer samples do not establish sustained performance or audible/subjective quality on a phone; rerun on target hardware before drawing that conclusion.

`VisualChecks` now records normal-time frame percentiles, raster and upload counts/timings, managed memory, main-thread allocations, effect peaks, canvas growth and the slowest frames in `render-verification*.json`. Movie Maker replay wall time is deliberately excluded from this comparison.
