using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class H51RunwayLightSwitch : MonoBehaviour
    {
        [SerializeField] H51RunwayLightingController controller;
        [SerializeField] TextMesh statusLabel;
        [SerializeField, Min(2f)] float maxLabelViewDistance = 10f;

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

            Vector3 labelPosition = statusLabel.transform.position;
            Vector3 toLabel = labelPosition - viewer.position;
            float distance = toLabel.magnitude;

            bool closeEnough =
                distance <= Mathf.Max(2f,maxLabelViewDistance);

            Vector3 toViewer = viewer.position - transform.position;
            toViewer.y = 0f;
            bool viewerOnReadableSide =
                toViewer.sqrMagnitude < .01f ||
                Vector3.Dot(
                    toViewer.normalized,
                    -transform.forward) > .05f;

            bool clearLineOfSight =
                closeEnough &&
                HasClearLineOfSight(
                    viewer.position,
                    labelPosition,
                    distance);

            bool visible =
                closeEnough &&
                viewerOnReadableSide &&
                clearLineOfSight;

            if (statusLabel.gameObject.activeSelf != visible)
                statusLabel.gameObject.SetActive(visible);
        }

        bool HasClearLineOfSight(
            Vector3 origin,
            Vector3 target,
            float targetDistance)
        {
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude < .0001f)
                return true;

            direction.Normalize();
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                targetDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i=0;i<hits.Length;i++)
            {
                Collider hit = hits[i].collider;
                if (!hit) continue;

                Transform hitTransform = hit.transform;
                if (hitTransform.IsChildOf(transform))
                    continue;
                if (viewer &&
                    (hitTransform == viewer ||
                     hitTransform.IsChildOf(viewer)))
                    continue;

                if (hits[i].distance <
                    targetDistance - .05f)
                    return false;
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
