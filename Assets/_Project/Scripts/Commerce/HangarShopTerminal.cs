using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HangarShopTerminal : MonoBehaviour
    {
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private ShipmentAreaController shipmentArea;
        [SerializeField] private HangarShopUI shopUI;
        [SerializeField] private Renderer terminalScreenRenderer;
        [SerializeField] private List<ShopCatalogEntry> catalog = new List<ShopCatalogEntry>();

        public IReadOnlyList<ShopCatalogEntry> Catalog => catalog;
        public PlayerWallet Wallet => wallet;
        public ShipmentAreaController ShipmentArea => shipmentArea;
        public string InteractionText => "E: use Hanger 51 parts computer";

        public void Configure(
            PlayerWallet configuredWallet,
            ShipmentAreaController configuredShipmentArea,
            HangarShopUI configuredShopUI,
            Renderer configuredScreenRenderer,
            List<ShopCatalogEntry> configuredCatalog)
        {
            wallet = configuredWallet;
            shipmentArea = configuredShipmentArea;
            shopUI = configuredShopUI;
            terminalScreenRenderer = configuredScreenRenderer;
            catalog = configuredCatalog ?? new List<ShopCatalogEntry>();
        }

        public bool TryOpen(out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveReferences();

            if (shopUI == null || wallet == null || shipmentArea == null)
            {
                resultMessage = "The shop terminal is missing its wallet, shipment area, or UI connection.";
                return false;
            }

            shopUI.Open(this);
            resultMessage = "Opened the Hanger 51 parts terminal.";
            return true;
        }

        public bool TryPurchase(int catalogIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveReferences();

            ShopCatalogEntry product = GetProduct(catalogIndex);
            if (product == null || !product.IsConfigured)
            {
                resultMessage = "That catalog entry is not configured.";
                return false;
            }

            if (wallet == null || !wallet.CanAfford(product.Price))
            {
                string balance = wallet != null ? wallet.FormattedBalance : "$0";
                resultMessage = $"Insufficient funds. Balance: {balance}.";
                return false;
            }

            if (shipmentArea == null)
            {
                resultMessage = "The shipment area is unavailable.";
                return false;
            }

            ShipmentCrateController createdCrate;
            string shipmentReason;
            if (!shipmentArea.TryCreateShipment(
                    product,
                    out createdCrate,
                    out shipmentReason))
            {
                resultMessage = shipmentReason;
                return false;
            }

            if (!wallet.TrySpend(product.Price))
            {
                if (createdCrate != null)
                {
                    Destroy(createdCrate.gameObject);
                }
                resultMessage = "The purchase could not be completed.";
                return false;
            }

            resultMessage = $"Purchased {product.DisplayName} for ${product.Price:N0}. "
                + $"A labeled crate is waiting in the shipment area. {shipmentReason}";
            return true;
        }

        public ShopCatalogEntry GetProduct(int catalogIndex)
        {
            return catalogIndex >= 0
                && catalog != null
                && catalogIndex < catalog.Count
                    ? catalog[catalogIndex]
                    : null;
        }

        private void ResolveReferences()
        {
            if (wallet == null)
            {
                wallet = FindFirstObjectByType<PlayerWallet>();
            }
            if (shipmentArea == null)
            {
                shipmentArea = FindFirstObjectByType<ShipmentAreaController>();
            }
            if (shopUI == null)
            {
                shopUI = FindFirstObjectByType<HangarShopUI>();
            }
        }
    }
}
