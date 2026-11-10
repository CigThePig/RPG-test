# PR #7, Part 2: The Wayfarer's Kit
## Final design specification: visual identity, adaptive shell, item art and flagship inventory

**Status:** design-only blueprint. No implementation is claimed by this file.
**Repository/branch:** CigThePig/RPG-test, feat/mobile-ui-ux-overhaul, draft PR #7.
**Engine:** Godot 4.6.1 .NET, C# and GDScript, compatibility renderer, Android landscape.
**Depends on:** revised PR7_UI_UX_PLAN.md, PR7_UI_INTERACTION_SPEC.md, completed original implementation parts (now consolidated Part 1).
**Owner of acceptance:** real-device player validation on Samsung Galaxy S25, backed by native automated tests.
**Next milestone:** one reviewed inventory design vertical slice, not all RPG screens at once.

---

## 1. Design decision and problem statement

### Why this is a true redesign

The currently implemented Pack screen has a 740-logical-pixel-width panel; a shared UiScreenHost with a single vertical ScrollContainer/VBoxContainer; oversized, identical full-width action buttons; an EQUIPPED text section before a PACK text section; and an item inspector that expands *under* the selected item row. A three-stack pack still requires scrolling. This is an interaction and spatial hierarchy problem, not merely a colour/font problem.

Part 2 replaces this pattern **for Pack / Items** and creates the visual/layout primitives future Parts 3–5 will reuse. It must not change item authority, quests, combat, save schemas or crafting/trading correctness.

### Core promise

Within approximately one glance a player must be able to locate equipped gear, identify owned items and their stack counts, tell what is selected, understand what it does, and find the valid action. The artwork is recognition first, ornament second.

**Selected direction: A, responsive three-region inventory.** At wide viewports: Equipment + Backpack + Inspector simultaneously. At smaller viewports: compact Equipment + Backpack with inspector replacing part of the view, or a single-pane drill-down. Two-pane-only and inventory-plus-bottom-sheet were considered, but use the available width less effectively and obscure item context; they remain compact fallbacks.

**Important:** the native game screenshot shows visible black gutters at the sides of the playable viewport. Design relative to the *Godot logical safe gameplay viewport*, not unowned physical letterbox bars. This part does not alter world camera framing or pretend the black gutters can receive controls.

---

## 2. Repo facts that constrain the design

| Production fact | Source / implication |
| --- | --- |
| Actual equipment slots are **weapon** and **body** only | Items/ItemService; show two functional gear slots, not fake helmet/rings/shield slots |
| ItemView has Selector, Instance, Id, Title, Count, MaxStack, Slot, Equipped, EquippedSlot and Properties | Read-only model for selection, material, quality, condition, stats and quantity |
| Gloot remains authoritative for stack identity, capacity, equip/unequip, craft, trade | Never store a second inventory or infer slot capacity from UI cell count |
| Current item IDs: iron_sword, oak_staff, leather_coat, potion, herb, ore, wood, coin, relic, dungeon_key | A small complete icon set is achievable now |
| Existing inventory control IDs include tab_items, tab_craft, tab_trade, item_<selector>, item_detail, equip, use, unequip_weapon, unequip_body | Preserve, alias, or migrate with equivalently meaningful tests; do not silently break automation |
| PackTab = 0/1/2 and PackSelection are exposed by RpgUi | Preserve compatibility getters/setters while introducing richer private selection state |
| ItemService can return "pack full" for operations even without exposing a fixed slot capacity | No fictitious "10/24 slots" or endless empty grid implying a capacity limit |
| Existing UiScreenHost.Body is a VBoxContainer and UiScreenHost.Scroll is a single TouchScroll | Build migration API supporting per-screen custom content while preserving legacy screens |
| UiPreferences supports text scale 1.0, 1.3, 1.4, reduced motion and high contrast | All new components must honour these immediately |
| Existing item action implementations in PackScreen use Foundation.ManageItem, Items.Equip/Unequip and RpgUi.UseFromPack | Reuse the exact production paths, not novel direct Gloot writes |

Initial equipment appearance maps staff vs dagger and outfit=tunic through ItemService; a live player character portrait is assigned to **Part 3**. In Part 2, do not promise accurate visual worn-gear rendering from the inventory silhouette until that pipeline is available.

---

## 3. Primary layout, for the normal landscape gameplay viewport

A **near-full-playable-viewport** overlay with a fixed header and a compact, fixed navigation strip; three independent body regions. Not a modal inside the old 740px scroll column. A subtle, opaque or high-contrast-appropriate dimmer can separate paused world art from the interface; Back remains always accessible.

### Composition A: selected primary proposal

~~~text
┌───────────────────────────────────────────────────────────────────────┐
│  ✦  THE WAYFARER'S KIT          Crowns 20           [ Back ]         │  fixed header
├───────────────────────────────────────────────────────────────────────┤
│  [ PACK • active ]      [ Craft ]      [ Trade ]                    │  compact secondary navigation
├──────────────────┬─────────────────────────┬────────────────────────┤
│  EQUIPPED        │ BACKPACK                │ INSPECT                 │
│                  │ 3 stacks                │                         │
│  [ character     │ ┌────┬────┬────┬────┐   │  [ 32px icon / bigger ] │
│    silhouette ]  │ │🪙20│🧪 3│🌿   │🗝️   │   │  Restorative draught    │
│                  │ ├────┼────┼────┼────┤   │  Consumable • ×3        │
│  WEAPON          │ │    │    │    │    │   │                         │
│  [sword] Iron    │ ├────┼────┼────┼────┤   │  Restores 35 health     │
│  sword  DMG 8    │ │    │    │    │    │   │  Current HP: 65/100     │
│                  │ └────┴────┴────┴────┘   │                         │
│  BODY            │                         │  [ USE ]                │
│  [coat] Leather  │ Swipe items when longer │  Action information     │
│  coat  DEF 4     │ than region             │  stays beside selection │
└──────────────────┴─────────────────────────┴────────────────────────┘
~~~

**The icons above are semantic placeholders for the wireframe, not final game art or actual default bag contents.** Only two equipment slots are active; other silhouette markings must not resemble locked/unimplemented slots.

### Layout rules, not brittle magic sizes

- Derive the outer overlay from the converted **logical safe area** with approximately 12–20 units of inset. Reserve the app system insets; do not place action UI in black letterbox gutters. The existing menu may remain compact; only the Inventory layout takes this wide mode during Part 2.
- Content is horizontally arranged using minimum widths and weighted allocation; at full landscape size start near **26% Equipment / 41% Backpack / 33% Inspector** of usable body width. These are starting ratios, not absolute cell sizes.
- Header height roughly 56–68 logical units, second navigation band 48–56, body occupies the remaining height. Back >=56 high with readable label; never make the entire content scroll just to reach Back.
- Equipment panel: silhouette is supportive rather than dominant; two unmistakable equipment-slot entries with icon, item name and compact relevant stat. Tapping a slot inspects its real equipped item. Empty slots say "Weapon empty" or "Body empty".
- Backpack panel: responsive **scrollable tile grid** of actual Gloot stacks; each tile is roughly 64–76 logical units minimum, with 6–10 spacing. Favour 5–6 columns in a wide landscape region if measured space permits. A small quantity badge appears at a consistent corner; a selected item's *name* remains visible in the inspector. Decorative unused cells may fill an incomplete last row but must not signal real Gloot capacity.
- Inspector panel: always allocated in three-region mode. Without selection: a discreet "Select an item to inspect" instructional state. With selection: recognizable icon, name, category/type, meaningful stats and a compact explicit action group at the bottom. Long details scroll *inside* this region; action controls remain fixed and visible when possible.
- The user does **not** have to scroll past equipment or large buttons to see the backpack. Up to all 10 current catalog types should fit at a suitable standard landscape size, when displayed one stack each; generated gear instances may require backpack-only vertical scrolling.

### Composition B: considered alternative, not the primary design

~~~text
┌────────────────────────────────────────────────────────────────┐
│  Header / Back                                                 │
├──────────────────────┬─────────────────────────────────────────┤
│ CHARACTER + GEAR     │ BACKPACK, broad scrolling grid           │
│ 2 functional slots   │ selected tile opens bottom inspection    │
│                      │ drawer over part of grid                 │
└──────────────────────┴─────────────────────────────────────────┘
~~~

This offers larger item tiles but makes inspection obscure part of the grid and weakens side-by-side comparisons. **Reject as default at wide sizes**. It remains acceptable only as a small-screen fallback.

---

## 4. Responsive behavior and phone constraints

Decide the layout **from safe content width, available height, font scale and measured child minimum sizes**, not solely from Android/desktop flags or raw pixels.

| Mode | Trigger (initial guidance, revise after measured layout) | Behavior |
| --- | --- | --- |
| Wide three-region | Effective safe width >= ~1050 at current text scale, enough vertical room for header + body | Equipment, Backpack, Inspector side by side; item grid normally 5–6 columns |
| Compact two-region | Effective safe width ~740–1049 and sufficient height | Equipment condensed + Backpack grid; tapping an item switches the right region into Inspector with a prominent "Back to items"; equipment remains visible |
| Constrained single-region | Effective safe width < ~740, or too little height after scale | One region at a time: Pack list/grid, optional Gear view, Inspector drill-down; explicit Back-to-pack, then regular Back out of the screen |

"Effective width" means the available logical area adjusted by the current text scale and real component minimum sizes; this is not a new OS dpi conversion. Height must be considered independently. No touch target should be made tiny to maintain a decorative three-column layout.

Specific acceptance cases: standard 1280x800, 1280x720, 1920x864, 1024x768 and **900x560 with nonzero insets**; repeat at text scales 1.0, 1.3 and 1.4, including high-contrast. The 900x560/1.4 case may legitimately use single-region drill-down. No horizontal scrolling or clipped Back/action controls.

On compact mode, tapping "Back to items" reverses the **local inspector navigation**; tapping the fixed header Back from the ordinary Pack view returns to the menu or direct gameplay according to the existing UiNavigation caller contract. Normal wide-mode Back leaves the Pack screen immediately. Android Back/Escape follow equivalent local-first behavior in compact mode; avoid a double-pop when the OS delivers both key and WM requests.

---

## 5. Visual language and component system

### Art direction: "The Wayfarer's Kit"

Naturalistic **2D pixel-object artwork** inside tactile, understated fantasy frames. Mosslight's palette should draw from moss, weathered stone, dark bark, bronze hardware and ivory labels. Avoid photo-real leather textures, glossy free-to-play gradients, heavyweight intricate filigree or imitation of named game assets.

Suggested token starting set (must pass measured contrast review and the high-contrast setting):

| Token | Candidate | Use |
| --- | --- | --- |
| Backdrop | #101A19 | Opaque pause backdrop / deep shadow |
| Surface | #172523 | Main frame, consistent with the existing theme |
| Raised panel | #20332E | Equipment/backpack/inspector regions |
| Slot well | #12211F | Tile recess and silhouette area |
| Main text | #EEE9D6 | Core text |
| Secondary text | #C9C6B6 | Explanations and metadata |
| Bronze | #BCAA76 | Borders, selection trim |
| Soft moss | #91B28A | Positive equipment change, with + number |
| Warm clay | #DDA087 | Negative equipment change, with − number |
| Focus | #FFDF8C | Distinct keyboard/controller focus, not selection |

Visual hierarchy: **Header / item name > meaningful stats > compact explanatory detail > decorative labels.** Font choices should remain compatible with Godot, Android packaging and license constraints; use the current readable typefaces until a separately reviewed licensed font is available. No essential information is conveyed by hue, hover or tiny glyphs alone.

**Different visual states must mean different things:**

- Unselected = recessed neutral surface, no thick border.
- Selected = persistent bronze inner stroke plus a visible selected cue; does **not** look black/pressed.
- Pressed = brief darkened/tactile state only while pointer/finger owns the tap.
- Keyboard focus = outer high-contrast gold outline distinct from selected.
- Disabled action = readable state, explicit reason text; the selected tile itself need not become disabled just because the action is.
- Equipped item = small "EQUIPPED" textual label in inspector/slot, not a rarity glow or selection confusion.
- Stat comparison = +/- numeric values and words, with colour redundant.
- No unsupported rarity tiers: actual generated quality 1–3 can appear as text, not invented common/rare/legendary tiers.

### Reusable UI building blocks

1. **RpgScreenFrame**: safe-area overlay, dimmer if needed, header, fixed Back, content mount.
2. **RpgPanel**: panel title and optional fixed footer; body content may scroll independently.
3. **ItemIcon**: deterministic texture/fallback, consistent scale/filter.
4. **ItemTile**: one real stack/instance, touch and focus target, quantity and selection state.
5. **EquipmentSlot**: actual weapon/body item with icon, named status and inspect action.
6. **ItemInspector**: named stats, meaningful description, compare block, contextual buttons and reasons.
7. **StatDelta**: signed and verbally named increase/decrease/unchanged state.
8. **CompactNav**: small Pack/Craft/Trade routes with visible current section and 56-unit hit targets where feasible.

These are *components*, not a rule that every future screen must look like a three-column inventory.

---

## 6. Item icon art plan, deterministic and cacheable

Use an **original 32x32 pixel glyph** source size, scaled with nearest-neighbour filtering into a 48–60 unit icon presentation within a >=64-unit tile. Keep 1–2px readable outer stroke, distinct silhouettes and three or four flat tones. Draw icons programmatically or ship a small authored sprite atlas under game/art/ui; do **not** add external copyrighted icon packs by default.

| Item ID | Planned silhouette / palette |
| --- | --- |
| iron_sword | diagonal sword with crossguard; steel blue-grey and brown grip |
| oak_staff | carved wooden staff with crooked head, amber/moss highlight |
| leather_coat | simple tunic/coat shoulders with leather seams |
| potion | squat corked draught bottle, muted ruby liquid |
| herb | silverleaf sprig with pale leaf clusters |
| ore | jagged bog-iron nugget with oxidized bronze flecks |
| wood | small bound pieces of seasoned timber / log silhouette |
| coin | stamped stacked crowns with contrasting rim |
| relic | ancient carved stone/sigil, not a generic sword |
| dungeon_key | distinctive old brass/stone ruin key |

- Icons represent **item type**, not individual instance identity; instance-specific material/quality/condition appear through a small overlay, border accent and textual inspector. Do not generate a separate image for every item instance.
- Cache by deterministic key (item ID and optional material variant), **not instance selector**; build once on asset initialization/use. Reuse the existing EnvironmentAssetCache pattern of Image -> ImageTexture with no per-frame SetPixel/texture creation.
- Avoid changing global RNG or procedural creature seeds to make UI assets.
- Unknown future IDs get a recognizable generic satchel/unknown-item fallback and their real item name in details. No missing-texture crash or misleading icon.
- All current catalog icons must visually remain distinguishable at actual phone size. Verify silhouette visibility at default and high contrast.
- Use the exact item catalog title and real properties for meaning. Optional flavour text can be an explicitly non-authoritative lookup keyed by known ID; it must never invent effects/requirements.

A live PPC hero preview with exact outfit/accessories is **not required for Part 2**. Use a tasteful empty character silhouette/panel supporting the real two gear slots; leave a safe preview mount for Part 3. Do not spend Part 2 rebuilding creature graphics.

---

## 7. Player interaction contract

### Selection state

Introduce a presentation-only state holding **source** (Bag or Equipped), selector, and equipped slot where relevant. Validate it against a fresh Items.View snapshot whenever inventory is opened/refreshed. Keep RpgUi.PackTab and RpgUi.PackSelection working as compatibility entry points during migration. The world never stores this UI selection.

A normal tap on a bag tile selects that item and updates the inspector. Tapping the selected tile again leaves it selected (it does not automatically use/equip and need not collapse the inspector). Tapping Weapon or Body selects the actually equipped item; the inspector then exposes **Unequip** rather than Equip. No double-tap, long-press, drag-and-drop or hidden gesture is required.

### Action behavior matrix

| Current selection/action | Intended result |
| --- | --- |
| Bag weapon/body item -> Equip | Use existing Foundation.ManageItem -> Items.Equip with exact selector/slot; show resulting equipped item and updated stats in the slot/inspector |
| Equipped Weapon/Body -> Unequip | Existing ManageItem -> Items.Unequip; if successful select the returned bag item and keep backpack context |
| Potion -> Use | Existing RpgUi.UseFromPack, including its pause/menu caller behavior; update health/count; clear stale selection if stack is exhausted |
| Herb/ore/wood | Inspect, show actual material/crafting relevance without fake Use action |
| Coins | Show currency summary and actual quantity; no Equip/Use |
| Relic/key | Display possession and honest identity; no false usable action |
| Failed equip (cannot grasp or full bag) | Do not mutate; visible precise blocker from EquipBlocker/transaction and no lost equipped item |
| Failed unequip (full bag) | Do not mutate; remain equipped and explain capacity issue |
| Save/load or other inventory mutation | Reconcile selection by real selector; handle missing item gracefully |
| Swap gear with full bag | Mirror actual Gloot swap-preflight; preview cannot claim success unless authority confirms |
| Tap during scrolling | No selection or action; no residual pressed/focus state |
| Back from wide Pack | Existing caller-aware Back |
| Back from compact Inspector | Return to Pack region first; another Back leaves Pack |
| Switching Pack/Craft/Trade | Preserve current Pack context for return; do not invent merchant accessibility |

All item actions remain **explicit buttons** in the inspector or equipment context, not "tap tile to use". When a button action fails, keep relevant context, display a visible explanatory reason and ensure the button's state isn't stale. Distinguish explanatory warning (e.g. using potion at full health restores nothing) from actual disabled action: do not silently change gameplay semantics.

### Gesture and focus ownership

- The backpack grid is the **only** continuously scrollable region in ordinary Pack view. The inspector may have an independent vertical scroll for long content. Equipment remains stationary.
- Swipe initiated on an ItemTile is owned by its enclosing scroll region after a movement deadzone; releasing anywhere does not select, activate or transfer the touch to another item. The selection persists unless the player deliberately taps another item.
- Match existing TouchScroll's touch/emulated-mouse deduplication, including mouse-down preceding screen-touch and mouse-up preceding release. Give each nested or neighbouring scroll owner exclusive ownership by hit region; never have two overlapping TouchScroll owners consume the same gesture.
- Opening/closing Pack, opening confirmations, system Back, app background, focus loss and viewport resize cancels all touches, scroll momentum and pressed visuals. Clear only transient states; preserve actual selection where appropriate.
- Keyboard/controller: focus traversal follows Equipment -> Backpack -> Inspector/actions in visual order. Focus border must not persist as "selected" after a touchscreen drag. Initial Back focus preserved where useful, directional traversal functional.
- A touch-selected item must remain visibly selected even when keyboard focus moves elsewhere; focus and selection are separate states.

---

## 8. Suggested Godot and C# architecture

### Do not force every screen into the existing VBox

Adapt the current UiScreenHost so the header/back/nav coordinator remains stable while the body accepts either:
1. the old legacy Scroll/VBox content for Character, Journal, Map, Settings, etc. until migrated; or
2. a mounted **custom Control layout** for Pack.

Recommended migration surface (illustrative names, not promised APIs):

~~~text
RpgUi (existing facade/navigation)
  └── UiScreenHost (shared overlay/header/back)
       ├── LegacyContentHost (TouchScroll + VBox for unconverted screens)
       └── CustomContentHost (Panel/Container owned by the active screen)
            └── PackRoot
                 ├── PackNav
                 ├── EquipmentPanel
                 ├── BackpackScroll
                 │    └── ItemGrid
                 └── InspectorPanel
~~~

Suggested code responsibilities:

| File / type | Responsibility |
| --- | --- |
| game/ui/UiScreenHost.cs | Allow custom content mount, safe-area reflow, fixed Back, restore legacy host on other screens |
| game/ui/RpgUi.cs | Keep Inventory(), PackTab, PackSelection and caller-aware navigation; instantiate/refresh Pack presentation; no gameplay logic migrated |
| game/ui/PackScreen.cs | Orchestrate read-only view model and child regions; leave transactional Craft/Trade code intact for Part 5 |
| game/ui/PackViewState.cs (new) | Presentation-only selection source/selector, responsive local drilldown and scroll offset; no persistence |
| game/ui/PackLayout.cs (new) | Equipment/Bag/Inspector compositions, breakpoint fallback and fixed action area |
| game/ui/ItemIconAtlas.cs (new) | Original deterministic icon atlas/fallback, caching and missing-ID handling |
| game/ui/ItemTile.cs (new) | One real bag instance/stack interactive target, selection/focus/high-contrast states |
| game/ui/EquipmentSlotView.cs (new) | The two production slots, not imaginary gear types |
| game/ui/ItemInspector.cs (new) | Reuse PackScreen.Describe/Stats/Comparison/EquipBlocker and real item actions |
| game/ui/UiDesign.cs | Extend tokens for frames/tiles/stat deltas, retain compatibility with existing screens |
| game/tests/* | New rendered touch/layout tests plus updated RpgScreensChecks, AccessibilityChecks, navigation checks |

Keep actual file count sensible: the ownership split matters more than using every suggested filename. Prefer Godot Containers, anchors and Control minimum sizes over calculating dozens of absolute positions; use explicit measurements only where actual icons/tiles need stable dimensions.

### Refresh cycle

Open -> read **one** Items.View snapshot -> derive equipment/bag/inspector presentation -> construct/update layout. Tap to inspect -> update selected visual and inspector without destroying/rebuilding the entire backpack. Item transaction -> call existing authority -> reacquire Items.View and reconcile selection -> update only affected panes. Back/reopen -> no stale node handles; keep intentional presentation context if navigation requests it.

Never parse ItemService inventory JSON in a per-frame _Process. Never regenerate image textures every refresh. Avoid layout jumps when selecting a tile. Keep the scroll offset after equipment/use actions when items remain in the same order.

### Scope boundary

Do not rewrite Craft/Trade transactions, ItemService, Items, Content, World, Narrative, persistence, combat, creature simulation, environment generation or game save format. Keep old Craft and Trade routes usable and testable while they await their final interface in Part 5. If the old tab labels/IDs change visually, preserve stable IDs or update tests to assert the same user-visible behavior rather than bypassing them.

---

## 9. Test design, observable success and failure

### Design signoff before implementation

Produce **two annotated static screen compositions** at normal S25 landscape logical viewport and a compact fallback at 900x560. Compare A (three-region) versus B (two-region with details drawer) on information visible without scrolling, item recognizability, real two-slot equipment, inspector readability, thumb-target distance, large-text behavior, and room left for game context. The three-region composition is the current recommended default; physical-size review can refine proportions and breakpoints.

### Functional regression suite

- Default player: sword and coat equipped, crowns, potion and other real bag stacks as provided by production start state. Verify exact counts/stats, zero duplicates, only weapon/body.
- Bag: empty, 1, 3, 10 distinct prototypes; many separately generated unique gear instances; stacking/quantities/unknown-ID fallback.
- Selection: select potion, coin, material, unique sword, equipped weapon/body; selected vs focused vs pressed; inspect a disabled or empty slot.
- Equipment: equip/unequip, swap, no grasp, full bag, failed transaction rollback, source selector remains stable; check actual world stat values.
- Use: one potion, last potion, full health explanatory note, cooldown/action refusal, no double consumption; return to correct caller.
- Gesture: tap tile, short motion under deadzone, long swipe across several tiles, repeated swipes in opposing directions, swipe + emulated mouse out of order, cancelled touch, release outside, simultaneous second touch, focus lost, Android Back.
- UI: long item title, high contrast, text scales 1.0/1.3/1.4, zero clipped interactive regions, fixed header/back and action footer, no text/action overlap, no horizontal scroll.
- Persistence: load a save with selected item no longer present, refresh on Gloot import, preferences preserved, no inventory schema change.
- Craft and Trade still reachable and behave exactly as before; preserve named automation paths or migrate carefully.
- Desktop mouse/keyboard selection and wheel scroll remain functional; actual mouse input isn't swallowed by Android emulation safeguards.

### Visual fixture screenshots

Capture in **standard and --mobile-ui** where applicable, using actual rendered game state rather than Photoshop:
- pack-default-wide, pack-selected-potion, pack-selected-sword-comparison, pack-equipped-weapon, pack-empty-bag, pack-material, pack-coin, pack-unknown-fallback
- pack-compact-900x560, pack-compact-inspector, pack-large-text-1.4, pack-high-contrast, pack-many-unique-instances, pack-disabled-full

At minimum run 1280x720, 1280x800, 1920x864, 1024x768 and 900x560 with nonzero insets. Check **visual density and actual information architecture**, not just that nothing crosses a rectangle bound.

### Performance

Performance on the physical Galaxy S25 has been confirmed good by the owner. No performance budgets, instrumentation or measurements are required for this work.

### Final phone acceptance checklist

- In the first five seconds, can the player identify equipped weapon and body armour, owned draught count and gold without searching or scrolling?
- Do icons remain identifiable at actual phone scale, without memorizing colours or relying only on tiny text?
- Can the player select, compare, equip, unequip and use items with ordinary taps; can they scroll repeatedly without ghost activation or a stuck-dark row?
- Are Back and every relevant action within reachable safe bounds even at the largest text preference?
- Can the player open Craft and Trade and return to the Pack without losing transaction functionality?
- Is performance stable through opening/closing, scrolling populated bags, Android background/restore and item mutations?
- Is the layout visibly more like a purposeful RPG inventory than the current long form?

**Exit gate:** do not call Part 2 complete until source/test/Android APK status and actual S25 player acceptance are documented. Keep PR #7 draft.

---

## 10. Execution order for a subsequent implementation agent

**2A: Design + layout spike.** Create a non-destructive prototype inside the game using real default item data. Capture annotated wide/compact screenshots; compare with the old UI. Do not broaden to Character/Map yet. Get the layout and icon scale judged before further investment.

**2B: Visual primitives + item icon atlas.** Implement cache/fallback and reusable ItemTile, EquipmentSlot, Inspector/StatDelta. Build source-level icon and contrast tests. Do not change transactions.

**2C: Inventory replacement.** Mount the custom Pack layout via UiScreenHost migration API; wire real ItemView data and selection/state reconciliation; use existing action paths. Keep Craft/Trade accessible.

**2D: Responsive and input hardening.** Reflow at all viewports and text scales; local compact inspector Back semantics; touch gestures, focus, emulated mouse, interruptions and scroll ownership.

**2E: Full verification + handoff.** Run .NET build, Godot import, headless suite, rendered desktop/mobile suites, preserved RPG adventure and Android APK for the precise final commit. Review screenshots and physical Galaxy S25 playtest. Document any known limits and outstanding defects before moving to Part 3.

### Explicit non-goals for Part 2

No additional equipment slots; no invented bag capacity; no item rarity/economy change; no drag/drop sorting or stack splitting; no new crafting/trade rules; no real paper-doll appearance simulation until Part 3; no UI-only edits to quest state; no world camera/aspect-ratio rewrite; no new external art licenses/dependencies without separate approval; no simultaneous rewrite of Character/Journal/Map.

### Completion definition in one sentence

**Part 2 succeeds when a real Galaxy S25 player can glance at a populated inventory, recognise their actual gear and items, inspect any item, perform its real action safely, and leave the screen feeling they just used an RPG inventory rather than navigated a list of settings.**
