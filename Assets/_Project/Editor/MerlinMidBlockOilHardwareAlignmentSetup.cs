using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinMidBlockOilHardwareAlignmentSetup
    {
        [MenuItem("Hanger 51/Merlin Condition/16 - Align Oil Hardware with Block Center")]
        public static void AlignOilHardwareWithBlockCenter()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 16 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 16 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 16 failed. No engine condition systems were found.");
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
                    "Merlin Condition Step 16 failed. No complete oil-service hardware could be aligned. Run Steps 7, 9, 10, and 14 first.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 16 changed the service hardware but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 16 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 16 complete. Moved the dipstick and oil cap to the block's vertical center and slightly outside its side faces on {adjusted} engine setup(s), including the shipment template.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/17 - Validate Mid-Block Oil Hardware Alignment")]
        public static void ValidateMidBlockOilHardwareAlignment()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 17 failed: no engine condition systems exist.");
                return;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition != null)
                {
                    ValidateCondition(condition, ref passed);
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Condition Step 17 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 17 passed. Every current engine and the shipment template has small, trigger-only oil hardware centered vertically on the block and exposed along its side faces.");
            }
        }

        private static bool AdjustCondition(EngineConditionController condition)
        {
            EngineAssemblyStation station = condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its station or portable root is incomplete.",
                    condition);
                return false;
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
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its engine block, dipstick, or oil filler is missing.",
                    condition);
                return false;
            }

            Bounds bounds = CalculateCoreBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true),
                dipstick.transform,
                filler.transform);

            float sideClearance = Mathf.Max(0.025f, bounds.extents.x * 0.035f);
            Vector3 dipstickLocal = new Vector3(
                bounds.center.x - bounds.extents.x - sideClearance,
                bounds.center.y,
                bounds.center.z - bounds.extents.z * 0.18f);
            Vector3 fillerLocal = new Vector3(
                bounds.center.x + bounds.extents.x + sideClearance,
                bounds.center.y,
                bounds.center.z - bounds.extents.z * 0.10f);

            Undo.RecordObject(dipstick.transform, "Align Merlin dipstick with block center");
            Undo.RecordObject(filler.transform, "Align Merlin oil cap with block center");
            dipstick.transform.position = engineCore.transform.TransformPoint(dipstickLocal);
            dipstick.transform.rotation = engineCore.transform.rotation;
            filler.transform.position = engineCore.transform.TransformPoint(fillerLocal);
            filler.transform.rotation = engineCore.transform.rotation;

            // Keep the final cowling-clear dimensions while moving the service
            // points out of the engine core and onto its side faces.
            SetPartTransform(
                dipstick.transform,
                "Yellow Dipstick Handle",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0.052f, 0.013f, 0.052f));
            SetPartTransform(
                dipstick.transform,
                "Dipstick Handle Center",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0.022f, 0.015f, 0.022f));
            SetPartTransform(
                filler.transform,
                "Oil Filler Neck",
                Vector3.zero,
                new Vector3(0.048f, 0.048f, 0.048f));
            SetPartTransform(
                filler.transform,
                "Yellow Oil Filler Cap",
                new Vector3(0f, 0.042f, 0f),
                new Vector3(0.062f, 0.018f, 0.062f));
            SetPartTransform(
                filler.transform,
                "Oil Cap Grip Bar 1",
                new Vector3(0f, 0.067f, 0f),
                new Vector3(0.080f, 0.014f, 0.022f));
            SetPartTransform(
                filler.transform,
                "Oil Cap Grip Bar 2",
                new Vector3(0f, 0.067f, 0f),
                new Vector3(0.022f, 0.014f, 0.080f));

            ConfigureTrigger(
                dipstick.GetComponent<BoxCollider>(),
                new Vector3(0f, -0.065f, 0f),
                new Vector3(0.16f, 0.30f, 0.16f));
            ConfigureTrigger(
                filler.GetComponent<BoxCollider>(),
                new Vector3(0f, 0.005f, 0f),
                new Vector3(0.18f, 0.16f, 0.18f));

            EditorUtility.SetDirty(dipstick);
            EditorUtility.SetDirty(filler);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
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
                    $"Merlin Condition Step 17 failed: '{condition.name}' has no valid station or portable root.",
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
                    $"Merlin Condition Step 17 failed: '{condition.name}' is missing its engine block, dipstick, or oil filler.",
                    condition);
                passed = false;
                return;
            }

            Bounds bounds = CalculateCoreBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true),
                dipstick.transform,
                filler.transform);
            Vector3 dipstickLocal = engineCore.transform.InverseTransformPoint(
                dipstick.transform.position);
            Vector3 fillerLocal = engineCore.transform.InverseTransformPoint(
                filler.transform.position);

            float verticalTolerance = Mathf.Max(0.035f, bounds.extents.y * 0.12f);
            float minimumSideOffset = bounds.extents.x * 0.96f;
            float maximumSideOffset = bounds.extents.x * 1.20f + 0.08f;

            float dipstickSideOffset = bounds.center.x - dipstickLocal.x;
            float fillerSideOffset = fillerLocal.x - bounds.center.x;
            bool dipstickCentered = Mathf.Abs(dipstickLocal.y - bounds.center.y)
                <= verticalTolerance;
            bool fillerCentered = Mathf.Abs(fillerLocal.y - bounds.center.y)
                <= verticalTolerance;
            bool dipstickExposed = dipstickSideOffset >= minimumSideOffset
                && dipstickSideOffset <= maximumSideOffset;
            bool fillerExposed = fillerSideOffset >= minimumSideOffset
                && fillerSideOffset <= maximumSideOffset;
            bool dipstickTriggerValid = ValidateTrigger(
                dipstick.GetComponent<BoxCollider>(),
                new Vector3(0.16f, 0.30f, 0.16f));
            bool fillerTriggerValid = ValidateTrigger(
                filler.GetComponent<BoxCollider>(),
                new Vector3(0.18f, 0.16f, 0.18f));

            if (dipstickCentered
                && fillerCentered
                && dipstickExposed
                && fillerExposed
                && dipstickTriggerValid
                && fillerTriggerValid)
            {
                return;
            }

            Debug.LogError(
                $"Merlin Condition Step 17 failed for '{condition.name}'. "
                + $"Dipstick center aligned={dipstickCentered} (Y {dipstickLocal.y:F3}, block center {bounds.center.y:F3}); "
                + $"oil cap center aligned={fillerCentered} (Y {fillerLocal.y:F3}); "
                + $"dipstick side exposed={dipstickExposed} (offset {dipstickSideOffset:F3}); "
                + $"oil cap side exposed={fillerExposed} (offset {fillerSideOffset:F3}); "
                + $"dipstick trigger={dipstickTriggerValid}; oil cap trigger={fillerTriggerValid}.",
                condition);
            passed = false;
        }

        private static void ConfigureTrigger(
            BoxCollider collider,
            Vector3 center,
            Vector3 size)
        {
            if (collider == null)
            {
                return;
            }

            Undo.RecordObject(collider, "Configure Merlin oil hardware trigger");
            collider.enabled = true;
            collider.isTrigger = true;
            collider.center = center;
            collider.size = size;
            EditorUtility.SetDirty(collider);
        }

        private static bool ValidateTrigger(
            BoxCollider collider,
            Vector3 expectedSize)
        {
            return collider != null
                && collider.enabled
                && collider.isTrigger
                && Approximately(collider.size.x, expectedSize.x)
                && Approximately(collider.size.y, expectedSize.y)
                && Approximately(collider.size.z, expectedSize.z);
        }

        private static bool Approximately(float value, float expected)
        {
            return Mathf.Abs(value - expected) <= 0.001f;
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

            Undo.RecordObject(part, "Resize Merlin oil hardware");
            part.localPosition = localPosition;
            part.localScale = localScale;
            EditorUtility.SetDirty(part);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate != null && candidate.name == name)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static Bounds CalculateCoreBounds(
            Transform engineCore,
            Renderer[] renderers,
            Transform dipstick,
            Transform filler)
        {
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, new Vector3(2f, 1.4f, 2.8f));
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || renderer.transform.IsChildOf(dipstick)
                    || renderer.transform.IsChildOf(filler))
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
                            Vector3 local = engineCore.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? min.x : max.x,
                                    y == 0 ? min.y : max.y,
                                    z == 0 ? min.z : max.z));
                            if (!initialized)
                            {
                                result = new Bounds(local, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(local);
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
