using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class P51WheelPartPickupRuntimeBootstrap : MonoBehaviour
    {
        private float nextRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeBootstrap()
        {
            if (FindFirstObjectByType<P51WheelPartPickupRuntimeBootstrap>() != null)
            {
                return;
            }

            GameObject root = new GameObject("P-51 Wheel Part Pickup Runtime Bootstrap");
            root.AddComponent<P51WheelPartPickupRuntimeBootstrap>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.20f;
            InventoryPickup[] pickups = FindObjectsByType<InventoryPickup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < pickups.Length; index++)
            {
                InventoryPickup pickup = pickups[index];
                if (pickup == null || pickup.Item == null || !IsP51WheelPart(pickup.Item))
                {
                    continue;
                }

                EnsureRootPickupCollider(pickup.gameObject);
                if (EnginePartConditionData.InferKind(pickup.Item) == EnginePartConditionKind.Rim)
                {
                    P51BareRimServiceTarget.EnsureForPickup(pickup);
                }
            }
        }

        private static bool IsP51WheelPart(InventoryItemDefinition item)
        {
            if (item == null)
            {
                return false;
            }

            string id = item.ItemId;
            return id == P51LandingGearInventoryBridge.MainTireItemId
                || id == P51LandingGearInventoryBridge.TailTireItemId
                || id == P51LandingGearInventoryBridge.MainRimItemId
                || id == P51LandingGearInventoryBridge.TailRimItemId;
        }

        private static void EnsureRootPickupCollider(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<BoxCollider>();
            }

            collider.enabled = true;
            collider.isTrigger = true;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    worldBounds.Encapsulate(renderers[index].bounds);
                }
            }

            Vector3 lossy = root.transform.lossyScale;
            float sx = Mathf.Max(0.001f, Mathf.Abs(lossy.x));
            float sy = Mathf.Max(0.001f, Mathf.Abs(lossy.y));
            float sz = Mathf.Max(0.001f, Mathf.Abs(lossy.z));
            collider.center = root.transform.InverseTransformPoint(worldBounds.center);
            collider.size = new Vector3(
                Mathf.Max(0.12f, worldBounds.size.x / sx),
                Mathf.Max(0.12f, worldBounds.size.y / sy),
                Mathf.Max(0.12f, worldBounds.size.z / sz));
        }
    }
}
