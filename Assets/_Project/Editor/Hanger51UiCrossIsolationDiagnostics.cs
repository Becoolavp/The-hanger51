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
    /// Tests the interaction that remains after the previous fresh-scene diagnostics:
    /// Hanger 51 runtime MonoBehaviours together with Unity UI MonoBehaviours. Optional variants
    /// add back exactly one external package family. The real scene is never modified.
    /// </summary>
    public static class Hanger51UiCrossIsolationDiagnostics
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

        private enum ExtraFamily
        {
            None,
            InputSystemUi,
            UrpCamera,
            UrpLights
        }

        [MenuItem("Hanger 51/Build/45 - Hanger51 + All UI Only")]
        public static void BuildHangerAndUiOnly()
        {
            BuildVariant(
                ExtraFamily.None,
                "HangerPlusUI",
                "__Hanger51PlusUI.unity",
                "TheHanger51_HangerPlusUI.exe",
                "HangerPlusUI_Player.log");
        }

        [MenuItem("Hanger 51/Build/46 - Hanger51 + UI + Input System")]
        public static void BuildHangerUiAndInput()
        {
            BuildVariant(
                ExtraFamily.InputSystemUi,
                "HangerPlusUI_Input",
                "__Hanger51PlusUIInput.unity",
                "TheHanger51_HangerPlusUI_Input.exe",
                "HangerPlusUI_Input_Player.log");
        }

        [MenuItem("Hanger 51/Build/47 - Hanger51 + UI + URP Camera")]
        public static void BuildHangerUiAndCamera()
        {
            BuildVariant(
                ExtraFamily.UrpCamera,
                "HangerPlusUI_Camera",
                "__Hanger51PlusUICamera.unity",
                "TheHanger51_HangerPlusUI_Camera.exe",
                "HangerPlusUI_Camera_Player.log");
        }

        [MenuItem("Hanger 51/Build/48 - Hanger51 + UI + URP Lights")]
        public static void BuildHangerUiAndLights()
        {
            BuildVariant(
                ExtraFamily.UrpLights,
                "HangerPlusUI_Lights",
                "__Hanger51PlusUILights.unity",
                "TheHanger51_HangerPlusUI_Lights.exe",
                "HangerPlusUI_Lights_Player.log");
        }

        private static void BuildVariant(
            ExtraFamily extraFamily,
            string variantFolder,
            string tempSceneFile,
            string executableName,
            string logName)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Hanger/UI cross diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Hanger/UI cross diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Hanger/UI cross diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Hanger/UI cross diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            if (sourceRoots.Length == 0)
            {
                Debug.LogError("Hanger/UI cross diagnostic failed because the active scene has no root GameObjects.");
                return;
            }

            string tempScenePath = TempSceneFolder + "/" + tempSceneFile;
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
                    Debug.LogError("Hanger/UI cross diagnostic could not create a fresh scene.");
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

                int keptHanger = 0;
                int keptUi = 0;
                int keptInput = 0;
                int keptCamera = 0;
                int keptLights = 0;
                int removedOther = 0;

                GameObject[] clonedRoots = diagnosticScene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < clonedRoots.Length; rootIndex++)
                {
                    MonoBehaviour[] behaviours = clonedRoots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                    for (int index = behaviours.Length - 1; index >= 0; index--)
                    {
                        MonoBehaviour behaviour = behaviours[index];
                        if (behaviour == null) continue;

                        MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                        string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                        Type type = behaviour.GetType();
                        string fullName = type.FullName ?? type.Name;

                        if (IsHanger51(type, scriptPath))
                        {
                            keptHanger++;
                            continue;
                        }

                        if (IsUiRelated(type, scriptPath))
                        {
                            keptUi++;
                            continue;
                        }

                        if (extraFamily == ExtraFamily.InputSystemUi && fullName == InputSystemUiType)
                        {
                            keptInput++;
                            continue;
                        }

                        if (extraFamily == ExtraFamily.UrpCamera && fullName == UrpCameraType)
                        {
                            keptCamera++;
                            continue;
                        }

                        if (extraFamily == ExtraFamily.UrpLights && fullName == UrpLightType)
                        {
                            keptLights++;
                            continue;
                        }

                        Object.DestroyImmediate(behaviour);
                        removedOther++;
                    }
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Hanger/UI cross diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Scene auditScene = EditorSceneManager.OpenScene(tempScenePath, OpenSceneMode.Additive);
                try
                {
                    AuditSavedScene(
                        auditScene,
                        variantFolder,
                        keptHanger,
                        keptUi,
                        keptInput,
                        keptCamera,
                        keptLights,
                        removedOther);
                }
                finally
                {
                    if (auditScene.IsValid() && auditScene.isLoaded)
                    {
                        EditorSceneManager.CloseScene(auditScene, true);
                    }
                }

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
                    Debug.LogError($"Hanger/UI cross diagnostic '{variantFolder}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"Hanger/UI cross diagnostic '{variantFolder}' build passed. "
                    + $"Size={report.summary.totalSize} bytes; duration={report.summary.totalTime}. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Hanger/UI cross diagnostic '{variantFolder}' failed unexpectedly.\n{exception}");
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

        private static void AuditSavedScene(
            Scene scene,
            string variantFolder,
            int expectedHanger,
            int expectedUi,
            int expectedInput,
            int expectedCamera,
            int expectedLights,
            int removedOther)
        {
            int hanger = 0;
            int ui = 0;
            int input = 0;
            int camera = 0;
            int lights = 0;
            int other = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                    string path = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                    Type type = behaviour.GetType();
                    string fullName = type.FullName ?? type.Name;

                    if (IsHanger51(type, path)) hanger++;
                    else if (IsUiRelated(type, path)) ui++;
                    else if (fullName == InputSystemUiType) input++;
                    else if (fullName == UrpCameraType) camera++;
                    else if (fullName == UrpLightType) lights++;
                    else other++;
                }
            }

            Debug.Log(
                $"Hanger/UI saved-scene audit '{variantFolder}': "
                + $"Hanger51={hanger} (expected {expectedHanger}), "
                + $"UI={ui} (expected {expectedUi}), "
                + $"InputUI={input} (expected {expectedInput}), "
                + $"URPCamera={camera} (expected {expectedCamera}), "
                + $"URPLights={lights} (expected {expectedLights}), "
                + $"Other={other}; removed during clone={removedOther}.");
        }

        private static bool IsHanger51(Type type, string scriptPath)
        {
            if (!string.IsNullOrEmpty(scriptPath)
                && scriptPath.StartsWith(HangerRoot, StringComparison.OrdinalIgnoreCase)
                && scriptPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            string ns = type != null ? type.Namespace ?? string.Empty : string.Empty;
            return ns.StartsWith("Hanger51", StringComparison.Ordinal);
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
                Debug.LogError($"Hanger/UI cross diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("Hanger/UI cross diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log($"Launched Hanger/UI cross diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
