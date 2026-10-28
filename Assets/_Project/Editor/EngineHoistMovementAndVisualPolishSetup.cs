using System.Collections.Generic;
using System.IO;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class EngineHoistMovementAndVisualPolishSetup
    {
        private const string HoistName = "Portable Engine Hoist";
        private const string DetailRootName = "Hoist Realism Upgrade";
        private const string HookRootName = "Realistic Curved Safety Hook";
        private const string ChainRootName = "Individual Load Chain Links";
        private const string HandleTargetName = "Hoist Interaction Handles";
        private const string HookPointName = "Load Hook Point";
        private const string LoadChainName = "Load Chain";

        private const string MaterialFolder = "Assets/_Project/EngineAssembly/Materials";
        private const string RedMaterialPath = MaterialFolder + "/HoistRed.mat";
        private const string BlackMaterialPath = MaterialFolder + "/HoistBlack.mat";
        private const string ChromeMaterialPath = MaterialFolder + "/HoistChrome.mat";
        private const string RubberMaterialPath = MaterialFolder + "/HoistRubber.mat";
        private const string WarningMaterialPath = MaterialFolder + "/HoistWarningYellow.mat";
        private const string HoseMaterialPath = MaterialFolder + "/HoistHydraulicHose.mat";

        [MenuItem("Hanger 51/Engine Hoist/3 - Fix Forward Movement and Add Realistic Detail")]
        public static void ApplyMovementAndVisualPolish()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Engine Hoist Step 3 failed. Exit Play mode first.");
                return;
            }

            GameObject hoist = GameObject.Find(HoistName);
            if (hoist == null)
            {
                Debug.LogError(
                    "Engine Hoist Step 3 failed. Run Engine Hoist Step 1 before applying the movement and visual upgrade.");
                return;
            }

            EngineHoistController controller = hoist.GetComponent<EngineHoistController>();
            Transform handleTarget = FindDescendant(hoist.transform, HandleTargetName);
            Collider handleCollider = handleTarget != null
                ? handleTarget.GetComponent<Collider>()
                : null;

            if (controller == null || handleCollider == null)
            {
                Debug.LogError(
                    "Engine Hoist Step 3 failed. The hoist controller or handle interaction collider is missing.",
                    hoist);
                return;
            }

            EnsureFolder(MaterialFolder);
            Material red = LoadRequiredMaterial(RedMaterialPath, "red hoist paint");
            Material black = LoadRequiredMaterial(BlackMaterialPath, "black hoist paint");
            Material chrome = LoadRequiredMaterial(ChromeMaterialPath, "chrome hardware");
            Material rubber = LoadRequiredMaterial(RubberMaterialPath, "rubber wheels");
            Material warning = CreateOrUpdateMaterial(
                WarningMaterialPath,
                new Color(0.95f, 0.58f, 0.025f, 1f),
                0.18f,
                0.34f);
            Material hose = CreateOrUpdateMaterial(
                HoseMaterialPath,
                new Color(0.015f, 0.017f, 0.020f, 1f),
                0.02f,
                0.24f);

            if (red == null || black == null || chrome == null || rubber == null)
            {
                return;
            }

            EngineHoistMovementCollisionGuard movementGuard =
                hoist.GetComponent<EngineHoistMovementCollisionGuard>();
            if (movementGuard == null)
            {
                movementGuard = Undo.AddComponent<EngineHoistMovementCollisionGuard>(hoist);
            }
            movementGuard.Configure(handleCollider);
            EditorUtility.SetDirty(movementGuard);

            Transform oldDetailRoot = hoist.transform.Find(DetailRootName);
            if (oldDetailRoot != null)
            {
                Object.DestroyImmediate(oldDetailRoot.gameObject);
            }

            GameObject detailRootObject = new GameObject(DetailRootName);
            Undo.RegisterCreatedObjectUndo(detailRootObject, "Create realistic hoist detail root");
            Transform detailRoot = detailRootObject.transform;
            detailRoot.SetParent(hoist.transform, false);

            BuildStructuralHardware(detailRoot, red, black, chrome, warning);
            BuildHydraulicDetail(detailRoot, black, chrome, hose);
            BuildCasterDetail(detailRoot, chrome, black, rubber);
            RebuildLoadChain(hoist.transform, chrome, black);
            RebuildHook(hoist.transform, chrome, black, red);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Engine Hoist Step 3 applied the upgrade but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Engine Hoist Step 3 applied the upgrade, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = hoist;
            Debug.Log(
                "Engine Hoist Step 3 complete. Disabled the Player-blocking handle collider while pushing, "
                + "added structural fasteners, boom adjustment hardware, hydraulic detail, caster hardware, "
                + "individual chain links, and a curved safety hook.",
                hoist);
        }

        [MenuItem("Hanger 51/Engine Hoist/4 - Validate Movement and Realistic Detail")]
        public static void ValidateMovementAndVisualPolish()
        {
            bool passed = true;
            GameObject hoist = GameObject.Find(HoistName);
            if (hoist == null)
            {
                Debug.LogError("Engine Hoist Step 4 failed: the portable engine hoist is missing.");
                return;
            }

            EngineHoistMovementCollisionGuard movementGuard =
                hoist.GetComponent<EngineHoistMovementCollisionGuard>();
            if (movementGuard == null || !movementGuard.IsConfigured)
            {
                Debug.LogError(
                    "Engine Hoist Step 4 failed: the forward-movement collision guard is missing or unconfigured.",
                    hoist);
                passed = false;
            }

            Transform detailRoot = hoist.transform.Find(DetailRootName);
            int detailRendererCount = detailRoot != null
                ? detailRoot.GetComponentsInChildren<Renderer>(true).Length
                : 0;
            if (detailRoot == null || detailRendererCount < 35)
            {
                Debug.LogError(
                    $"Engine Hoist Step 4 failed: expected at least 35 mechanical-detail renderers; found {detailRendererCount}.",
                    hoist);
                passed = false;
            }

            Transform hookPoint = FindDescendant(hoist.transform, HookPointName);
            Transform hookRoot = hookPoint != null ? hookPoint.Find(HookRootName) : null;
            int hookSegmentCount = hookRoot != null
                ? CountNamedDescendants(hookRoot, "Curved Hook Segment")
                : 0;
            if (hookRoot == null
                || hookSegmentCount < 9
                || FindDescendant(hookRoot, "Hook Safety Latch") == null
                || FindDescendant(hookRoot, "Forged Hook Eye") == null)
            {
                Debug.LogError(
                    $"Engine Hoist Step 4 failed: the curved safety hook is incomplete; found {hookSegmentCount} curve segments.",
                    hoist);
                passed = false;
            }

            Transform chainRoot = hoist.transform.Find(ChainRootName);
            int chainLinkCount = chainRoot != null
                ? CountDirectChildrenStartingWith(chainRoot, "Chain Link ")
                : 0;
            if (chainRoot == null || chainLinkCount < 6)
            {
                Debug.LogError(
                    $"Engine Hoist Step 4 failed: expected at least 6 individual chain links; found {chainLinkCount}.",
                    hoist);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Engine Hoist Step 4 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"Engine Hoist Step 4 passed. Forward movement guard configured; "
                    + $"{detailRendererCount} mechanical details, {chainLinkCount} chain links, "
                    + $"and {hookSegmentCount} curved hook segments are ready.",
                    hoist);
            }
        }

        private static void BuildStructuralHardware(
            Transform parent,
            Material red,
            Material black,
            Material chrome,
            Material warning)
        {
            CreatePart(parent, PrimitiveType.Cube, "Left Mast Base Gusset",
                new Vector3(-0.20f, 0.48f, -0.35f),
                new Vector3(0.055f, 0.46f, 0.42f),
                new Vector3(0f, 0f, -18f),
                red);
            CreatePart(parent, PrimitiveType.Cube, "Right Mast Base Gusset",
                new Vector3(0.20f, 0.48f, -0.35f),
                new Vector3(0.055f, 0.46f, 0.42f),
                new Vector3(0f, 0f, 18f),
                red);

            CreateCrossPin(parent, "Main Boom Pivot Pin",
                new Vector3(0f, 2.06f, -0.34f), 0.055f, 0.36f, chrome, black);
            CreateCrossPin(parent, "Boom Extension Lock Pin",
                new Vector3(0f, 2.08f, 0.95f), 0.042f, 0.31f, chrome, black);
            CreateCrossPin(parent, "Hydraulic Lower Pivot Pin",
                new Vector3(0f, 0.58f, -0.28f), 0.045f, 0.30f, chrome, black);
            CreateCrossPin(parent, "Hydraulic Upper Pivot Pin",
                new Vector3(0f, 1.88f, 0.15f), 0.045f, 0.30f, chrome, black);

            float[] holePositions = { 1.04f, 1.22f, 1.40f, 1.58f };
            for (int index = 0; index < holePositions.Length; index++)
            {
                float z = holePositions[index];
                CreateSideDisc(parent, $"Left Boom Adjustment Hole {index + 1}",
                    new Vector3(-0.112f, 2.08f, z), black);
                CreateSideDisc(parent, $"Right Boom Adjustment Hole {index + 1}",
                    new Vector3(0.112f, 2.08f, z), black);
            }

            Vector3[] baseBoltPositions =
            {
                new Vector3(-0.20f, 0.315f, -0.51f),
                new Vector3(0.20f, 0.315f, -0.51f),
                new Vector3(-0.20f, 0.315f, -0.19f),
                new Vector3(0.20f, 0.315f, -0.19f)
            };
            for (int index = 0; index < baseBoltPositions.Length; index++)
            {
                CreateVerticalBolt(parent, $"Mast Base Bolt {index + 1}", baseBoltPositions[index], chrome, black);
            }

            CreatePart(parent, PrimitiveType.Cube, "Load Rating Plate",
                new Vector3(0f, 1.25f, -0.478f),
                new Vector3(0.18f, 0.38f, 0.012f),
                Vector3.zero,
                warning);
            CreatePart(parent, PrimitiveType.Cube, "Rating Plate Upper Stripe",
                new Vector3(0f, 1.36f, -0.492f),
                new Vector3(0.14f, 0.026f, 0.008f),
                Vector3.zero,
                black);
            CreatePart(parent, PrimitiveType.Cube, "Rating Plate Lower Stripe",
                new Vector3(0f, 1.14f, -0.492f),
                new Vector3(0.14f, 0.026f, 0.008f),
                Vector3.zero,
                black);

            CreatePart(parent, PrimitiveType.Cube, "Left Leg Retaining Pin",
                new Vector3(-0.48f, 0.205f, -0.18f),
                new Vector3(0.035f, 0.035f, 0.22f),
                Vector3.zero,
                chrome);
            CreatePart(parent, PrimitiveType.Cube, "Right Leg Retaining Pin",
                new Vector3(0.48f, 0.205f, -0.18f),
                new Vector3(0.035f, 0.035f, 0.22f),
                Vector3.zero,
                chrome);
        }

        private static void BuildHydraulicDetail(
            Transform parent,
            Material black,
            Material chrome,
            Material hose)
        {
            Vector3[] hosePoints =
            {
                new Vector3(0.09f, 0.49f, -0.28f),
                new Vector3(0.17f, 0.68f, -0.24f),
                new Vector3(0.18f, 1.05f, -0.13f),
                new Vector3(0.13f, 1.42f, 0.00f),
                new Vector3(0.08f, 1.72f, 0.10f)
            };
            for (int index = 0; index < hosePoints.Length - 1; index++)
            {
                CreateCylinderBetween(
                    parent,
                    $"Hydraulic Hose Segment {index + 1}",
                    hosePoints[index],
                    hosePoints[index + 1],
                    0.018f,
                    hose);
            }

            CreatePart(parent, PrimitiveType.Cylinder, "Hydraulic Lower Fitting",
                new Vector3(0.09f, 0.49f, -0.28f),
                new Vector3(0.035f, 0.055f, 0.035f),
                new Vector3(90f, 0f, 0f),
                chrome);
            CreatePart(parent, PrimitiveType.Cylinder, "Hydraulic Upper Fitting",
                new Vector3(0.08f, 1.72f, 0.10f),
                new Vector3(0.035f, 0.055f, 0.035f),
                new Vector3(90f, 0f, 0f),
                chrome);

            CreatePart(parent, PrimitiveType.Cylinder, "Jack Release Valve Stem",
                new Vector3(0.15f, 0.44f, -0.31f),
                new Vector3(0.025f, 0.055f, 0.025f),
                new Vector3(0f, 0f, 90f),
                chrome);
            CreatePart(parent, PrimitiveType.Cylinder, "Jack Release Valve Knob",
                new Vector3(0.21f, 0.44f, -0.31f),
                new Vector3(0.050f, 0.028f, 0.050f),
                new Vector3(0f, 0f, 90f),
                black);

            CreatePart(parent, PrimitiveType.Cylinder, "Pump Handle Pivot Boss",
                new Vector3(-0.11f, 0.62f, -0.34f),
                new Vector3(0.060f, 0.070f, 0.060f),
                new Vector3(0f, 0f, 90f),
                black);
            CreateCrossPin(parent, "Pump Handle Pivot Pin",
                new Vector3(-0.11f, 0.62f, -0.34f), 0.025f, 0.20f, chrome, black);
        }

        private static void BuildCasterDetail(
            Transform parent,
            Material chrome,
            Material black,
            Material rubber)
        {
            Vector3[] wheelPositions =
            {
                new Vector3(-0.52f, 0.06f, -0.48f),
                new Vector3(0.52f, 0.06f, -0.48f),
                new Vector3(-0.52f, 0.06f, 0.70f),
                new Vector3(0.52f, 0.06f, 0.70f),
                new Vector3(-0.52f, 0.06f, 1.82f),
                new Vector3(0.52f, 0.06f, 1.82f)
            };

            for (int index = 0; index < wheelPositions.Length; index++)
            {
                Vector3 position = wheelPositions[index];
                CreatePart(parent, PrimitiveType.Cylinder, $"Caster {index + 1} Wheel Hub",
                    position,
                    new Vector3(0.043f, 0.072f, 0.043f),
                    new Vector3(0f, 0f, 90f),
                    chrome);
                CreatePart(parent, PrimitiveType.Cylinder, $"Caster {index + 1} Axle Cap",
                    position + new Vector3(index % 2 == 0 ? -0.075f : 0.075f, 0f, 0f),
                    new Vector3(0.055f, 0.018f, 0.055f),
                    new Vector3(0f, 0f, 90f),
                    black);
                CreatePart(parent, PrimitiveType.Cylinder, $"Caster {index + 1} Swivel Bearing",
                    position + new Vector3(0f, 0.15f, 0f),
                    new Vector3(0.070f, 0.025f, 0.070f),
                    Vector3.zero,
                    chrome);
                CreatePart(parent, PrimitiveType.Cube, $"Caster {index + 1} Brake Tab",
                    position + new Vector3(0f, 0.02f, 0.11f),
                    new Vector3(0.08f, 0.025f, 0.10f),
                    new Vector3(15f, 0f, 0f),
                    rubber);
            }
        }

        private static void RebuildLoadChain(
            Transform hoist,
            Material chrome,
            Material black)
        {
            Transform existingRoot = hoist.Find(ChainRootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot.gameObject);
            }

            Transform loadChain = FindDescendant(hoist, LoadChainName);
            if (loadChain != null)
            {
                Renderer renderer = loadChain.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            GameObject rootObject = new GameObject(ChainRootName);
            rootObject.transform.SetParent(hoist, false);
            Transform root = rootObject.transform;

            const int linkCount = 7;
            const float startY = 2.045f;
            const float endY = 1.715f;
            for (int index = 0; index < linkCount; index++)
            {
                float t = linkCount <= 1 ? 0f : index / (float)(linkCount - 1);
                Vector3 position = new Vector3(0f, Mathf.Lerp(startY, endY, t), 1.82f);
                CreateChainLink(
                    root,
                    $"Chain Link {index + 1}",
                    position,
                    index % 2 == 0 ? 0f : 90f,
                    index % 2 == 0 ? chrome : black);
            }
        }

        private static void CreateChainLink(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            float yaw,
            Material material)
        {
            GameObject linkObject = new GameObject(objectName);
            linkObject.transform.SetParent(parent, false);
            linkObject.transform.localPosition = localPosition;
            linkObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform link = linkObject.transform;

            const float halfWidth = 0.028f;
            const float halfHeight = 0.030f;
            const float radius = 0.008f;
            CreateCylinderBetween(link, "Left Rail",
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f), radius, material);
            CreateCylinderBetween(link, "Right Rail",
                new Vector3(halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f), radius, material);
            CreateCylinderBetween(link, "Top Curve",
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f), radius, material);
            CreateCylinderBetween(link, "Bottom Curve",
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f), radius, material);
        }

        private static void RebuildHook(
            Transform hoist,
            Material chrome,
            Material black,
            Material red)
        {
            Transform hookPoint = FindDescendant(hoist, HookPointName);
            if (hookPoint == null)
            {
                Debug.LogError("Engine Hoist Step 3 could not find the load hook point.", hoist);
                return;
            }

            List<GameObject> oldChildren = new List<GameObject>();
            foreach (Transform child in hookPoint)
            {
                oldChildren.Add(child.gameObject);
            }
            for (int index = 0; index < oldChildren.Count; index++)
            {
                Object.DestroyImmediate(oldChildren[index]);
            }

            GameObject rootObject = new GameObject(HookRootName);
            rootObject.transform.SetParent(hookPoint, false);
            Transform root = rootObject.transform;

            CreatePart(root, PrimitiveType.Cylinder, "Forged Hook Eye",
                new Vector3(0f, -0.015f, 0f),
                new Vector3(0.095f, 0.038f, 0.095f),
                new Vector3(90f, 0f, 0f),
                chrome);
            CreatePart(root, PrimitiveType.Cylinder, "Hook Eye Opening",
                new Vector3(0f, -0.015f, -0.041f),
                new Vector3(0.047f, 0.006f, 0.047f),
                new Vector3(90f, 0f, 0f),
                black);
            CreatePart(root, PrimitiveType.Cylinder, "Hook Swivel Neck",
                new Vector3(0f, -0.095f, 0f),
                new Vector3(0.048f, 0.075f, 0.048f),
                Vector3.zero,
                chrome);

            Vector3[] points =
            {
                new Vector3(0f, -0.14f, 0f),
                new Vector3(0f, -0.23f, 0f),
                new Vector3(0.01f, -0.32f, 0f),
                new Vector3(0.045f, -0.40f, 0f),
                new Vector3(0.105f, -0.455f, 0f),
                new Vector3(0.180f, -0.468f, 0f),
                new Vector3(0.245f, -0.438f, 0f),
                new Vector3(0.282f, -0.375f, 0f),
                new Vector3(0.286f, -0.305f, 0f),
                new Vector3(0.257f, -0.245f, 0f),
                new Vector3(0.205f, -0.207f, 0f)
            };

            for (int index = 0; index < points.Length - 1; index++)
            {
                float t = index / (float)(points.Length - 2);
                float radius = Mathf.Lerp(0.048f, 0.029f, t);
                CreateCylinderBetween(
                    root,
                    $"Curved Hook Segment {index + 1}",
                    points[index],
                    points[index + 1],
                    radius,
                    chrome);
                CreatePart(
                    root,
                    PrimitiveType.Sphere,
                    $"Forged Curve Joint {index + 1}",
                    points[index + 1],
                    Vector3.one * (radius * 2.02f),
                    Vector3.zero,
                    chrome);
            }

            CreateCylinderBetween(
                root,
                "Hook Safety Latch",
                new Vector3(0.018f, -0.205f, -0.012f),
                new Vector3(0.208f, -0.222f, -0.012f),
                0.013f,
                red);
            CreatePart(root, PrimitiveType.Cylinder, "Safety Latch Pivot",
                new Vector3(0.018f, -0.205f, -0.012f),
                new Vector3(0.024f, 0.020f, 0.024f),
                new Vector3(90f, 0f, 0f),
                black);
            CreatePart(root, PrimitiveType.Sphere, "Hook Tip Rounded End",
                points[points.Length - 1],
                Vector3.one * 0.060f,
                Vector3.zero,
                chrome);
        }

        private static void CreateCrossPin(
            Transform parent,
            string objectName,
            Vector3 position,
            float radius,
            float length,
            Material shaftMaterial,
            Material capMaterial)
        {
            CreatePart(parent, PrimitiveType.Cylinder, objectName,
                position,
                new Vector3(radius, length * 0.5f, radius),
                new Vector3(0f, 0f, 90f),
                shaftMaterial);
            CreatePart(parent, PrimitiveType.Cylinder, objectName + " Left Cap",
                position + new Vector3(-length * 0.52f, 0f, 0f),
                new Vector3(radius * 1.35f, radius * 0.20f, radius * 1.35f),
                new Vector3(0f, 0f, 90f),
                capMaterial);
            CreatePart(parent, PrimitiveType.Cylinder, objectName + " Right Cap",
                position + new Vector3(length * 0.52f, 0f, 0f),
                new Vector3(radius * 1.35f, radius * 0.20f, radius * 1.35f),
                new Vector3(0f, 0f, 90f),
                capMaterial);
        }

        private static void CreateSideDisc(
            Transform parent,
            string objectName,
            Vector3 position,
            Material material)
        {
            CreatePart(parent, PrimitiveType.Cylinder, objectName,
                position,
                new Vector3(0.030f, 0.008f, 0.030f),
                new Vector3(0f, 0f, 90f),
                material);
        }

        private static void CreateVerticalBolt(
            Transform parent,
            string objectName,
            Vector3 position,
            Material chrome,
            Material black)
        {
            CreatePart(parent, PrimitiveType.Cylinder, objectName + " Washer",
                position,
                new Vector3(0.036f, 0.006f, 0.036f),
                Vector3.zero,
                black);
            CreatePart(parent, PrimitiveType.Cylinder, objectName + " Head",
                position + Vector3.up * 0.018f,
                new Vector3(0.028f, 0.018f, 0.028f),
                Vector3.zero,
                chrome);
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
            if (length > 0.0001f)
            {
                cylinder.transform.localRotation =
                    Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }
            return cylinder;
        }

        private static Material LoadRequiredMaterial(string path, string description)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogError(
                    $"Engine Hoist Step 3 could not find the {description} material at '{path}'. Run Engine Hoist Step 1 again.");
            }
            return material;
        }

        private static Material CreateOrUpdateMaterial(
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
            else if (material.shader != shader)
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

        private static Transform FindDescendant(Transform root, string objectName)
        {
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

        private static int CountNamedDescendants(Transform root, string namePrefix)
        {
            int count = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name.StartsWith(namePrefix))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountDirectChildrenStartingWith(Transform root, string namePrefix)
        {
            int count = 0;
            foreach (Transform child in root)
            {
                if (child.name.StartsWith(namePrefix))
                {
                    count++;
                }
            }
            return count;
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
