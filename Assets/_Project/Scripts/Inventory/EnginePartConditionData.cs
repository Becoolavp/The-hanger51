using System;
using UnityEngine;

namespace Hanger51.Inventory
{
    public enum EnginePartConditionKind
    {
        None,
        EngineBlock,
        CylinderCover,
        SparkPlug
    }

    [Serializable]
    public sealed class EnginePartConditionData
    {
        [SerializeField] private string instanceId;
        [SerializeField] private EnginePartConditionKind kind;
        [SerializeField, Range(0f, 100f)] private float health = 100f;
        [SerializeField, Min(0f)] private float oilQuantityLiters;
        [SerializeField, Min(0f)] private float oilCapacityLiters;

        public string InstanceId => instanceId;
        public EnginePartConditionKind Kind => kind;
        public float Health => Mathf.Clamp(health, 0f, 100f);
        public float OilQuantityLiters => Mathf.Max(0f, oilQuantityLiters);
        public float OilCapacityLiters => Mathf.Max(0f, oilCapacityLiters);
        public bool IsTracked => kind != EnginePartConditionKind.None;
        public bool IsCracked => kind == EnginePartConditionKind.CylinderCover
            && Health <= 35f;
        public string Signature => $"{instanceId}:{kind}:{Health:F3}:{OilQuantityLiters:F3}:{OilCapacityLiters:F3}";

        public static EnginePartConditionData Create(
            EnginePartConditionKind conditionKind,
            float conditionHealth,
            float oilQuantity = 0f,
            float oilCapacity = 0f)
        {
            EnginePartConditionData data = new EnginePartConditionData
            {
                instanceId = Guid.NewGuid().ToString("N"),
                kind = conditionKind,
                health = Mathf.Clamp(conditionHealth, 0f, 100f),
                oilCapacityLiters = Mathf.Max(0f, oilCapacity),
                oilQuantityLiters = Mathf.Clamp(
                    oilQuantity,
                    0f,
                    Mathf.Max(0f, oilCapacity))
            };
            return data;
        }

        public static EnginePartConditionData CreateDefaultForItem(
            InventoryItemDefinition item)
        {
            EnginePartConditionKind inferredKind = InferKind(item);
            if (inferredKind == EnginePartConditionKind.None)
            {
                return null;
            }

            return inferredKind == EnginePartConditionKind.EngineBlock
                ? Create(inferredKind, 100f, 20f, 20f)
                : Create(inferredKind, 100f);
        }

        public static EnginePartConditionKind InferKind(InventoryItemDefinition item)
        {
            if (item == null)
            {
                return EnginePartConditionKind.None;
            }

            string identity = $"{item.ItemId} {item.name} {item.DisplayName}"
                .ToLowerInvariant();

            if (identity.Contains("spark") && identity.Contains("plug"))
            {
                return EnginePartConditionKind.SparkPlug;
            }

            if (identity.Contains("cover")
                || identity.Contains("cylinder head")
                || identity.Contains("valve cover"))
            {
                return EnginePartConditionKind.CylinderCover;
            }

            if (identity.Contains("engine block")
                || identity.Contains("engineblock")
                || identity.Contains("merlin block"))
            {
                return EnginePartConditionKind.EngineBlock;
            }

            return EnginePartConditionKind.None;
        }

        public static bool IsTrackedItem(InventoryItemDefinition item)
        {
            return InferKind(item) != EnginePartConditionKind.None;
        }

        public EnginePartConditionData Clone()
        {
            EnginePartConditionData clone = new EnginePartConditionData
            {
                instanceId = string.IsNullOrWhiteSpace(instanceId)
                    ? Guid.NewGuid().ToString("N")
                    : instanceId,
                kind = kind,
                health = Health,
                oilQuantityLiters = OilQuantityLiters,
                oilCapacityLiters = OilCapacityLiters
            };
            return clone;
        }

        public string GetConditionSummary()
        {
            switch (kind)
            {
                case EnginePartConditionKind.EngineBlock:
                    return $"Block {Health:F1}% | Oil {OilQuantityLiters:F1}/{OilCapacityLiters:F1} L";
                case EnginePartConditionKind.CylinderCover:
                    return IsCracked
                        ? $"{Health:F1}% — CRACKED"
                        : $"{Health:F1}%";
                case EnginePartConditionKind.SparkPlug:
                    return $"{Health:F2}%";
                default:
                    return string.Empty;
            }
        }

        public void EnsureValid()
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
            }

            health = Mathf.Clamp(health, 0f, 100f);
            oilCapacityLiters = Mathf.Max(0f, oilCapacityLiters);
            oilQuantityLiters = Mathf.Clamp(
                oilQuantityLiters,
                0f,
                oilCapacityLiters);
        }
    }
}
