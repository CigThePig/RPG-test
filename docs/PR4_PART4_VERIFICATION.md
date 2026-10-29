# Part 4 verification ledger

Work continues from `1bcb92784322b255bd4b85b7de1259c0e04a0703`. The original controls were captured from the baseline compiled assembly before rebuilding Part 4; actual new mobile controls were inspected after the first successful rendered run. Final exact-source CI results and artifact links will be recorded here after completion. Intermediate local source is dirty and must not be confused with a final committed APK.

## Changes and balance assessment

Attack/Dodge now have distinct larger touch targets; secondary spells/items occupy a separate row, Interact stays fixed and contextual, and utilities move to the upper right. Stable action IDs replace text lookup in gameplay replay/control tests. Safe-area conversion, compact geometry, synchronous input cancellation, crowd locator and a pale projectile rim are covered by dedicated checks. Menus keep the existing native-control/TouchScroll path; focus cancellation is hardened.

The lab offers normal/protected individual, two-melee, three/four mixed, projectile and eight-enemy fixtures. Normal health/resources and authored 65/120/55 enemy health replace generic lab enemy health; snapshot isolation is retained. Baseline enemy timings/speeds/damage and dungeon generation are retained deliberately. Stationary seed-42 exposure over 12 seconds recorded 8 Prowler hits / 112 damage, 6 Brute hits / 104 damage, and 5 Spit hits / 65 damage. Mixed fixtures killed the stationary player; this establishes that protected observation was insufficient for difficulty assessment, not that normal play is unfair. The same production mechanics and active-defense replays are required before balancing further; no values are changed to hit an invented win-rate target.

| Archetype | Final health | Speed | Attack windup / recovery | Other |
|---|---:|---:|---|---|
| Prowler | 65 | 88 | Bite 0.30 / 0.40 s | Bite base damage 9 |
| Brute | 120 | 46 | Slam 0.80 / 0.85 s | Slam base damage 14; 2.40 s cooldown; 52-unit, ±55° cone |
| Spitter | 55 | 62 | Spit 0.48 / 0.50 s | Base damage 7; 1.80 s cooldown; 135 units/s straight flight |

Actual damage includes existing actor stats/mitigation. Ember's legacy steering/interception, swept collision, sword timing, aim/lunge and all roll timing/immunity remain unchanged. Dense stress groups can still overlap; the conditional ground locator identifies player position without replacing soft avoidance with collision or attack tokens. The historical resume-to-black risk is not declared permanently fixed.

## Local results

- SDK 9.0.203, Godot 4.6.1 mono, pinned .NET 8 host. Native build: 58 pre-existing nullable warnings, no added warnings after fixes.
- Initial Part 4 integration passed 3,739 assertions. Subsequent source and final counts must be verified below.
- Initial mobile rendered suite passed and real viewport controls were inspected. Additional input tests and later presentation changes require another rendered pass.
- Fresh asset import exited successfully but emitted the known editor-shutdown EditorSettings error; this is not a clean runtime check.
- Final Android/replay/readiness checks pending. PR stays draft until executable requirements pass.

## Physical acceptance

See ANDROID_PLAYTESTING.md. Physical-device touch comfort, subjective combat feel, audio quality, and sustained ARM64 performance remain pending user acceptance before merge.
