using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51LooseWheelAssembly : MonoBehaviour
    {
        [SerializeField] private InventoryItemDefinition tireItem;
        [SerializeField] private InventoryItemDefinition rimItem;
        [SerializeField] private EnginePartConditionData tireCondition;
        [SerializeField] private EnginePartConditionData rimCondition;
        [SerializeField] private string wheelLabel = "P-51 wheel";
        [SerializeField, Min(0.2f)] private float separationHoldSeconds = 1.15f;

        private float separationProgress;

        public string InteractionText
        {
            get
            {
                int percent = Mathf.RoundToInt(separationProgress * 100f);
                string progress = separationProgress > 0f ? $" ({percent}%)" : string.Empty;
                return $"Hold R: separate tire from rim{progress} | X inspect loose {wheelLabel}";
            }
        }

        public static P51LooseWheelAssembly Create(
            string label,
            Vector3 worldPosition,
            Quaternion worldRotation,
            InventoryItemDefinition configuredTireItem,
            EnginePartConditionData configuredTireCondition,
            InventoryItemDefinition configuredRimItem,
            EnginePartConditionData configuredRimCondition)
        {
            if (configuredTireItem == null || configuredRimItem == null)
            {
                return null;
            }

            GameObject root = new GameObject($"Removed {label} Wheel Assembly");
            root.transform.SetPositionAndRotation(worldPosition, worldRotation);

            bool tail = label != null && label.ToLowerInvariant().Contains("tail");
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up * (tail ? 0.16f : 0.32f);
            collider.size = tail
                ? new Vector3(0.55f, 0.55f, 0.34f)
                : new Vector3(1.05f, 0.92f, 0.54f);

            P51LooseWheelAssembly loose = root.AddComponent<P51LooseWheelAssembly>();
            loose.tireItem = configuredTireItem;
            loose.rimItem = configuredRimItem;
            loose.tireCondition = configuredTireCondition != null
                ? configuredTireCondition.Clone()
                : EnginePartConditionData.CreateDefaultForItem(configuredTireItem);
            loose.rimCondition = configuredRimCondition != null
                ? configuredRimCondition.Clone()
                : EnginePartConditionData.CreateDefaultForItem(configuredRimItem);
            loose.wheelLabel = string.IsNullOrWhiteSpace(label) ? "P-51 wheel" : label;

            Transform tireVisual = CreateVisualChild(root.transform, configuredTireItem, "Loose Tire");
            Transform rimVisual = CreateVisualChild(root.transform, configuredRimItem, "Loose Rim");

            if (tireVisual != null)
            {
                EnginePartConditionVisual tireConditionVisual =
                    tireVisual.GetComponent<EnginePartConditionVisual>();
                if (tireConditionVisual == null)
                {
                    tireConditionVisual = tireVisual.gameObject.AddComponent<EnginePartConditionVisual>();
                }
                tireConditionVisual.Configure(loose.tireCondition);
            }

            if (rimVisual != null)
            {
                EnginePartConditionVisual rimConditionVisual =
                    rimVisual.GetComponent<EnginePartConditionVisual>();
                if (rimConditionVisual == null)
                {
                    rimConditionVisual = rimVisual.gameObject.AddComponent<EnginePartConditionVisual>();
                }
                rimConditionVisual.Configure(loose.rimCondition);
            }

            return loose;
        }

        public bool ProcessSeparation(
            bool removeHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!removeHeld)
            {
                CancelHold();
                return false;
            }

            separationProgress = Mathf.Clamp01(
                separationProgress
                + Mathf.Max(0f, deltaTime) / Mathf.Max(0.2f, separationHoldSeconds));
            if (separationProgress < 1f)
            {
                return false;
            }

            bool tireSpawned = SpawnPickup(
                tireItem,
                tireCondition,
                transform.position + transform.right * 0.52f + Vector3.up * 0.10f,
                transform.rotation);
            bool rimSpawned = SpawnPickup(
                rimItem,
                rimCondition,
                transform.position - transform.right * 0.52f + Vector3.up * 0.08f,
                transform.rotation);

            if (!tireSpawned || !rimSpawned)
            {
                separationProgress = 0f;
                resultMessage = "The loose wheel could not be separated into its tire and rim pickups.";
                return false;
            }

            resultMessage = $"Separated the {wheelLabel}: the tire and rim are now individual physical parts. Press E on each one to put it in inventory.";
            Destroy(gameObject);
            return true;
        }

        public string Inspect()
        {
            string tireSummary = tireCondition != null
                ? tireCondition.GetConditionSummary()
                : "condition unavailable";
            string rimSummary = rimCondition != null
                ? rimCondition.GetConditionSummary()
                : "condition unavailable";
            return $"Loose {wheelLabel} assembly | Tire: {tireSummary} | Rim: {rimSummary} | Hold R to separate them before inventory pickup.";
        }

        public void CancelHold()
        {
            separationProgress = 0f;
        }

        private static Transform CreateVisualChild(
            Transform parent,
            InventoryItemDefinition item,
            string childName)
        {
            if (parent == null || item == null)
            {
                return null;
            }

            GameObject child;
            if (item.WorldPrefab != null)
            {
                child = Instantiate(item.WorldPrefab, parent);
                child.transform.localScale = item.WorldScale;
            }
            else
            {
                child = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                child.transform.SetParent(parent, false);
            }

            child.name = childName;
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            RemovePickupAndColliders(child);
            return child.transform;
        }

        private static void RemovePickupAndColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            InventoryPickup[] pickups = root.GetComponentsInChildren<InventoryPickup>(true);
            for (int index = 0; index < pickups.Length; index++)
            {
                if (pickups[index] != null)
                {
                    Destroy(pickups[index]);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    Destroy(colliders[index]);
                }
            }
        }

        private static bool SpawnPickup(
            InventoryItemDefinition item,
            EnginePartConditionData condition,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (item == null)
            {
                return false;
            }

            GameObject pickupObject;
            if (item.WorldPrefab != null)
            {
                pickupObject = Instantiate(item.WorldPrefab);
                pickupObject.transform.localScale = item.WorldScale;
            }
            else
            {
                pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            }

            pickupObject.name = $"Separated {item.DisplayName}";
            pickupObject.transform.SetPositionAndRotation(worldPosition, worldRotation);

            Collider collider = pickupObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = pickupObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.Configure(item, condition);
            return true;
        }

        private void OnDisable()
        {
            separationProgress = 0f;
        }
    }
}
