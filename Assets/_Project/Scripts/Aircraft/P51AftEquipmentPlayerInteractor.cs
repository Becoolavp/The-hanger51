using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(240)]
    [DisallowMultipleComponent]
    public sealed class P51AftEquipmentPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(1f)] private float interactionDistance = 3.5f;
        [SerializeField, Min(0.4f)] private float holdDistance = 0.9f;

        private P51AftEquipmentItem heldItem;
        private P51AftAccessPanel heldPanel;
        private P51BatteryTester heldTester;
        private string prompt = string.Empty;
        private string statusMessage = string.Empty;
        private float statusUntil;
        private GUIStyle promptStyle;

        public bool IsHoldingSomething => heldItem != null || heldPanel != null || heldTester != null;

        public void Configure(Camera configuredCamera)
        {
            interactionCamera = configuredCamera;
        }

        private void Awake()
        {
            ResolveCamera();
        }

        private void Update()
        {
            ResolveCamera();
            prompt = string.Empty;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || interactionCamera == null)
            {
                return;
            }

            if (IsHoldingSomething && keyboard.fKey.wasPressedThisFrame)
            {
                DropHeldObject();
                SetStatus("Dropped carried aft-bay equipment.");
                return;
            }

            Ray ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Collide);

            if (heldTester != null)
            {
                P51AftEquipmentItem battery = hasHit
                    ? hit.collider.GetComponentInParent<P51AftEquipmentItem>()
                    : null;
                if (battery == null && hasHit)
                {
                    P51AftEquipmentSlot testerSlot = hit.collider.GetComponentInParent<P51AftEquipmentSlot>();
                    if (testerSlot != null)
                    {
                        battery = testerSlot.InstalledItem;
                    }
                }

                if (battery != null && battery.EquipmentKind == P51AftEquipmentKind.Battery)
                {
                    prompt = "E: connect battery tester leads   |   F: drop tester";
                    if (keyboard.eKey.wasPressedThisFrame)
                    {
                        SetStatus(heldTester.ReadBattery(battery));
                    }
                }
                else
                {
                    prompt = "Battery tester in hand   |   Aim at battery + E to test   |   F: drop";
                }
                return;
            }

            if (heldItem != null)
            {
                P51AftEquipmentSlot slot = hasHit ? hit.collider.GetComponentInParent<P51AftEquipmentSlot>() : null;
                if (slot != null)
                {
                    prompt = slot.AcceptedKind == heldItem.EquipmentKind
                        ? $"E: install {heldItem.DisplayName}   |   F: drop"
                        : $"Wrong rack position for {heldItem.DisplayName}   |   F: drop";

                    if (keyboard.eKey.wasPressedThisFrame
                        && slot.Bay != null
                        && slot.Bay.TryInstall(heldItem, slot, out string message))
                    {
                        heldItem.SetHeld(false);
                        heldItem = null;
                        SetStatus(message);
                    }
                }
                else
                {
                    prompt = $"Carrying {heldItem.DisplayName}   |   Aim at matching aft rack slot + E   |   F: drop";
                }
                return;
            }

            if (heldPanel != null)
            {
                P51AftEquipmentBay bay = hasHit ? hit.collider.GetComponentInParent<P51AftEquipmentBay>() : null;
                if (bay == null && heldPanel.Bay != null && Vector3.Distance(heldPanel.Bay.transform.position, interactionCamera.transform.position) < 4.5f)
                {
                    bay = heldPanel.Bay;
                }

                prompt = bay != null
                    ? "E: reinstall aft access panel   |   F: drop panel"
                    : "Carrying aft access panel   |   F: drop";

                if (bay != null && keyboard.eKey.wasPressedThisFrame)
                {
                    heldPanel.InstallOnAircraft(bay);
                    heldPanel.SetHeld(false);
                    heldPanel = null;
                    SetStatus("Reinstalled aft fuselage access panel.");
                }
                return;
            }

            if (!hasHit)
            {
                return;
            }

            P51AftAccessPanel panel = hit.collider.GetComponentInParent<P51AftAccessPanel>();
            if (panel != null)
            {
                prompt = panel.IsInstalled
                    ? "E: remove aft fuselage access panel"
                    : "E: pick up aft fuselage access panel";
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    if (panel.IsInstalled)
                    {
                        panel.RemoveFromAircraft();
                    }
                    HoldPanel(panel);
                    SetStatus("Aft equipment bay opened.");
                }
                return;
            }

            P51AftEquipmentSlot rackSlot = hit.collider.GetComponentInParent<P51AftEquipmentSlot>();
            if (rackSlot != null)
            {
                P51AftEquipmentBay bay = rackSlot.Bay;
                if (bay != null && !bay.AccessOpen)
                {
                    prompt = "Remove the aft access panel first.";
                    return;
                }

                P51AftEquipmentItem installed = rackSlot.InstalledItem;
                if (installed != null)
                {
                    prompt = $"E: remove {installed.DisplayName}";
                    if (keyboard.eKey.wasPressedThisFrame
                        && bay != null
                        && bay.TryRemove(installed, out string message))
                    {
                        HoldItem(installed);
                        SetStatus(message);
                    }
                }
                else
                {
                    prompt = rackSlot.AcceptedKind == P51AftEquipmentKind.Battery
                        ? "Battery rack position empty. Bring a 24 V aircraft battery here."
                        : "Oxygen rack position empty. Bring an oxygen bottle here.";
                }
                return;
            }

            P51AftEquipmentItem looseItem = hit.collider.GetComponentInParent<P51AftEquipmentItem>();
            if (looseItem != null)
            {
                prompt = looseItem.IsInstalled ? $"E: remove {looseItem.DisplayName}" : $"E: pick up {looseItem.DisplayName}";
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    if (looseItem.IsInstalled)
                    {
                        if (!looseItem.InstalledBay.TryRemove(looseItem, out string message))
                        {
                            SetStatus(message);
                            return;
                        }
                    }
                    HoldItem(looseItem);
                }
                return;
            }

            P51BatteryTester tester = hit.collider.GetComponentInParent<P51BatteryTester>();
            if (tester != null)
            {
                prompt = tester.InteractionText;
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    heldTester = tester;
                    heldTester.transform.SetParent(null, true);
                    heldTester.SetHeld(true);
                    SetStatus("Picked up battery tester. Aim at an aircraft battery and press E to connect the leads.");
                }
            }
        }

        private void LateUpdate()
        {
            Transform held = heldItem != null
                ? heldItem.transform
                : heldPanel != null
                    ? heldPanel.transform
                    : heldTester != null ? heldTester.transform : null;
            if (held == null || interactionCamera == null)
            {
                return;
            }

            Vector3 targetPosition = interactionCamera.transform.position
                + interactionCamera.transform.forward * holdDistance
                + interactionCamera.transform.right * 0.22f
                - interactionCamera.transform.up * 0.18f;
            held.position = Vector3.Lerp(held.position, targetPosition, 18f * Time.deltaTime);
            held.rotation = Quaternion.Slerp(
                held.rotation,
                Quaternion.LookRotation(interactionCamera.transform.forward, interactionCamera.transform.up),
                14f * Time.deltaTime);
        }

        private void HoldItem(P51AftEquipmentItem item)
        {
            heldItem = item;
            heldItem.transform.SetParent(null, true);
            heldItem.SetHeld(true);
        }

        private void HoldPanel(P51AftAccessPanel panel)
        {
            heldPanel = panel;
            heldPanel.transform.SetParent(null, true);
            heldPanel.SetHeld(true);
        }

        private void DropHeldObject()
        {
            if (heldItem != null)
            {
                heldItem.transform.SetParent(null, true);
                heldItem.SetHeld(false);
                heldItem = null;
            }
            if (heldPanel != null)
            {
                heldPanel.transform.SetParent(null, true);
                heldPanel.SetHeld(false);
                heldPanel = null;
            }
            if (heldTester != null)
            {
                heldTester.transform.SetParent(null, true);
                heldTester.SetHeld(false);
                heldTester = null;
            }
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            statusUntil = Time.unscaledTime + 4.5f;
        }

        private void ResolveCamera()
        {
            if (interactionCamera == null)
            {
                interactionCamera = GetComponent<Camera>();
            }
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(prompt)
                && (string.IsNullOrWhiteSpace(statusMessage) || Time.unscaledTime > statusUntil))
            {
                return;
            }

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 300f, Screen.height - 108f, 600f, 38f), prompt, promptStyle);
            }
            if (!string.IsNullOrWhiteSpace(statusMessage) && Time.unscaledTime <= statusUntil)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 330f, 70f, 660f, 44f), statusMessage, promptStyle);
            }
        }
    }
}
