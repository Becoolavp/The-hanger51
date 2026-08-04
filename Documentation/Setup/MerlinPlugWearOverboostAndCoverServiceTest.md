# Merlin Plug Wear, Overboost, Cover Service, and Cowling-Clearance Test

This milestone guarantees very slow spark-plug deterioration, adds sustained high-power cylinder-cover failures, combines cover removal and condition inspection on the same targets, repairs complete-engine shipment templates, and reduces the oil cap and dipstick handle for cowling clearance.

## Apply the update

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Merlin Condition > 9 - Add Slow Plug Wear, Overboost Failures, and Cover Service**.
4. Run **Hanger 51 > Merlin Condition > 10 - Validate Slow Plug Wear, Overboost Failures, and Cover Service**.
5. Save the scene.

Do not rerun Merlin Condition Steps 1, 5, or 7 afterward. If one of those older generation or repair steps is rerun later, rerun Steps 9 and 10 afterward.

## Expected wear behavior

- Every installed spark plug accumulates wear whenever its specific Merlin is running.
- Healthy plug wear is set to 0.20% per running hour at the baseline rate.
- Throttle changes the exact rate; healthy full-power wear is approximately one third of one percent per hour.
- Rough running accelerates plug wear, but normal deterioration remains deliberately slow.
- Wear is accumulated in double-precision remainders and applied in 0.001% steps so it cannot be rounded away by per-frame floating-point precision.
- `X` inspection reports plug condition to two decimal places.

## Expected overboost behavior

- High-power exposure begins at 95% throttle.
- Below approximately 92% throttle, accumulated exposure gradually cools down.
- The first minute at 95% or greater is a grace period.
- Continued operation damages one primary cylinder cover first.
- After another 45 seconds of sustained exposure, the opposite cover also begins taking slower damage.
- A cover is considered cracked at 35% condition.
- An installed cracked cover automatically activates the existing power loss, rough running, visible crack, fire, and oil-leak behavior.
- Removing a cracked cover stops its installed-engine fire and oil leak, but its recorded condition remains inspectable until a replacement is installed.

## Test normal plug wear

Normal wear is intentionally too slow for a large change during a short test.

1. Install a complete healthy engine in the P-51.
2. Inspect one plug with `X` and record its two-decimal condition.
3. Start the Merlin.
4. Run it at moderate or high power for several minutes.
5. Stop the engine.
6. Inspect the same plug again.
7. Confirm the value is slightly lower or that accumulated wear continues across a longer run.
8. Confirm other installed plugs also deteriorate independently.

The inspection uses two decimals because whole-number reporting would hide normal short-duration wear.

## Test the delivered-engine cover workflow

Purchase a new complete engine after running Step 9 so the repaired inactive template is used.

1. Purchase and unbox a **Complete Serviceable V-1650 Assembly**.
2. Aim at either installed cover.
3. Confirm its prompt includes `X` inspection.
4. Press `X` and confirm the cover reports approximately 100% and `installed`.
5. Remove both spark plugs associated with the bank being serviced.
6. Loosen every bolt assigned to that cover by holding `R` on each bolt.
7. Aim at the cover-placement/removal target.
8. Confirm it shows both the `R` removal instruction and the `X` inspection instruction.
9. Hold `R` to remove the cover.
10. Confirm the cover is returned to inventory.
11. Aim at the now-open bank target.
12. Press `X`.
13. Confirm the removed cover's recorded condition is still shown and marked `removed`.
14. Equip a replacement cover and reinstall it normally.
15. Confirm the replacement begins at new condition.

## Test natural sustained overboost

1. Begin with a healthy complete engine and full oil.
2. Install it in the P-51.
3. Start the engine.
4. Hold at least 95% throttle continuously.
5. Confirm no immediate cover failure occurs during the grace period.
6. Continue high-power operation.
7. Confirm one cover begins losing condition first.
8. Continue until that cover reaches 35% or less.
9. Confirm visible cracking, fire while running, oil leakage, rough running, and significant power loss.
10. Reduce below approximately 92% throttle and confirm further exposure begins cooling rather than continuing to accumulate.

A natural test can take several minutes because the failure is designed to result from sustained abuse rather than momentary full power.

## Accelerated overboost validation

1. Exit Play mode.
2. Select the engine station, portable engine root, or an object beneath the engine.
3. Run **Hanger 51 > Merlin Condition > 11 - Prime Selected Engine for Overboost Test**.
4. Enter Play mode.
5. Install and start the selected engine if necessary.
6. Hold at least 95% throttle.
7. Confirm the primary cover cracks after roughly 10–20 seconds.
8. Confirm fire, oil loss, rough running, and power reduction appear.
9. Reload the saved scene afterward, or use the existing restore-to-new-condition tool and allow the exposure to cool before another normal test.

## Test cowling clearance

1. Inspect the yellow oil cap and dipstick handle with the cowling removed.
2. Confirm both remain visible and selectable.
3. Install the engine and carry the cowling to the aircraft.
4. Install the cowling.
5. Walk around the nose and use both cockpit and external views.
6. Confirm neither the oil cap nor the dipstick handle visibly protrudes through the cowling.
7. Remove the cowling again.
8. Confirm the smaller service hardware remains accessible.

## Regression checks

- The hoist hook is not blocked by the service hardware.
- The original engine and purchased engines retain independent wear and overboost histories.
- Cover inspection does not hide or replace cover removal prompts.
- Spark-plug inspection does not hide or replace plug removal prompts.
- Removing a cover stops fire and oil leakage from that removed bank.
- A healthy engine still produces full configured power.
- A cracked installed cover still causes substantial power loss.
