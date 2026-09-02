using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51PilotSeat : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51AircraftServiceController serviceController;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private bool occupied;

        public P51FlightController FlightController => flightController;
        public Transform CameraAnchor => cameraAnchor;
        public Transform ExitPoint => exitPoint;
        public Collider InteractionCollider => interactionCollider;
        public bool IsOccupied => occupied;
        public string InteractionText => occupied
            ? string.Empty
            : "E: enter P-51 cockpit";

        private void Awake()
        {
            ResolveReferences();
            occupied = false;
        }

        public void Configure(
            P51FlightController configuredFlightController,
            P51AircraftServiceController configuredServiceController,
            Transform configuredCameraAnchor,
            Transform configuredExitPoint,
            Collider configuredInteractionCollider)
        {
            flightController = configuredFlightController;
            serviceController = configuredServiceController;
            cameraAnchor = configuredCameraAnchor;
            exitPoint = configuredExitPoint;
            interactionCollider = configuredInteractionCollider;
            occupied = false;
            ResolveReferences();
        }

        public bool CanEnter(out string reason)
        {
            reason = string.Empty;
            ResolveReferences();

            if (occupied)
            {
                reason = "The P-51 cockpit is already occupied.";
                return false;
            }

            if (flightController == null || cameraAnchor == null || exitPoint == null)
            {
                reason = "The P-51 cockpit is not configured correctly.";
                return false;
            }

            if (P51TowBarController.IsAircraftTowBarAttached(flightController))
            {
                reason = "Disconnect the tailwheel tow bar before entering the cockpit.";
                return false;
            }

            if (serviceController != null && serviceController.IsCowlingCarried)
            {
                reason = "Place or reinstall the cowling before entering the cockpit.";
                return false;
            }

            if (flightController.GroundSpeedMetersPerSecond > 3.5f)
            {
                reason = "Wait for the aircraft to stop before entering the cockpit.";
                return false;
            }

            return true;
        }

        public bool TryOccupy(out string reason)
        {
            if (!CanEnter(out reason))
            {
                return false;
            }

            occupied = true;
            flightController.SetPilotPresent(true);
            return true;
        }

        public bool TryRelease(out string reason)
        {
            reason = string.Empty;
            if (!occupied)
            {
                return true;
            }

            ResolveReferences();
            Rigidbody body = flightController != null
                ? flightController.AircraftBody
                : null;

            Vector3 preservedLinearVelocity = body != null
                ? body.linearVelocity
                : Vector3.zero;
            Vector3 preservedAngularVelocity = body != null
                ? body.angularVelocity
                : Vector3.zero;
            bool preserveDynamicMotion = body != null
                && (!flightController.IsGrounded
                    || flightController.GroundSpeedMetersPerSecond > 0.75f
                    || Mathf.Abs(preservedLinearVelocity.y) > 0.75f
                    || preservedAngularVelocity.sqrMagnitude > 0.16f);

            occupied = false;
            flightController?.SetPilotPresent(false);

            // SetPilotPresent(false) normally parks a stopped aircraft. During
            // an emergency exit, restore the current motion so the airplane
            // does not freeze in mid-air or snap to a stop while moving.
            if (preserveDynamicMotion && body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = preservedLinearVelocity;
                body.angularVelocity = preservedAngularVelocity;
                body.WakeUp();
            }

            return true;
        }

        public void ForceRelease()
        {
            occupied = false;
            flightController?.SetPilotPresent(false);
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponentInParent<P51FlightController>();
            }

            if (serviceController == null)
            {
                serviceController = GetComponentInParent<P51AircraftServiceController>();
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }
    }
}
