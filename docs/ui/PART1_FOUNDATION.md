# Part 1 — UI design system, navigation and foundations

**Goal:** replace the ad-hoc `RpgUi` panel/menu with a themed, safe-area-aware, stack-based screen system that Parts 2–4 build on. Visible gameplay behaviour should not change, apart from a cleaner Pause menu, a tabbed Book shell, Android Back that no longer quits, and readable sizes.

Read first: [README.md](README.md) §5 (contracts), [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md). Mockups: [`08-pause.svg`](mockups/08-pause.svg) (pause layout), [`02-book-pack.svg`](mockups/02-book-pack.svg) (Book shell: header, tabs, close).

## Scope

**In:** tokens, theme and density; fonts; icon pipeline and the initial icon set; `UiFrame` safe area; `ScreenHost`/`UiScreen` navigation with transitions; shared components; the Pause screen (restyled, with developer entries moved out); the Book shell, with the *existing* Pack/Character/Journal/Map content ported in roughly as-is; the Developer screen holding the existing tools; Android Back; UI probe logging; Android smoke migration; tests.

**Out (later parts):** HUD redesign (2), new Pack/Hero/Journal/Map content (3), dialogue restyle, settings, Save/Load cards and the defeat flow (4).

## Target structure after Part 1

```
RpgUi (CanvasLayer, layer 10)
└── root : Control  (FIRST child — ReviewChecks relies on this; full rect; MouseFilter Ignore;
                     Theme = UiTheme.Current; TextureFilter = LinearWithMipmaps)
    ├── hud : Control            (existing title/health/message/hotbar/stick/metrics/marker — Part 2 replaces)
    ├── toastLayer : Control     (Part 2; create the empty node now, so z-order is fixed)
    └── host : ScreenHost        (full rect; scrim + stacked UiScreens; above HUD)
```

## Step-by-step implementation

Make each step its own commit and keep the suite green after every one.

### Step 1 — Project settings and Back handling
1. `project.godot` → `[application] config/quit_on_go_back=false`. Godot then sends `NOTIFICATION_WM_GO_BACK_REQUEST` instead of quitting.
2. In `RpgUi._Notification`, on `NotificationWMGoBackRequest` call `HandleBack()`:
   - if `App.Narrative.speaking` → do nothing;
   - else if `host.AnyOpen` → `host.Back()`;
   - else → `Menu()` (open Pause).
3. In `Foundation._UnhandledInput`, change the `menu` action (Esc) to: `if (ui.BlocksInput) ui.Back(); else ui.Menu();`. `ReviewChecks`' Escape route, which closes the initial Pause, keeps working.
4. **Decision D1** (see README §7). If approved, set `window/stretch/aspect="expand"` here. Otherwise leave the default. Do not change it anywhere else.

### Step 2 — Tokens, density, theme, fonts
Create `game/ui/Foundation/`:

- `UiTokens.cs`: `public static class UiTokens` with `Color` fields for every colour token, ints for spacing/radii, floats for durations, and a `TypeRamp` record holding Touch/Desktop sizes. Names follow DESIGN_SYSTEM (`Surface1`, `TextPrimary`, `AccentGold`, `Space4`, `RadiusMd`, `MotionBase`, …).
- `UiDensity.cs`: `public sealed record UiDensity(bool Touch, float TextScale, float ButtonScale, bool LeftHanded)`, with computed props `TargetMin`, `TargetRow`, `TargetPrimary`, `GapTarget`, `ScreenPadding`, `Font(style)`. `RpgUi` owns `public UiDensity Density { get; private set; }`, initialised from `MobileLayout` with scales of 1 (Part 4 feeds user settings).
- `UiTheme.cs`: `public static Theme Build(UiDensity d)`. Set `DefaultFont`, `DefaultFontSize = Body`, and type variations with `theme.SetTypeVariation(name, "Label")`/`"Button"`: `DisplayLabel, TitleLabel, HeadingLabel, LabelStrong, CaptionLabel, HudNumber, GlyphLabel, PrimaryButton, SecondaryButton(default Button), GhostButton, DangerButton, ListRowButton, TabButton`. Add styleboxes for `PanelContainer` (panel), `Button` (normal/hover/pressed/disabled/focus), `ScrollContainer` grabbers (Touch: 12 lp wide, `line.frame`; the existing 28 lp minimum scrollbar width on the menu stays as the touch hit width), `HSlider` (grabber 40/24), `OptionButton`, `SpinBox`, `CheckButton`, and `TooltipPanel`. Expose `UiTheme.Current` and `UiTheme.Rebuild(d)`, which re-assigns `root.Theme` (Part 4 uses this for text size).
- Fonts (decision D2): put `AtkinsonHyperlegible-Regular.ttf`, `-Bold.ttf`, `AlegreyaSC-Bold.ttf` and `OFL.txt` in `game/ui/fonts/`. Load them with `GD.Load<FontFile>`. If a font is missing at runtime, fall back to `ThemeDB.FallbackFont` with a single `GD.PushWarning`. Do not fail, because CI imports must not depend on optional assets. Update `THIRD_PARTY_NOTICES.md` and `docs/UPSTREAM_DEPENDENCIES.md`.
- Apply the theme to `root` in `RpgUi._Ready()` **before** creating children. Delete the inline `Theme`/`StyleBoxFlat` construction from `_Ready`.
- Dialogue (temporary, Part 4 replaces it): after `ui` exists, set `App.Narrative.Set("ui_theme", UiTheme.Current)`. In `Narrative.gd build_ui()`, use `ui.theme = ui_theme if ui_theme != null else <current theme>`. Only the look changes.

### Step 3 — Icons
- Add `game/ui/icons/` with at least: `close back bag menu book person map scroll gear save load sound-on sound-off bug check warning info sword flame leaf-heart dash flask hand coin`. Parts 2/3 add the rest of DESIGN_SYSTEM §7. Each is a 24×24 viewBox, white fill, no strokes thinner than 2.
- Commit the `.import` files with `svg/scale=4.0` and mipmaps on (run `python3 tools/import-project.py` to generate them).
- `UiIcons.cs`: `Texture2D Get(string name)` with a cache and a fallback (`info`). Also `ItemIcon(string prototypeId)`, `AbilityIcon(string abilityId)` and `InteractionIcon(string kind)`, each a dictionary → name map.
- Set `root.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps`.

### Step 4 — `UiFrame` (safe area as a service)
- `UiFrame.cs` computes `Rect2 Safe` exactly as `RpgUi.Layout()` does today: the viewport rect; on Android `MobileCombatControls.ToLogicalSafeArea(...)`; then `VerificationSafeArea ?? safe`. **Desktop now also uses the safe rect**, which is simply the viewport there. It raises `event Action<Rect2> Changed`.
- `RpgUi.Layout()` asks `UiFrame` for `Safe`, then lays out the HUD (existing code, unchanged in Part 1) and calls `host.Arrange(safe)`. Keep the `MOSSLIGHT_CONTROL` logging and `CancelActionTouches()`/`Stick.Cancel()` at the top of `Layout()`.
- Keep `MobileCombatControls.ToLogicalSafeArea` and `Geometry` as public statics; tests call them directly.

### Step 5 — `ScreenHost` and `UiScreen`
```csharp
public enum ScreenFamily { Pause, Book, Developer }
public abstract partial class UiScreen : PanelContainer
{
    public abstract string Id { get; }                 // "pause", "book", "settings", "saveload", "developer", "confirm"
    public abstract ScreenFamily Family { get; }
    public virtual bool IsChild => false;              // child screens stack on top of a root of the same family
    public RpgUi Ui { get; internal set; } = null!;
    public virtual Rect2 Frame(Rect2 safe, UiDensity d) => /* centred default, see sizes below */;
    public virtual void Build() { }                    // once, after Ui is set
    public virtual void Refresh() { }                  // rebuild content from live state; idempotent; cheap
    public virtual bool OnBack() => false;             // true = consumed (e.g. leave a sub-page)
}
public partial class ScreenHost : Control
{
    public Action BeforeModal = () => { };             // RpgUi: CancelActionTouches; Stick.Cancel; App.TouchMovement=0; App.World.CancelPlayerDodge()
    public IReadOnlyList<UiScreen> Stack { get; }
    public UiScreen? Top { get; }
    public bool AnyOpen => Stack.Count > 0;
    public bool Has(ScreenFamily f);
    public event Action? Changed;
    public void Push(UiScreen s);      // BeforeModal() first, synchronously; then show + animate
    public void Replace(UiScreen s);   // CloseAll() then Push(s)
    public bool Back();                // Top.OnBack() ?? pop Top; returns false if the stack was empty
    public void CloseAll();
    public void Arrange(Rect2 safe);   // re-frame every screen; called from RpgUi.Layout()
}
```
- **Scrim:** one `ColorRect` (`scrim` token) behind the bottom-most screen, visible while `AnyOpen`. Tapping the scrim calls `Back()` only on Desktop. On Touch it does nothing, to avoid accidental closes.
- **Transitions:** `motion.base` open (alpha 0→1, position +24 lp → 0), `motion.fast` close. Pop and remove from `Stack` immediately; animate a detached visual, then `QueueFree`. With `App.VerificationMode` (or Reduced motion, Part 4) all durations are 0. `BlocksInput` must flip in the same call.
- **Focus:** on Desktop after a keyboard/joypad event, `Push` grabs focus on the screen's `DefaultFocus` control. On Touch, never grab focus.
- **Frames (sizes):**
  - Pause: width `min(640, safe.W-48)`, height `min(content, safe.H-48)`, centred.
  - Book: Touch fills `safe` inset by 16; Desktop is 1100×680 centred (clamped to safe).
  - Developer: same as Book.
  - Child screens (Settings, Save/Load): Touch fills `safe` inset by 16; Desktop is 960×640.
  - Confirm: 560 × auto, centred.

**`RpgUi` API mapping (keep the signatures):**

| Member | New implementation |
|---|---|
| `BlocksInput` | `host.AnyOpen` |
| `MenuOpen` | `host.Has(ScreenFamily.Pause)` (true also while Settings/Save/Load are on top of Pause) |
| `Menu()` | if speaking → return. If the Pause family is open → `CloseAll()`, else `Replace(new PauseScreen())` |
| `Back()` *(new)* | `host.Back()` |
| `HideMenu()` | if `MenuOpen` → `CloseAll()` |
| `ClosePanels()` | if the root family ≠ Pause → `CloseAll()` |
| `PauseForVerification()` | `CancelActionTouches(); Stick.Cancel(); host.CloseAll(); host.Push(new PauseScreen())` |
| `Inventory()/Character()/Journal()/Map()` | existing guard (`speaking` or `MenuOpen` → return), then `OpenBook(tab)` (below) |
| `OpenBook(BookTab tab)` *(new)* | if Book is open on `tab` → `CloseAll()`; if open on another tab → switch tab; else `Replace(new BookScreen(tab))` |
| `Workshop()/FeelLab()/Diagnostics()` | `Replace(new DeveloperScreen(page))` |
| `SettingsOpen` | `host.Has` the settings screen (Part 4); false in Part 1 |

### Step 6 — Components
Implement these from DESIGN_SYSTEM §6 now: `UiButton` (4 variants, optional icon, `DisabledReason`), `IconButton` (+ badge), `TabBar`, `Segmented`, `ListRow`, `SectionHeader`, `EmptyState`, `Badge`, `ConfirmDialog` (a `UiScreen` with `IsChild = true`). `ItemTile`, `StatBar`, `Toast`, `Toggle` and `SliderRow` may land with the part that first uses them.

Every component sets `CustomMinimumSize` from `Density` (never a literal) and gives its main control a stable `Name` (see Step 8).

### Step 7 — Screens
**PauseScreen** (mockup 08). From top to bottom:
1. Header: `DisplayLabel` "Mosslight". Caption: `"{player.Name} · Level {n} · {RegionName}"`, or "A new adventure" before the session starts.
2. **Primary** `UiButton` with text exactly **`Enter / Resume`**, Name `pause_resume`, calling `App.ResumeGame`.
3. A row of four `IconButton`s with labels **`Pack`, `Journal`, `Character`, `Map`** (Names `pause_pack` …). The text must be exactly these strings for `ReviewChecks`. Each closes Pause and opens the Book on that tab.
4. Secondary list (`ListRowButton`s): `Save` (keeps today's slot behaviour: a `Segmented` slot picker 1/2/3 bound to `SaveSlot`, then Save), `Load`, `Recover background autosave`, `Audio: On/Off`, `Camera zoom` (`App.CycleZoom`).
5. Ghost rows at the bottom: `New game` (now opens `ConfirmDialog` "Start a new game? Unsaved progress will be lost." → `App.Reset(); App.ResumeGame()`), `Developer tools` (only if `UiDev.Enabled` = `OS.IsDebugBuild() || args contain --dev`), and `Quit` (desktop only; confirm).

Keep every action's error handling (`InFeelCourse` guards, try/catch → `Notify`) exactly as in today's `BuildMenu`. Move that code; don't rewrite it.

**BookScreen** (shell; Part 3 fills it): header with `TitleLabel` ("Adventurer's Book"), `TabBar` (Pack, Hero, Journal, Map, with icons bag/person/scroll/map) and a close `IconButton` (Name `book_close`). The tab content host is a `TouchScroll` (except Map). In Part 1, port the bodies of today's `Inventory()`, `Character()`, `Journal()`, `Map()` into `PackPage`, `HeroPage`, `JournalPage` and `MapPage` with the same buttons/actions, using `ListRow`/`UiButton` styling and the type ramp. **Behaviour must stay identical.** After any mutation, the page calls `Refresh()` (not the old `Inventory()` re-entry).

**DeveloperScreen**: a `Segmented` control for Diagnostics | Feel lab | Workshop | Overlays. Move today's `Diagnostics()`, `FeelLab()` and `Workshop()` bodies here unchanged. Add the "Collision / navigation overlay" and "Performance overlay" toggles (moved from Pause) to *Overlays*. Header caption: "Development tools — not part of the game".

### Step 8 — Stable names and the UI probe
- Give stable `Name`s: `pause_resume, pause_pack, pause_journal, pause_character, pause_map, pause_save, pause_load, pause_recover, pause_new_game, pause_developer, pause_quit, book_close, book_tab_pack, book_tab_hero, book_tab_journal, book_tab_map, confirm_accept, confirm_cancel`.
- `UiProbe.cs`: on Android, or when the command line contains `--ui-probe`, after a screen is shown or arranged (deferred one frame), print `MOSSLIGHT_UI id=<name> x=<px> y=<px>` for every visible named Button in the top screen. Use physical px via `GetViewport().GetScreenTransform() * rect.GetCenter()` (same as `MOSSLIGHT_CONTROL`).
- Also log the stick: `MOSSLIGHT_CONTROL version=… id=stick x=… y=…` in `Layout()`.
- **Update `tools/android-smoke.py` in the same commit:** add `ui_target(name)` (latest matching `MOSSLIGHT_UI` line, with a bounded wait) and replace `tap(640,178)` → `tap_ui('pause_resume')` (all three sites) and `tap(640,566)` → `tap_ui('pause_new_game')` then `tap_ui('confirm_accept')`. Replace the 1280-space stick swipe with one starting at the logged stick centre and moving left by `110*h/800` px.
- Switch `VisualChecks` and `ReviewChecks` to find buttons by `Name` rather than `Text` (keep the texts anyway in Part 1).

### Step 9 — Tests (`game/tests/UiFoundationChecks.cs`, registered in `FoundationChecks` next to `MobileCombatControlsChecks`)
Assert, using the production `RpgUi` and real screens:
1. `ProjectSettings.GetSetting("application/config/quit_on_go_back")` is false.
2. Contrast: compute WCAG ratios in C# for every `Text*`/accent token on `Surface1`/`Surface2` and require ≥ 4.5, except `TextDisabled` (≥ 3.0 on `Surface1`).
3. Navigation: Pause→Book replaces (`MenuOpen` false, `BlocksInput` true). Book→`Menu()` replaces. `Back()` from Settings-like child returns to Pause. `Back()` on an empty stack returns false. `OpenBook(Pack)` twice closes. `OpenBook(Journal)` while Pack is open switches the tab without re-pushing (stack count stays 1).
4. Back request: `_Notification(NotificationWMGoBackRequest)` pops when open, opens Pause when empty, and does nothing while `Narrative.speaking`.
5. Input cancellation: set `App.TouchMovement`, start a stick touch, push any screen → movement is zero, `Stick.Finger < 0`, and no owned action touches remain.
6. Determinism: in verification mode, a pushed screen's `Modulate.A == 1` and its final position are reached in the same frame.
7. Touch sizes (mobile only): with Pause and each Book tab open, every visible `Button` in the top screen has `Size.Y >= Density.TargetMin`. Also every Button's global rect lies inside `UiFrame.Safe` (or inside its scroll viewport) at window sizes 1280×800, 1280×720, 1920×864 and 1024×768 with the 50/24 verification inset (reuse the loop pattern from `MobileCombatControlsChecks.Rendered`).
8. Exactly one visible Button each named/texted `Pack`, `Journal`, `Character`, `Map` while Pause is open (guards the `ReviewChecks` `.Single`).
9. `CanManageItems` is false while Pause is open and true while only the Book is open (alive, not speaking).

Keep all existing checks passing, including the menu-scroll swipe. If the new Pause list no longer overflows at 1280×800, move that swipe check to the Developer screen's Feel-lab page, which always overflows.

### Step 10 — Evidence and handoff
- Run `python3 tools/verify.py --headless`, `--visual`, and `--visual --mobile`. Replace `docs/native-screenshots/{menu,inventory,character,journal}.png` and the mobile equivalents with the new captures, and add `pause.png`/`developer.png`.
- Write `docs/PR7_PART1_HANDOFF.md`: what moved where, any changed test contract, D1/D2 outcomes, and limits (for example, "phone Back verified on emulator only").

## Acceptance criteria
- [ ] Android Back never quits. Esc/Back pop exactly one level. Esc with nothing open opens Pause.
- [ ] No hard-coded colours/sizes remain in `RpgUi`, Pause, Book or Developer code (grep for `new Color(` outside `UiTokens`/`RegionMap`).
- [ ] Touch: every button in Pause/Book/Developer is ≥ 80 lp tall and body text is ≥ 26 lp.
- [ ] Pause shows no developer tools. Developer tools are reachable in debug builds through one entry.
- [ ] All screens stay inside the safe area at the four test window sizes.
- [ ] Existing headless, visual, gameplay, adventure and Android smoke runs pass. The smoke uses logged coordinates, with no fixed 1280×800 taps left.
- [ ] Handoff written, with actual results.

## Pitfalls
- `ReviewChecks` uses `app.Ui.GetChildren().OfType<Control>().First()`. `root` must stay the **first** `Control` child of the CanvasLayer, and every screen must live under it.
- On mobile the HUD `inventory` button currently has the text "Pack". It is hidden while a screen is open (`hotbar.Visible = !BlocksInput` in `Update()`), which keeps the `.Single(b => b.Text == "Pack")` lookup valid. Preserve that order: `Update()` hides the HUD when `BlocksInput` is true.
- `TouchScroll` searches descendants for Buttons to press. Don't put `TouchScroll`s inside each other. A Book page that needs horizontal scrolling (none should) must not use `TouchScroll`.
- Godot `Button` text centring: list rows need `Alignment = HorizontalAlignment.Left`. Build rows as a `Button` containing an `HBoxContainer` with `MouseFilter = Ignore` children, so the whole row is one target and `TouchScroll` still finds the Button.
- Don't capture screenshots during a transition. Verification mode must force 0-duration tweens. Check this before regenerating evidence.
- `CanManageItems` uses `ui.MenuOpen`. Don't make `MenuOpen` mean "anything open", or Book mutations break.
