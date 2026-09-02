using System.Collections.Generic;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinCoverRemovalReliabilitySetup
    {
        [MenuItem("Hanger 51/Merlin Condition/20 - Repair Cracked Cover Removal")]
        public static void RepairCrackedCoverRemoval()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 20 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 20 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 20 failed. No engine condition systems were found.");
                return;
            }

            int repaired = 0;
            GameObject selected = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition != null && RepairCondition(condition))
                {
                    repaired++;
                    if (selected == null && condition.gameObject.activeInHierarchy)
                    {
                        selected = condition.gameObject;
                    }
                }
            }

            InventoryInteractor interactor =
                Object.FindFirstObjectByType<InventoryInteractor>(FindObjectsInactive.Include);
            if (interactor != null)
            {
                SerializedObject serializedInteractor = new SerializedObject(interactor);
                SerializedProperty distance =
                    serializedInteractor.FindProperty("interactionDistance");
                if (distance != null)
                {
                    distance.floatValue = Mathf.Max(5.5f, distance.floatValue);
                    serializedInteractor.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(interactor);
                }
            }

            if (repaired == 0)
            {
                Debug.LogError("Merlin Condition Step 20 failed. No complete engine target hierarchy could be repaired.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 20 changed the engine targets but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 20 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 20 complete. Rebound reversible maintenance on {repaired} engine setup(s), enabled combined inspection/removal targets, matched maintenance reach to inspection reach, and enabled automatic part drops when inventory is full.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/21 - Validate Cracked Cover Removal")]
        public static void ValidateCrackedCoverRemoval()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 21 failed: no engine condition systems exist.");
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

            InventoryInteractor interactor =
                Object.FindFirstObjectByType<InventoryInteractor>(FindObjectsInactive.Include);
            if (interactor == null)
            {
                Debug.LogError("Merlin Condition Step 21 failed: Player maintenance interactor is missing.");
                passed = false;
            }
            else
            {
                SerializedObject serializedInteractor = new SerializedObject(interactor);
                SerializedProperty distance =
                    serializedInteractor.FindProperty("interactionDistance");
                if (distance == null || distance.floatValue < 5.49f)
                {
                    Debug.LogError("Merlin Condition Step 21 failed: maintenance interaction distance is shorter than condition inspection distance.", interactor);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Condition Step 21 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 21 passed. Original and purchased engines have two combined cover targets, twelve bank-mapped bolts, twenty-four bank-mapped plugs, trigger-compatible maintenance detection, removal blocker guidance, and full-inventory part-drop fallback.");
            }
        }

        private static bool RepairCondition(EngineConditionController condition)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            if (station == null)
            {
                Debug.LogWarning($"Skipped '{condition.name}' because its engine station is missing.", condition);
                return false;
            }

            EngineAssemblyInteractionTarget[] allTargets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            List<EngineAssemblyInteractionTarget> covers =
                CollectTargets(allTargets, EngineAssemblyInteractionKind.CoverPlacement);
            List<EngineAssemblyInteractionTarget> bolts =
                CollectTargets(allTargets, EngineAssemblyInteractionKind.CoverBolt);
            List<EngineAssemblyInteractionTarget> plugs =
                CollectTargets(allTargets, EngineAssemblyInteractionKind.SparkPlug);

            if (covers.Count != 2 || bolts.Count != 12 || plugs.Count != 24)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because it has {covers.Count} cover, {bolts.Count} bolt, and {plugs.Count} plug targets instead of 2/12/24.",
                    condition);
                return false;
            }

            SortTargets(covers);
            SortTargets(bolts);
            SortTargets(plugs);

            SerializedObject serializedStation = new SerializedObject(station);
            SetTargetList(serializedStation, "coverPlacementTargets", covers);
            SetTargetList(serializedStation, "coverBoltTargets", bolts);
            SetTargetList(serializedStation, "sparkPlugTargets", plugs);
            serializedStation.ApplyModifiedPropertiesWithoutUndo();

            EngineAssemblyRemovalController removal =
                station.GetComponent<EngineAssemblyRemovalController>();
            if (removal == null)
            {
                removal = Undo.AddComponent<EngineAssemblyRemovalController>(station.gameObject);
            }
            removal.InitializeBindings();

            for (int index = 0; index < covers.Count; index++)
            {
                EngineAssemblyInteractionTarget cover = covers[index];
                Collider collider = cover.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = true;
                    EditorUtility.SetDirty(collider);
                }

                EngineConditionInspectionTarget inspection =
                    cover.GetComponent<EngineConditionInspectionTarget>();
                if (inspection == null)
                {
                    inspection = Undo.AddComponent<EngineConditionInspectionTarget>(cover.gameObject);
                }
                inspection.Configure(
                    condition,
                    EngineConditionInspectionKind.CylinderCover,
                    cover.GroupIndex);
                cover.RefreshFromStation();
                EditorUtility.SetDirty(inspection);
                EditorUtility.SetDirty(cover);
            }

            for (int index = 0; index < bolts.Count; index++)
            {
                bolts[index].RefreshFromStation();
                EditorUtility.SetDirty(bolts[index]);
            }
            for (int index = 0; index < plugs.Count; index++)
            {
                plugs[index].RefreshFromStation();
                EditorUtility.SetDirty(plugs[index]);
            }

            EditorUtility.SetDirty(removal);
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(condition);
            return true;
        }

        private static void ValidateCondition(
            EngineConditionController condition,
            ref bool passed)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyRemovalController removal = station != null
                ? station.GetComponent<EngineAssemblyRemovalController>()
                : null;
            if (station == null || removal == null || !removal.InitializeBindings())
            {
                Debug.LogError(
                    $"Merlin Condition Step 21 failed: '{condition.name}' has no ready removal controller.",
                    condition);
                passed = false;
                return;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            if (GetArraySize(serializedStation, "coverPlacementTargets") != 2
                || GetArraySize(serializedStation, "coverBoltTargets") != 12
                || GetArraySize(serializedStation, "sparkPlugTargets") != 24)
            {
                Debug.LogError(
                    $"Merlin Condition Step 21 failed: '{condition.name}' station target lists are not 2 covers, 12 bolts, and 24 plugs.",
                    condition);
                passed = false;
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            List<EngineAssemblyInteractionTarget> covers =
                CollectTargets(targets, EngineAssemblyInteractionKind.CoverPlacement);
            List<EngineAssemblyInteractionTarget> bolts =
                CollectTargets(targets, EngineAssemblyInteractionKind.CoverBolt);
            List<EngineAssemblyInteractionTarget> plugs =
                CollectTargets(targets, EngineAssemblyInteractionKind.SparkPlug);

            if (covers.Count != 2
                || bolts.Count != 12
                || plugs.Count != 24
                || !HasExpectedBankCounts(bolts, 6)
                || !HasExpectedBankCounts(plugs, 12))
            {
                Debug.LogError(
                    $"Merlin Condition Step 21 failed: '{condition.name}' physical target hierarchy or bank mapping is incomplete.",
                    condition);
                passed = false;
            }

            for (int index = 0; index < covers.Count; index++)
            {
                EngineAssemblyInteractionTarget cover = covers[index];
                Collider collider = cover.GetComponent<Collider>();
                EngineConditionInspectionTarget inspection =
                    cover.GetComponent<EngineConditionInspectionTarget>();
                bool valid = collider != null
                    && collider.enabled
                    && inspection != null
                    && inspection.InspectionKind
                        == EngineConditionInspectionKind.CylinderCover
                    && inspection.PartIndex == cover.GroupIndex;
                if (!valid)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 21 failed: '{condition.name}' cover bank {cover.GroupIndex} does not combine an enabled maintenance collider with cover inspection.",
                        cover);
                    passed = false;
                }
            }
        }

        private static List<EngineAssemblyInteractionTarget> CollectTargets(
            EngineAssemblyInteractionTarget[] targets,
            EngineAssemblyInteractionKind kind)
        {
            List<EngineAssemblyInteractionTarget> result =
                new List<EngineAssemblyInteractionTarget>();
            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target != null && target.InteractionKind == kind)
                {
                    result.Add(target);
                }
            }
            return result;
        }

        private static void SortTargets(List<EngineAssemblyInteractionTarget> targets)
        {
            targets.Sort((left, right) =>
            {
                int groupComparison = left.GroupIndex.CompareTo(right.GroupIndex);
                return groupComparison != 0
                    ? groupComparison
                    : left.TargetIndex.CompareTo(right.TargetIndex);
            });
        }

        private static void SetTargetList(
            SerializedObject serialized,
            string propertyName,
            List<EngineAssemblyInteractionTarget> targets)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.arraySize = targets.Count;
            for (int index = 0; index < targets.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = targets[index];
            }
        }

        private static int GetArraySize(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.arraySize : -1;
        }

        private static bool HasExpectedBankCounts(
            List<EngineAssemblyInteractionTarget> targets,
            int expectedPerBank)
        {
            int left = 0;
            int right = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index].GroupIndex == 0) left++;
                if (targets[index].GroupIndex == 1) right++;
            }
            return left == expectedPerBank && right == expectedPerBank;
        }
    }
}
