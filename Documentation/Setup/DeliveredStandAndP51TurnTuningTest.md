# Delivered Engine Stand and P-51 Turn Tuning Test

## Apply the shop repair

1. Exit Play mode.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Run **Hanger 51 > Shop and Shipping > 5 - Repair Chair and Add Removable Delivered Stands**.
4. Run **Hanger 51 > Shop and Shipping > 6 - Validate Chair and Removable Delivered Stands**.
5. Confirm `Shop Step 6 passed`.

## Apply the flight tuning

1. Run **Hanger 51 > P-51 Mustang > 16 - Tune Sustained Turning and Stall Behavior**.
2. Run **Hanger 51 > P-51 Mustang > 17 - Validate Sustained Turning and Stall Behavior**.
3. Confirm `P-51 Step 17 passed`.

## Chair check

1. Inspect the shop desk in Scene view.
2. Confirm the chair seat faces the keyboard and monitor.
3. Confirm the chair back is on the side away from the desk.

## Delivered stand-removal test

1. Enter Play mode.
2. Purchase and unbox a **Complete Serviceable V-1650 Assembly**.
3. Bring the hoist hook over the delivered engine.
4. Press `F` to lift the engine.
5. Move the hoist away from the delivered stand.
6. Lower the engine onto the floor at least 2.6 meters away.
7. Return to the empty delivered stand.
8. Aim at the front cross rail.
9. Confirm the prompt says `Hold R to dismantle and remove empty delivered engine stand`.
10. Hold `R` until the interaction completes.
11. Confirm the visible stand, casters, braces, and stand collision disappear.
12. Confirm the engine remains visible and serviceable.
13. Bring the hoist back to the engine and confirm it can still be lifted.
14. Confirm the original permanent engine stand remains unchanged.

The stand cannot be removed while the engine is suspended or still sitting on it. The delivered engine must be placed clear of the stand first.

## Sustained-turn test

1. Install and secure the Merlin in the P-51.
2. Take off and climb to a safe altitude.
3. Accelerate to approximately 120–150 knots.
4. Bank approximately 30 degrees and apply enough `S` input to hold altitude.
5. Complete a full 360-degree turn.
6. Confirm the nose follows the bank without a large skid.
7. Confirm airspeed decreases gradually rather than collapsing immediately.
8. Repeat at approximately 45 degrees of bank.
9. Confirm the airplane can sustain the turn with reasonable pitch and throttle management.
10. Repeat at approximately 60 degrees of bank with full or high power.
11. Confirm the aircraft still requires additional pitch and throttle, but does not enter an abrupt stall merely from banking.
12. Reduce airspeed deliberately below approximately 60 knots while holding excessive pitch.
13. Confirm the aircraft can still stall at genuinely low speed.

## Expected tuning behavior

- Full-stall lift reduction begins much lower than before.
- Lift recovers by approximately 64 knots instead of remaining reduced to roughly 84 knots.
- Induced and sideslip drag are lower in normal turns.
- Control authority reaches full strength sooner.
- Banked flight receives partial load-factor support, not automatic altitude hold.
- Mild coordinated yaw helps the nose follow the bank with keyboard-only controls.
- The pilot still needs throttle and pitch for steep or prolonged turns.

## Standalone build

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat one delivered-stand removal and one 45-degree sustained turn.
