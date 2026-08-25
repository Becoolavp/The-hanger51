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
    /// Splits the MonoBehaviour population of the real scene by script ownership while always
    /// rebuilding into a brand-new scene container. Step 17 proved that the fresh native scene runs
    /// when every MonoBehaviour is removed; these tests identify whether the corrupt serialized
    /// component belongs to Assets/_Project (Hanger 51) or to Unity/packages/other assets.
    /// </summary>
    public static class Hanger51FreshSceneScriptIsolationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";
        private const string ProjectScriptPrefix = "Assets/_Project/";

        private enum ScriptIsolationMode
        {
            RemoveHanger51Scripts,
            KeepOnlyHanger51Scripts
        }

        [MenuItem("Hanger 51/Build/23 - Fresh Scene WITHOUT Hanger 51 Scripts")]
        public static void BuildWithoutHanger51Scripts()
        {
            BuildVariant(
                ScriptIsolationMode.RemoveHanger51Scripts,
                "FreshNoHangerScripts",
                "__Hanger51FreshNoHangerScripts.unity",
                "TheHanger51_FreshNoHangerScripts.exe",
                "FreshNoHangerScripts_Player.log");
        }

        [MenuItem("Hanger 51/Build/24 - Fresh Scene ONLY Hanger 51 Scripts")]
        public static void BuildOnlyHanger51Scripts()
        {
            BuildVariant(
                ScriptIsolationMode.KeepOnlyHanger51Scripts,
                "FreshOnlyHangerScripts",
                "__Hanger51FreshOnlyHangerScripts.unity",
                "TheHanger51_FreshOnlyHangerScripts.exe",
                "FreshOnlyHangerScripts_Player.log");
        }

        [MenuItem("Hanger 51/Build/25 - Reveal Fresh No-Hanger Log")]
        public static void RevealNoHangerLog()
        {
            RevealLog("FreshNoHangerScripts", "FreshNoHangerScripts_Player.log");
        }

        [MenuItem("Hanger 51/Build/26 - Reveal Fresh Only-Hanger Log")]
        public static void RevealOnlyHangerLog()
        {
            RevealLog("FreshOnlyHangerScripts", "FreshOnlyHangerScripts_Player.log");
        }

        private static void BuildVariant(
            ScriptIsolationMode mode,
            string variantFolder,
            string tempSceneFileName,
            string executableName,
            string logName)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Script-isolation diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Script-isolation diagnostic failed. Wait for Unity to finish compiling first.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Script-isolation diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Script-isolation diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            if (sourceRoots.Length == 0)
            {
                Debug.LogError("Script-isolation diagnostic failed because the current scene has no root GameObjects.");
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
                    Debug.LogError("Script-isolation diagnostic could not create a new empty scene.");
                    return;
                }

                int clonedRoots = 0;
                int keptHanger = 0;
                int keptExternal = 0;
                int removedHanger = 0;
                int removedExternal = 0;
                HashSet<string> keptTypes = new HashSet<string>();
                HashSet<string> removedTypes = new HashSet<string>();

                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);
                    clonedRoots++;

                    MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int behaviourIndex = behaviours.Length - 1; behaviourIndex >= 0; behaviourIndex--)
                    {
                        MonoBehaviour behaviour = behaviours[behaviourIndex];
                        if (behaviour == null) continue;

                        bool isHanger = IsHanger51Behaviour(behaviour, out string scriptPath);
                        bool remove = mode == ScriptIsolationMode.RemoveHanger51Scripts
                            ? isHanger
                            : !isHanger;

                        string typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                        string descriptor = string.IsNullOrEmpty(scriptPath)
                            ? typeName
                            : typeName + " @ " + scriptPath;

                        if (remove)
                        {
                            removedTypes.Add(descriptor);
                            if (isHanger) removedHanger++;
                            else removedExternal++;
                            Object.DestroyImmediate(behaviour);
                        }
                        else
                        {
                            keptTypes.Add(descriptor);
                            if (isHanger) keptHanger++;
                            else keptExternal++;
                        }
                    }
                }

                if (clonedRoots == 0)
                {
                    Debug.LogError("Script-isolation diagnostic cloned zero scene roots.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Script-isolation diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"Script-isolation '{variantFolder}' created a brand-new scene with {clonedRoots} root(s). "
                    + $"Kept Hanger={keptHanger}, kept external/package={keptExternal}, "
                    + $"removed Hanger={removedHanger}, removed external/package={removedExternal}.\n"
                    + $"Kept MonoBehaviour types ({keptTypes.Count}): {string.Join(" | ", keptTypes)}\n"
                    + $"Removed MonoBehaviour types ({removedTypes.Count}): {string.Join(" | ", removedTypes)}");

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
                    Debug.LogError($"Script-isolation '{variantFolder}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"Script-isolation '{variantFolder}' build passed. Size={report.summary.totalSize} bytes; "
                    + $"duration={report.summary.totalTime}. Launching now. Log: '{outputFolder}'.");

                Launch(outputPath, Path.Combine(outputFolder, logName));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Script-isolation '{variantFolder}' failed unexpectedly.\n{exception}");
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

        private static bool IsHanger51Behaviour(MonoBehaviour behaviour, out string scriptPath)
        {
            scriptPath = string.Empty;
            if (behaviour == null) return false;

            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null) return false;

            scriptPath = AssetDatabase.GetAssetPath(script) ?? string.Empty;
            return scriptPath.StartsWith(ProjectScriptPrefix, StringComparison.OrdinalIgnoreCase);
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
                Debug.LogError($"Script-isolation EXE not found at '{executablePath}'.");
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
                Debug.LogError("Script-isolation build succeeded, but Windows did not return a Player process.");
                return;
            }

            Debug.Log($"Launched script-isolation diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
