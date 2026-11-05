# PR #7 Part 3 handoff: inventory, equipment, crafting, trading, character, journal and map

Builds on Parts 1–2 (`PR7_PART1_HANDOFF.md`, `PR7_PART2_HANDOFF.md`). Keep PR #7 draft. Part 4 and physical Galaxy S25 acceptance of Parts 1–3 remain outstanding.

## Delivered

All screens use the Part 1 screen host (persistent Back, TouchScroll body, focus, return to caller) and rebuild from production state on every open and after every action. Rebuilding the same screen keeps its scroll position. The only UI-owned state is presentation: the Pack tab, the selected row and the map zoom.

### Pack: Items · Craft · Trade (`game/ui/PackScreen.cs`)

Tabs are a segmented row. The open tab is shown in `[brackets]` with a thick outline, not by colour alone.

**Items.** The tab shows crowns and the stack count, then an **Equipped** card for each slot (weapon, body) with stats and an Unequip action. Below that is the bag list. Tapping a row selects it (`[Selected]`) and opens a detail card underneath with:
- stack size and maximum
- the unique Gloot instance (`Unique item #56`), slot and mass
- material and quality
- damage, defence, power or healing, and condition
- a comparison with whatever the slot holds (`Compared with equipped Iron sword: Damage +3.04`), with signs in words
- the contextual actions Equip and Use

Unavailable reasons are named:
- "Your current form cannot grasp a weapon"
- "Pack full · make room to swap out the equipped item"
- "Pack full · make room to unequip"

Using an item while health is full gives a note rather than a refusal. Materials say "Crafting material · see the Craft tab". There is an empty state, and no drag-and-drop.

**Craft.** Each recipe card shows the output, unique material/quality for generated items, and `have / need / missing` per ingredient, then a status. The status is one of: Ready, `Missing: Bog iron ×2`, cannot make, or a pack-full warning when the output does not currently fit. Craft is disabled until the ingredients are present. After an attempt, the notice distinguishes **missing ingredients**, **pack full (nothing was used)**, **cannot make** and generic failure. These come from the new `Items.LastCraftFailure`, set inside the existing transactional `Craft`. The crafting rollback from the first PR #7 commit is unchanged, so failures never partially grant output or advance quests.

**Trade.** This tab appears only beside a merchant (the same `NearbyMerchant` range as Interact); elsewhere it says where to go. It shows the merchant, your crowns, and per item: price, stock, stats and Buy. Buy is disabled with **out of stock**, **need N crowns · you have M** or **pack full**, from `Items.TradePreview` and `Items.LastTradeFailure`. The price comes from `Items.Price`, which is used by both the screen and the transaction. It is still the existing 5 crowns.

### Character progression (`CharacterOverview`)

- Strength, defence and power show a breakdown (`base · equipment · training · effects`) taken from the same sources `World.Stat` sums.
- Each learnable ability shows mana, range and recovery, its level and point requirements, and Learned/Not learned. Each skill shows its rank, effect per rank and current bonus.
- Learn and Train are disabled with the mirrored `Progression` reason (`Requires level 3 · you are level 2`, `Costs 1 skill point · you have 0`, `Fully trained`).
- Both open a **confirmation modal**, since spending is permanent. Cancel or Back returns to Character unchanged. Confirm runs `Progression.Unlock`/`Train` once and rebuilds Character. The modal is the Part 1 New Game confirmation, generalised as `RpgUi.Confirm`. New Game still uses it, and its Android target names are unchanged.
- The old "Use ability" buttons on the Character screen were removed; the hotbar remains the place to cast.

### Journal (`JournalScreen`, `Narrative.journal_rows()` / `journal_extras()`)

- Quests are grouped as Active, Available from the warden, Completed, Not yet available (naming the prerequisite) and Failed.
- Each card shows title, status and progress, description, then **Done:** and **Now:** stage history. Later stages are counted but not revealed. Each card also shows its reward: waiting, received, or the amount, using item names.
- Discoveries, ruin mechanism counts and lore follow in their own cards.
- The adapters are read-only (a test checks `export_state` before and after), and `journal()` is kept for compatibility.

### Map (`MapScreen`, `RegionMap`)

- "Whole map" and "Around you" (a 28×18-tile window centred on the player) are selectable tabs.
- The map draws only what `RegionState` knows: explored ruin tiles, discovered landmarks and explored mechanisms. A "You" label, outlined letters and a legend explain every marker by shape, letter and words.
- A discovered-rooms list (with letters) and an approximate explored percentage appear for the ruin.
- No minimap or regeneration was added.

### New item-service boundary (all in `ItemService.gd` / `Items.cs`)

- `inventory_view` (read-only rows for bag and slots)
- `equipped_selector`, `can_unequip` and `prototype_title` (read-only)
- `unequip` (bag must accept the item, else unchanged; publishes `item_unequipped`, and Foundation refreshes equipment appearance)
- `CraftPreview`, `TradePreview`, `LastCraftFailure`, `LastTradeFailure`, `Price` and `HasSpace`

There is no new item store and no save-format change.

## Verification

- `RpgScreensChecks.Run` (headless) checks:
  - the inventory view against the Gloot catalog
  - generated instance identity and properties
  - equip, unequip and the event, plus a full pack that keeps the item equipped
  - missing ingredients versus pack full, with exact content rollback
  - trade funds, stock and success
  - journal statuses, prerequisites, stage history and save/load, with no mutation
  - discoveries and lore
  - learn/train blockers that mirror `Progression`
  - the stat breakdown
- `RpgScreensChecks.Rendered` runs in both full rendered suites, or alone with `--visual-check --screens-only`. It drives the real screens through the loop below, using real touch on the phone layout:
  1. Chest loot.
  2. Craft a sword from the Craft tab; the draught recipe is disabled when ingredients are missing.
  3. Select the generated instance, inspect and compare it, then Equip.
  4. Use a draught; the Pack stays open.
  5. Save, Unequip, then Load; equipment is restored.
  6. Buy is disabled without crowns, then a purchase succeeds. Trade disappears away from the merchant.
  7. Train with Cancel (nothing changes), then Train with Confirm (exactly one rank).
  8. Journal cards.
  9. Map in whole and zoomed views; the ruin shows explored state only.
  10. Large text at 900×560: every button and card fits beside the scrollbar on Pack Items, Craft, Journal, Character and Map.
- `UiNavigationChecks` now selects the draught row before pressing `use`. Its navigation assertions are unchanged.
- Screenshots: `docs/native-screenshots/{,mobile-}pack-{items,detail,craft,trade}.png`, `character-{confirm,progression}.png`, `journal.png`, `map.png`, `map-ruin.png` and `pack-items-large-900x560.png`.

## Known limitations

- Pack-full before crafting is a warning, not a block. Gloot capacity can only be known for certain after ingredients are taken, so the authoritative transaction decides and reports. In production bags have no capacity limit; tests impose one with a Gloot constraint.
- There is no selling, sorting or filtering. The merchant price is the existing flat 5 crowns.
- The map has no pan gesture; "Around you" is the zoom. Fine map readability needs the phone.
- Ability descriptions are numeric (mana, range, recovery); there is no flavour text in content yet.
- Part 4 still owns the dialogue presentation, player-facing settings and preference persistence, the developer/player menu split, and the full accessibility audit.

## Phone checklist (Galaxy S25)

1. Open the chest, then Pack: the new items are listed. Select a sword, read its comparison, Equip it, then Unequip it from the Equipped card.
2. Craft a draught on the Craft tab and check that the notice and counts change. Try a recipe with missing ingredients and read the reason.
3. Stand by the warden: Trade lists stock and prices. Buy with too few crowns (disabled, with a reason), then with enough.
4. Character: training asks for confirmation. Cancel, then Confirm, and check that one point is spent.
5. Journal: read the Active, Available and Not yet available cards. After entering the ruin, check the Done/Now history.
6. Map in the ruin: switch between Whole map and Around you, and check the legend and that unexplored areas stay dark.
7. Swipe-scroll long lists without triggering rows. Use Back from every screen to return to its caller. Repeat with Large text.
