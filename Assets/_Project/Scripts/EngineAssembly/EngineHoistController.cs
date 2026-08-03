using System.Collections;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    public sealed class EngineHoistController : MonoBehaviour
    {
        [Header("Hoist References")]
        [SerializeField] private Transform hookPoint;
        [SerializeField] private Transform boomTip;
        [SerializeField] private Transform loadChainVisual;
        [SerializeField] private Transform leftSlingVisual;
        [SerializeField] private Transform rightSlingVisual;
        [SerializeField] private GameObject placementMarker;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private EngineAssemblyTransportController engineTransport;

        [Header("Movement")]
        [SerializeField, Min(0.6f)] private float pushDistance = 1.35f;
        [SerializeField, Min(1f)] private float movementSharpness = 10f;
        [SerializeField, Min(1f)] private float rotationSharpness = 10f;

        [Header("Lifting")]
        [SerializeField, Min(0.25f)] private float attachmentDistance = 1.15f;
        [SerializeField, Min(0.15f)] private float suspendedSlingLength = 0.52f;
        [SerializeField, Min(0.25f)] private float standSnapDistance = 1.15f;
        [SerializeField, Min(0.2f)] private float placementDuration = 0.9f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private Transform playerTransform;
        private float floorY;
        private bool isPlayerControlling;
        private bool hasAttachedEngine;
        private bool isPlacingEngine;
        private string pendingStatusMessage;

        public static EngineHoistController ActiveControlledHoist { get; private set; }

        public bool IsPlayerControlling => isPlayerControlling;
        public bool HasAttachedEngine => hasAttachedEngine;
        public bool IsBusy => isPlacingEngine;
        public Transform HookPoint => hookPoint;

        public string InteractionText
        {
            get
            {
                string controlText = isPlayerControlling
                    ? "E: release hoist"
                    : "E: grab and push hoist";

                if (isPlacingEngine)
                {
                    return "Lowering the engine — keep clear";
                }

                if (hasAttachedEngine)
                {
                    bool nearStand = engineTransport != null
                        && engineTransport.HorizontalDistanceFromHookToStand(hookPoint.position)
                            <= standSnapDistance;
                    string placement = nearStand
                        ? "F: lower engine back onto stand"
                        : "F: lower engine at marker";
                    return $"{controlText} | {placement}";
                }

                if (engineTransport == null || hookPoint == null)
                {
                    return controlText;
                }

                if (engineTransport.CanAttach(
                        hookPoint.position,
                        attachmentDistance,
                        out _))
                {
                    return $"{controlText} | F: connect hook and lift engine";
                }

                return $"{controlText} | Move hook over engine, then press F";
            }
        }

        private void Awake()
        {
            ResolvePlayer();
            floorY = transform.position.y;
            RefreshCableVisibility();
        }

        private void OnEnable()
        {
            ResolvePlayer();
            floorY = transform.position.y;
            RefreshCableVisibility();
        }

        private void LateUpdate()
        {
            if (isPlayerControlling && !isPlacingEngine)
            {
                FollowPlayer();
            }

            if (hasAttachedEngine && !isPlacingEngine && engineTransport != null && hookPoint != null)
            {
                Vector3 desiredLiftPosition =
                    hookPoint.position - hookPoint.up * suspendedSlingLength;
                engineTransport.UpdateSuspendedPose(
                    desiredLiftPosition,
                    transform.rotation,
                    8f,
                    8f,
                    Time.deltaTime);
            }

            UpdateCableVisuals();
            UpdatePlacementMarker();
        }

        public void Configure(
            Transform configuredHookPoint,
            Transform configuredBoomTip,
            Transform configuredLoadChainVisual,
            Transform configuredLeftSlingVisual,
            Transform configuredRightSlingVisual,
            GameObject configuredPlacementMarker,
            Collider configuredInteractionCollider,
            EngineAssemblyTransportController configuredEngineTransport)
        {
            hookPoint = configuredHookPoint;
            boomTip = configuredBoomTip;
            loadChainVisual = configuredLoadChainVisual;
            leftSlingVisual = configuredLeftSlingVisual;
            rightSlingVisual = configuredRightSlingVisual;
            placementMarker = configuredPlacementMarker;
            interactionCollider = configuredInteractionCollider;
            engineTransport = configuredEngineTransport;
            floorY = transform.position.y;
            ResolvePlayer();
            RefreshCableVisibility();
        }

        public bool TogglePlayerControl(out string resultMessage)
        {
            resultMessage = string.Empty;

            if (isPlacingEngine)
            {
                resultMessage = "Wait until the engine has finished lowering.";
                return false;
            }

            ResolvePlayer();
            if (playerTransform == null)
            {
                resultMessage = "The hoist could not find the Player.";
                return false;
            }

            if (isPlayerControlling)
            {
                ReleasePlayerControl();
                resultMessage = hasAttachedEngine
                    ? "Released the hoist. The engine remains suspended."
                    : "Released the engine hoist.";
                return true;
            }

            if (ActiveControlledHoist != null && ActiveControlledHoist != this)
            {
                resultMessage = "Release the other hoist first.";
                return false;
            }

            isPlayerControlling = true;
            ActiveControlledHoist = this;
            resultMessage = hasAttachedEngine
                ? "Grabbed the loaded engine hoist. Walk to move it."
                : "Grabbed the engine hoist. Walk to move it.";
            return true;
        }

        public bool ToggleEngineAttachment(out string resultMessage)
        {
            resultMessage = string.Empty;

            if (isPlacingEngine)
            {
                resultMessage = "The engine is already being lowered.";
                return false;
            }

            if (engineTransport == null || hookPoint == null)
            {
                resultMessage = "The hoist transport references are missing.";
                return false;
            }

            if (hasAttachedEngine)
            {
                StartCoroutine(PlaceEngineRoutine());
                resultMessage = engineTransport.HorizontalDistanceFromHookToStand(hookPoint.position)
                    <= standSnapDistance
                        ? "Lowering the engine back onto the stand."
                        : "Lowering the engine onto the floor marker.";
                return true;
            }

            bool wasOnStand = engineTransport.IsOnStand;
            if (!engineTransport.CanAttach(
                    hookPoint.position,
                    attachmentDistance,
                    out resultMessage))
            {
                return false;
            }

            engineTransport.BeginSuspension();
            hasAttachedEngine = true;
            RefreshCableVisibility();
            resultMessage = wasOnStand
                ? "Connected the lifting slings and lifted the engine clear of the stand."
                : "Connected the lifting slings and lifted the engine from its placed location.";
            return true;
        }

        public bool TryConsumeStatusMessage(out string message)
        {
            message = pendingStatusMessage;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            pendingStatusMessage = string.Empty;
            return true;
        }

        private void FollowPlayer()
        {
            ResolvePlayer();
            if (playerTransform == null)
            {
                ReleasePlayerControl();
                return;
            }

            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
            }
            forward.Normalize();

            Vector3 targetPosition = playerTransform.position + forward * pushDistance;
            targetPosition.y = floorY;
            Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);

            float positionBlend = 1f - Mathf.Exp(-movementSharpness * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, positionBlend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
        }

        private IEnumerator PlaceEngineRoutine()
        {
            isPlacingEngine = true;
            bool returnToStand = engineTransport.HorizontalDistanceFromHookToStand(hookPoint.position)
                <= standSnapDistance;

            Vector3 startPosition = engineTransport.TransportRoot.position;
            Quaternion startRotation = engineTransport.TransportRoot.rotation;
            Vector3 targetPosition;
            Quaternion targetRotation;

            if (returnToStand)
            {
                targetPosition = engineTransport.StandWorldPosition;
                targetRotation = engineTransport.StandWorldRotation;
            }
            else
            {
                Vector3 groundPoint = FindGroundPointBelowHook();
                targetRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                targetPosition = engineTransport.CalculateRootPositionForGroundContact(
                    groundPoint + Vector3.up * 0.025f,
                    targetRotation);
            }

            float elapsed = 0f;
            while (elapsed < placementDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / placementDuration);
                float smooth = normalized * normalized * (3f - 2f * normalized);
                engineTransport.SetWorldPose(
                    Vector3.Lerp(startPosition, targetPosition, smooth),
                    Quaternion.Slerp(startRotation, targetRotation, smooth));
                UpdateCableVisuals();
                yield return null;
            }

            engineTransport.SetWorldPose(targetPosition, targetRotation);
            engineTransport.CompletePlacement(returnToStand);
            hasAttachedEngine = false;
            isPlacingEngine = false;
            RefreshCableVisibility();
            pendingStatusMessage = returnToStand
                ? "Placed the engine back onto the maintenance stand with its assembly state preserved."
                : "Placed the engine on the floor with its assembly state preserved.";
        }

        private Vector3 FindGroundPointBelowHook()
        {
            Vector3 origin = hookPoint.position + Vector3.up * 2f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                20f,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = new Vector3(hookPoint.position.x, floorY, hookPoint.position.z);

            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
                if (hitTransform == null
                    || hitTransform.IsChildOf(transform)
                    || (engineTransport.TransportRoot != null
                        && hitTransform.IsChildOf(engineTransport.TransportRoot)))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestPoint = hit.point;
                }
            }

            return bestPoint;
        }

        private void UpdatePlacementMarker()
        {
            if (placementMarker == null)
            {
                return;
            }

            bool shouldShow = hasAttachedEngine && !isPlacingEngine;
            placementMarker.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            Vector3 markerPoint = engineTransport != null
                && engineTransport.HorizontalDistanceFromHookToStand(hookPoint.position)
                    <= standSnapDistance
                    ? new Vector3(
                        engineTransport.StandWorldPosition.x,
                        floorY + 0.015f,
                        engineTransport.StandWorldPosition.z)
                    : FindGroundPointBelowHook() + Vector3.up * 0.015f;

            placementMarker.transform.position = markerPoint;
            placementMarker.transform.rotation = Quaternion.identity;
        }

        private void UpdateCableVisuals()
        {
            if (boomTip != null && hookPoint != null && loadChainVisual != null)
            {
                SetCylinderBetween(loadChainVisual, boomTip.position, hookPoint.position, 0.014f);
            }

            bool showSlings = hasAttachedEngine
                && engineTransport != null
                && engineTransport.LeftLiftLug != null
                && engineTransport.RightLiftLug != null;

            if (leftSlingVisual != null)
            {
                leftSlingVisual.gameObject.SetActive(showSlings);
                if (showSlings)
                {
                    SetCylinderBetween(
                        leftSlingVisual,
                        hookPoint.position,
                        engineTransport.LeftLiftLug.position,
                        0.012f);
                }
            }

            if (rightSlingVisual != null)
            {
                rightSlingVisual.gameObject.SetActive(showSlings);
                if (showSlings)
                {
                    SetCylinderBetween(
                        rightSlingVisual,
                        hookPoint.position,
                        engineTransport.RightLiftLug.position,
                        0.012f);
                }
            }
        }

        private void RefreshCableVisibility()
        {
            if (loadChainVisual != null)
            {
                loadChainVisual.gameObject.SetActive(true);
            }

            if (leftSlingVisual != null)
            {
                leftSlingVisual.gameObject.SetActive(hasAttachedEngine);
            }

            if (rightSlingVisual != null)
            {
                rightSlingVisual.gameObject.SetActive(hasAttachedEngine);
            }

            if (placementMarker != null)
            {
                placementMarker.SetActive(hasAttachedEngine && !isPlacingEngine);
            }
        }

        private static void SetCylinderBetween(
            Transform cylinder,
            Vector3 start,
            Vector3 end,
            float radius)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.001f)
            {
                cylinder.gameObject.SetActive(false);
                return;
            }

            cylinder.gameObject.SetActive(true);
            cylinder.position = (start + end) * 0.5f;
            cylinder.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            cylinder.localScale = new Vector3(radius, length * 0.5f, radius);
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null)
            {
                return;
            }

            InventoryInteractor interactor = FindFirstObjectByType<InventoryInteractor>();
            if (interactor != null)
            {
                playerTransform = interactor.transform;
            }
        }

        private void ReleasePlayerControl()
        {
            isPlayerControlling = false;
            if (ActiveControlledHoist == this)
            {
                ActiveControlledHoist = null;
            }
        }

        private void OnDisable()
        {
            ReleasePlayerControl();
            StopAllCoroutines();
            isPlacingEngine = false;
        }

        private void OnValidate()
        {
            pushDistance = Mathf.Max(0.6f, pushDistance);
            movementSharpness = Mathf.Max(1f, movementSharpness);
            rotationSharpness = Mathf.Max(1f, rotationSharpness);
            attachmentDistance = Mathf.Max(0.25f, attachmentDistance);
            suspendedSlingLength = Mathf.Max(0.15f, suspendedSlingLength);
            standSnapDistance = Mathf.Max(0.25f, standSnapDistance);
            placementDuration = Mathf.Max(0.2f, placementDuration);
        }
    }
}
