using UnityEngine;

namespace Hanger51.Aircraft
{
    public static class P51LandingGearVisualRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureGeneratedLandingGearVisuals()
        {
            P51LandingGearMaintenanceController[] controllers =
                Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            for (int index = 0; index < controllers.Length; index++)
            {
                P51LandingGearMaintenanceController maintenance = controllers[index];
                if (maintenance == null)
                {
                    continue;
                }

                Transform aircraft = maintenance.transform;
                Transform gearRoot = aircraft.Find("P-51 Serviceable Retractable Landing Gear");
                if (gearRoot == null)
                {
                    continue;
                }

                Transform[] tires =
                {
                    FindDescendant(gearRoot, "Left Main Tire Visual"),
                    FindDescendant(gearRoot, "Right Main Tire Visual"),
                    FindDescendant(gearRoot, "Tailwheel Tire Visual")
                };
                Transform[] valves =
                {
                    FindDescendant(gearRoot, "Left Main Tire and Valve Service Target"),
                    FindDescendant(gearRoot, "Right Main Tire and Valve Service Target"),
                    FindDescendant(gearRoot, "Tailwheel Tire and Valve Service Target")
                };
                Transform[] proxies =
                {
                    FindDescendant(aircraft, "Wheel Physics Visual Proxy 1"),
                    FindDescendant(aircraft, "Wheel Physics Visual Proxy 2"),
                    FindDescendant(aircraft, "Wheel Physics Visual Proxy 3")
                };

                P51TireWearVisualController wear =
                    maintenance.GetComponent<P51TireWearVisualController>();
                if (wear == null)
                {
                    wear = maintenance.gameObject.AddComponent<P51TireWearVisualController>();
                }
                wear.Configure(maintenance, tires, valves);

                P51LandingGearVisualSuspensionFollower follower =
                    maintenance.GetComponent<P51LandingGearVisualSuspensionFollower>();
                if (follower == null)
                {
                    follower = maintenance.gameObject.AddComponent<P51LandingGearVisualSuspensionFollower>();
                }
                follower.Configure(maintenance, tires, proxies);

                if (maintenance.GetComponent<P51LandingGearReplacementService>() == null)
                {
                    maintenance.gameObject.AddComponent<P51LandingGearReplacementService>();
                }
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
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

    [DefaultExecutionOrder(180)]
    [DisallowMultipleComponent]
    public sealed class P51LandingGearVisualSuspensionFollower : MonoBehaviour
    {
        [SerializeField] private P51LandingGearMaintenanceController maintenance;
        [SerializeField] private Transform[] tireRoots = new Transform[3];
        [SerializeField] private Transform[] physicsVisualProxies = new Transform[3];

        public void Configure(
            P51LandingGearMaintenanceController configuredMaintenance,
            Transform[] configuredTires,
            Transform[] configuredProxies)
        {
            maintenance = configuredMaintenance;
            tireRoots = Copy(configuredTires);
            physicsVisualProxies = Copy(configuredProxies);
        }

        private void LateUpdate()
        {
            if (maintenance == null || maintenance.DeploymentFraction < 0.94f)
            {
                return;
            }

            for (int wheelIndex = 0; wheelIndex < 3; wheelIndex++)
            {
                if (!maintenance.IsGearInstalled(wheelIndex)
                    || !maintenance.IsTireInstalled(wheelIndex))
                {
                    continue;
                }

                Transform tire = wheelIndex < tireRoots.Length ? tireRoots[wheelIndex] : null;
                Transform proxy = wheelIndex < physicsVisualProxies.Length
                    ? physicsVisualProxies[wheelIndex]
                    : null;
                if (tire == null || proxy == null)
                {
                    continue;
                }

                tire.SetPositionAndRotation(proxy.position, proxy.rotation);
            }
        }

        private static Transform[] Copy(Transform[] source)
        {
            Transform[] result = new Transform[3];
            if (source != null)
            {
                System.Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            }
            return result;
        }
    }
}
