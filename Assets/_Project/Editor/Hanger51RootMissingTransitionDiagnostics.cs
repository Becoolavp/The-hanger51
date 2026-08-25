using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    /// <summary>
    /// Mirrors the all-root reserialization path without building and reports exactly when a
    /// Missing Script slot appears: temporary source load, independent cloning, cross-root
    /// reference restoration, save, or reopen. The real game scene is never modified.
    /// </summary>
    public static class Hanger51RootMissingTransitionDiagnostics
    {
        private const string TempSourcePath = "Assets/_Project/Scenes/__H51MissingTransitionSource.unity";
        private const string TempDestinationPath = "Assets/_Project/Scenes/__H51MissingTransitionDestination.unity";

        private sealed class ReferenceRecord
        {
            public Component owner;
            public string propertyPath;
            public Object target;
            public string description;
        }

        [MenuItem("Hanger 51/Build/92 - Trace Missing Script During Root Reserialization")]
        public static void TraceMissingScriptTransition()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Step 92 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Step 92 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene originalScene = SceneManager.GetActiveScene();
            if (!originalScene.IsValid() || !originalScene.isLoaded || string.IsNullOrWhiteSpace(originalScene.path))
            {
                Debug.LogError("Step 92 failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Step 92 failed because Unity could not save open scenes.");
                return;
            }

            Scene sourceScene = default;
            Scene destinationScene = default;
            Scene reopenedScene = default;

            try
            {
                DeleteAssetIfPresent(TempSourcePath);
                DeleteAssetIfPresent(TempDestinationPath);

                if (!AssetDatabase.CopyAsset(originalScene.path, TempSourcePath))
                {
                    Debug.LogError("Step 92 could not create the temporary source scene copy.");
                    return;
                }
                AssetDatabase.Refresh();

                sourceScene = EditorSceneManager.OpenScene(TempSourcePath, OpenSceneMode.Additive);
                int sourceMissing = CountMissingScripts(sourceScene);
                List<ReferenceRecord> references = CollectCrossRootReferences(sourceScene);

                destinationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Dictionary<Object, Object> map = new Dictionary<Object, Object>();
                GameObject[] sourceRoots = sourceScene.GetRootGameObjects();

                for (int index = 0; index < sourceRoots.Length; index++)
                {
                    GameObject sourceRoot = sourceRoots[index];
                    if (sourceRoot == null) continue;

                    GameObject cloneRoot = Object.Instantiate(sourceRoot);
                    cloneRoot.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(cloneRoot, destinationScene);
                    MapHierarchy(sourceRoot.transform, cloneRoot.transform, map);
                }

                int afterCloneMissing = CountMissingScripts(destinationScene);
                int previousMissing = afterCloneMissing;
                int restoredReferences = 0;
                List<string> transitions = new List<string>();

                for (int index = 0; index < references.Count; index++)
                {
                    ReferenceRecord record = references[index];
                    if (!map.TryGetValue(record.owner, out Object mappedOwnerObject)
                        || !(mappedOwnerObject is Component mappedOwner)
                        || !map.TryGetValue(record.target, out Object mappedTarget))
                    {
                        transitions.Add($"  REF [{index:D3}] could not be mapped: {record.description}");
                        continue;
                    }

                    SerializedObject serializedOwner = new SerializedObject(mappedOwner);
                    SerializedProperty property = serializedOwner.FindProperty(record.propertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        transitions.Add($"  REF [{index:D3}] property missing/not object ref: {record.description}");
                        continue;
                    }

                    property.objectReferenceValue = mappedTarget;
                    serializedOwner.ApplyModifiedPropertiesWithoutUndo();
                    restoredReferences++;

                    int currentMissing = CountMissingScripts(destinationScene);
                    if (currentMissing != previousMissing)
                    {
                        transitions.Add(
                            $"  MISSING SCRIPT COUNT CHANGED after REF [{index:D3}] {previousMissing} -> {currentMissing}: "
                            + record.description);
                        transitions.AddRange(GetMissingLocations(destinationScene));
                        previousMissing = currentMissing;
                    }
                }

                int afterRestoreMissing = CountMissingScripts(destinationScene);

                if (!EditorSceneManager.SaveScene(destinationScene, TempDestinationPath, false))
                {
                    Debug.LogError("Step 92 could not save the temporary destination scene.");
                    return;
                }

                int afterSaveMissing = CountMissingScripts(destinationScene);

                EditorSceneManager.CloseScene(destinationScene, true);
                destinationScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                reopenedScene = EditorSceneManager.OpenScene(TempDestinationPath, OpenSceneMode.Additive);
                int afterReopenMissing = CountMissingScripts(reopenedScene);
                List<string> reopenedMissingLocations = GetMissingLocations(reopenedScene);

                Debug.Log(
                    "Step 92 missing-script transition report:\n"
                    + $"  SourceCopyMissing={sourceMissing}\n"
                    + $"  AfterIndependentCloneMissing={afterCloneMissing}\n"
                    + $"  CrossRootReferencesRestored={restoredReferences}/{references.Count}\n"
                    + $"  AfterReferenceRestoreMissing={afterRestoreMissing}\n"
                    + $"  AfterSaveMissing={afterSaveMissing}\n"
                    + $"  AfterReopenMissing={afterReopenMissing}\n"
                    + "TRANSITIONS:\n"
                    + (transitions.Count == 0 ? "  <none>" : string.Join("\n", transitions))
                    + "\nMISSING LOCATIONS AFTER REOPEN:\n"
                    + (reopenedMissingLocations.Count == 0
                        ? "  <none>"
                        : string.Join("\n", reopenedMissingLocations)));
            }
            catch (Exception exception)
            {
                Debug.LogError("Step 92 failed unexpectedly.\n" + exception);
            }
            finally
            {
                if (reopenedScene.IsValid() && reopenedScene.isLoaded)
                    EditorSceneManager.CloseScene(reopenedScene, true);
                if (destinationScene.IsValid() && destinationScene.isLoaded)
                    EditorSceneManager.CloseScene(destinationScene, true);
                if (sourceScene.IsValid() && sourceScene.isLoaded)
                    EditorSceneManager.CloseScene(sourceScene, true);

                DeleteAssetIfPresent(TempSourcePath);
                DeleteAssetIfPresent(TempDestinationPath);

                if (originalScene.IsValid() && originalScene.isLoaded)
                    SceneManager.SetActiveScene(originalScene);
            }
        }

        private static List<ReferenceRecord> CollectCrossRootReferences(Scene scene)
        {
            List<ReferenceRecord> records = new List<ReferenceRecord>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject ownerRoot = roots[rootIndex];
                Component[] components = ownerRoot.GetComponentsInChildren<Component>(true);

                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component owner = components[componentIndex];
                    if (owner == null || owner is Transform) continue;

                    SerializedObject serialized;
                    try { serialized = new SerializedObject(owner); }
                    catch { continue; }

                    SerializedProperty iterator = serialized.GetIterator();
                    if (!iterator.Next(true)) continue;

                    do
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (iterator.propertyPath == "m_GameObject" || iterator.propertyPath == "m_Script") continue;

                        Object target = iterator.objectReferenceValue;
                        if (!TryGetRoot(target, scene, out GameObject targetRoot, out Transform targetTransform)) continue;
                        if (targetRoot == ownerRoot) continue;

                        records.Add(new ReferenceRecord
                        {
                            owner = owner,
                            propertyPath = iterator.propertyPath,
                            target = target,
                            description = $"{GetPath(ownerRoot.transform, owner.transform)} "
                                + $"[{owner.GetType().FullName}].{iterator.propertyPath} -> "
                                + $"{targetRoot.name}/{GetPath(targetRoot.transform, targetTransform)} "
                                + $"[{target.GetType().FullName}]"
                        });
                    }
                    while (iterator.Next(true));
                }
            }

            return records;
        }

        private static void MapHierarchy(Transform source, Transform destination, Dictionary<Object, Object> map)
        {
            if (source == null || destination == null) return;

            map[source.gameObject] = destination.gameObject;
            map[source] = destination;

            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] destinationComponents = destination.GetComponents<Component>();

            for (int sourceIndex = 0; sourceIndex < sourceComponents.Length; sourceIndex++)
            {
                Component sourceComponent = sourceComponents[sourceIndex];
                if (sourceComponent == null || sourceComponent is Transform) continue;

                Type type = sourceComponent.GetType();
                int ordinal = GetTypeOrdinal(sourceComponents, sourceIndex, type);
                Component destinationComponent = FindTypeOrdinal(destinationComponents, type, ordinal);
                if (destinationComponent != null)
                    map[sourceComponent] = destinationComponent;
            }

            int childCount = Mathf.Min(source.childCount, destination.childCount);
            for (int index = 0; index < childCount; index++)
                MapHierarchy(source.GetChild(index), destination.GetChild(index), map);
        }

        private static int GetTypeOrdinal(Component[] components, int upToIndex, Type type)
        {
            int ordinal = 0;
            for (int index = 0; index < upToIndex; index++)
            {
                Component component = components[index];
                if (component != null && component.GetType() == type) ordinal++;
            }
            return ordinal;
        }

        private static Component FindTypeOrdinal(Component[] components, Type type, int ordinal)
        {
            int seen = 0;
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component.GetType() != type) continue;
                if (seen == ordinal) return component;
                seen++;
            }
            return null;
        }

        private static bool TryGetRoot(Object value, Scene scene, out GameObject root, out Transform transform)
        {
            root = null;
            transform = null;

            if (value is GameObject gameObject)
            {
                if (gameObject.scene != scene) return false;
                transform = gameObject.transform;
            }
            else if (value is Component component)
            {
                if (component.gameObject.scene != scene) return false;
                transform = component.transform;
            }
            else
            {
                return false;
            }

            Transform cursor = transform;
            while (cursor.parent != null) cursor = cursor.parent;
            root = cursor.gameObject;
            return true;
        }

        private static int CountMissingScripts(Scene scene)
        {
            int total = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                    total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[index].gameObject);
            }
            return total;
        }

        private static List<string> GetMissingLocations(Scene scene)
        {
            List<string> result = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    GameObject gameObject = transforms[index].gameObject;
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                    if (missing <= 0) continue;
                    result.Add($"  {root.name}/{GetPath(root.transform, gameObject.transform)} | MissingScripts={missing}");
                }
            }
            return result;
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (root == target) return "<root>";
            List<string> names = new List<string>();
            Transform cursor = target;
            while (cursor != null && cursor != root)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
