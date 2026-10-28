# Hanger 51 Parts Computer and Shipping Test

## Purpose

This milestone adds a physical desk and computer inside the hangar, an interactive parts catalog, a test wallet, four marked shipment bays, animated wooden crates, inventory-part deliveries, and complete serviceable V-1650 assembly deliveries.

It also adds an Editor-only Scene-view clipping repair for the large ground Plane.

## Current setup commands

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run `Hanger 51 > Environment > 3 - Fix Scene View Camera Clipping`.
4. Run `Hanger 51 > Shop and Shipping > 1 - Build Parts Computer and Shipment Area`.
5. Run `Hanger 51 > Shop and Shipping > 2 - Validate Parts Computer and Shipment Area`.

Expected validation message:

`Shop Step 2 passed.`

Do not rerun older P-51 or hangar generation commands merely to install the shop. The shop setup uses the current saved hangar and current Merlin station as its source.

## Scene-view camera check

1. Open a Scene tab.
2. Move the Scene camera close to the Plane, hangar wall, desk, and P-51.
3. Confirm nearby geometry no longer disappears while the camera is still visibly separated from it.
4. Confirm distant runway and ground remain visible.
5. Enter Play mode and confirm the Player camera was not changed.

The menu command sets only the open Editor Scene-view cameras to:

- Dynamic Clipping: Off
- Near Clip: 0.01 m
- Far Clip: 100,000 m

## Desk and computer inspection

Walk around the new `Parts Computer Desk` and confirm it contains:

- Wooden desk top
- Steel legs and feet
- Three-drawer pedestal with handles
- Monitor, stand, bezel, screen, and power indicator
- Keyboard chassis and individual keys
- Mouse
- Computer tower with vents and power indicator
- Rolling-style desk chair

The desk should be inside the hangar and should not overlap the existing engine stand, hoist, or P-51 work area.

## Open and close the terminal

1. Enter Play mode.
2. Walk to the computer monitor.
3. Aim at the screen.
4. Confirm the prompt reads `E: use Hanger 51 parts computer`.
5. Press `E`.
6. Confirm the shop fills the screen and the cursor is available.
7. Confirm walking, aircraft interaction, tow-bar interaction, hoist interaction, and inventory interaction are blocked while the terminal is open.
8. Confirm the starting account balance is `$250,000`.
9. Confirm the terminal shows four open shipment bays.
10. Press `Escape` or click the red `X`.
11. Confirm the terminal closes, the cursor locks again, and normal walking resumes.

## Catalog check

Confirm these products appear:

1. V-1650 Spark Plug Set (24)
2. V-1650 Cylinder Cover
3. Merlin V-1650 Engine Block
4. Aircraft Oil Filter
5. Shop Rag Bundle (5)
6. Complete Serviceable V-1650 Assembly

Each entry should show a category, description, delivery type, and price.

## Buy and unbox an inventory part

1. Open the terminal.
2. Select `V-1650 Spark Plug Set (24)`.
3. Click `BUY & DELIVER`.
4. Confirm the balance decreases by `$3,600`.
5. Confirm the shipment capacity changes from `4/4` to `3/4`.
6. Close the terminal.
7. Walk to the marked shipment receiving area near the rear of the hangar.
8. Find the labeled wooden crate in Bay 1.
9. Confirm its label identifies the spark-plug set and quantity 24.
10. Aim at the crate and press `E`.
11. Confirm both steel bands retract and the lid swings open.
12. Confirm the crate produces one spark-plug pickup stack near the bay.
13. Confirm the bay remains reserved until the delivered pickup is collected.
14. Aim at the delivered spark-plug pickup and press `E`.
15. Open inventory with `I`.
16. Confirm 24 V-1650 spark plugs were added, subject to available inventory slots.
17. Return to the terminal and confirm the shipment bay becomes available again after the pickup is collected.

## Buy and unbox consumables

Repeat the purchase and unboxing process for:

- Aircraft Oil Filter
- Shop Rag Bundle (5)

Confirm each creates the existing inventory item rather than a decorative replacement.

## Shipment capacity test

1. Purchase four products without opening their crates.
2. Confirm all four marked shipment bays contain labeled crates.
3. Confirm the terminal reports `0/4` shipment bays open.
4. Attempt a fifth purchase.
5. Confirm it is blocked with a message explaining that all shipment bays are occupied.
6. Unbox and collect one inventory delivery.
7. Confirm one shipment bay becomes available again.

## Buy a complete V-1650 assembly

1. Make sure at least one shipment bay is open.
2. Open the terminal.
3. Select `Complete Serviceable V-1650 Assembly`.
4. Confirm the listed price is `$95,000`.
5. Buy it.
6. Close the terminal and walk to the assigned crate.
7. Confirm the crate label says `COMPLETE ASSEMBLY`.
8. Press `E` to unbox it.
9. Confirm a complete V-1650 maintenance stand appears at the unboxed-content position.
10. Confirm the assembly initially shows:
    - Engine block installed
    - Two cylinder covers installed
    - Twelve cover bolts tightened
    - Twenty-four spark plugs installed
11. Confirm the shipment bay remains reserved while the delivered stand remains in its delivery position.

## Complete-assembly maintenance test

The purchased assembly must use the same maintenance system as the original.

1. Aim at a spark plug and hold `R`.
2. Confirm the spark plug unscrews and returns to inventory.
3. Remove all spark plugs from one bank.
4. Aim at that bank's cover bolts and hold `R` on each.
5. Confirm all six bolts can be loosened.
6. Hold `R` on the cylinder cover.
7. Confirm the cover lifts off and returns to inventory.
8. Repeat for the other bank.
9. With both covers removed, hold `R` on the bare engine block.
10. Confirm the engine block can be removed through the existing station workflow.
11. Reinstall the engine block through the inventory Install action.
12. Equip and reinstall both covers using the existing highlighted areas.
13. Tighten all twelve cover bolts.
14. Equip and reinstall all twenty-four spark plugs.
15. Confirm the purchased station returns to `V-1650 assembly: complete`.

## Multiple-station interaction check

1. Walk between the original engine station and the purchased station.
2. Aim at each station separately.
3. Confirm the inventory Install target and maintenance prompts follow the station currently under the crosshair.
4. Confirm removing a part from one engine does not change the other engine's assembly state.

## Economy behavior for this milestone

The Player wallet starts at `$250,000` each time the scene starts. Purchase persistence and long-term business accounting are not part of this milestone yet.

## Standalone Build and Run

1. Run `Hanger 51 > Build > 1 - Prepare Current Scene for Build`.
2. Run `Hanger 51 > Build > 2 - Validate Build Setup`.
3. Run `Hanger 51 > Build > 3 - Build and Run Windows`.
4. In the standalone build, repeat:
   - Open and close the terminal
   - Buy one inventory part
   - Find and unbox its crate
   - Collect the delivered item
   - Buy and unbox one complete assembly
   - Remove and reinstall at least one spark plug
   - Verify Player controls restore after closing the terminal

## Report format for problems

For a compiler failure, provide the first complete red Console error, including file and line number.

For a runtime problem, report:

- Product selected
- Balance before and after purchase
- Shipment-bay count shown in the terminal
- Whether a crate appeared
- Whether the crate lid and bands animated
- What object appeared after unboxing
- The exact interaction prompt or status message shown
