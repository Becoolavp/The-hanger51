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
- It avoids unwanted physics rotation and momentum while basic movement is established.
- It is easy to inspect and tune in the Unity Editor.
- It can be replaced later if the final design requires fully physical player movement.

## ADR-003: Use the Unity Input System in Dynamic Update mode

**Status:** Accepted

Keyboard and mouse input is read through `UnityEngine.InputSystem` from `MonoBehaviour.Update()`.

The project setting must therefore be:

**Edit > Project Settings > Input System Package > Update Mode > Process Events In Dynamic Update**

The Editor tool **Hanger 51 > Setup > Validate First-Person Project Settings** checks this setting and reports the actual Unity Editor version.

## ADR-004: Generate repetitive prototype setup with Editor tools

**Status:** Accepted

The first-person test scene is generated from **Hanger 51 > Setup > Create First-Person Test Area**. Existing test players can be updated with **Hanger 51 > Setup > Apply First-Person Controller Defaults**.

## ADR-005: Keep project-owned assets under `Assets/_Project`

**Status:** Accepted

Scripts, scenes, prefabs, materials, and other game-owned assets will live under `Assets/_Project`. Third-party packages and imported assets should remain outside that folder when practical.

## ADR-006: Prefer direct movement for the first prototype

**Status:** Accepted, replacing the previous smoothing experiment

The prototype now applies walking and sprinting velocity directly instead of gradually accelerating and decelerating.

Reasons:

- The smoothing experiment made movement feel like sliding on ice.
- Direct movement is easier to validate and debug.
- Movement acceleration can be reconsidered later as an optional feel feature after the basic controller is reliable.

## ADR-007: Use CharacterController grounding for the prototype

**Status:** Accepted, replacing the custom sphere-probe experiment

The controller uses `CharacterController.isGrounded`, a small downward grounded velocity, and the collision flags returned by `CharacterController.Move()`.

The custom ground probe was removed because an overly short probe caused the player to lose grounded state after the first jump.

## Completed systems

- Repository initialized.
- Basic first-person controller written.
- One-click first-person movement test-area generator written.
- Input System update-mode validator added.
- Responsive direct movement restored.
- Repeated jump handling added without relying only on `wasPressedThisFrame`.
- Existing-scene controller-default command added.
- Setup and gameplay validation procedure documented.

## Validation status

The user identified progressive jitter, sliding movement, and unreliable repeated jumping during playtesting. The controller has been simplified and the suspected Input System update-mode mismatch now has an explicit validator. The current revision must be pulled, validated, and playtested before the movement milestone is marked complete.
