using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class H51RunwayLightSwitch : MonoBehaviour
    {
        [SerializeField] H51RunwayLightingController controller;
        [SerializeField] TextMesh statusLabel;

        Transform viewer;

        public string InteractionText =>
            controller
                ? "E: cycle runway lights  •  " + controller.StatusText
                : "Runway light controller unavailable";

        public void Configure(
            H51RunwayLightingController configuredController,
            TextMesh configuredLabel)
        {
            controller = configuredController;
            statusLabel = configuredLabel;
            if (statusLabel)
            {
                statusLabel.transform.localRotation = Quaternion.identity;
            }
            RefreshLabel();
            RefreshLabelVisibility();
        }

        void Awake()
        {
            if (!controller)
                controller = FindFirstObjectByType<H51RunwayLightingController>(FindObjectsInactive.Include);
            if (statusLabel)
                statusLabel.transform.localRotation = Quaternion.identity;
            RefreshLabel();
            RefreshLabelVisibility();
        }

        void Update()
        {
            RefreshLabel();
            RefreshLabelVisibility();
        }

        public bool TryCycle(out string message)
        {
            if (!controller)
            {
                message = "The runway lighting controller is unavailable.";
                return false;
            }

            controller.CycleMode();
            RefreshLabel();

            if (controller.Mode != H51RunwayLightMode.Off && !controller.EffectiveOn)
            {
                message =
                    controller.StatusText +
                    ". The lights are requested but grid power is unavailable or AUTO is waiting for darkness.";
            }
            else
            {
                message = controller.StatusText + ".";
            }
            return true;
        }

        void RefreshLabelVisibility()
        {
            if (!statusLabel) return;

            if (!viewer)
            {
                Camera camera = Camera.main;
                if (camera) viewer = camera.transform;
            }

            if (!viewer)
            {
                statusLabel.gameObject.SetActive(true);
                return;
            }

            Vector3 toViewer = viewer.position - transform.position;
            toViewer.y = 0f;

            // The readable cabinet face is local -Z and is mounted toward the
            // airport interior. Hide the TextMesh completely when viewed through
            // the wall from the exterior so it cannot appear mirrored/through-wall.
            bool viewerOnReadableSide =
                toViewer.sqrMagnitude < .01f ||
                Vector3.Dot(toViewer.normalized,-transform.forward) > .05f;

            if (statusLabel.gameObject.activeSelf != viewerOnReadableSide)
                statusLabel.gameObject.SetActive(viewerOnReadableSide);
        }

        void RefreshLabel()
        {
            if (!statusLabel) return;
            statusLabel.text = controller
                ? "RUNWAY LIGHTING\n" + controller.StatusText + "\nE: CYCLE OFF / AUTO / ON"
                : "RUNWAY LIGHTING\nCONTROLLER OFFLINE";
        }
    }
}
