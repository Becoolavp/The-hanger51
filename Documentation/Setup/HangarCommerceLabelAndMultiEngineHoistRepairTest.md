# Hanger 51 Commerce Label and Multi-Engine Hoist Repair Test

## Purpose

This repair corrects the reversed and through-wall commerce labels and allows the existing engine hoist to select purchased V-1650 assemblies instead of remaining permanently connected to the original Merlin station.

## Setup

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Wait for Unity to finish compiling and importing the new world-text shader.
5. Run **Hanger 51 > Shop and Shipping > 3 - Repair Labels and Multi-Engine Hoist**.
6. Run **Hanger 51 > Shop and Shipping > 4 - Validate Labels and Multi-Engine Hoist**.

Expected validation result:

`Shop Step 4 passed. Commerce labels face the correct direction, obey scene depth, and every engine hoist can select the nearest original or purchased Merlin assembly.`

## Label test

1. Enter Play mode.
2. Walk toward the shipment receiving sign from inside the hangar.
3. Confirm the words read normally from left to right.
4. Walk behind a wall, crate, desk, or engine that blocks the sign.
5. Confirm the text disappears behind the blocking object.
6. Purchase any product.
7. Walk to its crate.
8. Confirm the shipping label reads normally from left to right.
9. Walk around the back or opposite side of the crate.
10. Confirm the label does not render through the wood slats or other solid objects.

## Purchased complete-assembly hoist test

1. Purchase **Complete Serviceable V-1650 Assembly**.
2. Unbox the shipment.
3. Confirm the delivered engine shows both covers and all spark plugs.
4. Grab the engine hoist with `E`.
5. Move the hoist until the hook—not merely the hoist frame—is centered above the purchased engine lift point.
6. Confirm the prompt changes to:

   `F: connect hook and lift engine`

7. Press `F`.
8. Confirm the slings attach to the purchased engine.
9. Move the hoist and confirm the purchased engine follows it.
10. Press `F` away from the original stand and confirm the engine lowers at the floor marker.
11. Move the hook above the same purchased engine again.
12. Press `F` and confirm it can be lifted a second time.

## Multiple-engine selection test

1. Leave the original Merlin in its current location.
2. Place the purchased Merlin several meters away.
3. Move the hook above the original engine and press `F`.
4. Confirm the original engine is selected.
5. Place it back down.
6. Move the hook above the purchased engine and press `F`.
7. Confirm the purchased engine is selected.
8. Confirm the hoist never switches engines while one is suspended.

## P-51 installation test

1. Lift the purchased complete engine.
2. Move it to the P-51 engine bay.
3. Confirm the normal aircraft placement highlight appears.
4. Press `F` to lower it into the aircraft.
5. Tighten all four engine-mount bolts.
6. Confirm the engine remains the purchased assembly and retains its complete maintenance state.

## Standalone build

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the label-occlusion test.
5. Purchase and unbox a complete engine.
6. Lift the purchased engine with the hoist.
7. Place it on the floor or in the P-51.
