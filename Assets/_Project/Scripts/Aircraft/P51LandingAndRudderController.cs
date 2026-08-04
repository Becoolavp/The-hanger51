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
        [SerializeField, Min(0f)] private float rolloutAdhesionAcceleration = 3.4f;
        [SerializeField, Min(1f)] private float rolloutAdhesionFullSpeedMetersPerSecond = 42f;
        [SerializeField, Min(0f)] private float groundedPitchDamping = 24000f;
        [SerializeField, Min(0f)] private float groundedRollDamping = 15000f;
        [SerializeField, Min(0.5f)] private float maximumDepenetrationVelocity = 3f;

        private P51FlightController flightController;
        private P51RaycastLandingGear landingGear;
        private Rigidbody aircraftBody;
        private int previousGroundedWheelCount;
        private float rudderInput;

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
            previousGroundedWheelCount = landingGear != null
                ? landingGear.GroundedWheelCount
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
            rolloutAdhesionAcceleration = Mathf.Max(0f, configuredRolloutAdhesion);
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

            // The raycast gear applies its own body tuning earlier in the
            // physics step. Reassert the lower value afterward so collider
            // penetration correction cannot launch the airplane upward.
            aircraftBody.maxDepenetrationVelocity = maximumDepenetrationVelocity;

            if (flightController == null
                || landingGear == null
                || aircraftBody.isKinematic
                || !flightController.PilotPresent)
            {
                rudderInput = 0f;
                previousGroundedWheelCount = landingGear != null
                    ? landingGear.GroundedWheelCount
                    : 0;
                return;
            }

            ReadRudderInput();
            ApplyRudderForces();
            ApplyLowPowerApproachDrag();
            ApplyTouchdownAndRolloutControl();
            previousGroundedWheelCount = landingGear.GroundedWheelCount;
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

            if (!landingGear.AnyWheelGrounded)
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
            if (landingGear.AnyWheelGrounded
                || flightController.Throttle >= lowPowerThrottleThreshold)
            {
                return;
            }

            Vector3 velocity = aircraftBody.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < approachDragFadesMetersPerSecond
                || speed > approachDragBeginsMetersPerSecond
                || velocity.sqrMagnitude < 0.25f)
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
                -velocity.normalized * acceleration,
                ForceMode.Acceleration);
        }

        private void ApplyTouchdownAndRolloutControl()
        {
            int groundedWheelCount = landingGear.GroundedWheelCount;
            if (groundedWheelCount <= 0)
            {
                return;
            }

            Vector3 velocity = aircraftBody.linearVelocity;
            float verticalVelocity = Vector3.Dot(velocity, Vector3.up);

            // Remove a controlled portion of the first downward impact energy.
            // This represents tire and oleo compression and prevents the spring
            // model from returning nearly all of that energy as a bounce.
            if (previousGroundedWheelCount == 0 && verticalVelocity < -0.35f)
            {
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                    velocity,
                    Vector3.up);
                aircraftBody.linearVelocity = horizontalVelocity
                    + Vector3.up
                    * verticalVelocity
                    * touchdownVerticalVelocityRetention;
                velocity = aircraftBody.linearVelocity;
                verticalVelocity = Vector3.Dot(velocity, Vector3.up);
            }

            if (verticalVelocity > 0f)
            {
                aircraftBody.AddForce(
                    Vector3.down * verticalVelocity * upwardReboundDamping,
                    ForceMode.Acceleration);
            }

            if (groundedWheelCount >= 2)
            {
                float horizontalSpeed = Vector3.ProjectOnPlane(
                    aircraftBody.linearVelocity,
                    Vector3.up).magnitude;
                float speedFactor = Mathf.InverseLerp(
                    5f,
                    rolloutAdhesionFullSpeedMetersPerSecond,
                    horizontalSpeed);
                float lowPowerFactor = 1f - Mathf.InverseLerp(
                    0.20f,
                    0.72f,
                    flightController.Throttle);
                float contactFactor = groundedWheelCount >= 3 ? 1f : 0.78f;

                aircraftBody.AddForce(
                    Vector3.down
                    * rolloutAdhesionAcceleration
                    * speedFactor
                    * lowPowerFactor
                    * contactFactor,
                    ForceMode.Acceleration);

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

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
            style.normal.textColor = Color.white;
            GUI.Box(
                new Rect(18f, 228f, 355f, 34f),
                "LEFT / RIGHT ARROWS: RUDDER",
                style);
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
            approachDragBeginsMetersPerSecond = Mathf.Max(
                approachDragFadesMetersPerSecond + 1f,
                approachDragBeginsMetersPerSecond);
            approachDragFadesMetersPerSecond = Mathf.Max(
                1f,
                approachDragFadesMetersPerSecond);
            maximumApproachDragAcceleration = Mathf.Max(
                0f,
                maximumApproachDragAcceleration);
            touchdownVerticalVelocityRetention = Mathf.Clamp(
                touchdownVerticalVelocityRetention,
                0.1f,
                1f);
            upwardReboundDamping = Mathf.Max(0f, upwardReboundDamping);
            rolloutAdhesionAcceleration = Mathf.Max(
                0f,
                rolloutAdhesionAcceleration);
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
