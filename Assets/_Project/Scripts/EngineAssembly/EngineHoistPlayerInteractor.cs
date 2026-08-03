using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(150)]
    public sealed class EngineHoistPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 4.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private EngineHoistController aimedHoist;

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
                return;
            }

            EngineHoistController activeHoist = EngineHoistController.ActiveControlledHoist;
            aimedHoist = activeHoist != null ? activeHoist : FindAimedHoist();
            if (aimedHoist == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                aimedHoist.TogglePlayerControl(out string controlMessage);
                if (!string.IsNullOrWhiteSpace(controlMessage))
                {
                    inventoryUI.ShowStatusMessage(controlMessage, 2f);
                }
            }

            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                aimedHoist.ToggleEngineAttachment(out string attachmentMessage);
                if (!string.IsNullOrWhiteSpace(attachmentMessage))
                {
                    inventoryUI.ShowStatusMessage(attachmentMessage, 2.5f);
                }
            }

            if (aimedHoist.TryConsumeStatusMessage(out string completedMessage))
            {
                inventoryUI.ShowStatusMessage(completedMessage, 2.5f);
            }

            inventoryUI.SetInteractionPrompt(aimedHoist.InteractionText);
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        private EngineHoistController FindAimedHoist()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            EngineHoistController nearestHoist = null;

            for (int index = 0; index < hits.Length; index++)
            {
                EngineHoistController hoist =
                    hits[index].collider.GetComponentInParent<EngineHoistController>();
                if (hoist != null && hits[index].distance < nearestDistance)
                {
                    nearestDistance = hits[index].distance;
                    nearestHoist = hoist;
                }
            }

            return nearestHoist;
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
