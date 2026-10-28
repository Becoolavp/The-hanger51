using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51LandingGearAndLiveSpawnInheritanceRepair
    {
        private const string MasterAircraftName = "P-51D Mustang Test Aircraft";

        [MenuItem("Hanger 51/P-51 Mustang/49 - Fix Retracting Gear Hardware and Live Spawn Inheritance")]
        public static void RepairGearAndSpawner()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 49 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 49 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject master = GameObject.Find(MasterAircraftName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || master == null)
            {
                Debug.LogError("P-51 Step 49 failed. Open the saved hangar scene containing the master P-51 first.");
                return;
            }

            P51LandingGearMaintenanceController[] gearControllers =
                Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int aircraftRepaired = 0;
            int attachmentCount = 0;
            for (int index = 0; index < gearControllers.Length; index++)
            {
                P51LandingGearMaintenanceController maintenance = gearControllers[index];
                if (maintenance == null)
                {
                    continue;
                }

                P51LandingGearServiceAttachmentFollower follower =
                    maintenance.GetComponent<P51LandingGearServiceAttachmentFollower>();
                if (follower == null)
                {
                    follower = Undo.AddComponent<P51LandingGearServiceAttachmentFollower>(maintenance.gameObject);
                }

                Undo.RegisterFullObjectHierarchyUndo(
                    maintenance.gameObject,
                    "Attach landing gear service hardware to retracting gear");
                int attached = follower.RepairHierarchy();
                attachmentCount += attached;
                aircraftRepaired++;
                EditorUtility.SetDirty(follower);
                EditorUtility.SetDirty(maintenance.gameObject);
            }

            HangarAircraftSpawnConsole console =
                Object.FindFirstObjectByType<HangarAircraftSpawnConsole>(FindObjectsInactive.Include);
            AircraftEngineMountReceiver receiver = master.GetComponent<AircraftEngineMountReceiver>();
            EngineAssemblyTransportController engine = receiver != null
                ? receiver.InstalledTransport
                : null;
            if (engine == null)
            {
                EngineAssemblyTransportController[] transports =
                    Object.FindObjectsByType<EngineAssemblyTransportController>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int index = 0; index < transports.Length; index++)
                {
                    EngineAssemblyTransportController candidate = transports[index];
                    if (candidate != null && candidate.TransportRoot != null)
                    {
                        engine = candidate;
                        break;
                    }
                }
            }

            if (console == null || engine == null)
            {
                Debug.LogError("P-51 Step 49 repaired the landing gear, but the hangar spawn console or master Merlin source is missing.");
                return;
            }

            Undo.RecordObject(console, "Bind spawn console to live master P-51");
            console.ConfigureLiveMasterSources(master, engine);
            EditorUtility.SetDirty(console);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 49 completed its edits but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Debug.Log(
                $"P-51 Step 49 complete. Repaired {aircraftRepaired} P-51 landing-gear hierarchy set(s) with {attachmentCount} correctly attached service targets. "
                + "All mount-bolt targets and tire/valve targets now ride with their retracting gear. The hangar spawn console now uses the live master aircraft and live master Merlin as its primary spawn sources, so future master-aircraft/engine features are inherited by newly spawned airplanes.",
                master);
        }

        [MenuItem("Hanger 51/P-51 Mustang/50 - Validate Retracting Gear and Live Spawn Inheritance")]
        public static void ValidateGearAndSpawner()
        {
            bool passed = true;
            GameObject master = GameObject.Find(MasterAircraftName);
            if (master == null)
            {
                Debug.LogError("P-51 Step 50 failed: master P-51 is missing.");
                return;
            }

            P51LandingGearMaintenanceController[] gearControllers =
                Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int validatedAircraft = 0;
            for (int index = 0; index < gearControllers.Length; index++)
            {
                P51LandingGearMaintenanceController maintenance = gearControllers[index];
                if (maintenance == null)
                {
                    continue;
                }

                P51LandingGearServiceAttachmentFollower follower =
                    maintenance.GetComponent<P51LandingGearServiceAttachmentFollower>();
                int attached = follower != null ? follower.CorrectlyAttachedTargetCount : 0;
                if (follower == null || attached != 6)
                {
                    Debug.LogError(
                        $"P-51 Step 50 failed: '{maintenance.gameObject.name}' has {attached}/6 landing-gear service targets attached to the moving gear roots.",
                        maintenance);
                    passed = false;
                }
                else
                {
                    validatedAircraft++;
                }
            }

            HangarAircraftSpawnConsole console =
                Object.FindFirstObjectByType<HangarAircraftSpawnConsole>(FindObjectsInactive.Include);
            if (console == null
                || !console.IsConfigured
                || !console.UsesLiveMasterSources
                || console.MasterAircraftSource != master
                || console.MasterEngineSource == null)
            {
                Debug.LogError("P-51 Step 50 failed: hangar spawn console is not using the live master P-51 and Merlin sources.");
                passed = false;
            }

            if (gearControllers.Length <= 0)
            {
                Debug.LogError("P-51 Step 50 failed: no serviceable landing-gear controller exists.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 50 passed. Validated {validatedAircraft} P-51 landing-gear hierarchy set(s): all 3 mount bolts and all 3 tire/valve service targets follow the retracting gear. "
                    + "The physical hangar spawn console is live-master driven, so newly spawned airplanes clone the current aircraft and installed Merlin feature set rather than a frozen aircraft snapshot.",
                    master);
            }
        }
    }
}
