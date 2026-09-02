using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(65)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    [RequireComponent(typeof(P51RaycastLandingGear))]
    public sealed class P51LandingGearGroundRetractionGuard : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private P51LandingGearMaintenanceController maintenance;
        private P51RaycastLandingGear physicsGear;
        private P51FlightController flightController;
        private FieldInfo gearCommandDownField;
        private bool warningLatched;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            if (maintenance == null
                || physicsGear == null
                || gearCommandDownField == null)
            {
                return;
            }

            bool commandDown = maintenance.GearCommandDown;
            if (!commandDown && physicsGear.AnyWheelLoaded)
            {
                gearCommandDownField.SetValue(maintenance, true);
                if (!warningLatched && flightController != null && flightController.PilotPresent)
                {
                    warningLatched = true;
                    flightController.ShowCockpitMessage(
                        "Landing gear retraction blocked — weight is still on the wheels.",
                        2.8f);
                }
            }
            else if (commandDown || !physicsGear.AnyWheelLoaded)
            {
                warningLatched = false;
            }
        }

        private void ResolveReferences()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }
            if (physicsGear == null)
            {
                physicsGear = GetComponent<P51RaycastLandingGear>();
            }
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
            if (gearCommandDownField == null)
            {
                gearCommandDownField = typeof(P51LandingGearMaintenanceController)
                    .GetField("gearCommandDown", PrivateInstance);
            }
        }
    }
}
