# Engine decision

**Implemented: Godot 4.6.1 .NET + .NET SDK 9.0.203, 2D compatibility renderer.** Android and native desktop share one runtime. Desktop targets net8.0 and Android conditionally targets net9.0. GitHub Actions builds APKs for phone-first development. The choice changes the earlier browser-access priority: live arbitrary procedural anatomy and substantial reusable Godot RPG implementations now outweigh static browser hosting.

| Criterion | Canvas/Vite + baked PPC | Godot GDScript + baked PPC | Godot .NET + live PPC |
|---|---|---|---|
| Existing rendering retained | Browser loop | Exported textures | Original PPC motor, anatomy, queue, Godot actor |
| RPG reuse | Port every Godot subsystem | Native addons | Native addons plus engine-independent C# SPELL/grammar |
| Physics/navigation/UI/editor | Custom or more libraries | Native engine | Native engine plus semantic grid collision/pathfinding |
| Dynamic identity/equipment | New offline bake | New offline bake | Arbitrary genomes and original gear shape generation |
| Testing | Good headless JS/browser | Godot engine | Godot integration plus reproducible C# tooling |
| Export | Static web | Native + web | Native desktop; Godot 4 C# web unavailable |
| Maintenance burden here | Most RPG code must be rebuilt | Addon bridges; finite creature catalog | Managed/GDScript boundary, but far more original code reused |

PPC upstream itself targets Godot .NET 4.5.2. Its unchanged core/runtime builds on the pinned 4.6.1 SDK. Dialogue Manager 4 requires 4.6+, QuestSystem requires 4.4+, Gloot is pure Godot 4 GDScript; all selected runtime paths are exercised in the application. GDQuest's newer project/turn-based combat is not copied.

The game uses a deterministic ground-plane grid for authoritative navigation, radius collision and line-of-sight. Godot owns input, camera, fixed-frame scheduling, scene/UI composition, texture/audio presentation and exports. Grid collision is deliberate gameplay code, rather than a second physics representation that can disagree with save data. A future physics-backed controller should replace that boundary, not run alongside it.

Only one game runtime is selected. Browser files are isolated as historical art/simulation references, with no active hosting workflow.
