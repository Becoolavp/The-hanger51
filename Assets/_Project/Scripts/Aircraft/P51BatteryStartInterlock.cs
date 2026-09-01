using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class P51BatteryStartInterlock : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51AftEquipmentBay aftEquipmentBay;

        private bool wasRunning;
        private FieldInfo engineRunningField;
        private FieldInfo throttleField;

        private void Awake()
        {
            ResolveReferences();
            CacheFields();
            wasRunning = flightController != null && flightController.EngineRunning;
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheFields();
            wasRunning = flightController != null && flightController.EngineRunning;
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (flightController == null)
            {
                return;
            }

            bool runningNow = flightController.EngineRunning;
            if (!wasRunning && runningNow)
            {
                if (aftEquipmentBay == null)
                {
                    RejectStart("Start blocked: the aircraft electrical/battery system is not configured.");
                    runningNow = false;
                }
                else if (!aftEquipmentBay.TrySupplyStarterPower(out string batteryMessage))
                {
                    RejectStart(batteryMessage);
                    runningNow = false;
                }
                else
                {
                    flightController.ShowCockpitMessage(
                        $"Merlin started. {batteryMessage}",
                        3.5f);
                }
            }

            // Deliberately do nothing if the battery is removed or later falls below threshold
            // while the engine is already running. The user's requirement is a start interlock,
            // not an in-flight engine kill switch.
            wasRunning = runningNow;
        }

        private void RejectStart(string reason)
        {
            CacheFields();
            if (flightController == null)
            {
                return;
            }

            engineRunningField?.SetValue(flightController, false);
            throttleField?.SetValue(flightController, 0f);
            flightController.ShowCockpitMessage(reason, 4.5f);
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
            if (aftEquipmentBay == null)
            {
                aftEquipmentBay = GetComponentInChildren<P51AftEquipmentBay>(true);
            }
        }

        private void CacheFields()
        {
            if (engineRunningField == null)
            {
                engineRunningField = typeof(P51FlightController).GetField(
                    "engineRunning",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (throttleField == null)
            {
                throttleField = typeof(P51FlightController).GetField(
                    "throttle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
    }
}
