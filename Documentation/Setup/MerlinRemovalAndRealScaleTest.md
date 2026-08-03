# Merlin Removal and Real-Scale Test

Use this checklist after pulling the latest `agent/merlin-engine-assembly` branch.

## Setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. If the expanded hangar is not already present, run **Hanger 51 > Test Hangar > 1 - Build Expanded Hangar and Move Parts**.
4. Run **Hanger 51 > Test Hangar > 4 - Add Removal and Apply Real Scale**.
5. Confirm the Console reports `Test Hangar Step 4 complete`.
6. Run **Hanger 51 > Test Hangar > 5 - Validate Removal and Real Scale**.
7. Confirm the Console reports `Test Hangar Step 5 passed`.

Step 4 automatically reapplies the corrected cover mounts and polished hardware before adding disassembly and final scale adjustments.

## Scale reference

The generated engine model is approximately 6.15 Unity units long before scaling. The setup applies a scale of `0.36`, producing an engine approximately `2.21 m` long. The National Air and Space Museum lists its Packard Merlin V-1650-7 artifact at approximately `221.3 cm` overall length.

Configured world scales:

- Engine block: `(0.36, 0.36, 0.36)`
- Cylinder covers: `(0.36, 0.36, 0.36)`
- Spark plugs: `(0.22, 0.22, 0.22)`

## Controls

- Hold `E`: install, tighten, or screw in the targeted part.
- Hold `R`: remove, loosen, or unscrew the targeted installed part.
- Press `I`: open inventory.

## Installation test

1. Press Play.
2. Collect the engine block, both covers, and the spark plugs.
3. Place the engine block on the stand.
4. Confirm the engine is approximately 2.2 meters long and no longer oversized relative to the Player.
5. Install one cover.
6. Tighten its six bolts.
7. Confirm the bolt targets sit inward from the cover edges.
8. Confirm each threaded shaft travels into the cover body instead of hanging outside its side.
9. Install at least two spark plugs.
10. Confirm the threaded portion enters the cover while the gasket, hex, ceramic, and terminal remain visible.

## Removal test

1. Aim at an installed spark plug.
2. Confirm the prompt says to hold `R` to unscrew it.
3. Hold `R` until the plug rises and rotates out.
4. Confirm one spark plug returns to inventory.
5. Reinstall the same plug with `E`.
6. Remove every spark plug from one cylinder bank.
7. Aim at a tightened bolt on that bank.
8. Hold `R` until the bolt rotates upward into its loose position.
9. Confirm the bolt remains available for retightening with `E`.
10. Loosen the other five bolts on that cover.
11. Aim at the center of the installed cover.
12. Hold `R` until the cover lifts off.
13. Confirm the cover returns to inventory.
14. Confirm the opposite bank remains unchanged.
15. Reinstall the removed cover and retighten its bolts.

## Complete teardown test

1. Remove all 24 spark plugs.
2. Loosen all 12 cover bolts.
3. Remove both covers.
4. Aim at the bare engine block or stand.
5. Hold `R` until the engine block is removed.
6. Confirm the engine block returns to inventory.
7. Reinstall the engine block.
8. Confirm the normal assembly sequence is available again.

## Inventory-full safety test

1. Fill all inventory slots so no removed item can be accepted.
2. Attempt to remove a spark plug or cover.
3. Confirm the part remains installed.
4. Confirm the status message tells you to make inventory room.
5. Free one inventory space and repeat the removal.

## Dropped-part scale test

1. Remove a spark plug and open inventory.
2. Drop one spark plug.
3. Confirm the dropped model remains hand-sized.
4. Remove and drop a cover.
5. Confirm the dropped cover matches the scaled installed cover.
6. Remove and drop the engine block.
7. Confirm the dropped engine remains approximately 2.2 meters long.

## Standalone build test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat one spark-plug removal and reinstall.
5. Repeat one bolt loosen and retighten.
6. Remove and reinstall one cover.
7. Confirm scale and bolt seating match Play mode.
