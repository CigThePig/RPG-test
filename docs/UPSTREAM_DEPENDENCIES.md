# Upstream dependency register

Exact imported revisions, authors, paths, modifications and roles are machine-readable in [UPSTREAMS.json](../third_party/UPSTREAMS.json). No network fetch is required to build the source checkout.

| Upstream | Revision | Primary role | Modifications |
|---|---|---|---|
| [procedural-pixel-creatures](https://github.com/idlerunner00/procedural-pixel-creatures) | `a2207c4bd6f1be51d426684cf58f11668def7579` | Core anatomy/genomes/motor/rasterizer; live Godot renderer and queue; Plantfolk extension; offline art | None. Original source preserved; game and exporter adapters are separate. |
| [gloot](https://github.com/peter-kish/gloot) | `6b09b87ac07a8536779ef1ab0fafc7e99830f948` | Primary inventory, property bags, stacks, transfers, equipment slots, serialization, editor tools | inventory.gd propagates failed item restoration; inventory_item.gd rejects unknown prototypes. |
| [godot_dialogue_manager](https://github.com/nathanhoad/godot_dialogue_manager) | `ebfc3f0cb6e146f8b27c76be288157fcde0f0ffc` | Dialogue parser/editor/runtime, conditions, mutations, choices, C# integration | Replace three unstable UID-only icon references with asset paths (four files). |
| [quest-system](https://github.com/shomykohai/quest-system) | `d1d933c7b1822aceb637e3d6fe3a4814f973c3ee` | Quest resources, pool transitions, progression lifecycle, serialization | abstract_manager.gd restores ordered pool IDs without erasing an iterated list. |
| [SPELL](https://github.com/emanzione/SPELL) | `520b63f1536bf29b04931509048117ab0d8e9ef6` | Requirement/cost/effect execution and instance lifecycle; C# definitions compile data into SPELL | No source changes; legacy project file excluded. |
| [GraphDungeonGenerator](https://github.com/amidos2006/GraphDungeonGenerator) | `3d55c329d2cf31b7c5941f7e3e66f2b873cc8f24` | Mission graph grammar, spatial embedding, keys/locks and solvability; game adds tile construction and exhaustive tile validation | Remove upstream console entry point; sort grammar folders/files ordinally. World adapter corrects the reversed door-direction convention. |
| [skelerealms](https://github.com/SlashScreen/skelerealms) | `293dfd0b68541066bc505eadf03255d85218b4bf` | Extracted GOAP graph search; authoritative action execution stays in World | Extracted pure planner; value-aware preconditions/goals; game action definitions |
| [gdfxr](https://github.com/timothyqiu/gdfxr) | `8823532565967bfd3a39036a5154da85cf797b35` | SFXR synthesis, sound configuration/editor and WAV export | None. |

All imported projects above are MIT. Complete notices stay next to the sources. `vendor/ppc/THIRD_PARTY_NOTICES.md` retains PPC’s embedded PCG/fdlibm notices. No GDQuest sprites, fonts, Unity assets or native binary addons were copied. Generated environment textures use PPC’s original generator; no external artwork was added.

## Update policy

Upgrade one module at a time. Update its revision and modification register, reapply the small patches, run fresh import, both engine verification modes and the preserved exporter. Never replace a modified addon with an unpinned download.
