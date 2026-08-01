using UnityEngine;
using UnityEngine.UI;

namespace Hanger51.Inventory
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image itemColorImage;
        [SerializeField] private Text itemNameText;
        [SerializeField] private Text quantityText;

        public void Display(InventorySlotData slot)
        {
            bool hasItem = slot != null && !slot.IsEmpty;

            if (itemColorImage != null)
            {
                itemColorImage.enabled = hasItem;
                itemColorImage.color = hasItem
                    ? slot.Item.PlaceholderColor
                    : Color.clear;
            }

            if (itemNameText != null)
            {
                itemNameText.text = hasItem ? slot.Item.DisplayName : "Empty";
            }

            if (quantityText != null)
            {
                quantityText.text = hasItem && slot.Quantity > 1
                    ? $"x{slot.Quantity}"
                    : string.Empty;
            }
        }
    }
}
