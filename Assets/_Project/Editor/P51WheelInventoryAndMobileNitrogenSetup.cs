using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51WheelInventoryAndMobileNitrogenSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string HangarRootName = "Hanger 51 Test Hangar";
        private const string CartName = "P-51 Nitrogen Tire Service Cart";
        private const string ItemFolder = "Assets/_Project/Inventory/Items";
        private const string ServicePartsFolder = "Assets/_Project/Aircraft/P51/ServiceParts";

        private const string MainTireItemPath = ItemFolder + "/P51MainLandingTire.asset";
        private const string TailTireItemPath = ItemFolder + "/P51TailwheelTire.asset";
        private const string MainRimItemPath = ItemFolder + "/P51MainWheelRim.asset";
        private const string TailRimItemPath = ItemFolder + "/P51TailwheelRim.asset";
        private const string MainTirePrefabPath = ServicePartsFolder + "/P51MainLandingTire.prefab";
        private const string TailTirePrefabPath = ServicePartsFolder + "/P51TailwheelTire.prefab";
        private const string MainRimPrefabPath = ServicePartsFolder + "/P51MainWheelRim.prefab";
        private const string TailRimPrefabPath = ServicePartsFolder + "/P51TailwheelRim.prefab";

        private const string TireMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/TireRubber.mat";
        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string ServiceMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string YellowMaterialPath =
            "Assets/_Project/EngineAssembly/Materials/HangarTest/SafetyYellow.mat";

        [MenuItem("Hanger 51/P-51 Mustang/30 - Add Inventory Wheels and Mobile Nitrogen Cart")]
        public static void AddInventoryWheelsAndMobileNitrogenCart()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 30 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(
                FindObjectsInactive.Include);
            PlayerInventory playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || aircraft == null
                || terminal == null
                || playerInventory == null
                || inventoryUI == null)
            {
                Debug.LogError(
                    "P-51 Step 30 failed. Open the saved movement-test scene and confirm the current P-51, shop, and Player inventory exist.");
                return;
            }

            P51LandingGearMaintenanceController maintenance =
                aircraft.GetComponent<P51LandingGearMaintenanceController>();
            P51RaycastLandingGear physics = aircraft.GetComponent<P51RaycastLandingGear>();
            if (maintenance == null || physics == null || !physics.IsConfigured)
            {
                Debug.LogError(
                    "P-51 Step 30 failed. Run P-51 Steps 28 and 29 first so the current serviceable landing gear exists.",
                    aircraft);
                return;
            }

            Material tireMaterial = AssetDatabase.LoadAssetAtPath<Material>(TireMaterialPath);
            Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material darkMaterial = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material serviceMaterial = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            Material yellowMaterial = AssetDatabase.LoadAssetAtPath<Material>(YellowMaterialPath);
            if (tireMaterial == null
                || metalMaterial == null
                || darkMaterial == null
                || serviceMaterial == null)
            {
                Debug.LogError("P-51 Step 30 failed. Existing P-51 landing-gear materials are missing.");
                return;
            }
            if (yellowMaterial == null)
            {
                yellowMaterial = serviceMaterial;
            }

            EnsureFolder("Assets/_Project/Aircraft/P51", "ServiceParts");
            EnsureFolder("Assets/_Project/Inventory", "Items");

            GameObject mainTirePrefab = EnsureTirePrefab(
                MainTirePrefabPath,
                "P-51 Main Landing Tire",
                0.38f,
                0.22f,
                tireMaterial);
            GameObject tailTirePrefab = EnsureTirePrefab(
                TailTirePrefabPath,
                "P-51 Tailwheel Tire",
                0.16f,
                0.12f,
                tireMaterial);
            GameObject mainRimPrefab = CreateOrRefreshRimPrefab(
                MainRimPrefabPath,
                "P-51 Main Wheel Rim",
                0.23f,
                0.14f,
                metalMaterial,
                darkMaterial,
                serviceMaterial);
            GameObject tailRimPrefab = CreateOrRefreshRimPrefab(
                TailRimPrefabPath,
                "P-51 Tailwheel Rim",
                0.095f,
                0.075f,
                metalMaterial,
                darkMaterial,
                serviceMaterial);

            InventoryItemDefinition mainTire = CreateOrRefreshItem(
                MainTireItemPath,
                P51LandingGearInventoryBridge.MainTireItemId,
                "P-51 Main Landing Tire",
                "A condition-bearing P-51 main tire. Removed tire health, pressure, and failure state remain with this exact inventory item.",
                mainTirePrefab,
                2,
                new Color(0.08f, 0.08f, 0.075f, 1f));
            InventoryItemDefinition tailTire = CreateOrRefreshItem(
                TailTireItemPath,
                P51LandingGearInventoryBridge.TailTireItemId,
                "P-51 Tailwheel Tire",
                "The smaller P-51 tailwheel tire. Its individual health and pressure persist through inventory and reinstallation.",
                tailTirePrefab,
                2,
                new Color(0.08f, 0.08f, 0.075f, 1f));
            InventoryItemDefinition mainRim = CreateOrRefreshItem(
                MainRimItemPath,
                P51LandingGearInventoryBridge.MainRimItemId,
                "P-51 Main Wheel Rim",
                "A large P-51 main-wheel rim. Remove the tire first; the rim can then be removed, stored, dropped, and reinstalled like other aircraft parts.",
                mainRimPrefab,
                2,
                new Color(0.62f, 0.64f, 0.65f, 1f));
            InventoryItemDefinition tailRim = CreateOrRefreshItem(
                TailRimItemPath,
                P51LandingGearInventoryBridge.TailRimItemId,
                "P-51 Tailwheel Rim",
                "The smaller P-51 tailwheel rim. It is not interchangeable with a main-wheel rim.",
                tailRimPrefab,
                2,
                new Color(0.58f, 0.60f, 0.61f, 1f));

            if (mainTire == null || tailTire == null || mainRim == null || tailRim == null)
            {
                Debug.LogError("P-51 Step 30 failed. One or more tire/rim inventory assets could not be created.");
                return;
            }

            ConfigureShopCatalog(terminal, mainTire, tailTire, mainRim, tailRim);

            P51LandingGearInventoryBridge bridge =
                aircraft.GetComponent<P51LandingGearInventoryBridge>();
            if (bridge == null)
            {
                bridge = Undo.AddComponent<P51LandingGearInventoryBridge>(aircraft);
            }
            bridge.Configure(mainTire, tailTire, mainRim, tailRim);

            P51LandingGearServicePlayerInteractor interactor =
                playerInventory.GetComponent<P51LandingGearServicePlayerInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<P51LandingGearServicePlayerInteractor>(
                    playerInventory.gameObject);
            }
            Camera camera = playerInventory.GetComponentInChildren<Camera>();
            interactor.Configure(camera, inventoryUI);

            P51NitrogenCartController cart = RebuildMobileNitrogenCart(
                aircraft.transform,
                tireMaterial,
                metalMaterial,
                darkMaterial,
                serviceMaterial,
                yellowMaterial);
            if (cart == null)
            {
                Debug.LogError("P-51 Step 30 failed. The mobile nitrogen cart could not be created.");
                return;
            }

            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(interactor);
            EditorUtility.SetDirty(cart);
            EditorUtility.SetDirty(terminal);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 30 changed the wheel service system but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 30 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = cart.gameObject;
            Debug.Log(
                "P-51 Step 30 complete. Tires and rims are now condition-bearing inventory parts, main/tail rim sizes are distinct, removed parts go to inventory or drop beside the aircraft when full, and the nitrogen cart was rebuilt as a detailed movable hangar service cart that must be wheeled within hose range.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/31 - Validate Inventory Wheels and Mobile Nitrogen Cart")]
        public static void ValidateInventoryWheelsAndMobileNitrogenCart()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(
                FindObjectsInactive.Include);
            P51LandingGearInventoryBridge bridge = aircraft != null
                ? aircraft.GetComponent<P51LandingGearInventoryBridge>()
                : null;
            P51NitrogenCartController cart = Object.FindFirstObjectByType<P51NitrogenCartController>();

            InventoryItemDefinition mainTire =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(MainTireItemPath);
            InventoryItemDefinition tailTire =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(TailTireItemPath);
            InventoryItemDefinition mainRim =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(MainRimItemPath);
            InventoryItemDefinition tailRim =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(TailRimItemPath);

            if (bridge == null || !bridge.IsReady)
            {
                Debug.LogError("P-51 Step 31 failed: wheel/tire inventory bridge is missing or incomplete.", aircraft);
                passed = false;
            }

            if (!ValidateTrackedItem(mainTire, EnginePartConditionKind.Tire)
                || !ValidateTrackedItem(tailTire, EnginePartConditionKind.Tire)
                || !ValidateTrackedItem(mainRim, EnginePartConditionKind.Rim)
                || !ValidateTrackedItem(tailRim, EnginePartConditionKind.Rim))
            {
                Debug.LogError("P-51 Step 31 failed: main/tail tires or rims are not valid condition-bearing inventory items.");
                passed = false;
            }

            if (terminal == null
                || !HasProduct(terminal, P51LandingGearInventoryBridge.MainTireItemId, mainTire)
                || !HasProduct(terminal, P51LandingGearInventoryBridge.TailTireItemId, tailTire)
                || !HasProduct(terminal, P51LandingGearInventoryBridge.MainRimItemId, mainRim)
                || !HasProduct(terminal, P51LandingGearInventoryBridge.TailRimItemId, tailRim))
            {
                Debug.LogError("P-51 Step 31 failed: one or more wheel-service parts are missing from the shop catalog.");
                passed = false;
            }

            GameObject hangar = GameObject.Find(HangarRootName);
            Rigidbody cartBody = cart != null ? cart.GetComponent<Rigidbody>() : null;
            Renderer[] cartRenderers = cart != null
                ? cart.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            if (cart == null
                || cartBody == null
                || !cartBody.isKinematic
                || cartRenderers.Length < 28
                || (hangar != null && !cart.transform.IsChildOf(hangar.transform)))
            {
                Debug.LogError(
                    $"P-51 Step 31 failed: mobile nitrogen cart is missing, not parked in the hangar, lacks its movement body, or has only {cartRenderers.Length} detailed renderers.");
                passed = false;
            }

            P51LandingGearServicePlayerInteractor interactor =
                Object.FindFirstObjectByType<P51LandingGearServicePlayerInteractor>();
            if (interactor == null)
            {
                Debug.LogError("P-51 Step 31 failed: Player wheel/cart service interactor is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 31 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 31 passed. Main/tail tires and rims are distinct condition-bearing inventory/shop items, the wheel service bridge is ready, and the upgraded nitrogen cart is parked in the hangar with a kinematic rolling body and detailed service-equipment visuals.");
            }
        }

        private static P51NitrogenCartController RebuildMobileNitrogenCart(
            Transform aircraft,
            Material tireMaterial,
            Material metalMaterial,
            Material darkMaterial,
            Material serviceMaterial,
            Material yellowMaterial)
        {
            GameObject oldCart = GameObject.Find(CartName);
            if (oldCart != null)
            {
                Undo.DestroyObjectImmediate(oldCart);
            }

            GameObject hangar = GameObject.Find(HangarRootName);
            Transform parent = hangar != null ? hangar.transform : null;
            GameObject cart = new GameObject(CartName);
            Undo.RegisterCreatedObjectUndo(cart, "Create mobile nitrogen cart");
            if (parent != null)
            {
                cart.transform.SetParent(parent, false);
                cart.transform.localPosition = new Vector3(-7.1f, 0.02f, 5.8f);
                cart.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            }
            else
            {
                cart.transform.position = aircraft.position
                    - aircraft.right * 8f
                    - aircraft.forward * 6f;
                cart.transform.rotation = Quaternion.Euler(
                    0f,
                    aircraft.eulerAngles.y + 25f,
                    0f);
            }

            Vector3 groundedPosition = cart.transform.position;
            groundedPosition.y = FindGroundY(groundedPosition);
            cart.transform.position = groundedPosition;

            BoxCollider interactionCollider = cart.AddComponent<BoxCollider>();
            interactionCollider.center = new Vector3(0f, 0.92f, 0f);
            interactionCollider.size = new Vector3(1.35f, 1.85f, 0.95f);

            Rigidbody body = cart.AddComponent<Rigidbody>();
            body.mass = 92f;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            CreatePart(cart.transform, PrimitiveType.Cube, "Heavy Chassis",
                new Vector3(0f, 0.32f, 0f), new Vector3(1.05f, 0.12f, 0.70f), Vector3.zero, darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Lower Bottle Cradle",
                new Vector3(0.08f, 0.58f, 0f), new Vector3(0.82f, 0.08f, 0.62f), Vector3.zero, serviceMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Left Upright",
                new Vector3(-0.46f, 1.18f, -0.30f), new Vector3(0.075f, 1.55f, 0.075f), new Vector3(0f, 0f, -6f), darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Right Upright",
                new Vector3(-0.46f, 1.18f, 0.30f), new Vector3(0.075f, 1.55f, 0.075f), new Vector3(0f, 0f, -6f), darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Upper Cross Brace",
                new Vector3(-0.53f, 1.82f, 0f), new Vector3(0.09f, 0.09f, 0.66f), Vector3.zero, darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cylinder, "Padded Push Handle",
                new Vector3(-0.68f, 1.73f, 0f), new Vector3(0.055f, 0.42f, 0.055f), new Vector3(90f, 0f, 0f), tireMaterial);

            Transform handlePoint = new GameObject("Cart Push Handle Point").transform;
            handlePoint.SetParent(cart.transform, false);
            handlePoint.localPosition = new Vector3(-0.72f, 1.65f, 0f);

            BuildBottle(cart.transform, "Nitrogen Bottle Left", new Vector3(0.05f, 1.10f, -0.20f), metalMaterial, darkMaterial, yellowMaterial);
            BuildBottle(cart.transform, "Nitrogen Bottle Right", new Vector3(0.05f, 1.10f, 0.20f), metalMaterial, darkMaterial, yellowMaterial);

            CreatePart(cart.transform, PrimitiveType.Cube, "Bottle Retaining Strap Lower",
                new Vector3(0.02f, 0.92f, 0f), new Vector3(0.46f, 0.07f, 0.58f), Vector3.zero, darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Bottle Retaining Strap Upper",
                new Vector3(0.02f, 1.38f, 0f), new Vector3(0.46f, 0.07f, 0.58f), Vector3.zero, darkMaterial);

            CreatePart(cart.transform, PrimitiveType.Cube, "Regulator Manifold",
                new Vector3(0.08f, 1.78f, 0f), new Vector3(0.46f, 0.18f, 0.30f), Vector3.zero, serviceMaterial);
            CreateGauge(cart.transform, "Supply Pressure Gauge", new Vector3(0.04f, 1.98f, -0.14f), yellowMaterial, darkMaterial);
            CreateGauge(cart.transform, "Outlet Pressure Gauge", new Vector3(0.04f, 1.98f, 0.14f), yellowMaterial, darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cylinder, "Regulator Adjustment Knob",
                new Vector3(0.30f, 1.80f, 0f), new Vector3(0.08f, 0.07f, 0.08f), new Vector3(0f, 0f, 90f), yellowMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Service Instruction Plate",
                new Vector3(-0.50f, 1.25f, 0f), new Vector3(0.035f, 0.40f, 0.52f), Vector3.zero, yellowMaterial);

            CreatePart(cart.transform, PrimitiveType.Cylinder, "Hose Reel Outer",
                new Vector3(0.48f, 1.03f, 0f), new Vector3(0.29f, 0.13f, 0.29f), new Vector3(90f, 0f, 0f), darkMaterial);
            CreatePart(cart.transform, PrimitiveType.Cylinder, "Hose Reel Inner",
                new Vector3(0.49f, 1.03f, 0f), new Vector3(0.19f, 0.145f, 0.19f), new Vector3(90f, 0f, 0f), serviceMaterial);
            CreatePart(cart.transform, PrimitiveType.Cylinder, "Hose Reel Hub",
                new Vector3(0.50f, 1.03f, 0f), new Vector3(0.065f, 0.17f, 0.065f), new Vector3(90f, 0f, 0f), metalMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Hose Reel Crank",
                new Vector3(0.52f, 1.25f, 0.20f), new Vector3(0.05f, 0.28f, 0.05f), new Vector3(35f, 0f, 0f), serviceMaterial);

            Transform hoseOrigin = new GameObject("Nitrogen Hose Outlet").transform;
            hoseOrigin.SetParent(cart.transform, false);
            hoseOrigin.localPosition = new Vector3(0.58f, 1.02f, 0.28f);

            Transform[] wheels = new Transform[3];
            wheels[0] = BuildCartWheel(cart.transform, "Left Cart Wheel", new Vector3(0.25f, 0.24f, -0.43f), tireMaterial, metalMaterial, yellowMaterial, false);
            wheels[1] = BuildCartWheel(cart.transform, "Right Cart Wheel", new Vector3(0.25f, 0.24f, 0.43f), tireMaterial, metalMaterial, yellowMaterial, false);
            wheels[2] = BuildCartWheel(cart.transform, "Front Swivel Caster", new Vector3(-0.43f, 0.15f, 0f), tireMaterial, metalMaterial, yellowMaterial, true);

            CreatePart(cart.transform, PrimitiveType.Cube, "Caster Fork",
                new Vector3(-0.43f, 0.27f, 0f), new Vector3(0.18f, 0.24f, 0.08f), Vector3.zero, serviceMaterial);
            CreatePart(cart.transform, PrimitiveType.Cube, "Lower Tool Tray",
                new Vector3(-0.12f, 0.48f, 0f), new Vector3(0.58f, 0.06f, 0.60f), Vector3.zero, metalMaterial);

            LineRenderer hose = cart.AddComponent<LineRenderer>();
            hose.enabled = false;
            hose.useWorldSpace = true;
            hose.startWidth = 0.035f;
            hose.endWidth = 0.028f;
            hose.numCornerVertices = 5;
            hose.numCapVertices = 3;
            hose.sharedMaterial = darkMaterial;

            P51NitrogenCartController controller = cart.AddComponent<P51NitrogenCartController>();
            controller.Configure(hoseOrigin, hose, 9f);
            controller.ConfigureMovement(body, handlePoint, wheels, 1.35f, 4.5f);
            return controller;
        }

        private static void BuildBottle(
            Transform parent,
            string label,
            Vector3 position,
            Material metal,
            Material dark,
            Material yellow)
        {
            CreatePart(parent, PrimitiveType.Cylinder, label + " Body",
                position, new Vector3(0.18f, 0.62f, 0.18f), Vector3.zero, metal);
            CreatePart(parent, PrimitiveType.Sphere, label + " Shoulder",
                position + Vector3.up * 0.60f, new Vector3(0.18f, 0.14f, 0.18f), Vector3.zero, metal);
            CreatePart(parent, PrimitiveType.Cylinder, label + " Neck",
                position + Vector3.up * 0.72f, new Vector3(0.055f, 0.10f, 0.055f), Vector3.zero, dark);
            CreatePart(parent, PrimitiveType.Cylinder, label + " Valve",
                position + Vector3.up * 0.82f, new Vector3(0.075f, 0.035f, 0.075f), Vector3.zero, yellow);
        }

        private static void CreateGauge(
            Transform parent,
            string name,
            Vector3 position,
            Material face,
            Material bezel)
        {
            CreatePart(parent, PrimitiveType.Cylinder, name + " Bezel",
                position, new Vector3(0.115f, 0.035f, 0.115f), new Vector3(90f, 0f, 0f), bezel);
            CreatePart(parent, PrimitiveType.Cylinder, name + " Face",
                position + new Vector3(0f, 0f, -0.038f), new Vector3(0.088f, 0.010f, 0.088f), new Vector3(90f, 0f, 0f), face);
            CreatePart(parent, PrimitiveType.Cube, name + " Needle",
                position + new Vector3(0f, 0f, -0.052f), new Vector3(0.012f, 0.065f, 0.008f), new Vector3(0f, 0f, 28f), bezel);
        }

        private static Transform BuildCartWheel(
            Transform parent,
            string name,
            Vector3 position,
            Material tire,
            Material metal,
            Material marker,
            bool caster)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            CreatePart(root.transform, PrimitiveType.Cylinder, name + " Tire",
                Vector3.zero,
                caster ? new Vector3(0.13f, 0.07f, 0.13f) : new Vector3(0.22f, 0.085f, 0.22f),
                new Vector3(90f, 0f, 0f),
                tire);
            CreatePart(root.transform, PrimitiveType.Cylinder, name + " Hub",
                Vector3.zero,
                caster ? new Vector3(0.065f, 0.085f, 0.065f) : new Vector3(0.10f, 0.10f, 0.10f),
                new Vector3(90f, 0f, 0f),
                metal);
            CreatePart(root.transform, PrimitiveType.Cube, name + " Rotation Marker",
                caster ? new Vector3(0.09f, 0f, 0f) : new Vector3(0.16f, 0f, 0f),
                caster ? new Vector3(0.04f, 0.02f, 0.02f) : new Vector3(0.055f, 0.025f, 0.025f),
                Vector3.zero,
                marker);
            return root.transform;
        }

        private static GameObject EnsureTirePrefab(
            string path,
            string displayName,
            float radius,
            float width,
            Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = new GameObject(displayName);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Tire Body",
                Vector3.zero, new Vector3(radius, width * 0.50f, radius), new Vector3(0f, 0f, 90f), material);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Sidewall",
                Vector3.zero, new Vector3(radius * 0.78f, width * 0.515f, radius * 0.78f), new Vector3(0f, 0f, 90f), material);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOrRefreshRimPrefab(
            string path,
            string displayName,
            float radius,
            float width,
            Material metal,
            Material dark,
            Material service)
        {
            GameObject root = new GameObject(displayName);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Outer Rim",
                Vector3.zero, new Vector3(radius, width * 0.50f, radius), new Vector3(0f, 0f, 90f), metal);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Hub",
                Vector3.zero, new Vector3(radius * 0.40f, width * 0.62f, radius * 0.40f), new Vector3(0f, 0f, 90f), dark);

            for (int index = 0; index < 6; index++)
            {
                float angle = index * 60f * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius * 0.62f,
                    Mathf.Sin(angle) * radius * 0.62f,
                    -width * 0.29f);
                CreatePart(root.transform, PrimitiveType.Cylinder, $"Rim Bolt {index + 1}",
                    position,
                    Vector3.one * Mathf.Max(0.015f, radius * 0.07f),
                    new Vector3(90f, 0f, 0f),
                    service);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static InventoryItemDefinition CreateOrRefreshItem(
            string path,
            string itemId,
            string displayName,
            string description,
            GameObject worldPrefab,
            int stackSize,
            Color color)
        {
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = displayName.Replace(" ", string.Empty);
                AssetDatabase.CreateAsset(item, path);
            }

            SerializedObject serialized = new SerializedObject(item);
            SetString(serialized, "itemId", itemId);
            SetString(serialized, "displayName", displayName);
            SetString(serialized, "description", description);
            SetInt(serialized, "maxStackSize", stackSize);
            SetBool(serialized, "canEquip", true);
            SetColor(serialized, "placeholderColor", color);
            SetObject(serialized, "worldPrefab", worldPrefab);
            SetVector(serialized, "worldScale", Vector3.one);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void ConfigureShopCatalog(
            HangarShopTerminal terminal,
            InventoryItemDefinition mainTire,
            InventoryItemDefinition tailTire,
            InventoryItemDefinition mainRim,
            InventoryItemDefinition tailRim)
        {
            SerializedObject serializedTerminal = new SerializedObject(terminal);
            SerializedProperty catalog = serializedTerminal.FindProperty("catalog");
            if (catalog == null)
            {
                return;
            }

            ConfigureProduct(
                FindOrAppendProduct(catalog, P51LandingGearInventoryBridge.MainTireItemId),
                P51LandingGearInventoryBridge.MainTireItemId,
                "Landing Gear",
                "P-51 Main Landing Tire",
                "New condition-bearing main tire for either P-51 main rim. Delivered partially inflated.",
                450,
                mainTire);
            ConfigureProduct(
                FindOrAppendProduct(catalog, P51LandingGearInventoryBridge.TailTireItemId),
                P51LandingGearInventoryBridge.TailTireItemId,
                "Landing Gear",
                "P-51 Tailwheel Tire",
                "New condition-bearing smaller tailwheel tire. Delivered partially inflated.",
                180,
                tailTire);
            ConfigureProduct(
                FindOrAppendProduct(catalog, P51LandingGearInventoryBridge.MainRimItemId),
                P51LandingGearInventoryBridge.MainRimItemId,
                "Landing Gear",
                "P-51 Main Wheel Rim",
                "Large replacement main-wheel rim for either main landing-gear station.",
                650,
                mainRim);
            ConfigureProduct(
                FindOrAppendProduct(catalog, P51LandingGearInventoryBridge.TailRimItemId),
                P51LandingGearInventoryBridge.TailRimItemId,
                "Landing Gear",
                "P-51 Tailwheel Rim",
                "Smaller replacement rim sized only for the P-51 tailwheel.",
                260,
                tailRim);
            serializedTerminal.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProduct(
            SerializedProperty entry,
            string id,
            string category,
            string displayName,
            string description,
            int price,
            InventoryItemDefinition item)
        {
            SetString(entry, "productId", id);
            SetString(entry, "category", category);
            SetString(entry, "displayName", displayName);
            SetString(entry, "description", description);
            SetInt(entry, "price", price);
            SerializedProperty kind = entry.FindPropertyRelative("productKind");
            if (kind != null)
            {
                kind.enumValueIndex = (int)ShopProductKind.InventoryItem;
            }
            SetObject(entry, "inventoryItem", item);
            SetInt(entry, "quantity", 1);
            SetObject(entry, "assemblyTemplate", null);
        }

        private static SerializedProperty FindOrAppendProduct(
            SerializedProperty catalog,
            string productId)
        {
            for (int index = 0; index < catalog.arraySize; index++)
            {
                SerializedProperty entry = catalog.GetArrayElementAtIndex(index);
                SerializedProperty id = entry.FindPropertyRelative("productId");
                if (id != null && id.stringValue == productId)
                {
                    return entry;
                }
            }

            catalog.InsertArrayElementAtIndex(catalog.arraySize);
            return catalog.GetArrayElementAtIndex(catalog.arraySize - 1);
        }

        private static bool HasProduct(
            HangarShopTerminal terminal,
            string productId,
            InventoryItemDefinition item)
        {
            if (terminal == null || item == null)
            {
                return false;
            }

            for (int index = 0; index < terminal.Catalog.Count; index++)
            {
                ShopCatalogEntry product = terminal.Catalog[index];
                if (product != null
                    && product.ProductId == productId
                    && product.ProductKind == ShopProductKind.InventoryItem
                    && product.InventoryItem == item
                    && product.IsConfigured)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ValidateTrackedItem(
            InventoryItemDefinition item,
            EnginePartConditionKind expectedKind)
        {
            return item != null
                && item.WorldPrefab != null
                && item.CanEquip
                && EnginePartConditionData.InferKind(item) == expectedKind;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static float FindGroundY(Vector3 position)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                position + Vector3.up * 20f,
                Vector3.down,
                50f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                if (hits[index].normal.y >= 0.45f)
                {
                    best = Mathf.Max(best, hits[index].point.y + 0.02f);
                }
            }
            return float.IsNegativeInfinity(best) ? position.y : best;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void SetString(SerializedObject serialized, string name, string value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.stringValue = value;
        }
        private static void SetInt(SerializedObject serialized, string name, int value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.intValue = value;
        }
        private static void SetBool(SerializedObject serialized, string name, bool value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.boolValue = value;
        }
        private static void SetColor(SerializedObject serialized, string name, Color value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.colorValue = value;
        }
        private static void SetVector(SerializedObject serialized, string name, Vector3 value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.vector3Value = value;
        }
        private static void SetObject(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.stringValue = value;
        }
        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.intValue = value;
        }
        private static void SetObject(SerializedProperty parent, string name, Object value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
