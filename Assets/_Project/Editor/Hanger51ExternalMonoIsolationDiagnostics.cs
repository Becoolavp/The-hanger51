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
    /// Further isolates the standalone level0 corruption after proving that a fresh scene containing
    /// only Hanger 51 MonoBehaviours runs while the same fresh scene containing non-Hanger scripts
    /// crashes. These tests split external MonoBehaviours into UI-related and other package types.
    /// The user's real scene is never modified.
    /// </summary>
    public static class Hanger51ExternalMonoIsolationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";

        private enum MonoSelection
        {
            RemoveUi,
            KeepOnlyUi,
            KeepOnlyOtherExternal
        }

        [MenuItem("Hanger 51/Build/25 - Report External MonoBehaviour Types")]
        public static void ReportExternalMonoBehaviourTypes()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("External MonoBehaviour report failed: no valid active scene is open.");
                return;
            }

            Dictionary<string, int> counts = new Dictionary<string, int>();
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int hangerCount = 0;
            int uiCount = 0;
            int otherExternalCount = 0;

            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.gameObject.scene != scene) continue;

                MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                Type type = behaviour.GetType();

                if (IsHanger51(type, scriptPath))
                {
                    hangerCount++;
                    continue;
                }

                bool ui = IsUiRelated(type, scriptPath);
                if (ui) uiCount++;
                else otherExternalCount++;

                string key = $"{(ui ? "UI" : "OTHER")} | {type.FullName} | {type.Assembly.GetName().Name} | {scriptPath}";
                counts.TryGetValue(key, out int current);
                counts[key] = current + 1;
            }

            List<string> lines = new List<string>(counts.Count);
            foreach (KeyValuePair<string, int> pair in counts)
            {
                lines.Add($"{pair.Value,3} x {pair.Key}");
            }
            lines.Sort(StringComparer.OrdinalIgnoreCase);

            Debug.Log(
                $"External MonoBehaviour report for '{scene.path}': Hanger51={hangerCount}, UI-related={uiCount}, "
                + $"other external/package={otherExternalCount}.\n" + string.Join("\n", lines));
        }

        [MenuItem("Hanger 51/Build/26 - Fresh Scene WITHOUT UI MonoBehaviours")]
        public static void BuildWithoutUiMonoBehaviours()
        {
            BuildVariant(
                MonoSelection.RemoveUi,
                "ExternalWithoutUI",
                "__Hanger51ExternalWithoutUI.unity",
                "TheHanger51_ExternalWithoutUI.exe",
                "ExternalWithoutUI_Player.log");
        }

        [MenuItem("Hanger 51/Build/27 - Fresh Scene ONLY UI MonoBehaviours")]
        public static void BuildOnlyUiMonoBehaviours()
        {
            BuildVariant(
                MonoSelection.KeepOnlyUi,
                "ExternalOnlyUI",
                "__Hanger51ExternalOnlyUI.unity",
                "TheHanger51_ExternalOnlyUI.exe",
                "ExternalOnlyUI_Player.log");
        }

        [MenuItem("Hanger 51/Build/28 - Fresh Scene ONLY Other External MonoBehaviours")]
        public static void BuildOnlyOtherExternalMonoBehaviours()
        {
            BuildVariant(
                MonoSelection.KeepOnlyOtherExternal,
                "ExternalOnlyOther",
                "__Hanger51ExternalOnlyOther.unity",
                "TheHanger51_ExternalOnlyOther.exe",
                "ExternalOnlyOther_Player.log");
        }

        [MenuItem("Hanger 51/Build/29 - Reveal External UI Diagnostic Logs")]
        public static void RevealExternalDiagnosticFolder()
        {
            string folder = Path.GetFullPath(DiagnosticsRoot);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        private static void BuildVariant(
            MonoSelection selection,
            string variantFolder,
            string tempSceneFile,
            string executableName,
            string logName)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("External MonoBehaviour diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("External MonoBehaviour diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("External MonoBehaviour diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("External MonoBehaviour diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            string tempScenePath = TempSceneFolder + "/" + tempSceneFile;
            string outputFolder = Path.Combine(DiagnosticsRoot, variantFolder);
            string outputPath = Path.Combine(outputFolder, executableName);
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError("External MonoBehaviour diagnostic could not create a fresh empty scene.");
                    return;
                }

                int clonedRoots = 0;
                int removedHanger = 0;
                int removedUi = 0;
                int removedOther = 0;
                int keptHanger = 0;
                int keptUi = 0;
                int keptOther = 0;

                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);
                    clonedRoots++;

                    MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int index = behaviours.Length - 1; index >= 0; index--)
                    {
                        MonoBehaviour behaviour = behaviours[index];
                        if (behaviour == null) continue;

                        MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                        string path = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                        Type type = behaviour.GetType();
                        bool hanger = IsHanger51(type, path);
                        bool ui = !hanger && IsUiRelated(type, path);
                        bool other = !hanger && !ui;

                        bool keep;
                        switch (selection)
                        {
                            case MonoSelection.RemoveUi:
                                keep = !ui;
                                break;
                            case MonoSelection.KeepOnlyUi:
                                keep = ui;
                                break;
                            case MonoSelection.KeepOnlyOtherExternal:
                                keep = other;
                                break;
                            default:
                                keep = true;
                                break;
                        }

                        if (keep)
                        {
                            if (hanger) keptHanger++;
                            else if (ui) keptUi++;
                            else keptOther++;
                            continue;
                        }

                        if (hanger) removedHanger++;
                        else if (ui) removedUi++;
                        else removedOther++;
                        Object.DestroyImmediate(behaviour);
                    }
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"External MonoBehaviour diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"External Mono diagnostic '{variantFolder}' cloned {clonedRoots} roots into a fresh scene. "
                    + $"KEPT Hanger51={keptHanger}, UI={keptUi}, OtherExternal={keptOther}; "
                    + $"REMOVED Hanger51={removedHanger}, UI={removedUi}, OtherExternal={removedOther}.");

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
                    Debug.LogError($"External Mono diagnostic '{variantFolder}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"External Mono diagnostic '{variantFolder}' build passed. Size={report.summary.totalSize} bytes; "
                    + $"duration={report.summary.totalTime}. Launching now.");

                Launch(outputPath, Path.Combine(outputFolder, logName));
            }
            catch (Exception exception)
            {
                Debug.LogError($"External Mono diagnostic '{variantFolder}' failed unexpectedly.\n{exception}");
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

        private static bool IsHanger51(Type type, string scriptPath)
        {
            if (!string.IsNullOrEmpty(scriptPath) && scriptPath.StartsWith("Assets/_Project/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string ns = type != null ? type.Namespace : string.Empty;
            return !string.IsNullOrEmpty(ns) && ns.StartsWith("Hanger51", StringComparison.Ordinal);
        }

        private static bool IsUiRelated(Type type, string scriptPath)
        {
            string ns = type != null ? type.Namespace ?? string.Empty : string.Empty;
            string assembly = type != null ? type.Assembly.GetName().Name ?? string.Empty : string.Empty;
            string path = scriptPath ?? string.Empty;

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

            return path.IndexOf("com.unity.ugui", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("com.unity.textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("com.unity.dt.app-ui", StringComparison.OrdinalIgnoreCase) >= 0;
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
                Debug.LogError($"External Mono diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("External Mono diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log($"Launched external Mono diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
