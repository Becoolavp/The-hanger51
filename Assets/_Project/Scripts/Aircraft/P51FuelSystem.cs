using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
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

        [Header("Tank Capacity - US Gallons")]
        [SerializeField, Min(0f)] private float leftWingCapacityGallons = 92f;
        [SerializeField, Min(0f)] private float rightWingCapacityGallons = 92f;
        [SerializeField, Min(0f)] private float fuselageCapacityGallons = 85f;

        [Header("Starting Fuel - US Gallons")]
        [SerializeField, Min(0f)] private float startingLeftWingGallons = 35f;
        [SerializeField, Min(0f)] private float startingRightWingGallons = 35f;
        [SerializeField, Min(0f)] private float startingFuselageGallons = 15f;
        [SerializeField] private bool resetToStartingFuelOnAwake = true;

        [Header("Current Fuel - US Gallons")]
        [SerializeField, Min(0f)] private float leftWingGallons = 35f;
        [SerializeField, Min(0f)] private float rightWingGallons = 35f;
        [SerializeField, Min(0f)] private float fuselageGallons = 15f;

        [Header("Merlin Fuel Burn")]
        [SerializeField, Min(1f)] private float idleGallonsPerHour = 28f;
        [SerializeField, Min(1f)] private float fullPowerGallonsPerHour = 88f;
        [SerializeField, Min(0.001f)] private float unusableFuelGallons = 0.05f;

        private FieldInfo engineRunningField;
        private FieldInfo throttleField;
        private bool reflectionReady;

        public float LeftWingGallons => leftWingGallons;
        public float RightWingGallons => rightWingGallons;
        public float FuselageGallons => fuselageGallons;
        public float TotalFuelGallons => leftWingGallons + rightWingGallons + fuselageGallons;
        public float TotalCapacityGallons => leftWingCapacityGallons + rightWingCapacityGallons + fuselageCapacityGallons;
        public bool HasUsableFuel => TotalFuelGallons > unusableFuelGallons;
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

            // P51FlightController handles the T key earlier in the frame. If it tried to
            // start with empty tanks, immediately cancel that start before the timed Merlin
            // lifecycle controller can begin cranking.
            if (flightController.EngineRunning && !HasUsableFuel)
            {
                ForceEngineOff("Start blocked: no usable fuel in the P-51 tanks.");
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
            ConsumeFuel(requestedBurn);

            if (!HasUsableFuel)
            {
                leftWingGallons = 0f;
                rightWingGallons = 0f;
                fuselageGallons = 0f;
                ForceEngineOff("Merlin stopped: fuel exhausted.");
            }
        }

        public void Configure(
            P51FlightController configuredFlightController,
            float configuredStartingLeftGallons,
            float configuredStartingRightGallons,
            float configuredStartingFuselageGallons)
        {
            flightController = configuredFlightController;
            startingLeftWingGallons = Mathf.Clamp(configuredStartingLeftGallons, 0f, leftWingCapacityGallons);
            startingRightWingGallons = Mathf.Clamp(configuredStartingRightGallons, 0f, rightWingCapacityGallons);
            startingFuselageGallons = Mathf.Clamp(configuredStartingFuselageGallons, 0f, fuselageCapacityGallons);
            resetToStartingFuelOnAwake = true;
            ResetToStartingFuel();
            ResolveEngineFields();
        }

        public void ResetToStartingFuel()
        {
            leftWingGallons = Mathf.Clamp(startingLeftWingGallons, 0f, leftWingCapacityGallons);
            rightWingGallons = Mathf.Clamp(startingRightWingGallons, 0f, rightWingCapacityGallons);
            fuselageGallons = Mathf.Clamp(startingFuselageGallons, 0f, fuselageCapacityGallons);
        }

        public float GetTankGallons(P51FuelTankStation station)
        {
            switch (station)
            {
                case P51FuelTankStation.LeftWing: return leftWingGallons;
                case P51FuelTankStation.RightWing: return rightWingGallons;
                default: return fuselageGallons;
            }
        }

        public float GetTankCapacityGallons(P51FuelTankStation station)
        {
            switch (station)
            {
                case P51FuelTankStation.LeftWing: return leftWingCapacityGallons;
                case P51FuelTankStation.RightWing: return rightWingCapacityGallons;
                default: return fuselageCapacityGallons;
            }
        }

        public float GetTankFreeSpaceGallons(P51FuelTankStation station)
        {
            return Mathf.Max(0f, GetTankCapacityGallons(station) - GetTankGallons(station));
        }

        public float AddFuel(P51FuelTankStation station, float requestedGallons)
        {
            float amount = Mathf.Clamp(requestedGallons, 0f, GetTankFreeSpaceGallons(station));
            if (amount <= 0f)
            {
                return 0f;
            }

            switch (station)
            {
                case P51FuelTankStation.LeftWing:
                    leftWingGallons += amount;
                    break;
                case P51FuelTankStation.RightWing:
                    rightWingGallons += amount;
                    break;
                default:
                    fuselageGallons += amount;
                    break;
            }

            ClampFuel();
            return amount;
        }

        public string GetTankDisplayName(P51FuelTankStation station)
        {
            switch (station)
            {
                case P51FuelTankStation.LeftWing: return "left wing tank";
                case P51FuelTankStation.RightWing: return "right wing tank";
                default: return "fuselage tank";
            }
        }

        private void ConsumeFuel(float requestedGallons)
        {
            float remaining = Mathf.Max(0f, requestedGallons);
            if (remaining <= 0f)
            {
                return;
            }

            // Burn the fuselage tank first, then keep the wing tanks roughly balanced.
            float fromFuselage = Mathf.Min(fuselageGallons, remaining);
            fuselageGallons -= fromFuselage;
            remaining -= fromFuselage;

            while (remaining > 0.000001f && (leftWingGallons > 0f || rightWingGallons > 0f))
            {
                bool useLeft = leftWingGallons >= rightWingGallons;
                float available = useLeft ? leftWingGallons : rightWingGallons;
                float draw = Mathf.Min(available, remaining);
                if (useLeft)
                {
                    leftWingGallons -= draw;
                }
                else
                {
                    rightWingGallons -= draw;
                }
                remaining -= draw;
            }

            ClampFuel();
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
            leftWingGallons = Mathf.Clamp(leftWingGallons, 0f, leftWingCapacityGallons);
            rightWingGallons = Mathf.Clamp(rightWingGallons, 0f, rightWingCapacityGallons);
            fuselageGallons = Mathf.Clamp(fuselageGallons, 0f, fuselageCapacityGallons);
        }

        private void OnValidate()
        {
            leftWingCapacityGallons = Mathf.Max(0f, leftWingCapacityGallons);
            rightWingCapacityGallons = Mathf.Max(0f, rightWingCapacityGallons);
            fuselageCapacityGallons = Mathf.Max(0f, fuselageCapacityGallons);
            idleGallonsPerHour = Mathf.Max(1f, idleGallonsPerHour);
            fullPowerGallonsPerHour = Mathf.Max(idleGallonsPerHour, fullPowerGallonsPerHour);
            unusableFuelGallons = Mathf.Max(0.001f, unusableFuelGallons);
            ClampFuel();
        }
    }
}
