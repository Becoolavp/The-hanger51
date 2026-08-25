using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using DiagnosticsProcess = System.Diagnostics.Process;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    /// <summary>
    /// Isolates stale/corrupt serialized data inside scene roots. A temporary copy of the source
    /// scene is opened, then selected roots are freshly instantiated while the remaining roots are
    /// moved intact into a brand-new destination scene. All serialized cross-root scene-object
    /// references are remapped afterward, so the only intended variable is which roots were
    /// reserialized through Object.Instantiate. The user's real scene is never modified.
    /// </summary>
    public static class Hanger51RootReserializationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string TempSourcePath = TempSceneFolder + "/__Hanger51RootReserializeSource.unity";
        private const string DiagnosticsRoot = "Builds/Diagnostics";
        private const string P51RootName = "P-51D Mustang Test Aircraft";

        private sealed class ReferenceRecord
        {
            public Component owner;
            public string propertyPath;
            public Object target;
            public string description;
        }

        [MenuItem("Hanger 51/Build/81 - Report Scene Root Order")]
        public static void ReportRootOrder()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!ValidateScene(scene, "Root report")) return;

            List<GameObject> roots = GetSortedRoots(scene);
            List<string> lines = new List<string>();
            for (int index = 0; index < roots.Count; index++)
            {
                GameObject root = roots[index];
                int componentCount = root.GetComponentsInChildren<Component>(true).Length;
                int monoCount = root.GetComponentsInChildren<MonoBehaviour>(true).Length;
                lines.Add($"  [{index:D2}] {root.name} | Components={componentCount}, MonoBehaviours={monoCount}");
            }

            Debug.Log($"Scene-root report for '{scene.path}': {roots.Count} root(s).\n" + string.Join("\n", lines));
        }

        [MenuItem("Hanger 51/Build/82 - Reserialize Root Half A")]
        public static void BuildHalfA() => BuildVariant("HalfA", 0, 1, 2);

        [MenuItem("Hanger 51/Build/83 - Reserialize Root Half B")]
        public static void BuildHalfB() => BuildVariant("HalfB", 1, 2, 2);

        [MenuItem("Hanger 51/Build/84 - Reserialize Root Quarter 1")]
        public static void BuildQuarter1() => BuildVariant("Quarter1", 0, 1, 4);

        [MenuItem("Hanger 51/Build/85 - Reserialize Root Quarter 2")]
        public static void BuildQuarter2() => BuildVariant("Quarter2", 1, 2, 4);

        [MenuItem("Hanger 51/Build/86 - Reserialize Root Quarter 3")]
        public static void BuildQuarter3() => BuildVariant("Quarter3", 2, 3, 4);

        [MenuItem("Hanger 51/Build/87 - Reserialize Root Quarter 4")]
        public static void BuildQuarter4() => BuildVariant("Quarter4", 3, 4, 4);

        [MenuItem("Hanger 51/Build/88 - Reserialize NO Roots Control")]
        public static void BuildNoneControl() => BuildVariant("None", 0, 0, 1, true, false);

        [MenuItem("Hanger 51/Build/89 - Reserialize ALL Roots Control")]
        public static void BuildAllControl() => BuildVariant("All", 0, 1, 1, false, true);

        [MenuItem("Hanger 51/Build/90 - Reserialize ONLY P-51 Aircraft Root")]
        public static void BuildOnlyP51Root() => BuildVariant("OnlyP51", 0, 0, 1, false, false, P51RootName);

        private static void BuildVariant(
            string label,
            int numeratorStart,
            int numeratorEnd,
            int denominator,
            bool forceNone = false,
            bool forceAll = false,
            string exactRootName = null)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Root reserialization diagnostic failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Root reserialization diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene originalScene = SceneManager.GetActiveScene();
            if (!ValidateScene(originalScene, "Root reserialization diagnostic")) return;
            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Root reserialization diagnostic could not save open scenes.");
                return;
            }

            string sourcePath = originalScene.path;
            string tempDestPath = TempSceneFolder + "/__Hanger51RootReserialize" + label + ".unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, "RootReserialize_" + label);
            string outputPath = Path.Combine(outputFolder, "TheHanger51_RootReserialize_" + label + ".exe");
            string logPath = Path.Combine(outputFolder, "RootReserialize_" + label + "_Player.log");

            Scene tempSourceScene = default;
            Scene destinationScene = default;

            try
            {
                DeleteAssetIfPresent(TempSourcePath);
                DeleteAssetIfPresent(tempDestPath);
                PrepareOutputFolder(outputFolder);

                if (!AssetDatabase.CopyAsset(sourcePath, TempSourcePath))
                {
                    Debug.LogError("Root reserialization diagnostic could not create temporary source copy.");
                    return;
                }
                AssetDatabase.Refresh();

                tempSourceScene = EditorSceneManager.OpenScene(TempSourcePath, OpenSceneMode.Additive);
                if (!tempSourceScene.IsValid() || !tempSourceScene.isLoaded)
                {
                    Debug.LogError("Root reserialization diagnostic could not open temporary source copy.");
                    return;
                }

                List<GameObject> roots = GetSortedRoots(tempSourceScene);
                if (roots.Count == 0)
                {
                    Debug.LogError("Root reserialization diagnostic found zero scene roots.");
                    return;
                }

                List<ReferenceRecord> crossRootReferences = CollectCrossRootReferences(tempSourceScene);
                HashSet<GameObject> rootsToClone = new HashSet<GameObject>();

                if (!string.IsNullOrEmpty(exactRootName))
                {
                    for (int index = 0; index < roots.Count; index++)
                    {
                        if (roots[index].name == exactRootName) rootsToClone.Add(roots[index]);
                    }
                    if (rootsToClone.Count != 1)
                    {
                        Debug.LogError($"Root reserialization '{label}' expected exactly one root named '{exactRootName}', found {rootsToClone.Count}.");
                        return;
                    }
                }
                else
                {
                    int startIndex;
                    int endIndex;
                    if (forceNone)
                    {
                        startIndex = 0;
                        endIndex = 0;
                    }
                    else if (forceAll)
                    {
                        startIndex = 0;
                        endIndex = roots.Count;
                    }
                    else
                    {
                        startIndex = Mathf.FloorToInt(roots.Count * (numeratorStart / (float)denominator));
                        endIndex = numeratorEnd == denominator
                            ? roots.Count
                            : Mathf.FloorToInt(roots.Count * (numeratorEnd / (float)denominator));
                        endIndex = Mathf.Clamp(endIndex, startIndex + 1, roots.Count);
                    }
                    for (int index = startIndex; index < endIndex; index++) rootsToClone.Add(roots[index]);
                }

                destinationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!destinationScene.IsValid() || !destinationScene.isLoaded)
                {
                    Debug.LogError("Root reserialization diagnostic could not create destination scene.");
                    return;
                }

                Dictionary<Object, Object> map = new Dictionary<Object, Object>();
                List<string> clonedNames = new List<string>();
                List<string> preservedNames = new List<string>();

                for (int index = 0; index < roots.Count; index++)
                {
                    GameObject sourceRoot = roots[index];
                    if (!rootsToClone.Contains(sourceRoot)) continue;

                    GameObject cloneRoot = Object.Instantiate(sourceRoot);
                    cloneRoot.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(cloneRoot, destinationScene);
                    MapHierarchyByStructure(sourceRoot.transform, cloneRoot.transform, map);
                    clonedNames.Add(sourceRoot.name);
                }

                for (int index = 0; index < roots.Count; index++)
                {
                    GameObject sourceRoot = roots[index];
                    if (rootsToClone.Contains(sourceRoot)) continue;

                    MapHierarchyByStructure(sourceRoot.transform, sourceRoot.transform, map);
                    SceneManager.MoveGameObjectToScene(sourceRoot, destinationScene);
                    preservedNames.Add(sourceRoot.name);
                }

                int restoredReferences = 0;
                List<string> failedReferenceLines = new List<string>();
                for (int index = 0; index < crossRootReferences.Count; index++)
                {
                    ReferenceRecord record = crossRootReferences[index];
                    if (!map.TryGetValue(record.owner, out Object mappedOwnerObject)
                        || !(mappedOwnerObject is Component mappedOwner)
                        || !map.TryGetValue(record.target, out Object mappedTarget))
                    {
                        failedReferenceLines.Add("MAP FAIL: " + record.description);
                        continue;
                    }

                    SerializedObject serializedOwner = new SerializedObject(mappedOwner);
                    SerializedProperty property = serializedOwner.FindProperty(record.propertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        failedReferenceLines.Add("PROPERTY FAIL: " + record.description);
                        continue;
                    }

                    property.objectReferenceValue = mappedTarget;
                    serializedOwner.ApplyModifiedPropertiesWithoutUndo();
                    restoredReferences++;
                }

                if (!EditorSceneManager.SaveScene(destinationScene, tempDestPath, false))
                {
                    Debug.LogError("Root reserialization diagnostic could not save destination scene.");
                    return;
                }

                int missingScripts = CountMissingScripts(destinationScene);
                int monoCount = CountMonoBehaviours(destinationScene);
                bool rootCountOk = destinationScene.rootCount == roots.Count;
                bool scriptsOk = missingScripts == 0;
                bool refsOk = restoredReferences == crossRootReferences.Count;

                Debug.Log(
                    $"Root reserialization '{label}' prepared {destinationScene.rootCount}/{roots.Count} roots. "
                    + $"Freshly cloned={clonedNames.Count}, preserved intact={preservedNames.Count}, "
                    + $"cross-root refs restored={restoredReferences}/{crossRootReferences.Count}, "
                    + $"MonoBehaviours={monoCount}, MissingScripts={missingScripts}.\n"
                    + "CLONED ROOTS:\n  " + string.Join("\n  ", clonedNames)
                    + "\nPRESERVED ROOTS:\n  " + string.Join("\n  ", preservedNames));

                if (!rootCountOk || !scriptsOk || !refsOk)
                {
                    Debug.LogError(
                        $"Root reserialization diagnostic audit failed. RootCountOK={rootCountOk}, "
                        + $"MissingScriptsOK={scriptsOk}, CrossRootRefsOK={refsOk}.\n"
                        + string.Join("\n", failedReferenceLines));
                    return;
                }

                EditorSceneManager.CloseScene(destinationScene, true);
                destinationScene = default;
                EditorSceneManager.CloseScene(tempSourceScene, true);
                tempSourceScene = default;
                DeleteAssetIfPresent(TempSourcePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { tempDestPath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.DetailedBuildReport
                        | BuildOptions.Development
                        | BuildOptions.CleanBuildCache
                        | BuildOptions.StrictMode
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                {
                    string failure = report == null
                        ? "Unity returned no BuildReport."
                        : $"Result={report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.";
                    Debug.LogError($"Root reserialization '{label}' build failed. {failure}");
                    return;
                }

                Debug.Log($"Root reserialization '{label}' build PASSED. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Root reserialization '{label}' failed unexpectedly.\n{exception}");
            }
            finally
            {
                if (destinationScene.IsValid() && destinationScene.isLoaded)
                    EditorSceneManager.CloseScene(destinationScene, true);
                if (tempSourceScene.IsValid() && tempSourceScene.isLoaded)
                    EditorSceneManager.CloseScene(tempSourceScene, true);

                DeleteAssetIfPresent(TempSourcePath);
                DeleteAssetIfPresent(tempDestPath);

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
                        if (!TryGetRoot(target, scene, out GameObject targetRoot)) continue;
                        if (targetRoot == ownerRoot) continue;

                        records.Add(new ReferenceRecord
                        {
                            owner = owner,
                            propertyPath = iterator.propertyPath,
                            target = target,
                            description = $"{ownerRoot.name}/{GetRelativePath(ownerRoot.transform, owner.transform)} [{owner.GetType().Name}].{iterator.propertyPath} -> {targetRoot.name}"
                        });
                    }
                    while (iterator.Next(true));
                }
            }
            return records;
        }

        private static bool TryGetRoot(Object value, Scene scene, out GameObject root)
        {
            root = null;
            Transform transform = null;
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
            else return false;

            while (transform.parent != null) transform = transform.parent;
            root = transform.gameObject;
            return true;
        }

        private static List<GameObject> GetSortedRoots(Scene scene)
        {
            List<GameObject> roots = new List<GameObject>(scene.GetRootGameObjects());
            roots.Sort((a, b) =>
            {
                int nameCompare = string.Compare(a.name, b.name, StringComparison.Ordinal);
                if (nameCompare != 0) return nameCompare;
                return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
            });
            return roots;
        }

        private static void MapHierarchyByStructure(Transform source, Transform destination, Dictionary<Object, Object> map)
        {
            if (source == null || destination == null) return;
            map[source.gameObject] = destination.gameObject;
            map[source] = destination;

            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] destinationComponents = destination.GetComponents<Component>();
            Dictionary<Type, int> sourceTypeOrdinal = new Dictionary<Type, int>();

            for (int sourceIndex = 0; sourceIndex < sourceComponents.Length; sourceIndex++)
            {
                Component sourceComponent = sourceComponents[sourceIndex];
                if (sourceComponent == null || sourceComponent is Transform) continue;

                Type type = sourceComponent.GetType();
                sourceTypeOrdinal.TryGetValue(type, out int ordinal);
                sourceTypeOrdinal[type] = ordinal + 1;

                int destinationOrdinal = 0;
                Component matched = null;
                for (int destinationIndex = 0; destinationIndex < destinationComponents.Length; destinationIndex++)
                {
                    Component destinationComponent = destinationComponents[destinationIndex];
                    if (destinationComponent == null || destinationComponent is Transform || destinationComponent.GetType() != type) continue;
                    if (destinationOrdinal == ordinal)
                    {
                        matched = destinationComponent;
                        break;
                    }
                    destinationOrdinal++;
                }

                if (matched != null) map[sourceComponent] = matched;
            }

            int childCount = Mathf.Min(source.childCount, destination.childCount);
            for (int index = 0; index < childCount; index++)
                MapHierarchyByStructure(source.GetChild(index), destination.GetChild(index), map);
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "<root>";
            List<string> parts = new List<string>();
            Transform cursor = target;
            while (cursor != null && cursor != root)
            {
                parts.Add(cursor.name);
                cursor = cursor.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static int CountMissingScripts(Scene scene)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(roots[index]);
            return count;
        }

        private static int CountMonoBehaviours(Scene scene)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                count += roots[index].GetComponentsInChildren<MonoBehaviour>(true).Length;
            return count;
        }

        private static bool ValidateScene(Scene scene, string label)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError(label + " failed. Open the saved real game scene first.");
                return false;
            }
            return true;
        }

        private static void PrepareOutputFolder(string folder)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
        }

        private static void Launch(string outputPath, string logPath)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string absoluteLogPath = Path.GetFullPath(logPath);
            if (!File.Exists(executablePath))
            {
                Debug.LogError("Root reserialization EXE not found at '" + executablePath + "'.");
                return;
            }

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = $"-logFile \"{absoluteLogPath}\" -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900",
                UseShellExecute = false,
                CreateNoWindow = false
            };
            DiagnosticsProcess process = DiagnosticsProcess.Start(info);
            if (process == null)
            {
                Debug.LogError("Root reserialization build succeeded but Windows returned no Player process.");
                return;
            }
            Debug.Log($"Launched root reserialization diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
