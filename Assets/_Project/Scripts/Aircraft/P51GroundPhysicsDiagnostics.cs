using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51GroundPhysicsDiagnostics : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51RaycastLandingGear landingGear;
        [SerializeField] private Rigidbody aircraftBody;

        private GUIStyle diagnosticStyle;

        public bool IsConfigured => flightController != null
            && landingGear != null
            && aircraftBody != null;

        public void Configure(
            P51FlightController configuredFlightController,
            P51RaycastLandingGear configuredLandingGear,
            Rigidbody configuredAircraftBody)
        {
            flightController = configuredFlightController;
            landingGear = configuredLandingGear;
            aircraftBody = configuredAircraftBody;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnGUI()
        {
            if (flightController == null
                || !flightController.PilotPresent
                || landingGear == null
                || aircraftBody == null)
            {
                return;
            }

            if (diagnosticStyle == null)
            {
                diagnosticStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    padding = new RectOffset(10, 10, 8, 8),
                    normal = { textColor = Color.white }
                };
            }

            Vector3 localVelocity = transform.InverseTransformDirection(
                aircraftBody.linearVelocity);
            string detectedState =
                $"{(landingGear.LeftMainGrounded ? "L" : "-")}"
                + $"{(landingGear.RightMainGrounded ? "R" : "-")}"
                + $"{(landingGear.TailwheelGrounded ? "T" : "-")}";
            string loadedState =
                $"{(landingGear.LeftMainLoaded ? "L" : "-")}"
                + $"{(landingGear.RightMainLoaded ? "R" : "-")}"
                + $"{(landingGear.TailwheelLoaded ? "T" : "-")}";
            string diagnostics =
                $"GROUND PHYSICS\n"
                + $"Detected: {landingGear.GroundedWheelCount}/3 ({detectedState})\n"
                + $"Loaded: {landingGear.LoadedWheelCount}/3 ({loadedState})\n"
                + $"Forward speed: {localVelocity.z:F1} m/s\n"
                + $"Vertical speed: {localVelocity.y:F1} m/s\n"
                + $"Body dynamic: {!aircraftBody.isKinematic}\n"
                + $"Engine running: {flightController.EngineRunning}\n"
                + $"Throttle command: {flightController.Throttle * 100f:F0}%";

            GUI.Box(
                new Rect(18f, 232f, 300f, 168f),
                diagnostics,
                diagnosticStyle);
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }

            if (landingGear == null)
            {
                landingGear = GetComponent<P51RaycastLandingGear>();
            }

            if (aircraftBody == null)
            {
                aircraftBody = GetComponent<Rigidbody>();
            }
        }
    }
}