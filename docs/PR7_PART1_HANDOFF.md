# PR #7 Part 1 handoff

Implementation: `422cecba2ceff7a9d0e2e7b131713985fb550cd3` on `feat/mobile-ui-ux-overhaul`. A follow-up preserves the screen caller across inventory Use/refresh; see the final PR head for this fix and its regression. Keep PR #7 draft. Parts 2–4 and physical S25 acceptance remain outstanding.

## Delivered

- `UiNavigation`: presentation state/return paths, modal priority, defeat-safe Back. `UiScreenHost`: persistent title and Back, TouchScroll body. `RpgUi` remains the compatibility facade.
- `UiDesign`: shared forest/ivory/bronze theme, focus outline, wrapped button/text, spacing/target metrics and notification styling.
- Responsive overlays and a separate notification region; physical insets use the existing screen-transform conversion. Standard/Large screen text option (session-local until Part 4).
- CharacterOverview uses actual World/Gloot-derived stats, vitals, learned abilities, skills, appearance and exploration capabilities. Existing learning/training/casting controls remain below the overview.
- Menu → screen → Back returns to Menu. Direct open → Back returns to gameplay. Escape and Android system Back share behavior. New Game requires explicit confirmation. Defeated Resume explains recovery; Menu/Load/New Game remain usable.
- Transitions, resize, dialogue, defeat and focus loss cancel gesture ownership/motion. Lower UI hides beneath dialogue/confirmation; focus returns predictably.
- `PR7_UI_INTERACTION_SPEC.md` contains the transition and accessibility contract.

## Verification

`UiNavigationChecks.Run` is registered in FoundationChecks. `UiNavigationChecks.Rendered` runs in both full rendered suites and can be isolated with `--visual-check --ui-only` (add `--mobile-ui` for the phone layout). Existing MobileCombatControlsChecks are preserved.

Local verification before this handoff: .NET 9.0.203 build succeeds with existing addon warnings; Godot 4.6.1 import exits 0; headless suite passes 8,750 assertions; full desktop and mobile rendered suites pass. The final-source repeat of headless, full desktop and full mobile suites also passes. A subsequent focused fix preserves the Pack menu caller across potion use; actual production-item rendered regressions pass in desktop and mobile layouts. Exact final-head hosted workflows are tracked in PR #7. Local Godot import also emits an EditorSettings shutdown diagnostic during editor teardown; no runtime verification errors were accepted. Hosted workflows remain the authority for pinned .NET 8 engine hosting and Android export.

Rendered coverage: five viewport sizes (1280×800, 1280×720, 1920×864, 1024×768, 900×560), large screen text and nonzero safe insets, real touch Back, keyboard Escape, WM Back, focus, destructive cancellation and lifecycle pause. The native suite retains translated/scaled safe-area conversion and combat/touch ownership assertions. Pilot baselines are emitted to `docs/native-screenshots/*ui-character*`; full suites also emit standard Character/menu, dialogue, combat and adventure evidence.

Implementation-source workflow entry points (final run URLs/status are recorded in PR #7):
- Foundation: https://github.com/CigThePig/RPG-test/actions/runs/41390646111
- Android: https://github.com/CigThePig/RPG-test/actions/runs/41390646113
- Gameplay: https://github.com/CigThePig/RPG-test/actions/runs/41390646160

## Android smoke adaptation

The first APK built successfully, but the old Android smoke test tapped a fixed Resume coordinate that now belongs to Pack. It also assumed New Game had no confirmation. The follow-up reports actual physical navigation-button centers after Container layout, consumes these in OS-touch smoke, verifies Android Back cancels the destructive modal, and explicitly confirms reset. Production gameplay and button geometry are unchanged by this test adaptation. Use the final PR head's APK and workflow links, not an earlier build.

## Boundaries and phone acceptance

HUD/combat layout remains for Part 2; comprehensive RPG-screen presentation for Part 3; dialogue styling, independent preference persistence and developer/player menu separation for Part 4. Existing progression remains accessible. UI state does not alter world save schemas, inventory transactions, quests, dungeon generation, attack geometry or audio hit/miss semantics.

Phone checklist: Menu → Character → scroll → Back → Resume; direct Pack → system Back; Large text; background/resume; held movement/action canceled by menu; New Game Cancel; defeated Load/New Game recovery. Confirm real thumb comfort, readability, target separation and no ghost taps on Samsung Galaxy S25. Automated desktop/Mesa touch emulation cannot establish these subjective hardware results. Do not mark physical acceptance complete without Ross's test.
