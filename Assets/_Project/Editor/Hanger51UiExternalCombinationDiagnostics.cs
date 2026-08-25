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
    /// Re-tests the boundary established by the earlier fresh-scene diagnostics without any
    /// Hanger 51 runtime MonoBehaviours. Keeps every UI-related MonoBehaviour and selectively keeps
    /// the three remaining external package component families. Each generated scene is audited
    /// after save/reload so the console reports what actually survived component stripping.
    /// The real scene is never modified.
    /// </summary>
    public static class Hanger51UiExternalCombinationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";

        private const string InputSystemUiType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
        private const string UrpCameraType =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData";
        private const string UrpLightType =
            "UnityEngine.Rendering.Universal.UniversalAdditionalLightData";

        [MenuItem("Hanger 51/Build/41 - UI + Input System Only")]
        public static void BuildUiAndInput() => BuildVariant("UI_Input", true, false, false);

        [MenuItem("Hanger 51/Build/42 - UI + URP Camera Only")]
        public static void BuildUiAndCamera() => BuildVariant("UI_Camera", false, true, false);

        [MenuItem("Hanger 51/Build/43 - UI + URP Lights Only")]
        public static void BuildUiAndLights() => BuildVariant("UI_Lights", false, false, true);

        [MenuItem("Hanger 51/Build/44 - UI + All External Families")]
        public static void BuildUiAndAllExternal() => BuildVariant("UI_AllExternal", true, true, true);

        private static void BuildVariant(string variant, bool keepInput, bool keepCamera, bool keepLights)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("UI/external diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("UI/external diagnostic failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("UI/external diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("UI/external diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            string tempScenePath = TempSceneFolder + "/__Hanger51" + variant + ".unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, variant);
            string outputPath = Path.Combine(outputFolder, "TheHanger51_" + variant + ".exe");
            string logPath = Path.Combine(outputFolder, variant + "_Player.log");
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError("UI/external diagnostic could not create a fresh scene.");
                    return;
                }

                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);
                }

                MonoBehaviour[] behaviours = GetSceneBehaviours(diagnosticScene);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    Type type = behaviour.GetType();
                    MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                    string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                    string fullName = type.FullName ?? type.Name;

                    bool keep = IsUiRelated(type, scriptPath)
                        || (keepInput && fullName == InputSystemUiType)
                        || (keepCamera && fullName == UrpCameraType)
                        || (keepLights && fullName == UrpLightType);

                    if (!keep)
                    {
                        Object.DestroyImmediate(behaviour);
                    }
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError("UI/external diagnostic could not save '" + tempScenePath + "'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Scene auditScene = EditorSceneManager.OpenScene(tempScenePath, OpenSceneMode.Additive);
                string audit = AuditScene(auditScene);
                EditorSceneManager.CloseScene(auditScene, true);
                Debug.Log("UI/external diagnostic '" + variant + "' saved-scene audit:\n" + audit);

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
                        : "Result=" + report.summary.result + ", errors=" + report.summary.totalErrors
                            + ", warnings=" + report.summary.totalWarnings + ".";
                    Debug.LogError("UI/external diagnostic '" + variant + "' build failed. " + failure);
                    return;
                }

                Debug.Log("UI/external diagnostic '" + variant + "' build passed. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("UI/external diagnostic '" + variant + "' failed unexpectedly.\n" + exception);
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

        private static MonoBehaviour[] GetSceneBehaviours(Scene scene)
        {
            List<MonoBehaviour> result = new List<MonoBehaviour>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                result.AddRange(roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true));
            }
            return result.ToArray();
        }

        private static string AuditScene(Scene scene)
        {
            int hanger = 0;
            int ui = 0;
            int input = 0;
            int camera = 0;
            int lights = 0;
            int other = 0;
            Dictionary<string, int> types = new Dictionary<string, int>();

            MonoBehaviour[] behaviours = GetSceneBehaviours(scene);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null) continue;

                Type type = behaviour.GetType();
                MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                string path = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                string fullName = type.FullName ?? type.Name;

                if (IsHanger51(type, path)) hanger++;
                else if (IsUiRelated(type, path)) ui++;
                else if (fullName == InputSystemUiType) input++;
                else if (fullName == UrpCameraType) camera++;
                else if (fullName == UrpLightType) lights++;
                else other++;

                int current;
                types.TryGetValue(fullName, out current);
                types[fullName] = current + 1;
            }

            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, int> pair in types)
            {
                lines.Add("  " + pair.Value + " x " + pair.Key);
            }
            lines.Sort(StringComparer.OrdinalIgnoreCase);

            return "Hanger51=" + hanger + ", UI=" + ui + ", InputUI=" + input
                + ", URPCamera=" + camera + ", URPLights=" + lights + ", Other=" + other
                + ".\n" + string.Join("\n", lines);
        }

        private static bool IsHanger51(Type type, string scriptPath)
        {
            if (!string.IsNullOrEmpty(scriptPath)
                && scriptPath.StartsWith("Assets/_Project/", StringComparison.OrdinalIgnoreCase))
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
                Debug.LogError("UI/external diagnostic EXE not found at '" + executablePath + "'.");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = "-logFile \"" + absoluteLogPath
                    + "\" -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo);
            if (process == null)
            {
                Debug.LogError("UI/external diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log("Launched UI/external diagnostic PID " + process.Id + ". Log: '" + absoluteLogPath + "'.");
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
