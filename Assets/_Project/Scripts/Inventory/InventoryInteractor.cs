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
                currentPickup = null;
                return;
            }

            FindInteractionTarget();

            Keyboard keyboard = Keyboard.current;
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
                SetInteractionTarget(null, null);
            }
            else
            {
                inventoryUI.ShowStatusMessage("Inventory is full");
            }
        }

        private void FindInteractionTarget()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    interactionDistance,
                    interactionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                SetInteractionTarget(null, null);
                return;
            }

            InventoryPickup pickup = hit.collider.GetComponentInParent<InventoryPickup>();
            if (pickup != null)
            {
                SetInteractionTarget(pickup, null);
                return;
            }

            EngineAssemblyStation assemblyStation =
                hit.collider.GetComponentInParent<EngineAssemblyStation>();
            SetInteractionTarget(null, assemblyStation);
        }

        private void SetInteractionTarget(
            InventoryPickup pickup,
            EngineAssemblyStation assemblyStation)
        {
            currentPickup = pickup;
            currentAssemblyStation = assemblyStation;
            inventoryUI.SetAssemblyStation(currentAssemblyStation);

            string prompt = currentPickup != null
                ? currentPickup.InteractionText
                : currentAssemblyStation != null
                    ? currentAssemblyStation.InteractionText
                    : string.Empty;

            inventoryUI.SetInteractionPrompt(prompt);
        }

        private void OnDisable()
        {
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
