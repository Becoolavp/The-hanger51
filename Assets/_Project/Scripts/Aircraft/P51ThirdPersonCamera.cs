using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(340)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51PilotPlayerInteractor))]
    public sealed class P51ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private P51PilotPlayerInteractor pilotInteractor;
        [SerializeField] private Camera playerCamera;

        [Header("External Orbit View")]
        [SerializeField, Min(3f)] private float orbitDistance = 13.5f;
        [SerializeField, Min(0f)] private float focusForwardOffset = 0.9f;
        [SerializeField, Min(0f)] private float focusUpOffset = 0.45f;
        [SerializeField, Range(5f, 40f)] private float startingPitch = 14f;
        [SerializeField, Range(45f, 180f)] private float maximumOrbitYaw = 180f;
        [SerializeField, Range(2f, 75f)] private float minimumOrbitPitch = 5f;
        [SerializeField, Range(10f, 85f)] private float maximumOrbitPitch = 65f;
        [SerializeField, Min(0.01f)] private float orbitMouseSensitivity = 0.10f;
        [SerializeField, Min(1f)] private float cameraSharpness = 12f;
        [SerializeField, Min(0.05f)] private float obstaclePadding = 0.40f;
        [SerializeField, Range(45f, 100f)] private float externalFieldOfView = 80f;
        [SerializeField] private LayerMask cameraCollisionLayers = ~0;

        private bool thirdPersonActive;
        private float orbitYaw;
        private float orbitPitch;
        private GUIStyle viewStyle;

        public bool ThirdPersonActive => thirdPersonActive;
        public P51PilotPlayerInteractor PilotInteractor => pilotInteractor;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(
            P51PilotPlayerInteractor configuredPilotInteractor,
            Camera configuredCamera)
        {
            pilotInteractor = configuredPilotInteractor;
            playerCamera = configuredCamera;
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            if (pilotInteractor == null || !pilotInteractor.IsPiloting)
            {
                thirdPersonActive = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            {
                SetThirdPersonActive(!thirdPersonActive);
            }

            if (!thirdPersonActive)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue() * orbitMouseSensitivity;
                orbitYaw = Mathf.Clamp(
                    orbitYaw + mouseDelta.x,
                    -maximumOrbitYaw,
                    maximumOrbitYaw);
                orbitPitch = Mathf.Clamp(
                    orbitPitch - mouseDelta.y,
                    minimumOrbitPitch,
                    maximumOrbitPitch);
            }

            pilotInteractor.ResetCockpitLook();
        }

        private void LateUpdate()
        {
            if (!thirdPersonActive
                || pilotInteractor == null
                || !pilotInteractor.IsPiloting
                || playerCamera == null)
            {
                return;
            }

            UpdateExternalCamera(false);
        }

        private void SetThirdPersonActive(bool active)
        {
            if (pilotInteractor == null || !pilotInteractor.IsPiloting)
            {
                thirdPersonActive = false;
                return;
            }

            thirdPersonActive = active;
            P51PilotSeat seat = pilotInteractor.OccupiedSeat;
            if (active)
            {
                orbitYaw = 0f;
                orbitPitch = startingPitch;
                pilotInteractor.ResetCockpitLook();

                // The external camera must not inherit the aircraft's roll and
                // pitch from the cockpit anchor. Detaching it gives the mouse a
                // true world-up orbit around the airplane.
                if (playerCamera != null)
                {
                    playerCamera.transform.SetParent(null, true);
                }

                UpdateExternalCamera(true);
                seat?.FlightController?.ShowCockpitMessage(
                    "External view: move the mouse to orbit around the P-51. Press V for cockpit view.",
                    3f);
                return;
            }

            RestoreCockpitCamera(seat);
            seat?.FlightController?.ShowCockpitMessage(
                "Cockpit view restored. Press V for external view.",
                2.5f);
        }

        private void UpdateExternalCamera(bool snapImmediately)
        {
            P51PilotSeat seat = pilotInteractor != null
                ? pilotInteractor.OccupiedSeat
                : null;
            P51FlightController flightController = seat != null
                ? seat.FlightController
                : null;
            if (seat == null
                || seat.CameraAnchor == null
                || flightController == null
                || playerCamera == null)
            {
                return;
            }

            Transform aircraft = flightController.transform;
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                aircraft.forward,
                Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.001f)
            {
                horizontalForward = Vector3.forward;
            }
            else
            {
                horizontalForward.Normalize();
            }

            Vector3 focusPoint = seat.CameraAnchor.position
                + horizontalForward * focusForwardOffset
                + Vector3.up * focusUpOffset;

            float aircraftHeading = Mathf.Atan2(
                horizontalForward.x,
                horizontalForward.z) * Mathf.Rad2Deg;
            Quaternion orbitRotation = Quaternion.Euler(
                orbitPitch,
                aircraftHeading + orbitYaw,
                0f);
            Vector3 desiredPosition = focusPoint
                + orbitRotation * (Vector3.back * orbitDistance);
            desiredPosition = ResolveCameraCollision(
                focusPoint,
                desiredPosition,
                aircraft);

            Vector3 lookDirection = focusPoint - desiredPosition;
            Quaternion desiredRotation = lookDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : playerCamera.transform.rotation;

            if (snapImmediately)
            {
                playerCamera.transform.SetPositionAndRotation(
                    desiredPosition,
                    desiredRotation);
                playerCamera.fieldOfView = externalFieldOfView;
                return;
            }

            float blend = 1f - Mathf.Exp(-cameraSharpness * Time.deltaTime);
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position,
                desiredPosition,
                blend);
            playerCamera.transform.rotation = Quaternion.Slerp(
                playerCamera.transform.rotation,
                desiredRotation,
                blend);
            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView,
                externalFieldOfView,
                blend);
        }

        private void RestoreCockpitCamera(P51PilotSeat seat)
        {
            if (playerCamera == null || seat == null || seat.CameraAnchor == null)
            {
                return;
            }

            playerCamera.transform.SetParent(seat.CameraAnchor, false);
            playerCamera.transform.localPosition = Vector3.zero;
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.fieldOfView = 72f;
            pilotInteractor?.ResetCockpitLook();
        }

        private Vector3 ResolveCameraCollision(
            Vector3 focusPoint,
            Vector3 desiredPosition,
            Transform aircraftRoot)
        {
            Vector3 direction = desiredPosition - focusPoint;
            float distance = direction.magnitude;
            if (distance < 0.1f)
            {
                return desiredPosition;
            }

            direction /= distance;
            RaycastHit[] hits = Physics.SphereCastAll(
                focusPoint,
                0.22f,
                direction,
                distance,
                cameraCollisionLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = distance;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                Transform hitTransform = collider.transform;
                if ((aircraftRoot != null && hitTransform.IsChildOf(aircraftRoot))
                    || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hits[index].distance);
            }

            if (nearestDistance >= distance)
            {
                return desiredPosition;
            }

            float safeDistance = Mathf.Max(
                0.9f,
                nearestDistance - obstaclePadding);
            return focusPoint + direction * safeDistance;
        }

        private void ResolveReferences()
        {
            if (pilotInteractor == null)
            {
                pilotInteractor = GetComponent<P51PilotPlayerInteractor>();
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }
        }

        private void OnGUI()
        {
            if (pilotInteractor == null || !pilotInteractor.IsPiloting)
            {
                return;
            }

            if (viewStyle == null)
            {
                viewStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                viewStyle.normal.textColor = Color.white;
            }

            string label = thirdPersonActive
                ? "VIEW: EXTERNAL ORBIT   [V]"
                : "VIEW: COCKPIT   [V]";
            GUI.Box(
                new Rect(Screen.width - 258f, 18f, 240f, 38f),
                label,
                viewStyle);
        }

        private void OnDisable()
        {
            if (thirdPersonActive
                && pilotInteractor != null
                && pilotInteractor.IsPiloting)
            {
                RestoreCockpitCamera(pilotInteractor.OccupiedSeat);
            }
            thirdPersonActive = false;
        }

        private void OnValidate()
        {
            orbitDistance = Mathf.Max(3f, orbitDistance);
            focusForwardOffset = Mathf.Max(0f, focusForwardOffset);
            focusUpOffset = Mathf.Max(0f, focusUpOffset);
            orbitMouseSensitivity = Mathf.Max(0.01f, orbitMouseSensitivity);
            cameraSharpness = Mathf.Max(1f, cameraSharpness);
            obstaclePadding = Mathf.Max(0.05f, obstaclePadding);
            maximumOrbitYaw = Mathf.Clamp(maximumOrbitYaw, 45f, 180f);
            minimumOrbitPitch = Mathf.Clamp(minimumOrbitPitch, 2f, 75f);
            maximumOrbitPitch = Mathf.Clamp(
                maximumOrbitPitch,
                minimumOrbitPitch + 1f,
                85f);
        }
    }
}
