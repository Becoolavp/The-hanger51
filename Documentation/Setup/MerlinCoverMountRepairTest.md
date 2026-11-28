# Merlin Cylinder Cover Mount Repair Test

Use this test after pulling the latest `agent/merlin-engine-assembly` branch.

## Setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Merlin Assembly > 6 - Repair Cylinder Cover Mount Positions**.
4. Confirm the Console reports `Merlin Step 6 complete`.
5. Run **Hanger 51 > Merlin Assembly > 7 - Validate Cylinder Cover Mount Positions**.
6. Confirm the Console reports `Merlin Step 7 passed`.
7. Run **Hanger 51 > Merlin Assembly > 8 - Validate Cover Animation Path**.
8. Confirm the Console reports `Merlin Step 8 passed`.

Step 6 snaps both cover roots directly to the generated engine's left and right cylinder-bank transforms. It then rebuilds the cover highlights, interactive bolts, and spark-plug wells around the corrected poses.

The cover animation no longer uses a cached local-space destination. Each interaction target supplies a live world-space position and rotation, and the cover moves from a point above/outward from that target to the exact highlighted mount.

## Play-mode test

1. Press Play.
2. Collect and place the engine block.
3. Equip one cylinder cover.
4. Confirm the highlighted mounting zone appears on top of a cylinder bank.
5. Hold E on the highlight.
6. Confirm the cover first appears above/outward from the highlighted bank.
7. Confirm it lowers directly toward that highlight instead of appearing below the engine.
8. Inspect the installed cover from the front, rear, outer side, and center valley.
9. Confirm its lower flange sits against the cylinder-head deck.
10. Confirm the cover follows the same approximately 30-degree bank angle as that side of the engine.
11. Repeat for the other cover.
12. Confirm the six bolt highlights remain attached to each cover.
13. Confirm the spark-plug wells remain on top of the covers after both covers are secured.

## Standalone build test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat both cover placements in the standalone game.

## Technical mount reference

The generated engine bank root is positioned and rotated as part of the 60-degree V layout. The bank's head-deck top is approximately local Y `0.52`. The repaired cover root is mounted at bank-local position:

`(0, 0.535, 0)`

The extra `0.015` units prevent visible surface fighting while keeping the lower cover flange seated against the head deck.

The placement animation uses:

- final position: the cover target's world position;
- final rotation: the cover target's world rotation;
- start position: final position plus the bank-normal direction multiplied by the configured lift distance.

This prevents station, prefab, or parent-transform offsets from moving the animation underneath the engine.
