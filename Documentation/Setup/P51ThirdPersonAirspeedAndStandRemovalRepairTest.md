# P-51 Third-Person Camera, Airspeed Warning, and Delivered Stand Repair Test

## Setup

1. Exit Play mode.
2. Pull `agent/merlin-engine-assembly`.
3. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
4. Wait for Unity compilation to finish.
5. Run **Hanger 51 > Shop and Shipping > 7 - Repair Delivered Stand Removal Interaction**.
6. Run **Hanger 51 > Shop and Shipping > 8 - Validate Delivered Stand Removal Interaction**.
7. Run **Hanger 51 > P-51 Mustang > 18 - Add Third-Person Camera and Airspeed Warnings**.
8. Run **Hanger 51 > P-51 Mustang > 19 - Validate Third-Person Camera and Airspeed Warnings**.

Expected validation messages:

- `Shop Step 8 passed`
- `P-51 Step 19 passed`

Do not rerun Shop Step 1 or the older P-51 generation steps.

## Third-person camera test

1. Enter Play mode.
2. Enter the P-51 cockpit with `E`.
3. Confirm the top-right indicator reads `VIEW: COCKPIT [V]`.
4. Press `V`.
5. Confirm the camera moves behind and above the P-51.
6. Confirm the indicator changes to `VIEW: EXTERNAL [V]`.
7. Move the mouse left, right, up, and down.
8. Confirm the camera orbits around the aircraft.
9. Bank the aircraft and confirm the outside horizon remains stabilized.
10. Fly near the hangar or terrain and confirm the camera moves closer instead of passing through the obstacle.
11. Press `V` again.
12. Confirm the camera returns to the cockpit eye point.
13. Land, stop, and exit with `E`.
14. Confirm the normal walking camera is restored.

## Airspeed warning test

The additional airspeed display is placed beside the existing cockpit HUD.

1. While parked, confirm the number is gray and reads `GROUND`.
2. Take off and accelerate above approximately 100 knots.
3. Confirm the number is green and reads `SAFE`.
4. Reduce speed gradually.
5. Confirm the number transitions through:
   - Green: safe
   - Yellow: caution
   - Orange: low speed
   - Red: stall risk
6. Establish a 50-60 degree bank.
7. Confirm the display shows the approximate bank angle.
8. Confirm the caution colors appear at a higher airspeed during the steep bank than during wings-level flight.
9. Recover to wings level and confirm the thresholds reduce again.

## Delivered stand removal test

Shop Step 7 modifies the complete-engine shipment template. A complete assembly purchased before running Step 7 does not contain the repaired stand target. Purchase a new assembly for this test.

1. Purchase and unbox a new **Complete Serviceable V-1650 Assembly**.
2. Aim at several stand parts while the engine is still installed:
   - Base rail
   - Vertical post
   - Diagonal brace
   - Engine saddle
   - Caster wheel
3. Confirm the prompt explains that the engine must be lifted off first.
4. Aim directly at a spark plug, cover, or engine component.
5. Confirm the normal engine-maintenance interaction appears instead of the stand-removal prompt.
6. Move the hoist hook over the delivered engine and press `F`.
7. Move the engine at least 2.6 meters away.
8. Lower the engine onto the floor or install it in the P-51.
9. Return to the empty stand.
10. Aim at any visible rail, post, brace, saddle, or caster.
11. Confirm the prompt reads `Hold R to dismantle and remove empty delivered engine stand`.
12. Hold `R` until the progress reaches 100%.
13. Confirm all visible stand geometry disappears.
14. Confirm its collision disappears.
15. Confirm the shipment bay becomes available again.
16. Return to the moved engine and confirm its maintenance state and hoist compatibility remain intact.

## Standalone build

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the camera toggle, airspeed colors, and delivered-stand removal tests.
