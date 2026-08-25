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
    /// Final Hanger/UI isolation after the crashing Hanger type range was narrowed to indexes 23-26:
    /// P51TowBarPlayerInteractor, P51TurnPerformanceAssist, P51WingArmamentPlayerInteractor,
    /// and P51WingArmamentServiceTarget. Each diagnostic keeps all Unity UI MonoBehaviours plus
    /// only the explicitly selected Hanger 51 type(s). Every other MonoBehaviour is removed from
    /// a brand-new temporary scene. The real scene is never modified.
    /// </summary>
    public static class Hanger51FinalHangerUiIsolationDiagnostics
    {
        private const string TempSceneFolder = "Assets/_Project/Scenes";
        private const string DiagnosticsRoot = "Builds/Diagnostics";

        private const string TowBarInteractor = "Hanger51.Aircraft.P51TowBarPlayerInteractor";
        private const string TurnAssist = "Hanger51.Aircraft.P51TurnPerformanceAssist";
        private const string ArmamentInteractor = "Hanger51.Aircraft.P51WingArmamentPlayerInteractor";
        private const string ArmamentServiceTarget = "Hanger51.Aircraft.P51WingArmamentServiceTarget";

        [MenuItem("Hanger 51/Build/62 - UI + TowBar Interactor + Turn Assist")]
        public static void BuildPairTowBarTurn()
        {
            BuildVariant("Pair_TowBar_Turn", TowBarInteractor, TurnAssist);
        }

        [MenuItem("Hanger 51/Build/63 - UI + Armament Interactor + Armament Target")]
        public static void BuildPairArmament()
        {
            BuildVariant("Pair_Armament", ArmamentInteractor, ArmamentServiceTarget);
        }

        [MenuItem("Hanger 51/Build/64 - UI + TowBar Interactor ONLY")]
        public static void BuildTowBarOnly()
        {
            BuildVariant("Single_TowBar", TowBarInteractor);
        }

        [MenuItem("Hanger 51/Build/65 - UI + Turn Assist ONLY")]
        public static void BuildTurnAssistOnly()
        {
            BuildVariant("Single_TurnAssist", TurnAssist);
        }

        [MenuItem("Hanger 51/Build/66 - UI + Armament Interactor ONLY")]
        public static void BuildArmamentInteractorOnly()
        {
            BuildVariant("Single_ArmamentInteractor", ArmamentInteractor);
        }

        [MenuItem("Hanger 51/Build/67 - UI + Armament Service Target ONLY")]
        public static void BuildArmamentTargetOnly()
        {
            BuildVariant("Single_ArmamentTarget", ArmamentServiceTarget);
        }

        private static void BuildVariant(string label, params string[] allowedHangerTypesArray)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Final Hanger/UI diagnostic failed. Exit Play mode first.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Final Hanger/UI diagnostic failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Final Hanger/UI diagnostic failed. Open the saved real game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Final Hanger/UI diagnostic failed because Unity could not save the open scene(s).");
                return;
            }

            HashSet<string> allowedHangerTypes = new HashSet<string>(allowedHangerTypesArray, StringComparer.Ordinal);
            string tempScenePath = TempSceneFolder + "/__Hanger51FinalUi_" + label + ".unity";
            string outputFolder = Path.Combine(DiagnosticsRoot, "FinalUI_" + label);
            string outputPath = Path.Combine(outputFolder, "TheHanger51_FinalUI_" + label + ".exe");
            string logPath = Path.Combine(outputFolder, "FinalUI_" + label + "_Player.log");
            Scene diagnosticScene = default;

            try
            {
                DeleteTemporaryScene(tempScenePath);
                PrepareOutputFolder(outputFolder);

                diagnosticScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!diagnosticScene.IsValid() || !diagnosticScene.isLoaded)
                {
                    Debug.LogError("Final Hanger/UI diagnostic could not create a fresh scene.");
                    return;
                }

                GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
                {
                    GameObject sourceRoot = sourceRoots[rootIndex];
                    if (sourceRoot == null) continue;

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, diagnosticScene);
                }

                StripToUiAndSelectedTypes(diagnosticScene, allowedHangerTypes);

                if (!EditorSceneManager.SaveScene(diagnosticScene, tempScenePath, false))
                {
                    Debug.LogError($"Final Hanger/UI diagnostic could not save '{tempScenePath}'.");
                    return;
                }

                AuditDiagnostic(diagnosticScene, label, allowedHangerTypes);

                EditorSceneManager.CloseScene(diagnosticScene, true);
                diagnosticScene = default;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

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
                    Debug.LogError($"Final Hanger/UI diagnostic '{label}' build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"Final Hanger/UI diagnostic '{label}' build passed. Size={report.summary.totalSize} bytes; "
                    + $"duration={report.summary.totalTime}. Launching now.");
                Launch(outputPath, logPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Final Hanger/UI diagnostic '{label}' failed unexpectedly.\n{exception}");
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

        private static void StripToUiAndSelectedTypes(Scene scene, HashSet<string> allowedHangerTypes)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    if (IsUiRelated(behaviour)) continue;

                    string typeName = GetTypeName(behaviour);
                    if (allowedHangerTypes.Contains(typeName)) continue;

                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void AuditDiagnostic(Scene scene, string label, HashSet<string> allowedHangerTypes)
        {
            int uiCount = 0;
            int selectedHangerInstances = 0;
            int unexpectedCount = 0;
            Dictionary<string, int> selectedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            List<string> unexpected = new List<string>();

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;

                    if (IsUiRelated(behaviour))
                    {
                        uiCount++;
                        continue;
                    }

                    string typeName = GetTypeName(behaviour);
                    if (allowedHangerTypes.Contains(typeName))
                    {
                        selectedHangerInstances++;
                        selectedCounts.TryGetValue(typeName, out int count);
                        selectedCounts[typeName] = count + 1;
                        continue;
                    }

                    unexpectedCount++;
                    unexpected.Add(typeName);
                }
            }

            List<string> keptLines = new List<string>();
            foreach (string typeName in allowedHangerTypes)
            {
                selectedCounts.TryGetValue(typeName, out int count);
                keptLines.Add($"{count} x {typeName}");
            }
            keptLines.Sort(StringComparer.Ordinal);

            Debug.Log(
                $"Final Hanger/UI saved-scene audit '{label}': UI={uiCount}, selected Hanger instances={selectedHangerInstances}, "
                + $"unexpected MonoBehaviours={unexpectedCount}.\nKept selected Hanger types:\n  "
                + string.Join("\n  ", keptLines));

            if (unexpectedCount != 0)
            {
                Debug.LogError(
                    $"Final Hanger/UI diagnostic '{label}' audit mismatch. Unexpected types survived:\n  "
                    + string.Join("\n  ", unexpected));
            }
        }

        private static bool IsUiRelated(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
            string ns = type.Namespace ?? string.Empty;
            string assembly = type.Assembly.GetName().Name ?? string.Empty;

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

            return scriptPath.IndexOf("com.unity.ugui", StringComparison.OrdinalIgnoreCase) >= 0
                || scriptPath.IndexOf("com.unity.textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0
                || scriptPath.IndexOf("com.unity.dt.app-ui", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTypeName(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();
            return type.FullName ?? type.Name;
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
                Debug.LogError($"Final Hanger/UI diagnostic EXE not found at '{executablePath}'.");
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
                Debug.LogError("Final Hanger/UI diagnostic build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log($"Launched final Hanger/UI diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
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
