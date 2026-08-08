# P-51 Wheel Inventory and Mobile Nitrogen Cart Test

Use branch `agent/merlin-engine-assembly` and the saved movement-test scene.

## Editor setup

1. Exit Play mode and allow Unity to compile.
2. Clear the Console and confirm there are no new red compiler errors.
3. Confirm P-51 Steps 28 through 31 have already been completed.
4. Do not rerun older landing-gear generation steps after Step 30.

The latest bolt-animation and world-first wheel-part behavior is runtime code and does not require another scene setup step after Steps 30/31 are already installed.

## Large mounting-bolt animation

1. Enter Play mode with the airplane stopped and landing gear fully down.
2. Exit the cockpit.
3. Aim at one large landing-gear mounting bolt.
4. Hold `R`.
5. Confirm the bolt visibly rotates several turns while backing out along its own shaft, similar to the Merlin fasteners.
6. Confirm the landing-gear assembly is released only when the hold completes.
7. Hold `E` at the mount to reinstall the gear.
8. Confirm the large bolt visibly spins and moves back into its installed position.

## Tire physical pickup and inventory round trip

1. Aim at a main tire and press `X`; record its health and PSI.
2. Hold `R` at the tire.
3. Confirm the installed tire disappears from the rim and that a separate physical tire appears beside that wheel.
4. Confirm the loose tire does NOT enter inventory automatically.
5. Aim at the loose tire and confirm its pickup prompt shows the same health and PSI.
6. Press `E` on the loose tire.
7. Confirm that exact tire now enters Player inventory with the same health and PSI.
8. Equip that same main tire.
9. Aim at the bare wheel station and hold `E`.
10. Confirm the exact tire returns with the same health and PSI.
11. Repeat with the smaller tailwheel tire.

## Rim physical pickup and inventory round trip

1. Remove the tire first and pick it up or move away from it.
2. With the tire already removed, hold `R` at the same wheel station again.
3. Confirm the installed rim disappears and a separate physical rim appears beside that wheel.
4. Confirm the rim does NOT enter inventory automatically.
5. Aim at the loose rim and press `E` to pick it up.
6. Press `X` at the wheel station and confirm it reports the rim removed.
7. Equip the matching rim item.
8. Hold `E` at the wheel station.
9. Confirm the rim returns.
10. Equip the matching tire and hold `E` again to complete the wheel.
11. Confirm a main rim/tire cannot be used as a tailwheel rim/tire and vice versa.

Shop products should include:

- `P-51 Main Landing Tire` — $450
- `P-51 Tailwheel Tire` — $180
- `P-51 Main Wheel Rim` — $650
- `P-51 Tailwheel Rim` — $260

## Mobile nitrogen cart

1. Confirm the nitrogen cart starts parked in the hangar rather than beside the P-51.
2. Confirm the rebuilt model has a heavy chassis, twin nitrogen bottles, bottle straps, dual gauges, regulator block/knob, hose reel, three rolling wheels/caster, push handle, and tool tray.
3. Aim at the cart and press `E`.
4. Walk around and confirm the cart rolls ahead of the Player rather than teleporting to the airplane.
5. Press `E` again to release it.
6. Try pressing `N` at a tire while the cart is too far away and confirm connection is refused with a distance/range message.
7. Wheel the cart close enough to the P-51 and release it.
8. Aim at the tire valve and press `N`.
9. Confirm the hose connects only when the cart is within its approximately 9 m hose range.
10. Aim at the cart, use `Q` / `Z` to set the regulator, and hold `F` to service the connected tire.
11. Press `N` to disconnect.
12. Confirm the cart cannot be moved while its hose remains connected.

## Regression checks

- Gear retract/extend still works with `G`.
- Normal raycast suspension, steering, brakes, and penetration protection still work.
- A removed rim does not leave invisible wheel support.
- Tire health, pressure, and burst state survive aircraft -> world pickup -> inventory -> aircraft.
- Tire health and pressure still affect landing damage and drag.
- A destroyed tire still looks failed and produces strong side drag until replaced.
- Newly purchased tires remain partially inflated and require nitrogen service.
- Merlin maintenance, inventory, shop shipments, towing, camera, and flight behavior remain available.
- Run the existing Windows build validation after Play-mode checks pass.
