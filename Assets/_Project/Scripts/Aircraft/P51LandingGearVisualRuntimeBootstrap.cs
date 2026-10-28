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
            }
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
