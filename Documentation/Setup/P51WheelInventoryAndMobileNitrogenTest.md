# P-51 Wheel Inventory and Mobile Nitrogen Cart Test

Use branch `agent/merlin-engine-assembly` and the saved movement-test scene.

## Editor setup

1. Exit Play mode and allow Unity to compile.
2. Clear the Console and confirm there are no new red compiler errors.
3. Confirm P-51 Steps 28 through 31 were already completed.
4. Do not rerun older landing-gear generation steps.

The complete-wheel carry/rebuild flow, corrected bolt direction/parenting, and carried-wheel install highlight are runtime updates and do not require another Editor setup step.

## Complete wheel removal and carry

1. Enter Play mode with the airplane stopped and landing gear fully down.
2. Exit the cockpit.
3. Aim at a main wheel and press `X`; record tire health/PSI and rim health.
4. Confirm a visible wheel-retaining bolt is centered at the wheel hub.
5. Hold `R` at the wheel.
6. From the exposed bolt-head side, confirm the retaining bolt turns in the normal loosening direction and backs outward along its own shaft.
7. Confirm the tire and rim remain together on the axle until the bolt finishes coming out.
8. Confirm the complete tire + rim assembly then leaves the aircraft as one loose wheel.
9. Confirm the aircraft station has no tire or rim and another `R` cannot create a duplicate wheel.
10. Aim at the complete loose wheel and press `E`.
11. Confirm the complete wheel is visibly carried in front of the Player.
12. Press `E` away from the aircraft axle and confirm the wheel can be set back down.

## Loose wheel separation and rebuild

1. With the complete wheel on the floor, press `X` and confirm tire condition/PSI, rim condition, and its original wheel station are reported.
2. Hold `R` on the complete wheel.
3. Confirm the tire separates as a visible physical pickup and does not disappear.
4. Confirm the rim remains at the loose-wheel service position.
5. Aim at the separated tire and confirm its pickup prompt retains the original health/PSI.
6. Press `E` to put that tire in inventory.
7. With the bare loose rim still present, hold `R` again if testing full disassembly.
8. Confirm the rim becomes its own visible physical pickup and the loose-wheel rebuild position remains available.
9. Press `E` to put the rim in inventory.
10. Equip either the original rim or a correct new replacement rim.
11. Aim at the loose-wheel rebuild position and hold `E`; confirm that exact rim is installed into the loose assembly.
12. Equip either the original tire or a correct new replacement tire.
13. Hold `E` on the loose rim and confirm that exact tire is fitted.
14. Press `X` and confirm the loose wheel is complete again with the chosen tire health/PSI and rim condition.
15. Confirm main and tail wheel parts remain different sizes and cannot be mixed.

## Complete-wheel reinstallation highlight

1. Leave the original aircraft wheel station empty.
2. With no complete wheel being carried, look at the bottom of the strut.
3. Confirm there is no install highlight and no prompt claiming a wheel can currently be installed.
4. Equip individual tire or rim inventory items and confirm the aircraft strut still does not highlight; those parts must be assembled off-aircraft first.
5. Rebuild the loose wheel completely and press `E` to carry it.
6. Confirm only that wheel's original axle gets the pulsing install highlight.
7. Aim at the highlighted axle and hold `E`.
8. Confirm the complete tire + rim assembly installs together.
9. Confirm the wheel-retaining bolt turns in the tightening direction and moves inward along its shaft.
10. Confirm the completed wheel restores its saved tire health/PSI and rim health.
11. Confirm a carried wheel from another station cannot install on the wrong strut.

## Gear mount bolt direction and aircraft parenting

1. Aim at each large landing-gear mounting bolt.
2. Hold `R` and confirm it turns in the loosening direction and visibly backs out along its own shaft.
3. Reinstall the gear and confirm the bolt rotates the opposite direction while moving back into its installed position.
4. With all gear installed, move/tow/taxi the aircraft several meters.
5. Confirm all three large gear mounting bolts and all three wheel-retaining bolts remain attached to their aircraft/gear positions and do not stay behind at their old world coordinates.
6. Repeat after rotating the aircraft and confirm the bolts rotate/move with the aircraft hierarchy.

## Mobile nitrogen cart

1. Confirm the nitrogen cart starts parked in the hangar rather than beside the P-51.
2. Confirm the model has a heavy chassis, twin nitrogen bottles, bottle straps, dual gauges, regulator block/knob, hose reel, three rolling wheels/caster, push handle, and tool tray.
3. Aim at the cart and press `E`.
4. Walk around and confirm the cart rolls ahead of the Player.
5. Press `E` again to release it.
6. Try pressing `N` at a tire while the cart is too far away and confirm connection is refused with a range message.
7. Wheel the cart close enough to the P-51 and release it.
8. Aim at the tire valve and press `N`.
9. Confirm the hose connects only when the cart is within its approximately 9 m hose range.
10. Aim at the cart, use `Q` / `Z` to set the regulator, and hold `F` to service the connected tire.
11. Press `N` to disconnect.
12. Confirm the cart cannot be moved while its hose remains connected.

## Regression checks

- `G` gear retract/extend still works.
- Normal raycast suspension, steering, brakes, and ground-penetration protection still work.
- One wheel removal produces exactly one complete loose wheel assembly.
- Complete wheels are physically carryable and placeable.
- Tire separation leaves a visible conditioned tire pickup rather than destroying it.
- A loose wheel can be fully disassembled and rebuilt with original or replacement parts.
- Only a completed carried wheel highlights/installs at its original aircraft station.
- Individual equipped rims/tires never make an empty aircraft strut falsely advertise installation.
- Gear and wheel bolts remain parented to/move with the aircraft.
- Removed wheel stations do not retain invisible support.
- Tire health and pressure still affect landing damage and drag.
- A destroyed tire remains visibly failed and produces strong side drag until replaced.
- Condition survives installed wheel -> loose wheel -> separated pickup -> inventory -> rebuilt wheel -> aircraft.
- Newly purchased tires remain partially inflated and require nitrogen service.
- Merlin maintenance, inventory, shop shipments, towing, camera, and flight behavior remain available.
- Run the existing Windows build validation after Play-mode checks pass.
