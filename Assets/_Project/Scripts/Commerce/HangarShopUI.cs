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
        enum ShopDepartment
        {
            AircraftParts,
            Weapons
        }

        [Header("Panel")]
        [SerializeField] GameObject shopPanel;
        [SerializeField] Transform productListRoot;
        [SerializeField] Button productButtonTemplate;
        [SerializeField] Button buyButton;
        [SerializeField] Button closeButton;

        [Header("Text")]
        [SerializeField] Text balanceText;
        [SerializeField] Text shipmentCapacityText;
        [SerializeField] Text selectedNameText;
        [SerializeField] Text selectedCategoryText;
        [SerializeField] Text selectedDescriptionText;
        [SerializeField] Text selectedDeliveryText;
        [SerializeField] Text selectedPriceText;
        [SerializeField] Text statusText;

        [Header("Player Control")]
        [SerializeField] FirstPersonController firstPersonController;
        [SerializeField] InventoryUI inventoryUI;
        [SerializeField] List<Behaviour> gameplayBehavioursToDisable =
            new List<Behaviour>();

        readonly List<Button> spawnedProductButtons = new List<Button>();
        readonly List<Behaviour> behavioursDisabledByShop = new List<Behaviour>();

        HangarShopTerminal terminal;
        int selectedCatalogIndex = -1;
        bool isOpen;

        ShopDepartment activeDepartment = ShopDepartment.AircraftParts;
        Button aircraftTabButton;
        Button weaponsTabButton;

        RawImage previewImage;
        GameObject previewRoot;
        GameObject previewModel;
        Camera previewCamera;
        Light previewKeyLight;
        Light previewFillLight;
        RenderTexture previewTexture;

        const int PreviewLayer = 31;

        public static bool IsAnyShopOpen { get; private set; }
        public bool IsOpen => isOpen;

        void Awake()
        {
            ResolveReferences();
            ConfigureButtons();
            EnsureDepartmentAndPreviewUi();

            if (shopPanel != null)
                shopPanel.SetActive(false);

            SetStatus(string.Empty);
        }

        void Update()
        {
            if (!isOpen)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (previewModel != null)
            {
                previewModel.transform.Rotate(
                    Vector3.up,
                    10f * Time.unscaledDeltaTime,
                    Space.World);
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
            EnsureDepartmentAndPreviewUi();
        }

        public void Open(HangarShopTerminal configuredTerminal)
        {
            if (configuredTerminal == null)
                return;

            if (isOpen)
            {
                terminal = configuredTerminal;
                EnsureValidSelectionForDepartment();
                BuildProductButtons();
                RefreshAll();
                return;
            }

            ResolveReferences();
            EnsureDepartmentAndPreviewUi();

            terminal = configuredTerminal;
            activeDepartment = ShopDepartment.AircraftParts;
            selectedCatalogIndex = FindFirstIndexForDepartment(activeDepartment);
            isOpen = true;
            IsAnyShopOpen = true;

            inventoryUI?.SetInventoryOpen(false);
            DisableGameplayBehaviours();

            if (firstPersonController != null)
                firstPersonController.SetExternalInputBlocked(true);

            if (shopPanel != null)
                shopPanel.SetActive(true);

            if (terminal.Wallet != null)
            {
                terminal.Wallet.BalanceChanged -= HandleBalanceChanged;
                terminal.Wallet.BalanceChanged += HandleBalanceChanged;
            }

            RefreshDepartmentButtons();
            BuildProductButtons();
            RefreshAll();
            SetStatus("Choose Aircraft Parts or Weapons, then select a product. Purchases are delivered to the marked shipment bays.");
        }

        public void Close()
        {
            if (!isOpen)
                return;

            if (terminal != null && terminal.Wallet != null)
                terminal.Wallet.BalanceChanged -= HandleBalanceChanged;

            isOpen = false;
            IsAnyShopOpen = false;
            terminal = null;
            selectedCatalogIndex = -1;

            ClearPreviewModel();

            if (shopPanel != null)
                shopPanel.SetActive(false);

            RestoreGameplayBehaviours();

            if (firstPersonController != null)
                firstPersonController.SetExternalInputBlocked(false);
        }

        void ConfigureButtons()
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

        void EnsureDepartmentAndPreviewUi()
        {
            if (shopPanel == null)
                return;

            Transform window = shopPanel.transform.Find("Terminal Window");
            if (window == null)
                return;

            Transform productPanel = window.Find("Product List Panel");
            if (productPanel != null)
            {
                Transform scroll = productPanel.Find("Product Scroll View");
                if (scroll is RectTransform scrollRect)
                {
                    scrollRect.offsetMin = new Vector2(12f,12f);
                    scrollRect.offsetMax = new Vector2(-12f,-62f);
                }

                if (aircraftTabButton == null)
                {
                    aircraftTabButton = CreateRuntimeButton(
                        productPanel,
                        "Aircraft Parts Tab",
                        "AIRCRAFT PARTS",
                        new Vector2(12f,-10f),
                        new Vector2(190f,40f));

                    aircraftTabButton.onClick.AddListener(
                        () => SwitchDepartment(ShopDepartment.AircraftParts));
                }

                if (weaponsTabButton == null)
                {
                    weaponsTabButton = CreateRuntimeButton(
                        productPanel,
                        "Weapons Tab",
                        "WEAPONS",
                        new Vector2(214f,-10f),
                        new Vector2(190f,40f));

                    weaponsTabButton.onClick.AddListener(
                        () => SwitchDepartment(ShopDepartment.Weapons));
                }
            }

            Transform detailsPanel = window.Find("Product Details Panel");
            if (detailsPanel != null)
            {
                if (selectedDescriptionText != null)
                {
                    RectTransform descriptionRect = selectedDescriptionText.rectTransform;
                    descriptionRect.sizeDelta = new Vector2(300f,220f);
                }

                if (previewImage == null)
                {
                    GameObject previewObject = new GameObject(
                        "3D Product Preview",
                        typeof(RectTransform),
                        typeof(RawImage));

                    previewObject.transform.SetParent(detailsPanel,false);
                    previewImage = previewObject.GetComponent<RawImage>();
                    previewImage.color = Color.white;

                    RectTransform previewRect = previewObject.GetComponent<RectTransform>();
                    previewRect.anchorMin = previewRect.anchorMax = new Vector2(0f,1f);
                    previewRect.pivot = new Vector2(0f,1f);
                    previewRect.anchoredPosition = new Vector2(338f,-124f);
                    previewRect.sizeDelta = new Vector2(318f,220f);
                }
            }

            EnsurePreviewInfrastructure();
            RefreshDepartmentButtons();
        }

        Button CreateRuntimeButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

            buttonObject.transform.SetParent(parent,false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f,1f);
            rect.pivot = new Vector2(0f,1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(.10f,.15f,.18f,1f);

            GameObject textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(Text));

            textObject.transform.SetParent(buttonObject.transform,false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f,2f);
            textRect.offsetMax = new Vector2(-8f,-2f);

            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.font = selectedNameText != null ? selectedNameText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return buttonObject.GetComponent<Button>();
        }

        void SwitchDepartment(ShopDepartment department)
        {
            if (activeDepartment == department && selectedCatalogIndex >= 0)
                return;

            activeDepartment = department;
            selectedCatalogIndex = FindFirstIndexForDepartment(department);

            RefreshDepartmentButtons();
            BuildProductButtons();
            RefreshAll();

            SetStatus(
                department == ShopDepartment.Weapons
                    ? "Weapons department — select a firearm, ammunition, accessory, grenade, or medical item."
                    : "Aircraft Parts department — engine, airframe, service, and maintenance products.");
        }

        void RefreshDepartmentButtons()
        {
            SetTabVisual(aircraftTabButton, activeDepartment == ShopDepartment.AircraftParts);
            SetTabVisual(weaponsTabButton, activeDepartment == ShopDepartment.Weapons);
        }

        static void SetTabVisual(Button button,bool selected)
        {
            if (button == null)
                return;

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(.08f,.42f,.56f,1f)
                    : new Color(.10f,.15f,.18f,1f);
            }
        }

        bool MatchesDepartment(ShopCatalogEntry product,ShopDepartment department)
        {
            if (product == null)
                return false;

            bool combat =
                !string.IsNullOrWhiteSpace(product.ProductId) &&
                product.ProductId.StartsWith(
                    "combat-",
                    System.StringComparison.Ordinal);

            return department == ShopDepartment.Weapons
                ? combat
                : !combat;
        }

        int FindFirstIndexForDepartment(ShopDepartment department)
        {
            if (terminal == null)
                return -1;

            for (int i=0;i<terminal.Catalog.Count;i++)
            {
                if (MatchesDepartment(terminal.Catalog[i],department))
                    return i;
            }

            return -1;
        }

        void EnsureValidSelectionForDepartment()
        {
            ShopCatalogEntry selected =
                terminal != null
                    ? terminal.GetProduct(selectedCatalogIndex)
                    : null;

            if (!MatchesDepartment(selected,activeDepartment))
                selectedCatalogIndex = FindFirstIndexForDepartment(activeDepartment);
        }

        void BuildProductButtons()
        {
            for (int index=0;index<spawnedProductButtons.Count;index++)
            {
                if (spawnedProductButtons[index] != null)
                    Destroy(spawnedProductButtons[index].gameObject);
            }
            spawnedProductButtons.Clear();

            if (terminal == null ||
                productListRoot == null ||
                productButtonTemplate == null)
                return;

            for (int index=0;index<terminal.Catalog.Count;index++)
            {
                ShopCatalogEntry product = terminal.Catalog[index];
                if (!MatchesDepartment(product,activeDepartment))
                    continue;

                Button button =
                    Instantiate(productButtonTemplate,productListRoot);

                button.gameObject.name =
                    "Product " + (index+1) + " - " + product.DisplayName;
                button.gameObject.SetActive(true);

                Text buttonText =
                    button.GetComponentInChildren<Text>(true);

                if (buttonText != null)
                {
                    buttonText.text =
                        product.DisplayName +
                        "\n" +
                        product.Category +
                        "   •   $" +
                        product.Price.ToString("N0");
                }

                int capturedIndex = index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(
                    () => SelectProduct(capturedIndex));

                spawnedProductButtons.Add(button);
            }

            if (productButtonTemplate.gameObject.activeSelf)
                productButtonTemplate.gameObject.SetActive(false);

            if (productListRoot is RectTransform listRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
        }

        void SelectProduct(int catalogIndex)
        {
            selectedCatalogIndex = catalogIndex;
            RefreshSelectedProduct();
            SetStatus(string.Empty);
        }

        void BuySelectedProduct()
        {
            if (terminal == null || selectedCatalogIndex < 0)
            {
                SetStatus("Select a product first.");
                return;
            }

            bool purchased =
                terminal.TryPurchase(
                    selectedCatalogIndex,
                    out string resultMessage);

            SetStatus(resultMessage);
            RefreshAll();

            if (purchased)
                RefreshSelectedProduct();
        }

        void RefreshAll()
        {
            RefreshBalance();
            RefreshShipmentCapacity();
            RefreshSelectedProduct();
        }

        void RefreshBalance()
        {
            if (balanceText != null)
            {
                balanceText.text =
                    terminal != null && terminal.Wallet != null
                        ? "Account balance: " + terminal.Wallet.FormattedBalance
                        : "Account balance: unavailable";
            }
        }

        void RefreshShipmentCapacity()
        {
            if (shipmentCapacityText == null)
                return;

            ShipmentAreaController area =
                terminal != null
                    ? terminal.ShipmentArea
                    : null;

            shipmentCapacityText.text =
                area != null
                    ? "Shipment bays open: " +
                      area.AvailableSlotCount +
                      "/" +
                      area.SlotCount
                    : "Shipment area unavailable";
        }

        void RefreshSelectedProduct()
        {
            ShopCatalogEntry product =
                terminal != null
                    ? terminal.GetProduct(selectedCatalogIndex)
                    : null;

            bool hasProduct =
                product != null &&
                MatchesDepartment(product,activeDepartment);

            if (selectedNameText != null)
                selectedNameText.text = hasProduct ? product.DisplayName : "Select a product";

            if (selectedCategoryText != null)
                selectedCategoryText.text = hasProduct ? product.Category : string.Empty;

            if (selectedDescriptionText != null)
            {
                selectedDescriptionText.text =
                    hasProduct
                        ? product.Description
                        : "Choose a product from the selected department.";
            }

            if (selectedDeliveryText != null)
            {
                selectedDeliveryText.text =
                    hasProduct
                        ? product.DeliveryDescription
                        : string.Empty;
            }

            if (selectedPriceText != null)
            {
                selectedPriceText.text =
                    hasProduct
                        ? "Price: $" + product.Price.ToString("N0")
                        : "Price: —";
            }

            if (buyButton != null)
            {
                buyButton.interactable =
                    hasProduct &&
                    terminal != null &&
                    terminal.Wallet != null &&
                    terminal.Wallet.CanAfford(product.Price) &&
                    terminal.ShipmentArea != null &&
                    terminal.ShipmentArea.AvailableSlotCount > 0;
            }

            UpdatePreview(hasProduct ? product : null);
        }

        void EnsurePreviewInfrastructure()
        {
            if (previewRoot != null)
                return;

            previewRoot = new GameObject("H51 Shop Preview World");
            previewRoot.transform.SetParent(transform,false);
            previewRoot.transform.position = new Vector3(50000f,50000f,50000f);
            SetLayerRecursively(previewRoot,PreviewLayer);

            GameObject cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(previewRoot.transform,false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.025f,.032f,.038f,1f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = .01f;
            previewCamera.farClipPlane = 100f;

            GameObject keyObject = new GameObject("Preview Key Light");
            keyObject.transform.SetParent(previewRoot.transform,false);
            keyObject.transform.localRotation = Quaternion.Euler(28f,-38f,0f);
            previewKeyLight = keyObject.AddComponent<Light>();
            previewKeyLight.type = LightType.Directional;
            previewKeyLight.intensity = 1.25f;
            previewKeyLight.cullingMask = 1 << PreviewLayer;

            GameObject fillObject = new GameObject("Preview Fill Light");
            fillObject.transform.SetParent(previewRoot.transform,false);
            fillObject.transform.localRotation = Quaternion.Euler(330f,145f,0f);
            previewFillLight = fillObject.AddComponent<Light>();
            previewFillLight.type = LightType.Directional;
            previewFillLight.intensity = .65f;
            previewFillLight.cullingMask = 1 << PreviewLayer;

            previewTexture =
                new RenderTexture(
                    640,
                    440,
                    16,
                    RenderTextureFormat.ARGB32)
                {
                    name = "H51 Shop Product Preview"
                };

            previewTexture.Create();
            previewCamera.targetTexture = previewTexture;

            if (previewImage != null)
                previewImage.texture = previewTexture;
        }

        void UpdatePreview(ShopCatalogEntry product)
        {
            ClearPreviewModel();

            if (product == null)
                return;

            EnsurePreviewInfrastructure();

            GameObject prefab = null;

            if (product.ProductKind == ShopProductKind.InventoryItem &&
                product.InventoryItem != null)
            {
                prefab = product.InventoryItem.WorldPrefab;
            }
            else
            {
                prefab = product.AssemblyTemplate;
            }

            if (prefab == null || previewRoot == null || previewCamera == null)
                return;

            previewModel = Instantiate(prefab,previewRoot.transform);
            previewModel.name = "Preview - " + product.DisplayName;
            previewModel.transform.localPosition = Vector3.zero;
            previewModel.transform.localRotation = Quaternion.identity;

            SetLayerRecursively(previewModel,PreviewLayer);

            Collider[] colliders =
                previewModel.GetComponentsInChildren<Collider>(true);
            for (int i=0;i<colliders.Length;i++)
                colliders[i].enabled = false;

            Rigidbody[] bodies =
                previewModel.GetComponentsInChildren<Rigidbody>(true);
            for (int i=0;i<bodies.Length;i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
            }

            MonoBehaviour[] behaviours =
                previewModel.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i=0;i<behaviours.Length;i++)
                behaviours[i].enabled = false;

            Renderer[] renderers =
                previewModel.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i=1;i<renderers.Length;i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 center = bounds.center;
            Vector3 localCenter =
                previewRoot.transform.InverseTransformPoint(center);

            previewModel.transform.localPosition -= localCenter;

            float width = Mathf.Max(.01f,bounds.size.x);
            float height = Mathf.Max(.01f,bounds.size.y);
            float depth = Mathf.Max(.01f,bounds.size.z);
            float aspect = 640f/440f;

            previewCamera.orthographicSize =
                Mathf.Max(
                    height*.66f,
                    width/aspect*.66f,
                    depth*.70f,
                    .28f);

            previewCamera.transform.localPosition =
                new Vector3(0f,0f,-Mathf.Max(2.5f,depth*3f+1f));
            previewCamera.transform.localRotation =
                Quaternion.identity;

            previewCamera.Render();
        }

        void ClearPreviewModel()
        {
            if (previewModel != null)
            {
                Destroy(previewModel);
                previewModel = null;
            }
        }

        static void SetLayerRecursively(GameObject root,int layer)
        {
            if (root == null)
                return;

            root.layer = layer;

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);

            for (int i=0;i<transforms.Length;i++)
                transforms[i].gameObject.layer = layer;
        }

        void HandleBalanceChanged(int _)
        {
            RefreshBalance();
            RefreshSelectedProduct();
        }

        void SetStatus(string message)
        {
            if (statusText == null)
                return;

            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(statusText.text));
        }

        void DisableGameplayBehaviours()
        {
            behavioursDisabledByShop.Clear();

            if (gameplayBehavioursToDisable == null)
                return;

            for (int index=0;index<gameplayBehavioursToDisable.Count;index++)
            {
                Behaviour behaviour = gameplayBehavioursToDisable[index];

                if (behaviour == null ||
                    behaviour == this ||
                    !behaviour.enabled)
                    continue;

                behaviour.enabled = false;
                behavioursDisabledByShop.Add(behaviour);
            }
        }

        void RestoreGameplayBehaviours()
        {
            for (int index=0;index<behavioursDisabledByShop.Count;index++)
            {
                Behaviour behaviour = behavioursDisabledByShop[index];
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            behavioursDisabledByShop.Clear();
        }

        void ResolveReferences()
        {
            if (firstPersonController == null)
                firstPersonController = FindFirstObjectByType<FirstPersonController>();

            if (inventoryUI == null)
                inventoryUI = FindFirstObjectByType<InventoryUI>();
        }

        void OnDisable()
        {
            if (isOpen)
                Close();
        }

        void OnDestroy()
        {
            ClearPreviewModel();

            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
                previewTexture = null;
            }

            if (previewRoot != null)
            {
                Destroy(previewRoot);
                previewRoot = null;
            }
        }
    }
}
