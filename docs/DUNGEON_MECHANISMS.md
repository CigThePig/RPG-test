# Dungeon mechanisms: lever shortcuts and light seals

The mission grammar (`game/data/dungeon_rules.json`) has always produced **Lever** and **Puzzle** nodes, and Graph Dungeon Generator's layout grammar has always marked the doors after them as `LeverLock` and `PuzzleLock`. Until this change the game discarded both. Lever rooms were empty, puzzle rooms only held a chest, and their doors were ordinary open corridors. Now both are physical, saved and playable.

| Grammar | In the ruin | Player action | Opens |
| --- | --- | --- | --- |
| `Lever` node + `LeverLock` door | `lever` feature at the room core centre; `portcullis` gate on each adjacent lever passage | Interact once with the lever | Every portcullis on that room's lever passages (one-way shortcut) |
| `Puzzle` node + `PuzzleLock` door | Ring of four `brazier` features (N/E/S/W of the core centre) around the existing chest; `seal` gate on each adjacent puzzle passage | Interact with braziers until all four burn | Every seal on that room's puzzle passages |
| `KeyLock` door (unchanged) | `lock` gate | Spend one ruin key | That gate |

Key locks take precedence: if the layout marks a connection as both, it stays a key gate.

## Lever shortcuts

The grammar places a lever in a high-access room and links it back to a lower-access area. The portcullis on that link cannot be opened from the far side. Interacting with it says the lever is deeper in the ruin. Once pulled, the lever stays down (saved in `Depleted`) and its portcullis stays raised (saved in `OpenDoors`).

Across seeds 0–63 and 1337, 26 ruins have a lever. In 21 of them, the raised portcullis either gives a route home that bypasses locked key gates entirely (20 seeds) or shortens the route to the entrance even with every gate open (11 seeds); some do both. The other five connect areas that were already joined. `MechanismChecks` keeps this ratio at two-thirds or better.

## Light seals and the brazier ring

A seal is a curtain of pale light across a corridor. Its puzzle room holds four braziers in a ring around the core centre, on the landmark protected cross, so they always stand on open floor regardless of template. Lighting a brazier flips it **and its two ring neighbours**. When all four burn, the braziers become inert (their flames gain the ward's cyan core) and every seal on that room's puzzle passages breaks.

- On a ring of four, this toggle matrix is invertible over GF(2), so all 16 states have exactly one minimal answer. `MechanismChecks.Rules` walks every state.
- A fresh ring starts in a state whose minimal answer needs two to four lightings. It is chosen from a dedicated `DungeonLayout.Hash` slot and consumes no mission RNG.
- In 17 of the 26 seal seeds, the seal guards part of the mission itself (keys or the relic): removing the ring makes the ruin unsolvable.
- The journal records the brazier rule the first time a brazier is lit. Interacting with a seal says to light the nearby braziers.

## Generation order and safety

1. Mission and layout grammars run unchanged, including their RNG draws.
2. `DungeonLayout.Carve` records `DungeonPassage.Mechanism` (`""`, `"lever"`, `"puzzle"`) for unlocked passages. Geometry is unchanged.
3. Mission features, key gates and `Landmarks.Compose` run exactly as before, so room shapes, templates and anchors do not change for any seed.
4. `DungeonMechanisms.Install` adds levers, braziers, portcullises and seals. If any controller is unreachable, or the ruin is not solvable with mechanism gates closed, all mechanisms are stripped and the ruin plays exactly as it did before. No seed in 0–63 or 1337 needs this fallback.

`DungeonValidator` is mechanism-aware. Levers and brazier rings cost nothing, so its flood opens every gate whose controller it reaches and repeats to a fixed point. Each key-spending search state does this. A mechanism gate with no remaining controller is a wall. `DungeonValidator.Reach` exposes the same flood for tests and drivers.

## Runtime and presentation

- `RegionState.IsGate` / `ClosedGate`: every gate kind blocks movement, sight, projectiles and enemy routing until opened. `gatesOpen` placement queries still treat them as open.
- `World.Interact`: `lever_pulled`, `brazier_kindled` (count = braziers burning) and `seal_broken` events. Closed portcullises and seals give explanatory feedback without hiding nearby chests, keys or loot.
- `FeatureView` draws the lever (handle up/down), the brazier (cold ash, animated flame, cyan ward core when solved), the portcullis (bars lowered, or only teeth under the lintel when raised) and the seal (pulsing curtain and rune, or a faint floor glyph once broken). Only lit braziers and intact seals animate.
- The map marks explored gates as gold (key), grey (portcullis) or cyan (seal), dimmed once open, plus `L` and `B` markers.
- Narrative stores mechanism flags and two lore lines. The journal shows `Ruin mechanisms: N shortcuts raised · M light seals broken`.

## Saves

`RegionState.Lit` and `DungeonPassage.Mechanism` are new optional fields. Generator identity stays `rpg-regions-2` and schema 1. Saves made before this change deserialize with no lit braziers and no mechanism passages. Those ruins have no mechanism features, so they load and play exactly as saved; `MechanismChecks.Saves` proves this. A save that lights a non-brazier tile, or names an unknown passage mechanism, is rejected before mutation.

## Verification

```sh
python3 tools/verify.py --headless                               # MechanismChecks plus updated geometry/traversal checks
godot --headless --path . -- --adventure-check --adventure-ruin=33   # normal-health adventure through a lever and a brazier ring
godot --path . --audio-driver Dummy -- --visual-check --mechanisms-only [--mobile-ui]
xvfb-run -a python3 tools/gameplay-verify.py --presets precise --dungeon-only   # real touch expedition (seed 1 crosses a seal)
godot --headless --path . -- --dump-dungeons=7,42   # % portcullis, ~ seal, L lever, B brazier
```

`--adventure-ruin=N` runs the same normal-health production-API adventure. The only difference is that the ruin is pre-generated from seed `N` instead of the world-derived seed; the overworld, beasts, starter kit and rules are unchanged. The default `--adventure-check` ruin (seed 1299) has no mechanisms, and its record is unchanged at 10,966 ticks and 84 health.
