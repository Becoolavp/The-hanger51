using System;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Commerce
{
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    public sealed class DeliveredEngineStandWidePlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(0.5f)] private float interactionDistance = 4.5f;
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

        public void Configure(
            Camera configuredCamera,
            InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
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

            DeliveredEngineStandDisposalTarget target = FindAimedStandTarget();
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
                inventoryUI.ShowStatusMessage(resultMessage, 2.75f);
                ClearCurrentTarget();
                return;
            }

            inventoryUI.SetInteractionPrompt(currentTarget.InteractionText);
        }

        private DeliveredEngineStandDisposalTarget FindAimedStandTarget()
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
                Collider hitCollider = hits[index].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                DeliveredEngineStandDisposalTarget target =
                    hitCollider.GetComponentInParent<DeliveredEngineStandDisposalTarget>();
                if (target == null || target.StandRemoved)
                {
                    continue;
                }

                // Ignore the station's broad invisible root collider. Visible
                // child colliders on the actual rails, posts, braces, saddles,
                // and casters remain selectable.
                if (hitCollider.transform == target.transform)
                {
                    continue;
                }

                EngineAssemblyTransportController transport = target.EngineTransport;
                Transform portableEngineRoot = transport != null
                    ? transport.TransportRoot
                    : null;

                // Engine geometry is deliberately ignored so normal spark-plug,
                // cover, block, and hoist interactions win when the Player is
                // aiming at the Merlin rather than its holder.
                if (portableEngineRoot != null
                    && hitCollider.transform.IsChildOf(portableEngineRoot))
                {
                    continue;
                }

                return target;
            }

            return null;
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
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
            }

            if (inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
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
