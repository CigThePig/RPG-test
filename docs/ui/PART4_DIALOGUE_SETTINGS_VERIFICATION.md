# Part 4 — Dialogue, pause/settings, accessibility and verification

**Goal:** finish the experience. That means a themed Dialogue Manager presentation, a proper Pause with Save/Load cards and Settings, accessibility options that actually change behaviour, a clear defeat flow, developer tools fully separated from player UI, and a verification layer that catches UI regressions automatically and hands Ross a precise phone checklist.

> **Creative layer ([CREATIVE_DIRECTION.md](CREATIVE_DIRECTION.md)) owned by this part:** **C11 Fallen** (the death recap screen **replaces the non-blocking defeat banner in §2**; update `MobileCombatControlsChecks` as described there), **C12** coach marks, **C13** gamepad. Dialogue portraits are live creature portraits of the actual speaker (C6). Settings gain: Display ▸ *Combat numbers* and *Enemy nameplates* toggles; Accessibility ▸ *Reset tutorial hints*; Controls ▸ *Controller glyphs* (Auto / Keyboard / Gamepad). Pause gains *Return to title* (C1).

Prerequisites: Parts 1–3. Mockups: [`07-dialogue.svg`](mockups/07-dialogue.svg), [`08-pause.svg`](mockups/08-pause.svg), [`09-settings.svg`](mockups/09-settings.svg).

## 1. Dialogue presentation

### Behaviour that must not change
`Narrative.gd` keeps the dialogue logic: `talk()`, `advance(cue)`, `dialogue_epoch`, `advancing`, `speaking`, mutations through `self` (`accept_quest`, `report_to_warden`, `claim_pending`, …). Tests and the smoke rely on these presentation contracts:
- `Narrative.choices` is a `VBoxContainer` whose direct children are the response `Button`s, or a single `Continue` button whose text is exactly **`Continue`**.
- Disallowed responses are not rendered (current behaviour). They never show `[if` text.
- On Android, every choice logs `MOSSLIGHT_DIALOGUE_CHOICE text=<button text> x=<centre> y=<centre>` one frame after creation. The coordinates are in the same space as today (viewport px, which the smoke treats as 1280×800 space). **Upgrade:** log physical px like `MOSSLIGHT_UI`, and update the smoke to tap them directly instead of scaling.
- The smoke selects the choice whose text starts with **`I can help`**. Keep that wording.

### New presentation: `game/narrative/DialogueBalloon.gd`
`Narrative.build_ui()` instantiates `DialogueBalloon` (a `PanelContainer`, built in code, no `.tscn`) on a `CanvasLayer` at layer 20 (above the HUD), assigns `ui = balloon`, `text_label = balloon.text`, `choices = balloon.choices`, then sets `ui.theme = ui_theme` (set from C# in Part 1).

Layout (mockup 07), driven by `layout_dialogue(safe: Rect2, touch: bool)`, which `RpgUi.Layout()` calls through `App.Narrative.Call(...)`:
- Bottom sheet: `x = safe.x + 24`, width `min(safe.w - 48, 1100)` centred, bottom `safe.end.y - 24`, height = content (min 240, max 62% of `safe.h`; choices scroll inside a `TouchScroll`-equivalent if needed). Style `surface.1`, `radius.lg`, `elev.3`, 2 lp `line.frame`.
- **Nameplate**: a gold tab overlapping the sheet's top edge at the left, holding the `TitleLabel` speaker name (`current_line.character`). The text label no longer starts with "Warden:".
- **Portrait**: a 112 lp medallion (`surface.2`, gold ring) with a `person` icon. If a speaker-specific icon exists (`UiIcons` has `speaker-<name>`), use it.
- **Text**: Dialogue Manager's own `DialogueLabel` (`res://addons/dialogue_manager/nodes/dialogue_label/dialogue_label.tscn`), `BodyLabel` 26 lp, max width 900 lp, autowrap. Set `skip_action = &""` (we handle skipping). `seconds_per_step` comes from the Text speed setting: Instant 0, Fast 0.01, Normal 0.02 (default). In verification mode it is always 0.
- **Choices**: full-width rows, `target.row` tall, left-aligned, each with a leading icon chosen from Dialogue Manager **response tags**:

| Tag (add in `warden.dialogue`) | Icon | Style |
|---|---|---|
| `[#quest]` | `scroll` + gold "!" badge | Secondary, gold icon |
| `[#turnin]` | `check` gold | Secondary, gold border |
| `[#info]` | `chat` | Secondary |
| `[#trade]` | `scale` | Secondary |
| `[#leave]` | `back` | Ghost (always last) |
| *(none)* | `chat` | Secondary |

  Add the tags to every response in `game/data/warden.dialogue`. Keep the response texts unchanged.
- **Continue**: when a line has no responses, show one primary-style row with the text `Continue` and a `back`-rotated chevron icon.

Interaction:
- **Tap on the text/sheet**: if `DialogueLabel.is_typing`, then `skip_typing()`. Otherwise, if the only child is `Continue`, press it.
- **Keyboard**: Enter/Space = skip, then Continue. `1`–`9` choose the n-th visible choice. Esc/Back do nothing (deliberate choices; README §3).
- **Double-tap guard**: after new choices appear, ignore presses for 250 ms, because a new choice can appear exactly under the finger that pressed Continue. The guard is 0 in verification mode. Buttons exist and are enabled immediately; the guard lives in the press handler, not in `disabled`, so `VisualChecks.TapDialogueButton` keeps working.
- During dialogue the HUD action cluster is hidden (existing `Update()` rule), and toasts stay visible at the top.

### Trade from dialogue (optional, recommended)
Add `- Show me your supplies. [#trade]` → `do request_trade()` → `=> END` to `warden.dialogue`. `request_trade()` sets `pending_trade = true`. When `advance()` reaches `current_line == null` (dialogue end), emit `signal trade_requested` after `speaking = false`. Foundation connects it to `ui.OpenBook(BookTab.Pack, PackSegment.Trade)`. Test: choosing it ends the dialogue and leaves the Book open on Trade.

## 2. Pause, Save/Load, defeat

### Pause (final form; mockup 08)
Build on the Part 1 `PauseScreen`:
- Header: `DisplayLabel` "Mosslight"; caption "Wayfarer · Level 3 · The Sealed Ruin"; tracked quest line (`scroll` icon + stage text).
- Primary button: **Resume**. On a fresh launch (`!App.SessionStarted`) it reads **Begin adventure**. Name stays `pause_resume`. *(Part 1 switched the tests to look up `Name`. Confirm no test still matches the text `Enter / Resume` before renaming. If one does, update it in this commit.)*
- Shortcut row: Pack · Journal · Character · Map (texts stay exactly these strings for `ReviewChecks`, unless it was also migrated to Names).
- List: **Save game ›**, **Load game ›**, **Settings ›**.
- Ghost rows: **New game** (confirm), **Developer tools ›** (only when `UiDev.Enabled`), **Quit** (desktop only, confirm).
- Footer caption: "Last saved {relative time} · slot {n}", or "Not saved yet".

### Save/Load screen (child of Pause)
`Segmented` [Save | Load] above a list of **slot cards** (`ListRow`, 120 lp):

```
[▣]  Slot 2                                                   [ Save ]
     Wayfarer · Level 3 · The Sealed Ruin
     The Buried Light — Find keys, open sealed passages…  ·  Saved 2 Nov, 14:32
```

- Slots 1–3. In Load mode, a fourth card "Recovery autosave", with caption "Saved automatically when the game was last backgrounded".
- **Metadata sidecar.** After a successful `Persistence.Save(..., slot)`, write `user://slot-{n}.meta.json` atomically (temp + rename): `{ "version": 1, "saved_unix": …, "player": "Wayfarer", "level": 3, "region": "ruin", "quest": "The Buried Light", "stage": "…" }`. Do the same for the autosave (`autosave.meta.json`) in `SaveAutosave`. **The save file schema itself is untouched.** If the sidecar is missing (older saves), show the file's modification time and "Details unavailable".
- Save over an occupied slot → `ConfirmDialog` "Overwrite slot 2?". Load while a session is running → `ConfirmDialog` "Load slot 2? Unsaved progress will be lost." An empty slot's Load is disabled with the reason "Empty".
- Keep every guard and error path from today's `BuildMenu` (`InFeelCourse` messages, try/catch → `Notify`). After a Load: `App.RebuildViews(); App.ResumeGame();`, clear HUD transient state (toasts, ghost bars, title card), refresh the tracker, and toast "Loaded slot 2".
- `RpgUi.SaveSlot` remains the last-used slot.

### Defeat
- On death (`!Player.Alive`, edge-triggered as today), show a **non-blocking** banner in the title-card slot: `DisplayLabel` "You have fallen" in `danger.text`, with caption "Open the menu to load a save, recover, or start again." It must not join the screen stack: `MobileCombatControlsChecks` taps the HUD Menu button while dead.
- The HUD Menu button gets a slow 1 Hz gold glow while dead (static with reduced motion).
- Pause in the defeated state: the header reads "You have fallen". The primary button becomes **Load last save** (the most recent slot by `saved_unix`, falling back to mtime). Then come **Recover autosave** and **New game**. Resume is hidden.

## 3. Settings (child of Pause; mockup 09)

`Segmented` [Display · Audio · Controls · Accessibility · About] above a `TouchScroll` list of `Toggle`/`SliderRow`/`Segmented` rows. Changes apply **immediately** and persist on screen close.

| Section | Setting | Control | Default | Effect |
|---|---|---|---|---|
| Display | Text size | Segmented 100 / 115 / 130 % | 100 | `UiTheme.Rebuild(density with TextScale)`; re-layout everything. Dialogue picks up the theme. |
| | HUD opacity | Slider 50–100 % | 100 | `hud.plate` alpha × value; does not affect text |
| | Camera zoom | Slider 1.25–2.5 (step .25) | 1.5 | Sets `camera.Zoom`; replaces `CycleZoom` in the player UI (keep the method for dev) |
| | Show quest tracker | Toggle | on | HUD tracker visibility |
| | Enemy health bars | Toggle | on | Part 2 overlay (if implemented) |
| Audio | Master / Music / Effects / Ambience / Interface | Sliders 0–100 | 100/80/100/70/60 | Bus volumes (see below) |
| | Mute all | Toggle | off | Existing `Muted` + `AudioServer.SetBusMute(0, …)` |
| Controls *(Touch)* | Left-handed layout | Toggle | off | `UiDensity.LeftHanded` → mirrored geometry (Part 2) |
| | Button size | Segmented 100 / 115 % | 100 | `UiDensity.ButtonScale` multiplies the geometry factor `s` |
| | Stick dead zone | Slider 0.02–0.25 | preset value | `App.SelectFeel(App.Feel with { Deadzone = v })` |
| | Vibration | Toggle | on | Haptics on damage/level-up (DESIGN_SYSTEM §9) |
| Controls *(Desktop)* | Key bindings | read-only list | — | Shows the current bindings; rebinding is future work |
| Accessibility | Reduce motion | Toggle | off | Durations → fades only; no pulses, bobbing or typewriter; `CameraFeedback.Strength = 0` |
| | Screen shake | Toggle | on | `CameraFeedback.Strength` 0/1 (forced off by Reduce motion) |
| | Text speed | Segmented Instant / Fast / Normal | Normal | Dialogue `seconds_per_step` |
| | High-contrast HUD | Toggle | off | Opaque plates, 3 lp borders, no shadows, white values |
| | Reduce flashes | Toggle | off | `CombatEffects` flash alpha × 0.4 (presentation only) |
| About | Version | text | — | `BuildIdentity.Source` (short SHA), Godot version |
| | Licenses | text | — | "Mosslight uses open-source software. See THIRD_PARTY_NOTICES.md." |
| | Developer tools | Toggle *(debug builds only)* | on in debug | `UiDev.Enabled` |
| | Reset settings | Ghost button + confirm | — | Restores defaults |

**Storage.** `game/settings/UserSettings.cs` uses `ConfigFile` at `user://settings.cfg`, with sections matching the table. It is never part of game saves, never touched by `Persistence`, and **not loaded in verification mode** (defaults only, so captures are deterministic). Load it in `Foundation._Ready` before creating `RpgUi`, apply it after `RpgUi._Ready`, and save on Settings close and on `NotificationApplicationPaused`.

**Audio buses.** `AudioService.gd` currently routes everything to bus 0. At startup, create buses `Music`, `Effects`, `Ambience`, `UI` if they're missing (`AudioServer.add_bus`, `set_bus_name`, `set_bus_send(i, "Master")`), and assign each player's `bus`. Bus 0 mute keeps working for `Muted` and for the existing audio state check (`"muted": AudioServer.is_bus_mute(0)`). Convert slider % to dB with `linear_to_db(v/100)`; 0 % → mute that bus.

## 4. Developer tools separation

- `UiDev.Enabled = OS.IsDebugBuild() || args contain "--dev"`, overridable by the About toggle. Release exports never show developer UI.
- Everything developer-facing lives only in `DeveloperScreen` (Part 1): Diagnostics, Feel lab, Workshop, Overlays (collision, performance), Combat lab, and the "Mark issue" button toggle. The Pause menu and Settings contain none of it.
- Keep keyboard shortcuts F9/F10 working when `UiDev.Enabled` (the debug-APK smoke uses F10). In release builds they do nothing.
- Developer screens get a lumen-tinted header caption "Development tools — not part of the game", so screenshots can't be mistaken for player UI.

## 5. Focus loss, lifecycle and robustness

| Situation | Required behaviour |
|---|---|
| App backgrounded / focus lost during gameplay | Unchanged: cancel input, autosave (rules unchanged), `PauseForVerification()` shows Pause |
| … while the Book or Settings is open | Close everything and show Pause (current behaviour). Settings are saved first. |
| … while dialogue is open | **Change:** don't push Pause over dialogue. Cancel input only; dialogue still blocks gameplay. On resume the dialogue is exactly where it was. |
| … mid-transition | `ScreenHost.FinishTransitions()` completes all tweens instantly before pausing |
| Load while a screen is open | `CloseAll()` before restore; rebuild HUD transient state after |
| `Narrative.import_state` (load) during dialogue | Existing: hides `ui`, bumps the epoch. The balloon must also clear the typewriter and choices |
| Resize / rotation / safe-area change | `Layout()` re-arranges the HUD, `host.Arrange`, `layout_dialogue`; input ownership cleared (existing) |
| Text size 130% | No clipped text in any screen. Rows grow; lists scroll |

## 6. Accessibility checklist (automated where possible)

`game/tests/AccessibilityChecks.cs` (mobile and desktop runs):
1. Every visible `Label`/`Button` text in HUD, Book, Pause, Settings and dialogue has an effective font size ≥ 22 × TextScale (Touch), except controls with the `GlyphLabel` variation (≥ 18).
2. Every visible Button in screens is ≥ `TargetMin` tall and wide. HUD actions follow the geometry table.
3. Every icon-only button has a non-empty `TooltipText`. Godot ≥ 4.5 also exposes accessibility properties on `Control` (AccessKit). Set the accessible name on icon-only buttons; check the exact C# member name in the 4.6.1 API before use.
4. Token contrast (from Part 1) still passes after any palette edits.
5. With Reduce motion on, `ScreenHost` and `ToastLane` use 0 slide distance, `CameraFeedback.Strength == 0`, and `DialogueLabel.seconds_per_step == 0`.
6. At TextScale 1.30, for each Book tab, Pause, Settings section and dialogue line: no Label overflows its parent rect (compare `GetCombinedMinimumSize()` with the parent size), and all controls stay inside `UiFrame.Safe`.
7. Left-handed geometry passes the Part 2 overlap/safe-area test, and `attack` is in the left half of the safe rect.
8. Map markers: every gate/lever/brazier/landmark kind draws an icon or shape in addition to its colour (unit test on the marker table).

## 7. Verification

### 7.1 Deterministic UI captures (visual regression)
- Add a `--ui-capture` mode to `VisualChecks`, or extend it. With `ShowCharacters(false)`, a fixed seed, verification-mode durations and default settings, it produces `ui-*.png` for desktop and `--mobile`:
  `ui-hud`, `ui-hud-cooldown`, `ui-hud-low-health`, `ui-hud-toasts`, `ui-book-pack`, `ui-book-craft`, `ui-book-trade`, `ui-book-hero`, `ui-book-journal`, `ui-book-lore`, `ui-book-map-reach`, `ui-book-map-ruin`, `ui-dialogue`, `ui-dialogue-choices`, `ui-pause`, `ui-pause-defeated`, `ui-saveload`, `ui-settings-display`, `ui-settings-access`, `ui-confirm`, `ui-developer`, plus `ui-pause-text130` and `ui-hud-lefthanded` (mobile only).
- Commit baselines to `docs/ui-baselines/{desktop,mobile}/`.
- Write `tools/ui-diff.py <baseline_dir> <new_dir> [--threshold 0.5]`. It compares same-named PNGs, counts pixels whose max channel delta is > 24, fails if any image differs in more than `threshold`% of pixels or a baseline is missing, and writes side-by-side `diff-*.png` (baseline | new | highlighted delta) to an output folder.
- CI: in the existing visual workflow, run the captures, run `ui-diff.py`, and upload the diff folder as an artifact on failure. An intentional UI change updates the baselines in the same commit.
- Hosted llvmpipe captures are evidence of layout and regression, not of phone appearance (same caveat as `GAMEPLAY_VERIFICATION.md`).

### 7.2 Android OS-touch smoke additions (`tools/android-smoke.py`)
Add logging in `ScreenHost`: `MOSSLIGHT_UI_SCREEN stack=<id,id,…>` on every stack change (Android or `--ui-probe`). Then add these steps after the existing chest/dialogue flow, all with logged coordinates and bounded waits:
1. `tap_action('inventory')` → stack `book` → capture `book.png` → `tap_ui('book_tab_journal')` → capture → **`adb shell input keyevent 4`** (Back) → stack empty → Back again → stack `pause` → Back again → stack empty. Assert the pid is unchanged (Back never quits).
2. Pause → `pause_save` → slot 1 Save → confirm if prompted → toast log `MOSSLIGHT_SAVED slot=1`. Then `am force-stop` and relaunch → Begin/Load flow → `pause_load` → slot 1 → confirm → `MOSSLIGHT_LOADED slot=1`.
3. Settings → Controls → Left-handed on → the latest `MOSSLIGHT_CONTROL id=attack` x is < w/2 → turn it off again.
4. During dialogue: background (keyevent 3) and relaunch → assert no `pause` in the stack and dialogue choices are logged again.
Record the new screenshots and log extracts in the runtime-evidence artifact.

### 7.3 Performance budget
- HUD and screens must not allocate per frame in steady state. `RpgUi.Update()` sets `Label.Text` only when the string changed, reads JSON APIs at most every 30 ticks or on change signals, and avoids LINQ in per-frame paths (the current `actions.Values.Any(...)` in `_Input` is fine because it's event-driven).
- Measure with the existing render-verification harness on the mobile layout: frame-time p50/p95/p99 before Part 2 vs after Part 4, same workload (`PR4_PART4_PERFORMANCE.md` method). Budget: p95 regression ≤ 1.0 ms on hosted llvmpipe. Record node counts for HUD (< 150 Controls) and the Book (< 400 Controls with 30 bag items).
- If D1 (`expand`) was adopted, record the extra visible-actor cost separately.

### 7.4 Physical Galaxy S25 acceptance (Ross; copy into `docs/ANDROID_PLAYTESTING.md`)

| # | Check | Pass when |
|---|---|---|
| 1 | Readability | Health/mana values and toast text are readable at arm's length in daylight at 100% text size |
| 2 | Thumb reach | Potion, ability slot 5 and Interact are reachable with the right thumb without regripping. Same with the left thumb in left-handed mode |
| 3 | No accidental UI | Ten minutes of combat produce no accidental Book/Menu/tracker opens |
| 4 | Back gesture | Edge-swipe Back closes the Book, then opens Pause, then closes Pause. It never exits the app |
| 5 | Cutout/safe area | In both landscape orientations, no control is under the camera cutout or gesture bar |
| 6 | Dialogue | Tapping through warden dialogue quickly never selects a choice accidentally; typewriter skip works |
| 7 | Save/Load cards | Show the correct region/quest/time; overwrite/load confirmations appear; a load restores the game |
| 8 | Settings | Text 130%, left-handed, button 115%, reduce motion and volumes all take effect immediately and survive an app restart |
| 9 | Haptics | Damage/level-up vibration feels subtle, and turning it off silences it |
| 10 | Long session | 20+ minutes with several backgroundings: no stuck toasts, no frozen HUD, no lost input; returns to Pause correctly |
| 11 | Book usability | Equip/compare/craft/buy/learn/track/map-zoom all work with one thumb |
| 12 | Performance | No visible hitch when opening the Book, switching tabs or receiving toasts during combat |

## 8. Implementation steps
1. `UserSettings` + Settings screen (Display/Audio/Accessibility first), audio buses, reduce motion/text size wiring.
2. `DialogueBalloon.gd`, tags in `warden.dialogue`, layout hook, double-tap guard, optional trade request.
3. Save/Load screen + metadata sidecar; Pause final form; defeat banner and defeated Pause.
4. Developer separation and `UiDev`; the About section.
5. Lifecycle fixes (dialogue focus loss, `FinishTransitions`).
6. `AccessibilityChecks`, UI captures + `ui-diff.py` + CI wiring, Android smoke additions, performance measurement.
7. Creative layer: C11 recap, C12 coach marks, C13 gamepad (InputMap joypad events, focus navigation, glyph switching), with tests: recap shows killer and tip from a real presentation event; each coach mark triggers once and never during dialogue, combat or verification; joypad events drive attack, dodge and Book navigation.
8. Docs: `docs/PR7_PART4_HANDOFF.md`; update `README.md` (controls text: Back, settings), `docs/TESTING.md` (UI regression), `docs/ANDROID_PLAYTESTING.md` (S25 checklist), `docs/ROADMAP.md` (PR #7 milestone state), and tick the PR checklist **only** for items with recorded evidence.

## Acceptance criteria
- [ ] Dialogue is themed, safe-area aware, typewriter-skippable, double-tap safe and tag-iconified, with every Narrative/test/smoke contract intact.
- [ ] Pause contains only player features. Save/Load cards show metadata. Confirmations protect destructive actions. The defeat flow leads to recovery in ≤ 2 taps.
- [ ] Settings persist in `user://settings.cfg`, apply immediately, and never affect saves or verification runs.
- [ ] Accessibility checks pass, including at 130% text and in left-handed mode.
- [ ] UI visual regression runs in CI with committed baselines. The Android smoke covers Back, Book, Save/Load, Settings and dialogue backgrounding.
- [ ] The S25 checklist is handed to Ross, with results recorded as they come in (not claimed in advance).
