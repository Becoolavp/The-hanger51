using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51AftEquipmentSlot : MonoBehaviour
    {
        [SerializeField] private P51AftEquipmentBay bay;
        [SerializeField] private P51AftEquipmentKind acceptedKind;
        [SerializeField] private int slotIndex;

        public P51AftEquipmentBay Bay => bay;
        public P51AftEquipmentKind AcceptedKind => acceptedKind;
        public int SlotIndex => slotIndex;
        public P51AftEquipmentItem InstalledItem => bay != null ? bay.GetInstalledItem(slotIndex) : null;

        public void Configure(P51AftEquipmentBay configuredBay, P51AftEquipmentKind kind, int index)
        {
            bay = configuredBay;
            acceptedKind = kind;
            slotIndex = Mathf.Max(0, index);
        }
    }
}
