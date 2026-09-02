using UnityEngine;

namespace Hanger51.Player
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraSmoother : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private FirstPersonController playerController;

        [Header("Camera Position")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.65f, 0f);
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.025f;

        private Vector3 smoothedWorldPosition;
        private Vector3 positionVelocity;
        private bool isInitialized;

        private void Awake()
        {
            ResolveReferences();
            SnapToTarget();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (followTarget == null || playerController == null)
            {
                return;
            }

            Vector3 desiredPosition = GetDesiredPosition();

            if (!isInitialized || positionSmoothTime <= 0f)
            {
                smoothedWorldPosition = desiredPosition;
                positionVelocity = Vector3.zero;
                isInitialized = true;
            }
            else
            {
                smoothedWorldPosition = Vector3.SmoothDamp(
                    smoothedWorldPosition,
                    desiredPosition,
                    ref positionVelocity,
                    positionSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            Quaternion desiredRotation = Quaternion.Euler(
                playerController.CameraPitch,
                followTarget.eulerAngles.y,
                0f);

            transform.SetPositionAndRotation(smoothedWorldPosition, desiredRotation);
        }

        public void SnapToTarget()
        {
            ResolveReferences();
            if (followTarget == null)
            {
                return;
            }

            smoothedWorldPosition = GetDesiredPosition();
            positionVelocity = Vector3.zero;
            isInitialized = true;
            transform.position = smoothedWorldPosition;
        }

        private Vector3 GetDesiredPosition()
        {
            Vector3 adjustedOffset = eyeOffset;
            if (playerController != null)
            {
                adjustedOffset.y += playerController.CrouchCameraOffset;
            }

            return followTarget != null
                ? followTarget.TransformPoint(adjustedOffset)
                : transform.position;
        }

        private void ResolveReferences()
        {
            if (playerController == null)
            {
                playerController = GetComponentInParent<FirstPersonController>();
            }

            if (followTarget == null && playerController != null)
            {
                followTarget = playerController.transform;
            }
        }

        private void OnValidate()
        {
            positionSmoothTime = Mathf.Max(positionSmoothTime, 0f);
        }
    }
}
