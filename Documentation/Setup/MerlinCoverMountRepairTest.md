# Merlin Cylinder Cover Mount Repair Test

Use this test after pulling the latest `agent/merlin-engine-assembly` branch.

## Setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Merlin Assembly > 6 - Repair Cylinder Cover Mount Positions**.
4. Confirm the Console reports `Merlin Step 6 complete`.
5. Run **Hanger 51 > Merlin Assembly > 7 - Validate Cylinder Cover Mount Positions**.
6. Confirm the Console reports `Merlin Step 7 passed`.

Step 6 snaps both cover roots directly to the generated engine's left and right cylinder-bank transforms. It then rebuilds the cover highlights, interactive bolts, and spark-plug wells around the corrected poses.

## Play-mode test

1. Press Play.
2. Collect and place the engine block.
3. Equip one cylinder cover.
4. Confirm the highlighted mounting zone appears on top of a cylinder bank.
5. Hold E on the highlight.
6. Confirm the cover lowers onto the top of the bank rather than below the engine.
7. Inspect the cover from the front, rear, outer side, and center valley.
8. Confirm its lower flange sits against the cylinder-head deck.
9. Confirm the cover follows the same approximately 30-degree bank angle as that side of the engine.
10. Repeat for the other cover.
11. Confirm the six bolt highlights remain attached to each cover.
12. Confirm the spark-plug wells remain on top of the covers after both covers are secured.

## Standalone build test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Repeat both cover placements in the standalone game.

## Technical mount reference

The generated engine bank root is positioned and rotated as part of the 60-degree V layout. The bank's head-deck top is approximately local Y `0.52`. The repaired cover root is mounted at bank-local position:

`(0, 0.535, 0)`

The extra `0.015` units prevent visible surface fighting while keeping the lower cover flange seated against the head deck.
