# Verification and limits

## Reproduce

```
dotnet build
python3 tools/import-project.py
python3 tools/verify.py --headless
python3 tools/verify.py --visual
```

Use Godot **4.6.1 .NET**, SDK **9.0.203** (with the .NET 8 runtime/targeting pack for desktop), and Python 3. Visual verification needs a display; CI runs it under Xvfb. `tools/verify.py` requires the success marker, successful process exit and absence of engine/script errors. Native export presets are present; export templates and exported Linux/Windows packages were not locally exercised.

PR #4 Part 1 adds `CombatTelegraphChecks` to the existing integration runner, plus desktop and `--mobile` viewport captures and four production replay scenarios (`telegraph-bite`, `telegraph-dodge`, `telegraph-interrupt`, and `telegraph-cone`). Run `xvfb-run -a python3 tools/gameplay-verify.py --presets baseline,precise` to record the actual renderer and extract labelled early, middle and post-impact frames. The visual screenshot fixture slows engine time for reliable capture; the fixed-clock gameplay movies exercise the normal simulation cadence. Inspect the resulting images and movies as well as the assertions. Hosted workflows upload exact-head artifacts; neither software rendering nor an emulator establishes physical phone performance or feel.

## Executed evidence

- **213 production integration assertions** pass inside Godot, spanning C#, GDScript and imported addon implementations. Source: `game/tests/FoundationChecks.cs`; raw timings: `docs/verification.json`.
- All **nine registered PPC families** build deterministically and round-trip genomes, create live actor nodes and accept action/hit requests. The separate original exporter rendered **1,152 animation frames** for nine seeded family sheets, including Plantfolk, in approximately **3.1 seconds**, excluding compilation.
- Actual SPELL cast requirements/costs/effects drive melee windup, projectile damage, interruption, mitigation, death and Gloot loot. Tests cover resources, friendly-fire rejection, cooldown/action exclusion, consumables, status ticks/stack caps, temporary modifiers, experience, skill points, training and ability unlocks.
- Real Gloot inventories are tested for stack limits, split transfers, equipment/stat effects, generated material/quality items, crafting, merchant stock/payment and failed transactions. Snapshot validation rejects unknown items without mutating the running inventory.
- Real QuestSystem lifecycle receives crafting/defeat/acquisition events, enforces prerequisites, grants rewards/reputation once and restores all pool entries in order. Native dialogue verification follows an actual Dialogue Manager response mutation and checks prerequisite-gated choices.
- **Thirty dungeon seeds** generate reproducibly and pass tile-level key/lock/objective reachability. Region generation/navigation also runs at **32×24, 64×48 and 128×96**. Generated layout validation is additional to upstream graph checks.
- Unified actor/item/equipment/quest/region saves round-trip exactly and continue deterministically. Incompatible generator revisions and invalid item snapshots are rejected. Projectiles and windup/cooldown/status state belong to the serialized model.
- The actual native application runs under Xvfb/OpenGL software rendering. Input moves the player; menus, pack, equipment/character, journal, dialogue, dungeon transition and workshop produce real screenshots in `docs/native-screenshots/`. No script/runtime errors remain in those verification runs. The audio driver is deliberately Dummy on the hosted machine; audio sample generation is exercised, audible device playback is not verified here.
- The .NET project builds through the standard SDK/MSBuild entry point. This container intermittently fails CLI/editor process inspection inside Microsoft.Build; a documented direct-compiler fallback was also used. Asset imports finish; the container's Godot editor can still log the same MSBuild environment-helper exception. The running game and strict verification do not emit that exception. Hosted CI checks the normal environment separately.
- Original browser reference: **11 test groups** and the Vite production build still pass after relocation. Original asset/anatomy regeneration remains reproducible. No new browser deployment is claimed.

## Performance

Raw evidence records environment and workload. Machine: Linux hosted Intel Xeon Platinum 8573C. Rendering: Mesa llvmpipe software GPU, 1280×800 window. Measurements are not comparisons with Ross's desktop or mobile hardware.

`verification.json` contains 600 fixed steps at each of 16, 64 and 128 actors, generation/navigation at three region sizes, thirty dungeon timings, managed-memory observations and serialized save sizes. These benchmarks omit full rendered actor presentation and must not be quoted as rendered game FPS.

`render-verification.json` contains an actual 180-frame sample with nine visible actors (seventeen persistent actors including the unloaded dungeon). After grouping ground commands by texture, local samples were approximately **13–14 ms mean, 18–23 ms p95 and 26 ms p99**; the previous interleaved draw commands took around 26 ms mean. This is a short software-rendered test, not a sustained hardware/GPU performance guarantee. GC memory is managed memory, not total process or texture memory.

## Review regressions

Codex review findings are covered by executable cases: multiple overlapping corpses skip empty inventories; modal hotbar actions cannot mutate the paused world; anatomy changes return incompatible weapons and weapon abilities recheck current capabilities; acquisition quests reconcile already-owned relics at acceptance without repeating rewards. Additional regressions reject invalid crafting output before consumption and prevent interactions through walls. Equipment UI selects actual generated instance identities rather than a hardcoded prototype list.

## Development environments

The production `game/dev/Foundation.tscn` provides equivalent focused environments through functional screens and developer tools: movement/world, character/workshop, combat/abilities/statuses, inventory/equipment/crafting/trade, dialogue/quests, region/dungeon generation, save/load and diagnostics. They all use the production owners and data. Automated checks run these same implementations; there are no alternate mock game systems.

## Known practical limits

- No long-duration soak, native GPU/mobile profiling or packaged export validation has been completed locally.
- Combat currently uses single-target range/visibility and homing projectiles. Advanced attack arcs, area effects, physical actor-to-actor avoidance and tactical encounter tuning remain gameplay work.
- Distant/unloaded NPCs keep coarse schedules, not a detailed simulated economy. AI profiles are small and deterministic; relationships/knowledge have actual encounter state but no conversation-generated intelligence.
- Incapacitation uses tinted/resting PPC presentation; no bespoke death animation is claimed. Biped sword visuals map to PPC's available short-blade shape.
- Saves deliberately store full region arrays in schema 1; generator/schema changes require a future explicit migration or retaining the old executable. This is not an implemented streaming/delta-save system.
- Dungeon solvability verifies a valid key-spending route; it does not promise every possible key-spending order is recoverable. The adapter retains meaningful key consumption and rejects layouts with no valid route.

## Android remediation and mobile checks

`FoundationChecks` now includes historical defeat progression before acceptance and across restoration; distinct item-possession reconciliation; repeated reward rejection; all-family weapon reconciliation; failed/full/empty/partial Gloot loot transfers; and raw touch movement ownership, cancellation, dialogue/menu/death authority. The expanded suite currently executes 244 assertions.

`python3 tools/verify.py --visual --mobile` runs the production UI with the mobile layout enabled in the desktop engine. It records `mobile-*.png` screenshots and `render-verification-mobile.json`. This is hosted software rendering, not Android GPU evidence. The native/reference CI remains in `.github/workflows/ci.yml`.

The Android workflow additionally exports and structurally validates the signed ARM64 package, including its .NET publish payload. A separate x86_64 export is installed and launched in an Android emulator, with readiness/tick markers from the real C# composition root. The runtime evidence artifact includes install output, emulator log, logcat, screenshot and an explicit result. Read each actual workflow result; no package-only test is described as a runtime pass.

The physical Galaxy S25 checklist, signing limitations and offline phone workflow are in [ANDROID_DEVELOPMENT.md](ANDROID_DEVELOPMENT.md). Full engine diagnostics invoked in the Android development menu write their verification JSON to `user://`, not the read-only exported resource tree.
