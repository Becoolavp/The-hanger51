# P-51 Low-Oil, Oil Commerce, and Serviceable Landing Gear Test

Use branch `agent/merlin-engine-assembly` and scene `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.

## Editor setup

1. Exit Play mode and allow Unity to compile.
2. Clear the Console and confirm there are no new red compiler errors.
3. Run `Hanger 51 > Shop and Shipping > 7 - Add Purchasable Aircraft Oil Cans`.
4. Run `Hanger 51 > Shop and Shipping > 8 - Validate Purchasable Aircraft Oil Cans`.
5. Run `Hanger 51 > P-51 Mustang > 28 - Add Retractable Serviceable Landing Gear`.
6. Run `Hanger 51 > P-51 Mustang > 29 - Validate Retractable Serviceable Landing Gear`.
7. Run `Hanger 51 > Shop and Shipping > 9 - Add P-51 Replacement Tires`.
8. Run `Hanger 51 > Shop and Shipping > 10 - Validate P-51 Replacement Tires`.

Do not rerun older P-51 landing-gear generation steps after Step 28. Step 28 intentionally keeps the current raycast suspension and penetration guard while replacing the visible/service layer.

## Low-oil start test

1. Install a serviceable Merlin in the P-51.
2. Reduce oil below the 15 L recommended minimum.
3. Enter the cockpit and press `T`.
4. Confirm the engine starts instead of being automatically shut off.
5. Confirm a LOW OIL / CRITICALLY LOW OIL warning appears.
6. Run the engine briefly and confirm low oil still reduces power/quality and can damage the engine. Do not expect low oil to be safe just because starting is allowed.

## Purchasable oil-can test

1. Open the Hanger 51 shop.
2. Confirm `20 L Aircraft Engine Oil Can` appears under Fluids for $125.
3. Purchase it and open the delivered crate.
4. Confirm a normal reusable aircraft oil can appears in the shipment bay.
5. Pick it up with `E`, open it with `F`, and hold `E` at a Merlin oil filler.
6. Confirm the delivered can begins full and loses oil normally while pouring.

## Gear retraction test

1. Enter the P-51 cockpit.
2. Take off normally with all three tires serviceable.
3. Press `G` and confirm the main gear and tailwheel retract over about 2.4 seconds.
4. Confirm the HUD changes through RETRACTING to UP.
5. Confirm no invisible raycast wheel support or ground-penetration correction remains at the retracted wheel positions.
6. Press `G` again and confirm all three gear assemblies extend and the HUD returns to DOWN.
7. Land normally and confirm the existing raycast suspension, brakes, steering, and hard-stop penetration protection still work.

## Gear removal test

1. Exit the cockpit and stop the aircraft with the gear fully DOWN.
2. Aim at the large bolt near the left main gear mount.
3. Hold `R` and confirm the large mount bolt/left gear assembly is removed and appears beside the aircraft.
4. Confirm the left raycast wheel no longer supports the aircraft.
5. Hold `E` at the left mount target to reinstall that exact gear assembly.
6. Repeat for the right main and tail gear.
7. Confirm tire pressure/health follows the gear assembly through removal and reinstallation.

## Tire and rim test

1. Aim at a main tire and press `X`; note health and PSI.
2. Hold `R` to remove the tire from its rim.
3. Confirm the larger main rim remains visible without the tire.
4. Hold `E` without a replacement tire equipped and confirm the same removed tire goes back on with the same health and PSI.
5. Repeat on the tailwheel and confirm the tail rim/tire are visibly smaller.

## Nitrogen-cart test

Controls while on foot:

- Aim at a tire/valve and press `N` to connect the nearest nitrogen cart.
- Aim at the cart and use `Q` / `Z` to raise/lower regulator setpoint.
- Hold `F` at the cart to move tire pressure toward the setpoint.
- Press `N` to disconnect.
- Press `X` at a tire to inspect exact pressure and health.

Recommended pressures are 30 PSI for each main tire and 24 PSI for the tail tire.

## Underinflation damage test

1. Lower one main tire well below 30 PSI with the nitrogen cart.
2. Press `X` to record its starting health.
3. Perform a deliberately firm but controllable landing.
4. Compare both main tires.
5. Confirm the underinflated side loses more health and has more rolling drag/visual sag.

## Tire-wear visual test

As tire health falls, confirm the rubber progressively looks duller/browner and more worn. At zero health the tire should be visibly collapsed rather than looking new.

## Overpressure/burst test

1. Connect the nitrogen cart to a main tire.
2. Increase the regulator above the recommended value.
3. Hold `F` until tire pressure reaches the main burst threshold (about 43 PSI).
4. Confirm the tire fails, collapses visually, and the affected wheel produces very strong rolling drag similar to a brake being applied on that side.
5. The tail tire uses a lower burst threshold of about 35 PSI.

## Replacement-tire test

1. Remove the failed tire from the rim and leave the damaged tire on the floor.
2. Buy a `P-51 Main Landing Tire` ($450) or `P-51 Tailwheel Tire` ($180), matching the rim being serviced.
3. Pick up and equip the replacement tire from inventory.
4. Aim at the bare rim and hold `E`.
5. Confirm the purchased tire is consumed and a new 100% tire is mounted while the old failed tire remains separate.
6. The new tire intentionally begins only partially inflated (about 8 PSI main / 6 PSI tail).
7. Use the nitrogen cart to set it to 30 PSI main or 24 PSI tail before flight.

## Final regression checks

- Normal takeoff and landing still work.
- Hard landings do not push the wheel anchors through the map.
- A retracted or removed gear does not create invisible wheel support.
- `E` cockpit exit behavior remains unchanged.
- Engine condition, removable covers/plugs, oil service, hoist, commerce, towing, and external camera behavior remain available.
- Run the existing Windows Build and Run validation after all Play-mode tests pass.
