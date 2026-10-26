# Runtime and integration decision

## Selected: browser Canvas + original PPC offline renderer

A small browser world has the lowest barrier to Ross's desktop and phone playtesting. The decisive finding is that PPC's engine-independent C# core already exports animated, directional sprite sheets with pivots. We can reuse it directly, without porting its anatomy, motor, rasterizer, deterministic math, or genetics. The browser only selects and draws exported frames.

| Option | Reuse and visual fidelity | Access and cost | Decision |
| --- | --- | --- | --- |
| Godot 4 C# | Best live motor, procedural poses, body-fitted gear; existing runtime | Native build/testing overhead; official Godot documentation still states C# web export is unsupported | Strong future option if dynamic anatomy becomes central; reject for this milestone |
| Browser + offline .NET PPC | Original renderer, animation and environmental art; eight facings preserved | Easy static hosting/touch testing; export cost paid offline; finite identity catalog and clips | Adopt |
| Browser port of PPC / WASM .NET runtime | Could eventually expose live generation | Port duplicates substantial mature code; WASM bridge adds download/build/debugging cost without helping this tiny scene | Reject initially |
| Phaser / PixiJS browser renderer | Good future camera/batching/scene facilities | More framework surface than needed for eight animated agents and one cached ground | Keep Canvas now; rendering is separate and replaceable |

Godot primary reference: https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html . This limitation informed the choice, rather than assuming browser export or dismissing C# code reuse.

## Source inspection and adopted components

PPC commit **a2207c4bd6f1be51d426684cf58f11668def7579**: inspected `docs/ARCHITECTURE.md`, `Core/Framework/CreatureModel.cs`, genome sampling, anatomy/motion rigs, `SpritesheetExporter.cs`, `PngEncoder.cs`, workshop `EnvironmentArt.cs`, and licenses. The complete core and just the environmental art source are vendored unchanged. The only engine adapter is our small `GodotAdapter.cs`: image buffers, colour interpolation, and integer origins. No Godot runtime is copied. Everything renders through original upstream algorithms.

PPC's environment art is already designed at creature pixel density, with oblique silhouettes and upper-left light. It is therefore a better first choice than introducing a separate material pipeline. Five fixed, genuinely sampled genomes provide a reproducible initial catalog; new seeds alter geography/resources and select familiar identities, rather than producing an unlimited new anatomy catalog at runtime.

## Supporting candidates investigated, not adopted

These were inspected at source level, not added as dependencies:

| Project / inspected commit | Relevant evidence | Decision |
| --- | --- | --- |
| [claude-pxl](https://github.com/crsolver/claude-pxl), `21fee41f7ecce00bc868d7567f24ffd0f3acc8fa` | MIT; inspected package, canvas/image, rendering and sheet preview modules; TypeScript pixel-art DSL | Useful later for architecture/props. PPC already supplies the required terrain and vegetation; another art DSL adds little now |
| [GdPlanningAI](https://github.com/WahahaYes/GdPlanningAI), `f81b862a7d3b4c024b1bf9f3bf9875d17bbe694b` | Apache-2.0; inspected Rust Cargo dependencies, planner docs, pickup action requirements/provisions | Godot/GDExtension + Rust integration; excessive for three food-seeking states, incompatible with the selected lightweight browser runtime without a bridge |
| [Worldloom](https://github.com/IotA-asce/worldloom), `a21b792fa6952c3ce8cd9ee77293d203bdf041d1` | MIT; inspected architecture, events, authoritative resource ledger and package dependencies (including provider SDK and Minecraft-facing stack) | Keep separation of facts and observations as a design principle; do not import its persistence/provider/agent infrastructure |

No code was copied from those three projects. This is an integration-cost decision, not a claim that their implementations are poor. The remainder of the original shortlist was deferred after finding that PPC plus a compact authoritative simulation covered the milestone. No GPL, mixed-license examples, or assets of unclear provenance were imported.

## Intentional limits

A 32 × 26 navigation grid, eight agents, five identity assets, one appetite, two construction actions, and one event log are enough to test this direction. Appearance does not change live; limb damage, freeform equipment, physics, combat, and planner search are deferred. Do not grow a generic RPG engine before observing this playable slice.
