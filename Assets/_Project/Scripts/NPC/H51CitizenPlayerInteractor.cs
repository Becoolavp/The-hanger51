using System;
using Hanger51.Health;
using Hanger51.Inventory;
using Hanger51.Player;
using Hanger51.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.NPCs
{
    [DefaultExecutionOrder(650)]
    [DisallowMultipleComponent]
    public sealed class H51CitizenPlayerInteractor : MonoBehaviour
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] InventoryUI inventoryUI;
        [SerializeField] H51CitizenDialogueUI dialogueUI;
        [SerializeField] H51MedicalTreatmentUI medicalUI;
        [SerializeField, Min(1f)] float interactionDistance = 4.5f;
        [SerializeField] LayerMask interactionLayers = ~0;

        H51CitizenDialogue aimedCitizen;
        H51TownDoor aimedDoor;
        bool ownsPrompt;

        void Awake() => Resolve();

        void OnEnable() => Resolve();

        void Update()
        {
            Resolve();

            if (!playerCamera || !dialogueUI) return;

            if (dialogueUI.IsOpen)
            {
                ClearOwnedPrompt();
                return;
            }

            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                ClearOwnedPrompt();
                return;
            }

            FindAimedTarget(out aimedCitizen, out aimedDoor);
            if (!aimedCitizen && !aimedDoor)
            {
                ClearOwnedPrompt();
                return;
            }

            H51AnatomicalHealth aimedHealth =
                aimedCitizen
                    ? aimedCitizen.GetComponent<H51AnatomicalHealth>()
                    : null;

            bool injured =
                aimedHealth &&
                (aimedHealth.IsDead ||
                 aimedHealth.OverallStatus != "FULLY FUNCTIONAL" ||
                 aimedHealth.TotalBleedMlPerMinute > .5f);

            if (inventoryUI)
            {
                if (aimedCitizen && aimedCitizen.Identity)
                {
                    string prompt =
                        $"E  Talk to {aimedCitizen.Identity.DisplayName}";

                    if (injured)
                        prompt += aimedHealth.IsDead
                            ? "    F  Inspect injuries"
                            : "    F  Inspect / give first aid";

                    inventoryUI.SetInteractionPrompt(prompt);
                }
                else if (aimedDoor)
                    inventoryUI.SetInteractionPrompt(aimedDoor.InteractionText);
                ownsPrompt = true;
            }

            Keyboard keyboard = Keyboard.current;

            if (injured &&
                keyboard != null &&
                keyboard.fKey.wasPressedThisFrame)
            {
                Resolve();

                if (medicalUI &&
                    medicalUI.TryInspect(aimedHealth,out string reason))
                {
                    ClearOwnedPrompt();
                    return;
                }

                if (inventoryUI && !string.IsNullOrWhiteSpace(reason))
                    inventoryUI.ShowStatusMessage(reason,3f);
            }

            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ClearOwnedPrompt();

                if (aimedCitizen)
                    dialogueUI.Open(aimedCitizen);
                else if (aimedDoor)
                    aimedDoor.ToggleFromPlayer();
            }
        }

        void FindAimedTarget(out H51CitizenDialogue npc, out H51TownDoor door)
        {
            npc = null;
            door = null;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0) return;

            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (!collider) continue;

                H51CitizenDialogue foundNpc = collider.GetComponentInParent<H51CitizenDialogue>();
                if (foundNpc)
                {
                    npc = foundNpc;
                    return;
                }

                H51TownDoor foundDoor = collider.GetComponentInParent<H51TownDoor>();
                if (foundDoor)
                {
                    door = foundDoor;
                    return;
                }

                // The nearest solid object blocks interaction with anything behind it.
                if (!collider.isTrigger) return;
            }
        }

        void Resolve()
        {
            if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
            if (!inventoryUI) inventoryUI = FindFirstObjectByType<InventoryUI>();
            if (!dialogueUI) dialogueUI = GetComponent<H51CitizenDialogueUI>();
            if (!dialogueUI) dialogueUI = gameObject.AddComponent<H51CitizenDialogueUI>();
            if (!medicalUI) medicalUI = GetComponent<H51MedicalTreatmentUI>();
            if (!medicalUI) medicalUI = gameObject.AddComponent<H51MedicalTreatmentUI>();
        }

        void ClearOwnedPrompt()
        {
            if (!ownsPrompt) return;
            if (inventoryUI) inventoryUI.SetInteractionPrompt(string.Empty);
            ownsPrompt = false;
        }

        void OnDisable() => ClearOwnedPrompt();

        void OnValidate() => interactionDistance = Mathf.Max(1f, interactionDistance);
    }
}
