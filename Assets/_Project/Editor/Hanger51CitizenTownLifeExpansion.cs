using System;
using System.Collections.Generic;
using System.Linq;
using Hanger51.NPCs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenTownLifeExpansion
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string WorldExpansionName = "H51 Living Town Destinations";
        const string AnchorExpansionName = "H51 Living Town Destination Anchors";
        const string Generated = "Assets/_Project/Characters/H51Citizens/Generated/TownLifeExpansion";
        const string DepthTextShaderPath = "Assets/_Project/Shaders/Hanger51DepthWorldText.shader";

        struct Mats
        {
            public Material grass;
            public Material path;
            public Material wood;
            public Material metal;
            public Material wall;
            public Material darkWall;
            public Material glass;
            public Material red;
            public Material asphalt;
            public Material line;
            public Material theater;
            public Material medical;
            public Material service;
            public Material truck;
            public Material tire;
            public Material chrome;
        }

        [MenuItem("Hanger 51/World/Current/144 - Add Interactive Homes And Living Town Destinations")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 144 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51CitizenSocialGroundingPass.Build();

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            H51CitizenRegistry registry = citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;
            H51TrafficNetwork traffic = UnityEngine.Object.FindFirstObjectByType<H51TrafficNetwork>();

            if (!world || !terrain || !citizenRoot || !registry || !traffic)
            {
                Debug.LogError("Step 144 failed. The current countryside, citizens, terrain, registry, or traffic network is missing.");
                return;
            }

            try
            {
                Ensure(Generated + "/Materials");
                Mats mats = BuildMaterials();

                EditorUtility.DisplayProgressBar("Hanger 51 Town Life", "Making home doors interactive", .08f);
                int doors = SetupInteractiveHomeDoors(world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Town Life", "Resetting previous living-town destinations", .16f);
                Transform oldWorld = FindChild(world.transform, WorldExpansionName);
                if (oldWorld) UnityEngine.Object.DestroyImmediate(oldWorld.gameObject);
                Transform oldAnchors = FindChild(citizenRoot, AnchorExpansionName);
                if (oldAnchors) UnityEngine.Object.DestroyImmediate(oldAnchors.gameObject);

                H51RoadsideAssistanceController oldRoadside =
                    citizenRoot.GetComponent<H51RoadsideAssistanceController>();
                if (oldRoadside) UnityEngine.Object.DestroyImmediate(oldRoadside);

                Transform worldRoot = New(WorldExpansionName, world.transform);
                Transform anchorRoot = New(AnchorExpansionName, citizenRoot);

                H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
                H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);

                List<string> towns = citizens
                    .Where(c => c && !string.IsNullOrWhiteSpace(c.TownName))
                    .Select(c => c.TownName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                if (towns.Count == 0)
                {
                    Debug.LogError("Step 144 failed. No populated towns were found.");
                    return;
                }

                Physics.SyncTransforms();

                int parks = 0;
                int entertainment = 0;
                int medical = 0;
                int workAnchors = 0;
                int parkingSpots = 0;
                int reassignedWorkers = 0;
                Transform regionalTowTruck = null;
                Vector3 regionalTowBase = Vector3.zero;
                Quaternion regionalTowRotation = Quaternion.identity;

                for (int i = 0; i < towns.Count; i++)
                {
                    string town = towns[i];
                    Vector3 center = TownCenter(town, buildings, citizens);
                    center.y = Ground(terrain, center);

                    Transform townRoot = New(town + " Life Destinations", worldRoot);

                    EditorUtility.DisplayProgressBar(
                        "Hanger 51 Town Life",
                        "Building destinations for " + town,
                        .20f + .55f * (i / Mathf.Max(1f, towns.Count)));

                    Vector3 parkSite = FindClearSite(
                        terrain, center, 52f, i * 101 + 11, new Vector3(13f, 2f, 11f));
                    Transform park = BuildPark(townRoot, town, parkSite, center, mats);
                    H51CitizenAnchor parkAnchor = CreateAnchor(
                        anchorRoot, town, "Town Park", H51CitizenAnchorKind.Park,
                        parkSite, "Resident", 18, i * 100 + 1);
                    AddActivity(parkAnchor.transform, new Vector3(-4f,0f,1.5f), Quaternion.Euler(0,35,0), "relaxing on a park bench", 5f, 12f);
                    AddActivity(parkAnchor.transform, new Vector3(3.5f,0f,-1.5f), Quaternion.Euler(0,-45,0), "taking a walk through the park", 4f, 10f);
                    AddActivity(parkAnchor.transform, new Vector3(0f,0f,4f), Quaternion.identity, "chatting with people at the park", 5f, 11f);
                    parks++;
                    parkingSpots += BuildParking(townRoot, traffic, town, "park", parkSite, park.rotation, 2, mats);

                    Vector3 theaterSite = FindClearSite(
                        terrain, center, 82f, i * 101 + 29, new Vector3(11f, 4f, 9f));
                    Transform theater = BuildTheater(townRoot, town, theaterSite, center, mats, i == 0);
                    Vector3 theaterFront = theaterSite + theater.forward * 8.5f;
                    theaterFront.y = Ground(terrain, theaterFront);
                    H51CitizenAnchor theaterAnchor = CreateAnchor(
                        anchorRoot, town, i == 0 ? "Cinema" : "Community Theater",
                        H51CitizenAnchorKind.Entertainment, theaterFront, "Resident", 20, i * 100 + 2);
                    AddActivity(theaterAnchor.transform, new Vector3(-2.2f,0f,0f), theater.rotation, "waiting for the next show", 5f, 12f);
                    AddActivity(theaterAnchor.transform, new Vector3(2.2f,0f,0f), theater.rotation, "meeting friends at the theater", 5f, 12f);
                    AddActivity(theaterAnchor.transform, new Vector3(0f,0f,-1.8f), theater.rotation, "checking the movie listings", 3f, 7f);
                    H51CitizenAnchor theaterWork = CreateAnchor(
                        anchorRoot, town, "Theater Staff",
                        H51CitizenAnchorKind.Work, theaterFront + theater.right * 3f,
                        "Theater Attendant", 2, i * 100 + 3);
                    AddActivity(theaterWork.transform, Vector3.zero, theater.rotation, "checking tickets at the theater", 6f, 14f);
                    AddActivity(theaterWork.transform, new Vector3(1.7f,0f,-1f), theater.rotation, "straightening up the theater entrance", 6f, 14f);
                    entertainment++;
                    workAnchors++;
                    parkingSpots += BuildParking(townRoot, traffic, town, "theater", theaterSite + theater.forward * 11f, theater.rotation, 4, mats);

                    Vector3 recSite = FindClearSite(
                        terrain, center, 108f, i * 101 + 47, new Vector3(13f, 2f, 10f));
                    Transform recreation = BuildRecreation(townRoot, town, recSite, center, mats);
                    H51CitizenAnchor recAnchor = CreateAnchor(
                        anchorRoot, town, "Recreation Grounds",
                        H51CitizenAnchorKind.Entertainment, recSite, "Resident", 20, i * 100 + 4);
                    AddActivity(recAnchor.transform, new Vector3(-4f,0f,0f), recreation.rotation, "spending time at the recreation grounds", 5f, 12f);
                    AddActivity(recAnchor.transform, new Vector3(4f,0f,0f), recreation.rotation, "watching a pickup game", 5f, 12f);
                    AddActivity(recAnchor.transform, new Vector3(0f,0f,3f), recreation.rotation, "getting some fresh air", 4f, 10f);
                    entertainment++;
                    parkingSpots += BuildParking(townRoot, traffic, town, "recreation", recSite + recreation.forward * 12f, recreation.rotation, 3, mats);

                    Vector3 medicalSite = FindClearSite(
                        terrain, center, 136f, i * 101 + 63, new Vector3(13f, 5f, 10f));
                    Transform hospital = BuildMedicalCenter(townRoot, town, medicalSite, center, mats, i == 0);
                    Vector3 medicalFront = medicalSite + hospital.forward * 10.2f;
                    medicalFront.y = Ground(terrain, medicalFront);

                    H51CitizenAnchor medicalAnchor = CreateAnchor(
                        anchorRoot, town, i == 0 ? "Regional Hospital" : "Medical Clinic",
                        H51CitizenAnchorKind.Medical, medicalFront, "Patient", 18, i * 100 + 5);
                    AddActivity(medicalAnchor.transform, new Vector3(-2f,0f,0f), hospital.rotation, "waiting for a medical appointment", 7f, 16f);
                    AddActivity(medicalAnchor.transform, new Vector3(2f,0f,0f), hospital.rotation, "checking in for an appointment", 4f, 9f);
                    AddActivity(medicalAnchor.transform, new Vector3(0f,0f,-1.5f), hospital.rotation, "leaving a doctor's appointment", 3f, 7f);
                    medical++;

                    H51CitizenAnchor doctorWork = CreateAnchor(
                        anchorRoot, town, "Doctor Station",
                        H51CitizenAnchorKind.Work, medicalFront - hospital.forward * 2.5f - hospital.right * 2.5f,
                        "Doctor", i == 0 ? 4 : 2, i * 100 + 6);
                    AddActivity(doctorWork.transform, Vector3.zero, hospital.rotation, "reviewing a patient's chart", 7f, 16f);
                    AddActivity(doctorWork.transform, new Vector3(1.2f,0f,-.8f), hospital.rotation, "checking on a patient", 7f, 16f);

                    H51CitizenAnchor nurseWork = CreateAnchor(
                        anchorRoot, town, "Nurse Station",
                        H51CitizenAnchorKind.Work, medicalFront - hospital.forward * 2.5f + hospital.right * 2.5f,
                        "Nurse", i == 0 ? 5 : 3, i * 100 + 7);
                    AddActivity(nurseWork.transform, Vector3.zero, hospital.rotation, "preparing medical supplies", 6f, 14f);
                    AddActivity(nurseWork.transform, new Vector3(-1.2f,0f,-.8f), hospital.rotation, "checking the treatment area", 6f, 14f);
                    workAnchors += 2;
                    parkingSpots += BuildParking(townRoot, traffic, town, "medical", medicalSite + hospital.forward * 13f, hospital.rotation, i == 0 ? 6 : 4, mats);

                    reassignedWorkers += AssignWorker(citizens, town, doctorWork, "Doctor");
                    reassignedWorkers += AssignWorker(citizens, town, nurseWork, "Nurse");
                    reassignedWorkers += AssignWorker(citizens, town, theaterWork, "Theater Attendant");

                    if (i == 0)
                    {
                        Vector3 serviceSite = FindClearSite(
                            terrain, center, 164f, 91, new Vector3(12f, 4f, 10f));
                        Transform service = BuildRoadsideService(townRoot, town, serviceSite, center, mats);
                        Vector3 serviceFront = serviceSite + service.forward * 10f;
                        serviceFront.y = Ground(terrain, serviceFront);

                        H51CitizenAnchor towWork = CreateAnchor(
                            anchorRoot, town, "Roadside Service",
                            H51CitizenAnchorKind.Work, serviceFront,
                            "Tow Operator", 3, 9001);
                        AddActivity(towWork.transform, Vector3.zero, service.rotation, "checking the tow truck", 6f, 14f);
                        AddActivity(towWork.transform, new Vector3(2.2f,0f,-1f), service.rotation, "working around the roadside service garage", 6f, 14f);
                        workAnchors++;
                        reassignedWorkers += AssignWorker(citizens, town, towWork, "Tow Operator");

                        regionalTowBase = serviceSite + service.forward * 11.5f + service.right * 3.2f;
                        regionalTowBase.y = Ground(terrain, regionalTowBase) + .05f;
                        regionalTowRotation = service.rotation;
                        regionalTowTruck = BuildTowTruck(townRoot, regionalTowBase, regionalTowRotation, mats);

                        parkingSpots += BuildParking(townRoot, traffic, town, "roadside", serviceSite + service.forward * 13f, service.rotation, 3, mats);
                    }
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Town Life", "Connecting roadside assistance and refreshing schedules", .82f);

                if (regionalTowTruck)
                {
                    H51RoadsideAssistanceController roadside =
                        citizenRoot.gameObject.AddComponent<H51RoadsideAssistanceController>();
                    roadside.Configure(traffic, regionalTowTruck, regionalTowBase, regionalTowRotation);
                    EditorUtility.SetDirty(roadside);
                }

                registry.RefreshSceneReferences();

                H51CitizenBrain[] brains = citizenRoot.GetComponentsInChildren<H51CitizenBrain>(true);
                for (int i = 0; i < brains.Length; i++)
                    if (brains[i]) EditorUtility.SetDirty(brains[i]);

                EditorUtility.DisplayProgressBar("Hanger 51 Town Life", "Saving living-town expansion", .94f);
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();

                Selection.activeGameObject = worldRoot.gameObject;

                Debug.Log(
                    $"Step 144 complete. interactive home doors={doors}, towns={towns.Count}, parks={parks}, entertainment destinations={entertainment}, medical destinations={medical}, new work anchors={workAnchors}, destination parking spaces={parkingSpots}, citizens reassigned into new town jobs={reassignedWorkers}, regional tow service={(regionalTowTruck ? "active" : "missing")}. Citizens now rotate between multiple leisure destinations and can receive scheduled medical visits. Run Step 145.",
                    worldRoot.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/145 - Validate Interactive Homes And Living Town Destinations")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform worldRoot = world ? FindChild(world.transform, WorldExpansionName) : null;
            Transform anchorRoot = citizenRoot ? FindChild(citizenRoot, AnchorExpansionName) : null;

            if (!world || !citizenRoot || !worldRoot || !anchorRoot)
            {
                Debug.LogError("Step 145 failed. Run Step 144 first.");
                return;
            }

            H51EnterableBuilding[] homes = world.GetComponentsInChildren<H51EnterableBuilding>(true)
                .Where(b => b && b.Kind == H51EnterableBuildingKind.Home)
                .ToArray();
            H51TownDoor[] doors = world.GetComponentsInChildren<H51TownDoor>(true);
            H51CitizenAnchor[] anchors = anchorRoot.GetComponentsInChildren<H51CitizenAnchor>(true);
            H51ParkingSpot[] parking = worldRoot.GetComponentsInChildren<H51ParkingSpot>(true);
            H51RoadsideAssistanceController roadside =
                citizenRoot.GetComponent<H51RoadsideAssistanceController>();

            int parks = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Park);
            int entertainment = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Entertainment);
            int medical = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Medical);
            int doctors = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Work && a.OccupationHint == "Doctor");
            int nurses = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Work && a.OccupationHint == "Nurse");
            int activities = anchorRoot.GetComponentsInChildren<H51CitizenActivitySpot>(true).Length;

            List<string> towns = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true)
                .Where(c => c && !string.IsNullOrWhiteSpace(c.TownName))
                .Select(c => c.TownName)
                .Distinct()
                .ToList();

            bool ok =
                homes.Length > 0 &&
                doors.Length >= homes.Length &&
                parks >= towns.Count &&
                entertainment >= towns.Count * 2 &&
                medical >= towns.Count &&
                doctors >= towns.Count &&
                nurses >= towns.Count &&
                activities >= towns.Count * 10 &&
                parking.Length >= towns.Count * 8 &&
                roadside;

            if (ok)
            {
                Debug.Log(
                    $"Step 145 passed. home doors={doors.Length}/{homes.Length}, towns={towns.Count}, parks={parks}, entertainment={entertainment}, medical={medical}, doctor anchors={doctors}, nurse anchors={nurses}, life activity spots={activities}, new parking={parking.Length}, roadside/tow dispatch=active.",
                    worldRoot.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 145 failed. home doors={doors.Length}/{homes.Length}, towns={towns.Count}, parks={parks}, entertainment={entertainment}, medical={medical}, doctor anchors={doctors}, nurse anchors={nurses}, life activity spots={activities}, new parking={parking.Length}, roadside/tow dispatch={(roadside ? "active" : "MISSING")}.",
                    worldRoot.gameObject);
            }
        }

        static int SetupInteractiveHomeDoors(Transform world)
        {
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            int configured = 0;

            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building || building.Kind != H51EnterableBuildingKind.Home) continue;

                Transform door = FindChild(building.transform, "Open Door");
                if (!door) continue;

                Transform knob = FindChild(building.transform, "Door Knob");
                if (knob && knob.parent != door)
                    knob.SetParent(door, true);

                H51TownDoor interactive = door.GetComponent<H51TownDoor>();
                if (!interactive) interactive = door.gameObject.AddComponent<H51TownDoor>();

                Vector3 openPosition = door.localPosition;
                Quaternion openRotation = door.localRotation;
                float halfWidth = Mathf.Max(.25f, Mathf.Abs(door.localScale.x) * .5f);
                Vector3 hinge = openPosition - openRotation * Vector3.right * halfWidth;
                Vector3 closedPosition = hinge + Vector3.right * halfWidth;
                Quaternion closedRotation = Quaternion.identity;

                interactive.Configure(
                    closedPosition,
                    closedRotation,
                    openPosition,
                    openRotation,
                    true);

                EditorUtility.SetDirty(interactive);
                EditorUtility.SetDirty(door);
                configured++;
            }

            return configured;
        }

        static Transform BuildPark(Transform parent, string town, Vector3 site, Vector3 townCenter, Mats m)
        {
            Transform root = New(town + " Town Park", parent);
            root.position = site;
            root.rotation = FaceToward(site, townCenter);

            Box(root, "Park Lawn", new Vector3(0,.04f,0), new Vector3(24f,.08f,18f), m.grass, true);
            Box(root, "Walking Path A", new Vector3(0,.09f,0), new Vector3(3f,.04f,18f), m.path, false);
            Box(root, "Walking Path B", new Vector3(0,.095f,0), new Vector3(24f,.04f,2.4f), m.path, false);

            for (int side = -1; side <= 1; side += 2)
            {
                Bench(root, new Vector3(side * 5.2f,.12f,3.2f), Quaternion.Euler(0, side < 0 ? 90f : -90f, 0), m);
                Bench(root, new Vector3(side * 5.2f,.12f,-3.2f), Quaternion.Euler(0, side < 0 ? 90f : -90f, 0), m);
                Tree(root, new Vector3(side * 8f,0f,5.8f), m);
                Tree(root, new Vector3(side * 8f,0f,-5.8f), m);
            }

            WorldText(root, town + " PARK", new Vector3(0,2.5f,8.3f), Quaternion.Euler(0,180,0), .24f);
            return root;
        }

        static Transform BuildTheater(Transform parent, string town, Vector3 site, Vector3 townCenter, Mats m, bool cinema)
        {
            Transform root = New(town + (cinema ? " Cinema" : " Community Theater"), parent);
            root.position = site;
            root.rotation = FaceToward(site, townCenter);

            Box(root, "Theater Building", new Vector3(0,3.2f,0), new Vector3(18f,6.4f,13f), m.theater, true);
            Box(root, "Theater Roof", new Vector3(0,6.55f,0), new Vector3(19f,.35f,14f), m.darkWall, true);
            Box(root, "Marquee", new Vector3(0,4.5f,6.9f), new Vector3(11f,1.25f,1.6f), m.metal, true);
            Box(root, "Entrance Glass", new Vector3(0,1.6f,6.58f), new Vector3(5.6f,3.2f,.18f), m.glass, true);
            Box(root, "Left Poster Case", new Vector3(-5.4f,2.1f,6.62f), new Vector3(2.2f,3.1f,.18f), m.glass, false);
            Box(root, "Right Poster Case", new Vector3(5.4f,2.1f,6.62f), new Vector3(2.2f,3.1f,.18f), m.glass, false);
            WorldText(root, cinema ? "CINEMA" : "THEATER", new Vector3(0,5.0f,7.75f), Quaternion.Euler(0,180,0), .30f);

            H51CitizenWorkplace workplace = root.gameObject.AddComponent<H51CitizenWorkplace>();
            workplace.Configure(
                "life-" + Safe(town) + "-theater",
                cinema ? town + " Cinema" : town + " Community Theater",
                town,
                "Entertainment",
                "Theater Attendant",
                3);
            return root;
        }

        static Transform BuildRecreation(Transform parent, string town, Vector3 site, Vector3 townCenter, Mats m)
        {
            Transform root = New(town + " Recreation Grounds", parent);
            root.position = site;
            root.rotation = FaceToward(site, townCenter);

            Box(root, "Recreation Surface", new Vector3(0,.05f,0), new Vector3(23f,.10f,15f), m.path, true);
            Box(root, "Center Line", new Vector3(0,.115f,0), new Vector3(.12f,.02f,14f), m.line, false);
            Box(root, "Side Line Left", new Vector3(-10.7f,.115f,0), new Vector3(.10f,.02f,14f), m.line, false);
            Box(root, "Side Line Right", new Vector3(10.7f,.115f,0), new Vector3(.10f,.02f,14f), m.line, false);
            Bench(root, new Vector3(-7.5f,.12f,8.3f), Quaternion.Euler(0,180,0), m);
            Bench(root, new Vector3(0,.12f,8.3f), Quaternion.Euler(0,180,0), m);
            Bench(root, new Vector3(7.5f,.12f,8.3f), Quaternion.Euler(0,180,0), m);
            WorldText(root, "RECREATION", new Vector3(0,2.2f,-7.2f), Quaternion.identity, .22f);
            return root;
        }

        static Transform BuildMedicalCenter(Transform parent, string town, Vector3 site, Vector3 townCenter, Mats m, bool hospital)
        {
            Transform root = New(town + (hospital ? " Regional Hospital" : " Medical Clinic"), parent);
            root.position = site;
            root.rotation = FaceToward(site, townCenter);

            Vector3 size = hospital ? new Vector3(23f,8f,15f) : new Vector3(18f,6.5f,13f);
            Box(root, "Medical Building", new Vector3(0,size.y*.5f,0), size, m.medical, true);
            Box(root, "Medical Roof", new Vector3(0,size.y+.18f,0), new Vector3(size.x+1f,.35f,size.z+1f), m.darkWall, true);
            Box(root, "Medical Entrance", new Vector3(0,1.7f,size.z*.5f+.10f), new Vector3(5.4f,3.4f,.20f), m.glass, true);
            Box(root, "Entry Canopy", new Vector3(0,3.7f,size.z*.5f+1.2f), new Vector3(7f,.28f,2.4f), m.metal, true);

            // Red cross made from two simple blocks.
            Box(root, "Medical Cross Vertical", new Vector3(-size.x*.34f,size.y*.68f,size.z*.5f+.16f), new Vector3(.65f,2.6f,.16f), m.red, false);
            Box(root, "Medical Cross Horizontal", new Vector3(-size.x*.34f,size.y*.68f,size.z*.5f+.17f), new Vector3(2.6f,.65f,.16f), m.red, false);

            WorldText(
                root,
                hospital ? "REGIONAL HOSPITAL" : "MEDICAL CLINIC",
                new Vector3(1.7f,size.y*.72f,size.z*.5f+.22f),
                Quaternion.Euler(0,180,0),
                hospital ? .20f : .19f);

            H51CitizenWorkplace workplace = root.gameObject.AddComponent<H51CitizenWorkplace>();
            workplace.Configure(
                "life-" + Safe(town) + "-medical",
                hospital ? town + " Regional Hospital" : town + " Medical Clinic",
                town,
                "Medical",
                "Doctor",
                hospital ? 10 : 5);
            return root;
        }

        static Transform BuildRoadsideService(Transform parent, string town, Vector3 site, Vector3 townCenter, Mats m)
        {
            Transform root = New(town + " Roadside And Tow Service", parent);
            root.position = site;
            root.rotation = FaceToward(site, townCenter);

            Box(root, "Service Building", new Vector3(0,3f,0), new Vector3(18f,6f,13f), m.service, true);
            Box(root, "Service Roof", new Vector3(0,6.2f,0), new Vector3(19f,.35f,14f), m.darkWall, true);
            Box(root, "Tow Bay Opening", new Vector3(-3.8f,2.1f,6.58f), new Vector3(5.5f,4.2f,.18f), m.darkWall, false);
            Box(root, "Office Window", new Vector3(4.7f,2.2f,6.60f), new Vector3(3.2f,2.5f,.16f), m.glass, false);
            WorldText(root, "TOW & ROADSIDE", new Vector3(0,5.0f,6.72f), Quaternion.Euler(0,180,0), .20f);

            H51CitizenWorkplace workplace = root.gameObject.AddComponent<H51CitizenWorkplace>();
            workplace.Configure(
                "life-" + Safe(town) + "-roadside",
                town + " Roadside And Tow Service",
                town,
                "Roadside Service",
                "Tow Operator",
                4);
            return root;
        }

        static Transform BuildTowTruck(Transform parent, Vector3 position, Quaternion rotation, Mats m)
        {
            Transform root = New("Regional Tow Truck", parent);
            root.position = position;
            root.rotation = rotation;

            Box(root, "Truck Chassis", new Vector3(0,.65f,0), new Vector3(2.3f,.55f,5.7f), m.metal, true);
            Box(root, "Truck Cab", new Vector3(0,1.55f,1.45f), new Vector3(2.25f,1.65f,2.25f), m.truck, true);
            Box(root, "Windshield", new Vector3(0,1.75f,2.60f), new Vector3(1.65f,.75f,.08f), m.glass, false);
            Box(root, "Tow Bed", new Vector3(0,1.02f,-1.2f), new Vector3(2.15f,.22f,3.0f), m.darkWall, true);
            Box(root, "Tow Boom", new Vector3(0,2.15f,-1.10f), new Vector3(.20f,.20f,2.7f), m.chrome, false, Quaternion.Euler(22f,0,0));
            Box(root, "Tow Crossbar", new Vector3(0,1.25f,-2.75f), new Vector3(2.3f,.18f,.22f), m.chrome, false);

            Wheel(root, new Vector3(-1.12f,.55f,1.55f), m);
            Wheel(root, new Vector3(1.12f,.55f,1.55f), m);
            Wheel(root, new Vector3(-1.12f,.55f,-1.55f), m);
            Wheel(root, new Vector3(1.12f,.55f,-1.55f), m);

            return root;
        }

        static int BuildParking(
            Transform parent,
            H51TrafficNetwork traffic,
            string town,
            string idPrefix,
            Vector3 center,
            Quaternion rotation,
            int count,
            Mats m)
        {
            Transform root = New("Parking - " + town + " - " + idPrefix, parent);
            root.position = center;
            root.rotation = rotation;

            float width = Mathf.Max(8f, count * 3.0f + 2f);
            Box(root, "Parking Surface", new Vector3(0,.035f,0), new Vector3(width,.07f,6.5f), m.asphalt, true);

            int nearest = traffic.FindNearestNode(center);
            Vector3 roadApproach = nearest >= 0 ? traffic.GetNode(nearest) : center + root.forward * 8f;

            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * .5f) * 3.0f;
                Transform spot = New("Parking Spot " + (i + 1), root);
                spot.localPosition = new Vector3(x,.08f,0);
                spot.localRotation = Quaternion.identity;

                Box(spot, "Left Line", new Vector3(-1.25f,.015f,0), new Vector3(.08f,.025f,5.4f), m.line, false);
                Box(spot, "Right Line", new Vector3(1.25f,.015f,0), new Vector3(.08f,.025f,5.4f), m.line, false);

                H51ParkingSpot parking = spot.gameObject.AddComponent<H51ParkingSpot>();
                parking.Configure(
                    "life-" + Safe(town) + "-" + idPrefix + "-" + i,
                    "life-" + Safe(town) + "-" + idPrefix,
                    town,
                    roadApproach);
                EditorUtility.SetDirty(parking);
            }

            return count;
        }

        static H51CitizenAnchor CreateAnchor(
            Transform parent,
            string town,
            string label,
            H51CitizenAnchorKind kind,
            Vector3 position,
            string occupation,
            int capacity,
            int seed)
        {
            Transform t = New(label + " Anchor", parent);
            t.position = position;

            H51CitizenAnchor anchor = t.gameObject.AddComponent<H51CitizenAnchor>();
            anchor.Configure(
                "life-" + Safe(town) + "-" + kind.ToString().ToLowerInvariant() + "-" + seed,
                town,
                kind,
                occupation,
                capacity,
                label,
                string.Empty);
            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        static void AddActivity(
            Transform anchor,
            Vector3 localPosition,
            Quaternion rotation,
            string label,
            float min,
            float max)
        {
            Transform t = New("Activity - " + label, anchor);
            t.localPosition = localPosition;
            t.localRotation = rotation;
            H51CitizenActivitySpot spot = t.gameObject.AddComponent<H51CitizenActivitySpot>();
            spot.Configure(label, "Interact", min, max);
            EditorUtility.SetDirty(spot);
        }

        static int AssignWorker(
            H51CitizenIdentity[] citizens,
            string town,
            H51CitizenAnchor work,
            string occupation)
        {
            if (!work) return 0;

            H51CitizenIdentity chosen = citizens
                .Where(c => c &&
                            c.TownName == town &&
                            (c.Occupation == "Resident" ||
                             string.IsNullOrWhiteSpace(c.WorkAnchorId)))
                .OrderBy(c => StableHash(c.CitizenId + occupation))
                .FirstOrDefault();

            if (!chosen)
            {
                chosen = citizens
                    .Where(c => c &&
                                c.TownName == town &&
                                (c.Occupation.Contains("Community") ||
                                 c.Occupation.Contains("Town Worker")))
                    .OrderBy(c => StableHash(c.CitizenId + occupation))
                    .FirstOrDefault();
            }

            if (!chosen) return 0;

            chosen.SetWorkAssignment(work.AnchorId, occupation);
            EditorUtility.SetDirty(chosen);
            return 1;
        }

        static Vector3 TownCenter(
            string town,
            H51EnterableBuilding[] buildings,
            H51CitizenIdentity[] citizens)
        {
            List<Vector3> points = new List<Vector3>();

            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding b = buildings[i];
                if (b && b.TownName == town && b.Kind == H51EnterableBuildingKind.Home)
                    points.Add(b.transform.position);
            }

            if (points.Count == 0)
            {
                for (int i = 0; i < citizens.Length; i++)
                    if (citizens[i] && citizens[i].TownName == town)
                        points.Add(citizens[i].transform.position);
            }

            if (points.Count == 0) return Vector3.zero;

            Vector3 total = Vector3.zero;
            for (int i = 0; i < points.Count; i++) total += points[i];
            return total / points.Count;
        }

        static Vector3 FindClearSite(
            Terrain terrain,
            Vector3 center,
            float baseRadius,
            int seed,
            Vector3 halfExtents)
        {
            System.Random rng = new System.Random(8171 + seed);

            for (int ring = 0; ring < 6; ring++)
            {
                float radius = baseRadius + ring * 22f;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float angle = ((attempt / 20f) * 360f + (float)rng.NextDouble() * 14f) * Mathf.Deg2Rad;
                    Vector3 p = center + new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle)) * radius;
                    p.y = Ground(terrain, p);

                    Quaternion rotation = FaceToward(p, center);
                    Collider[] hits = Physics.OverlapBox(
                        p + Vector3.up * halfExtents.y,
                        halfExtents,
                        rotation,
                        ~0,
                        QueryTriggerInteraction.Ignore);

                    bool blocked = false;
                    for (int h = 0; h < hits.Length; h++)
                    {
                        Collider hit = hits[h];
                        if (!hit || hit is TerrainCollider) continue;

                        string n = hit.name.ToLowerInvariant();
                        if (n.Contains("detail") || n.Contains("grass") || n.Contains("tree"))
                            continue;

                        blocked = true;
                        break;
                    }

                    if (!blocked) return p;
                }
            }

            Vector3 fallback = center + new Vector3(baseRadius + 120f,0f,seed % 2 == 0 ? 90f : -90f);
            fallback.y = Ground(terrain, fallback);
            return fallback;
        }

        static Quaternion FaceToward(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) return Quaternion.identity;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        static void Bench(Transform parent, Vector3 position, Quaternion rotation, Mats m)
        {
            Transform root = New("Park Bench", parent);
            root.localPosition = position;
            root.localRotation = rotation;
            Box(root, "Seat", new Vector3(0,.58f,0), new Vector3(2.2f,.14f,.62f), m.wood, true);
            Box(root, "Back", new Vector3(0,1.03f,-.27f), new Vector3(2.2f,.75f,.12f), m.wood, true);
            Box(root, "Left Leg", new Vector3(-.78f,.28f,0), new Vector3(.12f,.56f,.50f), m.metal, true);
            Box(root, "Right Leg", new Vector3(.78f,.28f,0), new Vector3(.12f,.56f,.50f), m.metal, true);
        }

        static void Tree(Transform parent, Vector3 position, Mats m)
        {
            Transform root = New("Park Tree", parent);
            root.localPosition = position;
            Box(root, "Trunk", new Vector3(0,1.5f,0), new Vector3(.55f,3f,.55f), m.wood, true);
            Sphere(root, "Canopy A", new Vector3(0,3.7f,0), 1.55f, m.grass);
            Sphere(root, "Canopy B", new Vector3(.75f,3.5f,.15f), 1.05f, m.grass);
            Sphere(root, "Canopy C", new Vector3(-.72f,3.5f,-.12f), 1.0f, m.grass);
        }

        static void Wheel(Transform parent, Vector3 localPosition, Mats m)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Tow Truck Wheel";
            go.transform.SetParent(parent,false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0,0,90);
            go.transform.localScale = new Vector3(.52f,.22f,.52f);
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = m.tire;

            Transform hub = New("Wheel Hub", go.transform);
            hub.localPosition = Vector3.zero;
            Sphere(hub, "Hub", Vector3.zero, .26f, m.chrome);
        }

        static Transform Box(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool collider,
            Quaternion? localRotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;

            Collider c = go.GetComponent<Collider>();
            if (!collider && c) UnityEngine.Object.DestroyImmediate(c);
            return go.transform;
        }

        static Transform Sphere(
            Transform parent,
            string name,
            Vector3 localPosition,
            float scale,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider c = go.GetComponent<Collider>();
            if (c) UnityEngine.Object.DestroyImmediate(c);
            return go.transform;
        }

        static void WorldText(
            Transform parent,
            string text,
            Vector3 localPosition,
            Quaternion localRotation,
            float size)
        {
            Transform t = New("World Label - " + text, parent);
            t.localPosition = localPosition;
            t.localRotation = localRotation;

            TextMesh mesh = t.gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.color = Color.white;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font) mesh.font = font;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(DepthTextShaderPath)
                            ?? Shader.Find("Hanger51/Depth World Text");

            if (!shader || !font || !font.material) return;

            string path = Generated + "/Materials/TownLifeDepthText.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader) { name = "Town Life Depth Text" };
                mat.SetTexture("_MainTex", font.material.mainTexture);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", .02f);
                mat.renderQueue = 2450;
                AssetDatabase.CreateAsset(mat,path);
            }

            Renderer renderer = mesh.GetComponent<Renderer>();
            if (renderer)
            {
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static Mats BuildMaterials()
        {
            return new Mats
            {
                grass = Mat("Park_Grass", new Color(.18f,.42f,.12f)),
                path = Mat("Recreation_Path", new Color(.34f,.35f,.34f)),
                wood = Mat("Town_Wood", new Color(.34f,.18f,.08f)),
                metal = Mat("Town_Metal", new Color(.20f,.23f,.27f)),
                wall = Mat("Town_Wall", new Color(.72f,.72f,.68f)),
                darkWall = Mat("Town_Dark", new Color(.09f,.11f,.14f)),
                glass = Mat("Town_Glass", new Color(.16f,.27f,.34f)),
                red = Mat("Medical_Red", new Color(.72f,.05f,.06f)),
                asphalt = Mat("Town_Asphalt", new Color(.11f,.12f,.13f)),
                line = Mat("Parking_Line", new Color(.95f,.90f,.58f)),
                theater = Mat("Theater_Wall", new Color(.20f,.10f,.15f)),
                medical = Mat("Medical_Wall", new Color(.82f,.85f,.84f)),
                service = Mat("Roadside_Wall", new Color(.28f,.34f,.25f)),
                truck = Mat("Tow_Truck", new Color(.82f,.56f,.08f)),
                tire = Mat("Tow_Tire", new Color(.025f,.025f,.025f)),
                chrome = Mat("Tow_Chrome", new Color(.55f,.58f,.62f))
            };
        }

        static Material Mat(string name, Color color)
        {
            string path = Generated + "/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (!mat)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat,path);
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", .02f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .24f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static float Ground(Terrain terrain, Vector3 p)
        {
            return terrain
                ? terrain.SampleHeight(p) + terrain.transform.position.y
                : p.y;
        }

        static Terrain FindTerrain()
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < terrains.Length; i++)
                if (terrains[i] && terrains[i].gameObject.scene.IsValid())
                    return terrains[i];

            return null;
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
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].name == name)
                    return all[i];
            return null;
        }

        static Transform New(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            if (parent) go.transform.SetParent(parent,false);
            return go.transform;
        }

        static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            string result = value;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c,'_');
            return result.Replace(" ","_");
        }

        static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
                return hash;
            }
        }

        static void Ensure(string path)
        {
            string[] pieces = path.Split('/');
            string current = pieces[0];

            for (int i = 1; i < pieces.Length; i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current,pieces[i]);
                current = next;
            }
        }
    }
}
