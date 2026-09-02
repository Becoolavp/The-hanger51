# P-51 Flight Controls and Runway Test

This checklist validates the first flyable P-51 milestone without rebuilding or replacing the current edited airplane model.

## Install

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Confirm the current P-51, portable Merlin, and engine receiver are still present.
5. Run **Hanger 51 > P-51 Mustang > 8 - Add Flight Controls and Test Runway**.
6. Wait for Unity to finish saving and refreshing assets.
7. Run **Hanger 51 > P-51 Mustang > 9 - Validate Flight Controls and Test Runway**.
8. Confirm the Console reports `P-51 Step 9 passed`.

Step 8 does not run P-51 Step 1 or Step 4. It does not recreate any visual shapes removed from the airplane.

## Controls

- `E`: enter or exit the cockpit.
- `T`: start or stop the Merlin.
- `Q`: increase throttle.
- `Z`: decrease throttle.
- `W`: pitch the nose down.
- `S`: pitch the nose up.
- `A`: roll left.
- `D`: roll right.
- `Space`: hold wheel brakes.
- Mouse: look around the cockpit.
- `Escape`: release the mouse cursor.
- Left click: recapture the mouse cursor.

## Engine interlock

1. Enter the cockpit before installing the engine.
2. Press `T`.
3. Confirm the engine does not start.
4. Confirm the cockpit message says the Merlin must be lowered into the bay and all four mount bolts tightened.
5. Exit the cockpit with `E`.
6. Install the Merlin with the hoist.
7. Tighten all four P-51 engine-mount bolts.
8. Re-enter the cockpit.
9. Press `T`.
10. Confirm the propeller begins spinning at idle.

The starter only accepts an engine that is positioned in the aircraft receiver and secured by all four mount bolts.

## Cockpit entry and exit

1. Approach the cockpit from either side.
2. Aim toward the seat area.
3. Confirm the prompt reads `E: enter P-51 cockpit`.
4. Press `E`.
5. Confirm the view moves to the pilot eye position.
6. Move the mouse and confirm the view can look around while remaining attached to aircraft pitch and roll.
7. Confirm inventory, maintenance, and hoist interaction prompts do not operate from the cockpit.
8. While stopped on the runway, press `E`.
9. Confirm the Player appears beside the left side of the cockpit.
10. Re-enter the cockpit.
11. Begin rolling faster than walking speed.
12. Press `E`.
13. Confirm exit is blocked until the aircraft stops.

## Propeller and throttle

1. Start the engine with `T`.
2. Observe the propeller at idle.
3. Hold `Q` for several seconds.
4. Confirm the throttle percentage rises on the cockpit HUD.
5. Confirm propeller rotation visibly increases with throttle.
6. Hold `Z`.
7. Confirm throttle and propeller speed decrease.
8. Press `T` again.
9. Confirm the engine stops and throttle returns to zero.

## Taxi and ground handling

1. Start the engine.
2. Hold `Space` and raise throttle slightly with `Q`.
3. Confirm the airplane remains mostly stationary while the brakes are held.
4. Release `Space`.
5. Confirm the aircraft begins rolling forward.
6. Use short `A` and `D` inputs while moving slowly.
7. Confirm the taildragger turns mildly on the runway without instantly spinning.
8. Hold `Z` to idle and use `Space` to stop.
9. Inspect all three landing-gear contact points for sinking, excessive bouncing, or floating.

## First takeoff

1. Align with the runway centerline.
2. Start the engine.
3. Hold `Q` until throttle reaches 100%.
4. Use short `A` and `D` inputs to remain near the centerline.
5. Allow the aircraft to accelerate through approximately 80 to 100 knots.
6. Apply gentle `S` input to pitch up.
7. Confirm the main wheels leave the runway.
8. Release `S` after establishing a shallow climb.
9. Use `A` and `D` for gentle bank changes.
10. Use `W` to lower the nose and `S` to raise it.
11. Confirm control response becomes stronger as airspeed increases.
12. Reduce throttle with `Z` and confirm acceleration decreases.

The first-pass model includes reduced lift and control authority at low airspeed. Abrupt pitch input near the stall should produce settling instead of unlimited lift.

## Landing

1. Reduce throttle with `Z` before entering the runway area.
2. Use shallow banks and small pitch corrections.
3. Keep enough airspeed to avoid a deep stall.
4. Align with the runway.
5. Reduce throttle toward idle during the flare.
6. Touch down on the main wheels and allow the tail to settle.
7. Hold `Space` for braking after touchdown.
8. Stop before pressing `E` to leave the cockpit.

## Visual runway inspection

Confirm the runway contains:

- A continuous asphalt collision surface.
- Grass shoulder strips on both sides.
- White edge lines.
- Dashed centerline markings.
- Threshold stripes at both ends.
- Touchdown-zone bars.
- Runway edge lights attached to both sides.

Confirm no runway markings or lights are visibly floating far above the pavement.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the engine-start interlock test.
5. Enter the cockpit.
6. Start the installed Merlin.
7. Taxi, take off, make one gentle turn, and land.
8. Confirm the propeller, HUD, controls, camera handoff, runway collision, and exit restriction behave the same as in the Editor.

## First-pass tuning notes

The flight model is intended to establish the complete gameplay loop before detailed aerodynamic tuning. Record any of the following with approximate airspeed and throttle when possible:

- Takeoff occurs too early or too late.
- Pitch direction is reversed.
- Roll direction is reversed.
- Controls are too strong or too weak.
- Aircraft cannot remain on the runway.
- Tailwheel or main wheels bounce excessively.
- Aircraft stalls too abruptly or does not stall.
- Engine thrust is insufficient or excessive.
- Landing gear sinks into the runway.

Do not merge PR #3 until the Unity Editor and standalone flight checks have passed.
