using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51AftEquipmentBay : MonoBehaviour
    {
        [SerializeField] private Transform panelAnchor;
        [SerializeField] private P51AftAccessPanel accessPanel;
        [SerializeField] private P51AftEquipmentSlot[] slots = new P51AftEquipmentSlot[4];
        [SerializeField] private P51AftEquipmentItem[] installedItems = new P51AftEquipmentItem[4];
        [SerializeField, Min(0f)] private float minimumStarterVoltage = 20.5f;
        [SerializeField, Min(0f)] private float starterVoltageDrop = 0.45f;

        public Transform PanelAnchor => panelAnchor;
        public P51AftAccessPanel AccessPanel => accessPanel;
        public bool AccessOpen => accessPanel == null || !accessPanel.IsInstalled;
        public float MinimumStarterVoltage => minimumStarterVoltage;
        public P51AftEquipmentItem InstalledBattery => GetInstalledItem(0);

        public void Configure(
            Transform configuredPanelAnchor,
            P51AftAccessPanel configuredPanel,
            P51AftEquipmentSlot[] configuredSlots)
        {
            panelAnchor = configuredPanelAnchor;
            accessPanel = configuredPanel;
            slots = configuredSlots ?? new P51AftEquipmentSlot[0];
            if (installedItems == null || installedItems.Length < slots.Length)
            {
                installedItems = new P51AftEquipmentItem[slots.Length];
            }
        }

        public P51AftEquipmentItem GetInstalledItem(int slotIndex)
        {
            if (installedItems == null || slotIndex < 0 || slotIndex >= installedItems.Length)
            {
                return null;
            }
            return installedItems[slotIndex];
        }

        public bool TryInstall(P51AftEquipmentItem item, P51AftEquipmentSlot slot, out string message)
        {
            message = string.Empty;
            if (!AccessOpen)
            {
                message = "Remove the aft access panel first.";
                return false;
            }
            if (item == null || slot == null || slot.Bay != this)
            {
                message = "That equipment does not belong in this rack position.";
                return false;
            }
            if (item.EquipmentKind != slot.AcceptedKind)
            {
                message = slot.AcceptedKind == P51AftEquipmentKind.Battery
                    ? "This rack position accepts the aircraft battery."
                    : "This rack position accepts an oxygen bottle.";
                return false;
            }
            if (GetInstalledItem(slot.SlotIndex) != null)
            {
                message = "That rack position is already occupied.";
                return false;
            }

            EnsureInstalledArray(slot.SlotIndex + 1);
            installedItems[slot.SlotIndex] = item;
            item.SetInstalled(this, slot.SlotIndex, slot.transform);
            message = item.EquipmentKind == P51AftEquipmentKind.Battery
                ? $"Installed aircraft battery ({item.BatteryVoltage:F1} V)."
                : $"Installed oxygen bottle in rack position {slot.SlotIndex}.";
            return true;
        }

        public void InstallDirect(P51AftEquipmentItem item, P51AftEquipmentSlot slot)
        {
            if (item == null || slot == null)
            {
                return;
            }
            EnsureInstalledArray(slot.SlotIndex + 1);
            installedItems[slot.SlotIndex] = item;
            item.SetInstalled(this, slot.SlotIndex, slot.transform);
        }

        public bool TryRemove(P51AftEquipmentItem item, out string message)
        {
            message = string.Empty;
            if (!AccessOpen)
            {
                message = "Remove the aft access panel first.";
                return false;
            }
            if (item == null || item.InstalledBay != this)
            {
                message = "That item is not installed in this aft rack.";
                return false;
            }

            int index = item.InstalledSlotIndex;
            if (installedItems != null && index >= 0 && index < installedItems.Length)
            {
                installedItems[index] = null;
            }
            item.SetRemovedFromBay();
            message = $"Removed {item.DisplayName} from the aft equipment rack.";
            return true;
        }

        public bool TrySupplyStarterPower(out string message)
        {
            P51AftEquipmentItem battery = InstalledBattery;
            if (battery == null || battery.EquipmentKind != P51AftEquipmentKind.Battery)
            {
                message = "Start blocked: no aircraft battery is installed in the aft equipment bay.";
                return false;
            }
            if (!battery.CanSupplyStarter(minimumStarterVoltage))
            {
                message = $"Start blocked: battery voltage is only {battery.BatteryVoltage:F1} V. Minimum starter voltage is {minimumStarterVoltage:F1} V.";
                return false;
            }

            float before = battery.BatteryVoltage;
            battery.ConsumeStarterCharge(starterVoltageDrop);
            message = $"Battery supplied starter power ({before:F1} V → {battery.BatteryVoltage:F1} V).";
            return true;
        }

        private void EnsureInstalledArray(int length)
        {
            if (installedItems != null && installedItems.Length >= length)
            {
                return;
            }

            P51AftEquipmentItem[] replacement = new P51AftEquipmentItem[Mathf.Max(length, 4)];
            if (installedItems != null)
            {
                for (int i = 0; i < installedItems.Length && i < replacement.Length; i++)
                {
                    replacement[i] = installedItems[i];
                }
            }
            installedItems = replacement;
        }
    }
}
