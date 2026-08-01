using UnityEngine;

namespace Hanger51.Inventory
{
    public sealed class InventoryItemDropper : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Transform dropOrigin;
        [SerializeField, Min(0.5f)] private float forwardDistance = 1.5f;
        [SerializeField, Min(0.2f)] private float pickupScale = 0.55f;
        [SerializeField, Min(0.5f)] private float groundSearchDistance = 3f;

        public bool TryDropOne(int slotIndex, out string resultMessage)
        {
            resultMessage = "Nothing selected to drop.";

            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (inventory == null)
            {
                resultMessage = "Player inventory is missing.";
                return false;
            }

            if (!inventory.TryRemoveFromSlot(
                    slotIndex,
                    1,
                    out InventoryItemDefinition removedItem,
                    out int removedQuantity))
            {
                return false;
            }

            Vector3 groundPosition = FindDropGroundPosition();
            CreateDroppedPickup(removedItem, removedQuantity, groundPosition);

            resultMessage = $"Dropped {removedItem.DisplayName}.";
            return true;
        }

        private Vector3 FindDropGroundPosition()
        {
            Transform origin = dropOrigin != null ? dropOrigin : transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(origin.forward, Vector3.up).normalized;

            if (flatForward.sqrMagnitude < 0.01f)
            {
                flatForward = transform.forward;
            }

            Vector3 candidatePosition = transform.position + flatForward * forwardDistance;
            Vector3 rayOrigin = candidatePosition + Vector3.up * 1.5f;

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit groundHit,
                    groundSearchDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return groundHit.point;
            }

            return candidatePosition;
        }

        private void CreateDroppedPickup(
            InventoryItemDefinition item,
            int quantity,
            Vector3 groundPosition)
        {
            GameObject pickupObject;
            bool usesItemPrefab = item != null && item.WorldPrefab != null;

            if (usesItemPrefab)
            {
                pickupObject = Instantiate(item.WorldPrefab);
            }
            else
            {
                pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pickupObject.transform.localScale = Vector3.one * pickupScale;
                ApplyFallbackMaterial(pickupObject, item);
            }

            pickupObject.SetActive(true);
            pickupObject.transform.position = groundPosition;
            pickupObject.transform.rotation = Quaternion.identity;

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }

            pickup.Configure(item, quantity);
            EnsurePickupCollider(pickupObject);
            AlignBottomToGround(pickupObject, groundPosition.y);
        }

        private static void EnsurePickupCollider(GameObject pickupObject)
        {
            if (pickupObject.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            BoxCollider collider = pickupObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
        }

        private static void AlignBottomToGround(GameObject pickupObject, float groundY)
        {
            Renderer[] renderers = pickupObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                pickupObject.transform.position += Vector3.up * 0.02f;
                return;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                combinedBounds.Encapsulate(renderers[index].bounds);
            }

            float verticalOffset = groundY - combinedBounds.min.y + 0.02f;
            pickupObject.transform.position += Vector3.up * verticalOffset;
        }

        private static void ApplyFallbackMaterial(
            GameObject pickupObject,
            InventoryItemDefinition item)
        {
            Renderer pickupRenderer = pickupObject.GetComponent<Renderer>();
            if (pickupRenderer == null)
            {
                return;
            }

            Material runtimeMaterial = CreateRuntimeMaterial(item);
            if (runtimeMaterial != null)
            {
                pickupRenderer.material = runtimeMaterial;
            }
        }

        private static Material CreateRuntimeMaterial(InventoryItemDefinition item)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = item != null
                    ? $"Dropped {item.DisplayName} Material"
                    : "Dropped Item Material"
            };

            Color itemColor = item != null ? item.PlaceholderColor : Color.white;
            itemColor.a = 1f;
            material.color = itemColor;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", itemColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", itemColor);
            }

            return material;
        }

        private void OnValidate()
        {
            forwardDistance = Mathf.Max(forwardDistance, 0.5f);
            pickupScale = Mathf.Max(pickupScale, 0.2f);
            groundSearchDistance = Mathf.Max(groundSearchDistance, 0.5f);
        }
    }
}
