using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51CoolantCap : MonoBehaviour
    {
        [SerializeField] private P51RadiatorCoolingSystem coolingSystem;
        [SerializeField] private Transform capVisual;
        [SerializeField] private Vector3 installedLocalPosition;
        [SerializeField] private Vector3 installedLocalEuler;
        [SerializeField] private Vector3 removedLocalPosition;
        [SerializeField] private Vector3 removedLocalEuler;
        [SerializeField] private bool removed;

        public bool IsRemoved => removed;
        public P51RadiatorCoolingSystem CoolingSystem => coolingSystem;

        public void Configure(
            P51RadiatorCoolingSystem configuredCoolingSystem,
            Transform configuredCapVisual,
            Vector3 configuredInstalledPosition,
            Vector3 configuredInstalledEuler,
            Vector3 configuredRemovedPosition,
            Vector3 configuredRemovedEuler)
        {
            coolingSystem = configuredCoolingSystem;
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
            if (coolingSystem == null)
            {
                resultMessage = "This coolant cap is not connected to the radiator.";
                return false;
            }

            if (coolingSystem.EngineRunning)
            {
                resultMessage = "Stop the Merlin before opening the coolant system.";
                return false;
            }

            if (!removed && coolingSystem.CoolantTemperatureC > 80f)
            {
                resultMessage = $"Coolant is too hot to open safely ({coolingSystem.CoolantTemperatureC:F0} C).";
                return false;
            }

            removed = !removed;
            RefreshVisual();
            resultMessage = removed
                ? $"Removed radiator coolant cap. {coolingSystem.GetServiceReading()}"
                : "Reinstalled radiator coolant cap.";
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
