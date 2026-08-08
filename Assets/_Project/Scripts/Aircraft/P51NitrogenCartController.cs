using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51NitrogenCartController : MonoBehaviour
    {
        [SerializeField, Range(0f, 80f)] private float regulatorPsi = 20f;
        [SerializeField, Min(1f)] private float regulatorChangePerSecond = 8f;
        [SerializeField, Min(1f)] private float maximumHoseDistance = 9f;
        [SerializeField] private Transform hoseOrigin;
        [SerializeField] private LineRenderer hoseLine;

        private P51LandingGearMaintenanceController connectedController;
        private int connectedWheelIndex = -1;

        public float RegulatorPsi => regulatorPsi;
        public bool IsConnected => connectedController != null && connectedWheelIndex >= 0;
        public string InteractionText => IsConnected
            ? $"Nitrogen cart: {regulatorPsi:F0} PSI setpoint | Q/Z adjust | Hold F service | N disconnect"
            : $"Nitrogen cart: {regulatorPsi:F0} PSI setpoint | Q/Z adjust | Aim at a tire valve and press N";

        private void Awake()
        {
            UpdateHoseVisual();
        }

        private void LateUpdate()
        {
            if (IsConnected)
            {
                Transform valve = connectedController.GetValveTarget(connectedWheelIndex);
                if (valve == null
                    || Vector3.Distance(transform.position, valve.position) > maximumHoseDistance)
                {
                    Disconnect();
                }
            }
            UpdateHoseVisual();
        }

        public void Configure(
            Transform configuredHoseOrigin,
            LineRenderer configuredHoseLine,
            float configuredMaximumHoseDistance)
        {
            hoseOrigin = configuredHoseOrigin;
            hoseLine = configuredHoseLine;
            maximumHoseDistance = Mathf.Max(1f, configuredMaximumHoseDistance);
            regulatorPsi = 20f;
            Disconnect();
        }

        public void AdjustRegulator(float direction, float deltaTime)
        {
            regulatorPsi = Mathf.Clamp(
                regulatorPsi
                + Mathf.Clamp(direction, -1f, 1f)
                * regulatorChangePerSecond
                * Mathf.Max(0f, deltaTime),
                0f,
                80f);
        }

        public bool TryConnect(
            P51LandingGearMaintenanceController controller,
            int wheelIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (controller == null)
            {
                resultMessage = "No tire service target was selected.";
                return false;
            }
            if (!controller.IsGearInstalled(wheelIndex)
                || !controller.IsTireInstalled(wheelIndex))
            {
                resultMessage = "Install the landing gear and tire before connecting nitrogen.";
                return false;
            }
            if (controller.IsTireFailed(wheelIndex))
            {
                resultMessage = "That tire is destroyed and must be replaced before pressure service.";
                return false;
            }

            Transform valve = controller.GetValveTarget(wheelIndex);
            if (valve == null)
            {
                resultMessage = "That tire valve is not configured.";
                return false;
            }
            float distance = Vector3.Distance(transform.position, valve.position);
            if (distance > maximumHoseDistance)
            {
                resultMessage = $"Move the aircraft or nitrogen cart closer; the valve is {distance:F1} m away.";
                return false;
            }

            connectedController = controller;
            connectedWheelIndex = wheelIndex;
            UpdateHoseVisual();
            float correctPressure = controller.GetProperPressure(wheelIndex);
            resultMessage = $"Connected nitrogen hose to the {controller.GetWheelName(wheelIndex)} tire. Current cart setpoint is {regulatorPsi:F0} PSI; this tire requires {correctPressure:F0} PSI. Aim at the cart, use Q/Z to set the regulator, then hold F to service.";
            return true;
        }

        public void Disconnect()
        {
            connectedController = null;
            connectedWheelIndex = -1;
            UpdateHoseVisual();
        }

        public bool ServiceConnectedTire(float deltaTime, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsConnected)
            {
                resultMessage = "Connect the nitrogen hose to a tire valve first.";
                return false;
            }

            bool serviced = connectedController.ServicePressureToward(
                connectedWheelIndex,
                regulatorPsi,
                deltaTime,
                out resultMessage);
            if (connectedController.IsTireFailed(connectedWheelIndex))
            {
                Disconnect();
            }
            return serviced;
        }

        private void UpdateHoseVisual()
        {
            if (hoseLine == null)
            {
                return;
            }

            hoseLine.enabled = IsConnected;
            if (!IsConnected)
            {
                return;
            }

            Transform valve = connectedController.GetValveTarget(connectedWheelIndex);
            if (valve == null)
            {
                hoseLine.enabled = false;
                return;
            }

            Vector3 start = hoseOrigin != null ? hoseOrigin.position : transform.position;
            Vector3 end = valve.position;
            Vector3 middle = Vector3.Lerp(start, end, 0.5f) + Vector3.down * 0.35f;
            hoseLine.positionCount = 3;
            hoseLine.SetPosition(0, start);
            hoseLine.SetPosition(1, middle);
            hoseLine.SetPosition(2, end);
        }

        private void OnValidate()
        {
            regulatorPsi = Mathf.Clamp(regulatorPsi, 0f, 80f);
            regulatorChangePerSecond = Mathf.Max(1f, regulatorChangePerSecond);
            maximumHoseDistance = Mathf.Max(1f, maximumHoseDistance);
        }
    }
}
