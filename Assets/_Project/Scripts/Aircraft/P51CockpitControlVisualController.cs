using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(230)]
    [DisallowMultipleComponent]
    public sealed class P51CockpitControlVisualController : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private Transform stickPivot;
        [SerializeField] private Transform throttlePivot;
        [SerializeField, Range(1f, 30f)] private float stickPitchDegrees = 15f;
        [SerializeField, Range(1f, 30f)] private float stickRollDegrees = 14f;
        [SerializeField, Range(-60f, 0f)] private float throttleIdleDegrees = -28f;
        [SerializeField, Range(0f, 70f)] private float throttleFullDegrees = 36f;
        [SerializeField, Min(10f)] private float responseDegreesPerSecond = 180f;

        private Quaternion stickNeutralRotation = Quaternion.identity;
        private Quaternion throttleNeutralRotation = Quaternion.identity;
        private bool neutralCaptured;

        public bool IsConfigured => flightController != null
            && stickPivot != null
            && throttlePivot != null;
        public P51FlightController FlightController => flightController;
        public Transform StickPivot => stickPivot;
        public Transform ThrottlePivot => throttlePivot;

        public void Configure(
            P51FlightController configuredFlightController,
            Transform configuredStickPivot,
            Transform configuredThrottlePivot)
        {
            flightController = configuredFlightController;
            stickPivot = configuredStickPivot;
            throttlePivot = configuredThrottlePivot;
            CaptureNeutralRotations();
            ApplyImmediateVisuals();
        }

        private void Awake()
        {
            ResolveReferences();
            CaptureNeutralRotations();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureNeutralRotations();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (!neutralCaptured)
            {
                CaptureNeutralRotations();
            }

            if (!IsConfigured || !neutralCaptured)
            {
                return;
            }

            ReadStickInputs(out float pitch, out float roll);
            Quaternion targetStick = BuildStickRotation(pitch, roll);
            Quaternion targetThrottle = BuildThrottleRotation();

            float step = Mathf.Max(10f, responseDegreesPerSecond) * Time.deltaTime;
            stickPivot.localRotation = Quaternion.RotateTowards(
                stickPivot.localRotation,
                targetStick,
                step);
            throttlePivot.localRotation = Quaternion.RotateTowards(
                throttlePivot.localRotation,
                targetThrottle,
                step);
        }

        private void ReadStickInputs(out float pitch, out float roll)
        {
            pitch = 0f;
            roll = 0f;

            if (flightController == null || !flightController.PilotPresent)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // These are deliberately the same controls read by P51FlightController.
            // W is the existing nose-down command, so the stick moves physically forward.
            if (keyboard.wKey.isPressed) pitch += 1f;
            if (keyboard.sKey.isPressed) pitch -= 1f;
            if (keyboard.aKey.isPressed) roll -= 1f;
            if (keyboard.dKey.isPressed) roll += 1f;
        }

        private Quaternion BuildStickRotation(float pitch, float roll)
        {
            Quaternion inputRotation = Quaternion.Euler(
                pitch * stickPitchDegrees,
                0f,
                -roll * stickRollDegrees);
            return stickNeutralRotation * inputRotation;
        }

        private Quaternion BuildThrottleRotation()
        {
            float throttle = flightController != null
                ? Mathf.Clamp01(flightController.Throttle)
                : 0f;
            float leverAngle = Mathf.Lerp(
                throttleIdleDegrees,
                throttleFullDegrees,
                throttle);
            return throttleNeutralRotation * Quaternion.AngleAxis(leverAngle, Vector3.right);
        }

        private void ApplyImmediateVisuals()
        {
            if (!IsConfigured || !neutralCaptured)
            {
                return;
            }

            stickPivot.localRotation = BuildStickRotation(0f, 0f);
            throttlePivot.localRotation = BuildThrottleRotation();
        }

        private void CaptureNeutralRotations()
        {
            if (stickPivot == null || throttlePivot == null)
            {
                neutralCaptured = false;
                return;
            }

            stickNeutralRotation = stickPivot.localRotation;
            throttleNeutralRotation = throttlePivot.localRotation;
            neutralCaptured = true;
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }

            if (stickPivot == null)
            {
                stickPivot = FindDescendant(transform, "P-51 Cockpit Control Stick Pivot");
            }

            if (throttlePivot == null)
            {
                throttlePivot = FindDescendant(transform, "P-51 Cockpit Throttle Pivot");
            }
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == targetName)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
