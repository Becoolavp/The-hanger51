using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class P51RaycastLandingGear : MonoBehaviour
    {
        [Header("Aircraft")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Wheel Anchors")]
        [SerializeField] private Transform leftMainAnchor;
        [SerializeField] private Transform rightMainAnchor;
        [SerializeField] private Transform tailwheelAnchor;

        [Header("Visible Tires")]
        [SerializeField] private Transform leftMainVisual;
        [SerializeField] private Transform rightMainVisual;
        [SerializeField] private Transform tailwheelVisual;

        [Header("Main Gear")]
        [SerializeField, Min(0.1f)] private float mainWheelRadius = 0.38f;
        [SerializeField, Min(0.1f)] private float mainRestGroundDistance = 0.49f;
        [SerializeField, Min(0.05f)] private float mainSuspensionTravel = 0.24f;
        [SerializeField, Min(1000f)] private float mainSpringStrength = 230000f;
        [SerializeField, Min(100f)] private float mainDamperStrength = 22000f;

        [Header("Tailwheel")]
        [SerializeField, Min(0.05f)] private float tailwheelRadius = 0.16f;
        [SerializeField, Min(0.05f)] private float tailRestGroundDistance = 0.34f;
        [SerializeField, Min(0.05f)] private float tailSuspensionTravel = 0.22f;
        [SerializeField, Min(1000f)] private float tailSpringStrength = 70000f;
        [SerializeField, Min(100f)] private float tailDamperStrength = 7500f;
        [SerializeField, Range(0f, 35f)] private float maximumTailwheelSteerAngle = 20f;
        [SerializeField, Min(1f)] private float steeringFadeSpeedMetersPerSecond = 24f;

        [Header("Tire Forces")]
        [SerializeField, Min(0f)] private float mainLateralGrip = 9500f;
        [SerializeField, Min(0f)] private float tailLateralGrip = 4200f;
        [SerializeField, Range(0f, 0.1f)] private float rollingResistanceCoefficient = 0.012f;
        [SerializeField, Min(0f)] private float brakeVelocityGain = 10000f;
        [SerializeField, Range(0.1f, 2f)] private float mainBrakeFriction = 0.92f;
        [SerializeField, Range(0.1f, 2f)] private float tailBrakeFriction = 0.35f;
        [SerializeField, Range(0.1f, 2f)] private float lateralFrictionLimit = 1.15f;

        [Header("Taildragger Balance")]
        [SerializeField] private Vector3 tunedCenterOfMass = new Vector3(0f, 0.84f, -1.05f);
        [SerializeField, Min(0f)] private float groundedPitchDamping = 14000f;
        [SerializeField, Min(0f)] private float groundedRollDamping = 9000f;

        private readonly RaycastHit[] raycastHits = new RaycastHit[16];

        private WheelContact leftContact;
        private WheelContact rightContact;
        private WheelContact tailContact;

        private Quaternion leftBaseRotation;
        private Quaternion rightBaseRotation;
        private Quaternion tailBaseRotation;
        private bool visualRotationsCaptured;
        private float leftSpinDegrees;
        private float rightSpinDegrees;
        private float tailSpinDegrees;
        private float currentTailwheelSteerAngle;

        public bool IsConfigured => flightController != null
            && aircraftBody != null
            && leftMainAnchor != null
            && rightMainAnchor != null
            && tailwheelAnchor != null
            && leftMainVisual != null
            && rightMainVisual != null
            && tailwheelVisual != null;
        public int GroundedWheelCount { get; private set; }
        public bool LeftMainGrounded => leftContact.Grounded;
        public bool RightMainGrounded => rightContact.Grounded;
        public bool TailwheelGrounded => tailContact.Grounded;
        public bool AnyWheelGrounded => GroundedWheelCount > 0;
        public Transform LeftMainAnchor => leftMainAnchor;
        public Transform RightMainAnchor => rightMainAnchor;
        public Transform TailwheelAnchor => tailwheelAnchor;

        private void Awake()
        {
            ResolveReferences();
            CaptureVisualRotations();
            ApplyBodyTuning();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureVisualRotations();
            ApplyBodyTuning();
        }

        private void Start()
        {
            ApplyBodyTuning();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            ApplyBodyTuning();
            CaptureVisualRotations();

            if (!IsConfigured)
            {
                GroundedWheelCount = 0;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool pilotPresent = flightController != null && flightController.PilotPresent;
            float steeringInput = 0f;
            bool braking = false;
            if (pilotPresent && keyboard != null)
            {
                if (keyboard.aKey.isPressed) steeringInput -= 1f;
                if (keyboard.dKey.isPressed) steeringInput += 1f;
                braking = keyboard.spaceKey.isPressed;
            }

            float horizontalSpeed = aircraftBody != null
                ? Vector3.ProjectOnPlane(aircraftBody.linearVelocity, Vector3.up).magnitude
                : 0f;
            float steeringFade = 1f - Mathf.InverseLerp(
                steeringFadeSpeedMetersPerSecond * 0.45f,
                steeringFadeSpeedMetersPerSecond,
                horizontalSpeed);
            currentTailwheelSteerAngle = steeringInput
                * maximumTailwheelSteerAngle
                * steeringFade;

            bool applyForces = aircraftBody != null && !aircraftBody.isKinematic;
            leftContact = EvaluateWheel(
                leftMainAnchor,
                mainWheelRadius,
                mainRestGroundDistance,
                mainSuspensionTravel,
                mainSpringStrength,
                mainDamperStrength,
                mainLateralGrip,
                0f,
                braking,
                mainBrakeFriction,
                applyForces);
            rightContact = EvaluateWheel(
                rightMainAnchor,
                mainWheelRadius,
                mainRestGroundDistance,
                mainSuspensionTravel,
                mainSpringStrength,
                mainDamperStrength,
                mainLateralGrip,
                0f,
                braking,
                mainBrakeFriction,
                applyForces);
            tailContact = EvaluateWheel(
                tailwheelAnchor,
                tailwheelRadius,
                tailRestGroundDistance,
                tailSuspensionTravel,
                tailSpringStrength,
                tailDamperStrength,
                tailLateralGrip,
                currentTailwheelSteerAngle,
                braking,
                tailBrakeFriction,
                applyForces);

            GroundedWheelCount = 0;
            if (leftContact.Grounded) GroundedWheelCount++;
            if (rightContact.Grounded) GroundedWheelCount++;
            if (tailContact.Grounded) GroundedWheelCount++;

            leftSpinDegrees = AdvanceWheelSpin(
                leftSpinDegrees,
                leftContact.ForwardSpeed,
                mainWheelRadius);
            rightSpinDegrees = AdvanceWheelSpin(
                rightSpinDegrees,
                rightContact.ForwardSpeed,
                mainWheelRadius);
            tailSpinDegrees = AdvanceWheelSpin(
                tailSpinDegrees,
                tailContact.ForwardSpeed,
                tailwheelRadius);

            if (applyForces && GroundedWheelCount >= 2)
            {
                ApplyGroundedAngularDamping();
            }
        }

        private void LateUpdate()
        {
            if (!visualRotationsCaptured)
            {
                CaptureVisualRotations();
            }

            UpdateWheelVisual(
                leftMainVisual,
                leftMainAnchor,
                leftContact,
                leftBaseRotation,
                leftSpinDegrees,
                0f,
                mainWheelRadius);
            UpdateWheelVisual(
                rightMainVisual,
                rightMainAnchor,
                rightContact,
                rightBaseRotation,
                rightSpinDegrees,
                0f,
                mainWheelRadius);
            UpdateWheelVisual(
                tailwheelVisual,
                tailwheelAnchor,
                tailContact,
                tailBaseRotation,
                tailSpinDegrees,
                currentTailwheelSteerAngle,
                tailwheelRadius);
        }

        public void Configure(
            P51FlightController configuredFlightController,
            Rigidbody configuredAircraftBody,
            Transform configuredLeftMainAnchor,
            Transform configuredRightMainAnchor,
            Transform configuredTailwheelAnchor,
            Transform configuredLeftMainVisual,
            Transform configuredRightMainVisual,
            Transform configuredTailwheelVisual)
        {
            flightController = configuredFlightController;
            aircraftBody = configuredAircraftBody;
            leftMainAnchor = configuredLeftMainAnchor;
            rightMainAnchor = configuredRightMainAnchor;
            tailwheelAnchor = configuredTailwheelAnchor;
            leftMainVisual = configuredLeftMainVisual;
            rightMainVisual = configuredRightMainVisual;
            tailwheelVisual = configuredTailwheelVisual;
            visualRotationsCaptured = false;
            CaptureVisualRotations();
            ApplyBodyTuning();
        }

        private WheelContact EvaluateWheel(
            Transform wheelAnchor,
            float wheelRadius,
            float restGroundDistance,
            float suspensionTravel,
            float springStrength,
            float damperStrength,
            float lateralGrip,
            float steeringAngle,
            bool braking,
            float brakeFriction,
            bool applyForces)
        {
            WheelContact contact = new WheelContact
            {
                Grounded = false,
                CenterPosition = wheelAnchor != null
                    ? wheelAnchor.position
                    : transform.position,
                SurfaceNormal = transform.up,
                ForwardSpeed = 0f
            };

            if (wheelAnchor == null)
            {
                return contact;
            }

            Vector3 suspensionUp = transform.up.normalized;
            const float rayStartOffset = 0.06f;
            Vector3 rayOrigin = wheelAnchor.position + suspensionUp * rayStartOffset;
            float maximumRayDistance = restGroundDistance + suspensionTravel + rayStartOffset;

            if (!TryFindGroundHit(
                    rayOrigin,
                    -suspensionUp,
                    maximumRayDistance,
                    out RaycastHit groundHit))
            {
                return contact;
            }

            float anchorToGroundDistance = Mathf.Max(
                0f,
                groundHit.distance - rayStartOffset);
            if (anchorToGroundDistance > restGroundDistance + suspensionTravel)
            {
                return contact;
            }

            contact.Grounded = true;
            contact.SurfaceNormal = groundHit.normal.sqrMagnitude > 0.001f
                ? groundHit.normal.normalized
                : suspensionUp;
            contact.CenterPosition = groundHit.point
                + contact.SurfaceNormal * wheelRadius;

            Vector3 wheelForward = Vector3.ProjectOnPlane(
                transform.forward,
                contact.SurfaceNormal);
            if (wheelForward.sqrMagnitude < 0.001f)
            {
                wheelForward = Vector3.ProjectOnPlane(
                    wheelAnchor.forward,
                    contact.SurfaceNormal);
            }
            wheelForward.Normalize();
            if (Mathf.Abs(steeringAngle) > 0.01f)
            {
                wheelForward = Quaternion.AngleAxis(
                    steeringAngle,
                    contact.SurfaceNormal) * wheelForward;
            }

            Vector3 wheelRight = Vector3.Cross(
                contact.SurfaceNormal,
                wheelForward).normalized;
            Vector3 pointVelocity = aircraftBody != null
                ? aircraftBody.GetPointVelocity(wheelAnchor.position)
                : Vector3.zero;
            contact.ForwardSpeed = Vector3.Dot(pointVelocity, wheelForward);
            float lateralSpeed = Vector3.Dot(pointVelocity, wheelRight);

            float compression = Mathf.Clamp(
                restGroundDistance - anchorToGroundDistance,
                0f,
                suspensionTravel);
            float suspensionVelocity = Vector3.Dot(pointVelocity, suspensionUp);
            float suspensionForce = Mathf.Max(
                0f,
                compression * springStrength
                - suspensionVelocity * damperStrength);
            suspensionForce = Mathf.Min(
                suspensionForce,
                springStrength * suspensionTravel * 1.45f);

            if (!applyForces || aircraftBody == null)
            {
                return contact;
            }

            aircraftBody.AddForceAtPosition(
                suspensionUp * suspensionForce,
                wheelAnchor.position,
                ForceMode.Force);

            float lateralLimit = suspensionForce * lateralFrictionLimit;
            float lateralForce = Mathf.Clamp(
                -lateralSpeed * lateralGrip,
                -lateralLimit,
                lateralLimit);
            aircraftBody.AddForceAtPosition(
                wheelRight * lateralForce,
                groundHit.point,
                ForceMode.Force);

            if (Mathf.Abs(contact.ForwardSpeed) > 0.05f)
            {
                float rollingForce = suspensionForce
                    * rollingResistanceCoefficient
                    * -Mathf.Sign(contact.ForwardSpeed);
                aircraftBody.AddForceAtPosition(
                    wheelForward * rollingForce,
                    groundHit.point,
                    ForceMode.Force);
            }

            if (braking)
            {
                float brakeLimit = suspensionForce * brakeFriction;
                float brakeForce = Mathf.Clamp(
                    -contact.ForwardSpeed * brakeVelocityGain,
                    -brakeLimit,
                    brakeLimit);
                aircraftBody.AddForceAtPosition(
                    wheelForward * brakeForce,
                    groundHit.point,
                    ForceMode.Force);
            }

            return contact;
        }

        private bool TryFindGroundHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out RaycastHit bestHit)
        {
            bestHit = default;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                raycastHits,
                distance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            bool found = false;

            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = raycastHits[index].collider;
                if (collider == null
                    || collider.transform.IsChildOf(transform)
                    || raycastHits[index].distance >= nearestDistance)
                {
                    continue;
                }

                bestHit = raycastHits[index];
                nearestDistance = raycastHits[index].distance;
                found = true;
            }

            return found;
        }

        private void ApplyGroundedAngularDamping()
        {
            Vector3 localAngularVelocity = transform.InverseTransformDirection(
                aircraftBody.angularVelocity);
            Vector3 dampingTorque = new Vector3(
                -localAngularVelocity.x * groundedPitchDamping,
                0f,
                -localAngularVelocity.z * groundedRollDamping);
            aircraftBody.AddRelativeTorque(dampingTorque, ForceMode.Force);
        }

        private float AdvanceWheelSpin(
            float currentAngle,
            float forwardSpeed,
            float radius)
        {
            if (radius <= 0.001f)
            {
                return currentAngle;
            }

            float degreesPerSecond = -forwardSpeed / radius * Mathf.Rad2Deg;
            return Mathf.Repeat(
                currentAngle + degreesPerSecond * Time.fixedDeltaTime,
                360f);
        }

        private void UpdateWheelVisual(
            Transform visual,
            Transform anchor,
            WheelContact contact,
            Quaternion baseLocalRotation,
            float spinDegrees,
            float steeringAngle,
            float radius)
        {
            if (visual == null || anchor == null)
            {
                return;
            }

            Vector3 center = contact.Grounded
                ? contact.CenterPosition
                : anchor.position;
            Quaternion steering = Quaternion.AngleAxis(
                steeringAngle,
                Vector3.up);
            Quaternion spin = Quaternion.AngleAxis(
                spinDegrees,
                Vector3.right);
            Quaternion worldRotation = transform.rotation
                * steering
                * spin
                * baseLocalRotation;
            visual.SetPositionAndRotation(center, worldRotation);
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

        private void ApplyBodyTuning()
        {
            if (aircraftBody == null)
            {
                return;
            }

            aircraftBody.centerOfMass = tunedCenterOfMass;
            aircraftBody.maxDepenetrationVelocity = 8f;
            aircraftBody.constraints = RigidbodyConstraints.None;
        }

        private void CaptureVisualRotations()
        {
            if (visualRotationsCaptured)
            {
                return;
            }

            leftBaseRotation = GetVisualRotationRelativeToAircraft(leftMainVisual);
            rightBaseRotation = GetVisualRotationRelativeToAircraft(rightMainVisual);
            tailBaseRotation = GetVisualRotationRelativeToAircraft(tailwheelVisual);
            visualRotationsCaptured = leftMainVisual != null
                && rightMainVisual != null
                && tailwheelVisual != null;
        }

        private Quaternion GetVisualRotationRelativeToAircraft(Transform visual)
        {
            return visual != null
                ? Quaternion.Inverse(transform.rotation) * visual.rotation
                : Quaternion.identity;
        }

        private void OnValidate()
        {
            mainWheelRadius = Mathf.Max(0.1f, mainWheelRadius);
            mainRestGroundDistance = Mathf.Max(mainWheelRadius, mainRestGroundDistance);
            mainSuspensionTravel = Mathf.Max(0.05f, mainSuspensionTravel);
            mainSpringStrength = Mathf.Max(1000f, mainSpringStrength);
            mainDamperStrength = Mathf.Max(100f, mainDamperStrength);
            tailwheelRadius = Mathf.Max(0.05f, tailwheelRadius);
            tailRestGroundDistance = Mathf.Max(tailwheelRadius, tailRestGroundDistance);
            tailSuspensionTravel = Mathf.Max(0.05f, tailSuspensionTravel);
            tailSpringStrength = Mathf.Max(1000f, tailSpringStrength);
            tailDamperStrength = Mathf.Max(100f, tailDamperStrength);
            maximumTailwheelSteerAngle = Mathf.Clamp(maximumTailwheelSteerAngle, 0f, 35f);
            steeringFadeSpeedMetersPerSecond = Mathf.Max(1f, steeringFadeSpeedMetersPerSecond);
            rollingResistanceCoefficient = Mathf.Clamp(rollingResistanceCoefficient, 0f, 0.1f);
        }

        private struct WheelContact
        {
            public bool Grounded;
            public Vector3 CenterPosition;
            public Vector3 SurfaceNormal;
            public float ForwardSpeed;
        }
    }
}
