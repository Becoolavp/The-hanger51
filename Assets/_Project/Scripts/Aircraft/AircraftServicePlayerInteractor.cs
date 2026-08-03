using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(260)]
    public sealed class AircraftServicePlayerInteractor : MonoBehaviour
    {
        private const string CarryAnchorName = "P-51 Cowling Carry Anchor";

        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private Transform cowlingCarryAnchor;
        [SerializeField, Min(1f)] private float interactionDistance = 6f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private readonly Vector3 carryLocalPosition = new Vector3(1.10f, -0.72f, 2.10f);
        private readonly Quaternion carryLocalRotation = Quaternion.Euler(14f, -12f, 70f);

        private AircraftServiceInteractionTarget currentTarget;
        private P51PortableCowlingPanel currentLooseCowling;
        private P51AircraftServiceController carriedCowlingService;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            ResolveCarriedCowlingService();
            if (playerCamera == null || inventoryUI == null || inventoryUI.IsOpen)
            {
                CancelCurrentHold();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            FindInteractionCandidates(out AircraftServiceInteractionTarget target, out P51PortableCowlingPanel looseCowling);

            if (carriedCowlingService != null && carriedCowlingService.IsCowlingCarried)
            {
                HandleCarriedCowling(target, keyboard);
                return;
            }

            currentLooseCowling = looseCowling;
            if (currentLooseCowling != null)
            {
                if (target != currentTarget)
                {
                    CancelCurrentHold();
                    currentTarget = null;
                }

                if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    if (currentLooseCowling.ServiceController != null
                        && currentLooseCowling.ServiceController.TryBeginCowlingCarry(
                            cowlingCarryAnchor,
                            carryLocalPosition,
                            carryLocalRotation,
                            out string pickupMessage))
                    {
                        carriedCowlingService = currentLooseCowling.ServiceController;
                        currentLooseCowling = null;
                    }

                    if (!string.IsNullOrWhiteSpace(pickupMessage))
                    {
                        inventoryUI.ShowStatusMessage(pickupMessage, 2.8f);
                    }
                }

                inventoryUI.SetInteractionPrompt(currentLooseCowling != null
                    ? currentLooseCowling.InteractionText
                    : "E: place cowling | Hold E at highlighted opening to reinstall");
                return;
            }

            if (target != currentTarget)
            {
                CancelCurrentHold();
                currentTarget = target;
            }

            if (currentTarget == null)
            {
                return;
            }

            bool holdingE = keyboard != null && keyboard.eKey.isPressed;
            bool holdingR = keyboard != null && keyboard.rKey.isPressed;
            bool removingCowlingPanel = currentTarget.InteractionKind
                == AircraftServiceInteractionKind.CowlingPanel
                && currentTarget.CanRemove
                && holdingR
                && !holdingE;
            P51AircraftServiceController targetService =
                currentTarget.GetComponentInParent<P51AircraftServiceController>();

            bool completed = currentTarget.ProcessInteraction(
                holdingE,
                holdingR,
                Time.deltaTime,
                out string resultMessage);

            if (completed && removingCowlingPanel && targetService != null)
            {
                if (targetService.TryBeginCowlingCarry(
                    cowlingCarryAnchor,
                    carryLocalPosition,
                    carryLocalRotation,
                    out string carryMessage))
                {
                    carriedCowlingService = targetService;
                    resultMessage = carryMessage;
                }
            }

            if (completed && !string.IsNullOrWhiteSpace(resultMessage))
            {
                inventoryUI.ShowStatusMessage(resultMessage, 2.8f);
            }

            inventoryUI.SetInteractionPrompt(currentTarget.InteractionText);
        }

        public void Configure(
            Camera configuredCamera,
            InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        private void HandleCarriedCowling(
            AircraftServiceInteractionTarget aimedTarget,
            Keyboard keyboard)
        {
            bool validInstallTarget = aimedTarget != null
                && aimedTarget.InteractionKind == AircraftServiceInteractionKind.CowlingPanel
                && aimedTarget.CanInstall;

            AircraftServiceInteractionTarget desiredTarget = validInstallTarget
                ? aimedTarget
                : null;
            if (desiredTarget != currentTarget)
            {
                CancelCurrentHold();
                currentTarget = desiredTarget;
            }

            currentLooseCowling = null;

            if (currentTarget != null)
            {
                bool holdingE = keyboard != null && keyboard.eKey.isPressed;
                bool completed = currentTarget.ProcessInteraction(
                    holdingE,
                    false,
                    Time.deltaTime,
                    out string installMessage);

                if (completed)
                {
                    if (!string.IsNullOrWhiteSpace(installMessage))
                    {
                        inventoryUI.ShowStatusMessage(installMessage, 2.8f);
                    }

                    if (carriedCowlingService == null || !carriedCowlingService.IsCowlingCarried)
                    {
                        carriedCowlingService = null;
                    }
                }

                inventoryUI.SetInteractionPrompt(currentTarget.InteractionText);
                return;
            }

            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (TryFindCowlingPlacementPose(out Vector3 position, out Quaternion rotation)
                    && carriedCowlingService.TryPlaceCarriedCowling(
                        position,
                        rotation,
                        out string placementMessage))
                {
                    carriedCowlingService = null;
                    inventoryUI.ShowStatusMessage(placementMessage, 2.8f);
                    inventoryUI.SetInteractionPrompt(string.Empty);
                    return;
                }
            }

            inventoryUI.SetInteractionPrompt(
                "E: place carried cowling | Aim at highlighted engine opening and hold E to reinstall");
        }

        private void FindInteractionCandidates(
            out AircraftServiceInteractionTarget bestTarget,
            out P51PortableCowlingPanel nearestLooseCowling)
        {
            bestTarget = null;
            nearestLooseCowling = null;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            int bestPriority = -1;
            float bestTargetDistance = float.PositiveInfinity;
            float looseCowlingDistance = float.PositiveInfinity;

            for (int index = 0; index < hits.Length; index++)
            {
                AircraftServiceInteractionTarget candidate =
                    hits[index].collider.GetComponentInParent<AircraftServiceInteractionTarget>();
                if (candidate != null && candidate.CanInteract)
                {
                    int priority = GetPriority(candidate.InteractionKind);
                    if (priority > bestPriority
                        || (priority == bestPriority && hits[index].distance < bestTargetDistance))
                    {
                        bestTarget = candidate;
                        bestPriority = priority;
                        bestTargetDistance = hits[index].distance;
                    }
                }

                P51PortableCowlingPanel portablePanel =
                    hits[index].collider.GetComponentInParent<P51PortableCowlingPanel>();
                if (portablePanel != null
                    && portablePanel.CanPickUp
                    && hits[index].distance < looseCowlingDistance)
                {
                    nearestLooseCowling = portablePanel;
                    looseCowlingDistance = hits[index].distance;
                }
            }

            if (nearestLooseCowling != null
                && bestTarget != null
                && bestTargetDistance + 0.08f < looseCowlingDistance)
            {
                nearestLooseCowling = null;
            }
        }

        private bool TryFindCowlingPlacementPose(
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                Transform hitTransform = hits[index].collider != null
                    ? hits[index].collider.transform
                    : null;
                if (hitTransform == null
                    || hitTransform.IsChildOf(transform)
                    || (carriedCowlingService != null
                        && carriedCowlingService.TopCowlingPanel != null
                        && hitTransform.IsChildOf(carriedCowlingService.TopCowlingPanel.transform)))
                {
                    continue;
                }

                BuildSurfacePlacementPose(hits[index].point, hits[index].normal, out worldPosition, out worldRotation);
                return true;
            }

            Vector3 fallbackOrigin = playerCamera.transform.position
                + playerCamera.transform.forward * 2.4f
                + Vector3.up * 1.5f;
            if (Physics.Raycast(
                fallbackOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                6f,
                interactionLayers,
                QueryTriggerInteraction.Ignore))
            {
                BuildSurfacePlacementPose(groundHit.point, groundHit.normal, out worldPosition, out worldRotation);
                return true;
            }

            Vector3 flatForward = playerCamera.transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();
            worldPosition = transform.position + flatForward * 2.2f + Vector3.up * 0.12f;
            worldRotation = Quaternion.LookRotation(flatForward, Vector3.up);
            return true;
        }

        private void BuildSurfacePlacementPose(
            Vector3 surfacePoint,
            Vector3 surfaceNormal,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
                ? surfaceNormal.normalized
                : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, normal);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, normal);
            }
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.Cross(normal, Vector3.right);
            }
            forward.Normalize();

            worldPosition = surfacePoint + normal * 0.12f;
            worldRotation = Quaternion.LookRotation(forward, normal);
        }

        private static int GetPriority(AircraftServiceInteractionKind kind)
        {
            switch (kind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                case AircraftServiceInteractionKind.EngineMountBolt:
                    return 2;
                case AircraftServiceInteractionKind.CowlingPanel:
                    return 1;
                default:
                    return 0;
            }
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }

            if (cowlingCarryAnchor == null && playerCamera != null)
            {
                Transform existing = playerCamera.transform.Find(CarryAnchorName);
                if (existing == null)
                {
                    GameObject anchorObject = new GameObject(CarryAnchorName);
                    existing = anchorObject.transform;
                    existing.SetParent(playerCamera.transform, false);
                }

                cowlingCarryAnchor = existing;
            }
        }

        private void ResolveCarriedCowlingService()
        {
            if (carriedCowlingService != null && carriedCowlingService.IsCowlingCarried)
            {
                return;
            }

            carriedCowlingService = null;
            P51AircraftServiceController[] services = FindObjectsByType<P51AircraftServiceController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < services.Length; index++)
            {
                if (services[index] != null && services[index].IsCowlingCarried)
                {
                    carriedCowlingService = services[index];
                    return;
                }
            }
        }

        private void CancelCurrentHold()
        {
            if (currentTarget != null)
            {
                currentTarget.CancelHold();
            }
        }

        private void OnDisable()
        {
            CancelCurrentHold();
            currentTarget = null;
            currentLooseCowling = null;
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
