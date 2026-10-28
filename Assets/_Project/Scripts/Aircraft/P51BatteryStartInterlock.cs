using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    public sealed class P51BatteryStartInterlock : MonoBehaviour
    {
        [SerializeField] private P51AftEquipmentBay aftEquipmentBay;

        public P51AftEquipmentBay AftEquipmentBay => aftEquipmentBay;
        public bool IsConfigured => aftEquipmentBay != null;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public bool TryAuthorizeStarter(out string message)
        {
            ResolveReferences();
            if (aftEquipmentBay == null)
            {
                message = "Start blocked: the aircraft electrical/battery system is not configured.";
                return false;
            }

            return aftEquipmentBay.TrySupplyStarterPower(out message);
        }

        private void ResolveReferences()
        {
            if (aftEquipmentBay == null)
            {
                aftEquipmentBay = GetComponentInChildren<P51AftEquipmentBay>(true);
            }
        }
    }
}
