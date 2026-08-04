using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(340)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51PilotPlayerInteractor))]
    public sealed class P51ThirdPersonCamera : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo CockpitLookYawField =
            typeof(P51PilotPlayerInteractor).GetField(
                "cockpitLookYaw",
                PrivateInstance);
        private static readonly FieldInfo CockpitLookPitchField =
            typeof(P51PilotPlayerInteractor).GetField(
                "cockpitLookPitch",
                PrivateInstance);

        [Header("References")]
        [SerializeField] private P51PilotPlayerInteractor pilotInteractor;
        [SerializeField] private Camera playerCamera;

        [Header("External View")]
        [SerializeField, Min(3f)] private float chaseDistance = 13.5f;
        [SerializeField, Min(0f)] private float focusForwardOffset = 1.2f;
        [SerializeField, Min(0f)] private float focusUpOffset = 0.65f;
        [SerializeField, Range(5f, 40f)] private float startingPitch = 14f;
        [SerializeField, Range(-160f, 160f)] private float maximumOrbitYaw = 150f;
        [SerializeField, Range(5f, 75f)] private float minimumOrbitPitch = 7f;
        [SerializeField, Range(10f, 85f)] private float maximumOrbitPitch = 55f;
        [SerializeField, Min(0.01f)] private float orbitMouseSensitivity = 0.09f;
        [SerializeField, Min(1f)] private float cameraSharpness = 11f;
        [SerializeField, Min(0.05f)] private float obstaclePadding = 0.35f;
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
                Vector2 delta = mouse.delta.ReadValue() * orbitMouseSensitivity;
                orbitYaw = Mathf.Clamp(
                    orbitYaw + delta.x,
                    -maximumOrbitYaw,
                    maximumOrbitYaw);
                orbitPitch = Mathf.Clamp(
                    orbitPitch - delta.y,
                    minimumOrbitPitch,
                    maximumOrbitPitch);
            }

            ResetCockpitLookAccumulator();
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

            P51PilotSeat seat = pilotInteractor.OccupiedSeat;
            P51FlightController flightController = seat != null
                ? seat.FlightController
                : null;
            if (seat == null
                || seat.CameraAnchor == null
                || flightController == null)
            {
                return;
            }

            Transform aircraft = flightController.transform;
            Vector3 focusPoint = seat.CameraAnchor.position
                + aircraft.forward * focusForwardOffset
                + aircraft.up * focusUpOffset;

            Quaternion orbitRotation = Quaternion.Euler(
                orbitPitch,
                orbitYaw,
                0f);
            Vector3 localOffset = orbitRotation
                * new Vector3(0f, 0f, -chaseDistance);
            Vector3 desiredPosition = focusPoint
                + aircraft.TransformDirection(localOffset);
            desiredPosition = ResolveCameraCollision(
                focusPoint,
                desiredPosition,
                aircraft);

            float blend = 1f - Mathf.Exp(-cameraSharpness * Time.deltaTime);
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position,
                desiredPosition,
                blend);

            Vector3 lookDirection = focusPoint - playerCamera.transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);
                playerCamera.transform.rotation = Quaternion.Slerp(
                    playerCamera.transform.rotation,
                    desiredRotation,
                    blend);
            }

            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView,
                externalFieldOfView,
                blend);
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
                seat?.FlightController?.ShowCockpitMessage(
                    "Third-person aircraft view. Move the mouse to orbit; press V for cockpit view.",
                    3f);
                ResetCockpitLookAccumulator();
                return;
            }

            RestoreCockpitCamera(seat);
            seat?.FlightController?.ShowCockpitMessage(
                "Cockpit view restored. Press V for third-person view.",
                2.5f);
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
            ResetCockpitLookAccumulator();
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
            RaycastHit[] hits = Physics.RaycastAll(
                focusPoint,
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
                0.8f,
                nearestDistance - obstaclePadding);
            return focusPoint + direction * safeDistance;
        }

        private void ResetCockpitLookAccumulator()
        {
            if (pilotInteractor == null)
            {
                return;
            }

            CockpitLookYawField?.SetValue(pilotInteractor, 0f);
            CockpitLookPitchField?.SetValue(pilotInteractor, 0f);
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
                ? "VIEW: EXTERNAL   [V]"
                : "VIEW: COCKPIT   [V]";
            GUI.Box(
                new Rect(Screen.width - 218f, 18f, 200f, 38f),
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
            chaseDistance = Mathf.Max(3f, chaseDistance);
            focusForwardOffset = Mathf.Max(0f, focusForwardOffset);
            focusUpOffset = Mathf.Max(0f, focusUpOffset);
            orbitMouseSensitivity = Mathf.Max(0.01f, orbitMouseSensitivity);
            cameraSharpness = Mathf.Max(1f, cameraSharpness);
            obstaclePadding = Mathf.Max(0.05f, obstaclePadding);
            minimumOrbitPitch = Mathf.Clamp(minimumOrbitPitch, 5f, 75f);
            maximumOrbitPitch = Mathf.Clamp(
                maximumOrbitPitch,
                minimumOrbitPitch + 1f,
                85f);
        }
    }
}
