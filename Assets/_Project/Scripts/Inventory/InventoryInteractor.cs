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
                SetCurrentPickup(null);
                return;
            }

            FindPickupTarget();

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
                SetCurrentPickup(null);
            }
            else
            {
                inventoryUI.ShowStatusMessage("Inventory is full");
            }
        }

        private void FindPickupTarget()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    interactionDistance,
                    interactionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                SetCurrentPickup(null);
                return;
            }

            InventoryPickup pickup = hit.collider.GetComponentInParent<InventoryPickup>();
            SetCurrentPickup(pickup);
        }

        private void SetCurrentPickup(InventoryPickup pickup)
        {
            currentPickup = pickup;

            string prompt = currentPickup != null
                ? currentPickup.InteractionText
                : string.Empty;

            inventoryUI.SetInteractionPrompt(prompt);
        }

        private void OnDisable()
        {
            if (inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.5f, interactionDistance);
        }
    }
}
