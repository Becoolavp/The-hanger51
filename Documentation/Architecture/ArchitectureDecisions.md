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

Keyboard and mouse input are read through `UnityEngine.InputSystem` from `MonoBehaviour.Update()`.

The project setting must therefore be **Process Events In Dynamic Update**. The numbered setup tool configures and validates this setting.

## ADR-004: Generate repetitive prototype setup with numbered Editor tools

**Status:** Accepted

The Unity menu provides a numbered sequence:

1. Create or recreate the test area.
2. Apply the movement and camera fix.
3. Configure input and frame pacing.
4. Add the test scene to the build.
5. Validate all setup.

Numbering reduces ambiguity for a beginner and makes test reports easier to diagnose.

## ADR-005: Keep project-owned assets under `Assets/_Project`

**Status:** Accepted

Scripts, scenes, prefabs, materials, and other game-owned assets live under `Assets/_Project`. Third-party packages and imported assets should remain outside that folder when practical.

## ADR-006: Prefer direct movement for the first prototype

**Status:** Accepted

Walking and sprinting velocity are applied directly instead of gradually accelerating and decelerating.

Reasons:

- The smoothing experiment made movement feel like sliding on ice.
- Direct movement is easier to validate and debug.
- Acceleration can be reconsidered later as an optional feel feature.

## ADR-007: Use a stable ground probe without constant downward movement

**Status:** Accepted for prototype

Grounding combines `CharacterController.isGrounded`, a short sphere cast beneath the capsule, and collision flags returned by `CharacterController.Move()`.

The controller does not continuously push downward while standing. It also ignores the ground probe while rising so the floor cannot cancel the beginning of a jump.

## ADR-008: Smooth the first-person camera separately from collision movement

**Status:** Accepted for prototype

`FirstPersonCameraSmoother` follows the Player in `LateUpdate()` with a very short smoothing time. The Character Controller remains responsive, while tiny collision-position corrections are prevented from appearing directly as camera jitter.

## ADR-009: Apply runtime frame pacing explicitly

**Status:** Accepted for prototype

Every generated test scene contains a `Game Systems` object with `FramePacingController`. The prototype enables VSync at runtime so the Editor Game view and standalone build use consistent frame pacing.

## ADR-010: The test scene must be first in the build

**Status:** Accepted

The setup tool adds `Assets/_Project/Scenes/FirstPersonMovementTest.unity` as the first enabled build scene. This prevents a standalone build from opening without the test environment or Player.

## Completed systems

- Repository initialized.
- Responsive first-person movement controller written.
- Stable repeated-jump grounding added.
- First-person camera smoothing added.
- Runtime frame pacing added.
- One-click test-area generator written.
- Numbered setup and validation workflow added.
- Test scene build inclusion automated.
- Setup, gameplay, and standalone-build validation documented.

## Validation status

The user identified movement jitter and a standalone build that did not start in the generated test scene. The current revision directly addresses both issues. It must be pulled, applied through numbered Steps 2 through 5, then tested in both Play mode and a standalone build before the movement milestone is complete.
