using System.Collections.Generic;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hanger51.EditorTools
{
    public static class InventorySystemSetup
    {
        private const string PlayerObjectName = "Player";
        private const string InventoryUiObjectName = "Inventory UI";
        private const string PickupRootObjectName = "Inventory Test Pickups";
        private const string InventoryRootFolder = "Assets/_Project/Inventory";
        private const string ItemFolder = InventoryRootFolder + "/Items";
        private const string MaterialFolder = InventoryRootFolder + "/Materials";

        [MenuItem("Hanger 51/Inventory/1 - Install or Refresh Inventory System")]
        public static void InstallInventorySystem()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Inventory Step 1 failed. Exit Play mode before running setup.");
                return;
            }

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError(
                    $"Inventory Step 1 failed. The active scene needs a GameObject named '{PlayerObjectName}'.");
                return;
            }

            FirstPersonController firstPersonController = player.GetComponent<FirstPersonController>();
            Camera playerCamera = player.GetComponentInChildren<Camera>();

            if (firstPersonController == null || playerCamera == null)
            {
                Debug.LogError(
                    "Inventory Step 1 failed. Player needs FirstPersonController and a child Camera.",
                    player);
                return;
            }

            EnsureProjectFolders();

            InventoryItemDefinition shopRag = CreateOrUpdateItem(
                "ShopRag",
                "shop-rag",
                "Shop Rag",
                "A basic rag used for cleaning aircraft parts.",
                10,
                new Color(0.24f, 0.58f, 0.88f, 1f));

            InventoryItemDefinition oilFilter = CreateOrUpdateItem(
                "OilFilter",
                "oil-filter",
                "Oil Filter",
                "A placeholder aircraft oil filter.",
                5,
                new Color(1f, 0.42f, 0.04f, 1f));

            InventoryItemDefinition sparkPlug = CreateOrUpdateItem(
                "SparkPlug",
                "spark-plug",
                "Spark Plug",
                "A placeholder aviation spark plug.",
                12,
                new Color(0.78f, 0.78f, 0.82f, 1f));

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = Undo.AddComponent<PlayerInventory>(player);
            }

            InventoryInteractor interactor = player.GetComponent<InventoryInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<InventoryInteractor>(player);
            }

            InventoryUI inventoryUI = CreateInventoryUi(inventory, firstPersonController);
            ConfigureInteractor(interactor, playerCamera, inventory, inventoryUI);
            CreateTestPickups(shopRag, oilFilter, sparkPlug);

            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(interactor);
            EditorUtility.SetDirty(inventoryUI);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(activeScene.path))
            {
                Debug.LogError(
                    "Inventory Step 1 created the system, but the active scene has never been saved. "
                    + "Use File > Save As, then run Inventory Step 1 again.");
                return;
            }

            if (!EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Inventory Step 1 failed to save the active scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Inventory Step 1 installed the inventory, but automatic build preparation failed. "
                    + "Run Hanger 51 > Build > 1 - Prepare Current Scene for Build.");
                return;
            }

            Selection.activeGameObject = player;
            Debug.Log(
                "Inventory Step 1 complete. Refreshed the inventory, UI, three visible test pickups, "
                + "saved the scene, and prepared it for Build and Run.",
                player);
        }

        [MenuItem("Hanger 51/Inventory/2 - Validate Inventory Setup")]
        public static void ValidateInventorySetup()
        {
            bool passed = true;

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError("Inventory validation failed: no GameObject named 'Player'.");
                passed = false;
            }
            else
            {
                if (player.GetComponent<PlayerInventory>() == null)
                {
                    Debug.LogError("Inventory validation failed: Player is missing PlayerInventory.", player);
                    passed = false;
                }

                if (player.GetComponent<InventoryInteractor>() == null)
                {
                    Debug.LogError("Inventory validation failed: Player is missing InventoryInteractor.", player);
                    passed = false;
                }

                if (player.GetComponent<FirstPersonController>() == null)
                {
                    Debug.LogError("Inventory validation failed: Player is missing FirstPersonController.", player);
                    passed = false;
                }
            }

            GameObject inventoryUiObject = GameObject.Find(InventoryUiObjectName);
            InventoryUI inventoryUI = inventoryUiObject != null
                ? inventoryUiObject.GetComponent<InventoryUI>()
                : null;

            if (inventoryUI == null)
            {
                Debug.LogError("Inventory validation failed: Inventory UI is missing.");
                passed = false;
            }

            InventorySlotView[] slotViews = inventoryUiObject != null
                ? inventoryUiObject.GetComponentsInChildren<InventorySlotView>(true)
                : new InventorySlotView[0];

            if (slotViews.Length != 8)
            {
                Debug.LogError(
                    $"Inventory validation failed: expected 8 inventory slot views but found {slotViews.Length}.");
                passed = false;
            }

            GameObject pickupRoot = GameObject.Find(PickupRootObjectName);
            InventoryPickup[] pickups = pickupRoot != null
                ? pickupRoot.GetComponentsInChildren<InventoryPickup>(true)
                : new InventoryPickup[0];

            if (pickups.Length != 3)
            {
                Debug.LogError(
                    $"Inventory validation failed: expected 3 test pickups but found {pickups.Length}.");
                passed = false;
            }

            string[] expectedPickupNames =
            {
                "Shop Rag Pickup",
                "Oil Filter Pickup",
                "Spark Plug Pickup"
            };

            for (int index = 0; index < expectedPickupNames.Length; index++)
            {
                string expectedName = expectedPickupNames[index];
                Transform pickupTransform = pickupRoot != null
                    ? pickupRoot.transform.Find(expectedName)
                    : null;

                if (pickupTransform == null)
                {
                    Debug.LogError($"Inventory validation failed: missing '{expectedName}'.");
                    passed = false;
                    continue;
                }

                GameObject pickupObject = pickupTransform.gameObject;
                Renderer renderer = pickupObject.GetComponent<Renderer>();
                Collider pickupCollider = pickupObject.GetComponent<Collider>();
                InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();

                if (!pickupObject.activeInHierarchy)
                {
                    Debug.LogError($"Inventory validation failed: '{expectedName}' is inactive.");
                    passed = false;
                }

                if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null)
                {
                    Debug.LogError(
                        $"Inventory validation failed: '{expectedName}' does not have a visible renderer and material.");
                    passed = false;
                }

                if (pickupCollider == null || !pickupCollider.enabled)
                {
                    Debug.LogError($"Inventory validation failed: '{expectedName}' has no enabled collider.");
                    passed = false;
                }

                if (pickup == null || pickup.Item == null)
                {
                    Debug.LogError($"Inventory validation failed: '{expectedName}' has no item definition.");
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError(
                    "Inventory validation failed: the current scene is not ready for Build and Run. "
                    + "Run Hanger 51 > Build > 1 - Prepare Current Scene for Build.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Inventory Step 2 passed. Player inventory, eight UI slots, all three visible pickups, "
                    + "and the standalone build setup are ready.");
            }
        }

        private static InventoryUI CreateInventoryUi(
            PlayerInventory inventory,
            FirstPersonController firstPersonController)
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

            Undo.RegisterCreatedObjectUndo(canvasObject, "Create inventory UI");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

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
                new Color(0.94f, 0.86f, 0.34f));
            SetAnchoredRect(
                statusText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 175f),
                new Vector2(900f, 48f));

            GameObject panelObject = CreateImageObject(
                "Inventory Panel",
                canvasObject.transform,
                new Color(0.035f, 0.045f, 0.06f, 0.96f));
            SetAnchoredRect(
                panelObject.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(780f, 430f));

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
                new Vector2(700f, 48f));

            Text instructionText = CreateText(
                "Instructions",
                panelObject.transform,
                "Press I or Escape to close",
                18,
                TextAnchor.MiddleRight,
                new Color(0.72f, 0.76f, 0.82f));
            SetAnchoredRect(
                instructionText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -34f),
                new Vector2(700f, 48f));

            GameObject gridObject = new GameObject(
                "Slot Grid",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            Undo.RegisterCreatedObjectUndo(gridObject, "Create inventory slot grid");
            gridObject.transform.SetParent(panelObject.transform, false);

            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 0f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.offsetMin = new Vector2(40f, 40f);
            gridRect.offsetMax = new Vector2(-40f, -82f);

            GridLayoutGroup gridLayout = gridObject.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(160f, 130f);
            gridLayout.spacing = new Vector2(12f, 12f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            List<InventorySlotView> slotViews = new List<InventorySlotView>();
            for (int index = 0; index < 8; index++)
            {
                slotViews.Add(CreateSlotView(index + 1, gridObject.transform));
            }

            InventoryUI inventoryUI = canvasObject.AddComponent<InventoryUI>();
            SerializedObject serializedUi = new SerializedObject(inventoryUI);
            serializedUi.FindProperty("inventory").objectReferenceValue = inventory;
            serializedUi.FindProperty("firstPersonController").objectReferenceValue = firstPersonController;
            serializedUi.FindProperty("inventoryPanel").objectReferenceValue = panelObject;
            serializedUi.FindProperty("interactionPromptText").objectReferenceValue = promptText;
            serializedUi.FindProperty("statusText").objectReferenceValue = statusText;

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

            return inventoryUI;
        }

        private static InventorySlotView CreateSlotView(int slotNumber, Transform parent)
        {
            GameObject slotObject = CreateImageObject(
                $"Slot {slotNumber}",
                parent,
                new Color(0.11f, 0.13f, 0.17f, 1f));

            Image slotBackground = slotObject.GetComponent<Image>();
            slotBackground.raycastTarget = false;

            GameObject colorObject = CreateImageObject(
                "Item Color",
                slotObject.transform,
                Color.clear);
            RectTransform colorRect = colorObject.GetComponent<RectTransform>();
            SetAnchoredRect(
                colorRect,
                new Vector2(0.5f, 0.65f),
                Vector2.zero,
                new Vector2(62f, 62f));
            colorObject.GetComponent<Image>().raycastTarget = false;

            Text itemNameText = CreateText(
                "Item Name",
                slotObject.transform,
                "Empty",
                18,
                TextAnchor.MiddleCenter,
                Color.white);
            SetAnchoredRect(
                itemNameText.rectTransform,
                new Vector2(0.5f, 0.18f),
                Vector2.zero,
                new Vector2(145f, 48f));

            Text quantityText = CreateText(
                "Quantity",
                slotObject.transform,
                string.Empty,
                18,
                TextAnchor.MiddleRight,
                Color.white);
            SetAnchoredRect(
                quantityText.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(-12f, 12f),
                new Vector2(60f, 30f));

            Text slotNumberText = CreateText(
                "Slot Number",
                slotObject.transform,
                slotNumber.ToString(),
                14,
                TextAnchor.MiddleLeft,
                new Color(0.58f, 0.62f, 0.68f));
            SetAnchoredRect(
                slotNumberText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(10f, -10f),
                new Vector2(30f, 24f));

            InventorySlotView slotView = slotObject.AddComponent<InventorySlotView>();
            SerializedObject serializedSlotView = new SerializedObject(slotView);
            serializedSlotView.FindProperty("itemColorImage").objectReferenceValue = colorObject.GetComponent<Image>();
            serializedSlotView.FindProperty("itemNameText").objectReferenceValue = itemNameText;
            serializedSlotView.FindProperty("quantityText").objectReferenceValue = quantityText;
            serializedSlotView.ApplyModifiedPropertiesWithoutUndo();

            return slotView;
        }

        private static void ConfigureInteractor(
            InventoryInteractor interactor,
            Camera playerCamera,
            PlayerInventory inventory,
            InventoryUI inventoryUI)
        {
            SerializedObject serializedInteractor = new SerializedObject(interactor);
            serializedInteractor.FindProperty("playerCamera").objectReferenceValue = playerCamera;
            serializedInteractor.FindProperty("inventory").objectReferenceValue = inventory;
            serializedInteractor.FindProperty("inventoryUI").objectReferenceValue = inventoryUI;
            serializedInteractor.FindProperty("interactionDistance").floatValue = 3f;
            serializedInteractor.FindProperty("interactionLayers").intValue = ~0;
            serializedInteractor.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTestPickups(
            InventoryItemDefinition shopRag,
            InventoryItemDefinition oilFilter,
            InventoryItemDefinition sparkPlug)
        {
            GameObject existingRoot = GameObject.Find(PickupRootObjectName);
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }

            GameObject root = new GameObject(PickupRootObjectName);
            Undo.RegisterCreatedObjectUndo(root, "Create inventory test pickups");

            CreatePickup(
                "Shop Rag Pickup",
                shopRag,
                3,
                new Vector3(-2.5f, 0.45f, -5.8f),
                root.transform);

            CreatePickup(
                "Oil Filter Pickup",
                oilFilter,
                1,
                new Vector3(0f, 0.45f, -4.8f),
                root.transform);

            CreatePickup(
                "Spark Plug Pickup",
                sparkPlug,
                4,
                new Vector3(2.5f, 0.45f, -5.8f),
                root.transform);
        }

        private static void CreatePickup(
            string objectName,
            InventoryItemDefinition item,
            int quantity,
            Vector3 position,
            Transform parent)
        {
            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(pickupObject, "Create inventory pickup");
            pickupObject.name = objectName;
            pickupObject.transform.SetParent(parent);
            pickupObject.transform.position = position;
            pickupObject.transform.localScale = Vector3.one * 0.8f;
            pickupObject.SetActive(true);

            Renderer renderer = pickupObject.GetComponent<Renderer>();
            Material material = CreateOrUpdateMaterial(item);
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.sharedMaterial = material;
            }

            Collider pickupCollider = pickupObject.GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = true;
                pickupCollider.isTrigger = false;
            }

            InventoryPickup pickup = pickupObject.AddComponent<InventoryPickup>();
            SerializedObject serializedPickup = new SerializedObject(pickup);
            serializedPickup.FindProperty("item").objectReferenceValue = item;
            serializedPickup.FindProperty("quantity").intValue = quantity;
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InventoryItemDefinition CreateOrUpdateItem(
            string assetName,
            string itemId,
            string displayName,
            string description,
            int maxStackSize,
            Color placeholderColor)
        {
            string assetPath = $"{ItemFolder}/{assetName}.asset";
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath);

            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = assetName;
                AssetDatabase.CreateAsset(item, assetPath);
            }

            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("itemId").stringValue = itemId;
            serializedItem.FindProperty("displayName").stringValue = displayName;
            serializedItem.FindProperty("description").stringValue = description;
            serializedItem.FindProperty("maxStackSize").intValue = maxStackSize;
            serializedItem.FindProperty("placeholderColor").colorValue = placeholderColor;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(item);
            return item;
        }

        private static Material CreateOrUpdateMaterial(InventoryItemDefinition item)
        {
            if (item == null)
            {
                return null;
            }

            string assetPath = $"{MaterialFolder}/{item.name}Material.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    Debug.LogWarning($"Could not find a shader for pickup material '{item.name}'.");
                    return null;
                }

                material = new Material(shader)
                {
                    name = item.name + " Material"
                };

                AssetDatabase.CreateAsset(material, assetPath);
            }

            Color visibleColor = item.PlaceholderColor;
            visibleColor.a = 1f;
            material.color = visibleColor;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", visibleColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", visibleColor);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            material.renderQueue = -1;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateImageObject(string objectName, Transform parent, Color color)
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

        private static void EnsureProjectFolders()
        {
            EnsureFolderExists("Assets", "_Project");
            EnsureFolderExists("Assets/_Project", "Inventory");
            EnsureFolderExists(InventoryRootFolder, "Items");
            EnsureFolderExists(InventoryRootFolder, "Materials");
        }

        private static void EnsureFolderExists(string parentFolder, string folderName)
        {
            string fullPath = parentFolder + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }
}
