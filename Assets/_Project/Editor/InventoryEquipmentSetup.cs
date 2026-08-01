using System.Collections.Generic;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hanger51.EditorTools
{
    public static class InventoryEquipmentSetup
    {
        private const string PlayerObjectName = "Player";
        private const string InventoryUiObjectName = "Inventory UI";
        private const string EquippedHolderName = "Equipped Item Holder";

        [MenuItem("Hanger 51/Inventory/3 - Install Equipment and Drop UI")]
        public static void InstallEquipmentAndDropUi()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Inventory Step 3 failed. Exit Play mode before running setup.");
                return;
            }

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError("Inventory Step 3 failed. The active scene needs a GameObject named 'Player'.");
                return;
            }

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            FirstPersonController firstPersonController = player.GetComponent<FirstPersonController>();
            InventoryInteractor interactor = player.GetComponent<InventoryInteractor>();
            Camera playerCamera = player.GetComponentInChildren<Camera>();

            if (inventory == null
                || firstPersonController == null
                || interactor == null
                || playerCamera == null)
            {
                Debug.LogError(
                    "Inventory Step 3 failed. Run Inventory Step 1 first so Player has inventory, interaction, movement, and a Camera.",
                    player);
                return;
            }

            InventoryItemDropper itemDropper = player.GetComponent<InventoryItemDropper>();
            if (itemDropper == null)
            {
                itemDropper = Undo.AddComponent<InventoryItemDropper>(player);
            }

            ConfigureDropper(itemDropper, inventory, playerCamera.transform);
            EnsureInputSystemEventSystem();

            InventoryUI inventoryUI = CreateInventoryUi(
                inventory,
                firstPersonController,
                itemDropper);

            ConfigureInteractor(interactor, inventoryUI);
            CreateEquippedItemView(playerCamera, inventory);

            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(interactor);
            EditorUtility.SetDirty(itemDropper);
            EditorUtility.SetDirty(inventoryUI);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Scene activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Inventory Step 3 failed to save the active scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Inventory Step 3 installed the feature, but build preparation failed. Run Build Step 1.");
                return;
            }

            Selection.activeGameObject = player;
            Debug.Log(
                "Inventory Step 3 complete. Fixed quantity badges, added slot selection, Equip/Unequip, Drop One, "
                + "an equipped-item placeholder, saved the scene, and prepared the build.",
                player);
        }

        [MenuItem("Hanger 51/Inventory/4 - Validate Equipment and Drop UI")]
        public static void ValidateEquipmentAndDropUi()
        {
            bool passed = true;

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError("Inventory Step 4 failed: Player is missing.");
                passed = false;
            }
            else
            {
                if (player.GetComponent<PlayerInventory>() == null)
                {
                    Debug.LogError("Inventory Step 4 failed: PlayerInventory is missing.", player);
                    passed = false;
                }

                if (player.GetComponent<InventoryItemDropper>() == null)
                {
                    Debug.LogError("Inventory Step 4 failed: InventoryItemDropper is missing.", player);
                    passed = false;
                }

                Camera playerCamera = player.GetComponentInChildren<Camera>();
                EquippedItemView equippedItemView = playerCamera != null
                    ? playerCamera.GetComponentInChildren<EquippedItemView>(true)
                    : null;

                if (equippedItemView == null)
                {
                    Debug.LogError("Inventory Step 4 failed: EquippedItemView is missing.", player);
                    passed = false;
                }
            }

            GameObject inventoryUiObject = GameObject.Find(InventoryUiObjectName);
            InventoryUI inventoryUI = inventoryUiObject != null
                ? inventoryUiObject.GetComponent<InventoryUI>()
                : null;

            if (inventoryUI == null)
            {
                Debug.LogError("Inventory Step 4 failed: Inventory UI is missing.");
                passed = false;
            }

            InventorySlotView[] slotViews = inventoryUiObject != null
                ? inventoryUiObject.GetComponentsInChildren<InventorySlotView>(true)
                : new InventorySlotView[0];

            if (slotViews.Length != 8)
            {
                Debug.LogError(
                    $"Inventory Step 4 failed: expected 8 clickable slots but found {slotViews.Length}.");
                passed = false;
            }

            for (int index = 0; index < slotViews.Length; index++)
            {
                Transform quantityBadge = slotViews[index].transform.Find("Quantity Badge");
                if (quantityBadge == null)
                {
                    Debug.LogError(
                        $"Inventory Step 4 failed: Slot {index + 1} is missing its top-right Quantity Badge.");
                    passed = false;
                }
            }

            Button[] buttons = inventoryUiObject != null
                ? inventoryUiObject.GetComponentsInChildren<Button>(true)
                : new Button[0];

            bool hasEquipButton = false;
            bool hasDropButton = false;
            for (int index = 0; index < buttons.Length; index++)
            {
                hasEquipButton |= buttons[index].name == "Equip Button";
                hasDropButton |= buttons[index].name == "Drop Button";
            }

            if (!hasEquipButton || !hasDropButton)
            {
                Debug.LogError("Inventory Step 4 failed: Equip or Drop One button is missing.");
                passed = false;
            }

            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null
                || eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                Debug.LogError(
                    "Inventory Step 4 failed: EventSystem with InputSystemUIInputModule is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Inventory Step 4 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Inventory Step 4 passed. Quantity badges, clickable slots, equipment, dropping, "
                    + "UI input, equipped view, and standalone build setup are ready.");
            }
        }

        private static InventoryUI CreateInventoryUi(
            PlayerInventory inventory,
            FirstPersonController firstPersonController,
            InventoryItemDropper itemDropper)
        {
            GameObject existingUi = GameObject.Find(InventoryUiObjectName);
            if (existingUi != null)
            {
                Undo.DestroyObjectImmediate(existingUi);
            }

            GameObject canvasObject = new GameObject(
                InventoryUiObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create inventory equipment UI");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Text crosshair = CreateText(
                "Crosshair",
                canvasObject.transform,
                "+",
                24,
                TextAnchor.MiddleCenter,
                Color.white);
            SetAnchoredRect(
                crosshair.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(40f, 40f));

            Text promptText = CreateText(
                "Interaction Prompt",
                canvasObject.transform,
                string.Empty,
                24,
                TextAnchor.MiddleCenter,
                Color.white);
            SetAnchoredRect(
                promptText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 125f),
                new Vector2(900f, 48f));

            Text statusText = CreateText(
                "Status Text",
                canvasObject.transform,
                string.Empty,
                22,
                TextAnchor.MiddleCenter,
                new Color(0.94f, 0.86f, 0.34f, 1f));
            SetAnchoredRect(
                statusText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 175f),
                new Vector2(900f, 48f));

            GameObject panelObject = CreateImageObject(
                "Inventory Panel",
                canvasObject.transform,
                new Color(0.035f, 0.045f, 0.06f, 0.97f));
            SetAnchoredRect(
                panelObject.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1060f, 560f));

            Text titleText = CreateText(
                "Title",
                panelObject.transform,
                "INVENTORY",
                30,
                TextAnchor.MiddleLeft,
                Color.white);
            SetAnchoredRect(
                titleText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -34f),
                new Vector2(980f, 48f));

            Text instructionText = CreateText(
                "Instructions",
                panelObject.transform,
                "Click a slot, then Equip or Drop One. Press I or Escape to close.",
                17,
                TextAnchor.MiddleRight,
                new Color(0.72f, 0.76f, 0.82f, 1f));
            SetAnchoredRect(
                instructionText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -34f),
                new Vector2(980f, 48f));

            GameObject gridObject = new GameObject(
                "Slot Grid",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            Undo.RegisterCreatedObjectUndo(gridObject, "Create inventory slot grid");
            gridObject.transform.SetParent(panelObject.transform, false);

            SetAnchoredRect(
                gridObject.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(-170f, -25f),
                new Vector2(680f, 390f));

            GridLayoutGroup gridLayout = gridObject.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(155f, 150f);
            gridLayout.spacing = new Vector2(12f, 14f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            List<InventorySlotView> slotViews = new List<InventorySlotView>();
            for (int index = 0; index < 8; index++)
            {
                slotViews.Add(CreateSlotView(index + 1, gridObject.transform));
            }

            GameObject detailsPanel = CreateImageObject(
                "Selected Item Panel",
                panelObject.transform,
                new Color(0.07f, 0.085f, 0.11f, 1f));
            SetAnchoredRect(
                detailsPanel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, -25f),
                new Vector2(285f, 390f));

            Text selectedNameText = CreateText(
                "Selected Item Name",
                detailsPanel.transform,
                "Select an item",
                25,
                TextAnchor.MiddleLeft,
                Color.white);
            SetAnchoredRect(
                selectedNameText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -42f),
                new Vector2(235f, 54f));

            Text selectedDescriptionText = CreateText(
                "Selected Item Description",
                detailsPanel.transform,
                "Click an occupied inventory slot to equip or drop it.",
                17,
                TextAnchor.UpperLeft,
                new Color(0.78f, 0.82f, 0.88f, 1f));
            SetAnchoredRect(
                selectedDescriptionText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -135f),
                new Vector2(235f, 120f));

            Text equippedItemText = CreateText(
                "Equipped Item",
                detailsPanel.transform,
                "Equipped: None",
                18,
                TextAnchor.MiddleLeft,
                new Color(0.52f, 0.82f, 1f, 1f));
            SetAnchoredRect(
                equippedItemText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 122f),
                new Vector2(235f, 42f));

            Button equipButton = CreateButton(
                "Equip Button",
                detailsPanel.transform,
                "Equip",
                new Color(0.15f, 0.42f, 0.62f, 1f),
                out Text equipButtonText);
            SetAnchoredRect(
                equipButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 72f),
                new Vector2(235f, 46f));

            Button dropButton = CreateButton(
                "Drop Button",
                detailsPanel.transform,
                "Drop One",
                new Color(0.58f, 0.25f, 0.18f, 1f),
                out Text dropButtonText);
            SetAnchoredRect(
                dropButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 20f),
                new Vector2(235f, 46f));

            InventoryUI inventoryUI = canvasObject.AddComponent<InventoryUI>();
            SerializedObject serializedUi = new SerializedObject(inventoryUI);
            serializedUi.FindProperty("inventory").objectReferenceValue = inventory;
            serializedUi.FindProperty("firstPersonController").objectReferenceValue = firstPersonController;
            serializedUi.FindProperty("itemDropper").objectReferenceValue = itemDropper;
            serializedUi.FindProperty("inventoryPanel").objectReferenceValue = panelObject;
            serializedUi.FindProperty("interactionPromptText").objectReferenceValue = promptText;
            serializedUi.FindProperty("statusText").objectReferenceValue = statusText;
            serializedUi.FindProperty("selectedItemNameText").objectReferenceValue = selectedNameText;
            serializedUi.FindProperty("selectedItemDescriptionText").objectReferenceValue = selectedDescriptionText;
            serializedUi.FindProperty("equippedItemText").objectReferenceValue = equippedItemText;
            serializedUi.FindProperty("equipButton").objectReferenceValue = equipButton;
            serializedUi.FindProperty("equipButtonText").objectReferenceValue = equipButtonText;
            serializedUi.FindProperty("dropButton").objectReferenceValue = dropButton;

            SerializedProperty slotViewsProperty = serializedUi.FindProperty("slotViews");
            slotViewsProperty.arraySize = slotViews.Count;
            for (int index = 0; index < slotViews.Count; index++)
            {
                slotViewsProperty.GetArrayElementAtIndex(index).objectReferenceValue = slotViews[index];
            }

            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            panelObject.SetActive(false);
            promptText.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
            dropButtonText.raycastTarget = false;

            return inventoryUI;
        }

        private static InventorySlotView CreateSlotView(int slotNumber, Transform parent)
        {
            GameObject slotObject = CreateImageObject(
                $"Slot {slotNumber}",
                parent,
                new Color(0.11f, 0.13f, 0.17f, 1f));

            Image slotBackground = slotObject.GetComponent<Image>();
            slotBackground.raycastTarget = true;

            Button selectButton = slotObject.AddComponent<Button>();
            selectButton.targetGraphic = slotBackground;
            selectButton.transition = Selectable.Transition.ColorTint;

            GameObject colorObject = CreateImageObject(
                "Item Color",
                slotObject.transform,
                Color.clear);
            SetAnchoredRect(
                colorObject.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.62f),
                Vector2.zero,
                new Vector2(64f, 64f));
            colorObject.GetComponent<Image>().raycastTarget = false;

            Text itemNameText = CreateText(
                "Item Name",
                slotObject.transform,
                "Empty",
                16,
                TextAnchor.MiddleCenter,
                Color.white);
            SetAnchoredRect(
                itemNameText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 27f),
                new Vector2(140f, 44f));

            GameObject quantityBadge = CreateImageObject(
                "Quantity Badge",
                slotObject.transform,
                new Color(0.02f, 0.025f, 0.035f, 0.95f));
            SetAnchoredRect(
                quantityBadge.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(-31f, -19f),
                new Vector2(56f, 30f));
            quantityBadge.GetComponent<Image>().raycastTarget = false;

            Text quantityText = CreateText(
                "Quantity Text",
                quantityBadge.transform,
                "x1",
                16,
                TextAnchor.MiddleCenter,
                Color.white);
            StretchToParent(quantityText.rectTransform, 2f);

            Text slotNumberText = CreateText(
                "Slot Number",
                slotObject.transform,
                slotNumber.ToString(),
                14,
                TextAnchor.MiddleLeft,
                new Color(0.58f, 0.62f, 0.68f, 1f));
            SetAnchoredRect(
                slotNumberText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(16f, -16f),
                new Vector2(28f, 24f));

            InventorySlotView slotView = slotObject.AddComponent<InventorySlotView>();
            SerializedObject serializedSlotView = new SerializedObject(slotView);
            serializedSlotView.FindProperty("selectButton").objectReferenceValue = selectButton;
            serializedSlotView.FindProperty("slotBackground").objectReferenceValue = slotBackground;
            serializedSlotView.FindProperty("itemColorImage").objectReferenceValue = colorObject.GetComponent<Image>();
            serializedSlotView.FindProperty("itemNameText").objectReferenceValue = itemNameText;
            serializedSlotView.FindProperty("quantityBadge").objectReferenceValue = quantityBadge;
            serializedSlotView.FindProperty("quantityText").objectReferenceValue = quantityText;
            serializedSlotView.ApplyModifiedPropertiesWithoutUndo();

            return slotView;
        }

        private static void ConfigureDropper(
            InventoryItemDropper itemDropper,
            PlayerInventory inventory,
            Transform dropOrigin)
        {
            SerializedObject serializedDropper = new SerializedObject(itemDropper);
            serializedDropper.FindProperty("inventory").objectReferenceValue = inventory;
            serializedDropper.FindProperty("dropOrigin").objectReferenceValue = dropOrigin;
            serializedDropper.FindProperty("forwardDistance").floatValue = 1.5f;
            serializedDropper.FindProperty("pickupScale").floatValue = 0.55f;
            serializedDropper.FindProperty("groundSearchDistance").floatValue = 3f;
            serializedDropper.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureInteractor(
            InventoryInteractor interactor,
            InventoryUI inventoryUI)
        {
            SerializedObject serializedInteractor = new SerializedObject(interactor);
            serializedInteractor.FindProperty("inventoryUI").objectReferenceValue = inventoryUI;
            serializedInteractor.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateEquippedItemView(
            Camera playerCamera,
            PlayerInventory inventory)
        {
            Transform existingHolder = playerCamera.transform.Find(EquippedHolderName);
            if (existingHolder != null)
            {
                Undo.DestroyObjectImmediate(existingHolder.gameObject);
            }

            GameObject holder = new GameObject(EquippedHolderName);
            Undo.RegisterCreatedObjectUndo(holder, "Create equipped item holder");
            holder.transform.SetParent(playerCamera.transform, false);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(visual, "Create equipped item placeholder");
            visual.name = "Equipped Item Placeholder";
            visual.transform.SetParent(holder.transform, false);
            visual.transform.localPosition = new Vector3(0.43f, -0.34f, 0.82f);
            visual.transform.localRotation = Quaternion.Euler(12f, -18f, 8f);
            visual.transform.localScale = new Vector3(0.18f, 0.18f, 0.48f);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Undo.DestroyObjectImmediate(visualCollider);
            }

            Renderer visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer != null)
            {
                visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                visualRenderer.receiveShadows = false;
            }

            EquippedItemView equippedItemView = holder.AddComponent<EquippedItemView>();
            SerializedObject serializedView = new SerializedObject(equippedItemView);
            serializedView.FindProperty("inventory").objectReferenceValue = inventory;
            serializedView.FindProperty("visualRoot").objectReferenceValue = visual;
            serializedView.FindProperty("itemRenderer").objectReferenceValue = visualRenderer;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            visual.SetActive(false);
        }

        private static void EnsureInputSystemEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            GameObject eventSystemObject;

            if (eventSystem == null)
            {
                eventSystemObject = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            else
            {
                eventSystemObject = eventSystem.gameObject;
            }

            BaseInputModule[] inputModules =
                eventSystemObject.GetComponents<BaseInputModule>();

            for (int index = 0; index < inputModules.Length; index++)
            {
                if (inputModules[index] is InputSystemUIInputModule)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(inputModules[index]);
            }

            if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
            {
                Undo.AddComponent<InputSystemUIInputModule>(eventSystemObject);
            }
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Color backgroundColor,
            out Text labelText)
        {
            GameObject buttonObject = CreateImageObject(objectName, parent, backgroundColor);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            button.colors = colors;

            labelText = CreateText(
                "Label",
                buttonObject.transform,
                label,
                18,
                TextAnchor.MiddleCenter,
                Color.white);
            StretchToParent(labelText.rectTransform, 3f);
            labelText.raycastTarget = false;

            return button;
        }

        private static GameObject CreateImageObject(
            string objectName,
            Transform parent,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Undo.RegisterCreatedObjectUndo(imageObject, "Create inventory UI image");
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return imageObject;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            string textValue,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            Undo.RegisterCreatedObjectUndo(textObject, "Create inventory UI text");
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.text = textValue;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetAnchoredRect(
            RectTransform rectTransform,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static void StretchToParent(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
