# Dependency and attribution register

## Incorporated source and assets

| Upstream | Pinned version / commit | Incorporated component | License and modifications |
| --- | --- | --- | --- |
| [Procedural Pixel Creatures](https://github.com/idlerunner00/procedural-pixel-creatures), idlerunner00 | `a2207c4bd6f1be51d426684cf58f11668def7579` | All 60 engine-independent Core C# files; workshop `EnvironmentArt.cs`; generated sheets, props, textures and metadata | MIT. Sources unchanged, including comments. New exporter, Godot value/image adapter and browser consumer are project code. Upstream [license](vendor/ppc/LICENSE) and [complete third-party notices](vendor/ppc/THIRD_PARTY_NOTICES.md) retained |
| fdlibm / Sun Microsystems | adaptation already present in pinned PPC | `Core/Math/DMath.cs` deterministic kernels | Permissive notice retained in source and PPC notices; not public domain |
| PCG / Melissa O'Neill | adaptation already present in pinned PPC | `Core/Math/Random.cs` | Apache-2.0 notice/license retained in PPC notices |

The asset build uses .NET's installed framework/compiler; no runtime/SDK binaries are distributed in this repository. No external sprite packs, music, generated image-service output, or downloaded fonts are bundled.

## JavaScript development dependencies

Exact versions and all transitive resolutions are in `examples/mosslight/package-lock.json`. No JavaScript runtime dependencies ship with the game.

| Package | Version | Use | License |
| --- | --- | --- | --- |
| Vite | 6.4.5 | dev server and static bundler | MIT |
| Playwright | 1.56.1 | Chromium end-to-end testing | Apache-2.0 |
| Prettier | 3.6.2 | formatting project source | MIT |

Installed packages retain their original notices under `node_modules`; downloaded test browsers are development tools and are not committed or included in `dist`. GitHub Actions uses the checkout/setup-node/setup-dotnet/upload-artifact actions and setup-godot at the versions recorded in the workflow. The old Pages deployment has been removed.

## Investigated, not incorporated

claude-pxl (MIT), GdPlanningAI (Apache-2.0), and Worldloom (MIT) were inspected and declined for this milestone; exact commits and reasons are in [historical decisions](docs/legacy/DECISIONS.md). No code or assets from those projects were copied. The expanded source audit and current adoption decisions are in `docs/SOURCE_AUDIT.md`. Mentioning an upstream does not imply its assets were copied or relicensed.

## Native RPG foundation additions

See `third_party/UPSTREAMS.json` and `docs/UPSTREAM_DEPENDENCIES.md` for exact revisions, authors, included paths, modifications and integration responsibilities.

- **Procedural Pixel Creatures** — idlerunner00, MIT. Expanded to the original live Godot runtime and Plantfolk extension. Original source and all embedded notices retained.
- **Gloot** — Peter Kish, MIT. Complete addon in `addons/gloot/`; license retained. Inventory/item restore validation patches documented.
- **Dialogue Manager 4** — Nathan Hoad and contributors, MIT. Complete addon in `addons/dialogue_manager/`; license retained. Icon-reference portability patches documented.
- **QuestSystem** — Shomy, MIT. Complete addon in `addons/quest_system/`; license retained. Ordered pool-restore patch documented.
- **SPELL** — Emanuele Manzione, MIT. Complete engine-independent library source in `third_party/spell/`; license retained. Project file omitted in favour of the main .NET project.
- **Graph Dungeon Generator** — Ahmed Khalifa, MIT. Original mission/layout grammar source and input rules in `third_party/graph_dungeon/`; license retained. Console entry point omitted; deterministic directory ordering added.
- **Skelerealms** — Slashscreen, MIT. GOAP planning graph and PlannerNode adapted in `game/actors/Planner.gd`; license in `third_party/skelerealms/`. 3D entity/execution dependencies removed; value-aware goal/precondition checks added.
- **gdfxr** — Haoyu Qiu, MIT. Original Godot SFXR port/editor in `addons/gdfxr/`; license retained. Used for local procedural audio generation.

No code or assets from non-adopted audit candidates are included. In particular, GDQuest/Unity example artwork, native extension binaries and mixed-licence demo content were not acquired into the deliverable.
