# P-51 Wheel Inventory and Mobile Nitrogen Cart Test

Use branch `agent/merlin-engine-assembly` and the saved movement-test scene.

## Editor setup

1. Exit Play mode and allow Unity to compile.
2. Clear the Console and confirm there are no new red compiler errors.
3. Confirm P-51 Steps 28 through 31 were already completed.
4. If the nitrogen cart is still on the roof, run P-51 Step 32 and then Step 33.
5. Do not rerun older landing-gear generation steps.

The clarified tire/rim workflow and easier nitrogen controls are runtime updates and do not require another Editor setup step.

## Wheel removal from the aircraft

1. Enter Play mode with the airplane stopped and landing gear fully down.
2. Exit the cockpit.
3. Aim at a main wheel and press `X`; record tire health/PSI and rim health.
4. Hold `R` at the wheel.
5. Confirm the visible wheel-retaining bolt backs out before the wheel releases.
6. Confirm the rim and tire leave the strut together as one complete wheel assembly.
7. Confirm the aircraft station has no tire or rim and another `R` cannot create a duplicate wheel.
8. Aim at the complete loose wheel and press `E`; confirm it can be carried.
9. Press `E` away from the aircraft to set the complete wheel back down.

## Tire removal and reinstallation on the rim

1. With the complete wheel on the floor, press `X` and confirm tire condition/PSI and rim condition are reported.
2. Hold `R` on the complete wheel.
3. Confirm only the tire comes off the rim.
4. Confirm the tire becomes a visible physical pickup beside the wheel.
5. Confirm the rim remains intact as one bare rim; there is no further rim-disassembly action.
6. Aim at the separated tire and confirm its prompt retains the original health/PSI.
7. Press `E` to put that tire in inventory.
8. Equip that same tire, or a correct replacement tire.
9. Aim at the bare rim and hold `E`.
10. Confirm that exact equipped tire mounts onto the rim and the complete wheel returns.
11. Press `X` and verify the tire condition/PSI and rim condition are correct.

## Bare rim inventory option

1. Remove the tire again so the loose wheel is a bare rim.
2. With no correct tire equipped, press `E` on the bare rim.
3. Confirm the rim enters inventory as one complete rim item; it is not disassembled into smaller pieces.
4. Confirm the visible wheel rebuild position remains.
5. Equip the original rim or a correct replacement rim and hold `E` at the rebuild position.
6. Confirm the rim returns as one complete rim.
7. Equip a matching tire and hold `E` to mount it onto that rim.
8. Confirm the complete wheel can again be carried back to the aircraft.

## Complete-wheel reinstallation

1. Leave the original aircraft wheel station empty.
2. With no complete wheel being carried, confirm the empty strut does not falsely offer installation.
3. Equip individual tire/rim inventory items and confirm the aircraft strut still does not advertise complete-wheel installation.
4. Rebuild a complete tire + rim wheel off the aircraft.
5. Press `E` to carry the complete wheel.
6. Confirm only its correct/original axle highlights.
7. Aim at the highlighted axle and hold `E`.
8. Confirm the complete wheel installs and the retaining bolt tightens inward.
9. Confirm saved tire health/PSI and rim condition return to the aircraft.

## Nitrogen service on an installed tire

1. Park the nitrogen cart within hose range and release its handle.
2. Aim at an installed tire and press `N`.
3. Confirm the hose connects.
4. Keep looking at the tire; you do not need to turn back toward the cart.
5. Use `Q` / `Z` to adjust the cart regulator.
6. Hold `F` while still looking at the tire.
7. Confirm the tire PSI changes toward the regulator setpoint.
8. Set a main tire to 30 PSI or the tail tire to 24 PSI.
9. Press `X` and confirm the new pressure.
10. Press `N` to disconnect.

The same Q/Z + Hold F controls also continue to work while looking directly at the cart.

## Nitrogen service on a loose wheel

1. Remove a complete rim + tire wheel from the aircraft and place it on the floor.
2. Leave the tire mounted on its rim.
3. Wheel the nitrogen cart within hose range.
4. Aim at the loose complete wheel and press `N`.
5. Confirm the hose connects to the loose wheel.
6. Use `Q` / `Z` while looking at the loose wheel to set the regulator.
7. Hold `F` while looking at the loose wheel.
8. Confirm its saved tire-pressure value changes.
9. Press `X` and confirm the new PSI remains with that loose tire.
10. Reinstall the wheel on the aircraft and confirm the serviced pressure follows it.

A tire that has been removed from its rim cannot be pressure-serviced until it is mounted on a rim again.

## Regression checks

- `G` gear retract/extend still works.
- Existing raycast suspension, steering, brakes, and ground-penetration protection still work.
- One aircraft wheel removal creates exactly one rim + tire wheel assembly.
- The tire can be removed from and reinstalled on its rim repeatedly.
- The rim remains a single rim part; there is no rim sub-disassembly.
- A bare rim can still be stored in inventory and restored at its loose-wheel rebuild position.
- Complete wheels are physically carryable and reinstallable on the aircraft.
- Individual tire/rim inventory items do not falsely highlight the aircraft axle as a complete wheel.
- Tire health and PSI survive wheel removal, tire separation, inventory, reassembly, and aircraft reinstallation.
- Nitrogen service works while aiming at either an installed tire, a complete loose wheel, or the connected cart.
- Main tires target 30 PSI and tailwheel tires target 24 PSI.
- Overpressure can still burst a tire and destroyed tires still require replacement.
- Gear and wheel bolts remain attached to/move with the aircraft.
- Merlin maintenance, inventory, commerce, towing, camera, and flight behavior remain available.
- Run the existing Windows build validation after Play-mode checks pass.
