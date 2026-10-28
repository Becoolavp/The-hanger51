using System.Collections.Generic;
using System.IO;
using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.EngineAssembly;
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
    public static class HangarCommerceSetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string HangarRootName = "Hanger 51 Test Hangar";
        private const string DeskRootName = "Parts Computer Desk";
        private const string ShipmentAreaName = "Hanger 51 Shipment Area";
        private const string ShopUiName = "Hangar Shop UI";
        private const string CrateTemplateName = "Shipment Crate Template";
        private const string AssemblyTemplateName = "Complete V-1650 Shipment Template";

        private const string CommerceFolder = "Assets/_Project/Commerce";
        private const string MaterialFolder = CommerceFolder + "/Materials";
        private const string ItemFolder = "Assets/_Project/Inventory/Items";

        private const string SparkPlugItemPath = ItemFolder + "/SparkPlug.asset";
        private const string CylinderCoverItemPath = ItemFolder + "/MerlinCylinderCover.asset";
        private const string EngineBlockItemPath = ItemFolder + "/MerlinEngineBlock.asset";
        private const string OilFilterItemPath = ItemFolder + "/OilFilter.asset";
        private const string ShopRagItemPath = ItemFolder + "/ShopRag.asset";

        private sealed class CommerceMaterials
        {
            public Material DeskWood;
            public Material DarkMetal;
            public Material ComputerPlastic;
            public Material Screen;
            public Material KeyPlastic;
            public Material CrateWood;
            public Material CrateEdge;
            public Material SteelBand;
            public Material SafetyYellow;
            public Material ShippingWhite;
        }

        [MenuItem("Hanger 51/Shop and Shipping/1 - Build Parts Computer and Shipment Area")]
        public static void BuildPartsComputerAndShipmentArea()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 1 failed. Exit Play mode before building the shop and shipment area.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject hangar = GameObject.Find(HangarRootName);
            EngineAssemblyStation sourceStation =
                Object.FindFirstObjectByType<EngineAssemblyStation>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            FirstPersonController firstPersonController =
                Object.FindFirstObjectByType<FirstPersonController>();
            PlayerInventory playerInventory = Object.FindFirstObjectByType<PlayerInventory>();

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || hangar == null
                || sourceStation == null
                || inventoryUI == null
                || firstPersonController == null
                || playerInventory == null)
            {
                Debug.LogError(
                    "Shop Step 1 failed. Open the saved movement-test scene and confirm the expanded hangar, Player inventory, and Merlin station exist.");
                return;
            }

            InventoryItemDefinition sparkPlug =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(SparkPlugItemPath);
            InventoryItemDefinition cylinderCover =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(CylinderCoverItemPath);
            InventoryItemDefinition engineBlock =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(EngineBlockItemPath);
            InventoryItemDefinition oilFilter =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(OilFilterItemPath);
            InventoryItemDefinition shopRag =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(ShopRagItemPath);

            if (sparkPlug == null || cylinderCover == null || engineBlock == null)
            {
                Debug.LogError(
                    "Shop Step 1 failed. The Merlin spark-plug, cylinder-cover, or engine-block item asset is missing. Run the current Merlin setup first.");
                return;
            }

            EnsureFolder(CommerceFolder);
            EnsureFolder(MaterialFolder);
            CommerceMaterials materials = CreateOrRefreshMaterials();

            RemoveExistingCommerceSystem();

            GameObject commerceRoot = new GameObject(CommerceRootName);
            Undo.RegisterCreatedObjectUndo(commerceRoot, "Create Hanger 51 commerce system");
            commerceRoot.transform.SetParent(hangar.transform, false);

            GameObject templateRoot = new GameObject("Commerce Templates");
            templateRoot.transform.SetParent(commerceRoot.transform, false);

            GameObject assemblyTemplate = BuildCompleteAssemblyTemplate(
                sourceStation,
                templateRoot.transform);
            GameObject crateTemplate = BuildShipmentCrateTemplate(
                templateRoot.transform,
                materials);

            ShipmentAreaController shipmentArea = BuildShipmentArea(
                commerceRoot.transform,
                crateTemplate,
                materials);

            HangarShopUI shopUI = BuildShopUI(
                firstPersonController,
                inventoryUI);

            PlayerWallet wallet = playerInventory.GetComponent<PlayerWallet>();
            if (wallet == null)
            {
                wallet = Undo.AddComponent<PlayerWallet>(playerInventory.gameObject);
            }
            wallet.ConfigureStartingBalance(250000);

            List<ShopCatalogEntry> catalog = BuildCatalog(
                sparkPlug,
                cylinderCover,
                engineBlock,
                oilFilter,
                shopRag,
                assemblyTemplate);

            HangarShopTerminal terminal = BuildDeskAndComputer(
                commerceRoot.transform,
                materials);
            terminal.Configure(
                wallet,
                shipmentArea,
                shopUI,
                FindDescendant(terminal.transform, "Terminal Screen")?.GetComponent<Renderer>(),
                catalog);

            HangarCommercePlayerInteractor commerceInteractor =
                playerInventory.GetComponent<HangarCommercePlayerInteractor>();
            if (commerceInteractor == null)
            {
                commerceInteractor = Undo.AddComponent<HangarCommercePlayerInteractor>(
                    playerInventory.gameObject);
            }
            commerceInteractor.Configure(
                playerInventory.GetComponentInChildren<Camera>(),
                inventoryUI);

            List<Behaviour> blockedBehaviours = GatherGameplayBehaviours(
                playerInventory.gameObject,
                inventoryUI,
                commerceInteractor);
            ConfigureShopUiBlockedBehaviours(shopUI, blockedBehaviours);

            EnsureEventSystem();
            SceneViewClippingFix.FixSceneViewCameraClipping();

            EditorUtility.SetDirty(wallet);
            EditorUtility.SetDirty(shipmentArea);
            EditorUtility.SetDirty(terminal);
            EditorUtility.SetDirty(shopUI);
            EditorUtility.SetDirty(commerceInteractor);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 1 created the commerce system but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 1 created the commerce system, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = terminal.transform.root.gameObject;
            Debug.Log(
                "Shop Step 1 complete. Added a detailed desk and parts computer, six-product catalog, $250,000 test wallet, "
                + "four marked shipment bays, animated wooden crates, inventory-part deliveries, and a complete serviceable V-1650 delivery template. "
                + "Also repaired the open Scene view clipping settings for the kilometer-scale ground Plane.");
        }

        [MenuItem("Hanger 51/Shop and Shipping/2 - Validate Parts Computer and Shipment Area")]
        public static void ValidatePartsComputerAndShipmentArea()
        {
            bool passed = true;
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>();
            ShipmentAreaController shipmentArea =
                Object.FindFirstObjectByType<ShipmentAreaController>();
            HangarShopUI shopUI = Object.FindFirstObjectByType<HangarShopUI>();
            PlayerWallet wallet = Object.FindFirstObjectByType<PlayerWallet>();
            HangarCommercePlayerInteractor commerceInteractor =
                Object.FindFirstObjectByType<HangarCommercePlayerInteractor>();

            if (commerceRoot == null)
            {
                Debug.LogError("Shop Step 2 failed: the commerce system root is missing.");
                passed = false;
            }
            if (terminal == null || terminal.Catalog.Count < 6)
            {
                int count = terminal != null ? terminal.Catalog.Count : 0;
                Debug.LogError($"Shop Step 2 failed: expected at least 6 catalog products, found {count}.");
                passed = false;
            }
            else
            {
                bool foundCompleteAssembly = false;
                for (int index = 0; index < terminal.Catalog.Count; index++)
                {
                    ShopCatalogEntry product = terminal.Catalog[index];
                    if (product == null || !product.IsConfigured)
                    {
                        Debug.LogError($"Shop Step 2 failed: catalog product {index + 1} is incomplete.");
                        passed = false;
                        continue;
                    }

                    if (product.ProductKind == ShopProductKind.CompleteAssembly)
                    {
                        foundCompleteAssembly = true;
                    }
                }

                if (!foundCompleteAssembly)
                {
                    Debug.LogError("Shop Step 2 failed: no complete serviceable assembly product exists.");
                    passed = false;
                }
            }

            if (shipmentArea == null || shipmentArea.SlotCount != 4)
            {
                int count = shipmentArea != null ? shipmentArea.SlotCount : 0;
                Debug.LogError($"Shop Step 2 failed: expected 4 shipment bays, found {count}.");
                passed = false;
            }

            Transform templateRoot = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, "Commerce Templates")
                : null;
            Transform crateTemplate = templateRoot != null
                ? templateRoot.Find(CrateTemplateName)
                : null;
            Transform assemblyTemplate = templateRoot != null
                ? templateRoot.Find(AssemblyTemplateName)
                : null;
            EngineAssemblyStation templateStation = assemblyTemplate != null
                ? assemblyTemplate.GetComponentInChildren<EngineAssemblyStation>(true)
                : null;

            if (crateTemplate == null
                || crateTemplate.GetComponent<ShipmentCrateController>() == null)
            {
                Debug.LogError("Shop Step 2 failed: the shipment crate template is missing or incomplete.");
                passed = false;
            }
            if (assemblyTemplate == null
                || assemblyTemplate.gameObject.activeSelf
                || templateStation == null
                || !templateStation.IsComplete)
            {
                Debug.LogError(
                    "Shop Step 2 failed: the inactive complete V-1650 shipment template is missing or not fully assembled.");
                passed = false;
            }

            if (shopUI == null
                || GameObject.Find(ShopUiName) == null
                || wallet == null
                || commerceInteractor == null)
            {
                Debug.LogError(
                    "Shop Step 2 failed: shop UI, Player wallet, or Player commerce interactor is missing.");
                passed = false;
            }

            GameObject desk = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, DeskRootName)?.gameObject
                : null;
            if (desk == null || desk.GetComponentsInChildren<Renderer>(true).Length < 30)
            {
                Debug.LogError("Shop Step 2 failed: the detailed computer desk assembly is incomplete.");
                passed = false;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                Debug.LogError("Shop Step 2 failed: the UI EventSystem is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 2 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Shop Step 2 passed. Desk computer, six-product catalog, wallet, UI, four shipment bays, animated crate, "
                    + "inventory deliveries, complete serviceable V-1650 shipment, Player interaction, and standalone build setup are ready.");
            }
        }

        private static HangarShopTerminal BuildDeskAndComputer(
            Transform parent,
            CommerceMaterials materials)
        {
            GameObject desk = new GameObject(DeskRootName);
            desk.transform.SetParent(parent, false);
            desk.transform.localPosition = new Vector3(-10.6f, 0f, 8.6f);
            desk.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            CreatePart(desk.transform, PrimitiveType.Cube, "Desk Top",
                new Vector3(0f, 0.92f, 0f), new Vector3(4.4f, 0.16f, 1.7f), Vector3.zero, materials.DeskWood, true);
            CreatePart(desk.transform, PrimitiveType.Cube, "Rear Modesty Panel",
                new Vector3(0f, 0.48f, 0.68f), new Vector3(4.1f, 0.72f, 0.10f), Vector3.zero, materials.DarkMetal, true);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(desk.transform, PrimitiveType.Cube, $"Desk Leg {side}",
                    new Vector3(side * 1.85f, 0.45f, 0f), new Vector3(0.18f, 0.9f, 1.45f), Vector3.zero, materials.DarkMetal, true);
                CreatePart(desk.transform, PrimitiveType.Cube, $"Desk Foot {side}",
                    new Vector3(side * 1.85f, 0.06f, 0f), new Vector3(0.65f, 0.12f, 1.75f), Vector3.zero, materials.DarkMetal, true);
            }

            GameObject drawer = CreatePart(desk.transform, PrimitiveType.Cube, "Three-Drawer Pedestal",
                new Vector3(1.35f, 0.50f, 0.2f), new Vector3(1.05f, 0.76f, 1.25f), Vector3.zero, materials.ComputerPlastic, true);
            for (int index = 0; index < 3; index++)
            {
                CreatePart(drawer.transform, PrimitiveType.Cube, $"Drawer Face {index + 1}",
                    new Vector3(0f, 0.25f - index * 0.32f, -0.51f), new Vector3(0.91f, 0.25f, 0.04f), Vector3.zero, materials.DarkMetal, false);
                CreatePart(drawer.transform, PrimitiveType.Cylinder, $"Drawer Pull {index + 1}",
                    new Vector3(0f, 0.25f - index * 0.32f, -0.56f), new Vector3(0.05f, 0.20f, 0.05f), new Vector3(90f, 0f, 0f), materials.SteelBand, false);
            }

            GameObject computer = new GameObject("Hanger 51 Parts Computer");
            computer.transform.SetParent(desk.transform, false);

            CreatePart(computer.transform, PrimitiveType.Cube, "Monitor Stand Base",
                new Vector3(-0.35f, 1.06f, 0.1f), new Vector3(0.85f, 0.08f, 0.55f), Vector3.zero, materials.DarkMetal, false);
            CreatePart(computer.transform, PrimitiveType.Cube, "Monitor Stand Neck",
                new Vector3(-0.35f, 1.40f, 0.34f), new Vector3(0.14f, 0.70f, 0.14f), new Vector3(-8f, 0f, 0f), materials.DarkMetal, false);
            GameObject monitor = CreatePart(computer.transform, PrimitiveType.Cube, "Terminal Monitor",
                new Vector3(-0.35f, 1.84f, 0.21f), new Vector3(2.05f, 1.18f, 0.16f), new Vector3(-6f, 0f, 0f), materials.ComputerPlastic, true);
            GameObject screen = CreatePart(monitor.transform, PrimitiveType.Cube, "Terminal Screen",
                new Vector3(0f, 0f, -0.54f), new Vector3(0.90f, 0.78f, 0.025f), Vector3.zero, materials.Screen, false);
            CreatePart(monitor.transform, PrimitiveType.Cube, "Lower Monitor Bezel",
                new Vector3(0f, -0.43f, -0.55f), new Vector3(0.92f, 0.07f, 0.03f), Vector3.zero, materials.DarkMetal, false);
            CreatePart(monitor.transform, PrimitiveType.Sphere, "Monitor Power Light",
                new Vector3(0.78f, -0.43f, -0.59f), Vector3.one * 0.035f, Vector3.zero, materials.SafetyYellow, false);

            GameObject keyboard = CreatePart(computer.transform, PrimitiveType.Cube, "Keyboard Chassis",
                new Vector3(-0.35f, 1.05f, -0.48f), new Vector3(1.70f, 0.08f, 0.62f), new Vector3(5f, 0f, 0f), materials.ComputerPlastic, false);
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 12; column++)
                {
                    CreatePart(keyboard.transform, PrimitiveType.Cube,
                        $"Key {row + 1}-{column + 1}",
                        new Vector3(-0.43f + column * 0.078f, 0.57f, -0.31f + row * 0.18f),
                        new Vector3(0.060f, 0.08f, 0.13f), Vector3.zero, materials.KeyPlastic, false);
                }
            }

            CreatePart(computer.transform, PrimitiveType.Sphere, "Computer Mouse",
                new Vector3(0.90f, 1.08f, -0.42f), new Vector3(0.26f, 0.09f, 0.36f), Vector3.zero, materials.ComputerPlastic, false);
            GameObject tower = CreatePart(computer.transform, PrimitiveType.Cube, "Computer Tower",
                new Vector3(-1.55f, 1.48f, 0.15f), new Vector3(0.62f, 1.10f, 1.05f), Vector3.zero, materials.ComputerPlastic, true);
            for (int index = 0; index < 6; index++)
            {
                CreatePart(tower.transform, PrimitiveType.Cube, $"Tower Vent {index + 1}",
                    new Vector3(0f, 0.18f - index * 0.075f, -0.505f), new Vector3(0.65f, 0.025f, 0.025f), Vector3.zero, materials.DarkMetal, false);
            }
            CreatePart(tower.transform, PrimitiveType.Sphere, "Tower Power Light",
                new Vector3(0.20f, 0.36f, -0.55f), Vector3.one * 0.045f, Vector3.zero, materials.Screen, false);

            GameObject chair = new GameObject("Shop Desk Chair");
            chair.transform.SetParent(desk.transform, false);
            chair.transform.localPosition = new Vector3(0f, 0f, -2.0f);
            CreatePart(chair.transform, PrimitiveType.Cube, "Chair Seat",
                new Vector3(0f, 0.60f, 0f), new Vector3(1.15f, 0.16f, 1.05f), Vector3.zero, materials.ComputerPlastic, true);
            CreatePart(chair.transform, PrimitiveType.Cube, "Chair Back",
                new Vector3(0f, 1.20f, 0.42f), new Vector3(1.20f, 1.15f, 0.16f), new Vector3(-8f, 0f, 0f), materials.ComputerPlastic, true);
            CreatePart(chair.transform, PrimitiveType.Cylinder, "Chair Post",
                new Vector3(0f, 0.30f, 0f), new Vector3(0.10f, 0.30f, 0.10f), Vector3.zero, materials.DarkMetal, false);
            for (int index = 0; index < 5; index++)
            {
                float angle = index * 72f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                CreatePart(chair.transform, PrimitiveType.Cube, $"Chair Base Arm {index + 1}",
                    direction * 0.42f + Vector3.up * 0.10f,
                    new Vector3(0.12f, 0.08f, 0.75f), new Vector3(0f, angle, 0f), materials.DarkMetal, false);
            }

            GameObject interaction = new GameObject("Parts Computer Interaction");
            interaction.transform.SetParent(monitor.transform, false);
            interaction.transform.localPosition = new Vector3(0f, 0f, -0.75f);
            BoxCollider interactionCollider = interaction.AddComponent<BoxCollider>();
            interactionCollider.size = new Vector3(2.4f, 1.5f, 0.55f);
            HangarShopTerminal terminal = interaction.AddComponent<HangarShopTerminal>();

            return terminal;
        }

        private static ShipmentAreaController BuildShipmentArea(
            Transform parent,
            GameObject crateTemplate,
            CommerceMaterials materials)
        {
            GameObject area = new GameObject(ShipmentAreaName);
            area.transform.SetParent(parent, false);

            CreatePart(area.transform, PrimitiveType.Cube, "Shipment Area Header",
                new Vector3(0f, 3.1f, 14.9f), new Vector3(18f, 0.65f, 0.20f), Vector3.zero, materials.DarkMetal, false);
            TextMesh headerText = CreateWorldText(
                area.transform,
                "Shipment Receiving Sign",
                "SHIPMENT RECEIVING — UNBOX BEFORE NEXT DELIVERY",
                new Vector3(0f, 3.1f, 14.75f),
                Quaternion.Euler(0f, 180f, 0f),
                0.12f,
                Color.white);
            headerText.anchor = TextAnchor.MiddleCenter;

            float[] slotX = { -7.5f, -2.5f, 2.5f, 7.5f };
            List<ShipmentAreaController.ShipmentSlot> slots =
                new List<ShipmentAreaController.ShipmentSlot>();

            for (int index = 0; index < slotX.Length; index++)
            {
                GameObject slotRoot = new GameObject($"Shipment Bay {index + 1}");
                slotRoot.transform.SetParent(area.transform, false);
                slotRoot.transform.localPosition = new Vector3(slotX[index], 0f, 12.4f);

                CreatePart(slotRoot.transform, PrimitiveType.Cube, "Bay Floor Marking",
                    new Vector3(0f, 0.018f, 0f), new Vector3(4.1f, 0.036f, 3.9f), Vector3.zero, materials.SafetyYellow, false);
                CreatePart(slotRoot.transform, PrimitiveType.Cube, "Bay Inner Floor",
                    new Vector3(0f, 0.024f, 0f), new Vector3(3.65f, 0.045f, 3.45f), Vector3.zero, materials.DarkMetal, false);
                CreateWorldText(
                    slotRoot.transform,
                    "Bay Number",
                    $"BAY {index + 1}",
                    new Vector3(0f, 0.06f, -1.45f),
                    Quaternion.Euler(90f, 0f, 0f),
                    0.16f,
                    Color.white);

                Transform crateAnchor = new GameObject("Crate Anchor").transform;
                crateAnchor.SetParent(slotRoot.transform, false);
                crateAnchor.localPosition = new Vector3(0f, 0.04f, 0f);

                Transform contentAnchor = new GameObject("Unboxed Content Anchor").transform;
                contentAnchor.SetParent(area.transform, false);
                contentAnchor.localPosition = new Vector3(slotX[index], 0.04f, 8.0f);
                contentAnchor.localRotation = Quaternion.identity;

                ShipmentAreaController.ShipmentSlot slot =
                    new ShipmentAreaController.ShipmentSlot();
                slot.Configure(crateAnchor, contentAnchor);
                slots.Add(slot);
            }

            ShipmentAreaController controller = area.AddComponent<ShipmentAreaController>();
            controller.Configure(crateTemplate, slots);
            return controller;
        }

        private static GameObject BuildShipmentCrateTemplate(
            Transform parent,
            CommerceMaterials materials)
        {
            GameObject crate = new GameObject(CrateTemplateName);
            crate.transform.SetParent(parent, false);

            BoxCollider collider = crate.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.30f, 0f);
            collider.size = new Vector3(3.8f, 2.65f, 3.3f);

            GameObject pallet = new GameObject("Heavy Shipping Pallet");
            pallet.transform.SetParent(crate.transform, false);
            for (int index = -2; index <= 2; index++)
            {
                CreatePart(pallet.transform, PrimitiveType.Cube, $"Pallet Top Slat {index + 3}",
                    new Vector3(index * 0.72f, 0.12f, 0f), new Vector3(0.58f, 0.18f, 3.55f), Vector3.zero, materials.CrateWood, false);
            }
            for (int index = -1; index <= 1; index++)
            {
                CreatePart(pallet.transform, PrimitiveType.Cube, $"Pallet Runner {index + 2}",
                    new Vector3(index * 1.35f, 0.02f, 0f), new Vector3(0.30f, 0.20f, 3.80f), Vector3.zero, materials.CrateEdge, false);
            }

            CreatePart(crate.transform, PrimitiveType.Cube, "Crate Floor",
                new Vector3(0f, 0.34f, 0f), new Vector3(3.55f, 0.16f, 3.05f), Vector3.zero, materials.CrateWood, false);

            for (int cornerX = -1; cornerX <= 1; cornerX += 2)
            {
                for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
                {
                    CreatePart(crate.transform, PrimitiveType.Cube,
                        $"Corner Post {cornerX} {cornerZ}",
                        new Vector3(cornerX * 1.70f, 1.42f, cornerZ * 1.45f),
                        new Vector3(0.18f, 2.35f, 0.18f), Vector3.zero, materials.CrateEdge, false);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 6; row++)
                {
                    CreatePart(crate.transform, PrimitiveType.Cube,
                        $"Side {side} Slat {row + 1}",
                        new Vector3(side * 1.65f, 0.57f + row * 0.36f, 0f),
                        new Vector3(0.12f, 0.25f, 2.75f), Vector3.zero, materials.CrateWood, false);
                    CreatePart(crate.transform, PrimitiveType.Cube,
                        $"Front Rear {side} Slat {row + 1}",
                        new Vector3(0f, 0.57f + row * 0.36f, side * 1.40f),
                        new Vector3(3.25f, 0.25f, 0.12f), Vector3.zero, materials.CrateWood, false);
                }
            }

            Transform lidPivot = new GameObject("Crate Lid Hinge").transform;
            lidPivot.SetParent(crate.transform, false);
            lidPivot.localPosition = new Vector3(0f, 2.62f, 1.45f);
            CreatePart(lidPivot, PrimitiveType.Cube, "Crate Lid",
                new Vector3(0f, 0f, -1.45f), new Vector3(3.65f, 0.20f, 3.20f), Vector3.zero, materials.CrateWood, false);
            for (int index = -1; index <= 1; index++)
            {
                CreatePart(lidPivot, PrimitiveType.Cube, $"Lid Brace {index + 2}",
                    new Vector3(index * 1.25f, 0.14f, -1.45f), new Vector3(0.16f, 0.16f, 3.0f), Vector3.zero, materials.CrateEdge, false);
            }

            Transform leftBand = CreatePart(crate.transform, PrimitiveType.Cube, "Left Steel Shipping Band",
                new Vector3(-1.05f, 1.42f, 0f), new Vector3(0.10f, 2.65f, 3.35f), Vector3.zero, materials.SteelBand, false).transform;
            Transform rightBand = CreatePart(crate.transform, PrimitiveType.Cube, "Right Steel Shipping Band",
                new Vector3(1.05f, 1.42f, 0f), new Vector3(0.10f, 2.65f, 3.35f), Vector3.zero, materials.SteelBand, false).transform;

            CreatePart(crate.transform, PrimitiveType.Cube, "Shipping Label Plate",
                new Vector3(0f, 1.45f, -1.53f), new Vector3(2.35f, 1.05f, 0.05f), Vector3.zero, materials.ShippingWhite, false);
            TextMesh label = CreateWorldText(
                crate.transform,
                "Shipping Label Text",
                "HANGER 51 SUPPLY",
                new Vector3(0f, 1.45f, -1.57f),
                Quaternion.Euler(0f, 180f, 0f),
                0.055f,
                Color.black);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;

            CreatePart(crate.transform, PrimitiveType.Cube, "Fragile Placard",
                new Vector3(1.35f, 0.70f, -1.54f), new Vector3(0.48f, 0.34f, 0.05f), Vector3.zero, materials.SafetyYellow, false);

            ShipmentCrateController controller = crate.AddComponent<ShipmentCrateController>();
            SerializedObject serializedCrate = new SerializedObject(controller);
            serializedCrate.FindProperty("lidPivot").objectReferenceValue = lidPivot;
            serializedCrate.FindProperty("leftBand").objectReferenceValue = leftBand;
            serializedCrate.FindProperty("rightBand").objectReferenceValue = rightBand;
            serializedCrate.FindProperty("shippingLabel").objectReferenceValue = label;
            serializedCrate.FindProperty("interactionCollider").objectReferenceValue = collider;
            serializedCrate.FindProperty("openingDuration").floatValue = 1.25f;
            serializedCrate.ApplyModifiedPropertiesWithoutUndo();

            crate.SetActive(false);
            return crate;
        }

        private static GameObject BuildCompleteAssemblyTemplate(
            EngineAssemblyStation sourceStation,
            Transform parent)
        {
            GameObject template = Object.Instantiate(sourceStation.gameObject);
            template.name = AssemblyTemplateName;
            template.transform.SetParent(parent, false);
            template.transform.localPosition = Vector3.zero;
            template.transform.localRotation = Quaternion.identity;
            template.transform.localScale = Vector3.one;

            EngineAssemblyStation templateStation =
                template.GetComponent<EngineAssemblyStation>();
            templateStation?.SetAssemblyComplete();
            EngineAssemblyTransportController transport =
                template.GetComponent<EngineAssemblyTransportController>();
            transport?.SnapToStand();
            template.SetActive(false);
            return template;
        }

        private static List<ShopCatalogEntry> BuildCatalog(
            InventoryItemDefinition sparkPlug,
            InventoryItemDefinition cylinderCover,
            InventoryItemDefinition engineBlock,
            InventoryItemDefinition oilFilter,
            InventoryItemDefinition shopRag,
            GameObject assemblyTemplate)
        {
            List<ShopCatalogEntry> catalog = new List<ShopCatalogEntry>();
            catalog.Add(CreateCatalogEntry(
                "spark-plug-set-24",
                "Engine Parts",
                "V-1650 Spark Plug Set (24)",
                "A complete dual-ignition spark-plug set for all twelve cylinders. Delivered as one pickup stack inside a labeled crate.",
                3600,
                ShopProductKind.InventoryItem,
                sparkPlug,
                24,
                null));
            catalog.Add(CreateCatalogEntry(
                "merlin-cylinder-cover",
                "Engine Parts",
                "V-1650 Cylinder Cover",
                "One removable six-cylinder bank cover. It installs through the existing highlighted cover-mount workflow.",
                4200,
                ShopProductKind.InventoryItem,
                cylinderCover,
                1,
                null));
            catalog.Add(CreateCatalogEntry(
                "merlin-engine-block",
                "Major Components",
                "Merlin V-1650 Engine Block",
                "A bare Packard Merlin-style V-12 engine block ready to place on an empty compatible stand and assemble using normal inventory parts.",
                38000,
                ShopProductKind.InventoryItem,
                engineBlock,
                1,
                null));

            if (oilFilter != null)
            {
                catalog.Add(CreateCatalogEntry(
                    "oil-filter",
                    "Consumables",
                    "Aircraft Oil Filter",
                    "A replacement aircraft oil filter delivered as a normal inventory pickup.",
                    95,
                    ShopProductKind.InventoryItem,
                    oilFilter,
                    1,
                    null));
            }

            if (shopRag != null)
            {
                catalog.Add(CreateCatalogEntry(
                    "shop-rag-bundle",
                    "Consumables",
                    "Shop Rag Bundle (5)",
                    "Five general-purpose maintenance rags packed together in one shipment.",
                    30,
                    ShopProductKind.InventoryItem,
                    shopRag,
                    5,
                    null));
            }

            catalog.Add(CreateCatalogEntry(
                "complete-v1650-assembly",
                "Complete Assemblies",
                "Complete Serviceable V-1650 Assembly",
                "A fully assembled V-1650 on its maintenance stand with two covers, twelve secured cover bolts, and twenty-four spark plugs. Every part can be removed and reinstalled through the existing maintenance interactions.",
                95000,
                ShopProductKind.CompleteAssembly,
                null,
                1,
                assemblyTemplate));
            return catalog;
        }

        private static ShopCatalogEntry CreateCatalogEntry(
            string productId,
            string category,
            string displayName,
            string description,
            int price,
            ShopProductKind kind,
            InventoryItemDefinition inventoryItem,
            int quantity,
            GameObject assemblyTemplate)
        {
            ShopCatalogEntry entry = new ShopCatalogEntry();
            entry.Configure(
                productId,
                category,
                displayName,
                description,
                price,
                kind,
                inventoryItem,
                quantity,
                assemblyTemplate);
            return entry;
        }

        private static HangarShopUI BuildShopUI(
            FirstPersonController firstPersonController,
            InventoryUI inventoryUI)
        {
            GameObject existing = GameObject.Find(ShopUiName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            Font font = ResolveUiFont(inventoryUI);
            GameObject canvasObject = new GameObject(
                ShopUiName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateUiPanel(
                canvasObject.transform,
                "Shop Panel",
                new Color(0.015f, 0.022f, 0.030f, 0.94f));
            SetStretch(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject window = CreateUiPanel(
                panel.transform,
                "Terminal Window",
                new Color(0.055f, 0.075f, 0.090f, 1f));
            RectTransform windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(1180f, 740f);
            windowRect.anchoredPosition = Vector2.zero;

            Text title = CreateUiText(
                window.transform,
                "Title",
                "HANGER 51 PARTS & ASSEMBLY TERMINAL",
                font,
                32,
                TextAnchor.MiddleLeft,
                Color.white);
            SetAnchoredRect(title.rectTransform, new Vector2(24f, -22f), new Vector2(850f, 54f), new Vector2(0f, 1f));

            Text balance = CreateUiText(window.transform, "Balance", "Account balance:", font, 22, TextAnchor.MiddleRight, new Color(0.65f, 1f, 0.72f, 1f));
            SetAnchoredRect(balance.rectTransform, new Vector2(-92f, -22f), new Vector2(300f, 42f), new Vector2(1f, 1f));
            Text capacity = CreateUiText(window.transform, "Shipment Capacity", "Shipment bays open:", font, 17, TextAnchor.MiddleRight, new Color(0.78f, 0.85f, 0.92f, 1f));
            SetAnchoredRect(capacity.rectTransform, new Vector2(-92f, -60f), new Vector2(300f, 32f), new Vector2(1f, 1f));

            Button closeButton = CreateUiButton(window.transform, "Close Button", "X", font, 24, new Color(0.40f, 0.10f, 0.10f, 1f));
            SetAnchoredRect(closeButton.GetComponent<RectTransform>(), new Vector2(-30f, -30f), new Vector2(52f, 52f), new Vector2(1f, 1f));

            GameObject productPanel = CreateUiPanel(window.transform, "Product List Panel", new Color(0.035f, 0.047f, 0.057f, 1f));
            SetAnchoredRect(productPanel.GetComponent<RectTransform>(), new Vector2(24f, -100f), new Vector2(430f, 565f), new Vector2(0f, 1f));

            GameObject scrollObject = new GameObject("Product Scroll View", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(productPanel.transform, false);
            SetStretch(scrollObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));

            GameObject viewport = CreateUiPanel(scrollObject.transform, "Viewport", new Color(0f, 0f, 0f, 0.01f));
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            SetStretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Button productTemplate = CreateUiButton(
                content.transform,
                "Product Button Template",
                "Product",
                font,
                18,
                new Color(0.10f, 0.15f, 0.18f, 1f));
            LayoutElement productLayout = productTemplate.gameObject.AddComponent<LayoutElement>();
            productLayout.preferredHeight = 76f;
            productLayout.minHeight = 76f;
            Text productTemplateText = productTemplate.GetComponentInChildren<Text>();
            productTemplateText.alignment = TextAnchor.MiddleLeft;
            productTemplateText.horizontalOverflow = HorizontalWrapMode.Wrap;
            productTemplateText.rectTransform.offsetMin = new Vector2(18f, 4f);
            productTemplateText.rectTransform.offsetMax = new Vector2(-12f, -4f);
            productTemplate.gameObject.SetActive(false);

            GameObject detailsPanel = CreateUiPanel(window.transform, "Product Details Panel", new Color(0.035f, 0.047f, 0.057f, 1f));
            SetAnchoredRect(detailsPanel.GetComponent<RectTransform>(), new Vector2(474f, -100f), new Vector2(682f, 565f), new Vector2(0f, 1f));

            Text selectedName = CreateUiText(detailsPanel.transform, "Selected Name", "Select a product", font, 28, TextAnchor.UpperLeft, Color.white);
            SetAnchoredRect(selectedName.rectTransform, new Vector2(24f, -22f), new Vector2(630f, 70f), new Vector2(0f, 1f));
            Text selectedCategory = CreateUiText(detailsPanel.transform, "Selected Category", string.Empty, font, 18, TextAnchor.UpperLeft, new Color(0.35f, 0.78f, 1f, 1f));
            SetAnchoredRect(selectedCategory.rectTransform, new Vector2(24f, -82f), new Vector2(630f, 32f), new Vector2(0f, 1f));
            Text selectedDescription = CreateUiText(detailsPanel.transform, "Selected Description", string.Empty, font, 20, TextAnchor.UpperLeft, new Color(0.88f, 0.90f, 0.92f, 1f));
            selectedDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedDescription.verticalOverflow = VerticalWrapMode.Truncate;
            SetAnchoredRect(selectedDescription.rectTransform, new Vector2(24f, -126f), new Vector2(630f, 220f), new Vector2(0f, 1f));
            Text selectedDelivery = CreateUiText(detailsPanel.transform, "Selected Delivery", string.Empty, font, 18, TextAnchor.UpperLeft, new Color(0.95f, 0.78f, 0.30f, 1f));
            SetAnchoredRect(selectedDelivery.rectTransform, new Vector2(24f, -360f), new Vector2(630f, 60f), new Vector2(0f, 1f));
            Text selectedPrice = CreateUiText(detailsPanel.transform, "Selected Price", "Price: —", font, 28, TextAnchor.MiddleLeft, new Color(0.65f, 1f, 0.72f, 1f));
            SetAnchoredRect(selectedPrice.rectTransform, new Vector2(24f, -438f), new Vector2(300f, 54f), new Vector2(0f, 1f));

            Button buyButton = CreateUiButton(detailsPanel.transform, "Buy Button", "BUY & DELIVER", font, 23, new Color(0.08f, 0.42f, 0.20f, 1f));
            SetAnchoredRect(buyButton.GetComponent<RectTransform>(), new Vector2(-24f, 24f), new Vector2(250f, 62f), new Vector2(1f, 0f));

            Text status = CreateUiText(window.transform, "Shop Status", string.Empty, font, 18, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.36f, 1f));
            SetAnchoredRect(status.rectTransform, new Vector2(30f, 18f), new Vector2(1090f, 44f), new Vector2(0f, 0f));

            HangarShopUI shopUI = canvasObject.AddComponent<HangarShopUI>();
            shopUI.Configure(
                panel,
                content.transform,
                productTemplate,
                buyButton,
                closeButton,
                balance,
                capacity,
                selectedName,
                selectedCategory,
                selectedDescription,
                selectedDelivery,
                selectedPrice,
                status,
                firstPersonController,
                inventoryUI,
                new List<Behaviour>());
            panel.SetActive(false);
            return shopUI;
        }

        private static void ConfigureShopUiBlockedBehaviours(
            HangarShopUI shopUI,
            List<Behaviour> blockedBehaviours)
        {
            if (shopUI == null)
            {
                return;
            }

            SerializedObject serializedUi = new SerializedObject(shopUI);
            SerializedProperty property = serializedUi.FindProperty("gameplayBehavioursToDisable");
            property.arraySize = blockedBehaviours.Count;
            for (int index = 0; index < blockedBehaviours.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = blockedBehaviours[index];
            }
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<Behaviour> GatherGameplayBehaviours(
            GameObject player,
            InventoryUI inventoryUI,
            HangarCommercePlayerInteractor commerceInteractor)
        {
            List<Behaviour> behaviours = new List<Behaviour>();
            AddBehaviour(behaviours, inventoryUI);
            AddBehaviour(behaviours, player.GetComponent<InventoryInteractor>());
            AddBehaviour(behaviours, player.GetComponent<EngineHoistPlayerInteractor>());
            AddBehaviour(behaviours, player.GetComponent<AircraftServicePlayerInteractor>());
            AddBehaviour(behaviours, player.GetComponent<P51PilotPlayerInteractor>());
            AddBehaviour(behaviours, player.GetComponent<P51TowBarPlayerInteractor>());
            AddBehaviour(behaviours, commerceInteractor);
            return behaviours;
        }

        private static void AddBehaviour(List<Behaviour> list, Behaviour behaviour)
        {
            if (behaviour != null && !list.Contains(behaviour))
            {
                list.Add(behaviour);
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                BaseInputModule oldModule = eventSystem.GetComponent<BaseInputModule>();
                if (oldModule != null)
                {
                    Object.DestroyImmediate(oldModule);
                }
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static CommerceMaterials CreateOrRefreshMaterials()
        {
            return new CommerceMaterials
            {
                DeskWood = CreateMaterial(MaterialFolder + "/DeskWood.mat", new Color(0.24f, 0.12f, 0.045f, 1f), 0.05f, 0.28f),
                DarkMetal = CreateMaterial(MaterialFolder + "/DarkMetal.mat", new Color(0.055f, 0.07f, 0.08f, 1f), 0.78f, 0.42f),
                ComputerPlastic = CreateMaterial(MaterialFolder + "/ComputerPlastic.mat", new Color(0.08f, 0.09f, 0.10f, 1f), 0.08f, 0.32f),
                Screen = CreateEmissiveMaterial(MaterialFolder + "/TerminalScreen.mat", new Color(0.04f, 0.24f, 0.34f, 1f), new Color(0.10f, 1.15f, 1.65f, 1f)),
                KeyPlastic = CreateMaterial(MaterialFolder + "/KeyboardKeys.mat", new Color(0.16f, 0.17f, 0.18f, 1f), 0.05f, 0.36f),
                CrateWood = CreateMaterial(MaterialFolder + "/CrateWood.mat", new Color(0.42f, 0.24f, 0.09f, 1f), 0.02f, 0.22f),
                CrateEdge = CreateMaterial(MaterialFolder + "/CrateEdgeWood.mat", new Color(0.24f, 0.12f, 0.04f, 1f), 0.02f, 0.18f),
                SteelBand = CreateMaterial(MaterialFolder + "/CrateSteelBand.mat", new Color(0.34f, 0.37f, 0.40f, 1f), 0.82f, 0.52f),
                SafetyYellow = CreateEmissiveMaterial(MaterialFolder + "/ShipmentSafetyYellow.mat", new Color(0.92f, 0.58f, 0.04f, 1f), new Color(0.45f, 0.22f, 0.01f, 1f)),
                ShippingWhite = CreateMaterial(MaterialFolder + "/ShippingLabel.mat", new Color(0.88f, 0.84f, 0.68f, 1f), 0.01f, 0.15f)
            };
        }

        private static Material CreateMaterial(string path, Color color, float metallic, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateEmissiveMaterial(string path, Color color, Color emission)
        {
            Material material = CreateMaterial(path, color, 0.12f, 0.50f);
            if (material != null && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material,
            bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }
            return part;
        }

        private static TextMesh CreateWorldText(
            Transform parent,
            string objectName,
            string text,
            Vector3 localPosition,
            Quaternion localRotation,
            float characterSize,
            Color color)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = localRotation;
            TextMesh textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 64;
            textMesh.characterSize = characterSize;
            textMesh.color = color;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            return textMesh;
        }

        private static GameObject CreateUiPanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateUiText(
            Transform parent,
            string name,
            string text,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text uiText = textObject.GetComponent<Text>();
            uiText.text = text;
            uiText.font = font;
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = color;
            uiText.raycastTarget = false;
            return uiText;
        }

        private static Button CreateUiButton(
            Transform parent,
            string name,
            string label,
            Font font,
            int fontSize,
            Color backgroundColor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            colors.disabledColor = new Color(0.38f, 0.38f, 0.38f, 0.65f);
            button.colors = colors;

            Text buttonText = CreateUiText(buttonObject.transform, "Label", label, font, fontSize, TextAnchor.MiddleCenter, Color.white);
            SetStretch(buttonText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return button;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetStretch(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Font ResolveUiFont(InventoryUI inventoryUI)
        {
            Text existingText = inventoryUI != null
                ? inventoryUI.GetComponentInChildren<Text>(true)
                : null;
            if (existingText != null && existingText.font != null)
            {
                return existingText.font;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return font;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }
            return null;
        }

        private static void RemoveExistingCommerceSystem()
        {
            GameObject existingRoot = GameObject.Find(CommerceRootName);
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }

            GameObject existingUi = GameObject.Find(ShopUiName);
            if (existingUi != null)
            {
                Undo.DestroyObjectImmediate(existingUi);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)
                || folderPath == "Assets"
                || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
