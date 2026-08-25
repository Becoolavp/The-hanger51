using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(310)]
    [DisallowMultipleComponent]
    public sealed class P51WheelPartPlayerInteractor : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float interactionDistance = 6f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private PlayerInventory inventory;
        private InventoryUI inventoryUI;
        private Camera playerCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeInteractor()
        {
            if (FindFirstObjectByType<P51WheelPartPlayerInteractor>() != null)
            {
                return;
            }

            GameObject root = new GameObject("P-51 Wheel Part Player Interactor");
            root.AddComponent<P51WheelPartPlayerInteractor>();
        }

        private void Update()
        {
            ResolveReferences();
            if (inventory == null
                || inventoryUI == null
                || playerCamera == null
                || inventoryUI.IsOpen)
            {
                return;
            }

            FindCandidate(
                out P51BareRimServiceTarget bareRim,
                out InventoryPickup wheelPartPickup);

            Keyboard keyboard = Keyboard.current;
            if (bareRim != null)
            {
                bool holdE = keyboard != null && keyboard.eKey.isPressed;
                bool pressE = keyboard != null && keyboard.eKey.wasPressedThisFrame;
                if (bareRim.ProcessInteraction(
                        inventory,
                        holdE,
                        pressE,
                        Time.deltaTime,
                        out string message)
                    && !string.IsNullOrWhiteSpace(message))
                {
                    inventoryUI.ShowStatusMessage(message, 3.5f);
                }

                if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                {
                    inventoryUI.ShowStatusMessage(bareRim.Inspect(), 4f);
                }

                inventoryUI.SetInteractionPrompt(bareRim.GetInteractionText(inventory));
                return;
            }

            if (wheelPartPickup == null
                || wheelPartPickup.Item == null
                || wheelPartPickup.Quantity <= 0)
            {
                return;
            }

            // A rim always gets its dedicated service target so it can either be picked up
            // or accept an equipped replacement tire. This also covers rims dropped from inventory.
            if (EnginePartConditionData.InferKind(wheelPartPickup.Item)
                == EnginePartConditionKind.Rim)
            {
                P51BareRimServiceTarget target =
                    P51BareRimServiceTarget.EnsureForPickup(wheelPartPickup);
                if (target != null)
                {
                    bool holdE = keyboard != null && keyboard.eKey.isPressed;
                    bool pressE = keyboard != null && keyboard.eKey.wasPressedThisFrame;
                    if (target.ProcessInteraction(
                            inventory,
                            holdE,
                            pressE,
                            Time.deltaTime,
                            out string rimMessage)
                        && !string.IsNullOrWhiteSpace(rimMessage))
                    {
                        inventoryUI.ShowStatusMessage(rimMessage, 3.5f);
                    }

                    if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                    {
                        inventoryUI.ShowStatusMessage(target.Inspect(), 4f);
                    }

                    inventoryUI.SetInteractionPrompt(target.GetInteractionText(inventory));
                    return;
                }
            }

            // Tires remain ordinary inventory pickups. This late-running fallback means a
            // removed, purchased, or inventory-dropped tire is still pickable even if the
            // generic InventoryInteractor missed its generated collider that frame.
            if (keyboard != null
                && keyboard.eKey.wasPressedThisFrame
                && !wheelPartPickup.IsPickupBlocked)
            {
                string itemName = wheelPartPickup.Item.DisplayName;
                if (wheelPartPickup.TryPickup(inventory))
                {
                    inventoryUI.ShowStatusMessage($"Picked up {itemName}.", 2.5f);
                    return;
                }

                inventoryUI.ShowStatusMessage("Inventory is full; the wheel part stays on the floor.", 2.5f);
            }

            inventoryUI.SetInteractionPrompt(wheelPartPickup.InteractionText);
        }

        private void FindCandidate(
            out P51BareRimServiceTarget bestRim,
            out InventoryPickup bestPickup)
        {
            bestRim = null;
            bestPickup = null;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            float bestRimDistance = float.PositiveInfinity;
            float bestPickupDistance = float.PositiveInfinity;

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                P51LooseWheelAssembly completeWheel =
                    collider.GetComponentInParent<P51LooseWheelAssembly>();
                if (completeWheel != null)
                {
                    // Complete wheel carry/service is owned by P51LandingGearServicePlayerInteractor.
                    continue;
                }

                P51BareRimServiceTarget rim =
                    collider.GetComponentInParent<P51BareRimServiceTarget>();
                if (rim != null && hits[index].distance < bestRimDistance)
                {
                    bestRim = rim;
                    bestRimDistance = hits[index].distance;
                }

                InventoryPickup pickup = collider.GetComponentInParent<InventoryPickup>();
                if (pickup != null
                    && pickup.Item != null
                    && IsP51WheelPart(pickup.Item)
                    && hits[index].distance < bestPickupDistance)
                {
                    bestPickup = pickup;
                    bestPickupDistance = hits[index].distance;
                }
            }

            if (bestRim != null && bestRimDistance <= bestPickupDistance + 0.10f)
            {
                bestPickup = null;
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

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<PlayerInventory>();
            }
            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }
            if (playerCamera == null && inventory != null)
            {
                playerCamera = inventory.GetComponentInChildren<Camera>();
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
