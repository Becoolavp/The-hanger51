# Expanded Hangar and Hardware Polish Test

Use this checklist on `agent/merlin-engine-assembly` after pulling the latest branch.

## Numbered setup

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Run **Hanger 51 > Test Hangar > 1 - Build Expanded Hangar and Move Parts**.
4. Confirm the Console reports `Test Hangar Step 1 complete`.
5. Run **Hanger 51 > Test Hangar > 2 - Polish Bolts and Spark Plug Seating**.
6. Confirm the Console reports `Test Hangar Step 2 complete` and reports 12 bolts and 24 spark plugs.
7. Run **Hanger 51 > Test Hangar > 3 - Validate Hangar and Hardware**.
8. Confirm the Console reports `Test Hangar Step 3 passed`.

Step 1 replaces only the old generated test environment. It does not recreate the Player, inventory UI, engine system, or manually saved Unity assets. The V-1650 assembly root is moved as one unit so the existing relative positions of its pallets, parts, tray, and engine stand remain intact.

## Hangar inspection

1. Confirm the former 30-by-30 enclosed room is replaced by a substantially larger outdoor test floor.
2. Confirm a 30-unit-wide by 32-unit-long maintenance hangar stands near the middle of the test area.
3. Confirm the hangar has:
   - a concrete slab;
   - two full side walls;
   - a rear wall;
   - a wide open front doorway;
   - pitched roof panels;
   - visible steel posts and rafters;
   - four overhead lights;
   - safety floor markings;
   - two workbenches.
4. Confirm the Player starts outside the open hangar door.
5. Walk into the hangar and confirm the V-1650 engine block, two covers, 24 spark plugs, display pads, and engine stand are inside.
6. Confirm older loose inventory pickups are arranged on the general-parts workbench.
7. Confirm no pickup is trapped inside a wall, roof, bench, or floor.

## Bolt visual and seating test

1. Place the engine block on the stand.
2. Equip and lower the first cylinder cover.
3. Inspect one highlighted bolt before tightening it.
4. Confirm the bolt includes:
   - a threaded shaft;
   - a washer;
   - a six-sided hex head;
   - a dark socket-style recess.
5. Hold E until the bolt is fully tightened.
6. Confirm the shaft disappears into the cover and the washer rests against the cover surface.
7. Confirm the bottom of the bolt head is not floating above the cover.
8. Confirm the bolt head does not disappear completely through the cover.
9. Repeat on bolts near the front, middle, and rear of both covers.
10. Confirm completed bolt visuals do not block aiming at adjacent bolts.

## Spark-plug depth test

1. Place both covers and tighten all 12 bolts.
2. Equip the spark-plug stack.
3. Inspect a highlighted plug well before installation.
4. Hold E until the plug is completely threaded.
5. Confirm the threaded shell enters the cover.
6. Confirm the copper gasket and lower hex-shell area finish at the cover surface.
7. Confirm the entire threaded shell is not visibly floating above the cover.
8. Confirm the ceramic insulator and terminal remain visible above the cover.
9. Repeat on an inner and outer plug for each bank.
10. Confirm two plugs remain aligned with every cylinder position.
11. Confirm completed spark plugs do not block aiming at nearby open wells.

## Movement and collision test

1. Walk around both sides of the engine stand.
2. Walk between the part pallets, spark-plug tray, and workbench.
3. Confirm the larger area and hangar do not cause the Player to fall through the floor.
4. Confirm the open doorway is wide enough to enter without colliding with an invisible wall.
5. Confirm walking near the hangar frame, walls, roof supports, and benches produces normal physical collision.

## Standalone build test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Confirm the standalone game starts outside the hangar.
5. Enter the hangar and confirm all parts appear inside.
6. Place one cover and tighten at least two bolts.
7. After securing both covers, install at least two spark plugs.
8. Confirm the bolt and spark-plug final depth matches Play mode.

## Technical seating references

The generated cover top surface is approximately cover-local Y `0.448`.

The polished bolt target root is placed at cover-local Y:

`0.448`

The bolt shaft extends below that point while the washer and hex head remain above it.

The spark-plug model's copper gasket is approximately `0.155` units above the spark-plug root. The polished spark-plug root is placed at cover-local Y:

`0.292`

This seats the gasket near cover-local Y `0.447`, placing the threaded shell inside the cover while leaving the hex shell, ceramic insulator, and terminal visible.
