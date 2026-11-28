using System;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.World
{
    [DefaultExecutionOrder(690)]
    [DisallowMultipleComponent]
    public sealed class H51FacilityLightingPlayerInteractor : MonoBehaviour
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] InventoryUI inventoryUI;
        [SerializeField, Min(1f)] float interactionDistance = 4.5f;
        [SerializeField] LayerMask interactionLayers = ~0;

        H51RunwayLightSwitch aimedSwitch;
        bool ownsPrompt;

        void Awake() => Resolve();
        void OnEnable() => Resolve();

        void Update()
        {
            Resolve();
            if (!playerCamera || (inventoryUI && inventoryUI.IsOpen))
            {
                ClearPrompt();
                return;
            }

            aimedSwitch = FindAimedSwitch();
            if (!aimedSwitch)
            {
                ClearPrompt();
                return;
            }

            if (inventoryUI)
            {
                inventoryUI.SetInteractionPrompt(aimedSwitch.InteractionText);
                ownsPrompt = true;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                bool changed = aimedSwitch.TryCycle(out string message);
                if (inventoryUI)
                    inventoryUI.ShowStatusMessage(message,changed ? 3.2f : 2.5f);
            }
        }

        H51RunwayLightSwitch FindAimedSwitch()
        {
            Ray ray = new Ray(playerCamera.transform.position,playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return null;

            Array.Sort(hits,(a,b) => a.distance.CompareTo(b.distance));
            for (int i=0;i<hits.Length;i++)
            {
                Collider collider = hits[i].collider;
                if (!collider) continue;

                H51RunwayLightSwitch found =
                    collider.GetComponentInParent<H51RunwayLightSwitch>();
                if (found) return found;
                if (!collider.isTrigger) return null;
            }
            return null;
        }

        void Resolve()
        {
            if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
            if (!inventoryUI) inventoryUI = FindFirstObjectByType<InventoryUI>();
        }

        void ClearPrompt()
        {
            if (!ownsPrompt) return;
            if (inventoryUI) inventoryUI.SetInteractionPrompt(string.Empty);
            ownsPrompt = false;
        }

        void OnDisable() => ClearPrompt();
        void OnValidate() => interactionDistance = Mathf.Max(1f,interactionDistance);
    }
}
