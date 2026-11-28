using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    public static class P51LandingGearVisualRuntimeBootstrap
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureGeneratedLandingGearVisuals()
        {
            // Include inactive master/template aircraft as well as the currently active P-51.
            // HangarAircraftSpawnConsole can clone those sources later in the session, and the
            // clone must already carry the same wheel-visual repair components and bindings.
            P51LandingGearMaintenanceController[] controllers =
                Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int index = 0; index < controllers.Length; index++)
            {
                EnsureConfiguredFor(controllers[index]);
            }
        }

        public static bool EnsureConfiguredFor(P51LandingGearMaintenanceController maintenance)
        {
            if (maintenance == null)
            {
                return false;
            }

            // First repair the maintenance controller's authoritative tire/rim references from
            // the live hierarchy. This is deliberately repeated for cloned/spawned aircraft:
            // stale serialized references were the reason a completed wheel install could delete
            // the carried wheel while leaving no permanent wheel visible on the airplane.
            maintenance.RepairAllWheelVisualBindingsFromHierarchy();

            Transform aircraft = maintenance.transform;
            Transform gearRoot = aircraft.Find("P-51 Serviceable Retractable Landing Gear");
            if (gearRoot == null)
            {
                return false;
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

            P51RaycastLandingGear physics =
                maintenance.GetComponent<P51RaycastLandingGear>();
            Transform[] proxies =
            {
                GetConfiguredVisual(physics, "leftMainVisual"),
                GetConfiguredVisual(physics, "rightMainVisual"),
                GetConfiguredVisual(physics, "tailwheelVisual")
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
            if (maintenance.GetComponent<P51LandingGearGroundRetractionGuard>() == null)
            {
                maintenance.gameObject.AddComponent<P51LandingGearGroundRetractionGuard>();
            }

            P51WheelInstallDiagnostics.Ensure(maintenance);

            bool complete = true;
            for (int wheelIndex = 0; wheelIndex < 3; wheelIndex++)
            {
                complete &= tires[wheelIndex] != null;
                complete &= proxies[wheelIndex] != null;
            }
            return complete;
        }

        private static Transform GetConfiguredVisual(
            P51RaycastLandingGear physics,
            string fieldName)
        {
            if (physics == null)
            {
                return null;
            }

            FieldInfo field = typeof(P51RaycastLandingGear).GetField(
                fieldName,
                PrivateInstance);
            return field != null ? field.GetValue(physics) as Transform : null;
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
}
