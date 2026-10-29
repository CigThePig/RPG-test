# PR #4 Part 4 handoff

Part 3 adds combat profiles, tactical intentions beneath Skelerealms, soft NPC avoidance, authored dungeon archetypes, straight/bounded-homing projectiles and swept collision. Part 4 remains pending. Do not change the completed slash/roll rules to accommodate control layout work.

## Gameplay ownership

`World.Think` retains perception, faction checks, schedule updates and `Planner.gd`'s attack/retreat goals. `CombatBrain.Select` chooses an intention only; `World.Activate` still enters SPELL, which owns requirement/cost execution through `RpgAbility`. World owns action boundaries, committed cone direction, movement, damage, defenses and statuses. `CombatPresentationController` remains the only effects/audio router.

## Authored enemies

`foundation.json:combatProfiles` and `spawns.json:ruin.archetypes` are the authoring entry points. Eight dungeon inhabitants cycle the three archetypes with stable IDs and seeded locations. Spawning calls the existing footprint/reachability placement, and existing saved dungeon actors are retained. Ordinary overworld NPC identities/schedules are unchanged.

| Archetype | Body / speed | Attacks | Positioning and initial tuning |
|---|---|---|---|
| Prowler | quadruped / 88 | bite | 26 preferred, 36 approach; existing 0.30 s bite and 0.40 s recovery; occasional lateral cooldown movement |
| Brute | reptile / 46 | slam, bite fallback | 30 preferred, 42 approach; Slam 0.80 s windup, 0.85 s recovery, 2.40 s cooldown; committed 52-unit, 55-degree half-angle cone |
| Spitter | amorphous / 62 | spit, bite fallback | 72 minimum, 110 preferred, 140 approach; Spit 0.48 s windup, 0.50 s recovery, 1.80 s cooldown; 135 units/s straight droplet |

These values are starting points. Balance health (65/120/55), damage (bite 9, slam 14, spit 7 plus existing actor stats), pressure/recovery, projectile readability, retreat distances and mixed-room pacing against real phone play. Do not infer that a renderer or emulator pass establishes enjoyable difficulty.

Approach/retreat use hysteresis. Equal attack priorities and target distances break by ordinal ID. The selector reuses `CanActivate`, including cooldown, mana, equipment, state, range and sight. Retreat tries six ordered local directions with clearance; a cornered enemy may attack or hold. Repositioning is occasional and actor-ID phased. No wall tracking, global knowledge, squad turn-taking or frame-perfect dodge prediction was added. Missing/unknown saved profile IDs use safe legacy tactics.

## Spacing

`ActorSeparation` rebuilds reusable 48-unit region buckets from stable-ID actor snapshots. Nearby living bodies contribute a bounded soft steering correction, including the player as a neighbour; only NPC locomotion receives that correction. Exact overlaps get deterministic opposing directions. Speed is clamped to the actor's normal stat speed. Invalid correction travel falls back to navigation's desired velocity, and actual displacement still passes through `World.Move`/`CanStand`. There is no rigid actor collision; rolls, lunges and knockback use their established paths. Tight doorways may temporarily allow overlap. Crowds are not a full reciprocal-velocity solver.

## Projectile authority

`AbilityDef.Projectile` optionally supplies `Mode`, `TurnDegrees`, `Lifetime`, `Radius`, `Visual`; `ProjectileSpeed` remains the one speed source. Omitted configuration and `legacy` preserve Ember's target-following steering. `straight` commits at release. `bounded_homing` limits signed angular change each simulation step. The verification-only `fixture_homing` demonstrates 55 degrees/s. Lost/dead targets leave a projectile following its last heading to lifetime or collision. A source moving to another region invalidates its projectile.

`ProjectileFlight` uses analytic segment/disk actor tests and a radius-aware grid DDA with rounded tile sweeps. Earliest contact wins; terrain wins near-equal terrain/actor contacts, and actor ties use ordinal ID. Walls, rocks, locked doors and boundaries block. Other living hostiles can intercept; friendlies neither block nor receive damage. There is no piercing or splash. One list removal follows one terminal result; actor contact calls the existing Resolve/Damage pipeline, with dodge/invulnerability before status application. Expiration emits diagnostics, never an impact burst.

Actual collision emits `ProjectileImpact` with `actor`, `terrain` or `boundary` outcome. The existing controller coalesces actor collision with Hit/Miss/avoidance and flushes terrain contact to one subdued terrain effect. Release never implies a hit. Flight bodies read authoritative position/heading; Ember uses a warm core/streak and Spit a larger green organic core/streak. Streaks are bounded draw commands, not autonomous simulated particles.

Schema 1 remains unchanged. Heading and region are optional saved projectile fields. Legacy records derive region from source and normalize heading toward the retained target (unit X only if neither a valid heading nor target direction exists). Remaining lifetime is retained. Saves never regenerate actors/loot. Trails/effects reconstruct cleanly and do not execute damage. Tactical cadence/intent are optional actor fields.

## Reproduce encounters

Run `python3 tools/gameplay-verify.py --presets baseline,precise` under a working X display. The suite adds ten `tactics-*` scenarios: prowler, brute, spitter, mixed, spacing, straight evasion, bounded homing, wall, dodge and interruption. `tactical-state`, `tactical-event` and `tactical-result` JSONL rows include profile, intention, selected ability, headings and terminal observations. Frame-labelled contact sheets and full MP4s come from the real Godot renderer.

For hands-on testing: Menu → Developer diagnostics → **Combat archetypes test encounter (F9)**. This enters the reversible feel course, retains a normal-progress snapshot, and places one of each archetype. Return through the existing feel lab. F9 is a developer shortcut, not a new gameplay action. The Android smoke check uses the same fixture after normal control/inventory tests and checks real spit release and three resume cycles.

## Verification and risks

See `PR4_PART3_PERFORMANCE.md` for measured results and exact commits/artifacts. `CombatAiChecks`, `ActorSeparationChecks`, `ProjectileChecks` augment all prior checks. `VisualChecks` retains the original Part 2 five-workload comparison and adds idle, 1/3/4/8 enemies, projectiles and effects, with normal-time frame percentiles and opt-in subsystem timing.

AI runs on the existing five-Hz opportunity cadence, with slower per-profile minimum intervals. Avoidance is spatially bucketed; target perception and projectile hostile interception still scan the bounded current actor population. Revisit those queries before substantially increasing persistent population/projectile counts. Profiling is off by default. PPC action sampling/effect/audio limits remain unchanged. CPU/software-GPU evidence cannot certify ARM64 performance.

The historical intermittent Android resume-to-black failure remains a risk even when repeated emulator cycles pass. Part 4 must test physical S25 touch comfort, repeated combat/background transitions, long sessions, new projectile readability and multi-enemy frame pacing. Preserve air-only sword misses, confirmed impacts, fixed cones, all defenses, roll dust, inventory/quests/saves and normal NPC schedules.

Part 4 owns control layout, HUD usability, encounter balancing and final physical-device signoff. No Part 4 implementation is included here.
