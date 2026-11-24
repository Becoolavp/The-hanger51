using System;
using System.Collections.Generic;
using Hanger51.NPCs;
using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenCleanupFinalizer
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string PolishRootName = "Hanger 51 Citizen Building And Parking Polish";

        [MenuItem("Hanger 51/World/Current/138 - Clean Homes Stores Parking And Citizen Cars")]
        public static void Build()
        {
            Hanger51CitizenPolishAndParkingPass.Build();

            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            if (!world || !citizenRoot)
            {
                Debug.LogError("Step 138 failed. Step 136 did not leave the expected world/citizen roots.");
                return;
            }

            int removedOrphanWheels = 0;
            Transform[] all = world.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Transform t = all[i];
                if (!t || !t.gameObject.scene.IsValid()) continue;

                bool wheelNamed =
                    t.name == "Wheel" ||
                    t.name.StartsWith("Wheel F", StringComparison.Ordinal) ||
                    t.name.StartsWith("Wheel R", StringComparison.Ordinal);

                if (!wheelNamed) continue;
                if (t.GetComponentInParent<H51CitizenVehicle>()) continue;

                Transform p = t.parent;
                if (p && p.name == "Parked Car") continue;

                UnityEngine.Object.DestroyImmediate(t.gameObject);
                removedOrphanWheels++;
            }

            Debug.Log(
                $"Step 138 complete. Rebuilt the refined citizen pass and removed {removedOrphanWheels} orphan legacy wheel objects. Run Step 139.",
                world);
        }

        [MenuItem("Hanger 51/World/Current/139 - Validate Homes Stores Parking And Citizen Cars")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform polishRoot = world ? FindChild(world.transform, PolishRootName) : null;

            if (!world || !citizenRoot || !polishRoot)
            {
                Debug.LogError("Step 139 failed. Run Step 138 first.");
                return;
            }

            H51CitizenVehicle[] cars = citizenRoot.GetComponentsInChildren<H51CitizenVehicle>(true);
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);

            int placeholderCars = 0;
            int orphanWheels = 0;
            int staticCarVisuals = 0;
            int polishParkingPads = 0;
            int shrubsBlockingEntrances = 0;
            int badCommercialSigns = 0;

            Transform[] all = world.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (!t) continue;

                if (t.name == "Parked Car" && !t.GetComponentInParent<H51CitizenVehicle>())
                    placeholderCars++;

                bool wheelNamed =
                    t.name == "Wheel" ||
                    t.name.StartsWith("Wheel F", StringComparison.Ordinal) ||
                    t.name.StartsWith("Wheel R", StringComparison.Ordinal);

                if (wheelNamed && !t.GetComponentInParent<H51CitizenVehicle>())
                    orphanWheels++;

                if (polishRoot && IsDescendantOf(t, polishRoot) && t.name == "Parking Pad")
                    polishParkingPads++;
            }

            for (int i = 0; i < cars.Length; i++)
            {
                H51CitizenVehicle car = cars[i];
                if (!car) continue;
                Transform[] visuals = car.GetComponentsInChildren<Transform>(true);
                for (int v = 0; v < visuals.Length; v++)
                    if (visuals[v] && visuals[v] != car.transform && visuals[v].gameObject.isStatic)
                        staticCarVisuals++;
            }

            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building || !building.Entrance) continue;

                Transform[] inside = building.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < inside.Length; t++)
                {
                    Transform item = inside[t];
                    if (!item) continue;
                    if (item.name == "Yard Shrub" &&
                        Planar(item.position, building.Entrance.position) < 3.2f)
                        shrubsBlockingEntrances++;
                }

                if (building.Kind == H51EnterableBuildingKind.Home) continue;

                Transform awning = FindChild(building.transform, "Awning");
                if (!awning) continue;

                for (int t = 0; t < inside.Length; t++)
                {
                    Transform item = inside[t];
                    if (!item || !item.name.StartsWith("Sign -", StringComparison.Ordinal)) continue;
                    if (item.localPosition.y < awning.localPosition.y + .72f)
                        badCommercialSigns++;
                }
            }

            bool ok =
                cars.Length > 0 &&
                placeholderCars == 0 &&
                orphanWheels == 0 &&
                staticCarVisuals == 0 &&
                polishParkingPads == 0 &&
                shrubsBlockingEntrances == 0 &&
                badCommercialSigns == 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 139 passed. citizen cars={cars.Length}, placeholder cars=0, orphan wheels=0, static car visuals=0, overlapping polish parking pads=0, entrance-blocking shrubs=0, bad commercial signs=0.",
                    polishRoot.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 139 failed. citizen cars={cars.Length}, placeholder cars={placeholderCars}, orphan wheels={orphanWheels}, static car visuals={staticCarVisuals}, overlapping polish parking pads={polishParkingPads}, entrance-blocking shrubs={shrubsBlockingEntrances}, bad commercial signs={badCommercialSigns}.",
                    polishRoot.gameObject);
            }
        }

        static bool IsDescendantOf(Transform item, Transform ancestor)
        {
            Transform t = item;
            while (t)
            {
                if (t == ancestor) return true;
                t = t.parent;
            }
            return false;
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x*x + z*z);
        }

        static GameObject Find(string name)
        {
            GameObject direct = GameObject.Find(name);
            if (direct) return direct;

            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].gameObject.scene.IsValid() && all[i].name == name)
                    return all[i].gameObject;

            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (!root) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
