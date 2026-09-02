# Persistent Removed Merlin Part Condition Test

## Setup

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode and wait for Unity to compile.
4. Clear the Console and confirm no new red errors appear.
5. Run **Hanger 51 > Merlin Condition > 26 - Preserve Removed Part Condition**.
6. Run **Hanger 51 > Merlin Condition > 27 - Validate Removed Part Condition Persistence**.
7. Confirm Step 27 passes before entering Play mode.

## Cracked cover round trip

1. Purchase a fresh complete V-1650 and install it in the P-51.
2. Use the overboost test to crack one cylinder cover.
3. Stop the engine.
4. Press `X` on the cracked cover and record its exact health.
5. Remove that bank's twelve spark plugs and six bolts.
6. Remove the cracked cover.
7. Open inventory and confirm the cover entry reports the same health and `CRACKED`.
8. Drop the cover on the floor.
9. Confirm the loose cover remains visibly darkened and has crack markings.
10. Aim at it and confirm the pickup prompt reports the same health.
11. Pick it up again.
12. Reinstall it on the same or another compatible Merlin.
13. Press `X` after installation and confirm the original health returns rather than 100%.
14. Confirm the installed crack visual returns.

## Spark-plug round trip

1. Use a worn spark plug or temporarily inspect a plug with less than 100% health.
2. Record the exact value shown by `X`.
3. Remove that plug with `R`.
4. Open inventory and confirm its stored condition matches.
5. Drop the plug on the floor.
6. Confirm its ceramic remains darkened according to wear.
7. Pick it up and reinstall it.
8. Press `X` and confirm the same health returns.
9. Confirm intentionally removed plugs are not confused with other plugs in the stack; the next equipped instance displays the condition that will be installed.

## Full-inventory fallback

1. Fill every inventory slot.
2. Remove a worn plug or cracked cover.
3. Confirm the part is still removed and appears beside the engine.
4. Confirm its floor appearance and pickup prompt retain the original condition.
5. Free a slot, pick it up, reinstall it, and verify the condition again.

## Bare engine-block round trip

1. Remove both covers, all plugs, and the bare engine block.
2. Before removal, record block health and oil quantity.
3. Confirm the inventory or floor pickup reports both values.
4. Drop and pick up the block once.
5. Place it on a compatible stand.
6. Press `X` on the block and confirm health and oil quantity are restored.

## Regression checks

- Newly purchased replacement parts still begin at 100%.
- Different plugs in one inventory stack may have different health values.
- Dropping and picking up an item never changes its saved value.
- Reinstalling one bank's cover still preserves the untouched bank's plugs.
- Removing a cracked installed cover still stops that bank's fire and active oil leak.
- The purchased complete-engine template receives the persistence controller.
