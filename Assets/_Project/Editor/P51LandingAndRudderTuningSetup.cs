using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51LandingAndRudderTuningSetup
    {
        [MenuItem("Hanger 51/P-51 Mustang/22 - Tune Landing, Bounce, and Rudder Controls")]
        public static void TuneLandingBounceAndRudderControls()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 22 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            P51FlightController flightController =
                Object.FindFirstObjectByType<P51FlightController>();
            P51RaycastLandingGear landingGear = flightController != null
                ? flightController.GetComponent<P51RaycastLandingGear>()
                : null;
            Rigidbody aircraftBody = flightController != null
                ? flightController.GetComponent<Rigidbody>()
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || flightController == null
                || landingGear == null
                || aircraftBody == null)
            {
                Debug.LogError(
                    "P-51 Step 22 failed. Open the saved movement-test scene and confirm the current P-51 flight and raycast landing-gear systems exist.");
                return;
            }

            SerializedObject serializedFlight = new SerializedObject(flightController);
            SetFloat(serializedFlight, "zeroAngleLiftCoefficient", 0.28f);
            SetFloat(serializedFlight, "liftSlopePerRadian", 4.85f);
            SetFloat(serializedFlight, "maximumLiftCoefficient", 1.58f);
            SetFloat(serializedFlight, "parasiteDragCoefficient", 0.038f);
            SetFloat(serializedFlight, "inducedDragFactor", 0.038f);
            SetFloat(serializedFlight, "fullStallSpeedMetersPerSecond", 21f);
            SetFloat(serializedFlight, "liftRecoverySpeedMetersPerSecond", 35f);
            SetFloat(serializedFlight, "pitchDamping", 27000f);
            SetFloat(serializedFlight, "rollDamping", 19000f);
            SetFloat(serializedFlight, "yawDamping", 26000f);
            serializedFlight.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flightController);

            SerializedObject serializedGear = new SerializedObject(landingGear);
            SetFloat(serializedGear, "mainSuspensionTravel", 0.30f);
            SetFloat(serializedGear, "mainSpringStrength", 175000f);
            SetFloat(serializedGear, "mainDamperStrength", 43000f);
            SetFloat(serializedGear, "tailSuspensionTravel", 0.26f);
            SetFloat(serializedGear, "tailSpringStrength", 52000f);
            SetFloat(serializedGear, "tailDamperStrength", 14500f);
            SetFloat(serializedGear, "mainBrakeFriction", 0.78f);
            SetFloat(serializedGear, "tailBrakeFriction", 0.30f);
            SetFloat(serializedGear, "groundedPitchDamping", 25000f);
            SetFloat(serializedGear, "groundedRollDamping", 16000f);
            serializedGear.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(landingGear);

            P51TurnPerformanceAssist turnAssist =
                flightController.GetComponent<P51TurnPerformanceAssist>();
            if (turnAssist != null)
            {
                SerializedObject serializedTurn = new SerializedObject(turnAssist);
                SetFloat(serializedTurn, "bankLiftSupport", 0.54f);
                SetFloat(serializedTurn, "maximumExtraLoadG", 0.72f);
                SetFloat(serializedTurn, "coordinatedYawTorque", 16500f);
                serializedTurn.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(turnAssist);
            }

            P51LandingAndRudderController landingController =
                flightController.GetComponent<P51LandingAndRudderController>();
            if (landingController == null)
            {
                landingController = Undo.AddComponent<P51LandingAndRudderController>(
                    flightController.gameObject);
            }

            landingController.Configure(
                34000f,
                36f,
                0.42f,
                10f,
                3.4f,
                3f);

            SerializedObject serializedLanding = new SerializedObject(landingController);
            SetFloat(serializedLanding, "lowSpeedGroundYawTorque", 8500f);
            SetFloat(serializedLanding, "groundYawFadeSpeedMetersPerSecond", 24f);
            SetFloat(serializedLanding, "lowPowerThrottleThreshold", 0.42f);
            SetFloat(serializedLanding, "approachDragBeginsMetersPerSecond", 55f);
            SetFloat(serializedLanding, "approachDragFadesMetersPerSecond", 27f);
            SetFloat(serializedLanding, "maximumApproachDragAcceleration", 0.85f);
            SetFloat(serializedLanding, "rolloutAdhesionFullSpeedMetersPerSecond", 42f);
            SetFloat(serializedLanding, "groundedPitchDamping", 24000f);
            SetFloat(serializedLanding, "groundedRollDamping", 15000f);
            serializedLanding.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(landingController);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 22 applied the tuning but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 22 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = flightController.gameObject;
            Debug.Log(
                "P-51 Step 22 complete. Reduced approach float, increased aerodynamic damping, softened and damped the landing gear, added touchdown energy absorption and rollout adhesion, and mapped the left/right arrows to rudder.",
                flightController);
        }

        [MenuItem("Hanger 51/P-51 Mustang/23 - Validate Landing, Bounce, and Rudder Controls")]
        public static void ValidateLandingBounceAndRudderControls()
        {
            bool passed = true;
            P51FlightController flightController =
                Object.FindFirstObjectByType<P51FlightController>();
            P51RaycastLandingGear landingGear = flightController != null
                ? flightController.GetComponent<P51RaycastLandingGear>()
                : null;
            P51LandingAndRudderController landingController =
                flightController != null
                    ? flightController.GetComponent<P51LandingAndRudderController>()
                    : null;

            if (flightController == null
                || landingGear == null
                || landingController == null)
            {
                Debug.LogError(
                    "P-51 Step 23 failed: the flight controller, raycast landing gear, or landing/rudder controller is missing.");
                return;
            }

            SerializedObject serializedFlight = new SerializedObject(flightController);
            passed &= ValidateFloat(
                serializedFlight,
                "zeroAngleLiftCoefficient",
                0.28f,
                "zero-angle lift coefficient");
            passed &= ValidateFloat(
                serializedFlight,
                "maximumLiftCoefficient",
                1.58f,
                "maximum lift coefficient");
            passed &= ValidateFloat(
                serializedFlight,
                "parasiteDragCoefficient",
                0.038f,
                "parasite drag coefficient");
            passed &= ValidateFloat(
                serializedFlight,
                "pitchDamping",
                27000f,
                "pitch damping");

            SerializedObject serializedGear = new SerializedObject(landingGear);
            passed &= ValidateFloat(
                serializedGear,
                "mainSuspensionTravel",
                0.30f,
                "main suspension travel");
            passed &= ValidateFloat(
                serializedGear,
                "mainSpringStrength",
                175000f,
                "main spring strength");
            passed &= ValidateFloat(
                serializedGear,
                "mainDamperStrength",
                43000f,
                "main damper strength");
            passed &= ValidateFloat(
                serializedGear,
                "tailDamperStrength",
                14500f,
                "tailwheel damper strength");

            if (landingController.RudderTorque < 30000f
                || landingController.TouchdownVerticalVelocityRetention > 0.50f
                || landingController.UpwardReboundDamping < 8f
                || landingController.RolloutAdhesionAcceleration < 2.5f)
            {
                Debug.LogError(
                    "P-51 Step 23 failed: the landing/rudder controller is outside the expected tuning range.",
                    landingController);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 23 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 23 passed. Approach drag, revised lift and damping, shock absorption, rebound control, rollout adhesion, softened suspension, and arrow-key rudder controls are configured.");
            }
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static bool ValidateFloat(
            SerializedObject serializedObject,
            string propertyName,
            float expectedValue,
            string displayName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null
                || Mathf.Abs(property.floatValue - expectedValue) > 0.001f)
            {
                float actual = property != null ? property.floatValue : float.NaN;
                Debug.LogError(
                    $"P-51 Step 23 failed: {displayName} is {actual:F3}; expected {expectedValue:F3}.");
                return false;
            }

            return true;
        }
    }
}
