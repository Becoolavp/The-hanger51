using UnityEngine;

namespace Hanger51.Inventory
{
    [RequireComponent(typeof(Collider))]
    public sealed class InventoryPickup : MonoBehaviour
    {
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;

        public InventoryItemDefinition Item => item;
        public int Quantity => quantity;

        public string InteractionText
        {
            get
            {
                if (item == null)
                {
                    return string.Empty;
                }

                string quantityText = quantity > 1 ? $" x{quantity}" : string.Empty;
                return $"Press E to pick up {item.DisplayName}{quantityText}";
            }
        }

        public bool TryPickup(PlayerInventory inventory)
        {
            if (inventory == null || item == null || quantity <= 0)
            {
                return false;
            }

            int remaining = inventory.AddItem(item, quantity);
            if (remaining == quantity)
            {
                return false;
            }

            quantity = remaining;

            if (quantity <= 0)
            {
                Destroy(gameObject);
            }

            return true;
        }

        private void OnValidate()
        {
            quantity = Mathf.Max(1, quantity);
        }
    }
}
