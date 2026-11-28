using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEngine;

namespace Hanger51.Inventory
{
    // Do not require a Collider here. Several generated service-part prefabs intentionally contain
    // no collider of their own, and runtime delivery/drop code adds InventoryPickup before fitting
    // the final root interaction collider. Requiring the abstract Collider base type can make Unity
    // reject AddComponent<InventoryPickup>() before that setup has a chance to run.
    public sealed class InventoryPickup : MonoBehaviour
    {
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private List<EnginePartConditionData> conditionInstances =
            new List<EnginePartConditionData>();

        private bool runtimePickupBlocked;

        public InventoryItemDefinition Item => item;
        public int Quantity => quantity;
        public IReadOnlyList<EnginePartConditionData> ConditionInstances => conditionInstances;
        public bool IsPickupBlocked => runtimePickupBlocked;

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

        public void SetRuntimePickupBlocked(bool blocked)
        {
            runtimePickupBlocked = blocked;
        }

        public void Configure(InventoryItemDefinition configuredItem, int configuredQuantity)
        {
            item = configuredItem;
            quantity = Mathf.Max(1, configuredQuantity);
            conditionInstances.Clear();
            if (EnginePartConditionData.IsTrackedItem(item))
            {
                EnginePartConditionData transferred = quantity == 1
                    ? EnginePartConditionTransferContext.PeekForItem(item)
                    : null;
                for (int index = 0; index < quantity; index++)
                {
                    conditionInstances.Add(
                        transferred != null && index == 0
                            ? transferred.Clone()
                            : EnginePartConditionData.CreateDefaultForItem(item));
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
            if (runtimePickupBlocked
                || inventory == null
                || item == null
                || quantity <= 0)
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
                PrepareP51WheelPartForWorld();
            }

            return true;
        }

        private void FinalizeConfiguration()
        {
            runtimePickupBlocked = false;
            EnsureConditionCount();
            name = item != null ? $"{item.DisplayName} Pickup" : "Inventory Pickup";
            ApplyConditionVisual();
            PrepareP51WheelPartForWorld();
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

        private void PrepareP51WheelPartForWorld()
        {
            if (!IsP51WheelPart(item)
                || GetComponentInParent<P51LooseWheelAssembly>() != null)
            {
                return;
            }

            // P-51 tire/rim prefabs are generated service visuals. They must be interactable no
            // matter whether they came from the shop, tire removal, or an inventory Drop action.
            // Give every loose wheel part exactly one root interaction collider at configuration
            // time instead of relying on a delayed global scanner that can rewrite it later.
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = false;
                }
            }

            BoxCollider rootCollider = GetComponent<BoxCollider>();
            if (rootCollider == null)
            {
                rootCollider = gameObject.AddComponent<BoxCollider>();
            }

            rootCollider.enabled = true;
            rootCollider.isTrigger = true;
            FitRootColliderToVisiblePart(rootCollider);

            // Keep the pickup itself live. Bare-rim service uses InventoryInteractor priority to
            // decide between E pickup and Hold E mounting; the rim is never permanently disabled.
            runtimePickupBlocked = false;

            if (EnginePartConditionData.InferKind(item) == EnginePartConditionKind.Rim)
            {
                P51BareRimServiceTarget.EnsureForPickup(this);
            }
        }

        private void FitRootColliderToVisiblePart(BoxCollider collider)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one * 0.50f;
                return;
            }

            bool hasBounds = false;
            Bounds worldBounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    worldBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one * 0.50f;
                return;
            }

            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float sy = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            float sz = Mathf.Max(0.001f, Mathf.Abs(scale.z));
            collider.center = transform.InverseTransformPoint(worldBounds.center);
            collider.size = new Vector3(
                Mathf.Max(0.16f, worldBounds.size.x / sx + 0.08f),
                Mathf.Max(0.16f, worldBounds.size.y / sy + 0.08f),
                Mathf.Max(0.16f, worldBounds.size.z / sz + 0.08f));
        }

        private static bool IsP51WheelPart(InventoryItemDefinition candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            string id = candidate.ItemId;
            return id == P51LandingGearInventoryBridge.MainTireItemId
                || id == P51LandingGearInventoryBridge.TailTireItemId
                || id == P51LandingGearInventoryBridge.MainRimItemId
                || id == P51LandingGearInventoryBridge.TailRimItemId;
        }

        private void OnEnable()
        {
            if (item != null)
            {
                PrepareP51WheelPartForWorld();
            }
        }

        private void OnValidate()
        {
            quantity = Mathf.Max(1, quantity);
            EnsureConditionCount();
        }
    }
}
