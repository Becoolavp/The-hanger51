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

The first-person test scene is generated from **Hanger 51 > Setup > Create First-Person Test Area**. This reduces manual scene-building mistakes and makes the initial setup repeatable.

## ADR-005: Keep project-owned assets under `Assets/_Project`

**Status:** Accepted

Scripts, scenes, prefabs, materials, and other game-owned assets will live under `Assets/_Project`. Third-party packages and imported assets should remain outside that folder when practical.

## Completed systems

- Repository initialized.
- Basic first-person controller written.
- One-click first-person movement test-area generator written.
- Setup and gameplay validation procedure documented.

## Validation status

The code has been reviewed for obvious lifecycle and null-reference risks, but it has not yet been compiled or playtested in Unity. Do not mark the movement milestone complete until the documented gameplay test passes.
