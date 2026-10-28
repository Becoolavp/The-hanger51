using Hanger51.Aircraft;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FuelInteractionAndGroundStabilitySetup
    {
        private const string TailwheelMarkerName = "P-51 Tailwheel Raised Marker";
        private const float CorrectedTailRestDistance = 0.54f;

        [MenuItem("Hanger 51/P-51 Mustang/55 - Fix Fuel Interaction and Parked Ground Stability")]
        public static void ApplyRepair()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 55 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 55 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 55 failed. Open and save the active hangar scene first.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 55 failed. No P-51 aircraft were found.");
                return;
            }

            int stabilizers = 0;
            int tailwheelCalibrations = 0;
            int capHitboxes = 0;

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51ParkedGroundStabilizer stabilizer = flight.GetComponent<P51ParkedGroundStabilizer>();
                if (stabilizer == null)
                {
                    stabilizer = Undo.AddComponent<P51ParkedGroundStabilizer>(flight.gameObject);
                }
                stabilizer.RepairTailwheelCalibrationNow();
                EditorUtility.SetDirty(stabilizer);
                stabilizers++;

                P51RaycastLandingGear gear = flight.GetComponent<P51RaycastLandingGear>();
                if (gear != null && FindChildRecursive(flight.transform, TailwheelMarkerName) != null)
                {
                    SerializedObject serializedGear = new SerializedObject(gear);
                    SerializedProperty tailRest = serializedGear.FindProperty("tailRestGroundDistance");
                    if (tailRest != null)
                    {
                        tailRest.floatValue = Mathf.Max(tailRest.floatValue, CorrectedTailRestDistance);
                        serializedGear.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(gear);
                        tailwheelCalibrations++;
                    }
                }

                P51FuelCap[] caps = flight.GetComponentsInChildren<P51FuelCap>(true);
                for (int capIndex = 0; capIndex < caps.Length; capIndex++)
                {
                    P51FuelCap cap = caps[capIndex];
                    if (cap == null) continue;

                    Collider[] existing = cap.GetComponents<Collider>();
                    for (int colliderIndex = 0; colliderIndex < existing.Length; colliderIndex++)
                    {
                        Collider collider = existing[colliderIndex];
                        if (collider != null && !(collider is BoxCollider))
                        {
                            Undo.DestroyObjectImmediate(collider);
                        }
                    }

                    BoxCollider box = cap.GetComponent<BoxCollider>();
                    if (box == null)
                    {
                        box = Undo.AddComponent<BoxCollider>(cap.gameObject);
                    }
                    box.center = Vector3.zero;
                    box.size = new Vector3(2.25f, 4.0f, 2.25f);
                    box.enabled = true;
                    box.isTrigger = true;
                    EditorUtility.SetDirty(box);
                    capHitboxes++;
                }
            }

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player != null && player.GetComponent<P51FuelPlayerInteractor>() == null)
            {
                Undo.AddComponent<P51FuelPlayerInteractor>(player.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene);
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                $"P-51 Step 55 complete. Parking stabilizers={stabilizers}, raised-tailwheel suspension calibrations={tailwheelCalibrations}, "
                + $"fuel-cap interaction hitboxes={capHitboxes}. Fuel filler hits now route E to the cap, carried cans hard-follow the camera, "
                + "and engine-off low-speed aircraft motion is damped while the landing gear is on the ground.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/56 - Validate Fuel Interaction and Parked Ground Stability")]
        public static void ValidateRepair()
        {
            bool passed = true;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 56 failed: no P-51 aircraft were found.");
                return;
            }

            int validAircraft = 0;
            int validCaps = 0;
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid()) continue;

                P51ParkedGroundStabilizer stabilizer = flight.GetComponent<P51ParkedGroundStabilizer>();
                P51RaycastLandingGear gear = flight.GetComponent<P51RaycastLandingGear>();
                if (stabilizer == null || gear == null)
                {
                    Debug.LogError($"P-51 Step 56 failed: '{flight.name}' is missing the ground stabilizer or raycast landing gear.", flight);
                    passed = false;
                    continue;
                }

                if (FindChildRecursive(flight.transform, TailwheelMarkerName) != null)
                {
                    SerializedObject serializedGear = new SerializedObject(gear);
                    SerializedProperty tailRest = serializedGear.FindProperty("tailRestGroundDistance");
                    if (tailRest == null || tailRest.floatValue < CorrectedTailRestDistance - 0.005f)
                    {
                        Debug.LogError($"P-51 Step 56 failed: '{flight.name}' raised tailwheel is not calibrated to at least {CorrectedTailRestDistance:F2} m rest distance.", gear);
                        passed = false;
                    }
                }

                P51FuelCap[] caps = flight.GetComponentsInChildren<P51FuelCap>(true);
                for (int capIndex = 0; capIndex < caps.Length; capIndex++)
                {
                    BoxCollider box = caps[capIndex] != null ? caps[capIndex].GetComponent<BoxCollider>() : null;
                    if (box == null || !box.enabled || !box.isTrigger)
                    {
                        Debug.LogError($"P-51 Step 56 failed: '{flight.name}' fuel cap does not have the enlarged trigger hitbox.", flight);
                        passed = false;
                    }
                    else
                    {
                        validCaps++;
                    }
                }

                validAircraft++;
            }

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null || player.GetComponent<P51FuelPlayerInteractor>() == null)
            {
                Debug.LogError("P-51 Step 56 failed: Player fuel interactor is missing.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 56 passed. Aircraft checked={validAircraft}, enlarged fuel-cap hitboxes={validCaps}. "
                    + "Raised tailwheel suspension calibration is restored, parking stabilization is installed, and the Player fuel interactor is ready.");
            }
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name)
                {
                    return all[index];
                }
            }
            return null;
        }
    }
}
