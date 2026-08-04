using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(P51RaycastLandingGear))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51GroundPenetrationGuard : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51RaycastLandingGear landingGear;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Hard-Stop Clearances")]
        [SerializeField, Min(0.05f)] private float mainMinimumAnchorHeight = 0.27f;
        [SerializeField, Min(0.03f)] private float tailMinimumAnchorHeight = 0.13f;
        [SerializeField, Min(0f)] private float surfaceSkin = 0.015f;

        [Header("Recovery")]
        [SerializeField, Min(0.5f)] private float recoveryCastHeight = 2.4f;
        [SerializeField, Min(1f)] private float recoveryCastDistance = 5.5f;
        [SerializeField, Min(0.1f)] private float maximumCorrectionPerStep = 1.25f;
        [SerializeField, Range(0f, 1f)] private float downwardVelocityRetention = 0.08f;
        [SerializeField, Range(0f, 1f)] private float minimumGroundNormalUpDot = 0.45f;
        [SerializeField, Min(0.2f)] private float maximumSurfaceAboveAnchor = 1.6f;
        [SerializeField, Min(1f)] private float maximumDepenetrationVelocity = 20f;

        private readonly RaycastHit[] hits = new RaycastHit[24];

        public float MainMinimumAnchorHeight => mainMinimumAnchorHeight;
        public float TailMinimumAnchorHeight => tailMinimumAnchorHeight;
        public float MaximumDepenetrationVelocity => maximumDepenetrationVelocity;

        private void Awake()
        {
            ResolveReferences();
            ApplyBodySafety();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyBodySafety();
        }

        public void Configure(
            P51FlightController configuredFlightController,
            P51RaycastLandingGear configuredLandingGear,
            Rigidbody configuredBody,
            float configuredMainMinimumHeight,
            float configuredTailMinimumHeight,
            float configuredMaximumCorrection,
            float configuredDepenetrationVelocity)
        {
            flightController = configuredFlightController;
            landingGear = configuredLandingGear;
            aircraftBody = configuredBody;
            mainMinimumAnchorHeight = Mathf.Max(0.05f, configuredMainMinimumHeight);
            tailMinimumAnchorHeight = Mathf.Max(0.03f, configuredTailMinimumHeight);
            maximumCorrectionPerStep = Mathf.Max(0.1f, configuredMaximumCorrection);
            maximumDepenetrationVelocity = Mathf.Max(
                1f,
                configuredDepenetrationVelocity);
            ResolveReferences();
            ApplyBodySafety();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            ApplyBodySafety();

            if (aircraftBody == null
                || aircraftBody.isKinematic
                || landingGear == null
                || !landingGear.IsConfigured)
            {
                return;
            }

            PenetrationCorrection best = default;
            EvaluateAnchor(
                landingGear.LeftMainAnchor,
                mainMinimumAnchorHeight,
                ref best);
            EvaluateAnchor(
                landingGear.RightMainAnchor,
                mainMinimumAnchorHeight,
                ref best);
            EvaluateAnchor(
                landingGear.TailwheelAnchor,
                tailMinimumAnchorHeight,
                ref best);

            if (!best.Valid || best.Distance <= 0f)
            {
                return;
            }

            float correctionDistance = Mathf.Min(
                best.Distance + surfaceSkin,
                maximumCorrectionPerStep);
            Vector3 correction = best.Normal * correctionDistance;
            aircraftBody.position += correction;

            Vector3 velocity = aircraftBody.linearVelocity;
            float inwardVelocity = Vector3.Dot(velocity, best.Normal);
            if (inwardVelocity < 0f)
            {
                velocity -= best.Normal
                    * inwardVelocity
                    * (1f - downwardVelocityRetention);
                aircraftBody.linearVelocity = velocity;
            }

            aircraftBody.WakeUp();
        }

        private void EvaluateAnchor(
            Transform anchor,
            float minimumHeight,
            ref PenetrationCorrection best)
        {
            if (anchor == null)
            {
                return;
            }

            Vector3 origin = anchor.position + Vector3.up * recoveryCastHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                hits,
                recoveryCastDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestScore = float.PositiveInfinity;
            RaycastHit selectedHit = default;
            bool found = false;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                Collider collider = hit.collider;
                if (collider == null
                    || collider.transform.IsChildOf(transform)
                    || hit.normal.y < minimumGroundNormalUpDot)
                {
                    continue;
                }

                float surfaceRelativeHeight = hit.point.y - anchor.position.y;
                if (surfaceRelativeHeight > maximumSurfaceAboveAnchor)
                {
                    continue;
                }

                float signedHeight = Vector3.Dot(
                    anchor.position - hit.point,
                    hit.normal);
                float score = Mathf.Abs(signedHeight - minimumHeight);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                selectedHit = hit;
                found = true;
            }

            if (!found)
            {
                return;
            }

            float currentHeight = Vector3.Dot(
                anchor.position - selectedHit.point,
                selectedHit.normal);
            float penetration = minimumHeight - currentHeight;
            if (penetration <= 0f || (best.Valid && penetration <= best.Distance))
            {
                return;
            }

            best.Valid = true;
            best.Distance = penetration;
            best.Normal = selectedHit.normal.normalized;
        }

        private void ApplyBodySafety()
        {
            if (aircraftBody == null)
            {
                return;
            }

            aircraftBody.maxDepenetrationVelocity = maximumDepenetrationVelocity;
            if (!aircraftBody.isKinematic)
            {
                aircraftBody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousDynamic;
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

        private void OnValidate()
        {
            mainMinimumAnchorHeight = Mathf.Max(0.05f, mainMinimumAnchorHeight);
            tailMinimumAnchorHeight = Mathf.Max(0.03f, tailMinimumAnchorHeight);
            surfaceSkin = Mathf.Max(0f, surfaceSkin);
            recoveryCastHeight = Mathf.Max(0.5f, recoveryCastHeight);
            recoveryCastDistance = Mathf.Max(
                recoveryCastHeight + 0.5f,
                recoveryCastDistance);
            maximumCorrectionPerStep = Mathf.Max(0.1f, maximumCorrectionPerStep);
            downwardVelocityRetention = Mathf.Clamp01(downwardVelocityRetention);
            minimumGroundNormalUpDot = Mathf.Clamp01(minimumGroundNormalUpDot);
            maximumSurfaceAboveAnchor = Mathf.Max(0.2f, maximumSurfaceAboveAnchor);
            maximumDepenetrationVelocity = Mathf.Max(
                1f,
                maximumDepenetrationVelocity);
            ResolveReferences();
        }

        private struct PenetrationCorrection
        {
            public bool Valid;
            public float Distance;
            public Vector3 Normal;
        }
    }
}
