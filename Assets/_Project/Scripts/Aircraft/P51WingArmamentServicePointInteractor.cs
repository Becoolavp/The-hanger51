using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(316)]
    [DisallowMultipleComponent]
    public sealed class P51WingArmamentServicePointInteractor : MonoBehaviour
    {
        private const float HighlightRefreshSeconds = 0.12f;
        private const float TargetRefreshSeconds = 1f;

        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 5.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private P51WingArmamentServicePoint currentTarget;
        private P51WingArmamentServicePoint[] servicePoints = Array.Empty<P51WingArmamentServicePoint>();
        private float nextHighlightRefresh;
        private float nextTargetRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeInteractor()
        {
            PlayerInventory player = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (player != null && player.GetComponent<P51WingArmamentServicePointInteractor>() == null)
            {
                player.gameObject.AddComponent<P51WingArmamentServicePointInteractor>();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshServicePoints();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshServicePoints();
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredUI;
            ResolveReferences();
            RefreshServicePoints();
        }

        private void Update()
        {
            ResolveReferences();
            RefreshHighlightsIfNeeded();

            if (playerCamera == null || inventory == null || inventoryUI == null || inventoryUI.IsOpen)
            {
                CancelCurrent();
                return;
            }

            P51WingArmamentServicePoint target = FindTarget();
            if (target != currentTarget)
            {
                if (currentTarget != null)
                {
                    currentTarget.CancelHold();
                }
                currentTarget = target;
            }

            if (currentTarget == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool pressedE = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            bool holdE = keyboard != null && keyboard.eKey.isPressed;
            bool holdR = keyboard != null && keyboard.rKey.isPressed;

            if (currentTarget.ProcessInteraction(
                    inventory,
                    pressedE,
                    holdE,
                    holdR,
                    Time.deltaTime,
                    out string resultMessage)
                && !string.IsNullOrWhiteSpace(resultMessage))
            {
                inventoryUI.ShowStatusMessage(resultMessage, 3.5f);
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                inventoryUI.ShowStatusMessage(currentTarget.Inspect(), 4f);
            }

            inventoryUI.SetInteractionPrompt(currentTarget.GetInteractionText(inventory));
        }

        private P51WingArmamentServicePoint FindTarget()
        {
            RaycastHit[] hits = Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
            {
                return null;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null) continue;

                P51WingArmamentServicePoint target =
                    collider.GetComponentInParent<P51WingArmamentServicePoint>();
                if (target != null)
                {
                    return target;
                }
            }

            return null;
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (inventory == null)
            {
                inventory = FindFirstObjectByType<PlayerInventory>();
            }

            if (playerCamera == null && inventory != null)
            {
                playerCamera = inventory.GetComponentInChildren<Camera>(true);
            }

            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }
        }

        private void RefreshServicePoints()
        {
            servicePoints = FindObjectsByType<P51WingArmamentServicePoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            nextTargetRefresh = Time.unscaledTime + TargetRefreshSeconds;
        }

        private void RefreshHighlightsIfNeeded()
        {
            if (Time.unscaledTime >= nextTargetRefresh)
            {
                RefreshServicePoints();
            }

            if (Time.unscaledTime < nextHighlightRefresh)
            {
                return;
            }

            nextHighlightRefresh = Time.unscaledTime + HighlightRefreshSeconds;
            PlayerInventory highlightInventory = inventoryUI != null && inventoryUI.IsOpen ? null : inventory;

            for (int index = 0; index < servicePoints.Length; index++)
            {
                P51WingArmamentServicePoint target = servicePoints[index];
                if (target == null) continue;
                target.RefreshHighlight(highlightInventory);
            }
        }

        private void CancelCurrent()
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
            CancelCurrent();
            for (int index = 0; index < servicePoints.Length; index++)
            {
                if (servicePoints[index] != null)
                {
                    servicePoints[index].RefreshHighlight(null);
                }
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
