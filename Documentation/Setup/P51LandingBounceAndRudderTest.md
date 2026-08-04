# P-51 Landing, Bounce, and Rudder Test

Use this checklist after running P-51 Steps 22 and 23.

## Setup

1. Exit Play mode.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Run **Hanger 51 > P-51 Mustang > 22 - Tune Landing, Bounce, and Rudder Controls**.
4. Run **Hanger 51 > P-51 Mustang > 23 - Validate Landing, Bounce, and Rudder Controls**.
5. Confirm the Console reports `P-51 Step 23 passed`.

Do not rerun older P-51 generation or flight setup steps afterward.

## Rudder test

1. Enter the P-51 and take off normally.
2. Stabilize around 120 to 150 knots.
3. Hold the left arrow briefly.
4. Confirm the nose yaws left without commanding a large roll.
5. Hold the right arrow briefly.
6. Confirm the nose yaws right.
7. Bank 30 to 45 degrees with `A` or `D`.
8. Use a small amount of matching arrow-key rudder.
9. Confirm the nose follows the turn more cleanly.
10. Release the arrow and confirm the yaw input stops.

`A/D` remain roll controls. The left/right arrows are rudder controls.

## Approach test

1. Begin a long final around 110 knots.
2. Reduce throttle gradually below 40 percent.
3. Confirm the airplane decelerates more predictably instead of floating indefinitely.
4. Stabilize between approximately 90 and 105 knots.
5. Use small pitch corrections rather than holding full aft input.
6. Use arrow-key rudder to correct minor runway alignment errors.
7. Confirm the rudder remains controllable without violently swinging the airplane.

## Normal touchdown test

1. Cross the runway threshold around 85 to 95 knots.
2. Reduce throttle toward idle.
3. Flare gently and allow the main wheels to contact first or perform a shallow three-point landing.
4. Confirm the initial downward impact is absorbed.
5. Confirm the airplane does not immediately spring several feet back into the air.
6. Keep the airplane aligned with small arrow-key rudder corrections.
7. Let the tail settle naturally.
8. Apply brakes gradually after the airplane is firmly rolling on the runway.

## Bounce test

1. Repeat the landing with a slightly firmer touchdown.
2. Confirm a small skip is still possible.
3. Confirm the rebound is strongly damped rather than developing into repeated large bounces.
4. Confirm pitch and roll oscillations settle quickly after two or three wheels are touching.
5. Confirm the airplane stays on the runway at low throttle.

## Takeoff regression test

1. Stop on the runway.
2. Increase throttle normally.
3. Confirm the airplane accelerates without feeling pinned to the ground.
4. Raise the tail and rotate normally.
5. Confirm touchdown adhesion disappears as power and positive climb increase.
6. Confirm the previous sustained-turn improvement remains usable.

## Standalone build

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the rudder, approach, touchdown, bounce, and takeoff-regression checks.

Record the approximate approach speed, touchdown speed, throttle setting, and number or height of any remaining bounces before making another tuning pass.
