# Engine Hoist Movement and Visual Polish Test

## Setup

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Run **Hanger 51 > Engine Hoist > 3 - Fix Forward Movement and Add Realistic Detail**.
5. Confirm the Console reports `Engine Hoist Step 3 complete`.
6. Run **Hanger 51 > Engine Hoist > 4 - Validate Movement and Realistic Detail**.
7. Confirm the Console reports `Engine Hoist Step 4 passed`.

## Forward-movement test

1. Enter Play mode.
2. Aim at the rear push handles.
3. Press `E` to grab the hoist.
4. Hold the normal forward movement input.
5. Confirm the Player walks forward and the hoist moves ahead of the Player.
6. Continue forward for at least 10 meters.
7. Confirm the Player does not stop against an invisible box near the handles.
8. Walk backward while still controlling the hoist.
9. Strafe left and right.
10. Turn through at least 180 degrees while walking.
11. Press `E` to release the hoist.
12. Aim at the handles again and confirm the interaction remains available.
13. Grab the hoist a second time and confirm forward movement still works.

## Loaded-hoist movement test

1. Place an engine block on the stand.
2. Move the hook over the engine.
3. Press `F` to lift it.
4. Grab the loaded hoist with `E`.
5. Walk forward for at least 10 meters.
6. Confirm the Player can still move forward while loaded.
7. Turn left and right while moving.
8. Confirm the engine remains beneath the hook.
9. Release the hoist and confirm it remains stationary.
10. Re-grab it and continue moving.

## Visual-detail inspection

1. Inspect the mast base and confirm it has gussets and four visible fasteners.
2. Inspect the boom pivot and confirm the cross pin has visible end caps.
3. Inspect the black extension and confirm it has adjustment holes and a locking pin.
4. Inspect the hydraulic jack and confirm it has a hose, fittings, release valve, and pump-handle pivot.
5. Inspect the casters and confirm they have hubs, axle caps, swivel bearings, and brake tabs.
6. Confirm the mast includes a yellow load-rating plate.
7. Confirm the added hardware has no gameplay colliders.

## Hook and chain inspection

1. Inspect the load chain from the side.
2. Confirm it is made from separate alternating links rather than one solid rod.
3. Inspect the hook closely.
4. Confirm the hook has a visible eye and dark center opening.
5. Confirm it has a swivel neck beneath the eye.
6. Confirm the body forms a continuous J-shaped curve.
7. Confirm the curved section tapers toward the tip.
8. Confirm the tip is rounded.
9. Confirm a safety latch crosses the hook throat.
10. Lift an engine and confirm the twin slings still connect to the hook area correctly.

## Regression checks

1. Confirm `E` still grabs and releases the hoist.
2. Confirm `F` still lifts and lowers the engine.
3. Confirm the yellow placement marker still appears while loaded.
4. Confirm the engine can still return to the maintenance stand.
5. Confirm partial assembly state is preserved.
6. Confirm maintenance highlights return after the engine is placed.
7. Run **Hanger 51 > Engine Hoist > 2 - Validate Portable Engine Hoist** and confirm it still passes.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Grab the empty hoist and walk forward.
5. Lift an engine and walk forward while loaded.
6. Inspect the chain and hook.
7. Place the engine on the floor.
8. Return it to the stand.

## Current milestone limits

- The hoist still follows the Player rather than using fully simulated caster-wheel physics.
- The boom height and extension remain fixed.
- The individual chain links are visual and use the current fixed boom-to-hook distance.
