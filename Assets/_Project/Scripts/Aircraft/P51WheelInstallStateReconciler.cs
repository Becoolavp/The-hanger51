using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(270)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearInventoryBridge))]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    public sealed class P51WheelInstallStateReconciler : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private P51LandingGearInventoryBridge bridge;
        private P51LandingGearMaintenanceController maintenance;
        private FieldInfo rimInstalledField;
        private FieldInfo tireInstalledField;
        private MethodInfo refreshMethod;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeRepair()
        {
            P51LandingGearInventoryBridge[] bridges = FindObjectsByType<P51LandingGearInventoryBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < bridges.Length; index++)
            {
                P51LandingGearInventoryBridge candidate = bridges[index];
                if (candidate != null
                    && candidate.GetComponent<P51WheelInstallStateReconciler>() == null)
                {
                    candidate.gameObject.AddComponent<P51WheelInstallStateReconciler>();
                }
            }
        }

        private void Awake()
        {
            ResolveBindings();
        }

        private void OnEnable()
        {
            ResolveBindings();
        }

        private void Update()
        {
            ResolveBindings();
            P51LooseWheelAssembly carried = P51LooseWheelAssembly.CurrentCarried;
            if (carried == null
                || !carried.IsComplete
                || bridge == null
                || maintenance == null
                || rimInstalledField == null
                || tireInstalledField == null)
            {
                return;
            }

            bool[] rims = rimInstalledField.GetValue(bridge) as bool[];
            bool[] tires = tireInstalledField.GetValue(maintenance) as bool[];
            if (rims == null || tires == null || rims.Length < 3 || tires.Length < 3)
            {
                return;
            }

            bool repaired = false;
            for (int wheelIndex = 0; wheelIndex < 3; wheelIndex++)
            {
                if (!maintenance.IsGearInstalled(wheelIndex)
                    || !carried.CanInstallOn(wheelIndex))
                {
                    continue;
                }

                // A complete wheel being physically carried back to a compatible station proves
                // that any one-sided old state here is stale. Do not touch an actually complete
                // installed wheel; only normalize impossible rim/tire disagreement.
                if (rims[wheelIndex] == tires[wheelIndex])
                {
                    continue;
                }

                rims[wheelIndex] = false;
                tires[wheelIndex] = false;
                repaired = true;
            }

            if (repaired && refreshMethod != null)
            {
                refreshMethod.Invoke(bridge, null);
            }
        }

        private void ResolveBindings()
        {
            if (bridge == null)
            {
                bridge = GetComponent<P51LandingGearInventoryBridge>();
            }
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }

            if (rimInstalledField == null)
            {
                rimInstalledField = typeof(P51LandingGearInventoryBridge)
                    .GetField("rimInstalled", PrivateInstance);
            }
            if (tireInstalledField == null)
            {
                tireInstalledField = typeof(P51LandingGearMaintenanceController)
                    .GetField("tireInstalled", PrivateInstance);
            }
            if (refreshMethod == null)
            {
                refreshMethod = typeof(P51LandingGearInventoryBridge)
                    .GetMethod("RefreshMaintenanceVisualsAndPhysics", PrivateInstance);
            }
        }
    }
}
