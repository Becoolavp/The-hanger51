using System.Collections.Generic;
using Hanger51.EngineAssembly;
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
        [SerializeField] private InventoryItemDropper itemDropper;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Text interactionPromptText;
        [SerializeField] private Text statusText;
        [SerializeField] private List<InventorySlotView> slotViews = new List<InventorySlotView>();

        [Header("Selected Item")]
        [SerializeField] private Text selectedItemNameText;
        [SerializeField] private Text selectedItemDescriptionText;
        [SerializeField] private Text equippedItemText;
        [SerializeField] private Text installationTargetText;
        [SerializeField] private Button equipButton;
        [SerializeField] private Text equipButtonText;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button installButton;
        [SerializeField] private Text installButtonText;

        [Header("Controls")]
        [SerializeField] private bool openWithIKey = true;

        private float statusMessageClearTime;
        private bool isOpen;
        private int selectedSlotIndex = -1;
        private EngineAssemblyStation activeAssemblyStation;

        public bool IsOpen => isOpen;
        public EngineAssemblyStation ActiveAssemblyStation => activeAssemblyStation;

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

            if (itemDropper == null)
            {
                itemDropper = FindFirstObjectByType<InventoryItemDropper>();
            }

            ConfigureSlotButtons();
            ConfigureActionButtons();

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
                SelectFirstAvailableSlotWhenNeeded();
                RefreshSlots();
            }
        }

        public void SetAssemblyStation(EngineAssemblyStation station)
        {
            if (activeAssemblyStation == station)
            {
                return;
            }

            activeAssemblyStation = station;

            if (isOpen)
            {
                RefreshSelectedItemDetails();
            }
        }

        public void SetInteractionPrompt(string message)
        {
            if (interactionPromptText == null)
            {
                return;
            }

            interactionPromptText.text = isOpen ? string.Empty : message;
            interactionPromptText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(interactionPromptText.text));
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

        private void ConfigureSlotButtons()
        {
            for (int index = 0; index < slotViews.Count; index++)
            {
                if (slotViews[index] != null)
                {
                    slotViews[index].Configure(index, SelectSlot);
                }
            }
        }

        private void ConfigureActionButtons()
        {
            if (equipButton != null)
            {
                equipButton.onClick.RemoveAllListeners();
                equipButton.onClick.AddListener(EquipSelectedItem);
            }

            if (dropButton != null)
            {
                dropButton.onClick.RemoveAllListeners();
                dropButton.onClick.AddListener(DropSelectedItem);
            }

            if (installButton != null)
            {
                installButton.onClick.RemoveAllListeners();
                installButton.onClick.AddListener(InstallSelectedItem);
            }
        }

        private void SelectSlot(int slotIndex)
        {
            if (inventory == null)
            {
                return;
            }

            InventorySlotData slot = inventory.GetSlot(slotIndex);
            selectedSlotIndex = slot != null && !slot.IsEmpty ? slotIndex : -1;
            RefreshSlots();
        }

        private void EquipSelectedItem()
        {
            if (inventory == null || selectedSlotIndex < 0)
            {
                return;
            }

            InventorySlotData selectedSlot = inventory.GetSlot(selectedSlotIndex);
            if (selectedSlot == null || selectedSlot.IsEmpty)
            {
                return;
            }

            if (!selectedSlot.Item.CanEquip)
            {
                ShowStatusMessage($"{selectedSlot.Item.DisplayName} is too large to equip.");
                return;
            }

            bool wasEquipped = inventory.EquippedItem == selectedSlot.Item;
            if (inventory.ToggleEquipSlot(selectedSlotIndex))
            {
                ShowStatusMessage(
                    wasEquipped
                        ? $"Unequipped {selectedSlot.Item.DisplayName}."
                        : $"Equipped {selectedSlot.Item.DisplayName}.");
            }
        }

        private void DropSelectedItem()
        {
            if (itemDropper == null || selectedSlotIndex < 0)
            {
                ShowStatusMessage("Nothing selected to drop.");
                return;
            }

            if (itemDropper.TryDropOne(selectedSlotIndex, out string resultMessage))
            {
                SelectFirstAvailableSlotWhenNeeded();
            }

            ShowStatusMessage(resultMessage);
            RefreshSlots();
        }

        private void InstallSelectedItem()
        {
            if (activeAssemblyStation == null)
            {
                ShowStatusMessage("Aim at the engine stand before opening inventory.");
                return;
            }

            if (selectedSlotIndex < 0)
            {
                ShowStatusMessage("Select an item to install.");
                return;
            }

            if (activeAssemblyStation.TryInstall(
                    inventory,
                    selectedSlotIndex,
                    out string resultMessage))
            {
                SelectFirstAvailableSlotWhenNeeded();
            }

            ShowStatusMessage(resultMessage, 2f);
            RefreshSlots();
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

            ValidateSelectedSlot();

            IReadOnlyList<InventorySlotData> slots = inventory.Slots;

            for (int index = 0; index < slotViews.Count; index++)
            {
                InventorySlotData slot = index < slots.Count ? slots[index] : null;
                if (slotViews[index] != null)
                {
                    slotViews[index].Display(slot, index == selectedSlotIndex);
                }
            }

            RefreshSelectedItemDetails();
        }

        private void RefreshSelectedItemDetails()
        {
            InventorySlotData selectedSlot = inventory != null
                ? inventory.GetSlot(selectedSlotIndex)
                : null;

            bool hasSelection = selectedSlot != null && !selectedSlot.IsEmpty;

            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = hasSelection
                    ? selectedSlot.Item.DisplayName
                    : "Select an item";
            }

            if (selectedItemDescriptionText != null)
            {
                selectedItemDescriptionText.text = hasSelection
                    ? $"{selectedSlot.Item.Description}\n\nQuantity: {selectedSlot.Quantity}"
                    : "Click an occupied slot, then Equip, Drop One, or Install.";
            }

            if (equippedItemText != null)
            {
                equippedItemText.text = inventory != null && inventory.EquippedItem != null
                    ? $"Equipped: {inventory.EquippedItem.DisplayName}"
                    : "Equipped: None";
            }

            if (installationTargetText != null)
            {
                installationTargetText.text = activeAssemblyStation != null
                    ? activeAssemblyStation.ProgressText
                    : "Install target: None — aim at engine stand, then press I";
            }

            bool selectedItemCanEquip = hasSelection && selectedSlot.Item.CanEquip;

            if (equipButton != null)
            {
                equipButton.interactable = selectedItemCanEquip;
            }

            if (dropButton != null)
            {
                dropButton.interactable = hasSelection;
            }

            if (equipButtonText != null)
            {
                bool selectedItemIsEquipped = hasSelection
                    && inventory.EquippedItem == selectedSlot.Item;

                equipButtonText.text = !hasSelection
                    ? "Equip"
                    : !selectedSlot.Item.CanEquip
                        ? "Install Only"
                        : selectedItemIsEquipped
                            ? "Unequip"
                            : "Equip";
            }

            bool canInstall = hasSelection
                && activeAssemblyStation != null
                && activeAssemblyStation.CanInstall(selectedSlot.Item, out _);

            if (installButton != null)
            {
                installButton.interactable = canInstall;
            }

            if (installButtonText != null)
            {
                installButtonText.text = hasSelection && activeAssemblyStation != null
                    ? activeAssemblyStation.GetInstallButtonLabel(selectedSlot.Item)
                    : "Install";
            }
        }

        private void ValidateSelectedSlot()
        {
            InventorySlotData selectedSlot = inventory.GetSlot(selectedSlotIndex);
            if (selectedSlot == null || selectedSlot.IsEmpty)
            {
                selectedSlotIndex = -1;
            }
        }

        private void SelectFirstAvailableSlotWhenNeeded()
        {
            ValidateSelectedSlot();

            if (selectedSlotIndex >= 0 || inventory == null)
            {
                return;
            }

            IReadOnlyList<InventorySlotData> slots = inventory.Slots;
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index] != null && !slots[index].IsEmpty)
                {
                    selectedSlotIndex = index;
                    return;
                }
            }
        }
    }
}
