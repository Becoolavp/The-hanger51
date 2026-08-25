using System.Collections.Generic;
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

        private static readonly string[] LegacyGearRootNames =
        {
            "Left Main Landing Gear",
            "Right Main Landing Gear",
            "Tailwheel Assembly"
        };

        private P51LandingGearInventoryBridge bridge;
        private P51LandingGearMaintenanceController maintenance;
        private FieldInfo rimInstalledField;
        private FieldInfo tireInstalledField;
        private MethodInfo refreshMethod;
        private Renderer[] legacyGearRenderers = new Renderer[0];

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
            CacheLegacyLandingGearRenderers();
            ForceLegacyLandingGearHidden();
        }

        private void OnEnable()
        {
            ResolveBindings();
            CacheLegacyLandingGearRenderers();
            ForceLegacyLandingGearHidden();
        }

        private void Update()
        {
            ResolveBindings();
            ForceLegacyLandingGearHidden();

            if (bridge == null
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
                if (!maintenance.IsGearInstalled(wheelIndex))
                {
                    continue;
                }

                // The current maintenance model never leaves only a tire or only a rim on an
                // installed strut. The complete wheel comes off as one assembly, tire/rim work is
                // performed off-aircraft, and the complete wheel goes back on as one assembly.
                // Therefore rim/tire disagreement is always stale state from the older service
                // workflow. Normalize that impossible state to an empty axle immediately.
                //
                // This fixes all three symptoms caused by the disagreement:
                // - "wheel state is incomplete" at an otherwise ready station,
                // - a tire visual remaining behind after complete-wheel removal,
                // - the carried-wheel install highlight refusing to appear.
                if (rims[wheelIndex] == tires[wheelIndex])
                {
                    continue;
                }

                rims[wheelIndex] = false;
                tires[wheelIndex] = false;
                repaired = true;
            }

            if (repaired)
            {
                // Re-assign the arrays explicitly as well as mutating them in place. That keeps the
                // repair deterministic even if Unity serialization/reflection replaces one of the
                // backing arrays during another component's refresh pass.
                rimInstalledField.SetValue(bridge, rims);
                tireInstalledField.SetValue(maintenance, tires);

                if (refreshMethod != null)
                {
                    refreshMethod.Invoke(bridge, null);
                }
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

        private void CacheLegacyLandingGearRenderers()
        {
            List<Renderer> found = new List<Renderer>();
            Transform aircraftRoot = transform;
            for (int nameIndex = 0; nameIndex < LegacyGearRootNames.Length; nameIndex++)
            {
                Transform oldRoot = FindDescendant(aircraftRoot, LegacyGearRootNames[nameIndex]);
                if (oldRoot == null)
                {
                    continue;
                }

                Renderer[] renderers = oldRoot.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    Renderer renderer = renderers[index];
                    if (renderer != null && !found.Contains(renderer))
                    {
                        found.Add(renderer);
                    }
                }
            }

            legacyGearRenderers = found.ToArray();
        }

        private void ForceLegacyLandingGearHidden()
        {
            if (legacyGearRenderers == null)
            {
                return;
            }

            for (int index = 0; index < legacyGearRenderers.Length; index++)
            {
                Renderer renderer = legacyGearRenderers[index];
                if (renderer != null && renderer.enabled)
                {
                    renderer.enabled = false;
                }
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == objectName)
                {
                    return all[index];
                }
            }
            return null;
        }
    }
}
