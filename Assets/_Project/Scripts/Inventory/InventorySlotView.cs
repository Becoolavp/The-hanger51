using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hanger51.Inventory
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image itemColorImage;
        [SerializeField] private Text itemNameText;
        [SerializeField] private GameObject quantityBadge;
        [SerializeField] private Text quantityText;

        private int slotIndex = -1;
        private Action<int> selectionCallback;

        public int SlotIndex => slotIndex;

        public void Configure(int configuredSlotIndex, Action<int> onSelected)
        {
            slotIndex = configuredSlotIndex;
            selectionCallback = onSelected;

            if (selectButton == null)
            {
                return;
            }

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(NotifySelected);
        }

        public void Display(InventorySlotData slot, bool isSelected)
        {
            bool hasItem = slot != null && !slot.IsEmpty;

            if (slotBackground != null)
            {
                slotBackground.color = isSelected
                    ? new Color(0.24f, 0.44f, 0.62f, 1f)
                    : new Color(0.11f, 0.13f, 0.17f, 1f);
            }

            if (selectButton != null)
            {
                selectButton.interactable = hasItem;
            }

            if (itemColorImage != null)
            {
                itemColorImage.enabled = hasItem;
                itemColorImage.color = hasItem
                    ? slot.Item.PlaceholderColor
                    : Color.clear;
            }

            if (itemNameText != null)
            {
                if (hasItem)
                {
                    string condition = slot.GetConditionSummary();
                    itemNameText.text = string.IsNullOrWhiteSpace(condition)
                        ? slot.Item.DisplayName
                        : $"{slot.Item.DisplayName}\n{condition}";
                }
                else
                {
                    itemNameText.text = "Empty";
                }

                itemNameText.color = hasItem
                    ? Color.white
                    : new Color(0.58f, 0.62f, 0.68f, 1f);
            }

            if (quantityBadge != null)
            {
                quantityBadge.SetActive(hasItem);
            }

            if (quantityText != null)
            {
                quantityText.text = hasItem ? $"x{slot.Quantity}" : string.Empty;
            }
        }

        private void NotifySelected()
        {
            if (slotIndex >= 0)
            {
                selectionCallback?.Invoke(slotIndex);
            }
        }
    }
}
