# P-51 Easy Flight, Ground Safety, Emergency Exit, and Cowling Clearance Test

This milestone removes artificial climb during banked turns, strongly damps sideways airborne sliding, changes steep-bank support into descent-only protection, adds a landing-gear hard stop against terrain penetration, permits cockpit exit at any time, and lowers the Merlin oil cap and dipstick for cowling clearance.

## Apply the update

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > P-51 Mustang > 26 - Simplify Flight Handling and Harden Ground Contact**.
4. Run **Hanger 51 > P-51 Mustang > 27 - Validate Easy Flight and Ground Safety**.
5. Run **Hanger 51 > Merlin Condition > 12 - Lower Oil Cap and Dipstick for Cowling Clearance**.
6. Run **Hanger 51 > Merlin Condition > 13 - Validate Final Cowling Clearance**.
7. Save the scene.

Do not rerun older P-51 flight-generation or landing-gear setup steps afterward. If Merlin Condition Step 7 or Step 9 is rerun later, rerun Merlin Condition Steps 12 and 13 afterward.

## Expected flight behavior

- Banking no longer adds a general lift bonus.
- A nose-down aircraft should descend rather than climb merely because it is banked.
- Steep-bank protection begins around 65 degrees and only acts when the descent rate is already worse than approximately 8.5 m/s.
- The protection cannot create a climb from level flight.
- Sideways velocity is damped continuously, so the aircraft should follow its nose instead of drifting sideways like it is on ice.
- The flight model remains intentionally forgiving rather than simulator-level.

## Turn-path test

1. Take off and climb to a safe altitude.
2. Stabilize around 120–150 knots.
3. Bank 30 degrees with neutral pitch.
4. Confirm the aircraft turns without gaining a large amount of altitude.
5. Bank 45–60 degrees.
6. Point the nose slightly below the horizon.
7. Confirm altitude begins decreasing rather than increasing.
8. Release roll input and observe the velocity path.
9. Confirm the aircraft follows the nose instead of continuing sideways for an extended period.
10. Repeat at approximately 70–80 degrees of bank.
11. Confirm the airplane can descend rapidly but does not instantly enter a vertical fall while carrying adequate airspeed.
12. Slow below normal approach speed and confirm a genuine low-speed stall is still possible.

## Hard-landing and ground-penetration test

Begin with a normal landing before intentionally increasing touchdown firmness.

1. Land normally and confirm suspension movement remains smooth.
2. Repeat with a firmer touchdown.
3. Confirm the tires and wheel anchors remain above the runway surface.
4. Confirm the aircraft does not become permanently stuck below the map.
5. Watch the loaded-wheel diagnostics and confirm loaded contact appears only after the wheels take weight.
6. Perform a rejected landing or early liftoff.
7. Confirm the ground guard does not pull the aircraft back down after the wheels are clear.
8. Apply takeoff power and confirm the aircraft can leave the runway normally.

The hard-stop guard runs after the ordinary suspension and touchdown damping. It only corrects a wheel anchor that has already crossed its minimum physical clearance.

## Emergency-exit test

1. Enter the cockpit while parked.
2. Press `E` and confirm normal exit still works.
3. Re-enter and begin taxiing.
4. Press `E` while the aircraft is moving.
5. Confirm the cockpit releases immediately.
6. Confirm the player is placed beside or above the nearby ground surface rather than trapped inside the aircraft.
7. Re-enter, take off, and climb to a safe testing height.
8. Press `E` while airborne.
9. Confirm the player exits without the previous landing/airspeed restriction.
10. Confirm the aircraft does not freeze in mid-air; its existing motion continues with the engine shut down.

## Oil-service cowling-clearance test

1. Inspect the Merlin with its cowling removed.
2. Confirm the yellow dipstick handle remains visible and selectable.
3. Confirm the oil cap remains visible and selectable.
4. Pull and reinsert the dipstick with `E`.
5. Inspect the filler with `X`.
6. Install the engine in the P-51.
7. Carry and install the cowling.
8. Walk around the nose and inspect it from cockpit and external views.
9. Confirm neither the oil cap nor the dipstick handle protrudes through the cowling.
10. Remove the cowling again and confirm both service items remain accessible.
11. Purchase and unbox a new complete engine.
12. Confirm the purchased engine inherited the same lower, smaller hardware.

## Regression checks

- `A` and `D` still control roll.
- Left and right arrows still control rudder.
- `V` still toggles the external orbit camera.
- The external camera still faces the airplane.
- The aircraft does not gain altitude merely from banking.
- The aircraft does not return to the previous extreme-bank vertical-drop behavior.
- The hoist remains able to approach and lift the Merlin.
- Dipstick and oil-filler interactions still use trigger-only colliders.
- Normal takeoff is not held to the runway by the hard-stop guard.
- Emergency exit works even when the landing state is incorrect or the aircraft has partially sunk.
