# Merlin Final Placement Visual Repair Test

Use this checklist after pulling the latest `agent/merlin-engine-assembly` branch.

## Setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Test Hangar > 10 - Final Placement Visual Repair**.
4. Confirm the Console reports `Test Hangar Step 10 complete`.
5. Run **Hanger 51 > Test Hangar > 11 - Validate Final Placement Visuals**.
6. Confirm the Console reports `Test Hangar Step 11 passed`.

Step 10 replaces the existing cover, bolt, and spark-plug highlight objects. It does not rebuild the engine, inventory, hangar, or removal systems.

## Cover placement test

1. Press Play.
2. Install the engine block on the stand.
3. Pick up and equip one cylinder cover.
4. Close inventory.
5. Confirm the highlighted placement area is on the correct cylinder bank.
6. Confirm it follows the bank angle.
7. Confirm it is slightly smaller than the cover's lower flange.
8. Confirm it does not extend across the center valley or far beyond either end of the cover.
9. Hold `E` to place the cover.
10. Repeat with the second cover.

## Bolt-height test

1. Inspect all six loose bolts on the first installed cover.
2. Confirm every bolt starts at the same height relative to the cover.
3. Confirm neither the front, center, nor rear bolt floats higher than the others.
4. Tighten all six bolts.
5. Confirm every washer and head finishes at the same seating height.
6. Repeat on the opposite cover.
7. Loosen and retighten the previously incorrect bolt.
8. Confirm it returns to the same seating plane.

## Spark-plug marker test

1. Install both covers and tighten all 12 bolts.
2. Equip a spark plug.
3. Close inventory.
4. Confirm all available plug locations show a bright circular marker.
5. Confirm each marker also has a raised glowing beacon.
6. Confirm there are two markers per cylinder.
7. Confirm the markers sit directly above the plug wells on both covers.
8. Install one outer plug and one inner plug.
9. Confirm only the completed markers disappear.
10. Remove one installed plug with `R`.
11. Equip a spark plug again.
12. Confirm the marker returns at that exact well.

## Standalone build test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Recheck one cover placement area.
5. Recheck the formerly high bolt.
6. Recheck several spark-plug markers on both banks.

## Expected counts

Step 11 requires exactly:

- 2 compact cover placement areas
- 12 bolts on one shared seating plane
- 24 spark-plug markers containing a surface ring, raised beacon, and beacon stem
