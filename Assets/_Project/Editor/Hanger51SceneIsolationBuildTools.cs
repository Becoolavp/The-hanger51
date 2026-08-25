using System;
using System.Diagnostics;
using System.IO;
using Hanger51.Aircraft;
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
    /// Builds a temporary copy of the real game scene after removing only the P-51 wing-armament
    /// hierarchy/components. This is intentionally an isolation diagnostic: the user's saved scene
    /// is never modified. If this build runs while the normal build produces a corrupt level0,
    /// the generated armament scene data is the confirmed corruption boundary.
    /// </summary>
    public static class Hanger51SceneIsolationBuildTools
    {
        private const string BuildFolder = "Builds/Windows";
        private const string DiagnosticScenePath =
            "Assets/_Project/Scenes/__Hanger51WithoutArmamentDiagnostic.unity";
        private const string ExecutableName = "TheHanger51_NoArmamentDiagnostic.exe";
        private const string LogName = "TheHanger51_NoArmamentDiagnostic_Player.log";
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";

        [MenuItem("Hanger 51/Build/8 - Build Real Scene Without Armament")]
        public static void BuildRealSceneWithoutArmament()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Armament-isolation build failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Armament-isolation build failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Armament-isolation build failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Armament-isolation build failed. Unity could not save the open scenes.");
                return;
            }

            Scene diagnosticScene = default;
            try
            {
                DeleteDiagnosticSceneIfPresent();

                if (!AssetDatabase.CopyAsset(sourceScene.path, DiagnosticScenePath))
                {
                    Debug.LogError(
                        $"Armament-isolation build could not copy '{sourceScene.path}' to "
                        + $"'{DiagnosticScenePath}'.");
                    return;
                }
                AssetDatabase.Refresh();

                diagnosticScene = EditorSceneManager.OpenScene(
                    DiagnosticScenePath,
                    OpenSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError("Armament-isolation build could not open its temporary scene copy.");
                    return;
                }

                int removedObjects = RemoveArmamentFromScene(diagnosticScene);
                if (!EditorSceneManager.SaveScene(diagnosticScene, DiagnosticScenePath, false))
                {
                    Debug.LogError("Armament-isolation build could not save the sanitized temporary scene.");
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

                string outputPath = Path.Combine(BuildFolder, ExecutableName);
                Debug.Log(
                    $"Armament-isolation build started. Removed {removedObjects} armament scene "
                    + "object/component(s) from a TEMPORARY copy of the real scene. The player's "
                    + "saved scene was not changed.");

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { DiagnosticScenePath },
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
                    BuildSummary failedSummary = report != null ? report.summary : default;
                    Debug.LogError(
                        "Armament-isolation build failed. "
                        + (report == null
                            ? "Unity returned no BuildReport."
                            : $"Result: {failedSummary.result}; errors: {failedSummary.totalErrors}; "
                              + $"warnings: {failedSummary.totalWarnings}."));
                    return;
                }

                Debug.Log(
                    $"Armament-isolation build passed. Build size: {report.summary.totalSize} bytes. "
                    + $"Duration: {report.summary.totalTime}. Launching diagnostic player now.");
                Launch(outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("Armament-isolation build failed unexpectedly.\n" + exception);
            }
            finally
            {
                if (diagnosticScene.IsValid() && diagnosticScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(diagnosticScene, true);
                }
                DeleteDiagnosticSceneIfPresent();
            }
        }

        [MenuItem("Hanger 51/Build/9 - Reveal Armament Isolation Log")]
        public static void RevealArmamentIsolationLog()
        {
            string logPath = Path.GetFullPath(Path.Combine(BuildFolder, LogName));
            if (File.Exists(logPath))
            {
                EditorUtility.RevealInFinder(logPath);
                Debug.Log($"Armament-isolation Player log: {logPath}");
                return;
            }

            string folder = Path.GetFullPath(BuildFolder);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
            }
            Debug.LogWarning(
                $"No armament-isolation Player log exists yet at '{logPath}'. Run Build Step 8 first.");
        }

        private static int RemoveArmamentFromScene(Scene scene)
        {
            int removed = 0;
            GameObject[] roots = scene.GetRootGameObjects();

            // The system itself lives on the aircraft root, while the generated panels, bays,
            // mounts, visuals and service targets live under the named armament child hierarchy.
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null) continue;

                P51WingArmamentPlayerInteractor[] interactors =
                    root.GetComponentsInChildren<P51WingArmamentPlayerInteractor>(true);
                for (int index = 0; index < interactors.Length; index++)
                {
                    if (interactors[index] == null) continue;
                    Object.DestroyImmediate(interactors[index]);
                    removed++;
                }

                P51WingArmamentRuntimePerformanceGuard[] guards =
                    root.GetComponentsInChildren<P51WingArmamentRuntimePerformanceGuard>(true);
                for (int index = 0; index < guards.Length; index++)
                {
                    if (guards[index] == null) continue;
                    Object.DestroyImmediate(guards[index]);
                    removed++;
                }

                P51WingArmamentSystem[] systems =
                    root.GetComponentsInChildren<P51WingArmamentSystem>(true);
                for (int index = 0; index < systems.Length; index++)
                {
                    if (systems[index] == null) continue;
                    Object.DestroyImmediate(systems[index]);
                    removed++;
                }
            }

            // Delete the whole generated visual/service hierarchy only after removing the component
            // living on the aircraft root. Searching by transform name is safe inside the temporary
            // scene because Step 32 owns this exact hierarchy name.
            roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = transforms.Length - 1; index >= 0; index--)
                {
                    Transform candidate = transforms[index];
                    if (candidate == null || candidate.name != ArmamentRootName) continue;
                    Object.DestroyImmediate(candidate.gameObject);
                    removed++;
                    break;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"Temporary armament-isolation scene sanitized: removed {removed} armament "
                + $"object/component(s). Aircraft lookup target: '{AircraftRootName}'.");
            return removed;
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
                    "Armament-isolation build could not clean Builds/Windows. Make sure an older "
                    + "TheHanger51 player is closed, then try again.\n" + exception);
                return false;
            }
        }

        private static void Launch(string outputPath)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string logPath = Path.GetFullPath(Path.Combine(BuildFolder, LogName));
            if (!File.Exists(executablePath))
            {
                Debug.LogError($"Armament-isolation EXE was not found at '{executablePath}'.");
                return;
            }

            if (File.Exists(logPath))
            {
                try { File.Delete(logPath); }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not clear the previous isolation log: {exception.Message}");
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
                Debug.LogError("Windows did not return an armament-isolation Player process.");
                return;
            }

            Debug.Log(
                $"Launched real-scene-without-armament diagnostic build (PID {process.Id}). "
                + $"Log: '{logPath}'. If this stays open, armament scene serialization is confirmed "
                + "as the corruption boundary.");
        }

        private static void DeleteDiagnosticSceneIfPresent()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DiagnosticScenePath) != null)
            {
                AssetDatabase.DeleteAsset(DiagnosticScenePath);
                AssetDatabase.Refresh();
            }
        }
    }
}
