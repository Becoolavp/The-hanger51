using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(260)]
    public sealed class AircraftServicePlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 5.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private AircraftServiceInteractionTarget currentTarget;

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
                CancelCurrentHold();
                return;
            }

            AircraftServiceInteractionTarget target = FindBestTarget();
            if (target != currentTarget)
            {
                CancelCurrentHold();
                currentTarget = target;
            }

            if (currentTarget == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool holdingE = keyboard != null && keyboard.eKey.isPressed;
            bool holdingR = keyboard != null && keyboard.rKey.isPressed;

            if (currentTarget.ProcessInteraction(
                    holdingE,
                    holdingR,
                    Time.deltaTime,
                    out string resultMessage)
                && !string.IsNullOrWhiteSpace(resultMessage))
            {
                inventoryUI.ShowStatusMessage(resultMessage, 2.8f);
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

        private AircraftServiceInteractionTarget FindBestTarget()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
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

            AircraftServiceInteractionTarget bestTarget = null;
            int bestPriority = -1;
            float bestDistance = float.PositiveInfinity;

            for (int index = 0; index < hits.Length; index++)
            {
                AircraftServiceInteractionTarget candidate =
                    hits[index].collider.GetComponentInParent<AircraftServiceInteractionTarget>();
                if (candidate == null || !candidate.CanInteract)
                {
                    continue;
                }

                int priority = GetPriority(candidate.InteractionKind);
                if (priority > bestPriority
                    || (priority == bestPriority && hits[index].distance < bestDistance))
                {
                    bestTarget = candidate;
                    bestPriority = priority;
                    bestDistance = hits[index].distance;
                }
            }

            return bestTarget;
        }

        private static int GetPriority(AircraftServiceInteractionKind kind)
        {
            switch (kind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                case AircraftServiceInteractionKind.EngineMountBolt:
                    return 2;
                case AircraftServiceInteractionKind.CowlingPanel:
                    return 1;
                default:
                    return 0;
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

        private void CancelCurrentHold()
        {
            if (currentTarget != null)
            {
                currentTarget.CancelHold();
            }
        }

        private void OnDisable()
        {
            CancelCurrentHold();
            currentTarget = null;
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
