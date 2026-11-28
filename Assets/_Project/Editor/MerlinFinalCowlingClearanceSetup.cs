using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinFinalCowlingClearanceSetup
    {
        [MenuItem("Hanger 51/Merlin Condition/12 - Lower Oil Cap and Dipstick for Cowling Clearance")]
        public static void LowerOilCapAndDipstickForCowlingClearance()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 12 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 12 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 12 failed. No engine condition systems were found.");
                return;
            }

            int adjusted = 0;
            GameObject selected = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition != null && AdjustCondition(condition))
                {
                    adjusted++;
                    if (selected == null && condition.gameObject.activeInHierarchy)
                    {
                        selected = condition.gameObject;
                    }
                }
            }

            if (adjusted == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 12 failed. No complete oil-service hardware could be adjusted. Run Steps 7, 9, and 10 first.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 12 changed the hardware but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 12 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 12 complete. Lowered and reduced the oil cap and dipstick handle on {adjusted} engine condition setup(s), including the complete-engine shipment template.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/13 - Validate Final Cowling Clearance")]
        public static void ValidateFinalCowlingClearance()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 13 failed: no engine condition systems exist.");
                return;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                if (conditions[index] != null)
                {
                    ValidateCondition(conditions[index], ref passed);
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Condition Step 13 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 13 passed. Every current engine and the shipment template has lowered, cowling-clear oil-service hardware with small trigger-only interaction volumes.");
            }
        }

        private static bool AdjustCondition(EngineConditionController condition)
        {
            EngineAssemblyStation station = condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                return false;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            if (engineCore == null)
            {
                return false;
            }

            EngineDipstickController dipstick =
                transport.TransportRoot.GetComponentInChildren<EngineDipstickController>(true);
            EngineConditionInspectionTarget filler = FindOilFiller(transport.TransportRoot);
            if (dipstick == null || filler == null)
            {
                return false;
            }

            Bounds bounds = CalculateLocalBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true));

            dipstick.transform.position = engineCore.transform.TransformPoint(
                bounds.center + new Vector3(
                    -bounds.extents.x * 0.72f,
                    bounds.extents.y * 0.67f,
                    -bounds.extents.z * 0.18f));
            dipstick.transform.rotation = engineCore.transform.rotation;

            SetPartTransform(
                dipstick.transform,
                "Yellow Dipstick Handle",
                new Vector3(0f, 0.045f, 0f),
                new Vector3(0.075f, 0.018f, 0.075f));
            SetPartTransform(
                dipstick.transform,
                "Dipstick Handle Center",
                new Vector3(0f, 0.045f, 0f),
                new Vector3(0.032f, 0.021f, 0.032f));

            BoxCollider dipstickCollider = dipstick.GetComponent<BoxCollider>();
            if (dipstickCollider != null)
            {
                dipstickCollider.isTrigger = true;
                dipstickCollider.center = new Vector3(0f, -0.03f, 0f);
                dipstickCollider.size = new Vector3(0.19f, 0.34f, 0.19f);
                EditorUtility.SetDirty(dipstickCollider);
            }

            filler.transform.position = engineCore.transform.TransformPoint(
                bounds.center + new Vector3(
                    bounds.extents.x * 0.62f,
                    bounds.extents.y * 0.65f,
                    -bounds.extents.z * 0.10f));
            filler.transform.rotation = engineCore.transform.rotation;

            SetPartTransform(
                filler.transform,
                "Oil Filler Neck",
                Vector3.zero,
                new Vector3(0.065f, 0.065f, 0.065f));
            SetPartTransform(
                filler.transform,
                "Yellow Oil Filler Cap",
                new Vector3(0f, 0.065f, 0f),
                new Vector3(0.085f, 0.027f, 0.085f));
            SetPartTransform(
                filler.transform,
                "Oil Cap Grip Bar 1",
                new Vector3(0f, 0.098f, 0f),
                new Vector3(0.11f, 0.022f, 0.032f));
            SetPartTransform(
                filler.transform,
                "Oil Cap Grip Bar 2",
                new Vector3(0f, 0.098f, 0f),
                new Vector3(0.032f, 0.022f, 0.11f));

            BoxCollider fillerCollider = filler.GetComponent<BoxCollider>();
            if (fillerCollider != null)
            {
                fillerCollider.isTrigger = true;
                fillerCollider.center = new Vector3(0f, 0.01f, 0f);
                fillerCollider.size = new Vector3(0.22f, 0.20f, 0.22f);
                EditorUtility.SetDirty(fillerCollider);
            }

            EditorUtility.SetDirty(dipstick);
            EditorUtility.SetDirty(filler);
            EditorUtility.SetDirty(condition);
            return true;
        }

        private static void ValidateCondition(
            EngineConditionController condition,
            ref bool passed)
        {
            EngineAssemblyStation station = condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 13 failed: '{condition.name}' has no valid station or portable root.",
                    condition);
                passed = false;
                return;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            EngineDipstickController dipstick =
                transport.TransportRoot.GetComponentInChildren<EngineDipstickController>(true);
            EngineConditionInspectionTarget filler = FindOilFiller(transport.TransportRoot);
            if (engineCore == null || dipstick == null || filler == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 13 failed: '{condition.name}' is missing its engine block, dipstick, or oil filler.",
                    condition);
                passed = false;
                return;
            }

            Bounds bounds = CalculateLocalBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true));
            Vector3 dipstickLocal = engineCore.transform.InverseTransformPoint(
                dipstick.transform.position);
            Vector3 fillerLocal = engineCore.transform.InverseTransformPoint(
                filler.transform.position);

            Transform handle = FindDescendant(
                dipstick.transform,
                "Yellow Dipstick Handle");
            Transform cap = FindDescendant(
                filler.transform,
                "Yellow Oil Filler Cap");
            BoxCollider dipstickCollider = dipstick.GetComponent<BoxCollider>();
            BoxCollider fillerCollider = filler.GetComponent<BoxCollider>();

            float allowedTop = bounds.center.y + bounds.extents.y * 0.75f;
            bool valid = handle != null
                && handle.localScale.x <= 0.080f
                && handle.localScale.z <= 0.080f
                && cap != null
                && cap.localScale.x <= 0.090f
                && cap.localScale.z <= 0.090f
                && dipstickLocal.y <= allowedTop
                && fillerLocal.y <= allowedTop
                && ValidateTrigger(dipstickCollider, 0.36f)
                && ValidateTrigger(fillerCollider, 0.24f);

            if (!valid)
            {
                Debug.LogError(
                    $"Merlin Condition Step 13 failed: '{condition.name}' oil cap or dipstick is still too high, too large, or has an oversized solid collider.",
                    condition);
                passed = false;
            }
        }

        private static bool ValidateTrigger(
            BoxCollider collider,
            float maximumDimension)
        {
            if (collider == null || !collider.isTrigger)
            {
                return false;
            }

            Vector3 size = collider.size;
            return size.x <= maximumDimension
                && size.y <= maximumDimension
                && size.z <= maximumDimension;
        }

        private static EngineConditionInspectionTarget FindOilFiller(
            Transform portableRoot)
        {
            EngineConditionInspectionTarget[] targets =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                EngineConditionInspectionTarget target = targets[index];
                if (target != null
                    && target.InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    return target;
                }
            }
            return null;
        }

        private static void SetPartTransform(
            Transform root,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            Transform part = FindDescendant(root, name);
            if (part == null)
            {
                return;
            }

            part.localPosition = localPosition;
            part.localScale = localScale;
            EditorUtility.SetDirty(part);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == name)
                {
                    return transforms[index];
                }
            }
            return null;
        }

        private static Bounds CalculateLocalBounds(
            Transform root,
            Renderer[] renderers)
        {
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, new Vector3(2f, 1.4f, 2.8f));
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || renderer.GetComponentInParent<EngineDipstickController>() != null)
                {
                    continue;
                }

                EngineConditionInspectionTarget inspection =
                    renderer.GetComponentInParent<EngineConditionInspectionTarget>();
                if (inspection != null
                    && inspection.InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    continue;
                }

                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 corner = root.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? min.x : max.x,
                                    y == 0 ? min.y : max.y,
                                    z == 0 ? min.z : max.z));
                            if (!initialized)
                            {
                                result = new Bounds(corner, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(corner);
                            }
                        }
                    }
                }
            }

            if (!initialized || result.size.sqrMagnitude < 0.01f)
            {
                result = new Bounds(Vector3.zero, new Vector3(2f, 1.4f, 2.8f));
            }
            return result;
        }

        private static T GetObject<T>(
            SerializedObject serialized,
            string propertyName)
            where T : Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null
                ? property.objectReferenceValue as T
                : null;
        }
    }
}
