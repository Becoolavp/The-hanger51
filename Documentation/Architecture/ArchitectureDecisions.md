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

Movement, inventory, and build setup commands use numbered Unity menu items. Numbering reduces ambiguity for a beginner and makes test reports easier to diagnose.

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

## ADR-010: The current feature scene must be first in the build

**Status:** Accepted

The build workflow saves the active scene and inserts it as the first enabled build scene. This prevents a standalone build from opening without the feature currently being tested.

## ADR-011: Use item-definition assets for inventory content

**Status:** Accepted

Inventory items are represented by `InventoryItemDefinition` ScriptableObject assets rather than hard-coded strings inside the Player inventory.

Reasons:

- One item definition can be reused by pickups, UI, aircraft parts, shops, and save data.
- Stack size, description, and placeholder color remain in one inspectable asset.
- New items can be added without changing the inventory storage code.

## ADR-012: Keep the inventory fixed and slot-based

**Status:** Accepted for the current prototype

`PlayerInventory` contains eight fixed slots. Existing stacks are filled before empty slots are used.

The UI now allows selecting an occupied slot, but dragging and rearranging slots remain outside the current milestone.

## ADR-013: Block gameplay input through the existing controller

**Status:** Accepted

Opening inventory calls `FirstPersonController.SetExternalInputBlocked(true)`. This unlocks the cursor and prevents walking, jumping, mouse look, and accidental cursor recapture while the inventory panel is open.

This is a small integration point rather than a replacement of the working movement architecture.

## ADR-014: Refresh generated item assets and pickup materials

**Status:** Accepted

The inventory installer updates existing item-definition assets and materials every time it runs. Generated pickup materials are forced to use an opaque placeholder color, and each expected pickup is validated by exact name.

Reasons:

- An old or partially initialized asset should not survive repeated setup runs.
- Placeholder pickup visibility must be deterministic.
- Validation should identify the exact missing or invisible object.

## ADR-015: Every feature must pass standalone Build and Run testing

**Status:** Accepted

Each meaningful feature must pass both Unity Play mode testing and a standalone Windows build.

The permanent workflow is:

1. **Hanger 51 > Build > 1 - Prepare Current Scene for Build**
2. **Hanger 51 > Build > 2 - Validate Build Setup**
3. **Hanger 51 > Build > 3 - Build and Run Windows**

`Hanger51BuildTools` saves all open scenes, puts the active scene first in the enabled build list, checks scene paths and Windows Build Support, builds to `Builds/Windows/TheHanger51.exe`, and launches the result.

## ADR-016: Store equipment state in PlayerInventory

**Status:** Accepted for prototype

`PlayerInventory` owns the currently equipped `InventoryItemDefinition` rather than letting the UI own equipment state.

Reasons:

- Equipment remains correct when the UI closes or is rebuilt.
- Other systems can query the equipped item without depending on a Canvas.
- Dropping the final copy of an equipped item can automatically unequip it.

The current Equip button toggles the selected item between equipped and unequipped.

## ADR-017: Drop one unit at a time

**Status:** Accepted for prototype

`InventoryItemDropper` removes one unit from the selected slot and creates an `InventoryPickup` approximately 1.5 meters in front of the Player. A downward raycast aligns the pickup with the floor when possible.

Reasons:

- Dropping one unit avoids accidentally discarding an entire stack.
- The dropped object immediately reuses the existing E-key pickup workflow.
- Stack behavior can be validated without adding a quantity-selection dialog.

## ADR-018: Use a separate equipped-item view

**Status:** Accepted for placeholder phase

`EquippedItemView` listens to inventory changes and displays a small colored placeholder object under the Player Camera.

Reasons:

- Equipment has an immediate visible result.
- The placeholder proves the data flow before item-specific hand models exist.
- Finished models can replace the placeholder without changing inventory storage.

## ADR-019: Use an Input System UI module for clickable inventory controls

**Status:** Accepted

The equipment installer creates or repairs an `EventSystem` with `InputSystemUIInputModule`. Occupied slots and action buttons use Unity UI `Button` components.

Reasons:

- The project already uses the Unity Input System.
- Mouse clicks must work in both Play mode and standalone builds.
- Slot selection should not require custom mouse-coordinate code.

## Completed systems

- Repository initialized.
- Responsive first-person movement controller written and playtested.
- Stable repeated-jump grounding added and playtested.
- First-person camera smoothing added and playtested.
- Runtime frame pacing added.
- One-click test-area generator written.
- Numbered setup and validation workflow added.
- Test scene build inclusion automated and validated in a standalone build.
- Eight-slot inventory data model added.
- Reusable inventory item-definition assets added.
- E-key world pickup interaction added.
- I-key inventory panel and interaction prompt added.
- Numbered inventory installer and validator added.
- Pickup asset and material refresh added.
- Permanent Windows Build and Run workflow added.
- Top-right quantity badges added to prevent label overlap.
- Clickable slot selection and selected-item details added.
- Equip and unequip state added to PlayerInventory.
- Equipped-item placeholder view added.
- Drop One and repickup workflow added.
- Equipment and drop validation commands added.

## Validation status

The first-person movement scene and standalone build were validated successfully by the user. Basic inventory pickup and display were also validated, including the refreshed orange Oil Filter pickup.

The equipment-and-drop revision is implemented on `agent/inventory-ui-foundation`. It must now pass Inventory Steps 1 through 4, the Play-mode equip/drop checklist, and Build Steps 1 through 3 before this milestone is considered complete.
