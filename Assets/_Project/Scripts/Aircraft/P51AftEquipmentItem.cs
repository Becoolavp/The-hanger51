using UnityEngine;

namespace Hanger51.Aircraft
{
    public enum P51AftEquipmentKind
    {
        Battery,
        OxygenBottle
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51AftEquipmentItem : MonoBehaviour
    {
        [SerializeField] private P51AftEquipmentKind equipmentKind;
        [SerializeField] private float batteryVoltage = 25.2f;
        [SerializeField] private P51AftEquipmentBay installedBay;
        [SerializeField] private int installedSlotIndex = -1;

        private Rigidbody body;
        private Collider interactionCollider;

        public P51AftEquipmentKind EquipmentKind => equipmentKind;
        public float BatteryVoltage => batteryVoltage;
        public bool IsInstalled => installedBay != null && installedSlotIndex >= 0;
        public P51AftEquipmentBay InstalledBay => installedBay;
        public int InstalledSlotIndex => installedSlotIndex;
        public string DisplayName => equipmentKind == P51AftEquipmentKind.Battery
            ? "24 V aircraft battery"
            : "oxygen bottle";

        private void Awake()
        {
            ResolvePhysics();
        }

        private void OnEnable()
        {
            ResolvePhysics();
        }

        public void Configure(P51AftEquipmentKind kind, float configuredBatteryVoltage = 25.2f)
        {
            equipmentKind = kind;
            batteryVoltage = kind == P51AftEquipmentKind.Battery
                ? Mathf.Clamp(configuredBatteryVoltage, 0f, 28f)
                : 0f;
            installedBay = null;
            installedSlotIndex = -1;
            ResolvePhysics();
            SetLoosePhysics(true);
        }

        public void SetInstalled(P51AftEquipmentBay bay, int slotIndex, Transform slotAnchor)
        {
            installedBay = bay;
            installedSlotIndex = slotIndex;
            ResolvePhysics();

            if (slotAnchor != null)
            {
                transform.SetParent(slotAnchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            SetLoosePhysics(false);
        }

        public void SetRemovedFromBay()
        {
            installedBay = null;
            installedSlotIndex = -1;
            transform.SetParent(null, true);
            SetLoosePhysics(true);
        }

        public bool CanSupplyStarter(float minimumVoltage)
        {
            return equipmentKind == P51AftEquipmentKind.Battery
                && batteryVoltage >= minimumVoltage;
        }

        public void ConsumeStarterCharge(float voltageDrop)
        {
            if (equipmentKind != P51AftEquipmentKind.Battery)
            {
                return;
            }

            batteryVoltage = Mathf.Clamp(batteryVoltage - Mathf.Max(0f, voltageDrop), 0f, 28f);
        }

        public void SetBatteryVoltage(float voltage)
        {
            if (equipmentKind == P51AftEquipmentKind.Battery)
            {
                batteryVoltage = Mathf.Clamp(voltage, 0f, 28f);
            }
        }

        public void SetHeld(bool held)
        {
            ResolvePhysics();
            if (body != null)
            {
                body.isKinematic = held || IsInstalled;
                body.useGravity = !held && !IsInstalled;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (interactionCollider != null)
            {
                interactionCollider.enabled = !held;
            }
        }

        private void SetLoosePhysics(bool loose)
        {
            ResolvePhysics();
            if (body != null)
            {
                body.isKinematic = !loose;
                body.useGravity = loose;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }
        }

        private void ResolvePhysics()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }
    }
}
