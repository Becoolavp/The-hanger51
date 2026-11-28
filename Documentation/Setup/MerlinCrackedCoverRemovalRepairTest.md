# Merlin Cracked Cover Removal Repair Test

## Apply the repair

1. Exit Play mode.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Run `Hanger 51 > Merlin Condition > 20 - Repair Cracked Cover Removal`.
4. Confirm `Merlin Condition Step 20 complete.`
5. Run `Hanger 51 > Merlin Condition > 21 - Validate Cracked Cover Removal`.
6. Confirm `Merlin Condition Step 21 passed.`
7. Do not rerun older Merlin generation or fastener setup steps afterward.

## Blocker guidance

1. Enter Play mode with a complete engine.
2. Aim at either cylinder cover.
3. Confirm the prompt reports the remaining plugs and bolts that prevent cover removal.
4. Remove one spark plug from that bank.
5. Aim at the cover again.
6. Confirm the remaining plug count decreases.
7. Remove all twelve plugs from the selected bank.
8. Loosen one of its six bolts.
9. Confirm the remaining bolt count decreases.

## Cracked-cover removal

1. Use the existing accelerated overboost test or run the engine at 95% or more until one cover cracks.
2. Stop the engine before servicing it.
3. Remove all twelve plugs from the cracked-cover bank.
4. Loosen all six bolts assigned to that bank.
5. Aim at the cracked cover.
6. Confirm the prompt says `Hold R to lift off the left/right cylinder cover`.
7. Hold `R` until removal finishes.
8. Confirm the cover disappears from the engine.
9. Confirm fire and active oil leakage from that installed cover stop after removal.
10. Aim at the open bank target and press `X`.
11. Confirm the recorded cracked condition is still shown and the cover is reported as removed.

## Full-inventory fallback

1. Repeat the test with no free inventory slot before removing the cover.
2. Remove all plugs and loosen all bolts.
3. Hold `R` on the cover.
4. Confirm removal still completes.
5. Confirm a cylinder-cover pickup appears beside the appropriate side of the engine.
6. Free an inventory slot and press `E` on the dropped cover.
7. Confirm it can be collected normally.

The same fallback applies to removed spark plugs and the bare engine block.

## Purchased-engine regression

1. Purchase and unbox a new complete V-1650 after applying Step 20.
2. Confirm cover inspection still works with `X`.
3. Remove all twelve plugs and loosen all six bolts on one bank.
4. Hold `R` on that cover.
5. Confirm it removes normally.
6. Repeat with a full inventory and confirm the cover drops beside the delivered engine.

## Build regression

Run the three numbered Windows Build and Run menu steps and repeat one normal cover-removal test in the standalone build.
