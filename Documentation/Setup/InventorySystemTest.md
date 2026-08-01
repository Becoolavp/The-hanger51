# Inventory System Test

## Goal

Add a small, testable inventory foundation with eight slots, an inventory panel, world-item pickup interaction, item stacking, and placeholder maintenance items.

## Assumptions

- The first-person movement test scene already works.
- The project is on branch `agent/inventory-ui-foundation`.
- The active scene contains a GameObject named `Player` with `FirstPersonController` and a child Camera.
- The current milestone is display and pickup only. Moving, dropping, using, and saving items come later.

## Files created or changed

- `Assets/_Project/Scripts/Inventory/InventoryItemDefinition.cs`
- `Assets/_Project/Scripts/Inventory/InventorySlotData.cs`
- `Assets/_Project/Scripts/Inventory/PlayerInventory.cs`
- `Assets/_Project/Scripts/Inventory/InventoryPickup.cs`
- `Assets/_Project/Scripts/Inventory/InventoryInteractor.cs`
- `Assets/_Project/Scripts/Inventory/InventorySlotView.cs`
- `Assets/_Project/Scripts/Inventory/InventoryUI.cs`
- `Assets/_Project/Editor/InventorySystemSetup.cs`
- `Assets/_Project/Scripts/Player/FirstPersonController.cs`

## Unity Editor setup

Follow the numbered steps in order.

### 1. Install the inventory system

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Make sure Unity is not in Play mode.
3. Select **Hanger 51 > Inventory > 1 - Install Inventory System**.
4. Open **Window > General > Console**.
5. Confirm the Console reports **Inventory Step 1 complete**.
6. Save the scene with `Ctrl+S`.

Step 1 automatically creates:

- `PlayerInventory` on the Player.
- `InventoryInteractor` on the Player.
- A screen-space Canvas named `Inventory UI`.
- A crosshair, pickup prompt, status message, and inventory panel.
- Eight inventory slots.
- Three reusable item-definition assets.
- Three colored placeholder pickup cubes.

### 2. Validate the inventory setup

1. Select **Hanger 51 > Inventory > 2 - Validate Inventory Setup**.
2. Confirm the Console reports **Inventory Step 2 passed**.
3. Resolve any red validation message before entering Play mode.

## Controls

| Control | Action |
|---|---|
| E | Pick up the item at the center of the screen |
| I | Open or close inventory |
| Escape | Close inventory when it is open |
| WASD | Continue normal movement while inventory is closed |

## Generated item assets

The setup command creates these assets only when they do not already exist:

- `Assets/_Project/Inventory/Items/ShopRag.asset`
- `Assets/_Project/Inventory/Items/OilFilter.asset`
- `Assets/_Project/Inventory/Items/SparkPlug.asset`

Each item has:

- A stable item ID.
- A display name and description.
- A maximum stack size.
- A placeholder UI color.

## How to test it

1. Run Inventory Steps 1 and 2.
2. Press Play.
3. Confirm a small crosshair appears in the center of the screen.
4. Look directly at one of the three colored cubes near the Player start position.
5. Confirm the prompt says **Press E to pick up...**.
6. Press E.
7. Confirm the cube disappears and a short **Picked up...** status message appears.
8. Press I.
9. Confirm the inventory panel opens with eight slots.
10. Confirm the picked-up item appears with the correct quantity.
11. Confirm WASD and mouse look do not move the Player while inventory is open.
12. Press I or Escape to close inventory.
13. Confirm the cursor locks and normal movement resumes.
14. Pick up the remaining cubes.
15. Confirm identical items stack instead of occupying unnecessary slots.
16. Stop Play mode and check the Console for errors or warnings.

## Common problems

### The Hanger 51 Inventory menu is missing

Open **Window > General > Console**. A compiler error prevents Unity from loading Editor menu scripts. Copy the complete first red error.

### Inventory Step 1 cannot find the Player

Open `FirstPersonMovementTest.unity` and confirm the active scene contains a GameObject named exactly `Player`.

### Text or slot backgrounds do not appear

Confirm the Unity UI package is available and the `Inventory UI` Canvas exists in the Hierarchy. Rerun Inventory Step 1 after resolving any compiler error.

### Looking at a pickup shows no prompt

Aim the center crosshair directly at the cube and move within three meters. Confirm the cube still has a Box Collider and `InventoryPickup` component.

### Pressing E does nothing

Confirm `InventoryInteractor` is attached to Player and Inventory Step 2 passes.

### Pressing I does nothing

Confirm the `Inventory UI` object has an enabled `InventoryUI` component and Inventory Step 2 passes.

### The Player moves while inventory is open

Confirm the latest `FirstPersonController.cs` is compiled. It must expose the menu-input blocking method used by `InventoryUI`.

### Setup creates duplicate test content

Inventory Step 1 intentionally replaces the generated `Inventory UI` and `Inventory Test Pickups` objects so the setup remains repeatable. It does not recreate the entire movement scene.

## Recommended next step

After this milestone passes, add item selection and a simple item-details panel before implementing dropping, using, aircraft installation, durability, or saving.
