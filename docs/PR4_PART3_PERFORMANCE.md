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

Native build and 3,246 production assertions passed locally after extended deterministic-restore checks. Import succeeded; its editor shutdown emitted the previously observed EditorSettings diagnostic, which was not counted as a runtime pass. Local rendered attempts could not connect to an X display; desktop/mobile rendering ran on GitHub Actions. A headless touch replay was also unsuitable (no viewport touch dispatch) and is not represented as gameplay evidence.

Final code verification results appear below. The historical intermittent resume-to-black issue and unavailable physical ARM64/S25 testing remain limitations.

An intermediate Android attempt at `c0e7658` built and validated both packages, then failed during readiness polling because an ADB `logcat -d` dump returned exit 255. The retained log contains Godot/Dialogue startup and no established gameplay pass. The read-only logcat query now has three bounded retries with each failed output retained; touches, installs and gameplay assertions are never retried by this helper. This attempt is not an emulator pass.

An intermediate precise job on `1fff6fa` crashed in the Godot editor during asset import, with `libcoreclr.so` from the runner's preinstalled .NET 10.0.14 in the native backtrace. The engine's supplied `GodotPlugins.runtimeconfig.json` requests net8.0 with `LatestMajor`, so the runner had silently selected a newer major runtime. Workflows now set `DOTNET_ROLL_FORWARD=LatestPatch`, retaining the declared net8 host major while allowing installed patches; SDK 9.0.203 and Android net9 targets remain unchanged. This corrects environment drift; the precise failure remains recorded rather than being reported as a gameplay pass. Runtime-selection policy: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet#options-for-running-an-application .

## Final code verification

All production-code checks passed on `681d15851c02a55b7dc27419f434dfa4498ee766`. The subsequent documentation-only commit records these results and leaves runtime code unchanged.

| Check | Actual result |
|---|---|
| Native build and fresh import | Passed; 58 existing nullable warnings. The local editor still emitted its known shutdown EditorSettings diagnostic; no runtime check was represented by that import alone. |
| Production integration | 3,246 assertions passed, including heading-precision restoration and deterministic continuation. |
| Desktop and mobile-layout rendering | Both passed, including prior combat, dodge, camera, presentation and new tactical workloads. |
| Baseline and precise replay | Both passed all 45 scenarios and their deterministic repeats. |
| ARM64 package | Development APK exported and passed package, .NET payload, architecture and signature validation. |
| x86_64 Android runtime | Install, launch, movement, four attacks, three dodges, real damage, Gloot chest/inventory, three archetypes and real Spit release passed. |
| Background/resume | Three consecutive nonblank same-process cycles during mixed combat passed, with input cancellation. |
| Preserved reference | Original reference tests, generation and build passed. |

- [Native CI and raw performance/screenshots](https://github.com/CigThePig/RPG-test/actions/runs/40820360737/artifacts/12874223153).
- [Baseline complete recordings, contact sheets, traces and report](https://github.com/CigThePig/RPG-test/actions/runs/40820360672/artifacts/12874423224).
- [Precise complete recordings, contact sheets, traces and report](https://github.com/CigThePig/RPG-test/actions/runs/40820360672/artifacts/12874935714).
- [ARM64 development APK](https://github.com/CigThePig/RPG-test/actions/runs/40820360688/artifacts/12873994234).
- [Android runtime recording, screenshots and lifecycle logs](https://github.com/CigThePig/RPG-test/actions/runs/40820360688/artifacts/12874900922).

The original five Part 2 workloads remain in the raw reports for comparison. The earlier table records the initial Part 3 checkpoint. A later inspected normal-time sample on `5b693a9` measured eight enemies at **28.74 ms p95**, with AI p95 **1.355 ms**, and the projectile-heavy collision sample at **0.056 ms p95**. That commit has the same encounter code as the final tested commit; the last code change preserves projectile heading precision during restore. Runtime pinning, approach distribution and host scheduling are additional variables, so these cross-job timings are not a controlled speedup. Final exact-code raw metrics are in the linked native artifact; Movie Maker wall times are not gameplay FPS.

Real viewport frames from the three archetypes, mixed encounters, crowding and adjacent projectile/attack outcomes were inspected on `5b693a9`. The final `681d158` rendered suites and replays passed after its restoration-only change. The workspace became unavailable after the final native run; this prevented extracting the final artifact images or assembling an additional local evidence package. The complete final source-identified recordings and reports remain available in GitHub artifacts.

Soft avoidance and local approach-angle distribution reduce crowding; eight-melee stress encounters can still partially occlude the player. Part 4 owns encounter density and readability tuning. The historical intermittent resume-to-black failure is not declared permanently fixed. No physical ARM64/S25 performance, subjective thumb-comfort or audible-quality signoff was available. No outstanding inline review threads existed at review; relevant implementation findings (timer order, enemy-only learning, legacy targeting and restoration precision) were resolved and covered by checks.
