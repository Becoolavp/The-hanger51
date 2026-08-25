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
    /// Diagnoses standalone level0 corruption by rebuilding the current scene into a brand-new
    /// scene container. This separates corruption in the scene asset/settings from corruption in
    /// individual GameObject/native-component hierarchies. Every diagnostic uses its own output
    /// folder so one test can never delete another test's log.
    /// </summary>
    public static class Hanger51FreshSceneCloneDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";

        [MenuItem("Hanger 51/Build/15 - Build Fresh Scene Clone")]
        public static void BuildFreshSceneClone()
        {
            BuildVariant(
                "FreshSceneClone",
                "__Hanger51FreshSceneClone.unity",
                "TheHanger51_FreshSceneClone.exe",
                "FreshSceneClone_Player.log",
                RootSelection.All,
                false);
        }

        [MenuItem("Hanger 51/Build/16 - Reveal Fresh Scene Clone Log")]
        public static void RevealFreshSceneCloneLog()
        {
            RevealLog("FreshSceneClone", "FreshSceneClone_Player.log");
        }

        [MenuItem("Hanger 51/Build/17 - Build Fresh Scene Clone Without MonoBehaviours")]
        public static void BuildFreshSceneCloneWithoutMonoBehaviours()
        {
            BuildVariant(
                "FreshSceneNoMono",
                "__Hanger51FreshSceneNoMono.unity",
                "TheHanger51_FreshSceneNoMono.exe",
                "FreshSceneNoMono_Player.log",
                RootSelection.All,
                true);
        }

        [MenuItem("Hanger 51/Build/18 - Reveal Fresh Scene No-Mono Log")]
        public static void RevealFreshSceneNoMonoLog()
        {
            RevealLog("FreshSceneNoMono", "FreshSceneNoMono_Player.log");
        }

        [MenuItem("Hanger 51/Build/19 - Build Fresh Native Root Half A")]
        public static void BuildFreshNativeRootHalfA()
        {
            BuildVariant(
                "FreshNativeHalfA",
                "__Hanger51FreshNativeHalfA.unity",
                "TheHanger51_FreshNativeHalfA.exe",
                "FreshNativeHalfA_Player.log",
                RootSelection.FirstHalf,
                true);
        }

        [MenuItem("Hanger 51/Build/20 - Build Fresh Native Root Half B")]
        public static void BuildFreshNativeRootHalfB()
        {
            BuildVariant(
                "FreshNativeHalfB",
                "__Hanger51FreshNativeHalfB.unity",
                "TheHanger51_FreshNativeHalfB.exe",
                "FreshNativeHalfB_Player.log",
                RootSelection.SecondHalf,
                true);
        }

        [MenuItem("Hanger 51/Build/21 - Reveal Fresh Native Half A Log")]
        public static void RevealFreshNativeHalfALog()
        {
            RevealLog("FreshNativeHalfA", "FreshNativeHalfA_Player.log");
        }

        [MenuItem("Hanger 51/Build/22 - Reveal Fresh Native Half B Log")]
        public static void RevealFreshNativeHalfBLog()
        {
            RevealLog("FreshNativeHalfB", "FreshNativeHalfB_Player.log");
        }

        private enum RootSelection
        {
            All,
            FirstHalf,
            SecondHalf
        }

        private static void BuildVariant(
            string variantFolder,
            string tempSceneFileName,
            string executableName,
            string logName,
            RootSelection rootSelection,
            bool stripMonoBehaviours)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Fresh-scene diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Fresh-scene diagnostic failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Fresh-scene diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Fresh-scene diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            if (sourceRoots.Length == 0)
            {
                Debug.LogError("Fresh-scene diagnostic failed because the current scene has no root GameObjects.");
                return;
            }

            string tempScenePath = TempSceneFolder + "/" + tempSceneFileName;
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
                    Debug.LogError("Fresh-scene diagnostic could not create a new empty scene.");
                    return;
                }

                int clonedRootCount = CloneSelectedRoots(
                    sourceRoots,
                    diagnosticScene,
                    rootSelection,
                    stripMonoBehaviours,
                    out int removedMonoCount,
                    out List<string> clonedRootNames);

                if (clonedRootCount == 0)
                {
                    Debug.LogError("Fresh-scene diagnostic selected zero scene roots to clone.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Fresh-scene diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"Fresh-scene diagnostic '{variantFolder}' created a BRAND-NEW scene container and cloned "
                    + $"{clonedRootCount}/{sourceRoots.Length} root object(s). Removed {removedMonoCount} "
                    + $"MonoBehaviour component(s). Roots: {string.Join(", ", clonedRootNames)}");

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
                    Debug.LogError($"Fresh-scene diagnostic '{variantFolder}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"Fresh-scene diagnostic '{variantFolder}' build passed. Size={report.summary.totalSize} bytes; "
                    + $"duration={report.summary.totalTime}. Launching now. Its log is preserved in '{outputFolder}'.");

                Launch(outputPath, Path.Combine(outputFolder, logName));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Fresh-scene diagnostic '{variantFolder}' failed unexpectedly.\n{exception}");
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

        private static int CloneSelectedRoots(
            GameObject[] sourceRoots,
            Scene targetScene,
            RootSelection selection,
            bool stripMonoBehaviours,
            out int removedMonoCount,
            out List<string> clonedRootNames)
        {
            removedMonoCount = 0;
            clonedRootNames = new List<string>();

            int splitIndex = Mathf.CeilToInt(sourceRoots.Length / 2f);
            int cloned = 0;

            for (int index = 0; index < sourceRoots.Length; index++)
            {
                bool include = selection == RootSelection.All
                    || (selection == RootSelection.FirstHalf && index < splitIndex)
                    || (selection == RootSelection.SecondHalf && index >= splitIndex);
                if (!include) continue;

                GameObject sourceRoot = sourceRoots[index];
                if (sourceRoot == null) continue;

                GameObject clone = Object.Instantiate(sourceRoot);
                clone.name = sourceRoot.name;
                SceneManager.MoveGameObjectToScene(clone, targetScene);

                if (stripMonoBehaviours)
                {
                    MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int behaviourIndex = behaviours.Length - 1; behaviourIndex >= 0; behaviourIndex--)
                    {
                        MonoBehaviour behaviour = behaviours[behaviourIndex];
                        if (behaviour == null) continue;
                        Object.DestroyImmediate(behaviour);
                        removedMonoCount++;
                    }
                }

                clonedRootNames.Add(clone.name);
                cloned++;
            }

            return cloned;
        }

        private static void PrepareOutputFolder(string outputFolder)
        {
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);
        }

        private static void Launch(string outputPath, string logPath)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string absoluteLogPath = Path.GetFullPath(logPath);
            if (!File.Exists(executablePath))
            {
                Debug.LogError($"Fresh-scene diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("Fresh-scene diagnostic build succeeded, but Windows did not return a Player process.");
                return;
            }

            Debug.Log($"Launched fresh-scene diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
        }

        private static void RevealLog(string variantFolder, string logName)
        {
            string folder = Path.GetFullPath(Path.Combine(DiagnosticsRoot, variantFolder));
            string logPath = Path.Combine(folder, logName);

            if (File.Exists(logPath))
            {
                EditorUtility.RevealInFinder(logPath);
                Debug.Log($"Diagnostic Player log: {logPath}");
                return;
            }

            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
            }

            Debug.LogWarning($"No diagnostic log exists yet at '{logPath}'. Run the matching build step first.");
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
