using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FuelFillerHeightRepairSetup
    {
        private const float TargetFillerLocalY = 1.91f;
        private const float CapInstalledOffsetY = 0.046f;
        private static readonly Vector3 CapRemovalOffset = new Vector3(-0.28f, 0.05f, -0.10f);

        [MenuItem("Hanger 51/P-51 Mustang/61 - Lower Fuel Cap and Filler Tube to Fuselage")]
        public static void LowerFuelCapAndFillerTube()
        {
            if (!CanEdit(out Scene scene))
            {
                return;
            }

            P51FuelSystem[] systems = Object.FindObjectsByType<P51FuelSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (systems.Length == 0)
            {
                Debug.LogError("P-51 Step 61 failed. No P-51 fuel systems were found in the current scene.");
                return;
            }

            int repaired = 0;
            for (int index = 0; index < systems.Length; index++)
            {
                P51FuelSystem fuel = systems[index];
                if (fuel == null || !fuel.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51FuelFiller filler = fuel.GetComponentInChildren<P51FuelFiller>(true);
                P51FuelCap cap = fuel.GetComponentInChildren<P51FuelCap>(true);
                if (filler == null || cap == null)
                {
                    Debug.LogWarning($"P-51 Step 61 skipped '{fuel.name}' because its filler or cap is missing.", fuel);
                    continue;
                }

                Undo.RecordObject(filler.transform, "Lower P-51 fuel filler tube");
                Vector3 fillerPosition = filler.transform.localPosition;
                fillerPosition.y = TargetFillerLocalY;
                filler.transform.localPosition = fillerPosition;

                Vector3 installedPosition = fillerPosition + Vector3.up * CapInstalledOffsetY;
                cap.Configure(
                    fuel,
                    P51FuelTankStation.Fuselage,
                    cap.transform,
                    installedPosition,
                    Vector3.zero,
                    installedPosition + CapRemovalOffset,
                    new Vector3(70f, 20f, 12f));
                filler.Configure(fuel, cap, P51FuelTankStation.Fuselage, 1.35f);

                EditorUtility.SetDirty(filler);
                EditorUtility.SetDirty(cap);
                EditorUtility.SetDirty(filler.transform);
                EditorUtility.SetDirty(cap.transform);
                repaired++;
            }

            if (repaired == 0)
            {
                Debug.LogError("P-51 Step 61 failed. Fuel systems were found, but no complete cap/filler assemblies could be repaired.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 61 lowered the fuel hardware but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Debug.Log(
                $"P-51 Step 61 complete. Lowered {repaired} fuel filler/cap assembly set(s) by placing the filler tube at local Y {TargetFillerLocalY:F2}. "
                + "The removable cap's installed and removed positions were updated with it, so the visible hardware and refueling interaction remain aligned with the fuselage.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/62 - Validate Fuel Cap and Filler Tube Height")]
        public static void ValidateFuelCapAndFillerTube()
        {
            bool passed = true;
            P51FuelSystem[] systems = Object.FindObjectsByType<P51FuelSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (systems.Length == 0)
            {
                Debug.LogError("P-51 Step 62 failed. No P-51 fuel systems were found.");
                return;
            }

            int validated = 0;
            for (int index = 0; index < systems.Length; index++)
            {
                P51FuelSystem fuel = systems[index];
                if (fuel == null || !fuel.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51FuelFiller filler = fuel.GetComponentInChildren<P51FuelFiller>(true);
                P51FuelCap cap = fuel.GetComponentInChildren<P51FuelCap>(true);
                if (filler == null || cap == null)
                {
                    Debug.LogError($"P-51 Step 62 failed. '{fuel.name}' is missing its filler or cap.", fuel);
                    passed = false;
                    continue;
                }

                float expectedCapY = TargetFillerLocalY + CapInstalledOffsetY;
                if (Mathf.Abs(filler.transform.localPosition.y - TargetFillerLocalY) > 0.005f)
                {
                    Debug.LogError(
                        $"P-51 Step 62 failed. '{fuel.name}' filler is at local Y {filler.transform.localPosition.y:F3}; expected {TargetFillerLocalY:F3}.",
                        filler);
                    passed = false;
                }
                if (!cap.IsRemoved && Mathf.Abs(cap.transform.localPosition.y - expectedCapY) > 0.005f)
                {
                    Debug.LogError(
                        $"P-51 Step 62 failed. '{fuel.name}' installed cap is not aligned with the lowered filler tube.",
                        cap);
                    passed = false;
                }
                if (cap.FuelSystem != fuel || filler.FuelSystem != fuel || filler.FuelCap != cap)
                {
                    Debug.LogError(
                        $"P-51 Step 62 failed. '{fuel.name}' cap/filler references are not connected to the same aircraft fuel system.",
                        fuel);
                    passed = false;
                }
                validated++;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 62 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 62 passed. Validated {validated} lowered P-51 fuel cap/filler assembly set(s); cap removal and refueling interaction remain connected.");
            }
        }

        private static bool CanEdit(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 61 failed. Exit Play mode first.");
                return false;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 61 failed. Wait for Unity to finish compiling.");
                return false;
            }
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 61 failed. Open and save the Hanger 51 test scene first.");
                return false;
            }
            return true;
        }
    }
}
