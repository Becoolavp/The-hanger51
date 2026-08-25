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
    /// Builds fresh-scene diagnostics that keep every Hanger51 MonoBehaviour plus exactly one
    /// external/package MonoBehaviour type. This isolates the interaction responsible for the
    /// standalone level0 corruption without changing the user's real scene.
    /// </summary>
    public static class Hanger51ExternalTypeInteractionDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";

        private const string InputUiType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
        private const string UrpCameraType =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData";
        private const string UrpLightType =
            "UnityEngine.Rendering.Universal.UniversalAdditionalLightData";

        [MenuItem("Hanger 51/Build/29 - Hanger51 + Input System UI Module")]
        public static void BuildWithInputSystemUiModule()
        {
            BuildVariant("H51PlusInputUI", "Input System UI Module", InputUiType);
        }

        [MenuItem("Hanger 51/Build/30 - Hanger51 + URP Camera Data")]
        public static void BuildWithUrpCameraData()
        {
            BuildVariant("H51PlusURPCamera", "URP Camera Data", UrpCameraType);
        }

        [MenuItem("Hanger 51/Build/31 - Hanger51 + URP Light Data")]
        public static void BuildWithUrpLightData()
        {
            BuildVariant("H51PlusURPLights", "URP Light Data", UrpLightType);
        }

        private static void BuildVariant(string variantName, string displayName, string keptExternalType)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("External interaction diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("External interaction diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("External interaction diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("External interaction diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            string tempScenePath = $"{TempSceneFolder}/__{variantName}.unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, variantName);
            string outputPath = Path.Combine(outputFolder, $"TheHanger51_{variantName}.exe");
            string logPath = Path.Combine(outputFolder, $"{variantName}_Player.log");
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                int keptHanger51 = 0;
                int keptExternal = 0;
                int removed = 0;

                foreach (GameObject sourceRoot in sourceRoots)
                {
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);

                    MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int index = behaviours.Length - 1; index >= 0; index--)
                    {
                        MonoBehaviour behaviour = behaviours[index];
                        if (behaviour == null) continue;

                        if (IsHanger51Behaviour(behaviour))
                        {
                            keptHanger51++;
                            continue;
                        }

                        string fullName = behaviour.GetType().FullName ?? string.Empty;
                        if (string.Equals(fullName, keptExternalType, StringComparison.Ordinal))
                        {
                            keptExternal++;
                            continue;
                        }

                        Object.DestroyImmediate(behaviour);
                        removed++;
                    }
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Could not save temporary diagnostic scene '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"External interaction diagnostic '{displayName}': kept {keptHanger51} Hanger51 "
                    + $"MonoBehaviours and {keptExternal} '{keptExternalType}' component(s); removed {removed} other MonoBehaviours.");

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
                    string detail = report == null
                        ? "Unity returned no BuildReport."
                        : $"Result={report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.";
                    Debug.LogError($"External interaction diagnostic '{displayName}' build failed. {detail}");
                    return;
                }

                Launch(outputPath, logPath, displayName);
            }
            catch (Exception exception)
            {
                Debug.LogError($"External interaction diagnostic '{displayName}' failed unexpectedly.\n{exception}");
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
                && path.StartsWith("Assets/_Project/", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrepareOutputFolder(string outputFolder)
        {
            if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);
            Directory.CreateDirectory(outputFolder);
        }

        private static void Launch(string outputPath, string logPath, string displayName)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string absoluteLogPath = Path.GetFullPath(logPath);

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
                Debug.LogError($"'{displayName}' build succeeded, but Windows did not return a Player process.");
                return;
            }

            Debug.Log($"Launched '{displayName}' diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
