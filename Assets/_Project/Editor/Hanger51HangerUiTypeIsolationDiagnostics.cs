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
    /// Isolates the standalone level0 corruption after proving that a fresh scene containing all
    /// Hanger 51 runtime MonoBehaviours plus Unity UI crashes, while Hanger-only and UI-only scenes run.
    /// Hanger 51 runtime script TYPES are sorted deterministically and split into halves/quarters.
    /// Every build keeps all UI-related MonoBehaviours and only the selected Hanger 51 type range.
    /// All other external/package MonoBehaviours are removed. The real scene is never modified.
    /// </summary>
    public static class Hanger51HangerUiTypeIsolationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";
        private const string HangerRoot = "Assets/_Project/";

        [MenuItem("Hanger 51/Build/49 - Report Hanger51 Runtime Script Types")]
        public static void ReportHangerTypes()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Hanger/UI type report failed: no valid active scene is open.");
                return;
            }

            Dictionary<string, int> hangerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int uiCount = 0;
            int otherCount = 0;

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.gameObject.scene != scene) continue;

                if (IsHanger51Behaviour(behaviour))
                {
                    string typeName = GetTypeName(behaviour);
                    hangerCounts.TryGetValue(typeName, out int count);
                    hangerCounts[typeName] = count + 1;
                }
                else if (IsUiRelated(behaviour))
                {
                    uiCount++;
                }
                else
                {
                    otherCount++;
                }
            }

            List<string> typeNames = new List<string>(hangerCounts.Keys);
            typeNames.Sort(StringComparer.Ordinal);

            List<string> lines = new List<string>();
            for (int index = 0; index < typeNames.Count; index++)
            {
                string typeName = typeNames[index];
                lines.Add($"  [{index:D2}] {hangerCounts[typeName],3} x {typeName}");
            }

            Debug.Log(
                $"Hanger/UI type report for '{scene.path}': Hanger runtime types={typeNames.Count}, "
                + $"Hanger instances={SumCounts(hangerCounts)}, UI instances={uiCount}, other external={otherCount}.\n"
                + string.Join("\n", lines));
        }

        [MenuItem("Hanger 51/Build/50 - UI + Hanger Type Half A")]
        public static void BuildHalfA() => BuildRange("HalfA", 0, 1, 2);

        [MenuItem("Hanger 51/Build/51 - UI + Hanger Type Half B")]
        public static void BuildHalfB() => BuildRange("HalfB", 1, 2, 2);

        [MenuItem("Hanger 51/Build/52 - UI + Hanger Type Quarter 1")]
        public static void BuildQuarter1() => BuildRange("Quarter1", 0, 1, 4);

        [MenuItem("Hanger 51/Build/53 - UI + Hanger Type Quarter 2")]
        public static void BuildQuarter2() => BuildRange("Quarter2", 1, 2, 4);

        [MenuItem("Hanger 51/Build/54 - UI + Hanger Type Quarter 3")]
        public static void BuildQuarter3() => BuildRange("Quarter3", 2, 3, 4);

        [MenuItem("Hanger 51/Build/55 - UI + Hanger Type Quarter 4")]
        public static void BuildQuarter4() => BuildRange("Quarter4", 3, 4, 4);

        private static void BuildRange(string label, int numeratorStart, int numeratorEnd, int denominator)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Hanger/UI type diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Hanger/UI type diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Hanger/UI type diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Hanger/UI type diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            List<string> allHangerTypes = CollectSortedHangerTypes(sourceScene);
            if (allHangerTypes.Count == 0)
            {
                Debug.LogError("Hanger/UI type diagnostic found zero Hanger 51 runtime MonoBehaviour types.");
                return;
            }

            int startIndex = Mathf.FloorToInt(allHangerTypes.Count * (numeratorStart / (float)denominator));
            int endIndex = numeratorEnd == denominator
                ? allHangerTypes.Count
                : Mathf.FloorToInt(allHangerTypes.Count * (numeratorEnd / (float)denominator));
            endIndex = Mathf.Clamp(endIndex, startIndex + 1, allHangerTypes.Count);

            HashSet<string> allowedHangerTypes = new HashSet<string>(StringComparer.Ordinal);
            List<string> allowedTypeList = new List<string>();
            for (int index = startIndex; index < endIndex; index++)
            {
                string typeName = allHangerTypes[index];
                allowedHangerTypes.Add(typeName);
                allowedTypeList.Add(typeName);
            }

            string variantFolder = "HangerUI_Types_" + label;
            string tempScenePath = TempSceneFolder + "/__Hanger51UiTypes" + label + ".unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, variantFolder);
            string outputPath = Path.Combine(outputFolder, "TheHanger51_UI_Types_" + label + ".exe");
            string logPath = Path.Combine(outputFolder, "UI_Types_" + label + "_Player.log");
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                GameObject[] sourceRoots = sourceScene.GetRootGameObjects();

                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);
                }

                StripToUiAndSelectedHangerTypes(diagnosticScene, allowedHangerTypes);

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Hanger/UI type diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                AuditSavedDiagnosticScene(diagnosticScene, label, allowedHangerTypes, allHangerTypes.Count);

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"Hanger/UI '{label}' keeps Hanger type indexes [{startIndex}..{endIndex - 1}] "
                    + $"of {allHangerTypes.Count} total types:\n  "
                    + string.Join("\n  ", allowedTypeList));

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
                        : $"Result={report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.";
                    Debug.LogError($"Hanger/UI type diagnostic '{label}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"Hanger/UI type diagnostic '{label}' build passed. Size={report.summary.totalSize} bytes; "
                    + $"duration={report.summary.totalTime}. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Hanger/UI type diagnostic '{label}' failed unexpectedly.\n{exception}");
            }
            finally
            {
                if (diagnosticScene.IsValid() && diagnosticScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(diagnosticScene, true);
                }
                DeleteTemporaryScene(tempScenePath);
            }
        }

        private static List<string> CollectSortedHangerTypes(Scene scene)
        {
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null || !IsHanger51Behaviour(behaviour)) continue;
                    unique.Add(GetTypeName(behaviour));
                }
            }

            List<string> result = new List<string>(unique);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static void StripToUiAndSelectedHangerTypes(Scene scene, HashSet<string> allowedHangerTypes)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    if (IsUiRelated(behaviour))
                    {
                        continue;
                    }

                    if (IsHanger51Behaviour(behaviour)
                        && allowedHangerTypes.Contains(GetTypeName(behaviour)))
                    {
                        continue;
                    }

                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void AuditSavedDiagnosticScene(
            Scene scene,
            string label,
            HashSet<string> allowedTypes,
            int totalHangerTypeCount)
        {
            int hangerInstances = 0;
            int uiInstances = 0;
            int otherInstances = 0;
            HashSet<string> hangerTypesPresent = new HashSet<string>(StringComparer.Ordinal);

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    if (IsHanger51Behaviour(behaviour))
                    {
                        hangerInstances++;
                        hangerTypesPresent.Add(GetTypeName(behaviour));
                    }
                    else if (IsUiRelated(behaviour))
                    {
                        uiInstances++;
                    }
                    else
                    {
                        otherInstances++;
                    }
                }
            }

            Debug.Log(
                $"Hanger/UI saved-scene audit '{label}': HangerInstances={hangerInstances}, "
                + $"HangerTypes={hangerTypesPresent.Count}/{totalHangerTypeCount} "
                + $"(expected selected={allowedTypes.Count}), UI={uiInstances}, OtherExternal={otherInstances}.");

            if (otherInstances != 0 || hangerTypesPresent.Count != allowedTypes.Count)
            {
                Debug.LogError(
                    $"Hanger/UI diagnostic '{label}' audit mismatch. The diagnostic scene does not contain "
                    + "exactly the requested script groups; do not trust this build result.");
            }
        }

        private static bool IsHanger51Behaviour(MonoBehaviour behaviour)
        {
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            string path = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
            if (!string.IsNullOrEmpty(path)
                && path.StartsWith(HangerRoot, StringComparison.OrdinalIgnoreCase)
                && path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            string ns = behaviour.GetType().Namespace ?? string.Empty;
            return ns.StartsWith("Hanger51", StringComparison.Ordinal)
                && (string.IsNullOrEmpty(path)
                    || path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static bool IsUiRelated(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
            string ns = type.Namespace ?? string.Empty;
            string assembly = type.Assembly.GetName().Name ?? string.Empty;

            if (ns.StartsWith("UnityEngine.UI", StringComparison.Ordinal)
                || ns.StartsWith("UnityEngine.EventSystems", StringComparison.Ordinal)
                || ns.StartsWith("TMPro", StringComparison.Ordinal)
                || ns.StartsWith("Unity.AppUI", StringComparison.Ordinal))
            {
                return true;
            }

            if (assembly.IndexOf("UnityEngine.UI", StringComparison.OrdinalIgnoreCase) >= 0
                || assembly.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0
                || assembly.IndexOf("AppUI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return scriptPath.IndexOf("com.unity.ugui", StringComparison.OrdinalIgnoreCase) >= 0
                || scriptPath.IndexOf("com.unity.textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0
                || scriptPath.IndexOf("com.unity.dt.app-ui", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTypeName(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();
            return type.FullName ?? type.Name;
        }

        private static int SumCounts(Dictionary<string, int> counts)
        {
            int total = 0;
            foreach (KeyValuePair<string, int> pair in counts) total += pair.Value;
            return total;
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
                Debug.LogError($"Hanger/UI diagnostic EXE not found at '{executablePath}'.");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = $"-logFile \"{absoluteLogPath}\" -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo);
            if (process == null)
            {
                Debug.LogError("Hanger/UI diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log($"Launched Hanger/UI diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
        }

        private static void DeleteTemporaryScene(string tempScenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(tempScenePath) != null)
            {
                AssetDatabase.DeleteAsset(tempScenePath);
                AssetDatabase.Refresh();
            }
        }
    }
}
