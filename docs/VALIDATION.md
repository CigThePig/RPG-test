# First-playable delivery and validation

## Built experience

**Mosslight Reach** is a browser-playable procedural wetland sandbox with a controlled PPC biped, eight independent animated foragers, berry patches, fallen wood, planting, constructed feeding stations, contextual inspection, an event journal, seed regeneration, pause, and browser persistence. Keyboard/mouse and tap-to-route controls share the same world. It starts immediately from a checkout using the generated assets.

The browser/offline-.NET choice reuses the original PPC anatomy, genome sampling, locomotion motor, CPU rasterizer, directional sheet exporter, PNG encoder, and environment artwork. There are five sampled creature identities across four families, two quadruped anatomy variants, and 640 animation frames. Original upstream source is unchanged. Semantic wing rigs permit water travel; sampled anatomical walking speeds affect movement. There is no attempt to infer physical traits from pixels.

The interaction loop is discover/inspect a food source, choose between gathering and leaving food available, use berries/wood to create or stock a new source, and watch independent hungry agents respond. Shared resource depletion, regrowth, distance, reachability and locomotion produce different food allocation and destinations. A controlled test verifies that a player-built/stocked station is consumed by an autonomous agent and leaves a real meal event. Another removes an agent's selected target and confirms replanning; total removal produces a waiting state. These are rule interactions, not a scripted delivery quest. This milestone does not demonstrate complex social emergence or a balanced survival economy.

## Executed checks

- **10 headless test groups pass.** `npm test` covers 100 deterministic generation seeds (1, 42, 1337 plus 97 others), seed variation, valid spawn cells, reachable starter resource types, action atomicity, depletion, planting/regrowth, station construction/stocking/consumption, invalidated NPC targets, wing-vs-ground reachability, save/load deterministic continuation, corrupt-save rejection, and sheet/asset metadata.
- **Thirty ten-minute multi-seed simulation runs pass.** Each runs 6,000 × 0.1-second updates, checks valid positions and bounded needs/resources, and observes meals. The focused benchmark additionally runs 18,000 × 1/30-second steps (the live game cadence) for seed 42. This is not a many-hour ecological stability claim.
- **Production build passes.** `npm run build` uses the lockfile-resolved Vite 6.4.5. Generated browser assets are included and relative URLs are used for static subdirectory hosting.
- **Actual Chromium end-to-end pass.** `npm run test:e2e` hosts `dist` itself; enters the game; moves using keyboard input; harvests through E; saves; generates seed 1337; restores seed 42; opens agent/event notes; verifies pause; resizes to 390 × 844; checks no horizontal overflow; tests click-to-walk and action controls. No page errors or missing local assets were observed.
- **PPC asset regeneration passes.** `DOTNET_ROOT=/tmp/dotnet npm run art` ran with an installed .NET 8 SDK. Two complete runs produced identical SHA-256 hashes for all 32 exported art files. All 60 vendored C# core files match the pinned upstream source. The portable compiler wrapper avoids an environment-specific MSBuild/process-inspection failure; the normal SDK project remains available for development.
- **Dependency audit passes.** `npm audit --audit-level=high` reported zero vulnerabilities after upgrading the initial Vite pin to patched 6.4.5. No runtime packages are installed.

CI re-runs export, verifies generated JSON against committed metadata, runs tests/build, executes Chromium, and uploads visual evidence. CI status and automated review findings must be inspected on the PR; local success alone is not a claim that hosted checks have finished.

## Measured performance

Linux x64 hosted development environment, Node 24.22.1, headless Chromium 141, desktop viewport 1440 × 1000. These are small-workload observations, not guarantees for Ross's phone or arbitrary populations. Raw data: [browser](browser-measurements.json), [headless simulation](simulation-measurements.json).

| Measurement | Observed result |
| --- | --- |
| Procedural export | 3.02 seconds for five sheets and all environment art, excluding compilation (other runs approximately 3.0–3.4 s) |
| Generation, 100 seeds | p50 0.215 ms; p95 0.736 ms |
| Save serialization | p50 0.024 ms; p95 0.170 ms |
| Save reconstruction/validation | p50 0.325 ms; p95 1.026 ms |
| Seed 42 ten-minute simulation | 18,000 steps in 61.0 ms; 145 meals; eight agents |
| Long-run save size | 15,477 bytes, including bounded history |
| Browser frame timing, 663 samples | mean 17.32 ms (~57.7 FPS); p50 16.7 ms; p95 16.8 ms; p99 33.3 ms; 1.81% above 25 ms |
| Browser ground-cache rebuild | 148.6 ms in sampled world restoration; paid once, not per frame |
| Live simulation | fixed 30 Hz; drawing targets display refresh |
| Browser heap | Chrome reported 10 MB; coarse browser estimate, not total process/texture memory |
| Static ground RGBA | 1536 × 832 × 4 = 5,111,808 bytes, excluding canvas/browser overhead |

Headless steps-per-second greatly exceeds live cadence because the model is tiny and rendering is absent; it should not be presented as a realistic large-world benchmark. Browser frame statistics exclude asset loading/cache creation, include ordinary interaction in an approximately 12-second run, and can vary with hosted scheduling. No long-duration GPU/mobile profiling has been completed.

## Visual review

Screenshots are captured from the running production browser game, not mockups:

- [Arrival](screenshots/arrival.png)
- [Gameplay](screenshots/gameplay.png)
- [Field notes](screenshots/field-notes.png)
- [390-pixel layout](screenshots/mobile.png)

Visual inspection prompted two revisions: lower-contrast terrain to separate creature silhouettes, and larger PPC vegetation to fit creature density. A further pass replaced coarse tile-shaped ground boundaries with continuous geography sampled per logical pixel. Characters retain upstream ground pivots/shadows and sort among props by depth. Mobile UI fits; fonts are entirely local/system fallbacks.

## Limitations and next directions

No known critical blocker remains in the exercised loop. The scope is intentionally small: one appetite, two construction actions, a fixed exported anatomy catalog, no combat/death/dialogue/equipment management, non-blocking trees, and no collision avoidance between agents. Ground navigation samples cells while shoreline art samples a continuous field; boundary alignment is approximate. Exported walk cadence can slide against game velocity. There is one browser-local save slot and no historical replay beyond saved state plus a 100-event log.

GitHub Pages workflow is included for main/manual deployment. A live public build is **not verified** and the substantive feature PR must remain unmerged. Future experiments are in [ROADMAP](ROADMAP.md); they do not prematurely choose the eventual RPG genre.
