# Portable Engine Hoist Test

## Setup

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Run **Hanger 51 > Engine Hoist > 1 - Install Portable Engine Hoist**.
5. Confirm the Console reports `Engine Hoist Step 1 complete`.
6. Run **Hanger 51 > Engine Hoist > 2 - Validate Portable Engine Hoist**.
7. Confirm the Console reports `Engine Hoist Step 2 passed`.

## Controls

- Aim at the hoist handles and press `E` to grab or release the hoist.
- Walk normally while holding the hoist to push it around the hangar.
- Position the boom hook above the engine lift point and press `F` to connect the slings and lift the engine.
- Move the loaded hoist to the desired location.
- Press `F` again to lower the engine onto the yellow placement marker.
- Move the hook back over the maintenance stand and press `F` to snap the engine back onto the stand.

## Bare-engine test

1. Enter Play mode.
2. Pick up and place the engine block on the maintenance stand.
3. Do not install either cylinder cover.
4. Aim at the hoist handles and press `E`.
5. Walk until the hook is above the engine.
6. Press `F`.
7. Confirm the twin slings appear and the bare engine rises clear of the stand.
8. Walk the loaded hoist several meters.
9. Confirm the engine follows beneath the hook without the stand moving.
10. Press `F`.
11. Confirm the engine lowers to the yellow floor marker.
12. Confirm the engine remains bare and no inventory item is created or consumed.

## Partially assembled test

1. Return the engine to the maintenance stand.
2. Install one cylinder cover.
3. Tighten three of its six bolts.
4. Leave the other cover uninstalled.
5. Lift the engine with the hoist.
6. Move it to another part of the hangar.
7. Lower it onto the floor.
8. Confirm the installed cover remains installed.
9. Confirm exactly three bolts remain tightened.
10. Confirm the uninstalled cover remains absent.
11. Continue installing or removing parts on the placed engine.

## Complete-engine test

1. Install both covers, all 12 bolts, and all 24 spark plugs.
2. Lift the completed engine.
3. Confirm every visible part moves with the engine.
4. Release the hoist with `E` while the engine is suspended.
5. Confirm the hoist and engine remain stationary.
6. Grab the hoist again with `E`.
7. Move it to a clear floor location.
8. Press `F` and confirm the complete engine lowers without losing any parts.

## Return-to-stand test

1. Lift an engine in any assembly state.
2. Push the hoist until the hook is over the maintenance stand.
3. Confirm the yellow marker moves to the stand location.
4. Press `F`.
5. Confirm the engine snaps back to its exact stand pose.
6. Confirm assembly interactions work normally again.
7. Confirm the engine state is unchanged.

## Visibility and interaction checks

1. Confirm the shop crane has two black base legs, six casters, a red mast, red main boom, black telescoping extension, hydraulic ram, pump handle, chain, hook, and twin slings.
2. Confirm the placement marker appears only while carrying an engine.
3. Confirm cover, bolt, and spark-plug highlights are hidden while the engine is suspended.
4. Confirm the highlights return after the engine is placed.
5. Confirm the stand remains behind when the engine is lifted.
6. Confirm the Player can release and re-grab a loaded hoist.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat one bare or partial lift.
5. Move the loaded hoist.
6. Place the engine on the floor.
7. Return it to the stand.
8. Confirm the state is preserved in the standalone build.

## Known milestone limits

- The hoist currently follows the Player rather than using simulated wheel physics.
- The boom height and extension are fixed for this milestone.
- Floor placement uses the ground below the hook.
- A later aircraft engine-mount receiver can use the same portable engine root and hoist placement flow.
