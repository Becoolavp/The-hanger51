using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [RequireComponent(typeof(EngineHoistController))]
    public sealed class EngineHoistMovementCollisionGuard : MonoBehaviour
    {
        [SerializeField] private Collider handleInteractionCollider;

        private EngineHoistController hoistController;

        public Collider HandleInteractionCollider => handleInteractionCollider;
        public bool IsConfigured => hoistController != null && handleInteractionCollider != null;

        private void Awake()
        {
            ResolveReferences();
            ApplyColliderState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyColliderState();
        }

        private void LateUpdate()
        {
            ApplyColliderState();
        }

        public void Configure(Collider configuredHandleInteractionCollider)
        {
            handleInteractionCollider = configuredHandleInteractionCollider;
            ResolveReferences();
            ApplyColliderState();
        }

        private void ResolveReferences()
        {
            if (hoistController == null)
            {
                hoistController = GetComponent<EngineHoistController>();
            }

            if (handleInteractionCollider != null)
            {
                return;
            }

            Transform handleTarget = FindDescendant(transform, "Hoist Interaction Handles");
            if (handleTarget != null)
            {
                handleInteractionCollider = handleTarget.GetComponent<Collider>();
            }
        }

        private void ApplyColliderState()
        {
            ResolveReferences();
            if (handleInteractionCollider == null)
            {
                return;
            }

            // The handle collider is needed to initially select the hoist, but
            // while pushing it the collider sits directly in front of the
            // Player capsule and can prevent forward movement. Active control
            // is tracked globally, so releasing the hoist does not require the
            // collider to remain enabled during movement.
            bool shouldBeEnabled = hoistController == null
                || !hoistController.IsPlayerControlling;

            if (handleInteractionCollider.enabled != shouldBeEnabled)
            {
                handleInteractionCollider.enabled = shouldBeEnabled;
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }

            return null;
        }

        private void OnDisable()
        {
            // Always leave the interaction target usable when the component or
            // GameObject is disabled in the Editor or during scene changes.
            if (handleInteractionCollider != null)
            {
                handleInteractionCollider.enabled = true;
            }
        }
    }
}
