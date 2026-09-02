using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinTopCenterOilHardwareAlignmentSetup
    {
        [MenuItem("Hanger 51/Merlin Condition/18 - Align Oil Hardware Along Top Centerline")]
        public static void AlignOilHardwareAlongTopCenterline()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 18 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 18 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 18 failed. No engine condition systems were found.");
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
                    "Merlin Condition Step 18 failed. No complete oil-service hardware could be aligned. Run Steps 7, 9, 10, and 14 first.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 18 changed the service hardware but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 18 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 18 complete. Placed the dipstick and oil cap along the engine top centerline, separated front-to-back, with only their small handles exposed on {adjusted} engine setup(s), including the shipment template.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/19 - Validate Top-Center Oil Hardware Alignment")]
        public static void ValidateTopCenterOilHardwareAlignment()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 19 failed: no engine condition systems exist.");
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
                Debug.LogError("Merlin Condition Step 19 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 19 passed. Every current engine and the shipment template has small oil-service hardware exposed through the top, centered across the block, and separated along the engine length.");
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

            ConfigureSmallHardware(dipstick, filler);
            EnsureSingleBoxTrigger(
                dipstick.gameObject,
                new Vector3(0f, -0.065f, 0f),
                new Vector3(0.16f, 0.30f, 0.16f));
            EnsureSingleBoxTrigger(
                filler.gameObject,
                new Vector3(0f, 0.005f, 0f),
                new Vector3(0.18f, 0.16f, 0.18f));

            float blockTop = bounds.center.y + bounds.extents.y;
            float longitudinalSpacing = Mathf.Max(
                0.16f,
                bounds.extents.z * 0.24f);

            Vector3 dipstickLocal = new Vector3(
                bounds.center.x,
                blockTop,
                bounds.center.z - longitudinalSpacing);
            Vector3 fillerLocal = new Vector3(
                bounds.center.x,
                blockTop,
                bounds.center.z + longitudinalSpacing);

            Undo.RecordObject(dipstick.transform, "Align Merlin dipstick on top centerline");
            Undo.RecordObject(filler.transform, "Align Merlin oil cap on top centerline");
            dipstick.transform.position = engineCore.transform.TransformPoint(dipstickLocal);
            dipstick.transform.rotation = engineCore.transform.rotation;
            filler.transform.position = engineCore.transform.TransformPoint(fillerLocal);
            filler.transform.rotation = engineCore.transform.rotation;

            float desiredProtrusion = Mathf.Clamp(
                bounds.extents.y * 0.075f,
                0.045f,
                0.085f);
            MoveRenderedTopTo(
                engineCore.transform,
                dipstick.transform,
                blockTop + desiredProtrusion);
            MoveRenderedTopTo(
                engineCore.transform,
                filler.transform,
                blockTop + desiredProtrusion);

            EditorUtility.SetDirty(dipstick);
            EditorUtility.SetDirty(filler);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
            return true;
        }

        private static void ConfigureSmallHardware(
            EngineDipstickController dipstick,
            EngineConditionInspectionTarget filler)
        {
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
        }

        private static void MoveRenderedTopTo(
            Transform engineCore,
            Transform hardwareRoot,
            float desiredLocalTop)
        {
            float currentTop = CalculateRenderedLocalTop(
                engineCore,
                hardwareRoot.GetComponentsInChildren<Renderer>(true));
            if (float.IsInfinity(currentTop))
            {
                return;
            }

            Vector3 localPosition = engineCore.InverseTransformPoint(
                hardwareRoot.position);
            localPosition.y += desiredLocalTop - currentTop;
            hardwareRoot.position = engineCore.TransformPoint(localPosition);
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
                    $"Merlin Condition Step 19 failed: '{condition.name}' has no valid station or portable root.",
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
                    $"Merlin Condition Step 19 failed: '{condition.name}' is missing its engine block, dipstick, or oil filler.",
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

            float blockTop = bounds.center.y + bounds.extents.y;
            float dipstickVisualTop = CalculateRenderedLocalTop(
                engineCore.transform,
                dipstick.transform.GetComponentsInChildren<Renderer>(true));
            float fillerVisualTop = CalculateRenderedLocalTop(
                engineCore.transform,
                filler.transform.GetComponentsInChildren<Renderer>(true));
            float dipstickProtrusion = dipstickVisualTop - blockTop;
            float fillerProtrusion = fillerVisualTop - blockTop;

            float centerTolerance = Mathf.Max(0.04f, bounds.extents.x * 0.12f);
            float minimumLongitudinalSeparation = Mathf.Max(
                0.22f,
                bounds.extents.z * 0.30f);

            bool dipstickCentered = Mathf.Abs(dipstickLocal.x - bounds.center.x)
                <= centerTolerance;
            bool fillerCentered = Mathf.Abs(fillerLocal.x - bounds.center.x)
                <= centerTolerance;
            bool separatedAlongLength = fillerLocal.z - dipstickLocal.z
                >= minimumLongitudinalSeparation;
            bool dipstickInsideLength = dipstickLocal.z
                >= bounds.center.z - bounds.extents.z * 0.90f
                && dipstickLocal.z <= bounds.center.z + bounds.extents.z * 0.90f;
            bool fillerInsideLength = fillerLocal.z
                >= bounds.center.z - bounds.extents.z * 0.90f
                && fillerLocal.z <= bounds.center.z + bounds.extents.z * 0.90f;
            bool dipstickExposureValid = dipstickProtrusion >= 0.035f
                && dipstickProtrusion <= 0.095f;
            bool fillerExposureValid = fillerProtrusion >= 0.035f
                && fillerProtrusion <= 0.095f;
            bool dipstickTriggerValid = ValidateTrigger(
                dipstick.GetComponent<BoxCollider>(),
                new Vector3(0.16f, 0.30f, 0.16f));
            bool fillerTriggerValid = ValidateTrigger(
                filler.GetComponent<BoxCollider>(),
                new Vector3(0.18f, 0.16f, 0.18f));

            if (dipstickCentered
                && fillerCentered
                && separatedAlongLength
                && dipstickInsideLength
                && fillerInsideLength
                && dipstickExposureValid
                && fillerExposureValid
                && dipstickTriggerValid
                && fillerTriggerValid)
            {
                return;
            }

            Debug.LogError(
                $"Merlin Condition Step 19 failed for '{condition.name}'. "
                + $"Dipstick centered={dipstickCentered} (X {dipstickLocal.x:F3}, center {bounds.center.x:F3}); "
                + $"cap centered={fillerCentered} (X {fillerLocal.x:F3}); "
                + $"length separation valid={separatedAlongLength} (dipstick Z {dipstickLocal.z:F3}, cap Z {fillerLocal.z:F3}); "
                + $"dipstick inside length={dipstickInsideLength}; cap inside length={fillerInsideLength}; "
                + $"dipstick protrusion={dipstickProtrusion:F3} valid={dipstickExposureValid}; "
                + $"cap protrusion={fillerProtrusion:F3} valid={fillerExposureValid}; "
                + $"dipstick trigger={dipstickTriggerValid}; cap trigger={fillerTriggerValid}.",
                condition);
            passed = false;
        }

        private static void EnsureSingleBoxTrigger(
            GameObject root,
            Vector3 center,
            Vector3 size)
        {
            BoxCollider rootBox = root.GetComponent<BoxCollider>();
            if (rootBox == null)
            {
                rootBox = Undo.AddComponent<BoxCollider>(root);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = colliders.Length - 1; index >= 0; index--)
            {
                Collider collider = colliders[index];
                if (collider == null || collider == rootBox)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(collider);
            }

            Undo.RecordObject(rootBox, "Configure Merlin top-center service trigger");
            rootBox.enabled = true;
            rootBox.isTrigger = true;
            rootBox.center = center;
            rootBox.size = size;
            EditorUtility.SetDirty(rootBox);
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

            Undo.RecordObject(part, "Resize Merlin top-center oil hardware");
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

                EncapsulateRendererBounds(engineCore, renderer, ref result, ref initialized);
            }

            if (!initialized || result.size.sqrMagnitude < 0.01f)
            {
                result = new Bounds(Vector3.zero, new Vector3(2f, 1.4f, 2.8f));
            }
            return result;
        }

        private static float CalculateRenderedLocalTop(
            Transform engineCore,
            Renderer[] renderers)
        {
            float top = float.NegativeInfinity;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
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
                            top = Mathf.Max(top, local.y);
                        }
                    }
                }
            }

            return float.IsNegativeInfinity(top)
                ? float.PositiveInfinity
                : top;
        }

        private static void EncapsulateRendererBounds(
            Transform root,
            Renderer renderer,
            ref Bounds result,
            ref bool initialized)
        {
            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 local = root.InverseTransformPoint(
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
