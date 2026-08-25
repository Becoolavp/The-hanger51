using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace Hanger51.EditorTools
{
    public static class Hanger51BuildTools
    {
        private const string BuildFolder = "Builds/Windows";
        private const string ExecutableName = "TheHanger51.exe";
        private const string PlayerLogName = "TheHanger51_Player.log";

        [MenuItem("Hanger 51/Build/1 - Prepare Current Scene for Build")]
        public static void PrepareCurrentSceneForBuildMenu()
        {
            PrepareCurrentSceneForBuild(true);
        }

        [MenuItem("Hanger 51/Build/2 - Validate Build Setup")]
        public static void ValidateBuildSetupMenu()
        {
            ValidateBuildSetup(true);
        }

        [MenuItem("Hanger 51/Build/3 - Build and Run Windows")]
        public static void BuildAndRunWindows()
        {
            if (!PrepareCurrentSceneForBuild(false))
            {
                return;
            }

            if (!ValidateBuildSetup(false))
            {
                return;
            }

            string[] scenePaths = GetEnabledBuildScenePaths();
            if (scenePaths.Length == 0)
            {
                Debug.LogError("Build Step 3 failed. There are no enabled scenes to build.");
                return;
            }

            if (!PrepareCleanBuildFolder())
            {
                return;
            }

            string outputPath = Path.Combine(BuildFolder, ExecutableName);

            // Do not use BuildOptions.AutoRunPlayer here. Unity reports a successful build even if
            // the standalone player immediately crashes, and AutoRunPlayer gives us no reliable
            // place to force a diagnostic Player.log. Build first, then launch the completed EXE
            // ourselves with development-friendly command-line options.
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.DetailedBuildReport | BuildOptions.Development
            };

            Debug.Log($"Build Step 3 started. Building {scenePaths.Length} scene(s) to '{outputPath}'.");

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"Build Step 3 failed. Result: {summary.result}. "
                    + $"Errors: {summary.totalErrors}. Warnings: {summary.totalWarnings}.");
                return;
            }

            Debug.Log(
                $"Build Step 3 build passed. Built '{outputPath}'. "
                + $"Build size: {summary.totalSize} bytes. Duration: {summary.totalTime}.");

            LaunchBuiltPlayer(outputPath, true);
        }

        [MenuItem("Hanger 51/Build/4 - Run Last Windows Build (Diagnostic)")]
        public static void RunLastWindowsBuildDiagnostic()
        {
            string outputPath = Path.Combine(BuildFolder, ExecutableName);
            if (!File.Exists(outputPath))
            {
                Debug.LogError(
                    $"No Windows build exists at '{outputPath}'. Run Build Step 3 first.");
                return;
            }

            LaunchBuiltPlayer(outputPath, true);
        }

        [MenuItem("Hanger 51/Build/5 - Reveal Last Player Log")]
        public static void RevealLastPlayerLog()
        {
            string logPath = Path.GetFullPath(Path.Combine(BuildFolder, PlayerLogName));
            if (File.Exists(logPath))
            {
                EditorUtility.RevealInFinder(logPath);
                Debug.Log($"Last standalone Player log: {logPath}");
                return;
            }

            string folder = Path.GetFullPath(BuildFolder);
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
            }
            Debug.LogWarning(
                $"No dedicated standalone Player log exists yet at '{logPath}'. "
                + "Run Build Step 3 or Build Step 4 once, then use this command again.");
        }

        public static bool PrepareCurrentSceneForBuild(bool logSuccess)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Build Step 1 failed. Exit Play mode before preparing a build.");
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Build Step 1 failed. Wait for Unity to finish compiling.");
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("Build Step 1 failed. Unity does not have a valid active scene.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(activeScene.path))
            {
                Debug.LogError(
                    "Build Step 1 failed. The active scene has never been saved. "
                    + "Use File > Save As before building.");
                return false;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Build Step 1 failed. Unity could not save all open scenes.");
                return false;
            }

            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(scene => scene.path == activeScene.path);
            scenes.Insert(0, new EditorBuildSettingsScene(activeScene.path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();

            if (logSuccess)
            {
                Debug.Log(
                    $"Build Step 1 passed. Saved all open scenes and placed "
                    + $"'{activeScene.path}' first in the build list.");
            }

            return true;
        }

        public static bool ValidateBuildSetup(bool logSuccess)
        {
            bool passed = true;

            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Build validation failed: Unity is currently in Play mode.");
                passed = false;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Build validation failed: Unity is still compiling scripts.");
                passed = false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                Debug.LogError("Build validation failed: the active scene is not saved.");
                passed = false;
            }
            else
            {
                EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
                bool activeSceneIsFirst = buildScenes.Length > 0
                    && buildScenes[0].enabled
                    && buildScenes[0].path == activeScene.path;

                if (!activeSceneIsFirst)
                {
                    Debug.LogError(
                        "Build validation failed: the active scene is not the first enabled build scene.");
                    passed = false;
                }
            }

            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone,
                    BuildTarget.StandaloneWindows64))
            {
                Debug.LogError(
                    "Build validation failed: Windows Build Support is not installed for this Unity Editor. "
                    + "Add it through Unity Hub > Installs > gear icon > Add modules.");
                passed = false;
            }

            string[] scenePaths = GetEnabledBuildScenePaths();
            if (scenePaths.Length == 0)
            {
                Debug.LogError("Build validation failed: there are no enabled build scenes.");
                passed = false;
            }

            for (int index = 0; index < scenePaths.Length; index++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePaths[index]) == null)
                {
                    Debug.LogError(
                        $"Build validation failed: build scene does not exist at '{scenePaths[index]}'.");
                    passed = false;
                }
            }

            if (passed && logSuccess)
            {
                Debug.Log(
                    $"Build Step 2 passed. {scenePaths.Length} enabled scene(s) are ready for a Windows build.");
            }

            return passed;
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
                    "Build Step 3 could not clean the Windows build folder. "
                    + "Make sure an older TheHanger51.exe is not still running, then try again.\n"
                    + exception);
                return false;
            }
        }

        private static void LaunchBuiltPlayer(string outputPath, bool resetLog)
        {
            string executablePath = Path.GetFullPath(outputPath);
            if (!File.Exists(executablePath))
            {
                Debug.LogError($"Cannot launch Windows build: EXE not found at '{executablePath}'.");
                return;
            }

            string workingDirectory = Path.GetDirectoryName(executablePath);
            string logPath = Path.GetFullPath(Path.Combine(BuildFolder, PlayerLogName));

            if (resetLog && File.Exists(logPath))
            {
                try
                {
                    File.Delete(logPath);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Could not clear the previous Player log at '{logPath}'. "
                        + $"The new run will still be launched. {exception.Message}");
                }
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = workingDirectory,
                    // Windowed + D3D11 makes this development launcher less sensitive to a bad
                    // fullscreen mode or DX12/driver startup problem. The player can still change
                    // normal display settings later when we move out of diagnostic development.
                    Arguments = $"-logFile \"{logPath}\" -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo);
                if (process == null)
                {
                    Debug.LogError("Windows build was created, but Windows did not return a Player process.");
                    return;
                }

                Debug.Log(
                    $"Launched Windows development build (PID {process.Id}). "
                    + $"Standalone diagnostics are being written to '{logPath}'. "
                    + "If the game closes or crashes, use Hanger 51 > Build > 5 - Reveal Last Player Log.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Windows build succeeded but the EXE could not be launched at '{executablePath}'.\n"
                    + exception);
            }
        }

        private static string[] GetEnabledBuildScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct()
                .ToArray();
        }
    }
}
