using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51FlightController : MonoBehaviour
    {
        [Header("Required References")]
        [SerializeField] private AircraftEngineMountReceiver engineReceiver;
        [SerializeField] private Transform propellerRoot;
        [SerializeField] private Transform[] landingGearContactPoints = new Transform[3];

        [Header("Aircraft Mass and Power")]
        [SerializeField, Min(1000f)] private float aircraftMassKg = 4200f;
        [SerializeField, Min(1000f)] private float maximumThrustNewtons = 18500f;
        [SerializeField, Range(0.05f, 1f)] private float throttleChangePerSecond = 0.32f;
        [SerializeField, Min(0f)] private float idlePropellerRpm = 700f;
        [SerializeField, Min(100f)] private float maximumPropellerRpm = 2800f;

        [Header("Aerodynamics")]
        [SerializeField, Min(1f)] private float wingAreaSquareMeters = 21.64f;
        [SerializeField, Min(0.1f)] private float airDensity = 1.225f;
        [SerializeField] private float zeroAngleLiftCoefficient = 0.34f;
        [SerializeField, Min(0.1f)] private float liftSlopePerRadian = 5.1f;
        [SerializeField, Min(0.1f)] private float maximumLiftCoefficient = 1.45f;
        [SerializeField, Min(0.001f)] private float parasiteDragCoefficient = 0.030f;
        [SerializeField, Min(0f)] private float inducedDragFactor = 0.050f;
        [SerializeField, Min(1f)] private float fullStallSpeedMetersPerSecond = 23f;
        [SerializeField, Min(1f)] private float liftRecoverySpeedMetersPerSecond = 43f;
        [SerializeField, Min(0f)] private float sideAreaSquareMeters = 5.6f;
        [SerializeField, Min(0f)] private float sideDragCoefficient = 0.82f;

        [Header("Control Authority")]
        [SerializeField, Min(100f)] private float pitchTorque = 46000f;
        [SerializeField, Min(100f)] private float rollTorque = 68000f;
        [SerializeField, Min(100f)] private float yawStabilityTorque = 26000f;
        [SerializeField, Min(0f)] private float pitchDamping = 19000f;
        [SerializeField, Min(0f)] private float rollDamping = 16000f;
        [SerializeField, Min(0f)] private float yawDamping = 22000f;
        [SerializeField, Min(1f)] private float fullControlSpeedMetersPerSecond = 42f;

        [Header("Ground Handling")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.05f)] private float groundProbeDistance = 0.32f;
        [SerializeField, Min(0f)] private float groundSteeringTorque = 12500f;
        [SerializeField, Min(0f)] private float groundLateralGrip = 4500f;
        [SerializeField, Min(0f)] private float rollingResistance = 80f;
        [SerializeField, Min(0f)] private float wheelBrakeStrength = 8800f;

        private readonly RaycastHit[] groundHits = new RaycastHit[12];

        private Rigidbody aircraftBody;
        private float throttle;
        private float pitchInput;
        private float rollInput;
        private bool wheelBrakesApplied;
        private bool engineRunning;
        private bool pilotPresent;
        private bool grounded;
        private float propellerAngle;
        private string cockpitMessage = string.Empty;
        private float cockpitMessageClearTime;
        private GUIStyle hudStyle;
        private GUIStyle messageStyle;

        public AircraftEngineMountReceiver EngineReceiver => engineReceiver;
        public Transform PropellerRoot => propellerRoot;
        public Rigidbody AircraftBody => aircraftBody;
        public bool PilotPresent => pilotPresent;
        public bool EngineRunning => engineRunning;
        public bool IsGrounded => grounded;
        public bool EngineInstalled => engineReceiver != null
            && engineReceiver.EnginePositioned
            && engineReceiver.AllMountBoltsTightened
            && engineReceiver.InstalledTransport != null;
        public float Throttle => throttle;
        public float AirspeedMetersPerSecond => aircraftBody != null
            ? aircraftBody.linearVelocity.magnitude
            : 0f;
        public float AirspeedKnots => AirspeedMetersPerSecond * 1.9438445f;
        public float GroundSpeedMetersPerSecond
        {
            get
            {
                if (aircraftBody == null)
                {
                    return 0f;
                }

                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                    aircraftBody.linearVelocity,
                    Vector3.up);
                return horizontalVelocity.magnitude;
            }
        }

        private void Awake()
        {
            aircraftBody = GetComponent<Rigidbody>();
            ResolveReferences();
            ConfigureRigidbody();
            SetPilotPresent(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!pilotPresent)
            {
                pitchInput = 0f;
                rollInput = 0f;
                wheelBrakesApplied = false;
                UpdatePropellerVisual();
                return;
            }

            ReadPilotInput();

            if (engineRunning && !EngineInstalled)
            {
                engineRunning = false;
                throttle = 0f;
                ShowCockpitMessage("Engine stopped: the Merlin is no longer fully mounted.", 3f);
            }

            UpdatePropellerVisual();
        }

        private void FixedUpdate()
        {
            if (aircraftBody == null || aircraftBody.isKinematic)
            {
                grounded = CheckGrounded();
                return;
            }

            grounded = CheckGrounded();
            ApplyPropellerThrust();
            ApplyAerodynamicForces();
            ApplyFlightControls();
            ApplyGroundHandling();
        }

        public void Configure(
            AircraftEngineMountReceiver configuredReceiver,
            Transform configuredPropellerRoot,
            Transform[] configuredLandingGearContactPoints)
        {
            engineReceiver = configuredReceiver;
            propellerRoot = configuredPropellerRoot;
            landingGearContactPoints = configuredLandingGearContactPoints ?? new Transform[0];
            ResolveReferences();

            if (aircraftBody == null)
            {
                aircraftBody = GetComponent<Rigidbody>();
            }

            ConfigureRigidbody();
        }

        public void SetPilotPresent(bool isPresent)
        {
            pilotPresent = isPresent;

            if (aircraftBody == null)
            {
                aircraftBody = GetComponent<Rigidbody>();
            }

            if (aircraftBody == null)
            {
                return;
            }

            if (pilotPresent)
            {
                aircraftBody.isKinematic = false;
                aircraftBody.useGravity = true;
                aircraftBody.WakeUp();
                ShowCockpitMessage(
                    EngineInstalled
                        ? "Merlin installed. Press T to start."
                        : "Install and tighten the Merlin before starting.",
                    3.5f);
            }
            else
            {
                engineRunning = false;
                throttle = 0f;
                pitchInput = 0f;
                rollInput = 0f;
                wheelBrakesApplied = true;
                aircraftBody.linearVelocity = Vector3.zero;
                aircraftBody.angularVelocity = Vector3.zero;
                aircraftBody.isKinematic = true;
                cockpitMessage = string.Empty;
            }
        }

        public bool CanExitCockpit(out string reason)
        {
            reason = string.Empty;
            if (!pilotPresent)
            {
                return true;
            }

            if (!grounded)
            {
                reason = "Land the aircraft before leaving the cockpit.";
                return false;
            }

            if (GroundSpeedMetersPerSecond > 3.5f)
            {
                reason = "Slow below walking speed before leaving the cockpit.";
                return false;
            }

            return true;
        }

        public void ShowCockpitMessage(string message, float duration = 2.5f)
        {
            cockpitMessage = message ?? string.Empty;
            cockpitMessageClearTime = Time.unscaledTime + Mathf.Max(0.25f, duration);
        }

        private void ResolveReferences()
        {
            if (engineReceiver == null)
            {
                engineReceiver = GetComponent<AircraftEngineMountReceiver>();
            }

            if (propellerRoot == null)
            {
                Transform[] transforms = GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index] != null
                        && transforms[index].name == "Four-Blade Hamilton Standard Propeller")
                    {
                        propellerRoot = transforms[index];
                        break;
                    }
                }
            }
        }

        private void ConfigureRigidbody()
        {
            if (aircraftBody == null)
            {
                return;
            }

            aircraftBody.mass = Mathf.Max(1000f, aircraftMassKg);
            aircraftBody.useGravity = true;
            aircraftBody.interpolation = RigidbodyInterpolation.Interpolate;
            aircraftBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            aircraftBody.maxAngularVelocity = 3.5f;
            aircraftBody.centerOfMass = new Vector3(0f, 1.24f, -0.32f);
            aircraftBody.linearDamping = 0.015f;
            aircraftBody.angularDamping = 0.06f;
        }

        private void ReadPilotInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                pitchInput = 0f;
                rollInput = 0f;
                wheelBrakesApplied = false;
                return;
            }

            float throttleDirection = 0f;
            if (keyboard.qKey.isPressed) throttleDirection += 1f;
            if (keyboard.zKey.isPressed) throttleDirection -= 1f;
            throttle = Mathf.Clamp01(
                throttle + throttleDirection * throttleChangePerSecond * Time.deltaTime);

            pitchInput = 0f;
            if (keyboard.wKey.isPressed) pitchInput += 1f;
            if (keyboard.sKey.isPressed) pitchInput -= 1f;

            rollInput = 0f;
            if (keyboard.aKey.isPressed) rollInput -= 1f;
            if (keyboard.dKey.isPressed) rollInput += 1f;

            wheelBrakesApplied = keyboard.spaceKey.isPressed;

            if (keyboard.tKey.wasPressedThisFrame)
            {
                ToggleEngine();
            }
        }

        private void ToggleEngine()
        {
            if (engineRunning)
            {
                engineRunning = false;
                throttle = 0f;
                ShowCockpitMessage("Merlin stopped.");
                return;
            }

            if (!EngineInstalled)
            {
                ShowCockpitMessage(
                    "Start blocked: lower the Merlin into the bay and tighten all four mount bolts.",
                    4f);
                return;
            }

            engineRunning = true;
            ShowCockpitMessage("Merlin started. Q increases throttle; Z decreases throttle.", 3.5f);
        }

        private void ApplyPropellerThrust()
        {
            if (!engineRunning || throttle <= 0f)
            {
                return;
            }

            float speedFactor = Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0f, 160f, AirspeedMetersPerSecond));
            aircraftBody.AddForce(
                transform.forward * maximumThrustNewtons * throttle * speedFactor,
                ForceMode.Force);
        }

        private void ApplyAerodynamicForces()
        {
            Vector3 velocity = aircraftBody.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 0.5f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float forwardSpeed = Mathf.Max(0f, localVelocity.z);
            float dynamicPressure = 0.5f * airDensity * forwardSpeed * forwardSpeed;

            float angleOfAttack = Mathf.Atan2(
                -localVelocity.y,
                Mathf.Max(1f, Mathf.Abs(localVelocity.z)));
            float liftCoefficient = Mathf.Clamp(
                zeroAngleLiftCoefficient + angleOfAttack * liftSlopePerRadian,
                -0.75f,
                maximumLiftCoefficient);

            float stallFactor = Mathf.InverseLerp(
                fullStallSpeedMetersPerSecond,
                liftRecoverySpeedMetersPerSecond,
                forwardSpeed);
            liftCoefficient *= Mathf.Lerp(0.26f, 1f, stallFactor);

            Vector3 liftDirection = Vector3.ProjectOnPlane(transform.up, velocity.normalized);
            if (liftDirection.sqrMagnitude < 0.001f)
            {
                liftDirection = transform.up;
            }
            else
            {
                liftDirection.Normalize();
            }

            float liftForce = dynamicPressure * wingAreaSquareMeters * liftCoefficient;
            aircraftBody.AddForce(liftDirection * liftForce, ForceMode.Force);

            float dragCoefficient = parasiteDragCoefficient
                + inducedDragFactor * liftCoefficient * liftCoefficient;
            float dragForce = 0.5f * airDensity * speed * speed
                * wingAreaSquareMeters * dragCoefficient;
            aircraftBody.AddForce(-velocity.normalized * dragForce, ForceMode.Force);

            float sideSpeed = localVelocity.x;
            if (Mathf.Abs(sideSpeed) > 0.05f)
            {
                float sideForce = 0.5f * airDensity * sideSpeed * Mathf.Abs(sideSpeed)
                    * sideAreaSquareMeters * sideDragCoefficient;
                aircraftBody.AddForce(-transform.right * sideForce, ForceMode.Force);
            }
        }

        private void ApplyFlightControls()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(aircraftBody.linearVelocity);
            float forwardSpeed = Mathf.Max(0f, localVelocity.z);
            float authority = Mathf.Lerp(
                0.08f,
                1f,
                Mathf.InverseLerp(8f, fullControlSpeedMetersPerSecond, forwardSpeed));

            float sideslipAngle = Mathf.Atan2(
                localVelocity.x,
                Mathf.Max(2f, Mathf.Abs(localVelocity.z)));

            Vector3 controlTorque = new Vector3(
                pitchInput * pitchTorque,
                sideslipAngle * yawStabilityTorque,
                rollInput * rollTorque) * authority;
            aircraftBody.AddRelativeTorque(controlTorque, ForceMode.Force);

            Vector3 localAngularVelocity = transform.InverseTransformDirection(
                aircraftBody.angularVelocity);
            Vector3 dampingTorque = new Vector3(
                -localAngularVelocity.x * pitchDamping,
                -localAngularVelocity.y * yawDamping,
                -localAngularVelocity.z * rollDamping);
            aircraftBody.AddRelativeTorque(dampingTorque, ForceMode.Force);
        }

        private void ApplyGroundHandling()
        {
            if (!grounded)
            {
                return;
            }

            Vector3 velocity = aircraftBody.linearVelocity;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float forwardGroundSpeed = Vector3.Dot(horizontalVelocity, transform.forward);
            float sideGroundSpeed = Vector3.Dot(horizontalVelocity, transform.right);

            aircraftBody.AddForce(
                -horizontalVelocity * rollingResistance,
                ForceMode.Force);
            aircraftBody.AddForce(
                -transform.right * sideGroundSpeed * groundLateralGrip,
                ForceMode.Force);

            float steeringAuthority = Mathf.InverseLerp(0f, 22f, Mathf.Abs(forwardGroundSpeed));
            aircraftBody.AddTorque(
                Vector3.up * rollInput * groundSteeringTorque * steeringAuthority,
                ForceMode.Force);

            if (wheelBrakesApplied)
            {
                aircraftBody.AddForce(
                    -horizontalVelocity * wheelBrakeStrength,
                    ForceMode.Force);
            }
        }

        private bool CheckGrounded()
        {
            if (landingGearContactPoints == null || landingGearContactPoints.Length == 0)
            {
                return false;
            }

            for (int pointIndex = 0; pointIndex < landingGearContactPoints.Length; pointIndex++)
            {
                Transform point = landingGearContactPoints[pointIndex];
                if (point == null)
                {
                    continue;
                }

                int hitCount = Physics.RaycastNonAlloc(
                    point.position + Vector3.up * 0.08f,
                    Vector3.down,
                    groundHits,
                    groundProbeDistance,
                    groundLayers,
                    QueryTriggerInteraction.Ignore);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    Collider collider = groundHits[hitIndex].collider;
                    if (collider == null || collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private void UpdatePropellerVisual()
        {
            if (propellerRoot == null)
            {
                return;
            }

            float rpm = engineRunning
                ? Mathf.Lerp(idlePropellerRpm, maximumPropellerRpm, throttle)
                : 0f;

            propellerAngle = Mathf.Repeat(
                propellerAngle + rpm * 6f * Time.deltaTime,
                360f);
            propellerRoot.localRotation = Quaternion.Euler(0f, 0f, propellerAngle);
        }

        private void OnGUI()
        {
            if (!pilotPresent)
            {
                return;
            }

            EnsureHudStyles();

            string engineState = engineRunning
                ? "RUNNING"
                : EngineInstalled ? "READY" : "NO INSTALLED ENGINE";
            string flightState = grounded ? "GROUND" : "AIRBORNE";
            string hud =
                $"P-51D MUSTANG\n"
                + $"Engine: {engineState}\n"
                + $"Throttle: {Mathf.RoundToInt(throttle * 100f)}%\n"
                + $"Airspeed: {AirspeedKnots:F0} kt\n"
                + $"Altitude: {transform.position.y * 3.28084f:F0} ft\n"
                + $"State: {flightState}\n\n"
                + "T Start/Stop | Q Throttle + | Z Throttle -\n"
                + "W Pitch Down | S Pitch Up | A/D Roll\n"
                + "Space Wheel Brakes | E Exit when stopped";

            GUI.Box(new Rect(18f, 18f, 355f, 205f), hud, hudStyle);

            if (!string.IsNullOrWhiteSpace(cockpitMessage)
                && Time.unscaledTime <= cockpitMessageClearTime)
            {
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 260f, Screen.height - 92f, 520f, 52f),
                    cockpitMessage,
                    messageStyle);
            }
            else if (Time.unscaledTime > cockpitMessageClearTime)
            {
                cockpitMessage = string.Empty;
            }
        }

        private void EnsureHudStyles()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 16,
                    padding = new RectOffset(14, 12, 12, 10),
                    normal = { textColor = Color.white }
                };
            }

            if (messageStyle == null)
            {
                messageStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }
        }

        private void OnValidate()
        {
            aircraftMassKg = Mathf.Max(1000f, aircraftMassKg);
            maximumThrustNewtons = Mathf.Max(1000f, maximumThrustNewtons);
            maximumPropellerRpm = Mathf.Max(maximumPropellerRpm, idlePropellerRpm + 100f);
            wingAreaSquareMeters = Mathf.Max(1f, wingAreaSquareMeters);
            airDensity = Mathf.Max(0.1f, airDensity);
            maximumLiftCoefficient = Mathf.Max(0.1f, maximumLiftCoefficient);
            fullStallSpeedMetersPerSecond = Mathf.Max(1f, fullStallSpeedMetersPerSecond);
            liftRecoverySpeedMetersPerSecond = Mathf.Max(
                fullStallSpeedMetersPerSecond + 1f,
                liftRecoverySpeedMetersPerSecond);
            groundProbeDistance = Mathf.Max(0.05f, groundProbeDistance);
        }
    }
}
