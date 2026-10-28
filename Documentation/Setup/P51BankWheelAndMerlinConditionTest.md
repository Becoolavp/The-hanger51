# P-51 Extreme-Bank, Wheel-Contact, and Merlin Condition Test

This milestone repairs extreme-bank lift collapse and false landing-gear contact, then adds independent oil, engine-block, cylinder-cover, and spark-plug condition to every current Merlin assembly and the complete-engine shipment template.

## Apply the current setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > P-51 Mustang > 24 - Repair Extreme Banks and Wheel Contact**.
4. Run **Hanger 51 > P-51 Mustang > 25 - Validate Extreme Banks and Wheel Contact**.
5. Run **Hanger 51 > Merlin Condition > 1 - Add Oil, Wear, Damage, and Inspection**.
6. Run **Hanger 51 > Merlin Condition > 2 - Validate Oil, Wear, Damage, and Inspection**.
7. Save the scene.

Do not rerun older P-51 aircraft-generation, landing-gear-generation, or Merlin-generation steps after these steps.

## Current service controls

- `E` at a dipstick: pull or reinsert the dipstick.
- `E` at an oil can: pick it up.
- `E` while carrying a can away from a filler: set it down.
- `F` while carrying a can: open or close its cap.
- Hold `E` while aiming at the oil filler with an open can: pour oil.
- `X` while aiming at the block, either cover, a spark plug, or filler: inspect exact condition.
- Existing `E` installation and `R` removal controls remain active on engine parts.

## Oil baseline

- Oil capacity: **20.0 L**.
- Safe minimum: **15.0 L**.
- Dipstick indications:
  - FULL
  - SAFE
  - LOW
  - CRITICAL
- Each oil can begins with **20.0 L** and has finite contents.
- The engine and oil can stop accepting oil once the engine reaches 20.0 L.

## Test the corrected landing gear

1. Enter the P-51 and taxi slowly.
2. Watch all three visible tires.
3. Confirm a tire moves smoothly toward the runway instead of jumping instantly downward.
4. Accelerate for takeoff.
5. Raise the tail and rotate normally.
6. Confirm the wheels release as the airplane becomes airborne.
7. Confirm the airplane does not remain pulled toward the runway after liftoff.
8. Fly a low pass without touching down.
9. Confirm being near the runway alone does not activate sticky rollout forces.
10. Land normally.
11. Confirm touchdown damping occurs only after the wheels actually carry weight.

The diagnostics can report both detected and supporting contact internally. Flight and rollout forces use load-bearing wheel contact, not merely a runway entering the fully extended suspension ray.

## Test extreme banks

Begin at a safe altitude and at least 130 knots.

1. Establish a 45-degree bank and turn normally.
2. Increase through 60 degrees.
3. Confirm the airplane still requires pitch and power to maintain altitude.
4. Increase toward 75–85 degrees.
5. Confirm the airplane enters a descending, energy-losing turn rather than immediately falling vertically.
6. Release roll input and recover.
7. Repeat below approximately 55 knots.
8. Confirm the speed-gated protection does not prevent a genuine low-speed stall.

The steep-bank reserve begins near 58 degrees, reaches its configured maximum near 84 degrees, and supplies less than one gravity. It does not auto-level or make extreme banks altitude-neutral.

## Test the dipstick

1. Stop the Merlin.
2. Make sure the engine is not hanging from the hoist.
3. Aim at the dipstick.
4. Press `E`.
5. Confirm the dipstick visibly lifts out.
6. Confirm the oil stain extends near the full mark.
7. Confirm the message reports approximately `20.0/20.0 L`.
8. Press `E` again.
9. Confirm the dipstick reinserts.
10. Start the engine and attempt to pull it.
11. Confirm the interaction is blocked while running.

## Test an oil can

1. Aim at either blue aircraft oil can.
2. Press `E` to pick it up.
3. Press `F` to open the cap.
4. Aim at the Merlin oil filler.
5. Hold `E`.
6. Because a new engine is full, confirm it does not accept extra oil.
7. Press `F` to close the can.
8. Press `E` away from the filler to set it down.
9. Pick the can up and enter the cockpit.
10. Confirm the can is automatically placed on the ground before cockpit entry.

## Inspect component condition

Aim at each component and press `X`:

- Engine block: exact block percentage, available-power percentage, and oil quantity.
- Left or right cover: exact percentage and crack state.
- Any installed spark plug: cylinder number, A/B position, and exact percentage.
- Oil filler: exact oil quantity.

Spark-plug inspection is additive. The existing installation or removal prompt must remain visible beside the `X` inspection prompt.

## Verify normal wear

Normal wear is intentionally slow.

1. Install the complete engine in the P-51.
2. Start it and run at moderate power for several minutes.
3. Stop the engine.
4. Inspect several spark plugs.
5. Confirm the values have changed only slightly.
6. Inspect the block and covers.
7. Confirm a healthy, full-oil engine still reports close to full power.

Each of the 24 plug positions wears independently. Two plugs support each cylinder. Losing condition in one plug reduces that cylinder less than losing both plugs.

## Accelerated visible-damage test

This menu is for validation only and avoids waiting through normal wear rates.

1. Exit Play mode.
2. Select the original engine station, its portable engine root, or an object beneath it.
3. Run **Hanger 51 > Merlin Condition > 3 - Apply Visible Test Damage to Selected Engine**.
4. Enter Play mode.
5. Inspect the block and confirm approximately 42% condition.
6. Inspect spark plugs and confirm approximately 38% condition.
7. Inspect the left cover and confirm it is cracked.
8. Confirm visible dark block damage and plug discoloration.
9. Confirm oil is leaking from the cracked cover.
10. Install the damaged engine in the P-51.
11. Start it and increase power.
12. Confirm fire appears from the cracked cover.
13. Confirm the cockpit reports rough running.
14. Confirm acceleration and available power are substantially reduced.
15. Stop the engine and observe the oil quantity falling because of the crack.

To restore it:

1. Exit Play mode.
2. Select the same engine or its condition owner.
3. Run **Hanger 51 > Merlin Condition > 4 - Restore Selected Engine to New Condition**.

## Verify independent purchased engines

The inactive complete-assembly shipment template is configured by Merlin Condition Step 1.

1. Purchase and unbox a new complete V-1650 after applying the setup.
2. Pull its dipstick and confirm it has its own full 20 L oil supply.
3. Inspect its block, covers, and plugs and confirm new condition.
4. Apply test damage to the original engine only.
5. Enter Play mode again and compare the original engine with a newly purchased engine.
6. Confirm one engine's oil and component condition does not modify the other.

## Standalone build

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat one bank test, one takeoff-release test, one dipstick inspection, one oil-can interaction, and one condition inspection.
