# Purchased Merlin Cover Removal While Installed in the P-51

## Setup

1. Exit Play mode and clear the Console.
2. Run **Hanger 51 > Merlin Condition > 22 - Repair Cover Removal After P-51 Installation**.
3. Run **Hanger 51 > Merlin Condition > 23 - Validate Cover Removal After P-51 Installation**.
4. Confirm Step 23 reports `2/12/24` for every current engine and the complete-engine shipment template.

## Exact regression sequence

1. Enter Play mode.
2. Purchase a complete V-1650 assembly.
3. Unpack it and attach it to the hoist.
4. Remove the P-51 top cowling.
5. Lower the purchased engine into the P-51 and secure the four engine-mount bolts.
6. Start the engine and use the overboost test procedure until one cylinder cover cracks.
7. Stop the engine.
8. Remove all twelve spark plugs from the cracked cover's bank.
9. Loosen all six cover bolts from that bank.
10. Aim at the cracked cover target.
11. Confirm the prompt reads `Hold R to lift off the left/right cylinder cover` rather than reporting that no bolt targets are configured.
12. Hold `R` until removal completes.
13. Confirm the cover visual disappears.
14. Confirm fire and active oil leakage from that cover stop.
15. Press `X` on the open bank target and confirm the removed cover's recorded condition remains available.

## Inventory-full regression

1. Repeat with a full inventory after the twelve plugs have been removed.
2. Hold `R` on the ready cover target.
3. Confirm the cover is removed anyway and a pickup is dropped beside the installed engine/P-51 rather than at the abandoned shipment location.
4. Free an inventory slot and collect the dropped cover.

## Stand regression

1. Repeat ordinary cover removal on the original stand engine.
2. Confirm the same target registry still works while the portable root remains underneath its station.

## Expected result

The removal controller reads the station's serialized target registry first and the portable transport root second. Reparenting the purchased engine into the P-51 must not disconnect the two cover targets, twelve bolt targets, or twenty-four plug targets from reversible maintenance.
