# Mosslight Reach migration

| Previous component | Result |
|---|---|
| PPC engine-independent C# core / licenses | Preserved unchanged in `vendor/ppc`; compiled once by native runtime and offline tools |
| Reproducible offline exporter | Preserved and expanded to arbitrary family/seed and full-family catalogs; Plantfolk included |
| Baked creature/terrain art | Preserved in reference; matching generated terrain art used in native game |
| Browser runtime/UI | Isolated under `examples/mosslight/`, excluded by `.gdignore`; not the active game |
| Hunger/berries/feeders/planting | Removed from active gameplay; historical experiment and tests stay isolated |
| Browser persistence/navigation | Retained as reference/test evidence; replaced by unified native RPG coordinator and region collision/pathfinding |
| Seed tests/benchmarks | Preserved; added actual Godot integration, tile dungeon validation and population measurements |
| Browser docs/screenshots | Archived under `docs/legacy/` and existing screenshot evidence; current docs describe native architecture |
| CI | Native builds/import/integration/rendered tests plus preserved reference checks |
| Pages hosting | Removed; native C# runtime cannot be hosted as a Godot web export |

The original implementation remains in Git history and as an isolated development reference. There is no compatibility promise for its browser save format; schema 1 native saves identify their own generator and content.
