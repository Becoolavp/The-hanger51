using System.Collections.Generic;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    public sealed class HangarShopUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Transform productListRoot;
        [SerializeField] private Button productButtonTemplate;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button closeButton;

        [Header("Text")]
        [SerializeField] private Text balanceText;
        [SerializeField] private Text shipmentCapacityText;
        [SerializeField] private Text selectedNameText;
        [SerializeField] private Text selectedCategoryText;
        [SerializeField] private Text selectedDescriptionText;
        [SerializeField] private Text selectedDeliveryText;
        [SerializeField] private Text selectedPriceText;
        [SerializeField] private Text statusText;

        [Header("Player Control")]
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private List<Behaviour> gameplayBehavioursToDisable =
            new List<Behaviour>();

        private readonly List<Button> spawnedProductButtons = new List<Button>();
        private readonly List<Behaviour> behavioursDisabledByShop = new List<Behaviour>();
        private HangarShopTerminal terminal;
        private int selectedCatalogIndex = -1;
        private bool isOpen;

        public static bool IsAnyShopOpen { get; private set; }
        public bool IsOpen => isOpen;

        private void Awake()
        {
            ResolveReferences();
            ConfigureButtons();
            if (shopPanel != null)
            {
                shopPanel.SetActive(false);
            }
            SetStatus(string.Empty);
        }

        private void Update()
        {
            if (!isOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Configure(
            GameObject configuredShopPanel,
            Transform configuredProductListRoot,
            Button configuredProductButtonTemplate,
            Button configuredBuyButton,
            Button configuredCloseButton,
            Text configuredBalanceText,
            Text configuredShipmentCapacityText,
            Text configuredSelectedNameText,
            Text configuredSelectedCategoryText,
            Text configuredSelectedDescriptionText,
            Text configuredSelectedDeliveryText,
            Text configuredSelectedPriceText,
            Text configuredStatusText,
            FirstPersonController configuredFirstPersonController,
            InventoryUI configuredInventoryUI,
            List<Behaviour> configuredBehavioursToDisable)
        {
            shopPanel = configuredShopPanel;
            productListRoot = configuredProductListRoot;
            productButtonTemplate = configuredProductButtonTemplate;
            buyButton = configuredBuyButton;
            closeButton = configuredCloseButton;
            balanceText = configuredBalanceText;
            shipmentCapacityText = configuredShipmentCapacityText;
            selectedNameText = configuredSelectedNameText;
            selectedCategoryText = configuredSelectedCategoryText;
            selectedDescriptionText = configuredSelectedDescriptionText;
            selectedDeliveryText = configuredSelectedDeliveryText;
            selectedPriceText = configuredSelectedPriceText;
            statusText = configuredStatusText;
            firstPersonController = configuredFirstPersonController;
            inventoryUI = configuredInventoryUI;
            gameplayBehavioursToDisable = configuredBehavioursToDisable
                ?? new List<Behaviour>();
            ConfigureButtons();
        }

        public void Open(HangarShopTerminal configuredTerminal)
        {
            if (configuredTerminal == null)
            {
                return;
            }

            if (isOpen)
            {
                terminal = configuredTerminal;
                RefreshAll();
                return;
            }

            ResolveReferences();
            terminal = configuredTerminal;
            selectedCatalogIndex = terminal.Catalog.Count > 0 ? 0 : -1;
            isOpen = true;
            IsAnyShopOpen = true;

            inventoryUI?.SetInventoryOpen(false);
            DisableGameplayBehaviours();

            if (firstPersonController != null)
            {
                firstPersonController.SetExternalInputBlocked(true);
            }

            if (shopPanel != null)
            {
                shopPanel.SetActive(true);
            }

            if (terminal.Wallet != null)
            {
                terminal.Wallet.BalanceChanged -= HandleBalanceChanged;
                terminal.Wallet.BalanceChanged += HandleBalanceChanged;
            }

            BuildProductButtons();
            RefreshAll();
            SetStatus("Select a product. Purchases are delivered to the marked shipment bays.");
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            if (terminal != null && terminal.Wallet != null)
            {
                terminal.Wallet.BalanceChanged -= HandleBalanceChanged;
            }

            isOpen = false;
            IsAnyShopOpen = false;
            terminal = null;
            selectedCatalogIndex = -1;

            if (shopPanel != null)
            {
                shopPanel.SetActive(false);
            }

            RestoreGameplayBehaviours();
            if (firstPersonController != null)
            {
                firstPersonController.SetExternalInputBlocked(false);
            }
        }

        private void ConfigureButtons()
        {
            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(BuySelectedProduct);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        private void BuildProductButtons()
        {
            for (int index = 0; index < spawnedProductButtons.Count; index++)
            {
                if (spawnedProductButtons[index] != null)
                {
                    Destroy(spawnedProductButtons[index].gameObject);
                }
            }
            spawnedProductButtons.Clear();

            if (terminal == null
                || productListRoot == null
                || productButtonTemplate == null)
            {
                return;
            }

            for (int index = 0; index < terminal.Catalog.Count; index++)
            {
                ShopCatalogEntry product = terminal.Catalog[index];
                Button button = Instantiate(productButtonTemplate, productListRoot);
                button.gameObject.name = $"Product {index + 1} - {product.DisplayName}";
                button.gameObject.SetActive(true);

                Text buttonText = button.GetComponentInChildren<Text>(true);
                if (buttonText != null)
                {
                    buttonText.text = $"{product.DisplayName}\n{product.Category}   •   ${product.Price:N0}";
                }

                int capturedIndex = index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectProduct(capturedIndex));
                spawnedProductButtons.Add(button);
            }

            if (productButtonTemplate.gameObject.activeSelf)
            {
                productButtonTemplate.gameObject.SetActive(false);
            }

            if (productListRoot is RectTransform listRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
            }
        }

        private void SelectProduct(int catalogIndex)
        {
            selectedCatalogIndex = catalogIndex;
            RefreshSelectedProduct();
            SetStatus(string.Empty);
        }

        private void BuySelectedProduct()
        {
            if (terminal == null || selectedCatalogIndex < 0)
            {
                SetStatus("Select a product first.");
                return;
            }

            bool purchased = terminal.TryPurchase(
                selectedCatalogIndex,
                out string resultMessage);
            SetStatus(resultMessage);
            RefreshAll();

            if (purchased)
            {
                RefreshSelectedProduct();
            }
        }

        private void RefreshAll()
        {
            RefreshBalance();
            RefreshShipmentCapacity();
            RefreshSelectedProduct();
        }

        private void RefreshBalance()
        {
            if (balanceText != null)
            {
                balanceText.text = terminal != null && terminal.Wallet != null
                    ? $"Account balance: {terminal.Wallet.FormattedBalance}"
                    : "Account balance: unavailable";
            }
        }

        private void RefreshShipmentCapacity()
        {
            if (shipmentCapacityText != null)
            {
                ShipmentAreaController area = terminal != null
                    ? terminal.ShipmentArea
                    : null;
                shipmentCapacityText.text = area != null
                    ? $"Shipment bays open: {area.AvailableSlotCount}/{area.SlotCount}"
                    : "Shipment area unavailable";
            }
        }

        private void RefreshSelectedProduct()
        {
            ShopCatalogEntry product = terminal != null
                ? terminal.GetProduct(selectedCatalogIndex)
                : null;
            bool hasProduct = product != null;

            if (selectedNameText != null)
            {
                selectedNameText.text = hasProduct ? product.DisplayName : "Select a product";
            }
            if (selectedCategoryText != null)
            {
                selectedCategoryText.text = hasProduct ? product.Category : string.Empty;
            }
            if (selectedDescriptionText != null)
            {
                selectedDescriptionText.text = hasProduct
                    ? product.Description
                    : "Choose a part or complete assembly from the catalog.";
            }
            if (selectedDeliveryText != null)
            {
                selectedDeliveryText.text = hasProduct
                    ? product.DeliveryDescription
                    : string.Empty;
            }
            if (selectedPriceText != null)
            {
                selectedPriceText.text = hasProduct
                    ? $"Price: ${product.Price:N0}"
                    : "Price: —";
            }
            if (buyButton != null)
            {
                buyButton.interactable = hasProduct
                    && terminal != null
                    && terminal.Wallet != null
                    && terminal.Wallet.CanAfford(product.Price)
                    && terminal.ShipmentArea != null
                    && terminal.ShipmentArea.AvailableSlotCount > 0;
            }
        }

        private void HandleBalanceChanged(int _)
        {
            RefreshBalance();
            RefreshSelectedProduct();
        }

        private void SetStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusText.text));
        }

        private void DisableGameplayBehaviours()
        {
            behavioursDisabledByShop.Clear();
            if (gameplayBehavioursToDisable == null)
            {
                return;
            }

            for (int index = 0; index < gameplayBehavioursToDisable.Count; index++)
            {
                Behaviour behaviour = gameplayBehavioursToDisable[index];
                if (behaviour == null || behaviour == this || !behaviour.enabled)
                {
                    continue;
                }

                behaviour.enabled = false;
                behavioursDisabledByShop.Add(behaviour);
            }
        }

        private void RestoreGameplayBehaviours()
        {
            for (int index = 0; index < behavioursDisabledByShop.Count; index++)
            {
                Behaviour behaviour = behavioursDisabledByShop[index];
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }
            behavioursDisabledByShop.Clear();
        }

        private void ResolveReferences()
        {
            if (firstPersonController == null)
            {
                firstPersonController = FindFirstObjectByType<FirstPersonController>();
            }
            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                Close();
            }
        }
    }
}
