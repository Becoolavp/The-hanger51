# Inventory System Test

## Goal

Add a small, testable inventory foundation with eight slots, an inventory panel, world-item pickup interaction, item stacking, and placeholder maintenance items. Every feature must be tested in both Play mode and a standalone Windows build.

## Assumptions

- The first-person movement test scene already works.
- The project is on branch `agent/inventory-ui-foundation`.
- The active scene contains a GameObject named `Player` with `FirstPersonController` and a child Camera.
- The current milestone is display and pickup only. Moving, dropping, using, and saving items come later.
- Windows Build Support is installed through Unity Hub.

## Files created or changed

- `Assets/_Project/Scripts/Inventory/InventoryItemDefinition.cs`
- `Assets/_Project/Scripts/Inventory/InventorySlotData.cs`
- `Assets/_Project/Scripts/Inventory/PlayerInventory.cs`
- `Assets/_Project/Scripts/Inventory/InventoryPickup.cs`
- `Assets/_Project/Scripts/Inventory/InventoryInteractor.cs`
- `Assets/_Project/Scripts/Inventory/InventorySlotView.cs`
- `Assets/_Project/Scripts/Inventory/InventoryUI.cs`
- `Assets/_Project/Editor/InventorySystemSetup.cs`
- `Assets/_Project/Editor/Hanger51BuildTools.cs`
- `Assets/_Project/Scripts/Player/FirstPersonController.cs`

## Unity Editor setup

Follow these numbered steps in order.

### 1. Install or refresh the inventory system

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Make sure Unity is not in Play mode.
3. Select **Hanger 51 > Inventory > 1 - Install or Refresh Inventory System**.
4. Open **Window > General > Console**.
5. Confirm the Console reports **Inventory Step 1 complete**.

Step 1 now automatically saves the active scene and prepares it for Build and Run. It also refreshes existing item assets and materials instead of leaving old placeholder values unchanged.

Step 1 creates:

- `PlayerInventory` on the Player.
- `InventoryInteractor` on the Player.
- A screen-space Canvas named `Inventory UI`.
- A crosshair, pickup prompt, status message, and inventory panel.
- Eight inventory slots.
- Three reusable item-definition assets.
- Three visible placeholder pickup cubes.

The pickups are arranged in a triangle:

- Blue Shop Rag: left rear.
- Bright orange Oil Filter: center front.
- Silver Spark Plug: right rear.

### 2. Validate the inventory setup

1. Select **Hanger 51 > Inventory > 2 - Validate Inventory Setup**.
2. Confirm the Console reports **Inventory Step 2 passed**.
3. Resolve any red validation message before entering Play mode.

The validator checks all three pickups by exact name, along with their renderers, materials, colliders, item definitions, UI, Player components, and build preparation.

### 3. Test in Play mode

1. Press Play.
2. Confirm a small crosshair appears in the center of the screen.
3. Confirm all three pickup cubes are visible.
4. Look directly at a cube from within three meters.
5. Confirm the prompt says **Press E to pick up...**.
6. Press E.
7. Confirm the cube disappears and a short **Picked up...** message appears.
8. Press I.
9. Confirm the inventory panel opens with eight slots.
10. Confirm the picked-up item and quantity appear.
11. Confirm movement and mouse look are blocked while inventory is open.
12. Press I or Escape to close inventory.
13. Pick up the remaining cubes.
14. Stop Play mode and check the Console for errors.

### 4. Prepare the current scene for a build

1. Select **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Confirm the Console reports **Build Step 1 passed**.

This saves all open scenes and places the current active scene first in the enabled build list.

### 5. Validate the build setup

1. Select **Hanger 51 > Build > 2 - Validate Build Setup**.
2. Confirm the Console reports **Build Step 2 passed**.

The validator checks that:

- Unity is not in Play mode.
- Scripts are not compiling.
- The active scene is saved and first in the build list.
- Windows Build Support is installed.
- Every enabled build scene exists.

### 6. Build and run the current feature

1. Select **Hanger 51 > Build > 3 - Build and Run Windows**.
2. Wait for Unity to finish building.
3. Confirm the standalone game launches automatically.
4. Repeat the pickup and inventory tests in the standalone build.
5. Close the standalone game after testing.

The build is created at:

`Builds/Windows/TheHanger51.exe`

## Controls

| Control | Action |
|---|---|
| E | Pick up the item at the center of the screen |
| I | Open or close inventory |
| Escape | Close inventory when it is open |
| WASD | Continue normal movement while inventory is closed |

## Generated item assets

- `Assets/_Project/Inventory/Items/ShopRag.asset`
- `Assets/_Project/Inventory/Items/OilFilter.asset`
- `Assets/_Project/Inventory/Items/SparkPlug.asset`

Each item has a stable item ID, display name, description, maximum stack size, and placeholder color. Running Inventory Step 1 refreshes these values and their materials.

## Common problems

### The orange Oil Filter cube is missing

Run **Hanger 51 > Inventory > 1 - Install or Refresh Inventory System**, followed by Inventory Step 2. The refreshed Oil Filter uses a fully opaque bright-orange material and a clearer center-front position.

### The Hanger 51 Inventory or Build menu is missing

Open **Window > General > Console**. A compiler error prevents Unity from loading Editor menu scripts. Copy the complete first red error.

### Inventory Step 1 cannot find the Player

Open `FirstPersonMovementTest.unity` and confirm the active scene contains a GameObject named exactly `Player`.

### Looking at a pickup shows no prompt

Aim the center crosshair directly at the cube and move within three meters. Confirm Inventory Step 2 passes.

### Pressing E or I does nothing

Run Inventory Step 2. Confirm `InventoryInteractor` and `PlayerInventory` are attached to Player and `Inventory UI` contains an enabled `InventoryUI` component.

### The Player moves while inventory is open

Confirm the latest `FirstPersonController.cs` is compiled. It must expose the external input-blocking method used by `InventoryUI`.

### Build validation says Windows support is missing

Open Unity Hub, locate the installed Unity version, select its gear or options menu, choose **Add modules**, and install **Windows Build Support**.

### The standalone build does not contain the latest feature

Keep the feature scene open, rerun Build Steps 1 and 2, then use Build Step 3. Build Step 1 saves all open scenes before building.

## Recommended next step

After the Editor and standalone tests pass, add item selection and a simple item-details panel before implementing dropping, using, aircraft installation, durability, or saving.
