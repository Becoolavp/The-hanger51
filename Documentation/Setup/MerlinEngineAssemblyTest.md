# Merlin V-1650 Engine Assembly Test

## Goal

Validate the first aircraft-maintenance assembly loop using the existing inventory system:

- Pick up one Merlin V-1650-style engine block.
- Pick up two cylinder-bank covers.
- Pick up 24 individual spark plugs.
- Place the engine block onto the maintenance stand.
- Install both covers.
- Install all 24 spark plugs.
- Confirm the same flow works in a standalone Windows build.

## Generated content

The setup tool creates or refreshes:

- A detailed, black-and-metallic 60-degree V-12 engine block.
- Front reduction-gear housing and propeller shaft.
- Rear supercharger housing and intake.
- Two six-cylinder banks.
- Twelve exhaust stubs.
- Ignition rails, intake runners, coolant crossover, magnetos, and visible hardware.
- Two removable cylinder-bank covers.
- Twenty-four separate multi-part metal-and-ceramic spark plugs.
- A wheeled steel engine stand.
- An Install button and assembly-target status line in the inventory UI.

The art is a procedural prototype built from Unity primitives. It is intended to establish proportion, recognizability, interaction, and assembly behavior before final imported 3D art.

## Numbered Unity setup

### 1. Pull the feature branch

1. Open GitHub Desktop.
2. Select `The-hanger51`.
3. Switch to `agent/merlin-engine-assembly`.
4. Click **Fetch origin**.
5. Click **Pull origin** when shown.
6. Return to Unity and wait for compilation to finish.
7. Open **Window > General > Console**.
8. Confirm there are no red compiler errors.

### 2. Open the movement test scene

Open:

`Assets/_Project/Scenes/FirstPersonMovementTest.unity`

Do not recreate the movement test area.

### 3. Generate the models, items, stand, UI, and pickups

Select:

**Hanger 51 > Merlin Assembly > 1 - Install or Refresh V-1650 Assembly**

Expected Console result:

`Merlin Step 1 complete`

This command is repeatable. It replaces only the generated `V-1650 Assembly Test` scene root and refreshes its generated assets. It does not move or delete the earlier Shop Rag, Oil Filter, or other inventory test objects.

### 4. Validate the generated feature

Select:

**Hanger 51 > Merlin Assembly > 2 - Validate V-1650 Assembly**

Expected Console result:

`Merlin Step 2 passed`

The validator checks:

- One engine-block pickup.
- Two cylinder-cover pickups.
- Twenty-four spark-plug pickups.
- Two installed cover visual positions.
- Twenty-four installed spark-plug visual positions.
- The Install inventory button.
- Item-specific world prefabs.
- Standalone build readiness.

## Play-mode test

### 5. Inspect the generated parts

1. Press Play.
2. Walk to the new parts area.
3. Confirm the engine block, both covers, and all spark plugs are laid out separately.
4. Inspect the spark plug closely and confirm it has:
   - metal threaded shell;
   - thread rings;
   - copper gasket;
   - metal shell and terminal;
   - ceramic insulator and ribs;
   - center and ground electrodes.
5. Inspect the engine block and confirm it reads as a long V-12 rather than a generic cube.

### 6. Pick up the assembly parts

1. Pick up the engine block with `E`.
2. Pick up both cylinder covers.
3. Pick up all 24 spark plugs.
4. Open inventory with `I`.
5. Confirm the inventory contains:
   - one Merlin V-1650 Engine Block;
   - two V-1650 Cylinder Covers;
   - 24 V-1650 Spark Plugs.
6. Confirm the engine block and covers say **Install Only** instead of allowing Equip.
7. Equip a spark plug and confirm the detailed spark-plug model appears in first person.
8. Unequip it.

### 7. Place the engine block

1. Close inventory.
2. Walk to the engine stand.
3. Aim the crosshair at the stand until the prompt mentions placing the engine block.
4. Press `I` without moving the crosshair away first.
5. Select the engine block inventory slot.
6. Confirm the green action button says **Place Engine Block**.
7. Click it.
8. Confirm the inventory quantity decreases by one.
9. Confirm the engine appears on the stand.

### 8. Install both cylinder covers

1. Keep the inventory open.
2. Select the cylinder-cover slot.
3. Click **Install Cover** once.
4. Confirm one bank cover appears.
5. Click **Install Cover** again.
6. Confirm the second bank cover appears.
7. Confirm the cover quantity reaches zero.

The spark-plug Install action remains unavailable until both covers are installed.

### 9. Install the spark plugs

1. Select the spark-plug slot.
2. Click **Install Spark Plug** repeatedly.
3. Confirm one visible plug is added per click.
4. Confirm the progress line advances from `0/24` to `24/24`.
5. Confirm the final stand prompt reports that the assembly is complete.
6. Confirm the inventory spark-plug quantity reaches zero.

### 10. Test dropping and repickup

Before installing every spark plug:

1. Select the spark-plug stack.
2. Click **Drop One**.
3. Close inventory.
4. Confirm the dropped object uses the detailed spark-plug model rather than a cube.
5. Pick it back up with `E`.
6. Continue installation.

## Standalone build test

### 11. Prepare the scene

Select:

**Hanger 51 > Build > 1 - Prepare Current Scene for Build**

Expected result:

`Build Step 1 passed`

### 12. Validate the build

Select:

**Hanger 51 > Build > 2 - Validate Build Setup**

Expected result:

`Build Step 2 passed`

### 13. Build and run Windows

Select:

**Hanger 51 > Build > 3 - Build and Run Windows**

In the standalone game, repeat:

1. Pick up the engine block, covers, and several spark plugs.
2. Place the block on the stand.
3. Install both covers.
4. Install several spark plugs.
5. Drop and repick up one detailed spark plug.
6. Confirm there are no missing models, input failures, or empty scenes.

## Resetting the test

Select:

**Hanger 51 > Merlin Assembly > 3 - Reset Assembly and Respawn Parts**

This regenerates the stand and all 27 pickups with an empty assembly state.

## Common problems

### The Merlin Assembly menu is missing

Unity has not compiled the latest scripts. Check the first red Console error and confirm the current GitHub branch is `agent/merlin-engine-assembly`.

### The Install button is missing

Run Merlin Step 1 again. It upgrades the existing inventory panel after the inventory equipment UI exists.

### Install is disabled

Confirm all of the following:

- The crosshair was aimed at the engine stand before opening inventory.
- The selected item belongs to this assembly.
- The engine block is installed before covers.
- Both covers are installed before spark plugs.
- The required quantity is not already complete.

### A part cannot be picked up

Move within approximately three meters and aim at the visible part itself. Each spark plug has a slightly enlarged invisible pickup collider so it remains practical to target.

### Dropped parts are cubes

The generated item asset has lost its `World Prefab` reference. Rerun Merlin Step 1 and then Merlin Step 2.

### The old placeholder spark plug is still present

Merlin Step 1 refreshes `SparkPlug.asset` and creates a new separate 24-plug test group. Earlier spark-plug test objects can remain elsewhere in the scene, but newly dropped and generated V-1650 plugs use the detailed prefab.

### The standalone build contains old content

Keep `FirstPersonMovementTest.unity` open, rerun Merlin Steps 1 and 2, then repeat Build Steps 1 through 3.
