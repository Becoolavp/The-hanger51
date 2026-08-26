using System;
using System.Reflection;
using Hanger51.Aircraft;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FinalFuelTailwheelParkingPolishSetup
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string TailwheelRaisedMarkerName = "P-51 Tailwheel Raised Marker";
        private const float CorrectedTailRestDistance = 0.54f;

        [MenuItem("Hanger 51/P-51 Mustang/57 - Fix Tailwheel Visual, Fuel Entry Conflict and Final Parked Stability")]
        public static void ApplyFinalPolish()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 57 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 57 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 57 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 57 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int parkingConfigured = 0;
            int tailwheelFollowersConfigured = 0;
            int restDistancesCorrected = 0;

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51RaycastLandingGear physics = flight.GetComponent<P51RaycastLandingGear>();
                P51LandingGearMaintenanceController maintenance =
                    flight.GetComponent<P51LandingGearMaintenanceController>();
                if (physics == null || maintenance == null)
                {
                    continue;
                }

                P51ParkedGroundStabilizer stabilizer =
                    flight.GetComponent<P51ParkedGroundStabilizer>();
                if (stabilizer == null)
                {
                    stabilizer = Undo.AddComponent<P51ParkedGroundStabilizer>(flight.gameObject);
                }
                stabilizer.ConfigureParkingStability(2.5f, 0.55f, 24f, 20f);
                EditorUtility.SetDirty(stabilizer);
                parkingConfigured++;

                if (FindChildRecursive(flight.transform, TailwheelRaisedMarkerName) != null)
                {
                    SerializedObject serializedPhysics = new SerializedObject(physics);
                    SerializedProperty tailRest = serializedPhysics.FindProperty("tailRestGroundDistance");
                    if (tailRest != null && tailRest.floatValue < CorrectedTailRestDistance - 0.001f)
                    {
                        tailRest.floatValue = CorrectedTailRestDistance;
                        serializedPhysics.ApplyModifiedPropertiesWithoutUndo();
                        restDistancesCorrected++;
                    }
                    stabilizer.RepairTailwheelCalibrationNow();
                    EditorUtility.SetDirty(physics);
                }

                Transform gearRoot = FindChildRecursive(
                    flight.transform,
                    "P-51 Serviceable Retractable Landing Gear");
                Transform[] tires =
                {
                    FindChildRecursive(gearRoot, "Left Main Tire Visual"),
                    FindChildRecursive(gearRoot, "Right Main Tire Visual"),
                    FindChildRecursive(gearRoot, "Tailwheel Tire Visual")
                };
                Transform[] proxies =
                {
                    ReadPrivateTransform(physics, "leftMainVisual"),
                    ReadPrivateTransform(physics, "rightMainVisual"),
                    ReadPrivateTransform(physics, "tailwheelVisual")
                };

                if (tires[2] != null && proxies[2] != null)
                {
                    P51LandingGearVisualSuspensionFollower follower =
                        flight.GetComponent<P51LandingGearVisualSuspensionFollower>();
                    if (follower == null)
                    {
                        follower = Undo.AddComponent<P51LandingGearVisualSuspensionFollower>(flight.gameObject);
                    }
                    follower.Configure(maintenance, tires, proxies);
                    EditorUtility.SetDirty(follower);
                    tailwheelFollowersConfigured++;
                }
            }

            FirstPersonController player =
                Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null)
            {
                Debug.LogError("P-51 Step 57 failed. The Player FirstPersonController is missing.");
                return;
            }

            P51FuelPlayerInteractor fuelInteractor = player.GetComponent<P51FuelPlayerInteractor>();
            if (fuelInteractor == null)
            {
                fuelInteractor = Undo.AddComponent<P51FuelPlayerInteractor>(player.gameObject);
            }
            P51PilotPlayerInteractor pilotInteractor = player.GetComponent<P51PilotPlayerInteractor>();
            if (pilotInteractor == null)
            {
                Debug.LogError("P-51 Step 57 failed. The Player cockpit interactor is missing.", player);
                return;
            }

            EditorUtility.SetDirty(fuelInteractor);
            EditorUtility.SetDirty(pilotInteractor);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 57 applied the fixes but Unity could not save the scene.");
                return;
            }

            Debug.Log(
                $"P-51 Step 57 complete. Parking stabilizers configured={parkingConfigured}, tailwheel visual followers configured={tailwheelFollowersConfigured}, "
                + $"tailwheel rest distances newly corrected={restDistancesCorrected}. Fuel servicing now owns E while aimed at fuel hardware, "
                + "so the cockpit prompt/entry is suppressed; the grounded tailwheel stays on the runway while its lower oleo stretches down to meet it; "
                + "and pilot-occupied engine-off aircraft receive a low-speed hard parking lock.",
                player);
        }

        [MenuItem("Hanger 51/P-51 Mustang/58 - Validate Tailwheel, Fuel Interaction and Final Parked Stability")]
        public static void ValidateFinalPolish()
        {
            bool passed = true;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 58 failed: no P-51 aircraft were found.");
                return;
            }

            int validAircraft = 0;
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51RaycastLandingGear physics = flight.GetComponent<P51RaycastLandingGear>();
                P51ParkedGroundStabilizer stabilizer = flight.GetComponent<P51ParkedGroundStabilizer>();
                P51LandingGearVisualSuspensionFollower follower =
                    flight.GetComponent<P51LandingGearVisualSuspensionFollower>();
                Transform tire = FindChildRecursive(flight.transform, "Tailwheel Tire Visual");
                Transform strut = FindChildRecursive(flight.transform, "Tailwheel Oleo Strut");

                if (physics == null || stabilizer == null || follower == null || tire == null || strut == null)
                {
                    Debug.LogError($"P-51 Step 58 failed: '{flight.name}' is missing tailwheel/parking components or visuals.", flight);
                    passed = false;
                    continue;
                }

                SerializedObject serializedPhysics = new SerializedObject(physics);
                SerializedProperty tailRest = serializedPhysics.FindProperty("tailRestGroundDistance");
                if (FindChildRecursive(flight.transform, TailwheelRaisedMarkerName) != null
                    && (tailRest == null || tailRest.floatValue < CorrectedTailRestDistance - 0.001f))
                {
                    Debug.LogError($"P-51 Step 58 failed: '{flight.name}' raised tailwheel rest distance is not calibrated to at least {CorrectedTailRestDistance:F2} m.", physics);
                    passed = false;
                }

                if (stabilizer.HardParkingLockSpeedMetersPerSecond < 0.50f)
                {
                    Debug.LogError($"P-51 Step 58 failed: '{flight.name}' final parked hard-lock setting was not applied.", stabilizer);
                    passed = false;
                }

                if (!follower.TailwheelStrutConnected)
                {
                    Debug.LogError($"P-51 Step 58 failed: '{flight.name}' tailwheel suspension follower cannot find the tire/oleo connection.", follower);
                    passed = false;
                }

                validAircraft++;
            }

            FirstPersonController player =
                Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            P51FuelPlayerInteractor fuelInteractor =
                player != null ? player.GetComponent<P51FuelPlayerInteractor>() : null;
            P51PilotPlayerInteractor pilotInteractor =
                player != null ? player.GetComponent<P51PilotPlayerInteractor>() : null;
            if (player == null || fuelInteractor == null || pilotInteractor == null)
            {
                Debug.LogError("P-51 Step 58 failed: Player fuel/cockpit interaction components are incomplete.");
                passed = false;
            }

            P51FuelCap[] caps = Object.FindObjectsByType<P51FuelCap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (caps.Length == 0)
            {
                Debug.LogError("P-51 Step 58 failed: no removable P-51 fuel caps were found.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 58 passed. Valid aircraft={validAircraft}, fuel caps={caps.Length}. "
                    + "Fuel targeting has priority over cockpit entry/prompt, raised tailwheel suspension is calibrated, "
                    + "the grounded tailwheel is visually reconnected to its oleo, and final engine-off parking stabilization is installed.");
            }
        }

        private static Transform ReadPrivateTransform(P51RaycastLandingGear physics, string fieldName)
        {
            if (physics == null)
            {
                return null;
            }

            FieldInfo field = typeof(P51RaycastLandingGear).GetField(fieldName, PrivateInstance);
            return field != null ? field.GetValue(physics) as Transform : null;
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null && candidate.name == targetName)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
