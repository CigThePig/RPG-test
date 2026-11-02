# PR #7 interaction contract (Part 1)

## Presentation ownership

`UiNavigation` owns only presentation state and the invoking-menu return flag. `UiScreenHost` owns a fixed title/Back header and a TouchScroll body. `RpgUi` remains the compatibility facade: ActionControl, BlocksInput, MenuOpen, CombatControls, Stick and Foundation dispatch are retained. World/Gloot/Narrative/RegionState own all gameplay data. CharacterOverview reads production state each time it opens; it has no inventory, quest or save authority.

Priority: dialogue > confirmation > pause menu > information screen > gameplay. Lower surfaces hide while a higher surface owns focus. Transitions synchronously cancel gesture owners and update input blocking; they do not defer blocking to the next render frame.

## Transition table

| Current surface / trigger | Result | Input/focus |
| --- | --- | --- |
| Gameplay / Menu, Escape, Android Back | Pause menu | Cancel gameplay gestures; focus Resume |
| Menu / Character, Pack, Journal, Map | Requested screen | Record menu caller; focus fixed Back |
| Screen opened from menu / Back, Escape, Android Back | Menu | Restore menu focus; remain paused |
| Screen opened directly / Back | Gameplay | Release UI focus; dead player gets recovery menu |
| Menu / Back or Resume | Gameplay if alive | Defeated player retains recovery actions |
| Menu / New game | Confirmation | Hide underlying menu; focus Cancel |
| Confirmation / Back or Cancel | Menu | No world mutation |
| Confirmation / Start new game | New game | Existing Foundation reset/resume path |
| Any / dialogue begins | Dialogue owns presentation | Cancel gameplay gestures; lower UI hidden; Back cannot dismiss dialogue |
| Dialogue ends | Previous surface | Restore lower surface if it was paused |
| Any / focus out or app pause | Safe pause | Clear touches/motion/dodge; existing autosave policy retained |
| Resume/focus in | Remain paused | Explicit player resume required; dialogue retains priority |
| Any / resize or text-scale change | Same screen | Cancel gestures; recompute safe layout |
| Defeat | Recovery available | Cancel pending touches; gameplay commands remain blocked; Menu touch remains available |
| Successful load or autosave recovery | Gameplay | Existing Foundation load/rebuild/resume, no UI state in world saves |
| Failed load/save | Remain in menu | Existing error appears in notification region |

`ClosePanels` and `HideMenu` remain programmatic compatibility resets to gameplay for existing verification and course helpers. User-facing screen dismissal uses Back and preserves its caller. The pilot overview is read-only. Existing progression controls remain below the overview so learning/training remains available; Part 3 will redesign these controls.

## Touch and focus contract

- Stable action IDs and existing MobileCombatControls geometry remain unchanged. Only the original owning finger may release inside the original action. No transfer across buttons. OS-emulated mouse events are deduplicated.
- Opening/replacing/closing screens, confirmations, menu, dialogue, defeat, lifecycle pause and layout changes cancel pending action touches, joystick, scrolling gestures, TouchMovement and dodge. Orphan action releases never dispatch.
- TouchScroll owns swipes that begin over rows, suppresses emulated clicks, and activates only an uncanceled release inside the original row. Its existing scrollbar, slider and option-selector exceptions remain native.
- Pilot Back stays outside the scroll surface. It receives initial keyboard focus. Native Godot tab/directional traversal follows visual insertion order; a bronze focus outline is visible. Confirmation Cancel receives initial focus. No required pilot information needs hover.
- Back is handled in the UI input stage before gameplay shortcuts; WMGoBackRequest uses the same coordinator. `config/quit_on_go_back=false` prevents Android from exiting instead. Android may deliver both a key event and WMGoBackRequest for one press; SystemBack coalesces opposite key/window delivery types within 250 ms (they can cross frame queues), so one physical press cannot pop the confirmation and then resume the menu beneath it. Explicit navigation resets the pair guard, same-source repeated Back remains available, and visible button/programmatic Back remains independent.

## Layout and accessibility tokens

- Shared dark forest surface `#172523`, ivory text `#eee9d6`, bronze borders `#bcaa76`, spacing 12 logical units; panel padding 16 horizontal / 12 vertical. Ivory against the opaque surface exceeds 4.5:1 normal-text contrast. Focus is indicated by a three-unit outline, not color alone.
- Default type 20 logical units; Large is 26 (1.3x). `SetTextScale` supports 1–1.4x. The menu exposes Standard/Large for immediate use; persistence is Part 4. Text scaling applies to the screens/menu/confirmation. Existing HUD and combat-button type remain stable until Part 2.
- Buttons have a 56 logical-unit minimum height. Back is at least 112 wide and always visible. Logical viewport units are **not** Android dp; physical density/thumb comfort requires S25 acceptance. Never claim 48dp from a viewport-pixel assertion alone.
- Overlay regions use 16-unit inset from the converted safe region, max screen width 740 / menu width 560, remaining height with 96 units reserved below for notifications. The persistent header stays above independently scrolling content. Wrapped labels and buttons accommodate large text; long legacy development controls may still require Part 4 cleanup.
- Physical display insets convert through MobileCombatControls.ToLogicalSafeArea using viewport screen transform (including scale/translation), never world camera transform. Verification injection uses already-logical safe bounds.
- Gameplay HUD, action cluster and notification region retain current behavior pending Part 2. No animation or flashing is introduced. Information uses text labels and numeric values, not color-only signals.

## Verification and phone checklist

UiNavigationChecks exercises state transitions in the native suite and actual rendered controls, focus, touch Back, Escape, WM Back, New Game cancellation, lifecycle pause, and Character at 1280×800, 1280×720, 1920×864, 1024×768 and 900×560 with large text and nonzero insets. Existing MobileCombatControlsChecks remain enabled, including transformed safe area and real input ownership checks. Screenshots are emitted to docs/native-screenshots/*ui-character*.

On Galaxy S25: open Menu → Character → Back → Resume; open Pack → system Back; scroll Character with Large text; rotate/background/resume; hold movement/action while opening menu; cancel New Game; recover after defeat through Load/New Game. Confirm readable text, reachable Back, no accidental row actions, no stuck movement and comfortable targets. Physical acceptance remains a user device check.
