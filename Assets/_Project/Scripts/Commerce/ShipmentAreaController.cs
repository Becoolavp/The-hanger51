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
            [NonSerialized] private UnityEngine.Object activeOccupant;

            public Transform CrateAnchor => crateAnchor;
            public Transform ContentAnchor => contentAnchor;
            public UnityEngine.Object ActiveOccupant => activeOccupant;
            public bool IsAvailable => crateAnchor != null && activeOccupant == null;

            public void Configure(Transform configuredCrateAnchor, Transform configuredContentAnchor)
            {
                crateAnchor = configuredCrateAnchor;
                contentAnchor = configuredContentAnchor;
                activeOccupant = null;
            }

            public void SetActiveOccupant(UnityEngine.Object occupant)
            {
                activeOccupant = occupant;
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
                reason = "All shipment bays are occupied. Collect or move an existing delivery first.";
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

            slot.SetActiveOccupant(crateController);
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

        public bool TransferSlot(
            int slotIndex,
            UnityEngine.Object currentOccupant,
            UnityEngine.Object newOccupant)
        {
            ShipmentSlot slot = GetSlot(slotIndex);
            if (slot == null
                || (slot.ActiveOccupant != null
                    && slot.ActiveOccupant != currentOccupant))
            {
                return false;
            }

            slot.SetActiveOccupant(newOccupant);
            return true;
        }

        public void ReleaseSlot(int slotIndex, UnityEngine.Object occupant)
        {
            ShipmentSlot slot = GetSlot(slotIndex);
            if (slot != null
                && (slot.ActiveOccupant == null || slot.ActiveOccupant == occupant))
            {
                slot.SetActiveOccupant(null);
            }
        }

        private ShipmentSlot GetSlot(int slotIndex)
        {
            return shipmentSlots != null
                && slotIndex >= 0
                && slotIndex < shipmentSlots.Count
                    ? shipmentSlots[slotIndex]
                    : null;
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
                shipmentSlots[index]?.SetActiveOccupant(null);
            }
        }

        private void Awake()
        {
            ClearRuntimeAssignments();
        }
    }
}
