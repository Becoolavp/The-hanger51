using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(80)]
    [DisallowMultipleComponent]
    public sealed class P51WheelLandingGear : MonoBehaviour
    {
        [Header("Aircraft References")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private Rigidbody aircraftBody;

        [Header("Wheel Colliders")]
        [SerializeField] private WheelCollider leftMainWheel;
        [SerializeField] private WheelCollider rightMainWheel;
        [SerializeField] private WheelCollider tailWheel;

        [Header("Wheel Visuals")]
        [SerializeField] private Transform leftMainWheelVisual;
        [SerializeField] private Transform rightMainWheelVisual;
        [SerializeField] private Transform tailWheelVisual;

        [Header("Handling")]
        [SerializeField, Range(0f, 35f)] private float maximumTailwheelSteerAngle = 18f;
        [SerializeField, Min(1f)] private float steeringFadeSpeedMetersPerSecond = 24f;
        [SerializeField, Min(0f)] private float mainWheelBrakeTorque = 6500f;
        [SerializeField, Min(0f)] private float tailWheelBrakeTorque = 1800f;
        [SerializeField] private Vector3 tunedCenterOfMass = new Vector3(0f, 0.96f, -0.72f);

        private Quaternion leftVisualRotationOffset = Quaternion.identity;
        private Quaternion rightVisualRotationOffset = Quaternion.identity;
        private Quaternion tailVisualRotationOffset = Quaternion.identity;
        private bool visualOffsetsInitialized;

        public int ConfiguredWheelCount
        {
            get
            {
                int count = 0;
                if (leftMainWheel != null) count++;
                if (rightMainWheel != null) count++;
                if (tailWheel != null) count++;
                return count;
            }
        }

        public bool HasAllWheelVisuals => leftMainWheelVisual != null
            && rightMainWheelVisual != null
            && tailWheelVisual != null;
        public bool AnyWheelGrounded =>
            (leftMainWheel != null && leftMainWheel.isGrounded)
            || (rightMainWheel != null && rightMainWheel.isGrounded)
            || (tailWheel != null && tailWheel.isGrounded);
        public WheelCollider LeftMainWheel => leftMainWheel;
        public WheelCollider RightMainWheel => rightMainWheel;
        public WheelCollider TailWheel => tailWheel;

        private void Awake()
        {
            ResolveReferences();
            ApplyBodyTuning();
            InitializeVisualOffsets();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyBodyTuning();
            InitializeVisualOffsets();
        }

        private void Start()
        {
            // Run once after the flight controller's Awake so this component owns
            // the final taildragger center-of-mass adjustment.
            ApplyBodyTuning();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            ApplyBodyTuning();

            bool piloted = flightController != null && flightController.PilotPresent;
            Keyboard keyboard = Keyboard.current;
            float steeringInput = 0f;
            bool braking = false;

            if (piloted && keyboard != null)
            {
                if (keyboard.aKey.isPressed) steeringInput -= 1f;
                if (keyboard.dKey.isPressed) steeringInput += 1f;
                braking = keyboard.spaceKey.isPressed;
            }

            float speed = flightController != null
                ? flightController.GroundSpeedMetersPerSecond
                : aircraftBody != null
                    ? Vector3.ProjectOnPlane(aircraftBody.linearVelocity, Vector3.up).magnitude
                    : 0f;
            float steeringFade = 1f - Mathf.InverseLerp(
                steeringFadeSpeedMetersPerSecond * 0.45f,
                steeringFadeSpeedMetersPerSecond,
                speed);

            if (tailWheel != null)
            {
                tailWheel.steerAngle = steeringInput
                    * maximumTailwheelSteerAngle
                    * steeringFade;
                tailWheel.brakeTorque = braking ? tailWheelBrakeTorque : 0f;
                tailWheel.motorTorque = 0f;
            }

            float mainBrake = braking ? mainWheelBrakeTorque : 0f;
            if (leftMainWheel != null)
            {
                leftMainWheel.brakeTorque = mainBrake;
                leftMainWheel.motorTorque = 0f;
            }
            if (rightMainWheel != null)
            {
                rightMainWheel.brakeTorque = mainBrake;
                rightMainWheel.motorTorque = 0f;
            }
        }

        private void LateUpdate()
        {
            InitializeVisualOffsets();
            SyncWheelVisual(leftMainWheel, leftMainWheelVisual, leftVisualRotationOffset);
            SyncWheelVisual(rightMainWheel, rightMainWheelVisual, rightVisualRotationOffset);
            SyncWheelVisual(tailWheel, tailWheelVisual, tailVisualRotationOffset);
        }

        public void Configure(
            P51FlightController configuredFlightController,
            Rigidbody configuredAircraftBody,
            WheelCollider configuredLeftMainWheel,
            WheelCollider configuredRightMainWheel,
            WheelCollider configuredTailWheel,
            Transform configuredLeftMainVisual,
            Transform configuredRightMainVisual,
            Transform configuredTailVisual)
        {
            flightController = configuredFlightController;
            aircraftBody = configuredAircraftBody;
            leftMainWheel = configuredLeftMainWheel;
            rightMainWheel = configuredRightMainWheel;
            tailWheel = configuredTailWheel;
            leftMainWheelVisual = configuredLeftMainVisual;
            rightMainWheelVisual = configuredRightMainVisual;
            tailWheelVisual = configuredTailVisual;
            visualOffsetsInitialized = false;
            ApplyBodyTuning();
            InitializeVisualOffsets();
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
            if (aircraftBody != null)
            {
                aircraftBody.centerOfMass = tunedCenterOfMass;
            }
        }

        private void InitializeVisualOffsets()
        {
            if (visualOffsetsInitialized)
            {
                return;
            }

            leftVisualRotationOffset = CalculateVisualOffset(leftMainWheel, leftMainWheelVisual);
            rightVisualRotationOffset = CalculateVisualOffset(rightMainWheel, rightMainWheelVisual);
            tailVisualRotationOffset = CalculateVisualOffset(tailWheel, tailWheelVisual);
            visualOffsetsInitialized = true;
        }

        private static Quaternion CalculateVisualOffset(
            WheelCollider wheelCollider,
            Transform visual)
        {
            return wheelCollider != null && visual != null
                ? Quaternion.Inverse(wheelCollider.transform.rotation) * visual.rotation
                : Quaternion.identity;
        }

        private static void SyncWheelVisual(
            WheelCollider wheelCollider,
            Transform visual,
            Quaternion rotationOffset)
        {
            if (wheelCollider == null || visual == null)
            {
                return;
            }

            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visual.SetPositionAndRotation(position, rotation * rotationOffset);
        }

        private void OnValidate()
        {
            maximumTailwheelSteerAngle = Mathf.Clamp(maximumTailwheelSteerAngle, 0f, 35f);
            steeringFadeSpeedMetersPerSecond = Mathf.Max(1f, steeringFadeSpeedMetersPerSecond);
            mainWheelBrakeTorque = Mathf.Max(0f, mainWheelBrakeTorque);
            tailWheelBrakeTorque = Mathf.Max(0f, tailWheelBrakeTorque);
        }
    }
}
