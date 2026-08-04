using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51EasyFlightGroundSafetySetup
    {
        [MenuItem("Hanger 51/P-51 Mustang/26 - Simplify Flight Handling and Harden Ground Contact")]
        public static void SimplifyFlightHandlingAndHardenGroundContact()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 26 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 26 failed. Open and save the movement-test scene first.");
                return;
            }

            P51FlightController flight = FindFlightController();
            if (flight == null)
            {
                Debug.LogError("P-51 Step 26 failed. No P-51 flight controller was found.");
                return;
            }

            Rigidbody body = flight.GetComponent<Rigidbody>();
            P51RaycastLandingGear gear = flight.GetComponent<P51RaycastLandingGear>();
            if (body == null || gear == null)
            {
                Debug.LogError("P-51 Step 26 failed. The aircraft Rigidbody or raycast landing gear is missing.", flight);
                return;
            }

            TuneBaseFlightModel(flight);

            P51TurnPerformanceAssist turnAssist =
                flight.GetComponent<P51TurnPerformanceAssist>();
            if (turnAssist == null)
            {
                turnAssist = Undo.AddComponent<P51TurnPerformanceAssist>(flight.gameObject);
            }
            turnAssist.Configure(18f, 34f, 0f, 0f, 15000f, 78f);
            turnAssist.ConfigureEasyHandling(3.2f, 20f);

            P51ExtremeBankLiftReserve bankProtection =
                flight.GetComponent<P51ExtremeBankLiftReserve>();
            if (bankProtection == null)
            {
                bankProtection = Undo.AddComponent<P51ExtremeBankLiftReserve>(flight.gameObject);
            }
            bankProtection.Configure(65f, 86f, 27f, 40f, 0.48f, 11000f);
            bankProtection.ConfigureDescentProtection(8.5f, 0.82f, 0.20f);

            P51LandingAndRudderController landing =
                flight.GetComponent<P51LandingAndRudderController>();
            if (landing == null)
            {
                landing = Undo.AddComponent<P51LandingAndRudderController>(flight.gameObject);
            }
            landing.Configure(34000f, 36f, 0.36f, 11f, 0f, 20f);

            TuneRaycastGear(gear);

            P51GroundPenetrationGuard groundGuard =
                flight.GetComponent<P51GroundPenetrationGuard>();
            if (groundGuard == null)
            {
                groundGuard = Undo.AddComponent<P51GroundPenetrationGuard>(flight.gameObject);
            }
            groundGuard.Configure(flight, gear, body, 0.27f, 0.13f, 1.25f, 20f);

            P51PilotSeat seat = flight.GetComponentInChildren<P51PilotSeat>(true);
            P51EmergencyExitSafety exitSafety =
                flight.GetComponent<P51EmergencyExitSafety>();
            if (exitSafety == null)
            {
                exitSafety = Undo.AddComponent<P51EmergencyExitSafety>(flight.gameObject);
            }
            exitSafety.Configure(seat);

            body.maxDepenetrationVelocity = 20f;
            body.collisionDetectionMode = body.isKinematic
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.ContinuousDynamic;

            EditorUtility.SetDirty(flight);
            EditorUtility.SetDirty(turnAssist);
            EditorUtility.SetDirty(bankProtection);
            EditorUtility.SetDirty(landing);
            EditorUtility.SetDirty(gear);
            EditorUtility.SetDirty(groundGuard);
            EditorUtility.SetDirty(exitSafety);
            EditorUtility.SetDirty(body);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 26 changed the aircraft but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 26 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = flight.gameObject;
            Debug.Log(
                "P-51 Step 26 complete. Removed artificial turn-climb lift, added strong lateral flight damping, changed extreme-bank protection to descent-only assistance, added hard-stop wheel penetration recovery, increased depenetration speed, and enabled safe cockpit exit at any time.",
                flight);
        }

        [MenuItem("Hanger 51/P-51 Mustang/27 - Validate Easy Flight and Ground Safety")]
        public static void ValidateEasyFlightAndGroundSafety()
        {
            bool passed = true;
            P51FlightController flight = FindFlightController();
            if (flight == null)
            {
                Debug.LogError("P-51 Step 27 failed: no P-51 flight controller exists.");
                return;
            }

            Rigidbody body = flight.GetComponent<Rigidbody>();
            P51RaycastLandingGear gear = flight.GetComponent<P51RaycastLandingGear>();
            P51TurnPerformanceAssist turnAssist =
                flight.GetComponent<P51TurnPerformanceAssist>();
            P51ExtremeBankLiftReserve bankProtection =
                flight.GetComponent<P51ExtremeBankLiftReserve>();
            P51LandingAndRudderController landing =
                flight.GetComponent<P51LandingAndRudderController>();
            P51GroundPenetrationGuard groundGuard =
                flight.GetComponent<P51GroundPenetrationGuard>();
            P51EmergencyExitSafety exitSafety =
                flight.GetComponent<P51EmergencyExitSafety>();
            P51PilotSeat seat = flight.GetComponentInChildren<P51PilotSeat>(true);

            if (body == null || gear == null || landing == null)
            {
                Debug.LogError("P-51 Step 27 failed: required Rigidbody, raycast gear, or landing controller is missing.", flight);
                passed = false;
            }

            if (turnAssist == null
                || turnAssist.BankLiftSupport > 0.001f
                || turnAssist.MaximumExtraLoadG > 0.001f
                || turnAssist.LateralSlipDamping < 3f
                || turnAssist.MaximumLateralCorrectionAcceleration < 19f)
            {
                Debug.LogError("P-51 Step 27 failed: turn assistance still adds lift or lacks lateral damping.", flight);
                passed = false;
            }

            if (bankProtection == null
                || bankProtection.SupportBeginsDegrees < 64f
                || bankProtection.MaximumAssistedDescentRateMetersPerSecond > 9f)
            {
                Debug.LogError("P-51 Step 27 failed: descent-only extreme-bank protection is not configured.", flight);
                passed = false;
            }

            if (groundGuard == null
                || groundGuard.MainMinimumAnchorHeight < 0.25f
                || groundGuard.MaximumDepenetrationVelocity < 19f)
            {
                Debug.LogError("P-51 Step 27 failed: hard-stop ground penetration protection is missing or incorrectly configured.", flight);
                passed = false;
            }

            if (exitSafety == null || seat == null)
            {
                Debug.LogError("P-51 Step 27 failed: emergency cockpit-exit safety is missing.", flight);
                passed = false;
            }

            if (!ValidateFlightValues(flight))
            {
                Debug.LogError("P-51 Step 27 failed: easy-flight aerodynamic values were not applied.", flight);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 27 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 27 passed. Turn lift cannot create a banked climb, lateral sliding is strongly damped, steep-bank help is descent-only, hard landings have a wheel hard stop, and cockpit exit is available at any time.",
                    flight);
            }
        }

        private static void TuneBaseFlightModel(P51FlightController flight)
        {
            SerializedObject serialized = new SerializedObject(flight);
            SetFloat(serialized, "zeroAngleLiftCoefficient", 0.24f);
            SetFloat(serialized, "liftSlopePerRadian", 4.6f);
            SetFloat(serialized, "maximumLiftCoefficient", 1.50f);
            SetFloat(serialized, "parasiteDragCoefficient", 0.034f);
            SetFloat(serialized, "inducedDragFactor", 0.040f);
            SetFloat(serialized, "fullStallSpeedMetersPerSecond", 18.5f);
            SetFloat(serialized, "liftRecoverySpeedMetersPerSecond", 33f);
            SetFloat(serialized, "sideAreaSquareMeters", 8f);
            SetFloat(serialized, "sideDragCoefficient", 1.45f);
            SetFloat(serialized, "pitchTorque", 44000f);
            SetFloat(serialized, "rollTorque", 62000f);
            SetFloat(serialized, "yawStabilityTorque", 34000f);
            SetFloat(serialized, "pitchDamping", 26000f);
            SetFloat(serialized, "rollDamping", 23000f);
            SetFloat(serialized, "yawDamping", 34000f);
            SetFloat(serialized, "fullControlSpeedMetersPerSecond", 36f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TuneRaycastGear(P51RaycastLandingGear gear)
        {
            SerializedObject serialized = new SerializedObject(gear);
            SetFloat(serialized, "visualPositionSharpness", 22f);
            SetFloat(serialized, "airborneVisualReturnSharpness", 18f);
            SetFloat(serialized, "mainSuspensionTravel", 0.30f);
            SetFloat(serialized, "mainSpringStrength", 210000f);
            SetFloat(serialized, "mainDamperStrength", 32000f);
            SetFloat(serialized, "tailSuspensionTravel", 0.25f);
            SetFloat(serialized, "tailSpringStrength", 65000f);
            SetFloat(serialized, "tailDamperStrength", 12000f);
            SetFloat(serialized, "minimumSupportingForce", 850f);
            SetFloat(serialized, "releaseWhileClimbingSpeed", 0.55f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ValidateFlightValues(P51FlightController flight)
        {
            SerializedObject serialized = new SerializedObject(flight);
            return Approximately(serialized, "zeroAngleLiftCoefficient", 0.24f, 0.01f)
                && Approximately(serialized, "sideDragCoefficient", 1.45f, 0.05f)
                && Approximately(serialized, "yawDamping", 34000f, 100f)
                && Approximately(serialized, "rollDamping", 23000f, 100f);
        }

        private static bool Approximately(
            SerializedObject serialized,
            string propertyName,
            float expected,
            float tolerance)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null
                && Mathf.Abs(property.floatValue - expected) <= tolerance;
        }

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static P51FlightController FindFlightController()
        {
            P51FlightController[] found = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }
    }
}
