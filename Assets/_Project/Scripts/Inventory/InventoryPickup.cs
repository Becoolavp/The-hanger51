using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Inventory
{
    [RequireComponent(typeof(Collider))]
    public sealed class InventoryPickup : MonoBehaviour
    {
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private List<EnginePartConditionData> conditionInstances =
            new List<EnginePartConditionData>();

        public InventoryItemDefinition Item => item;
        public int Quantity => quantity;
        public IReadOnlyList<EnginePartConditionData> ConditionInstances => conditionInstances;

        public string InteractionText
        {
            get
            {
                if (item == null)
                {
                    return string.Empty;
                }

                string quantityText = quantity > 1 ? $" x{quantity}" : string.Empty;
                string conditionText = GetConditionSummary();
                return string.IsNullOrWhiteSpace(conditionText)
                    ? $"Press E to pick up {item.DisplayName}{quantityText}"
                    : $"Press E to pick up {item.DisplayName}{quantityText} — {conditionText}";
            }
        }

        public void Configure(InventoryItemDefinition configuredItem, int configuredQuantity)
        {
            item = configuredItem;
            quantity = Mathf.Max(1, configuredQuantity);
            conditionInstances.Clear();
            if (EnginePartConditionData.IsTrackedItem(item))
            {
                for (int index = 0; index < quantity; index++)
                {
                    conditionInstances.Add(
                        EnginePartConditionData.CreateDefaultForItem(item));
                }
            }
            FinalizeConfiguration();
        }

        public void Configure(
            InventoryItemDefinition configuredItem,
            EnginePartConditionData configuredCondition)
        {
            item = configuredItem;
            quantity = 1;
            conditionInstances.Clear();
            if (EnginePartConditionData.IsTrackedItem(item))
            {
                conditionInstances.Add(
                    configuredCondition != null
                        ? configuredCondition.Clone()
                        : EnginePartConditionData.CreateDefaultForItem(item));
            }
            FinalizeConfiguration();
        }

        public void Configure(
            InventoryItemDefinition configuredItem,
            IReadOnlyList<EnginePartConditionData> configuredConditions)
        {
            item = configuredItem;
            conditionInstances.Clear();
            if (configuredConditions != null)
            {
                for (int index = 0; index < configuredConditions.Count; index++)
                {
                    EnginePartConditionData condition = configuredConditions[index];
                    conditionInstances.Add(
                        condition != null
                            ? condition.Clone()
                            : EnginePartConditionData.CreateDefaultForItem(item));
                }
            }

            quantity = Mathf.Max(1, conditionInstances.Count);
            EnsureConditionCount();
            FinalizeConfiguration();
        }

        public bool TryPickup(PlayerInventory inventory)
        {
            if (inventory == null || item == null || quantity <= 0)
            {
                return false;
            }

            EnsureConditionCount();
            int originalQuantity = quantity;
            int remaining;
            if (EnginePartConditionData.IsTrackedItem(item))
            {
                remaining = inventory.AddItemInstances(
                    item,
                    quantity,
                    conditionInstances);
            }
            else
            {
                remaining = inventory.AddItem(item, quantity);
            }

            if (remaining == originalQuantity)
            {
                return false;
            }

            int accepted = originalQuantity - remaining;
            if (conditionInstances.Count > 0 && accepted > 0)
            {
                int removeCount = Mathf.Min(accepted, conditionInstances.Count);
                conditionInstances.RemoveRange(0, removeCount);
            }

            quantity = remaining;
            if (quantity <= 0)
            {
                Destroy(gameObject);
            }
            else
            {
                EnsureConditionCount();
                ApplyConditionVisual();
            }

            return true;
        }

        private void FinalizeConfiguration()
        {
            EnsureConditionCount();
            name = item != null ? $"{item.DisplayName} Pickup" : "Inventory Pickup";
            ApplyConditionVisual();
        }

        private void EnsureConditionCount()
        {
            if (conditionInstances == null)
            {
                conditionInstances = new List<EnginePartConditionData>();
            }

            if (!EnginePartConditionData.IsTrackedItem(item))
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

        private string GetConditionSummary()
        {
            EnsureConditionCount();
            if (conditionInstances.Count == 0)
            {
                return string.Empty;
            }

            if (conditionInstances.Count == 1)
            {
                return conditionInstances[0]?.GetConditionSummary() ?? string.Empty;
            }

            float minimum = 100f;
            float maximum = 0f;
            bool cracked = false;
            for (int index = 0; index < conditionInstances.Count; index++)
            {
                EnginePartConditionData condition = conditionInstances[index];
                if (condition == null)
                {
                    continue;
                }

                minimum = Mathf.Min(minimum, condition.Health);
                maximum = Mathf.Max(maximum, condition.Health);
                cracked |= condition.IsCracked;
            }

            string range = Mathf.Abs(maximum - minimum) <= 0.05f
                ? $"{minimum:F1}%"
                : $"{minimum:F1}–{maximum:F1}%";
            return cracked ? $"{range}, includes cracked" : range;
        }

        private void ApplyConditionVisual()
        {
            EnginePartConditionVisual visual =
                GetComponent<EnginePartConditionVisual>();
            if (visual == null && conditionInstances.Count > 0)
            {
                visual = gameObject.AddComponent<EnginePartConditionVisual>();
            }

            if (visual != null)
            {
                visual.Configure(
                    conditionInstances.Count > 0
                        ? conditionInstances[0]
                        : null);
            }
        }

        private void OnValidate()
        {
            quantity = Mathf.Max(1, quantity);
            EnsureConditionCount();
        }
    }
}
