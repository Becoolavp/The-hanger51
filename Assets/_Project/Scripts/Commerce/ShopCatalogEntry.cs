using System;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Commerce
{
    public enum ShopProductKind
    {
        InventoryItem,
        CompleteAssembly
    }

    [Serializable]
    public sealed class ShopCatalogEntry
    {
        [SerializeField] private string productId = "new-product";
        [SerializeField] private string category = "Parts";
        [SerializeField] private string displayName = "New Product";
        [SerializeField, TextArea(2, 5)] private string description = string.Empty;
        [SerializeField, Min(0)] private int price = 100;
        [SerializeField] private ShopProductKind productKind = ShopProductKind.InventoryItem;
        [SerializeField] private InventoryItemDefinition inventoryItem;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private GameObject assemblyTemplate;

        public string ProductId => productId;
        public string Category => category;
        public string DisplayName => displayName;
        public string Description => description;
        public int Price => price;
        public ShopProductKind ProductKind => productKind;
        public InventoryItemDefinition InventoryItem => inventoryItem;
        public int Quantity => quantity;
        public GameObject AssemblyTemplate => assemblyTemplate;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(productId)
            && !string.IsNullOrWhiteSpace(displayName)
            && price >= 0
            && ((productKind == ShopProductKind.InventoryItem && inventoryItem != null)
                || (productKind == ShopProductKind.CompleteAssembly && assemblyTemplate != null));

        public string DeliveryDescription => productKind == ShopProductKind.InventoryItem
            ? $"Crated inventory delivery — quantity {quantity}"
            : "Large crated assembly — delivered complete and serviceable";

        public void Configure(
            string configuredProductId,
            string configuredCategory,
            string configuredDisplayName,
            string configuredDescription,
            int configuredPrice,
            ShopProductKind configuredKind,
            InventoryItemDefinition configuredInventoryItem,
            int configuredQuantity,
            GameObject configuredAssemblyTemplate)
        {
            productId = string.IsNullOrWhiteSpace(configuredProductId)
                ? "new-product"
                : configuredProductId.Trim();
            category = string.IsNullOrWhiteSpace(configuredCategory)
                ? "Parts"
                : configuredCategory.Trim();
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
                ? "New Product"
                : configuredDisplayName.Trim();
            description = configuredDescription ?? string.Empty;
            price = Mathf.Max(0, configuredPrice);
            productKind = configuredKind;
            inventoryItem = configuredInventoryItem;
            quantity = Mathf.Max(1, configuredQuantity);
            assemblyTemplate = configuredAssemblyTemplate;
        }
    }
}
