using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(285)]
    [DisallowMultipleComponent]
    public sealed class P51LandingGearServicePlayerInteractor : MonoBehaviour
    {
        private const string CarryAnchorName = "P-51 Wheel Carry Anchor";

        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Transform wheelCarryAnchor;
        [SerializeField, Min(1f)] private float interactionDistance = 6f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private P51LandingGearServiceTarget currentTarget;
        private P51NitrogenCartController currentCart;
        private P51LooseWheelAssembly currentLooseWheel;

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
            if (playerCamera == null || inventoryUI == null || inventoryUI.IsOpen)
            {
                CancelCurrentHold();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            FindCandidate(
                out P51LandingGearServiceTarget target,
                out P51NitrogenCartController cart,
                out P51LooseWheelAssembly looseWheel);

            P51LooseWheelAssembly carriedWheel = P51LooseWheelAssembly.CurrentCarried;
            if (carriedWheel != null)
            {
                HandleCarriedWheel(carriedWheel, target, keyboard);
                return;
            }

            if (target != currentTarget)
            {
                currentTarget?.CancelHold();
            }
            if (looseWheel != currentLooseWheel)
            {
                currentLooseWheel?.CancelHold();
            }

            currentTarget = target;
            currentCart = cart;
            currentLooseWheel = looseWheel;

            if (currentLooseWheel != null)
            {
                HandleLooseWheel(currentLooseWheel, keyboard);
                return;
            }

            if (currentTarget != null)
            {
                HandleAircraftTarget(currentTarget, keyboard);
                return;
            }

            if (currentCart != null)
            {
                HandleCart(currentCart, keyboard);
            }
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        private void HandleCarriedWheel(
            P51LooseWheelAssembly carriedWheel,
            P51LandingGearServiceTarget aimedTarget,
            Keyboard keyboard)
        {
            currentLooseWheel = null;
            currentCart = null;

            bool validInstallTarget = aimedTarget != null
                && aimedTarget.ServiceKind == P51LandingGearServiceKind.TireAndValve
                && carriedWheel.CanInstallOn(aimedTarget.WheelIndex);

            P51LandingGearServiceTarget desiredTarget = validInstallTarget
                ? aimedTarget
                : null;
            if (desiredTarget != currentTarget)
            {
                currentTarget?.CancelHold();
                currentTarget = desiredTarget;
            }

            if (currentTarget != null)
            {
                bool holdingE = keyboard != null && keyboard.eKey.isPressed;
                if (currentTarget.ProcessInteraction(
                        inventory,
                        holdingE,
                        false,
                        Time.deltaTime,
                        out string installMessage)
                    && !string.IsNullOrWhiteSpace(installMessage))
                {
                    inventoryUI.ShowStatusMessage(installMessage, 3.5f);
                }

                if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                {
                    inventoryUI.ShowStatusMessage(currentTarget.Inspect(), 4f);
                }

                inventoryUI.SetInteractionPrompt(currentTarget.InteractionText);
                return;
            }

            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (TryFindWheelPlacementPose(out Vector3 position, out Quaternion rotation)
                    && carriedWheel.TryPlace(position, rotation, out string placementMessage))
                {
                    inventoryUI.ShowStatusMessage(placementMessage, 2.8f);
                    inventoryUI.SetInteractionPrompt(string.Empty);
                    return;
                }
            }

            inventoryUI.SetInteractionPrompt(
                $"E: set down carried {carriedWheel.WheelLabel} wheel | Carry it to its highlighted original axle and hold E to reinstall");
        }

        private void HandleLooseWheel(
            P51LooseWheelAssembly looseWheel,
            Keyboard keyboard)
        {
            if (keyboard != null
                && keyboard.eKey.wasPressedThisFrame
                && looseWheel.IsComplete)
            {
                if (looseWheel.TryBeginCarry(wheelCarryAnchor, out string carryMessage))
                {
                    inventoryUI.ShowStatusMessage(carryMessage, 3f);
                    currentLooseWheel = null;
                    inventoryUI.SetInteractionPrompt(
                        $"E: set down carried {looseWheel.WheelLabel} wheel | Carry it to its highlighted original axle and hold E to reinstall");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(carryMessage))
                {
                    inventoryUI.ShowStatusMessage(carryMessage, 3f);
                }
            }

            if (keyboard != null
                && keyboard.eKey.wasPressedThisFrame
                && looseWheel.IsBareRim
                && !looseWheel.HasCorrectEquippedTire(inventory))
            {
                if (looseWheel.TryPickupBareRim(inventory, out string rimMessage)
                    || !string.IsNullOrWhiteSpace(rimMessage))
                {
                    inventoryUI.ShowStatusMessage(rimMessage, 3f);
                }
            }

            P51NitrogenCartController connectedCart = FindConnectedCart(looseWheel);
            if (keyboard != null
                && keyboard.nKey.wasPressedThisFrame
                && looseWheel.IsComplete)
            {
                string nitrogenMessage;
                if (connectedCart != null)
                {
                    connectedCart.Disconnect();
                    connectedCart = null;
                    nitrogenMessage = "Nitrogen hose disconnected from the loose wheel.";
                }
                else
                {
                    P51NitrogenCartController nearest = FindNearestCart(
                        looseWheel.ServiceValveTarget != null
                            ? looseWheel.ServiceValveTarget.position
                            : looseWheel.transform.position);
                    if (nearest == null)
                    {
                        nitrogenMessage = "No nitrogen cart is available.";
                    }
                    else
                    {
                        nearest.TryConnect(looseWheel, out nitrogenMessage);
                        connectedCart = FindConnectedCart(looseWheel);
                    }
                }
                inventoryUI.ShowStatusMessage(nitrogenMessage, 3.5f);
            }

            HandleNitrogenControls(connectedCart, keyboard);

            bool holdE = keyboard != null && keyboard.eKey.isPressed;
            bool holdR = keyboard != null && keyboard.rKey.isPressed;
            if (looseWheel.ProcessService(
                    inventory,
                    holdE,
                    holdR,
                    Time.deltaTime,
                    out string serviceMessage)
                && !string.IsNullOrWhiteSpace(serviceMessage))
            {
                inventoryUI.ShowStatusMessage(serviceMessage, 4f);
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                inventoryUI.ShowStatusMessage(looseWheel.Inspect(), 4.5f);
            }

            string prompt = looseWheel.GetInteractionText(inventory);
            if (connectedCart != null)
            {
                prompt += $" | HOSE CONNECTED {connectedCart.RegulatorPsi:F0} PSI: Q/Z adjust | Hold F service | N disconnect";
            }
            inventoryUI.SetInteractionPrompt(prompt);
        }

        private void HandleAircraftTarget(
            P51LandingGearServiceTarget target,
            Keyboard keyboard)
        {
            bool holdE = keyboard != null && keyboard.eKey.isPressed;
            bool holdR = keyboard != null && keyboard.rKey.isPressed;
            if (target.ProcessInteraction(
                    inventory,
                    holdE,
                    holdR,
                    Time.deltaTime,
                    out string serviceMessage)
                && !string.IsNullOrWhiteSpace(serviceMessage))
            {
                inventoryUI.ShowStatusMessage(serviceMessage, 3.5f);
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                inventoryUI.ShowStatusMessage(target.Inspect(), 4f);
            }

            P51NitrogenCartController connectedCart = target.ServiceKind
                == P51LandingGearServiceKind.TireAndValve
                ? FindConnectedCart(target.Controller, target.WheelIndex)
                : null;

            if (keyboard != null
                && keyboard.nKey.wasPressedThisFrame
                && target.ServiceKind == P51LandingGearServiceKind.TireAndValve)
            {
                string nitrogenMessage;
                if (connectedCart != null)
                {
                    connectedCart.Disconnect();
                    connectedCart = null;
                    nitrogenMessage = "Nitrogen hose disconnected.";
                }
                else
                {
                    P51NitrogenCartController nearest = FindNearestCart(target.ServicePoint.position);
                    if (nearest == null)
                    {
                        nitrogenMessage = "No nitrogen cart is available.";
                    }
                    else
                    {
                        nearest.TryConnect(target.Controller, target.WheelIndex, out nitrogenMessage);
                        connectedCart = FindConnectedCart(target.Controller, target.WheelIndex);
                    }
                }
                inventoryUI.ShowStatusMessage(nitrogenMessage, 3.5f);
            }

            HandleNitrogenControls(connectedCart, keyboard);

            string prompt = target.InteractionText;
            if (connectedCart != null)
            {
                prompt += $" | HOSE CONNECTED {connectedCart.RegulatorPsi:F0} PSI: Q/Z adjust | Hold F service | N disconnect";
            }
            inventoryUI.SetInteractionPrompt(prompt);
        }

        private void HandleCart(P51NitrogenCartController cart, Keyboard keyboard)
        {
            if (keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    cart.TryToggleMove(transform, out string moveMessage);
                    if (!string.IsNullOrWhiteSpace(moveMessage))
                    {
                        inventoryUI.ShowStatusMessage(moveMessage, 2.5f);
                    }
                }

                float adjust = 0f;
                if (keyboard.qKey.isPressed) adjust += 1f;
                if (keyboard.zKey.isPressed) adjust -= 1f;
                if (Mathf.Abs(adjust) > 0.01f)
                {
                    cart.AdjustRegulator(adjust, Time.deltaTime);
                }

                if (keyboard.fKey.isPressed)
                {
                    ShowNitrogenServiceResult(cart);
                }

                if (keyboard.nKey.wasPressedThisFrame && cart.IsConnected)
                {
                    cart.Disconnect();
                    inventoryUI.ShowStatusMessage("Nitrogen hose disconnected.", 2f);
                }
            }

            inventoryUI.SetInteractionPrompt(cart.InteractionText);
        }

        private void HandleNitrogenControls(
            P51NitrogenCartController cart,
            Keyboard keyboard)
        {
            if (cart == null || keyboard == null)
            {
                return;
            }

            float adjust = 0f;
            if (keyboard.qKey.isPressed) adjust += 1f;
            if (keyboard.zKey.isPressed) adjust -= 1f;
            if (Mathf.Abs(adjust) > 0.01f)
            {
                cart.AdjustRegulator(adjust, Time.deltaTime);
            }

            if (keyboard.fKey.isPressed)
            {
                ShowNitrogenServiceResult(cart);
            }
        }

        private void ShowNitrogenServiceResult(P51NitrogenCartController cart)
        {
            if (cart == null)
            {
                return;
            }

            if (cart.ServiceConnectedTire(Time.deltaTime, out string nitrogenMessage))
            {
                inventoryUI.ShowStatusMessage(nitrogenMessage, 0.3f);
            }
            else if (!string.IsNullOrWhiteSpace(nitrogenMessage))
            {
                inventoryUI.ShowStatusMessage(nitrogenMessage, 2f);
            }
        }

        private void FindCandidate(
            out P51LandingGearServiceTarget bestTarget,
            out P51NitrogenCartController bestCart,
            out P51LooseWheelAssembly bestLooseWheel)
        {
            bestTarget = null;
            bestCart = null;
            bestLooseWheel = null;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                return;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float targetDistance = float.PositiveInfinity;
            float cartDistance = float.PositiveInfinity;
            float looseWheelDistance = float.PositiveInfinity;

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                P51LandingGearServiceTarget candidateTarget =
                    collider.GetComponentInParent<P51LandingGearServiceTarget>();
                if (candidateTarget != null && hits[index].distance < targetDistance)
                {
                    bestTarget = candidateTarget;
                    targetDistance = hits[index].distance;
                }

                P51NitrogenCartController candidateCart =
                    collider.GetComponentInParent<P51NitrogenCartController>();
                if (candidateCart != null && hits[index].distance < cartDistance)
                {
                    bestCart = candidateCart;
                    cartDistance = hits[index].distance;
                }

                P51LooseWheelAssembly candidateLooseWheel =
                    collider.GetComponentInParent<P51LooseWheelAssembly>();
                if (candidateLooseWheel != null
                    && !candidateLooseWheel.IsCarried
                    && hits[index].distance < looseWheelDistance)
                {
                    bestLooseWheel = candidateLooseWheel;
                    looseWheelDistance = hits[index].distance;
                }
            }

            float closest = Mathf.Min(targetDistance, Mathf.Min(cartDistance, looseWheelDistance));
            if (closest == float.PositiveInfinity)
            {
                return;
            }

            const float tolerance = 0.10f;
            if (looseWheelDistance <= closest + tolerance)
            {
                bestTarget = null;
                bestCart = null;
                return;
            }

            if (targetDistance <= closest + tolerance)
            {
                bestCart = null;
                bestLooseWheel = null;
                return;
            }

            bestTarget = null;
            bestLooseWheel = null;
        }

        private bool TryFindWheelPlacementPose(
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
                P51LooseWheelAssembly carried = P51LooseWheelAssembly.CurrentCarried;
                if (hitTransform == null
                    || hitTransform.IsChildOf(transform)
                    || (carried != null && hitTransform.IsChildOf(carried.transform)))
                {
                    continue;
                }

                Vector3 normal = hits[index].normal.sqrMagnitude > 0.001f
                    ? hits[index].normal.normalized
                    : Vector3.up;
                Vector3 flatForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, normal);
                if (flatForward.sqrMagnitude < 0.001f)
                {
                    flatForward = Vector3.ProjectOnPlane(transform.forward, normal);
                }
                if (flatForward.sqrMagnitude < 0.001f)
                {
                    flatForward = Vector3.forward;
                }
                flatForward.Normalize();

                worldPosition = hits[index].point + normal * 0.16f;
                worldRotation = Quaternion.LookRotation(flatForward, normal)
                    * Quaternion.Euler(0f, 0f, 90f);
                return true;
            }

            Vector3 fallbackOrigin = playerCamera.transform.position
                + playerCamera.transform.forward * 2.2f
                + Vector3.up * 1.5f;
            if (Physics.Raycast(
                fallbackOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                6f,
                interactionLayers,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    groundHit.normal);
                if (flatForward.sqrMagnitude < 0.001f)
                {
                    flatForward = transform.forward;
                }
                flatForward.Normalize();
                worldPosition = groundHit.point + groundHit.normal * 0.16f;
                worldRotation = Quaternion.LookRotation(flatForward, groundHit.normal)
                    * Quaternion.Euler(0f, 0f, 90f);
                return true;
            }

            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
            }
            forward.Normalize();
            worldPosition = transform.position + forward * 2f + Vector3.up * 0.25f;
            worldRotation = Quaternion.LookRotation(forward, Vector3.up)
                * Quaternion.Euler(0f, 0f, 90f);
            return true;
        }

        private static P51NitrogenCartController FindNearestCart(Vector3 position)
        {
            P51NitrogenCartController[] carts = FindObjectsByType<P51NitrogenCartController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            P51NitrogenCartController best = null;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < carts.Length; index++)
            {
                if (carts[index] == null)
                {
                    continue;
                }
                float distance = Vector3.Distance(position, carts[index].transform.position);
                if (distance < bestDistance)
                {
                    best = carts[index];
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static P51NitrogenCartController FindConnectedCart(
            P51LandingGearMaintenanceController controller,
            int wheelIndex)
        {
            P51NitrogenCartController[] carts = FindObjectsByType<P51NitrogenCartController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < carts.Length; index++)
            {
                if (carts[index] != null && carts[index].IsConnectedTo(controller, wheelIndex))
                {
                    return carts[index];
                }
            }
            return null;
        }

        private static P51NitrogenCartController FindConnectedCart(
            P51LooseWheelAssembly looseWheel)
        {
            P51NitrogenCartController[] carts = FindObjectsByType<P51NitrogenCartController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < carts.Length; index++)
            {
                if (carts[index] != null && carts[index].IsConnectedTo(looseWheel))
                {
                    return carts[index];
                }
            }
            return null;
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
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (wheelCarryAnchor == null && playerCamera != null)
            {
                Transform existing = playerCamera.transform.Find(CarryAnchorName);
                if (existing == null)
                {
                    GameObject anchorObject = new GameObject(CarryAnchorName);
                    existing = anchorObject.transform;
                    existing.SetParent(playerCamera.transform, false);
                }

                existing.localPosition = new Vector3(0.78f, -0.58f, 1.55f);
                existing.localRotation = Quaternion.Euler(8f, -8f, 82f);
                wheelCarryAnchor = existing;
            }
        }

        private void CancelCurrentHold()
        {
            currentTarget?.CancelHold();
            currentLooseWheel?.CancelHold();
        }

        private void OnDisable()
        {
            CancelCurrentHold();
            currentTarget = null;
            currentCart = null;
            currentLooseWheel = null;
            if (inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
            ResolveReferences();
        }
    }
}
