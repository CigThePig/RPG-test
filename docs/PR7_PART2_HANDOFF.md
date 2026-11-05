# PR #7 Part 2 handoff: gameplay HUD, thumb ergonomics and interaction feedback

Builds on Part 1 (`PR7_PART1_HANDOFF.md`). Keep PR #7 draft. Parts 3–4 and physical Galaxy S25 acceptance of Parts 1–2 remain outstanding.

## Delivered

- **Gameplay HUD** (`game/ui/GameplayHud.cs`). It replaces the text-only status line with three things. First, an identity line: name, level, region, time, and a DEFEATED/dodging status word. Second, captioned HP/MP/XP bars that show numbers. Low health adds `LOW` and a bright outline, so the warning never relies on colour alone. Third, a compact quest tracker. The HUD's rectangle is fixed (≤400×196 logical units, top-left inside the converted safe area, left of Pack/Menu), and values only redraw. The world stays visible behind a translucent tracker.
- **Read-only quest objective** (`Narrative.objective()` → `QuestObjective`). Fields: id, title, status (`active` / `reward_pending` / `available`), objective text, staged, stage id, progress, required, pending reward and reward. Priority: active quest, then unclaimed reward, then the warden's next offer. It never mutates quest state and never parses `journal()`, which is unchanged for compatibility. Part 3 should expand this into journal rows and stage history.
- **Contextual Interact.** `World.PreviewInteraction` mirrors `World.Interact`'s target walk and mutates nothing. `Foundation.PreviewInteract` adds the merchant Talk precedence. Labels: Talk, Open chest, Gather herb/ore/wood, Take relic/key, Pull lever, Kindle, Unlock, Enter ruin, Leave ruin, Loot. Unavailable states are named too: Need key, Gate shut, Sealed, or plain Interact when nothing is near. Interact is never disabled; pressing it when unavailable still gives the authoritative explanation.
- **Action availability.** Cooldown seconds plus a proportional shade (a child control, so the touch rectangle is unaffected). Low mana disables the spell and says `low mana`. Ember shows `no target` (advisory, still enabled). The draught count comes from Gloot (`×3`), and the button is disabled at zero. Tapping a disabled control shows a concise reason and never dispatches or takes finger ownership. Failed casts name the first actionable reason: not learned, cooldown, mana, busy, weapon, or no target.
- **Notifications.** Up to two lines in the existing notification region. Ability-name confirmations are minor and never displace quest, discovery, loot or craft notices. A quest stage and the loot that caused it arrive together and are shown together. Level-ups are announced.
- **Thumb ergonomics.** Mobile layout version 2: Interact grows from 100×72 to 108×86 and moves inward so its two-line label reads as a primary action next to Dodge and Attack. Every other rectangle, every action ID, release-inside ownership, multitouch, mouse-emulation deduplication and safe-area conversion are unchanged. Desktop hotbar buttons are fixed at 140×72 with clipped two-line labels, so labels can never resize the grid.
- **Budget.** Per frame, the HUD and ability labels compare cached integer keys and update only changed controls. Gloot counts, interaction previews, target scans and objective reads run every sixth frame.

## Verification

- `HudChecks.Run`, part of the headless FoundationChecks suite, covers six areas: objective transitions (available → active counted → completed → next offer → staged stages), no mutation, save/load fidelity, pending reward under a full pack, preview/Interact agreement for real reach and ruin features (seed 1; levers/portcullises on seed 33), lock with and without a key, merchant Talk precedence, and defeated actors.
- `HudChecks.Rendered`, part of both full rendered suites, can be run alone with `--visual-check --hud-only` (add `--mobile-ui` for phone). It checks the HUD at 1280×800, 1280×720, 1920×864, 1024×768, 900×560 and 2340×1080 (S25 landscape window) with nonzero insets. It checks that the HUD and two-line notices clear every control. Touch rectangles must stay identical while cooldowns, mana, potion count, interaction label and HUD values change. A disabled tap must explain without dispatching, the Open chest label must actually open the chest, and quest completion must not be hidden by a routine attack notice. `MobileCombatControlsChecks` is unchanged and still passes on the new geometry.
- Screenshots: `docs/native-screenshots/{,mobile-}hud-*.png` (sizes, `-unavailable`, `-interact`).
- Local results on this head are listed in the PR. The local environment was .NET 9.0.203 with the .NET 8.0.15 runtime and Godot 4.6.1 mono, using Xvfb/Mesa rendering. Hosted workflows remain the authority for the Android export and smoke run. Android smoke now reads `MOSSLIGHT_CONTROL version=2`.

## Known limitations

- HUD font sizes are fixed (identity 18, bars 15, tracker 16 logical units) and do not follow the Standard/Large menu option. This keeps the HUD rectangle stable over the action cluster. Whether HUD text should scale is a Part 4 accessibility decision.
- A chest or resource next to a full pack is labelled by its action, not by "Inventory full". The press reports the full pack, but the preview does not simulate Gloot capacity.
- `no target` for Ember is a presentation check (living hostile in range and line of sight). Cooldown, mana and busy state are reported separately so the label never flickers mid-swing.
- Logical units are not Android dp. Thumb reach, occlusion and readability on a Galaxy S25 still need a physical test.

## Phone checklist (Galaxy S25)

1. Read HP/MP/XP and the quest tracker in daylight. Confirm they never cover an enemy telegraph you need to see.
2. Walk to the starting chest: Interact should read "Open chest" and brighten. Press it and confirm the chest opens. Walk away and confirm the button returns to a dim "Interact".
3. With the left thumb holding the stick, use Attack, Dodge, Interact, Mend and Draught with the right thumb. Is Interact comfortably reachable without covering Dodge?
4. Spend mana, then tap Mend: it should explain "needs N mana" without casting. Use all draughts and tap Draught: it should explain how to get more.
5. Accept the prowler quest from the warden and defeat a prowler: the tracker should show 1/2. Save, load and confirm the tracker matches.
6. In the ruin, check that the Interact labels for keys, locked gates (Need key → Unlock), levers, braziers and seals read correctly.
7. Open the menu mid-gesture, background the app and resume, and get defeated. There should be no ghost actions, and the recovery path should remain.
