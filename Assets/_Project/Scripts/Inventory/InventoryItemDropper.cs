using System.Collections.Generic;
using Hanger51.Aircraft;
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
                    out int removedQuantity,
                    out List<EnginePartConditionData> removedConditions))
            {
                return false;
            }

            Vector3 groundPosition = FindDropGroundPosition();
            CreateDroppedPickup(
                removedItem,
                removedQuantity,
                removedConditions,
                groundPosition);

            string conditionText = removedConditions.Count > 0
                && removedConditions[0] != null
                    ? $" ({removedConditions[0].GetConditionSummary()})"
                    : string.Empty;
            resultMessage = $"Dropped {removedItem.DisplayName}{conditionText}.";
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
            IReadOnlyList<EnginePartConditionData> conditions,
            Vector3 groundPosition)
        {
            GameObject pickupObject;
            bool usesItemPrefab = item != null && item.WorldPrefab != null;

            if (usesItemPrefab)
            {
                pickupObject = Instantiate(item.WorldPrefab);
                pickupObject.transform.localScale = item.WorldScale;
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

            if (EnginePartConditionData.IsTrackedItem(item))
            {
                pickup.Configure(item, conditions);
            }
            else
            {
                pickup.Configure(item, quantity);
            }

            EnsurePickupCollider(pickupObject);
            PrepareP51WheelPartPickup(pickupObject, pickup, item);
            AlignBottomToGround(pickupObject, groundPosition.y);
        }

        private static void PrepareP51WheelPartPickup(
            GameObject pickupObject,
            InventoryPickup pickup,
            InventoryItemDefinition item)
        {
            if (pickupObject == null || pickup == null || item == null)
            {
                return;
            }

            EnginePartConditionKind kind = EnginePartConditionData.InferKind(item);
            if (kind != EnginePartConditionKind.Tire
                && kind != EnginePartConditionKind.Rim)
            {
                return;
            }

            BoxCollider rootCollider = pickupObject.GetComponent<BoxCollider>();
            if (rootCollider == null)
            {
                rootCollider = pickupObject.AddComponent<BoxCollider>();
            }
            rootCollider.enabled = true;
            rootCollider.isTrigger = true;
            FitColliderToRenderers(pickupObject, rootCollider);

            pickup.enabled = true;
            pickup.SetRuntimePickupBlocked(false);

            if (kind == EnginePartConditionKind.Rim)
            {
                // Dropped rims must immediately behave exactly like freshly removed bare rims:
                // normal E pickup when no matching tire is equipped, or Hold E to mount a tire.
                P51BareRimServiceTarget.EnsureForPickup(pickup);
            }
        }

        private static void FitColliderToRenderers(
            GameObject root,
            BoxCollider collider)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one * 0.45f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            Vector3 scale = root.transform.lossyScale;
            float sx = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float sy = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            float sz = Mathf.Max(0.001f, Mathf.Abs(scale.z));
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(
                Mathf.Max(0.12f, bounds.size.x / sx),
                Mathf.Max(0.12f, bounds.size.y / sy),
                Mathf.Max(0.12f, bounds.size.z / sz));
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
