using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Inventory
{
    [Serializable]
    public sealed class InventorySlotData
    {
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField, Min(0)] private int quantity;
        [SerializeField] private List<EnginePartConditionData> conditionInstances =
            new List<EnginePartConditionData>();

        public InventoryItemDefinition Item => item;
        public int Quantity => quantity;
        public bool IsEmpty => item == null || quantity <= 0;
        public IReadOnlyList<EnginePartConditionData> ConditionInstances => conditionInstances;
        public bool CarriesCondition => !IsEmpty
            && EnginePartConditionData.IsTrackedItem(item);

        public EnginePartConditionData PeekCondition()
        {
            EnsureConditionCount();
            if (!CarriesCondition || conditionInstances.Count == 0)
            {
                return null;
            }

            EnginePartConditionData condition =
                conditionInstances[conditionInstances.Count - 1];
            return condition != null ? condition.Clone() : null;
        }

        public string GetConditionSummary()
        {
            EnsureConditionCount();
            if (!CarriesCondition || conditionInstances.Count == 0)
            {
                return string.Empty;
            }

            float minimum = 100f;
            float maximum = 0f;
            bool cracked = false;
            EnginePartConditionData first = null;

            for (int index = 0; index < conditionInstances.Count; index++)
            {
                EnginePartConditionData condition = conditionInstances[index];
                if (condition == null)
                {
                    continue;
                }

                first ??= condition;
                minimum = Mathf.Min(minimum, condition.Health);
                maximum = Mathf.Max(maximum, condition.Health);
                cracked |= condition.IsCracked;
            }

            if (first == null)
            {
                return string.Empty;
            }

            if (first.Kind == EnginePartConditionKind.EngineBlock
                && conditionInstances.Count == 1)
            {
                return first.GetConditionSummary();
            }

            string range = Mathf.Abs(maximum - minimum) <= 0.05f
                ? $"{minimum:F1}%"
                : $"{minimum:F1}–{maximum:F1}%";
            return cracked ? $"{range} — includes CRACKED" : range;
        }

        internal void Set(InventoryItemDefinition newItem, int newQuantity)
        {
            bool itemChanged = item != newItem;
            item = newItem;
            quantity = Mathf.Max(0, newQuantity);

            if (itemChanged)
            {
                conditionInstances.Clear();
            }

            if (quantity == 0)
            {
                item = null;
                conditionInstances.Clear();
                return;
            }

            EnsureConditionCount();
        }

        internal int Add(
            InventoryItemDefinition newItem,
            int requestedQuantity,
            IReadOnlyList<EnginePartConditionData> requestedConditions,
            int requestedConditionStartIndex = 0)
        {
            if (newItem == null || requestedQuantity <= 0)
            {
                return 0;
            }

            if (!IsEmpty && item != newItem)
            {
                return 0;
            }

            if (IsEmpty)
            {
                item = newItem;
                quantity = 0;
                conditionInstances.Clear();
            }

            int added = 0;
            bool tracked = EnginePartConditionData.IsTrackedItem(newItem);
            for (int index = 0; index < requestedQuantity; index++)
            {
                quantity++;
                if (tracked)
                {
                    int conditionIndex = requestedConditionStartIndex + index;
                    EnginePartConditionData source = requestedConditions != null
                        && conditionIndex >= 0
                        && conditionIndex < requestedConditions.Count
                            ? requestedConditions[conditionIndex]
                            : null;
                    EnginePartConditionData condition = source != null
                        ? source.Clone()
                        : EnginePartConditionData.CreateDefaultForItem(newItem);
                    condition?.EnsureValid();
                    conditionInstances.Add(condition);
                }
                added++;
            }

            EnsureConditionCount();
            return added;
        }

        internal bool Remove(
            int requestedQuantity,
            out InventoryItemDefinition removedItem,
            out int removedQuantity,
            out List<EnginePartConditionData> removedConditions)
        {
            removedItem = null;
            removedQuantity = 0;
            removedConditions = new List<EnginePartConditionData>();

            if (IsEmpty || requestedQuantity <= 0)
            {
                return false;
            }

            EnsureConditionCount();
            removedItem = item;
            removedQuantity = Mathf.Min(requestedQuantity, quantity);

            if (CarriesCondition)
            {
                for (int index = 0; index < removedQuantity; index++)
                {
                    int lastIndex = conditionInstances.Count - 1;
                    EnginePartConditionData condition = lastIndex >= 0
                        ? conditionInstances[lastIndex]
                        : EnginePartConditionData.CreateDefaultForItem(item);
                    removedConditions.Add(condition != null ? condition.Clone() : null);
                    if (lastIndex >= 0)
                    {
                        conditionInstances.RemoveAt(lastIndex);
                    }
                }
            }

            quantity -= removedQuantity;
            if (quantity <= 0)
            {
                Clear();
            }
            else
            {
                EnsureConditionCount();
            }

            return true;
        }

        internal void Clear()
        {
            item = null;
            quantity = 0;
            conditionInstances.Clear();
        }

        internal void EnsureConditionCount()
        {
            if (conditionInstances == null)
            {
                conditionInstances = new List<EnginePartConditionData>();
            }

            if (IsEmpty || !EnginePartConditionData.IsTrackedItem(item))
            {
                conditionInstances.Clear();
                return;
            }

            while (conditionInstances.Count < quantity)
            {
                conditionInstances.Add(
                    EnginePartConditionData.CreateDefaultForItem(item));
            }

            if (conditionInstances.Count > quantity)
            {
                conditionInstances.RemoveRange(
                    quantity,
                    conditionInstances.Count - quantity);
            }

            for (int index = 0; index < conditionInstances.Count; index++)
            {
                if (conditionInstances[index] == null)
                {
                    conditionInstances[index] =
                        EnginePartConditionData.CreateDefaultForItem(item);
                }
                conditionInstances[index]?.EnsureValid();
            }
        }
    }
}
