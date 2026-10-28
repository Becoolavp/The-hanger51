using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51MerlinAudioStartupAndAmmoFitSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private static readonly Vector3 FinalAmmoScale = new Vector3(0.70f, 0.24f, 0.72f);
        private static readonly Vector3 FinalAmmoLocalPosition = new Vector3(0f, 0.045f, 0f);

        [MenuItem("Hanger 51/P-51 Mustang/42 - Final Ammo Fit and Add Merlin Engine Audio Startup FX")]
        public static void InstallFinalAmmoAndMerlinAudio()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 42 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 42 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || aircraft == null)
            {
                Debug.LogError("P-51 Step 42 failed. Open the saved hangar scene containing the P-51 first.");
                return;
            }

            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            if (armamentRoot == null)
            {
                Debug.LogError("P-51 Step 42 failed. The serviceable wing armament root is missing.", aircraft);
                return;
            }

            int fittedAmmoBoxes = FitAllAmmoBoxes(armamentRoot);
            if (fittedAmmoBoxes != 6)
            {
                Debug.LogError($"P-51 Step 42 failed. Expected 6 installed ammo-box visuals, found {fittedAmmoBoxes}.", aircraft);
                return;
            }

            P51FlightController flightController = aircraft.GetComponent<P51FlightController>();
            if (flightController == null)
            {
                Debug.LogError("P-51 Step 42 failed. The P-51 flight controller is missing.", aircraft);
                return;
            }

            P51MerlinLifecycleController lifecycle = aircraft.GetComponent<P51MerlinLifecycleController>();
            if (lifecycle == null)
            {
                lifecycle = Undo.AddComponent<P51MerlinLifecycleController>(aircraft);
            }
            lifecycle.Configure(3.2f, 2.2f);
            EditorUtility.SetDirty(lifecycle);

            P51MerlinAudioAndExhaustFxController audioFx =
                aircraft.GetComponent<P51MerlinAudioAndExhaustFxController>();
            if (audioFx == null)
            {
                audioFx = Undo.AddComponent<P51MerlinAudioAndExhaustFxController>(aircraft);
            }
            EditorUtility.SetDirty(audioFx);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 42 completed its edits but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Debug.Log(
                "P-51 Step 42 complete. Refit all six ammunition boxes fully inside the wing, added a 3.2-second Merlin startup and 2.2-second shutdown, "
                + "throttle-responsive deep V-12 rumble/combustion audio, condition-driven rough/broken-engine sound and misfires, and startup fire/smoke from the twelve existing exhaust stacks.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/43 - Validate Ammo Fit and Merlin Engine Audio Startup FX")]
        public static void ValidateFinalAmmoAndMerlinAudio()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 43 failed: P-51 aircraft is missing.");
                return;
            }

            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            int fittedAmmo = 0;
            if (armamentRoot != null)
            {
                Transform[] all = armamentRoot.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < all.Length; index++)
                {
                    Transform candidate = all[index];
                    if (candidate == null || candidate.name != "Installed Wing Ammo Box") continue;
                    if (Approximately(candidate.localScale, FinalAmmoScale)
                        && Approximately(candidate.localPosition, FinalAmmoLocalPosition))
                    {
                        fittedAmmo++;
                    }
                    else
                    {
                        Debug.LogError(
                            $"P-51 Step 43 failed: ammo box '{GetHierarchyPath(candidate)}' is not using the final inside-wing fit.");
                        passed = false;
                    }
                }
            }

            if (fittedAmmo != 6)
            {
                Debug.LogError($"P-51 Step 43 failed: expected 6 final-fitted ammo boxes, found {fittedAmmo}.");
                passed = false;
            }

            P51MerlinLifecycleController lifecycle = aircraft.GetComponent<P51MerlinLifecycleController>();
            P51MerlinAudioAndExhaustFxController audioFx =
                aircraft.GetComponent<P51MerlinAudioAndExhaustFxController>();
            P51EngineConditionPowerBridge conditionBridge = aircraft.GetComponent<P51EngineConditionPowerBridge>();
            if (lifecycle == null)
            {
                Debug.LogError("P-51 Step 43 failed: gradual Merlin lifecycle controller is missing.");
                passed = false;
            }
            if (audioFx == null)
            {
                Debug.LogError("P-51 Step 43 failed: Merlin audio/exhaust FX controller is missing.");
                passed = false;
            }
            if (conditionBridge == null)
            {
                Debug.LogError("P-51 Step 43 failed: engine-condition power bridge is missing, so damage cannot drive engine audio.");
                passed = false;
            }

            int exhaustStacks = 0;
            Transform[] aircraftTransforms = aircraft.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < aircraftTransforms.Length; index++)
            {
                Transform candidate = aircraftTransforms[index];
                if (candidate != null && candidate.name.Contains("Exhaust Stack"))
                {
                    exhaustStacks++;
                }
            }
            if (exhaustStacks != 12)
            {
                Debug.LogError($"P-51 Step 43 failed: expected 12 exhaust stacks for startup FX, found {exhaustStacks}.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 43 passed. Ammo boxes={fittedAmmo}/6, exhaust stacks={exhaustStacks}/12, gradual startup/shutdown, throttle-responsive Merlin audio, "
                    + "condition-driven broken-engine audio, repair recovery, and exhaust startup fire/smoke are installed.");
            }
        }

        private static int FitAllAmmoBoxes(Transform armamentRoot)
        {
            int count = 0;
            Transform[] all = armamentRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate == null || candidate.name != "Installed Wing Ammo Box") continue;

                Undo.RecordObject(candidate, "Final fit P-51 wing ammunition box");
                candidate.localScale = FinalAmmoScale;
                candidate.localPosition = FinalAmmoLocalPosition;
                EditorUtility.SetDirty(candidate);
                count++;
            }
            return count;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f
                && Mathf.Abs(a.y - b.y) < 0.001f
                && Mathf.Abs(a.z - b.z) < 0.001f;
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null) return "<null>";
            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
