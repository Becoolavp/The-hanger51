# P-51 Serviceable Wing Armament Test

This test covers the game-authentic P-51 wing armament maintenance loop added by P-51 Steps 32 and 33.

## Editor setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity` and remain out of Play mode.
2. Run `Hanger 51 > P-51 Mustang > 32 - Add Serviceable Wing Armament`.
3. Run `Hanger 51 > P-51 Mustang > 33 - Validate Serviceable Wing Armament`.
4. Confirm Step 33 passes with no red Console errors.

## Shop and shipment

1. Enter Play mode and use the Hanger 51 shop terminal.
2. Confirm the **Armament** category contains:
   - `P-51 M2 Wing Gun`
   - `P-51 Wing Ammunition Box`
3. Buy one of each.
4. Open each shipment crate.
5. Confirm the delivered gun and ammunition box are visible physical objects and can be picked up into inventory.
6. Drop each item once and confirm it can be picked up again.

## Wing access panels

1. Walk to the top of either wing while outside the cockpit.
2. Aim at the large armament access panel.
3. Press `E`.
4. Confirm the panel hinges upward rather than disappearing.
5. Confirm a dark recessed armament bay becomes visible beneath it.
6. Confirm the bay exposes **three gun stations and three ammunition positions**.
7. Press `E` on the panel again and confirm it closes and hides the internal service targets.

## Gun installation

1. Open the wing panel.
2. Equip a `P-51 M2 Wing Gun` in inventory.
3. Confirm each empty gun position highlights while the gun is equipped.
4. Aim at one highlighted mount and hold `E`.
5. Confirm the hold-down bolts visibly move/tighten during the hold.
6. Confirm the gun appears installed at completion and the inventory gun is consumed.
7. Inspect with `X` and confirm that station reports `gun installed`.
8. With no ammunition box installed, hold `R` and confirm the gun can be unbolted and returned to inventory.

## Ammunition installation

1. Reinstall a gun.
2. Equip a `P-51 Wing Ammunition Box`.
3. Confirm the adjacent ammunition position highlights.
4. Hold `E` on the ammunition position.
5. Confirm the box appears in the compartment and a visible feed belt connects toward the gun.
6. Inspect with `X` and confirm ammunition is reported for that station.
7. Repeat until desired stations are loaded.

## Six-station layout

Confirm there are three independently serviceable gun positions in the left wing and three independently serviceable gun positions in the right wing. Each station must have its own installed-gun state, ammunition-box state, ammunition count, muzzle point, and casing-ejection point.

## Flight firing

1. Close both wing armament panels.
2. Enter the cockpit.
3. Confirm the armament HUD shows installed gun count and total ammunition.
4. Hold **Left Ctrl**.
5. Confirm every installed-and-loaded station fires together.
6. Confirm visible muzzle flashes and short tracer lines appear ahead of installed guns.
7. Confirm small spent casing objects eject downward/outward from the wing and fall under physics.
8. Confirm the HUD ammunition total decreases while firing.
9. Continue until one box is empty and confirm that station stops firing while other loaded stations continue.
10. Exit the cockpit, reopen the panel, and confirm an empty box can be cleared with `R` and replaced with a new purchased box.

## Safety/state checks

- Guns cannot be serviced while the player is in the cockpit.
- Guns cannot fire while either armament access panel is open.
- A gun cannot be removed while its ammunition box is still installed.
- Ammunition cannot be installed until the adjacent gun is installed.
- A partially used ammunition box currently remains installed until it is fired empty; an unused full box may be returned to inventory.
- Firing uses game visual ray/tracer effects only; this system does not recreate real internal weapon mechanics.
