using System.IO;
using Hanger51.Aircraft;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51TailwheelTowBarSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string TowBarRootName = "P-51 Tailwheel Tow Bar";
        private const string MaterialFolder =
            "Assets/_Project/Aircraft/P51/Materials/TowBar";
        private const string YellowMaterialPath = MaterialFolder + "/TowBarSafetyYellow.mat";
        private const string BlackMaterialPath = MaterialFolder + "/TowBarRubberBlack.mat";
        private const string SteelMaterialPath = MaterialFolder + "/TowBarHardwareSteel.mat";
        private const string DarkSteelMaterialPath = MaterialFolder + "/TowBarDarkSteel.mat";
        private const string WarningMaterialPath = MaterialFolder + "/TowBarWarningRed.mat";

        [MenuItem("Hanger 51/P-51 Mustang/14 - Add Tailwheel Tow Bar")]
        public static void AddTailwheelTowBar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 14 failed. Exit Play mode before adding the tow bar.");
                return;
            }

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 14 failed. The current P-51 aircraft is missing.");
                return;
            }

            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            P51RaycastLandingGear landingGear =
                aircraft.GetComponent<P51RaycastLandingGear>();
            Rigidbody aircraftBody = aircraft.GetComponent<Rigidbody>();
            if (flightController == null
                || landingGear == null
                || aircraftBody == null
                || landingGear.TailwheelAnchor == null)
            {
                Debug.LogError(
                    "P-51 Step 14 failed. Run P-51 Step 12 first so the current flight controller, raycast landing gear, Rigidbody, and tailwheel anchor exist.",
                    aircraft);
                return;
            }

            GameObject existingTowBar = GameObject.Find(TowBarRootName);
            if (existingTowBar != null)
            {
                Undo.DestroyObjectImmediate(existingTowBar);
            }

            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Aircraft");
            EnsureFolder("Assets/_Project/Aircraft/P51");
            EnsureFolder("Assets/_Project/Aircraft/P51/Materials");
            EnsureFolder(MaterialFolder);

            Material yellow = CreateMaterial(
                YellowMaterialPath,
                new Color(0.92f, 0.60f, 0.035f, 1f),
                0.35f,
                0.42f);
            Material black = CreateMaterial(
                BlackMaterialPath,
                new Color(0.025f, 0.028f, 0.030f, 1f),
                0.02f,
                0.20f);
            Material steel = CreateMaterial(
                SteelMaterialPath,
                new Color(0.43f, 0.46f, 0.48f, 1f),
                0.82f,
                0.62f);
            Material darkSteel = CreateMaterial(
                DarkSteelMaterialPath,
                new Color(0.10f, 0.11f, 0.12f, 1f),
                0.62f,
                0.40f);
            Material warning = CreateMaterial(
                WarningMaterialPath,
                new Color(0.62f, 0.045f, 0.025f, 1f),
                0.18f,
                0.34f);

            GameObject towBar = new GameObject(TowBarRootName);
            Undo.RegisterCreatedObjectUndo(towBar, "Create P-51 tailwheel tow bar");

            Vector3 aircraftForward = Vector3.ProjectOnPlane(
                aircraft.transform.forward,
                Vector3.up).normalized;
            Vector3 aircraftRight = Vector3.ProjectOnPlane(
                aircraft.transform.right,
                Vector3.up).normalized;
            if (aircraftForward.sqrMagnitude < 0.01f)
            {
                aircraftForward = Vector3.forward;
            }
            if (aircraftRight.sqrMagnitude < 0.01f)
            {
                aircraftRight = Vector3.right;
            }

            Vector3 initialPosition = landingGear.TailwheelAnchor.position
                - aircraftRight * 1.75f
                - aircraftForward * 0.55f;
            initialPosition.y = FindGroundHeight(initialPosition, aircraft.transform)
                + 0.29f;
            towBar.transform.SetPositionAndRotation(
                initialPosition,
                Quaternion.LookRotation(aircraftForward, Vector3.up));

            Transform visualRoot = new GameObject("Tow Bar Mechanical Assembly").transform;
            Undo.RegisterCreatedObjectUndo(visualRoot.gameObject, "Create tow bar visuals");
            visualRoot.SetParent(towBar.transform, false);

            Transform towHead = CreateMarker(
                towBar.transform,
                "Tailwheel Tow Head",
                Vector3.zero);
            Transform handleGrip = CreateMarker(
                towBar.transform,
                "Tow Bar Handle Grip Point",
                new Vector3(0f, 0.50f, -2.78f));

            CreatePart(
                visualRoot,
                PrimitiveType.Cube,
                "Tailwheel Yoke Cross Tube",
                new Vector3(0f, 0f, -0.10f),
                new Vector3(0.72f, 0.10f, 0.18f),
                Vector3.zero,
                yellow);
            CreatePart(
                visualRoot,
                PrimitiveType.Cube,
                "Yoke Center Reinforcement",
                new Vector3(0f, 0.02f, -0.21f),
                new Vector3(0.28f, 0.16f, 0.30f),
                Vector3.zero,
                yellow);

            Transform leftJaw = CreateClampJaw(
                visualRoot,
                "Left Padded Tailwheel Jaw",
                -0.25f,
                yellow,
                black,
                steel,
                true);
            Transform rightJaw = CreateClampJaw(
                visualRoot,
                "Right Padded Tailwheel Jaw",
                0.25f,
                yellow,
                black,
                steel,
                false);

            CreateSquareBeamBetween(
                visualRoot,
                "Left A-Frame Drawbar",
                new Vector3(-0.29f, 0.02f, -0.16f),
                new Vector3(-0.09f, 0.28f, -1.55f),
                0.09f,
                yellow);
            CreateSquareBeamBetween(
                visualRoot,
                "Right A-Frame Drawbar",
                new Vector3(0.29f, 0.02f, -0.16f),
                new Vector3(0.09f, 0.28f, -1.55f),
                0.09f,
                yellow);
            CreatePart(
                visualRoot,
                PrimitiveType.Cube,
                "A-Frame Cross Brace",
                new Vector3(0f, 0.13f, -0.73f),
                new Vector3(0.61f, 0.075f, 0.10f),
                Vector3.zero,
                yellow);

            CreateSquareBeamBetween(
                visualRoot,
                "Outer Adjustable Draw Tube",
                new Vector3(0f, 0.28f, -1.43f),
                new Vector3(0f, 0.39f, -2.23f),
                0.14f,
                yellow);
            CreateSquareBeamBetween(
                visualRoot,
                "Inner Telescoping Handle Tube",
                new Vector3(0f, 0.39f, -2.12f),
                new Vector3(0f, 0.50f, -2.76f),
                0.10f,
                darkSteel);

            CreateCylinderBetween(
                visualRoot,
                "Telescoping Lock Pin",
                new Vector3(-0.17f, 0.37f, -1.91f),
                new Vector3(0.17f, 0.37f, -1.91f),
                0.035f,
                steel);
            CreatePart(
                visualRoot,
                PrimitiveType.Sphere,
                "Lock Pin Retaining Knob",
                new Vector3(0.21f, 0.37f, -1.91f),
                Vector3.one * 0.085f,
                Vector3.zero,
                black);

            CreateCylinderBetween(
                visualRoot,
                "Tow Bar T-Handle",
                new Vector3(-0.58f, 0.50f, -2.78f),
                new Vector3(0.58f, 0.50f, -2.78f),
                0.047f,
                yellow);
            CreateCylinderBetween(
                visualRoot,
                "Left Rubber Handle Grip",
                new Vector3(-0.58f, 0.50f, -2.78f),
                new Vector3(-0.37f, 0.50f, -2.78f),
                0.065f,
                black);
            CreateCylinderBetween(
                visualRoot,
                "Right Rubber Handle Grip",
                new Vector3(0.37f, 0.50f, -2.78f),
                new Vector3(0.58f, 0.50f, -2.78f),
                0.065f,
                black);

            CreateSquareBeamBetween(
                visualRoot,
                "Tailwheel Clamp Release Lever",
                new Vector3(0.11f, 0.47f, -2.48f),
                new Vector3(0.32f, 0.67f, -2.62f),
                0.042f,
                steel);
            CreatePart(
                visualRoot,
                PrimitiveType.Sphere,
                "Clamp Release Lever Knob",
                new Vector3(0.34f, 0.69f, -2.64f),
                Vector3.one * 0.095f,
                Vector3.zero,
                black);

            Transform leftTransportWheel = CreateTransportWheel(
                visualRoot,
                "Left Tow Bar Transport Wheel",
                new Vector3(-0.36f, -0.16f, -0.96f),
                black,
                steel,
                yellow);
            Transform rightTransportWheel = CreateTransportWheel(
                visualRoot,
                "Right Tow Bar Transport Wheel",
                new Vector3(0.36f, -0.16f, -0.96f),
                black,
                steel,
                yellow);

            CreatePart(
                visualRoot,
                PrimitiveType.Cube,
                "Tow Bar Warning Plate",
                new Vector3(0f, 0.31f, -1.12f),
                new Vector3(0.34f, 0.16f, 0.018f),
                new Vector3(8f, 0f, 0f),
                warning);
            for (int index = 0; index < 4; index++)
            {
                float x = index < 2 ? -0.145f : 0.145f;
                float y = index % 2 == 0 ? 0.255f : 0.365f;
                CreatePart(
                    visualRoot,
                    PrimitiveType.Sphere,
                    $"Warning Plate Fastener {index + 1}",
                    new Vector3(x, y, -1.137f),
                    Vector3.one * 0.028f,
                    Vector3.zero,
                    steel);
            }

            BuildSafetyChain(visualRoot, darkSteel);

            GameObject interactionObject = new GameObject("Tow Bar Handle Interaction");
            Undo.RegisterCreatedObjectUndo(interactionObject, "Create tow bar interaction handle");
            interactionObject.transform.SetParent(towBar.transform, false);
            interactionObject.transform.localPosition = new Vector3(0f, 0.50f, -2.48f);
            interactionObject.transform.localRotation = Quaternion.identity;
            BoxCollider interactionCollider = interactionObject.AddComponent<BoxCollider>();
            interactionCollider.center = Vector3.zero;
            interactionCollider.size = new Vector3(1.35f, 0.62f, 0.82f);
            interactionCollider.isTrigger = false;

            P51TowBarController controller = towBar.AddComponent<P51TowBarController>();
            controller.Configure(
                flightController,
                landingGear,
                aircraftBody,
                landingGear.TailwheelAnchor,
                towHead,
                handleGrip,
                interactionCollider,
                leftJaw,
                rightJaw,
                leftTransportWheel,
                rightTransportWheel);

            InstallPlayerInteractor();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(towBar);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(scene.path)
                || !EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 14 created the tow bar but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 14 created the tow bar, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = towBar;
            Debug.Log(
                "P-51 Step 14 complete. Added a detailed portable tailwheel tow bar with padded axle jaws, animated clamp movement, adjustable A-frame drawbar, locking hardware, release lever, T-handle, transport wheels, Player controls, aircraft towing, and cockpit safety interlocks. The P-51 model and flight physics were not rebuilt.",
                towBar);
        }

        [MenuItem("Hanger 51/P-51 Mustang/15 - Validate Tailwheel Tow Bar")]
        public static void ValidateTailwheelTowBar()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            GameObject towBar = GameObject.Find(TowBarRootName);
            if (aircraft == null || towBar == null)
            {
                Debug.LogError("P-51 Step 15 failed: the aircraft or tailwheel tow bar is missing.");
                return;
            }

            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            P51RaycastLandingGear landingGear =
                aircraft.GetComponent<P51RaycastLandingGear>();
            P51TowBarController controller =
                towBar.GetComponent<P51TowBarController>();
            if (flightController == null
                || landingGear == null
                || landingGear.TailwheelAnchor == null
                || controller == null
                || !controller.IsConfigured
                || controller.FlightController != flightController)
            {
                Debug.LogError(
                    "P-51 Step 15 failed: the tow bar is not connected to the current P-51 flight controller and tailwheel anchor.",
                    towBar);
                passed = false;
            }

            Renderer[] renderers = towBar.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length < 28)
            {
                Debug.LogError(
                    $"P-51 Step 15 failed: expected at least 28 tow-bar detail renderers, found {renderers.Length}.",
                    towBar);
                passed = false;
            }

            if (towBar.transform.Find("Tow Bar Mechanical Assembly/Left Padded Tailwheel Jaw") == null
                || towBar.transform.Find("Tow Bar Mechanical Assembly/Right Padded Tailwheel Jaw") == null
                || towBar.transform.Find("Tow Bar Mechanical Assembly/Left Tow Bar Transport Wheel") == null
                || towBar.transform.Find("Tow Bar Mechanical Assembly/Right Tow Bar Transport Wheel") == null
                || towBar.transform.Find("Tow Bar Handle Interaction") == null)
            {
                Debug.LogError(
                    "P-51 Step 15 failed: the padded jaws, transport wheels, or handle interaction are incomplete.",
                    towBar);
                passed = false;
            }

            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            P51TowBarPlayerInteractor playerInteractor = inventoryInteractor != null
                ? inventoryInteractor.GetComponent<P51TowBarPlayerInteractor>()
                : null;
            if (playerInteractor == null)
            {
                Debug.LogError("P-51 Step 15 failed: the Player tow-bar interactor is missing.");
                passed = false;
            }

            if (towBar.GetComponent<Rigidbody>() != null)
            {
                Debug.LogError(
                    "P-51 Step 15 failed: the hand tow bar should not have an independent Rigidbody.",
                    towBar);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 15 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 15 passed. The detailed tailwheel tow bar, padded clamp jaws, transport wheels, Player interaction, aircraft connection, cockpit interlock, and Build and Run setup are ready.",
                    towBar);
            }
        }

        private static Transform CreateClampJaw(
            Transform parent,
            string objectName,
            float localX,
            Material yellow,
            Material black,
            Material steel,
            bool leftSide)
        {
            Transform jaw = new GameObject(objectName).transform;
            Undo.RegisterCreatedObjectUndo(jaw.gameObject, $"Create {objectName}");
            jaw.SetParent(parent, false);
            jaw.localPosition = new Vector3(localX, 0.02f, 0.02f);

            CreatePart(
                jaw,
                PrimitiveType.Cube,
                "Forged Jaw Plate",
                Vector3.zero,
                new Vector3(0.085f, 0.34f, 0.30f),
                Vector3.zero,
                yellow);
            float inward = leftSide ? 0.055f : -0.055f;
            CreatePart(
                jaw,
                PrimitiveType.Cube,
                "Rubber Axle Pad",
                new Vector3(inward, 0f, 0.035f),
                new Vector3(0.055f, 0.19f, 0.17f),
                Vector3.zero,
                black);
            CreatePart(
                jaw,
                PrimitiveType.Cylinder,
                "Jaw Pivot Bushing",
                new Vector3(0f, 0.12f, -0.08f),
                new Vector3(0.13f, 0.055f, 0.13f),
                new Vector3(0f, 0f, 90f),
                steel);
            CreatePart(
                jaw,
                PrimitiveType.Cylinder,
                "Jaw Lock Pin",
                new Vector3(0f, -0.12f, -0.07f),
                new Vector3(0.08f, 0.075f, 0.08f),
                new Vector3(0f, 0f, 90f),
                steel);
            return jaw;
        }

        private static Transform CreateTransportWheel(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Material black,
            Material steel,
            Material yellow)
        {
            Transform wheelRoot = new GameObject(objectName).transform;
            Undo.RegisterCreatedObjectUndo(wheelRoot.gameObject, $"Create {objectName}");
            wheelRoot.SetParent(parent, false);
            wheelRoot.localPosition = localPosition;

            CreatePart(
                wheelRoot,
                PrimitiveType.Cylinder,
                "Rubber Transport Tire",
                Vector3.zero,
                new Vector3(0.125f, 0.055f, 0.125f),
                new Vector3(0f, 0f, 90f),
                black);
            CreatePart(
                wheelRoot,
                PrimitiveType.Cylinder,
                "Transport Wheel Hub",
                Vector3.zero,
                new Vector3(0.060f, 0.068f, 0.060f),
                new Vector3(0f, 0f, 90f),
                steel);
            float side = localPosition.x < 0f ? -1f : 1f;
            CreatePart(
                parent,
                PrimitiveType.Cube,
                objectName + " Fork",
                localPosition + new Vector3(side * 0.04f, 0.14f, 0f),
                new Vector3(0.075f, 0.27f, 0.08f),
                new Vector3(0f, 0f, side * 8f),
                yellow);
            return wheelRoot;
        }

        private static void BuildSafetyChain(Transform parent, Material material)
        {
            for (int index = 0; index < 7; index++)
            {
                float t = index / 6f;
                Vector3 position = Vector3.Lerp(
                    new Vector3(-0.28f, -0.08f, -0.18f),
                    new Vector3(-0.20f, 0.02f, -0.68f),
                    t);
                CreatePart(
                    parent,
                    PrimitiveType.Cylinder,
                    $"Safety Chain Link {index + 1}",
                    position,
                    new Vector3(0.035f, 0.065f, 0.035f),
                    new Vector3(index % 2 == 0 ? 90f : 0f, 0f, 0f),
                    material);
            }
        }

        private static void InstallPlayerInteractor()
        {
            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            if (inventoryInteractor == null)
            {
                Debug.LogWarning("P-51 tow-bar setup could not find the Player InventoryInteractor.");
                return;
            }

            P51TowBarPlayerInteractor interactor =
                inventoryInteractor.GetComponent<P51TowBarPlayerInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<P51TowBarPlayerInteractor>(
                    inventoryInteractor.gameObject);
            }

            Camera camera = inventoryInteractor.GetComponentInChildren<Camera>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            interactor.Configure(camera, inventoryUI);
            EditorUtility.SetDirty(interactor);
        }

        private static Transform CreateMarker(
            Transform parent,
            string objectName,
            Vector3 localPosition)
        {
            Transform marker = new GameObject(objectName).transform;
            Undo.RegisterCreatedObjectUndo(marker.gameObject, $"Create {objectName}");
            marker.SetParent(parent, false);
            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one;
            return marker;
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
            Undo.RegisterCreatedObjectUndo(part, $"Create {objectName}");
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

        private static GameObject CreateSquareBeamBetween(
            Transform parent,
            string objectName,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject beam = CreatePart(
                parent,
                PrimitiveType.Cube,
                objectName,
                (start + end) * 0.5f,
                new Vector3(thickness, thickness, direction.magnitude),
                Vector3.zero,
                material);
            if (direction.sqrMagnitude > 0.0001f)
            {
                beam.transform.localRotation = Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
            }
            return beam;
        }

        private static GameObject CreateCylinderBetween(
            Transform parent,
            string objectName,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject cylinder = CreatePart(
                parent,
                PrimitiveType.Cylinder,
                objectName,
                (start + end) * 0.5f,
                new Vector3(radius, direction.magnitude * 0.5f, radius),
                Vector3.zero,
                material);
            if (direction.sqrMagnitude > 0.0001f)
            {
                cylinder.transform.localRotation = Quaternion.FromToRotation(
                    Vector3.up,
                    direction.normalized);
            }
            return cylinder;
        }

        private static float FindGroundHeight(Vector3 position, Transform aircraft)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                position + Vector3.up * 5f,
                Vector3.down,
                15f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            float height = position.y;
            for (int index = 0; index < hits.Length; index++)
            {
                if (hits[index].collider == null
                    || hits[index].collider.transform.IsChildOf(aircraft)
                    || hits[index].distance >= nearest)
                {
                    continue;
                }
                nearest = hits[index].distance;
                height = hits[index].point.y;
            }
            return height;
        }

        private static Material CreateMaterial(
            string assetPath,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != shader)
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
            if (string.IsNullOrWhiteSpace(parent)
                || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
