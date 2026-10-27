# Movement feel investigation

The physical report was slow, awkward movement despite passing system tests. PR #2 at `620b50d` was the starting point; 347 assertions were reproduced locally before extending the suite to 376 (including controlled-course save isolation).

## Confirmed path and causes

Android touch → Godot `InputEventScreenTouch/Drag` → TouchStick's owned finger/anchor/displacement → remapped analog → Foundation fixed 60Hz accumulator → screen-to-ground projection → World.PlayerMove → Stat(speed), action gate and substepped RegionState circular-footprint collision → authoritative actor coordinates → CharacterView → PPC external motor/renderer → Camera2D/canvas transform → display. Hotbar fingers have independent ownership; emulated mouse duplicates are filtered only for that hotbar. Menus/backgrounding cancel movement. A last-child input observer records events before a control consumes them.

1. **Starting speed hit the anatomy minimum of 40 ground px/s**, 1.67 tiles/s. AnatomyAdapter clamps generated speed to 40–100. This is a poor sole tuning policy for player navigation. NPC statistics and saved actor speed remain intact; player control now has an explicit multiplier, still applied to World.Stat so equipment/status effects remain effective.
2. **Directional appearance was inconsistent.** The old inverse projection normalized ground length; projection then halved vertical speed. Measured full-input projected speeds were 40 horizontal, 20 vertical and 25.30 diagonal canvas px/s. This is confirmed behavior, not an inference from screenshots.
3. **Camera follow compounds perceived lag.** Follow rate 12 has an approximate 83ms time constant away from bounds; precise uses 20 (~50ms). Edge framing deliberately clamps the target, so distance from player to screen center is not always follow error. Teleport markers must be excluded from camera settling conclusions.
4. **PPC external drive can alias velocity under catch-up.** It inferred velocity from one host position across multiple animation steps, then observed zero displacement on later steps. An optional measured external velocity now keeps animation speed consistent with accepted movement. Original upstream behavior remains available for other hosts and baseline preset.
5. **Collision discarded the complete blocked substep before sliding**, leaving up to 2px of unused approach distance and always preferring X. Contact bisection now retains the safe part, then slides the remaining request in dominant-axis order. Terrain authority and radius restrictions remain unchanged. This reduces a quantization source; subjective stickiness still needs phone feedback.

The joystick already had a floating anchor and remapped dead zone. There was no evidence that it failed to reach full magnitude in the deterministic full-drag scenario. The lab changes dead zone from .10 to .08 and uses a .8 response exponent for useful small thumb motions. No simulation acceleration or frame-dependent movement was introduced.

## Initial controlled experiments

Actual rendered movies and JSONL were captured for all four configurations before the later contact/animation refinements. `movement-experiments.json` records these working-tree measurements and their limitations. The final CI regenerates evidence at the exact committed head.

| Preset | Projected H / V / diagonal px/s | Response | Follow | Purpose |
| --- | --- | --- | --- | --- |
| Baseline | 40 / 20 / 25.30 | immediate | 12 | Original input/speed/camera settings |
| Precise | 96 / 96 / 96 | immediate | 20 | Provisional default, precise combat/navigation |
| Soft | 96 / 96 / 96 | 45ms exponential | 14 | Short smoothing, immediate release |
| Explore | 128 / 128 / 128 | 70ms exponential | 18 | Faster travel, wider view (zoom 1.35) |

These are perceptibly distinct policies, not three tiny speed changes. Initial movies passed displacement, real-event presence, release, clearance and temporal rendering checks. The directional comparison isolates projection mathematically and experimentally. The presets also combine speed/camera/input tuning; they are **not** a causal experiment isolating every parameter. A fastest-travel score did not select the default: precise preserves immediate reversal while improving pace without explore's higher speed and smoothing. Physical preference is still unverified.

At the initial 40px/s anatomy, a straight 96-ground-pixel chest spacing takes 2.4s to center versus 1.0s at precise; entering the 48px interaction radius takes 1.2s versus .5s. These are scale calculations, not measured complete interaction times. Real replay route distances, positions, blocked periods and measured speed appear in `scenarios.json` and `report.json`. Vertical ground travel is faster by design to produce equal projected motion; NPC movement remains in ground units. Combat balance will need assessment against this player-only directional advantage.

## Controller, camera and animation contract

- Screen vectors retain analog magnitude and a unit projected maximum; diagonal input gets no projected speed advantage. Ground Y can reach twice X because of the half-height projection.
- Release is immediate for every preset. Soft/explore smooth changes with `1-exp(-dt/response)`, independent of update frequency. Blocking actions and menus clear smoothed input and stale velocity diagnostics.
- Collision uses bounded 2px substeps, safe contact refinement and authoritative circular geometry. No automatic obstacle nudges or pass-through terrain.
- Player raster cadence is 20Hz versus the old 10Hz in non-baseline presets. Other creatures remain 10Hz. Accepted displacement drives facing/locomotion; actions still use PPC action cues and World restrictions. PPC gait blending is retained, so perfect foot planting is not claimed.
- Camera uses Godot's delta-aware follow smoothing and existing map bounds; there is no camera shake or look-ahead. Baseline remains switchable on the phone.

## Verification and remaining uncertainty

The integration suite checks 30/60/120Hz elapsed-time displacement, all eight directions, stale-motion reset in Windup/Recovery/Staggered/Dead, invalid input, immediate release, actual gap traversal at radii 6/9/11, rejection at radius 13, reversal after wall contact, and motor catch-up velocity. Existing multi-seed feature and dungeon routes, anatomy relocation and save roundtrips remain required.

Hosted software-renderer frame times are recorded separately from simulation. Movie clips have imposed timing; their capture wall times do not measure gameplay FPS. Real-time reports contain p50/p95/p99 render intervals. No physical S25 performance measurement or subjective comfort result is available from this environment. Sampled contact sheets were visually inspected; this alone does not certify smooth animation. See the latest Actions evidence for final source-identical clips and logs.

A local **real-time FFmpeg/Xvfb** capture (llvmpipe, instrumented working tree before final packaging) measured render interval p50/p95/p99 **14.88 / 22.16 / 32.98ms**, with 33.32 simulation seconds during 33.74 wall seconds. The real-time trace matched the independent fixed-clock replay after flushing injected batches. These figures are a short desktop software-renderer sample, not an S25 measurement or a sustained 60FPS guarantee. Sampled real-time frames show live world movement, dungeon presentation and combat; the clip includes startup before gameplay, which is explicitly visible in its contact sheet.

### Physical feedback follow-up: combat and right-hand controls

The player reported that the left stick was comfortable and preferred the soft/explore range. Those presets and the default are unchanged in this follow-up. The next reported problems were involuntary turns on receiving a hit, weak attack presentation, difficult Android menu scrolling, and uncomfortable action buttons.

Confirmed in code: `Damage` moves a victim 15 ground pixels immediately, and the creature's facing followed that displacement as though it were walking. Stagger now disables velocity-driven facing and locomotion velocity; the normal world position and authoritative knockback/collision remain intact. Presentation events carry the actual other actor, fixing the previously hard-coded leftward hit cue. Facing resumes with intentional movement after stagger.

Sword gameplay uses 140 ms windup and 240 ms recovery, whereas the generic creature action lasted around a second scaled by traits. Gameplay actions now map the rig's strike peak to the ability windup and finish at the end of recovery. Armed strike presentation adds torso rotation and arm extension, plus a short directional sword trail centered on impact. The trail is presentation, not an extra hit or an increase in damage/range. Native creature/export actions retain their original timing and amplitude. Other creature families keep their own action rigs.

Mobile controls now place a larger Attack target low on the right, arrange secondary actions above/alongside it, and separate Pack/Menu from the primary target. Cooldowns wrap onto a second line to preserve hit-target geometry. Menu swipes explicitly own one finger and scroll from anywhere over ordinary rows; release after a swipe or cancellation cannot activate a row. Sliders, selectors, scrollbar drags and desktop mice retain native handling. The scrollbar is wider. This addresses a reproduced engine-event swipe failure, but final thumb comfort needs another phone pass.

Regression evidence includes motor facing/timing assertions, a rendered menu swipe over button rows, and an `attack-readability` replay. That replay uses the production Attack button three times and an explicitly labeled NPC bite command to exercise real windup, damage, collision and hit presentation. Its NPC position is reset before the bite (marker in the trace), and facing must stay within 0.12 radians across the incoming hit. It does not claim autonomous NPC behavior or subjective combat quality has been certified.
