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
    /// Finds serialized scene-object references that cross from one root GameObject to another.
    /// Step 15 clones each root independently, which can sever these references; the rebuilt scene
    /// preserves them. These diagnostics start from the known-good independent-root clone and
    /// selectively restore cross-root references so the offending reference can be isolated.
    /// The user's real scene is never modified.
    /// </summary>
    public static class Hanger51CrossRootReferenceDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";

        private sealed class ReferenceRecord
        {
            public Component owner;
            public string ownerRoot;
            public string ownerPath;
            public string ownerType;
            public string propertyPath;
            public Object target;
            public string targetRoot;
            public string targetPath;
            public string targetType;

            public string Key => ownerRoot + "|" + ownerPath + "|" + ownerType + "|" + propertyPath
                + "|" + targetRoot + "|" + targetPath + "|" + targetType;

            public override string ToString()
            {
                return ownerRoot + "/" + ownerPath + " [" + ownerType + "]." + propertyPath
                    + " -> " + targetRoot + "/" + targetPath + " [" + targetType + "]";
            }
        }

        [MenuItem("Hanger 51/Build/73 - Report Cross-Root Serialized References")]
        public static void ReportCrossRootReferences()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!ValidateSourceScene(scene, "Cross-root reference report")) return;

            List<ReferenceRecord> records = CollectCrossRootReferences(scene);
            List<string> lines = new List<string>();
            for (int index = 0; index < records.Count; index++)
            {
                lines.Add("  [" + index.ToString("D3") + "] " + records[index]);
            }

            Debug.Log(
                "Cross-root serialized reference report for '" + scene.path + "': found "
                + records.Count + " reference(s).\n" + string.Join("\n", lines));
        }

        [MenuItem("Hanger 51/Build/74 - Fresh Clone Restore Cross-Root Half A")]
        public static void BuildHalfA() => BuildVariant("HalfA", 0, 1, 2);

        [MenuItem("Hanger 51/Build/75 - Fresh Clone Restore Cross-Root Half B")]
        public static void BuildHalfB() => BuildVariant("HalfB", 1, 2, 2);

        [MenuItem("Hanger 51/Build/76 - Fresh Clone Restore Cross-Root Quarter 1")]
        public static void BuildQuarter1() => BuildVariant("Quarter1", 0, 1, 4);

        [MenuItem("Hanger 51/Build/77 - Fresh Clone Restore Cross-Root Quarter 2")]
        public static void BuildQuarter2() => BuildVariant("Quarter2", 1, 2, 4);

        [MenuItem("Hanger 51/Build/78 - Fresh Clone Restore Cross-Root Quarter 3")]
        public static void BuildQuarter3() => BuildVariant("Quarter3", 2, 3, 4);

        [MenuItem("Hanger 51/Build/79 - Fresh Clone Restore Cross-Root Quarter 4")]
        public static void BuildQuarter4() => BuildVariant("Quarter4", 3, 4, 4);

        [MenuItem("Hanger 51/Build/80 - Fresh Clone Restore ALL Cross-Root References")]
        public static void BuildAll() => BuildVariant("All", 0, 1, 1);

        private static void BuildVariant(string label, int numeratorStart, int numeratorEnd, int denominator)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Cross-root diagnostic failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Cross-root diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!ValidateSourceScene(sourceScene, "Cross-root diagnostic")) return;
            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Cross-root diagnostic failed because Unity could not save open scenes.");
                return;
            }

            List<ReferenceRecord> records = CollectCrossRootReferences(sourceScene);
            if (records.Count == 0)
            {
                Debug.LogWarning("Cross-root diagnostic found zero serialized cross-root references.");
                return;
            }

            int startIndex = Mathf.FloorToInt(records.Count * (numeratorStart / (float)denominator));
            int endIndex = numeratorEnd == denominator
                ? records.Count
                : Mathf.FloorToInt(records.Count * (numeratorEnd / (float)denominator));
            endIndex = Mathf.Clamp(endIndex, startIndex + 1, records.Count);

            string tempScenePath = TempSceneFolder + "/__Hanger51CrossRoot" + label + ".unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, "CrossRoot_" + label);
            string outputPath = Path.Combine(outputFolder, "TheHanger51_CrossRoot_" + label + ".exe");
            string logPath = Path.Combine(outputFolder, "CrossRoot_" + label + "_Player.log");
            Scene diagnosticScene = default;

            try
            {
                DeleteAssetIfPresent(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Dictionary<Object, Object> cloneMap = CloneRootsAndBuildMap(sourceScene, diagnosticScene);
                if (cloneMap.Count == 0)
                {
                    Debug.LogError("Cross-root diagnostic could not construct the clone object map.");
                    return;
                }

                int restored = 0;
                List<string> restoredLines = new List<string>();
                for (int index = startIndex; index < endIndex; index++)
                {
                    ReferenceRecord record = records[index];
                    if (!cloneMap.TryGetValue(record.owner, out Object clonedOwnerObject)
                        || !(clonedOwnerObject is Component clonedOwner)
                        || !cloneMap.TryGetValue(record.target, out Object clonedTarget))
                    {
                        Debug.LogWarning("Could not map cross-root reference: " + record);
                        continue;
                    }

                    SerializedObject serializedOwner = new SerializedObject(clonedOwner);
                    SerializedProperty property = serializedOwner.FindProperty(record.propertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        Debug.LogWarning("Could not find cloned serialized property for: " + record);
                        continue;
                    }

                    property.objectReferenceValue = clonedTarget;
                    serializedOwner.ApplyModifiedPropertiesWithoutUndo();
                    restored++;
                    restoredLines.Add("  [" + index.ToString("D3") + "] " + record);
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError("Cross-root diagnostic could not save '" + tempScenePath + "'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "Cross-root diagnostic '" + label + "' restored " + restored
                    + " reference(s), indexes [" + startIndex + ".." + (endIndex - 1) + "] of "
                    + records.Count + " total.\n" + string.Join("\n", restoredLines));

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { tempScenePath },
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
                        : "Result=" + report.summary.result + ", errors=" + report.summary.totalErrors
                            + ", warnings=" + report.summary.totalWarnings + ".";
                    Debug.LogError("Cross-root diagnostic '" + label + "' build failed. " + failure);
                    return;
                }

                Debug.Log("Cross-root diagnostic '" + label + "' build passed. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("Cross-root diagnostic '" + label + "' failed unexpectedly.\n" + exception);
            }
            finally
            {
                if (diagnosticScene.IsValid() && diagnosticScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(diagnosticScene, true);
                }
                DeleteAssetIfPresent(tempScenePath);
            }
        }

        private static List<ReferenceRecord> CollectCrossRootReferences(Scene scene)
        {
            List<ReferenceRecord> result = new List<ReferenceRecord>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                Component[] components = root.GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component owner = components[componentIndex];
                    if (owner == null || owner is Transform) continue;

                    SerializedObject serialized;
                    try { serialized = new SerializedObject(owner); }
                    catch { continue; }

                    SerializedProperty iterator = serialized.GetIterator();
                    bool enterChildren = true;
                    while (iterator.Next(enterChildren))
                    {
                        enterChildren = false;
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (iterator.propertyPath == "m_GameObject" || iterator.propertyPath == "m_Script") continue;

                        Object target = iterator.objectReferenceValue;
                        if (!TryGetSceneObjectRoot(target, scene, out GameObject targetRoot, out Transform targetTransform))
                        {
                            continue;
                        }
                        if (targetRoot == root) continue;

                        ReferenceRecord record = new ReferenceRecord
                        {
                            owner = owner,
                            ownerRoot = root.name,
                            ownerPath = GetRelativePath(root.transform, owner.transform),
                            ownerType = owner.GetType().FullName ?? owner.GetType().Name,
                            propertyPath = iterator.propertyPath,
                            target = target,
                            targetRoot = targetRoot.name,
                            targetPath = GetRelativePath(targetRoot.transform, targetTransform),
                            targetType = target.GetType().FullName ?? target.GetType().Name
                        };

                        if (seen.Add(record.Key)) result.Add(record);
                    }
                }
            }

            result.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            return result;
        }

        private static Dictionary<Object, Object> CloneRootsAndBuildMap(Scene sourceScene, Scene targetScene)
        {
            Dictionary<Object, Object> map = new Dictionary<Object, Object>();
            GameObject[] roots = sourceScene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject sourceRoot = roots[index];
                if (sourceRoot == null) continue;
                GameObject cloneRoot = Object.Instantiate(sourceRoot);
                cloneRoot.name = sourceRoot.name;
                SceneManager.MoveGameObjectToScene(cloneRoot, targetScene);
                MapHierarchy(sourceRoot.transform, cloneRoot.transform, map);
            }
            return map;
        }

        private static void MapHierarchy(Transform source, Transform clone, Dictionary<Object, Object> map)
        {
            if (source == null || clone == null) return;
            map[source.gameObject] = clone.gameObject;
            map[source] = clone;

            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] cloneComponents = clone.GetComponents<Component>();
            bool[] used = new bool[cloneComponents.Length];

            for (int sourceIndex = 0; sourceIndex < sourceComponents.Length; sourceIndex++)
            {
                Component sourceComponent = sourceComponents[sourceIndex];
                if (sourceComponent == null || sourceComponent is Transform) continue;
                Type type = sourceComponent.GetType();
                for (int cloneIndex = 0; cloneIndex < cloneComponents.Length; cloneIndex++)
                {
                    if (used[cloneIndex]) continue;
                    Component cloneComponent = cloneComponents[cloneIndex];
                    if (cloneComponent == null || cloneComponent.GetType() != type) continue;
                    used[cloneIndex] = true;
                    map[sourceComponent] = cloneComponent;
                    break;
                }
            }

            int childCount = Mathf.Min(source.childCount, clone.childCount);
            for (int index = 0; index < childCount; index++)
            {
                MapHierarchy(source.GetChild(index), clone.GetChild(index), map);
            }
        }

        private static bool TryGetSceneObjectRoot(
            Object value,
            Scene scene,
            out GameObject root,
            out Transform transform)
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

        private static string GetRelativePath(Transform root, Transform target)
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

        private static bool ValidateSourceScene(Scene scene, string label)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError(label + " failed. Open the saved real game scene first.");
                return false;
            }
            return true;
        }

        private static void PrepareOutputFolder(string outputFolder)
        {
            if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);
            Directory.CreateDirectory(outputFolder);
        }

        private static void Launch(string outputPath, string logPath)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string absoluteLogPath = Path.GetFullPath(logPath);
            if (!File.Exists(executablePath))
            {
                Debug.LogError("Cross-root diagnostic EXE not found at '" + executablePath + "'.");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = "-logFile \"" + absoluteLogPath
                    + "\" -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo);
            if (process == null)
            {
                Debug.LogError("Cross-root diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }
            Debug.Log("Launched cross-root diagnostic PID " + process.Id + ". Log: '" + absoluteLogPath + "'.");
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
