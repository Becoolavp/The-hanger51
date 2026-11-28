using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Commerce
{
    [DefaultExecutionOrder(230)]
    [DisallowMultipleComponent]
    public sealed class DeliveredEngineStandDisposalPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private DeliveredEngineStandDisposalTarget currentTarget;

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
                ClearCurrentTarget();
                return;
            }

            DeliveredEngineStandDisposalTarget target = FindAimedTarget();
            if (target != currentTarget)
            {
                currentTarget?.CancelHold();
                currentTarget = target;
            }

            if (currentTarget == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool holdingR = keyboard != null && keyboard.rKey.isPressed;
            if (currentTarget.ProcessDismantleHold(
                    holdingR,
                    Time.deltaTime,
                    out string resultMessage))
            {
                inventoryUI.ShowStatusMessage(resultMessage, 2.5f);
                ClearCurrentTarget();
                return;
            }

            inventoryUI.SetInteractionPrompt(currentTarget.InteractionText);
        }

        public void Configure(
            Camera configuredCamera,
            InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        private DeliveredEngineStandDisposalTarget FindAimedTarget()
        {
            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return null;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                DeliveredEngineStandDisposalTarget target =
                    hits[index].collider.GetComponentInParent<DeliveredEngineStandDisposalTarget>();
                if (target != null && !target.StandRemoved)
                {
                    return target;
                }
            }

            return null;
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

        private void ClearCurrentTarget()
        {
            if (currentTarget != null)
            {
                currentTarget.CancelHold();
                currentTarget = null;
                if (inventoryUI != null)
                {
                    inventoryUI.SetInteractionPrompt(string.Empty);
                }
            }
        }

        private void OnDisable()
        {
            ClearCurrentTarget();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.5f, interactionDistance);
        }
    }
}
