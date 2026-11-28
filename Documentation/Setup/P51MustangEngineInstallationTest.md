# P-51 Mustang Engine Installation Test

## Setup

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Confirm the current portable hoist and V-1650 engine system are already generated.
5. Run **Hanger 51 > P-51 Mustang > 1 - Build Full P-51 and Engine Installation System**.
6. Confirm the Console reports `P-51 Step 1 complete`.
7. Run **Hanger 51 > P-51 Mustang > 2 - Validate P-51 and Engine Installation System**.
8. Confirm the Console reports `P-51 Step 2 passed`.

## Expected aircraft

1. The P-51 is outside and to the right of the open hangar entrance.
2. The aircraft points toward the hangar so the engine hoist can approach the nose.
3. The airframe is approximately 9.83 m long, 11.28 m in wingspan, and 3.71 m high.
4. Confirm the model includes:
   - tapered fuselage and openable upper engine cowling;
   - laminar-flow-style tapered wings;
   - horizontal stabilizers, vertical fin, and rudder;
   - bubble canopy, cockpit frame, seat, and instrument panel;
   - four-blade propeller and spinner;
   - main landing gear and tailwheel;
   - ventral radiator scoop and intake;
   - twelve exhaust stacks;
   - six wing gun barrels;
   - navigation lights, markings, and control-surface seams.

## Cowling removal

1. Enter Play mode.
2. Walk to the P-51 nose.
3. Confirm ten cowling screw locations are highlighted.
4. Aim at one screw and hold `R`.
5. Confirm the screw rotates and rises from the panel.
6. Repeat for all ten screws.
7. Confirm the interaction message says all cowling screws are loose.
8. Aim at the highlighted top cowling panel.
9. Hold `R`.
10. Confirm the complete curved cowling panel lifts away from the engine bay.
11. Confirm the panel moves onto the padded service cradle beside the aircraft.
12. Confirm the engine bay, mount rails, firewall, and tubular mount structure are exposed.

## Engine placement guidance

1. Prepare the V-1650 in any assembly state:
   - bare block;
   - one or both covers installed;
   - partially tightened cover bolts;
   - some or all spark plugs installed.
2. Use the portable hoist to lift the engine from the maintenance stand.
3. Push the loaded hoist out of the hangar toward the P-51 nose.
4. Confirm a translucent engine-shaped placement volume appears in the open engine bay.
5. Confirm both engine-mount rails are highlighted.
6. Confirm four engine receiver pads are highlighted.
7. Move the hook over the highlighted engine bay.
8. Confirm the hoist prompt changes to `F: lower engine into highlighted P-51 engine bay`.
9. Press `F`.
10. Confirm the engine lowers and rotates into the exact aircraft mount pose.
11. Confirm the hoist releases the engine after placement.
12. Confirm the engine keeps its exact previous cover, bolt, and spark-plug state.

## Engine mounting bolts

1. After placement, confirm four engine-mount bolt targets are highlighted.
2. Confirm there are two rear/firewall mounts and two forward mounts.
3. Aim at mount bolt 1 and hold `E`.
4. Confirm the bolt rotates inward and its washer seats against the mount.
5. Repeat for all four mount bolts.
6. Confirm the completion message says the engine is installed.
7. Confirm each completed bolt highlight disappears.
8. Confirm the engine remains in the aircraft when the hoist is moved away.

## Cowling installation

1. Confirm all four engine-mount bolts are tight.
2. Aim at the highlighted cowling opening.
3. Hold `E`.
4. Confirm the cowling moves from the service cradle back onto the aircraft.
5. Confirm all ten screw locations appear highlighted again.
6. Hold `E` on each screw.
7. Confirm every screw rotates down and finishes flush with the cowling.
8. Confirm the final message says all top-cowling screws are secure.

## Installation safety checks

1. Remove the cowling but leave at least one engine-mount bolt loose.
2. Attempt to replace the cowling.
3. Confirm the game requires all four engine-mount bolts to be secured first.
4. With the cowling installed, position the hoist over the engine and press `F`.
5. Confirm the engine cannot be lifted until the cowling is removed.
6. Remove the cowling but leave one mount bolt tight.
7. Press `F` with the hook over the engine.
8. Confirm the engine cannot be lifted until every mount bolt is loose.

## Full engine removal

1. Unscrew all ten cowling screws with `R`.
2. Remove the cowling to its cradle with `R`.
3. Hold `R` on each of the four engine-mount bolts.
4. Confirm each bolt backs away from the mount.
5. Confirm the final message says all mount bolts are loose.
6. Move the hoist hook over the installed engine.
7. Press `F`.
8. Confirm the engine rises out of the aircraft while keeping its current assembly state.
9. Confirm the P-51 engine-bay placement highlight becomes visible again.
10. Return the engine to the maintenance stand or place it on the floor.
11. Reinstall it in the P-51 to confirm the cycle is repeatable.

## Partial-engine preservation test

1. Reset the P-51 service state with **Hanger 51 > P-51 Mustang > 3 - Reset P-51 Service State** only when the aircraft bay is empty.
2. Prepare an engine with exactly one cylinder cover installed.
3. Tighten three cover bolts.
4. Install five spark plugs if the engine state permits it.
5. Lift and install the engine in the P-51.
6. Tighten all four aircraft mount bolts.
7. Confirm the engine still has exactly the same internal assembly state.
8. Remove the engine again.
9. Return it to the stand.
10. Confirm the one cover, three tightened cover bolts, and installed spark plugs remain unchanged.

## Standalone Build and Run

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat the full cowling-removal sequence.
5. Lift an engine from the stand.
6. Follow the aircraft engine-bay highlights.
7. Lower the engine into the aircraft.
8. Tighten all four engine-mount bolts.
9. Replace and secure the cowling.
10. Remove the cowling, loosen the mounts, and lift the engine out again.

## Current milestone limits

- The P-51 is a procedural gameplay model rather than a final externally authored production mesh.
- The aircraft is static and cannot yet be entered, started, taxied, or flown.
- Fuel, coolant, oil, electrical, throttle, propeller-control, and exhaust connections are not part of this milestone.
- The top cowling is moved to a service cradle instead of being carried in inventory.
- Four principal engine-mount bolts are modeled for the first installation gameplay pass.
- The same receiver and transport architecture can later support hoses, wiring, propeller connection, lower cowling panels, and aircraft flight readiness.
