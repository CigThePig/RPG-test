# PR #7 — Revised Part 4: Journal and Map

## Delivered behavior

- Journal owns a quest index grouped by active, available, completed, locked and failed status. Selecting a quest opens a separate reading area with known current/completed stages, prerequisite explanations and actual reward/pending-reward state. Future stage text stays hidden. Collected discoveries, mechanism counts and gathered lore remain read-only Narrative facts.
- Wide layouts show index and reading area together; compact/large-text layouts offer Quests and Reading pages. Quest selection survives switching screens. Back returns from a compact reading page to the index before leaving the journal.
- Map owns the available canvas rather than sitting inside a long document. Whole map and Around you remain explicit choices. A collapsible Guide contains legend and discovered rooms; compact views temporarily show this as a reading page, and Back returns to the canvas.
- Around you has explicit bounded West/North/South/East pan controls and You/recenter. Panning never changes exploration or world coordinates. It preserves context while opening/closing the guide. Region changes reset pan position. No drag gesture is required.
- Ruin terrain/features remain filtered by RegionState exploration, and landmarks by discovery authority. Undiscovered rooms/mechanisms are not shown. Entrance markers require known tiles. Marker positions use each landmark's actual focal anchor.
- Hero preview now renders the actual PPC model in a neutral front-facing pose, independently of the player's facing, animation and world position. The single cached image is reused across screen opens and resizes until the equipped model changes. Raster size growth is bounded and has a written fallback.

## Verification

Run native build, fresh import, headless integration, focused rendered screen suites for desktop and mobile, then the existing exact-commit native/gameplay/Android workflows. Results and APK are recorded in PR #7, with SHA and run links.

Focused coverage includes portrait facing/cache/motor isolation, selected quest retention, future stage secrecy, map pan/recenter bounds and fog state, guide Back behavior, enlarged text and safe-area layouts. Existing quest save/load, stage progression, equipment, journal/map and adventure checks remain in force.

## Galaxy S25 acceptance — pending

1. Face north in the world, then open Character; the portrait should face the camera. Repeat facing south/east/west and verify gameplay facing is preserved after closing it.
2. Read an active and completed quest, then a locked/available quest. Check reward text, stage history and selected-quest retention when switching screens.
3. In the Reach and a partially explored ruin, switch map scales, pan and recenter. Open/close Guide with its button and Android Back. Hidden terrain/rooms must stay hidden.
4. Check 140% text, short landscape height, swipes over quest buttons, scrolling the reader/guide, repeated open/close, background/resume and save/load.

PR stays draft and unmerged. The prior `PR7_PART4_HANDOFF.md` is the historical foundation milestone, not this revised Part 4 acceptance record.
