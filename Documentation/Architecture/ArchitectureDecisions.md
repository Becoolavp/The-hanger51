# Architecture Decisions

This file records short, practical decisions so future changes do not accidentally replace working systems without explanation.

## ADR-001: Build the game as small playable milestones

**Status:** Accepted

The project will remain playable after each meaningful feature. Large systems will be divided into small changes that can be tested independently.

## ADR-002: Use a Character Controller for the first-person prototype

**Status:** Accepted for prototype

The first player uses Unity's built-in `CharacterController` rather than a Rigidbody.

Reasons:

- It provides straightforward collision-constrained movement.
- It avoids unwanted physics rotation and momentum while the basic feel is being established.
- It is easier for a beginner to inspect and tune.
- It can be replaced later if the final design needs fully physical player movement.

## ADR-003: Use the Unity Input System

**Status:** Accepted

Keyboard and mouse input is read through `UnityEngine.InputSystem`. The first prototype polls devices directly to avoid requiring an Input Actions asset before the movement loop is proven.

A later milestone may introduce an Input Actions asset when rebinding, gamepad support, menus, or multiple gameplay modes justify it.

## ADR-004: Generate repetitive prototype setup with Editor tools

**Status:** Accepted

The first-person test scene is generated from **Hanger 51 > Setup > Create First-Person Test Area**. Existing test players can be updated with **Hanger 51 > Setup > Apply First-Person Smoothing Defaults**.

This reduces manual scene-building mistakes and keeps controller tuning repeatable.

## ADR-005: Keep project-owned assets under `Assets/_Project`

**Status:** Accepted

Scripts, scenes, prefabs, materials, and other game-owned assets will live under `Assets/_Project`. Third-party packages and imported assets should remain outside that folder when practical.

## ADR-006: Smooth horizontal velocity and probe the ground explicitly

**Status:** Accepted for prototype

The controller moves horizontal velocity toward a desired velocity using separate acceleration, deceleration, direction-change, and air-control values.

Grounding uses a short sphere cast beneath the Character Controller instead of depending only on `CharacterController.isGrounded`. Collision flags returned by `CharacterController.Move` remain a secondary confirmation for floor and ceiling contact.

Reasons:

- `isGrounded` can briefly change state near steps, slopes, and frame boundaries.
- A configurable probe is easier to inspect and tune.
- Separate response values allow later adjustment without restructuring the controller.

## ADR-007: Do not apply continuous downward velocity on flat ground

**Status:** Accepted for prototype

The controller sets vertical velocity to zero while the custom ground probe confirms stable ground contact. It does not continuously push the Character Controller downward on every grounded frame.

Reasons:

- Repeated downward movement can cause tiny collision-resolution height changes that are visible through a first-person camera.
- The explicit ground probe already maintains reliable grounded state on the flat prototype floor.
- Slope and stair behavior will be evaluated separately before adding any ground-snapping behavior.

## ADR-008: Ignore ground-probe results while the player is rising

**Status:** Accepted

A ground probe may continue seeing the floor during the first few frames after takeoff. The controller therefore cannot become grounded while vertical velocity is upward.

This keeps gravity active through the entire jump arc and prevents a short constant-velocity section immediately after takeoff.

## Completed systems

- Repository initialized.
- Basic first-person controller written.
- One-click first-person movement test-area generator written.
- Horizontal acceleration, deceleration, and direction-change response added.
- Explicit ground probing added.
- Continuous grounded downward force removed.
- Rising-state ground-probe lockout added for a continuous jump arc.
- Existing-scene smoothing setup command added.
- Setup and gameplay validation procedure documented.

## Validation status

The user has playtested multiple controller revisions. Earlier revisions became progressively jittery during sustained sprinting and repeated A/D reversals, and the jump remained uneven. The latest revision addresses two identified logic problems: grounded downward motion and ground detection during upward movement. It must be pulled, applied with the smoothing-default command, and playtested before the movement milestone is marked complete.
