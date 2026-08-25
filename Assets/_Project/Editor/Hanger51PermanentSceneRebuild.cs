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

namespace Hanger51.EditorTools
{
    /// <summary>
    /// Rebuilds the active game scene into a brand-new Unity scene container without cloning roots
    /// one by one. A temporary copy of the source scene is opened, then its ACTUAL root GameObjects
    /// are moved into a fresh scene. Moving all roots together preserves cross-root scene references
    /// while discarding the old scene container/serialized scene-level payload that can corrupt
    /// standalone level0 data.
    ///
    /// The original scene is never overwritten. The rebuilt scene is written to Assets/sofar_Rebuilt.unity.
    /// </summary>
    public static class Hanger51PermanentSceneRebuild
    {
        private const string RebuiltScenePath = "Assets/sofar_Rebuilt.unity";
        private const string TempSourcePath = "Assets/_Project/Scenes/__Hanger51RebuildSource.unity";
        private const string OutputFolder = "Builds/Diagnostics/RebuiltScene";
        private const string ExecutableName = "TheHanger51_RebuiltScene.exe";
        private const string LogName = "RebuiltScene_Player.log";

        [MenuItem("Hanger 51/Build/70 - Create and Build Rebuilt Game Scene")]
        public static void CreateAndBuildRebuiltScene()
        {
            if (!CanOperate(out Scene originalScene))
            {
                return;
            }

            string originalPath = originalScene.path;
            int originalRootCount = originalScene.rootCount;
            Scene tempSourceScene = default;
            Scene rebuiltScene = default;

            try
            {
                if (!EditorSceneManager.SaveOpenScenes())
                {
                    Debug.LogError("Rebuilt-scene Step 70 failed because Unity could not save the currently open scene(s).");
                    return;
                }

                DeleteAssetIfPresent(TempSourcePath);
                DeleteAssetIfPresent(RebuiltScenePath);

                if (!AssetDatabase.CopyAsset(originalPath, TempSourcePath))
                {
                    Debug.LogError($"Rebuilt-scene Step 70 could not create temporary source copy '{TempSourcePath}'.");
                    return;
                }

                AssetDatabase.Refresh();
                tempSourceScene = EditorSceneManager.OpenScene(TempSourcePath, OpenSceneMode.Additive);
                if (!tempSourceScene.IsValid() || !tempSourceScene.isLoaded)
                {
                    Debug.LogError("Rebuilt-scene Step 70 could not open the temporary source copy.");
                    return;
                }

                GameObject[] roots = tempSourceScene.GetRootGameObjects();
                if (roots.Length == 0)
                {
                    Debug.LogError("Rebuilt-scene Step 70 found zero root GameObjects in the temporary source copy.");
                    return;
                }

                rebuiltScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!rebuiltScene.IsValid() || !rebuiltScene.isLoaded)
                {
                    Debug.LogError("Rebuilt-scene Step 70 could not create a brand-new destination scene.");
                    return;
                }

                // Important: these are the actual objects from the temporary source scene, not
                // Object.Instantiate clones. Cross-root references therefore remain references to
                // the same moved objects after all roots land in the rebuilt scene.
                for (int index = 0; index < roots.Length; index++)
                {
                    GameObject root = roots[index];
                    if (root == null) continue;
                    SceneManager.MoveGameObjectToScene(root, rebuiltScene);
                }

                if (rebuiltScene.rootCount != roots.Length)
                {
                    Debug.LogError(
                        $"Rebuilt-scene Step 70 root-count mismatch after move. Expected {roots.Length}, "
                        + $"rebuilt scene has {rebuiltScene.rootCount}. The rebuilt scene was not trusted.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(rebuiltScene, RebuiltScenePath, false))
                {
                    Debug.LogError($"Rebuilt-scene Step 70 could not save '{RebuiltScenePath}'.");
                    return;
                }

                int missingScripts = CountMissingScripts(rebuiltScene);
                int legacyTargets = CountComponentsByTypeName(rebuiltScene, "Hanger51.Aircraft.P51WingArmamentServiceTarget");
                int safePoints = CountComponentsByTypeName(rebuiltScene, "Hanger51.Aircraft.P51WingArmamentServicePoint");

                Debug.Log(
                    $"Rebuilt-scene Step 70 created '{RebuiltScenePath}' in a BRAND-NEW scene container. "
                    + $"Roots={rebuiltScene.rootCount}/{originalRootCount}, MissingScripts={missingScripts}, "
                    + $"SafeArmamentPoints={safePoints}, LegacyArmamentTargets={legacyTargets}. "
                    + "The original scene remains untouched.");

                if (missingScripts != 0 || legacyTargets != 0)
                {
                    Debug.LogError(
                        "Rebuilt-scene Step 70 validation failed before build. Missing scripts or legacy armament "
                        + "targets remain, so this rebuilt scene will not be used for the test build.");
                    return;
                }

                // Close temporary/rebuilt editor scenes before building from the saved rebuilt asset.
                EditorSceneManager.CloseScene(rebuiltScene, true);
                rebuiltScene = default;
                EditorSceneManager.CloseScene(tempSourceScene, true);
                tempSourceScene = default;
                DeleteAssetIfPresent(TempSourcePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                PrepareOutputFolder();
                string outputPath = Path.Combine(OutputFolder, ExecutableName);
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { RebuiltScenePath },
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
                    Debug.LogError($"Rebuilt-scene Step 70 Windows build failed. {failure}");
                    return;
                }

                Debug.Log(
                    $"Rebuilt-scene Step 70 build PASSED. Size={report.summary.totalSize} bytes; "
                    + $"duration={report.summary.totalTime}. Launching rebuilt-scene player now.");
                Launch(outputPath, Path.Combine(OutputFolder, LogName));
            }
            catch (Exception exception)
            {
                Debug.LogError("Rebuilt-scene Step 70 failed unexpectedly.\n" + exception);
            }
            finally
            {
                if (rebuiltScene.IsValid() && rebuiltScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(rebuiltScene, true);
                }
                if (tempSourceScene.IsValid() && tempSourceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(tempSourceScene, true);
                }
                DeleteAssetIfPresent(TempSourcePath);

                // Keep the user's original scene active/open. Step 70 never replaces it.
                if (originalScene.IsValid() && originalScene.isLoaded)
                {
                    SceneManager.SetActiveScene(originalScene);
                }
            }
        }

        [MenuItem("Hanger 51/Build/71 - Validate Rebuilt Game Scene")]
        public static void ValidateRebuiltScene()
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(RebuiltScenePath);
            if (asset == null)
            {
                Debug.LogError($"Rebuilt-scene Step 71 failed: '{RebuiltScenePath}' does not exist. Run Step 70 first.");
                return;
            }

            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(RebuiltScenePath, OpenSceneMode.Additive);
                int missingScripts = CountMissingScripts(scene);
                int legacyTargets = CountComponentsByTypeName(scene, "Hanger51.Aircraft.P51WingArmamentServiceTarget");
                int safePoints = CountComponentsByTypeName(scene, "Hanger51.Aircraft.P51WingArmamentServicePoint");
                int monoCount = CountMonoBehaviours(scene);

                if (missingScripts == 0 && legacyTargets == 0 && safePoints == 14)
                {
                    Debug.Log(
                        $"Rebuilt-scene validation PASSED for '{RebuiltScenePath}'. Roots={scene.rootCount}, "
                        + $"MonoBehaviours={monoCount}, SafeArmamentPoints={safePoints}, "
                        + "LegacyArmamentTargets=0, MissingScripts=0.");
                }
                else
                {
                    Debug.LogError(
                        $"Rebuilt-scene validation FAILED for '{RebuiltScenePath}'. Roots={scene.rootCount}, "
                        + $"MonoBehaviours={monoCount}, SafeArmamentPoints={safePoints}, "
                        + $"LegacyArmamentTargets={legacyTargets}, MissingScripts={missingScripts}.");
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("Hanger 51/Build/72 - Promote Rebuilt Scene to Main Build Scene")]
        public static void PromoteRebuiltScene()
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(RebuiltScenePath);
            if (asset == null)
            {
                Debug.LogError($"Rebuilt-scene Step 72 failed: '{RebuiltScenePath}' does not exist. Run Step 70 first.");
                return;
            }

            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Rebuilt-scene Step 72 failed. Exit Play mode and wait for compilation to finish first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Rebuilt-scene Step 72 could not save the currently open scene(s).");
                return;
            }

            Scene rebuilt = EditorSceneManager.OpenScene(RebuiltScenePath, OpenSceneMode.Single);
            if (!rebuilt.IsValid() || !rebuilt.isLoaded)
            {
                Debug.LogError("Rebuilt-scene Step 72 could not open the rebuilt scene.");
                return;
            }

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            System.Collections.Generic.List<EditorBuildSettingsScene> scenes =
                new System.Collections.Generic.List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(RebuiltScenePath, true));
            for (int index = 0; index < existing.Length; index++)
            {
                EditorBuildSettingsScene entry = existing[index];
                if (string.Equals(entry.path, RebuiltScenePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(entry.path, "Assets/sofar.unity", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep the original scene asset as a backup, but do not build it anymore.
                    scenes.Add(new EditorBuildSettingsScene(entry.path, false));
                    continue;
                }
                scenes.Add(entry);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Rebuilt-scene Step 72 PASSED. '{RebuiltScenePath}' is open and is now the first enabled "
                + "build scene. The original 'Assets/sofar.unity' was NOT deleted or overwritten and is disabled "
                + "in Build Settings as a backup. You can now run normal Build Step 3.");
        }

        private static bool CanOperate(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Rebuilt-scene Step 70 failed. Exit Play mode first.");
                return false;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Rebuilt-scene Step 70 failed. Wait for Unity to finish compiling first.");
                return false;
            }
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Rebuilt-scene Step 70 failed. Open the saved real game scene first.");
                return false;
            }
            if (string.Equals(scene.path, RebuiltScenePath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("Rebuilt-scene Step 70 expects the original game scene as the active scene, not the rebuilt scene.");
                return false;
            }
            return true;
        }

        private static int CountMissingScripts(Scene scene)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (root == null) continue;
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform transform = transforms[transformIndex];
                    if (transform == null) continue;
                    count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
            }
            return count;
        }

        private static int CountComponentsByTypeName(Scene scene, string fullTypeName)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null) continue;
                    Type type = behaviour.GetType();
                    string name = type.FullName ?? type.Name;
                    if (name == fullTypeName) count++;
                }
            }
            return count;
        }

        private static int CountMonoBehaviours(Scene scene)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                count += roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true).Length;
            }
            return count;
        }

        private static void PrepareOutputFolder()
        {
            if (Directory.Exists(OutputFolder))
            {
                Directory.Delete(OutputFolder, true);
            }
            Directory.CreateDirectory(OutputFolder);
        }

        private static void Launch(string outputPath, string logPath)
        {
            string executablePath = Path.GetFullPath(outputPath);
            string absoluteLogPath = Path.GetFullPath(logPath);
            if (!File.Exists(executablePath))
            {
                Debug.LogError($"Rebuilt-scene EXE not found at '{executablePath}'.");
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
                Debug.LogError("Rebuilt-scene build succeeded, but Windows returned no Player process.");
                return;
            }

            Debug.Log($"Launched rebuilt-scene diagnostic PID {process.Id}. Log: '{absoluteLogPath}'.");
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
