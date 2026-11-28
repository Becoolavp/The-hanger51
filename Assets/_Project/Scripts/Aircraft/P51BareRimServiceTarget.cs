using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(290)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryPickup))]
    [RequireComponent(typeof(Collider))]
    public sealed class P51BareRimServiceTarget : MonoBehaviour
    {
        [SerializeField] private InventoryPickup pickup;
        [SerializeField, Range(-1, 2)] private int originWheelIndex = -1;
        [SerializeField, Min(0.2f)] private float mountHoldSeconds = 1.15f;

        private float mountProgress;

        public InventoryPickup Pickup => pickup;
        public int OriginWheelIndex => originWheelIndex;
        public bool IsReady => pickup != null && pickup.Item != null;

        private bool IsTailRim => pickup != null
            && pickup.Item != null
            && pickup.Item.ItemId == P51LandingGearInventoryBridge.TailRimItemId;

        public static P51BareRimServiceTarget EnsureForPickup(
            InventoryPickup configuredPickup,
            int configuredOriginWheelIndex = -1)
        {
            if (configuredPickup == null
                || configuredPickup.Item == null
                || EnginePartConditionData.InferKind(configuredPickup.Item)
                    != EnginePartConditionKind.Rim)
            {
                return null;
            }

            P51BareRimServiceTarget target =
                configuredPickup.GetComponent<P51BareRimServiceTarget>();
            if (target == null)
            {
                target = configuredPickup.gameObject.AddComponent<P51BareRimServiceTarget>();
            }

            target.Configure(configuredPickup, configuredOriginWheelIndex);
            return target;
        }

        public void Configure(
            InventoryPickup configuredPickup,
            int configuredOriginWheelIndex = -1)
        {
            InventoryPickup previousPickup = pickup;
            pickup = configuredPickup != null
                ? configuredPickup
                : GetComponent<InventoryPickup>();

            EnginePartConditionData condition = GetRimCondition();
            int savedStation = condition != null ? condition.WheelStationIndex : -1;
            originWheelIndex = configuredOriginWheelIndex >= 0
                ? Mathf.Clamp(configuredOriginWheelIndex, 0, 2)
                : savedStation;

            if (condition != null && originWheelIndex >= 0)
            {
                condition.SetWheelStationIndex(originWheelIndex);
            }

            // Do not reset mountProgress on routine runtime refreshes. Only a genuinely different
            // pickup starts with a fresh hold.
            if (previousPickup != null && previousPickup != pickup)
            {
                mountProgress = 0f;
            }

            if (pickup != null)
            {
                // A rim must always remain a valid normal pickup. InventoryInteractor gives this
                // service target priority while the player is aiming at the rim, so there is no
                // need to permanently block InventoryPickup. This also gives us a safe pickup
                // fallback if a special tire-mount interaction is ever unavailable.
                pickup.SetRuntimePickupBlocked(false);
            }
        }

        private void Awake()
        {
            Configure(GetComponent<InventoryPickup>(), -1);
        }

        private void OnEnable()
        {
            if (pickup == null)
            {
                pickup = GetComponent<InventoryPickup>();
            }
            if (pickup != null)
            {
                pickup.SetRuntimePickupBlocked(false);
            }
        }

        public string GetInteractionText(PlayerInventory inventory)
        {
            if (!IsReady)
            {
                return string.Empty;
            }

            if (HasCorrectTireEquipped(inventory))
            {
                int percent = Mathf.RoundToInt(mountProgress * 100f);
                string progress = mountProgress > 0f ? $" ({percent}%)" : string.Empty;
                return $"Hold E: mount equipped {inventory.EquippedItem.DisplayName} on this {pickup.Item.DisplayName}{progress} | X inspect";
            }

            return $"E: pick up {pickup.Item.DisplayName} | Equip {ExpectedTireName()} to mount a tire | X inspect";
        }

        public bool ProcessInteraction(
            PlayerInventory inventory,
            bool installHeld,
            bool pickupPressed,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsReady || inventory == null)
            {
                CancelHold();
                return false;
            }

            pickup.SetRuntimePickupBlocked(false);
            bool matchingTire = HasCorrectTireEquipped(inventory);

            if (matchingTire)
            {
                if (!installHeld)
                {
                    CancelHold();
                    return false;
                }

                mountProgress = Mathf.Clamp01(
                    mountProgress + Mathf.Max(0f, deltaTime)
                    / Mathf.Max(0.2f, mountHoldSeconds));
                if (mountProgress < 1f)
                {
                    return false;
                }

                mountProgress = 0f;
                return TryMountEquippedTire(inventory, out resultMessage);
            }

            CancelHold();
            if (!pickupPressed)
            {
                return false;
            }

            return TryPickupRim(inventory, out resultMessage);
        }

        public string Inspect()
        {
            if (!IsReady)
            {
                return "Bare rim service target is not ready.";
            }

            EnginePartConditionData condition = GetRimCondition();
            string conditionText = condition != null
                ? condition.GetConditionSummary()
                : "condition unavailable";
            return $"Bare {pickup.Item.DisplayName} | {conditionText} | The rim is one complete part. Pick it up with E, or equip {ExpectedTireName()} and hold E to mount that tire directly on this rim.";
        }

        public void CancelHold()
        {
            mountProgress = 0f;
        }

        private bool TryPickupRim(
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (pickup == null || inventory == null)
            {
                resultMessage = "The rim pickup is not ready.";
                return false;
            }

            pickup.SetRuntimePickupBlocked(false);
            bool pickedUp = pickup.TryPickup(inventory);
            if (!pickedUp)
            {
                resultMessage = "Inventory is full; the rim stays on the floor.";
                return false;
            }

            resultMessage = $"Picked up {pickup.Item.DisplayName}.";
            return true;
        }

        private bool TryMountEquippedTire(
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!HasCorrectTireEquipped(inventory))
            {
                resultMessage = $"Equip {ExpectedTireName()} first.";
                return false;
            }

            InventoryItemDefinition rimItem = pickup.Item;
            EnginePartConditionData rimCondition = GetRimCondition();
            if (rimCondition == null)
            {
                rimCondition = EnginePartConditionData.CreateDefaultForItem(rimItem);
            }
            if (rimCondition != null && originWheelIndex >= 0)
            {
                rimCondition.SetWheelStationIndex(originWheelIndex);
            }

            InventoryItemDefinition tireItem = inventory.EquippedItem;
            if (!inventory.TryRemoveFirstItem(
                    tireItem,
                    out EnginePartConditionData tireCondition))
            {
                resultMessage = "The equipped tire could not be removed from inventory.";
                return false;
            }

            tireCondition ??= EnginePartConditionData.CreateDefaultForItem(tireItem);
            int assemblyOrigin = rimCondition != null
                ? rimCondition.WheelStationIndex
                : originWheelIndex;
            string label = GetWheelLabel(assemblyOrigin);

            P51LooseWheelAssembly wheel = P51LooseWheelAssembly.Create(
                label,
                assemblyOrigin,
                transform.position,
                transform.rotation,
                tireItem,
                tireCondition,
                rimItem,
                rimCondition);
            if (wheel == null)
            {
                inventory.AddConditionedItem(tireItem, tireCondition);
                resultMessage = "The complete wheel could not be created; the tire was returned to inventory.";
                return false;
            }

            pickup.SetRuntimePickupBlocked(false);
            resultMessage = $"Mounted {tireItem.DisplayName} onto the {rimItem.DisplayName}. Pressure is {tireCondition.TirePressurePsi:F1} PSI; pressure does not block physical installation. Press E on the complete wheel to carry it back to the aircraft.";
            Destroy(gameObject);
            return true;
        }

        private EnginePartConditionData GetRimCondition()
        {
            if (pickup == null
                || pickup.ConditionInstances == null
                || pickup.ConditionInstances.Count == 0)
            {
                return null;
            }

            return pickup.ConditionInstances[0];
        }

        private bool HasCorrectTireEquipped(PlayerInventory inventory)
        {
            if (inventory == null
                || inventory.EquippedItem == null
                || pickup == null
                || pickup.Item == null)
            {
                return false;
            }

            string expectedId = IsTailRim
                ? P51LandingGearInventoryBridge.TailTireItemId
                : P51LandingGearInventoryBridge.MainTireItemId;
            return inventory.EquippedItem.ItemId == expectedId;
        }

        private string ExpectedTireName()
        {
            return IsTailRim
                ? "P-51 Tailwheel Tire"
                : "P-51 Main Landing Tire";
        }

        private string GetWheelLabel(int stationIndex)
        {
            if (stationIndex == 0) return "left main";
            if (stationIndex == 1) return "right main";
            if (stationIndex == 2 || IsTailRim) return "tail";
            return "main";
        }

        private void OnDisable()
        {
            if (pickup != null)
            {
                pickup.SetRuntimePickupBlocked(false);
            }
            CancelHold();
        }

        private void OnDestroy()
        {
            if (pickup != null)
            {
                pickup.SetRuntimePickupBlocked(false);
            }
        }

        private void OnValidate()
        {
            originWheelIndex = Mathf.Clamp(originWheelIndex, -1, 2);
            mountHoldSeconds = Mathf.Max(0.2f, mountHoldSeconds);
        }
    }
}
