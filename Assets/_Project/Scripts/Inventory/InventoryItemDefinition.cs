using UnityEngine;

namespace Hanger51.Inventory
{
    [CreateAssetMenu(
        fileName = "InventoryItem",
        menuName = "Hanger 51/Inventory/Item Definition")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "new-item";
        [SerializeField] private string displayName = "New Item";
        [SerializeField, TextArea(2, 4)] private string description = string.Empty;
        [SerializeField, Min(1)] private int maxStackSize = 10;
        [SerializeField] private Color placeholderColor = Color.white;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public int MaxStackSize => maxStackSize;
        public Color PlaceholderColor => placeholderColor;

        private void OnValidate()
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? name.ToLowerInvariant() : itemId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            maxStackSize = Mathf.Max(1, maxStackSize);
        }
    }
}
