using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(430)]
    [DisallowMultipleComponent]
    public sealed class EngineConditionPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 5.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField, Min(0.2f)] private float dropDistance = 1.6f;

        private EngineOilCanController carriedOilCan;
        private bool promptOwned;

        public bool IsCarryingOilCan => carriedOilCan != null
            && carriedOilCan.IsCarried;

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
            if (playerCamera == null
                || inventoryUI == null
                || inventoryUI.IsOpen)
            {
                ClearPrompt();
                carriedOilCan?.StopPouring();
                return;
            }

            if (carriedOilCan != null && carriedOilCan.IsCarried)
            {
                HandleCarriedOilCan();
                return;
            }

            carriedOilCan = null;
            HandleNormalInteraction();
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        public void SetDownCarriedOilCan()
        {
            if (carriedOilCan == null || !carriedOilCan.IsCarried)
            {
                carriedOilCan = null;
                return;
            }

            DropCarriedOilCan(false);
        }

        private void HandleCarriedOilCan()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                carriedOilCan.TryToggleCap(out string capMessage);
                inventoryUI.ShowStatusMessage(capMessage, 2.5f);
            }

            EngineConditionInspectionTarget filler = FindAimedFiller();
            if (filler != null && filler.Condition != null)
            {
                string prompt;
                if (!carriedOilCan.IsOpen)
                {
                    prompt = "F: open oil can before pouring";
                }
                else if (carriedOilCan.IsEmpty)
                {
                    prompt = "Oil can is empty | E: set it down";
                }
                else if (!filler.Condition.CanService)
                {
                    prompt = filler.Condition.EngineRunning
                        ? "Stop the engine before adding oil"
                        : "The engine cannot be oiled while suspended";
                }
                else
                {
                    prompt = $"Hold E to pour oil — {filler.Condition.GetOilReadingText()}";
                }

                SetPrompt(prompt);
                if (keyboard != null && keyboard.eKey.isPressed)
                {
                    carriedOilCan.PourInto(
                        filler.Condition,
                        Time.deltaTime,
                        out string pourMessage);
                    SetPrompt(pourMessage);
                }
                else
                {
                    carriedOilCan.StopPouring();
                }
                return;
            }

            carriedOilCan.StopPouring();
            SetPrompt($"{carriedOilCan.InteractionText} | E: set down");
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                DropCarriedOilCan(true);
            }
        }

        private void HandleNormalInteraction()
        {
            RaycastHit[] hits = GetSortedHits();
            Keyboard keyboard = Keyboard.current;

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                EngineOilCanController oilCan =
                    collider.GetComponentInParent<EngineOilCanController>();
                if (oilCan != null && !oilCan.IsCarried)
                {
                    SetPrompt(oilCan.InteractionText);
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                    {
                        if (oilCan.TryPickUp(playerCamera.transform, out string message))
                        {
                            carriedOilCan = oilCan;
                        }
                        inventoryUI.ShowStatusMessage(message, 3f);
                    }
                    return;
                }

                EngineDipstickController dipstick =
                    collider.GetComponentInParent<EngineDipstickController>();
                if (dipstick != null)
                {
                    SetPrompt(dipstick.InteractionText);
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                    {
                        dipstick.TryToggle(out string message);
                        inventoryUI.ShowStatusMessage(message, 4f);
                    }
                    return;
                }

                EngineConditionInspectionTarget inspection =
                    collider.GetComponentInParent<EngineConditionInspectionTarget>();
                if (inspection != null)
                {
                    string inspectPrompt = inspection.InspectionKind
                        == EngineConditionInspectionKind.OilFiller
                            ? "X: inspect oil quantity | Carry an open oil can here to refill"
                            : inspection.InspectionPrompt;

                    EngineAssemblyInteractionTarget maintenanceTarget =
                        collider.GetComponentInParent<EngineAssemblyInteractionTarget>();
                    string maintenancePrompt = maintenanceTarget != null
                        ? maintenanceTarget.InteractionText
                        : string.Empty;
                    SetPrompt(string.IsNullOrWhiteSpace(maintenancePrompt)
                        ? inspectPrompt
                        : $"{maintenancePrompt} | {inspectPrompt}");

                    if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                    {
                        inventoryUI.ShowStatusMessage(
                            inspection.GetInspectionText(),
                            5f);
                    }
                    return;
                }
            }

            ClearPrompt();
        }

        private EngineConditionInspectionTarget FindAimedFiller()
        {
            RaycastHit[] hits = GetSortedHits();
            for (int index = 0; index < hits.Length; index++)
            {
                EngineConditionInspectionTarget target = hits[index].collider != null
                    ? hits[index].collider.GetComponentInParent<EngineConditionInspectionTarget>()
                    : null;
                if (target != null
                    && target.InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    return target;
                }
            }
            return null;
        }

        private RaycastHit[] GetSortedHits()
        {
            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            return hits;
        }

        private void DropCarriedOilCan(bool showMessage)
        {
            if (carriedOilCan == null)
            {
                return;
            }

            Transform reference = playerCamera != null
                ? playerCamera.transform
                : transform;
            Vector3 forward = Vector3.ProjectOnPlane(
                reference.forward,
                Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
            }
            forward.Normalize();

            Vector3 desired = transform.position
                + forward * dropDistance
                + Vector3.up * 0.6f;
            if (Physics.Raycast(
                    desired + Vector3.up * 1.5f,
                    Vector3.down,
                    out RaycastHit groundHit,
                    4f,
                    interactionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                desired = groundHit.point + Vector3.up * 0.42f;
            }

            EngineOilCanController can = carriedOilCan;
            carriedOilCan = null;
            can.Drop(
                desired,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            if (showMessage && inventoryUI != null)
            {
                inventoryUI.ShowStatusMessage("Set down the oil can.", 2f);
            }
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

        private void SetPrompt(string prompt)
        {
            if (inventoryUI == null)
            {
                return;
            }
            inventoryUI.SetInteractionPrompt(prompt ?? string.Empty);
            promptOwned = true;
        }

        private void ClearPrompt()
        {
            if (promptOwned && inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
            }
            promptOwned = false;
        }

        private void OnDisable()
        {
            carriedOilCan?.StopPouring();
            SetDownCarriedOilCan();
            ClearPrompt();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
            dropDistance = Mathf.Max(0.2f, dropDistance);
        }
    }
}
