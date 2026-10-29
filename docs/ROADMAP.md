# Next game development

PR #4 milestones: Part 1 establishes authored target/cone geometry, typed world presentation events and state-derived enemy warnings; Part 2 adds procedural attack animation and impact feedback; Part 3 implements authored enemy tactics, spacing and swept projectile flight; Part 4 will address mobile controls, encounter balance and final verification. Circle damage and final mobile balancing remain future work; Part 3 verification status is recorded in PR4_PART3_PERFORMANCE.md.

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
