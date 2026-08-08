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
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private PlayerInventory inventory;
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

            FindCandidate(
                out P51LandingGearServiceTarget target,
                out P51NitrogenCartController cart,
                out P51LooseWheelAssembly looseWheel);

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

            Keyboard keyboard = Keyboard.current;
            if (currentLooseWheel != null)
            {
                bool holdR = keyboard != null && keyboard.rKey.isPressed;
                if (currentLooseWheel.ProcessSeparation(
                        holdR,
                        Time.deltaTime,
                        out string separationMessage)
                    && !string.IsNullOrWhiteSpace(separationMessage))
                {
                    inventoryUI.ShowStatusMessage(separationMessage, 4f);
                }

                if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                {
                    inventoryUI.ShowStatusMessage(currentLooseWheel.Inspect(), 4.5f);
                }

                inventoryUI.SetInteractionPrompt(currentLooseWheel.InteractionText);
                return;
            }

            if (currentTarget != null)
            {
                bool holdE = keyboard != null && keyboard.eKey.isPressed;
                bool holdR = keyboard != null && keyboard.rKey.isPressed;
                if (currentTarget.ProcessInteraction(
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
                    inventoryUI.ShowStatusMessage(currentTarget.Inspect(), 4f);
                }

                if (keyboard != null
                    && keyboard.nKey.wasPressedThisFrame
                    && currentTarget.ServiceKind == P51LandingGearServiceKind.TireAndValve)
                {
                    P51NitrogenCartController nearest = FindNearestCart(currentTarget.ServicePoint.position);
                    string nitrogenMessage;
                    if (nearest == null)
                    {
                        nitrogenMessage = "No nitrogen cart is within hose range.";
                    }
                    else if (nearest.IsConnected)
                    {
                        nearest.Disconnect();
                        nitrogenMessage = "Nitrogen hose disconnected.";
                    }
                    else
                    {
                        nearest.TryConnect(
                            currentTarget.Controller,
                            currentTarget.WheelIndex,
                            out nitrogenMessage);
                    }
                    inventoryUI.ShowStatusMessage(nitrogenMessage, 3.5f);
                }

                inventoryUI.SetInteractionPrompt(currentTarget.InteractionText);
                return;
            }

            if (currentCart != null)
            {
                if (keyboard != null)
                {
                    if (keyboard.eKey.wasPressedThisFrame)
                    {
                        if (currentCart.TryToggleMove(transform, out string moveMessage)
                            && !string.IsNullOrWhiteSpace(moveMessage))
                        {
                            inventoryUI.ShowStatusMessage(moveMessage, 2.5f);
                        }
                        else if (!string.IsNullOrWhiteSpace(moveMessage))
                        {
                            inventoryUI.ShowStatusMessage(moveMessage, 2.5f);
                        }
                    }

                    float adjust = 0f;
                    if (keyboard.qKey.isPressed) adjust += 1f;
                    if (keyboard.zKey.isPressed) adjust -= 1f;
                    if (Mathf.Abs(adjust) > 0.01f)
                    {
                        currentCart.AdjustRegulator(adjust, Time.deltaTime);
                    }

                    if (keyboard.fKey.isPressed)
                    {
                        if (currentCart.ServiceConnectedTire(
                                Time.deltaTime,
                                out string nitrogenMessage))
                        {
                            inventoryUI.ShowStatusMessage(nitrogenMessage, 0.3f);
                        }
                        else if (!string.IsNullOrWhiteSpace(nitrogenMessage))
                        {
                            inventoryUI.ShowStatusMessage(nitrogenMessage, 2f);
                        }
                    }

                    if (keyboard.nKey.wasPressedThisFrame && currentCart.IsConnected)
                    {
                        currentCart.Disconnect();
                        inventoryUI.ShowStatusMessage("Nitrogen hose disconnected.", 2f);
                    }
                }

                inventoryUI.SetInteractionPrompt(currentCart.InteractionText);
            }
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
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
