using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinOilHardwareCowlingClearanceHardeningSetup
    {
        private static readonly Vector3 DipstickColliderSize =
            new Vector3(0.16f, 0.30f, 0.16f);
        private static readonly Vector3 FillerColliderSize =
            new Vector3(0.18f, 0.16f, 0.18f);

        [MenuItem("Hanger 51/Merlin Condition/14 - Force Final Oil Hardware Cowling Clearance")]
        public static void ForceFinalOilHardwareCowlingClearance()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 14 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 14 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 14 failed. No engine condition systems were found.");
                return;
            }

            int adjusted = 0;
            GameObject selected = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null || !AdjustCondition(condition))
                {
                    continue;
                }

                adjusted++;
                if (selected == null && condition.gameObject.activeInHierarchy)
                {
                    selected = condition.gameObject;
                }
            }

            if (adjusted == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 14 failed. No complete oil-service hardware could be adjusted. Run Steps 7, 9, and 10 first.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 14 changed the hardware but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 14 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 14 complete. Forced lower, smaller oil-cap and dipstick geometry with exact trigger-only colliders on {adjusted} engine setup(s), including the complete-engine shipment template.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/15 - Validate Forced Oil Hardware Cowling Clearance")]
        public static void ValidateForcedOilHardwareCowlingClearance()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 15 failed: no engine condition systems exist.");
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
                Debug.LogError("Merlin Condition Step 15 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 15 passed. Every current engine and the shipment template has visibly cowling-clear oil hardware with exact trigger-only interaction colliders.");
            }
        }

        private static bool AdjustCondition(EngineConditionController condition)
        {
            if (!TryGetHardware(
                    condition,
                    out EngineAssemblyStation station,
                    out EngineAssemblyTransportController transport,
                    out GameObject engineCore,
                    out EngineDipstickController dipstick,
                    out EngineConditionInspectionTarget filler))
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its station, engine block, dipstick, or oil filler is incomplete.",
                    condition);
                return false;
            }

            Bounds bounds = CalculateCoreBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true),
                dipstick.transform,
                filler.transform);

            Vector3 dipstickLocal = bounds.center + new Vector3(
                -bounds.extents.x * 0.72f,
                bounds.extents.y * 0.40f,
                -bounds.extents.z * 0.18f);
            dipstick.transform.SetPositionAndRotation(
                engineCore.transform.TransformPoint(dipstickLocal),
                engineCore.transform.rotation);
            SetPart(
                dipstick.transform,
                "Yellow Dipstick Handle",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0.052f, 0.013f, 0.052f));
            SetPart(
                dipstick.transform,
                "Dipstick Handle Center",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0.022f, 0.015f, 0.022f));
            EnsureSingleBoxTrigger(
                dipstick.gameObject,
                new Vector3(0f, -0.065f, 0f),
                DipstickColliderSize);

            Vector3 fillerLocal = bounds.center + new Vector3(
                bounds.extents.x * 0.62f,
                bounds.extents.y * 0.38f,
                -bounds.extents.z * 0.10f);
            filler.transform.SetPositionAndRotation(
                engineCore.transform.TransformPoint(fillerLocal),
                engineCore.transform.rotation);
            SetPart(
                filler.transform,
                "Oil Filler Neck",
                Vector3.zero,
                new Vector3(0.048f, 0.048f, 0.048f));
            SetPart(
                filler.transform,
                "Yellow Oil Filler Cap",
                new Vector3(0f, 0.042f, 0f),
                new Vector3(0.062f, 0.018f, 0.062f));
            SetPart(
                filler.transform,
                "Oil Cap Grip Bar 1",
                new Vector3(0f, 0.067f, 0f),
                new Vector3(0.080f, 0.014f, 0.022f));
            SetPart(
                filler.transform,
                "Oil Cap Grip Bar 2",
                new Vector3(0f, 0.067f, 0f),
                new Vector3(0.022f, 0.014f, 0.080f));
            EnsureSingleBoxTrigger(
                filler.gameObject,
                new Vector3(0f, 0.005f, 0f),
                FillerColliderSize);

            EditorUtility.SetDirty(dipstick);
            EditorUtility.SetDirty(filler);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
            return true;
        }

        private static void EnsureSingleBoxTrigger(
            GameObject root,
            Vector3 center,
            Vector3 size)
        {
            // Add the required replacement first. EngineConditionInspectionTarget
            // requires a Collider, so deleting an unexpected old collider before
            // the replacement exists can be rejected by Unity.
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

            rootBox.isTrigger = true;
            rootBox.enabled = true;
            rootBox.center = center;
            rootBox.size = size;
            EditorUtility.SetDirty(rootBox);
        }

        private static void ValidateCondition(
            EngineConditionController condition,
            ref bool passed)
        {
            if (!TryGetHardware(
                    condition,
                    out _,
                    out _,
                    out GameObject engineCore,
                    out EngineDipstickController dipstick,
                    out EngineConditionInspectionTarget filler))
            {
                Debug.LogError(
                    $"Merlin Condition Step 15 failed: '{condition.name}' is missing its station, engine block, dipstick, or oil filler.",
                    condition);
                passed = false;
                return;
            }

            Bounds coreBounds = CalculateCoreBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true),
                dipstick.transform,
                filler.transform);
            float allowedVisualTop =
                coreBounds.center.y + coreBounds.extents.y * 0.72f;
            float dipstickVisualTop = CalculateRenderedLocalTop(
                engineCore.transform,
                dipstick.transform.GetComponentsInChildren<Renderer>(true));
            float fillerVisualTop = CalculateRenderedLocalTop(
                engineCore.transform,
                filler.transform.GetComponentsInChildren<Renderer>(true));

            Transform handle = FindDescendant(
                dipstick.transform,
                "Yellow Dipstick Handle");
            Transform cap = FindDescendant(
                filler.transform,
                "Yellow Oil Filler Cap");
            BoxCollider dipstickCollider = dipstick.GetComponent<BoxCollider>();
            BoxCollider fillerCollider = filler.GetComponent<BoxCollider>();

            bool handleValid = handle != null
                && Mathf.Abs(handle.localScale.x) <= 0.055f
                && Mathf.Abs(handle.localScale.z) <= 0.055f;
            bool capValid = cap != null
                && Mathf.Abs(cap.localScale.x) <= 0.065f
                && Mathf.Abs(cap.localScale.z) <= 0.065f;
            bool dipstickHeightValid =
                dipstickVisualTop <= allowedVisualTop + 0.001f;
            bool fillerHeightValid =
                fillerVisualTop <= allowedVisualTop + 0.001f;
            bool dipstickTriggerValid = ValidateExactTrigger(
                dipstickCollider,
                DipstickColliderSize);
            bool fillerTriggerValid = ValidateExactTrigger(
                fillerCollider,
                FillerColliderSize);
            bool dipstickColliderTreeValid = HasOnlyRootTrigger(dipstick.transform);
            bool fillerColliderTreeValid = HasOnlyRootTrigger(filler.transform);

            if (handleValid
                && capValid
                && dipstickHeightValid
                && fillerHeightValid
                && dipstickTriggerValid
                && fillerTriggerValid
                && dipstickColliderTreeValid
                && fillerColliderTreeValid)
            {
                return;
            }

            Debug.LogError(
                $"Merlin Condition Step 15 failed for '{condition.name}'. "
                + $"handle={handleValid}; cap={capValid}; "
                + $"dipstickTop={dipstickVisualTop:F4}; fillerTop={fillerVisualTop:F4}; allowedTop={allowedVisualTop:F4}; "
                + $"dipstickTrigger={dipstickTriggerValid}; fillerTrigger={fillerTriggerValid}; "
                + $"dipstickColliderTree={dipstickColliderTreeValid}; fillerColliderTree={fillerColliderTreeValid}.",
                condition);
            passed = false;
        }

        private static bool TryGetHardware(
            EngineConditionController condition,
            out EngineAssemblyStation station,
            out EngineAssemblyTransportController transport,
            out GameObject engineCore,
            out EngineDipstickController dipstick,
            out EngineConditionInspectionTarget filler)
        {
            station = condition != null
                ? condition.GetComponent<EngineAssemblyStation>()
                : null;
            transport = condition != null
                ? condition.GetComponent<EngineAssemblyTransportController>()
                : null;
            engineCore = null;
            dipstick = null;
            filler = null;

            if (station == null || transport == null || transport.TransportRoot == null)
            {
                return false;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            dipstick = transport.TransportRoot
                .GetComponentInChildren<EngineDipstickController>(true);
            filler = FindOilFiller(transport.TransportRoot);
            return engineCore != null && dipstick != null && filler != null;
        }

        private static bool ValidateExactTrigger(
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

        private static bool HasOnlyRootTrigger(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            return colliders.Length == 1
                && colliders[0] != null
                && colliders[0].transform == root
                && colliders[0].enabled
                && colliders[0].isTrigger;
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

        private static void SetPart(
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

                EncapsulateRendererBounds(
                    engineCore,
                    renderer,
                    ref result,
                    ref initialized);
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
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 local = engineCore.InverseTransformPoint(
                                SelectCorner(world, x, y, z));
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
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 local = root.InverseTransformPoint(
                            SelectCorner(world, x, y, z));
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

        private static Vector3 SelectCorner(Bounds bounds, int x, int y, int z)
        {
            return new Vector3(
                x == 0 ? bounds.min.x : bounds.max.x,
                y == 0 ? bounds.min.y : bounds.max.y,
                z == 0 ? bounds.min.z : bounds.max.z);
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
