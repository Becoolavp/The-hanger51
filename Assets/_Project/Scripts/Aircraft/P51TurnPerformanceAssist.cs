using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51TurnPerformanceAssist : MonoBehaviour
    {
        [Header("Turn Support")]
        [SerializeField, Min(1f)] private float minimumAssistSpeedMetersPerSecond = 20f;
        [SerializeField, Min(1f)] private float fullAssistSpeedMetersPerSecond = 34f;
        [SerializeField, Range(0f, 1f)] private float bankLiftSupport = 0.62f;
        [SerializeField, Min(0f)] private float maximumExtraLoadG = 0.85f;
        [SerializeField, Min(0f)] private float coordinatedYawTorque = 18000f;
        [SerializeField, Range(20f, 85f)] private float maximumAssistedBankDegrees = 70f;

        private P51FlightController flightController;
        private Rigidbody aircraftBody;

        public float MinimumAssistSpeedMetersPerSecond => minimumAssistSpeedMetersPerSecond;
        public float FullAssistSpeedMetersPerSecond => fullAssistSpeedMetersPerSecond;
        public float BankLiftSupport => bankLiftSupport;
        public float MaximumExtraLoadG => maximumExtraLoadG;
        public float CoordinatedYawTorque => coordinatedYawTorque;

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
            bankLiftSupport = Mathf.Clamp01(configuredBankLiftSupport);
            maximumExtraLoadG = Mathf.Max(0f, configuredMaximumExtraLoadG);
            coordinatedYawTorque = Mathf.Max(0f, configuredCoordinatedYawTorque);
            maximumAssistedBankDegrees = Mathf.Clamp(
                configuredMaximumAssistedBankDegrees,
                20f,
                85f);
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

            float maximumBankSine = Mathf.Sin(
                maximumAssistedBankDegrees * Mathf.Deg2Rad);
            float bankSine = Mathf.Clamp(
                Vector3.Dot(transform.right, Vector3.up),
                -maximumBankSine,
                maximumBankSine);
            float bankMagnitude = Mathf.Abs(bankSine);
            if (bankMagnitude < 0.08f)
            {
                return;
            }

            float bankCosine = Mathf.Sqrt(
                Mathf.Max(0.04f, 1f - bankSine * bankSine));
            float requiredAdditionalLoadG = Mathf.Clamp(
                1f / bankCosine - 1f,
                0f,
                maximumExtraLoadG);
            float supportedLoadG = requiredAdditionalLoadG
                * bankLiftSupport
                * speedFactor;

            Vector3 liftDirection = Vector3.ProjectOnPlane(
                transform.up,
                velocity.normalized);
            if (liftDirection.sqrMagnitude > 0.001f)
            {
                liftDirection.Normalize();
                aircraftBody.AddForce(
                    liftDirection
                    * aircraftBody.mass
                    * Physics.gravity.magnitude
                    * supportedLoadG,
                    ForceMode.Force);
            }

            // A banked airplane needs a small yaw response to keep the nose
            // following the turn. This reduces the large sideslip and energy
            // loss produced by roll-only keyboard controls while preserving
            // the player's responsibility to manage pitch and throttle.
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
            bankLiftSupport = Mathf.Clamp01(bankLiftSupport);
            maximumExtraLoadG = Mathf.Max(0f, maximumExtraLoadG);
            coordinatedYawTorque = Mathf.Max(0f, coordinatedYawTorque);
            maximumAssistedBankDegrees = Mathf.Clamp(
                maximumAssistedBankDegrees,
                20f,
                85f);
        }
    }
}
