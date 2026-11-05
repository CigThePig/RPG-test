# PR #7: review of Claude's interface redesign

Reviewed source: `dcb97203051d26464cc552168b0b92169d0a816a` and
`4823381df4076ebf1cfec3ee6e40296e3e0120d6`, based on PR #7's
`9617b7ee3d0f49faa1c5211e678eaae87d612479`.

These commits were on `ccr-cfce9e4c-upjx1j`, outside the PR branch. They are
preserved as ancestors of the review fixes brought into
`feat/mobile-ui-ux-overhaul`; no separate pull request or main-branch merge is needed.

## Review coverage

Inspected the complete source diff: shared theme and vector components, font loading,
HUD and combat controls, touch stick, Pack and item tiles, character/progression,
crafting and trading, Journal, Map, supporting screens, dialogue, navigation and
modals, gallery, test changes and Android layout-version integration. Reviewed the
committed redesign contact sheet and runtime compact-screen captures.

## Findings and fixes

1. **Journal tabs stopped responding after the first page switch.** The shared
   tab callback captured the initial selection, but Journal switches pages without
   rebuilding the tabs. It now reads the live selection flag. Rendered coverage
   makes repeated Quests → Reading → Quests round trips.
2. **Journal pane switches stopped canceling owned touches.** The redesign wrapped
   scrolls in panels, invalidating the old direct `TouchScroll` casts. Cancellation
   now reaches the descendant scrolls before hiding a pane. A regression holds a
   quest row, activates page navigation, returns, and releases the finger; neither
   a stuck tint nor a revived tap is allowed.
3. **Compact Pack/Map headers could exceed the safe screen at large text sizes.**
   Single-pane Pack shows only the alternate Bag/Gear destination and moves crowns
   into the backpack header, preserving vertical space for inspection. Compact Map
   moves its region chip below the action row. Header bounds join the rendered
   regression checks. Compact tests use an explicit 780×504 logical safe rectangle;
   touch positions pass through the viewport's screen transform after resizing.
4. **Equipment comparisons could hide lost attributes.** The new stat rows listed
   only properties on the candidate. A replacement without an equipped item's
   power/defence/damage property could conceal that loss. Pack and commerce now
   include the union of comparable attributes, showing zero and a negative delta
   for a removed attribute. A headless regression covers differing attribute sets.
5. **Font licences were missing from exported packages.** Godot's all-resources
   export does not include plain TXT files automatically. All export presets now
   include the bundled OFL texts. APK verification requires both licences and all
   four imported font payloads, following each actual import remap.

## Verification and acceptance

The review adds targeted regressions to the existing production integration and
rendered suites. GitHub Actions on the PR is the authoritative record for the
published source: native/headless, desktop and mobile UI, preserved reference,
both gameplay profiles, signed ARM64 package verification and Android runtime.

Physical Galaxy S25 visual acceptance, thumb comfort and device performance remain
the user's acceptance gates. Automated rendering and emulator installation do not
close those gates. Keep PR #7 as a draft until its remaining plan gates are met.
