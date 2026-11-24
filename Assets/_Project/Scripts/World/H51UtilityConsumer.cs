using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51UtilityConsumer : MonoBehaviour
    {
        [SerializeField] bool criticalFacility;
        [SerializeField] bool hasBackupPower;
        [SerializeField] bool requiresPoweredWater = true;
        [SerializeField] Light[] managedLights = new Light[0];

        bool gridElectricityAvailable = true;
        bool gridWaterAvailable = true;
        bool outageApplied;
        bool[] lightStatesBeforeOutage = new bool[0];

        public bool CriticalFacility => criticalFacility;
        public bool HasElectricity =>
            gridElectricityAvailable || (criticalFacility && hasBackupPower);
        public bool HasWater =>
            !requiresPoweredWater ||
            gridWaterAvailable ||
            (criticalFacility && hasBackupPower);

        public void Configure(
            bool isCriticalFacility,
            bool backupPower,
            bool waterNeedsPower,
            Light[] lights)
        {
            criticalFacility = isCriticalFacility;
            hasBackupPower = backupPower;
            requiresPoweredWater = waterNeedsPower;
            managedLights = lights ?? new Light[0];
        }

        public void ApplyUtilityState(bool gridPower, bool gridWater)
        {
            gridElectricityAvailable = gridPower;
            gridWaterAvailable = gridWater;

            if (!HasElectricity)
            {
                if (!outageApplied)
                {
                    lightStatesBeforeOutage = new bool[managedLights.Length];
                    for (int i = 0; i < managedLights.Length; i++)
                        lightStatesBeforeOutage[i] = managedLights[i] && managedLights[i].enabled;
                    outageApplied = true;
                }

                for (int i = 0; i < managedLights.Length; i++)
                    if (managedLights[i])
                        managedLights[i].enabled = false;
                return;
            }

            if (!outageApplied) return;

            for (int i = 0; i < managedLights.Length; i++)
            {
                if (!managedLights[i]) continue;
                bool restore =
                    i < lightStatesBeforeOutage.Length &&
                    lightStatesBeforeOutage[i];
                managedLights[i].enabled = restore;
            }
            outageApplied = false;
        }
    }
}
