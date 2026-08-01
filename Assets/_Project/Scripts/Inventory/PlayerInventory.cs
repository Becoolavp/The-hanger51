using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Inventory
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int slotCount = 8;
        [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>();
        [SerializeField] private InventoryItemDefinition equippedItem;

        public event Action InventoryChanged;

        public IReadOnlyList<InventorySlotData> Slots => slots;
        public int SlotCount => slotCount;
        public InventoryItemDefinition EquippedItem => equippedItem;

        private void Awake()
        {
            EnsureSlotCount();
            RemoveInvalidEquippedItem();
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
                NotifyInventoryChanged();
            }

            return remaining;
        }

        public bool ToggleEquipSlot(int slotIndex)
        {
            InventorySlotData slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                return false;
            }

            equippedItem = equippedItem == slot.Item ? null : slot.Item;
            NotifyInventoryChanged();
            return true;
        }

        public void Unequip()
        {
            if (equippedItem == null)
            {
                return;
            }

            equippedItem = null;
            NotifyInventoryChanged();
        }

        public bool TryRemoveFromSlot(
            int slotIndex,
            int requestedQuantity,
            out InventoryItemDefinition removedItem,
            out int removedQuantity)
        {
            removedItem = null;
            removedQuantity = 0;

            InventorySlotData slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || requestedQuantity <= 0)
            {
                return false;
            }

            removedItem = slot.Item;
            removedQuantity = Mathf.Min(requestedQuantity, slot.Quantity);
            slot.Set(removedItem, slot.Quantity - removedQuantity);

            RemoveInvalidEquippedItem();
            NotifyInventoryChanged();
            return true;
        }

        public InventorySlotData GetSlot(int slotIndex)
        {
            EnsureSlotCount();

            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return null;
            }

            return slots[slotIndex];
        }

        public void ClearInventory()
        {
            EnsureSlotCount();

            bool changed = equippedItem != null;
            equippedItem = null;

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
                NotifyInventoryChanged();
            }
        }

        private bool ContainsItem(InventoryItemDefinition item)
        {
            if (item == null)
            {
                return false;
            }

            for (int index = 0; index < slots.Count; index++)
            {
                InventorySlotData slot = slots[index];
                if (!slot.IsEmpty && slot.Item == item)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveInvalidEquippedItem()
        {
            if (equippedItem != null && !ContainsItem(equippedItem))
            {
                equippedItem = null;
            }
        }

        private void NotifyInventoryChanged()
        {
            InventoryChanged?.Invoke();
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
            RemoveInvalidEquippedItem();
        }
    }
}
