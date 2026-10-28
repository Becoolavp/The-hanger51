using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(P51RaycastLandingGear))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51LandingAndRudderController : MonoBehaviour
    {
        [Header("Rudder")]
        [SerializeField, Min(1000f)] private float rudderTorque = 34000f;
        [SerializeField, Min(1f)] private float fullRudderAuthoritySpeedMetersPerSecond = 36f;
        [SerializeField, Min(0f)] private float lowSpeedGroundYawTorque = 8500f;
        [SerializeField, Min(1f)] private float groundYawFadeSpeedMetersPerSecond = 24f;

        [Header("Low-Power Approach")]
        [SerializeField, Range(0.05f, 0.8f)] private float lowPowerThrottleThreshold = 0.42f;
        [SerializeField, Min(5f)] private float approachDragBeginsMetersPerSecond = 55f;
        [SerializeField, Min(1f)] private float approachDragFadesMetersPerSecond = 27f;
        [SerializeField, Min(0f)] private float maximumApproachDragAcceleration = 0.85f;

        [Header("Touchdown Energy Absorption")]
        [SerializeField, Range(0.1f, 1f)] private float touchdownVerticalVelocityRetention = 0.42f;
        [SerializeField, Min(0f)] private float upwardReboundDamping = 10f;
        [SerializeField, Min(0.05f)] private float touchdownDampingWindowSeconds = 0.65f;
        [SerializeField, Min(0f)] private float rolloutAdhesionAcceleration = 0f;
        [SerializeField, Min(1f)] private float rolloutAdhesionFullSpeedMetersPerSecond = 42f;
        [SerializeField, Min(0f)] private float groundedPitchDamping = 24000f;
        [SerializeField, Min(0f)] private float groundedRollDamping = 15000f;
        [SerializeField, Min(0.5f)] private float maximumDepenetrationVelocity = 3f;

        private P51FlightController flightController;
        private P51RaycastLandingGear landingGear;
        private Rigidbody aircraftBody;
        private int previousLoadedWheelCount;
        private float lastTouchdownTime = float.NegativeInfinity;
        private float rudderInput;
        private GUIStyle rudderStyle;

        public float RudderInput => rudderInput;
        public float RudderTorque => rudderTorque;
        public float TouchdownVerticalVelocityRetention => touchdownVerticalVelocityRetention;
        public float UpwardReboundDamping => upwardReboundDamping;
        public float RolloutAdhesionAcceleration => rolloutAdhesionAcceleration;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            previousLoadedWheelCount = landingGear != null
                ? landingGear.LoadedWheelCount
                : 0;
        }

        public void Configure(
            float configuredRudderTorque,
            float configuredFullRudderSpeed,
            float configuredTouchdownRetention,
            float configuredReboundDamping,
            float configuredRolloutAdhesion,
            float configuredMaximumDepenetrationVelocity)
        {
            rudderTorque = Mathf.Max(1000f, configuredRudderTorque);
            fullRudderAuthoritySpeedMetersPerSecond = Mathf.Max(
                1f,
                configuredFullRudderSpeed);
            touchdownVerticalVelocityRetention = Mathf.Clamp(
                configuredTouchdownRetention,
                0.1f,
                1f);
            upwardReboundDamping = Mathf.Max(0f, configuredReboundDamping);
            // Kept for backward-compatible setup calls, but continuous
            // downward adhesion is intentionally disabled.
            rolloutAdhesionAcceleration = 0f;
            maximumDepenetrationVelocity = Mathf.Max(
                0.5f,
                configuredMaximumDepenetrationVelocity);
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            if (aircraftBody == null)
            {
                return;
            }

            aircraftBody.maxDepenetrationVelocity = maximumDepenetrationVelocity;

            if (flightController == null
                || landingGear == null
                || aircraftBody.isKinematic
                || !flightController.PilotPresent)
            {
                rudderInput = 0f;
                previousLoadedWheelCount = landingGear != null
                    ? landingGear.LoadedWheelCount
                    : 0;
                return;
            }

            ReadRudderInput();
            ApplyRudderForces();
            ApplyLowPowerApproachDrag();
            ApplyTouchdownAndRolloutControl();
            previousLoadedWheelCount = landingGear.LoadedWheelCount;
        }

        private void ReadRudderInput()
        {
            rudderInput = 0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.leftArrowKey.isPressed)
            {
                rudderInput -= 1f;
            }
            if (keyboard.rightArrowKey.isPressed)
            {
                rudderInput += 1f;
            }
        }

        private void ApplyRudderForces()
        {
            if (Mathf.Abs(rudderInput) < 0.001f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(
                aircraftBody.linearVelocity);
            float forwardSpeed = Mathf.Max(0f, localVelocity.z);
            float authority = Mathf.Lerp(
                0.12f,
                1f,
                Mathf.InverseLerp(
                    7f,
                    fullRudderAuthoritySpeedMetersPerSecond,
                    forwardSpeed));

            aircraftBody.AddRelativeTorque(
                Vector3.up * rudderInput * rudderTorque * authority,
                ForceMode.Force);

            if (!landingGear.AnyWheelLoaded)
            {
                return;
            }

            float horizontalSpeed = Vector3.ProjectOnPlane(
                aircraftBody.linearVelocity,
                Vector3.up).magnitude;
            float groundAuthority = 1f - Mathf.InverseLerp(
                groundYawFadeSpeedMetersPerSecond * 0.45f,
                groundYawFadeSpeedMetersPerSecond,
                horizontalSpeed);
            aircraftBody.AddTorque(
                Vector3.up
                * rudderInput
                * lowSpeedGroundYawTorque
                * groundAuthority,
                ForceMode.Force);
        }

        private void ApplyLowPowerApproachDrag()
        {
            if (landingGear.AnyWheelLoaded
                || flightController.Throttle >= lowPowerThrottleThreshold)
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                aircraftBody.linearVelocity,
                Vector3.up);
            float speed = horizontalVelocity.magnitude;
            if (speed < approachDragFadesMetersPerSecond
                || speed > approachDragBeginsMetersPerSecond
                || horizontalVelocity.sqrMagnitude < 0.25f)
            {
                return;
            }

            float speedFactor = Mathf.InverseLerp(
                approachDragFadesMetersPerSecond,
                approachDragBeginsMetersPerSecond,
                speed);
            float lowPowerFactor = 1f - Mathf.InverseLerp(
                0f,
                lowPowerThrottleThreshold,
                flightController.Throttle);
            float acceleration = maximumApproachDragAcceleration
                * speedFactor
                * lowPowerFactor;

            aircraftBody.AddForce(
                -horizontalVelocity.normalized * acceleration,
                ForceMode.Acceleration);
        }

        private void ApplyTouchdownAndRolloutControl()
        {
            int loadedWheelCount = landingGear.LoadedWheelCount;
            if (loadedWheelCount <= 0)
            {
                return;
            }

            Vector3 velocity = aircraftBody.linearVelocity;
            float verticalVelocity = Vector3.Dot(velocity, Vector3.up);

            if (previousLoadedWheelCount == 0 && verticalVelocity < -0.35f)
            {
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                    velocity,
                    Vector3.up);
                aircraftBody.linearVelocity = horizontalVelocity
                    + Vector3.up
                    * verticalVelocity
                    * touchdownVerticalVelocityRetention;
                lastTouchdownTime = Time.fixedTime;
                velocity = aircraftBody.linearVelocity;
                verticalVelocity = Vector3.Dot(velocity, Vector3.up);
            }

            bool insideTouchdownWindow =
                Time.fixedTime - lastTouchdownTime <= touchdownDampingWindowSeconds;
            if (insideTouchdownWindow
                && verticalVelocity > 0f
                && flightController.Throttle < 0.65f)
            {
                aircraftBody.AddForce(
                    Vector3.down * verticalVelocity * upwardReboundDamping,
                    ForceMode.Acceleration);
            }

            if (loadedWheelCount >= 2)
            {
                Vector3 localAngularVelocity = transform.InverseTransformDirection(
                    aircraftBody.angularVelocity);
                aircraftBody.AddRelativeTorque(
                    new Vector3(
                        -localAngularVelocity.x * groundedPitchDamping,
                        0f,
                        -localAngularVelocity.z * groundedRollDamping),
                    ForceMode.Force);
            }
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

        private void OnGUI()
        {
            if (flightController == null || !flightController.PilotPresent)
            {
                return;
            }

            if (rudderStyle == null)
            {
                rudderStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
                rudderStyle.normal.textColor = Color.white;
            }

            GUI.Box(
                new Rect(18f, Screen.height - 54f, 300f, 34f),
                "LEFT / RIGHT ARROWS: RUDDER",
                rudderStyle);
        }

        private void OnValidate()
        {
            rudderTorque = Mathf.Max(1000f, rudderTorque);
            fullRudderAuthoritySpeedMetersPerSecond = Mathf.Max(
                1f,
                fullRudderAuthoritySpeedMetersPerSecond);
            lowSpeedGroundYawTorque = Mathf.Max(0f, lowSpeedGroundYawTorque);
            groundYawFadeSpeedMetersPerSecond = Mathf.Max(
                1f,
                groundYawFadeSpeedMetersPerSecond);
            lowPowerThrottleThreshold = Mathf.Clamp(
                lowPowerThrottleThreshold,
                0.05f,
                0.8f);
            approachDragFadesMetersPerSecond = Mathf.Max(
                1f,
                approachDragFadesMetersPerSecond);
            approachDragBeginsMetersPerSecond = Mathf.Max(
                approachDragFadesMetersPerSecond + 1f,
                approachDragBeginsMetersPerSecond);
            maximumApproachDragAcceleration = Mathf.Max(
                0f,
                maximumApproachDragAcceleration);
            touchdownVerticalVelocityRetention = Mathf.Clamp(
                touchdownVerticalVelocityRetention,
                0.1f,
                1f);
            upwardReboundDamping = Mathf.Max(0f, upwardReboundDamping);
            touchdownDampingWindowSeconds = Mathf.Max(
                0.05f,
                touchdownDampingWindowSeconds);
            rolloutAdhesionAcceleration = 0f;
            rolloutAdhesionFullSpeedMetersPerSecond = Mathf.Max(
                1f,
                rolloutAdhesionFullSpeedMetersPerSecond);
            groundedPitchDamping = Mathf.Max(0f, groundedPitchDamping);
            groundedRollDamping = Mathf.Max(0f, groundedRollDamping);
            maximumDepenetrationVelocity = Mathf.Max(
                0.5f,
                maximumDepenetrationVelocity);
        }
    }
}
