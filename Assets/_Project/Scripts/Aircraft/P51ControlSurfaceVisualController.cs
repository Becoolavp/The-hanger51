using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(210)]
    [DisallowMultipleComponent]
    public sealed class P51ControlSurfaceVisualController : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51LandingAndRudderController rudderController;
        [SerializeField] private Transform leftAileronPivot;
        [SerializeField] private Transform rightAileronPivot;
        [SerializeField] private Transform leftElevatorPivot;
        [SerializeField] private Transform rightElevatorPivot;
        [SerializeField] private Transform rudderPivot;
        [SerializeField, Range(1f, 35f)] private float aileronDeflectionDegrees = 18f;
        [SerializeField, Range(1f, 35f)] private float elevatorDeflectionDegrees = 24f;
        [SerializeField, Range(1f, 40f)] private float rudderDeflectionDegrees = 28f;
        [SerializeField, Min(1f)] private float responseDegreesPerSecond = 150f;

        private Quaternion leftAileronNeutral;
        private Quaternion rightAileronNeutral;
        private Quaternion leftElevatorNeutral;
        private Quaternion rightElevatorNeutral;
        private Quaternion rudderNeutral;
        private float displayedRoll;
        private float displayedPitch;
        private float displayedRudder;
        private bool neutralCaptured;

        public bool IsConfigured => flightController != null
            && leftAileronPivot != null
            && rightAileronPivot != null
            && leftElevatorPivot != null
            && rightElevatorPivot != null
            && rudderPivot != null;

        public void Configure(
            P51FlightController configuredFlightController,
            P51LandingAndRudderController configuredRudderController,
            Transform configuredLeftAileron,
            Transform configuredRightAileron,
            Transform configuredLeftElevator,
            Transform configuredRightElevator,
            Transform configuredRudder)
        {
            flightController = configuredFlightController;
            rudderController = configuredRudderController;
            leftAileronPivot = configuredLeftAileron;
            rightAileronPivot = configuredRightAileron;
            leftElevatorPivot = configuredLeftElevator;
            rightElevatorPivot = configuredRightElevator;
            rudderPivot = configuredRudder;
            CaptureNeutralRotations();
            ApplyVisuals();
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

        private void Update()
        {
            ResolveReferences();
            if (!neutralCaptured)
            {
                CaptureNeutralRotations();
            }

            float targetPitch = 0f;
            float targetRoll = 0f;
            float targetRudder = 0f;

            if (flightController != null && flightController.PilotPresent)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed) targetPitch += 1f;
                    if (keyboard.sKey.isPressed) targetPitch -= 1f;
                    if (keyboard.aKey.isPressed) targetRoll -= 1f;
                    if (keyboard.dKey.isPressed) targetRoll += 1f;
                    if (keyboard.leftArrowKey.isPressed) targetRudder -= 1f;
                    if (keyboard.rightArrowKey.isPressed) targetRudder += 1f;
                }
                else if (rudderController != null)
                {
                    targetRudder = rudderController.RudderInput;
                }
            }

            float inputResponse = Mathf.Max(2f, responseDegreesPerSecond / 28f);
            displayedPitch = Mathf.MoveTowards(displayedPitch, targetPitch, inputResponse * Time.deltaTime);
            displayedRoll = Mathf.MoveTowards(displayedRoll, targetRoll, inputResponse * Time.deltaTime);
            displayedRudder = Mathf.MoveTowards(displayedRudder, targetRudder, inputResponse * Time.deltaTime);
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (!neutralCaptured)
            {
                return;
            }

            // Aircraft local +X runs right. Positive rotation about +X raises the trailing edge.
            // D/right roll therefore raises the right aileron and lowers the left. W is the
            // existing nose-down command, so it drives both elevator trailing edges down.
            SetRotation(leftAileronPivot, leftAileronNeutral, -displayedRoll * aileronDeflectionDegrees, Vector3.right);
            SetRotation(rightAileronPivot, rightAileronNeutral, displayedRoll * aileronDeflectionDegrees, Vector3.right);
            SetRotation(leftElevatorPivot, leftElevatorNeutral, -displayedPitch * elevatorDeflectionDegrees, Vector3.right);
            SetRotation(rightElevatorPivot, rightElevatorNeutral, -displayedPitch * elevatorDeflectionDegrees, Vector3.right);
            SetRotation(rudderPivot, rudderNeutral, -displayedRudder * rudderDeflectionDegrees, Vector3.up);
        }

        private static void SetRotation(Transform target, Quaternion neutral, float degrees, Vector3 axis)
        {
            if (target != null)
            {
                target.localRotation = neutral * Quaternion.AngleAxis(degrees, axis);
            }
        }

        private void CaptureNeutralRotations()
        {
            if (leftAileronPivot == null || rightAileronPivot == null
                || leftElevatorPivot == null || rightElevatorPivot == null || rudderPivot == null)
            {
                neutralCaptured = false;
                return;
            }

            leftAileronNeutral = leftAileronPivot.localRotation;
            rightAileronNeutral = rightAileronPivot.localRotation;
            leftElevatorNeutral = leftElevatorPivot.localRotation;
            rightElevatorNeutral = rightElevatorPivot.localRotation;
            rudderNeutral = rudderPivot.localRotation;
            neutralCaptured = true;
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
            if (rudderController == null)
            {
                rudderController = GetComponent<P51LandingAndRudderController>();
            }
        }
    }
}
