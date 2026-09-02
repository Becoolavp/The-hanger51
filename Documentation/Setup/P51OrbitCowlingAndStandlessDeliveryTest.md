# P-51 Orbit Camera, Carried Cowling, and Standless Delivery Test

Use this checklist after pulling `agent/merlin-engine-assembly`.

## Editor setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Shop and Shipping > 9 - Make Complete Assemblies Unpack Without Stands**.
4. Confirm `Shop Step 9 complete` appears in the Console.
5. Run **Hanger 51 > Shop and Shipping > 10 - Validate Standless Complete Assembly Deliveries**.
6. Confirm `Shop Step 10 passed` appears.
7. Run **Hanger 51 > P-51 Mustang > 20 - Refine External Orbit Camera and Cowling Carry Rule**.
8. Confirm `P-51 Step 20 complete` appears.
9. Run **Hanger 51 > P-51 Mustang > 21 - Validate Orbit Camera and Cowling Carry Rule**.
10. Confirm `P-51 Step 21 passed` appears.

Do not rerun Shop Step 1 or any old P-51 generation step.

## External orbit camera

1. Enter Play mode.
2. Enter the P-51 cockpit with `E`.
3. Press `V`.
4. Confirm the camera moves behind and above the aircraft.
5. Move the mouse left and right.
6. Confirm the camera moves around the aircraft instead of rotating the aircraft-facing view in place.
7. Move the mouse up and down.
8. Confirm the camera changes elevation around the aircraft.
9. Orbit to the left side, front, right side, and rear.
10. Confirm the P-51 remains centered and the camera always faces it.
11. Bank the aircraft steeply.
12. Confirm the external camera stays world-up rather than rolling with the cockpit.
13. Fly near the ground or hangar.
14. Confirm the camera moves closer instead of passing through geometry.
15. Press `V` again.
16. Confirm the camera returns exactly to the cockpit anchor.
17. Land, stop, and exit with `E`.
18. Confirm the normal walking camera is restored.

## Carried-cowling requirement

1. Remove the top cowling normally.
2. Place it on the floor with `E`.
3. Walk to the engine opening without carrying it.
4. Confirm the cowling installation highlight is absent.
5. Confirm holding `E` at the opening does not pull the panel across the hangar.
6. Return to the loose cowling.
7. Press `E` to pick it up.
8. Return to the P-51.
9. Confirm the cowling-shaped installation highlight now appears.
10. Hold `E` at the opening.
11. Confirm the carried panel animates into its installed position.
12. Confirm all ten screw targets appear.

When the engine is installed, all four engine-mount bolts must still be tight before the carried cowling can be installed.

## Standless complete-engine delivery

Purchases from an earlier Play session do not persist. Purchase a new complete assembly after running Shop Step 9.

1. Open the parts computer.
2. Purchase **Complete Serviceable V-1650 Assembly**.
3. Walk to the delivered crate.
4. Press `E` to unbox it.
5. Confirm the crate bands retract and the lid opens.
6. Confirm the complete Merlin appears on the receiving floor.
7. Confirm no maintenance stand, casters, rails, braces, or saddles remain after the crate disappears.
8. Confirm no stand-removal prompt appears anywhere.
9. Aim at spark plugs, covers, bolts, and the engine block.
10. Confirm normal maintenance prompts remain available.
11. Move the hoist hook over the engine lift point.
12. Press `F` and confirm the complete engine can be lifted.
13. Move the engine at least 3.5 meters from the shipment position.
14. Confirm the shipment bay becomes available again.
15. Lower the engine on the floor or install it in the P-51.
16. Confirm its full maintenance state remains intact.

## Standalone build

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the orbit-camera toggle, carried-cowling installation, and complete-engine unboxing tests in the standalone build.
