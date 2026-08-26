using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(P51RaycastLandingGear))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51ParkedGroundStabilizer : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string TailwheelRaisedMarkerName = "P-51 Tailwheel Raised Marker";
        private const float TailwheelRaiseCompensationMeters = 0.20f;

        [Header("Engine-Off Parking Stability")]
        [SerializeField, Min(0.1f)] private float parkingBrakeReleaseSpeedMetersPerSecond = 2.5f;
        [SerializeField, Min(0f)] private float horizontalVelocityDamping = 16f;
        [SerializeField, Min(0f)] private float angularVelocityDamping = 13f;
        [SerializeField, Min(0f)] private float horizontalStopThreshold = 0.045f;
        [SerializeField, Min(0f)] private float angularStopThreshold = 0.035f;

        private P51FlightController flightController;
        private P51RaycastLandingGear landingGear;
        private Rigidbody aircraftBody;
        private bool tailwheelCompensationChecked;

        public bool ParkingStabilizationActive { get; private set; }
        public bool TailwheelRestDistanceCompensated { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnSceneAircraft()
        {
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || flight.GetComponent<P51RaycastLandingGear>() == null)
                {
                    continue;
                }

                if (flight.GetComponent<P51ParkedGroundStabilizer>() == null)
                {
                    flight.gameObject.AddComponent<P51ParkedGroundStabilizer>();
                }
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyTailwheelRestDistanceCompensation();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyTailwheelRestDistanceCompensation();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            ApplyTailwheelRestDistanceCompensation();
            ParkingStabilizationActive = false;

            if (flightController == null
                || landingGear == null
                || aircraftBody == null
                || aircraftBody.isKinematic
                || flightController.EngineRunning
                || landingGear.GroundedWheelCount < 2)
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                aircraftBody.linearVelocity,
                Vector3.up);
            if (horizontalVelocity.magnitude > parkingBrakeReleaseSpeedMetersPerSecond)
            {
                return;
            }

            ParkingStabilizationActive = true;

            float linearBlend = 1f - Mathf.Exp(
                -Mathf.Max(0f, horizontalVelocityDamping) * Time.fixedDeltaTime);
            Vector3 verticalVelocity = Vector3.Project(aircraftBody.linearVelocity, Vector3.up);
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, linearBlend);
            if (horizontalVelocity.magnitude <= horizontalStopThreshold)
            {
                horizontalVelocity = Vector3.zero;
            }
            aircraftBody.linearVelocity = verticalVelocity + horizontalVelocity;

            float angularBlend = 1f - Mathf.Exp(
                -Mathf.Max(0f, angularVelocityDamping) * Time.fixedDeltaTime);
            Vector3 angularVelocity = Vector3.Lerp(
                aircraftBody.angularVelocity,
                Vector3.zero,
                angularBlend);
            if (angularVelocity.magnitude <= angularStopThreshold)
            {
                angularVelocity = Vector3.zero;
            }
            aircraftBody.angularVelocity = angularVelocity;
        }

        public void RepairTailwheelCalibrationNow()
        {
            tailwheelCompensationChecked = false;
            ApplyTailwheelRestDistanceCompensation();
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
            if (landingGear == null)
            {
                landingGear = GetComponent<P51RaycastLandingGear>();
            }
            if (aircraftBody == null)
            {
                aircraftBody = GetComponent<Rigidbody>();
            }
        }

        private void ApplyTailwheelRestDistanceCompensation()
        {
            if (tailwheelCompensationChecked || landingGear == null)
            {
                return;
            }

            tailwheelCompensationChecked = true;
            TailwheelRestDistanceCompensated = false;

            Transform marker = FindChildRecursive(transform, TailwheelRaisedMarkerName);
            if (marker == null)
            {
                return;
            }

            FieldInfo field = typeof(P51RaycastLandingGear).GetField(
                "tailRestGroundDistance",
                PrivateInstance);
            if (field == null || field.FieldType != typeof(float))
            {
                Debug.LogError(
                    "P-51 parked-ground stabilizer could not find the tailwheel rest-distance field.",
                    this);
                return;
            }

            float current = (float)field.GetValue(landingGear);
            float correctedMinimum = 0.34f + TailwheelRaiseCompensationMeters;
            if (current < correctedMinimum - 0.001f)
            {
                field.SetValue(landingGear, current + TailwheelRaiseCompensationMeters);
            }

            TailwheelRestDistanceCompensated = true;
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

        private void OnValidate()
        {
            parkingBrakeReleaseSpeedMetersPerSecond = Mathf.Max(
                0.1f,
                parkingBrakeReleaseSpeedMetersPerSecond);
            horizontalVelocityDamping = Mathf.Max(0f, horizontalVelocityDamping);
            angularVelocityDamping = Mathf.Max(0f, angularVelocityDamping);
            horizontalStopThreshold = Mathf.Max(0f, horizontalStopThreshold);
            angularStopThreshold = Mathf.Max(0f, angularStopThreshold);
        }
    }
}
