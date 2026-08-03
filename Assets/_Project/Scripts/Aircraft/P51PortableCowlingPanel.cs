using UnityEngine;

namespace Hanger51.Aircraft
{
    [RequireComponent(typeof(Collider))]
    public sealed class P51PortableCowlingPanel : MonoBehaviour
    {
        [SerializeField] private P51AircraftServiceController serviceController;
        [SerializeField] private Collider pickupCollider;

        public P51AircraftServiceController ServiceController => serviceController;
        public bool CanPickUp => serviceController != null && serviceController.IsCowlingLoose;
        public string InteractionText => CanPickUp
            ? "E: pick up top engine cowling"
            : string.Empty;

        private void Awake()
        {
            ResolveReferences();
            RefreshFromService();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshFromService();
        }

        public void Configure(
            P51AircraftServiceController configuredServiceController,
            Collider configuredPickupCollider)
        {
            serviceController = configuredServiceController;
            pickupCollider = configuredPickupCollider;
            ResolveReferences();
            RefreshFromService();
        }

        public void RefreshFromService()
        {
            ResolveReferences();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = CanPickUp;
            }
        }

        private void ResolveReferences()
        {
            if (pickupCollider == null)
            {
                pickupCollider = GetComponent<Collider>();
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
