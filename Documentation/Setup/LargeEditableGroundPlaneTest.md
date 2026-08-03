# Large Editable Ground Plane

This milestone adds one ordinary scene GameObject named exactly `Plane` beneath the current hangar, runway, and P-51 test area.

The initial Plane is approximately 2,500 meters by 2,500 meters. It is centered around the current runway and positioned slightly below the paved runway surface so the two surfaces do not flicker through each other.

The setup gives the Plane its own saved mesh asset, a ground material, and a non-trigger MeshCollider. It does not modify the hangar, runway, P-51, tow bar, engine, or flight systems.

## Install

1. Pull `agent/merlin-engine-assembly`.
2. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
3. Exit Play mode.
4. Run **Hanger 51 > Environment > 1 - Add Large Editable Ground Plane**.
5. Wait for the scene and generated assets to save.
6. Run **Hanger 51 > Environment > 2 - Validate Large Editable Ground Plane**.

Expected validation message:

`Environment Step 2 passed.`

## Scene hierarchy

The setup creates a top-level object named:

`Plane`

Generated assets are saved under:

- `Assets/_Project/Environment/Meshes/LargeEditableGroundPlane.asset`
- `Assets/_Project/Environment/Materials/LargeGroundPlane.mat`

## Basic editing

Select `Plane` in the Hierarchy.

Use the Transform component to edit it immediately:

- Increase **Scale X** to make the area wider.
- Increase **Scale Z** to make the area longer.
- Move **Position X/Z** to shift the entire environment area.
- Move **Position Y** to raise or lower the ground.
- Rotate around **Y** if a future layout requires it.

The default Unity Plane mesh is 10 meters by 10 meters before scaling. At the generated scale of 250 on X and Z, the visible ground is approximately 2,500 meters square.

## Reshaping vertices later

The project does not currently include ProBuilder. The Plane still owns a unique project mesh, so it is ready to be converted for vertex editing later without sharing Unity's built-in Plane mesh.

A future ProBuilder workflow can be used to:

- Select and move individual vertices.
- Raise hills or lower depressions.
- Add subdivisions.
- Extrude edges.
- Create irregular ground boundaries.

Do not install a random third-party mesh editor merely to reshape this Plane. Use Unity ProBuilder or a modeled terrain mesh when that milestone begins.

## Important rerun behavior

Running Environment Step 1 again will preserve an existing active object named `Plane`.

It will not reset:

- Position
- Rotation
- Scale
- Existing project mesh edits
- A custom material you assigned afterward

It only restores missing required components and confirms the mesh and collider are connected.

## Play-mode test

1. Enter Play mode.
2. Walk away from the hangar in several directions.
3. Confirm the Player remains above visible ground.
4. Tow the P-51 away from its original location.
5. Confirm the aircraft remains over the larger ground area.
6. Taxi on the runway and confirm the lower Plane does not flicker through the asphalt.
7. Exit Play mode.

## Standalone test

1. Run **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Run **Hanger 51 > Build > 2 - Validate Build Setup**.
3. Run **Hanger 51 > Build > 3 - Build and Run Windows**.
4. Walk, tow, and taxi away from the original hangar area.
5. Confirm the Plane is visible and has collision throughout the expanded area.

## If the runway flickers

Select `Plane` and lower its **Position Y** by approximately `0.05` to `0.15` meters. The runway should remain above the Plane.

## If objects appear below the Plane

Raise the Plane carefully using **Position Y**. Keep the paved runway slightly above it to avoid overlapping surfaces.
