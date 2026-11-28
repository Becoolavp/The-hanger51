using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    public sealed class P51LandingGearReplacementService : MonoBehaviour
    {
        public const string MainTireItemId = "p51-main-landing-tire";
        public const string TailTireItemId = "p51-tailwheel-tire";

        private P51LandingGearMaintenanceController maintenance;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            ResolveBindings();
        }

        private void OnEnable()
        {
            ResolveBindings();
        }

        public bool CanUseEquippedReplacement(int wheelIndex, PlayerInventory inventory)
        {
            if (inventory == null || inventory.EquippedItem == null)
            {
                return false;
            }

            string expected = wheelIndex == 2 ? TailTireItemId : MainTireItemId;
            return inventory.EquippedItem.ItemId == expected;
        }

        public bool TryInstallReplacementTire(
            int wheelIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            _ = wheelIndex;
            _ = inventory;
            ResolveBindings();
            resultMessage = IsReady
                ? "Replacement tires are installed into the loose wheel assembly off the aircraft. Remove/carry the complete wheel, separate it on the floor, rebuild it with the replacement tire, then carry the completed wheel back to its original strut."
                : "The landing-gear replacement system is not ready.";
            return false;
        }

        private void ResolveBindings()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }
            IsReady = maintenance != null;
        }

        private void OnValidate()
        {
            ResolveBindings();
        }
    }
}
