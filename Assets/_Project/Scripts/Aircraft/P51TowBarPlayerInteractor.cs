using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(360)]
    [DisallowMultipleComponent]
    public sealed class P51TowBarPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 5.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private P51TowBarController aimedTowBar;
        private bool promptOwned;

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
            if (playerCamera == null || inventoryUI == null || inventoryUI.IsOpen)
            {
                ClearOwnedPrompt();
                aimedTowBar = null;
                return;
            }

            P51PilotPlayerInteractor pilotInteractor =
                GetComponent<P51PilotPlayerInteractor>();
            if (pilotInteractor != null && pilotInteractor.IsPiloting)
            {
                ClearOwnedPrompt();
                aimedTowBar = null;
                return;
            }

            P51TowBarController activeTowBar =
                P51TowBarController.ActiveControlledTowBar;
            aimedTowBar = activeTowBar != null
                ? activeTowBar
                : FindAimedTowBar();
            if (aimedTowBar == null)
            {
                ClearOwnedPrompt();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                aimedTowBar.TogglePlayerControl(out string controlMessage);
                if (!string.IsNullOrWhiteSpace(controlMessage))
                {
                    inventoryUI.ShowStatusMessage(controlMessage, 2.5f);
                }
            }

            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                aimedTowBar.ToggleTailwheelAttachment(out string attachmentMessage);
                if (!string.IsNullOrWhiteSpace(attachmentMessage))
                {
                    inventoryUI.ShowStatusMessage(attachmentMessage, 3f);
                }
            }

            inventoryUI.SetInteractionPrompt(aimedTowBar.InteractionText);
            promptOwned = true;
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        private P51TowBarController FindAimedTowBar()
        {
            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            P51TowBarController nearestTowBar = null;
            for (int index = 0; index < hits.Length; index++)
            {
                P51TowBarController towBar =
                    hits[index].collider.GetComponentInParent<P51TowBarController>();
                if (towBar != null && hits[index].distance < nearestDistance)
                {
                    nearestDistance = hits[index].distance;
                    nearestTowBar = towBar;
                }
            }

            return nearestTowBar;
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

        private void ClearOwnedPrompt()
        {
            if (promptOwned && inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
            }
            promptOwned = false;
        }

        private void OnDisable()
        {
            ClearOwnedPrompt();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
