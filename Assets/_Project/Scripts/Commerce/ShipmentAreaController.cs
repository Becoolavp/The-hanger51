using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    public sealed class ShipmentAreaController : MonoBehaviour
    {
        [Serializable]
        public sealed class ShipmentSlot
        {
            [SerializeField] private Transform crateAnchor;
            [SerializeField] private Transform contentAnchor;
            [NonSerialized] private ShipmentCrateController activeCrate;

            public Transform CrateAnchor => crateAnchor;
            public Transform ContentAnchor => contentAnchor;
            public ShipmentCrateController ActiveCrate => activeCrate;
            public bool IsAvailable => crateAnchor != null && activeCrate == null;

            public void Configure(Transform configuredCrateAnchor, Transform configuredContentAnchor)
            {
                crateAnchor = configuredCrateAnchor;
                contentAnchor = configuredContentAnchor;
                activeCrate = null;
            }

            public void SetActiveCrate(ShipmentCrateController crate)
            {
                activeCrate = crate;
            }
        }

        [SerializeField] private GameObject crateTemplate;
        [SerializeField] private List<ShipmentSlot> shipmentSlots = new List<ShipmentSlot>();

        public int SlotCount => shipmentSlots != null ? shipmentSlots.Count : 0;
        public int AvailableSlotCount
        {
            get
            {
                int count = 0;
                if (shipmentSlots == null)
                {
                    return count;
                }

                for (int index = 0; index < shipmentSlots.Count; index++)
                {
                    ShipmentSlot slot = shipmentSlots[index];
                    if (slot != null && slot.IsAvailable)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Configure(GameObject configuredCrateTemplate, List<ShipmentSlot> configuredSlots)
        {
            crateTemplate = configuredCrateTemplate;
            shipmentSlots = configuredSlots ?? new List<ShipmentSlot>();
            ClearRuntimeAssignments();
        }

        public bool TryCreateShipment(
            ShopCatalogEntry product,
            out ShipmentCrateController createdCrate,
            out string reason)
        {
            createdCrate = null;
            reason = string.Empty;

            if (product == null || !product.IsConfigured)
            {
                reason = "That shop product is not configured for delivery.";
                return false;
            }

            if (crateTemplate == null)
            {
                reason = "The shipment crate template is missing.";
                return false;
            }

            int availableIndex = FindAvailableSlotIndex();
            if (availableIndex < 0)
            {
                reason = "All shipment bays are occupied. Unbox an existing delivery first.";
                return false;
            }

            ShipmentSlot slot = shipmentSlots[availableIndex];
            GameObject crateObject = Instantiate(
                crateTemplate,
                slot.CrateAnchor.position,
                slot.CrateAnchor.rotation);
            crateObject.name = $"Shipment Crate - {product.DisplayName}";

            ShipmentCrateController crateController =
                crateObject.GetComponent<ShipmentCrateController>();
            if (crateController == null)
            {
                Destroy(crateObject);
                reason = "The shipment crate template has no crate controller.";
                return false;
            }

            slot.SetActiveCrate(crateController);
            crateController.Configure(
                product,
                this,
                availableIndex,
                slot.ContentAnchor != null ? slot.ContentAnchor : slot.CrateAnchor);
            crateObject.SetActive(true);

            createdCrate = crateController;
            reason = $"Shipment assigned to bay {availableIndex + 1}.";
            return true;
        }

        public void ReleaseSlot(int slotIndex, ShipmentCrateController crate)
        {
            if (shipmentSlots == null
                || slotIndex < 0
                || slotIndex >= shipmentSlots.Count)
            {
                return;
            }

            ShipmentSlot slot = shipmentSlots[slotIndex];
            if (slot != null && (slot.ActiveCrate == null || slot.ActiveCrate == crate))
            {
                slot.SetActiveCrate(null);
            }
        }

        private int FindAvailableSlotIndex()
        {
            if (shipmentSlots == null)
            {
                return -1;
            }

            for (int index = 0; index < shipmentSlots.Count; index++)
            {
                ShipmentSlot slot = shipmentSlots[index];
                if (slot != null && slot.IsAvailable)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ClearRuntimeAssignments()
        {
            if (shipmentSlots == null)
            {
                return;
            }

            for (int index = 0; index < shipmentSlots.Count; index++)
            {
                shipmentSlots[index]?.SetActiveCrate(null);
            }
        }

        private void Awake()
        {
            ClearRuntimeAssignments();
        }
    }
}
