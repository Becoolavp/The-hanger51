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
    /// Keeps all Hanger 51 runtime MonoBehaviours plus InputSystemUIInputModule and
    /// UniversalAdditionalCameraData, then varies how many UniversalAdditionalLightData components
    /// are preserved. This isolates whether standalone level0 corruption depends on a specific URP
    /// light-data component or simply on the number of serialized URP light-data components.
    /// The real scene is never modified.
    /// </summary>
    public static class Hanger51UrpLightSubsetDiagnostics
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

        [MenuItem("Hanger 51/Build/35 - Report URP Light Data Paths")]
        public static void ReportUrpLightDataPaths()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("URP light report failed: there is no valid active scene.");
                return;
            }

            List<string> paths = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;
                    string fullName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                    if (fullName != UrpLightType) continue;
                    paths.Add(GetPath(behaviour.transform));
                }
            }

            paths.Sort(StringComparer.Ordinal);
            Debug.Log(
                $"URP light-data report for '{scene.path}': found {paths.Count} component(s).\n"
                + string.Join("\n", paths.ConvertAll((path, index) => $"  [{index}] {path}")));
        }

        [MenuItem("Hanger 51/Build/36 - Triple Combo With 1 URP Light")]
        public static void BuildWithOneLight() => BuildVariant(1);

        [MenuItem("Hanger 51/Build/37 - Triple Combo With 2 URP Lights")]
        public static void BuildWithTwoLights() => BuildVariant(2);

        [MenuItem("Hanger 51/Build/38 - Triple Combo With 3 URP Lights")]
        public static void BuildWithThreeLights() => BuildVariant(3);

        [MenuItem("Hanger 51/Build/39 - Triple Combo With 4 URP Lights")]
        public static void BuildWithFourLights() => BuildVariant(4);

        [MenuItem("Hanger 51/Build/40 - Triple Combo With All URP Lights")]
        public static void BuildWithAllLights() => BuildVariant(int.MaxValue);

        private static void BuildVariant(int lightLimit)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("URP light-subset diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("URP light-subset diagnostic failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("URP light-subset diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("URP light-subset diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            string suffix = lightLimit == int.MaxValue ? "All" : lightLimit.ToString();
            string variantFolder = "Triple_Lights_" + suffix;
            string tempScenePath = TempSceneFolder + $"/__Hanger51TripleLights{suffix}.unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, variantFolder);
            string outputPath = Path.Combine(outputFolder, $"TheHanger51_Triple_Lights_{suffix}.exe");
            string logPath = Path.Combine(outputFolder, $"Triple_Lights_{suffix}_Player.log");
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError("URP light-subset diagnostic could not create a fresh scene.");
                    return;
                }

                List<MonoBehaviour> lightBehaviours = new List<MonoBehaviour>();
                List<MonoBehaviour> allBehaviours = new List<MonoBehaviour>();
                int keptHanger = 0;
                int keptInput = 0;
                int keptCamera = 0;
                int removedOther = 0;

                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);

                    MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                    allBehaviours.AddRange(behaviours);
                }

                for (int index = 0; index < allBehaviours.Count; index++)
                {
                    MonoBehaviour behaviour = allBehaviours[index];
                    if (behaviour == null) continue;

                    if (IsHanger51Behaviour(behaviour))
                    {
                        keptHanger++;
                        continue;
                    }

                    string fullName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                    if (fullName == InputSystemUiType)
                    {
                        keptInput++;
                        continue;
                    }

                    if (fullName == UrpCameraType)
                    {
                        keptCamera++;
                        continue;
                    }

                    if (fullName == UrpLightType)
                    {
                        lightBehaviours.Add(behaviour);
                        continue;
                    }

                    Object.DestroyImmediate(behaviour);
                    removedOther++;
                }

                lightBehaviours.Sort((a, b) =>
                    string.Compare(GetPath(a.transform), GetPath(b.transform), StringComparison.Ordinal));

                int keepLightCount = lightLimit == int.MaxValue
                    ? lightBehaviours.Count
                    : Mathf.Min(lightLimit, lightBehaviours.Count);

                List<string> keptLightPaths = new List<string>();
                for (int index = 0; index < lightBehaviours.Count; index++)
                {
                    MonoBehaviour lightData = lightBehaviours[index];
                    if (lightData == null) continue;

                    if (index < keepLightCount)
                    {
                        keptLightPaths.Add(GetPath(lightData.transform));
                    }
                    else
                    {
                        Object.DestroyImmediate(lightData);
                    }
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"URP light-subset diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"URP light-subset diagnostic '{variantFolder}' created a fresh scene. "
                    + $"Kept Hanger51={keptHanger}, InputUI={keptInput}, URPCamera={keptCamera}, "
                    + $"URPLights={keepLightCount}/{lightBehaviours.Count}, removed other MonoBehaviours={removedOther}.\n"
                    + "Kept URP light-data paths:\n"
                    + string.Join("\n", keptLightPaths));

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
                    Debug.LogError($"URP light-subset diagnostic '{variantFolder}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"URP light-subset diagnostic '{variantFolder}' build passed. "
                    + $"Size={report.summary.totalSize} bytes; duration={report.summary.totalTime}. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"URP light-subset diagnostic failed unexpectedly.\n{exception}");
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
                Debug.LogError($"URP light-subset diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("URP light-subset diagnostic build succeeded, but Windows did not return a Player process.");
                return;
            }

            Debug.Log($"Launched URP light-subset diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
