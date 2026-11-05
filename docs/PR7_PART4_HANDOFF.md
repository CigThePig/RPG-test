# PR #7 Part 4 handoff: dialogue, menus and settings, accessibility, final acceptance

Builds on Parts 1–3. With this part, all four planned parts of PR #7 are implemented. **Physical Galaxy S25 acceptance has not been done and remains the release gate.** Keep the PR draft until Ross has run the checklist below on the actual ARM64 APK.

## Delivered

### Dialogue (`Narrative.gd` presentation, `RpgUi.Layout`)

- Dialogue Manager still owns lines, responses, conditions, flags and advancement (`advance`, the `choices` container and the epoch guard are unchanged). RpgUi now supplies only presentation through `Narrative.present(theme, rect)`.
- **Theme:** the shared theme at the current text size, or high contrast when that is on.
- **Layout:** a panel in the lower part of the converted safe area, at most 900 wide and about half the height. It sits on its own canvas layer above the gameplay UI.
- **Contents:** a speaker label (bronze, upper case), the line text, then responses. Responses are at least 64 tall, wrap, and sit in a `TouchScroll`, so a swipe never selects one.
- Conditional responses that are not allowed stay hidden, as before.
- **Focus** starts on the first response. Back and Escape never dismiss dialogue. Losing app focus or going to the background keeps it open and waiting, and gameplay touches are ignored while it is open.

### Menu, Save & load, Settings, Developer tools

- **Menu (player entries only):**
  - Enter / Resume, Pack, Journal, Character, Map
  - Save & load, Settings, New game (with confirmation)
  - **Developer tools**, Quit

  The Android target names `resume`, `new_game`, `cancel_new_game` and `confirm_new_game` are unchanged.
- **Developer tools** screen: creature workshop, collision/navigation overlay, performance overlay, game-feel lab/playtest recording, and developer diagnostics with the combat labs and integration checks. Nothing was removed, and every test hook still works.
- **Save & load** screen: three slot cards plus the background autosave. Each card shows its state.
  - **Saved game:** level, region, time of day and save time, read with the same validation Load uses.
  - **Empty.**
  - **Older version:** kept safe; it can be neither loaded nor overwritten, and a reason is shown.
  - **Damaged:** it cannot be loaded. "Replace damaged file" copies the file aside (`slot-N.json.damaged-K.json`) before writing.
  - Saving to an empty slot is immediate. Overwriting a saved game, loading, recovering the autosave and replacing a damaged file all ask first, and Cancel or Back changes nothing.
  - Saving while defeated or inside the controlled course is disabled, with the reason shown.
  - Historic-save protection (older generator never overwritten), the autosave rules and the save format are unchanged. `Persistence.Inspect` and `ReplaceDamaged` are new and never delete anything.
- **Settings** screen, using the new `UiPreferences` stored in `user://ui_preferences.json`, separate from world saves:
  - Text size: Standard 1.0 / Large 1.3 / Largest 1.4. This covers menus, screens, confirmations, dialogue and notices (capped so two lines still fit); the HUD stays fixed.
  - Sound on or off.
  - Reduced motion: removes the camera impact nudge via `CameraFeedback.Strength`. Combat timing, hit/miss sounds and cues are unchanged.
  - High contrast: opaque panels, ivory borders, brighter secondary text and an opaque quest tracker.
  - Camera zoom (session only), and Restore defaults (with confirmation).
  - Changes apply and save immediately. Loading a game or starting a new one never changes them. A damaged or unrecognised settings file falls back to defaults, says so on the Settings screen, and is not overwritten until the player changes a setting.
- Verification runs use their own per-layout folders (`user://verification/{desktop,mobile}/`) for settings and manual slots. Automated tests can no longer read or overwrite a player's settings or slots.

### Accessibility completion

- **Contrast:** every text and indicator token passes 4.5:1, checked in code. The lowest is XP bar text at 4.7:1.
- **Disabled controls:** a distinct face, and a written reason next to them everywhere.
- **Selection and state:** shown with words or brackets, not colour alone.
- Menu entries are at least 56 units tall and Tab or directional focus reaches every entry. Dialogue focuses its first response.
- No flashing and no new animation.

## Verification

- `AccessibilityChecks.Run` (headless) covers:
  - token contrast
  - preferences round-trip, damaged files (untouched) and unknown versions
  - no preference fields in world saves
  - slot states (empty, ready, older generator, failed validation, truncated JSON), with Inspect never mutating files
  - an older adventure is never overwritten; replacing a damaged slot keeps a copy
- `AccessibilityChecks.Rendered` runs in both full rendered suites, or alone with `--visual-check --part4-only`. It covers:
  - the player-only menu: 56-unit targets and a complete Tab focus order
  - Developer tools reachable, with Back returning to the menu
  - each setting applied, persisted and reloaded, with world state untouched and kept across a load
  - Restore defaults behind a confirmation
  - Save & load: empty → save, overwrite Cancel (file untouched), Load Confirm (state restored, play resumed), older and damaged slots disabled with reasons, damaged replace that keeps a copy
  - dialogue at 1280×800, 900×560 and 2340×1080 with Largest text and insets: inside the safe area, 56+ responses inside the panel, first-response focus, surviving Back/Escape/focus loss, no attack from a tap where Attack lives, closing through its own responses
- The full **The Buried Light** route was run with the revamped UI: `--adventure-check` (default and ruin 33) and the rendered `tools/adventure-verify.py` route, which uses live Dialogue Manager choices, journal and map, and finishes at the reward checkpoint. Results are in the PR.
- Screenshots: `docs/native-screenshots/{,mobile-}menu-part4.png`, `developer-tools.png`, `settings.png`, `save-load.png` and `dialogue-{1280x800,900x560,2340x1080}-largest.png`.

## Known limitations

- The Android smoke test still taps dialogue responses at the logical coordinates `Narrative.gd` logs (unchanged behaviour). Its menu target names are unchanged, but the menu no longer contains Save/Load/Audio rows. Check the hosted Android run.
- There is no control remapping, HUD scaling, control-opacity setting or colour-blind palette. Information never depends on colour alone, so none was required for correctness, but players may still want them.
- Logical units are not dp. Thumb comfort, physical readability and OS font scaling on the S25 are unverified.
- All four parts are on the PR branch. Current hosted run URLs and results are tracked in the PR description; physical acceptance is still pending.

## Final phone acceptance checklist (Samsung Galaxy S25, ARM64 APK)

1. **Reach and readability:** HUD bars and tracker, action labels and notices are legible in daylight. Attack, Dodge and Interact are comfortable with the right thumb while the left holds the stick.
2. **World visibility:** the HUD and notices never hide a telegraph you need. Dialogue leaves the top of the world visible.
3. **Multitouch:** move while attacking, dodging and casting. Opening the menu mid-gesture leaves no stuck movement or ghost actions.
4. **Scrolling:** swipe Pack, Journal, Settings and the dialogue responses without triggering rows.
5. **Menus and dialogue:** every screen's Back and Android Back return to its caller. Back does not close dialogue. Talk to the warden and accept, report and claim rewards by touch.
6. **Save and load:** save to an empty slot; Overwrite asks and Cancel keeps the save; Load asks; Settings survive a load and an app restart.
7. **Interruption:** background the app during combat, dialogue and a confirmation, then resume. Nothing is lost or triggered.
8. **Settings:** Largest text everywhere, high contrast and reduced motion; check that sound off persists.
9. **The integrated adventure:** play The Buried Light end to end (combat, ruin keys/gates/levers/braziers, journal, map, relic, return, reward) on the phone.
10. Record observed issues in the PR. Fix high-severity UX defects before marking the PR ready.

## PR #7 review corrections

- **Autosave isolation:** recovery autosaves now use `Persistence.SlotDirectory` alongside manual slots. Production still defaults to `user://`; every verification layout writes its recovery files to its own verification folder. A rendered lifecycle regression checks the normal recovery file byte-for-byte (or verifies it remains absent), without seeding or modifying it, and requires the isolated autosave to load successfully.
- **Damaged save inspection:** malformed/missing metadata is reported as damaged, while recognised older generator/schema versions remain protected. Wrong-type schema fields and null collections no longer abort slot inspection. Malformed recovery metadata is also preserved before a new autosave is written. Added native regressions plus malformed-metadata coverage in the actual Save & load screen.
- **Trade capacity:** read-only preflight now uses disposable Gloot copies of the two bags and the same payment-then-delivery transfers as production. Spending the final coin stack can free a slot; leftover coins still block an item that cannot fit. Regressions require agreement with the real transaction, unchanged preview state/events, and exact inventory restoration on failure.

Local verification uses Godot 4.6.1, SDK 9.0.203 and the .NET 8.0.15 engine host. Build and full headless verification pass (8,878 assertions). Rendered and hosted verification results are recorded in the PR description. Import exits successfully with the pre-existing Dialogue Manager editor `is_importing` and EditorSettings shutdown diagnostics; runtime verification rejects script/engine errors.
