# Part 3 — RPG screens: the Adventurer's Book

**Goal:** turn Pack, Character, Journal and Map into real RPG screens: an item grid with details and comparisons, equipment slots, crafting and trading with clear requirements, a Hero sheet with abilities and skills, a stage-based journal, and an icon map with quest markers. **Gloot, QuestSystem/Narrative and World stay authoritative.** Every page is a view that rebuilds from live queries after each command.

Prerequisites: Part 1 (Book shell, components). Part 2's `journal_entries()` and `ability3` slot. If Part 3 lands first, build `journal_entries()` here and keep Hero's "Use" buttons until `ability3` exists. Mockups: [`02-book-pack.svg`](mockups/02-book-pack.svg), [`03-book-craft.svg`](mockups/03-book-craft.svg), [`04-book-hero.svg`](mockups/04-book-hero.svg), [`05-book-journal.svg`](mockups/05-book-journal.svg), [`06-book-map.svg`](mockups/06-book-map.svg).

## 0. Book frame recap (from Part 1)

- Touch: the Book fills `safe` inset by 16 (1248 × 768 at 1280×800). Desktop: 1100 × 680, centred.
- Header row (height `target.row`): `TitleLabel` "Adventurer's Book" · `TabBar` [Pack · Hero · Journal · Map] · crowns chip (`coin` icon + amount) · close `IconButton`.
- Content area = frame minus header minus padding. All page layouts below are expressed as columns of this area, so they reflow for `expand` widths (S25 ≈ 1696 lp content width) and for Desktop.
- The world simulation is paused while the Book is open (`GameplayBlocked`). Merchant proximity, cooldowns etc. are therefore stable for as long as the Book is open. Compute them on `Refresh()`.
- Mutations always go through `App.ManageItem(() => …)`. If it returns false while `CanManageItems` is false, show a `warning` toast "You can't do that right now".

**Responsive rule.** If the content width is < 980 lp (Desktop small windows, compact fallback), pages with three columns collapse to two: the details pane becomes a bottom sheet over the grid (height 50%, with a close handle). Test this at 1024×768.

## 1. Data APIs (all additive; existing callers keep working)

| Where | Add | Notes |
|---|---|---|
| `ItemService.gd catalog(owner)` | add fields `quality:int`, `material:String`, `condition:int`, `stats:{damage, defence, power, mass, healing}` (only present properties), `equipped:false` | keep the existing fields and `properties` string |
| `ItemService.gd` | `equipment(owner) -> String` JSON `[{slot, selector, id, title, quality, material, stats}]` for each occupied `owner:slot` | read-only |
| `ItemService.gd` | `unequip(owner, slot) -> bool`: if the slot item exists and `bag(owner).can_add_item(item)` → move it to the bag; else false. Mirrors the safety rule in `equip` | new mutation |
| `Items.cs` | `Unequip(ActorState actor, string slot)` → `service.Call("unequip", …)` then publish `item_unequipped`. Extend Foundation's existing `item_equipped` view refresh (Foundation.cs ~line 325) to handle `item_unequipped` the same way | |
| `ItemService.gd` | `preview(prototype, material, quality) -> String` JSON of the properties a generated item *would* have. Build the item in memory and **never** add it, touch `serial`, or emit signals. `CraftingChecks` compares `serial` exactly | for craft previews |
| `Narrative.gd` | `journal_entries()` (schema in PART2_HUD §5) plus `"goal": {"kind","target"}` for non-staged quests (e.g. `{"kind":"crafted","target":"draught"}`) | |
| `Narrative.gd` | `lore_entries() -> String` JSON `{discoveries:[{key, name}], mechanisms:{shortcuts, seals}, lore:[{key, text}]}`, built from the same `flags` that `journal()` reads | |
| `quests.json` | optional `"marker"` on each stage (see §6). `EventQuest.update` ignores unknown keys; `ContentAudit` only checks IDs | |
| `UiText.cs` | display-name tables: `RegionName`, `SkillName`, `StatusName`, `StatName`, `QualityName(int)`, `ItemCategory(id, row)`, `LandmarkName(template)`, `ItemBlurb(id, stats)` | no raw IDs in the UI |

Add headless tests for every new GDScript function in `BookChecks.cs`, using isolated owners (`"book-test:*"`) and `Persistence.Capture/Restore` like `CraftingChecks`.

## 2. Pack ▸ Bag (mockup 02)

```
[ Bag | Craft | Trade ]                                                  ◎ 20 crowns
┌ Equipped ───────────┐ ┌ Bag (n items) ───────────────────┐ ┌ Details ─────────────────┐
│ Weapon              │ │ [tile][tile][tile][tile]          │ │ [96 icon]  Iron sword     │
│ [80] Iron sword     │ │ [tile][tile][tile][tile]          │ │ Superior · Iron · Weapon  │
│      Damage 8       │ │ …                                 │ │ Damage   9   ▲ +1         │
│ Body                │ │                                   │ │ Mass     2.0              │
│ [80] — empty —      │ │                                   │ │ Condition 100%            │
│ Totals              │ │                                   │ │ [ Equip ]  [ Unequip ]    │
│ Damage +8 Def +0    │ │                                   │ │                           │
└─────────────────────┘ └───────────────────────────────────┘ └───────────────────────────┘
   260 lp                  flexible (min 4 tile columns)          380 lp
```

**Equipped column.** One card per slot that appears in content (`weapon`, `body`; derive the list from `items.json` slot values so new slots appear automatically). Each card: slot caption, 80 lp `ItemTile`, name, primary stat. An empty slot shows a dashed `line.subtle` frame with "Empty". Tapping a filled card selects that item in Details. Below the cards, "Totals" lists `Items.Bonus(player, "damage"|"defence"|"power")`. If the player can't grasp (`!Grasps`), the weapon card shows an amber caption "Your form can't hold a weapon".

**Bag grid.** One `ItemTile` per bag stack, excluding `coin` (shown in the header chip). Columns = `floor((width + 12) / (tile + 12))`, tile 96 (Touch) / 64 (Desktop). Sort by category (Equipment → Consumable → Material → Quest item), then title. A section caption row between categories is optional when ≤ 2 rows. Tile content: icon (`UiIcons.ItemIcon(id)`), count badge if `count > 1`, quality pips, "E" chip if equipped. Tap → select (gold 3 lp frame) → Details. On Desktop, arrow keys move the selection (use `FocusNeighbor*` from grid positions) and Enter runs the primary action. Empty bag → `EmptyState` "Your pack is empty. Gather herbs, open chests and loot fallen foes."

**Details pane.** Nothing selected → `EmptyState` "Select an item to see details". Otherwise:
- 96 lp icon in a quality frame, `HeadingLabel` title, caption "{Quality} · {Material} · {Category}" (omit empty parts).
- One stat row per present stat (`StatName` label, value right-aligned). For equipment, compare with the item currently in the same slot: `▲ +n` in `accent.moss`, `▼ −n` in `danger.text`, nothing when equal. The arrow glyph carries the meaning as well as the colour.
- `ItemBlurb`: one generated sentence, e.g. "Restores 35 health." or "Opens one sealed gate in the ruin."
- Actions (primary first):
  - **Equip**: slot item, not equipped. If `slot == "weapon" && !player.Grasps`, disable with the reason "Can't hold weapons in this form".
  - **Unequip**: equipped item. Disable with "No room in your pack" if `unequip` would fail. Pre-check with a new `ItemService.can_unequip(owner, slot)` or by trying and handling `false`.
  - **Use**: `healing > 0`. Like today, this **closes the Book first**, then calls `App.UseQuickItem(selector)`, because using items is gameplay. The label is "Use" and the caption under it reads "Closes the pack".
  - No Drop/Sell (decision D5).
- After any action: `Refresh()` and keep the same selector selected if it still exists.

## 3. Pack ▸ Craft (mockup 03)

Left: recipe list (`ListRow`, 96 tall). Right: recipe details.

- **Row:** output icon · output display name ("Restorative draught", from `items.json` name, not the recipe ID) · subtitle listing the ingredients ("2 Silverleaf", "3 Bog iron · 1 Seasoned wood") · trailing chip **Ready** (moss + check) or **Missing** (`text.secondary`). Add a gold **Quest** chip when an active quest has `goal.kind == "crafted"` and `goal.target == recipe.Id`. Sort: Quest, then Ready, then the rest by name.
- **Details:**
  - Output tile (96) + name + "×{Count}". For material recipes, show the `preview()` stats ("Iron · Superior · Damage 9.6") and compare them with the equipped item in the same slot, like the Bag page.
  - Ingredient rows: icon · name · `have / need`. Enough → moss check. Short → `danger.text` "Need 1 more".
  - **Craft** (primary, `target.primary`):
    - Missing ingredients → disabled, reason "Missing: 1 Silverleaf".
    - Otherwise `App.ManageItem(() => World.Items.Craft("player", recipe))`. Success → `loot` toast "Crafted Restorative draught" plus a 300 ms gold flash on the output tile. Failure (ingredients were sufficient, so it was capacity) → `warning` toast "No room in your pack for Restorative draught". The transactional `Items.Craft` from this PR guarantees nothing was consumed.
  - No batch crafting in this PR.

## 4. Pack ▸ Trade

- The segment is enabled only if `App.NearbyMerchant() != null` when the Book opens. Otherwise it stays visible but disabled, with the reason "Find a merchant to trade" (discoverability). The **Bag** segment is the default unless the Book was opened from dialogue (Part 4 "Show me your supplies").
- Header: merchant name ("Reach warden's supplies") and the player's crowns.
- One row per merchant stock stack (catalog of `merchant.Id`, excluding `coin`, as today): icon · name · stats caption · "{count} in stock" · price chip "5 crowns" (`coin` icon; the price stays the constant `5` used by today's `Trade(..., 5)`; put it in one `const` and don't scatter it) · **Buy** button.
  - Disabled reasons: "Need {5 - crowns} more crowns", "Out of stock". If `Trade` returns false after passing these checks, it was capacity → `warning` toast "No room in your pack".
  - Success → `loot` toast "Bought Leather coat" and `Refresh()`.
- Selling is out of scope (D5). Don't render a Sell tab.

## 5. Hero (mockup 04)

Two columns: **Profile** (380 lp) and **Growth** (rest; `Segmented` [Abilities | Skills]).

**Profile:**
- 120 lp medallion with the level number and XP ring (same as the HUD, larger). Name ("Wayfarer") and family caption (e.g. "Biped"; take `Appearance.Family` through a display table, and move the seed to the Developer screen).
- XP bar 340 × 10 with caption "40 / 300 XP to level 4".
- Skill points chip: gold "2 skill points to spend" if > 0, else `text.secondary` "No skill points".
- Attribute table (label left, value right; `LabelStrong` values): Health `84 / 105`, Mana `42 / 60`, Strength `World.Stat(actor,"strength")`, Defence `World.Stat(actor,"defence")`, Weapon damage `+Items.Bonus(actor,"damage")`, Speed `actor.Speed`. When a stat differs from the base attribute (skills/equipment), add a caption "(+2 from Blades)" where it can be derived cheaply. Otherwise show only the value.
- Traits: chips "Grasps", "Swims", "Flies" (only the true ones), with icons.
- Reputation: "Wardens · 5" with a one-word standing (`< 0` Wary, `0–19` Neutral, `20–49` Friendly, `≥ 50` Honoured). Use a `UiText` helper; don't add mechanics.

**Abilities** (cards, `ListRow` 112 tall), for every ability that `a.PlayerLearnable || actor.Abilities.Contains(a.Id)` (today's filter):
- Icon · name · caption "9 mana · 1.5 s cooldown · range 240" (omit range for self abilities).
- Trailing:
  - **Known** chip (moss), plus a caption showing where it lives on the HUD ("Button 2" / "Slot 5") so players learn the controls.
  - **Learn · 1 pt**: primary button → `App.ManageItem(() => Progression.Unlock(...))`. Disabled reasons: "Requires level 2", "Needs 1 skill point".
- **Remove the "Use · ability" buttons.** Casting from a menu is a workaround that Part 2's `ability3` slot replaces. Keep them only if Part 2 hasn't landed, and note that in the handoff.
- Learning success → `level`-category toast "Learned Bark ward · now on your action bar".

**Skills:** one row per `Content.Skills`. Each row: name (`SkillName`) · caption "+{PerRank} {StatName} per rank" · rank pips (`MaxRank` circles, filled = rank; pips have outlines, so they don't rely on colour) · **Train · 1 pt** button with the reasons "No skill points" / "Max rank". Success → `Refresh()` and a toast "Blades rank 2".

Badge: the **Hero** tab shows a gold badge with the skill-point count while `SkillPoints > 0`.

## 6. Journal (mockup 05)

`Segmented` [Quests | Lore]. **Quests** has a list (420 lp) and details (rest).

**List** (from `journal_entries()`), grouped under `SectionHeader`s: **Active** (tracked quest first, with the `pin` icon), **Completed** (check icon, `text.secondary`), **Failed** (`danger.text` caption). Row: `scroll` icon · name · subtitle (current stage text, or "Completed") · trailing progress `2/4` (staged: done stages / stage count; others: `progress/required`). The quest selected by default is the tracked one (or the one the tracker was tapped for).

**Details:**
- `TitleLabel` name and the description in `BodyLabel`.
- **Stages** (staged quests): a vertical checklist. Done stages get a moss check icon and `text.secondary` text. The current stage gets a gold filled circle, `text.primary` text and a gold left bar. Future stages are **not shown** (no spoilers); a caption says "Step 2 of 4". Non-staged quests: a `StatBar`-style progress bar "1 / 2 Mire prowlers defeated" (use the `goal` target through `UiText`).
- **Reward:** item tile (64) + "Restorative draught ×3". If `reward_pending`: an amber callout with a `warning` icon: "Your reward is waiting. Make room in your pack, then speak to the warden."
- **Actions:**
  - **Track** / **Tracking** toggle (`Narrative.track_quest(id)`), shown only for active quests.
  - **Show on map** (secondary), shown when the current stage has a resolvable marker (§7). It switches to the Map tab and centres on the marker.
- Empty state: "No quests yet. The wardens in Reach may need a hand."

**Lore:** `lore_entries()` rendered as:
1. **Discoveries**: rows with the `map` icon and landmark name (via `UiText.LandmarkName`), caption "The Sealed Ruin".
2. **Ruin mechanisms**: one summary row with "2 shortcuts raised · 1 light seal broken" (correct pluralisation, as today).
3. **Notes**: each lore text as a card with a 4 lp gold left bar, `BodyLabel` in `text.secondary`, newest first.

Empty → "Explore the ruin to uncover its history."

The old `journal()` string API stays (used by older tests/evidence). Don't delete it.

## 7. Map (mockup 06)

Layout: the map canvas (rest) plus a sidebar (300 lp; on Touch it can collapse to a 80 lp rail with a toggle).

**Canvas** (`RegionMap`, rewritten, still one `_Draw`):
- Background `surface.sunken`. Terrain: wall/rock `#26302D`, water `#2E5560`, floor `#5B6B42`. The ruin keeps fog of war: draw only `ExploredTiles`, as today. Batch rects per row run (draw horizontal runs of the same terrain as one rect) to cut draw calls ~10×. The map is drawn only while the tab is open, never per frame otherwise.
- Markers drawn as 28 lp circular badges with `UiIcons` textures (`DrawTextureRect`) instead of letters: landmarks by template (`archive`→`book`, `shrine`→`brazier`, `storeroom`→`chest`, `relic sanctuary`→`relic`, `ruined entrance`→`door`); levers, brazier rings and gates per DESIGN_SYSTEM §2 (shape + colour). Only discovered/explored ones, as today.
- **Player:** a gold arrow (triangle 18 lp) rotated by `App.VisualFacing()`, with a 2 lp dark outline.
- **Objective marker:** a gold ring (pulsing 1 Hz, static with reduced motion) + `target` icon at the tracked quest's current-stage marker.
- **Interaction:** zoom levels Fit / 2× / 4× using the +/− IconButtons (80 lp) at the canvas's bottom-right, plus a "Centre on me" IconButton (`target`). When zoomed in, drag pans (screen drag on Touch, left-drag on Desktop; clamp to the region bounds). Pinch zoom is out of scope. Tap a marker → the sidebar shows its name and status ("Lever · pulled").

**Quest markers (data-driven).** Add optional `"marker"` objects to `quests.json` stages:

```json
{"id": "enter_ruin",     ..., "marker": {"region": "reach", "feature": "portal"}},
{"id": "recover_relic",  ..., "marker": {"region": "ruin",  "feature": "relic", "requires_discovery": true}},
{"id": "return_reach",   ..., "marker": {"region": "ruin",  "feature": "exit"}},
{"id": "report_warden",  ..., "marker": {"region": "reach", "profile": "warden"}}
```

`QuestMarkers.Resolve(World, entry) → (string region, Vector2 ground)?`:
- `feature`: the first tile in `region.Features` with that kind (excluding depleted features for `relic`).
- `profile`: the first living actor in that region with `Profile == value`.
- `requires_discovery`: resolve only if the tile is in `ExploredTiles` or inside a discovered landmark.
- If the marker's region ≠ the displayed region, mark the region transition instead (the `portal` in Reach, the `exit` in the ruin) with a dashed gold ring and the caption "Way to The Sealed Ruin".
- Unresolvable → no marker. The sidebar says "Location unknown — explore to find it."

**Sidebar:** `TitleLabel` region name; for the ruin, "Explored {n}%" (explored floor tiles / floor tiles); the tracked objective row (icon + stage text + "Marked" / "Location unknown"); and the **legend**, listing only the marker types currently drawn, each as icon + label.

## 8. Implementation steps

1. Data APIs (§1) with tests in `BookChecks.cs`.
2. Reusable `ItemTile`, `StatBar` (from Part 2), `DetailsPane` and `UiText` tables.
3. Pack ▸ Bag, then Craft, then Trade. Delete the ported Part 1 Pack page.
4. Hero.
5. Journal (Quests, Lore) and the tracker → Journal deep link.
6. Map rewrite, quest markers, Journal → Map deep link.
7. Responsive two-column fallback at < 980 lp.
8. Evidence and handoff.

## 9. Tests (`game/tests/BookChecks.cs`)

Use isolated owners and restore state in `finally`, as `CraftingChecks` does.

1. **Catalog/equipment API:** fields are present, types are correct, `coin` appears in the catalog (the UI filters it, not the service), and `equipment()` reflects `Equip`/`Unequip`.
2. **Unequip:** succeeds with room; fails with a full constrained bag (reuse `LimitStacks` from `CraftingChecks`) and leaves the slot and bag exactly unchanged (semantic JSON compare); publishes `item_unequipped`; the player view's gear refreshes.
3. **Preview purity:** `preview("iron_sword","iron",2)` returns damage/material/quality, and `export_state()` is semantically identical before and after (including `serial`).
4. **Craft page logic** (call page methods, not pixels): with 1 herb, the draught is "Missing 1 Silverleaf" and the button is disabled; with 2 herbs, it's Ready. Pressing Craft in a full one-stack bag that frees a slot succeeds (mirrors `CraftingChecks`). Quest chip appears iff quest 3 is active.
5. **Trade:** the segment is disabled without a nearby merchant. With a merchant and < 5 crowns, the reason reads "Need n more crowns". A purchase moves stock and crowns exactly as `Items.Trade` does today.
6. **Hero:** learning `ward` below level 2 shows "Requires level 2". At level 2 with 1 point it succeeds, removes the point, and makes `ability3` visible on the HUD (mobile). Train shows "Max rank" at `MaxRank`.
7. **Journal:** after accepting quest 2 and entering the ruin, the entry shows stage 1 done and stage 2 current, and future stages are absent. `reward_pending` shows the callout. Track toggles `tracked_quest()`.
8. **Map markers:** in a generated ruin, the relic marker is unresolved until the sanctuary is discovered, then resolves to the relic tile. In Reach during `enter_ruin`, the portal is marked. In the ruin during `report_warden`, the exit is marked as the transition.
9. **Touch sizes and safe area** (mobile): reuse the Part 1 check for every Book tab and segment.
10. **Authority:** after a sequence of Book operations (equip, unequip, craft, buy, learn, train), `Persistence.Capture` round-trips, and the UI holds no item/quest state. Assert that no page caches the catalog across a `Refresh()` (for example, mutate Gloot directly, call `Refresh()`, and see the change).

## 10. Evidence and handoff

- Captures (desktop + `--mobile`): `book-pack.png` (with an item selected and a comparison visible), `book-pack-empty-slot.png`, `book-craft.png` (one Ready, one Missing), `book-trade.png`, `book-hero.png`, `book-hero-skills.png`, `book-journal.png` (Buried Light mid-quest), `book-journal-lore.png`, `book-map-reach.png` (portal marker), `book-map-ruin.png` (explored, with gates/levers/braziers). These replace `inventory.png`, `character.png`, `journal.png`, `mechanism-map.png`, and update `AdventureVisualRun`'s map and journal captures.
- `docs/PR7_PART3_HANDOFF.md`: new APIs, behaviour changes (Use closes the Book; Use-ability buttons removed), quest marker format (also add it to `CONTENT_AUTHORING.md`), and limits.

## Acceptance criteria
- [ ] Every item shows its details. Equipment shows comparisons. Equip/Unequip/Use work through Gloot, and failures explain themselves.
- [ ] Crafting and trading show requirements before commitment, and failures never consume anything (transactional crafting preserved).
- [ ] Hero shows stats, abilities and skills with clear learn/train eligibility.
- [ ] Journal shows ordered stages without spoilers, pending rewards, tracking and lore.
- [ ] The map uses icons and shapes (not letters or colour alone), shows the player's facing and the tracked objective, and supports zoom/pan.
- [ ] All existing adventure, crafting and persistence tests pass unchanged.
