using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class P51EmergencyExitSafety : MonoBehaviour
    {
        [SerializeField] private P51PilotSeat pilotSeat;
        [SerializeField] private P51PilotPlayerInteractor playerInteractor;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.5f)] private float rayStartHeight = 3.5f;
        [SerializeField, Min(1f)] private float rayDistance = 12f;
        [SerializeField, Min(0.5f)] private float standingClearance = 1.15f;

        private readonly RaycastHit[] hits = new RaycastHit[24];
        private bool wasOccupied;

        private void Awake()
        {
            ResolveReferences();
            wasOccupied = pilotSeat != null && pilotSeat.IsOccupied;
        }

        private void OnEnable()
        {
            ResolveReferences();
            wasOccupied = pilotSeat != null && pilotSeat.IsOccupied;
        }

        public void Configure(P51PilotSeat configuredSeat)
        {
            pilotSeat = configuredSeat;
            ResolveReferences();
            wasOccupied = pilotSeat != null && pilotSeat.IsOccupied;
        }

        private void LateUpdate()
        {
            ResolveReferences();
            bool occupied = pilotSeat != null && pilotSeat.IsOccupied;
            if (wasOccupied && !occupied)
            {
                MovePlayerToSafeExit();
            }
            wasOccupied = occupied;
        }

        private void MovePlayerToSafeExit()
        {
            if (playerInteractor == null)
            {
                return;
            }

            Vector3 candidate = pilotSeat != null && pilotSeat.ExitPoint != null
                ? pilotSeat.ExitPoint.position
                : transform.position + transform.right * 2.5f + Vector3.up;
            candidate += Vector3.up * 0.65f;

            Vector3 safePosition = candidate;
            Vector3 origin = candidate + Vector3.up * rayStartHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                hits,
                rayDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                Collider collider = hit.collider;
                if (collider == null
                    || collider.transform.IsChildOf(transform)
                    || hit.normal.y < 0.45f
                    || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                safePosition = hit.point + Vector3.up * standingClearance;
            }

            if (characterController == null)
            {
                characterController = playerInteractor.GetComponent<CharacterController>();
            }

            bool controllerWasEnabled = characterController != null
                && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            playerInteractor.transform.position = safePosition;

            if (characterController != null)
            {
                characterController.enabled = controllerWasEnabled;
            }
        }

        private void ResolveReferences()
        {
            if (pilotSeat == null)
            {
                pilotSeat = GetComponentInChildren<P51PilotSeat>(true);
            }
            if (playerInteractor == null)
            {
                playerInteractor = FindFirstObjectByType<P51PilotPlayerInteractor>();
            }
            if (characterController == null && playerInteractor != null)
            {
                characterController = playerInteractor.GetComponent<CharacterController>();
            }
        }

        private void OnValidate()
        {
            rayStartHeight = Mathf.Max(0.5f, rayStartHeight);
            rayDistance = Mathf.Max(rayStartHeight + 1f, rayDistance);
            standingClearance = Mathf.Max(0.5f, standingClearance);
            ResolveReferences();
        }
    }
}
