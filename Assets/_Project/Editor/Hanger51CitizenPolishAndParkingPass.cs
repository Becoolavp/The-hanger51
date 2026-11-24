using System;
using System.Collections.Generic;
using System.Linq;
using Hanger51.NPCs;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenPolishAndParkingPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string InteriorPassName = "Hanger 51 Enterable Town Interiors And Traffic";
        const string PolishRootName = "Hanger 51 Citizen Building And Parking Polish";
        const string Gen = "Assets/_Project/Characters/H51Citizens/Generated/InteriorParkingPolish";

        struct Mats
        {
            public Material trim, door, brass, floorWood, rug, sofa, cushion, darkWood, lightWood;
            public Material metal, appliance, counter, mattress, pillow, cabinet, asphalt, parkingLine;
            public Material tire, wheel, glass, headlight, tailLight, chrome;
            public Material[] carPaint;
        }

        [MenuItem("Hanger 51/World/Current/136 - Refine Doors Interiors Cars And Parking")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 136 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51CitizenInteriorAndTrafficPass.Build();

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform interiorPass = world ? FindChild(world.transform, InteriorPassName) : null;
            H51CitizenRegistry registry = citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;
            H51TrafficNetwork traffic = interiorPass ? interiorPass.GetComponentInChildren<H51TrafficNetwork>(true) : null;

            if (!world || !terrain || !citizenRoot || !interiorPass || !registry || !traffic)
            {
                Debug.LogError("Step 136 failed. Step 134 did not produce the expected citizen/interior/traffic objects.");
                return;
            }

            try
            {
                Ensure(Gen + "/Materials");
                Mats mats = BuildMaterials();

                Transform old = FindChild(world.transform, PolishRootName);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform polishRoot = New(PolishRootName, world.transform);

                H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Correcting door openings and replacing crude interior furniture", .12f);

                int fixedDoors = 0;
                int polishedInteriors = 0;
                for (int i = 0; i < buildings.Length; i++)
                {
                    H51EnterableBuilding building = buildings[i];
                    if (!building) continue;

                    Transform interior = FindChild(building.transform, "H51 Enterable Interior");
                    Transform floor = interior ? FindChild(interior, "Interior Floor") : null;
                    if (!interior || !floor) continue;

                    DisableLegacyDoorBlockers(building.transform, interior);
                    RemoveOldInteriorFurniture(interior);

                    Transform detail = New("H51 Interior Polish", interior);
                    BuildDoor(building, floor, detail, mats);
                    BuildInterior(building, floor, detail, mats);
                    AddInteriorLight(building, floor, detail);

                    fixedDoors++;
                    polishedInteriors++;
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building actual destination parking spaces", .48f);

                List<H51ParkingSpot> spots = BuildParkingSpots(
                    polishRoot,
                    buildings,
                    traffic,
                    terrain,
                    mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Rebuilding citizen cars and assigning their home parking", .68f);

                H51CitizenVehicle[] vehicles = citizenRoot.GetComponentsInChildren<H51CitizenVehicle>(true);
                Dictionary<string,H51CitizenIdentity> citizens = citizenRoot
                    .GetComponentsInChildren<H51CitizenIdentity>(true)
                    .Where(c => c && !string.IsNullOrEmpty(c.CitizenId))
                    .ToDictionary(c => c.CitizenId, c => c);

                int polishedCars = 0;
                int parkedCars = 0;

                for (int i = 0; i < vehicles.Length; i++)
                {
                    H51CitizenVehicle vehicle = vehicles[i];
                    if (!vehicle) continue;

                    int seed = StableHash(vehicle.OwnerCitizenId);
                    RebuildCarVisual(vehicle.transform, seed, mats);
                    polishedCars++;

                    if (!citizens.TryGetValue(vehicle.OwnerCitizenId, out H51CitizenIdentity owner))
                        continue;

                    H51CitizenAnchor home = registry.FindAnchor(owner.HomeAnchorId);
                    if (!home) continue;

                    H51ParkingSpot homeSpot = FindBestAvailableSpot(
                        spots,
                        home.transform.position,
                        home.TownName,
                        vehicle,
                        70f);

                    if (!homeSpot) continue;
                    vehicle.SetInitialParkingSpot(homeSpot);
                    parkedCars++;
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Rebaking pedestrian navigation around the corrected interiors", .88f);

                int navMeshes = RebuildNavMeshes(citizenRoot);

                registry.RefreshSceneReferences();
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = polishRoot.gameObject;

                Debug.Log(
                    $"Step 136 complete. corrected doors={fixedDoors}, polished interiors={polishedInteriors}, parking spaces={spots.Count}, polished cars={polishedCars}, cars assigned home parking={parkedCars}/{vehicles.Length}, NavMeshes rebaked={navMeshes}. The same-destination re-entry loop is also disabled in the citizen brain. Run Step 137.",
                    polishRoot.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/137 - Validate Doors Parking Cars And Interiors")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform polishRoot = world ? FindChild(world.transform, PolishRootName) : null;

            if (!world || !citizenRoot || !polishRoot)
            {
                Debug.LogError("Step 137 failed. Run Step 136 first.");
                return;
            }

            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            H51ParkingSpot[] spots = world.GetComponentsInChildren<H51ParkingSpot>(true);
            H51CitizenVehicle[] vehicles = citizenRoot.GetComponentsInChildren<H51CitizenVehicle>(true);
            NavMeshSurface[] surfaces = citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);

            int activeLegacyDoorBlockers = 0;
            int missingPolish = 0;
            int missingLights = 0;
            int sparseCars = 0;
            int carsWithoutParking = 0;
            int carsAwayFromParking = 0;
            int duplicateReservations = 0;

            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building) continue;

                Transform interior = FindChild(building.transform, "H51 Enterable Interior");
                if (!interior || !FindChild(interior, "H51 Interior Polish"))
                    missingPolish++;

                if (!interior || !interior.GetComponentInChildren<H51InteriorLightProximity>(true))
                    missingLights++;

                foreach (Transform t in building.GetComponentsInChildren<Transform>(true))
                {
                    if (!t || !t.gameObject.activeSelf) continue;
                    if (IsLegacyDoorBlockerName(t.name))
                        activeLegacyDoorBlockers++;
                }
            }

            Dictionary<H51ParkingSpot,int> reservationCounts = new Dictionary<H51ParkingSpot,int>();
            for (int i = 0; i < vehicles.Length; i++)
            {
                H51CitizenVehicle vehicle = vehicles[i];
                if (!vehicle) continue;

                if (vehicle.transform.childCount < 14) sparseCars++;

                H51ParkingSpot current = vehicle.CurrentParkingSpot;
                if (!current)
                {
                    carsWithoutParking++;
                    continue;
                }

                if (!reservationCounts.ContainsKey(current)) reservationCounts[current] = 0;
                reservationCounts[current]++;

                if (Planar(vehicle.transform.position, current.transform.position) > .75f)
                    carsAwayFromParking++;
            }

            foreach (KeyValuePair<H51ParkingSpot,int> pair in reservationCounts)
                if (pair.Value > 1) duplicateReservations += pair.Value - 1;

            int surfacesWithData = surfaces.Count(s => s && s.navMeshData);
            int minSpots = Mathf.Max(buildings.Length * 2, vehicles.Length + 12);

            bool ok =
                buildings.Length > 0 &&
                activeLegacyDoorBlockers == 0 &&
                missingPolish == 0 &&
                missingLights == 0 &&
                spots.Length >= minSpots &&
                sparseCars == 0 &&
                carsWithoutParking == 0 &&
                carsAwayFromParking == 0 &&
                duplicateReservations == 0 &&
                surfacesWithData == surfaces.Length;

            if (ok)
            {
                Debug.Log(
                    $"Step 137 passed. buildings={buildings.Length}, parking spaces={spots.Length}, cars={vehicles.Length}, legacy door blockers=0, polished interiors={buildings.Length}, parked cars={vehicles.Length}, duplicate reservations=0, NavMeshes={surfacesWithData}.",
                    polishRoot.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 137 failed. buildings={buildings.Length}, active legacy door blockers={activeLegacyDoorBlockers}, missing interior polish={missingPolish}, missing proximity lights={missingLights}, parking spaces={spots.Length}/{minSpots} minimum, cars={vehicles.Length}, sparse car visuals={sparseCars}, cars without parking={carsWithoutParking}, cars away from reserved spot={carsAwayFromParking}, duplicate parking reservations={duplicateReservations}, NavMeshes={surfacesWithData}/{surfaces.Length}.",
                    polishRoot.gameObject);
            }
        }

        static void DisableLegacyDoorBlockers(Transform building, Transform interior)
        {
            Transform[] all = building.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (!t || t == building || IsDescendantOf(t, interior)) continue;
                if (IsLegacyDoorBlockerName(t.name))
                    t.gameObject.SetActive(false);
            }
        }

        static bool IsLegacyDoorBlockerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name == "Door Trim" ||
                   name == "Wood Door" ||
                   name == "Front Door" ||
                   name == "Service Door" ||
                   name == "Service Bay";
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

        static void RemoveOldInteriorFurniture(Transform interior)
        {
            HashSet<string> shellNames = new HashSet<string>
            {
                "Interior Floor",
                "Back Wall",
                "Left Wall",
                "Right Wall",
                "Front Wall Left",
                "Front Wall Right",
                "Door Header",
                "Entrance",
                "Interior Center",
                "Interior Activity Frame"
            };

            for (int i = interior.childCount - 1; i >= 0; i--)
            {
                Transform child = interior.GetChild(i);
                if (shellNames.Contains(child.name)) continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        static void BuildDoor(
            H51EnterableBuilding building,
            Transform floor,
            Transform root,
            Mats mats)
        {
            Vector3 center = floor.localPosition;
            Vector3 size = floor.localScale;
            float floorY = center.y + size.y * .5f;
            float frontZ = center.z + size.z * .5f + .08f;

            string lower = building.DisplayName.ToLowerInvariant();
            bool service =
                lower.Contains("garage") ||
                lower.Contains("fire") ||
                lower.Contains("farm");

            float width = service ? 4.8f : building.Kind == H51EnterableBuildingKind.Home ? 2.05f : 2.35f;
            float height = service ? 4.1f : 3.2f;
            float jamb = .16f;

            Box(root, "Door Frame Left",
                new Vector3(center.x-width*.5f-jamb*.5f, floorY+height*.5f, frontZ+.08f),
                new Vector3(jamb,height+.15f,.22f), mats.trim, true);

            Box(root, "Door Frame Right",
                new Vector3(center.x+width*.5f+jamb*.5f, floorY+height*.5f, frontZ+.08f),
                new Vector3(jamb,height+.15f,.22f), mats.trim, true);

            Box(root, "Door Frame Header",
                new Vector3(center.x,floorY+height+.08f,frontZ+.08f),
                new Vector3(width+jamb*2f,.16f,.22f), mats.trim, true);

            Box(root, "Door Threshold",
                new Vector3(center.x,floorY+.035f,frontZ+.10f),
                new Vector3(width+.18f,.07f,.48f), mats.metal, false);

            if (service)
            {
                Box(root, "Open Service Door Left",
                    new Vector3(center.x-width*.52f, floorY+height*.5f, frontZ-.62f),
                    new Vector3(.10f,height-.18f,1.25f), mats.metal, false);
                Box(root, "Open Service Door Right",
                    new Vector3(center.x+width*.52f, floorY+height*.5f, frontZ-.62f),
                    new Vector3(.10f,height-.18f,1.25f), mats.metal, false);
                return;
            }

            float doorWidth = width - .18f;
            Quaternion openRotation = Quaternion.Euler(0f,-74f,0f);
            Vector3 hinge = new Vector3(center.x-width*.5f+.08f, floorY+height*.5f, frontZ-.02f);
            Vector3 doorCenter = hinge + openRotation * Vector3.right * (doorWidth*.5f);

            BoxRot(root, "Open Door",
                doorCenter,
                openRotation,
                new Vector3(doorWidth,height-.16f,.10f),
                mats.door,
                true);

            Sphere(root, "Door Knob",
                doorCenter + openRotation * new Vector3(doorWidth*.38f,0f,-.08f),
                .07f,
                mats.brass);
        }

        static void BuildInterior(
            H51EnterableBuilding building,
            Transform floor,
            Transform root,
            Mats mats)
        {
            Vector3 center = floor.localPosition;
            Vector3 size = floor.localScale;
            float y = center.y + size.y*.5f;
            float w = size.x;
            float d = size.z;

            if (building.Kind == H51EnterableBuildingKind.Home)
            {
                AddHomeInterior(root, center, w, d, y, mats);
                return;
            }

            string lower = building.DisplayName.ToLowerInvariant();

            if (lower.Contains("cafe"))
                AddCafeInterior(root, center, w, d, y, mats);
            else if (lower.Contains("clinic"))
                AddClinicInterior(root, center, w, d, y, mats);
            else if (lower.Contains("garage") || lower.Contains("fire"))
                AddServiceInterior(root, center, w, d, y, mats);
            else if (lower.Contains("store") || lower.Contains("supply") || lower.Contains("post"))
                AddStoreInterior(root, center, w, d, y, mats);
            else
                AddOfficeInterior(root, center, w, d, y, mats);
        }

        static void AddHomeInterior(Transform root, Vector3 c, float w, float d, float y, Mats m)
        {
            Box(root, "Living Rug", c + new Vector3(-w*.20f,.025f,d*.15f),
                new Vector3(w*.34f,.05f,d*.30f), m.rug, false);

            Transform sofa = New("Living Room Sofa", root);
            sofa.localPosition = c + new Vector3(-w*.27f,y+.02f-c.y,d*.02f);
            Box(sofa, "Sofa Base", new Vector3(0,.34f,0), new Vector3(Mathf.Min(3.2f,w*.30f),.46f,.96f), m.sofa, true);
            Box(sofa, "Sofa Back", new Vector3(0,.83f,-.39f), new Vector3(Mathf.Min(3.2f,w*.30f),.88f,.18f), m.sofa, true);
            Box(sofa, "Left Arm", new Vector3(-Mathf.Min(3.2f,w*.30f)*.48f,.62f,0), new Vector3(.18f,.56f,1.0f), m.sofa, true);
            Box(sofa, "Right Arm", new Vector3(Mathf.Min(3.2f,w*.30f)*.48f,.62f,0), new Vector3(.18f,.56f,1.0f), m.sofa, true);
            Box(sofa, "Seat Cushion", new Vector3(0,.62f,.08f), new Vector3(Mathf.Min(2.72f,w*.26f),.16f,.72f), m.cushion, false);

            Table(root, c + new Vector3(-w*.08f,y-c.y,d*.18f), 1.5f,.8f,.48f,m.darkWood);

            Box(root, "TV Console", c + new Vector3(w*.30f,y+.42f-c.y,d*.12f),
                new Vector3(1.75f,.78f,.46f), m.darkWood, true);
            Box(root, "Television", c + new Vector3(w*.30f,y+1.16f-c.y,d*.12f),
                new Vector3(1.5f,.82f,.10f), m.glass, false);

            float kitchenZ = c.z-d*.34f;
            for (int i=-1;i<=1;i++)
                Box(root, "Lower Kitchen Cabinet " + i,
                    new Vector3(c.x+i*1.15f,y+.48f,kitchenZ),
                    new Vector3(1.08f,.92f,.62f), m.cabinet, true);

            Box(root, "Kitchen Countertop", new Vector3(c.x,y+.98f,kitchenZ),
                new Vector3(3.6f,.10f,.72f), m.counter, true);
            Box(root, "Kitchen Sink", new Vector3(c.x-.6f,y+1.05f,kitchenZ),
                new Vector3(.72f,.08f,.44f), m.metal, false);
            Box(root, "Refrigerator", new Vector3(c.x+w*.34f,y+1.02f,c.z-d*.30f),
                new Vector3(.82f,2.04f,.78f), m.appliance, true);

            Table(root, c + new Vector3(w*.08f,y-c.y,-d*.03f), 1.55f,1.05f,.72f,m.lightWood);
            Chair(root, new Vector3(c.x+w*.08f-1.05f,y,c.z-d*.03f), Quaternion.Euler(0,90,0), m);
            Chair(root, new Vector3(c.x+w*.08f+1.05f,y,c.z-d*.03f), Quaternion.Euler(0,-90,0), m);

            Transform bed = New("Bed", root);
            bed.localPosition = new Vector3(c.x-w*.28f,y,c.z-d*.30f);
            Box(bed, "Bed Frame", new Vector3(0,.28f,0), new Vector3(2.05f,.38f,2.75f), m.darkWood, true);
            Box(bed, "Mattress", new Vector3(0,.54f,0), new Vector3(1.92f,.30f,2.58f), m.mattress, true);
            Box(bed, "Pillow", new Vector3(0,.76f,-.88f), new Vector3(1.25f,.16f,.48f), m.pillow, false);
            Box(bed, "Headboard", new Vector3(0,.92f,-1.28f), new Vector3(2.08f,1.28f,.14f), m.darkWood, true);

            Box(root, "Dresser", new Vector3(c.x-w*.04f,y+.54f,c.z-d*.39f),
                new Vector3(1.45f,1.08f,.54f), m.darkWood, true);
        }

        static void AddStoreInterior(Transform root, Vector3 c, float w, float d, float y, Mats m)
        {
            Box(root, "Checkout Counter", new Vector3(c.x+w*.16f,y+.56f,c.z-d*.30f),
                new Vector3(w*.36f,1.12f,.72f), m.counter, true);
            Box(root, "Register", new Vector3(c.x+w*.20f,y+1.24f,c.z-d*.30f),
                new Vector3(.48f,.28f,.42f), m.appliance, false);

            for (int side=-1; side<=1; side+=2)
            {
                Shelf(root, new Vector3(c.x+side*w*.28f,y,c.z+d*.04f), 1.0f, d*.34f, 1.8f, m);
                Shelf(root, new Vector3(c.x+side*w*.28f,y,c.z-d*.22f), 1.0f, d*.22f, 1.8f, m);
            }
        }

        static void AddCafeInterior(Transform root, Vector3 c, float w, float d, float y, Mats m)
        {
            Box(root, "Cafe Counter Base", new Vector3(c.x,y+.55f,c.z-d*.31f),
                new Vector3(w*.52f,1.10f,.68f), m.cabinet, true);
            Box(root, "Cafe Counter Top", new Vector3(c.x,y+1.13f,c.z-d*.31f),
                new Vector3(w*.55f,.08f,.78f), m.counter, true);
            Box(root, "Coffee Machine", new Vector3(c.x+w*.16f,y+1.48f,c.z-d*.31f),
                new Vector3(.62f,.62f,.46f), m.appliance, false);

            for (int x=-1;x<=1;x+=2)
            {
                Vector3 p = new Vector3(c.x+x*w*.22f,y,c.z+d*.08f);
                Table(root,p,1.25f,1.25f,.72f,m.lightWood);
                Chair(root,p+new Vector3(0,0,1.0f),Quaternion.identity,m);
                Chair(root,p+new Vector3(0,0,-1.0f),Quaternion.Euler(0,180,0),m);
            }
        }

        static void AddClinicInterior(Transform root, Vector3 c, float w, float d, float y, Mats m)
        {
            Box(root, "Reception Base", new Vector3(c.x,y+.56f,c.z+d*.18f),
                new Vector3(w*.42f,1.12f,.70f), m.counter, true);
            Box(root, "Reception Top", new Vector3(c.x,y+1.16f,c.z+d*.18f),
                new Vector3(w*.45f,.08f,.80f), m.appliance, true);

            ExamBed(root,new Vector3(c.x-w*.25f,y,c.z-d*.22f),m);
            ExamBed(root,new Vector3(c.x+w*.25f,y,c.z-d*.22f),m);

            Box(root, "Medical Storage", new Vector3(c.x+w*.38f,y+1.0f,c.z-d*.39f),
                new Vector3(1.0f,2.0f,.54f), m.appliance, true);
        }

        static void AddServiceInterior(Transform root, Vector3 c, float w, float d, float y, Mats m)
        {
            Workbench(root,new Vector3(c.x,y,c.z-d*.38f),w*.50f,m);
            Shelf(root,new Vector3(c.x-w*.39f,y,c.z-d*.12f),.86f,d*.30f,2.1f,m);
            Shelf(root,new Vector3(c.x+w*.39f,y,c.z-d*.12f),.86f,d*.30f,2.1f,m);

            Box(root, "Service Mat Left", new Vector3(c.x-w*.20f,y+.025f,c.z+d*.06f),
                new Vector3(2.3f,.05f,4.8f), m.rug, false);
            Box(root, "Service Mat Right", new Vector3(c.x+w*.20f,y+.025f,c.z+d*.06f),
                new Vector3(2.3f,.05f,4.8f), m.rug, false);
        }

        static void AddOfficeInterior(Transform root, Vector3 c, float w, float d, float y, Mats m)
        {
            for (int side=-1;side<=1;side+=2)
            {
                Desk(root,new Vector3(c.x+side*w*.22f,y,c.z-d*.08f),m);
                Chair(root,new Vector3(c.x+side*w*.22f,y,c.z-d*.08f+1.0f),Quaternion.identity,m);
            }
            Box(root, "Records Cabinet", new Vector3(c.x+w*.38f,y+1.0f,c.z-d*.39f),
                new Vector3(1.0f,2.0f,.56f), m.metal, true);
            Box(root, "Public Counter", new Vector3(c.x,y+.56f,c.z+d*.20f),
                new Vector3(w*.40f,1.12f,.68f), m.counter, true);
        }

        static void AddInteriorLight(H51EnterableBuilding building, Transform floor, Transform root)
        {
            GameObject fixture = new GameObject("Ceiling Light");
            fixture.transform.SetParent(root,false);
            fixture.transform.localPosition = floor.localPosition + new Vector3(0f,3.2f,0f);

            Light light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Clamp(Mathf.Max(floor.localScale.x,floor.localScale.z)*.72f,7f,13f);
            light.intensity = building.Kind == H51EnterableBuildingKind.Home ? 1.05f : 1.25f;
            light.color = new Color(1f,.90f,.74f);
            light.shadows = LightShadows.None;
            light.enabled = false;

            H51InteriorLightProximity proximity = fixture.AddComponent<H51InteriorLightProximity>();
            proximity.Configure(light,32f);
        }

        static List<H51ParkingSpot> BuildParkingSpots(
            Transform parent,
            H51EnterableBuilding[] buildings,
            H51TrafficNetwork traffic,
            Terrain terrain,
            Mats mats)
        {
            Transform root = New("Citizen Parking", parent);
            List<H51ParkingSpot> spots = new List<H51ParkingSpot>();

            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building || !building.Entrance) continue;

                int count = building.Kind == H51EnterableBuildingKind.Home ? 2 :
                            building.Kind == H51EnterableBuildingKind.Farm ? 5 : 4;

                float spacing = 2.65f;
                float forwardDistance = building.Kind == H51EnterableBuildingKind.Home ? 4.2f : 5.8f;
                Vector3 right = building.transform.right;
                Vector3 forward = building.transform.forward;
                Vector3 center = building.Entrance.position + forward * forwardDistance;

                Transform lot = New("Parking - " + building.DisplayName, root);

                float lotWidth = Mathf.Max(5.7f,count*spacing+.9f);
                Vector3 pad = center;
                pad.y = terrain.SampleHeight(pad) + terrain.transform.position.y + .02f;

                BoxWorld(lot,"Parking Pad",pad,
                    Quaternion.LookRotation(forward,Vector3.up),
                    new Vector3(lotWidth,.04f,5.3f),
                    mats.asphalt,
                    false);

                for (int s=0; s<count; s++)
                {
                    float lateral = (s-(count-1)*.5f)*spacing;
                    Vector3 p = center + right*lateral;
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y + .08f;

                    int node = traffic.FindNearestNode(p);
                    if (node < 0) continue;
                    Vector3 approach = traffic.GetNode(node);

                    Transform spotTransform = New("Parking Space " + (s+1).ToString("00"), lot);
                    spotTransform.position = p;
                    spotTransform.rotation = Quaternion.LookRotation(-forward,Vector3.up);

                    H51ParkingSpot spot = spotTransform.gameObject.AddComponent<H51ParkingSpot>();
                    spot.Configure(
                        "PARK-" + Safe(building.BuildingId) + "-" + s.ToString("00"),
                        building.BuildingId,
                        building.TownName,
                        approach);

                    Box(spotTransform,"Parking Line Left",
                        new Vector3(-1.14f,.02f,0f),
                        new Vector3(.08f,.04f,4.65f),
                        mats.parkingLine,
                        false);
                    Box(spotTransform,"Parking Line Right",
                        new Vector3(1.14f,.02f,0f),
                        new Vector3(.08f,.04f,4.65f),
                        mats.parkingLine,
                        false);

                    spots.Add(spot);
                }
            }

            return spots;
        }

        static H51ParkingSpot FindBestAvailableSpot(
            List<H51ParkingSpot> spots,
            Vector3 target,
            string town,
            H51CitizenVehicle vehicle,
            float maxDistance)
        {
            H51ParkingSpot best = null;
            float bestDistance = maxDistance;

            for (int i=0;i<spots.Count;i++)
            {
                H51ParkingSpot spot = spots[i];
                if (!spot || !spot.IsAvailableFor(vehicle)) continue;
                if (!string.IsNullOrEmpty(town) && !string.IsNullOrEmpty(spot.TownName) && spot.TownName != town)
                    continue;

                float distance = Planar(spot.transform.position,target);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = spot;
            }

            return best;
        }

        static void RebuildCarVisual(Transform car, int seed, Mats m)
        {
            for (int i=car.childCount-1;i>=0;i--)
                UnityEngine.Object.DestroyImmediate(car.GetChild(i).gameObject);

            int variant = Mathf.Abs(seed)%3;
            Material paint = m.carPaint[Mathf.Abs(seed)%m.carPaint.Length];

            if (variant == 1)
                BuildPickup(car,paint,m);
            else if (variant == 2)
                BuildWagon(car,paint,m);
            else
                BuildSedan(car,paint,m);
        }

        static void BuildSedan(Transform car, Material paint, Mats m)
        {
            Box(car,"Lower Body",new Vector3(0,.55f,0),new Vector3(1.82f,.56f,4.35f),paint,false);
            Box(car,"Hood",new Vector3(0,.88f,1.40f),new Vector3(1.68f,.20f,1.38f),paint,false);
            Box(car,"Trunk",new Vector3(0,.86f,-1.55f),new Vector3(1.67f,.22f,1.05f),paint,false);
            Box(car,"Cabin",new Vector3(0,1.22f,-.10f),new Vector3(1.58f,.64f,1.86f),paint,false);
            Box(car,"Roof",new Vector3(0,1.59f,-.10f),new Vector3(1.42f,.12f,1.28f),paint,false);
            BoxRot(car,"Windshield",new Vector3(0,1.32f,.78f),Quaternion.Euler(18f,0f,0f),new Vector3(1.43f,.56f,.08f),m.glass,false);
            BoxRot(car,"Rear Window",new Vector3(0,1.30f,-.97f),Quaternion.Euler(-18f,0f,0f),new Vector3(1.42f,.54f,.08f),m.glass,false);
            SideWindows(car,-.805f,m); SideWindows(car,.805f,m);
            CarDetails(car,m);
        }

        static void BuildWagon(Transform car, Material paint, Mats m)
        {
            Box(car,"Lower Body",new Vector3(0,.55f,0),new Vector3(1.86f,.58f,4.45f),paint,false);
            Box(car,"Hood",new Vector3(0,.88f,1.48f),new Vector3(1.70f,.20f,1.25f),paint,false);
            Box(car,"Wagon Cabin",new Vector3(0,1.20f,-.35f),new Vector3(1.60f,.68f,2.55f),paint,false);
            Box(car,"Wagon Roof",new Vector3(0,1.60f,-.35f),new Vector3(1.45f,.12f,2.28f),paint,false);
            BoxRot(car,"Windshield",new Vector3(0,1.32f,.88f),Quaternion.Euler(17f,0f,0f),new Vector3(1.45f,.57f,.08f),m.glass,false);
            Box(car,"Rear Hatch Glass",new Vector3(0,1.30f,-1.62f),new Vector3(1.42f,.58f,.08f),m.glass,false);
            SideWindows(car,-.815f,m); SideWindows(car,.815f,m);
            CarDetails(car,m);
        }

        static void BuildPickup(Transform car, Material paint, Mats m)
        {
            Box(car,"Lower Body",new Vector3(0,.58f,.15f),new Vector3(1.90f,.62f,4.55f),paint,false);
            Box(car,"Hood",new Vector3(0,.92f,1.52f),new Vector3(1.73f,.24f,1.30f),paint,false);
            Box(car,"Pickup Cab",new Vector3(0,1.24f,.42f),new Vector3(1.62f,.70f,1.45f),paint,false);
            Box(car,"Pickup Roof",new Vector3(0,1.65f,.42f),new Vector3(1.46f,.12f,1.18f),paint,false);
            BoxRot(car,"Windshield",new Vector3(0,1.35f,1.08f),Quaternion.Euler(16f,0f,0f),new Vector3(1.46f,.58f,.08f),m.glass,false);
            Box(car,"Rear Cab Glass",new Vector3(0,1.36f,-.25f),new Vector3(1.42f,.54f,.08f),m.glass,false);
            Box(car,"Pickup Bed Floor",new Vector3(0,.86f,-1.25f),new Vector3(1.62f,.14f,1.95f),paint,false);
            Box(car,"Pickup Bed Left",new Vector3(-.82f,1.05f,-1.25f),new Vector3(.14f,.54f,1.95f),paint,false);
            Box(car,"Pickup Bed Right",new Vector3(.82f,1.05f,-1.25f),new Vector3(.14f,.54f,1.95f),paint,false);
            Box(car,"Tailgate",new Vector3(0,1.02f,-2.20f),new Vector3(1.72f,.52f,.12f),paint,false);
            CarDetails(car,m);
        }

        static void SideWindows(Transform car, float x, Mats m)
        {
            Box(car,"Side Window Front " + x,new Vector3(x,1.34f,.37f),new Vector3(.07f,.48f,.72f),m.glass,false);
            Box(car,"Side Window Rear " + x,new Vector3(x,1.34f,-.48f),new Vector3(.07f,.48f,.72f),m.glass,false);
        }

        static void CarDetails(Transform car, Mats m)
        {
            Wheel(car,"Wheel FL",new Vector3(-.96f,.45f,1.38f),m);
            Wheel(car,"Wheel FR",new Vector3(.96f,.45f,1.38f),m);
            Wheel(car,"Wheel RL",new Vector3(-.96f,.45f,-1.42f),m);
            Wheel(car,"Wheel RR",new Vector3(.96f,.45f,-1.42f),m);

            Box(car,"Front Bumper",new Vector3(0,.50f,2.25f),new Vector3(1.82f,.16f,.14f),m.chrome,false);
            Box(car,"Rear Bumper",new Vector3(0,.50f,-2.25f),new Vector3(1.82f,.16f,.14f),m.chrome,false);

            Box(car,"Headlight L",new Vector3(-.55f,.72f,2.20f),new Vector3(.42f,.22f,.08f),m.headlight,false);
            Box(car,"Headlight R",new Vector3(.55f,.72f,2.20f),new Vector3(.42f,.22f,.08f),m.headlight,false);
            Box(car,"Tail Light L",new Vector3(-.57f,.72f,-2.20f),new Vector3(.40f,.22f,.08f),m.tailLight,false);
            Box(car,"Tail Light R",new Vector3(.57f,.72f,-2.20f),new Vector3(.40f,.22f,.08f),m.tailLight,false);
        }

        static void Wheel(Transform parent, string name, Vector3 pos, Mats m)
        {
            GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = name;
            tire.transform.SetParent(parent,false);
            tire.transform.localPosition = pos;
            tire.transform.localRotation = Quaternion.Euler(0f,0f,90f);
            tire.transform.localScale = new Vector3(.36f,.17f,.36f);
            tire.GetComponent<Renderer>().sharedMaterial = m.tire;
            Collider tc = tire.GetComponent<Collider>();
            if (tc) UnityEngine.Object.DestroyImmediate(tc);

            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = name + " Hub";
            hub.transform.SetParent(tire.transform,false);
            hub.transform.localPosition = Vector3.zero;
            hub.transform.localRotation = Quaternion.identity;
            hub.transform.localScale = new Vector3(.54f,1.04f,.54f);
            hub.GetComponent<Renderer>().sharedMaterial = m.wheel;
            Collider hc = hub.GetComponent<Collider>();
            if (hc) UnityEngine.Object.DestroyImmediate(hc);
        }

        static void Table(Transform root, Vector3 p, float width, float depth, float height, Material mat)
        {
            Box(root,"Table Top",p+Vector3.up*height,new Vector3(width,.12f,depth),mat,true);
            float x=width*.42f,z=depth*.40f;
            Box(root,"Table Leg",p+new Vector3(-x,height*.48f,-z),new Vector3(.10f,height,.10f),mat,true);
            Box(root,"Table Leg",p+new Vector3(x,height*.48f,-z),new Vector3(.10f,height,.10f),mat,true);
            Box(root,"Table Leg",p+new Vector3(-x,height*.48f,z),new Vector3(.10f,height,.10f),mat,true);
            Box(root,"Table Leg",p+new Vector3(x,height*.48f,z),new Vector3(.10f,height,.10f),mat,true);
        }

        static void Chair(Transform root, Vector3 p, Quaternion rotation, Mats m)
        {
            Transform chair = New("Chair",root);
            chair.localPosition = p;
            chair.localRotation = rotation;
            Box(chair,"Seat",new Vector3(0,.48f,0),new Vector3(.66f,.12f,.66f),m.lightWood,true);
            Box(chair,"Back",new Vector3(0,.91f,-.27f),new Vector3(.66f,.74f,.10f),m.lightWood,true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box(chair,"Leg",new Vector3(x*.26f,.23f,z*.25f),new Vector3(.08f,.46f,.08f),m.darkWood,true);
        }

        static void Shelf(Transform root, Vector3 p, float width, float depth, float height, Mats m)
        {
            Box(root,"Shelf Left",p+new Vector3(-width*.43f,height*.5f,0),new Vector3(.10f,height,depth),m.metal,true);
            Box(root,"Shelf Right",p+new Vector3(width*.43f,height*.5f,0),new Vector3(.10f,height,depth),m.metal,true);
            for(int i=0;i<4;i++)
                Box(root,"Shelf Board",p+new Vector3(0,.20f+i*(height-.25f)/3f,0),new Vector3(width,.08f,depth),m.lightWood,true);
        }

        static void Desk(Transform root, Vector3 p, Mats m)
        {
            Table(root,p,1.85f,.88f,.72f,m.darkWood);
            Box(root,"Desk Drawer",p+new Vector3(.62f,.48f,0),new Vector3(.48f,.72f,.70f),m.darkWood,true);
        }

        static void ExamBed(Transform root, Vector3 p, Mats m)
        {
            Box(root,"Exam Bed Base",p+new Vector3(0,.45f,0),new Vector3(2.0f,.65f,.82f),m.metal,true);
            Box(root,"Exam Bed Cushion",p+new Vector3(0,.82f,0),new Vector3(1.92f,.16f,.76f),m.mattress,false);
        }

        static void Workbench(Transform root, Vector3 p, float width, Mats m)
        {
            Box(root,"Workbench Top",p+new Vector3(0,.88f,0),new Vector3(width,.14f,.82f),m.darkWood,true);
            Box(root,"Workbench Back",p+new Vector3(0,1.55f,-.36f),new Vector3(width,1.25f,.10f),m.metal,true);
            Box(root,"Workbench Leg L",p+new Vector3(-width*.43f,.44f,0),new Vector3(.14f,.88f,.68f),m.metal,true);
            Box(root,"Workbench Leg R",p+new Vector3(width*.43f,.44f,0),new Vector3(.14f,.88f,.68f),m.metal,true);
        }

        static GameObject Box(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool collider)
        {
            return BoxRot(parent,name,localPosition,Quaternion.identity,localScale,material,collider);
        }

        static GameObject BoxRot(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material,
            bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent,false);
            g.transform.localPosition = localPosition;
            g.transform.localRotation = localRotation;
            g.transform.localScale = localScale;
            Renderer renderer = g.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider col = g.GetComponent<Collider>();
            if (!collider && col) UnityEngine.Object.DestroyImmediate(col);
            g.isStatic = true;
            return g;
        }

        static GameObject BoxWorld(
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material,
            bool collider)
        {
            GameObject g = Box(parent,name,Vector3.zero,scale,material,collider);
            g.transform.position = position;
            g.transform.rotation = rotation;
            return g;
        }

        static void Sphere(Transform parent, string name, Vector3 localPosition, float scale, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(parent,false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = Vector3.one*scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            Collider col = g.GetComponent<Collider>();
            if (col) UnityEngine.Object.DestroyImmediate(col);
            g.isStatic = true;
        }

        static Mats BuildMaterials()
        {
            Mats m = new Mats();
            m.trim = Mat("Door Trim",new Color(.82f,.79f,.70f),.02f);
            m.door = Mat("Door Wood",new Color(.25f,.12f,.055f),.04f);
            m.brass = Mat("Brass",new Color(.55f,.38f,.10f),.20f);
            m.floorWood = Mat("Wood Floor",new Color(.31f,.23f,.14f),.02f);
            m.rug = Mat("Rug",new Color(.12f,.20f,.26f),0f);
            m.sofa = Mat("Sofa",new Color(.15f,.22f,.27f),0f);
            m.cushion = Mat("Cushion",new Color(.23f,.31f,.35f),0f);
            m.darkWood = Mat("Dark Wood",new Color(.19f,.10f,.045f),.02f);
            m.lightWood = Mat("Light Wood",new Color(.43f,.29f,.15f),.02f);
            m.metal = Mat("Interior Metal",new Color(.27f,.29f,.30f),.06f);
            m.appliance = Mat("Appliance",new Color(.53f,.54f,.52f),.08f);
            m.counter = Mat("Counter",new Color(.49f,.41f,.30f),.04f);
            m.mattress = Mat("Mattress",new Color(.66f,.69f,.66f),0f);
            m.pillow = Mat("Pillow",new Color(.82f,.81f,.72f),0f);
            m.cabinet = Mat("Cabinet",new Color(.34f,.22f,.12f),.02f);
            m.asphalt = Mat("Parking Asphalt",new Color(.12f,.13f,.14f),0f);
            m.parkingLine = Mat("Parking Line",new Color(.75f,.72f,.54f),0f);
            m.tire = Mat("Tire",new Color(.018f,.018f,.018f),0f);
            m.wheel = Mat("Wheel",new Color(.30f,.32f,.33f),.12f);
            m.glass = Mat("Car Glass",new Color(.055f,.10f,.13f),.22f);
            m.headlight = Mat("Headlight",new Color(.86f,.82f,.62f),.08f);
            m.tailLight = Mat("Tail Light",new Color(.52f,.035f,.025f),.10f);
            m.chrome = Mat("Chrome",new Color(.35f,.36f,.36f),.20f);
            m.carPaint = new[]
            {
                Mat("Car Blue",new Color(.06f,.16f,.31f),.14f),
                Mat("Car Red",new Color(.38f,.055f,.045f),.14f),
                Mat("Car Green",new Color(.075f,.20f,.105f),.12f),
                Mat("Car Cream",new Color(.58f,.54f,.44f),.12f),
                Mat("Car Gray",new Color(.21f,.23f,.24f),.14f),
                Mat("Car Brown",new Color(.28f,.13f,.065f),.10f),
                Mat("Car Teal",new Color(.055f,.23f,.23f),.12f)
            };
            return m;
        }

        static Material Mat(string name, Color color, float smoothness)
        {
            Ensure(Gen + "/Materials");
            string path = Gen + "/Materials/" + Safe(name) + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader){ name="H51 "+name, color=color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic",0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",smoothness);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        static int RebuildNavMeshes(Transform citizenRoot)
        {
            NavMeshSurface[] surfaces = citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);
            int built=0;
            for(int i=0;i<surfaces.Length;i++)
            {
                NavMeshSurface surface=surfaces[i];
                if(!surface) continue;
                surface.RemoveData();
                surface.BuildNavMesh();
                if(surface.navMeshData) built++;
                EditorUtility.SetDirty(surface);
            }
            return built;
        }

        static int StableHash(string value)
        {
            unchecked
            {
                int hash=23;
                if(string.IsNullOrEmpty(value)) return hash;
                for(int i=0;i<value.Length;i++) hash=hash*31+value[i];
                return hash & 0x7fffffff;
            }
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x=a.x-b.x,z=a.z-b.z;
            return Mathf.Sqrt(x*x+z*z);
        }

        static Terrain FindTerrain()
        {
            GameObject g=Find(TerrainName);
            Terrain t=g?(g.GetComponent<Terrain>()??g.GetComponentInChildren<Terrain>(true)):null;
            if(t) return t;
            Terrain[] all=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            return all.Length>0?all[0]:null;
        }

        static GameObject Find(string name)
        {
            GameObject direct=GameObject.Find(name);
            if(direct) return direct;
            Transform[] all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            for(int i=0;i<all.Length;i++)
                if(all[i]&&all[i].gameObject.scene.IsValid()&&all[i].name==name)
                    return all[i].gameObject;
            return null;
        }

        static Transform FindChild(Transform root,string name)
        {
            if(!root) return null;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name) return t;
            return null;
        }

        static Transform New(string name,Transform parent)
        {
            GameObject g=new GameObject(name);
            g.transform.SetParent(parent,false);
            return g.transform;
        }

        static void Ensure(string path)
        {
            string[] parts=path.Split('/');
            string current=parts[0];
            for(int i=1;i<parts.Length;i++)
            {
                string next=current+"/"+parts[i];
                if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]);
                current=next;
            }
        }

        static string Safe(string value)
        {
            if(string.IsNullOrEmpty(value)) return "unnamed";
            foreach(char c in System.IO.Path.GetInvalidFileNameChars()) value=value.Replace(c,'_');
            return value.Replace(' ','_').Replace('/','_').Replace('\\','_');
        }
    }
}
