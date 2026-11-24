using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class H51RunwayLightSwitch : MonoBehaviour
    {
        [SerializeField] H51RunwayLightingController controller;
        [SerializeField] TextMesh statusLabel;

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
            RefreshLabel();
        }

        void Awake()
        {
            if (!controller)
                controller = FindFirstObjectByType<H51RunwayLightingController>(FindObjectsInactive.Include);
            RefreshLabel();
        }

        void Update()
        {
            RefreshLabel();
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

        void RefreshLabel()
        {
            if (!statusLabel) return;
            statusLabel.text = controller
                ? "RUNWAY LIGHTING\n" + controller.StatusText + "\nE: CYCLE OFF / AUTO / ON"
                : "RUNWAY LIGHTING\nCONTROLLER OFFLINE";
        }
    }
}
