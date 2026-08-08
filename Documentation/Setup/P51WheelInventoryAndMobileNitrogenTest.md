# P-51 Wheel Inventory and Mobile Nitrogen Cart Test

Use branch `agent/merlin-engine-assembly` and the saved movement-test scene.

## Editor setup

1. Exit Play mode and allow Unity to compile.
2. Clear the Console and confirm there are no new red compiler errors.
3. Confirm P-51 Steps 28 through 31 were already completed.
4. Do not rerun older landing-gear generation steps.

The complete-wheel removal, wheel-retaining bolt, loose-wheel separation, and equipped-part highlight changes are runtime updates and do not require another Editor setup step.

## Complete wheel removal

1. Enter Play mode with the airplane stopped and the landing gear fully down.
2. Exit the cockpit.
3. Aim at a main wheel and press `X`; record tire health/PSI and rim health.
4. Confirm a visible wheel-retaining bolt is centered at the hub/outboard face of the wheel.
5. Hold `R` at the wheel.
6. Confirm the retaining bolt visibly rotates several turns and backs outward while the tire and rim remain on the axle.
7. At the end of the hold, confirm the tire and rim leave the airplane together as one complete loose wheel assembly.
8. Confirm the aircraft no longer shows either the installed tire or installed rim at that station.
9. Confirm another `R` on the aircraft does not create another tire or wheel copy.
10. Press `X` at the aircraft station and confirm the rim and tire are reported removed.

## Loose wheel separation

1. Aim at the complete wheel assembly lying beside the airplane.
2. Press `X` and confirm it reports both the tire condition/PSI and rim condition.
3. Hold `R` on the loose wheel.
4. Confirm the complete wheel disappears and becomes two separate physical parts: one tire pickup and one rim pickup.
5. Aim at the separated tire and confirm its pickup prompt retains the original health/PSI.
6. Press `E` to put the tire in inventory.
7. Aim at the separated rim and confirm its condition is retained.
8. Press `E` to put the rim in inventory.
9. Repeat with the smaller tailwheel and confirm its tire/rim dimensions remain distinct from the main wheel parts.

## Equipped-part installation highlights

1. Leave one aircraft wheel station empty after the wheel has been removed.
2. Equip the correct main or tail rim from inventory.
3. Confirm a pulsing highlight appears around only the matching empty axle/wheel install point.
4. Aim at the highlighted station and hold `E` to install the rim.
5. Confirm the rim appears and the rim item is consumed from inventory.
6. Equip the correct matching tire.
7. Confirm the same wheel station highlights again, now indicating the tire installation area.
8. Hold `E` to fit the tire.
9. Confirm the tire returns with its stored health/PSI and the wheel-retaining bolt visibly screws back inward.
10. Confirm a main rim/tire does not highlight or install at the tailwheel station and a tailwheel part does not highlight or install at a main station.

## Gear mount bolt animation

1. Aim at the large landing-gear mounting bolt above the wheel assembly.
2. Hold `R`.
3. Confirm the large bolt rotates multiple turns and visibly backs out before the complete strut/gear assembly releases.
4. Reinstall the gear and confirm the animation reverses as the bolt screws back in.

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
- One complete wheel removal produces exactly one loose wheel assembly.
- Rim and tire remain together until the loose wheel is deliberately separated.
- Removed wheel stations do not retain invisible support.
- Tire health and pressure still affect landing damage and drag.
- A destroyed tire remains visibly failed and produces strong side drag until replaced.
- Condition survives installed wheel -> loose wheel -> separated pickup -> inventory -> reinstallation.
- Correct equipped rim/tire highlights only its valid install location.
- Newly purchased tires remain partially inflated and require nitrogen service.
- Merlin maintenance, inventory, shop shipments, towing, camera, and flight behavior remain available.
- Run the existing Windows build validation after Play-mode checks pass.
