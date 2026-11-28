# P-51 Portable Cowling and Airframe Refinement Test

## Setup

1. Switch to `agent/merlin-engine-assembly` in GitHub Desktop.
2. Fetch and pull the latest branch.
3. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
4. Exit Play mode.
5. Run **Hanger 51 > P-51 Mustang > 4 - Add Portable Cowling and Refine Airframe**.
6. Confirm the Console reports `P-51 Step 4 complete`.
7. Run **Hanger 51 > P-51 Mustang > 5 - Validate Portable Cowling and Refined Airframe**.
8. Confirm the Console reports `P-51 Step 5 passed`.

## Cowling removal and carry

1. Enter Play mode.
2. Walk to the P-51 nose.
3. Hold `R` on all ten top-cowling screws.
4. Confirm each screw rises and its highlight changes correctly.
5. Hold `R` on the highlighted cowling panel.
6. Confirm the cowling lifts into the Player's hands instead of moving to a service cradle.
7. Confirm no cradle exists beside the aircraft.
8. Walk at least ten meters while carrying the cowling.
9. Confirm the panel follows the camera without colliding with the Player.

## Free placement

1. Aim at the concrete apron and press `E`.
2. Confirm the cowling is placed on the apron at the crosshair location.
3. Walk around it and confirm it remains where it was placed.
4. Aim at the loose cowling and press `E`.
5. Confirm it returns to the Player's hands.
6. Aim at a workbench or another clear surface and press `E`.
7. Confirm it rests at that location rather than snapping to a fixed point.
8. Pick it up again.
9. Repeat once on a different clear floor location.

## Cowling reinstallation

1. Make sure the engine bay is empty, or that an installed engine has all four mount bolts tightened.
2. Pick up the loose cowling.
3. Aim at the highlighted engine opening.
4. Hold `E`.
5. Confirm the panel moves from the Player's hands to the correct installed pose.
6. Confirm all ten screw locations become available.
7. Tighten at least two screws.
8. Confirm the screw shafts remain inside the cowling structure.

## Internal engine-mount bolts

1. Remove the cowling again.
2. Use the hoist to place the engine in the highlighted bay.
3. Inspect all four mount bolts from directly above.
4. Confirm each bolt is positioned over an internal engine-foot rail.
5. Confirm the bolts are arranged as two rear and two forward attachment points.
6. Inspect the aircraft from the left side.
7. Confirm no threaded shaft or bolt head exits the left nose skin.
8. Inspect the aircraft from the right side.
9. Confirm no threaded shaft or bolt head exits the right nose skin.
10. Tighten each bolt with `E`.
11. Confirm each shaft travels vertically downward into its internal saddle.
12. Loosen each bolt with `R`.
13. Confirm each bolt rises vertically and remains inside the open engine bay.

## Airframe cleanup

1. Walk completely around the P-51.
2. Confirm the old cowling cradle is gone.
3. Confirm detached red propeller-tip cubes are gone.
4. Confirm the old floating flap and aileron seam bars are gone.
5. Confirm the old floating tail-stripe blocks are gone.
6. Inspect the wing roots and confirm the new fairings touch the fuselage and wing surfaces.
7. Inspect the tail and confirm the dorsal fillet joins the fin to the fuselage.
8. Inspect the radiator scoop and confirm the transition fairing joins it to the belly.
9. Inspect the propeller and confirm the spinner backplate and four blade-root cuffs remain attached to the rotating assembly.
10. Inspect the windshield structure and confirm its posts connect to the cockpit framing.
11. Inspect the exhaust shrouds and confirm they remain against the nose sides.

## Full service-cycle test

1. Remove and carry the cowling.
2. Place it somewhere clear on the apron.
3. Install the engine with the hoist.
4. Tighten all four internal engine-mount bolts.
5. Pick up the cowling.
6. Reinstall it.
7. Tighten all ten cowling screws.
8. Unscrew all ten screws.
9. Carry and place the cowling somewhere else.
10. Loosen all four engine-mount bolts.
11. Lift the engine out with the hoist.
12. Confirm the engine's current covers, cover bolts, and spark plugs remain unchanged.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Remove and carry the cowling.
5. Place it in two different locations.
6. Pick it back up and reinstall it.
7. Install the engine.
8. Tighten and loosen at least one rear and one forward mount bolt.
9. Confirm no bolt exits the aircraft skin in the standalone build.
10. Walk around the aircraft and confirm the detached generated pieces remain absent.

## Regeneration note

Running **P-51 Step 1** recreates the original generated aircraft hierarchy. After running Step 1 again, rerun Steps 4 and 5 before testing or building.
