using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51ExtremeBankLiftReserve : MonoBehaviour
    {
        [Header("Steep-Bank Envelope")]
        [SerializeField, Range(35f, 80f)] private float supportBeginsDegrees = 62f;
        [SerializeField, Range(60f, 89f)] private float fullSupportDegrees = 86f;
        [SerializeField, Min(1f)] private float minimumSupportSpeedMetersPerSecond = 27f;
        [SerializeField, Min(1f)] private float fullSupportSpeedMetersPerSecond = 40f;
        [SerializeField, Range(0f, 1f)] private float maximumVerticalGravitySupport = 0.48f;
        [SerializeField, Min(0f)] private float extremeBankRollDamping = 11000f;

        [Header("Descent-Only Protection")]
        [SerializeField, Min(1f)] private float maximumAssistedDescentRateMetersPerSecond = 8.5f;
        [SerializeField, Range(0.05f, 1f)] private float descentCorrectionFraction = 0.82f;
        [SerializeField, Range(0f, 1f)] private float noseDownSupportMultiplier = 0.20f;

        private P51FlightController flightController;
        private Rigidbody aircraftBody;

        public float SupportBeginsDegrees => supportBeginsDegrees;
        public float FullSupportDegrees => fullSupportDegrees;
        public float MaximumVerticalGravitySupport => maximumVerticalGravitySupport;
        public float MaximumAssistedDescentRateMetersPerSecond =>
            maximumAssistedDescentRateMetersPerSecond;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(
            float configuredSupportBegins,
            float configuredFullSupport,
            float configuredMinimumSpeed,
            float configuredFullSpeed,
            float configuredMaximumSupport,
            float configuredRollDamping)
        {
            supportBeginsDegrees = Mathf.Clamp(configuredSupportBegins, 35f, 80f);
            fullSupportDegrees = Mathf.Clamp(
                configuredFullSupport,
                supportBeginsDegrees + 1f,
                89f);
            minimumSupportSpeedMetersPerSecond = Mathf.Max(1f, configuredMinimumSpeed);
            fullSupportSpeedMetersPerSecond = Mathf.Max(
                minimumSupportSpeedMetersPerSecond + 1f,
                configuredFullSpeed);
            maximumVerticalGravitySupport = Mathf.Clamp01(configuredMaximumSupport);
            extremeBankRollDamping = Mathf.Max(0f, configuredRollDamping);
        }

        public void ConfigureDescentProtection(
            float configuredMaximumDescentRate,
            float configuredCorrectionFraction,
            float configuredNoseDownSupportMultiplier)
        {
            maximumAssistedDescentRateMetersPerSecond = Mathf.Max(
                1f,
                configuredMaximumDescentRate);
            descentCorrectionFraction = Mathf.Clamp(
                configuredCorrectionFraction,
                0.05f,
                1f);
            noseDownSupportMultiplier = Mathf.Clamp01(
                configuredNoseDownSupportMultiplier);
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
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float forwardSpeed = Mathf.Max(0f, localVelocity.z);
            float speedFactor = Mathf.InverseLerp(
                minimumSupportSpeedMetersPerSecond,
                fullSupportSpeedMetersPerSecond,
                forwardSpeed);
            if (speedFactor <= 0f)
            {
                return;
            }

            float bankSine = Mathf.Clamp(
                Vector3.Dot(transform.right, Vector3.up),
                -1f,
                1f);
            float bankDegrees = Mathf.Abs(
                Mathf.Asin(bankSine) * Mathf.Rad2Deg);
            float bankFactor = Mathf.InverseLerp(
                supportBeginsDegrees,
                fullSupportDegrees,
                bankDegrees);
            if (bankFactor <= 0f)
            {
                return;
            }

            Vector3 localAngularVelocity = transform.InverseTransformDirection(
                aircraftBody.angularVelocity);
            aircraftBody.AddRelativeTorque(
                Vector3.forward
                * -localAngularVelocity.z
                * extremeBankRollDamping
                * bankFactor,
                ForceMode.Force);

            float verticalSpeed = Vector3.Dot(velocity, Vector3.up);
            float targetMinimumVerticalSpeed =
                -maximumAssistedDescentRateMetersPerSecond;

            // Never add altitude during a level or climbing turn. Assistance
            // begins only after the airplane is already descending faster than
            // the configured easy-flight limit.
            if (verticalSpeed >= targetMinimumVerticalSpeed)
            {
                return;
            }

            float requiredAcceleration =
                (targetMinimumVerticalSpeed - verticalSpeed)
                / Mathf.Max(0.001f, Time.fixedDeltaTime)
                * descentCorrectionFraction;

            float noseVertical = Vector3.Dot(transform.forward, Vector3.up);
            float pitchFactor = Mathf.Lerp(
                noseDownSupportMultiplier,
                1f,
                Mathf.InverseLerp(-0.30f, 0.05f, noseVertical));

            float maximumSupportAcceleration = Physics.gravity.magnitude
                * maximumVerticalGravitySupport
                * bankFactor
                * speedFactor
                * pitchFactor;
            float appliedAcceleration = Mathf.Clamp(
                requiredAcceleration,
                0f,
                maximumSupportAcceleration);

            aircraftBody.AddForce(
                Vector3.up * appliedAcceleration,
                ForceMode.Acceleration);
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
            supportBeginsDegrees = Mathf.Clamp(supportBeginsDegrees, 35f, 80f);
            fullSupportDegrees = Mathf.Clamp(
                fullSupportDegrees,
                supportBeginsDegrees + 1f,
                89f);
            minimumSupportSpeedMetersPerSecond = Mathf.Max(
                1f,
                minimumSupportSpeedMetersPerSecond);
            fullSupportSpeedMetersPerSecond = Mathf.Max(
                minimumSupportSpeedMetersPerSecond + 1f,
                fullSupportSpeedMetersPerSecond);
            maximumVerticalGravitySupport = Mathf.Clamp01(
                maximumVerticalGravitySupport);
            extremeBankRollDamping = Mathf.Max(0f, extremeBankRollDamping);
            maximumAssistedDescentRateMetersPerSecond = Mathf.Max(
                1f,
                maximumAssistedDescentRateMetersPerSecond);
            descentCorrectionFraction = Mathf.Clamp(
                descentCorrectionFraction,
                0.05f,
                1f);
            noseDownSupportMultiplier = Mathf.Clamp01(
                noseDownSupportMultiplier);
        }
    }
}
