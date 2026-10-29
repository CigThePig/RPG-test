# Part 4 performance measurements

Final tested runtime/toolchain commit: `c7c4d3293f5008312e30353d0199e5df34d8582a`. Godot 4.6.1 .NET; SDK 9.0.203 selected with SDK roll-forward disabled. [Exact-source native artifact](https://github.com/CigThePig/RPG-test/actions/runs/40858363449/artifacts/12891025574).

The following are 120 normal-time rendered frames per workload under the actual mobile layout on GitHub Actions Ubuntu with **llvmpipe (LLVM 20.1.2, 256 bits)**. They are neither Movie Maker timings nor physical Galaxy S25 results. Hosted CPU/GPU scheduling varies; comparisons across runs are not controlled optimizations or established regressions.

| Workload | Frame p50 / p95 / p99 ms | Simulation p95 ms | AI p95 ms | Avoidance p95 ms | Projectile p95 ms |
|---|---:|---:|---:|---:|---:|
| idle | 22.13 / 23.18 / 24.07 | 0.081 | 0.000 | 0.011 | 0.000 |
| one engaged | 22.74 / 24.72 / 28.69 | 1.441 | 1.372 | 0.028 | 0.000 |
| three archetypes | 24.36 / 27.02 / 28.08 | 1.496 | 1.410 | 0.047 | 0.027 |
| four enemies | 24.79 / 27.23 / 28.15 | 1.436 | 1.363 | 0.047 | 0.006 |
| eight enemies | 26.95 / 30.34 / 35.08 | 1.471 | 1.332 | 0.068 | 0.006 |
| projectiles | 23.97 / 26.81 / 27.74 | 1.466 | 1.382 | 0.044 | 0.057 |
| projectiles and effects | 25.19 / 27.31 / 28.81 | 1.470 | 1.369 | 0.044 | 0.045 |

Subsystem numbers are the most recent simulation tick seen by a render frame, not a census of all ticks. AI includes navigation; do not add its navigation submeasurement again. The raw report retains PPC raster/upload counters, all original Part 2 workloads and slow-frame observations.

| Workload | PPC rasters / uploads | Main-thread allocated MiB | Managed heap start → end MiB | Peak effects / projectiles |
|---|---:|---:|---:|---:|
| idle | 44 / 44 | 0.81 | 16.50 → 17.31 | 0 / 0 |
| one engaged | 100 / 100 | 0.98 | 19.02 → 20.03 | 0 / 0 |
| three archetypes | 178 / 178 | 1.33 | 16.34 → 17.72 | 1 / 1 |
| four enemies | 231 / 231 | 1.50 | 22.95 → 24.51 | 1 / 1 |
| eight enemies | 430 / 430 | 3.63 | 29.86 → 33.60 | 2 / 2 |
| projectiles | 176 / 176 | 1.43 | 19.07 → 20.54 | 1 / 5 |
| projectiles and effects | 293 / 293 | 1.66 | 15.01 → 16.74 | 5 / 5 |

Managed observations include harness, view rebuilds, GDScript bridges, UI and tracing. Collection timing is uncontrolled. They are not total process/GPU memory or proof of a long-session leak. The eight-enemy sample is a stress workload; ordinary phone pacing still requires direct ARM64 measurement. No 60 FPS Galaxy S25 certification is claimed.

## Baseline interpretation

The Part 3 report remains the historical reference. An additional same-host, same short-fixture local mobile comparison before/after the initial Part 4 code measured p95: idle 19.26 → 18.48 ms, one engaged 17.45 → 18.89, three 27.20 → 19.86, four 18.76 → 18.87, eight 20.02 → 21.28, projectiles 19.02 → 23.66, combined effects 28.14 → 21.62. Projectile collision was 0.080 → 0.086 ms. The baseline assembly was built from the Part 3 handoff before recompiling Part 4; the later sample preceded final button-state styling/HUD edits. These are short diagnostic checkpoints, not final-source performance certification or evidence of a speedup. Final exact-source measurements above supersede them for the delivered APK.

Control geometry/style resources are built on creation/resize, not each frame. Contextual Interact queries are throttled to ten Hz; normal tactical cadence, pooled effects/audio and bounded PPC action sampling remain. The locator uses a bounded nearby-body scan and only draws under crowding. No timing, animation quality, collision accuracy or telegraph readability was removed for these numbers.

## Remaining measurement limits

The existing rendered performance harness reuses the same rigs for equivalent workload comparisons; separate tactical replays and the Android lab render the actual authored bodies. Single Brute/Spitter behaviour is exercised there, rather than presented as independent real-time microbenchmarks. Native and replay checks are repeated, but they do not establish a sustained ARM64 thermal, GPU-memory or long-session leak result. Use the 15–20 minute Galaxy S25 checklist and exported trace before merge. Audio-resource tests use a dummy driver; listening quality remains manual acceptance.
