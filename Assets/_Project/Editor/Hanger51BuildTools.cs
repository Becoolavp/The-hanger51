using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51BuildTools
    {
        private const string BuildFolder = "Builds/Windows";
        private const string ExecutableName = "TheHanger51.exe";

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

            Directory.CreateDirectory(BuildFolder);
            string outputPath = Path.Combine(BuildFolder, ExecutableName);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.AutoRunPlayer | BuildOptions.DetailedBuildReport
            };

            Debug.Log($"Build Step 3 started. Building {scenePaths.Length} scene(s) to '{outputPath}'.");

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"Build Step 3 passed. Built and launched '{outputPath}'. "
                    + $"Build size: {summary.totalSize} bytes. Duration: {summary.totalTime}.");
                return;
            }

            Debug.LogError(
                $"Build Step 3 failed. Result: {summary.result}. "
                + $"Errors: {summary.totalErrors}. Warnings: {summary.totalWarnings}.");
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
