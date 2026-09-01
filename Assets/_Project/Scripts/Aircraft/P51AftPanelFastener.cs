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
            fastenerIndex = Mathf.Clamp(configuredIndex, 0, 3);
            secured = startsSecured;
            NormalizeMountTransform();
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
            ResolvePanel();
            NormalizeMountTransform();
            RefreshTarget(true);
        }

        private void OnEnable()
        {
            ResolvePanel();
            NormalizeMountTransform();
            RefreshTarget(true);
        }

        private void Update()
        {
            if (!rotationCaptured)
            {
                NormalizeMountTransform();
                RefreshTarget(true);
                return;
            }

            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void ResolvePanel()
        {
            if (panel == null)
            {
                panel = GetComponentInParent<P51AftAccessPanel>();
            }
        }

        private void NormalizeMountTransform()
        {
            ResolvePanel();
            if (panel == null)
            {
                CaptureCurrentRotation();
                return;
            }

            // The removable panel is thin on local X. Its exterior skin is on the negative-X face,
            // so all four quarter-turn fasteners sit just outside that face and point through the
            // skin along the panel normal. This also repairs legacy scenes where Fastener 1 was
            // left rotated or displaced away from the panel.
            BoxCollider panelCollider = panel.GetComponent<BoxCollider>();
            Vector3 center = panelCollider != null ? panelCollider.center : Vector3.zero;
            Vector3 size = panelCollider != null ? panelCollider.size : Vector3.one;

            float ySign = fastenerIndex < 2 ? 1f : -1f;
            float zSign = (fastenerIndex & 1) == 0 ? -1f : 1f;
            Vector3 panelLocalPosition = new Vector3(
                center.x - size.x * 0.56f,
                center.y + size.y * 0.40f * ySign,
                center.z + size.z * 0.42f * zSign);

            transform.position = panel.transform.TransformPoint(panelLocalPosition);

            Quaternion panelLocalMountRotation = Quaternion.Euler(0f, 0f, -90f);
            Quaternion worldSecuredRotation = panel.transform.rotation * panelLocalMountRotation;
            securedRotation = transform.parent != null
                ? Quaternion.Inverse(transform.parent.rotation) * worldSecuredRotation
                : worldSecuredRotation;
            rotationCaptured = true;
        }

        private void CaptureCurrentRotation()
        {
            securedRotation = transform.localRotation;
            rotationCaptured = true;
        }

        private void RefreshTarget(bool snap)
        {
            if (!rotationCaptured)
            {
                NormalizeMountTransform();
            }

            // Twist around the fastener's own cylinder axis. The base mount rotation remains fixed
            // normal to the panel, so releasing a fastener turns it without tipping it sideways.
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
            fastenerIndex = Mathf.Clamp(fastenerIndex, 0, 3);
            ResolvePanel();
            if (panel != null)
            {
                NormalizeMountTransform();
                RefreshTarget(true);
            }
        }
    }
}
