using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinPersistentRemovedPartConditionSetup
    {
        private const string BlockItemPath =
            "Assets/_Project/Inventory/Items/MerlinEngineBlock.asset";
        private const string CoverItemPath =
            "Assets/_Project/Inventory/Items/MerlinCylinderCover.asset";
        private const string PlugItemPath =
            "Assets/_Project/Inventory/Items/SparkPlug.asset";

        [MenuItem("Hanger 51/Merlin Condition/26 - Preserve Removed Part Condition")]
        public static void PreserveRemovedPartCondition()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 26 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Merlin Condition Step 26 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 26 failed. No Merlin condition systems were found.");
                return;
            }

            int configured = 0;
            GameObject selected = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                EnginePartConditionPersistenceController persistence =
                    condition.GetComponent<EnginePartConditionPersistenceController>();
                if (persistence == null)
                {
                    persistence = Undo.AddComponent<EnginePartConditionPersistenceController>(
                        condition.gameObject);
                }

                if (!persistence.InitializeNow())
                {
                    Debug.LogWarning(
                        $"Skipped '{condition.name}' because its condition persistence bindings are incomplete.",
                        condition);
                    continue;
                }

                EngineAssemblyInteractionTarget[] targets =
                    condition.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
                EngineAssemblyTransportController transport =
                    condition.GetComponent<EngineAssemblyTransportController>();
                if (transport != null && transport.TransportRoot != null)
                {
                    targets = transport.TransportRoot.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
                }

                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    targets[targetIndex]?.RefreshFromStation();
                }

                EditorUtility.SetDirty(persistence);
                EditorUtility.SetDirty(condition);
                configured++;
                if (selected == null && condition.gameObject.activeInHierarchy)
                {
                    selected = condition.gameObject;
                }
            }

            if (configured == 0)
            {
                Debug.LogError("Merlin Condition Step 26 failed. No complete engine condition setup could be updated.");
                return;
            }

            if (!ValidateItemDefinitions(false))
            {
                Debug.LogError("Merlin Condition Step 26 failed. One or more engine-part inventory definitions are missing or cannot be identified.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Merlin Condition Step 26 changed the engines but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Condition Step 26 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 26 complete. Added exact per-instance condition persistence to {configured} engine setup(s), including the purchased complete-engine template. Covers, spark plugs, and bare blocks now carry health through inventory, ground pickups, and reinstallation; blocks also carry oil quantity.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/27 - Validate Removed Part Condition Persistence")]
        public static void ValidateRemovedPartConditionPersistence()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 27 failed: no Merlin condition systems exist.");
                return;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                EnginePartConditionPersistenceController persistence =
                    condition.GetComponent<EnginePartConditionPersistenceController>();
                if (persistence == null
                    || !persistence.ValidateConfiguration(out string details))
                {
                    Debug.LogError(
                        $"Merlin Condition Step 27 failed for '{condition.name}': {(persistence == null ? "persistence controller is missing" : details)}.",
                        condition);
                    passed = false;
                }

                if (condition.GetComponent<EngineAssemblyRemovalController>() == null)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 27 failed: '{condition.name}' has no removal controller.",
                        condition);
                    passed = false;
                }
            }

            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null)
            {
                Debug.LogError("Merlin Condition Step 27 failed: Player inventory is missing.");
                passed = false;
            }

            passed &= ValidateItemDefinitions(true);

            EnginePartConditionData testCover = EnginePartConditionData.Create(
                EnginePartConditionKind.CylinderCover,
                22.5f);
            EnginePartConditionData clonedCover = testCover.Clone();
            if (!clonedCover.IsCracked
                || Mathf.Abs(clonedCover.Health - 22.5f) > 0.001f
                || clonedCover.InstanceId != testCover.InstanceId)
            {
                Debug.LogError("Merlin Condition Step 27 failed: condition data does not clone cracked cover identity and health correctly.");
                passed = false;
            }

            EnginePartConditionData testBlock = EnginePartConditionData.Create(
                EnginePartConditionKind.EngineBlock,
                63f,
                11.4f,
                20f);
            EnginePartConditionData clonedBlock = testBlock.Clone();
            if (Mathf.Abs(clonedBlock.Health - 63f) > 0.001f
                || Mathf.Abs(clonedBlock.OilQuantityLiters - 11.4f) > 0.001f
                || Mathf.Abs(clonedBlock.OilCapacityLiters - 20f) > 0.001f)
            {
                Debug.LogError("Merlin Condition Step 27 failed: engine-block health or oil state does not survive cloning.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Condition Step 27 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 27 passed. Every current Merlin and the purchased-engine template can capture and restore individual block, cover, and plug condition; inventory slots and ground pickups support per-instance records; cracked-cover appearance and block oil state are retained.");
            }
        }

        private static bool ValidateItemDefinitions(bool logErrors)
        {
            bool passed = true;
            passed &= ValidateItem(
                BlockItemPath,
                EnginePartConditionKind.EngineBlock,
                logErrors);
            passed &= ValidateItem(
                CoverItemPath,
                EnginePartConditionKind.CylinderCover,
                logErrors);
            passed &= ValidateItem(
                PlugItemPath,
                EnginePartConditionKind.SparkPlug,
                logErrors);
            return passed;
        }

        private static bool ValidateItem(
            string path,
            EnginePartConditionKind expectedKind,
            bool logErrors)
        {
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            bool valid = item != null
                && EnginePartConditionData.InferKind(item) == expectedKind
                && item.WorldPrefab != null;
            if (!valid && logErrors)
            {
                Debug.LogError(
                    $"Merlin Condition Step 27 failed: '{path}' is missing, has no world prefab, or is not recognized as {expectedKind}.");
            }
            return valid;
        }
    }
}
