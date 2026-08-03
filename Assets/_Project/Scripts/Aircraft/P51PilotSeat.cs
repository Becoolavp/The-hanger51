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

            if (flightController != null
                && !flightController.CanExitCockpit(out reason))
            {
                return false;
            }

            occupied = false;
            flightController?.SetPilotPresent(false);
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
