using System;
using Hanger51.Aircraft;
using Hanger51.EngineAssembly;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Inventory
{
    public sealed class InventoryInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private InventoryPickup currentPickup;
        private EngineAssemblyStation currentAssemblyStation;
        private EngineAssemblyInteractionTarget currentAssemblyTarget;
        private P51BareRimServiceTarget currentBareRim;

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }

            if (playerCamera == null || inventory == null || inventoryUI == null)
            {
                Debug.LogError(
                    $"{nameof(InventoryInteractor)} on '{name}' is missing required references.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (inventoryUI.IsOpen)
            {
                CancelCurrentAssemblyHold();
                CancelCurrentRimHold();
                currentPickup = null;
                return;
            }

            Keyboard keyboard = Keyboard.current;

            P51BareRimServiceTarget aimedRim = FindBareRimTarget();
            if (aimedRim != null)
            {
                if (currentBareRim != aimedRim)
                {
                    CancelCurrentRimHold();
                    currentBareRim = aimedRim;
                }

                CancelCurrentAssemblyHold();
                currentPickup = null;
                currentAssemblyStation = null;
                currentAssemblyTarget = null;
                inventoryUI.SetAssemblyStation(null);

                bool holdingE = keyboard != null && keyboard.eKey.isPressed;
                bool pressedE = keyboard != null && keyboard.eKey.wasPressedThisFrame;
                if (currentBareRim.ProcessInteraction(
                        inventory,
                        holdingE,
                        pressedE,
                        Time.deltaTime,
                        out string rimMessage)
                    && !string.IsNullOrWhiteSpace(rimMessage))
                {
                    inventoryUI.ShowStatusMessage(rimMessage, 3.5f);

                    if (rimMessage.StartsWith("Picked up ", StringComparison.Ordinal))
                    {
                        currentBareRim = null;
                        inventoryUI.SetInteractionPrompt(string.Empty);
                        return;
                    }
                }

                if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                {
                    inventoryUI.ShowStatusMessage(currentBareRim.Inspect(), 4f);
                }

                inventoryUI.SetInteractionPrompt(currentBareRim.GetInteractionText(inventory));
                return;
            }

            if (currentBareRim != null)
            {
                CancelCurrentRimHold();
                currentBareRim = null;
            }

            FindInteractionTarget();

            if (currentAssemblyTarget != null)
            {
                bool holdingE = keyboard != null && keyboard.eKey.isPressed;
                bool holdingR = keyboard != null && keyboard.rKey.isPressed;

                if (currentAssemblyTarget.ProcessInteraction(
                        inventory,
                        holdingE,
                        holdingR,
                        Time.deltaTime,
                        out string assemblyMessage))
                {
                    inventoryUI.ShowStatusMessage(assemblyMessage, 2f);
                }

                inventoryUI.SetInteractionPrompt(currentAssemblyTarget.InteractionText);
                return;
            }

            if (currentAssemblyStation != null)
            {
                EngineAssemblyRemovalController removalController =
                    currentAssemblyStation.GetComponent<EngineAssemblyRemovalController>();
                EnginePartConditionPersistenceController persistence =
                    currentAssemblyStation.GetComponent<EnginePartConditionPersistenceController>();
                bool holdingR = keyboard != null && keyboard.rKey.isPressed;
                bool transferBlockCondition = removalController != null
                    && removalController.CanRemoveEngineBlock;

                if (transferBlockCondition)
                {
                    EnginePartConditionTransferContext.Begin(
                        persistence != null ? persistence.CaptureEngineBlock() : null);
                }

                try
                {
                    if (removalController != null
                        && removalController.ProcessEngineRemovalHold(
                            inventory,
                            holdingR,
                            Time.deltaTime,
                            out string removalMessage))
                    {
                        inventoryUI.ShowStatusMessage(removalMessage, 2f);
                    }
                }
                finally
                {
                    if (transferBlockCondition)
                    {
                        EnginePartConditionTransferContext.End();
                    }
                }

                string stationPrompt = removalController != null
                    && removalController.CanRemoveEngineBlock
                        ? removalController.EngineRemovalInteractionText
                        : currentAssemblyStation.InteractionText;
                inventoryUI.SetInteractionPrompt(stationPrompt);
                return;
            }

            if (currentPickup == null
                || currentPickup.IsPickupBlocked
                || keyboard == null
                || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            string itemName = currentPickup.Item != null
                ? currentPickup.Item.DisplayName
                : "item";

            bool pickedUp = currentPickup.TryPickup(inventory);
            if (pickedUp)
            {
                inventoryUI.ShowStatusMessage($"Picked up {itemName}");
                SetInteractionTarget(null, null, null);
            }
            else
            {
                inventoryUI.ShowStatusMessage("Inventory is full");
            }
        }

        private P51BareRimServiceTarget FindBareRimTarget()
        {
            if (playerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Max(interactionDistance, 6f),
                interactionLayers,
                QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return null;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                P51BareRimServiceTarget target =
                    collider.GetComponentInParent<P51BareRimServiceTarget>();
                if (target != null && target.IsReady)
                {
                    return target;
                }
            }

            return null;
        }

        private void FindInteractionTarget()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            if (hits.Length == 0)
            {
                SetInteractionTarget(null, null, null);
                return;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            InventoryPickup nearestPickup = null;
            float nearestPickupDistance = float.PositiveInfinity;
            EngineAssemblyInteractionTarget bestAssemblyTarget = null;
            EngineAssemblyStation bestTargetStation = null;
            float bestTargetDistance = float.PositiveInfinity;
            int bestTargetPriority = -1;
            EngineAssemblyStation nearestStation = null;

            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];

                InventoryPickup pickup = hit.collider.GetComponentInParent<InventoryPickup>();
                if (pickup != null && nearestPickup == null)
                {
                    nearestPickup = pickup;
                    nearestPickupDistance = hit.distance;
                }

                EngineAssemblyInteractionTarget assemblyTarget =
                    hit.collider.GetComponentInParent<EngineAssemblyInteractionTarget>();
                if (assemblyTarget != null && assemblyTarget.CanInteract)
                {
                    int priority = GetTargetPriority(assemblyTarget.InteractionKind);
                    if (priority > bestTargetPriority
                        || (priority == bestTargetPriority && hit.distance < bestTargetDistance))
                    {
                        bestAssemblyTarget = assemblyTarget;
                        bestTargetStation =
                            assemblyTarget.GetComponentInParent<EngineAssemblyStation>();
                        bestTargetDistance = hit.distance;
                        bestTargetPriority = priority;
                    }
                }

                if (nearestStation == null)
                {
                    nearestStation = hit.collider.GetComponentInParent<EngineAssemblyStation>();
                }
            }

            if (nearestPickup != null
                && (bestAssemblyTarget == null
                    || nearestPickupDistance + 0.05f < bestTargetDistance))
            {
                SetInteractionTarget(nearestPickup, null, null);
                return;
            }

            if (bestAssemblyTarget != null)
            {
                SetInteractionTarget(null, bestTargetStation, bestAssemblyTarget);
                return;
            }

            SetInteractionTarget(null, nearestStation, null);
        }

        private static int GetTargetPriority(EngineAssemblyInteractionKind kind)
        {
            switch (kind)
            {
                case EngineAssemblyInteractionKind.SparkPlug:
                    return 3;
                case EngineAssemblyInteractionKind.CoverBolt:
                    return 2;
                case EngineAssemblyInteractionKind.CoverPlacement:
                    return 1;
                default:
                    return 0;
            }
        }

        private void SetInteractionTarget(
            InventoryPickup pickup,
            EngineAssemblyStation assemblyStation,
            EngineAssemblyInteractionTarget assemblyTarget)
        {
            if (currentAssemblyTarget != assemblyTarget
                || currentAssemblyStation != assemblyStation)
            {
                CancelCurrentAssemblyHold();
            }

            currentPickup = pickup;
            currentAssemblyStation = assemblyStation;
            currentAssemblyTarget = assemblyTarget;
            inventoryUI.SetAssemblyStation(currentAssemblyStation);

            inventoryUI.SetInteractionPrompt(GetCurrentPrompt());
        }

        private string GetCurrentPrompt()
        {
            if (currentPickup != null)
            {
                return currentPickup.InteractionText;
            }

            if (currentAssemblyTarget != null)
            {
                return currentAssemblyTarget.InteractionText;
            }

            if (currentAssemblyStation == null)
            {
                return string.Empty;
            }

            EngineAssemblyRemovalController removalController =
                currentAssemblyStation.GetComponent<EngineAssemblyRemovalController>();

            return removalController != null && removalController.CanRemoveEngineBlock
                ? removalController.EngineRemovalInteractionText
                : currentAssemblyStation.InteractionText;
        }

        private void CancelCurrentRimHold()
        {
            currentBareRim?.CancelHold();
        }

        private void CancelCurrentAssemblyHold()
        {
            if (currentAssemblyTarget != null)
            {
                currentAssemblyTarget.CancelHold();
            }

            if (currentAssemblyStation != null)
            {
                EngineAssemblyRemovalController removalController =
                    currentAssemblyStation.GetComponent<EngineAssemblyRemovalController>();
                removalController?.CancelEngineRemovalHold();
            }
        }

        private void OnDisable()
        {
            CancelCurrentAssemblyHold();
            CancelCurrentRimHold();
            EnginePartConditionTransferContext.Clear();

            if (inventoryUI != null)
            {
                inventoryUI.SetAssemblyStation(null);
                inventoryUI.SetInteractionPrompt(string.Empty);
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.5f, interactionDistance);
        }
    }
}
