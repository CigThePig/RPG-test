# PR #7 — Revised Part 3: Character and progression

## Implementation

Character now mounts its own full safe-area composition through `UiScreenHost.MountCustom`. Wide views keep a hero/gear pane alongside independently scrolling Stats, Train and Abilities pages. Compact views add a full-width Hero page. The breakpoint uses available safe width divided by text scale; required controls retain the shared 56-unit minimum target and persistent Back button. The selected page survives confirmation refresh and resizing.

The hero preview shares the existing player `CreatureActor.Texture`, including actual PPC equipment appearance. It adds no creature, actor, simulation, render loop, texture upload or per-frame asset generation. It reads alpha bounds once per open/rebuild to trim the transparent PPC canvas with a shared AtlasTexture. Missing art gets a written family/portrait fallback. The screen includes actual level, health/mana, XP, weapon/body item icons and titles, and equipment navigation. Wayfarer is a presentation title, not a new gameplay class.

Stats use `CharacterOverview.Breakdown` and `World.Stat` for effective/base/equipment/training/effect contributions, with explicit signed bonuses. Training shows actual ranks, availability/mastery, one-point costs, current breakdown and projected total. Abilities distinguish Learned, Available and Locked with actual requirements, mana/range/cooldown and blocker reasons. Enemy-only abilities stay out of the learnable list unless already known.

All spending retains the existing confirmation and `Progression` authority. Training additionally validates the captured player/rank at acceptance so a repeated activation cannot purchase a second rank. Navigation retires custom input trees using the existing deferred disposal contract. World state, save schema, combat and item transactions are unchanged.

## Verification

- Native .NET 9.0.203 build passed.
- Fresh Godot 4.6.1 Mono import completed.
- Native headless suite passed: 8,889 assertions.
- Rendered desktop/mobile results and CI run status are recorded in the PR description after execution.
- Rendered coverage includes pages/portrait, cancel, duplicate confirmation with points remaining, disabled training, existing production item/quest/map flows, safe-area viewport checks and enlarged text.

## Physical Galaxy S25 checklist — pending

1. Open Character from Menu; check hero/gear, Stats, Train and Abilities in landscape.
2. Equip/unequip in Pack and reopen Character; verify the preview and numbers agree.
3. Scroll each page in both directions; swipes must not activate training or leave pressed tint.
4. Cancel training with Back; confirm points/ranks stay unchanged. Confirm once and verify exactly one rank/cost.
5. Check learned versus level/point-blocked abilities; learn an available ability and verify after save/load.
6. Try 140% text, background/resume, Android Back and reopening the screen. Check readability and thumb comfort.

This is revised Part 3 of the six-part plan. `PR7_PART3_HANDOFF.md` remains the historical foundation milestone. This PR stays draft and unmerged; automated screenshots do not constitute Galaxy S25 visual or performance acceptance.
