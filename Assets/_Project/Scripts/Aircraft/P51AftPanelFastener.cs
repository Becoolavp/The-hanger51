using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51AftPanelFastener : MonoBehaviour
    {
        [SerializeField] private P51AftAccessPanel panel;
        [SerializeField] private int fastenerIndex;
        [SerializeField] private bool secured = true;
        [SerializeField, Min(90f)] private float turnSpeedDegreesPerSecond = 540f;

        private Quaternion securedRotation;
        private bool rotationCaptured;
        private Quaternion targetRotation;

        public P51AftAccessPanel Panel => panel;
        public int FastenerIndex => fastenerIndex;
        public bool IsSecured => secured;

        public void Configure(P51AftAccessPanel configuredPanel, int configuredIndex, bool startsSecured)
        {
            panel = configuredPanel;
            fastenerIndex = Mathf.Max(0, configuredIndex);
            secured = startsSecured;
            CaptureRotation();
            RefreshTarget(true);
        }

        public bool TryToggle(out string message)
        {
            message = string.Empty;
            if (panel == null)
            {
                message = "This aft-panel fastener is not connected to a panel.";
                return false;
            }
            if (!panel.IsInstalled)
            {
                message = "Reinstall the aft access panel before working its fasteners.";
                return false;
            }

            secured = !secured;
            RefreshTarget(false);
            int remaining = panel.SecuredFastenerCount;
            message = secured
                ? $"Secured aft-panel fastener {fastenerIndex + 1}. {remaining} fastener{(remaining == 1 ? string.Empty : "s")} secured."
                : remaining > 0
                    ? $"Released aft-panel fastener {fastenerIndex + 1}. {remaining} still secured."
                    : "All aft-panel fasteners released. The access panel can now be removed.";
            return true;
        }

        private void Awake()
        {
            CaptureRotation();
            RefreshTarget(true);
        }

        private void OnEnable()
        {
            CaptureRotation();
            RefreshTarget(true);
        }

        private void Update()
        {
            if (!rotationCaptured)
            {
                CaptureRotation();
                RefreshTarget(true);
                return;
            }

            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void CaptureRotation()
        {
            if (rotationCaptured)
            {
                return;
            }
            securedRotation = transform.localRotation;
            rotationCaptured = true;
        }

        private void RefreshTarget(bool snap)
        {
            CaptureRotation();
            targetRotation = secured
                ? securedRotation
                : securedRotation * Quaternion.AngleAxis(90f, Vector3.up);
            if (snap)
            {
                transform.localRotation = targetRotation;
            }
        }

        private void OnValidate()
        {
            turnSpeedDegreesPerSecond = Mathf.Max(90f, turnSpeedDegreesPerSecond);
        }
    }
}
