# Part 2 — Gameplay HUD and mobile controls

**Goal:** a combat-readable HUD with graphic vitals, icon action buttons with cooldown sweeps, a context-sensitive Interact button, a quest tracker and a non-destructive notification system. The input ownership model from PR #4 Part 4 stays exactly as it is.

> **Superseded in part by [CREATIVE_DIRECTION.md](CREATIVE_DIRECTION.md):** the §1 layout table becomes **C3 (HUD v3: minimap top-right, tracker under the vitals, tall/short tiers, `LayoutVersion = 3`)**. The action IDs `ember`/`mend`/`ability3` become **`skill1`/`skill2`/`skill3`** bound to the player's action bar (C5; legacy aliases kept in `ActionControl`). Everything else in this guide (button states, interact preview, toasts, vitals, desktop hotbar) still applies to those slots. This part also owns **C4** (combat text, nameplates), **C10** (quest ceremony, loot flight) and **C14** (ambience). Run `python3 docs/ui/mockups/check_geometry.py` after any geometry change.

Prerequisite: Part 1 (tokens, components, icons, `UiFrame`, `ScreenHost`). Mockups: [`01-hud-mobile.svg`](mockups/01-hud-mobile.svg), [`10-hud-desktop.svg`](mockups/10-hud-desktop.svg).

## Scope

**In:** `Hud/` components; mobile control geometry v2; ability/dodge/potion button visuals and states; Interact preview; quest tracker (plus the `Narrative.journal_entries()` data API it needs); toast lane, region title card and level-up banner; low-health vignette; desktop hotbar; optional enemy health bars and target highlight; tests and evidence.

**Out:** Book content (3); dialogue, settings, defeat flow and haptics/HUD opacity settings UI (4). Implement the hooks for HUD opacity and left-handed mode here; Part 4 adds their settings controls.

## 1. HUD layout (Touch)

All positions are relative to `UiFrame.Safe` (origin `T` = top-left, `R`/`B` = right/bottom edges) and multiplied by the compact factor `s = min(1, safe.W/900, safe.H/560)`. That is the same factor `MobileCombatControls.Geometry` already uses.

| Element | Rect (lp, before × s) | Notes |
|---|---|---|
| Vitals | `T + (24, 20)`, 432 × 110 | not interactive; `StatusRect` returns this rect |
| Toast lane | between the vitals' right edge + 16 and the tracker's left edge − 16, top `T.y + 20`; width `min(440, span)` | if `span < 360`, stack toasts under the vitals at the left instead (`x = T.x+24`, `y = vitals.bottom + 12`, width 432) |
| Book button (`inventory`) | `(R-196, T.y+20)`, 80 × 80 | IconButton `bag` + caption "Pack"; gold badge dot if `SkillPoints > 0` |
| Menu button (`menu`) | `(R-104, T.y+20)`, 80 × 80 | IconButton `menu` + caption "Menu" |
| Quest tracker (`quest`) — full | `(R-384, T.y+116)`, 360 × 112 | only if `safe.H ≥ 700` |
| Quest tracker — compact | `(R-288, T.y+20)`, 80 × 80 | IconButton `scroll` with a progress badge ("1/2") |
| Stick | `(T.x+40, B-260)`, 200 × 200 | unchanged |
| Action cluster | see §2 | |
| Region title card | centred horizontally, top `T.y + 150` | non-interactive |

These rects were checked for **≥ 12 lp gaps and safe-area containment** at 1280×800, 1280×720, 1920×864, 1024×768, 1728×800 (S25 with `expand`), 900×560 and 800×480, with both the 50/24 verification inset and no inset. Keep that table-driven check as a unit test (§9).

### Left-handed mode (hook now; setting in Part 4)
`UiDensity.LeftHanded` mirrors the stick and the action cluster horizontally: `x' = safe.X + (safe.End.X - (x + w))`. Vitals, utilities and tracker do **not** mirror. Mirroring happens inside `MobileCombatControls.Geometry(safe, leftHanded)` (add an overload; keep the 1-arg version calling it with `false`) so that tests can cover both.

## 2. Action cluster geometry v2 (decision D3)

`MobileCombatControls.LayoutVersion = 2`. `Geometry(safe)` keeps its `At(right, bottom, width, height)` convention:

| id | At(right, bottom, w, h) | Visual | Content |
|---|---|---|---|
| `attack` | (172, 172, 136, 136) | circle, fill `#795433`, 3 lp `line.bright` | `sword` icon 60 lp, no text |
| `dodge` | (330, 150, 108, 108) | circle, fill `#355B56` | `dash` icon 44 + `GlyphLabel` "Dodge" |
| `ember` | (300, 282, 92, 92) | circle, fill `surface.2` | `flame` 40 + "Ember" + cost chip "9" (lumen) |
| `mend` | (156, 330, 92, 92) | circle, fill `surface.2` | `leaf-heart` 40 + "Mend" + cost chip "12" |
| `potion` | (446, 214, 88, 88) | circle, fill `surface.2` | `flask` 40 + count chip "×3" |
| `ability3` *(new)* | (260, 430, 88, 88) | circle, fill `surface.2` | first learned ability not in {slash, ember, mend} (today: `ward` "Bark ward"); **hidden until one is learned** |
| `interact` | (480, 384, 184, 80) | pill (`radius.round`) | icon 36 + verb (`LabelStrong` 22), see §4 |
| `inventory`, `menu` | see §1 | rounded square | |
| `quest` *(new)* | see §1 | card / icon | §5 |
| `stick` | (unchanged) | | |

Thumb fan: the inner ring (dodge, ember, mend) is 170–200 lp from the Attack centre, and the outer ring (potion, ability3, interact) is 300–350 lp.

Rules carried forward from v1 (all are tested today; keep them):
- The registered `Button` **is** the hitbox. Visuals (circle, icon, labels, sweep) are drawn inside it by an `ActionButton` control set to `MouseFilter = Ignore`. Text changes never resize the button.
- `RpgUi._Input` touch dispatch, emulated-mouse suppression and cancellation are unchanged. They iterate `hotbar.GetChildren().OfType<Button>()`, so register the new `ability3` and `quest` buttons in `MobileCombatControls` like the others. A hidden `ability3` is skipped automatically because `IsVisibleInTree()` is false.
- `MOSSLIGHT_CONTROL version=2 id=… x=… y=…` is printed for every action including `ability3` (only when visible), `quest` and `stick`. **Update `tools/android-smoke.py`**: the regex `version=1` becomes `version=\d+`.

## 3. Action button states

`ActionButton` (one class for every circular action) draws, from bottom to top: the plate, the icon, the label, the cost/count chip, the cooldown sweep and the seconds text, and the ready ring.

| State | Trigger (read live in `RpgUi.Update()`) | Visual |
|---|---|---|
| Ready | `cooldown == 0` and affordable and alive | full opacity; brief ready ring (scale 1→1.15, alpha 1→0, 250 ms) on the frame cooldown reaches 0 |
| Cooling | `a.Cooldowns[id] > 0` | dark sweep wedge (`#000` @55%) covering `remaining/total`, drawn clockwise from 12 o'clock and shrinking; `HudNumber` 26 in the centre: `ceil(s)` for ≥ 1 s, one decimal below 1 s. Totals: `AbilityDef.Cooldown`; dodge uses `CombatTuning.DodgeCooldown` (0.70 s; reference the constant, don't copy it) |
| Unaffordable | `a.Mana < def.Cost` | icon `SelfModulate` 50% grey; cost chip turns `danger.fill` with white text |
| Pressed | owned touch | plate darkened 20% (replaces today's `Modulate` 0.7/0.8 tint) |
| Dead | `!a.Alive` | whole cluster at 40% opacity |
| Potion empty | `Items.Count("player","potion") == 0` | icon dim, chip "×0" in `danger.text`. Tapping toasts "No draughts left — craft one from 2 Silverleaf" |

`Disabled` keeps today's meaning (cooldown, unaffordable or dead), so a cooling ability can't be spam-queued. **Do not** change which conditions disable a button.

## 4. Context-sensitive Interact

### Data: an interaction preview that cannot drift from the dispatch
Add a pure query in `World`, next to `Interact` (game/world/World.cs ~line 750):

```csharp
public readonly record struct InteractionPreview(string Kind, string Verb, bool Actionable, string Hint, int Feature = -1, string ActorId = "");
public InteractionPreview PreviewInteraction(ActorState a);
```

It must walk **the same order and skip rules** as `Interact`. Read that method line by line:
1. Nearby features in `InteractionTargets(a)` order:
   - `portcullis`/`seal` → remember the hint, continue.
   - `lever` → actionable only if `DungeonMechanisms.Pull` *would* raise gates. Add a pure `DungeonMechanisms.CanPull(r, at)` and make `Pull` call it.
   - `brazier` → actionable only if `Kindle` would succeed. Add a pure `CanKindle(r, at)` the same way.
   - `lock` → skip if open; if the player has no `dungeon_key`, remember "key required" and continue; else actionable.
   - `portal`/`exit`, `chest`, `herb|ore|wood|relic|key` → actionable. (They may still fail at dispatch with "Inventory full". That is fine: the preview says what will be *attempted*.)
2. Then `LootTargets(a).Any()` → `Loot`.
3. Otherwise not actionable, with the hint "A ruin key is required", the seal/portcullis hint, or none.

Then `Foundation.PreviewInteraction()` adds the merchant rule from `Foundation.Interact`: `NearbyMerchant() != null && !InteractionTargets.Any() && !LootTargets.Any()` → `Talk`. Make `HasInteraction()` return `PreviewInteraction().Actionable`, so the Interact emphasis, the button label and the dispatch share one source.

| Kind | Verb (label) | Icon |
|---|---|---|
| talk | Talk | `chat` |
| chest | Open | `chest` |
| herb / ore / wood | Gather | `gather` |
| relic | Take relic | `relic` |
| key | Take key | `key` |
| lock (has key) | Unlock | `key` |
| lever | Pull | `lever` |
| brazier | Kindle | `brazier` |
| portal | Enter ruin | `portal` |
| exit | To Reach | `door` |
| loot | Loot | `skull` |
| *(not actionable, key required)* | Locked | `lock` (amber) |
| *(not actionable, seal/portcullis hint)* | Inspect | `eye` (amber) |
| *(nothing)* | Interact | `hand` (dim) |

### Presentation
- Poll at the existing 10 Hz cadence (`World.Tick % 6`). Keep the 350 ms emphasis hold. When the verb changes, cross-fade the label over 120 ms.
- Actionable: plate `surface.2` with a 3 lp gold border, and the icon/label in `text.primary`. Not actionable with a hint: amber icon and `text.secondary`. Nothing: 60% opacity.
- Tapping always dispatches `App.Interact()` as today. The resulting `World.InteractionFeedback` becomes a toast (§6).
- **Target highlight (should-have):** a small gold chevron (12×8 lp, bobbing 2 lp at 1 Hz; static with reduced motion) above the previewed feature tile or actor. Draw it from one `Node2D` overlay modelled on `PlayerLocator` (ZIndex 50, presentation only).

## 5. Quest tracker

### Data API (also used by Part 3's Journal)
Add to `Narrative.gd`, keeping `journal()` for compatibility:

```gdscript
func journal_entries() -> String   # JSON array, one object per quest the player knows about
# {
#   "id": 2, "name": "The Buried Light", "description": "...",
#   "status": "active" | "completed" | "failed",          # "available" quests are omitted
#   "tracked": true,
#   "stages": [{"id":"enter_ruin","text":"...","state":"done"|"current"}],   # [] for non-staged quests; future stages omitted
#   "objective": "Find keys, open sealed passages and recover the sanctuary relic.",
#   "progress": 1, "required": 2,                          # non-staged only; staged quests report the stage index/count
#   "reward": {"item": "potion", "count": 3},
#   "reward_pending": false
# }
func track_quest(id: int) -> bool  # sets flags["tracked_quest"]; returns false if the quest isn't active
func tracked_quest() -> int        # flags value if still active; else the highest-id active quest; else 0
```

`tracked_quest` lives in the existing `flags` dictionary, so it is saved and restored with no schema change. This is the one piece of player preference allowed in game saves, because it belongs to the journal. On quest completion, `tracked_quest()` falls through to the next active quest automatically.

### Presentation (full mode, 360 × 112)
```
┌──────────────────────────────────────┐
│ ◈ The Buried Light               2/4 │  LabelStrong 22 (ellipsis) · HudNumber 22 gold
│ Find keys, open sealed passages and  │  CaptionLabel 22, max 2 lines, ellipsis
│ recover the sanctuary relic.         │
└──────────────────────────────────────┘
```
- Plate `hud.plate`, 2 lp `line.frame`, `radius.md`. The scroll icon is gold.
- Tap → `OpenBook(Journal)` with the tracked quest selected.
- On a stage change: the card flashes a gold border (1 pulse, 400 ms) and the objective text cross-fades.
- **Combat dimming:** while any hostile within 160 ground units is alive, fade the tracker to 40% opacity over 200 ms. It stays tappable. Restore it when combat ends.
- No active quest → hide the tracker (`Visible = false`; its hitbox goes with it).
- Compact mode (80 × 80): `scroll` icon + badge "2/4". Tap → Journal.
- Re-query `journal_entries()` only on Narrative change signals (quest updated, notice) and on `Update()` at most every 30 ticks. Never parse JSON every frame.

## 6. Notifications

### Toast lane
- `ToastLane` holds at most **3** visible toasts. New toasts enter at the top and older ones move down. Overflow drops the oldest instantly.
- Each toast: 40 lp icon, title (`LabelStrong`), optional caption, `surface.1` @92%, `elev.2`, `radius.md`, with a left accent bar (4 lp) in the category colour.
- **Merge:** a toast with the same `(category, key)` arriving while one is visible updates it in place (count "×6", hold timer reset, 120 ms scale pop).
- Hold time: `toast.hold` (DESIGN_SYSTEM §8). Toasts never take input (`MouseFilter = Ignore`), so they can never steal a touch.

| Category | Accent | Icon | Sources |
|---|---|---|---|
| `loot` | gold | item icon | `World.Events` `item_acquired` for actor `player` → "Silverleaf ×3" (key = item id) |
| `quest` | gold | `scroll` | Narrative typed notices `quest_stage` ("New objective" + stage text), `quest_complete`, `reward`, `reward_pending` |
| `discovery` | lumen | `map` | Narrative `discovery` → also triggers the title card ("Forgotten Archive", `TitleLabel`) |
| `level` | gold | `star` | `level_gained` → banner "Level 3" + toast "+1 skill point · open Hero to spend it" |
| `info` | `line.bright` | `info` | `Notify(text)` default; `World.InteractionFeedback` after Interact |
| `warning` | amber | `warning` | feedback containing "full", "required", "Insufficient"; failed casts |

**Typed notices:** add `signal notice_typed(kind: String, title: String, detail: String)` to `Narrative.gd`. Emit it alongside the existing `notice(text)` at each current emission site (stage advance, quest complete, reward received, reward pending, discovered). Foundation connects `notice_typed` to the HUD. Keep `notice` → `ui.Notify` connected for anything untyped. Avoid duplicates: when a typed notice arrives, drop an identical untyped text that arrives in the same frame.

**`Notify(string)` stays public** (tests and dev tools call it). It creates an `info` toast and stores `LastNotice` (new public getter) for tests. **Spam control:** `Foundation.Cast` currently calls `Notify(d.Name)` on every successful cast. Change success to no toast (the button press is the feedback) and keep failures as `warning` toasts. Identical texts within 1 s merge.

### Title cards and banners
- **Region title card** on `region_entered` (player): `DisplayLabel` region name ("The Reach", "The Sealed Ruin" from a `UiText.RegionName` table) with a `CaptionLabel` subtitle ("Ruin depth 1" or the time of day). Timing: fade in `motion.slow`, hold 1.6 s, fade out `motion.slow`. Non-interactive. Suppressed during the first second after a load.
- **Level-up banner**: same slot, `DisplayLabel` "Level 3" in gold with a `star` icon, plus the `level` toast.
- Only one card at a time; a newer one replaces the older.

## 7. Vitals panel

```
 ╭────╮  ███████████████████░░░░░  84/100      health 240×26, value HudNumber 24
 │ 3  │  ████████████░░░░░░░░░░░░  42/60       mana 240×18, value HudNumber 22 lumen
 ╰────╯  [◆ Bark ward 6s] [☠ Poison 3s]        status chips (Chip, 28 tall), max 4 + "+n"
 The Reach · 14:32                             CaptionLabel 22, text.secondary
```
- **Level medallion** 76 × 76: `surface.2` circle, `HudNumber` 28 level, with an XP ring (6 lp stroke, `accent.gold` arc over a `surface.sunken` track) showing `Experience / (Level*100)`. A gold badge dot at the top-right while `SkillPoints > 0`.
- **StatBar** (DESIGN_SYSTEM §6). The ghost segment shows recent loss: it waits 400 ms, then drains over 350 ms. Healing animates the fill up over 200 ms without a ghost.
- **Low health** (≤ 25%): the bar border pulses `danger.fill` at 1 Hz, and a screen-edge vignette (radial `GradientTexture2D`, transparent centre → `danger.fill` @28% at the edges) fades in over 300 ms on a full-rect `TextureRect` beneath all HUD controls (`MouseFilter = Ignore`). With reduced motion: static border and vignette. Never flash.
- Status chips come from `ActorState.Statuses` (`Id`, `Remaining`, `Stacks`). Statuses have no display names in content, so add a `UiText.StatusName(id)` table (`burn` → "Burning", `stagger` → "Staggered", the ward buff → "Bark ward"; fallback: title-cased id). Show stacks as "×2".
- The region and clock caption replace today's `health` label text. Keep the existing format `HH:MM`.

## 8. Desktop HUD (`MobileLayout == false`)

- Vitals, toasts, tracker and title cards are as on Touch (Desktop type ramp).
- **Hotbar** (mockup 10): centred at the bottom, 24 lp above `safe.End.Y`. Slots are 64 × 64 with gap 8: `[1 Sword] [2 Ember] [3 Mend] [4 Draught] [5 Ability3 — if learned] [Space Dodge] [E Interact]`. Each slot is an `ActionButton` with a keycap chip (`GlyphLabel`) at the top-left. Interact is 160 wide with the verb label. Add `dodge` to the desktop actions; it currently skips it. Keep the action IDs.
- **Utilities** top-right: 48 × 48 IconButtons `Pack (I)`, `Hero (C)`, `Journal (J)`, `Map (M)`, `Menu (Esc)` with tooltips "Pack · I". The IDs `inventory, character, journal, map, menu` stay.
- Hover shows a tooltip panel for abilities: name, cost, cooldown, range (the current `TooltipText` content, styled).
- Bind key `5` to `ability3` in `Foundation`'s bindings table.

## 9. Implementation steps

1. `Narrative.journal_entries/track_quest/tracked_quest` and `notice_typed` (GDScript), plus headless tests in a new `HudChecks.cs`.
2. `World.PreviewInteraction`, `DungeonMechanisms.CanPull/CanKindle` (with `Pull`/`Kindle` refactored to use them), `Foundation.PreviewInteraction`, and `HasInteraction` → preview. Add parity tests (below).
3. `MobileCombatControls` v2: geometry table, `LayoutVersion = 2`, `Geometry(safe, leftHanded)`, `ability3`/`quest` registration. Update `android-smoke.py` regexes.
4. `ActionButton` drawing and states; replace text-based labels in `RpgUi.Update()` with state setters. Keep setting `Button.Text` for accessibility/tests, but render it invisibly (`AddThemeColorOverride("font_color", transparent)`) so the hitbox and the `attack.Text` resize test are unchanged.
5. `VitalsPanel` (`StatusRect` → its global rect), low-health vignette, `QuestTracker`, `ToastLane`, `Banner`. Wire `World.Events.Published` and `Narrative.notice_typed` in `RpgUi` (unsubscribe in `_ExitTree`).
6. Desktop hotbar and utilities.
7. Optional: target highlight overlay and enemy health bars (36 × 5 lp above hostiles damaged in the last 5 s, max 8, presentation-only `Node2D`).
8. Creative layer: C5 data (`ActorState.ActionBar`, `World.SetActionBar`, auto-placement on unlock) and `skill1..3` HUD binding; C3 minimap + tier switch; C4 `CombatTextLayer` + nameplates; C10 quest card, loot flight, new-item dots; C14 if time allows.
9. Evidence and handoff.

## 10. Tests (`game/tests/HudChecks.cs`, registered in `FoundationChecks`)

1. **Geometry:** for both `leftHanded` values and every (size, inset) pair in §1, every action rect lies inside the safe rect, all pairs have gaps ≥ 12·s, and the stick doesn't intersect any action. Extend `MobileCombatControlsChecks.Rendered` to include the new IDs with the real rendered controls.
2. **Preview parity:** in a sandbox (`Persistence.Capture`/`Restore`, as `CraftingChecks` does), place the player next to each feature kind and each combination (lock without a key plus a chest behind it; portcullis plus a lever; depleted lever; brazier ring; corpse with loot; merchant alone; merchant plus chest). Assert that `PreviewInteraction().Kind` predicts the event or feedback that `Foundation.Interact()` then produces. For non-actionable previews, assert the feedback equals the `Hint`.
3. **Cooldown visuals:** after a real `Cast("ember")`, `ActionButton("ember").SweepFraction` is in (0, 1] and decreasing over ticks, and `Disabled` is unchanged from the v1 rules.
4. **Toasts:** two `item_acquired` events for the same item within the hold merge into one toast with the summed count. A 4th toast evicts the oldest. Toasts have `MouseFilter == Ignore`. `Notify` sets `LastNotice`. A successful cast creates no toast.
5. **Tracker:** with no active quest it is hidden. After accepting quest 1 it shows "Trouble in the Reach" and "0/2". `track_quest(3)` switches it. The tracked id survives `Persistence.Capture/Restore`.
6. **StatusRect:** inside the safe area, not intersecting `menu`/`inventory`/`quest` (extends the existing check).
7. **Verification determinism:** title cards and toasts render at full opacity immediately in verification mode, so captures are stable.
8. **C3/C5/C4/C10:** the 160-layout geometry table; a legacy save without `ActionBar` loads with the default bar; unlocking `ward` fills `skill3` and the HUD shows it; `SetActionBar` rejects unknown abilities and `slash`; the minimap texture rebuilds only on region or explored-count change (count rebuilds across 120 ticks of idle play = 0); combat text appears for hit, miss and dodge observations, with at most 24 live entries; the quest card appears on completion and its fly-to-pack ends at the Book button's rect.

Keep the existing input-pipeline checks (`MobileCombatControlsChecks.InputPipeline`) and the Android smoke attack/dodge/interact assertions passing.

## 11. Evidence and handoff
- Captures (desktop + `--mobile`): `hud.png`, `hud-combat-cooldown.png` (after Ember), `hud-low-health.png`, `hud-interact-chest.png`, `hud-interact-locked.png` (in the ruin, at a gate without a key), `hud-toasts.png` (after looting a chest), `hud-level-up.png`. Add them to `VisualChecks`, plus the mobile equivalents.
- `docs/PR7_PART2_HANDOFF.md`: the geometry v2 table, changed log formats (`version=2`, new ids), parity-test coverage, and anything unverified on a phone (thumb reach of the outer ring, readability of the 18 lp glyph labels).

## Acceptance criteria
- [ ] Health, mana and XP are graphic. Health is legible at arm's length on the S25 (phone check in Part 4).
- [ ] Every ability shows cooldown progress and the reason it can't be used, with no change to eligibility rules.
- [ ] Interact always shows what will happen, and the parity test proves it matches the dispatch.
- [ ] The quest tracker shows the current stage and opens the Journal.
- [ ] Loot/quest/level messages are no longer lost: up to 3 toasts with merging.
- [ ] All v1 input-ownership tests pass. The Android smoke passes with `version=2` logs.
