# P-51 Wheel Inventory and Mobile Nitrogen Cart Test

Use branch `agent/merlin-engine-assembly` and the saved movement-test scene.

## Editor setup

1. Exit Play mode and allow Unity to compile.
2. Clear the Console and confirm there are no new red compiler errors.
3. Confirm P-51 Steps 28 and 29 have already been completed.
4. Run `Hanger 51 > P-51 Mustang > 30 - Add Inventory Wheels and Mobile Nitrogen Cart`.
5. Expect `P-51 Step 30 complete`.
6. Run `Hanger 51 > P-51 Mustang > 31 - Validate Inventory Wheels and Mobile Nitrogen Cart`.
7. Expect `P-51 Step 31 passed`.

Do not rerun older landing-gear generation steps after Step 30.

## Tire inventory round trip

1. Enter Play mode with the airplane stopped and the gear fully down.
2. Exit the cockpit.
3. Aim at a main tire and press `X`; record its health and PSI.
4. Hold `R` at the tire.
5. Confirm the tire disappears from the rim and appears in Player inventory.
6. Confirm the inventory condition text still shows the same health and PSI.
7. Equip that same main tire.
8. Aim at the bare wheel station and hold `E`.
9. Confirm the exact tire returns with the same health and PSI.
10. Repeat with the smaller tailwheel tire.

If inventory is full, the removed conditioned tire should appear beside the aircraft as a normal pickup rather than losing its state.

## Rim inventory round trip

1. Remove a tire first.
2. With the tire already removed, hold `R` at the same wheel station again.
3. Confirm the rim disappears and enters inventory.
4. Press `X` and confirm the wheel station reports the rim removed.
5. Equip the matching rim item.
6. Hold `E` at the wheel station.
7. Confirm the rim returns.
8. Equip the matching tire and hold `E` again to complete the wheel.
9. Confirm a main rim/tire cannot be used as a tailwheel rim/tire and vice versa.

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
- Tire health and pressure still affect landing damage and drag.
- A destroyed tire still looks failed and produces strong side drag until replaced.
- Newly purchased tires remain partially inflated and require nitrogen service.
- Merlin maintenance, inventory, shop shipments, towing, camera, and flight behavior remain available.
- Run the existing Windows build validation after Play-mode checks pass.
