using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51TowBarController : MonoBehaviour
    {
        [Header("Aircraft")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51RaycastLandingGear landingGear;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField] private Transform tailwheelAttachPoint;

        [Header("Tow Bar")]
        [SerializeField] private Transform towHead;
        [SerializeField] private Transform handleGrip;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private Transform leftClampJaw;
        [SerializeField] private Transform rightClampJaw;
        [SerializeField] private Transform leftTransportWheel;
        [SerializeField] private Transform rightTransportWheel;

        [Header("Player Handling")]
        [SerializeField, Min(0.5f)] private float handleDistanceFromPlayer = 1.0f;
        [SerializeField, Min(0.5f)] private float maximumTowSpeed = 3.2f;
        [SerializeField, Min(10f)] private float maximumTowYawRate = 70f;
        [SerializeField, Min(1f)] private float freeMovementSharpness = 12f;
        [SerializeField, Min(1f)] private float freeRotationSharpness = 12f;

        [Header("Attachment")]
        [SerializeField, Min(0.2f)] private float attachmentDistance = 0.8f;
        [SerializeField, Min(0.1f)] private float freeTowHeadHeight = 0.29f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private Transform playerTransform;
        private bool playerHolding;
        private bool attachedToTailwheel;
        private float towBarLength;
        private Vector3 tailwheelLocalPosition;
        private Vector3 leftClampOpenPosition;
        private Vector3 rightClampOpenPosition;
        private Vector3 leftClampClosedPosition;
        private Vector3 rightClampClosedPosition;
        private Vector3 previousTowBarPosition;
        private float transportWheelSpin;

        public static P51TowBarController ActiveControlledTowBar { get; private set; }
        public static P51TowBarController ActiveAttachedTowBar { get; private set; }

        public bool IsPlayerHolding => playerHolding;
        public bool IsAttachedToTailwheel => attachedToTailwheel;
        public bool IsConfigured => flightController != null
            && landingGear != null
            && aircraftBody != null
            && tailwheelAttachPoint != null
            && towHead != null
            && handleGrip != null
            && interactionCollider != null;
        public Transform TowHead => towHead;
        public Transform HandleGrip => handleGrip;
        public P51FlightController FlightController => flightController;

        public string InteractionText
        {
            get
            {
                string controlText = playerHolding
                    ? "E: release tow bar"
                    : "E: grab tow bar handle";

                if (attachedToTailwheel)
                {
                    return $"{controlText} | F: disconnect tow bar from tailwheel";
                }

                if (IsTowHeadNearTailwheel())
                {
                    return $"{controlText} | F: connect tow bar to P-51 tailwheel";
                }

                return playerHolding
                    ? $"{controlText} | Move the fork over the tailwheel, then press F"
                    : controlText;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            CacheGeometry();
            previousTowBarPosition = transform.position;
            RefreshCollider();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheGeometry();
            previousTowBarPosition = transform.position;
            RefreshCollider();
        }

        private void FixedUpdate()
        {
            if (attachedToTailwheel && playerHolding)
            {
                MoveAircraftFromPlayer(Time.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            if (attachedToTailwheel)
            {
                SnapTowBarToTailwheel();
            }
            else if (playerHolding)
            {
                FollowPlayerAsLooseTowBar();
            }

            AnimateClampJaws();
            AnimateTransportWheels();
        }

        public void Configure(
            P51FlightController configuredFlightController,
            P51RaycastLandingGear configuredLandingGear,
            Rigidbody configuredAircraftBody,
            Transform configuredTailwheelAttachPoint,
            Transform configuredTowHead,
            Transform configuredHandleGrip,
            Collider configuredInteractionCollider,
            Transform configuredLeftClampJaw,
            Transform configuredRightClampJaw,
            Transform configuredLeftTransportWheel,
            Transform configuredRightTransportWheel)
        {
            flightController = configuredFlightController;
            landingGear = configuredLandingGear;
            aircraftBody = configuredAircraftBody;
            tailwheelAttachPoint = configuredTailwheelAttachPoint;
            towHead = configuredTowHead;
            handleGrip = configuredHandleGrip;
            interactionCollider = configuredInteractionCollider;
            leftClampJaw = configuredLeftClampJaw;
            rightClampJaw = configuredRightClampJaw;
            leftTransportWheel = configuredLeftTransportWheel;
            rightTransportWheel = configuredRightTransportWheel;
            ResolveReferences();
            CacheGeometry();
            previousTowBarPosition = transform.position;
            RefreshCollider();
        }

        public bool TogglePlayerControl(out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveReferences();

            if (playerHolding)
            {
                ReleasePlayerControl();
                resultMessage = attachedToTailwheel
                    ? "Released the tow-bar handle. The tow bar remains connected to the tailwheel."
                    : "Set down the tow bar.";
                return true;
            }

            if (flightController != null && flightController.PilotPresent)
            {
                resultMessage = "Exit the P-51 cockpit before handling the tow bar.";
                return false;
            }

            if (ActiveControlledTowBar != null && ActiveControlledTowBar != this)
            {
                resultMessage = "Release the other tow bar first.";
                return false;
            }

            if (EngineHoistController.ActiveControlledHoist != null)
            {
                resultMessage = "Release the engine hoist before grabbing the tow bar.";
                return false;
            }

            ResolvePlayer();
            if (playerTransform == null)
            {
                resultMessage = "The tow bar could not find the Player.";
                return false;
            }

            playerHolding = true;
            ActiveControlledTowBar = this;
            RefreshCollider();
            resultMessage = attachedToTailwheel
                ? "Grabbed the connected tow bar. Walk to reposition the P-51 by hand."
                : "Grabbed the tow bar. Move its fork to the P-51 tailwheel and press F.";
            return true;
        }

        public bool ToggleTailwheelAttachment(out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveReferences();

            if (attachedToTailwheel)
            {
                if (flightController != null
                    && flightController.GroundSpeedMetersPerSecond > 0.75f)
                {
                    resultMessage = "Stop the aircraft before disconnecting the tow bar.";
                    return false;
                }

                attachedToTailwheel = false;
                if (ActiveAttachedTowBar == this)
                {
                    ActiveAttachedTowBar = null;
                }

                resultMessage = playerHolding
                    ? "Disconnected the tow bar. It remains in your hands."
                    : "Disconnected the tow bar from the tailwheel.";
                return true;
            }

            if (!IsConfigured)
            {
                resultMessage = "The tow bar or P-51 tailwheel references are incomplete.";
                return false;
            }

            if (flightController.PilotPresent)
            {
                resultMessage = "Exit the cockpit before connecting the tow bar.";
                return false;
            }

            if (flightController.EngineRunning)
            {
                resultMessage = "Shut down the Merlin before connecting the tow bar.";
                return false;
            }

            if (landingGear.GroundedWheelCount < 2)
            {
                resultMessage = "The aircraft must be resting on its landing gear before towing.";
                return false;
            }

            if (!IsTowHeadNearTailwheel())
            {
                float distance = towHead != null && tailwheelAttachPoint != null
                    ? Vector3.Distance(towHead.position, tailwheelAttachPoint.position)
                    : 0f;
                resultMessage = $"Move the tow-bar fork closer to the tailwheel ({distance:F1} m away).";
                return false;
            }

            if (ActiveAttachedTowBar != null && ActiveAttachedTowBar != this)
            {
                resultMessage = "Another tow bar is already connected to the aircraft.";
                return false;
            }

            PrepareAircraftForTowing();
            attachedToTailwheel = true;
            ActiveAttachedTowBar = this;
            SnapTowBarToTailwheel();
            resultMessage = playerHolding
                ? "Locked the fork around the tailwheel. Walk to reposition the P-51."
                : "Connected the tow bar to the tailwheel. Grab the handle with E.";
            return true;
        }

        public static bool IsAircraftTowBarAttached(P51FlightController controller)
        {
            return controller != null
                && ActiveAttachedTowBar != null
                && ActiveAttachedTowBar.attachedToTailwheel
                && ActiveAttachedTowBar.flightController == controller;
        }

        private void PrepareAircraftForTowing()
        {
            if (aircraftBody == null)
            {
                return;
            }

            aircraftBody.linearVelocity = Vector3.zero;
            aircraftBody.angularVelocity = Vector3.zero;
            aircraftBody.isKinematic = true;
            aircraftBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            tailwheelLocalPosition = flightController.transform.InverseTransformPoint(
                tailwheelAttachPoint.position);
        }

        private void MoveAircraftFromPlayer(float deltaTime)
        {
            ResolvePlayer();
            if (playerTransform == null || aircraftBody == null || flightController == null)
            {
                ReleasePlayerControl();
                return;
            }

            PrepareAircraftForTowing();

            Vector3 forward = GetFlatPlayerForward();
            Vector3 desiredHandlePosition = playerTransform.position
                + forward * handleDistanceFromPlayer;
            Vector3 desiredTailwheelPosition = desiredHandlePosition
                + forward * towBarLength;
            desiredTailwheelPosition.y = tailwheelAttachPoint.position.y;

            Quaternion currentYaw = Quaternion.Euler(
                0f,
                flightController.transform.eulerAngles.y,
                0f);
            Quaternion preservedTilt = Quaternion.Inverse(currentYaw)
                * flightController.transform.rotation;
            Quaternion desiredYaw = Quaternion.LookRotation(forward, Vector3.up);
            Quaternion desiredRotation = desiredYaw * preservedTilt;
            Quaternion nextRotation = Quaternion.RotateTowards(
                aircraftBody.rotation,
                desiredRotation,
                maximumTowYawRate * deltaTime);

            Vector3 desiredAircraftPosition = desiredTailwheelPosition
                - nextRotation * tailwheelLocalPosition;
            desiredAircraftPosition.y = aircraftBody.position.y;
            Vector3 nextPosition = Vector3.MoveTowards(
                aircraftBody.position,
                desiredAircraftPosition,
                maximumTowSpeed * deltaTime);

            aircraftBody.MoveRotation(nextRotation);
            aircraftBody.MovePosition(nextPosition);
        }

        private void FollowPlayerAsLooseTowBar()
        {
            ResolvePlayer();
            if (playerTransform == null)
            {
                ReleasePlayerControl();
                return;
            }

            Vector3 forward = GetFlatPlayerForward();
            Vector3 desiredHeadPosition = playerTransform.position
                + forward * (handleDistanceFromPlayer + towBarLength);
            desiredHeadPosition.y = FindGroundHeight(desiredHeadPosition)
                + freeTowHeadHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(forward, Vector3.up);

            float positionBlend = 1f - Mathf.Exp(
                -freeMovementSharpness * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(
                -freeRotationSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(
                transform.position,
                desiredHeadPosition,
                positionBlend);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationBlend);
        }

        private void SnapTowBarToTailwheel()
        {
            if (tailwheelAttachPoint == null)
            {
                return;
            }

            transform.SetPositionAndRotation(
                tailwheelAttachPoint.position,
                flightController != null
                    ? flightController.transform.rotation
                    : tailwheelAttachPoint.rotation);
        }

        private void AnimateClampJaws()
        {
            float blend = 1f - Mathf.Exp(-14f * Time.deltaTime);
            if (leftClampJaw != null)
            {
                leftClampJaw.localPosition = Vector3.Lerp(
                    leftClampJaw.localPosition,
                    attachedToTailwheel
                        ? leftClampClosedPosition
                        : leftClampOpenPosition,
                    blend);
            }
            if (rightClampJaw != null)
            {
                rightClampJaw.localPosition = Vector3.Lerp(
                    rightClampJaw.localPosition,
                    attachedToTailwheel
                        ? rightClampClosedPosition
                        : rightClampOpenPosition,
                    blend);
            }
        }

        private void AnimateTransportWheels()
        {
            Vector3 horizontalDelta = Vector3.ProjectOnPlane(
                transform.position - previousTowBarPosition,
                Vector3.up);
            transportWheelSpin = Mathf.Repeat(
                transportWheelSpin
                + horizontalDelta.magnitude / 0.12f * Mathf.Rad2Deg,
                360f);
            previousTowBarPosition = transform.position;

            Quaternion spin = Quaternion.Euler(transportWheelSpin, 0f, 0f);
            if (leftTransportWheel != null)
            {
                leftTransportWheel.localRotation = spin;
            }
            if (rightTransportWheel != null)
            {
                rightTransportWheel.localRotation = spin;
            }
        }

        private bool IsTowHeadNearTailwheel()
        {
            return towHead != null
                && tailwheelAttachPoint != null
                && Vector3.Distance(towHead.position, tailwheelAttachPoint.position)
                    <= attachmentDistance;
        }

        private Vector3 GetFlatPlayerForward()
        {
            Vector3 forward = playerTransform != null
                ? playerTransform.forward
                : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            return forward.normalized;
        }

        private float FindGroundHeight(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 4f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                12f,
                groundLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            float height = transform.position.y - freeTowHeadHeight;

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null
                    || collider.transform.IsChildOf(transform)
                    || (flightController != null
                        && collider.transform.IsChildOf(flightController.transform))
                    || hits[index].distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hits[index].distance;
                height = hits[index].point.y;
            }

            return height;
        }

        private void CacheGeometry()
        {
            if (towHead != null && handleGrip != null)
            {
                towBarLength = Vector2.Distance(
                    new Vector2(towHead.position.x, towHead.position.z),
                    new Vector2(handleGrip.position.x, handleGrip.position.z));
            }
            towBarLength = Mathf.Max(1.5f, towBarLength);

            if (flightController != null && tailwheelAttachPoint != null)
            {
                tailwheelLocalPosition = flightController.transform.InverseTransformPoint(
                    tailwheelAttachPoint.position);
            }

            if (leftClampJaw != null)
            {
                leftClampOpenPosition = leftClampJaw.localPosition;
                leftClampClosedPosition = new Vector3(
                    -0.17f,
                    leftClampOpenPosition.y,
                    leftClampOpenPosition.z);
            }
            if (rightClampJaw != null)
            {
                rightClampOpenPosition = rightClampJaw.localPosition;
                rightClampClosedPosition = new Vector3(
                    0.17f,
                    rightClampOpenPosition.y,
                    rightClampOpenPosition.z);
            }
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = FindFirstObjectByType<P51FlightController>();
            }
            if (landingGear == null && flightController != null)
            {
                landingGear = flightController.GetComponent<P51RaycastLandingGear>();
            }
            if (aircraftBody == null && flightController != null)
            {
                aircraftBody = flightController.GetComponent<Rigidbody>();
            }
            if (tailwheelAttachPoint == null && landingGear != null)
            {
                tailwheelAttachPoint = landingGear.TailwheelAnchor;
            }
            ResolvePlayer();
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null)
            {
                return;
            }

            InventoryInteractor inventoryInteractor =
                FindFirstObjectByType<InventoryInteractor>();
            playerTransform = inventoryInteractor != null
                ? inventoryInteractor.transform
                : null;
        }

        private void ReleasePlayerControl()
        {
            playerHolding = false;
            if (ActiveControlledTowBar == this)
            {
                ActiveControlledTowBar = null;
            }
            RefreshCollider();
        }

        private void RefreshCollider()
        {
            if (interactionCollider != null)
            {
                interactionCollider.enabled = !playerHolding;
            }
        }

        private void OnDisable()
        {
            ReleasePlayerControl();
            if (ActiveAttachedTowBar == this)
            {
                ActiveAttachedTowBar = null;
            }
            attachedToTailwheel = false;
        }

        private void OnValidate()
        {
            handleDistanceFromPlayer = Mathf.Max(0.5f, handleDistanceFromPlayer);
            maximumTowSpeed = Mathf.Max(0.5f, maximumTowSpeed);
            maximumTowYawRate = Mathf.Max(10f, maximumTowYawRate);
            attachmentDistance = Mathf.Max(0.2f, attachmentDistance);
            freeTowHeadHeight = Mathf.Max(0.1f, freeTowHeadHeight);
        }
    }
}
