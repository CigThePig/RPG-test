# Next game development

PR #4 milestones: Part 1 establishes authored target/cone geometry, typed world presentation events and state-derived enemy warnings; Part 2 adds procedural attack animation and impact feedback; Part 3 implements authored enemy tactics, spacing and swept projectile flight; Part 4 completes mobile controls, normal-health encounter audits, integration and executable verification. Circle damage remains future work; physical S25 acceptance remains pending. Final results are recorded in PR4_PART4_VERIFICATION.md.

The selected engine and source integrations are implemented. Continue by building game content and extending tested production paths:

1. Develop encounter pacing, directional targeting, telegraphs, area attacks, projectile behaviours and tactical enemy profiles using the current ability/action definitions.
2. Add hand-authored points of interest, settlements and region transitions. Expand the region generator's terrain/material constraints and validate collision/accessibility across more layouts.
3. Author quests and dialogue through the existing resources/events. Add richer objective matchers for particular actor/region/item IDs and quest invalidation rules based on world conditions.
4. Extend generated equipment with construction components, slot requirements, condition wear and explicit material-dependent interactions. Add matching PPC weapon shapes where originals do not cover the intended silhouette.
5. Expand NPC goals into multi-action plans, profession activities, trade/rest routines and social consequences. The planner, shared action boundary, memory/relationship data and persistence are present; a simulated political economy is not.
6. Refine progression/unlock costs, classless skill choices and equipment-derived capabilities. Experience/levels, skill points/ranks, training and ability unlocks are already implemented; balance/content trees remain design work.
7. Improve creature hit/death presentation, family-specific attack clips, ground contact, palettes, shores, vegetation depth and UI art. Original procedural art/motor APIs are available.
8. Profile native hardware rendering, GPU/texture memory, transitions and longer sessions. Software-rendered CI measurements are verification evidence, not a 60-FPS promise. Add population/render tiers where measurements justify them.
9. Add incremental region deltas, explicit save schema migrations and player-authored input/gamepad settings once the v1 content contracts settle. Do not create competing state owners.

The development environment is deliberately compact. There is no finished story, comprehensive economy/crime simulation, multiplayer, planet streaming, class balance, commercial UI/audio polish or mobile/browser export promise. Those are not hidden behind placeholder interfaces.

## PR #4 milestone state

- [x] Part 1: Combat architecture, shared geometry and telegraphs.
- [x] Part 2: Procedural combat animation and impact feedback.
- [x] Part 3: Enemy intelligence, spacing and projectiles.
- [x] Part 4: Mobile controls, encounter balancing and final verification.

Part 3 code was verified on `681d15851c02a55b7dc27419f434dfa4498ee766`. See PR4_PART4_HANDOFF.md and PR4_PART3_PERFORMANCE.md for continuation contracts and actual evidence.

Part 4 implementation adds separated mobile combat/utility zones, stable control IDs, safe-area/resize checks, expanded real input regression tests, normal-health combat fixtures, a conditional crowd locator and higher-contrast Spit. Final source c7c4d3293f5008312e30353d0199e5df34d8582a passed all executable checks; see PR4_PART4_VERIFICATION.md. Phone-only acceptance remains separate from code review readiness.

## PR #5 — Dungeon scale, exploration spaces & landmark foundations

Part 1 implemented the procedural spatial foundation: three-tile connectors, shared logical gates, physical-clearance coverage, deterministic seed checks and actual viewport comparisons. Its fixed one-room-per-cell grid has since been replaced by variable rooms (9×9 minimum core plus wings), 3×3 halls and turning three-tile corridors; see `DUNGEON_LAYOUT.md`. `PR5_PART1_HANDOFF.md` records Part 1's own verification.

Milestones on the same draft PR:

1. Dungeon scale and traversal — completed, including foreground-wall depth.
2. Procedural environmental art and rendering architecture — implemented and verified; physical S25 acceptance pending; see `PR5_PART2_HANDOFF.md`.
3. Handcrafted landmarks, environmental themes and procedural decoration — implemented; followed by variable room shapes and corridors (`DUNGEON_LAYOUT.md`).
4. Integrated RPG exploration adventure and final verification.

Mosslight is a traditional fantasy action RPG; the flexible approximately 50/50 procedural/authored direction is a creative goal, not a numeric quota or a creature-collection system.

Part 3 is implemented and verified on runtime `488174f7a0835584cf5059cb65e9e9b3c87be37e`: 6,210 assertions, full desktop/mobile visuals, gameplay movies/repeats and installed Android smoke pass. It adds five authored templates, coherent themes, production structural/cosmetic composition, resolved save manifests and reusable content hooks. Exact acceptance evidence and any remaining limits are tracked in [PR5_PART3_HANDOFF.md](PR5_PART3_HANDOFF.md). Part 4 should use these landmarks with existing quests, encounters and rewards; keep this PR draft until final integration review.
