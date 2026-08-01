using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Inventory
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int slotCount = 8;
        [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>();

        public event Action InventoryChanged;

        public IReadOnlyList<InventorySlotData> Slots => slots;
        public int SlotCount => slotCount;

        private void Awake()
        {
            EnsureSlotCount();
        }

        public int AddItem(InventoryItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return Mathf.Max(0, quantity);
            }

            EnsureSlotCount();

            int remaining = quantity;
            bool changed = false;

            for (int index = 0; index < slots.Count && remaining > 0; index++)
            {
                InventorySlotData slot = slots[index];
                if (slot.IsEmpty || slot.Item != item)
                {
                    continue;
                }

                int availableSpace = item.MaxStackSize - slot.Quantity;
                if (availableSpace <= 0)
                {
                    continue;
                }

                int amountToAdd = Mathf.Min(availableSpace, remaining);
                slot.Set(item, slot.Quantity + amountToAdd);
                remaining -= amountToAdd;
                changed = true;
            }

            for (int index = 0; index < slots.Count && remaining > 0; index++)
            {
                InventorySlotData slot = slots[index];
                if (!slot.IsEmpty)
                {
                    continue;
                }

                int amountToAdd = Mathf.Min(item.MaxStackSize, remaining);
                slot.Set(item, amountToAdd);
                remaining -= amountToAdd;
                changed = true;
            }

            if (changed)
            {
                InventoryChanged?.Invoke();
            }

            return remaining;
        }

        public void ClearInventory()
        {
            EnsureSlotCount();

            bool changed = false;
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].IsEmpty)
                {
                    continue;
                }

                slots[index].Clear();
                changed = true;
            }

            if (changed)
            {
                InventoryChanged?.Invoke();
            }
        }

        private void EnsureSlotCount()
        {
            slotCount = Mathf.Max(1, slotCount);

            while (slots.Count < slotCount)
            {
                slots.Add(new InventorySlotData());
            }

            if (slots.Count > slotCount)
            {
                slots.RemoveRange(slotCount, slots.Count - slotCount);
            }

            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index] == null)
                {
                    slots[index] = new InventorySlotData();
                }
            }
        }

        private void OnValidate()
        {
            EnsureSlotCount();
        }
    }
}
