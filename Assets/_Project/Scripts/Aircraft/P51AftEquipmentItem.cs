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
        private bool held;

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
            ApplyPhysicsState();
        }

        private void OnEnable()
        {
            ResolvePhysics();
            ApplyPhysicsState();
        }

        public void Configure(P51AftEquipmentKind kind, float configuredBatteryVoltage = 25.2f)
        {
            equipmentKind = kind;
            batteryVoltage = kind == P51AftEquipmentKind.Battery
                ? Mathf.Clamp(configuredBatteryVoltage, 0f, 28f)
                : 0f;
            installedBay = null;
            installedSlotIndex = -1;
            held = false;
            ResolvePhysics();
            ApplyPhysicsState();
        }

        public void SetInstalled(P51AftEquipmentBay bay, int slotIndex, Transform slotAnchor)
        {
            installedBay = bay;
            installedSlotIndex = slotIndex;
            held = false;
            ResolvePhysics();

            if (slotAnchor != null)
            {
                transform.SetParent(slotAnchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            ApplyPhysicsState();
        }

        public void SetRemovedFromBay()
        {
            installedBay = null;
            installedSlotIndex = -1;
            held = false;
            transform.SetParent(null, true);
            ResolvePhysics();
            ApplyPhysicsState();
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

        public void SetHeld(bool isHeld)
        {
            held = isHeld;
            ResolvePhysics();
            ApplyPhysicsState();
        }

        // Editor setup/migration tools use this to repair previously saved aft equipment whose
        // installed collider was left solid. It is also safe to call at runtime.
        public void RefreshPhysicsState()
        {
            ResolvePhysics();
            ApplyPhysicsState();
        }

        private void ApplyPhysicsState()
        {
            bool installed = IsInstalled;
            bool loose = !held && !installed;

            if (body != null)
            {
                body.isKinematic = !loose;
                body.useGravity = loose;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            if (interactionCollider == null && colliders.Length > 0)
            {
                interactionCollider = colliders[0];
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider itemCollider = colliders[i];
                if (itemCollider == null)
                {
                    continue;
                }

                bool isInteractionCollider = itemCollider == interactionCollider;
                if (held)
                {
                    itemCollider.enabled = false;
                    continue;
                }

                // Installed service items live inside the aircraft's own dynamic Rigidbody.
                // A solid child Rigidbody collider can collide with the parent aircraft's broad
                // fuselage/rack colliders and inject huge impulses into the airplane. Keep only
                // one trigger collider for raycast interaction while installed. Loose equipment
                // gets that same primary collider back as a normal physical collider.
                itemCollider.enabled = isInteractionCollider;
                itemCollider.isTrigger = installed;
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
                if (interactionCollider == null)
                {
                    Collider[] colliders = GetComponentsInChildren<Collider>(true);
                    if (colliders.Length > 0)
                    {
                        interactionCollider = colliders[0];
                    }
                }
            }
        }
    }
}
