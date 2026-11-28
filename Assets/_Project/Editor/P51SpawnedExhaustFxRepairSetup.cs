using Hanger51.Aircraft;
using Hanger51.Commerce;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51SpawnedExhaustFxRepairSetup
    {
        private const string MasterAircraftName = "P-51D Mustang Test Aircraft";

        [MenuItem("Hanger 51/P-51 Mustang/51 - Fix Spawned P-51 Duplicate Exhaust Fire and Smoke")]
        public static void InstallSpawnedExhaustRepair()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 51 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 51 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject master = GameObject.Find(MasterAircraftName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || master == null)
            {
                Debug.LogError("P-51 Step 51 failed. Open the saved hangar scene containing the master P-51 first.");
                return;
            }

            P51MerlinAudioAndExhaustFxController[] controllers =
                Object.FindObjectsByType<P51MerlinAudioAndExhaustFxController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int repaired = 0;
            for (int index = 0; index < controllers.Length; index++)
            {
                P51MerlinAudioAndExhaustFxController controller = controllers[index];
                if (controller == null)
                {
                    continue;
                }

                P51ExhaustFxCloneSanitizer sanitizer =
                    controller.GetComponent<P51ExhaustFxCloneSanitizer>();
                if (sanitizer == null)
                {
                    sanitizer = Undo.AddComponent<P51ExhaustFxCloneSanitizer>(controller.gameObject);
                }
                EditorUtility.SetDirty(sanitizer);
                repaired++;
            }

            HangarAircraftSpawnConsole console =
                Object.FindFirstObjectByType<HangarAircraftSpawnConsole>(FindObjectsInactive.Include);
            if (console != null
                && console.MasterAircraftSource != null
                && console.MasterAircraftSource.GetComponent<P51ExhaustFxCloneSanitizer>() == null)
            {
                P51ExhaustFxCloneSanitizer sanitizer =
                    Undo.AddComponent<P51ExhaustFxCloneSanitizer>(console.MasterAircraftSource);
                EditorUtility.SetDirty(sanitizer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 51 completed its edits but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Debug.Log(
                $"P-51 Step 51 complete. Installed exhaust-FX clone sanitizers on {repaired} Merlin-audio P-51 source/template object(s). "
                + "Spawned airplanes now keep only the 12 exhaust emitters owned by their own Merlin controller, remove stale cloned duplicates, and continuously realign startup fire/smoke to the actual exhaust-stack outlets.",
                master);
        }

        [MenuItem("Hanger 51/P-51 Mustang/52 - Validate Spawned P-51 Exhaust FX Repair")]
        public static void ValidateSpawnedExhaustRepair()
        {
            bool passed = true;
            GameObject master = GameObject.Find(MasterAircraftName);
            if (master == null)
            {
                Debug.LogError("P-51 Step 52 failed: master P-51 is missing.");
                return;
            }

            int stackCount = CountExhaustStacks(master);
            if (stackCount != 12)
            {
                Debug.LogError($"P-51 Step 52 failed: master P-51 has {stackCount} exhaust stacks; expected 12.", master);
                passed = false;
            }

            P51MerlinAudioAndExhaustFxController[] controllers =
                Object.FindObjectsByType<P51MerlinAudioAndExhaustFxController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int protectedControllers = 0;
            for (int index = 0; index < controllers.Length; index++)
            {
                P51MerlinAudioAndExhaustFxController controller = controllers[index];
                if (controller == null)
                {
                    continue;
                }

                if (controller.GetComponent<P51ExhaustFxCloneSanitizer>() == null)
                {
                    Debug.LogError(
                        $"P-51 Step 52 failed: '{controller.gameObject.name}' has Merlin startup exhaust FX but no clone sanitizer.",
                        controller);
                    passed = false;
                }
                else
                {
                    protectedControllers++;
                }
            }

            HangarAircraftSpawnConsole console =
                Object.FindFirstObjectByType<HangarAircraftSpawnConsole>(FindObjectsInactive.Include);
            if (console == null
                || console.MasterAircraftSource != master
                || master.GetComponent<P51ExhaustFxCloneSanitizer>() == null)
            {
                Debug.LogError("P-51 Step 52 failed: live-master hangar spawner or master exhaust sanitizer is not configured correctly.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 52 passed. Master exhaust stacks={stackCount}/12 and Merlin-audio aircraft/template controllers protected={protectedControllers}. "
                    + "Newly spawned P-51s will discard cloned runtime exhaust emitters and use only their own correctly aligned 12-stack startup FX.",
                    master);
            }
        }

        private static int CountExhaustStacks(GameObject aircraft)
        {
            int count = 0;
            Transform[] all = aircraft.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null && candidate.name.Contains("Exhaust Stack"))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
