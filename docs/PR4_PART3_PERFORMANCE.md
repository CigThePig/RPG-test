# Part 3 performance and verification

Measured checkpoints: baseline `3fdb480c5ae5f27ebd781abea33c13947c770ba0`, Part 3 `c0e76584d04fe3e3ada89ad48f7f17a9b42e1eab`. GitHub Actions Ubuntu llvmpipe software GPU. Each original fixture uses the same four visible rigs and 120 normal-time frames; hosted runner scheduling/hardware is not controlled across jobs. These samples show no observed frame-time regression, not a demonstrated optimization or physical-phone result.

| Same Part 2 workload | Baseline p50 / p95 / p99 ms | Part 3 p50 / p95 / p99 ms |
|---|---:|---:|
| idle | 22.74 / 26.31 / 27.14 | 16.71 / 19.90 / 21.41 |
| one attack | 24.09 / 26.21 / 26.53 | 17.65 / 19.85 / 20.23 |
| one hit reaction | 23.97 / 26.46 / 27.06 | 17.61 / 19.98 / 20.69 |
| four attackers | 25.28 / 28.06 / 29.83 | 18.63 / 20.67 / 21.33 |
| four simultaneous hits | 24.17 / 29.72 / 30.36 | 18.59 / 21.19 / 23.24 |

[Baseline native evidence](https://github.com/CigThePig/RPG-test/actions/runs/40794339418/artifacts/12862529431), [Part 3 native evidence](https://github.com/CigThePig/RPG-test/actions/runs/40815820104/artifacts/12871864644). Both desktop/mobile rendered suites passed at the Part 3 checkpoint.

| New workload | Frame p50 / p95 / p99 ms | AI p95 ms | Avoidance p95 ms | Projectile p95 ms | PPC uploads | Allocated MB |
|---|---:|---:|---:|---:|---:|---:|
| idle | 16.33 / 18.15 / 23.03 | 0.000 | 0.009 | 0.000 | 39 | 0.55 |
| one engaged | 16.72 / 17.60 / 18.37 | 1.057 | 0.023 | 0.000 | 82 | 0.66 |
| three archetypes | 17.86 / 19.60 / 20.59 | 1.149 | 0.032 | 0.026 | 165 | 0.93 |
| four enemies | 17.95 / 20.00 / 21.36 | 1.193 | 0.034 | 0.009 | 209 | 1.03 |
| eight enemies | 19.00 / 23.47 / 25.16 | 2.675 | 0.049 | 0.006 | 404 | 1.50 |
| projectiles | 17.79 / 20.13 / 22.90 | 1.206 | 0.032 | 0.064 | 168 | 1.05 |
| projectiles and effects | 18.39 / 20.77 / 23.04 | 0.001 | 0.040 | 0.046 | 288 | 1.25 |

Subsystem timings are the latest simulation tick observed per rendered frame; they are not a census of every tick. AI includes navigation; the navigation column is separately recorded in raw JSON and must not be added again. Main-thread allocation totals include harness activity, view sync, GDScript bridges and tracing. Managed heap is not process/texture memory or a controlled leak test. Projectile workload peaks at eight active shots and combined workload at five effects in this sample.

The mobile-layout sample recorded eight-enemy frame p95 22.72 ms, simulation p95 2.80 ms, AI p95 2.68 ms, avoidance p95 0.057 ms. Projectile-heavy p95 collision time was 0.052 ms. These short samples cannot certify sustained phone performance. New workloads exercise profile decisions using the existing rendered rigs; the separate tactical replay reconstructs each authored archetype body.

## Bounds and remaining limits

The existing 5-Hz planning opportunities and slower profile cadence remain. Local avoidance uses pooled regional buckets, stable snapshots and nearby-cell queries. Projectile terrain collision uses analytic tile sweeps with grid traversal rather than tiny physics substeps. Actor interception/perception scan the current bounded population; re-evaluate broad-phase queries before substantially expanding populations or projectile volume. Effects and audio retain their established pools and limits. Opt-in profiling is disabled during ordinary play.

## Verification ledger

Native build and 3,244 production assertions passed locally after extended deterministic-restore checks. Import succeeded; its editor shutdown emitted the previously observed EditorSettings diagnostic, which was not counted as a runtime pass. Local rendered attempts could not connect to an X display; desktop/mobile rendering ran on GitHub Actions. A headless touch replay was also unsuitable (no viewport touch dispatch) and is not represented as gameplay evidence.

Final exact-head CI, replay, Android, screenshot review and lifecycle results will be appended after completion. The historical intermittent resume-to-black issue and unavailable physical ARM64/S25 testing remain limitations.

An intermediate Android attempt at `c0e7658` built and validated both packages, then failed during readiness polling because an ADB `logcat -d` dump returned exit 255. The retained log contains Godot/Dialogue startup and no established gameplay pass. The read-only logcat query now has three bounded retries with each failed output retained; touches, installs and gameplay assertions are never retried by this helper. This attempt is not an emulator pass.

An intermediate precise job on `1fff6fa` crashed in the Godot editor during asset import, with `libcoreclr.so` from the runner's preinstalled .NET 10.0.14 in the native backtrace. The engine's supplied `GodotPlugins.runtimeconfig.json` requests net8.0 with `LatestMajor`, so the runner had silently selected a newer major runtime. Workflows now set `DOTNET_ROLL_FORWARD=LatestPatch`, retaining the declared net8 host major while allowing installed patches; SDK 9.0.203 and Android net9 targets remain unchanged. This corrects environment drift; the precise failure remains recorded rather than being reported as a gameplay pass. Runtime-selection policy: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet#options-for-running-an-application .
