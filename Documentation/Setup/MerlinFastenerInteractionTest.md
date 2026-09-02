# Merlin Cover-Bolt and Spark-Plug Interaction Test

## Purpose

Validate the physical V-1650 assembly flow added after the base engine assembly feature:

1. Equip a cylinder cover.
2. Use its highlighted mounting area to lower it onto the engine.
3. Tighten six highlighted bolts on each cover.
4. Equip spark plugs only after both covers are secured.
5. Screw two spark plugs into each of the twelve cylinders.
6. Repeat the flow in the standalone Windows build.

## Required branch

`agent/merlin-engine-assembly`

## Numbered Unity setup

### 1. Generate the base engine assembly

Open:

`Assets/_Project/Scenes/FirstPersonMovementTest.unity`

Run:

`Hanger 51 > Merlin Assembly > 1 - Install or Refresh V-1650 Assembly`

Expected Console result:

`Merlin Step 1 complete`

### 2. Validate the base engine assembly

Run:

`Hanger 51 > Merlin Assembly > 2 - Validate V-1650 Assembly`

Expected Console result:

`Merlin Step 2 passed`

### 3. Add the physical fastener interactions

Run:

`Hanger 51 > Merlin Assembly > 4 - Add Highlights and Fastener Interactions`

Expected Console result:

`Merlin Step 4 complete`

This step:

- makes the cylinder cover equippable;
- adds two highlighted cover mounting zones;
- adds six interactive bolts to each cover;
- moves all 24 installed spark-plug visuals onto the tops of the covers;
- creates two plug wells per cylinder;
- adds hold-E placement, tightening, and threading animations;
- saves the active scene;
- prepares the active scene for Build and Run.

### 4. Validate the fastener interactions

Run:

`Hanger 51 > Merlin Assembly > 5 - Validate Highlights and Fasteners`

Expected Console result:

`Merlin Step 5 passed`

The validator checks:

- two cover mounting targets;
- twelve interactive cover bolts;
- twenty-four spark-plug targets;
- equippable cover and spark-plug item definitions;
- two named plugs per cylinder;
- standalone build preparation.

## Play-mode validation

### 5. Collect the required parts

1. Press Play.
2. Pick up the engine block.
3. Pick up both cylinder covers.
4. Pick up all 24 spark plugs.
5. Open inventory with `I`.
6. Confirm the inventory contains one block, two covers, and 24 spark plugs.

### 6. Place the engine block

1. Aim at the engine stand.
2. Open inventory with `I`.
3. Select the engine block.
4. Click **Place Engine Block**.
5. Close inventory.
6. Confirm the engine core appears on the stand.

### 7. Place the first cover

1. Open inventory.
2. Select the cylinder-cover stack.
3. Click **Equip**.
4. Close inventory.
5. Confirm a yellow mounting area appears on an uncovered engine bank.
6. Aim at that yellow area.
7. Hold `E` until the cover lowers fully into position.
8. Confirm one cover is removed from inventory.

### 8. Tighten the first cover

1. Confirm six yellow bolt locations appear on the placed cover.
2. Aim at one highlighted bolt.
3. Hold `E` until the bolt rotates and seats.
4. Repeat for the other five bolts.
5. Confirm the cover-bolt progress increases once per completed bolt.
6. Confirm releasing `E` early resets that bolt rather than completing it.

### 9. Place and secure the second cover

1. Keep the remaining cover equipped or equip it again.
2. Use the second highlighted mounting zone.
3. Hold `E` to lower the cover into place.
4. Tighten all six highlighted bolts.
5. Confirm the total cover-bolt progress reaches `12/12`.

### 10. Install the spark plugs

1. Open inventory.
2. Select the spark-plug stack.
3. Click **Equip**.
4. Close inventory.
5. Confirm the open plug wells on top of both covers are highlighted.
6. Aim at any highlighted well.
7. Hold `E`.
8. Confirm the plug appears above the well, rotates several turns, and lowers into the cover.
9. Confirm exactly one spark plug is removed from inventory.
10. Repeat until all 24 plugs are installed.
11. Confirm each of the six cylinders on each bank has two plugs.
12. Confirm final progress reports the V-1650 assembly as complete.

## Standalone Build and Run validation

### 11. Prepare the current scene

Run:

`Hanger 51 > Build > 1 - Prepare Current Scene for Build`

Expected:

`Build Step 1 passed`

### 12. Validate the build setup

Run:

`Hanger 51 > Build > 2 - Validate Build Setup`

Expected:

`Build Step 2 passed`

### 13. Build and run Windows

Run:

`Hanger 51 > Build > 3 - Build and Run Windows`

Repeat these checks in the standalone build:

1. Place the engine block.
2. Equip and place both covers using the highlighted zones.
3. Tighten all twelve cover bolts.
4. Equip the spark plugs.
5. Screw several plugs into highlighted wells.
6. Confirm each completed action consumes the correct inventory quantity.
7. Confirm movement, inventory, highlighting, and hold-E interaction remain responsive.

## Expected hierarchy additions

Under `V-1650 Engine Stand`:

- `Interactive Fastener System`
  - two cover mount targets;
  - twenty-four spark-plug targets.
- `Installed Left Cylinder Cover`
  - `Interactive Cover Bolts`
    - six bolt targets.
- `Installed Right Cylinder Cover`
  - `Interactive Cover Bolts`
    - six bolt targets.

## Common problems

### No highlighted cover area

Confirm:

- the engine block is already on the stand;
- the cover is equipped, not merely selected;
- Merlin Step 4 was run after Merlin Step 1;
- the inventory was closed after equipping.

### Bolts do not highlight

The cover must first be completely placed. If the cover is visible but no bolts appear, rerun Merlin Step 4 and then Step 5.

### Spark-plug wells do not highlight

All twelve cover bolts must be fully tightened before plug installation unlocks. The spark-plug item must also be equipped.

### The stand prompt appears instead of the bolt or plug prompt

The updated interactor uses all raycast hits and prioritizes the smaller physical target. Confirm the latest `InventoryInteractor.cs` compiled and that Step 5 passes.

### A plug does not consume from inventory

The plug must remain equipped and present in an inventory slot until the hold completes. Releasing `E` early intentionally consumes nothing.

### Editor works but the standalone build does not

Rerun Merlin Steps 4 and 5, then Build Steps 1 through 3. Confirm the active scene was saved after Step 4.
