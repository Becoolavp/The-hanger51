using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(240)]
    [DisallowMultipleComponent]
    public sealed class P51AftEquipmentPlayerInteractor : MonoBehaviour
    {
        private enum ServiceMotionKind
        {
            None,
            ItemToHand,
            ItemToSlot,
            PanelToHand,
            PanelToMount
        }

        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(1f)] private float interactionDistance = 3.5f;
        [SerializeField, Min(0.4f)] private float holdDistance = 0.9f;
        [SerializeField, Min(0.2f)] private float serviceMotionDuration = 0.62f;
        [SerializeField, Min(2f)] private float placementGuideDistance = 5f;

        private P51AftEquipmentItem heldItem;
        private P51AftAccessPanel heldPanel;
        private P51BatteryTester heldTester;
        private string prompt = string.Empty;
        private string statusMessage = string.Empty;
        private float statusUntil;
        private GUIStyle promptStyle;

        private ServiceMotionKind motionKind;
        private Transform motionTransform;
        private Vector3 motionStartPosition;
        private Quaternion motionStartRotation;
        private Vector3 motionMidpoint;
        private float motionStartedAt;
        private P51AftEquipmentSlot pendingSlot;
        private P51AftEquipmentBay pendingBay;

        private P51AftEquipmentSlot[] cachedSlots = new P51AftEquipmentSlot[0];
        private float nextSlotRefreshTime;

        public bool IsHoldingSomething => heldItem != null || heldPanel != null || heldTester != null;
        public bool HasActiveAftInteraction { get; private set; }
        public bool IsServiceAnimating => motionKind != ServiceMotionKind.None;
        public float InteractionDistance => interactionDistance;

        public void Configure(Camera configuredCamera)
        {
            interactionCamera = configuredCamera;
        }

        public void ConfigureServiceReach(float configuredInteractionDistance, float configuredHoldDistance)
        {
            interactionDistance = Mathf.Max(1f, configuredInteractionDistance);
            holdDistance = Mathf.Max(0.4f, configuredHoldDistance);
        }

        private void Awake()
        {
            ResolveCamera();
            RefreshSlotCache(true);
        }

        private void Update()
        {
            ResolveCamera();
            RefreshSlotCache(false);
            UpdatePlacementHighlights();

            prompt = string.Empty;
            HasActiveAftInteraction = IsHoldingSomething || IsServiceAnimating;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || interactionCamera == null)
            {
                return;
            }

            if (IsServiceAnimating)
            {
                prompt = GetMotionPrompt();
                return;
            }

            if (IsHoldingSomething && keyboard.fKey.wasPressedThisFrame)
            {
                DropHeldObject();
                SetStatus("Dropped carried aft-bay equipment.");
                return;
            }

            Ray ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            bool hasHit = TryFindAftInteractionHit(ray, out RaycastHit hit);
            if (hasHit)
            {
                HasActiveAftInteraction = true;
            }

            if (heldTester != null)
            {
                P51AftEquipmentItem battery = hasHit ? hit.collider.GetComponentInParent<P51AftEquipmentItem>() : null;
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
                bool validSlot = slot != null
                    && slot.AcceptedKind == heldItem.EquipmentKind
                    && slot.InstalledItem == null
                    && slot.Bay != null
                    && slot.Bay.AccessOpen;

                if (slot != null)
                {
                    prompt = validSlot
                        ? $"E: install {heldItem.DisplayName} in highlighted rack position   |   F: drop"
                        : slot.AcceptedKind != heldItem.EquipmentKind
                            ? $"Wrong rack position for {heldItem.DisplayName}   |   F: drop"
                            : slot.InstalledItem != null
                                ? "That rack position is already occupied."
                                : "Open the aft access panel before installing equipment.";

                    if (validSlot && keyboard.eKey.wasPressedThisFrame)
                    {
                        BeginItemInstall(slot);
                    }
                }
                else
                {
                    prompt = $"Carrying {heldItem.DisplayName}   |   Highlighted cage = install position   |   F: drop";
                }
                return;
            }

            if (heldPanel != null)
            {
                P51AftEquipmentBay bay = hasHit ? hit.collider.GetComponentInParent<P51AftEquipmentBay>() : null;
                if (bay == null && heldPanel.Bay != null
                    && Vector3.Distance(heldPanel.Bay.transform.position, interactionCamera.transform.position) < 4.5f)
                {
                    bay = heldPanel.Bay;
                }

                prompt = bay != null
                    ? "E: reinstall aft access panel   |   F: drop panel"
                    : "Carrying aft access panel   |   F: drop";

                if (bay != null && keyboard.eKey.wasPressedThisFrame)
                {
                    BeginPanelInstall(bay);
                }
                return;
            }

            if (!hasHit)
            {
                return;
            }

            P51AftPanelFastener fastener = hit.collider.GetComponentInParent<P51AftPanelFastener>();
            if (fastener != null)
            {
                prompt = fastener.IsSecured
                    ? $"E: release aft-panel fastener {fastener.FastenerIndex + 1}"
                    : $"E: secure aft-panel fastener {fastener.FastenerIndex + 1}";
                if (keyboard.eKey.wasPressedThisFrame && fastener.TryToggle(out string fastenerMessage))
                {
                    SetStatus(fastenerMessage);
                }
                return;
            }

            P51AftAccessPanel panel = hit.collider.GetComponentInParent<P51AftAccessPanel>();
            if (panel != null)
            {
                if (panel.IsInstalled)
                {
                    int remaining = panel.SecuredFastenerCount;
                    prompt = remaining > 0
                        ? $"Release {remaining} aft-panel fastener{(remaining == 1 ? string.Empty : "s")} before removing the panel."
                        : "E: remove aft fuselage access panel";

                    if (keyboard.eKey.wasPressedThisFrame && remaining == 0)
                    {
                        BeginPanelRemoval(panel);
                    }
                }
                else
                {
                    prompt = "E: pick up aft fuselage access panel";
                    if (keyboard.eKey.wasPressedThisFrame)
                    {
                        HoldPanel(panel);
                        SetStatus("Picked up aft fuselage access panel.");
                    }
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
                    if (keyboard.eKey.wasPressedThisFrame)
                    {
                        BeginItemRemoval(installed, bay);
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
                prompt = looseItem.IsInstalled
                    ? $"E: remove {looseItem.DisplayName}"
                    : $"E: pick up {looseItem.DisplayName}";
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    if (looseItem.IsInstalled)
                    {
                        BeginItemRemoval(looseItem, looseItem.InstalledBay);
                    }
                    else
                    {
                        HoldItem(looseItem);
                    }
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

        private void BeginItemRemoval(P51AftEquipmentItem item, P51AftEquipmentBay bay)
        {
            if (item == null || bay == null)
            {
                return;
            }

            Vector3 startPosition = item.transform.position;
            Quaternion startRotation = item.transform.rotation;
            if (!bay.TryRemove(item, out string message))
            {
                SetStatus(message);
                return;
            }

            heldItem = item;
            heldItem.SetHeld(true);
            heldItem.transform.SetParent(null, true);
            heldItem.transform.SetPositionAndRotation(startPosition, startRotation);

            Vector3 target = GetHandPosition();
            Vector3 outward = interactionCamera != null ? interactionCamera.transform.forward * -0.08f : Vector3.left * 0.08f;
            BeginMotion(
                ServiceMotionKind.ItemToHand,
                heldItem.transform,
                startPosition,
                startRotation,
                Vector3.Lerp(startPosition, target, 0.48f) + Vector3.up * 0.10f + outward);
            SetStatus($"Removing {item.DisplayName} from the aft rack.");
        }

        private void BeginItemInstall(P51AftEquipmentSlot slot)
        {
            if (heldItem == null || slot == null)
            {
                return;
            }

            pendingSlot = slot;
            Vector3 target = slot.transform.position;
            Vector3 outward = (heldItem.transform.position - target).normalized;
            if (outward.sqrMagnitude < 0.001f)
            {
                outward = interactionCamera != null ? -interactionCamera.transform.forward : Vector3.left;
            }
            BeginMotion(
                ServiceMotionKind.ItemToSlot,
                heldItem.transform,
                heldItem.transform.position,
                heldItem.transform.rotation,
                Vector3.Lerp(heldItem.transform.position, target, 0.58f) + Vector3.up * 0.06f + outward * 0.08f);
        }

        private void BeginPanelRemoval(P51AftAccessPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            Vector3 startPosition = panel.transform.position;
            Quaternion startRotation = panel.transform.rotation;
            if (!panel.TryRemoveFromAircraft(out string message))
            {
                SetStatus(message);
                return;
            }

            heldPanel = panel;
            heldPanel.SetHeld(true);
            heldPanel.transform.SetParent(null, true);
            heldPanel.transform.SetPositionAndRotation(startPosition, startRotation);

            Vector3 target = GetHandPosition();
            Vector3 cameraSide = interactionCamera != null ? interactionCamera.transform.right * 0.10f : Vector3.right * 0.10f;
            BeginMotion(
                ServiceMotionKind.PanelToHand,
                heldPanel.transform,
                startPosition,
                startRotation,
                Vector3.Lerp(startPosition, target, 0.45f) + Vector3.up * 0.14f + cameraSide);
            SetStatus("Aft panel released. Lifting it clear of the fuselage.");
        }

        private void BeginPanelInstall(P51AftEquipmentBay bay)
        {
            if (heldPanel == null || bay == null || bay.PanelAnchor == null)
            {
                return;
            }

            pendingBay = bay;
            Vector3 target = bay.PanelAnchor.position;
            Vector3 outward = (heldPanel.transform.position - target).normalized;
            if (outward.sqrMagnitude < 0.001f)
            {
                outward = interactionCamera != null ? -interactionCamera.transform.forward : Vector3.left;
            }
            BeginMotion(
                ServiceMotionKind.PanelToMount,
                heldPanel.transform,
                heldPanel.transform.position,
                heldPanel.transform.rotation,
                Vector3.Lerp(heldPanel.transform.position, target, 0.55f) + Vector3.up * 0.12f + outward * 0.10f);
        }

        private void BeginMotion(
            ServiceMotionKind kind,
            Transform targetTransform,
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 midpoint)
        {
            motionKind = kind;
            motionTransform = targetTransform;
            motionStartPosition = startPosition;
            motionStartRotation = startRotation;
            motionMidpoint = midpoint;
            motionStartedAt = Time.unscaledTime;
            HasActiveAftInteraction = true;
        }

        private void UpdateServiceMotion()
        {
            if (motionKind == ServiceMotionKind.None || motionTransform == null)
            {
                return;
            }

            float duration = Mathf.Max(0.2f, serviceMotionDuration);
            float rawT = Mathf.Clamp01((Time.unscaledTime - motionStartedAt) / duration);
            float t = Mathf.SmoothStep(0f, 1f, rawT);
            GetMotionTarget(out Vector3 targetPosition, out Quaternion targetRotation);

            float oneMinusT = 1f - t;
            Vector3 curvedPosition = oneMinusT * oneMinusT * motionStartPosition
                + 2f * oneMinusT * t * motionMidpoint
                + t * t * targetPosition;
            motionTransform.SetPositionAndRotation(
                curvedPosition,
                Quaternion.Slerp(motionStartRotation, targetRotation, t));

            if (rawT >= 1f)
            {
                FinishMotion();
            }
        }

        private void GetMotionTarget(out Vector3 position, out Quaternion rotation)
        {
            switch (motionKind)
            {
                case ServiceMotionKind.ItemToSlot:
                    if (pendingSlot != null)
                    {
                        position = pendingSlot.transform.position;
                        rotation = pendingSlot.transform.rotation;
                        return;
                    }
                    break;
                case ServiceMotionKind.PanelToMount:
                    if (pendingBay != null && pendingBay.PanelAnchor != null)
                    {
                        position = pendingBay.PanelAnchor.position;
                        rotation = pendingBay.PanelAnchor.rotation;
                        return;
                    }
                    break;
                case ServiceMotionKind.ItemToHand:
                case ServiceMotionKind.PanelToHand:
                    position = GetHandPosition();
                    rotation = GetHandRotation();
                    return;
            }

            position = motionTransform != null ? motionTransform.position : GetHandPosition();
            rotation = motionTransform != null ? motionTransform.rotation : GetHandRotation();
        }

        private void FinishMotion()
        {
            ServiceMotionKind finishedKind = motionKind;
            Transform finishedTransform = motionTransform;
            motionKind = ServiceMotionKind.None;
            motionTransform = null;

            if (finishedKind == ServiceMotionKind.ItemToHand)
            {
                if (heldItem != null)
                {
                    heldItem.transform.SetPositionAndRotation(GetHandPosition(), GetHandRotation());
                }
                SetStatus("Equipment removed. Compatible empty rack positions are highlighted while you carry it.");
            }
            else if (finishedKind == ServiceMotionKind.ItemToSlot)
            {
                if (heldItem != null && pendingSlot != null
                    && pendingSlot.Bay != null
                    && pendingSlot.Bay.TryInstall(heldItem, pendingSlot, out string message))
                {
                    heldItem.SetHeld(false);
                    heldItem = null;
                    SetStatus(message);
                }
                else if (heldItem != null)
                {
                    heldItem.transform.SetPositionAndRotation(GetHandPosition(), GetHandRotation());
                    SetStatus("That rack position became unavailable. The equipment is still in your hand.");
                }
                pendingSlot = null;
            }
            else if (finishedKind == ServiceMotionKind.PanelToHand)
            {
                if (heldPanel != null)
                {
                    heldPanel.transform.SetPositionAndRotation(GetHandPosition(), GetHandRotation());
                }
                SetStatus("Aft access panel removed.");
            }
            else if (finishedKind == ServiceMotionKind.PanelToMount)
            {
                if (heldPanel != null && pendingBay != null)
                {
                    heldPanel.InstallOnAircraft(pendingBay);
                    heldPanel.SetHeld(false);
                    int released = heldPanel.FastenerCount - heldPanel.SecuredFastenerCount;
                    heldPanel = null;
                    SetStatus(released > 0
                        ? $"Panel settled into place. Secure the {released} released fastener{(released == 1 ? string.Empty : "s")} before flight."
                        : "Aft access panel installed and secured.");
                }
                pendingBay = null;
            }

            if (finishedTransform == null)
            {
                HasActiveAftInteraction = IsHoldingSomething;
            }
        }

        private string GetMotionPrompt()
        {
            switch (motionKind)
            {
                case ServiceMotionKind.ItemToHand:
                    return "Removing equipment...";
                case ServiceMotionKind.ItemToSlot:
                    return "Installing equipment...";
                case ServiceMotionKind.PanelToHand:
                    return "Lifting aft access panel clear...";
                case ServiceMotionKind.PanelToMount:
                    return "Settling aft access panel into place...";
                default:
                    return string.Empty;
            }
        }

        private bool TryFindAftInteractionHit(Ray ray, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, ~0, QueryTriggerInteraction.Collide);
            float bestScore = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit candidate = hits[i];
                if (candidate.collider == null || !IsAftInteractionTarget(candidate.collider))
                {
                    continue;
                }

                float priorityBias = candidate.collider.GetComponentInParent<P51AftPanelFastener>() != null
                    ? -0.18f
                    : candidate.collider.GetComponentInParent<P51AftEquipmentSlot>() != null
                        ? -0.04f
                        : 0f;
                float score = candidate.distance + priorityBias;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestHit = candidate;
                found = true;
            }
            return found;
        }

        private void LateUpdate()
        {
            if (interactionCamera == null)
            {
                return;
            }

            if (IsServiceAnimating)
            {
                UpdateServiceMotion();
                return;
            }

            Transform held = heldItem != null
                ? heldItem.transform
                : heldPanel != null
                    ? heldPanel.transform
                    : heldTester != null ? heldTester.transform : null;
            if (held == null)
            {
                return;
            }

            Vector3 targetPosition = GetHandPosition();
            held.position = Vector3.Lerp(held.position, targetPosition, 18f * Time.deltaTime);
            held.rotation = Quaternion.Slerp(
                held.rotation,
                GetHandRotation(),
                14f * Time.deltaTime);
        }

        private Vector3 GetHandPosition()
        {
            if (interactionCamera == null)
            {
                return transform.position + transform.forward * holdDistance;
            }
            return interactionCamera.transform.position
                + interactionCamera.transform.forward * holdDistance
                + interactionCamera.transform.right * 0.22f
                - interactionCamera.transform.up * 0.18f;
        }

        private Quaternion GetHandRotation()
        {
            return interactionCamera != null
                ? Quaternion.LookRotation(interactionCamera.transform.forward, interactionCamera.transform.up)
                : transform.rotation;
        }

        private void HoldItem(P51AftEquipmentItem item)
        {
            heldItem = item;
            heldItem.transform.SetParent(null, true);
            heldItem.SetHeld(true);
            HasActiveAftInteraction = true;
        }

        private void HoldPanel(P51AftAccessPanel panel)
        {
            heldPanel = panel;
            heldPanel.transform.SetParent(null, true);
            heldPanel.SetHeld(true);
            HasActiveAftInteraction = true;
        }

        private void DropHeldObject()
        {
            HideAllPlacementHighlights();
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

        private void RefreshSlotCache(bool force)
        {
            if (!force && Time.unscaledTime < nextSlotRefreshTime)
            {
                return;
            }

            cachedSlots = Object.FindObjectsByType<P51AftEquipmentSlot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            nextSlotRefreshTime = Time.unscaledTime + 1f;
        }

        private void UpdatePlacementHighlights()
        {
            if (cachedSlots == null)
            {
                return;
            }

            for (int i = 0; i < cachedSlots.Length; i++)
            {
                P51AftEquipmentSlot slot = cachedSlots[i];
                if (slot == null)
                {
                    continue;
                }

                bool show = heldItem != null
                    && slot.AcceptedKind == heldItem.EquipmentKind
                    && slot.InstalledItem == null
                    && slot.Bay != null
                    && slot.Bay.AccessOpen
                    && interactionCamera != null
                    && Vector3.Distance(slot.transform.position, interactionCamera.transform.position) <= placementGuideDistance;
                slot.SetPlacementHighlighted(show);
            }
        }

        private void HideAllPlacementHighlights()
        {
            if (cachedSlots == null)
            {
                return;
            }
            for (int i = 0; i < cachedSlots.Length; i++)
            {
                if (cachedSlots[i] != null)
                {
                    cachedSlots[i].SetPlacementHighlighted(false);
                }
            }
        }

        private static bool IsAftInteractionTarget(Collider collider)
        {
            return collider != null
                && (collider.GetComponentInParent<P51AftPanelFastener>() != null
                    || collider.GetComponentInParent<P51AftAccessPanel>() != null
                    || collider.GetComponentInParent<P51AftEquipmentSlot>() != null
                    || collider.GetComponentInParent<P51AftEquipmentItem>() != null
                    || collider.GetComponentInParent<P51BatteryTester>() != null);
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

        private void OnDisable()
        {
            HideAllPlacementHighlights();
            motionKind = ServiceMotionKind.None;
            motionTransform = null;
            pendingSlot = null;
            pendingBay = null;
            HasActiveAftInteraction = false;
        }
    }
}
