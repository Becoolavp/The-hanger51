using UnityEngine;

namespace Hanger51.Aircraft
{
    public enum P51LandingGearServiceKind
    {
        MountBolt,
        TireAndValve
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51LandingGearServiceTarget : MonoBehaviour
    {
        [SerializeField] private P51LandingGearMaintenanceController controller;
        [SerializeField] private P51LandingGearServiceKind serviceKind;
        [SerializeField, Range(0, 2)] private int wheelIndex;
        [SerializeField, Min(0.2f)] private float holdDuration = 1.15f;

        private float holdProgress;
        private bool removing;

        public P51LandingGearMaintenanceController Controller
        {
            get
            {
                ResolveController();
                return controller;
            }
        }
        public P51LandingGearServiceKind ServiceKind => serviceKind;
        public int WheelIndex => wheelIndex;
        public Transform ServicePoint => transform;

        public string InteractionText
        {
            get
            {
                ResolveController();
                if (controller == null)
                {
                    return string.Empty;
                }

                string name = controller.GetWheelName(wheelIndex);
                if (!controller.CanService(out string reason))
                {
                    return $"{name} gear — {reason} | X inspect";
                }

                int percent = Mathf.RoundToInt(holdProgress * 100f);
                string progress = holdProgress > 0f ? $" ({percent}%)" : string.Empty;
                if (serviceKind == P51LandingGearServiceKind.MountBolt)
                {
                    return controller.IsGearInstalled(wheelIndex)
                        ? $"Hold R: remove large {name} gear mounting bolt and gear{progress} | X inspect"
                        : $"Hold E: reinstall {name} gear and large mounting bolt{progress} | X inspect";
                }

                if (!controller.IsGearInstalled(wheelIndex))
                {
                    return $"{name} wheel station — reinstall the gear first | X inspect";
                }

                return controller.IsTireInstalled(wheelIndex)
                    ? $"Hold R: remove {name} tire from rim{progress} | N: connect nitrogen cart | X inspect"
                    : $"Hold E: reinstall the same {name} tire on rim{progress} | X inspect";
            }
        }

        private void Awake()
        {
            ResolveController();
        }

        private void OnEnable()
        {
            ResolveController();
        }

        public void Configure(
            P51LandingGearMaintenanceController configuredController,
            P51LandingGearServiceKind configuredKind,
            int configuredWheelIndex,
            float configuredHoldDuration)
        {
            controller = configuredController;
            serviceKind = configuredKind;
            wheelIndex = Mathf.Clamp(configuredWheelIndex, 0, 2);
            holdDuration = Mathf.Max(0.2f, configuredHoldDuration);
            ResolveController();
        }

        public bool ProcessInteraction(
            bool holdInstall,
            bool holdRemove,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveController();
            if (controller == null)
            {
                CancelHold();
                return false;
            }

            bool shouldRemove = holdRemove && !holdInstall;
            bool shouldInstall = holdInstall && !holdRemove;
            bool valid;
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                valid = shouldRemove
                    ? controller.IsGearInstalled(wheelIndex)
                    : shouldInstall && !controller.IsGearInstalled(wheelIndex);
            }
            else
            {
                valid = controller.IsGearInstalled(wheelIndex)
                    && (shouldRemove
                        ? controller.IsTireInstalled(wheelIndex)
                        : shouldInstall && !controller.IsTireInstalled(wheelIndex));
            }

            if (!valid || !controller.CanService(out resultMessage))
            {
                CancelHold();
                return false;
            }

            if (holdProgress > 0f && removing != shouldRemove)
            {
                holdProgress = 0f;
            }
            removing = shouldRemove;
            holdProgress = Mathf.Clamp01(
                holdProgress + Mathf.Max(0f, deltaTime) / holdDuration);
            if (holdProgress < 1f)
            {
                return false;
            }

            bool completed;
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                completed = removing
                    ? controller.TryRemoveGear(wheelIndex, out resultMessage)
                    : controller.TryInstallGear(wheelIndex, out resultMessage);
            }
            else
            {
                completed = removing
                    ? controller.TryRemoveTire(wheelIndex, out resultMessage)
                    : controller.TryInstallTire(wheelIndex, out resultMessage);
            }

            CancelHold();
            return completed;
        }

        public string Inspect()
        {
            ResolveController();
            return controller != null
                ? controller.GetInspectionText(wheelIndex)
                : "Landing gear condition controller is missing.";
        }

        public void CancelHold()
        {
            holdProgress = 0f;
            removing = false;
        }

        private void ResolveController()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<P51LandingGearMaintenanceController>();
            }
        }

        private void OnDisable()
        {
            CancelHold();
        }

        private void OnValidate()
        {
            wheelIndex = Mathf.Clamp(wheelIndex, 0, 2);
            holdDuration = Mathf.Max(0.2f, holdDuration);
            ResolveController();
        }
    }
}
