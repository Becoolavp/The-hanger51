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
        [SerializeField, Min(1f)] private float interactionDistance = 6f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private P51LandingGearServiceTarget currentTarget;
        private P51NitrogenCartController currentCart;

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
                CancelTargetHold();
                return;
            }

            FindCandidate(out P51LandingGearServiceTarget target, out P51NitrogenCartController cart);
            if (target != currentTarget)
            {
                CancelTargetHold();
                currentTarget = target;
            }
            currentCart = cart;

            Keyboard keyboard = Keyboard.current;
            if (currentTarget != null)
            {
                bool holdE = keyboard != null && keyboard.eKey.isPressed;
                bool holdR = keyboard != null && keyboard.rKey.isPressed;
                if (currentTarget.ProcessInteraction(
                        holdE,
                        holdR,
                        Time.deltaTime,
                        out string serviceMessage)
                    && !string.IsNullOrWhiteSpace(serviceMessage))
                {
                    inventoryUI.ShowStatusMessage(serviceMessage, 3f);
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
            out P51NitrogenCartController bestCart)
        {
            bestTarget = null;
            bestCart = null;
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
            }

            if (bestTarget != null && targetDistance <= cartDistance + 0.10f)
            {
                bestCart = null;
            }
            else if (bestCart != null)
            {
                bestTarget = null;
            }
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
        }

        private void CancelTargetHold()
        {
            currentTarget?.CancelHold();
        }

        private void OnDisable()
        {
            CancelTargetHold();
            currentTarget = null;
            currentCart = null;
            if (inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
