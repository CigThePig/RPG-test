# Mosslight UI design system — "Lantern & Moss"

This is the single reference for visual tokens and components. All values live in code in `game/ui/Foundation/UiTokens.cs` (Part 1). **Never hard-code a colour, size or duration in a screen.** Reference a token. If a token is missing, add it here and in `UiTokens` in the same commit.

## 1. Units and density

All sizes are **logical px (lp)** on the 1280×800 base canvas (`canvas_items` stretch).

On the Galaxy S25 (2340×1080 px, ~416 ppi, landscape), one lp is drawn at 1080/800 = **1.35 physical px ≈ 0.082 mm**. That holds for both `keep` and `expand` aspect, because height sets the scale. Useful conversions:

| lp | mm on S25 | Android equivalent |
|---|---|---|
| 22 | 1.8 | ≈ 11 sp text |
| 26 | 2.1 | ≈ 13 sp text |
| 28 | 2.3 | ≈ 14 sp text |
| 80 | 6.6 | ≈ 42 dp target |
| 88 | 7.2 | ≈ 46 dp target |
| 96 | 7.9 | ≈ 50 dp target |
| 132 | 10.9 | primary combat button |

There are two **density profiles**, chosen once at startup from `RpgUi.MobileLayout`:

| Token | Touch (mobile / `--mobile-ui`) | Desktop |
|---|---|---|
| `target.min` (any tappable) | 80 | 40 |
| `target.row` (list rows, menu entries) | 88 | 48 |
| `target.primary` (primary buttons in screens) | 96 | 52 |
| `gap.target` (between adjacent targets) | 12 | 8 |
| type ramp | Touch column below | Desktop column below |

User settings (Part 4) multiply the type ramp by **text scale** ∈ {1.00, 1.15, 1.30} and Touch targets by **button scale** ∈ {1.00, 1.15}. Layouts must survive the largest scale: text wraps or ellipsizes and never overflows its container.

## 2. Colour tokens

Contrast is measured against `surface.1` unless noted. Required: text ≥ 4.5:1, large text/icons ≥ 3:1.

### Surfaces and lines
| Token | Hex | Alpha | Use |
|---|---|---|---|
| `scrim` | `#050A0A` | 0.55 | Behind modal screens (world stays faintly visible) |
| `surface.1` | `#101A1A` | 0.96 | Screen panels, dialogue sheet (matches the current panel colour) |
| `surface.2` | `#182726` | 1.0 | Cards, list rows, item tiles |
| `surface.3` | `#22352F` | 1.0 | Hover / pressed / selected backgrounds |
| `surface.sunken` | `#0A1111` | 1.0 | Bar tracks, input wells, map background |
| `hud.plate` | `#0B1414` | 0.72 | Back-plates behind HUD text and buttons (opacity follows the HUD opacity setting) |
| `line.subtle` | `#2A3B38` | 1.0 | Dividers, row separators |
| `line.frame` | `#7A8557` | 1.0 | Panel frames (current border colour, 4.5:1) |
| `line.bright` | `#B0AF7E` | 1.0 | Button borders, focus-adjacent frames (7.8:1) |

### Text
| Token | Hex | Contrast on s1 / s2 / s3 | Use |
|---|---|---|---|
| `text.primary` | `#E4EBD0` | 14.4 / 12.6 / 10.6 | Body, titles |
| `text.secondary` | `#AFBB9E` | 8.8 / 7.7 / 6.4 | Subtitles, captions, units |
| `text.disabled` | `#6B7766` | 3.8 / 3.3 / 2.8 | Disabled labels only (never the sole carrier of meaning) |
| `text.onAccent` | `#14201C` | 9.1 on gold | Text on gold/ember fills |

### Accents and semantic colours
| Token | Hex | On s1 | Meaning |
|---|---|---|---|
| `accent.gold` | `#D9BC6E` | 9.6 | Primary CTA, currency, XP, **focus ring**, selection, tracked quest |
| `accent.moss` | `#8DB65A` | 7.6 | Success, positive stat delta, "ready", quest complete |
| `accent.ember` | `#E4793A` | 6.0 | Sword/attack, damage dealt |
| `accent.lumen` | `#9FE3E6` | 12.3 | Mana, magic, info, light seals |
| `accent.amber` | `#E9A23B` | 8.2 | Warning, braziers |
| `danger.text` | `#F07A6E` | ≈6 | Errors, negative deltas, "missing" counts |
| `danger.fill` | `#C9443A` | — | Health bar fill, destructive buttons (white text 4.8:1) |

### Bars
| Bar | Track | Fill | Ghost (recent loss) | Label |
|---|---|---|---|---|
| Health | `#2A1312` | `#C9443A` → `#E0574C` top highlight | `#F0C9A0` | `text.primary` with 3 px outline `#000` @70% |
| Mana | `#0F2430` | `#3C9BC0` | `#BFEFF2` | same |
| XP | `#2B2617` | `accent.gold` | — | caption only |
| Cooldown sweep | — | `#000000` @ 55% | — | seconds in `text.primary` |

### Item quality tiers (Gloot `quality` property; missing = 0)
| Tier | Name | Frame colour | Extra signal (not colour-only) |
|---|---|---|---|
| 0 | Common | `line.subtle` | — |
| 1 | Fine | `accent.moss` | 1 pip |
| 2 | Superior | `accent.lumen` | 2 pips |
| 3+ | Masterwork | `accent.gold` | 3 pips |

### Map / mechanism colours (keep the existing hues; add shapes)
| Feature | Colour | Shape/icon (required) |
|---|---|---|
| Locked gate | `#D4B66B` | key glyph on the bar |
| Portcullis | `#A7B0B2` | vertical hatch |
| Light seal | `#9FE3E6` | diamond |
| Opened gate | same @ 35% | outline only |
| Lever | `#D4B66B` / spent `#A7B0B2` | lever icon |
| Brazier ring | `#F2A443` / solved `#9FE3E6` | flame icon |
| Entrance / exit | `#8FD18F` | door icon |
| Player | `accent.gold` | arrow pointing along facing |
| Objective (tracked) | `accent.gold` | pulsing ring + quest icon |

## 3. Typography

Fonts (decision D2): **Atkinson Hyperlegible** Regular/Bold for UI and body text, **Alegreya SC** Bold for display/headings. Vendor them in `game/ui/fonts/` with `OFL.txt`, and add entries to `THIRD_PARTY_NOTICES.md` and `docs/UPSTREAM_DEPENDENCIES.md`. Fallback: Godot's default theme font with the same sizes.

Font import settings: leave `antialiasing` at grayscale and `multichannel_signed_distance_field` off. `canvas_items` stretch rasterises fonts at the scaled size, so text stays sharp. HUD numerals use a `FontVariation` with `outline_size = 3` and outline colour `#000000B3`.

| Style (theme type variation) | Font | Touch | Desktop | Line height | Use |
|---|---|---|---|---|---|
| `DisplayLabel` | Alegreya SC Bold | 44 | 34 | 1.1 | Region title card, "You have fallen" |
| `TitleLabel` | Alegreya SC Bold | 32 | 26 | 1.15 | Screen titles, Book tab header |
| `HeadingLabel` | Atkinson Bold | 28 | 22 | 1.2 | Section headings, item name in details |
| `BodyLabel` (default) | Atkinson Regular | 26 | 20 | 1.35 | Body text, dialogue text, descriptions |
| `LabelStrong` | Atkinson Bold | 26 | 20 | 1.35 | Row titles, button text |
| `CaptionLabel` | Atkinson Regular | 22 | 16 | 1.3 | Secondary lines, units, hints |
| `HudNumber` | Atkinson Bold + outline | 24 | 18 | 1.0 | Bar values, counts on tiles |
| `GlyphLabel` | Atkinson Bold | 18 | 14 | 1.0 | Labels under action icons, key-cap hints, badges |

Rules:
- Minimum text size on Touch is **22 lp**. The single exception is `GlyphLabel` (18 lp) inside an action button or badge, where an icon carries the meaning.
- Sentence case everywhere except `DisplayLabel`/`TitleLabel`, which are small-caps via the font. No ALL-CAPS strings in code. Replace the current "PACK & EQUIPMENT"-style headings.
- Numbers use tabular figures where the font supports them (`opentype_features = {"tnum": 1}`), so bars don't jitter.
- Truncate with an ellipsis (`TextOverrunBehavior = TrimEllipsis`) in tiles, rows and the tracker. Wrap with `AutowrapMode.WordSmart` in descriptions and dialogue.

## 4. Spacing, radii, elevation

- Spacing scale (4-lp grid): `space.1=4, space.2=8, space.3=12, space.4=16, space.5=24, space.6=32, space.7=48`.
- Screen padding: Touch 24 / Desktop 20. Section gap: `space.5`. Row inner padding: 16 horizontal, 12 vertical.
- Radii: `radius.sm=6` (chips, badges), `radius.md=12` (buttons, tiles, rows), `radius.lg=18` (panels, sheets), `radius.round=999` (circular action buttons, emulated with a radius ≥ half the size).
- Borders: panels 2 lp `line.frame`; buttons 2 lp `line.bright`; selected/focused 3 lp `accent.gold`.
- Elevation (StyleBoxFlat `shadow_size`/`shadow_color`): `elev.1` 4 lp `#000` @35% (tiles), `elev.2` 10 lp @45% (panels, toasts), `elev.3` 18 lp @55% (dialogs, dialogue sheet). Shadows are cosmetic. Set them to 0 when the "High contrast" setting is on (Part 4) and draw solid plates instead.

## 5. Stylebox recipes

```csharp
// Panel / sheet
new StyleBoxFlat { BgColor = UiTokens.Surface1, BorderColor = UiTokens.LineFrame,
  BorderWidthAll = 2, CornerRadiusAll = 18, ShadowSize = 10, ShadowColor = new Color(0,0,0,.45f),
  ContentMarginAll = UiDensity.ScreenPadding };
// Primary button (gold)        normal: fill Gold,     text OnAccent; pressed: Gold.Darkened(.18); disabled: Surface2 + TextDisabled
// Secondary button (default)   normal: fill Surface2, border LineBright, text Primary; hover/pressed: Surface3
// Ghost button                 no fill, no border, text Secondary → Primary on hover; used for "Goodbye", "Cancel"
// Danger button                fill DangerFill, white text; only inside ConfirmDialog
// Focus                        theme "focus" stylebox: no fill, 3 lp AccentGold border, radius+2, expand margin 3
```

Godot 4 has no `BorderWidthAll` *property* on `StyleBoxFlat`; call `SetBorderWidthAll(2)`, `SetCornerRadiusAll(18)` and `SetContentMarginAll(m)`. Wrap these in small factory helpers in `UiTheme`.

## 6. Components

Every component is a small C# class (or factory) under `game/ui/Foundation/Components/`. It takes data and callbacks, and never queries game state itself. Screens pass in live data.

| Component | Anatomy | Sizes (Touch / Desktop) | States |
|---|---|---|---|
| **UiButton** (variants: Primary, Secondary, Ghost, Danger) | optional leading icon (28/20) + label (`LabelStrong`) | height `target.primary` (96/52) or `target.row` in lists; min width 160/100 | normal, hover (desktop), pressed (darken 18%), focused (gold ring), disabled (with a reason via `DisabledReason` caption or tooltip) |
| **IconButton** | icon centred on a circle/rounded plate; optional badge | 80×80 / 40×40 | as above + badge dot (gold) or count |
| **TabBar** (Book tabs) | icon + label per tab; selected tab gets a gold underline 4 lp and `text.primary`; others `text.secondary` | height 88 / 52; tabs share width equally | selected, unselected, badge (e.g. unspent skill points) |
| **Segmented** | 2–5 pill segments in one rounded track | height 72 / 40 | selected (fill `surface.3`, gold text), disabled (with reason) |
| **ListRow** | 56 leading icon well · title (`LabelStrong`) / subtitle (`CaptionLabel`) · trailing (value, chevron, badge, or button) | min height 88 / 48 | normal, pressed, selected (gold left bar 4 lp + `surface.3`), disabled |
| **ItemTile** | 96×96 / 64×64 square; icon 56/40; count badge bottom-right (`HudNumber`); quality pips top-left; "E" equipped chip top-right | — | normal, selected (3 lp gold), equipped, unusable (icon 40% opacity), new (gold dot) |
| **StatBar** | track + fill + ghost fill + centred value text | height 26 health, 18 mana, 6 XP (Touch); ×0.75 desktop | normal, low (≤ 25%: border pulses `danger.fill` 1 Hz; static when reduced motion), changed (ghost drains after 400 ms over 350 ms) |
| **Badge / Chip** | `radius.sm` pill; `GlyphLabel` or `CaptionLabel` | height 28 / 22 | neutral, gold, moss, danger, lumen |
| **SectionHeader** | `HeadingLabel` + optional trailing caption/action; 1 lp `line.subtle` under | — | — |
| **EmptyState** | 64 icon (`text.secondary`) + one sentence + optional action | — | — |
| **Toast** | 40 icon · title (`LabelStrong`) / optional caption · optional count "×3" | width ≤ 460 / 400, height ≥ 72 / 52; `surface.1` @ 0.92, elev.2 | enter, hold, merge (count bumps with a 120 ms scale pop), exit |
| **ConfirmDialog** | title, one-line consequence, [Cancel (ghost)] [Confirm (danger/primary)] | 560 wide; buttons `target.primary` | — |
| **Toggle** | ListRow with a trailing switch (64×36 / 44×24) | row height | on (gold), off, disabled |
| **SliderRow** | ListRow title + value caption; `HSlider` full width under it (grabber 40/24) | 120 / 64 tall | — |

Interaction rules shared by all components:
- On Touch, activation is **on release inside**. Inside a `TouchScroll`, this already holds. Outside scroll containers, Godot `Button` already acts on release. Keep `ActionMode = Release`.
- Hover styles exist only on Desktop (`MobileLayout == false`). On Touch, the pressed state gives the immediate feedback (≤ 1 frame).
- Focus rings show only after keyboard/gamepad navigation (track the "last input was key/joypad" flag in `ScreenHost`). Never show them after a touch.
- Every disabled control that a player might reasonably press shows its reason, as a caption in the row/button or in the details pane.

## 7. Icons

Author **monochrome SVG** icons on a 24×24 grid in `game/ui/icons/<name>.svg`: solid white fills, 2 lp minimum stroke weight, chunky silhouettes that read at 20 lp. Import settings (`.import`): `svg/scale = 4.0` (96 px textures), `compress/mode = Lossless`, `mipmaps/generate = true`. Tint at runtime with `SelfModulate`/`icon_normal_color`.

**Important:** the project default `textures/canvas_textures/default_texture_filter=0` is *nearest* (for pixel art). Set `TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps` on the UI root `Control` so icons and any scaled UI textures stay smooth. Don't change the project-wide setting.

Required set (name → meaning):

`sword` attack · `flame` ember · `leaf-heart` mend · `ward` bark ward · `dash` dodge · `flask` draught/potion · `hand` interact (generic) · `chat` talk · `chest` open · `gather` herb/ore/wood · `key` key/unlock · `lock` locked · `lever` pull · `brazier` kindle · `portal` enter ruin · `door` exit/return · `eye` inspect · `skull` loot/defeat · `bag` pack · `menu` pause · `book` journal/Book · `person` hero · `map` map · `close` · `back` · `check` · `warning` · `info` · `star` level up · `coin` crowns · `relic` · `herb` · `ore` · `wood` · `staff` · `coat` · `scroll` quest · `pin` track quest · `gear` settings · `save` · `load` · `sound-on` · `sound-off` · `vibrate` · `hammer` craft · `scale` trade · `plus` · `minus` · `target` objective · `bug` developer.

Item and ability icons are chosen by ID through one lookup table in `UiIcons` (`ItemIcon(prototypeId)`, `AbilityIcon(abilityId)`, `InteractionIcon(kind)`), with a `fallback` icon. Content authors then add new items without touching screens.

## 8. Motion

| Token | Duration | Easing | Use |
|---|---|---|---|
| `motion.instant` | 0 ms | — | Verification mode, reduced motion |
| `motion.fast` | 120 ms | ease-in (cubic) | Close / dismiss, pressed feedback |
| `motion.base` | 180 ms | ease-out (cubic) | Screen open (fade 0→1 + slide 24 lp up), tab content cross-fade |
| `motion.slow` | 320 ms | ease-out (quart) | Region title card, level-up banner |
| `toast.hold` | 2600 ms (+40 ms per char above 30, max 5000) | — | Toast visible time |
| `bar.ghostDelay` / `bar.ghostDrain` | 400 / 350 ms | linear | Health/mana ghost |

Rules:
- Logical state changes **before** the animation: `BlocksInput` becomes true when a screen is pushed and false the moment it is popped. A closing screen's controls get `MouseFilter = Ignore` during its fade-out.
- Use one `Tween` per screen and kill it before starting another. Never `await` a tween in code that tests call synchronously.
- Reduced motion (Part 4 setting) means fades only (no slides, pulses, typewriter or scale pops), and `CameraFeedback.Strength = 0`.
- Nothing flashes or pulses faster than 1 Hz, and no full-screen flashes.

## 9. Sound and haptics

Use the existing `AudioService` (gdfxr presets) for UI cues, routed to a `UI` bus that Part 4 adds: `ui_tap` (soft click on any button release), `ui_open`/`ui_close` (book), `ui_tab`, `ui_confirm`, `ui_error` (low thud for a rejected action), `ui_toast_loot`, `ui_quest`, `ui_levelup`. Keep them short (< 150 ms) and quiet (−12 dB relative to combat). Haptics (`Input.VibrateHandheld(ms)`, Android only, setting-gated, default on): 12 ms on taking damage, 20 ms on level-up, none on taps.

## 10. Voice and copy

- Plain, warm, and brief. Second person and present tense. "No room in your pack", not "Inventory full error".
- Buttons are verbs: "Equip", "Craft", "Buy", "Track", "Save to slot 2". Rows are nouns.
- Numbers: "2 / 3", "×3", "5 crowns", "3 s". Use `×` (U+00D7) for counts and `·` (U+00B7) as a separator.
- Consistent names: "Pack", "Adventurer's Book", "Hero", "Journal", "Map", "Crowns", "Draught" (short for Restorative draught on buttons).
- Never show raw IDs (`forge_sword`, `beast`, `ruin`) to the player. Map them through content names or a small display-name table in `UiText.cs`.
