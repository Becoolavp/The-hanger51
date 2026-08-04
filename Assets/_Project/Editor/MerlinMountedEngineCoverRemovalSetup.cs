using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinMountedEngineCoverRemovalSetup
    {
        [MenuItem("Hanger 51/Merlin Condition/22 - Repair Cover Removal After P-51 Installation")]
        public static void RepairCoverRemovalAfterP51Installation()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 22 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 22 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 22 failed. No Merlin condition systems were found.");
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
                Debug.LogError("Merlin Condition Step 22 failed. No complete portable-engine target registries could be repaired.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 22 changed the target registries but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 22 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 22 complete. Rebuilt portable target registries on {repaired} engine setup(s), including the complete-engine shipment template. Cover removal now follows the engine into the P-51 instead of searching the abandoned stand hierarchy.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/23 - Validate Cover Removal After P-51 Installation")]
        public static void ValidateCoverRemovalAfterP51Installation()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 23 failed: no Merlin condition systems exist.");
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
                Debug.LogError("Merlin Condition Step 23 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 23 passed. Every current Merlin and the purchased-engine template has a transport-root registry of 2 covers, 12 bolts, and 24 plugs, and the removal controller resolves those references independently of the stand hierarchy.");
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
                    $"Skipped '{condition.name}' because its station or portable root is missing.",
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
                    $"Skipped '{condition.name}' because its portable root contains {covers.Count} cover, {bolts.Count} bolt, and {plugs.Count} plug targets instead of 2/12/24.",
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
                Collider collider = covers[index].GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = true;
                    EditorUtility.SetDirty(collider);
                }
                covers[index].RefreshFromStation();
                EditorUtility.SetDirty(covers[index]);
            }

            transport.RefreshMaintenanceTargets();
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(removal);
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
            EngineAssemblyRemovalController removal =
                condition.GetComponent<EngineAssemblyRemovalController>();

            if (station == null
                || transport == null
                || transport.TransportRoot == null
                || removal == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 23 failed: '{condition.name}' is missing its station, portable root, or removal controller.",
                    condition);
                passed = false;
                return;
            }

            EngineAssemblyInteractionTarget[] rootTargets =
                transport.TransportRoot.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            int rootCovers = CountTargets(rootTargets, EngineAssemblyInteractionKind.CoverPlacement);
            int rootBolts = CountTargets(rootTargets, EngineAssemblyInteractionKind.CoverBolt);
            int rootPlugs = CountTargets(rootTargets, EngineAssemblyInteractionKind.SparkPlug);

            removal.InitializeBindings();
            bool registryResolved = removal.TryGetConfiguredTargetCounts(
                out int registeredCovers,
                out int registeredBolts,
                out int registeredPlugs);

            if (rootCovers != 2
                || rootBolts != 12
                || rootPlugs != 24
                || !registryResolved
                || registeredCovers != 2
                || registeredBolts != 12
                || registeredPlugs != 24)
            {
                Debug.LogError(
                    $"Merlin Condition Step 23 failed for '{condition.name}'. Portable root={rootCovers}/{rootBolts}/{rootPlugs}; removal registry={registeredCovers}/{registeredBolts}/{registeredPlugs}; resolved={registryResolved}. Expected 2/12/24.",
                    condition);
                passed = false;
            }
        }

        private static List<EngineAssemblyInteractionTarget> CollectTargets(
            EngineAssemblyInteractionTarget[] allTargets,
            EngineAssemblyInteractionKind kind)
        {
            List<EngineAssemblyInteractionTarget> result =
                new List<EngineAssemblyInteractionTarget>();
            for (int index = 0; index < allTargets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = allTargets[index];
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
                int group = left.GroupIndex.CompareTo(right.GroupIndex);
                return group != 0
                    ? group
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

        private static int CountTargets(
            EngineAssemblyInteractionTarget[] targets,
            EngineAssemblyInteractionKind kind)
        {
            int count = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null && targets[index].InteractionKind == kind)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
