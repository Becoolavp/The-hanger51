using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Commerce
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class HangarCommercePlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 4.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private HangarShopTerminal aimedTerminal;
        private ShipmentCrateController aimedCrate;
        private HangarAircraftSpawnConsole aimedAircraftSpawner;
        private ShipmentCrateController activeUnboxingCrate;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();

            if (activeUnboxingCrate != null
                && activeUnboxingCrate.TryConsumeStatusMessage(out string completedMessage))
            {
                inventoryUI?.ShowStatusMessage(completedMessage, 3f);
                activeUnboxingCrate = null;
            }

            if (HangarShopUI.IsAnyShopOpen
                || playerCamera == null
                || inventoryUI == null
                || inventoryUI.IsOpen)
            {
                return;
            }

            FindAimedCommerceTarget();

            if (aimedAircraftSpawner != null)
            {
                inventoryUI.SetInteractionPrompt(aimedAircraftSpawner.InteractionText);
            }
            else if (aimedTerminal != null)
            {
                inventoryUI.SetInteractionPrompt(aimedTerminal.InteractionText);
            }
            else if (aimedCrate != null)
            {
                inventoryUI.SetInteractionPrompt(aimedCrate.InteractionText);
            }
            else
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (aimedAircraftSpawner != null)
            {
                bool spawned = aimedAircraftSpawner.TrySpawn(out string spawnMessage);
                inventoryUI.ShowStatusMessage(spawnMessage, spawned ? 4f : 3f);
                return;
            }

            if (aimedTerminal != null)
            {
                if (!aimedTerminal.TryOpen(out string terminalMessage))
                {
                    inventoryUI.ShowStatusMessage(terminalMessage, 2.5f);
                }
                return;
            }

            if (aimedCrate != null)
            {
                if (aimedCrate.TryBeginUnboxing(out string crateMessage))
                {
                    activeUnboxingCrate = aimedCrate;
                }
                inventoryUI.ShowStatusMessage(crateMessage, 2.5f);
            }
        }

        public void Configure(
            Camera configuredCamera,
            InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        private void FindAimedCommerceTarget()
        {
            aimedTerminal = null;
            aimedCrate = null;
            aimedAircraftSpawner = null;

            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                Collider hitCollider = hits[index].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                ShipmentCrateController crate =
                    hitCollider.GetComponentInParent<ShipmentCrateController>();
                if (crate != null && !crate.IsOpened)
                {
                    aimedCrate = crate;
                    return;
                }

                HangarAircraftSpawnConsole spawner =
                    hitCollider.GetComponentInParent<HangarAircraftSpawnConsole>();
                if (spawner != null)
                {
                    aimedAircraftSpawner = spawner;
                    return;
                }

                HangarShopTerminal terminal =
                    hitCollider.GetComponentInParent<HangarShopTerminal>();
                if (terminal != null)
                {
                    aimedTerminal = terminal;
                    return;
                }
            }
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
