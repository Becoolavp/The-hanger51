using System;
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
                currentPickup = null;
                return;
            }

            FindInteractionTarget();

            Keyboard keyboard = Keyboard.current;
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
                bool holdingR = keyboard != null && keyboard.rKey.isPressed;

                if (removalController != null
                    && removalController.ProcessEngineRemovalHold(
                        inventory,
                        holdingR,
                        Time.deltaTime,
                        out string removalMessage))
                {
                    inventoryUI.ShowStatusMessage(removalMessage, 2f);
                }

                string stationPrompt = removalController != null
                    && removalController.CanRemoveEngineBlock
                        ? removalController.EngineRemovalInteractionText
                        : currentAssemblyStation.InteractionText;
                inventoryUI.SetInteractionPrompt(stationPrompt);
                return;
            }

            if (currentPickup == null || keyboard == null || !keyboard.eKey.wasPressedThisFrame)
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

        private void FindInteractionTarget()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                SetInteractionTarget(null, null, null);
                return;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            EngineAssemblyStation nearestStation = null;

            for (int index = 0; index < hits.Length; index++)
            {
                InventoryPickup pickup = hits[index].collider.GetComponentInParent<InventoryPickup>();
                if (pickup != null)
                {
                    SetInteractionTarget(pickup, null, null);
                    return;
                }

                EngineAssemblyInteractionTarget assemblyTarget =
                    hits[index].collider.GetComponentInParent<EngineAssemblyInteractionTarget>();
                if (assemblyTarget != null && assemblyTarget.CanInteract)
                {
                    EngineAssemblyStation targetStation =
                        assemblyTarget.GetComponentInParent<EngineAssemblyStation>();
                    SetInteractionTarget(null, targetStation, assemblyTarget);
                    return;
                }

                if (nearestStation == null)
                {
                    nearestStation = hits[index].collider.GetComponentInParent<EngineAssemblyStation>();
                }
            }

            SetInteractionTarget(null, nearestStation, null);
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
