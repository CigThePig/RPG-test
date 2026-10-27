# Free-form melee and dodge

The player can swing the sword into empty space. The swing locks the current PPC yaw at initiation; the trail, collision-safe lunge, and impact query all use that same ground-space direction. At the existing windup boundary the world selects the nearest eligible hostile within the 110-degree arc and current reach, with ordinal actor IDs as the tie-break. A miss still consumes the ordinary cost and cooldown. NPC attacks, projectiles, healing, and other SPELL abilities keep their targeted execution path.

The 12-unit lunge is world movement over 0.08–0.18 seconds and is clipped by existing terrain/door collision. The sword's existing pose, trail, windup, and recovery have not been retimed or replaced.

Dodge has its own action state and 0.70-second initiation cooldown. It covers up to 90 ground units over 0.40 seconds with ease-out, subject to collision. The direction is the current unsmoothed movement intent, or visual facing if neutral, and is locked for the roll. Direct hits are rejected from 0.10 seconds through just before 0.30 seconds; existing periodic damage retains its normal rules. An early or late hit interrupts the roll. Opening a modal, backgrounding, or loading a saved roll clears its transient motion but retains a valid cooldown. Space dodges on desktop; the separate mobile button is beside Attack. Release inside the original button to activate it.

`game/combat/CombatTuning.cs` contains the new arc, lunge, and dodge constants; existing slash timing, base range, cost, cooldown, and damage remain in `game/data/foundation.json`. Dodge presentation uses the authoritative action progress, with a biped tuck and rotation and a limited fallback for other families. It does not drive world position or damage.

The automated replay and Android emulator exercise controls and state, but they cannot establish subjective combat feel or phone ergonomics. Physical Galaxy S25 playtesting remains necessary. Development APKs may use an ephemeral signing key; a differently signed installed build may need to be uninstalled first, which can remove local saves.
