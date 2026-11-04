# PR #7: Complete RPG Interface Redesign
**Revised canonical plan (v2) | 2 November 2026 | Target: `feat/mobile-ui-ux-overhaul` -> `main`**

> **Scope reset, not a code reset.** The original four implementation parts are retained unchanged in history as **new Part 1: Functional UI foundation**. All shipped code, safeguards, source references, tests and previous handoffs remain valid as historical records. Revised Parts 2–3 have implementation on this branch; physical Galaxy S25 acceptance remains pending. Parts 4–5 have implementation on this branch; Part 6 remains planned. PR #7 stays **draft and unmerged** until visual and physical-device acceptance. Part 1 has outstanding Galaxy S25 issues, including swipe/highlight behavior under verification, and is not considered phone-accepted.

## Why the plan changed

Actual Galaxy S25 playtesting found an architectural/visual problem beyond the button-focus defect. The pack screen is a generic scrolling stack of identical buttons, labels and cards. Equipment lacks a paper-doll or recognizable gear slots; three backpack stacks already require scrolling because oversized action buttons occupy the viewport. The same structural problem affects Character, Journal, Map and menus. The initial Part 1–4 work created sound functional contracts, **not the final player-facing RPG experience**.

`UiScreenHost` currently exposes one vertically scrolling `VBoxContainer` to every information screen; `UiDesign.Action/Card/Tabs` amplify that uniformity. **Keep the input/navigation/data contracts but permit every screen to own its layout.** Rebuild presentation, not gameplay state.

**New success criterion:** Mosslight looks and behaves like a coherent mobile-first fantasy action RPG with visually distinct inventory, character, journal, map and merchant experiences. A screenshot should communicate what the player owns, can do and is exploring before they read a paragraph.

## Design principles and non-negotiable contracts

1. **Screen purpose determines layout.** Reuse design tokens, navigation, controls and components, not a one-column layout. Preserve a common header/Back and confirmation model, but give Inventory, Character, Journal and Map their own spatial composition.
2. **Visual objects, not document forms.** Equipment should occupy recognizable slots, backpack items should have consistent icons or silhouettes, actions appear beside the currently selected object. Keep item names, quantities and stat changes legible alongside iconography.
3. **Mobile landscape first.** Design for the actual Galaxy S25 plus 1280x720, 1280x800, 1920x864, 1024x768 and 900x560 logical viewports, nonzero safe-area insets and text scales 1.0–1.4. Choose adaptive arrangements rather than simply shrinking controls or forcing horizontal scroll.
4. **Fantasy identity, restrained execution.** Mosslight’s overgrown-ruins/forest palette, stone/bronze framing, subtle parchment-like panels and readable pixel-world-friendly item imagery. Avoid copying another game's art, oversaturated rarity glows, excessive decorations, generic admin-dashboard cards, or bespoke effects that conflict with the world.
5. **Shared art pipeline.** First check existing item data, procedural/world art and available licensed assets. Introduce a small deterministic, cached item icon/portrait/slot visual pipeline where needed. Draw a clear icon even when an item has no bespoke asset. Avoid per-frame asset regeneration and large texture allocations.
6. **Touchable and understandable.** One tap selects an item, explicit buttons invoke destructive/equipment actions, swipe scrolls without selection, Back behaves consistently, no hidden essential long-press/double-tap actions. Selected vs focused vs pressed must be visually distinct; disabled controls must explain why.
7. **No gameplay fork.** `World`, `Foundation`, Gloot/`Items`, `Narrative.gd`, `RegionState`, persistence and skill rules remain authoritative. No parallel inventory, stat cache, quest progress, merchant stock or unapproved world-save migrations.
8. **Preserve integration contracts.** Stable action IDs and `RpgUi` compatibility methods, multitouch ownership, touch cancellation, Android Back deduplication, combat hit/miss semantics, save-slot protections, craft/trade atomicity, accessibility preferences and visual/headless CI checks all remain in force.
9. **Measure usability visually and physically.** Passing tests is necessary, not sufficient. Each Part 2–5 feature receives representative landscape screenshots at two scales and an Android APK playtest checklist. No acceptance purely from simulated screenshots.
10. **Incrementally replace, never strand the player.** Each new screen must be usable from the existing menu and maintain all current gameplay actions during migration. Preserve old functionality until the new view covers it; avoid reworking gameplay mechanics under cover of UI design.

## Roadmap and acceptance

### Part 1 of 6 — Functional foundations (original Parts 1–4; implementation already exists)

**Completed engineering scope:** responsive safe-area hosting, navigation and modal/back ownership; gesture cancellation; HUD and contextual interaction; Gloot-backed pack/equip/crafting/trade; character progression, structured quests and explored map; player/developer menu separation; dialogue, save/load validation, settings and accessibility; transaction/save safety and verification.

**Historical crosswalk:** old Part 1 = new Part 1A (navigation/design tokens); old Part 2 = new Part 1B (HUD/controls); old Part 3 = new Part 1C (functional RPG screens); old Part 4 = new Part 1D (dialogue/menu/settings/a11y). Keep `PR7_PART1_HANDOFF.md` through `PR7_PART4_HANDOFF.md` unchanged as records of the original milestones.

**Remaining acceptance:** resolve device-reported touchscreen defects (notably menu-row dark/stuck state), run final-source source/build/visual checks, and establish an accepted foundation baseline on the physical S25. Completion of the original coding does not imply completion of device acceptance.

### Part 2 of 6 — Art direction, flexible screen host, and flagship Inventory

**Detailed implementation blueprint:** [PR7_PART2_DESIGN.md](PR7_PART2_DESIGN.md). The document specifies the chosen responsive three-region layout, fallback compositions, actual two-slot equipment model, original ten-item icon plan, Godot architecture, touch/state behavior, test fixtures, visual review checkpoint and explicit non-goals. **This is design approval material, not a claim that Part 2 has been implemented.**

**Outcome:** first polished RPG-quality vertical slice and definitive visual grammar for all subsequent screens.

**Design checkpoint before full implementation:** provide annotated inventories at a standard S25 landscape viewport and at compact 900x560, with item/equipment/icon/selection/details states. Compare at least two compositions by density, thumb ergonomics and how many items fit; select one direction before scaling to other screens. Do not confuse an old mockup or an automated image with an accepted finished design.

**Implementation:**
- Separate a shared safe-area/header/back/modal shell from per-screen layout: `UiScreenHost` must no longer force every screen into one scrollable `VBoxContainer`. Support independently scrollable list/grid regions and fixed inspection/actions regions.
- Create reusable visual primitives: framed slots, generated item icon presentation with fallback, selected/hover/focus/pressed states, text hierarchy, stat deltas, compact action group, item quantity overlays and small contextual hints. Prefer reusable Godot `Control` nodes and cached textures.
- Landscape inventory composition: identifiable **equipment/character** region, **backpack grid/list** region and **persistent selected-item inspection/action** region. A reasonable starting allocation is roughly 28% / 40% / 32%; adjust to actual layout/safe areas, not arbitrary pixel constants.
- A selected item shows name, condition/quality/material when available, description/stats, equipped-vs-candidate comparisons, and context-specific Use/Equip/Unequip, with clear disabled reasons. Do not allocate giant Unequip buttons under every equipped slot.
- Keep currency and capacity/stack summary compact and discoverable. Represent the existing actual item instances, quantity and gear slots; do not invent inventory size or item art as gameplay facts.
- Existing Craft/Trade remain functional during this vertical slice and will receive their final treatment in Part 5. Make their navigation unambiguous without dedicating most of the inventory viewport to permanent large tabs.
- On compact views: adapt pane placement, use an inspector sheet/page if necessary, and preserve inventory context when returning. No horizontal overflow; no forced tiny touch targets.
- Preserve automatic updates after item transactions/load and keep the existing `Items` transaction path. Ensure scrolling does not click rows or leave visual selection stuck.

**Acceptance:** screenshot review of populated and empty inventory, selected gear/consumable/currency, comparison, full pack, long names, larger text and two aspect ratios; functional touch tests for open, select, scroll, equip/unequip/use, cancel, swap and scroll restore; Android frame/memory sanity; no save or transaction regressions.

### Part 3 of 6 — Character identity, readable attributes and progression

Implementation: [Character presentation handoff](PR7_CHARACTER_PRESENTATION_HANDOFF.md). Dedicated responsive hero/gear and Stats/Train/Abilities pages, cached PPC art, authoritative breakdowns and confirmed spending. Automated and physical acceptance are tracked separately.

**Outcome:** Character should feel like inspecting and improving a hero, not reading a debug dump.

- Dedicated layout with a prominent live or cached **Procedural Pixel Creatures** character preview, identity/class/level summary and equipment context. Reuse existing creature rendering pipeline when feasible, with a robust/static fallback if expensive or unavailable; never change actor data simply to render a preview.
- Clear grouped attributes and effective/base/equipment/training values using existing `CharacterOverview.Breakdown` and world authority.
- Skills/abilities presented as purposeful entries with learned/locked/available states, costs, and reasons; explicit confirmation for irreversible training.
- Gear/stat comparison makes changes obvious with numbers and signs, not just colour. Current abilities and future unlocks must not be confused.
- Responsive tab/pane compositions, legible text scaling, controlled focus and no huge list of every stat followed by giant buttons.

**Acceptance:** first-level and progressed character, available/blocked skills, equipped and unequipped appearances, portrait performance, confirmations, phone accessibility, no double spend, save/load consistency.

### Part 4 of 6 — Quest Journal and Exploration Map

Implementation: [Exploration presentation handoff](PR7_EXPLORATION_PRESENTATION_HANDOFF.md). Dedicated quest index/reading panes, compact reading pages, map-dominant canvas, optional guide and bounded around-you pan controls. The user's S25 portrait-facing feedback is corrected with a separate cached front-facing PPC render. Automated and physical acceptance remain separate.

**Outcome:** two distinct, information-rich RPG experiences rather than generic scroll cards.

**Journal:** quest index grouped by status and a persistent selected-quest reading area with current objective, stage history, reward and relevant lore/discoveries. Clearly distinguish active vs available/complete, hide unknown future stage spoilers, and preserve quest selection while switching and returning.

**Map:** map canvas dominates the screen. Overlay compact title, player location, exploration controls/zoom and a collapsible legend/discoveries drawer rather than a lengthy text list ahead of or below the map. Use only `RegionState` explored tiles, landmarks, mechanisms and known entrance/objective facts; never reveal hidden rooms.

**Acceptance:** active/completed/locked journal states; map in Reach and partially discovered ruins; map markers and fog integrity; readable controls at small sizes; touch scrolling/zoom, Back, pause/resume and no unexplored information leakage.

### Part 5 of 6 — Crafting, Merchants and full-shell coherence

Implementation: [Commerce and supporting-shell handoff](PR7_COMMERCE_SHELL_HANDOFF.md). Dedicated workbench/merchant previews, persistent transaction actions, contextual Trade navigation, tile-based pause/save library, grouped settings and adaptive story/reply dialogue. Existing authorities and protective confirmations remain in force. Part 4 received positive S25 feedback; Part 5 still needs device acceptance.

**Outcome:** all remaining player-facing screens share the new art direction without having the same layout.

- **Crafting:** recipe collection, materials owned/required and item-result preview; actions and blockers visible at a glance. Keep exact transactional rollback and full-pack behavior.
- **Trading:** merchant identity and inventory, prices, owned crowns and selected purchase preview; trade should be context-aware instead of a visually dominant always-on Pack tab. Preserve any existing reachable merchant workflows until their replacements work.
- **Pause menu:** prioritize Resume, Pack, Character, Journal and Map; secondary Save/load, Settings, New game and Quit should read as secondary; Developer tools must stay available but deliberately separated.
- **Save/load and Settings:** readable compact summaries and grouped options; irreversible actions keep explicit confirmation and backup protections. Accessibility preferences apply to new controls, not just original buttons.
- **Dialogue:** fit the same art direction and respect Dialogue Manager's actual narrative state, safe areas, response focus, input capture and interruptions. The interface should not block the visible speaker/required choice or show gameplay actions.
- Replace unnecessary full-height forms and oversized buttons across these surfaces while keeping every existing command accessible.

**Acceptance:** complete recipe and purchase transaction scenarios, no action-leaking during dialogue or modal transitions, corrupted/incompatible saves protected, settings persistent, all labels readable and screens visually coherent.

### Part 6 of 6 — System polish, regression and final S25 acceptance

**Outcome:** a coherent finished UI, not merely six separately attractive screen builds.

- Audit every gameplay and overlay transition, including menu-to-screen-to-Back, direct HUD opens, modal cancellation, Android Back key/window pairing, activity pause/resume, defeat/recovery, fast repeated taps, two-finger gestures and menu drag across buttons.
- Integrate pressed/selected/focus feedback and subtle optional transitions; honor reduced motion and high contrast. Ensure fixed controls and notification/HUD do not hide required telegraphs.
- Run `dotnet build`, import, headless integration, desktop visual, `--mobile-ui` visual and relevant gameplay/adventure smoke checks; verify Android APK generated at the **exact final commit**. Do not erase preexisting assertions to make the redesign pass.
- Automated screenshots at 1280x720, 1280x800, 1920x864, 1024x768 and 900x560, with nonzero insets, standard/large text, selected/unselected and empty/full states. Review screenshots for hierarchy, clipping, visible item count and contrasts, not merely bounds checks.
- **Physical Galaxy S25 release gate:** personally inspect inventory/item selection and swipe, equipment/stats, character progression, journal navigation, map panning/zoom, crafting/trading, dialogue, save/load, Android Back, interruption, one complete The Buried Light progression, frame rate and touch comfort. Record observations and fix high-severity regressions before merge.

## Engineering implementation boundaries

- Keep the old functional screens alive behind a migration boundary until a replacement passes its acceptance gate; choose stable names/IDs or update callsites/tests as part of the same focused step.
- Treat `UiDesign` as design tokens/components, not a reason to render every datum as a button. Keep `TouchScroll` isolated to genuinely scrollable subregions.
- Stable `RpgUi.Inventory/Character/Journal/Map/SaveLoad/Settings/Back` and other external interfaces stay available. Preserve `UiNavigation` authority and `MobileCombatControls` geometry unless a separately reviewed ergonomic adjustment is warranted.
- Item icon generation, creature portrait rendering and map drawing must be cache-aware, deterministic when using data-driven procedural art and resilient to missing/unknown items.
- Avoid introducing costly dependencies or external assets without explicit licensing, attribution and Android packaging review. The game should remain runnable offline.
- Preserve all gameplay and progression authority, save formats, action semantics and existing quest access, especially *The Buried Light*. Do not let visual refactoring become a gameplay overhaul.

## Evidence and work discipline

For each new part: maintain a design note/wireframe or screenshot references; scope code changed and preserve authority; add/adjust purposeful tests; run available GitHub workflows; record commit SHAs and Android artifact status; document physical-device findings separately from emulated success. Keep PR #7 in **draft** throughout. New part numbers refer to this revised plan; historical handoffs retain their original numbering and must not be rewritten as if those milestones had been redesigned already.

---

## Appendix: preserved original four-part plan (historical baseline)

The text below is intentionally retained verbatim for traceability. Its Parts 1–4 correspond to **new Part 1A–1D**, not the newly planned Parts 2–6. It documents the previously implemented functional milestones and historical acceptance evidence, not the current final release criteria.

---

# PR #7: Mobile UI and RPG experience overhaul
Status: All four parts are implemented on this branch (Part 1: theme, navigation/screen host, Character pilot; Part 2: gameplay HUD, structured objective, contextual Interact; Part 3: Pack/crafting/trading, progression, Journal, Map; Part 4: themed dialogue, Save & load with slot states and confirmations, persistent UI settings, developer/player menu split, accessibility verification). Automated verification is recorded in `PR7_PART1_HANDOFF.md` … `PR7_PART4_HANDOFF.md` and the PR. **Physical Galaxy S25 acceptance remains pending and is the release gate.** The crafting regression fix remains implemented. Keep the pull request in draft until that acceptance is recorded.

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
