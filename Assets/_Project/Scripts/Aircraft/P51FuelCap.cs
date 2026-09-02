using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51FuelCap : MonoBehaviour
    {
        [SerializeField] private P51FuelSystem fuelSystem;
        [SerializeField] private P51FuelTankStation tankStation;
        [SerializeField] private Transform capVisual;
        [SerializeField] private Vector3 installedLocalPosition;
        [SerializeField] private Vector3 installedLocalEuler;
        [SerializeField] private Vector3 removedLocalPosition;
        [SerializeField] private Vector3 removedLocalEuler;
        [SerializeField] private bool removed;

        public bool IsRemoved => removed;
        public P51FuelTankStation TankStation => tankStation;
        public P51FuelSystem FuelSystem => fuelSystem;

        public void Configure(
            P51FuelSystem configuredFuelSystem,
            P51FuelTankStation configuredStation,
            Transform configuredCapVisual,
            Vector3 configuredInstalledPosition,
            Vector3 configuredInstalledEuler,
            Vector3 configuredRemovedPosition,
            Vector3 configuredRemovedEuler)
        {
            fuelSystem = configuredFuelSystem;
            tankStation = configuredStation;
            capVisual = configuredCapVisual != null ? configuredCapVisual : transform;
            installedLocalPosition = configuredInstalledPosition;
            installedLocalEuler = configuredInstalledEuler;
            removedLocalPosition = configuredRemovedPosition;
            removedLocalEuler = configuredRemovedEuler;
            removed = false;
            RefreshVisual();
        }

        public bool TryToggle(out string resultMessage)
        {
            resultMessage = string.Empty;
            if (fuelSystem == null)
            {
                resultMessage = "This fuel cap is not connected to a fuel system.";
                return false;
            }

            if (fuelSystem.EngineRunning)
            {
                resultMessage = "Stop the Merlin before opening a fuel tank.";
                return false;
            }

            removed = !removed;
            RefreshVisual();
            string tankName = fuelSystem.GetTankDisplayName(tankStation);
            resultMessage = removed
                ? $"Removed the {tankName} fuel cap."
                : $"Reinstalled the {tankName} fuel cap.";
            return true;
        }

        public void SetInstalled()
        {
            removed = false;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (capVisual == null)
            {
                capVisual = transform;
            }

            capVisual.localPosition = removed ? removedLocalPosition : installedLocalPosition;
            capVisual.localRotation = Quaternion.Euler(removed ? removedLocalEuler : installedLocalEuler);
        }
    }
}
