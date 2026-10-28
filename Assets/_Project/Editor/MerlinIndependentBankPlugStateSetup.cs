using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinIndependentBankPlugStateSetup
    {
        [MenuItem("Hanger 51/Merlin Condition/24 - Preserve Untouched Bank Spark Plugs")]
        public static void PreserveUntouchedBankSparkPlugs()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 24 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 24 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 24 failed. No Merlin condition systems were found.");
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

            if (repaired == 0)
            {
                Debug.LogError("Merlin Condition Step 24 failed. No complete engine bank setup could be repaired.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 24 changed the engines but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 24 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 24 complete. Added independent left/right bank state protection to {repaired} engine setup(s), including the purchased complete-engine template. Reinstalling one cover will no longer clear the untouched bank's spark plugs.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/25 - Validate Independent Bank Spark Plugs")]
        public static void ValidateIndependentBankSparkPlugs()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 25 failed: no Merlin condition systems exist.");
                return;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                EngineBankStateIsolationController isolation =
                    condition.GetComponent<EngineBankStateIsolationController>();
                if (isolation == null)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 25 failed: '{condition.name}' is missing its bank-isolation controller.",
                        condition);
                    passed = false;
                    continue;
                }

                if (!isolation.ValidateConfiguration(out string details))
                {
                    Debug.LogError(
                        $"Merlin Condition Step 25 failed for '{condition.name}': {details}.",
                        condition);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Condition Step 25 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 25 passed. Every current Merlin and the purchased-engine template preserves each secured bank independently with 6 bolts and 12 spark plugs assigned per side.");
            }
        }

        private static bool RepairCondition(EngineConditionController condition)
        {
            EngineAssemblyStation station = condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its station or portable engine root is missing.",
                    condition);
                return false;
            }

            EngineAssemblyInteractionTarget[] allTargets =
                transport.TransportRoot.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            List<EngineAssemblyInteractionTarget> covers =
                CollectTargets(allTargets, EngineAssemblyInteractionKind.CoverPlacement);
            List<EngineAssemblyInteractionTarget> bolts =
                CollectTargets(allTargets, EngineAssemblyInteractionKind.CoverBolt);
            List<EngineAssemblyInteractionTarget> plugs =
                CollectTargets(allTargets, EngineAssemblyInteractionKind.SparkPlug);

            if (covers.Count != 2 || bolts.Count != 12 || plugs.Count != 24)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its portable root has {covers.Count} cover, {bolts.Count} bolt, and {plugs.Count} plug targets instead of 2/12/24.",
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

            EngineBankStateIsolationController isolation =
                condition.GetComponent<EngineBankStateIsolationController>();
            if (isolation == null)
            {
                isolation = Undo.AddComponent<EngineBankStateIsolationController>(
                    condition.gameObject);
            }

            bool initialized = isolation.InitializeNow();
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(isolation);
            return initialized;
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
                if (target != null
                    && target.InteractionKind == kind
                    && !result.Contains(target))
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
            SerializedObject serializedStation,
            string propertyName,
            List<EngineAssemblyInteractionTarget> targets)
        {
            SerializedProperty property = serializedStation.FindProperty(propertyName);
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
    }
}
