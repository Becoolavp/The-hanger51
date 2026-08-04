using System.Collections;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ShipmentCrateController : MonoBehaviour
    {
        [Header("Crate Visuals")]
        [SerializeField] private Transform lidPivot;
        [SerializeField] private Transform leftBand;
        [SerializeField] private Transform rightBand;
        [SerializeField] private TextMesh shippingLabel;
        [SerializeField] private Collider interactionCollider;
        [SerializeField, Min(0.2f)] private float openingDuration = 1.25f;

        private ShipmentAreaController shipmentArea;
        private int shipmentSlotIndex = -1;
        private Vector3 contentWorldPosition;
        private Quaternion contentWorldRotation = Quaternion.identity;
        private string productName = "Shipment";
        private ShopProductKind productKind;
        private InventoryItemDefinition inventoryItem;
        private int quantity = 1;
        private GameObject assemblyTemplate;
        private bool configured;
        private bool opening;
        private bool opened;
        private bool slotReleased;
        private string pendingStatusMessage;

        public bool IsOpening => opening;
        public bool IsOpened => opened;
        public string ProductName => productName;
        public string InteractionText => opened
            ? string.Empty
            : opening
                ? $"Unboxing {productName}..."
                : $"E: unbox shipment — {productName}";

        private void Awake()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }

        public void Configure(
            ShopCatalogEntry product,
            ShipmentAreaController configuredShipmentArea,
            int configuredSlotIndex,
            Transform contentAnchor)
        {
            shipmentArea = configuredShipmentArea;
            shipmentSlotIndex = configuredSlotIndex;
            contentWorldPosition = contentAnchor != null
                ? contentAnchor.position
                : transform.position + transform.forward * 3f;
            contentWorldRotation = contentAnchor != null
                ? contentAnchor.rotation
                : transform.rotation;

            productName = product != null ? product.DisplayName : "Shipment";
            productKind = product != null
                ? product.ProductKind
                : ShopProductKind.InventoryItem;
            inventoryItem = product != null ? product.InventoryItem : null;
            quantity = product != null ? Mathf.Max(1, product.Quantity) : 1;
            assemblyTemplate = product != null ? product.AssemblyTemplate : null;
            configured = product != null && product.IsConfigured;
            opening = false;
            opened = false;
            slotReleased = false;
            pendingStatusMessage = string.Empty;

            UpdateShippingLabel();
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }
        }

        public bool TryBeginUnboxing(out string resultMessage)
        {
            resultMessage = string.Empty;

            if (!configured)
            {
                resultMessage = "This shipment has no valid contents.";
                return false;
            }

            if (opened)
            {
                resultMessage = "This shipment has already been opened.";
                return false;
            }

            if (opening)
            {
                resultMessage = $"Already unboxing {productName}.";
                return false;
            }

            opening = true;
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            StartCoroutine(OpenCrateRoutine());
            resultMessage = $"Opening shipment: {productName}.";
            return true;
        }

        public bool TryConsumeStatusMessage(out string message)
        {
            message = pendingStatusMessage;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            pendingStatusMessage = string.Empty;
            return true;
        }

        private IEnumerator OpenCrateRoutine()
        {
            Quaternion lidStartRotation = lidPivot != null
                ? lidPivot.localRotation
                : Quaternion.identity;
            Quaternion lidEndRotation = lidStartRotation * Quaternion.Euler(-112f, 0f, 0f);
            Vector3 leftBandStart = leftBand != null ? leftBand.localScale : Vector3.one;
            Vector3 rightBandStart = rightBand != null ? rightBand.localScale : Vector3.one;

            float elapsed = 0f;
            float duration = Mathf.Max(0.2f, openingDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float smooth = normalized * normalized * (3f - 2f * normalized);

                if (lidPivot != null)
                {
                    lidPivot.localRotation = Quaternion.Slerp(
                        lidStartRotation,
                        lidEndRotation,
                        smooth);
                }

                float bandScale = 1f - smooth;
                if (leftBand != null)
                {
                    leftBand.localScale = new Vector3(
                        leftBandStart.x,
                        leftBandStart.y * bandScale,
                        leftBandStart.z);
                }
                if (rightBand != null)
                {
                    rightBand.localScale = new Vector3(
                        rightBandStart.x,
                        rightBandStart.y * bandScale,
                        rightBandStart.z);
                }

                yield return null;
            }

            GameObject deliveredContent = productKind == ShopProductKind.InventoryItem
                ? SpawnInventoryDelivery()
                : SpawnAssemblyDelivery();
            bool spawned = deliveredContent != null;
            if (spawned)
            {
                TransferSlotToDeliveredContent(deliveredContent);
            }

            opening = false;
            opened = true;
            if (shippingLabel != null)
            {
                shippingLabel.text = spawned
                    ? $"OPENED\n{productName}"
                    : $"DELIVERY ERROR\n{productName}";
            }

            pendingStatusMessage = spawned
                ? productKind == ShopProductKind.InventoryItem
                    ? $"Unboxed {productName}. Pick up the delivered item to clear this shipment bay."
                    : $"Unboxed {productName}. The stand was removed with the crate; the complete Merlin is ready on the floor."
                : $"The {productName} shipment opened, but its contents could not be created.";

            yield return new WaitForSeconds(3f);
            if (!spawned)
            {
                ReleaseShipmentSlot();
            }
            Destroy(gameObject);
        }

        private GameObject SpawnInventoryDelivery()
        {
            if (inventoryItem == null)
            {
                return null;
            }

            GameObject pickupObject;
            if (inventoryItem.WorldPrefab != null)
            {
                pickupObject = Instantiate(inventoryItem.WorldPrefab);
                pickupObject.transform.localScale = inventoryItem.WorldScale;
            }
            else
            {
                pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pickupObject.transform.localScale = Vector3.one * 0.45f;
                ApplyFallbackMaterial(pickupObject, inventoryItem);
            }

            pickupObject.name = $"Delivered {inventoryItem.DisplayName}";
            pickupObject.transform.SetPositionAndRotation(
                contentWorldPosition,
                contentWorldRotation);
            pickupObject.SetActive(true);

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.Configure(inventoryItem, quantity);

            if (pickupObject.GetComponentInChildren<Collider>() == null)
            {
                pickupObject.AddComponent<BoxCollider>();
            }

            AlignBottomToGround(pickupObject, contentWorldPosition.y);
            return pickupObject;
        }

        private GameObject SpawnAssemblyDelivery()
        {
            if (assemblyTemplate == null)
            {
                return null;
            }

            GameObject assembly = Instantiate(assemblyTemplate);
            assembly.name = $"Delivered {productName}";
            assembly.transform.SetPositionAndRotation(
                contentWorldPosition,
                contentWorldRotation);
            assembly.SetActive(true);

            EngineAssemblyStation station =
                assembly.GetComponentInChildren<EngineAssemblyStation>(true);
            if (station == null || !station.SetAssemblyComplete())
            {
                Destroy(assembly);
                return null;
            }

            EngineAssemblyTransportController transport =
                station.GetComponent<EngineAssemblyTransportController>();
            if (transport == null
                || transport.TransportRoot == null
                || transport.GroundContactPoint == null)
            {
                Destroy(assembly);
                return null;
            }

            HideDeliveredStand(station.transform, transport.TransportRoot);

            Quaternion floorRotation = Quaternion.Euler(
                0f,
                contentWorldRotation.eulerAngles.y,
                0f);
            Vector3 rootPosition = transport.CalculateRootPositionForGroundContact(
                contentWorldPosition + Vector3.up * 0.035f,
                floorRotation);
            transport.SetWorldPose(rootPosition, floorRotation);
            transport.CompletePlacement(false);
            transport.RefreshMaintenanceTargets();

            // Shipment occupancy follows the actual portable engine rather than
            // the invisible station owner. Moving the Merlin with the hoist will
            // therefore free the receiving bay normally.
            return transport.TransportRoot.gameObject;
        }

        private static void HideDeliveredStand(
            Transform stationRoot,
            Transform portableEngineRoot)
        {
            if (stationRoot == null)
            {
                return;
            }

            Renderer[] renderers = stationRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || (portableEngineRoot != null
                        && renderer.transform.IsChildOf(portableEngineRoot)))
                {
                    continue;
                }

                renderer.enabled = false;
            }

            Collider[] colliders = stationRoot.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null
                    || (portableEngineRoot != null
                        && collider.transform.IsChildOf(portableEngineRoot)))
                {
                    continue;
                }

                collider.enabled = false;
            }

            DeliveredEngineStandDisposalTarget[] oldTargets =
                stationRoot.GetComponentsInChildren<DeliveredEngineStandDisposalTarget>(true);
            for (int index = 0; index < oldTargets.Length; index++)
            {
                if (oldTargets[index] != null)
                {
                    oldTargets[index].enabled = false;
                }
            }
        }

        private void TransferSlotToDeliveredContent(GameObject deliveredContent)
        {
            ShipmentDeliveryOccupancy occupancy =
                deliveredContent.GetComponent<ShipmentDeliveryOccupancy>();
            if (occupancy == null)
            {
                occupancy = deliveredContent.AddComponent<ShipmentDeliveryOccupancy>();
            }

            occupancy.Configure(
                shipmentArea,
                shipmentSlotIndex,
                deliveredContent.transform.position,
                3.5f);

            if (shipmentArea != null
                && shipmentArea.TransferSlot(shipmentSlotIndex, this, occupancy))
            {
                slotReleased = true;
            }
        }

        private void UpdateShippingLabel()
        {
            if (shippingLabel == null)
            {
                return;
            }

            string quantityText = productKind == ShopProductKind.InventoryItem
                ? $"QTY {quantity}"
                : "COMPLETE ASSEMBLY";
            shippingLabel.text = $"HANGER 51 SUPPLY\n{productName}\n{quantityText}";
        }

        private void ReleaseShipmentSlot()
        {
            if (slotReleased)
            {
                return;
            }

            slotReleased = true;
            shipmentArea?.ReleaseSlot(shipmentSlotIndex, this);
        }

        private static void AlignBottomToGround(GameObject target, float groundY)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            target.transform.position += Vector3.up * (groundY - bounds.min.y + 0.03f);
        }

        private static void ApplyFallbackMaterial(
            GameObject target,
            InventoryItemDefinition item)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (renderer == null || shader == null)
            {
                return;
            }

            Color color = item != null ? item.PlaceholderColor : Color.white;
            color.a = 1f;
            Material material = new Material(shader)
            {
                name = item != null
                    ? $"Delivered {item.DisplayName} Material"
                    : "Delivered Item Material",
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            renderer.material = material;
        }

        private void OnDestroy()
        {
            ReleaseShipmentSlot();
        }

        private void OnValidate()
        {
            openingDuration = Mathf.Max(0.2f, openingDuration);
        }
    }
}
