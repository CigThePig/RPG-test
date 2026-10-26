# Reproducible procedural artwork

Run `npm run art` with a .NET 8 SDK available. It compiles only `vendor/ppc/Core`, the unchanged upstream `EnvironmentArt.cs`, and our small exporter/adapter. No NuGet packages, Godot editor, manual PNG export, network service, or downloaded binary is required. `tools/art/Art.csproj` supports conventional .NET development; `tools/export-art.mjs` also runs directly with the SDK compiler on hosts where MSBuild's process inspection is restricted.

`tools/art/Program.cs` samples genomes via upstream **GenomeFactory.Sample**, builds anatomy via **CreatureFactory**, and exports via **SpritesheetExporter**. Seeds: biped 42, quadrupeds 17/1337, winged 91, arthropod 61. Every sheet contains idle/walk at 6 fps in eight directions (128 frames each). Pages, dimensions, clipping rectangles, per-frame durations, and ground origins remain in the upstream JSON schema. We retain original rendering/shadows and do not reconstruct anatomy in JavaScript.

The same run calls upstream GroundTile, Tree, Rock and Bush generators. Trees use scale 1.65, bushes 1.25. Five seeded variants each keep scenery consistent while giving variety. The browser gently blends ground textures with a restrained earth/green/water palette and samples continuous world fields for rounded river/path edges. The HUD uses system fonts and muted botanical colours. Tiny log/feeding-station props are drawn in code with the same upper-left highlights and ground depth ordering.

Outputs:

- `public/art/*.png`: original pipeline RGBA sprites and environmental images.
- `public/art/*.json`: sprite metadata and image-origin manifest.
- `src/anatomy.json`: generated gameplay bridge, read by the headless simulation.

Generated browser assets are deliberately checked in: the entire artwork catalog is small, and anyone can launch the project without an SDK. Do not edit PNGs or generated anatomy JSON manually. Re-run the exporter after changing seeds/options; test metadata bounds and semantic capabilities with `npm test`.

Upstream source is unchanged; new exporter, adapter, build script and browser integration are separate. The original 30-degree oblique creature camera, pixel silhouettes, palette ramps, ground origins and directional clips are preserved. Ground-plane dimensions are foreshortened, not a full isometric diamond grid. All original license notices—including fdlibm/PCG notices—are in `vendor/ppc/`.
