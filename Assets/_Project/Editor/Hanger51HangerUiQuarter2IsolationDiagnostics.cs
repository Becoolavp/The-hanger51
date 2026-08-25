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
    /// Continues the Hanger/UI type binary search after Quarter 2 (indexes 15..30 of 62)
    /// was proven to reproduce the standalone level0 corruption. Every diagnostic keeps
    /// all Unity UI MonoBehaviours plus only an exact Hanger 51 type-index range.
    /// All other MonoBehaviours are removed from a temporary fresh scene.
    /// </summary>
    public static class Hanger51HangerUiQuarter2IsolationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";
        private const string HangerRoot = "Assets/_Project/";

        [MenuItem("Hanger 51/Build/56 - UI + Hanger Q2 First 8 Types [15-22]")]
        public static void BuildQ2First8() => BuildExactRange("Q2_First8", 15, 23);

        [MenuItem("Hanger 51/Build/57 - UI + Hanger Q2 Second 8 Types [23-30]")]
        public static void BuildQ2Second8() => BuildExactRange("Q2_Second8", 23, 31);

        [MenuItem("Hanger 51/Build/58 - UI + Hanger Types [15-18]")]
        public static void Build15To18() => BuildExactRange("Types_15_18", 15, 19);

        [MenuItem("Hanger 51/Build/59 - UI + Hanger Types [19-22]")]
        public static void Build19To22() => BuildExactRange("Types_19_22", 19, 23);

        [MenuItem("Hanger 51/Build/60 - UI + Hanger Types [23-26]")]
        public static void Build23To26() => BuildExactRange("Types_23_26", 23, 27);

        [MenuItem("Hanger 51/Build/61 - UI + Hanger Types [27-30]")]
        public static void Build27To30() => BuildExactRange("Types_27_30", 27, 31);

        private static void BuildExactRange(string label, int startIndex, int endExclusive)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Hanger/UI Q2 diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Hanger/UI Q2 diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Hanger/UI Q2 diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Hanger/UI Q2 diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            List<string> allHangerTypes = CollectSortedHangerTypes(sourceScene);
            if (allHangerTypes.Count <= startIndex)
            {
                Debug.LogError($"Hanger/UI Q2 diagnostic expected more than {startIndex} Hanger types but found {allHangerTypes.Count}.");
                return;
            }

            endExclusive = Mathf.Min(endExclusive, allHangerTypes.Count);
            HashSet<string> allowedTypes = new HashSet<string>(StringComparer.Ordinal);
            List<string> selectedNames = new List<string>();
            for (int index = startIndex; index < endExclusive; index++)
            {
                string typeName = allHangerTypes[index];
                allowedTypes.Add(typeName);
                selectedNames.Add($"[{index:D2}] {typeName}");
            }

            string tempScenePath = TempSceneFolder + "/__Hanger51UiQ2_" + label + ".unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, "HangerUI_Q2_" + label);
            string outputPath = Path.Combine(outputFolder, "TheHanger51_UI_Q2_" + label + ".exe");
            string logPath = Path.Combine(outputFolder, "UI_Q2_" + label + "_Player.log");
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

                StripToUiAndSelectedHangerTypes(diagnosticScene, allowedTypes);

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Hanger/UI Q2 diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                Audit(diagnosticScene, label, allowedTypes.Count);
                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"Hanger/UI Q2 '{label}' keeps exact Hanger indexes [{startIndex}..{endExclusive - 1}] of {allHangerTypes.Count}:\n  "
                    + string.Join("\n  ", selectedNames));

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
                    Debug.LogError($"Hanger/UI Q2 diagnostic '{label}' build failed. {failure}");
                    return;
                }

                Debug.Log($"Hanger/UI Q2 diagnostic '{label}' build passed. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Hanger/UI Q2 diagnostic '{label}' failed unexpectedly.\n{exception}");
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

        private static void StripToUiAndSelectedHangerTypes(Scene scene, HashSet<string> allowedTypes)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;
                    if (IsUiRelated(behaviour)) continue;
                    if (IsHanger51Behaviour(behaviour) && allowedTypes.Contains(GetTypeName(behaviour))) continue;
                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void Audit(Scene scene, string label, int expectedHangerTypeCount)
        {
            int hangerInstances = 0;
            int uiInstances = 0;
            int otherInstances = 0;
            HashSet<string> hangerTypes = new HashSet<string>(StringComparer.Ordinal);

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
                        hangerTypes.Add(GetTypeName(behaviour));
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
                $"Hanger/UI Q2 saved-scene audit '{label}': HangerInstances={hangerInstances}, HangerTypes={hangerTypes.Count} "
                + $"(expected={expectedHangerTypeCount}), UI={uiInstances}, OtherExternal={otherInstances}.");

            if (hangerTypes.Count != expectedHangerTypeCount || otherInstances != 0)
            {
                Debug.LogError($"Hanger/UI Q2 diagnostic '{label}' audit mismatch. Do not trust this build result.");
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
                && (string.IsNullOrEmpty(path) || path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0);
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
                || ns.StartsWith("Unity.AppUI", StringComparison.Ordinal)) return true;

            if (assembly.IndexOf("UnityEngine.UI", StringComparison.OrdinalIgnoreCase) >= 0
                || assembly.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0
                || assembly.IndexOf("AppUI", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return scriptPath.IndexOf("com.unity.ugui", StringComparison.OrdinalIgnoreCase) >= 0
                || scriptPath.IndexOf("com.unity.textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0
                || scriptPath.IndexOf("com.unity.dt.app-ui", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTypeName(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();
            return type.FullName ?? type.Name;
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
                Debug.LogError($"Hanger/UI Q2 diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("Hanger/UI Q2 diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log($"Launched Hanger/UI Q2 diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
