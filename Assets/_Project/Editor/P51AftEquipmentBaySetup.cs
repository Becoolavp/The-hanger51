using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Commerce;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51AftEquipmentBaySetup
    {
        private const string BayRootName = "P-51 Aft Equipment Bay";
        private const string PanelName = "P-51 Aft Equipment Access Panel";
        private const string TemplateRootName = "P-51 Aft Equipment Commerce Templates";
        private const string TesterName = "Hanger 51 Handheld Battery Tester";
        private const string CutMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_Fuselage_AftEquipmentBayCut.asset";
        private const string MetalMaterialPath = "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string InteriorMaterialPath = "Assets/_Project/Aircraft/P51/Materials/CockpitInterior.mat";
        private const string ServiceMaterialPath = "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";

        private static readonly Vector3 PanelPosition = new Vector3(-0.535f, 1.49f, -3.12f);

        [MenuItem("Hanger 51/P-51 Mustang/Current/Install Aft Equipment Bay, Battery and Oxygen Rack")]
        public static void InstallAftEquipmentBay()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 aft equipment setup requires Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Open the saved Hanger 51 gameplay scene before installing the aft equipment bay.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material interior = AssetDatabase.LoadAssetAtPath<Material>(InteriorMaterialPath);
            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            if (metal == null || dark == null || interior == null || service == null)
            {
                Debug.LogError("P-51 aft equipment setup is missing one or more existing P-51 materials.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("No P-51 aircraft were found in the current scene.");
                return;
            }

            Mesh sourceFuselage = null;
            for (int i = 0; i < aircraft.Length && sourceFuselage == null; i++)
            {
                MeshFilter filter = FindFuselageFilter(aircraft[i]);
                if (filter != null && filter.sharedMesh != null)
                {
                    sourceFuselage = filter.sharedMesh;
                }
            }

            Mesh cutFuselage = CreateOrUpdateAftBayCutMesh(sourceFuselage);
            if (cutFuselage == null)
            {
                Debug.LogError("Could not create the aft-equipment access opening in the current P-51 fuselage mesh.");
                return;
            }

            int updatedAircraft = 0;
            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                MeshFilter fuselageFilter = FindFuselageFilter(flight);
                if (fuselageFilter != null)
                {
                    Undo.RecordObject(fuselageFilter, "Cut P-51 aft equipment access opening");
                    fuselageFilter.sharedMesh = cutFuselage;
                    EditorUtility.SetDirty(fuselageFilter);
                }

                BuildBayForAircraft(flight, metal, dark, interior, service);
                EnsureBatteryStartInterlock(flight);
                updatedAircraft++;
            }

            GameObject templateRoot = BuildCommerceTemplates(dark, interior, service);
            ConfigureShopCatalog(templateRoot);
            BuildHangarBatteryTester(dark, service);
            ConfigurePlayerInteractor();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Aft equipment bay changes were created, but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Aft equipment setup completed, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"P-51 aft equipment bay installed on {updatedAircraft} aircraft. The fuselage now has a real removable-panel opening, "
                + "a battery cradle, three oxygen-bottle cradles, a 24 V starter interlock, purchasable replacement battery/O2 bottles, "
                + "and a handheld hangar battery tester. Fresh P-51 clones inherit the live master bay.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Validate Aft Equipment Bay and Electrical Start")]
        public static void ValidateAftEquipmentBay()
        {
            bool passed = true;
            int checkedAircraft = 0;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                checkedAircraft++;
                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                P51BatteryStartInterlock interlock = flight.GetComponent<P51BatteryStartInterlock>();
                MeshFilter fuselage = FindFuselageFilter(flight);
                if (bay == null || bay.AccessPanel == null || interlock == null)
                {
                    Debug.LogError($"'{flight.name}' is missing the aft bay, removable panel, or battery-start interlock.", flight);
                    passed = false;
                    continue;
                }

                if (bay.InstalledBattery == null || bay.InstalledBattery.EquipmentKind != P51AftEquipmentKind.Battery)
                {
                    Debug.LogError($"'{flight.name}' does not have its initial aircraft battery installed.", flight);
                    passed = false;
                }

                int oxygenCount = 0;
                for (int slot = 1; slot <= 3; slot++)
                {
                    P51AftEquipmentItem item = bay.GetInstalledItem(slot);
                    if (item != null && item.EquipmentKind == P51AftEquipmentKind.OxygenBottle)
                    {
                        oxygenCount++;
                    }
                }
                if (oxygenCount != 3)
                {
                    Debug.LogError($"'{flight.name}' should have three removable oxygen bottles installed; found {oxygenCount}.", flight);
                    passed = false;
                }

                if (fuselage == null || fuselage.sharedMesh == null || fuselage.sharedMesh.name.IndexOf("Aft Equipment Bay Cut", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Debug.LogError($"'{flight.name}' is not using the aft-bay-cut fuselage mesh.", flight);
                    passed = false;
                }
            }

            HangarShopTerminal terminal = FindFirstIncludingInactive<HangarShopTerminal>();
            if (terminal == null || !CatalogContains(terminal, "p51-24v-battery") || !CatalogContains(terminal, "p51-oxygen-bottle"))
            {
                Debug.LogError("The hangar shop is missing the P-51 battery and/or oxygen-bottle products.");
                passed = false;
            }

            P51BatteryTester tester = FindFirstIncludingInactive<P51BatteryTester>();
            P51AftEquipmentPlayerInteractor interactor = FindFirstIncludingInactive<P51AftEquipmentPlayerInteractor>();
            if (tester == null || interactor == null)
            {
                Debug.LogError("The handheld battery tester or player aft-equipment interactor is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 aft equipment validation passed. Aircraft checked={checkedAircraft}. Battery + 3 oxygen bottles are removable, "
                    + "replacement equipment is in the shop, the tester is available in the hangar, and the starter is protected by installed-battery voltage.");
            }
        }

        private static void BuildBayForAircraft(
            P51FlightController flight,
            Material metal,
            Material dark,
            Material interior,
            Material service)
        {
            Transform old = FindDirectChild(flight.transform, BayRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            GameObject bayObject = new GameObject(BayRootName);
            Undo.RegisterCreatedObjectUndo(bayObject, "Create P-51 aft equipment bay");
            Transform bayRoot = bayObject.transform;
            bayRoot.SetParent(flight.transform, false);
            bayRoot.localPosition = Vector3.zero;
            bayRoot.localRotation = Quaternion.identity;
            bayRoot.localScale = Vector3.one;
            P51AftEquipmentBay bay = Undo.AddComponent<P51AftEquipmentBay>(bayObject);

            // Structural backplate and shelf remain behind the actual fuselage opening.
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Bay Inner Backplate",
                new Vector3(0.18f, 1.49f, -3.12f), new Vector3(0.035f, 0.70f, 1.26f), Vector3.zero, interior, false);
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Bay Lower Shelf",
                new Vector3(-0.12f, 1.08f, -3.12f), new Vector3(0.56f, 0.045f, 1.24f), Vector3.zero, dark, false);
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Bay Upper Rail",
                new Vector3(-0.12f, 1.82f, -3.12f), new Vector3(0.56f, 0.035f, 1.24f), Vector3.zero, service, false);

            // Visible opening structure gives the cut fuselage some believable thickness.
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Opening Top Rim",
                new Vector3(-0.49f, 1.84f, -3.12f), new Vector3(0.055f, 0.045f, 1.27f), Vector3.zero, dark, false);
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Opening Bottom Rim",
                new Vector3(-0.49f, 1.14f, -3.12f), new Vector3(0.055f, 0.045f, 1.27f), Vector3.zero, dark, false);
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Opening Forward Rim",
                new Vector3(-0.49f, 1.49f, -2.49f), new Vector3(0.055f, 0.70f, 0.045f), Vector3.zero, dark, false);
            CreatePrimitive(bayRoot, PrimitiveType.Cube, "Aft Opening Rear Rim",
                new Vector3(-0.49f, 1.49f, -3.75f), new Vector3(0.055f, 0.70f, 0.045f), Vector3.zero, dark, false);

            Transform panelAnchor = new GameObject("Aft Access Panel Installed Anchor").transform;
            panelAnchor.SetParent(bayRoot, false);
            panelAnchor.localPosition = PanelPosition;
            panelAnchor.localRotation = Quaternion.identity;

            GameObject panelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(panelObject, "Create removable aft access panel");
            panelObject.name = PanelName;
            panelObject.transform.SetParent(panelAnchor, false);
            panelObject.transform.localPosition = Vector3.zero;
            panelObject.transform.localRotation = Quaternion.identity;
            panelObject.transform.localScale = new Vector3(0.050f, 0.72f, 1.28f);
            ApplyMaterial(panelObject, metal);
            Rigidbody panelBody = Undo.AddComponent<Rigidbody>(panelObject);
            panelBody.mass = 8f;
            panelBody.isKinematic = true;
            panelBody.useGravity = false;
            P51AftAccessPanel panel = Undo.AddComponent<P51AftAccessPanel>(panelObject);

            // Light raised stiffeners keep the removable panel from reading as a plain flat patch.
            CreatePrimitive(panelObject.transform, PrimitiveType.Cube, "Panel Forward Stiffener",
                new Vector3(-0.53f, 0f, 0.38f), new Vector3(0.08f, 0.78f, 0.035f), Vector3.zero, dark, false);
            CreatePrimitive(panelObject.transform, PrimitiveType.Cube, "Panel Rear Stiffener",
                new Vector3(-0.53f, 0f, -0.38f), new Vector3(0.08f, 0.78f, 0.035f), Vector3.zero, dark, false);

            P51AftEquipmentSlot[] slots = new P51AftEquipmentSlot[4];
            slots[0] = CreateSlot(bayRoot, bay, "Battery Cradle", P51AftEquipmentKind.Battery, 0,
                new Vector3(-0.16f, 1.28f, -2.78f), new Vector3(0.42f, 0.34f, 0.38f));
            slots[1] = CreateSlot(bayRoot, bay, "Oxygen Bottle Cradle 1", P51AftEquipmentKind.OxygenBottle, 1,
                new Vector3(-0.13f, 1.21f, -3.39f), new Vector3(0.26f, 0.18f, 0.56f));
            slots[2] = CreateSlot(bayRoot, bay, "Oxygen Bottle Cradle 2", P51AftEquipmentKind.OxygenBottle, 2,
                new Vector3(-0.13f, 1.47f, -3.39f), new Vector3(0.26f, 0.18f, 0.56f));
            slots[3] = CreateSlot(bayRoot, bay, "Oxygen Bottle Cradle 3", P51AftEquipmentKind.OxygenBottle, 3,
                new Vector3(-0.13f, 1.72f, -3.39f), new Vector3(0.26f, 0.18f, 0.56f));

            bay.Configure(panelAnchor, panel, slots);
            panel.Configure(bay, panelAnchor, true);

            P51AftEquipmentItem battery = CreateBattery("Installed P-51 24 V Battery", dark, service, 25.2f);
            bay.InstallDirect(battery, slots[0]);
            for (int i = 1; i <= 3; i++)
            {
                P51AftEquipmentItem oxygen = CreateOxygenBottle($"Installed P-51 Oxygen Bottle {i}", interior, dark);
                bay.InstallDirect(oxygen, slots[i]);
            }

            EditorUtility.SetDirty(bay);
            EditorUtility.SetDirty(panel);
        }

        private static P51AftEquipmentSlot CreateSlot(
            Transform parent,
            P51AftEquipmentBay bay,
            string name,
            P51AftEquipmentKind kind,
            int index,
            Vector3 localPosition,
            Vector3 colliderSize)
        {
            GameObject slotObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(slotObject, "Create P-51 aft rack slot");
            slotObject.transform.SetParent(parent, false);
            slotObject.transform.localPosition = localPosition;
            slotObject.transform.localRotation = Quaternion.identity;
            BoxCollider collider = Undo.AddComponent<BoxCollider>(slotObject);
            collider.size = colliderSize;
            collider.isTrigger = true;
            P51AftEquipmentSlot slot = Undo.AddComponent<P51AftEquipmentSlot>(slotObject);
            slot.Configure(bay, kind, index);
            return slot;
        }

        private static P51AftEquipmentItem CreateBattery(
            string name,
            Material bodyMaterial,
            Material terminalMaterial,
            float voltage)
        {
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create P-51 aircraft battery");
            BoxCollider collider = Undo.AddComponent<BoxCollider>(root);
            collider.size = new Vector3(0.34f, 0.24f, 0.30f);
            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.mass = 18f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            P51AftEquipmentItem item = Undo.AddComponent<P51AftEquipmentItem>(root);

            CreatePrimitive(root.transform, PrimitiveType.Cube, "Battery Case",
                Vector3.zero, new Vector3(0.34f, 0.24f, 0.30f), Vector3.zero, bodyMaterial, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Battery Positive Terminal",
                new Vector3(-0.10f, 0.145f, 0f), new Vector3(0.035f, 0.025f, 0.035f), Vector3.zero, terminalMaterial, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Battery Negative Terminal",
                new Vector3(0.10f, 0.145f, 0f), new Vector3(0.035f, 0.025f, 0.035f), Vector3.zero, terminalMaterial, false);
            item.Configure(P51AftEquipmentKind.Battery, voltage);
            return item;
        }

        private static P51AftEquipmentItem CreateOxygenBottle(string name, Material bottleMaterial, Material dark)
        {
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create P-51 oxygen bottle");
            CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(root);
            collider.direction = 2;
            collider.radius = 0.075f;
            collider.height = 0.52f;
            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.mass = 7f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            P51AftEquipmentItem item = Undo.AddComponent<P51AftEquipmentItem>(root);

            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Oxygen Cylinder",
                Vector3.zero, new Vector3(0.075f, 0.26f, 0.075f), new Vector3(90f, 0f, 0f), bottleMaterial, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Oxygen Valve",
                new Vector3(0f, 0f, 0.31f), new Vector3(0.035f, 0.055f, 0.035f), new Vector3(90f, 0f, 0f), dark, false);
            item.Configure(P51AftEquipmentKind.OxygenBottle, 0f);
            return item;
        }

        private static GameObject BuildCommerceTemplates(Material dark, Material oxygenMaterial, Material service)
        {
            GameObject old = GameObject.Find(TemplateRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            GameObject root = new GameObject(TemplateRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create P-51 aft equipment shop templates");

            P51AftEquipmentItem battery = CreateBattery("P-51 24 V Battery Shop Template", dark, service, 25.2f);
            battery.transform.SetParent(root.transform, false);
            P51AftEquipmentItem oxygen = CreateOxygenBottle("P-51 Oxygen Bottle Shop Template", oxygenMaterial, dark);
            oxygen.transform.SetParent(root.transform, false);

            root.SetActive(false);
            return root;
        }

        private static void ConfigureShopCatalog(GameObject templateRoot)
        {
            HangarShopTerminal terminal = FindFirstIncludingInactive<HangarShopTerminal>();
            if (terminal == null || templateRoot == null)
            {
                Debug.LogWarning("Aft equipment was installed, but the hangar shop terminal/templates could not be found for catalog additions.");
                return;
            }

            P51AftEquipmentItem[] templateItems = templateRoot.GetComponentsInChildren<P51AftEquipmentItem>(true);
            GameObject batteryTemplate = null;
            GameObject oxygenTemplate = null;
            for (int i = 0; i < templateItems.Length; i++)
            {
                if (templateItems[i].EquipmentKind == P51AftEquipmentKind.Battery)
                {
                    batteryTemplate = templateItems[i].gameObject;
                }
                else if (templateItems[i].EquipmentKind == P51AftEquipmentKind.OxygenBottle)
                {
                    oxygenTemplate = templateItems[i].gameObject;
                }
            }

            List<ShopCatalogEntry> catalog = new List<ShopCatalogEntry>();
            IReadOnlyList<ShopCatalogEntry> existing = terminal.Catalog;
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] != null
                        && existing[i].ProductId != "p51-24v-battery"
                        && existing[i].ProductId != "p51-oxygen-bottle")
                    {
                        catalog.Add(existing[i]);
                    }
                }
            }

            ShopCatalogEntry batteryEntry = new ShopCatalogEntry();
            batteryEntry.Configure(
                "p51-24v-battery",
                "P-51 Electrical",
                "P-51 24 V Aircraft Battery",
                "Replacement 24 V aircraft battery for the aft equipment rack. A weak or missing battery prevents Merlin starter engagement.",
                450,
                ShopProductKind.ServiceObject,
                null,
                1,
                batteryTemplate);
            catalog.Add(batteryEntry);

            ShopCatalogEntry oxygenEntry = new ShopCatalogEntry();
            oxygenEntry.Configure(
                "p51-oxygen-bottle",
                "P-51 Oxygen",
                "P-51 Oxygen Bottle",
                "Removable high-altitude oxygen bottle for one of the three aft rack positions. Oxygen consumption will be expanded later.",
                225,
                ShopProductKind.ServiceObject,
                null,
                1,
                oxygenTemplate);
            catalog.Add(oxygenEntry);

            HangarShopUI ui = FindFirstIncludingInactive<HangarShopUI>();
            Renderer screen = terminal.GetComponentInChildren<Renderer>(true);
            terminal.Configure(terminal.Wallet, terminal.ShipmentArea, ui, screen, catalog);
            EditorUtility.SetDirty(terminal);
        }

        private static void BuildHangarBatteryTester(Material dark, Material service)
        {
            P51BatteryTester existing = FindFirstIncludingInactive<P51BatteryTester>();
            if (existing != null)
            {
                return;
            }

            HangarShopTerminal terminal = FindFirstIncludingInactive<HangarShopTerminal>();
            Vector3 position = terminal != null
                ? terminal.transform.position + terminal.transform.right * 1.05f + terminal.transform.forward * 0.45f + Vector3.up * 0.85f
                : new Vector3(1f, 1f, 1f);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(root, "Create handheld battery tester");
            root.name = TesterName;
            root.transform.position = position;
            root.transform.localScale = new Vector3(0.18f, 0.11f, 0.27f);
            ApplyMaterial(root, dark);
            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.mass = 1.2f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Undo.AddComponent<P51BatteryTester>(root);

            CreatePrimitive(root.transform, PrimitiveType.Cube, "Tester Display",
                new Vector3(0f, 0.34f, -0.50f), new Vector3(0.72f, 0.20f, 0.06f), Vector3.zero, service, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Red Test Probe",
                new Vector3(-0.62f, -0.05f, 0f), new Vector3(0.10f, 0.75f, 0.10f), new Vector3(0f, 0f, 90f), service, false);
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Black Test Probe",
                new Vector3(0.62f, -0.05f, 0f), new Vector3(0.10f, 0.75f, 0.10f), new Vector3(0f, 0f, 90f), dark, false);
        }

        private static void ConfigurePlayerInteractor()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera selected = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].CompareTag("MainCamera"))
                {
                    selected = cameras[i];
                    break;
                }
            }
            if (selected == null && cameras.Length > 0)
            {
                selected = cameras[0];
            }
            if (selected == null)
            {
                Debug.LogWarning("Could not find a player camera for aft-equipment interactions.");
                return;
            }

            P51AftEquipmentPlayerInteractor interactor = selected.GetComponent<P51AftEquipmentPlayerInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<P51AftEquipmentPlayerInteractor>(selected.gameObject);
            }
            interactor.Configure(selected);
            EditorUtility.SetDirty(interactor);
        }

        private static void EnsureBatteryStartInterlock(P51FlightController flight)
        {
            P51BatteryStartInterlock interlock = flight.GetComponent<P51BatteryStartInterlock>();
            if (interlock == null)
            {
                interlock = Undo.AddComponent<P51BatteryStartInterlock>(flight.gameObject);
            }
            EditorUtility.SetDirty(interlock);
        }

        private static Mesh CreateOrUpdateAftBayCutMesh(Mesh source)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(CutMeshPath);
            if (source == null)
            {
                return existing;
            }
            if (source == existing || source.name.IndexOf("Aft Equipment Bay Cut", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return existing != null ? existing : source;
            }

            Mesh working = Object.Instantiate(source);
            working.name = "P-51D Fuselage Aft Equipment Bay Cut";
            Vector3[] vertices = working.vertices;
            for (int subMesh = 0; subMesh < working.subMeshCount; subMesh++)
            {
                int[] triangles = working.GetTriangles(subMesh);
                List<int> kept = new List<int>(triangles.Length);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    Vector3 center = (a + b + c) / 3f;
                    bool insidePanelOpening = center.z >= -3.76f
                        && center.z <= -2.48f
                        && center.y >= 1.12f
                        && center.y <= 1.87f
                        && center.x <= -0.20f;
                    if (!insidePanelOpening)
                    {
                        kept.Add(triangles[i]);
                        kept.Add(triangles[i + 1]);
                        kept.Add(triangles[i + 2]);
                    }
                }
                working.SetTriangles(kept, subMesh, false);
            }
            working.RecalculateNormals();
            working.RecalculateTangents();
            working.RecalculateBounds();

            if (existing == null)
            {
                AssetDatabase.CreateAsset(working, CutMeshPath);
                return working;
            }

            EditorUtility.CopySerialized(working, existing);
            Object.DestroyImmediate(working);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static MeshFilter FindFuselageFilter(P51FlightController flight)
        {
            if (flight == null)
            {
                return null;
            }

            MeshFilter[] filters = flight.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter fallback = null;
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i] != null ? filters[i].sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }
                if (mesh.name.IndexOf("Fuselage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return filters[i];
                }
                Bounds bounds = mesh.bounds;
                if (fallback == null && bounds.size.z > 7f && bounds.size.x < 2.5f)
                {
                    fallback = filters[i];
                }
            }
            return fallback;
        }

        private static Transform CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool keepCollider)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            Undo.RegisterCreatedObjectUndo(obj, $"Create {name}");
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.Euler(localEuler);
            obj.transform.localScale = localScale;
            ApplyMaterial(obj, material);
            if (!keepCollider)
            {
                Collider collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }
            return obj.transform;
        }

        private static void ApplyMaterial(GameObject obj, Material material)
        {
            Renderer renderer = obj != null ? obj.GetComponent<Renderer>() : null;
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static bool CatalogContains(HangarShopTerminal terminal, string productId)
        {
            if (terminal == null || terminal.Catalog == null)
            {
                return false;
            }
            for (int i = 0; i < terminal.Catalog.Count; i++)
            {
                ShopCatalogEntry entry = terminal.Catalog[i];
                if (entry != null && entry.ProductId == productId)
                {
                    return true;
                }
            }
            return false;
        }

        private static T FindFirstIncludingInactive<T>() where T : Object
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return objects != null && objects.Length > 0 ? objects[0] : null;
        }
    }
}
