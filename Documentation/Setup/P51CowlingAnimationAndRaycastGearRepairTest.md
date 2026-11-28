# P-51 Cowling Animation and Raycast Ground-Physics Test

This checklist validates the replacement for the temporary cowling box/beacons and the failed rigid-sphere/WheelCollider landing-gear attempts.

## Current setup commands

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > P-51 Mustang > 12 - Restore Cowling Animation and Repair Ground Physics**.
4. Run **Hanger 51 > P-51 Mustang > 13 - Validate Cowling Animation and Ground Physics**.
5. Treat Step 13 as the current validator. Do not rerun P-51 Steps 1, 4, 8, 10, or 11 after Step 12.

Expected validation result:

`P-51 Step 13 passed.`

## Visual cleanup check

1. Enter Play mode.
2. Walk to the open engine bay.
3. Confirm the rectangular raised guide is gone.
4. Confirm all three glowing guide spheres are gone.
5. Confirm only the translucent cowling-shaped placement highlight appears when the cowling can be installed.
6. Confirm no new visible shapes were added to the edited airframe.

## Cowling removal animation

1. Install and tighten the cowling if it is not already installed.
2. Loosen all ten cowling screws with `R`.
3. Aim at the cowling.
4. Hold `R`.
5. Confirm the real cowling panel rises clear of the engine bay.
6. Confirm it tilts slightly while lifting.
7. Confirm it moves into the Player's hands when the hold completes.
8. Release `R` partway through a second removal attempt and confirm the panel returns to its starting position rather than teleporting.

## Cowling installation animation from the Player's hands

1. Carry the cowling near the aircraft.
2. Confirm the cowling-shaped translucent highlight appears at the engine opening.
3. Aim at the highlighted opening.
4. Hold `E`.
5. Confirm the actual panel moves smoothly from the Player's hands to the aircraft.
6. Confirm it finishes exactly at the cowling mount.
7. Confirm all ten screw highlights appear after placement.

## Cowling installation animation from a world position

1. Remove the cowling again.
2. Press `E` to place it on the apron or hangar floor.
3. Walk back to the aircraft without picking it up.
4. Aim at the cowling-shaped opening highlight.
5. Hold `E`.
6. Confirm the loose panel flies from its placed position back to the aircraft.
7. Confirm it does not move toward the deleted service-cradle position.

## Ground-physics diagnostic check

Enter the cockpit with `E`. A second diagnostic panel should appear below the normal flight HUD.

Before starting the engine, confirm:

- `Gear contacts: 3/3 (LRT)`
- `Body dynamic: True`
- `Engine running: False`
- `Throttle command: 0%`

`LRT` means the left main wheel, right main wheel, and tailwheel all have ground contact.

If the airplane is parked on uneven ground, a temporary `2/3` is acceptable while the suspension settles, but the runway test should stabilize at `3/3`.

## Engine and thrust-state check

1. Confirm the Merlin is in the engine bay.
2. Confirm all four aircraft engine-mount bolts are tight.
3. Press `T`.
4. Confirm the diagnostic panel changes to `Engine running: True`.
5. Hold `Q` until approximately 20% throttle.
6. Confirm `Throttle command` increases.
7. Confirm `Forward speed` begins increasing above `0.0 m/s`.
8. Confirm the wheels visually rotate.

Do not continue to full power unless all of these are true:

- Gear contacts are at least `2/3` and normally `3/3`.
- Body dynamic is `True`.
- Engine running is `True`.
- Forward speed increases after brake release.

## Brake-release test

1. Hold `Space`.
2. Set approximately 20% throttle.
3. Confirm forward speed remains close to zero.
4. Release `Space`.
5. Confirm forward speed begins increasing.
6. Hold `Space` again.
7. Confirm forward speed decreases progressively.
8. Confirm the aircraft does not pitch abruptly onto its nose.

## Taxi test

1. Use 15-30% throttle.
2. Tap `A` and confirm the tailwheel steers left.
3. Tap `D` and confirm the tailwheel steers right.
4. Confirm the tire visuals rotate rather than sliding across the runway.
5. Confirm the fuselage, spinner, and propeller remain clear of the pavement.
6. Confirm the diagnostic panel usually remains at `3/3` during slow taxi.

## Full-power acceleration test

1. Align the airplane with the runway.
2. Release `Space`.
3. Increase throttle gradually to 100%.
4. Confirm forward speed rises continuously.
5. Confirm the airplane no longer remains stationary at full throttle.
6. Confirm the two main-wheel contacts remain present during the takeoff roll.
7. Expect the tailwheel contact to disappear as the tail rises.
8. At approximately 80-100 knots, apply gentle `S` input.
9. Confirm the aircraft can rotate and leave the runway.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the cowling animation test.
5. Confirm the box and three spheres are absent.
6. Confirm the diagnostic panel reports gear, Rigidbody, engine, throttle, and speed state.
7. Repeat brake release, taxi, and full-power acceleration.

## Useful failure report

For any remaining no-movement problem, report these five diagnostic lines exactly as shown while at 100% throttle with the brakes released:

1. Gear contacts
2. Forward speed
3. Body dynamic
4. Engine running
5. Throttle command

Also state whether the propeller is spinning and whether the nose, fuselage, or propeller is touching the runway.
