using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51FuelFiller : MonoBehaviour
    {
        [SerializeField] private P51FuelSystem fuelSystem;
        [SerializeField] private P51FuelCap fuelCap;
        [SerializeField] private P51FuelTankStation tankStation;
        [SerializeField, Min(0.1f)] private float pourRateGallonsPerSecond = 1.35f;

        public P51FuelTankStation TankStation => tankStation;
        public P51FuelSystem FuelSystem => fuelSystem;
        public P51FuelCap FuelCap => fuelCap;
        public bool IsOpen => fuelCap != null && fuelCap.IsRemoved;

        public void Configure(
            P51FuelSystem configuredFuelSystem,
            P51FuelCap configuredFuelCap,
            P51FuelTankStation configuredStation,
            float configuredPourRateGallonsPerSecond = 1.35f)
        {
            fuelSystem = configuredFuelSystem;
            fuelCap = configuredFuelCap;
            tankStation = configuredStation;
            pourRateGallonsPerSecond = Mathf.Max(0.1f, configuredPourRateGallonsPerSecond);
        }

        public bool TryPourFromCan(P51FuelCan can, float deltaTime, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (fuelSystem == null || fuelCap == null)
            {
                resultMessage = "This filler is not connected to the P-51 fuel system.";
                return false;
            }
            if (fuelSystem.EngineRunning)
            {
                resultMessage = "Stop the Merlin before refueling.";
                return false;
            }
            if (!fuelCap.IsRemoved)
            {
                resultMessage = "Remove the fuel cap first.";
                return false;
            }
            if (can == null)
            {
                resultMessage = "Pick up a fuel can first.";
                return false;
            }
            if (!can.HasFuel)
            {
                resultMessage = "The fuel can is empty.";
                return false;
            }

            float freeSpace = fuelSystem.GetTankFreeSpaceGallons(tankStation);
            if (freeSpace <= 0.001f)
            {
                resultMessage = $"The {fuelSystem.GetTankDisplayName(tankStation)} is full.";
                return false;
            }

            float requested = Mathf.Min(
                pourRateGallonsPerSecond * Mathf.Max(0f, deltaTime),
                freeSpace);
            float drawn = can.DrawFuel(requested);
            float added = fuelSystem.AddFuel(tankStation, drawn);
            if (added + 0.0001f < drawn)
            {
                // This should only occur if another source filled the tank during this frame.
                can.Configure(can.CapacityGallons, can.GallonsRemaining + (drawn - added));
            }

            resultMessage = $"{fuelSystem.GetTankDisplayName(tankStation)}: {fuelSystem.GetTankGallons(tankStation):F1}/{fuelSystem.GetTankCapacityGallons(tankStation):F0} gal | Can: {can.GallonsRemaining:F1}/{can.CapacityGallons:F0} gal";
            return added > 0f;
        }

        private void OnValidate()
        {
            pourRateGallonsPerSecond = Mathf.Max(0.1f, pourRateGallonsPerSecond);
        }
    }
}
