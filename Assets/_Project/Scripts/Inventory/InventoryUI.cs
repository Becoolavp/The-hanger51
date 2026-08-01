using System.Collections.Generic;
using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hanger51.Inventory
{
    public sealed class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Text interactionPromptText;
        [SerializeField] private Text statusText;
        [SerializeField] private List<InventorySlotView> slotViews = new List<InventorySlotView>();

        [Header("Controls")]
        [SerializeField] private bool openWithIKey = true;

        private float statusMessageClearTime;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<PlayerInventory>();
            }

            if (firstPersonController == null)
            {
                firstPersonController = FindFirstObjectByType<FirstPersonController>();
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }

            SetInteractionPrompt(string.Empty);
            ClearStatusMessage();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshSlots;
            }

            RefreshSlots();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshSlots;
            }

            if (isOpen && firstPersonController != null)
            {
                firstPersonController.SetExternalInputBlocked(false);
            }

            isOpen = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (openWithIKey && keyboard.iKey.wasPressedThisFrame)
                {
                    SetInventoryOpen(!isOpen);
                }
                else if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
                {
                    SetInventoryOpen(false);
                }
            }

            if (statusMessageClearTime > 0f && Time.unscaledTime >= statusMessageClearTime)
            {
                ClearStatusMessage();
            }
        }

        public void SetInventoryOpen(bool shouldOpen)
        {
            isOpen = shouldOpen;

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isOpen);
            }

            if (firstPersonController != null)
            {
                firstPersonController.SetExternalInputBlocked(isOpen);
            }

            if (isOpen)
            {
                SetInteractionPrompt(string.Empty);
                RefreshSlots();
            }
        }

        public void SetInteractionPrompt(string message)
        {
            if (interactionPromptText == null)
            {
                return;
            }

            interactionPromptText.text = isOpen ? string.Empty : message;
            interactionPromptText.gameObject.SetActive(!string.IsNullOrWhiteSpace(interactionPromptText.text));
        }

        public void ShowStatusMessage(string message, float duration = 1.5f)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            statusMessageClearTime = Time.unscaledTime + Mathf.Max(0.1f, duration);
        }

        private void ClearStatusMessage()
        {
            statusMessageClearTime = 0f;

            if (statusText == null)
            {
                return;
            }

            statusText.text = string.Empty;
            statusText.gameObject.SetActive(false);
        }

        private void RefreshSlots()
        {
            if (inventory == null)
            {
                return;
            }

            IReadOnlyList<InventorySlotData> slots = inventory.Slots;

            for (int index = 0; index < slotViews.Count; index++)
            {
                InventorySlotData slot = index < slots.Count ? slots[index] : null;
                if (slotViews[index] != null)
                {
                    slotViews[index].Display(slot);
                }
            }
        }
    }
}
