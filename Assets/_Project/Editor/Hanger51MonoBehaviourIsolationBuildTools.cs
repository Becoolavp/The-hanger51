using System;
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
    /// Produces temporary standalone builds that progressively strip MonoBehaviour serialization
    /// from the real game scene. These builds are diagnostic only and never modify the user's saved
    /// scene. They are designed specifically for a player that reports level0 as corrupted before
    /// frame one while an empty diagnostic scene runs normally.
    /// </summary>
    public static class Hanger51MonoBehaviourIsolationBuildTools
    {
        private const string BuildFolder = "Builds/Windows";

        private const string ProjectScenePath =
            "Assets/_Project/Scenes/__Hanger51WithoutProjectScriptsDiagnostic.unity";
        private const string ProjectExeName = "TheHanger51_NoProjectScriptsDiagnostic.exe";
        private const string ProjectLogName = "TheHanger51_NoProjectScriptsDiagnostic_Player.log";

        private const string AllScenePath =
            "Assets/_Project/Scenes/__Hanger51WithoutAnyMonoBehavioursDiagnostic.unity";
        private const string AllExeName = "TheHanger51_NoMonoBehavioursDiagnostic.exe";
        private const string AllLogName = "TheHanger51_NoMonoBehavioursDiagnostic_Player.log";

        [MenuItem("Hanger 51/Build/11 - Build Without Hanger 51 Scripts")]
        public static void BuildWithoutProjectScripts()
        {
            BuildIsolation(
                false,
                ProjectScenePath,
                ProjectExeName,
                ProjectLogName,
                "project-script isolation");
        }

        [MenuItem("Hanger 51/Build/12 - Reveal Hanger 51 Script Isolation Log")]
        public static void RevealProjectScriptIsolationLog()
        {
            RevealLog(ProjectLogName, "project-script isolation");
        }

        [MenuItem("Hanger 51/Build/13 - Build Without Any MonoBehaviours")]
        public static void BuildWithoutAnyMonoBehaviours()
        {
            BuildIsolation(
                true,
                AllScenePath,
                AllExeName,
                AllLogName,
                "all-MonoBehaviour isolation");
        }

        [MenuItem("Hanger 51/Build/14 - Reveal All-MonoBehaviour Isolation Log")]
        public static void RevealAllMonoBehaviourIsolationLog()
        {
            RevealLog(AllLogName, "all-MonoBehaviour isolation");
        }

        private static void BuildIsolation(
            bool removeAllMonoBehaviours,
            string diagnosticScenePath,
            string executableName,
            string logName,
            string label)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError($"{label} build failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError($"{label} build failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError($"{label} build failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError($"{label} build failed. Unity could not save the currently open scenes.");
                return;
            }

            Scene diagnosticScene = default;
            try
            {
                DeleteDiagnosticSceneIfPresent(diagnosticScenePath);

                if (!AssetDatabase.CopyAsset(sourceScene.path, diagnosticScenePath))
                {
                    Debug.LogError(
                        $"{label} build could not copy '{sourceScene.path}' to '{diagnosticScenePath}'.");
                    return;
                }
                AssetDatabase.Refresh();

                diagnosticScene = EditorSceneManager.OpenScene(
                    diagnosticScenePath,
                    OpenSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError($"{label} build could not open its temporary scene copy.");
                    return;
                }

                int removed = StripMonoBehaviours(diagnosticScene, removeAllMonoBehaviours);
                if (!EditorSceneManager.SaveScene(diagnosticScene, diagnosticScenePath, false))
                {
                    Debug.LogError($"{label} build could not save its stripped temporary scene.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!PrepareCleanBuildFolder())
                {
                    return;
                }

                string outputPath = Path.Combine(BuildFolder, executableName);
                Debug.Log(
                    $"{label} build started. Removed {removed} MonoBehaviour component(s) from a "
                    + "TEMPORARY copy of the real scene. Your saved scene was not modified.");

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { diagnosticScenePath },
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
                    BuildSummary summary = report != null ? report.summary : default;
                    Debug.LogError(
                        $"{label} build failed. "
                        + (report == null
                            ? "Unity returned no BuildReport."
                            : $"Result: {summary.result}; errors: {summary.totalErrors}; warnings: {summary.totalWarnings}."));
                    return;
                }

                Debug.Log(
                    $"{label} build passed. Build size: {report.summary.totalSize} bytes. "
                    + $"Duration: {report.summary.totalTime}. Launching now.");
                Launch(outputPath, logName, label);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{label} build failed unexpectedly.\n{exception}");
            }
            finally
            {
                if (diagnosticScene.IsValid() && diagnosticScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(diagnosticScene, true);
                }
                DeleteDiagnosticSceneIfPresent(diagnosticScenePath);
            }
        }

        private static int StripMonoBehaviours(Scene scene, bool removeAllMonoBehaviours)
        {
            int removed = 0;
            int retained = 0;
            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null) continue;

                MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    bool shouldRemove = removeAllMonoBehaviours || IsProjectMonoBehaviour(behaviour);
                    if (!shouldRemove)
                    {
                        retained++;
                        continue;
                    }

                    try
                    {
                        Object.DestroyImmediate(behaviour, true);
                        removed++;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"[MonoBehaviour Isolation] Could not remove '{behaviour.GetType().FullName}' "
                            + $"from '{GetPath(behaviour.transform)}': {exception.Message}",
                            behaviour);
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"[MonoBehaviour Isolation] Temporary scene strip complete. Removed {removed}; "
                + $"retained {retained}; mode={(removeAllMonoBehaviours ? "ALL" : "Assets/_Project only")}.");
            return removed;
        }

        private static bool IsProjectMonoBehaviour(MonoBehaviour behaviour)
        {
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null)
            {
                // A MonoBehaviour that has no resolvable MonoScript is exactly the kind of scene data
                // this diagnostic is intended to exclude.
                return true;
            }

            string path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith("Assets/_Project/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PrepareCleanBuildFolder()
        {
            try
            {
                if (Directory.Exists(BuildFolder))
                {
                    Directory.Delete(BuildFolder, true);
                }
                Directory.CreateDirectory(BuildFolder);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Could not clean Builds/Windows. Make sure every older Hanger 51 diagnostic "
                    + "player is closed, then try again.\n" + exception);
                return false;
            }
        }

        private static void Launch(string outputPath, string logName, string label)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string logPath = Path.GetFullPath(Path.Combine(BuildFolder, logName));
            if (!File.Exists(executablePath))
            {
                Debug.LogError($"{label} EXE was not found at '{executablePath}'.");
                return;
            }

            if (File.Exists(logPath))
            {
                try { File.Delete(logPath); }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not clear the previous {label} log: {exception.Message}");
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = $"-logFile \"{logPath}\" -force-d3d11 -screen-fullscreen 0 "
                    + "-screen-width 1600 -screen-height 900",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo);
            if (process == null)
            {
                Debug.LogError($"Windows did not return a process for the {label} player.");
                return;
            }

            Debug.Log(
                $"Launched {label} Windows player (PID {process.Id}). Diagnostics: '{logPath}'.");
        }

        private static void RevealLog(string logName, string label)
        {
            string logPath = Path.GetFullPath(Path.Combine(BuildFolder, logName));
            if (File.Exists(logPath))
            {
                EditorUtility.RevealInFinder(logPath);
                Debug.Log($"{label} Player log: {logPath}");
                return;
            }

            string folder = Path.GetFullPath(BuildFolder);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
            }
            Debug.LogWarning($"No {label} Player log exists yet at '{logPath}'.");
        }

        private static void DeleteDiagnosticSceneIfPresent(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null) return "<no transform>";
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
