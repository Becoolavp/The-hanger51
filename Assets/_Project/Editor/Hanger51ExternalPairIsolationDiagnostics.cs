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
    /// Pairwise isolation for the three external MonoBehaviour types left after the fresh-scene
    /// diagnostics. Each build keeps every Hanger 51 MonoBehaviour plus exactly two selected
    /// external component types, while removing UI and every other external MonoBehaviour.
    /// The real scene is never modified.
    /// </summary>
    public static class Hanger51ExternalPairIsolationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";
        private const string HangerRoot = "Assets/_Project/";

        private const string InputSystemUiType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
        private const string UrpCameraType =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData";
        private const string UrpLightType =
            "UnityEngine.Rendering.Universal.UniversalAdditionalLightData";

        [MenuItem("Hanger 51/Build/32 - Hanger51 + Input UI + URP Camera")]
        public static void BuildInputAndCamera()
        {
            BuildPair(
                "Pair_Input_Camera",
                "__Hanger51PairInputCamera.unity",
                "TheHanger51_Pair_Input_Camera.exe",
                "Pair_Input_Camera_Player.log",
                InputSystemUiType,
                UrpCameraType);
        }

        [MenuItem("Hanger 51/Build/33 - Hanger51 + Input UI + URP Lights")]
        public static void BuildInputAndLights()
        {
            BuildPair(
                "Pair_Input_Lights",
                "__Hanger51PairInputLights.unity",
                "TheHanger51_Pair_Input_Lights.exe",
                "Pair_Input_Lights_Player.log",
                InputSystemUiType,
                UrpLightType);
        }

        [MenuItem("Hanger 51/Build/34 - Hanger51 + URP Camera + URP Lights")]
        public static void BuildCameraAndLights()
        {
            BuildPair(
                "Pair_Camera_Lights",
                "__Hanger51PairCameraLights.unity",
                "TheHanger51_Pair_Camera_Lights.exe",
                "Pair_Camera_Lights_Player.log",
                UrpCameraType,
                UrpLightType);
        }

        private static void BuildPair(
            string variantFolder,
            string tempSceneFileName,
            string executableName,
            string logName,
            string allowedExternalTypeA,
            string allowedExternalTypeB)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("External-pair diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("External-pair diagnostic failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("External-pair diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("External-pair diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            if (sourceRoots.Length == 0)
            {
                Debug.LogError("External-pair diagnostic failed because the current scene has no root GameObjects.");
                return;
            }

            string tempScenePath = TempSceneFolder + "/" + tempSceneFileName;
            string outputFolder = Path.Combine(DiagnosticsRoot, variantFolder);
            string outputPath = Path.Combine(outputFolder, executableName);
            string logPath = Path.Combine(outputFolder, logName);
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError("External-pair diagnostic could not create a new empty scene.");
                    return;
                }

                int keptHangerCount = 0;
                int keptExternalA = 0;
                int keptExternalB = 0;
                int removedCount = 0;
                List<string> roots = new List<string>();

                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);
                    roots.Add(clone.name);

                    MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int index = behaviours.Length - 1; index >= 0; index--)
                    {
                        MonoBehaviour behaviour = behaviours[index];
                        if (behaviour == null) continue;

                        if (IsHanger51Behaviour(behaviour))
                        {
                            keptHangerCount++;
                            continue;
                        }

                        string fullName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                        if (fullName == allowedExternalTypeA)
                        {
                            keptExternalA++;
                            continue;
                        }

                        if (fullName == allowedExternalTypeB)
                        {
                            keptExternalB++;
                            continue;
                        }

                        Object.DestroyImmediate(behaviour);
                        removedCount++;
                    }
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"External-pair diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"External-pair diagnostic '{variantFolder}' created a fresh scene. "
                    + $"Kept Hanger51={keptHangerCount}, {allowedExternalTypeA}={keptExternalA}, "
                    + $"{allowedExternalTypeB}={keptExternalB}; removed other MonoBehaviours={removedCount}. "
                    + $"Roots cloned={roots.Count}.");

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
                    Debug.LogError($"External-pair diagnostic '{variantFolder}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"External-pair diagnostic '{variantFolder}' build passed. "
                    + $"Size={report.summary.totalSize} bytes; duration={report.summary.totalTime}. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"External-pair diagnostic '{variantFolder}' failed unexpectedly.\n{exception}");
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

        private static bool IsHanger51Behaviour(MonoBehaviour behaviour)
        {
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null) return false;

            string path = AssetDatabase.GetAssetPath(script);
            return !string.IsNullOrEmpty(path)
                && path.StartsWith(HangerRoot, StringComparison.OrdinalIgnoreCase)
                && path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0;
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
                Debug.LogError($"External-pair diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("External-pair diagnostic build succeeded, but Windows did not return a Player process.");
                return;
            }

            Debug.Log($"Launched external-pair diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
