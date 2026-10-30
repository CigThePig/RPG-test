# PR5 Part 3 — Authored places in procedural dungeons

## Implementation summary

Continues pre-Part3 branch `a080e538c080be8204b8d4ecb86205324ab5e1a5`, preserving its gameplay/CI corrections. The existing PR #5 remains draft and unmerged. Five JSON-authored room compositions now appear in production new dungeons: Ruined Entrance, Forgotten Archive, Abandoned Shrine, Collapsed Storeroom and Relic Sanctuary. Weathered, overgrown and ceremonial materials share the Part 2 renderer. Authored arrangements mix with independent seeded orientations, prop variants, optional ground slots, coherent environmental zones and existing ambient scatter.

## Architecture and procedural integration

`Landmarks` owns library parsing, static validation, feature-derived classification, cardinal mask compatibility, rotation, independent stable hashing, candidate application, radius-11 room validation and bounded safe-open fallback. The original graph/layout/grammar and its Random stream are unchanged. Compose runs only after the original layout passes solvability and before the new region is returned. Selection prioritizes entrance/objective/key/chest before ordinary rooms, preserving required art under the 96-node budget. No feature moves, actors shrink or combat/movement timings change. RegionState owns resolved `Landmarks` alongside authoritative terrain. Presentation reads that manifest without rerunning selection.

The primary role comes from the existing center feature; dead-end/junction/hub are secondary topology descriptors. Masks use N/E/S/W bits 1/2/4/8 and rotate with local geometry/anchors/focal/facing. Every shipped template supports all nonempty masks by preserving full cross lanes. Role eligibility prevents optional rooms replacing mission entrance/objective landmarks.

## Template authoring

[LANDMARK_AUTHORING.md](LANDMARK_AUTHORING.md) documents the schema with a complete practical example, coordinates, supported symbols/assets/categories/themes, masks, rotations, protected lanes/features, seeds, previews, validation commands and revision policy. ContentAudit loads the packaged library at startup; JSON typos, missing resources and malformed entries fail clearly. Individual production-renderer previews accept template ID, quarter-turn and deterministic seed. No new editor framework is required.

## Structural safety

The 11-cell/9-interior/three-wide-door geometry is retained. Blocking elements are existing Wall terrain, with one matching visual each; floor motifs and scattered records have no collision. All central three-tile cross lanes and the perimeter ring remain open, with radius-11 swept footprint connectivity across every floor cell. Critical feature centers retain at least 60 units of structural separation. Boundary/gate cells are untouched. Candidate failure rolls back before trying the next bounded option; safe-open preserves otherwise valid generated rooms. Large sampled dungeons can exhaust the architectural budget and deliberately receive optional open fallback rooms; diagnostics identify seed, coordinates, template/revision/turn and reason. Required architecture is admitted first.

New column tests use actual World.Move, dodge, slash and projectile flight. Existing canonical-seed footprint checks cover player, biped NPC, prowler, brute and spitter; mission topology signatures, grouped gate progression and live generated-doorway encounters remain. The touch expedition now targets the protected perimeter instead of assuming an authored corner tile is floor. Its actual path must go around new blockers.

The original wall-depth fixture now explicitly selects a shared boundary row, because an interior two-tile rubble composition can also satisfy its former generic wall predicate. Foreground reference pixels are isolated to that boundary and ambient tall props are hidden in this wall-only probe. Separate new column pixel checks retain real actor/prop foreground/background verification. No runtime sorting rule was weakened.

## Environmental art

The original 384×12 floor and 1664×30 wall atlases are retained. One 1170×64 RGBA8 atlas adds five structural silhouettes × three variants × three themes, with gutters. Added payload: 299,520 bytes; total three-atlas payload: 517,632 bytes. No room/seed texture keys. Floor/wall themes use existing atlas vertex tints. Flat records, moss, thresholds, mosaics and offerings share one retained ground mesh below telegraphs. Column/shelf/altar/open-storage/collapse props use front ground-edge pivots in the existing actor Y-sort layer. Solid-looking furniture always has authoritative terrain. No fake decorative loot chests, external asset dependencies or alternate renderer.

## Save compatibility

Schema 1 and `rpg-regions-1` are unchanged. Missing optional `RegionState.Landmarks` defaults empty. Old saved terrain/features/actors/gates/loot/quests remain authoritative and receive no inferred new structural landmarks. A pre-Part3 expanded-region fixture was exported by the untouched baseline production code in a detached checkout; PR4's original fixture is unchanged. New saves store resolved room/template/revision/orientation/theme/focal/anchors, not a live pointer into a mutable template library. Future template edits affect only newly generated regions. The renderer must retain compatible meanings for existing saved asset IDs. Snapshot validation checks anchor bounds, asset IDs, budgets and terrain agreement before restore.

## Verification

Baseline build/import passed; baseline integration: **4,609 assertions** on a080e538. Initial Part3 expanded integration: **6,208 assertions passed**. Focused mobile-layout landmark and wall/tree pixel suites passed. The final source's full integration/desktop/mobile suites, touch replay workflows and Android export/emulator results are being established; final run IDs, commit identity and results will be appended after completion. Historical Part1/Part2 results are not claimed as verification of this implementation.

Commands: `dotnet build`; `python3 tools/import-project.py`; `python3 tools/verify.py --headless`; `python3 tools/verify.py --visual [--mobile]`; `python3 tools/gameplay-verify.py --presets baseline,precise`. Focused authoring commands are in LANDMARK_AUTHORING.md. New content tests cover every template/rotation/nonempty mask, malformed definitions, 97 independently repeated seeds (0..63, 1337, 1000..1031), all production template/theme coverage, resolved saves, budgets and structural collision. Existing recent-fix checks remain in the full suite.

## Visual evidence

Actual production Godot frames are captured under `docs/landmark-screenshots/` by LandmarkVisualChecks, using real TerrainView, Part 2 atlases, PPC actors, normal camera and ordinary Y sorting. Five named landmark captures, canonical-seed room captures, coherent adjacency, physical crossing, column front/back and live combat are produced for desktop and mobile layouts. Final evidence links and inspection notes will be appended after final captures. Individual preview fixtures are explicitly separate from natural-generation acceptance.

## Performance

The existing environment fixture is used for a same-host pre-Part3/current comparison; observations are software-renderer results, not Galaxy S25 FPS. Generation/selection/structural-validation timing, mesh construction, frame intervals, node/texture/buffer counts and six transition observations are recorded by existing and new fixtures. Raw measurements and final comparisons will be appended after final verification. No performance improvement is claimed from historical workers.

## Android delivery

The existing Android workflow remains authoritative. All export presets explicitly include `game/data/landmarks/*.json`; APK verification requires the packaged library. Installed smoke retains existing touch/combat/lifecycle tests, requires generated entrance/sanctuary metadata and structures, verifies the third atlas, and adds real dungeon joystick release/displacement/clear-footprint observations before Attack/Dodge and repeated return/re-entry. Final workflow/APK/source/runtime links will be appended. No physical Galaxy S25 is connected; device presentation/audio/sustained performance remain user acceptance. Differently signed development APKs can require uninstalling and deleting private saves; preserve valuable progress first.

## Part 4 integration contract

Use region ID + logical room X/Y + template ID/revision for stable identity. The saved focal and roles are content hooks, not a second mission system. A focal may be a solid altar: obtain nearby reachable interaction/encounter stands through existing FindPlacement/Path with real radii/occupancy. Use existing World events, QuestSystem, Gloot, Feature state, encounter profiles and interaction handling for quest destinations, rewards, loot and narrative context. Preserve original center mission-feature indices; no duplicated relic/portal/key authority. Enemies and rewards must not be emitted from cosmetic anchors. Resolved anchors/theme remain stable on revisits/save/load. Optional scatter may change presentation density without altering structure or gameplay. Part4 retains adventure pacing, quest integration, reward loops and final PR readiness review.
