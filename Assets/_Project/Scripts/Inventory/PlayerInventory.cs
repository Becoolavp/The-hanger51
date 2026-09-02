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
        public EnginePartConditionData EquippedCondition =>
            PeekFirstCondition(equippedItem);

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

            List<EnginePartConditionData> defaults = null;
            if (EnginePartConditionData.IsTrackedItem(item))
            {
                defaults = new List<EnginePartConditionData>(quantity);
                EnginePartConditionData transferred = quantity == 1
                    ? EnginePartConditionTransferContext.PeekForItem(item)
                    : null;
                for (int index = 0; index < quantity; index++)
                {
                    defaults.Add(
                        transferred != null && index == 0
                            ? transferred.Clone()
                            : EnginePartConditionData.CreateDefaultForItem(item));
                }
            }

            return AddItemInstances(item, quantity, defaults);
        }

        public int AddConditionedItem(
            InventoryItemDefinition item,
            EnginePartConditionData condition)
        {
            List<EnginePartConditionData> conditions =
                new List<EnginePartConditionData>(1)
                {
                    condition != null
                        ? condition.Clone()
                        : EnginePartConditionData.CreateDefaultForItem(item)
                };
            return AddItemInstances(item, 1, conditions);
        }

        public int AddItemInstances(
            InventoryItemDefinition item,
            int quantity,
            IReadOnlyList<EnginePartConditionData> conditions)
        {
            if (item == null || quantity <= 0)
            {
                return Mathf.Max(0, quantity);
            }

            EnsureSlotCount();
            int remaining = quantity;
            int sourceIndex = 0;
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

                int requested = Mathf.Min(availableSpace, remaining);
                int added = slot.Add(item, requested, conditions, sourceIndex);
                remaining -= added;
                sourceIndex += added;
                changed |= added > 0;
            }

            for (int index = 0; index < slots.Count && remaining > 0; index++)
            {
                InventorySlotData slot = slots[index];
                if (!slot.IsEmpty)
                {
                    continue;
                }

                int requested = Mathf.Min(item.MaxStackSize, remaining);
                int added = slot.Add(item, requested, conditions, sourceIndex);
                remaining -= added;
                sourceIndex += added;
                changed |= added > 0;
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
            if (slot == null || slot.IsEmpty || !slot.Item.CanEquip)
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
            return TryRemoveFromSlot(
                slotIndex,
                requestedQuantity,
                out removedItem,
                out removedQuantity,
                out _);
        }

        public bool TryRemoveFromSlot(
            int slotIndex,
            int requestedQuantity,
            out InventoryItemDefinition removedItem,
            out int removedQuantity,
            out List<EnginePartConditionData> removedConditions)
        {
            removedItem = null;
            removedQuantity = 0;
            removedConditions = new List<EnginePartConditionData>();

            InventorySlotData slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || requestedQuantity <= 0)
            {
                return false;
            }

            if (!slot.Remove(
                    requestedQuantity,
                    out removedItem,
                    out removedQuantity,
                    out removedConditions))
            {
                return false;
            }

            RemoveInvalidEquippedItem();
            NotifyInventoryChanged();
            return true;
        }

        public bool TryRemoveFirstItem(
            InventoryItemDefinition requiredItem,
            out EnginePartConditionData removedCondition)
        {
            removedCondition = null;
            if (requiredItem == null)
            {
                return false;
            }

            EnsureSlotCount();
            for (int index = 0; index < slots.Count; index++)
            {
                InventorySlotData slot = slots[index];
                if (slot == null || slot.IsEmpty || slot.Item != requiredItem)
                {
                    continue;
                }

                if (!TryRemoveFromSlot(
                        index,
                        1,
                        out InventoryItemDefinition removedItem,
                        out int removedQuantity,
                        out List<EnginePartConditionData> conditions)
                    || removedItem != requiredItem
                    || removedQuantity != 1)
                {
                    return false;
                }

                removedCondition = conditions.Count > 0
                    ? conditions[0]
                    : EnginePartConditionData.CreateDefaultForItem(requiredItem);
                return true;
            }

            return false;
        }

        public EnginePartConditionData PeekFirstCondition(
            InventoryItemDefinition requiredItem)
        {
            if (requiredItem == null)
            {
                return null;
            }

            EnsureSlotCount();
            for (int index = 0; index < slots.Count; index++)
            {
                InventorySlotData slot = slots[index];
                if (slot != null && !slot.IsEmpty && slot.Item == requiredItem)
                {
                    return slot.PeekCondition();
                }
            }

            return null;
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
            if (equippedItem != null
                && (!equippedItem.CanEquip || !ContainsItem(equippedItem)))
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
                slots[index].EnsureConditionCount();
            }
        }

        private void OnValidate()
        {
            EnsureSlotCount();
            RemoveInvalidEquippedItem();
        }
    }
}
