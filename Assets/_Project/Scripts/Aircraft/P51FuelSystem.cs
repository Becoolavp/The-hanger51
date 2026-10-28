using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    // Retain the original enum values so existing serialized filler/cap components remain
    // readable while scenes are migrated to the new single-main-tank layout. Only Fuselage
    // is a usable station after the conversion.
    public enum P51FuelTankStation
    {
        LeftWing = 0,
        RightWing = 1,
        Fuselage = 2
    }

    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class P51FuelSystem : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Header("References")]
        [SerializeField] private P51FlightController flightController;

        [Header("Single Main/Rear Tank - US Gallons")]
        [SerializeField, Min(0f)] private float mainTankCapacityGallons = 269f;
        [SerializeField, Min(0f)] private float startingFuelGallons = 85f;
        [SerializeField, Min(0f)] private float fuelGallons = 85f;
        [SerializeField] private bool resetToStartingFuelOnAwake = true;

        [Header("Merlin Fuel Burn")]
        [SerializeField, Min(1f)] private float idleGallonsPerHour = 28f;
        [SerializeField, Min(1f)] private float fullPowerGallonsPerHour = 88f;
        [SerializeField, Min(0.001f)] private float unusableFuelGallons = 0.05f;

        private FieldInfo engineRunningField;
        private FieldInfo throttleField;
        private bool reflectionReady;

        // Legacy properties intentionally report zero for the removed wing tanks so older
        // callers cannot accidentally treat them as usable fuel storage.
        public float LeftWingGallons => 0f;
        public float RightWingGallons => 0f;
        public float FuselageGallons => fuelGallons;
        public float TotalFuelGallons => fuelGallons;
        public float TotalCapacityGallons => mainTankCapacityGallons;
        public bool HasUsableFuel => fuelGallons > unusableFuelGallons;
        public bool EngineRunning => flightController != null && flightController.EngineRunning;
        public P51FlightController FlightController => flightController;

        private void Awake()
        {
            ResolveReferences();
            if (resetToStartingFuelOnAwake)
            {
                ResetToStartingFuel();
            }
            else
            {
                ClampFuel();
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            ClampFuel();
        }

        private void Update()
        {
            ResolveReferences();
            if (flightController == null)
            {
                return;
            }

            // P51FlightController processes T earlier in the frame. Cancel an attempted
            // start immediately when the main tank is dry before the timed Merlin startup
            // controller can begin its cranking sequence.
            if (flightController.EngineRunning && !HasUsableFuel)
            {
                ForceEngineOff("Start blocked: no usable fuel in the P-51 main tank.");
                return;
            }

            if (!flightController.EngineRunning)
            {
                return;
            }

            float gallonsPerHour = Mathf.Lerp(
                idleGallonsPerHour,
                fullPowerGallonsPerHour,
                Mathf.Clamp01(flightController.Throttle));
            float requestedBurn = gallonsPerHour / 3600f * Time.deltaTime;
            fuelGallons = Mathf.Max(0f, fuelGallons - requestedBurn);

            if (!HasUsableFuel)
            {
                fuelGallons = 0f;
                ForceEngineOff("Merlin stopped: main fuel tank exhausted.");
            }
        }

        public void Configure(
            P51FlightController configuredFlightController,
            float configuredStartingFuelGallons)
        {
            flightController = configuredFlightController;
            mainTankCapacityGallons = 269f;
            startingFuelGallons = Mathf.Clamp(
                configuredStartingFuelGallons,
                0f,
                mainTankCapacityGallons);
            resetToStartingFuelOnAwake = true;
            ResetToStartingFuel();
            ResolveEngineFields();
        }

        // Compatibility overload for the original three-tank editor setup. Existing callers
        // may still pass left/right/fuselage starting quantities; they are simply combined
        // into the single rear/main tank during migration.
        public void Configure(
            P51FlightController configuredFlightController,
            float configuredStartingLeftGallons,
            float configuredStartingRightGallons,
            float configuredStartingFuselageGallons)
        {
            Configure(
                configuredFlightController,
                Mathf.Max(0f, configuredStartingLeftGallons)
                    + Mathf.Max(0f, configuredStartingRightGallons)
                    + Mathf.Max(0f, configuredStartingFuselageGallons));
        }

        public void ResetToStartingFuel()
        {
            fuelGallons = Mathf.Clamp(startingFuelGallons, 0f, mainTankCapacityGallons);
        }

        public float GetTankGallons(P51FuelTankStation station)
        {
            return station == P51FuelTankStation.Fuselage ? fuelGallons : 0f;
        }

        public float GetTankCapacityGallons(P51FuelTankStation station)
        {
            return station == P51FuelTankStation.Fuselage ? mainTankCapacityGallons : 0f;
        }

        public float GetTankFreeSpaceGallons(P51FuelTankStation station)
        {
            return Mathf.Max(0f, GetTankCapacityGallons(station) - GetTankGallons(station));
        }

        public float AddFuel(P51FuelTankStation station, float requestedGallons)
        {
            if (station != P51FuelTankStation.Fuselage)
            {
                return 0f;
            }

            float amount = Mathf.Clamp(
                requestedGallons,
                0f,
                Mathf.Max(0f, mainTankCapacityGallons - fuelGallons));
            if (amount <= 0f)
            {
                return 0f;
            }

            fuelGallons += amount;
            ClampFuel();
            return amount;
        }

        public string GetTankDisplayName(P51FuelTankStation station)
        {
            return station == P51FuelTankStation.Fuselage
                ? "main rear fuel tank"
                : "removed wing tank";
        }

        private void ForceEngineOff(string message)
        {
            ResolveEngineFields();
            if (flightController == null || !reflectionReady)
            {
                return;
            }

            engineRunningField.SetValue(flightController, false);
            throttleField.SetValue(flightController, 0f);
            flightController.ShowCockpitMessage(message, 4f);
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
            ResolveEngineFields();
        }

        private void ResolveEngineFields()
        {
            if (reflectionReady || flightController == null)
            {
                return;
            }

            engineRunningField = typeof(P51FlightController).GetField("engineRunning", PrivateInstance);
            throttleField = typeof(P51FlightController).GetField("throttle", PrivateInstance);
            reflectionReady = engineRunningField != null && throttleField != null;
            if (!reflectionReady)
            {
                Debug.LogError("P-51 fuel system could not bind the flight-controller engine state fields.", this);
            }
        }

        private void ClampFuel()
        {
            mainTankCapacityGallons = Mathf.Max(0f, mainTankCapacityGallons);
            startingFuelGallons = Mathf.Clamp(startingFuelGallons, 0f, mainTankCapacityGallons);
            fuelGallons = Mathf.Clamp(fuelGallons, 0f, mainTankCapacityGallons);
        }

        private void OnValidate()
        {
            mainTankCapacityGallons = Mathf.Max(0f, mainTankCapacityGallons);
            idleGallonsPerHour = Mathf.Max(1f, idleGallonsPerHour);
            fullPowerGallonsPerHour = Mathf.Max(idleGallonsPerHour, fullPowerGallonsPerHour);
            unusableFuelGallons = Mathf.Max(0.001f, unusableFuelGallons);
            ClampFuel();
        }
    }
}
