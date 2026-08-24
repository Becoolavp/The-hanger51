using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(295)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryPickup))]
    [RequireComponent(typeof(Collider))]
    public sealed class P51BareRimServiceTarget : MonoBehaviour
    {
        [SerializeField] private InventoryPickup pickup;
        [SerializeField, Range(-1, 2)] private int originWheelIndex = -1;
        [SerializeField, Min(0.2f)] private float mountHoldSeconds = 1.15f;
        [SerializeField, Min(1f)] private float interactionDistance = 6f;

        private PlayerInventory inventory;
        private InventoryUI inventoryUI;
        private Camera playerCamera;
        private float mountProgress;

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
            pickup = configuredPickup != null
                ? configuredPickup
                : GetComponent<InventoryPickup>();

            int savedStation = GetRimCondition() != null
                ? GetRimCondition().WheelStationIndex
                : -1;
            originWheelIndex = configuredOriginWheelIndex >= 0
                ? Mathf.Clamp(configuredOriginWheelIndex, 0, 2)
                : savedStation;

            EnginePartConditionData condition = GetRimCondition();
            if (condition != null && originWheelIndex >= 0)
            {
                condition.SetWheelStationIndex(originWheelIndex);
            }

            ResolvePlayerReferences();
            if (pickup != null)
            {
                pickup.SetRuntimePickupBlocked(true);
            }
        }

        private void Awake()
        {
            if (pickup == null)
            {
                pickup = GetComponent<InventoryPickup>();
            }
            Configure(pickup, -1);
        }

        private void OnEnable()
        {
            ResolvePlayerReferences();
            if (pickup != null)
            {
                pickup.SetRuntimePickupBlocked(true);
            }
        }

        private void Update()
        {
            ResolvePlayerReferences();
            if (pickup == null || inventory == null || inventoryUI == null || playerCamera == null)
            {
                return;
            }

            pickup.SetRuntimePickupBlocked(true);
            if (!IsPlayerAimingAtThisRim())
            {
                mountProgress = 0f;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool correctTireEquipped = HasCorrectTireEquipped();

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                inventoryUI.ShowStatusMessage(Inspect(), 4f);
            }

            if (correctTireEquipped)
            {
                bool holdInstall = keyboard != null && keyboard.eKey.isPressed;
                if (holdInstall)
                {
                    mountProgress = Mathf.Clamp01(
                        mountProgress
                        + Time.deltaTime / Mathf.Max(0.2f, mountHoldSeconds));
                    if (mountProgress >= 1f)
                    {
                        if (TryMountEquippedTire(out string mountMessage))
                        {
                            inventoryUI.ShowStatusMessage(mountMessage, 4f);
                            return;
                        }

                        inventoryUI.ShowStatusMessage(mountMessage, 3f);
                        mountProgress = 0f;
                    }
                }
                else
                {
                    mountProgress = 0f;
                }

                int percent = Mathf.RoundToInt(mountProgress * 100f);
                string progress = mountProgress > 0f ? $" ({percent}%)" : string.Empty;
                inventoryUI.SetInteractionPrompt(
                    $"Hold E: mount equipped {inventory.EquippedItem.DisplayName} on this {pickup.Item.DisplayName}{progress} | X inspect");
                return;
            }

            mountProgress = 0f;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (TryPickupRim(out string pickupMessage))
                {
                    inventoryUI.ShowStatusMessage(pickupMessage, 2.5f);
                    return;
                }
                inventoryUI.ShowStatusMessage(pickupMessage, 2.5f);
            }

            inventoryUI.SetInteractionPrompt(
                $"E: pick up {pickup.Item.DisplayName} | Equip {ExpectedTireName()} to mount a tire | X inspect");
        }

        private bool TryPickupRim(out string resultMessage)
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
                pickup.SetRuntimePickupBlocked(true);
                resultMessage = "Inventory is full; the rim stays on the floor.";
                return false;
            }

            resultMessage = $"Picked up {pickup.Item.DisplayName}.";
            return true;
        }

        private bool TryMountEquippedTire(out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!HasCorrectTireEquipped())
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
            resultMessage = $"Mounted {tireItem.DisplayName} onto the {rimItem.DisplayName}. The complete wheel can now be carried back to the aircraft.";
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

        private bool HasCorrectTireEquipped()
        {
            if (inventory == null || inventory.EquippedItem == null || pickup == null || pickup.Item == null)
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

        private string Inspect()
        {
            EnginePartConditionData condition = GetRimCondition();
            string conditionText = condition != null
                ? condition.GetConditionSummary()
                : "condition unavailable";
            return $"Bare {pickup.Item.DisplayName} | {conditionText} | Mount {ExpectedTireName()} directly on this rim, or press E with no matching tire equipped to pick the rim up.";
        }

        private bool IsPlayerAimingAtThisRim()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    interactionDistance,
                    ~0,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            P51BareRimServiceTarget target =
                hit.collider != null
                    ? hit.collider.GetComponentInParent<P51BareRimServiceTarget>()
                    : null;
            return target == this;
        }

        private void ResolvePlayerReferences()
        {
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<PlayerInventory>();
            }
            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }
            if (playerCamera == null && inventory != null)
            {
                playerCamera = inventory.GetComponentInChildren<Camera>();
            }
        }

        private void OnDisable()
        {
            if (pickup != null)
            {
                pickup.SetRuntimePickupBlocked(false);
            }
            mountProgress = 0f;
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
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
