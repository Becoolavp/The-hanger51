using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(210)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EngineConditionInspectionFollower : MonoBehaviour
    {
        [SerializeField] private Transform visualToFollow;
        [SerializeField] private Collider inspectionCollider;
        [SerializeField] private Vector3 localPositionOffset;
        [SerializeField] private Quaternion localRotationOffset = Quaternion.identity;

        public void Configure(
            Transform configuredVisual,
            Collider configuredCollider,
            Vector3 configuredLocalPositionOffset,
            Quaternion configuredLocalRotationOffset)
        {
            visualToFollow = configuredVisual;
            inspectionCollider = configuredCollider;
            localPositionOffset = configuredLocalPositionOffset;
            localRotationOffset = configuredLocalRotationOffset;
            SanitizeCollider();
            Snap();
        }

        private void Awake()
        {
            ResolveCollider();
            SanitizeCollider();
            Snap();
        }

        private void OnEnable()
        {
            ResolveCollider();
            SanitizeCollider();
            Snap();
        }

        private void LateUpdate()
        {
            Snap();
        }

        private void ResolveCollider()
        {
            if (inspectionCollider == null)
            {
                inspectionCollider = GetComponent<Collider>();
            }
        }

        private void SanitizeCollider()
        {
            ResolveCollider();
            if (inspectionCollider == null)
            {
                return;
            }

            // Inspection volumes are raycast targets only. They must never be
            // solid geometry that can block the Player, engine hoist, cowling,
            // or maintenance tools.
            inspectionCollider.isTrigger = true;

            if (inspectionCollider is BoxCollider box)
            {
                box.size = new Vector3(
                    Mathf.Clamp(box.size.x, 0.15f, 0.50f),
                    Mathf.Clamp(box.size.y, 0.15f, 0.50f),
                    Mathf.Clamp(box.size.z, 0.15f, 0.50f));
            }
        }

        private void Snap()
        {
            if (visualToFollow == null)
            {
                if (inspectionCollider != null)
                {
                    inspectionCollider.enabled = false;
                }
                return;
            }

            transform.SetPositionAndRotation(
                visualToFollow.TransformPoint(localPositionOffset),
                visualToFollow.rotation * localRotationOffset);
            if (inspectionCollider != null)
            {
                inspectionCollider.enabled = visualToFollow.gameObject.activeInHierarchy;
            }
        }

        private void OnValidate()
        {
            SanitizeCollider();
        }
    }
}
