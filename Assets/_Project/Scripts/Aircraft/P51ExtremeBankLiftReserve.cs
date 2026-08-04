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
        [SerializeField, Range(35f, 80f)] private float supportBeginsDegrees = 58f;
        [SerializeField, Range(60f, 89f)] private float fullSupportDegrees = 84f;
        [SerializeField, Min(1f)] private float minimumSupportSpeedMetersPerSecond = 27f;
        [SerializeField, Min(1f)] private float fullSupportSpeedMetersPerSecond = 40f;
        [SerializeField, Range(0f, 1f)] private float maximumVerticalGravitySupport = 0.70f;
        [SerializeField, Min(0f)] private float extremeBankRollDamping = 9000f;

        private P51FlightController flightController;
        private Rigidbody aircraftBody;

        public float SupportBeginsDegrees => supportBeginsDegrees;
        public float FullSupportDegrees => fullSupportDegrees;
        public float MaximumVerticalGravitySupport => maximumVerticalGravitySupport;

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

            Vector3 localVelocity = transform.InverseTransformDirection(
                aircraftBody.linearVelocity);
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

            // This is deliberately less than one gravity. A very steep bank
            // still descends and loses energy, but it no longer loses every
            // bit of vertical support in a single physics step.
            float supportAcceleration = Physics.gravity.magnitude
                * maximumVerticalGravitySupport
                * bankFactor
                * speedFactor;
            aircraftBody.AddForce(
                Vector3.up * supportAcceleration,
                ForceMode.Acceleration);

            Vector3 localAngularVelocity = transform.InverseTransformDirection(
                aircraftBody.angularVelocity);
            aircraftBody.AddRelativeTorque(
                Vector3.forward
                * -localAngularVelocity.z
                * extremeBankRollDamping
                * bankFactor,
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
        }
    }
}
