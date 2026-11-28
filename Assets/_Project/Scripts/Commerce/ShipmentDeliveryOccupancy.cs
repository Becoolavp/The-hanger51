using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    public sealed class ShipmentDeliveryOccupancy : MonoBehaviour
    {
        [SerializeField] private ShipmentAreaController shipmentArea;
        [SerializeField] private int shipmentSlotIndex = -1;
        [SerializeField] private Vector3 originalDeliveryPosition;
        [SerializeField, Min(0.5f)] private float releaseDistance = 3.5f;

        private bool released;

        public void Configure(
            ShipmentAreaController configuredShipmentArea,
            int configuredSlotIndex,
            Vector3 configuredDeliveryPosition,
            float configuredReleaseDistance = 3.5f)
        {
            shipmentArea = configuredShipmentArea;
            shipmentSlotIndex = configuredSlotIndex;
            originalDeliveryPosition = configuredDeliveryPosition;
            releaseDistance = Mathf.Max(0.5f, configuredReleaseDistance);
            released = false;
        }

        private void Update()
        {
            if (released)
            {
                return;
            }

            Vector2 current = new Vector2(transform.position.x, transform.position.z);
            Vector2 original = new Vector2(
                originalDeliveryPosition.x,
                originalDeliveryPosition.z);
            if (Vector2.Distance(current, original) >= releaseDistance)
            {
                ReleaseSlot();
                Destroy(this);
            }
        }

        private void ReleaseSlot()
        {
            if (released)
            {
                return;
            }

            released = true;
            shipmentArea?.ReleaseSlot(shipmentSlotIndex, this);
        }

        private void OnDestroy()
        {
            ReleaseSlot();
        }

        private void OnValidate()
        {
            releaseDistance = Mathf.Max(0.5f, releaseDistance);
        }
    }
}
