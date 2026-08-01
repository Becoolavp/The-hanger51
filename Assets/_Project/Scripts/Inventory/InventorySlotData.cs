using System;
using UnityEngine;

namespace Hanger51.Inventory
{
    [Serializable]
    public sealed class InventorySlotData
    {
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField, Min(0)] private int quantity;

        public InventoryItemDefinition Item => item;
        public int Quantity => quantity;
        public bool IsEmpty => item == null || quantity <= 0;

        internal void Set(InventoryItemDefinition newItem, int newQuantity)
        {
            item = newItem;
            quantity = Mathf.Max(0, newQuantity);

            if (quantity == 0)
            {
                item = null;
            }
        }

        internal void Clear()
        {
            item = null;
            quantity = 0;
        }
    }
}
