# P-51 Tailwheel Tow Bar Test

## Purpose

This checklist verifies the portable hand-operated tow bar added by P-51 Steps 14 and 15.

The tow bar is intended only for slow manual positioning on the paved apron and runway. It keeps the aircraft Rigidbody kinematic while connected and moved, so it does not compete with the flight or raycast-landing-gear physics.

## Setup

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Run **Hanger 51 > P-51 Mustang > 14 - Add Tailwheel Tow Bar**.
5. Run **Hanger 51 > P-51 Mustang > 15 - Validate Tailwheel Tow Bar**.
6. Confirm there are no red Console errors.

Do not rerun older P-51 generation or wheel-repair steps. Step 14 only adds or replaces the tow bar.

## Controls

- `E`: grab or release the tow-bar handle.
- `F`: connect or disconnect the tow bar at the P-51 tailwheel.
- Normal Player movement: move and turn the tow bar while holding it.

## Visual inspection

Confirm the tow bar includes:

- Yellow twin A-frame drawbars
- Adjustable square-tube handle section
- Silver telescoping lock pin
- Black retaining knob
- Padded left and right tailwheel jaws
- Jaw pivot bushings and lock pins
- Clamp-release lever
- T-handle with two rubber grips
- Two small transport wheels
- Warning plate and fasteners
- Safety-chain detail

There should be no floating guidance boxes or spheres.

## Loose tow-bar handling

1. Enter Play mode.
2. Walk to the tow bar beside the aircraft tail.
3. Aim at the T-handle.
4. Press `E`.
5. Confirm the prompt says the tow bar is in your hands.
6. Walk forward, backward, left, and right.
7. Turn while walking.
8. Confirm the bar follows on its two small transport wheels.
9. Confirm it remains low to the pavement rather than floating at chest height.
10. Press `E` to set it down.
11. Aim at the handle and press `E` again.

## Tailwheel connection

1. While holding the tow bar, face the P-51 tailwheel.
2. Move the yellow padded fork toward the wheel.
3. When the prompt mentions the tailwheel, press `F`.
4. Confirm the tow bar snaps to the tailwheel axle area.
5. Confirm the left and right padded jaws close inward.
6. Confirm the tow bar remains connected when you press `E` to release the handle.
7. Press `E` again to grab the connected handle.

The tow bar should refuse to connect when:

- Someone is in the cockpit
- The Merlin is running
- The airplane is not resting on at least two landing-gear points
- The tow-head fork is too far from the tailwheel

## Aircraft movement

1. Connect the tow bar.
2. Grab the handle with `E`.
3. Stand behind the tail and face toward the aircraft nose.
4. Walk backward slowly to pull the aircraft rearward.
5. Walk forward slowly to push it forward.
6. Turn left while walking.
7. Confirm the airplane yaws left around the tailwheel connection.
8. Turn right and confirm the opposite movement.
9. Reposition the airplane near the runway centerline.
10. Confirm the engine, cowling, mount bolts, and all aircraft parts move with the airframe.
11. Confirm the Player can stop moving and the aircraft remains in its new position.

The system limits manual towing speed and yaw rate. It is not intended for fast movement or towing during flight.

## Handle release while connected

1. Stop walking.
2. Press `E`.
3. Confirm the handle is released.
4. Confirm the tow bar remains attached to the tailwheel.
5. Walk away and return.
6. Aim at the handle and press `E`.
7. Confirm towing resumes.

## Cockpit interlock

1. Leave the tow bar connected.
2. Release its handle.
3. Walk to the cockpit.
4. Press `E` to enter.
5. Confirm entry is refused with a message telling you to disconnect the tow bar.
6. Return to the tow bar.
7. Press `F` to disconnect it.
8. Walk back to the cockpit.
9. Confirm cockpit entry works normally.

## Disconnect and park the tow bar

1. Stop the aircraft completely.
2. Press `F` while the tow bar is attached.
3. Confirm the padded jaws open.
4. Confirm the tow bar remains in your hands when disconnected while held.
5. Move it clear of the tailwheel.
6. Press `E` to place it beside the hangar or apron.

## Engine-state preservation

Repeat one towing test with a fully installed Merlin:

1. Install and bolt down the engine.
2. Install the top cowling.
3. Connect the tow bar.
4. Move the P-51 at least 15 meters.
5. Disconnect the tow bar.
6. Remove the cowling.
7. Confirm the Merlin remains aligned with the engine mounts.
8. Confirm all four aircraft mount-bolt states remain unchanged.
9. Confirm the engine assembly state remains unchanged.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Grab and move the loose tow bar.
5. Connect it to the tailwheel.
6. Reposition the aircraft.
7. Release and re-grab the connected handle.
8. Verify the cockpit interlock.
9. Disconnect and park the tow bar.
10. Enter the cockpit and continue the normal taxi/flight test.

## Current milestone limits

- The tow bar uses controlled kinematic ground repositioning rather than a fully simulated flexible tow joint.
- The airplane remains aligned with the tow-bar direction instead of allowing unrestricted tow-head articulation.
- The system is intended for flat apron and runway surfaces.
- Collision avoidance around buildings and other equipment remains the Player's responsibility.
