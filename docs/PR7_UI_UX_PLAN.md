# PR #7: Mobile UI and RPG experience overhaul
Status: Part 1 implementation is on this branch (shared theme, navigation/screen host, Character pilot and native regressions); automated build/render/APK verification and physical acceptance are tracked in the PR and `PR7_PART1_HANDOFF.md`. Parts 2-4 are **not yet implemented**. The crafting regression fix remains implemented. Keep the pull request in draft until all four parts and physical Galaxy S25 acceptance are completed.

## Goal and design priorities

Make Mosslight's existing, fully playable quest/combat/inventory loop legible, predictable, comfortable, and visually cohesive on a phone, without rewriting any authoritative gameplay systems. This PR builds on the merged PR #5 adventure, including The Buried Light, dungeon landmarks, exploration state, mechanisms and persistence.

The priorities, in order:
1. **Usability and correctness:** a player understands their current objective, available actions, consequences and reasons an action cannot be taken.
2. **Thumb ergonomics and input reliability:** stable left-stick movement, attack/dodge/interact emphasis, reliable simultaneous fingers and dismissal behavior.
3. **Clarity and accessibility:** legible scaling, high contrast, non-color-only feedback, touch targets and predictable navigation.
4. **Coherent fantasy presentation:** restrained stone/forest/parchment/bronze visual language with consistent icons, typography and panels, without covering the world or sacrificing responsiveness.
5. **Preserve gameplay and performance:** no new authority, no save-breaking state or replay regressions, no per-frame UI allocations that degrade the S25.

The current code is largely assembled in `game/ui/RpgUi.cs` (~680 lines), with `MobileCombatControls.cs`, `TouchStick.cs`, `TouchScroll.cs`, `game/narrative/Narrative.gd`, `game/dev/Foundation.cs`, Gloot and QuestSystem. The current dialogue has a separate absolute-position panel. Refactor these incrementally; do not replace stable systems wholesale.

## Cross-part implementation contracts

### Authority and compatibility
- `Foundation` dispatches user intents and gates gameplay; `World` owns actors, combat, world events, regions and timers. UI renders state and requests actions.
- Gloot and `ItemService.gd` own item instances, stacks, equipment, crafting/trading and capacity; the C# `Items` facade remains authoritative. Do not build a parallel client inventory.
- QuestSystem / `Narrative.gd` own quest status, stages, pending rewards, dialogue and flags. Introduce **read-only structured UI data** for current quest objective, journal rows and stage progress rather than parsing the current `Narrative.journal()` formatted string. Add the smallest objective read model in Part 2; expand it in Part 3. Preserve the existing journal API for compatibility where needed.
- `RegionState` and the persisted exploration manifests own map visibility, gates, landmarks and mechanism state. The map may only render discoveries the world actually knows.
- Keep existing save slots, unified persistence, generator compatibility checks, game identity, and historic progress. UI preferences, when introduced, are separate from authoritative world saves. No save-schema change merely to reposition a panel or change a theme.
- Do not rebalance combat, change attack geometry, mix hit/miss cues, change quest rules, or add unrequested game content under cover of the UI PR.

### Input and public interfaces
- Preserve stable mobile action IDs: `attack`, `dodge`, `ember`, `mend`, `potion`, `interact`, `inventory`, `menu`. Preserve `RpgUi.ActionControl(id)`, `BlocksInput`, `MenuOpen`, `CombatControls`, `Stick`, and existing `Foundation.Ui` integration, or provide tested compatibility adapters before callers change.
- Preserve the release-inside-original-target semantics, single-owner finger handling, cancellation, mouse-emulation deduplication, independent joystick motion and multi-finger actions currently covered by `MobileCombatControlsChecks`. Keep touch regions stable when text/cooldowns/icons change. Do not let a scrolling gesture activate an item.
- Treat `MobileCombatControls.ToLogicalSafeArea` as the physical-to-viewport conversion authority; avoid unbounded screen-coordinate math that ignores camera transform, window scale, letterboxing or system insets.
- No actionable gameplay gesture is left alive after opening a panel/menu, dialogue, death, OS focus loss, background/resume, resizing, or swapping layout. Avoid double dispatch and ghost taps.
- A defeated character must always retain a usable Menu/Load/New Game recovery path, including on touch-only builds.

### Navigation behavior to specify in Part 1

Implement a small, testable UI interaction contract before broadly refactoring screens. Recommended interaction priority: **dialogue / confirmation modal > pause menu > RPG information screen > gameplay**. If dialogue or a destructive confirmation is active, Back should not accidentally resume gameplay. Back from an RPG screen returns to its invoking screen when available, otherwise gameplay; Back from the pause menu resumes only when it is safe to resume. Give each screen an explicit, visible Close/Back action; support Android system Back and desktop Escape coherently. Keep input blocked in overlays and transition atomically, without frame windows in which world actions leak through.

A single modal or menu should own input focus. Menu-to-Inventory-to-Back should return to the menu rather than arbitrarily dropping the player into combat. Save/load, dialogue, defeat, and background resume need named navigation cases. Accessibility focus/tab order and disabled-action explanations should be part of this contract.

Build `docs/PR7_UI_INTERACTION_SPEC.md` as a **Part 1 deliverable** with a transition table, touch ownership and cancellation cases, safe-area rules, and the accepted screen hierarchy. The Part 1 Work session may adjust this specification if real implementation/testing shows a better behavior; document deliberate changes.

### Accessible and scalable baseline

Establish in Part 1, use in Parts 2-3, validate comprehensively in Part 4:
- Contrast: make normal text and key state indicators readable on mobile, ideally targeting WCAG-style contrast ratios (4.5:1 normal text, 3:1 large text / meaningful indicators) where practical; do not rely on color alone.
- Typeface and type size: provide sensible default, larger text options or a scalable type system, deterministic wrapping, ellipsis/scroll rules and keyboard/focus support. Do not embed essential info only in tooltips (which phone users cannot hover).
- Touch: target at least Android-equivalent ~48 dp for interactive targets where applicable, translated correctly into Godot's logical/physical geometry; validate on actual phone rather than assuming viewport pixels equal dp. Compact fallbacks must keep controls usable, separated and within safe bounds.
- Essential actions retain icon **and** label or unambiguous tutorial affordance; reduced motion and animation timing must not interfere with input or gameplay; avoid rapid flashing.
- Panel/list layouts must survive unusual aspect ratios, large text, notifications and long strings. Visible UI cannot cover required combat telegraphs or overlap action targets.

### Per-phase definition of done

Each of the four parts must land on **this branch and PR**, include documented design choices, playable UI, native integration tests, desktop + `--mobile-ui` rendered screenshots, Android development APK/export verification when infrastructure is available, and a concise physical Galaxy S25 playtest checklist. Source SHAs, run URLs and any failures/limits should be recorded in the PR/handoff. Native emulation and Mesa rendering are **not** proof of touch comfort or physical S25 performance. Do not mark a phase complete based solely on code review or screenshots.

At minimum verify exact source via `dotnet build`, `python3 tools/import-project.py`, `python3 tools/verify.py --headless`, `python3 tools/verify.py --visual`, and `python3 tools/verify.py --visual --mobile`, with the existing CI workflows. Where visual changes affect movement/combat feedback, also run the existing gameplay replay/visual workflows and compare baseline behavior. Add focused tests and artifacts per milestone rather than repeatedly replaying only old general-purpose tests.

---

## Part 1 of 4: UI design system, navigation, screen host and pilot screen

**Outcome:** a stable, reusable UI foundation with at least one complete, demonstrably usable player-facing screen. Avoid a framework-only Part 1.

**Implementation**
- Document the navigation, focus, input-blocking, cancel/release and Back behavior in `docs/PR7_UI_INTERACTION_SPEC.md`. Cover Android Back / keyboard Escape / screen Close, menu-to-screen return, destructive confirmation, dialogue lock, defeat, app suspension and resizing.
- Extract shared visual tokens, theme and reusable UI elements (panel, title, button, status indicators, spacing, scroll surfaces, item rows, confirmation modal, notification style) using native Godot Controls and a shared Theme resource/code. Keep a restrained fantasy aesthetic, avoid heavy decorative textures until ergonomics prove out.
- Introduce a thin screen host/navigation coordinator separate from the main world logic. Keep `RpgUi` as a compatibility facade for existing tests and Foundation command calls while relocating screen presentation by responsibility. Never duplicate item, quest or save authority.
- Establish responsive safe-area layouts and HUD/overlay/notification regions at 1280×800, 1280×720, 1920×864, 1024×768 and 900×560 with nonzero insets and translated/scaled screen transforms. Handle Godot viewport-vs-physical display coordinates correctly.
- Establish text scaling, touch-target metrics, contrast, icon/label and focus-order rules. Preserve swipe scrolling and suppression of accidental list-row clicks.
- Complete one **real player-facing pilot view**, preferably a read-only Character overview or similarly representative screen, using production state, with working open/close/back/focus/scroll/resize and no placeholder data. Later Part 3 may expand the Character screen with training/equipment comparisons.
- Preserve existing hotbar action IDs and accessible `RpgUi` compatibility API. Maintain the current game experience until the replacement is proven, migrating screens incrementally.

**Acceptance / regressions**
- Navigate gameplay > menu > pilot screen > back > menu > resume; direct screen open > close > gameplay; dialogue/defeat/focus-loss and Android Back behavior. No phantom actions or stuck motion.
- Visual baseline screenshots for real production layout, large text and safe insets; no overlapping controls/text, unreadable title, clipped lists, or touch targets outside safe bounds.
- Add navigation/overlay/input assertions without weakening `MobileCombatControlsChecks` or real native tests.
- A runnable Android development APK is available for phone evaluation before proceeding.

## Part 2 of 4: Gameplay HUD, thumb ergonomics and interaction feedback

**Outcome:** moment-to-moment play is readable without opening a menu and is comfortable under both thumbs.

**Implementation**
- Replace text-only health/mana/XP with readable visual bars, numeric values where useful, level/status and a compact objective tracker. Make the world remain visible.
- Expose a minimal **read-only current quest objective API** from `Narrative.gd` / QuestSystem (id, title, active stage text, progress, completion and pending reward states). The HUD consumes this, not `journal()` string parsing.
- Prioritize Attack, Dodge and Interact. Keep existing action IDs, touch geometry and gameplay commands but evaluate thumb reach and screen occlusion; refine sizes/placement deliberately, not gratuitously. Support constrained layouts and keep secondary spells/potion discoverable.
- Contextual Interact should display real current action (Talk/Open/Loot/Activate/Enter/Use, as appropriate) based on the same targets and precedence the authoritative interaction path uses. If no valid interaction, show consistent unavailable state; avoid lying when multiple targets compete.
- Show cooldowns, mana restrictions, target requirements, potion quantities, active state, and concise reasons for disabled controls. Keep hit and miss feedback semantically separate. Provide notifications for quest progression, discovery, loot, craft success/failure and important world events without covering combat telegraphs.
- Prevent HUD refresh/animation from shifting touch rectangles or consuming excessive frame/GC budget. No new targeting system, combat mechanic or character movement behavior.

**Acceptance / regressions**
- Production combat with left-thumb movement and right-thumb attack/dodge/spells/potion/interact, overlapping fingers, canceled touches, focus loss, opening a panel mid-gesture, and defeat recovery.
- Context labels agree with actions actually taken. Disabled controls never fire; an unavailable action explains why where appropriate. Quest objective tracker tracks quest stage changes after save/load.
- Screenshots at multiple resolutions and the Galaxy S25 normal landscape view, plus recorded thumb-reach and occlusion feedback. No regression in  existing combat, hit/miss, safe-area or input tests.

## Part 3 of 4: Inventory, equipment, crafting, trading, character, journal and map

**Outcome:** the entire RPG information and item-management loop becomes legible, actionable and consistent on touch.

**Implementation**
- Inventory/equipment: show actual Gloot stack/instance identity, slot, equipped state, item statistics, modifiers, qualities/materials, item detail, comparison and contextual actions. Prefer select > Inspect/Equip/Use/Unequip over mandatory drag-and-drop. Report correct unavailable reasons.
- Crafting/trading: show complete recipe ingredients with owned/required counts and output, true capacity/stack/availability, price/currency and merchant stock. Preserve the transactional craft fix from the first PR #7 commit. Distinguish 'missing ingredients' from 'bag full', 'cannot make', or 'insufficient funds' where production state supports it. Never partially grant output or advance quests on failure.
- Character/progression: stats and bonuses derived from World/Gloot, skill-point spending and unlock requirements, confirmation or clear feedback for irreversible actions.
- Journal: expand the structured read-only QuestSystem/Narrative adapter to expose quest ID, status, active stage, history, prerequisites and pending rewards; render readable objective/stage history and lore without parsing presentation text. Do not mutate quest progress from UI.
- Map: present existing explored terrain and discovered landmarks/gates/levers/braziers with readable markers/legend and sensible zoom or pan *if supported reliably*. Do not reveal undiscovered content or regenerate world state; prioritize clarity over a speculative complex minimap.
- Make panel tabs, empty states, selection and Back return behavior consistent. Support meaningful long strings and large text without clipped controls.

**Acceptance / regressions**
- End-to-end production path: loot > inspect > equip > use > craft > trade > review changed stats > read current/completed quest stages > consult explored map > save/load > recheck UI state.
- Test both successful and failed craft/trade/equip paths, generated equipment instances, bag capacity, pending quest rewards, hidden map exploration, and persistence fidelity.
- Confirm no parallel inventory/quest state and no UI-induced changes to world/quest/saving semantics. Capture mobile screenshots and Android APK evidence.

## Part 4 of 4: Dialogue, pause/settings, accessibility completion and final acceptance

**Outcome:** all player-facing entry points are coherent, legible, robust to interruption and usable on physical hardware.

**Implementation**
- Bring the Dialogue Manager-backed dialogue presentation from `game/narrative/Narrative.gd` into the shared responsive theme/navigation rules, without duplicating line/choice advancement. Handle conditional/disabled choices and asynchronous dialogue safely, with visible speaker identity and touch-friendly response layout.
- Finish pause/menu flow, save-slot listing and clear feedback for compatible/incompatible/absent/corrupt saves; add confirmation before New Game or destructive replacement. Preserve historic save compatibility rules, incompatible generator protections and retained autosave behavior.
- Separate ordinary player navigation/settings from the existing creature workshop, combat test fixtures, feel lab, performance and collision tools. Keep test/dev tools accessible through an explicit Developer entry or debug mode and preserve existing test hooks.
- Persist *UI preferences* (e.g. preferred text size, control visibility/scale or placement if introduced, contrast/reduced motion, sound), independently of world saves. Do not introduce control remapping that breaks the action input pipeline without a full automated/phone test plan.
- Test accessibility thoroughly: scalable text, contrast and non-color-only states, reachability, readable disabled explanations, touch-only navigation, focus/keyboard navigation, excessive animation avoidance and OS scaling/insets.
- Validate background/resume, focus loss, dialogue interrupted by app pause, save/load, defeat, menu and modal transitions, scene/view rebuilds, Android Back behavior, rotated/resized viewports and actions while input is blocked. Preserve screenshots/trace markers and release/verification entry points.
- Run the entire integrated **The Buried Light** route with the revamped UI, including combat, dialogues, inventory management, rune/gate interactions, journal, map, saving and loading.

**Acceptance / release gate**
- All existing exact-head checks pass; no blocking errors, test skips hidden as pass, or Android regression.
- Desktop + mobile UI screenshots show every redesigned flow, including representative large-text and safe-area cases.
- User tests the actual ARM64 APK on a **Samsung Galaxy S25**, covering reach, readability, world visibility, multitouch, scrolling, menu/dialogue, save/load and interruption. Track observed issues and fix high-severity UX defects before changing draft to ready.
- Keep this PR unmerged until acceptance is recorded. State clearly which tests are automated and which subjective/physical observations remain pending.

---

## Deliberately out of scope

No new combat systems, quest/story content, dungeon-generation overhaul, quest-state rewrite, replacement item backend, new save format, wholesale controller architecture rewrite, mandatory drag/drop inventory, heavyweight realtime minimap, or untested texture-heavy UI system. Cosmetic polish must never outrank response time, readability or production correctness.

## Initial crafting implementation already committed

The first PR #7 commits address the [PR #5 crafting review finding](https://github.com/CigThePig/RPG-test/pull/5#discussion_r4432742201): consume ingredients before checking output capacity, commit only full output, roll back full Gloot snapshots on failure, and publish acquisition/crafting events only on success. `game/tests/CraftingChecks.cs` and `FoundationChecks` exercise this path. Do not conflate this completed correctness fix with completion of Parts 1-4.

## Handoff discipline

For each Work session: inspect current PR head and prior part handoff first; work only on `feat/mobile-ui-ux-overhaul`; avoid opening a replacement PR; implement the current part in production code; add focused tests, screenshots and a runnable APK; report exact commit SHA, CI URLs, completed/partial items, known deficiencies and physical-phone checks. Preserve prior complete behavior and leave later parts for later sessions. The next part starts only after the preceding part's functional and verification acceptance is credible.
