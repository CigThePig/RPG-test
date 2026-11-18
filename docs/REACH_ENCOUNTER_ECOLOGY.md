# Living Reach encounter ecology

Fresh adventures plan 42 non-respawning sites over the stored 320×240 Reach. Existing adventures have no ecology revision and keep their original terrain, population, quests and loot. Start another slot to see the new sites; there is no automatic migration into an occupied old save.

## Authored encounters

| Template | Composition | Site requirements and purpose |
| --- | --- | --- |
| Lone hunter | Mire prowler | Near woodland/grass, resting post; basic observation and melee |
| Hunting pack | Three prowlers | Broad woodland space, separated reachable homes; pounce and pack calls |
| Rocky guardian | Brute | Rock within ten tiles, 240-unit charge lane, lateral dodge clearance |
| Poisoned shore | Spitter, mire prowler | Dry ground 2–7 tiles from water, open sightlines and retreat space |
| Woodland ambush | Stalker | Woodland edge near approaches, disturbed undergrowth; existing ambusher AI |
| Alarmed ground | Howler, two prowlers | Open woodland, at least six movement spokes; support and escalation |
| Defended refuge | Shellback, mender | Rocky woodland, connected supporting positions; guard and healing |
| Coiled threat | Lasher | Damp/rocky fringe, clear long lane; coil and fang |
| Pack leader | Alpha, three prowlers | Remote woodland away from approaches; howl, pack tactics and morale |

No enemy stats scale with player level. Complexity unlocks at 1,350 / 2,200 / 3,500 world units from spawn. Town buildings have a 300-unit placement margin; arrival has a 720-unit sanctuary. Objectives have protected interaction space. Ecology perception/pursuit also respects the settlement sanctuary.

The planner floods the real collision map from arrival using an 11-unit ground footprint, builds water/rock/approach distance fields, evaluates eight travel spokes, checks charge and escape lanes and validates separate connected group homes. A bounded candidate lattice is scored against each template, with stable hash tie breaks. Sites remain at least 600 units apart. Their nominal combat territory is 192 units around the post; existing per-profile roaming and leash rules govern actual movement. Terrain and collision remain unchanged. Non-colliding signs use one nearby draw list; tall cosmetic silhouettes clear the homes and important charge/ranged lanes.

## Authoritative population and presentation

Saved site IDs use ecology revision and world tile. Creature IDs add their composition slot. A small territory index queries nearby sites independently of scenery chunks. Within 900 units, one creature is activated per six fixed ticks; at most 24 ecology actors can be resident. The same limit includes corpses. A missing member is created with `EnemyRoster.Create`; previously visited members restore their exact actor state and stored PPC genome.

Beyond 1,250 units, eligible actors move out of `World.Actors` into dormant snapshots. Combat awareness, committed actions, statuses, targeting, projectiles and hazards pin their participants. Active pursuits continue the existing AI beyond the old 800-unit ordinary simulation cutoff, including returning after a region change. Nothing despawns from a live fight. Saturation delays a new site instead of evicting a combatant.

Character views are added incrementally and removed when no longer nearby. Distant planned creatures generate no PPC bodies or renderers. View synchronization does not rebuild the full scene or terrain on population changes.

Ally calls, fallen-ally reactions and support effects are scoped to their ecology site. Legacy and dungeon inhabitants retain their prior behavior together. Player noise and direct perception can still legitimately attract another nearby group; site boundaries are not invisible walls.

## Save contract

Schema 1 and `rpg-regions-2` are unchanged. Optional region fields store the ecology revision and complete plan. The save contains both active and dormant actors, with an explicit dormant-ID list; all actors pass the existing validation before restore partitions them. This preserves damage, action state, death and inventories without inventing a second loot or reward system. Missing ecology fields mean no new population. Dormancy does not advance statuses or cooldowns; participants with live statuses remain active. Death and Gloot transfers remain production-owned and cannot repeat through reactivation. There is no respawn timer.

## Reproduce verification

- `python3 tools/verify.py --headless --test-only=ReachEncounterChecks` writes six seeded placement overviews, candidate rejection counts, lifecycle checks and normal-health production combat reports under `build/ecology/`.
- `xvfb-run -a -s '-screen 0 2560x1440x24' python3 tools/ecology-verify.py` captures actual planned encounters at 2340×1080, including ability windups and partial-combat save/restore. The scripted duelist uses movement, dodge, sword, ember, mend and starter potions. The recording advances four fixed simulation ticks per displayed frame; it is behavior evidence, not a frame-rate benchmark.
- The existing integration, adventure, visual, gameplay replay and Android workflows remain required.

`plans.json` separates plan generation time from activation cost. `combat.json` distinguishes stationary opening pressure from a duelist that responds immediately; a failed stationary exposure does not measure unavoidable damage. `rendered-combat.json` reports simulated time, abilities, hits, ally calls, active/view counts and simulation-batch percentiles. Existing scenery instrumentation continues to measure worker, upload and eviction costs.

Physical Galaxy S25 60 FPS, occupied-territory p50/p95/p99/worst frame pacing and prolonged memory behavior require device measurement. Desktop llvmpipe results cannot establish those outcomes. The performance overlay and existing trace pipeline remain available.
