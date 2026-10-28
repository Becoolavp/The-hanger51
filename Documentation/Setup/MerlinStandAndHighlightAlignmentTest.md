# Merlin Stand and Highlight Alignment Test

Use this checklist after pulling the latest `agent/merlin-engine-assembly` branch.

## Setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Test Hangar > 6 - Rescale Stand and Align Highlights**.
4. Confirm the Console reports `Test Hangar Step 6 complete`.
5. Run **Hanger 51 > Test Hangar > 7 - Validate Stand and Highlights**.
6. Confirm the Console reports `Test Hangar Step 7 passed`.

Step 6 measures the currently scaled engine and covers. It does not reuse the original full-size stand or highlight dimensions.

## Stand test

1. Press Play.
2. Enter the hangar and walk around the empty engine stand.
3. Confirm the stand is only moderately longer and wider than the real-scale engine.
4. Confirm the base rails, cross rails, vertical posts, saddles, braces, and caster wheels form one compact stand.
5. Place the engine block.
6. Confirm the engine is centered over the stand.
7. Confirm both saddles sit below the crankcase rather than far outside it.
8. Confirm the wheels and rails do not extend several meters beyond the engine.
9. Confirm the stand remains large enough to walk around and aim at the engine.

## Cover highlight test

1. Pick up and equip one cylinder cover.
2. Close inventory.
3. Confirm exactly one cover mounting highlight appears.
4. Confirm the highlight is a thin footprint matching the length and width of the scaled cover.
5. Confirm it lies on the cylinder-bank mounting deck.
6. Confirm it follows the same bank angle as the cover.
7. Confirm it is not the old full-size rectangle.
8. Install the cover.
9. Repeat for the opposite bank.

## Bolt highlight test

1. After placing a cover, inspect its six loose bolts.
2. Confirm each available bolt has one small highlight ring.
3. Confirm each ring is centered on its bolt head.
4. Confirm every bolt remains inset from the cover edge.
5. Tighten a front, center, and rear bolt.
6. Confirm each ring disappears when that bolt is secured.
7. Confirm the remaining rings do not overlap each other.
8. Confirm completed bolt geometry does not block adjacent bolt targets.

## Spark-plug highlight test

1. Install and secure both covers.
2. Equip a spark plug.
3. Confirm 24 small plug-well highlights appear.
4. Confirm each ring lies on the top surface of a cover, not below or above it.
5. Confirm each cylinder position has two rings.
6. Confirm the rings follow both bank angles.
7. Install an outer and inner plug on each bank.
8. Confirm the selected ring disappears when its plug is installed.
9. Confirm the remaining rings stay centered on their open wells.
10. Unscrew one installed plug with `R`.
11. Confirm its correctly aligned ring becomes available again when a spark plug is equipped.

## Removal alignment test

1. Aim at an installed plug and hold `R`.
2. Confirm the removal animation begins from the installed position.
3. Remove all plugs from one bank.
4. Loosen one cover bolt with `R`.
5. Confirm the bolt rises from its aligned position.
6. Loosen the remaining bolts.
7. Remove the cover.
8. Equip the returned cover.
9. Confirm its mounting highlight reappears in the same corrected position and scale.

## Standalone build test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Confirm the compact stand appears in the build.
5. Confirm one cover highlight matches the scaled cover.
6. Confirm several bolt highlights are centered and properly sized.
7. Confirm several spark-plug highlights sit on the cover surface.
8. Complete one install and one removal interaction for each highlighted part type.

## Expected generated counts

- 2 cover mounting highlights
- 12 bolt highlights
- 24 spark-plug well highlights

The validator also checks that the stand dimensions remain close to the measured engine dimensions and that the active scene is prepared for the Windows build.
