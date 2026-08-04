using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51TurnPerformanceAssist : MonoBehaviour
    {
        [Header("Turn Coordination")]
        [SerializeField, Min(1f)] private float minimumAssistSpeedMetersPerSecond = 20f;
        [SerializeField, Min(1f)] private float fullAssistSpeedMetersPerSecond = 34f;
        [SerializeField, Min(0f)] private float coordinatedYawTorque = 15000f;
        [SerializeField, Range(20f, 85f)] private float maximumAssistedBankDegrees = 75f;

        [Header("Easy-Flight Lateral Damping")]
        [SerializeField, Min(0f)] private float lateralSlipDamping = 2.8f;
        [SerializeField, Min(0f)] private float maximumLateralCorrectionAcceleration = 18f;

        [Header("Deprecated Lift Support")]
        [SerializeField, Range(0f, 1f)] private float bankLiftSupport;
        [SerializeField, Min(0f)] private float maximumExtraLoadG;

        private P51FlightController flightController;
        private Rigidbody aircraftBody;

        public float MinimumAssistSpeedMetersPerSecond => minimumAssistSpeedMetersPerSecond;
        public float FullAssistSpeedMetersPerSecond => fullAssistSpeedMetersPerSecond;
        public float BankLiftSupport => bankLiftSupport;
        public float MaximumExtraLoadG => maximumExtraLoadG;
        public float CoordinatedYawTorque => coordinatedYawTorque;
        public float LateralSlipDamping => lateralSlipDamping;
        public float MaximumLateralCorrectionAcceleration =>
            maximumLateralCorrectionAcceleration;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(
            float configuredMinimumAssistSpeed,
            float configuredFullAssistSpeed,
            float configuredBankLiftSupport,
            float configuredMaximumExtraLoadG,
            float configuredCoordinatedYawTorque,
            float configuredMaximumAssistedBankDegrees)
        {
            minimumAssistSpeedMetersPerSecond = Mathf.Max(1f, configuredMinimumAssistSpeed);
            fullAssistSpeedMetersPerSecond = Mathf.Max(
                minimumAssistSpeedMetersPerSecond + 1f,
                configuredFullAssistSpeed);

            // The old implementation added extra lift every time the airplane
            // banked. That could make a nose-down airplane climb. Keep these
            // serialized fields for scene compatibility, but disable the lift
            // bonus permanently. Steep-bank protection is handled separately
            // by a descent-only limiter.
            bankLiftSupport = 0f;
            maximumExtraLoadG = 0f;

            coordinatedYawTorque = Mathf.Max(0f, configuredCoordinatedYawTorque);
            maximumAssistedBankDegrees = Mathf.Clamp(
                configuredMaximumAssistedBankDegrees,
                20f,
                85f);
        }

        public void ConfigureEasyHandling(
            float configuredLateralSlipDamping,
            float configuredMaximumCorrectionAcceleration)
        {
            lateralSlipDamping = Mathf.Max(0f, configuredLateralSlipDamping);
            maximumLateralCorrectionAcceleration = Mathf.Max(
                0f,
                configuredMaximumCorrectionAcceleration);
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            if (flightController == null
                || aircraftBody == null
                || aircraftBody.isKinematic
                || !flightController.PilotPresent
                || flightController.IsGrounded)
            {
                return;
            }

            Vector3 velocity = aircraftBody.linearVelocity;
            if (velocity.sqrMagnitude < 1f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float forwardSpeed = Mathf.Max(0f, localVelocity.z);
            float speedFactor = Mathf.InverseLerp(
                minimumAssistSpeedMetersPerSecond,
                fullAssistSpeedMetersPerSecond,
                forwardSpeed);
            if (speedFactor <= 0f)
            {
                return;
            }

            // Remove the sideways velocity that makes the aircraft feel as if
            // it is sliding on ice. This is linear damping rather than a sudden
            // velocity snap, so turns remain smooth and player-controlled.
            float lateralAcceleration = Mathf.Clamp(
                -localVelocity.x * lateralSlipDamping * speedFactor,
                -maximumLateralCorrectionAcceleration,
                maximumLateralCorrectionAcceleration);
            aircraftBody.AddForce(
                transform.right * lateralAcceleration,
                ForceMode.Acceleration);

            float maximumBankSine = Mathf.Sin(
                maximumAssistedBankDegrees * Mathf.Deg2Rad);
            float bankSine = Mathf.Clamp(
                Vector3.Dot(transform.right, Vector3.up),
                -maximumBankSine,
                maximumBankSine);
            if (Mathf.Abs(bankSine) < 0.08f)
            {
                return;
            }

            // Roll-only keyboard input needs a mild yaw response so the nose
            // follows the turn. No lift or altitude force is applied here.
            float yawTorque = -bankSine
                * coordinatedYawTorque
                * speedFactor;
            aircraftBody.AddRelativeTorque(
                Vector3.up * yawTorque,
                ForceMode.Force);
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }

            if (aircraftBody == null)
            {
                aircraftBody = GetComponent<Rigidbody>();
            }
        }

        private void OnValidate()
        {
            minimumAssistSpeedMetersPerSecond = Mathf.Max(
                1f,
                minimumAssistSpeedMetersPerSecond);
            fullAssistSpeedMetersPerSecond = Mathf.Max(
                minimumAssistSpeedMetersPerSecond + 1f,
                fullAssistSpeedMetersPerSecond);
            bankLiftSupport = 0f;
            maximumExtraLoadG = 0f;
            coordinatedYawTorque = Mathf.Max(0f, coordinatedYawTorque);
            maximumAssistedBankDegrees = Mathf.Clamp(
                maximumAssistedBankDegrees,
                20f,
                85f);
            lateralSlipDamping = Mathf.Max(0f, lateralSlipDamping);
            maximumLateralCorrectionAcceleration = Mathf.Max(
                0f,
                maximumLateralCorrectionAcceleration);
        }
    }
}
