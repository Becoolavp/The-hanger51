using Hanger51.Aircraft;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class Hanger51RunwaySignLegacyGunAndTowBarRepair
    {
        const string LegacyRadiatorFairingName =
            "Radiator Scoop Transition Fairing";

        static readonly string[] LegacyGunNames =
        {
            "Left Wing Gun 1",
            "Left Wing Gun 2",
            "Left Wing Gun 3",
            "Right Wing Gun 1",
            "Right Wing Gun 2",
            "Right Wing Gun 3"
        };

        [MenuItem("Hanger 51/World/Current/160 - Repair Runway Sign Legacy Guns And Spawned Tow Bar")]
        public static void Build()
        {
            if (EditorApplication.isPlaying ||
                EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 160 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError(
                    "Step 160 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            P51FlightController[] aircraft =
                Object.FindObjectsByType<P51FlightController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int aircraftChecked = 0;
            int legacyGunStubsRemoved = 0;
            int legacyRadiatorFairingsRemoved = 0;

            for (int i=0;i<aircraft.Length;i++)
            {
                P51FlightController flight = aircraft[i];
                if (!flight ||
                    flight.gameObject.scene != scene)
                    continue;

                aircraftChecked++;
                legacyGunStubsRemoved +=
                    RemoveNamedChildren(
                        flight.transform,
                        LegacyGunNames);
                legacyRadiatorFairingsRemoved +=
                    RemoveNamedChildren(
                        flight.transform,
                        new[] { LegacyRadiatorFairingName });

                EditorUtility.SetDirty(flight);
            }

            H51RunwayLightSwitch runwaySwitch =
                Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);

            bool runwaySwitchConfigured = false;
            if (runwaySwitch)
            {
                SerializedObject serializedSwitch =
                    new SerializedObject(runwaySwitch);
                SerializedProperty distance =
                    serializedSwitch.FindProperty(
                        "maxLabelViewDistance");
                if (distance != null)
                {
                    distance.floatValue = 8f;
                    serializedSwitch.ApplyModifiedPropertiesWithoutUndo();
                    runwaySwitchConfigured = true;
                    EditorUtility.SetDirty(runwaySwitch);
                }
            }

            P51TowBarController towBar =
                Object.FindFirstObjectByType<P51TowBarController>(
                    FindObjectsInactive.Include);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 160 complete. Aircraft checked=" +
                aircraftChecked +
                ", obsolete fixed wing-gun stubs removed=" +
                legacyGunStubsRemoved +
                ", obsolete radiator transition fairings removed=" +
                legacyRadiatorFairingsRemoved +
                ", runway sign nearby/occlusion configuration=" +
                runwaySwitchConfigured +
                ", tow bar present=" + (towBar != null) +
                ". Runtime tow-bar code now retargets the nearest active P-51 tailwheel before attachment. Run Step 161 to validate.");
        }

        [MenuItem("Hanger 51/World/Current/161 - Validate Runway Sign Legacy Guns And Spawned Tow Bar")]
        public static void Validate()
        {
            bool passed = true;

            P51FlightController[] aircraft =
                Object.FindObjectsByType<P51FlightController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int aircraftChecked = 0;
            int legacyGunStubs = 0;
            int legacyRadiatorFairings = 0;
            int serviceableGunVisuals = 0;

            for (int i=0;i<aircraft.Length;i++)
            {
                P51FlightController flight = aircraft[i];
                if (!flight ||
                    !flight.gameObject.scene.IsValid())
                    continue;

                aircraftChecked++;
                legacyGunStubs +=
                    CountNamedChildren(
                        flight.transform,
                        LegacyGunNames);
                legacyRadiatorFairings +=
                    CountNamedChildren(
                        flight.transform,
                        new[] { LegacyRadiatorFairingName });
                serviceableGunVisuals +=
                    CountNamedChildren(
                        flight.transform,
                        new[] { "Installed M2 Wing Gun" });
            }

            if (aircraftChecked == 0)
            {
                Debug.LogError(
                    "Step 161 failed: no P-51 aircraft were found.");
                passed = false;
            }

            if (legacyGunStubs != 0)
            {
                Debug.LogError(
                    "Step 161 failed: obsolete fixed wing-gun stubs remain=" +
                    legacyGunStubs + ".");
                passed = false;
            }

            if (legacyRadiatorFairings != 0)
            {
                Debug.LogError(
                    "Step 161 failed: obsolete radiator transition fairings remain=" +
                    legacyRadiatorFairings + ".");
                passed = false;
            }

            if (serviceableGunVisuals < 6)
            {
                Debug.LogError(
                    "Step 161 failed: expected at least six real serviceable Installed M2 Wing Gun visuals; found=" +
                    serviceableGunVisuals + ".");
                passed = false;
            }

            H51RunwayLightSwitch runwaySwitch =
                Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);

            bool runwayVisibilityReady = false;
            if (runwaySwitch)
            {
                SerializedObject serializedSwitch =
                    new SerializedObject(runwaySwitch);
                SerializedProperty distance =
                    serializedSwitch.FindProperty(
                        "maxLabelViewDistance");
                runwayVisibilityReady =
                    distance != null &&
                    distance.floatValue <= 8.01f;
            }

            if (!runwayVisibilityReady)
            {
                Debug.LogError(
                    "Step 161 failed: runway-light sign nearby/occlusion configuration is missing.");
                passed = false;
            }

            P51TowBarController towBar =
                Object.FindFirstObjectByType<P51TowBarController>(
                    FindObjectsInactive.Include);
            if (!towBar || !towBar.IsConfigured)
            {
                Debug.LogError(
                    "Step 161 failed: the tailwheel tow bar is missing or incompletely configured.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Step 161 passed. Aircraft checked=" +
                    aircraftChecked +
                    ", obsolete gun stubs=0, obsolete radiator fairings=0, real serviceable gun visuals=" +
                    serviceableGunVisuals +
                    ", runway sign nearby/occlusion configuration=ready, tow bar=ready for nearest-aircraft runtime targeting.");
            }
        }

        static int RemoveNamedChildren(
            Transform root,
            string[] names)
        {
            if (!root || names == null)
                return 0;

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);
            int removed = 0;

            for (int i=all.Length-1;i>=0;i--)
            {
                Transform candidate = all[i];
                if (!candidate || candidate == root)
                    continue;

                for (int n=0;n<names.Length;n++)
                {
                    if (candidate.name != names[n])
                        continue;

                    Undo.DestroyObjectImmediate(
                        candidate.gameObject);
                    removed++;
                    break;
                }
            }

            return removed;
        }

        static int CountNamedChildren(
            Transform root,
            string[] names)
        {
            if (!root || names == null)
                return 0;

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);
            int count = 0;

            for (int i=0;i<all.Length;i++)
            {
                Transform candidate = all[i];
                if (!candidate || candidate == root)
                    continue;

                for (int n=0;n<names.Length;n++)
                {
                    if (candidate.name == names[n])
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }
    }
}
