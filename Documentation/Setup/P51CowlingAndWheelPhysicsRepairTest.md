# P-51 Cowling Reinstall and Rolling-Wheel Repair Test

This test covers the repair added after the first P-51 flight test exposed two issues:

- The top-cowling opening stopped highlighting and could not accept the panel after the Merlin was fully mounted.
- The original rigid sphere wheel contacts generated enough static friction to prevent the aircraft from rolling, while allowing the nose to pitch forward.

## Required branch

`agent/merlin-engine-assembly`

## Scene

`Assets/_Project/Scenes/FirstPersonMovementTest.unity`

## Install the repair

1. Exit Play mode.
2. Pull the latest branch revision.
3. Allow Unity to compile.
4. Clear the Console.
5. Do not rerun P-51 Step 1, Step 4, or Step 8.
6. Select **Hanger 51 > P-51 Mustang > 10 - Repair Cowling Reinstall and Add Rolling Wheels**.
7. Confirm the Console reports `P-51 Step 10 complete`.
8. Select **Hanger 51 > P-51 Mustang > 11 - Validate Cowling Reinstall and Rolling Wheels**.
9. Confirm the Console reports `P-51 Step 11 passed`.

Step 10 is repeatable. It replaces only the reinstall guide and wheel-physics components.

## Cowling reinstall test with a mounted engine

1. Enter Play mode.
2. Confirm the Merlin is inside the P-51 engine bay.
3. Tighten all four aircraft engine-mount bolts.
4. Leave the top cowling removed.
5. Confirm a raised rectangular yellow/orange guide appears above the open engine bay.
6. Confirm three raised placement beacons appear along the center of the opening.
7. Carry the cowling and aim at the raised guide.
8. Hold `E`.
9. Confirm the cowling snaps into the installed pose.
10. Confirm the ten cowling-screw highlights appear.
11. Tighten at least two screws.

## Reinstall from a freely placed panel

1. Remove the cowling again.
2. Place it on the runway or hangar floor with `E`.
3. Leave it resting there rather than carrying it.
4. Return to the aircraft nose.
5. Confirm the raised reinstall guide is still visible.
6. Aim at the opening and hold `E`.
7. Confirm the loose panel snaps from its placed location into the installed pose.
8. Confirm the panel does not teleport to an obsolete service-cradle location during the hold.

## Mount-bolt sequencing test

1. Remove the cowling.
2. Loosen one aircraft engine-mount bolt.
3. Confirm the raised cowling guide disappears while the engine is not fully secured.
4. Tighten the loose mount bolt.
5. Confirm the raised cowling guide returns.
6. Reinstall the cowling.

## Static landing-gear inspection

While outside Play mode, expand:

`P-51D Mustang Test Aircraft > P-51 Flight Landing Gear Colliders`

Confirm:

- `Left Main Wheel Physics` has a `WheelCollider`.
- `Right Main Wheel Physics` has a `WheelCollider`.
- `Tailwheel Physics` has a `WheelCollider`.
- None of those three objects has a `SphereCollider`.
- The aircraft root has `P51WheelLandingGear`.
- The Rigidbody center of mass is approximately `(0, 0.96, -0.72)`.

## Engine-off rolling test

1. Enter Play mode.
2. Enter the cockpit using `E`.
3. Leave the engine stopped.
4. Confirm the aircraft rests on both main wheels and the tailwheel.
5. Confirm it does not fall onto the spinner or nose.
6. Hold `Space` and confirm the airplane remains held by the wheel brakes.
7. Release `Space`.

## Taxi test

1. Start the installed Merlin with `T`.
2. Hold `Q` until throttle reaches approximately 15%.
3. Release the wheel brakes.
4. Confirm the airplane begins rolling forward.
5. Observe both visible main tires and confirm they rotate while the aircraft moves.
6. Tap `A` and confirm the tailwheel steers the aircraft left at low speed.
7. Tap `D` and confirm it steers right.
8. Hold `Space` and confirm the WheelColliders brake the aircraft.
9. Confirm braking does not immediately flip the aircraft onto its nose.

## Full-power ground run

1. Align the P-51 with the runway centerline.
2. Start the Merlin.
3. Release `Space`.
4. Increase throttle gradually to 100% using `Q`.
5. Confirm ground speed and airspeed increase continuously.
6. Confirm the aircraft does not remain stuck at zero speed.
7. Confirm the main wheels visibly rotate faster as speed increases.
8. Use small `A` and `D` inputs for centerline correction.
9. At approximately 80–100 knots, apply gentle `S` input.
10. Confirm the aircraft can rotate and lift off.

## Landing test

1. Return to the runway at a controlled speed.
2. Touch down on the main wheels.
3. Allow the tailwheel to settle.
4. Reduce throttle with `Z`.
5. Apply gradual wheel braking with `Space`.
6. Confirm the wheels continue rolling until braking slows them.
7. Confirm the aircraft remains upright and does not pitch onto the propeller.

## Standalone Build and Run

1. Select **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Select **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Select **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the cowling reinstall test with all four engine-mount bolts tight.
5. Repeat the low-throttle taxi test.
6. Repeat the full-power acceleration test.
7. Test braking after landing or after a high-speed ground run.

## Report useful measurements

For further tuning, record:

- Throttle percentage when the airplane first begins moving.
- Time required to reach 60 knots.
- Airspeed at rotation.
- Whether the tail rises naturally before takeoff.
- Whether either wing drops during the ground run.
- Whether braking causes bounce, skid, or nose-over.
