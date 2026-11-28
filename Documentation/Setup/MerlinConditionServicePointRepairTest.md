# Merlin Condition Service-Point Repair Test

This repair removes the oversized solid inspection colliders created by the first condition-system pass and anchors the dipstick and oil filler directly to each portable engine block.

## Apply

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Merlin Condition > 5 - Repair Service Points and Inspection Colliders**.
4. Run **Hanger 51 > Merlin Condition > 6 - Validate Service Points and Inspection Colliders**.

Expected validation message:

`Merlin Condition Step 6 passed`

## What Step 5 changes

- Deletes the broad block and cover inspection-follower objects.
- Creates one small trigger-only block inspection point.
- Creates one small trigger-only inspection point on each cover.
- Converts the dipstick and filler interaction colliders to triggers.
- Parents the dipstick and filler to the actual engine-block visual.
- Removes duplicate dipsticks or fillers left by a partial or repeated setup.
- Repairs active engines and the inactive complete-engine shipment template.

Trigger colliders can be raycast by the condition interactor but do not physically block the Player, hoist, hook, cowling, or engine.

## Play-mode test

1. Walk completely around the original Merlin.
2. Confirm there is no invisible wall around it.
3. Move the hoist hook over the engine lift point.
4. Confirm the hook can approach normally.
5. Aim directly at the dipstick and press `E`.
6. Confirm the dipstick pulls out and reports the oil level.
7. Press `E` again and confirm it reinserts.
8. Aim at the oil filler and press `X`.
9. Confirm the oil quantity is shown.
10. Aim at the block and each cover and press `X`.
11. Confirm all three inspection points work without covering the entire engine.

## Movement test

1. Lift the engine away from its stand with the hoist.
2. Confirm the dipstick and filler move with the engine.
3. Confirm nothing remains floating above the empty stand.
4. Lower the engine elsewhere and test the dipstick again.
5. Install the engine in the P-51 and confirm both service objects remain aligned with the block.

## Purchased-engine test

1. Purchase and unbox a new complete V-1650 assembly.
2. Confirm its dipstick and filler are attached to the delivered engine.
3. Confirm no invisible inspection box blocks the hoist.
4. Lift the engine and confirm the service points follow it.

After any future rerun of **Merlin Condition Step 1**, rerun Steps 5 and 6. Normal purchased engines inherit the repaired inactive shipment template and do not require a separate repair.
