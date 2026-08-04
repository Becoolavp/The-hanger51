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
            Snap();
        }

        private void Awake()
        {
            if (inspectionCollider == null)
            {
                inspectionCollider = GetComponent<Collider>();
            }
            Snap();
        }

        private void LateUpdate()
        {
            Snap();
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
    }
}
