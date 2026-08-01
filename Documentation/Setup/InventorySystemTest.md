# Inventory System Test

## Goal

Provide a small inventory foundation with eight clickable slots, readable quantity badges, item selection, equip and unequip controls, one-at-a-time dropping, world pickup interaction, item stacking, and standalone Windows build validation.

## Assumptions

- The first-person movement test scene already works.
- The project is on branch `agent/inventory-ui-foundation`.
- The active scene contains a GameObject named `Player` with `FirstPersonController` and a child Camera.
- Equip currently uses a colored placeholder object rather than a finished hand-held model.
- Drop One removes one unit from the selected stack and creates a pickup in front of the Player.
- Windows Build Support is installed through Unity Hub.

## Files created or changed

- `Assets/_Project/Scripts/Inventory/InventoryItemDefinition.cs`
- `Assets/_Project/Scripts/Inventory/InventorySlotData.cs`
- `Assets/_Project/Scripts/Inventory/PlayerInventory.cs`
- `Assets/_Project/Scripts/Inventory/InventoryPickup.cs`
- `Assets/_Project/Scripts/Inventory/InventoryInteractor.cs`
- `Assets/_Project/Scripts/Inventory/InventorySlotView.cs`
- `Assets/_Project/Scripts/Inventory/InventoryUI.cs`
- `Assets/_Project/Scripts/Inventory/InventoryItemDropper.cs`
- `Assets/_Project/Scripts/Inventory/EquippedItemView.cs`
- `Assets/_Project/Editor/InventorySystemSetup.cs`
- `Assets/_Project/Editor/InventoryEquipmentSetup.cs`
- `Assets/_Project/Editor/Hanger51BuildTools.cs`
- `Assets/_Project/Scripts/Player/FirstPersonController.cs`

## Unity Editor setup

Follow these numbered steps in order.

### 1. Install or refresh the basic inventory

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Make sure Unity is not in Play mode.
3. Select **Hanger 51 > Inventory > 1 - Install or Refresh Inventory System**.
4. Open **Window > General > Console**.
5. Confirm the Console reports **Inventory Step 1 complete**.

Step 1 saves the active scene, prepares it for Build and Run, refreshes item assets and materials, and creates the basic inventory, UI, prompt, and three test pickups.

### 2. Validate the basic inventory

1. Select **Hanger 51 > Inventory > 2 - Validate Inventory Setup**.
2. Confirm the Console reports **Inventory Step 2 passed**.
3. Resolve any red validation message before continuing.

### 3. Install the corrected equipment and drop UI

1. Select **Hanger 51 > Inventory > 3 - Install Equipment and Drop UI**.
2. Confirm the Console reports **Inventory Step 3 complete**.

Step 3 automatically:

- Rebuilds the inventory panel with more space.
- Places every quantity inside a dedicated top-right badge.
- Makes occupied slots clickable.
- Adds a selected-item name and description panel.
- Adds **Equip/Unequip** and **Drop One** buttons.
- Adds an Input System-compatible EventSystem for mouse clicks.
- Adds `InventoryItemDropper` to Player.
- Adds a colored equipped-item placeholder to the Player Camera.
- Saves the scene and prepares the standalone build.

### 4. Validate equipment and dropping

1. Select **Hanger 51 > Inventory > 4 - Validate Equipment and Drop UI**.
2. Confirm the Console reports **Inventory Step 4 passed**.
3. Resolve any red validation message before entering Play mode.

Step 4 checks the clickable slots, quantity badges, action buttons, UI EventSystem, item dropper, equipped-item view, and build preparation.

## Play-mode test

1. Press Play.
2. Confirm the crosshair and all three pickup cubes appear.
3. Pick up the blue Shop Rag with `E`.
4. Pick up the orange Oil Filter with `E`.
5. Pick up the silver Spark Plugs with `E`.
6. Press `I` to open inventory.
7. Confirm all quantities appear in dark badges in the top-right corner of their slots.
8. Confirm no quantity overlaps an item name, slot number, or another quantity.
9. Click the Shop Rag slot.
10. Confirm the slot changes color to show selection.
11. Confirm the right panel shows the Shop Rag name, description, and quantity.
12. Click **Equip**.
13. Confirm the right panel reads **Equipped: Shop Rag**.
14. Confirm a blue placeholder object appears in the lower-right portion of the first-person view.
15. Click the Shop Rag slot again, then click **Unequip**.
16. Confirm the placeholder disappears.
17. Select the Spark Plug stack.
18. Record its quantity.
19. Click **Drop One**.
20. Confirm the quantity decreases by exactly one.
21. Close inventory with `I` or `Escape`.
22. Confirm a silver pickup cube appears on the floor in front of the Player.
23. Aim at the dropped cube and press `E`.
24. Reopen inventory and confirm the Spark Plug quantity returns to its previous value.
25. Confirm movement and mouse look remain blocked while inventory is open.
26. Stop Play mode and check the Console for errors.

## Standalone build test

### 5. Prepare the scene

1. Select **Hanger 51 > Build > 1 - Prepare Current Scene for Build**.
2. Confirm the Console reports **Build Step 1 passed**.

### 6. Validate the build

1. Select **Hanger 51 > Build > 2 - Validate Build Setup**.
2. Confirm the Console reports **Build Step 2 passed**.

### 7. Build and run Windows

1. Select **Hanger 51 > Build > 3 - Build and Run Windows**.
2. Wait for the standalone game to launch.
3. Repeat the pickup, quantity-badge, slot-selection, equip, unequip, drop, and repickup tests.
4. Close the standalone game after testing.

The build is created at:

`Builds/Windows/TheHanger51.exe`

## Controls

| Control | Action |
|---|---|
| E | Pick up the item at the center of the screen |
| I | Open or close inventory |
| Escape | Close inventory when it is open |
| Mouse click | Select a slot or press an inventory action button |
| WASD | Normal movement while inventory is closed |

## Common problems

### Quantity numbers still overlap

Run Inventory Step 3 again. The corrected UI uses a child object named `Quantity Badge` inside every slot. Then rerun Inventory Step 4.

### Inventory slots or buttons do not respond to clicks

Run Inventory Step 3 again and then Step 4. The scene must contain an `EventSystem` with `InputSystemUIInputModule`. The cursor should be visible while inventory is open.

### Equip changes the text but no placeholder appears

Run Inventory Step 3 again. Confirm Player Camera contains `Equipped Item Holder` with an enabled `EquippedItemView` component.

### Drop One does not create a pickup

Confirm Player has `InventoryItemDropper`, select an occupied slot, and run Inventory Step 4. The dropped cube is placed approximately 1.5 meters in front of Player and aligned to the floor when a floor is found.

### Dropping the last equipped item

When the final copy of an equipped item leaves the inventory, the inventory automatically unequips it and hides the equipped placeholder.

### The Hanger 51 Inventory or Build menu is missing

Open **Window > General > Console**. A compiler error prevents Unity from loading Editor menu scripts. Copy the complete first red error.

### Build validation says Windows support is missing

Open Unity Hub, locate the installed Unity version, select its options menu, choose **Add modules**, and install **Windows Build Support**.

### The standalone build does not contain the latest feature

Keep the feature scene open and rerun Inventory Step 3, Inventory Step 4, and Build Steps 1 through 3.

## Recommended next step

After both Play mode and the standalone build pass, the next inventory milestone should replace the generic equipped cube with item-specific hand models and add a deliberate use or installation action for aircraft-maintenance gameplay.
