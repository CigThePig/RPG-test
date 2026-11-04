# PR #7 — Revised Part 5: Crafting, Trading and supporting shell

## Delivered interfaces

- **Workbench:** recipe collection beside a selected result preview, actual generated material/quality/stats, equipped-item comparison and owned/required material counts. Craft and its blocker remain visible while details scroll. Compact/large-text views switch between Collection and Details; Back returns to Collection first.
- **Merchants:** nearby merchant identity, actual stock, selected item preview, price, owned crowns and post-purchase balance. Buy stays visible with an explicit blocker. The merchant is revalidated at purchase. Trade navigation is contextual near merchants, while existing direct entry points remain compatible and explain when no merchant is present.
- **Pause:** primary Resume and Pack/Character/Journal/Map tiles, secondary Save/Load/Settings/New game/Quit, and a separate Developer tools area. The summary refreshes from current player state. Quit and New game require confirmation; defeat keeps recovery options visible.
- **Save library:** four slot tiles (three manual plus recovery), each showing real inspected state, alongside one selected slot's details/actions. Empty, ready, damaged and incompatible states retain their existing rules, damaged-file backups, confirmations and actual Persistence validation. No save schema changes.
- **Settings:** Display, Comfort and Audio categories with a live preference/readability summary on wide views. Existing independent UI preference persistence, contrast, text scale, sound, reduced motion and session zoom remain authoritative.
- **Dialogue:** a speaker heading and independently scrolling story/reply areas. Wide standard-text screens place them side by side; compact or larger-text layouts stack them. Safe-area positioning leaves more world space visible. Dialogue Manager still owns line/choice advancement, focus, asynchronous epochs and interruption behavior.

## Correctness and navigation

Product previews use disposable, unattached Gloot items and share the existing material formula with generation. They do not add items, advance serials, or change equipment. Native checks compare preview stats with actual crafted output and verify exact inventory immutability. Existing craft rollback, full-pack behavior, trade atomicity and event publication remain unchanged.

All custom layouts use the established host and deferred retirement, preserve stable action IDs, and cancel owned gestures during replacement. Compact Commerce Back is local and retains Android paired-Back deduplication. Dialogue owns Back over all lower surfaces. Native menu gesture checks deliberately force overflow when necessary so the smaller pause menu still receives actual swipe/cancel regression coverage.

## Verification and delivery

- Native build and fresh Godot import; headless suite: **8,891 assertions**.
- Focused desktop/mobile screen and supporting-shell suites passed, plus a standard-text wide-dialogue touch run. Coverage includes one-purchase/one-craft behavior, blocked ingredients/funds, slot protections, damaged-file backup, confirmed quit cancellation, preference persistence, dialogue focus/interruption/combat blocking and largest text at compact sizes.
- Exact-source full native/gameplay/Android workflow results and source SHA are recorded in PR #7. Screenshots are available in their evidence artifacts.
- `tools/package-install.py` checks the source APK, creates a flat install ZIP and verifies extracted bytes. Always deliver a ZIP and verify the saved/downloaded copy's archive and extracted-APK hashes; the previous raw APK delivery was truncated in transit. A successful upload alone is insufficient.

## Galaxy S25 acceptance — pending for Part 5

1. Loot materials, select recipes, compare the result, craft, and check missing-material/full-pack outcomes. Scroll both panes, and use Collection/Details at largest text size.
2. Open Pack beside the warden, browse stock, buy once and verify currency/stock. Confirm Trade is not a permanent navigation choice when away from merchants.
3. Pause/resume, open each main destination, cancel Quit/New game, and reach Developer tools separately.
4. Save to an empty slot, cancel an overwrite/load, then confirm a load. Recovery and incompatible/damaged files must retain their protections.
5. Change settings in all categories and restart; check that loading a world preserves preferences.
6. Talk to the warden at standard/largest text, scroll story and replies, exercise Android Back and background/resume. No gameplay actions may fire through dialogue.

The user reported the preceding Part 4 build looks good and requested Part 5. This records overall S25 feedback, not a claim that every cross-screen release checklist in Part 6 has been completed. PR stays draft and unmerged; Part 6 remains planned.
