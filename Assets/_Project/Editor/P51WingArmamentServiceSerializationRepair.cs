using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    /// <summary>
    /// Migrates the legacy P51WingArmamentServiceTarget components to the standalone-safe
    /// P51WingArmamentServicePoint component without changing the armament hierarchy.
    ///
    /// The legacy component is defined in P51WingArmamentSystem.cs alongside other MonoBehaviours.
    /// The replacement has its own matching script file, giving Unity an unambiguous MonoScript
    /// asset for standalone scene serialization.
    /// </summary>
    public static class P51WingArmamentServiceSerializationRepair
    {
        [MenuItem("Hanger 51/Build/68 - Repair Armament Service Target Serialization")]
        public static void RepairCurrentScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Armament serialization repair failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Armament serialization repair failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Armament serialization repair failed. Open the saved game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Armament serialization repair stopped because Unity could not save the open scene(s).");
                return;
            }

            List<P51WingArmamentServiceTarget> legacyTargets = CollectLegacyTargets(scene);
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair P-51 armament service serialization");

            int migrated = 0;
            int alreadySafe = 0;

            try
            {
                for (int index = 0; index < legacyTargets.Count; index++)
                {
                    P51WingArmamentServiceTarget legacy = legacyTargets[index];
                    if (legacy == null) continue;

                    GameObject owner = legacy.gameObject;
                    P51WingArmamentServicePoint replacement = owner.GetComponent<P51WingArmamentServicePoint>();
                    if (replacement != null)
                    {
                        alreadySafe++;
                        Undo.DestroyObjectImmediate(legacy);
                        continue;
                    }

                    SerializedObject serializedLegacy = new SerializedObject(legacy);
                    serializedLegacy.Update();

                    float holdSeconds = ReadFloat(serializedLegacy, "holdSeconds", 1.25f);
                    Transform[] bolts = ReadTransformArray(serializedLegacy, "holdDownBolts");
                    GameObject highlight = ReadGameObject(serializedLegacy, "installHighlightRoot");

                    replacement = Undo.AddComponent<P51WingArmamentServicePoint>(owner);
                    replacement.Configure(
                        legacy.System,
                        legacy.ServiceKind,
                        legacy.WingIndex,
                        legacy.StationIndex,
                        bolts,
                        highlight,
                        holdSeconds);

                    EditorUtility.SetDirty(replacement);
                    Undo.DestroyObjectImmediate(legacy);
                    migrated++;
                }

                PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
                InventoryUI inventoryUi = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
                P51WingArmamentServicePointInteractor safeInteractor = null;

                if (inventory != null)
                {
                    safeInteractor = inventory.GetComponent<P51WingArmamentServicePointInteractor>();
                    if (safeInteractor == null)
                    {
                        safeInteractor = Undo.AddComponent<P51WingArmamentServicePointInteractor>(inventory.gameObject);
                    }

                    Camera playerCamera = inventory.GetComponentInChildren<Camera>(true);
                    safeInteractor.Configure(playerCamera, inventoryUi);
                    EditorUtility.SetDirty(safeInteractor);

                    P51WingArmamentPlayerInteractor legacyInteractor =
                        inventory.GetComponent<P51WingArmamentPlayerInteractor>();
                    if (legacyInteractor != null && legacyInteractor.enabled)
                    {
                        Undo.RecordObject(legacyInteractor, "Disable legacy armament interactor");
                        legacyInteractor.enabled = false;
                        EditorUtility.SetDirty(legacyInteractor);
                    }
                }

                P51WingArmamentRuntimePerformanceGuard[] legacyGuards =
                    Object.FindObjectsByType<P51WingArmamentRuntimePerformanceGuard>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int index = 0; index < legacyGuards.Length; index++)
                {
                    P51WingArmamentRuntimePerformanceGuard guard = legacyGuards[index];
                    if (guard == null || guard.gameObject.scene != scene || !guard.enabled) continue;
                    Undo.RecordObject(guard, "Disable legacy armament performance guard");
                    guard.enabled = false;
                    EditorUtility.SetDirty(guard);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();

                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError("Armament serialization repair changed the scene but Unity could not save it.");
                    return;
                }

                Undo.CollapseUndoOperations(undoGroup);

                int remainingLegacy = CollectLegacyTargets(scene).Count;
                int safeCount = CollectSafeTargets(scene).Count;
                Debug.Log(
                    $"Armament serialization repair complete for '{scene.path}'. "
                    + $"Migrated={migrated}, already-safe duplicates cleaned={alreadySafe}, "
                    + $"safe service points now={safeCount}, legacy targets remaining={remainingLegacy}. "
                    + "The old armament interactor/performance guard were disabled so only the standalone-safe service path runs.");

                if (remainingLegacy != 0)
                {
                    Debug.LogError(
                        "Armament serialization repair is incomplete because one or more legacy "
                        + "P51WingArmamentServiceTarget components remain in the scene.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("Armament serialization repair failed unexpectedly.\n" + exception);
            }
        }

        [MenuItem("Hanger 51/Build/69 - Validate Armament Service Serialization Repair")]
        public static void ValidateCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Armament serialization validation failed: no valid active scene.");
                return;
            }

            int legacyCount = CollectLegacyTargets(scene).Count;
            int safeCount = CollectSafeTargets(scene).Count;
            int enabledLegacyInteractors = CountEnabledInScene<P51WingArmamentPlayerInteractor>(scene);
            int safeInteractors = CountInScene<P51WingArmamentServicePointInteractor>(scene);
            int enabledLegacyGuards = CountEnabledInScene<P51WingArmamentRuntimePerformanceGuard>(scene);

            bool passed = legacyCount == 0
                && safeCount > 0
                && safeInteractors > 0
                && enabledLegacyInteractors == 0
                && enabledLegacyGuards == 0;

            if (passed)
            {
                Debug.Log(
                    $"Armament serialization validation PASSED for '{scene.path}'. "
                    + $"SafePoints={safeCount}, SafeInteractors={safeInteractors}, LegacyTargets=0, "
                    + "enabled legacy interactors=0, enabled legacy guards=0.");
            }
            else
            {
                Debug.LogError(
                    $"Armament serialization validation FAILED for '{scene.path}'. "
                    + $"SafePoints={safeCount}, SafeInteractors={safeInteractors}, "
                    + $"LegacyTargets={legacyCount}, enabled legacy interactors={enabledLegacyInteractors}, "
                    + $"enabled legacy guards={enabledLegacyGuards}.");
            }
        }

        private static List<P51WingArmamentServiceTarget> CollectLegacyTargets(Scene scene)
        {
            List<P51WingArmamentServiceTarget> result = new List<P51WingArmamentServiceTarget>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                P51WingArmamentServiceTarget[] targets =
                    roots[rootIndex].GetComponentsInChildren<P51WingArmamentServiceTarget>(true);
                result.AddRange(targets);
            }
            return result;
        }

        private static List<P51WingArmamentServicePoint> CollectSafeTargets(Scene scene)
        {
            List<P51WingArmamentServicePoint> result = new List<P51WingArmamentServicePoint>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                P51WingArmamentServicePoint[] targets =
                    roots[rootIndex].GetComponentsInChildren<P51WingArmamentServicePoint>(true);
                result.AddRange(targets);
            }
            return result;
        }

        private static int CountInScene<T>(Scene scene) where T : MonoBehaviour
        {
            int count = 0;
            T[] behaviours = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                T behaviour = behaviours[index];
                if (behaviour != null && behaviour.gameObject.scene == scene) count++;
            }
            return count;
        }

        private static int CountEnabledInScene<T>(Scene scene) where T : Behaviour
        {
            int count = 0;
            T[] behaviours = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                T behaviour = behaviours[index];
                if (behaviour != null && behaviour.gameObject.scene == scene && behaviour.enabled) count++;
            }
            return count;
        }

        private static float ReadFloat(SerializedObject serializedObject, string propertyName, float fallback)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.floatValue : fallback;
        }

        private static GameObject ReadGameObject(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as GameObject : null;
        }

        private static Transform[] ReadTransformArray(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return Array.Empty<Transform>();
            }

            Transform[] result = new Transform[property.arraySize];
            for (int index = 0; index < property.arraySize; index++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                result[index] = element != null ? element.objectReferenceValue as Transform : null;
            }
            return result;
        }
    }
}
