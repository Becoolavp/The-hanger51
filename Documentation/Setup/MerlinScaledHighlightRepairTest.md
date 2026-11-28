# Merlin Scaled Highlight Repair Test

Use this checklist after pulling the latest `agent/merlin-engine-assembly` branch.

## Why this repair exists

The previous highlight pass measured the entire cylinder-cover hierarchy. After bolt targets were parented under a cover, their bolt models and highlight rings became part of later cover measurements. Re-running the setup could therefore enlarge or shift the cover footprint and produce repeated bolt and spark-plug validation errors.

The repair measures only physical cover mesh geometry. Any mesh beneath an `EngineAssemblyInteractionTarget` is excluded.

## Setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Test Hangar > 8 - Repair Scaled Highlight Geometry**.
4. Confirm the Console reports `Test Hangar Step 8 complete`.
5. Run **Hanger 51 > Test Hangar > 9 - Validate Repaired Highlights**.
6. Confirm the Console reports `Test Hangar Step 9 passed`.

Do not use the old Step 7 result to judge this repair. Step 9 replaces that validator for the corrected geometry.

## Expected counts

- Cover mounting highlights: 2
- Cover-bolt highlights: 12
- Spark-plug well highlights: 24

## Cover test

1. Press Play.
2. Place the engine block on the stand.
3. Equip one cylinder cover.
4. Close inventory.
5. Confirm the highlighted footprint follows the correct cylinder bank.
6. Confirm the footprint is only slightly larger than the cover flange.
7. Confirm it uses the same angle as the bank.
8. Confirm it is not shifted by nearby bolt or plug targets.
9. Install the cover.
10. Repeat for the opposite bank.

## Bolt test

1. Inspect all six bolt rings on one installed cover.
2. Confirm each ring is centered on one bolt.
3. Confirm all rings remain inside the cover edges.
4. Confirm front, middle, and rear rings are distributed evenly.
5. Tighten several bolts.
6. Confirm each completed ring disappears.
7. Loosen one bolt with `R`.
8. Confirm its ring returns at the same position.

## Spark-plug test

1. Install and secure both covers.
2. Equip a spark plug.
3. Confirm 24 well rings appear across both banks.
4. Confirm every ring rests on the cover surface.
5. Confirm two rings appear for each cylinder.
6. Install one plug.
7. Confirm only that well ring disappears.
8. Remove the plug with `R`.
9. Equip a plug again and confirm the ring returns at the exact same location.

## Repeatability test

1. Exit Play mode.
2. Run Step 8 a second time.
3. Run Step 9 again.
4. Confirm Step 9 still passes.
5. Confirm the cover footprints did not grow or move after the second run.

## Standalone test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Confirm cover, bolt, and plug highlights match Play mode.
