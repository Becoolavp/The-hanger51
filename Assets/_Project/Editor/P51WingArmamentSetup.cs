using System;
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
    public static class P51WingArmamentSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string ArmamentFolder = "Assets/_Project/Aircraft/P51/Armament";
        private const string PrefabFolder = ArmamentFolder + "/Prefabs";
        private const string MaterialFolder = ArmamentFolder + "/Materials";
        private const string ItemFolder = "Assets/_Project/Inventory/Items";

        private const string GunPrefabPath = PrefabFolder + "/P51M2WingGun.prefab";
        private const string AmmoPrefabPath = PrefabFolder + "/P51WingAmmoBox.prefab";
        private const string GunItemPath = ItemFolder + "/P51M2WingGun.asset";
        private const string AmmoItemPath = ItemFolder + "/P51WingAmmoBox.asset";

        private const string GunMetalPath = MaterialFolder + "/WingGunMetal.mat";
        private const string GunDarkPath = MaterialFolder + "/WingGunDark.mat";
        private const string BayPath = MaterialFolder + "/ArmamentBayDark.mat";
        private const string AmmoOlivePath = MaterialFolder + "/AmmoOlive.mat";
        private const string BrassPath = MaterialFolder + "/ArmamentBrass.mat";
        private const string HighlightPath = MaterialFolder + "/ArmamentInstallHighlight.mat";

        [MenuItem("Hanger 51/P-51 Mustang/32 - Add Serviceable Wing Armament")]
        public static void AddServiceableWingArmament()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 32 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(FindObjectsInactive.Include);
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || aircraft == null
                || terminal == null
                || inventory == null
                || inventoryUI == null)
            {
                Debug.LogError("P-51 Step 32 failed. Open the saved movement-test scene and confirm the current P-51, shop, and Player inventory exist.");
                return;
            }

            P51FlightController flight = aircraft.GetComponent<P51FlightController>();
            if (flight == null)
            {
                Debug.LogError("P-51 Step 32 failed. The current P-51 flight controller is missing.", aircraft);
                return;
            }

            EnsureFolder("Assets/_Project/Aircraft/P51", "Armament");
            EnsureFolder(ArmamentFolder, "Prefabs");
            EnsureFolder(ArmamentFolder, "Materials");
            EnsureFolder("Assets/_Project/Inventory", "Items");

            Material gunMetal = CreateOrRefreshMaterial(GunMetalPath, new Color(0.13f, 0.14f, 0.15f, 1f), 0.82f, 0.34f, false);
            Material gunDark = CreateOrRefreshMaterial(GunDarkPath, new Color(0.045f, 0.05f, 0.055f, 1f), 0.55f, 0.24f, false);
            Material bayDark = CreateOrRefreshMaterial(BayPath, new Color(0.035f, 0.04f, 0.045f, 1f), 0.32f, 0.18f, false);
            Material ammoOlive = CreateOrRefreshMaterial(AmmoOlivePath, new Color(0.20f, 0.24f, 0.12f, 1f), 0.18f, 0.16f, false);
            Material brass = CreateOrRefreshMaterial(BrassPath, new Color(0.68f, 0.46f, 0.15f, 1f), 0.72f, 0.38f, false);
            Material highlight = CreateOrRefreshMaterial(HighlightPath, new Color(0.10f, 1f, 0.25f, 1f), 0.05f, 0.15f, true);
            if (gunMetal == null || gunDark == null || bayDark == null || ammoOlive == null || brass == null || highlight == null)
            {
                Debug.LogError("P-51 Step 32 failed. One or more armament materials could not be created.");
                return;
            }

            GameObject gunPrefab = CreateM2StyleWingGunPrefab(gunMetal, gunDark, brass);
            GameObject ammoPrefab = CreateWingAmmoBoxPrefab(ammoOlive, gunDark, brass);
            if (gunPrefab == null || ammoPrefab == null)
            {
                Debug.LogError("P-51 Step 32 failed. The gun or ammunition-box prefab could not be created.");
                return;
            }

            InventoryItemDefinition gunItem = CreateOrRefreshItem(
                GunItemPath,
                P51WingArmamentSystem.GunItemId,
                "P-51 M2 Wing Gun",
                "A game-authentic external M2-style aircraft gun assembly for one P-51 wing station. Install it through an open wing access panel and secure its hold-down bolts.",
                gunPrefab,
                1,
                new Color(0.13f, 0.14f, 0.15f, 1f));
            InventoryItemDefinition ammoItem = CreateOrRefreshItem(
                AmmoItemPath,
                P51WingArmamentSystem.AmmoBoxItemId,
                "P-51 Wing Ammunition Box",
                "A boxed ammunition belt for one P-51 wing gun station. Place it in the compartment beside an installed gun and connect the belt before flight.",
                ammoPrefab,
                3,
                new Color(0.20f, 0.24f, 0.12f, 1f));
            if (gunItem == null || ammoItem == null)
            {
                Debug.LogError("P-51 Step 32 failed. Armament inventory items could not be created.");
                return;
            }

            ConfigureShopCatalog(terminal, gunItem, ammoItem);

            Transform oldRoot = aircraft.transform.Find(ArmamentRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            }
            P51WingArmamentSystem oldSystem = aircraft.GetComponent<P51WingArmamentSystem>();
            if (oldSystem != null)
            {
                Undo.DestroyObjectImmediate(oldSystem);
            }

            GameObject armamentRootObject = new GameObject(ArmamentRootName);
            Undo.RegisterCreatedObjectUndo(armamentRootObject, "Create P-51 wing armament");
            armamentRootObject.transform.SetParent(aircraft.transform, false);
            Transform armamentRoot = armamentRootObject.transform;

            Transform[] panelPivots = new Transform[2];
            GameObject[] bayInteriors = new GameObject[2];
            GameObject[] gunVisuals = new GameObject[6];
            GameObject[] ammoVisuals = new GameObject[6];
            Transform[] muzzles = new Transform[6];
            Transform[] ejectionPorts = new Transform[6];
            List<P51WingArmamentServiceTarget> serviceTargets = new List<P51WingArmamentServiceTarget>();

            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                bool left = wingIndex == 0;
                float sign = left ? -1f : 1f;
                string wingName = left ? "Left" : "Right";
                float centerX = sign * 2.45f;

                GameObject panelPivotObject = new GameObject($"{wingName} Wing Armament Panel Pivot");
                panelPivotObject.transform.SetParent(armamentRoot, false);
                panelPivotObject.transform.localPosition = new Vector3(centerX, 1.61f, -0.78f);
                panelPivots[wingIndex] = panelPivotObject.transform;

                GameObject panel = CreatePrimitive(
                    panelPivotObject.transform,
                    PrimitiveType.Cube,
                    $"{wingName} Wing Armament Access Panel",
                    new Vector3(0f, 0f, 0.78f),
                    new Vector3(2.85f, 0.045f, 1.56f),
                    Vector3.zero,
                    gunMetal,
                    false);
                CreatePanelDetail(panel.transform, gunDark, wingName);

                GameObject panelTargetObject = new GameObject($"{wingName} Wing Armament Panel Service Target");
                panelTargetObject.transform.SetParent(armamentRoot, false);
                panelTargetObject.transform.localPosition = new Vector3(centerX, 1.66f, 0f);
                BoxCollider panelCollider = panelTargetObject.AddComponent<BoxCollider>();
                panelCollider.isTrigger = true;
                panelCollider.size = new Vector3(2.92f, 0.22f, 1.64f);
                P51WingArmamentServiceTarget panelTarget = panelTargetObject.AddComponent<P51WingArmamentServiceTarget>();
                serviceTargets.Add(panelTarget);

                GameObject interior = new GameObject($"{wingName} Wing Armament Bay Interior");
                interior.transform.SetParent(armamentRoot, false);
                interior.transform.localPosition = new Vector3(centerX, 1.56f, 0f);
                bayInteriors[wingIndex] = interior;

                CreatePrimitive(
                    interior.transform,
                    PrimitiveType.Cube,
                    $"{wingName} Armament Bay Recess",
                    Vector3.zero,
                    new Vector3(2.78f, 0.055f, 1.48f),
                    Vector3.zero,
                    bayDark,
                    false);
                CreateBayRibs(interior.transform, gunDark, left);

                for (int localStation = 0; localStation < 3; localStation++)
                {
                    int stationIndex = wingIndex * 3 + localStation;
                    float globalGunX = sign * (1.55f + localStation * 0.75f);
                    float localGunX = globalGunX - centerX;
                    float gunZ = -0.46f + localStation * 0.045f;

                    GameObject gunTargetObject = new GameObject($"{wingName} Gun Mount {localStation + 1}");
                    gunTargetObject.transform.SetParent(interior.transform, false);
                    gunTargetObject.transform.localPosition = new Vector3(localGunX, 0.12f, gunZ);
                    SphereCollider gunCollider = gunTargetObject.AddComponent<SphereCollider>();
                    gunCollider.isTrigger = true;
                    gunCollider.radius = 0.36f;

                    GameObject mountedGun = InstantiateVisualPrefab(gunPrefab, gunTargetObject.transform, "Installed M2 Wing Gun");
                    gunVisuals[stationIndex] = mountedGun;

                    GameObject muzzleObject = new GameObject("Muzzle");
                    muzzleObject.transform.SetParent(gunTargetObject.transform, false);
                    muzzleObject.transform.localPosition = new Vector3(0f, 0f, 1.70f);
                    muzzles[stationIndex] = muzzleObject.transform;

                    GameObject ejectObject = new GameObject("Spent Casing Ejection Port");
                    ejectObject.transform.SetParent(gunTargetObject.transform, false);
                    ejectObject.transform.localPosition = new Vector3(sign * 0.12f, -0.18f, 0.10f);
                    ejectObject.transform.localRotation = Quaternion.Euler(0f, 0f, left ? -18f : 18f);
                    ejectionPorts[stationIndex] = ejectObject.transform;

                    Transform[] gunBolts = CreateGunHoldDownBolts(gunTargetObject.transform, gunMetal, gunDark);
                    GameObject gunHighlight = CreateInstallHighlight(gunTargetObject.transform, highlight, 0.46f, 0.31f);
                    P51WingArmamentServiceTarget gunTarget = gunTargetObject.AddComponent<P51WingArmamentServiceTarget>();
                    serviceTargets.Add(gunTarget);

                    float ammoLocalX = localGunX + (left ? -0.30f : 0.30f);
                    GameObject ammoTargetObject = new GameObject($"{wingName} Ammo Bay {localStation + 1}");
                    ammoTargetObject.transform.SetParent(interior.transform, false);
                    ammoTargetObject.transform.localPosition = new Vector3(ammoLocalX, 0.13f, 0.43f);
                    BoxCollider ammoCollider = ammoTargetObject.AddComponent<BoxCollider>();
                    ammoCollider.isTrigger = true;
                    ammoCollider.size = new Vector3(0.54f, 0.38f, 0.56f);

                    GameObject mountedAmmo = InstantiateVisualPrefab(ammoPrefab, ammoTargetObject.transform, "Installed Wing Ammo Box");
                    ammoVisuals[stationIndex] = mountedAmmo;
                    CreateFeedBeltBridge(mountedAmmo.transform, left ? Vector3.right : Vector3.left, brass, gunDark);

                    Transform[] ammoLatches = CreateAmmoHoldDowns(ammoTargetObject.transform, gunDark);
                    GameObject ammoHighlight = CreateInstallHighlight(ammoTargetObject.transform, highlight, 0.38f, 0.27f);
                    P51WingArmamentServiceTarget ammoTarget = ammoTargetObject.AddComponent<P51WingArmamentServiceTarget>();
                    serviceTargets.Add(ammoTarget);

                    CreateTextLabel(
                        interior.transform,
                        $"GUN {localStation + 1}",
                        new Vector3(localGunX, 0.18f, -0.68f),
                        0.055f,
                        Color.white);
                    CreateTextLabel(
                        interior.transform,
                        $"AMMO {localStation + 1}",
                        new Vector3(ammoLocalX, 0.18f, 0.70f),
                        0.045f,
                        new Color(0.85f, 0.88f, 0.72f, 1f));

                    // Configure after the system is created below. Store temporary component data
                    // through local captured arrays to avoid changing the user's aircraft hierarchy.
                    gunTarget.Configure(null, P51WingArmamentServiceKind.GunMount, wingIndex, stationIndex, gunBolts, gunHighlight);
                    ammoTarget.Configure(null, P51WingArmamentServiceKind.AmmoBay, wingIndex, stationIndex, ammoLatches, ammoHighlight);
                }

                panelTarget.Configure(null, P51WingArmamentServiceKind.WingPanel, wingIndex, wingIndex * 3, Array.Empty<Transform>(), null);
                interior.SetActive(false);
            }

            P51WingArmamentSystem system = Undo.AddComponent<P51WingArmamentSystem>(aircraft);
            system.Configure(
                flight,
                gunItem,
                ammoItem,
                panelPivots,
                bayInteriors,
                gunVisuals,
                ammoVisuals,
                muzzles,
                ejectionPorts);

            for (int index = 0; index < serviceTargets.Count; index++)
            {
                P51WingArmamentServiceTarget target = serviceTargets[index];
                if (target == null) continue;
                SerializedObject serializedTarget = new SerializedObject(target);
                SerializedProperty systemProperty = serializedTarget.FindProperty("system");
                if (systemProperty != null) systemProperty.objectReferenceValue = system;
                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }

            P51WingArmamentPlayerInteractor armamentInteractor =
                inventory.GetComponent<P51WingArmamentPlayerInteractor>();
            if (armamentInteractor == null)
            {
                armamentInteractor = Undo.AddComponent<P51WingArmamentPlayerInteractor>(inventory.gameObject);
            }
            Camera playerCamera = inventory.GetComponentInChildren<Camera>(true);
            armamentInteractor.Configure(playerCamera, inventoryUI);

            EditorUtility.SetDirty(system);
            EditorUtility.SetDirty(armamentInteractor);
            EditorUtility.SetDirty(terminal);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 32 built the wing armament system but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 32 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = armamentRootObject;
            Debug.Log(
                "P-51 Step 32 complete. Added two hinged wing armament access panels, six serviceable gun mounts, six ammunition compartments, purchasable/unboxable M2-style wing guns and ammunition boxes, hold-down hardware, installation highlights, cockpit firing, tracers, and spent casing ejection.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/33 - Validate Serviceable Wing Armament")]
        public static void ValidateServiceableWingArmament()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            P51WingArmamentSystem system = aircraft != null ? aircraft.GetComponent<P51WingArmamentSystem>() : null;
            Transform root = aircraft != null ? aircraft.transform.Find(ArmamentRootName) : null;
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(FindObjectsInactive.Include);
            InventoryItemDefinition gunItem = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(GunItemPath);
            InventoryItemDefinition ammoItem = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(AmmoItemPath);

            if (aircraft == null || system == null || root == null)
            {
                Debug.LogError("P-51 Step 33 failed: aircraft, armament system, or armament hierarchy is missing.");
                passed = false;
            }

            P51WingArmamentServiceTarget[] targets = root != null
                ? root.GetComponentsInChildren<P51WingArmamentServiceTarget>(true)
                : Array.Empty<P51WingArmamentServiceTarget>();
            int panelTargets = 0;
            int gunTargets = 0;
            int ammoTargets = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == null) continue;
                switch (targets[index].ServiceKind)
                {
                    case P51WingArmamentServiceKind.WingPanel: panelTargets++; break;
                    case P51WingArmamentServiceKind.GunMount: gunTargets++; break;
                    case P51WingArmamentServiceKind.AmmoBay: ammoTargets++; break;
                }
            }
            if (panelTargets != 2 || gunTargets != 6 || ammoTargets != 6)
            {
                Debug.LogError($"P-51 Step 33 failed: expected 2 wing panels, 6 gun mounts, and 6 ammo bays; found {panelTargets}, {gunTargets}, and {ammoTargets}.");
                passed = false;
            }

            if (gunItem == null || gunItem.ItemId != P51WingArmamentSystem.GunItemId || gunItem.WorldPrefab == null)
            {
                Debug.LogError("P-51 Step 33 failed: P-51 M2 Wing Gun inventory item/prefab is missing or invalid.");
                passed = false;
            }
            if (ammoItem == null || ammoItem.ItemId != P51WingArmamentSystem.AmmoBoxItemId || ammoItem.WorldPrefab == null)
            {
                Debug.LogError("P-51 Step 33 failed: P-51 Wing Ammunition Box inventory item/prefab is missing or invalid.");
                passed = false;
            }

            if (terminal == null
                || !HasProduct(terminal, P51WingArmamentSystem.GunItemId, gunItem)
                || !HasProduct(terminal, P51WingArmamentSystem.AmmoBoxItemId, ammoItem))
            {
                Debug.LogError("P-51 Step 33 failed: gun or ammunition product is missing from the shop catalog.");
                passed = false;
            }

            P51WingArmamentPlayerInteractor interactor = Object.FindFirstObjectByType<P51WingArmamentPlayerInteractor>(FindObjectsInactive.Include);
            if (interactor == null)
            {
                Debug.LogError("P-51 Step 33 failed: Player wing-armament interactor is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 33 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log("P-51 Step 33 passed. The two wing access panels, six gun stations, six ammunition bays, shop items, Player service interaction, and cockpit armament runtime are configured.");
            }
        }

        private static GameObject CreateM2StyleWingGunPrefab(Material metal, Material dark, Material brass)
        {
            AssetDatabase.DeleteAsset(GunPrefabPath);
            GameObject root = new GameObject("P-51 M2 Wing Gun");

            CreatePrimitive(root.transform, PrimitiveType.Cube, "Receiver",
                new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.30f, 0.62f), Vector3.zero, metal, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Feed Cover",
                new Vector3(0f, 0.18f, 0.02f), new Vector3(0.37f, 0.075f, 0.46f), new Vector3(-3f, 0f, 0f), dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Left Receiver Plate",
                new Vector3(-0.19f, 0f, 0f), new Vector3(0.035f, 0.255f, 0.56f), Vector3.zero, dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Right Receiver Plate",
                new Vector3(0.19f, 0f, 0f), new Vector3(0.035f, 0.255f, 0.56f), Vector3.zero, dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Rear Housing",
                new Vector3(0f, 0f, -0.43f), new Vector3(0.30f, 0.27f, 0.25f), Vector3.zero, metal, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Rear Buffer Cap",
                new Vector3(0f, 0f, -0.61f), new Vector3(0.115f, 0.105f, 0.115f), new Vector3(90f, 0f, 0f), dark, false);

            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Barrel Jacket",
                new Vector3(0f, 0f, 0.73f), new Vector3(0.072f, 0.47f, 0.072f), new Vector3(90f, 0f, 0f), dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Barrel",
                new Vector3(0f, 0f, 1.29f), new Vector3(0.035f, 0.34f, 0.035f), new Vector3(90f, 0f, 0f), metal, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Muzzle Collar",
                new Vector3(0f, 0f, 1.64f), new Vector3(0.064f, 0.075f, 0.064f), new Vector3(90f, 0f, 0f), dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Muzzle Opening",
                new Vector3(0f, 0f, 1.72f), new Vector3(0.043f, 0.018f, 0.043f), new Vector3(90f, 0f, 0f), gunBlackMaterial: dark, keepCollider: false);

            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Side Actuator",
                new Vector3(0.22f, 0.07f, -0.12f), new Vector3(0.045f, 0.16f, 0.045f), new Vector3(0f, 0f, 90f), dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Feed Chute Guide",
                new Vector3(-0.23f, 0.09f, 0.11f), new Vector3(0.09f, 0.14f, 0.28f), new Vector3(0f, 0f, 6f), metal, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Bottom Mount Rail",
                new Vector3(0f, -0.18f, -0.03f), new Vector3(0.25f, 0.07f, 0.50f), Vector3.zero, dark, false);

            for (int xIndex = -1; xIndex <= 1; xIndex += 2)
            {
                for (int zIndex = -1; zIndex <= 1; zIndex += 2)
                {
                    CreatePrimitive(root.transform, PrimitiveType.Cube, "Mount Lug",
                        new Vector3(xIndex * 0.14f, -0.21f, zIndex * 0.19f),
                        new Vector3(0.10f, 0.055f, 0.12f), Vector3.zero, metal, false);
                }
            }

            for (int index = 0; index < 12; index++)
            {
                bool right = index >= 6;
                int local = index % 6;
                CreatePrimitive(root.transform, PrimitiveType.Sphere, "Receiver Fastener",
                    new Vector3(right ? 0.211f : -0.211f, 0.09f - local * 0.035f, -0.22f + local * 0.09f),
                    Vector3.one * 0.026f, Vector3.zero, brass, false);
            }

            BoxCollider rootCollider = root.AddComponent<BoxCollider>();
            rootCollider.isTrigger = true;
            rootCollider.center = new Vector3(0f, 0f, 0.50f);
            rootCollider.size = new Vector3(0.52f, 0.48f, 2.42f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GunPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateWingAmmoBoxPrefab(Material olive, Material dark, Material brass)
        {
            AssetDatabase.DeleteAsset(AmmoPrefabPath);
            GameObject root = new GameObject("P-51 Wing Ammunition Box");
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Ammunition Case",
                Vector3.zero, new Vector3(0.44f, 0.32f, 0.46f), Vector3.zero, olive, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Case Lid",
                new Vector3(0f, 0.18f, 0f), new Vector3(0.46f, 0.055f, 0.48f), Vector3.zero, dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Front Latch",
                new Vector3(0f, 0.09f, 0.245f), new Vector3(0.11f, 0.13f, 0.035f), Vector3.zero, dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Carry Handle",
                new Vector3(0f, 0.27f, -0.02f), new Vector3(0.24f, 0.035f, 0.06f), Vector3.zero, dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Left Handle Post",
                new Vector3(-0.11f, 0.23f, -0.02f), new Vector3(0.035f, 0.10f, 0.04f), Vector3.zero, dark, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Right Handle Post",
                new Vector3(0.11f, 0.23f, -0.02f), new Vector3(0.035f, 0.10f, 0.04f), Vector3.zero, dark, false);

            for (int index = 0; index < 10; index++)
            {
                float x = -0.18f + index * 0.04f;
                CreatePrimitive(root.transform, PrimitiveType.Cube, "Visible Belt Link",
                    new Vector3(x, 0.22f, 0.17f), new Vector3(0.032f, 0.022f, 0.08f),
                    new Vector3(0f, index % 2 == 0 ? 4f : -4f, 0f), brass, false);
            }

            BoxCollider rootCollider = root.AddComponent<BoxCollider>();
            rootCollider.isTrigger = true;
            rootCollider.center = Vector3.zero;
            rootCollider.size = new Vector3(0.54f, 0.52f, 0.56f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AmmoPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreatePanelDetail(Transform panel, Material dark, string wingName)
        {
            CreatePrimitive(panel, PrimitiveType.Cube, $"{wingName} Panel Hinge Strip",
                new Vector3(0f, 0.055f, -0.48f), new Vector3(2.55f, 0.035f, 0.08f), Vector3.zero, dark, false);
            for (int index = -4; index <= 4; index++)
            {
                CreatePrimitive(panel, PrimitiveType.Sphere, "Panel Fastener",
                    new Vector3(index * 0.28f, 0.065f, 0.50f), Vector3.one * 0.035f, Vector3.zero, dark, false);
            }
        }

        private static void CreateBayRibs(Transform interior, Material dark, bool left)
        {
            for (int index = -2; index <= 2; index++)
            {
                CreatePrimitive(interior, PrimitiveType.Cube, "Armament Bay Rib",
                    new Vector3(index * 0.55f, 0.06f, 0f), new Vector3(0.035f, 0.12f, 1.30f), Vector3.zero, dark, false);
            }
            CreatePrimitive(interior, PrimitiveType.Cube, "Front Bay Spar",
                new Vector3(0f, 0.08f, 0.67f), new Vector3(2.70f, 0.12f, 0.045f), Vector3.zero, dark, false);
            CreatePrimitive(interior, PrimitiveType.Cube, "Rear Bay Spar",
                new Vector3(0f, 0.08f, -0.67f), new Vector3(2.70f, 0.12f, 0.045f), Vector3.zero, dark, false);
        }

        private static Transform[] CreateGunHoldDownBolts(Transform parent, Material metal, Material dark)
        {
            Transform[] bolts = new Transform[4];
            Vector3[] positions =
            {
                new Vector3(-0.16f, 0.15f, -0.21f),
                new Vector3(0.16f, 0.15f, -0.21f),
                new Vector3(-0.16f, 0.15f, 0.21f),
                new Vector3(0.16f, 0.15f, 0.21f)
            };
            for (int index = 0; index < bolts.Length; index++)
            {
                GameObject bolt = CreatePrimitive(parent, PrimitiveType.Cylinder, "Gun Hold-Down Bolt",
                    positions[index], new Vector3(0.032f, 0.035f, 0.032f), Vector3.zero, metal, false);
                CreatePrimitive(bolt.transform, PrimitiveType.Cube, "Bolt Head Slot",
                    new Vector3(0f, 0.07f, 0f), new Vector3(0.055f, 0.012f, 0.018f), Vector3.zero, dark, false);
                bolts[index] = bolt.transform;
            }
            return bolts;
        }

        private static Transform[] CreateAmmoHoldDowns(Transform parent, Material dark)
        {
            Transform[] latches = new Transform[2];
            latches[0] = CreatePrimitive(parent, PrimitiveType.Cube, "Ammo Box Latch",
                new Vector3(-0.20f, 0.18f, 0f), new Vector3(0.05f, 0.10f, 0.32f), Vector3.zero, dark, false).transform;
            latches[1] = CreatePrimitive(parent, PrimitiveType.Cube, "Ammo Box Latch",
                new Vector3(0.20f, 0.18f, 0f), new Vector3(0.05f, 0.10f, 0.32f), Vector3.zero, dark, false).transform;
            return latches;
        }

        private static GameObject CreateInstallHighlight(Transform parent, Material material, float radius, float thickness)
        {
            GameObject root = new GameObject("Armament Install Highlight");
            root.transform.SetParent(parent, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Highlight Front",
                new Vector3(0f, 0.22f, radius), new Vector3(radius * 1.65f, thickness * 0.10f, thickness * 0.10f), Vector3.zero, material, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Highlight Rear",
                new Vector3(0f, 0.22f, -radius), new Vector3(radius * 1.65f, thickness * 0.10f, thickness * 0.10f), Vector3.zero, material, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Highlight Left",
                new Vector3(-radius, 0.22f, 0f), new Vector3(thickness * 0.10f, thickness * 0.10f, radius * 1.65f), Vector3.zero, material, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Highlight Right",
                new Vector3(radius, 0.22f, 0f), new Vector3(thickness * 0.10f, thickness * 0.10f, radius * 1.65f), Vector3.zero, material, false);
            root.SetActive(false);
            return root;
        }

        private static void CreateFeedBeltBridge(Transform ammoVisual, Vector3 towardGun, Material brass, Material dark)
        {
            if (ammoVisual == null) return;
            GameObject bridge = new GameObject("Installed Feed Belt");
            bridge.transform.SetParent(ammoVisual, false);
            for (int index = 0; index < 8; index++)
            {
                float t = index / 7f;
                Vector3 position = towardGun.normalized * Mathf.Lerp(0.22f, 0.48f, t)
                    + Vector3.up * Mathf.Lerp(0.18f, 0.09f, t)
                    + Vector3.back * Mathf.Sin(t * Mathf.PI) * 0.05f;
                CreatePrimitive(bridge.transform, PrimitiveType.Cube, "Feed Belt Link",
                    position, new Vector3(0.045f, 0.028f, 0.085f),
                    new Vector3(0f, 0f, towardGun.x > 0f ? -8f : 8f), index % 2 == 0 ? brass : dark, false);
            }
        }

        private static GameObject InstantiateVisualPrefab(GameObject prefab, Transform parent, string objectName)
        {
            GameObject visual = prefab != null
                ? PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject
                : null;
            if (visual == null) return null;
            visual.name = objectName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null) Object.DestroyImmediate(colliders[index]);
            }
            InventoryPickup[] pickups = visual.GetComponentsInChildren<InventoryPickup>(true);
            for (int index = 0; index < pickups.Length; index++)
            {
                if (pickups[index] != null) Object.DestroyImmediate(pickups[index]);
            }
            visual.SetActive(false);
            return visual;
        }

        private static void CreateTextLabel(Transform parent, string text, Vector3 position, float size, Color color)
        {
            GameObject labelObject = new GameObject(text + " Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh mesh = labelObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 42;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
        }

        private static InventoryItemDefinition CreateOrRefreshItem(
            string path,
            string itemId,
            string displayName,
            string description,
            GameObject worldPrefab,
            int maxStack,
            Color placeholderColor)
        {
            InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
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
            SetInt(serialized, "maxStackSize", maxStack);
            SetBool(serialized, "canEquip", true);
            SetColor(serialized, "placeholderColor", placeholderColor);
            SetObject(serialized, "worldPrefab", worldPrefab);
            SetVector(serialized, "worldScale", Vector3.one);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void ConfigureShopCatalog(
            HangarShopTerminal terminal,
            InventoryItemDefinition gunItem,
            InventoryItemDefinition ammoItem)
        {
            SerializedObject serializedTerminal = new SerializedObject(terminal);
            SerializedProperty catalog = serializedTerminal.FindProperty("catalog");
            if (catalog == null) return;

            ConfigureCatalogProduct(
                FindOrAppendProduct(catalog, P51WingArmamentSystem.GunItemId),
                P51WingArmamentSystem.GunItemId,
                "Armament",
                "P-51 M2 Wing Gun",
                "Crated replacement wing gun assembly. One gun fills one of the three serviceable positions in either wing.",
                3800,
                gunItem);
            ConfigureCatalogProduct(
                FindOrAppendProduct(catalog, P51WingArmamentSystem.AmmoBoxItemId),
                P51WingArmamentSystem.AmmoBoxItemId,
                "Armament",
                "P-51 Wing Ammunition Box",
                "Crated boxed ammunition belt for one installed wing gun. Six boxes fully supply all six gun stations.",
                650,
                ammoItem);
            serializedTerminal.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindOrAppendProduct(SerializedProperty catalog, string productId)
        {
            for (int index = 0; index < catalog.arraySize; index++)
            {
                SerializedProperty item = catalog.GetArrayElementAtIndex(index);
                SerializedProperty id = item.FindPropertyRelative("productId");
                if (id != null && id.stringValue == productId) return item;
            }
            catalog.InsertArrayElementAtIndex(catalog.arraySize);
            return catalog.GetArrayElementAtIndex(catalog.arraySize - 1);
        }

        private static void ConfigureCatalogProduct(
            SerializedProperty entry,
            string productId,
            string category,
            string displayName,
            string description,
            int price,
            InventoryItemDefinition item)
        {
            SetString(entry, "productId", productId);
            SetString(entry, "category", category);
            SetString(entry, "displayName", displayName);
            SetString(entry, "description", description);
            SetInt(entry, "price", price);
            SerializedProperty kind = entry.FindPropertyRelative("productKind");
            if (kind != null) kind.enumValueIndex = (int)ShopProductKind.InventoryItem;
            SetObject(entry, "inventoryItem", item);
            SetInt(entry, "quantity", 1);
            SetObject(entry, "assemblyTemplate", null);
        }

        private static bool HasProduct(HangarShopTerminal terminal, string id, InventoryItemDefinition expectedItem)
        {
            if (terminal == null) return false;
            for (int index = 0; index < terminal.Catalog.Count; index++)
            {
                ShopCatalogEntry product = terminal.Catalog[index];
                if (product != null
                    && product.ProductId == id
                    && product.IsConfigured
                    && product.ProductKind == ShopProductKind.InventoryItem
                    && product.InventoryItem == expectedItem)
                {
                    return true;
                }
            }
            return false;
        }

        private static Material CreateOrRefreshMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness,
            bool emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 4f);
                material.EnableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!keepCollider)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            return part;
        }

        // Named parameter helper used only to make the muzzle opening call visually explicit.
        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material gunBlackMaterial,
            bool keepCollider,
            int unused = 0)
        {
            return CreatePrimitive(parent, primitive, name, localPosition, localScale, localEuler, gunBlackMaterial, keepCollider);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void SetString(SerializedObject obj, string name, string value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null) property.stringValue = value;
        }
        private static void SetInt(SerializedObject obj, string name, int value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null) property.intValue = value;
        }
        private static void SetBool(SerializedObject obj, string name, bool value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null) property.boolValue = value;
        }
        private static void SetColor(SerializedObject obj, string name, Color value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null) property.colorValue = value;
        }
        private static void SetVector(SerializedObject obj, string name, Vector3 value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null) property.vector3Value = value;
        }
        private static void SetObject(SerializedObject obj, string name, Object value)
        {
            SerializedProperty property = obj.FindProperty(name);
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
