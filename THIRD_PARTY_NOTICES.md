# Dependency and attribution register

## Incorporated source and assets

| Upstream | Pinned version / commit | Incorporated component | License and modifications |
| --- | --- | --- | --- |
| [Procedural Pixel Creatures](https://github.com/idlerunner00/procedural-pixel-creatures), idlerunner00 | `a2207c4bd6f1be51d426684cf58f11668def7579` | All 60 engine-independent Core C# files; workshop `EnvironmentArt.cs`; generated sheets, props, textures and metadata | MIT. Sources unchanged, including comments. New exporter, Godot value/image adapter and browser consumer are project code. Upstream [license](vendor/ppc/LICENSE) and [complete third-party notices](vendor/ppc/THIRD_PARTY_NOTICES.md) retained |
| fdlibm / Sun Microsystems | adaptation already present in pinned PPC | `Core/Math/DMath.cs` deterministic kernels | Permissive notice retained in source and PPC notices; not public domain |
| PCG / Melissa O'Neill | adaptation already present in pinned PPC | `Core/Math/Random.cs` | Apache-2.0 notice/license retained in PPC notices |

The asset build uses .NET's installed framework/compiler; no runtime/SDK binaries are distributed in this repository. No external sprite packs, music, generated image-service output, or downloaded fonts are bundled.

## JavaScript development dependencies

Exact versions and all transitive resolutions are in `package-lock.json`. No JavaScript runtime dependencies ship with the game.

| Package | Version | Use | License |
| --- | --- | --- | --- |
| Vite | 6.4.5 | dev server and static bundler | MIT |
| Playwright | 1.56.1 | Chromium end-to-end testing | Apache-2.0 |
| Prettier | 3.6.2 | formatting project source | MIT |

Installed packages retain their original notices under `node_modules`; downloaded test browsers are development tools and are not committed or included in `dist`. GitHub Actions uses official checkout/setup-node/setup-dotnet/upload-artifact/upload-pages-artifact/deploy-pages actions at the major versions recorded in the workflow files.

## Investigated, not incorporated

claude-pxl (MIT), GdPlanningAI (Apache-2.0), and Worldloom (MIT) were inspected and declined for this milestone; exact commits and reasons are in [DECISIONS](docs/DECISIONS.md). No code or assets from those projects were copied. Other shortlist projects remain deferred/unverified for integration. Do not treat their mention in the assignment as license clearance.
