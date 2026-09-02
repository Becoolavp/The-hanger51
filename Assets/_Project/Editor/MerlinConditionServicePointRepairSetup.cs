using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinConditionServicePointRepairSetup
    {
        private const string BlockPointName =
            "Engine Block Condition Inspection Point";
        private const string LeftCoverPointName =
            "Left Cover Condition Inspection Point";
        private const string RightCoverPointName =
            "Right Cover Condition Inspection Point";

        [MenuItem("Hanger 51/Merlin Condition/5 - Repair Service Points and Inspection Colliders")]
        public static void RepairServicePointsAndInspectionColliders()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "Merlin Condition Step 5 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError(
                    "Merlin Condition Step 5 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 5 failed. Run Merlin Condition Step 1 first.");
                return;
            }

            int repaired = 0;
            GameObject selectedObject = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition != null && RepairCondition(condition))
                {
                    repaired++;
                    if (selectedObject == null && condition.gameObject.activeInHierarchy)
                    {
                        selectedObject = condition.gameObject;
                    }
                }
            }

            if (repaired == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 5 failed. No complete engine condition setup could be repaired.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError(
                    "Merlin Condition Step 5 repaired the engines but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Merlin Condition Step 5 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selectedObject != null
                ? selectedObject
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 5 complete. Repaired {repaired} engine condition setup(s): removed solid oversized inspection boxes, added small trigger-only inspection points, and anchored the dipstick and oil filler directly to each portable engine block.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/6 - Validate Service Points and Inspection Colliders")]
        public static void ValidateServicePointsAndInspectionColliders()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 6 failed: no engine condition systems exist.");
                passed = false;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                ValidateCondition(condition, ref passed);
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError(
                    "Merlin Condition Step 6 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 6 passed. Every current engine and complete-engine shipment template has one block-anchored dipstick, one block-anchored filler, small trigger-only inspection points, and no oversized solid inspection followers.");
            }
        }

        private static bool RepairCondition(EngineConditionController condition)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its station or portable engine root is missing.",
                    condition);
                return false;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            GameObject[] covers = GetObjectArray<GameObject>(
                serializedStation,
                "cylinderCoverVisuals");
            if (engineCore == null || covers.Length < 2
                || covers[0] == null || covers[1] == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its engine-block or cover visuals are incomplete.",
                    condition);
                return false;
            }

            RemoveBroadFollowers(transport.TransportRoot);
            RemoveNamedChild(engineCore.transform, BlockPointName);
            RemoveNamedChild(covers[0].transform, LeftCoverPointName);
            RemoveNamedChild(covers[1].transform, RightCoverPointName);

            EngineDipstickController dipstick =
                KeepSingleDipstick(transport.TransportRoot);
            EngineConditionInspectionTarget filler =
                KeepSingleOilFiller(transport.TransportRoot);
            if (dipstick == null || filler == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its dipstick or oil filler was not generated. Rerun Merlin Condition Step 1, then Step 5.",
                    condition);
                return false;
            }

            Bounds blockBounds = CalculateLocalBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true));
            AnchorDipstick(dipstick, engineCore.transform, blockBounds);
            AnchorFiller(filler, engineCore.transform, blockBounds);

            CreateInspectionPoint(
                engineCore.transform,
                BlockPointName,
                blockBounds,
                new Vector3(-0.48f, 0.12f, -0.28f),
                condition,
                EngineConditionInspectionKind.EngineBlock,
                0,
                0.42f);

            for (int index = 0; index < 2; index++)
            {
                Bounds coverBounds = CalculateLocalBounds(
                    covers[index].transform,
                    covers[index].GetComponentsInChildren<Renderer>(true));
                CreateInspectionPoint(
                    covers[index].transform,
                    index == 0 ? LeftCoverPointName : RightCoverPointName,
                    coverBounds,
                    new Vector3(0f, 0.72f, 0f),
                    condition,
                    EngineConditionInspectionKind.CylinderCover,
                    index,
                    0.34f);
            }

            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
            return true;
        }

        private static void RemoveBroadFollowers(Transform portableRoot)
        {
            EngineConditionInspectionFollower[] followers =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionFollower>(true);
            for (int index = followers.Length - 1; index >= 0; index--)
            {
                EngineConditionInspectionFollower follower = followers[index];
                if (follower != null)
                {
                    Undo.DestroyObjectImmediate(follower.gameObject);
                }
            }
        }

        private static EngineDipstickController KeepSingleDipstick(
            Transform portableRoot)
        {
            EngineDipstickController[] dipsticks =
                portableRoot.GetComponentsInChildren<EngineDipstickController>(true);
            EngineDipstickController keeper = null;
            for (int index = 0; index < dipsticks.Length; index++)
            {
                EngineDipstickController candidate = dipsticks[index];
                if (candidate == null)
                {
                    continue;
                }

                if (keeper == null)
                {
                    keeper = candidate;
                }
                else
                {
                    Undo.DestroyObjectImmediate(candidate.gameObject);
                }
            }
            return keeper;
        }

        private static EngineConditionInspectionTarget KeepSingleOilFiller(
            Transform portableRoot)
        {
            EngineConditionInspectionTarget[] targets =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            EngineConditionInspectionTarget keeper = null;
            for (int index = 0; index < targets.Length; index++)
            {
                EngineConditionInspectionTarget candidate = targets[index];
                if (candidate == null
                    || candidate.InspectionKind != EngineConditionInspectionKind.OilFiller)
                {
                    continue;
                }

                if (keeper == null)
                {
                    keeper = candidate;
                }
                else
                {
                    Undo.DestroyObjectImmediate(candidate.gameObject);
                }
            }
            return keeper;
        }

        private static void AnchorDipstick(
            EngineDipstickController dipstick,
            Transform engineCore,
            Bounds blockBounds)
        {
            Transform target = dipstick.transform;
            Undo.SetTransformParent(target, engineCore, "Anchor Merlin dipstick to engine block");
            target.localPosition = blockBounds.center + new Vector3(
                -blockBounds.extents.x * 0.42f,
                blockBounds.extents.y * 0.54f,
                blockBounds.extents.z * 0.10f);
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.10f, 0f);
            collider.size = new Vector3(0.26f, 0.56f, 0.26f);
            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(target);
        }

        private static void AnchorFiller(
            EngineConditionInspectionTarget filler,
            Transform engineCore,
            Bounds blockBounds)
        {
            Transform target = filler.transform;
            Undo.SetTransformParent(target, engineCore, "Anchor Merlin oil filler to engine block");
            target.localPosition = blockBounds.center + new Vector3(
                blockBounds.extents.x * 0.38f,
                blockBounds.extents.y * 0.57f,
                blockBounds.extents.z * 0.03f);
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.10f, 0f);
            collider.size = new Vector3(0.34f, 0.34f, 0.34f);
            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(target);
        }

        private static void CreateInspectionPoint(
            Transform parent,
            string name,
            Bounds localBounds,
            Vector3 normalizedOffset,
            EngineConditionController condition,
            EngineConditionInspectionKind kind,
            int partIndex,
            float maximumSize)
        {
            GameObject target = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(target, "Create small engine inspection point");
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localBounds.center + new Vector3(
                localBounds.extents.x * normalizedOffset.x,
                localBounds.extents.y * normalizedOffset.y,
                localBounds.extents.z * normalizedOffset.z);
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;

            float size = Mathf.Clamp(
                Mathf.Min(localBounds.size.x, Mathf.Min(localBounds.size.y, localBounds.size.z))
                    * 0.30f,
                0.20f,
                maximumSize);
            BoxCollider collider = Undo.AddComponent<BoxCollider>(target);
            collider.isTrigger = true;
            collider.size = Vector3.one * size;

            EngineConditionInspectionTarget inspection =
                Undo.AddComponent<EngineConditionInspectionTarget>(target);
            inspection.Configure(condition, kind, partIndex);
            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(inspection);
        }

        private static Bounds CalculateLocalBounds(
            Transform root,
            Renderer[] renderers)
        {
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.one * 0.5f);
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

                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 localCorner = root.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? min.x : max.x,
                                    y == 0 ? min.y : max.y,
                                    z == 0 ? min.z : max.z));
                            if (!initialized)
                            {
                                result = new Bounds(localCorner, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            if (!initialized || result.size.sqrMagnitude < 0.01f)
            {
                result = new Bounds(Vector3.zero, new Vector3(1.8f, 1.2f, 2.6f));
            }
            return result;
        }

        private static void ValidateCondition(
            EngineConditionController condition,
            ref bool passed)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 6 failed: '{condition.name}' has no valid station or portable root.",
                    condition);
                passed = false;
                return;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            GameObject[] covers = GetObjectArray<GameObject>(
                serializedStation,
                "cylinderCoverVisuals");
            if (engineCore == null || covers.Length < 2
                || covers[0] == null || covers[1] == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 6 failed: '{condition.name}' has incomplete engine visuals.",
                    condition);
                passed = false;
                return;
            }

            EngineConditionInspectionFollower[] followers =
                transport.TransportRoot.GetComponentsInChildren<EngineConditionInspectionFollower>(true);
            if (followers.Length > 0)
            {
                Debug.LogError(
                    $"Merlin Condition Step 6 failed: '{condition.name}' still has {followers.Length} broad inspection follower(s).",
                    condition);
                passed = false;
            }

            EngineDipstickController[] dipsticks =
                transport.TransportRoot.GetComponentsInChildren<EngineDipstickController>(true);
            if (dipsticks.Length != 1
                || !dipsticks[0].transform.IsChildOf(engineCore.transform)
                || !ValidateTriggerCollider(dipsticks[0].GetComponent<BoxCollider>(), 0.70f))
            {
                Debug.LogError(
                    $"Merlin Condition Step 6 failed: '{condition.name}' does not have one small, trigger-only, block-anchored dipstick.",
                    condition);
                passed = false;
            }

            EngineConditionInspectionTarget[] targets =
                transport.TransportRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            List<EngineConditionInspectionTarget> fillers =
                new List<EngineConditionInspectionTarget>();
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null
                    && targets[index].InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    fillers.Add(targets[index]);
                }
            }

            if (fillers.Count != 1
                || !fillers[0].transform.IsChildOf(engineCore.transform)
                || !ValidateTriggerCollider(fillers[0].GetComponent<BoxCollider>(), 0.55f))
            {
                Debug.LogError(
                    $"Merlin Condition Step 6 failed: '{condition.name}' does not have one small, trigger-only, block-anchored oil filler.",
                    condition);
                passed = false;
            }

            ValidateNamedPoint(
                engineCore.transform,
                BlockPointName,
                condition,
                ref passed);
            ValidateNamedPoint(
                covers[0].transform,
                LeftCoverPointName,
                condition,
                ref passed);
            ValidateNamedPoint(
                covers[1].transform,
                RightCoverPointName,
                condition,
                ref passed);
        }

        private static void ValidateNamedPoint(
            Transform parent,
            string name,
            EngineConditionController condition,
            ref bool passed)
        {
            Transform point = FindDirectChild(parent, name);
            BoxCollider collider = point != null
                ? point.GetComponent<BoxCollider>()
                : null;
            if (point == null || !ValidateTriggerCollider(collider, 0.55f))
            {
                Debug.LogError(
                    $"Merlin Condition Step 6 failed: '{condition.name}' is missing the small trigger-only '{name}'.",
                    condition);
                passed = false;
            }
        }

        private static bool ValidateTriggerCollider(
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

        private static void RemoveNamedChild(Transform parent, string name)
        {
            Transform existing = FindDirectChild(parent, name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }
            return null;
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

        private static T[] GetObjectArray<T>(
            SerializedObject serialized,
            string propertyName)
            where T : Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return new T[0];
            }

            T[] result = new T[property.arraySize];
            for (int index = 0; index < property.arraySize; index++)
            {
                result[index] = property
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as T;
            }
            return result;
        }
    }
}
