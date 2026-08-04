# Merlin Visible Dipstick and Oil-Cap Repair Test

This repair removes any missing, buried, duplicated, or previously reparented oil-service hardware and creates a new visible dipstick and oil filler on every current Merlin condition setup, including the inactive complete-engine shipment template.

## Apply

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Merlin Condition > 7 - Rebuild Visible Dipstick and Oil Cap**.
4. Expect `Merlin Condition Step 7 complete`.
5. Run **Hanger 51 > Merlin Condition > 8 - Validate Visible Dipstick and Oil Cap**.
6. Expect `Merlin Condition Step 8 passed`.

Do not rerun Merlin Condition Step 1 after this repair unless the full condition system needs to be regenerated. If Step 1 is ever rerun, repeat Steps 5 through 8 afterward.

## Visual check

1. Find the original Merlin.
2. Look along the upper outside surfaces of the engine block.
3. Confirm a large yellow circular dipstick handle is visible on one upper side.
4. Confirm a large yellow oil cap with crossed dark grip bars is visible on the opposite upper side.
5. Confirm neither item is floating over the empty maintenance stand.
6. Confirm neither item is buried entirely inside the engine.

## Interaction check

1. Enter Play mode.
2. Aim at the yellow dipstick handle.
3. Confirm `E: pull oil dipstick` appears.
4. Press `E` and confirm the rod lifts upward.
5. Confirm the oil stain appears and the oil quantity is reported.
6. Press `E` again and confirm the dipstick reinserts.
7. Aim at the yellow oil cap.
8. Press `X` and confirm the exact oil quantity is reported.
9. Carry an opened oil can to the cap and hold `E`.
10. Confirm the filler accepts oil only when the engine can be serviced.

## Hoist and movement check

1. Bring the hook to the engine lift point.
2. Confirm the trigger-only oil-service colliders do not obstruct the hook.
3. Lift the engine.
4. Confirm the dipstick and cap move with the engine.
5. Place the engine elsewhere and repeat both interactions.

## Purchased-engine check

1. Purchase and unbox a new complete V-1650 assembly.
2. Confirm the delivered engine has the same visible yellow dipstick handle and yellow oil cap.
3. Confirm both interactions work.
4. Confirm no service hardware remains behind when the engine is moved.
