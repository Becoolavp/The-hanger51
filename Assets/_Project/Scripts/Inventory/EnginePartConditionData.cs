using System;
using UnityEngine;

namespace Hanger51.Inventory
{
    public enum EnginePartConditionKind
    {
        None,
        EngineBlock,
        CylinderCover,
        SparkPlug,
        Tire,
        Rim
    }

    [Serializable]
    public sealed class EnginePartConditionData
    {
        [SerializeField] private string instanceId;
        [SerializeField] private EnginePartConditionKind kind;
        [SerializeField, Range(0f, 100f)] private float health = 100f;
        [SerializeField, Min(0f)] private float oilQuantityLiters;
        [SerializeField, Min(0f)] private float oilCapacityLiters;
        [SerializeField, Min(0f)] private float tirePressurePsi;
        [SerializeField, Min(0f)] private float recommendedTirePressurePsi;
        [SerializeField] private bool tireFailed;

        public string InstanceId => instanceId;
        public EnginePartConditionKind Kind => kind;
        public float Health => Mathf.Clamp(health, 0f, 100f);
        public float OilQuantityLiters => Mathf.Max(0f, oilQuantityLiters);
        public float OilCapacityLiters => Mathf.Max(0f, oilCapacityLiters);
        public float TirePressurePsi => Mathf.Max(0f, tirePressurePsi);
        public float RecommendedTirePressurePsi => Mathf.Max(0f, recommendedTirePressurePsi);
        public bool TireFailed => kind == EnginePartConditionKind.Tire
            && (tireFailed || Health <= 0.01f);
        public bool IsTracked => kind != EnginePartConditionKind.None;
        public bool IsCracked => kind == EnginePartConditionKind.CylinderCover
            && Health <= 35f;
        public string Signature =>
            $"{instanceId}:{kind}:{Health:F3}:{OilQuantityLiters:F3}:{OilCapacityLiters:F3}:{TirePressurePsi:F3}:{RecommendedTirePressurePsi:F3}:{TireFailed}";

        public static EnginePartConditionData Create(
            EnginePartConditionKind conditionKind,
            float conditionHealth,
            float oilQuantity = 0f,
            float oilCapacity = 0f,
            float pressurePsi = 0f,
            float recommendedPressurePsi = 0f,
            bool failedTire = false)
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
                    Mathf.Max(0f, oilCapacity)),
                tirePressurePsi = Mathf.Max(0f, pressurePsi),
                recommendedTirePressurePsi = Mathf.Max(0f, recommendedPressurePsi),
                tireFailed = failedTire
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

            if (inferredKind == EnginePartConditionKind.EngineBlock)
            {
                return Create(inferredKind, 100f, 20f, 20f);
            }

            if (inferredKind == EnginePartConditionKind.Tire)
            {
                bool tail = IsTailwheelItem(item);
                return Create(
                    inferredKind,
                    100f,
                    0f,
                    0f,
                    tail ? 6f : 8f,
                    tail ? 24f : 30f,
                    false);
            }

            return Create(inferredKind, 100f);
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

            if (identity.Contains("tire") || identity.Contains("tyre"))
            {
                return EnginePartConditionKind.Tire;
            }

            if (identity.Contains("rim")
                || identity.Contains("wheel hub")
                || identity.Contains("wheel-hub"))
            {
                return EnginePartConditionKind.Rim;
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
                oilCapacityLiters = OilCapacityLiters,
                tirePressurePsi = TirePressurePsi,
                recommendedTirePressurePsi = RecommendedTirePressurePsi,
                tireFailed = TireFailed
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
                case EnginePartConditionKind.Tire:
                    return TireFailed
                        ? $"{Health:F1}% — DESTROYED | {TirePressurePsi:F1} PSI"
                        : $"{Health:F1}% | {TirePressurePsi:F1} PSI"
                            + (RecommendedTirePressurePsi > 0.1f
                                ? $" / {RecommendedTirePressurePsi:F0} PSI correct"
                                : string.Empty);
                case EnginePartConditionKind.Rim:
                    return $"Rim {Health:F1}%";
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
            tirePressurePsi = Mathf.Clamp(tirePressurePsi, 0f, 80f);
            recommendedTirePressurePsi = Mathf.Clamp(
                recommendedTirePressurePsi,
                0f,
                80f);
            if (kind != EnginePartConditionKind.Tire)
            {
                tireFailed = false;
                tirePressurePsi = 0f;
                recommendedTirePressurePsi = 0f;
            }
        }

        private static bool IsTailwheelItem(InventoryItemDefinition item)
        {
            if (item == null)
            {
                return false;
            }

            string identity = $"{item.ItemId} {item.name} {item.DisplayName}"
                .ToLowerInvariant();
            return identity.Contains("tailwheel")
                || identity.Contains("tail wheel")
                || identity.Contains("tail-tire")
                || identity.Contains("tail tire");
        }
    }
}
