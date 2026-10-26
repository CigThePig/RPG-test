# Integration map

| Domain | Authoritative owner | Actual upstream contribution | Runtime connection |
|---|---|---|---|
| Entity identity/attributes/health/progression | `game/actors/State.cs`, `World` | Game adapters required by SPELL/PPC | IDs key all actor references and events |
| Creature anatomy/genetics/animation | `vendor/ppc/Core`, PPC Runtime | Complete original core/runtime + Plantfolk | `CharacterView`, workshop, full genome saves |
| Inventory/stacks/containers | Gloot `Inventory`/`InventoryItem` | Complete Gloot addon | `ItemService.gd` ↔ `Items.cs`; no C# duplicate store |
| Equipment slots/instances | Gloot `ItemSlot` | Original slot implementation | Slot checks, stat bonuses and PPC gear variants |
| Procedural item construction | Gloot item properties | Original property bag/serialization | Material/quality constructor in `ItemService` |
| Crafting/merchant/loot rules | `Items` and `World` | Gloot transfer/split/merge primitives | Recipes and loot definitions mutate actual inventories |
| Ability execution | SPELL `SpellsContext` | Complete original costs/requirements/effects/lifecycle | `RpgAbility` binds definitions to authoritative actions |
| Persistent action/cooldown/status state | `ActorState` and `World` | SPELL creates action; game adapter owns serializable timeline | Windup, recovery, projectile/status resolution |
| Hit detection/mitigation/death | `World` | Required gameplay adapter, not supplied by SPELL | Grid geometry/range, defence, recovery, XP, loot, cues |
| NPC planning | `Planner.gd` | Skelerealms graph planner and PlannerNode | Value-aware conditions; World executes shared actions |
| Schedules/perception/factions/memory | `World`, `NpcProfile`, `ActorState` | Game integration around planner | Data-driven activity, hostility, sight and recorded encounters |
| Dialogue | Dialogue Manager 4 | Complete addon/compiler/editor/runtime | Upstream script format, live conditions and mutations |
| Quest lifecycle | QuestSystem manager/resources/pools | Complete addon with restore bug fix | EventQuest counters/dependencies/failure; shared event subscriptions |
| Region geography/navigation | `Regions`/`RegionState` | Godot FastNoiseLite | Same tiles feed collision, paths, art, feature access |
| Dungeon mission and layout | Graph Dungeon Generator | Complete graph/layout/grammar source | Tile adapter, key pairing, exhaustive reachability validation |
| Spawns/region changes | World | Game integration around generated layout | Stable IDs, valid-floor spawn selection, persistent regions |
| Save/load | `Persistence` | Gloot/QuestSystem native serialization | One schema/coordinator; three atomic file slots |
| UI/camera/input | Godot + `RpgUi`/`Foundation` | Engine controls; real narrative addon output | Functional commands, panels and fixed tick |
| Procedural audio | gdfxr | Original SFXR synthesizer/config/editor | Cached presets, cue/ambience/music channels, WAV export |
| Artwork tooling | PPC offline exporter | Original sheet/environment algorithms | Arbitrary family/seed catalogs, matching pixels and metadata |

Custom adapters are explicitly distinguished from imported source. No claim is made that Skelerealms supplies our actor, persistence, inventory or combat implementation. Those conflicting modules were inspected and not imported.
