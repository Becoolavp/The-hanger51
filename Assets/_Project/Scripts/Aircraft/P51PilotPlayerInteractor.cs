using System;
using System.Collections.Generic;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class P51PilotPlayerInteractor : MonoBehaviour
    {
        [Header("Player References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private FirstPersonCameraSmoother cameraSmoother;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private InventoryInteractor inventoryInteractor;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private AircraftServicePlayerInteractor aircraftServiceInteractor;
        [SerializeField] private EngineHoistPlayerInteractor engineHoistInteractor;
        [SerializeField] private EquippedItemView equippedItemView;

        [Header("Cockpit Interaction")]
        [SerializeField, Min(1f)] private float interactionDistance = 6.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField, Min(0.01f)] private float cockpitMouseSensitivity = 0.08f;
        [SerializeField, Range(20f, 150f)] private float maximumLookYaw = 105f;
        [SerializeField, Range(10f, 89f)] private float maximumLookPitch = 70f;

        private readonly List<Behaviour> temporarilyDisabledBehaviours = new List<Behaviour>();

        private P51PilotSeat aimedSeat;
        private P51PilotSeat occupiedSeat;
        private P51ThirdPersonCamera thirdPersonCamera;
        private Transform originalCameraParent;
        private Vector3 originalCameraLocalPosition;
        private Quaternion originalCameraLocalRotation;
        private float originalCameraFieldOfView;
        private bool inventoryUIWasEnabled;
        private float cockpitLookYaw;
        private float cockpitLookPitch;
        private bool promptWasOwned;

        public bool IsPiloting => occupiedSeat != null;
        public P51PilotSeat OccupiedSeat => occupiedSeat;

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

            if (occupiedSeat != null)
            {
                HandleCockpitInput();
                return;
            }

            HandleEntryInteraction();
        }

        public void Configure(
            Camera configuredCamera,
            FirstPersonController configuredFirstPersonController,
            FirstPersonCameraSmoother configuredCameraSmoother,
            CharacterController configuredCharacterController,
            InventoryInteractor configuredInventoryInteractor,
            InventoryUI configuredInventoryUI)
        {
            playerCamera = configuredCamera;
            firstPersonController = configuredFirstPersonController;
            cameraSmoother = configuredCameraSmoother;
            characterController = configuredCharacterController;
            inventoryInteractor = configuredInventoryInteractor;
            inventoryUI = configuredInventoryUI;
            ResolveReferences();
        }

        public void ResetCockpitLook()
        {
            cockpitLookYaw = 0f;
            cockpitLookPitch = 0f;
        }

        private void HandleEntryInteraction()
        {
            if (playerCamera == null || inventoryUI == null || inventoryUI.IsOpen)
            {
                ClearOwnedPrompt();
                aimedSeat = null;
                return;
            }

            P51PilotSeat newSeat = FindAimedSeat();
            if (newSeat != aimedSeat)
            {
                if (aimedSeat != null)
                {
                    ClearOwnedPrompt();
                }
                aimedSeat = newSeat;
            }

            if (aimedSeat == null)
            {
                return;
            }

            inventoryUI.SetInteractionPrompt(aimedSeat.InteractionText);
            promptWasOwned = true;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (!aimedSeat.TryOccupy(out string reason))
            {
                inventoryUI.ShowStatusMessage(reason, 3f);
                return;
            }

            EnterCockpit(aimedSeat);
            aimedSeat = null;
        }

        private void HandleCockpitInput()
        {
            if (playerCamera == null || occupiedSeat == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            bool externalViewOwnsMouse = thirdPersonCamera != null
                && thirdPersonCamera.ThirdPersonActive;
            if (!externalViewOwnsMouse
                && mouse != null
                && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue() * cockpitMouseSensitivity;
                cockpitLookYaw = Mathf.Clamp(
                    cockpitLookYaw + mouseDelta.x,
                    -maximumLookYaw,
                    maximumLookYaw);
                cockpitLookPitch = Mathf.Clamp(
                    cockpitLookPitch - mouseDelta.y,
                    -maximumLookPitch,
                    maximumLookPitch);
                playerCamera.transform.localRotation = Quaternion.Euler(
                    cockpitLookPitch,
                    cockpitLookYaw,
                    0f);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                TryExitCockpit();
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }

            if (mouse != null
                && mouse.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLocked(true);
            }
        }

        private P51PilotSeat FindAimedSeat()
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
            for (int index = 0; index < hits.Length; index++)
            {
                P51PilotSeat seat = hits[index].collider.GetComponentInParent<P51PilotSeat>();
                if (seat != null && !seat.IsOccupied)
                {
                    return seat;
                }
            }

            return null;
        }

        private void EnterCockpit(P51PilotSeat seat)
        {
            if (seat == null || seat.CameraAnchor == null || playerCamera == null)
            {
                seat?.ForceRelease();
                return;
            }

            occupiedSeat = seat;
            ClearOwnedPrompt();

            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                inventoryUI.SetInventoryOpen(false);
            }

            originalCameraParent = playerCamera.transform.parent;
            originalCameraLocalPosition = playerCamera.transform.localPosition;
            originalCameraLocalRotation = playerCamera.transform.localRotation;
            originalCameraFieldOfView = playerCamera.fieldOfView;

            temporarilyDisabledBehaviours.Clear();
            DisableForCockpit(firstPersonController);
            DisableForCockpit(cameraSmoother);
            DisableForCockpit(inventoryInteractor);
            DisableForCockpit(aircraftServiceInteractor);
            DisableForCockpit(engineHoistInteractor);
            DisableForCockpit(equippedItemView);

            EngineAssemblyStation[] engineStations = FindObjectsByType<EngineAssemblyStation>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < engineStations.Length; index++)
            {
                DisableForCockpit(engineStations[index]);
            }

            inventoryUIWasEnabled = inventoryUI != null && inventoryUI.enabled;
            if (inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
                inventoryUI.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            playerCamera.transform.SetParent(seat.CameraAnchor, false);
            playerCamera.transform.localPosition = Vector3.zero;
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.fieldOfView = 72f;
            ResetCockpitLook();
            SetCursorLocked(true);
        }

        private void TryExitCockpit()
        {
            if (occupiedSeat == null)
            {
                return;
            }

            if (!occupiedSeat.TryRelease(out string reason))
            {
                occupiedSeat.FlightController?.ShowCockpitMessage(reason, 3f);
                return;
            }

            P51PilotSeat seatBeingExited = occupiedSeat;
            occupiedSeat = null;

            if (playerCamera != null)
            {
                playerCamera.transform.SetParent(originalCameraParent, false);
                playerCamera.transform.localPosition = originalCameraLocalPosition;
                playerCamera.transform.localRotation = originalCameraLocalRotation;
                playerCamera.fieldOfView = originalCameraFieldOfView;
            }

            if (seatBeingExited.ExitPoint != null)
            {
                transform.position = seatBeingExited.ExitPoint.position;
                float aircraftYaw = seatBeingExited.transform.root.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, aircraftYaw + 90f, 0f);
            }

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            for (int index = 0; index < temporarilyDisabledBehaviours.Count; index++)
            {
                if (temporarilyDisabledBehaviours[index] != null)
                {
                    temporarilyDisabledBehaviours[index].enabled = true;
                }
            }
            temporarilyDisabledBehaviours.Clear();

            if (inventoryUI != null)
            {
                inventoryUI.enabled = inventoryUIWasEnabled;
                inventoryUI.SetInteractionPrompt(string.Empty);
                inventoryUI.ShowStatusMessage("Exited the P-51 cockpit.", 2f);
            }

            if (cameraSmoother != null && cameraSmoother.enabled)
            {
                cameraSmoother.SnapToTarget();
            }

            SetCursorLocked(true);
        }

        private void DisableForCockpit(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled || behaviour == this)
            {
                return;
            }

            temporarilyDisabledBehaviours.Add(behaviour);
            behaviour.enabled = false;
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (cameraSmoother == null && playerCamera != null)
            {
                cameraSmoother = playerCamera.GetComponent<FirstPersonCameraSmoother>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (inventoryInteractor == null)
            {
                inventoryInteractor = GetComponent<InventoryInteractor>();
            }

            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            }

            if (aircraftServiceInteractor == null)
            {
                aircraftServiceInteractor = GetComponent<AircraftServicePlayerInteractor>();
            }

            if (engineHoistInteractor == null)
            {
                engineHoistInteractor = GetComponent<EngineHoistPlayerInteractor>();
            }

            if (equippedItemView == null && playerCamera != null)
            {
                equippedItemView = playerCamera.GetComponentInChildren<EquippedItemView>(true);
            }

            if (thirdPersonCamera == null)
            {
                thirdPersonCamera = GetComponent<P51ThirdPersonCamera>();
            }
        }

        private void ClearOwnedPrompt()
        {
            if (promptWasOwned && inventoryUI != null)
            {
                inventoryUI.SetInteractionPrompt(string.Empty);
            }

            promptWasOwned = false;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable()
        {
            ClearOwnedPrompt();

            if (occupiedSeat != null)
            {
                occupiedSeat.ForceRelease();
                occupiedSeat = null;
            }
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
            cockpitMouseSensitivity = Mathf.Max(0.01f, cockpitMouseSensitivity);
        }
    }
}
