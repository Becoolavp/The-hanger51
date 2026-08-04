using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinWearOverboostAndCoverServiceSetup
    {
        private const float PlugWearPerHour = 0.20f;
        private const float OverboostThreshold = 0.95f;
        private const float OverboostGraceSeconds = 60f;
        private const float PrimaryDamagePerMinute = 55f;
        private const float SecondaryDelaySeconds = 45f;
        private const float SecondaryDamagePerMinute = 24f;
        private const float ExposureCooldownPerSecond = 0.75f;

        [MenuItem("Hanger 51/Merlin Condition/9 - Add Slow Plug Wear, Overboost Failures, and Cover Service")]
        public static void AddSlowPlugWearOverboostAndCoverService()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "Merlin Condition Step 9 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError(
                    "Merlin Condition Step 9 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 9 failed. Run Merlin Condition Step 1 first.");
                return;
            }

            int configured = 0;
            int hardwareAdjusted = 0;
            GameObject selected = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null || !ConfigureCondition(condition))
                {
                    continue;
                }

                configured++;
                if (TuneOilServiceHardware(condition))
                {
                    hardwareAdjusted++;
                }

                if (selected == null && condition.gameObject.activeInHierarchy)
                {
                    selected = condition.gameObject;
                }
            }

            if (configured == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 9 failed. No complete engine condition setup could be updated.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError(
                    "Merlin Condition Step 9 updated the engines but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Merlin Condition Step 9 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 9 complete. Configured {configured} engine(s) for precise very-slow plug wear, sustained 95%+ overboost cover failures, combined cover removal/inspection targets, and reduced oil-service hardware. Adjusted visible hardware on {hardwareAdjusted} engine(s), including the complete-engine shipment template.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/10 - Validate Slow Plug Wear, Overboost Failures, and Cover Service")]
        public static void ValidateSlowPlugWearOverboostAndCoverService()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 10 failed: no engine condition systems exist.");
                passed = false;
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
                Debug.LogError(
                    "Merlin Condition Step 10 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 10 passed. Precise slow plug wear, sustained 95%+ overboost damage, removable/inspectable covers, and cowling-clear oil-service hardware are configured on every current engine and the shipment template.");
            }
        }

        [MenuItem("Hanger 51/Merlin Condition/11 - Prime Selected Engine for Overboost Test")]
        public static void PrimeSelectedEngineForOverboostTest()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "Prime the overboost test outside Play mode, then enter Play mode to run the engine.");
                return;
            }

            EngineConditionController condition = FindSelectedCondition();
            if (condition == null)
            {
                Debug.LogError(
                    "Select an engine, its portable root, or its condition object first.");
                return;
            }

            EngineWearAndOverboostController wear =
                condition.GetComponent<EngineWearAndOverboostController>();
            if (wear == null)
            {
                Debug.LogError(
                    "The selected engine has no current wear/overboost controller. Run Merlin Condition Step 9 first.",
                    condition);
                return;
            }

            wear.PrimeForOverboostTest();
            EditorUtility.SetDirty(wear);
            EditorUtility.SetDirty(condition);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                "Selected engine primed for a short overboost validation. Install it, start it, and hold at least 95% throttle; the primary cover should crack after roughly 10–20 seconds. Reload the scene or restore the engine afterward.",
                condition);
        }

        private static bool ConfigureCondition(EngineConditionController condition)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its station or portable root is incomplete.",
                    condition);
                return false;
            }

            // Disable the original per-frame wear subtraction. The precise
            // controller accumulates tiny fractions and applies them in 0.001%
            // steps so very slow wear cannot be rounded away by float precision.
            SerializedObject serializedCondition = new SerializedObject(condition);
            SetFloat(serializedCondition, "sparkPlugWearPerRunningHour", 0f);
            serializedCondition.ApplyModifiedPropertiesWithoutUndo();

            EngineWearAndOverboostController wear =
                condition.GetComponent<EngineWearAndOverboostController>();
            if (wear == null)
            {
                wear = Undo.AddComponent<EngineWearAndOverboostController>(
                    condition.gameObject);
            }
            wear.Configure(
                PlugWearPerHour,
                OverboostThreshold,
                OverboostGraceSeconds,
                PrimaryDamagePerMinute,
                SecondaryDelaySeconds,
                SecondaryDamagePerMinute,
                ExposureCooldownPerSecond);

            EngineAssemblyRemovalController removal =
                station.GetComponent<EngineAssemblyRemovalController>();
            if (removal == null)
            {
                removal = Undo.AddComponent<EngineAssemblyRemovalController>(
                    station.gameObject);
            }

            RemoveSeparateCoverInspectionPoints(transport.TransportRoot);

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            int configuredCovers = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target == null
                    || target.InteractionKind
                        != EngineAssemblyInteractionKind.CoverPlacement
                    || target.GroupIndex < 0
                    || target.GroupIndex > 1)
                {
                    continue;
                }

                EngineConditionInspectionTarget inspection =
                    target.GetComponent<EngineConditionInspectionTarget>();
                if (inspection == null)
                {
                    inspection = Undo.AddComponent<EngineConditionInspectionTarget>(
                        target.gameObject);
                }
                inspection.Configure(
                    condition,
                    EngineConditionInspectionKind.CylinderCover,
                    target.GroupIndex);
                target.RefreshFromStation();
                EditorUtility.SetDirty(inspection);
                EditorUtility.SetDirty(target);
                configuredCovers++;
            }

            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(wear);
            EditorUtility.SetDirty(removal);
            return configuredCovers >= 2;
        }

        private static void RemoveSeparateCoverInspectionPoints(Transform portableRoot)
        {
            EngineConditionInspectionTarget[] inspections =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            for (int index = inspections.Length - 1; index >= 0; index--)
            {
                EngineConditionInspectionTarget inspection = inspections[index];
                if (inspection == null
                    || inspection.InspectionKind
                        != EngineConditionInspectionKind.CylinderCover
                    || inspection.GetComponent<EngineAssemblyInteractionTarget>() != null)
                {
                    continue;
                }

                string lowerName = inspection.name.ToLowerInvariant();
                bool dedicatedGeneratedObject = lowerName.Contains("condition inspection")
                    || lowerName.Contains("inspection point");
                if (dedicatedGeneratedObject)
                {
                    Undo.DestroyObjectImmediate(inspection.gameObject);
                    continue;
                }

                Collider collider = inspection.GetComponent<Collider>();
                Undo.DestroyObjectImmediate(inspection);
                if (collider != null && collider.isTrigger)
                {
                    Undo.DestroyObjectImmediate(collider);
                }
            }
        }

        private static bool TuneOilServiceHardware(
            EngineConditionController condition)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
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
            EngineConditionInspectionTarget filler =
                FindOilFiller(transport.TransportRoot);
            if (dipstick == null || filler == null)
            {
                Debug.LogWarning(
                    $"'{condition.name}' has no visible dipstick or filler to resize. Run Merlin Condition Step 7, then rerun Step 9.",
                    condition);
                return false;
            }

            Bounds bounds = CalculateLocalBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true));

            dipstick.transform.position = engineCore.transform.TransformPoint(
                bounds.center + new Vector3(
                    -bounds.extents.x * 0.72f,
                    bounds.extents.y * 0.86f,
                    -bounds.extents.z * 0.18f));
            dipstick.transform.rotation = engineCore.transform.rotation;

            SetPartTransform(
                dipstick.transform,
                "Yellow Dipstick Handle",
                new Vector3(0f, 0.065f, 0f),
                new Vector3(0.115f, 0.028f, 0.115f));
            SetPartTransform(
                dipstick.transform,
                "Dipstick Handle Center",
                new Vector3(0f, 0.065f, 0f),
                new Vector3(0.050f, 0.032f, 0.050f));

            BoxCollider dipstickCollider = dipstick.GetComponent<BoxCollider>();
            if (dipstickCollider != null)
            {
                dipstickCollider.isTrigger = true;
                dipstickCollider.center = new Vector3(0f, 0.02f, 0f);
                dipstickCollider.size = new Vector3(0.24f, 0.42f, 0.24f);
                EditorUtility.SetDirty(dipstickCollider);
            }

            filler.transform.position = engineCore.transform.TransformPoint(
                bounds.center + new Vector3(
                    bounds.extents.x * 0.62f,
                    bounds.extents.y * 0.84f,
                    -bounds.extents.z * 0.10f));
            filler.transform.rotation = engineCore.transform.rotation;

            SetPartTransform(
                filler.transform,
                "Oil Filler Neck",
                Vector3.zero,
                new Vector3(0.090f, 0.090f, 0.090f));
            SetPartTransform(
                filler.transform,
                "Yellow Oil Filler Cap",
                new Vector3(0f, 0.10f, 0f),
                new Vector3(0.135f, 0.045f, 0.135f));
            SetPartTransform(
                filler.transform,
                "Oil Cap Grip Bar 1",
                new Vector3(0f, 0.155f, 0f),
                new Vector3(0.18f, 0.035f, 0.045f));
            SetPartTransform(
                filler.transform,
                "Oil Cap Grip Bar 2",
                new Vector3(0f, 0.155f, 0f),
                new Vector3(0.045f, 0.035f, 0.18f));

            BoxCollider fillerCollider = filler.GetComponent<BoxCollider>();
            if (fillerCollider != null)
            {
                fillerCollider.isTrigger = true;
                fillerCollider.center = new Vector3(0f, 0.04f, 0f);
                fillerCollider.size = new Vector3(0.28f, 0.26f, 0.28f);
                EditorUtility.SetDirty(fillerCollider);
            }

            EditorUtility.SetDirty(dipstick);
            EditorUtility.SetDirty(filler);
            return true;
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

        private static void ValidateCondition(
            EngineConditionController condition,
            ref bool passed)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            EngineWearAndOverboostController wear =
                condition.GetComponent<EngineWearAndOverboostController>();
            EngineAssemblyRemovalController removal =
                station != null
                    ? station.GetComponent<EngineAssemblyRemovalController>()
                    : null;

            if (station == null
                || transport == null
                || transport.TransportRoot == null
                || wear == null
                || removal == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 10 failed: '{condition.name}' is missing its station, portable root, removal controller, or wear/overboost controller.",
                    condition);
                passed = false;
                return;
            }

            SerializedObject serializedCondition = new SerializedObject(condition);
            SerializedProperty legacyWear =
                serializedCondition.FindProperty("sparkPlugWearPerRunningHour");
            if (legacyWear == null || Mathf.Abs(legacyWear.floatValue) > 0.0001f)
            {
                Debug.LogError(
                    $"Merlin Condition Step 10 failed: '{condition.name}' still has legacy per-frame plug wear enabled.",
                    condition);
                passed = false;
            }

            if (Mathf.Abs(wear.SparkPlugWearPerRunningHour - PlugWearPerHour) > 0.001f
                || Mathf.Abs(wear.OverboostThrottleThreshold - OverboostThreshold) > 0.001f
                || Mathf.Abs(wear.OverboostGraceSeconds - OverboostGraceSeconds) > 0.01f)
            {
                Debug.LogError(
                    $"Merlin Condition Step 10 failed: '{condition.name}' has incorrect wear or overboost settings.",
                    wear);
                passed = false;
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            bool[] foundCover = new bool[2];
            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target == null
                    || target.InteractionKind
                        != EngineAssemblyInteractionKind.CoverPlacement
                    || target.GroupIndex < 0
                    || target.GroupIndex > 1)
                {
                    continue;
                }

                EngineConditionInspectionTarget inspection =
                    target.GetComponent<EngineConditionInspectionTarget>();
                Collider collider = target.GetComponent<Collider>();
                if (inspection == null
                    || inspection.Condition != condition
                    || inspection.InspectionKind
                        != EngineConditionInspectionKind.CylinderCover
                    || inspection.PartIndex != target.GroupIndex
                    || collider == null
                    || !collider.enabled)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 10 failed: '{condition.name}' cover target {target.GroupIndex} does not combine removal, installation, and condition inspection.",
                        target);
                    passed = false;
                }
                foundCover[target.GroupIndex] = true;
            }

            if (!foundCover[0] || !foundCover[1])
            {
                Debug.LogError(
                    $"Merlin Condition Step 10 failed: '{condition.name}' does not expose both cover service targets.",
                    condition);
                passed = false;
            }

            EngineConditionInspectionTarget[] inspections =
                transport.TransportRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            for (int index = 0; index < inspections.Length; index++)
            {
                EngineConditionInspectionTarget inspection = inspections[index];
                if (inspection != null
                    && inspection.InspectionKind
                        == EngineConditionInspectionKind.CylinderCover
                    && inspection.GetComponent<EngineAssemblyInteractionTarget>() == null)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 10 failed: '{condition.name}' still has a separate cover inspection collider that can interfere with removal.",
                        inspection);
                    passed = false;
                }
            }

            EngineDipstickController dipstick =
                transport.TransportRoot.GetComponentInChildren<EngineDipstickController>(true);
            EngineConditionInspectionTarget filler =
                FindOilFiller(transport.TransportRoot);
            Transform handle = dipstick != null
                ? FindDescendant(dipstick.transform, "Yellow Dipstick Handle")
                : null;
            Transform cap = filler != null
                ? FindDescendant(filler.transform, "Yellow Oil Filler Cap")
                : null;

            if (handle == null
                || handle.localScale.x > 0.125f
                || handle.localScale.z > 0.125f
                || cap == null
                || cap.localScale.x > 0.145f
                || cap.localScale.z > 0.145f)
            {
                Debug.LogError(
                    $"Merlin Condition Step 10 failed: '{condition.name}' oil cap or dipstick handle is not scaled for cowling clearance.",
                    condition);
                passed = false;
            }
        }

        private static EngineConditionInspectionTarget FindOilFiller(
            Transform portableRoot)
        {
            EngineConditionInspectionTarget[] targets =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null
                    && targets[index].InspectionKind
                        == EngineConditionInspectionKind.OilFiller)
                {
                    return targets[index];
                }
            }
            return null;
        }

        private static Bounds CalculateLocalBounds(
            Transform root,
            Renderer[] renderers)
        {
            bool initialized = false;
            Bounds result = new Bounds(
                Vector3.zero,
                new Vector3(2f, 1.4f, 2.8f));
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
                    && inspection.InspectionKind
                        == EngineConditionInspectionKind.OilFiller)
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
                result = new Bounds(
                    Vector3.zero,
                    new Vector3(2f, 1.4f, 2.8f));
            }
            return result;
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

        private static EngineConditionController FindSelectedCondition()
        {
            if (Selection.activeGameObject != null)
            {
                EngineConditionController direct =
                    Selection.activeGameObject.GetComponentInParent<EngineConditionController>();
                if (direct != null)
                {
                    return direct;
                }

                EngineConditionLink link =
                    Selection.activeGameObject.GetComponentInParent<EngineConditionLink>();
                if (link != null)
                {
                    return link.Condition;
                }
            }

            EngineConditionController[] found =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
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

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }
    }
}
