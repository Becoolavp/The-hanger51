using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51LooseWheelAssembly : MonoBehaviour
    {
        private enum ServiceAction
        {
            None,
            RemoveTire,
            RemoveRim,
            InstallRim,
            InstallTire
        }

        [SerializeField] private InventoryItemDefinition tireItem;
        [SerializeField] private InventoryItemDefinition rimItem;
        [SerializeField] private EnginePartConditionData tireCondition;
        [SerializeField] private EnginePartConditionData rimCondition;
        [SerializeField] private string wheelLabel = "P-51 wheel";
        [SerializeField, Range(0, 2)] private int originWheelIndex;
        [SerializeField] private bool tireInstalled = true;
        [SerializeField] private bool rimInstalled = true;
        [SerializeField, Min(0.2f)] private float serviceHoldSeconds = 1.15f;

        private Collider interactionCollider;
        private Transform tireVisual;
        private Transform rimVisual;
        private float serviceProgress;
        private ServiceAction activeAction;

        public static P51LooseWheelAssembly CurrentCarried { get; private set; }

        public bool IsComplete => tireInstalled && rimInstalled;
        public bool IsCarried => CurrentCarried == this;
        public bool HasTire => tireInstalled;
        public bool HasRim => rimInstalled;
        public int OriginWheelIndex => originWheelIndex;
        public string WheelLabel => wheelLabel;

        public static P51LooseWheelAssembly Create(
            string label,
            int configuredOriginWheelIndex,
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

            bool tail = configuredOriginWheelIndex == 2;
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
            loose.originWheelIndex = Mathf.Clamp(configuredOriginWheelIndex, 0, 2);
            loose.tireInstalled = true;
            loose.rimInstalled = true;
            loose.interactionCollider = collider;
            loose.BuildOrRefreshVisuals();
            return loose;
        }

        private void Awake()
        {
            ResolveCollider();
            ResolveExistingVisuals();
            RefreshVisuals();
        }

        public string GetInteractionText(PlayerInventory inventory)
        {
            int percent = Mathf.RoundToInt(serviceProgress * 100f);
            string progress = serviceProgress > 0f ? $" ({percent}%)" : string.Empty;

            if (IsCarried)
            {
                return $"Carrying {wheelLabel} wheel assembly";
            }

            if (IsComplete)
            {
                return $"E: carry complete {wheelLabel} wheel | Hold R: separate tire from rim{progress} | X inspect";
            }

            if (rimInstalled && !tireInstalled)
            {
                bool hasCorrectTire = inventory != null && inventory.EquippedItem == tireItem;
                return hasCorrectTire
                    ? $"Hold E: fit equipped {tireItem.DisplayName} to loose rim{progress} | Hold R: remove rim | X inspect"
                    : $"Equip {tireItem.DisplayName} to rebuild wheel | Hold R: remove loose rim{progress} | X inspect";
            }

            if (!rimInstalled)
            {
                bool hasCorrectRim = inventory != null && inventory.EquippedItem == rimItem;
                return hasCorrectRim
                    ? $"Hold E: install equipped {rimItem.DisplayName} into loose wheel assembly{progress} | X inspect"
                    : $"Equip {rimItem.DisplayName} to start rebuilding this wheel | X inspect";
            }

            return $"Loose {wheelLabel} wheel service position | X inspect";
        }

        public bool ProcessService(
            PlayerInventory inventory,
            bool installHeld,
            bool removeHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (IsCarried)
            {
                CancelHold();
                return false;
            }

            ServiceAction desired = ResolveDesiredAction(inventory, installHeld, removeHeld);
            if (desired == ServiceAction.None)
            {
                CancelHold();
                return false;
            }

            if (activeAction != desired)
            {
                activeAction = desired;
                serviceProgress = 0f;
            }

            serviceProgress = Mathf.Clamp01(
                serviceProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.2f, serviceHoldSeconds));
            if (serviceProgress < 1f)
            {
                return false;
            }

            bool completed = CompleteServiceAction(desired, inventory, out resultMessage);
            serviceProgress = 0f;
            activeAction = ServiceAction.None;
            return completed;
        }

        public bool TryBeginCarry(Transform carryAnchor, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsComplete)
            {
                resultMessage = "Reassemble the tire and rim into a complete wheel before carrying it back to the aircraft.";
                return false;
            }
            if (carryAnchor == null)
            {
                resultMessage = "The Player wheel-carry anchor is missing.";
                return false;
            }
            if (CurrentCarried != null && CurrentCarried != this)
            {
                resultMessage = "You are already carrying another wheel assembly.";
                return false;
            }

            CurrentCarried = this;
            CancelHold();
            ResolveCollider();
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            transform.SetParent(carryAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            resultMessage = $"Picked up the complete {wheelLabel} wheel assembly. Carry it to its highlighted landing-gear axle and hold E to reinstall, or press E away from the axle to set it down.";
            return true;
        }

        public bool TryPlace(Vector3 worldPosition, Quaternion worldRotation, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsCarried)
            {
                return false;
            }

            CurrentCarried = null;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            ResolveCollider();
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }
            resultMessage = $"Set down the {wheelLabel} wheel assembly.";
            return true;
        }

        public bool CanInstallOn(int wheelIndex)
        {
            return IsComplete && wheelIndex == originWheelIndex;
        }

        public EnginePartConditionData CaptureTireCondition()
        {
            return tireCondition != null ? tireCondition.Clone() : null;
        }

        public EnginePartConditionData CaptureRimCondition()
        {
            return rimCondition != null ? rimCondition.Clone() : null;
        }

        public void CompleteAircraftInstallation()
        {
            if (CurrentCarried == this)
            {
                CurrentCarried = null;
            }
            Destroy(gameObject);
        }

        public string Inspect()
        {
            string tireSummary = tireInstalled
                ? tireCondition != null ? tireCondition.GetConditionSummary() : "condition unavailable"
                : "removed from assembly";
            string rimSummary = rimInstalled
                ? rimCondition != null ? rimCondition.GetConditionSummary() : "condition unavailable"
                : "removed from assembly";
            string state = IsComplete
                ? "complete and ready to carry/reinstall"
                : rimInstalled
                    ? "rim installed; tire missing"
                    : "rim and tire missing";
            return $"Loose {wheelLabel} wheel assembly | {state} | Tire: {tireSummary} | Rim: {rimSummary} | Origin station: {GetOriginName()}";
        }

        public void CancelHold()
        {
            serviceProgress = 0f;
            activeAction = ServiceAction.None;
        }

        private ServiceAction ResolveDesiredAction(
            PlayerInventory inventory,
            bool installHeld,
            bool removeHeld)
        {
            if (removeHeld && !installHeld)
            {
                if (IsComplete)
                {
                    return ServiceAction.RemoveTire;
                }
                if (rimInstalled && !tireInstalled)
                {
                    return ServiceAction.RemoveRim;
                }
            }

            if (installHeld && !removeHeld && inventory != null)
            {
                if (!rimInstalled && inventory.EquippedItem == rimItem)
                {
                    return ServiceAction.InstallRim;
                }
                if (rimInstalled && !tireInstalled && inventory.EquippedItem == tireItem)
                {
                    return ServiceAction.InstallTire;
                }
            }

            return ServiceAction.None;
        }

        private bool CompleteServiceAction(
            ServiceAction action,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            switch (action)
            {
                case ServiceAction.RemoveTire:
                    if (!SpawnPickup(
                            tireItem,
                            tireCondition,
                            transform.position + transform.right * 0.66f + Vector3.up * 0.12f,
                            transform.rotation))
                    {
                        resultMessage = "The tire could not be placed beside the loose rim.";
                        return false;
                    }
                    tireInstalled = false;
                    RefreshVisuals();
                    resultMessage = $"Separated the tire from the {wheelLabel} rim. The exact tire is now a physical pickup beside the rim; press E on it to put it in inventory.";
                    return true;

                case ServiceAction.RemoveRim:
                    if (!SpawnPickup(
                            rimItem,
                            rimCondition,
                            transform.position - transform.right * 0.58f + Vector3.up * 0.10f,
                            transform.rotation))
                    {
                        resultMessage = "The rim could not be placed beside the loose wheel service position.";
                        return false;
                    }
                    rimInstalled = false;
                    RefreshVisuals();
                    resultMessage = $"Removed the {wheelLabel} rim as a separate physical pickup. The loose wheel service position remains so you can rebuild it with this rim or a new replacement rim.";
                    return true;

                case ServiceAction.InstallRim:
                    if (inventory == null
                        || rimItem == null
                        || !inventory.TryRemoveFirstItem(rimItem, out EnginePartConditionData installedRim))
                    {
                        resultMessage = "The equipped rim could not be removed from inventory.";
                        return false;
                    }
                    rimCondition = installedRim ?? EnginePartConditionData.CreateDefaultForItem(rimItem);
                    rimInstalled = true;
                    RefreshVisuals();
                    resultMessage = $"Installed that exact {rimItem.DisplayName} into the loose {wheelLabel} assembly. Fit the matching tire next.";
                    return true;

                case ServiceAction.InstallTire:
                    if (inventory == null
                        || tireItem == null
                        || !inventory.TryRemoveFirstItem(tireItem, out EnginePartConditionData installedTire))
                    {
                        resultMessage = "The equipped tire could not be removed from inventory.";
                        return false;
                    }
                    tireCondition = installedTire ?? EnginePartConditionData.CreateDefaultForItem(tireItem);
                    tireInstalled = true;
                    RefreshVisuals();
                    resultMessage = $"Mounted that exact {tireItem.DisplayName} onto the loose {wheelLabel} rim. The wheel assembly is complete again and can now be carried back to its original strut.";
                    return true;

                default:
                    return false;
            }
        }

        private void BuildOrRefreshVisuals()
        {
            ResolveExistingVisuals();
            if (tireVisual == null)
            {
                tireVisual = CreateVisualChild(transform, tireItem, "Loose Tire");
            }
            if (rimVisual == null)
            {
                rimVisual = CreateVisualChild(transform, rimItem, "Loose Rim");
            }
            RefreshVisuals();
        }

        private void ResolveExistingVisuals()
        {
            if (tireVisual == null)
            {
                tireVisual = transform.Find("Loose Tire");
            }
            if (rimVisual == null)
            {
                rimVisual = transform.Find("Loose Rim");
            }
        }

        private void RefreshVisuals()
        {
            if (tireVisual != null)
            {
                tireVisual.gameObject.SetActive(tireInstalled);
                ConfigureConditionVisual(tireVisual, tireCondition);
            }
            if (rimVisual != null)
            {
                rimVisual.gameObject.SetActive(rimInstalled);
                ConfigureConditionVisual(rimVisual, rimCondition);
            }
        }

        private static void ConfigureConditionVisual(
            Transform visualRoot,
            EnginePartConditionData condition)
        {
            if (visualRoot == null)
            {
                return;
            }

            EnginePartConditionVisual visual = visualRoot.GetComponent<EnginePartConditionVisual>();
            if (visual == null)
            {
                visual = visualRoot.gameObject.AddComponent<EnginePartConditionVisual>();
            }
            visual.Configure(condition);
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
            DisablePickupAndColliders(child);
            return child.transform;
        }

        private static void DisablePickupAndColliders(GameObject root)
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
                    pickups[index].enabled = false;
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = false;
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

            pickupObject.SetActive(true);
            pickupObject.name = $"Separated {item.DisplayName}";
            pickupObject.transform.SetPositionAndRotation(worldPosition, worldRotation);

            Collider collider = pickupObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = pickupObject.AddComponent<BoxCollider>();
            }
            collider.enabled = true;
            collider.isTrigger = true;

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.enabled = true;
            pickup.Configure(item, condition);

            EnginePartConditionVisual conditionVisual =
                pickupObject.GetComponent<EnginePartConditionVisual>();
            if (conditionVisual == null)
            {
                conditionVisual = pickupObject.AddComponent<EnginePartConditionVisual>();
            }
            conditionVisual.Configure(condition);
            return true;
        }

        private void ResolveCollider()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }

        private string GetOriginName()
        {
            return originWheelIndex == 0
                ? "left main"
                : originWheelIndex == 1
                    ? "right main"
                    : "tail";
        }

        private void OnDestroy()
        {
            if (CurrentCarried == this)
            {
                CurrentCarried = null;
            }
        }

        private void OnDisable()
        {
            CancelHold();
        }
    }
}
