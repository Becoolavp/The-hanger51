using System.Collections.Generic;
using System.IO;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class EngineHoistSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string TransportRootName = "Portable Engine Assembly Root";
        private const string HoistName = "Portable Engine Hoist";
        private const string LiftPointName = "Engine Lift Point";
        private const string GroundPointName = "Engine Ground Contact Point";
        private const string LeftLugName = "Left Engine Lift Lug";
        private const string RightLugName = "Right Engine Lift Lug";

        private const string MaterialFolder = "Assets/_Project/EngineAssembly/Materials";
        private const string RedMaterialPath = MaterialFolder + "/HoistRed.mat";
        private const string BlackMaterialPath = MaterialFolder + "/HoistBlack.mat";
        private const string ChromeMaterialPath = MaterialFolder + "/HoistChrome.mat";
        private const string RubberMaterialPath = MaterialFolder + "/HoistRubber.mat";
        private const string MarkerMaterialPath = MaterialFolder + "/HoistPlacementMarker.mat";

        [MenuItem("Hanger 51/Engine Hoist/1 - Install Portable Engine Hoist")]
        public static void InstallPortableEngineHoist()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Engine Hoist Step 1 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Engine Hoist Step 1 failed. The V-1650 engine stand is missing. Run the current Merlin setup first.");
                return;
            }

            EnsureFolder(MaterialFolder);
            Material red = CreateMaterial(
                RedMaterialPath,
                new Color(0.67f, 0.035f, 0.025f, 1f),
                0.48f,
                0.55f);
            Material black = CreateMaterial(
                BlackMaterialPath,
                new Color(0.025f, 0.028f, 0.032f, 1f),
                0.72f,
                0.52f);
            Material chrome = CreateMaterial(
                ChromeMaterialPath,
                new Color(0.72f, 0.74f, 0.78f, 1f),
                0.94f,
                0.82f);
            Material rubber = CreateMaterial(
                RubberMaterialPath,
                new Color(0.018f, 0.018f, 0.021f, 1f),
                0.02f,
                0.18f);
            Material marker = CreateMarkerMaterial(MarkerMaterialPath);

            Transform transportRoot = GetOrCreateTransportRoot(station.transform);
            ReparentPortableAssembly(station, transportRoot);

            Bounds engineBounds = CalculatePhysicalBounds(
                transportRoot.gameObject,
                transportRoot);
            if (engineBounds.size.sqrMagnitude < 0.01f)
            {
                Debug.LogError(
                    "Engine Hoist Step 1 failed. The portable engine geometry could not be measured.",
                    station);
                return;
            }

            Transform liftPoint = GetOrCreateMarkerTransform(
                transportRoot,
                LiftPointName,
                new Vector3(
                    engineBounds.center.x,
                    engineBounds.max.y + 0.12f,
                    engineBounds.center.z));
            Transform groundPoint = GetOrCreateMarkerTransform(
                transportRoot,
                GroundPointName,
                new Vector3(
                    engineBounds.center.x,
                    engineBounds.min.y,
                    engineBounds.center.z));

            float lugOffset = Mathf.Clamp(engineBounds.size.x * 0.22f, 0.18f, 0.32f);
            Transform leftLug = GetOrCreateMarkerTransform(
                transportRoot,
                LeftLugName,
                new Vector3(
                    engineBounds.center.x - lugOffset,
                    engineBounds.max.y - 0.02f,
                    engineBounds.center.z));
            Transform rightLug = GetOrCreateMarkerTransform(
                transportRoot,
                RightLugName,
                new Vector3(
                    engineBounds.center.x + lugOffset,
                    engineBounds.max.y - 0.02f,
                    engineBounds.center.z));

            Collider stationCollider = station.GetComponent<Collider>();
            EngineAssemblyTransportController transportController =
                station.GetComponent<EngineAssemblyTransportController>();
            if (transportController == null)
            {
                transportController = Undo.AddComponent<EngineAssemblyTransportController>(station.gameObject);
            }

            transportController.Configure(
                transportRoot,
                liftPoint,
                groundPoint,
                leftLug,
                rightLug,
                stationCollider,
                transportRoot.localPosition,
                transportRoot.localRotation,
                transportRoot.localScale);
            EditorUtility.SetDirty(transportController);

            GameObject hoist = CreateHoistModel(
                station,
                transportController,
                red,
                black,
                chrome,
                rubber,
                marker);
            InstallPlayerInteractor();

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Engine Hoist Step 1 created the hoist but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Engine Hoist Step 1 created the hoist, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = hoist;
            Debug.Log(
                "Engine Hoist Step 1 complete. Created a movable hydraulic shop crane, portable engine assembly root, "
                + "hook and sling system, floor placement marker, player controls, and prepared Build and Run.",
                hoist);
        }

        [MenuItem("Hanger 51/Engine Hoist/2 - Validate Portable Engine Hoist")]
        public static void ValidatePortableEngineHoist()
        {
            bool passed = true;
            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError("Engine Hoist Step 2 failed: the V-1650 engine stand is missing.");
                return;
            }

            EngineAssemblyTransportController transport =
                station.GetComponent<EngineAssemblyTransportController>();
            if (transport == null
                || transport.TransportRoot == null
                || transport.LiftPoint == null
                || transport.GroundContactPoint == null
                || transport.LeftLiftLug == null
                || transport.RightLiftLug == null)
            {
                Debug.LogError(
                    "Engine Hoist Step 2 failed: the portable engine transport references are incomplete.",
                    station);
                passed = false;
            }

            GameObject hoist = GameObject.Find(HoistName);
            EngineHoistController hoistController = hoist != null
                ? hoist.GetComponent<EngineHoistController>()
                : null;
            if (hoist == null || hoistController == null || hoistController.HookPoint == null)
            {
                Debug.LogError("Engine Hoist Step 2 failed: the portable engine hoist or hook is missing.");
                passed = false;
            }
            else
            {
                Bounds hoistBounds = CalculateRendererBounds(hoist);
                if (hoistBounds.size.y < 1.8f || hoistBounds.size.y > 3.0f
                    || hoistBounds.size.z < 1.8f || hoistBounds.size.z > 3.4f)
                {
                    Debug.LogError(
                        $"Engine Hoist Step 2 failed: hoist dimensions are {hoistBounds.size.x:F2} × "
                        + $"{hoistBounds.size.y:F2} × {hoistBounds.size.z:F2} m, outside the expected shop-crane range.",
                        hoist);
                    passed = false;
                }
            }

            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            EngineHoistPlayerInteractor playerHoistInteractor = inventoryInteractor != null
                ? inventoryInteractor.GetComponent<EngineHoistPlayerInteractor>()
                : null;
            if (playerHoistInteractor == null)
            {
                Debug.LogError("Engine Hoist Step 2 failed: the Player hoist interactor is missing.");
                passed = false;
            }

            if (transport != null && transport.TransportRoot != null)
            {
                Transform engineCore = FindDescendant(
                    transport.TransportRoot,
                    "Installed Engine Core");
                Transform leftCover = FindDescendant(
                    transport.TransportRoot,
                    "Installed Left Cylinder Cover");
                Transform rightCover = FindDescendant(
                    transport.TransportRoot,
                    "Installed Right Cylinder Cover");
                int targetCount = transport.TransportRoot
                    .GetComponentsInChildren<EngineAssemblyInteractionTarget>(true).Length;

                if (engineCore == null || leftCover == null || rightCover == null || targetCount != 38)
                {
                    Debug.LogError(
                        $"Engine Hoist Step 2 failed: the portable root must contain the engine, both covers, and 38 interaction targets; found {targetCount} targets.",
                        transport.TransportRoot);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Engine Hoist Step 2 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Engine Hoist Step 2 passed. The real-scale hydraulic hoist, hook, lifting slings, portable engine root, "
                    + "38 preserved maintenance targets, player controls, and Build and Run setup are ready.",
                    hoist);
            }
        }

        private static Transform GetOrCreateTransportRoot(Transform station)
        {
            Transform root = station.Find(TransportRootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(TransportRootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create portable engine assembly root");
                root = rootObject.transform;
                root.SetParent(station, false);
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        private static void ReparentPortableAssembly(
            EngineAssemblyStation station,
            Transform transportRoot)
        {
            string[] namedRoots =
            {
                "Installed Engine Core",
                "Installed Left Cylinder Cover",
                "Installed Right Cylinder Cover"
            };

            for (int index = 0; index < namedRoots.Length; index++)
            {
                Transform target = FindDescendant(station.transform, namedRoots[index]);
                ReparentIfNeeded(target, transportRoot);
            }

            Transform[] allTransforms = station.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < allTransforms.Length; index++)
            {
                Transform candidate = allTransforms[index];
                if (candidate == null || candidate == transportRoot)
                {
                    continue;
                }

                if (candidate.name.StartsWith("Installed ")
                    && candidate.name.Contains("Spark Plug"))
                {
                    ReparentIfNeeded(candidate, transportRoot);
                }
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                Transform targetTransform = targets[index].transform;
                if (!targetTransform.IsChildOf(transportRoot))
                {
                    ReparentIfNeeded(targetTransform, transportRoot);
                }
            }
        }

        private static void ReparentIfNeeded(Transform target, Transform parent)
        {
            if (target == null || target == parent || target.IsChildOf(parent))
            {
                return;
            }

            Undo.SetTransformParent(target, parent, "Move engine component into portable root");
            EditorUtility.SetDirty(target);
        }

        private static Transform GetOrCreateMarkerTransform(
            Transform parent,
            string objectName,
            Vector3 localPosition)
        {
            Transform marker = parent.Find(objectName);
            if (marker == null)
            {
                GameObject markerObject = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(markerObject, $"Create {objectName}");
                marker = markerObject.transform;
                marker.SetParent(parent, false);
            }

            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one;
            return marker;
        }

        private static GameObject CreateHoistModel(
            EngineAssemblyStation station,
            EngineAssemblyTransportController transport,
            Material red,
            Material black,
            Material chrome,
            Material rubber,
            Material markerMaterial)
        {
            GameObject existing = GameObject.Find(HoistName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject hoist = new GameObject(HoistName);
            Undo.RegisterCreatedObjectUndo(hoist, "Create portable engine hoist");
            hoist.transform.SetParent(station.transform.parent, true);
            hoist.transform.position = station.transform.TransformPoint(new Vector3(2.45f, 0f, 0.10f));
            hoist.transform.rotation = station.transform.rotation * Quaternion.Euler(0f, -90f, 0f);

            CreatePart(hoist.transform, PrimitiveType.Cube, "Left Folding Base Leg",
                new Vector3(-0.48f, 0.10f, 0.72f),
                new Vector3(0.16f, 0.18f, 2.35f),
                Vector3.zero,
                black);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Right Folding Base Leg",
                new Vector3(0.48f, 0.10f, 0.72f),
                new Vector3(0.16f, 0.18f, 2.35f),
                Vector3.zero,
                black);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Rear Base Crossmember",
                new Vector3(0f, 0.13f, -0.44f),
                new Vector3(1.20f, 0.22f, 0.22f),
                Vector3.zero,
                black);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Mast Base Plate",
                new Vector3(0f, 0.25f, -0.35f),
                new Vector3(0.58f, 0.10f, 0.52f),
                Vector3.zero,
                red);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Upright Mast",
                new Vector3(0f, 1.12f, -0.35f),
                new Vector3(0.24f, 1.95f, 0.24f),
                new Vector3(5f, 0f, 0f),
                red);

            CreatePart(hoist.transform, PrimitiveType.Cube, "Main Boom",
                new Vector3(0f, 2.08f, 0.37f),
                new Vector3(0.26f, 0.23f, 1.62f),
                Vector3.zero,
                red);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Telescoping Boom Extension",
                new Vector3(0f, 2.08f, 1.38f),
                new Vector3(0.21f, 0.18f, 0.88f),
                Vector3.zero,
                black);

            Transform boomTip = GetOrCreateMarkerTransform(
                hoist.transform,
                "Boom Tip",
                new Vector3(0f, 2.08f, 1.82f));
            Transform hookPoint = GetOrCreateMarkerTransform(
                hoist.transform,
                "Load Hook Point",
                new Vector3(0f, 1.69f, 1.82f));

            CreatePart(hoist.transform, PrimitiveType.Cube, "Left Upper Brace",
                new Vector3(-0.16f, 2.28f, -0.08f),
                new Vector3(0.075f, 0.075f, 0.95f),
                new Vector3(-18f, 0f, 0f),
                red);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Right Upper Brace",
                new Vector3(0.16f, 2.28f, -0.08f),
                new Vector3(0.075f, 0.075f, 0.95f),
                new Vector3(-18f, 0f, 0f),
                red);

            CreateCylinderBetween(
                hoist.transform,
                "Hydraulic Ram Body",
                new Vector3(0f, 0.58f, -0.28f),
                new Vector3(0f, 1.85f, 0.12f),
                0.085f,
                black);
            CreateCylinderBetween(
                hoist.transform,
                "Hydraulic Chrome Rod",
                new Vector3(0f, 1.45f, -0.02f),
                new Vector3(0f, 1.94f, 0.20f),
                0.042f,
                chrome);
            CreatePart(hoist.transform, PrimitiveType.Cylinder, "Hydraulic Pump Body",
                new Vector3(0f, 0.48f, -0.26f),
                new Vector3(0.13f, 0.26f, 0.13f),
                Vector3.zero,
                black);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Pump Handle",
                new Vector3(-0.34f, 0.92f, -0.46f),
                new Vector3(0.07f, 0.07f, 1.05f),
                new Vector3(48f, 0f, -18f),
                red);
            CreatePart(hoist.transform, PrimitiveType.Cylinder, "Pump Handle Grip",
                new Vector3(-0.65f, 1.24f, -0.78f),
                new Vector3(0.08f, 0.20f, 0.08f),
                new Vector3(90f, 0f, 0f),
                black);

            CreatePart(hoist.transform, PrimitiveType.Cube, "Left Push Handle",
                new Vector3(-0.32f, 1.15f, -0.67f),
                new Vector3(0.08f, 0.08f, 0.62f),
                new Vector3(22f, 0f, 0f),
                red);
            CreatePart(hoist.transform, PrimitiveType.Cube, "Right Push Handle",
                new Vector3(0.32f, 1.15f, -0.67f),
                new Vector3(0.08f, 0.08f, 0.62f),
                new Vector3(22f, 0f, 0f),
                red);

            int wheelNumber = 1;
            Vector3[] wheelPositions =
            {
                new Vector3(-0.52f, 0.08f, -0.48f),
                new Vector3(0.52f, 0.08f, -0.48f),
                new Vector3(-0.52f, 0.08f, 0.70f),
                new Vector3(0.52f, 0.08f, 0.70f),
                new Vector3(-0.52f, 0.08f, 1.82f),
                new Vector3(0.52f, 0.08f, 1.82f)
            };

            for (int index = 0; index < wheelPositions.Length; index++)
            {
                CreateCaster(
                    hoist.transform,
                    $"Caster {wheelNumber++}",
                    wheelPositions[index],
                    chrome,
                    rubber);
            }

            Transform loadChain = CreatePart(
                hoist.transform,
                PrimitiveType.Cylinder,
                "Load Chain",
                Vector3.zero,
                new Vector3(0.014f, 0.20f, 0.014f),
                Vector3.zero,
                black).transform;
            Transform leftSling = CreatePart(
                hoist.transform,
                PrimitiveType.Cylinder,
                "Left Engine Sling",
                Vector3.zero,
                new Vector3(0.012f, 0.30f, 0.012f),
                Vector3.zero,
                chrome).transform;
            Transform rightSling = CreatePart(
                hoist.transform,
                PrimitiveType.Cylinder,
                "Right Engine Sling",
                Vector3.zero,
                new Vector3(0.012f, 0.30f, 0.012f),
                Vector3.zero,
                chrome).transform;
            leftSling.gameObject.SetActive(false);
            rightSling.gameObject.SetActive(false);

            CreateHookVisual(hookPoint, chrome, black);

            GameObject handleTarget = new GameObject("Hoist Interaction Handles");
            handleTarget.transform.SetParent(hoist.transform, false);
            handleTarget.transform.localPosition = new Vector3(0f, 1.12f, -0.67f);
            BoxCollider handleCollider = handleTarget.AddComponent<BoxCollider>();
            handleCollider.center = Vector3.zero;
            handleCollider.size = new Vector3(0.90f, 0.90f, 0.55f);

            GameObject placementMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placementMarker.name = "Engine Placement Marker";
            placementMarker.transform.SetParent(hoist.transform.parent, true);
            placementMarker.transform.localScale = new Vector3(0.62f, 0.008f, 0.62f);
            placementMarker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            Collider markerCollider = placementMarker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Object.DestroyImmediate(markerCollider);
            }
            placementMarker.SetActive(false);

            EngineHoistController controller = Undo.AddComponent<EngineHoistController>(hoist);
            controller.Configure(
                hookPoint,
                boomTip,
                loadChain,
                leftSling,
                rightSling,
                placementMarker,
                handleCollider,
                transport);
            EditorUtility.SetDirty(controller);

            return hoist;
        }

        private static void CreateCaster(
            Transform parent,
            string objectName,
            Vector3 position,
            Material chrome,
            Material rubber)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            CreatePart(root.transform, PrimitiveType.Cube, "Caster Fork",
                new Vector3(0f, 0.08f, 0f),
                new Vector3(0.12f, 0.20f, 0.08f),
                Vector3.zero,
                chrome);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Caster Wheel",
                new Vector3(0f, -0.02f, 0f),
                new Vector3(0.12f, 0.055f, 0.12f),
                new Vector3(0f, 0f, 90f),
                rubber);
        }

        private static void CreateHookVisual(
            Transform hookPoint,
            Material chrome,
            Material black)
        {
            CreatePart(hookPoint, PrimitiveType.Sphere, "Hook Swivel",
                Vector3.zero,
                Vector3.one * 0.09f,
                Vector3.zero,
                chrome);
            CreatePart(hookPoint, PrimitiveType.Cylinder, "Hook Shank",
                new Vector3(0f, -0.10f, 0f),
                new Vector3(0.035f, 0.10f, 0.035f),
                Vector3.zero,
                chrome);
            CreatePart(hookPoint, PrimitiveType.Cylinder, "Hook Curve Lower",
                new Vector3(0.045f, -0.21f, 0f),
                new Vector3(0.035f, 0.10f, 0.035f),
                new Vector3(0f, 0f, -42f),
                chrome);
            CreatePart(hookPoint, PrimitiveType.Cylinder, "Hook Curve Tip",
                new Vector3(0.115f, -0.25f, 0f),
                new Vector3(0.030f, 0.075f, 0.030f),
                new Vector3(0f, 0f, 62f),
                chrome);
            CreatePart(hookPoint, PrimitiveType.Cube, "Hook Safety Latch",
                new Vector3(0.075f, -0.16f, 0f),
                new Vector3(0.018f, 0.09f, 0.035f),
                new Vector3(0f, 0f, 28f),
                black);
        }

        private static void InstallPlayerInteractor()
        {
            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            if (inventoryInteractor == null)
            {
                Debug.LogWarning("Engine hoist setup could not find the Player InventoryInteractor.");
                return;
            }

            EngineHoistPlayerInteractor hoistInteractor =
                inventoryInteractor.GetComponent<EngineHoistPlayerInteractor>();
            if (hoistInteractor == null)
            {
                hoistInteractor = Undo.AddComponent<EngineHoistPlayerInteractor>(inventoryInteractor.gameObject);
            }

            Camera playerCamera = inventoryInteractor.GetComponentInChildren<Camera>();
            InventoryUI inventoryUi = Object.FindFirstObjectByType<InventoryUI>();
            hoistInteractor.Configure(playerCamera, inventoryUi);
            EditorUtility.SetDirty(hoistInteractor);
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
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

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return part;
        }

        private static GameObject CreateCylinderBetween(
            Transform parent,
            string objectName,
            Vector3 startLocal,
            Vector3 endLocal,
            float radius,
            Material material)
        {
            Vector3 direction = endLocal - startLocal;
            float length = direction.magnitude;
            GameObject cylinder = CreatePart(
                parent,
                PrimitiveType.Cylinder,
                objectName,
                (startLocal + endLocal) * 0.5f,
                new Vector3(radius, length * 0.5f, radius),
                Vector3.zero,
                material);
            cylinder.transform.localRotation =
                Quaternion.FromToRotation(Vector3.up, direction.normalized);
            return cylinder;
        }

        private static Material CreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
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
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateMarkerMaterial(string path)
        {
            Material material = CreateMaterial(
                path,
                new Color(1f, 0.72f, 0.05f, 0.70f),
                0.05f,
                0.30f);

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(1.8f, 0.75f, 0.05f, 1f));
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds CalculatePhysicalBounds(
            GameObject root,
            Transform reference)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool hasPoint = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (filter == null
                    || filter.sharedMesh == null
                    || IsInteractionVisual(filter.transform, root.transform))
                {
                    continue;
                }

                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 point = reference.InverseTransformPoint(
                        filter.transform.TransformPoint(localCorner));

                    if (!hasPoint)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return hasPoint
                ? bounds
                : new Bounds(Vector3.zero, Vector3.zero);
        }

        private static bool IsInteractionVisual(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null && current != root)
            {
                if (current.GetComponent<EngineAssemblyInteractionTarget>() != null)
                {
                    return true;
                }
                current = current.parent;
            }

            string objectName = candidate.name;
            return objectName.Contains("Highlight")
                || objectName.Contains("Placement Beacon")
                || objectName.Contains("Beacon Stem");
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }
            return null;
        }

        private static EngineAssemblyStation FindStation()
        {
            EngineAssemblyStation[] stations = Object.FindObjectsByType<EngineAssemblyStation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < stations.Length; index++)
            {
                if (stations[index] != null && stations[index].name == StationName)
                {
                    return stations[index];
                }
            }
            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string name = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
