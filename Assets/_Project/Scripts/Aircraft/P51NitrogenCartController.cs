using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51NitrogenCartController : MonoBehaviour
    {
        [Header("Nitrogen Service")]
        [SerializeField, Range(0f, 80f)] private float regulatorPsi = 20f;
        [SerializeField, Min(1f)] private float regulatorChangePerSecond = 8f;
        [SerializeField, Min(1f)] private float maximumHoseDistance = 9f;
        [SerializeField] private Transform hoseOrigin;
        [SerializeField] private LineRenderer hoseLine;

        [Header("Movable Cart")]
        [SerializeField] private Rigidbody cartBody;
        [SerializeField] private Transform handlePoint;
        [SerializeField] private Transform[] rollingWheels = new Transform[3];
        [SerializeField, Min(0.5f)] private float pushDistance = 1.35f;
        [SerializeField, Min(0.5f)] private float maximumPushSpeed = 4.5f;
        [SerializeField, Min(30f)] private float maximumTurnRateDegrees = 220f;
        [SerializeField, Min(0.05f)] private float wheelRadius = 0.21f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private P51LandingGearMaintenanceController connectedController;
        private int connectedWheelIndex = -1;
        private P51LooseWheelAssembly connectedLooseWheel;
        private Transform mover;
        private bool beingMoved;
        private Vector3 lastCartPosition;

        public float RegulatorPsi => regulatorPsi;
        public bool IsConnected =>
            (connectedController != null && connectedWheelIndex >= 0)
            || connectedLooseWheel != null;
        public bool IsBeingMoved => beingMoved && mover != null;
        public string InteractionText
        {
            get
            {
                if (IsBeingMoved)
                {
                    return $"E: release nitrogen cart | Regulator {regulatorPsi:F0} PSI";
                }
                if (IsConnected)
                {
                    return $"Nitrogen cart: {regulatorPsi:F0} PSI setpoint | Q/Z adjust | Hold F service | N disconnect | Disconnect before moving";
                }
                return $"E: grab and wheel cart | {regulatorPsi:F0} PSI setpoint | Q/Z adjust | Aim at a mounted tire + N to connect";
            }
        }

        private void Awake()
        {
            ResolveMovementReferences();
            lastCartPosition = transform.position;
            UpdateHoseVisual();
        }

        private void FixedUpdate()
        {
            if (IsBeingMoved)
            {
                MoveTowardPlayerHandle();
            }
        }

        private void LateUpdate()
        {
            if (IsConnected)
            {
                Transform valve = GetConnectedValveTarget();
                if (valve == null
                    || !ConnectionStillValid()
                    || Vector3.Distance(transform.position, valve.position) > maximumHoseDistance)
                {
                    Disconnect();
                }
            }

            UpdateWheelVisuals();
            UpdateHoseVisual();
        }

        public void Configure(
            Transform configuredHoseOrigin,
            LineRenderer configuredHoseLine,
            float configuredMaximumHoseDistance)
        {
            hoseOrigin = configuredHoseOrigin;
            hoseLine = configuredHoseLine;
            maximumHoseDistance = Mathf.Max(1f, configuredMaximumHoseDistance);
            regulatorPsi = 20f;
            ResolveMovementReferences();
            Disconnect();
        }

        public void ConfigureMovement(
            Rigidbody configuredBody,
            Transform configuredHandlePoint,
            Transform[] configuredRollingWheels,
            float configuredPushDistance,
            float configuredMaximumPushSpeed)
        {
            cartBody = configuredBody;
            handlePoint = configuredHandlePoint;
            rollingWheels = CopyWheelArray(configuredRollingWheels);
            pushDistance = Mathf.Max(0.5f, configuredPushDistance);
            maximumPushSpeed = Mathf.Max(0.5f, configuredMaximumPushSpeed);
            ResolveMovementReferences();
            PrepareBody();
            lastCartPosition = transform.position;
        }

        public bool IsConnectedTo(
            P51LandingGearMaintenanceController controller,
            int wheelIndex)
        {
            return connectedLooseWheel == null
                && connectedController == controller
                && connectedWheelIndex == wheelIndex;
        }

        public bool IsConnectedTo(P51LooseWheelAssembly looseWheel)
        {
            return connectedController == null
                && connectedLooseWheel != null
                && connectedLooseWheel == looseWheel;
        }

        public bool TryToggleMove(Transform playerMover, out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveMovementReferences();

            if (IsBeingMoved)
            {
                StopMoving();
                resultMessage = "Released the nitrogen cart handle.";
                return true;
            }

            if (IsConnected)
            {
                resultMessage = "Disconnect the nitrogen hose before moving the cart.";
                return false;
            }
            if (playerMover == null)
            {
                resultMessage = "The Player movement reference is missing.";
                return false;
            }
            if (cartBody == null)
            {
                resultMessage = "The nitrogen cart movement body is missing. Run P-51 Step 30.";
                return false;
            }

            mover = playerMover;
            beingMoved = true;
            PrepareBody();
            resultMessage = "Grabbed the nitrogen cart. Walk to wheel it around; press E again to release it.";
            return true;
        }

        public void StopMoving()
        {
            beingMoved = false;
            mover = null;
            if (cartBody != null)
            {
                cartBody.linearVelocity = Vector3.zero;
                cartBody.angularVelocity = Vector3.zero;
            }
        }

        public void AdjustRegulator(float direction, float deltaTime)
        {
            regulatorPsi = Mathf.Clamp(
                regulatorPsi
                + Mathf.Clamp(direction, -1f, 1f)
                * regulatorChangePerSecond
                * Mathf.Max(0f, deltaTime),
                0f,
                80f);
        }

        public bool TryConnect(
            P51LandingGearMaintenanceController controller,
            int wheelIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (IsBeingMoved)
            {
                resultMessage = "Release the nitrogen cart handle before connecting the hose.";
                return false;
            }
            if (IsConnected)
            {
                resultMessage = "The nitrogen hose is already connected. Press N at the connected tire to disconnect first.";
                return false;
            }
            if (controller == null)
            {
                resultMessage = "No tire service target was selected.";
                return false;
            }
            if (!controller.IsGearInstalled(wheelIndex)
                || !controller.IsTireInstalled(wheelIndex))
            {
                resultMessage = "Install the landing gear, rim, and tire before connecting nitrogen.";
                return false;
            }
            if (controller.IsTireFailed(wheelIndex))
            {
                resultMessage = "That tire is destroyed and must be replaced before pressure service.";
                return false;
            }

            Transform valve = controller.GetValveTarget(wheelIndex);
            if (valve == null)
            {
                resultMessage = "That tire valve is not configured.";
                return false;
            }
            float distance = Vector3.Distance(transform.position, valve.position);
            if (distance > maximumHoseDistance)
            {
                resultMessage = $"The nitrogen cart is {distance:F1} m from the valve. Wheel it within {maximumHoseDistance:F0} m before connecting.";
                return false;
            }

            connectedController = controller;
            connectedWheelIndex = wheelIndex;
            connectedLooseWheel = null;
            UpdateHoseVisual();
            float correctPressure = controller.GetProperPressure(wheelIndex);
            resultMessage = $"Connected nitrogen hose to the {controller.GetWheelName(wheelIndex)} tire. Setpoint {regulatorPsi:F0} PSI; correct pressure {correctPressure:F0} PSI. Keep looking at the tire or the cart: Q/Z adjusts pressure and Hold F services.";
            return true;
        }

        public bool TryConnect(
            P51LooseWheelAssembly looseWheel,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (IsBeingMoved)
            {
                resultMessage = "Release the nitrogen cart handle before connecting the hose.";
                return false;
            }
            if (IsConnected)
            {
                resultMessage = "The nitrogen hose is already connected. Press N at the connected tire to disconnect first.";
                return false;
            }
            if (looseWheel == null || !looseWheel.IsComplete)
            {
                resultMessage = "The tire must be mounted on its rim before connecting nitrogen.";
                return false;
            }
            if (looseWheel.IsTireFailed)
            {
                resultMessage = "That tire is destroyed and must be replaced before pressure service.";
                return false;
            }

            Transform valve = looseWheel.ServiceValveTarget;
            if (valve == null)
            {
                resultMessage = "The loose wheel tire valve is not available.";
                return false;
            }
            float distance = Vector3.Distance(transform.position, valve.position);
            if (distance > maximumHoseDistance)
            {
                resultMessage = $"The nitrogen cart is {distance:F1} m from the loose wheel. Wheel it within {maximumHoseDistance:F0} m before connecting.";
                return false;
            }

            connectedController = null;
            connectedWheelIndex = -1;
            connectedLooseWheel = looseWheel;
            UpdateHoseVisual();
            resultMessage = $"Connected nitrogen hose to the loose {looseWheel.WheelLabel} tire. Setpoint {regulatorPsi:F0} PSI; correct pressure {looseWheel.ProperPressurePsi:F0} PSI. Q/Z adjusts and Hold F services while looking at the wheel or cart.";
            return true;
        }

        public void Disconnect()
        {
            connectedController = null;
            connectedWheelIndex = -1;
            connectedLooseWheel = null;
            UpdateHoseVisual();
        }

        public bool ServiceConnectedTire(float deltaTime, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsConnected)
            {
                resultMessage = "Connect the nitrogen hose to a tire first.";
                return false;
            }

            bool serviced;
            if (connectedLooseWheel != null)
            {
                serviced = connectedLooseWheel.ServicePressureToward(
                    regulatorPsi,
                    deltaTime,
                    out resultMessage);
                if (connectedLooseWheel == null || connectedLooseWheel.IsTireFailed)
                {
                    Disconnect();
                }
                return serviced;
            }

            serviced = connectedController.ServicePressureToward(
                connectedWheelIndex,
                regulatorPsi,
                deltaTime,
                out resultMessage);
            if (connectedController == null
                || connectedController.IsTireFailed(connectedWheelIndex))
            {
                Disconnect();
            }
            return serviced;
        }

        private bool ConnectionStillValid()
        {
            if (connectedLooseWheel != null)
            {
                return connectedLooseWheel.IsComplete && !connectedLooseWheel.IsTireFailed;
            }

            return connectedController != null
                && connectedWheelIndex >= 0
                && connectedController.IsGearInstalled(connectedWheelIndex)
                && connectedController.IsTireInstalled(connectedWheelIndex)
                && !connectedController.IsTireFailed(connectedWheelIndex);
        }

        private Transform GetConnectedValveTarget()
        {
            if (connectedLooseWheel != null)
            {
                return connectedLooseWheel.ServiceValveTarget;
            }

            return connectedController != null && connectedWheelIndex >= 0
                ? connectedController.GetValveTarget(connectedWheelIndex)
                : null;
        }

        private void MoveTowardPlayerHandle()
        {
            if (mover == null || cartBody == null)
            {
                StopMoving();
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(mover.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 target = mover.position + forward * pushDistance;
            target.y = FindGroundHeight(target, cartBody.position.y);
            Vector3 nextPosition = Vector3.MoveTowards(
                cartBody.position,
                target,
                maximumPushSpeed * Time.fixedDeltaTime);
            Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                cartBody.rotation,
                targetRotation,
                maximumTurnRateDegrees * Time.fixedDeltaTime);

            cartBody.MovePosition(nextPosition);
            cartBody.MoveRotation(nextRotation);
        }

        private float FindGroundHeight(Vector3 target, float fallbackY)
        {
            Vector3 origin = target + Vector3.up * 2.5f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                6f,
                groundLayers,
                QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null
                    || collider.transform == transform
                    || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hits[index].normal.y < 0.45f)
                {
                    continue;
                }

                bestY = Mathf.Max(bestY, hits[index].point.y + 0.02f);
            }

            return float.IsNegativeInfinity(bestY) ? fallbackY : bestY;
        }

        private void UpdateWheelVisuals()
        {
            Vector3 current = transform.position;
            float distance = Vector3.ProjectOnPlane(
                current - lastCartPosition,
                Vector3.up).magnitude;
            if (distance > 0.0001f && rollingWheels != null)
            {
                float degrees = distance / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg;
                for (int index = 0; index < rollingWheels.Length; index++)
                {
                    Transform wheel = rollingWheels[index];
                    if (wheel != null)
                    {
                        wheel.Rotate(Vector3.up, degrees, Space.Self);
                    }
                }
            }
            lastCartPosition = current;
        }

        private void UpdateHoseVisual()
        {
            if (hoseLine == null)
            {
                return;
            }

            hoseLine.enabled = IsConnected;
            if (!IsConnected)
            {
                return;
            }

            Transform valve = GetConnectedValveTarget();
            if (valve == null)
            {
                hoseLine.enabled = false;
                return;
            }

            Vector3 start = hoseOrigin != null ? hoseOrigin.position : transform.position;
            Vector3 end = valve.position;
            Vector3 middle = Vector3.Lerp(start, end, 0.5f) + Vector3.down * 0.35f;
            hoseLine.positionCount = 3;
            hoseLine.SetPosition(0, start);
            hoseLine.SetPosition(1, middle);
            hoseLine.SetPosition(2, end);
        }

        private void ResolveMovementReferences()
        {
            if (cartBody == null)
            {
                cartBody = GetComponent<Rigidbody>();
            }
        }

        private void PrepareBody()
        {
            if (cartBody == null)
            {
                return;
            }

            cartBody.isKinematic = true;
            cartBody.useGravity = false;
            cartBody.interpolation = RigidbodyInterpolation.Interpolate;
            cartBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private static Transform[] CopyWheelArray(Transform[] source)
        {
            Transform[] result = new Transform[3];
            if (source != null)
            {
                System.Array.Copy(
                    source,
                    result,
                    Mathf.Min(source.Length, result.Length));
            }
            return result;
        }

        private void OnDisable()
        {
            StopMoving();
            Disconnect();
        }

        private void OnValidate()
        {
            regulatorPsi = Mathf.Clamp(regulatorPsi, 0f, 80f);
            regulatorChangePerSecond = Mathf.Max(1f, regulatorChangePerSecond);
            maximumHoseDistance = Mathf.Max(1f, maximumHoseDistance);
            pushDistance = Mathf.Max(0.5f, pushDistance);
            maximumPushSpeed = Mathf.Max(0.5f, maximumPushSpeed);
            maximumTurnRateDegrees = Mathf.Max(30f, maximumTurnRateDegrees);
            wheelRadius = Mathf.Max(0.05f, wheelRadius);
            ResolveMovementReferences();
        }
    }
}
