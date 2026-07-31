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

The controller now moves its horizontal velocity toward a desired velocity using separate acceleration and deceleration values. This avoids instant speed changes while keeping the controller responsive.

Grounding uses a short sphere cast beneath the Character Controller instead of depending only on `CharacterController.isGrounded`. The collision flags returned by `CharacterController.Move` remain a secondary confirmation for floor and ceiling contact.

Reasons:

- `isGrounded` can briefly change state near steps, slopes, and frame boundaries.
- A configurable probe is easier to inspect and tune.
- Separate ground and air smoothing values allow later adjustment without restructuring the controller.

## Completed systems

- Repository initialized.
- Basic first-person controller written.
- One-click first-person movement test-area generator written.
- Horizontal acceleration and deceleration added.
- Explicit ground probing added for jump stability.
- Existing-scene smoothing setup command added.
- Setup and gameplay validation procedure documented.

## Validation status

Initial movement has been playtested by the user. The original movement and jump behavior required additional smoothing and grounding changes. The current revision must be pulled, applied with the smoothing-default command, and playtested again before the movement milestone is marked complete.
