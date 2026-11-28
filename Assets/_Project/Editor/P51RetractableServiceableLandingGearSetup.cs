using System;
using Hanger51.Aircraft;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51RetractableServiceableLandingGearSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string NewGearRootName = "P-51 Serviceable Retractable Landing Gear";
        private const string NitrogenCartName = "P-51 Nitrogen Tire Service Cart";

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

        [MenuItem("Hanger 51/P-51 Mustang/28 - Add Retractable Serviceable Landing Gear")]
        public static void AddRetractableServiceableLandingGear()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 28 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || aircraft == null)
            {
                Debug.LogError("P-51 Step 28 failed. Open the saved movement-test scene with the current P-51.");
                return;
            }

            P51FlightController flight = aircraft.GetComponent<P51FlightController>();
            P51RaycastLandingGear physics = aircraft.GetComponent<P51RaycastLandingGear>();
            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            if (flight == null || physics == null || body == null || !physics.IsConfigured)
            {
                Debug.LogError("P-51 Step 28 failed. The current flight controller, Rigidbody, or raycast gear is missing/incomplete.", aircraft);
                return;
            }

            Material tire = AssetDatabase.LoadAssetAtPath<Material>(TireMaterialPath);
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            Material yellow = AssetDatabase.LoadAssetAtPath<Material>(YellowMaterialPath);
            if (tire == null || metal == null || dark == null || service == null)
            {
                Debug.LogError("P-51 Step 28 failed. One or more existing P-51 landing-gear materials are missing.", aircraft);
                return;
            }
            if (yellow == null) yellow = service;

            Transform existing = aircraft.transform.Find(NewGearRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject gearSystem = new GameObject(NewGearRootName);
            Undo.RegisterCreatedObjectUndo(gearSystem, "Create serviceable P-51 landing gear");
            gearSystem.transform.SetParent(aircraft.transform, false);

            Transform[] anchors =
            {
                physics.LeftMainAnchor,
                physics.RightMainAnchor,
                physics.TailwheelAnchor
            };
            Transform[] gearRoots = new Transform[3];
            Transform[] tireRoots = new Transform[3];
            Transform[] rimRoots = new Transform[3];
            Transform[] boltVisuals = new Transform[3];
            Transform[] valveTargets = new Transform[3];
            Transform[] physicsProxies = new Transform[3];
            Vector3[] deployedPositions = new Vector3[3];
            Vector3[] deployedEulers = new Vector3[3];
            Vector3[] retractedPositions = new Vector3[3];
            Vector3[] retractedEulers = new Vector3[3];

            for (int index = 0; index < 3; index++)
            {
                if (anchors[index] == null)
                {
                    Debug.LogError($"P-51 Step 28 failed. Wheel anchor {index} is missing.", aircraft);
                    Undo.DestroyObjectImmediate(gearSystem);
                    return;
                }

                Vector3 localAnchor = aircraft.transform.InverseTransformPoint(anchors[index].position);
                bool tail = index == 2;
                float side = index == 0 ? -1f : index == 1 ? 1f : 0f;
                BuildGearAssembly(
                    gearSystem.transform,
                    index,
                    localAnchor,
                    tail,
                    side,
                    tire,
                    metal,
                    dark,
                    service,
                    yellow,
                    out gearRoots[index],
                    out tireRoots[index],
                    out rimRoots[index],
                    out boltVisuals[index],
                    out valveTargets[index]);

                deployedPositions[index] = gearRoots[index].localPosition;
                deployedEulers[index] = gearRoots[index].localEulerAngles;
                if (tail)
                {
                    retractedPositions[index] = deployedPositions[index]
                        + new Vector3(0f, 0.48f, 0.26f);
                    retractedEulers[index] = new Vector3(-72f, 0f, 0f);
                }
                else
                {
                    retractedPositions[index] = deployedPositions[index]
                        + new Vector3(-side * 0.52f, 0.86f, 0.10f);
                    retractedEulers[index] = new Vector3(0f, 0f, side * -78f);
                }

                GameObject proxy = new GameObject($"Wheel Physics Visual Proxy {index + 1}");
                proxy.transform.SetParent(aircraft.transform, false);
                proxy.transform.position = anchors[index].position;
                proxy.transform.rotation = anchors[index].rotation;
                physicsProxies[index] = proxy.transform;
            }

            DisableOldLandingGearRenderers(aircraft.transform, gearSystem.transform);
            P51WheelLandingGear oldWheelGear = aircraft.GetComponent<P51WheelLandingGear>();
            if (oldWheelGear != null)
            {
                oldWheelGear.enabled = false;
                EditorUtility.SetDirty(oldWheelGear);
            }
            WheelCollider[] oldWheelColliders = aircraft.GetComponentsInChildren<WheelCollider>(true);
            for (int index = 0; index < oldWheelColliders.Length; index++)
            {
                if (oldWheelColliders[index] != null) oldWheelColliders[index].enabled = false;
            }

            physics.Configure(
                flight,
                body,
                anchors[0],
                anchors[1],
                anchors[2],
                physicsProxies[0],
                physicsProxies[1],
                physicsProxies[2]);

            P51LandingGearMaintenanceController maintenance =
                aircraft.GetComponent<P51LandingGearMaintenanceController>();
            if (maintenance == null)
            {
                maintenance = Undo.AddComponent<P51LandingGearMaintenanceController>(aircraft);
            }
            maintenance.Configure(
                flight,
                physics,
                body,
                gearRoots,
                tireRoots,
                rimRoots,
                boltVisuals,
                valveTargets,
                deployedPositions,
                deployedEulers,
                retractedPositions,
                retractedEulers);

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            Camera playerCamera = inventory != null ? inventory.GetComponentInChildren<Camera>() : null;
            if (inventory == null || inventoryUI == null || playerCamera == null)
            {
                Debug.LogError("P-51 Step 28 failed. Player inventory, UI, or camera is missing.", aircraft);
                return;
            }

            P51LandingGearServicePlayerInteractor interactor =
                inventory.GetComponent<P51LandingGearServicePlayerInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<P51LandingGearServicePlayerInteractor>(inventory.gameObject);
            }
            interactor.Configure(playerCamera, inventoryUI);

            P51NitrogenCartController cart = BuildNitrogenCart(
                aircraft.transform,
                metal,
                dark,
                tire,
                yellow);

            EditorUtility.SetDirty(physics);
            EditorUtility.SetDirty(maintenance);
            EditorUtility.SetDirty(interactor);
            EditorUtility.SetDirty(cart);
            EditorUtility.SetDirty(flight);
            EditorUtility.SetDirty(body);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 28 changed the aircraft but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 28 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 28 complete. Added improved retractable main/tail gear visuals, G-key retraction, one large removable mount bolt per gear assembly, removable main/tail tires with different rim sizes, persistent tire health/pressure, hard-landing damage, low-pressure damage amplification, overpressure bursts, failed-tire drag, a nitrogen service cart, and Player service interactions while preserving the existing raycast suspension and hard-stop ground protection.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/29 - Validate Retractable Serviceable Landing Gear")]
        public static void ValidateRetractableServiceableLandingGear()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 29 failed: P-51 aircraft is missing.");
                return;
            }

            P51FlightController flight = aircraft.GetComponent<P51FlightController>();
            P51RaycastLandingGear physics = aircraft.GetComponent<P51RaycastLandingGear>();
            P51LandingGearMaintenanceController maintenance =
                aircraft.GetComponent<P51LandingGearMaintenanceController>();
            P51GroundPenetrationGuard guard = aircraft.GetComponent<P51GroundPenetrationGuard>();
            Transform root = aircraft.transform.Find(NewGearRootName);

            if (flight == null || physics == null || maintenance == null || guard == null || root == null)
            {
                Debug.LogError("P-51 Step 29 failed: flight, raycast gear, service controller, ground guard, or new visual root is missing.", aircraft);
                passed = false;
            }

            P51LandingGearServiceTarget[] targets =
                aircraft.GetComponentsInChildren<P51LandingGearServiceTarget>(true);
            int mountTargets = 0;
            int tireTargets = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == null) continue;
                if (targets[index].ServiceKind == P51LandingGearServiceKind.MountBolt) mountTargets++;
                if (targets[index].ServiceKind == P51LandingGearServiceKind.TireAndValve) tireTargets++;
            }
            if (mountTargets != 3 || tireTargets != 3)
            {
                Debug.LogError($"P-51 Step 29 failed: expected 3 mount-bolt and 3 tire/valve targets; found {mountTargets} and {tireTargets}.", aircraft);
                passed = false;
            }

            P51NitrogenCartController cart = Object.FindFirstObjectByType<P51NitrogenCartController>();
            P51LandingGearServicePlayerInteractor interactor =
                Object.FindFirstObjectByType<P51LandingGearServicePlayerInteractor>();
            if (cart == null || interactor == null)
            {
                Debug.LogError("P-51 Step 29 failed: nitrogen cart or Player landing-gear interactor is missing.");
                passed = false;
            }

            Renderer[] newRenderers = root != null
                ? root.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            if (newRenderers.Length < 20)
            {
                Debug.LogError($"P-51 Step 29 failed: new landing gear has only {newRenderers.Length} renderers; expected a detailed three-wheel assembly.", aircraft);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 29 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 29 passed. Three retractable raycast-supported gear stations, three large mounting bolts, three removable tires/rims, nitrogen service, tire health/pressure failure behavior, Player interaction, and hard-stop ground protection are configured.",
                    aircraft);
            }
        }

        private static void BuildGearAssembly(
            Transform parent,
            int wheelIndex,
            Vector3 localAnchor,
            bool tail,
            float side,
            Material tireMaterial,
            Material metalMaterial,
            Material darkMaterial,
            Material serviceMaterial,
            Material yellowMaterial,
            out Transform gearRoot,
            out Transform tireRoot,
            out Transform rimRoot,
            out Transform boltVisual,
            out Transform valveTarget)
        {
            string label = tail ? "Tailwheel" : side < 0f ? "Left Main" : "Right Main";
            GameObject root = new GameObject($"{label} Serviceable Gear Visual");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localAnchor;
            root.transform.localRotation = Quaternion.identity;
            gearRoot = root.transform;

            float radius = tail ? 0.16f : 0.38f;
            float width = tail ? 0.12f : 0.22f;
            float strutHeight = tail ? 0.58f : 1.05f;

            tireRoot = new GameObject($"{label} Tire Visual").transform;
            tireRoot.SetParent(root.transform, false);
            CreatePrimitive(
                tireRoot,
                PrimitiveType.Cylinder,
                $"{label} Tire",
                Vector3.zero,
                new Vector3(radius, width * 0.50f, radius),
                new Vector3(0f, 0f, 90f),
                tireMaterial,
                false);

            rimRoot = new GameObject($"{label} Rim Visual").transform;
            rimRoot.SetParent(root.transform, false);
            CreatePrimitive(
                rimRoot,
                PrimitiveType.Cylinder,
                $"{label} Rim",
                Vector3.zero,
                new Vector3(radius * 0.60f, width * 0.58f, radius * 0.60f),
                new Vector3(0f, 0f, 90f),
                metalMaterial,
                false);
            CreatePrimitive(
                rimRoot,
                PrimitiveType.Cylinder,
                $"{label} Hub",
                Vector3.zero,
                new Vector3(radius * 0.25f, width * 0.68f, radius * 0.25f),
                new Vector3(0f, 0f, 90f),
                darkMaterial,
                false);

            CreatePrimitive(
                root.transform,
                PrimitiveType.Cylinder,
                $"{label} Oleo Strut",
                new Vector3(0f, strutHeight * 0.50f, 0f),
                new Vector3(tail ? 0.045f : 0.065f, strutHeight * 0.50f, tail ? 0.045f : 0.065f),
                Vector3.zero,
                metalMaterial,
                false);
            CreatePrimitive(
                root.transform,
                PrimitiveType.Cylinder,
                $"{label} Upper Strut Sleeve",
                new Vector3(0f, strutHeight * 0.78f, 0f),
                new Vector3(tail ? 0.065f : 0.095f, strutHeight * 0.23f, tail ? 0.065f : 0.095f),
                Vector3.zero,
                darkMaterial,
                false);

            if (!tail)
            {
                CreatePrimitive(
                    root.transform,
                    PrimitiveType.Cube,
                    $"{label} Gear Door",
                    new Vector3(side * 0.10f, 0.72f, 0.04f),
                    new Vector3(0.05f, 0.70f, 0.34f),
                    new Vector3(0f, 0f, side * 4f),
                    metalMaterial,
                    false);
                CreatePrimitive(
                    root.transform,
                    PrimitiveType.Cylinder,
                    $"{label} Scissor Link A",
                    new Vector3(side * 0.10f, 0.33f, 0f),
                    new Vector3(0.025f, 0.20f, 0.025f),
                    new Vector3(0f, 0f, side * 32f),
                    serviceMaterial,
                    false);
                CreatePrimitive(
                    root.transform,
                    PrimitiveType.Cylinder,
                    $"{label} Scissor Link B",
                    new Vector3(-side * 0.10f, 0.33f, 0f),
                    new Vector3(0.025f, 0.20f, 0.025f),
                    new Vector3(0f, 0f, side * -32f),
                    serviceMaterial,
                    false);
            }

            GameObject mountTarget = new GameObject($"{label} Large Mount Bolt Service Target");
            mountTarget.transform.SetParent(parent, false);
            mountTarget.transform.localPosition = localAnchor + new Vector3(0f, strutHeight, 0f);
            BoxCollider mountCollider = mountTarget.AddComponent<BoxCollider>();
            mountCollider.isTrigger = true;
            mountCollider.size = tail
                ? new Vector3(0.34f, 0.28f, 0.34f)
                : new Vector3(0.42f, 0.32f, 0.42f);
            P51LandingGearServiceTarget mountService =
                mountTarget.AddComponent<P51LandingGearServiceTarget>();

            GameObject bolt = CreatePrimitive(
                mountTarget.transform,
                PrimitiveType.Cylinder,
                $"{label} Large Mount Bolt",
                Vector3.zero,
                tail ? new Vector3(0.10f, 0.10f, 0.10f) : new Vector3(0.14f, 0.12f, 0.14f),
                new Vector3(0f, 0f, 90f),
                serviceMaterial,
                false);
            boltVisual = bolt.transform;

            GameObject tireTargetObject = new GameObject($"{label} Tire and Valve Service Target");
            tireTargetObject.transform.SetParent(parent, false);
            tireTargetObject.transform.localPosition = localAnchor;
            SphereCollider tireCollider = tireTargetObject.AddComponent<SphereCollider>();
            tireCollider.isTrigger = true;
            tireCollider.radius = tail ? 0.24f : 0.48f;
            P51LandingGearServiceTarget tireService =
                tireTargetObject.AddComponent<P51LandingGearServiceTarget>();
            valveTarget = tireTargetObject.transform;

            CreatePrimitive(
                tireTargetObject.transform,
                PrimitiveType.Cylinder,
                $"{label} Valve Stem",
                tail
                    ? new Vector3(0.08f, 0.07f, 0f)
                    : new Vector3(0.16f, 0.14f, 0f),
                tail
                    ? new Vector3(0.012f, 0.05f, 0.012f)
                    : new Vector3(0.016f, 0.07f, 0.016f),
                new Vector3(0f, 0f, 90f),
                yellowMaterial,
                false);

            mountService.Configure(null, P51LandingGearServiceKind.MountBolt, wheelIndex, 1.35f);
            tireService.Configure(null, P51LandingGearServiceKind.TireAndValve, wheelIndex, 1.15f);
        }

        private static P51NitrogenCartController BuildNitrogenCart(
            Transform aircraft,
            Material metal,
            Material dark,
            Material tire,
            Material yellow)
        {
            GameObject existing = GameObject.Find(NitrogenCartName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject cart = new GameObject(NitrogenCartName);
            Undo.RegisterCreatedObjectUndo(cart, "Create nitrogen tire service cart");
            Vector3 candidate = aircraft.position + aircraft.right * 4.4f + aircraft.forward * 1.0f;
            candidate.y = FindGroundY(candidate);
            cart.transform.SetPositionAndRotation(
                candidate,
                Quaternion.Euler(0f, aircraft.eulerAngles.y - 90f, 0f));

            BoxCollider rootCollider = cart.AddComponent<BoxCollider>();
            rootCollider.size = new Vector3(1.25f, 1.55f, 0.85f);
            rootCollider.center = new Vector3(0f, 0.78f, 0f);

            CreatePrimitive(cart.transform, PrimitiveType.Cube, "Cart Frame",
                new Vector3(0f, 0.45f, 0f), new Vector3(1.05f, 0.10f, 0.62f), Vector3.zero, dark, false);
            CreatePrimitive(cart.transform, PrimitiveType.Cube, "Cart Handle",
                new Vector3(-0.55f, 1.05f, 0f), new Vector3(0.08f, 1.15f, 0.62f), new Vector3(0f, 0f, -12f), dark, false);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePrimitive(cart.transform, PrimitiveType.Cylinder, $"Cart Wheel {side}",
                    new Vector3(0.33f, 0.26f, side * 0.38f), new Vector3(0.19f, 0.075f, 0.19f), new Vector3(90f, 0f, 0f), tire, false);
            }

            CreatePrimitive(cart.transform, PrimitiveType.Cylinder, "Nitrogen Bottle A",
                new Vector3(0.05f, 1.02f, -0.18f), new Vector3(0.18f, 0.66f, 0.18f), Vector3.zero, metal, false);
            CreatePrimitive(cart.transform, PrimitiveType.Cylinder, "Nitrogen Bottle B",
                new Vector3(0.05f, 1.02f, 0.18f), new Vector3(0.18f, 0.66f, 0.18f), Vector3.zero, metal, false);
            CreatePrimitive(cart.transform, PrimitiveType.Sphere, "Regulator Gauge",
                new Vector3(0.05f, 1.76f, 0f), Vector3.one * 0.16f, Vector3.zero, yellow, false);
            CreatePrimitive(cart.transform, PrimitiveType.Cylinder, "Hose Reel",
                new Vector3(0.43f, 0.88f, 0f), new Vector3(0.24f, 0.11f, 0.24f), new Vector3(90f, 0f, 0f), dark, false);

            GameObject hoseOriginObject = new GameObject("Nitrogen Hose Outlet");
            hoseOriginObject.transform.SetParent(cart.transform, false);
            hoseOriginObject.transform.localPosition = new Vector3(0.48f, 0.88f, 0.28f);

            LineRenderer hose = cart.AddComponent<LineRenderer>();
            hose.enabled = false;
            hose.useWorldSpace = true;
            hose.startWidth = 0.035f;
            hose.endWidth = 0.028f;
            hose.numCornerVertices = 4;
            hose.sharedMaterial = dark;

            P51NitrogenCartController controller = cart.AddComponent<P51NitrogenCartController>();
            controller.Configure(hoseOriginObject.transform, hose, 9f);
            return controller;
        }

        private static void DisableOldLandingGearRenderers(Transform aircraft, Transform keepRoot)
        {
            string[] oldNames =
            {
                "Left Main Landing Gear",
                "Right Main Landing Gear",
                "Tailwheel Assembly"
            };
            for (int nameIndex = 0; nameIndex < oldNames.Length; nameIndex++)
            {
                Transform oldRoot = FindDescendant(aircraft, oldNames[nameIndex]);
                if (oldRoot == null || oldRoot.IsChildOf(keepRoot)) continue;
                Renderer[] renderers = oldRoot.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index] != null) renderers[index].enabled = false;
                }
            }
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
                if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static float FindGroundY(Vector3 position)
        {
            if (Physics.Raycast(
                position + Vector3.up * 20f,
                Vector3.down,
                out RaycastHit hit,
                50f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                return hit.point.y + 0.02f;
            }
            return 0.02f;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name) return all[index];
            }
            return null;
        }
    }
}
