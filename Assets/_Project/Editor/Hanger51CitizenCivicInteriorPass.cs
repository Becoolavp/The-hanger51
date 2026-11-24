using System;
using System.Collections.Generic;
using System.Linq;
using Hanger51.NPCs;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenCivicInteriorPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string LivingWorldName = "H51 Living Town Destinations";
        const string LivingAnchorName = "H51 Living Town Destination Anchors";
        const string CivicRootName = "H51 Civic Interior And Police Pass";
        const string CivicAnchorName = "H51 Civic Interior Police Anchors";
        const string CatalogPath = "Assets/_Project/Characters/H51Citizens/Generated/H51CitizenVisualCatalog.asset";
        const string Generated = "Assets/_Project/Characters/H51Citizens/Generated/TownLifeExpansion";
        const string DepthTextShaderPath = "Assets/_Project/Shaders/Hanger51DepthWorldText.shader";

        static readonly string[] FemaleNames =
        {
            "Abigail","Alicia","Anna","Caroline","Claire","Emily","Emma","Evelyn","Grace","Hannah",
            "Julia","Laura","Leah","Maria","Megan","Natalie","Nicole","Olivia","Rachel","Rebecca",
            "Sarah","Sophia","Victoria","Zoe","Molly","Brooke","Erin","Amanda","Chloe","Ella",
            "Lily","Madison","Samantha","Katherine","Kayla","Brianna","Hailey","Allison","Danielle","Paige"
        };

        static readonly string[] MaleNames =
        {
            "Aaron","Adam","Andrew","Ben","Brandon","Charles","Daniel","David","Derek","Ethan",
            "Henry","Jack","James","Jason","John","Kevin","Luke","Matthew","Michael","Nathan",
            "Patrick","Robert","Ryan","Scott","Thomas","William","Caleb","Eric","Noah","Logan",
            "Cole","Jacob","Samuel","Benjamin","Tyler","Connor","Dylan","Evan","Owen","Lucas"
        };

        static readonly string[] NeutralNames =
        {
            "Alex","Morgan","Jordan","Taylor","Casey","Riley","Cameron","Avery","Quinn","Parker",
            "Jamie","Drew","Reese","Skyler","Rowan","Emerson","Hayden","Finley","Dakota","Blake"
        };

        static readonly string[] LastNames =
        {
            "Anderson","Baker","Bennett","Brown","Campbell","Carter","Clark","Collins","Cooper","Davis",
            "Edwards","Evans","Foster","Garcia","Green","Hall","Harris","Hayes","Hill","Hughes",
            "Jackson","Johnson","Kelly","King","Larson","Lewis","Martin","Miller","Mitchell","Moore",
            "Morgan","Nelson","Parker","Peterson","Phillips","Reed","Reynolds","Roberts","Robinson","Rogers",
            "Scott","Smith","Stewart","Taylor","Thomas","Thompson","Turner","Walker","Ward","Williams"
        };

        struct Mats
        {
            public Material floor;
            public Material wall;
            public Material ceiling;
            public Material trim;
            public Material glass;
            public Material wood;
            public Material darkWood;
            public Material metal;
            public Material counter;
            public Material chair;
            public Material blue;
            public Material policeBlue;
            public Material red;
            public Material white;
            public Material black;
            public Material hospital;
            public Material theater;
            public Material service;
            public Material asphalt;
            public Material line;
        }

        [MenuItem("Hanger 51/World/Current/146 - Build Police Station And Walkable Civic Interiors")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 146 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51CitizenTownLifeExpansion.Build();

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform livingWorld = world ? FindChild(world.transform, LivingWorldName) : null;
            Transform livingAnchors = citizenRoot ? FindChild(citizenRoot, LivingAnchorName) : null;
            H51CitizenRegistry registry = citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;
            H51TrafficNetwork traffic = UnityEngine.Object.FindFirstObjectByType<H51TrafficNetwork>();
            H51CitizenVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<H51CitizenVisualCatalog>(CatalogPath);

            if (!world || !terrain || !citizenRoot || !livingWorld || !livingAnchors || !registry || !traffic || !catalog)
            {
                Debug.LogError("Step 146 failed. Run the current citizen/town build chain first so Step 144 and the citizen visual catalog exist.");
                return;
            }

            try
            {
                Ensure(Generated + "/Materials");
                Mats mats = BuildMaterials();

                Transform old = FindChild(world.transform, CivicRootName);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform oldAnchors = FindChild(citizenRoot, CivicAnchorName);
                if (oldAnchors) UnityEngine.Object.DestroyImmediate(oldAnchors.gameObject);

                Transform civicRoot = New(CivicRootName, world.transform);
                Transform civicAnchors = New(CivicAnchorName, citizenRoot);

                H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
                H51CitizenWorkplace[] workplaces = livingWorld.GetComponentsInChildren<H51CitizenWorkplace>(true);
                List<string> towns = citizens
                    .Where(c => c && !string.IsNullOrWhiteSpace(c.TownName))
                    .Select(c => c.TownName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                if (towns.Count == 0)
                {
                    Debug.LogError("Step 146 failed. No populated towns were found.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Civic Interiors", "Rebuilding theater, medical and roadside buildings as walkable interiors", .08f);

                int theaterInteriors = 0;
                int medicalInteriors = 0;
                int serviceInteriors = 0;
                int civicDoors = 0;
                int clearanceMarkers = 0;

                for (int i = 0; i < workplaces.Length; i++)
                {
                    H51CitizenWorkplace workplace = workplaces[i];
                    if (!workplace) continue;

                    if (string.Equals(workplace.Category, "Entertainment", StringComparison.OrdinalIgnoreCase))
                    {
                        RebuildTheater(workplace, livingAnchors, mats, ref civicDoors, ref clearanceMarkers);
                        theaterInteriors++;
                    }
                    else if (string.Equals(workplace.Category, "Medical", StringComparison.OrdinalIgnoreCase))
                    {
                        RebuildMedical(workplace, livingAnchors, mats, ref civicDoors, ref clearanceMarkers);
                        medicalInteriors++;
                    }
                    else if (string.Equals(workplace.Category, "Roadside Service", StringComparison.OrdinalIgnoreCase))
                    {
                        RebuildRoadside(workplace, livingAnchors, mats, ref civicDoors, ref clearanceMarkers);
                        serviceInteriors++;
                    }
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Civic Interiors", "Building the regional police station and SWAT facilities", .38f);

                string primaryTown = towns[0];
                Vector3 primaryCenter = TownCenter(primaryTown, citizens);
                Vector3 policeSite = FindClearSite(
                    terrain,
                    primaryCenter,
                    118f,
                    14651,
                    new Vector3(14f, 5f, 12f));

                Transform policeStation = BuildPoliceStation(
                    civicRoot,
                    primaryTown,
                    policeSite,
                    primaryCenter,
                    mats,
                    ref civicDoors,
                    ref clearanceMarkers);

                H51CitizenAnchor policeWork = CreateAnchor(
                    civicAnchors,
                    primaryTown,
                    "Police Patrol Office",
                    H51CitizenAnchorKind.Work,
                    policeStation.TransformPoint(new Vector3(3.8f, .05f, 1.0f)),
                    policeStation.rotation,
                    "Police Officer",
                    8,
                    14601);
                AddActivity(policeWork.transform, new Vector3(0f,0f,0f), Quaternion.identity, "reviewing patrol reports", 7f, 16f);
                AddActivity(policeWork.transform, new Vector3(-1.8f,0f,-1.0f), Quaternion.identity, "checking dispatch information", 6f, 14f);
                AddActivity(policeWork.transform, new Vector3(1.5f,0f,-1.7f), Quaternion.Euler(0,180,0), "preparing for patrol", 6f, 14f);

                H51CitizenAnchor swatWork = CreateAnchor(
                    civicAnchors,
                    primaryTown,
                    "SWAT Ready Room",
                    H51CitizenAnchorKind.Work,
                    policeStation.TransformPoint(new Vector3(-5.4f, .05f, -3.1f)),
                    policeStation.rotation,
                    "SWAT Officer",
                    6,
                    14602);
                AddActivity(swatWork.transform, Vector3.zero, Quaternion.identity, "checking SWAT equipment", 7f, 16f);
                AddActivity(swatWork.transform, new Vector3(2.1f,0f,0f), Quaternion.identity, "reviewing the tactical briefing board", 7f, 16f);

                H51CitizenAnchor dispatchWork = CreateAnchor(
                    civicAnchors,
                    primaryTown,
                    "Police Dispatch",
                    H51CitizenAnchorKind.Work,
                    policeStation.TransformPoint(new Vector3(4.8f, .05f, -3.7f)),
                    policeStation.rotation,
                    "Police Dispatcher",
                    4,
                    14603);
                AddActivity(dispatchWork.transform, Vector3.zero, Quaternion.identity, "monitoring police dispatch", 8f, 18f);
                AddActivity(dispatchWork.transform, new Vector3(-1.5f,0f,0f), Quaternion.identity, "taking a service call", 6f, 14f);

                H51CitizenAnchor policeVisitor = CreateAnchor(
                    civicAnchors,
                    primaryTown,
                    "Police Station Lobby",
                    H51CitizenAnchorKind.Civic,
                    policeStation.TransformPoint(new Vector3(-3.5f, .05f, 4.2f)),
                    policeStation.rotation,
                    "Visitor",
                    10,
                    14604);
                AddActivity(policeVisitor.transform, Vector3.zero, Quaternion.identity, "waiting in the police station lobby", 5f, 12f);
                AddActivity(policeVisitor.transform, new Vector3(2.0f,0f,0f), Quaternion.identity, "speaking with the front desk officer", 5f, 12f);

                int policeParking = BuildParking(
                    civicRoot,
                    traffic,
                    primaryTown,
                    "police",
                    policeSite + policeStation.forward * 14f,
                    policeStation.rotation,
                    8,
                    mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Civic Interiors", "Staffing doctors, nurses, theater crews, police and SWAT", .58f);

                registry.RefreshSceneReferences();
                citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
                HashSet<string> usedNames = new HashSet<string>(
                    citizens.Where(c => c && !string.IsNullOrWhiteSpace(c.DisplayName)).Select(c => c.DisplayName),
                    StringComparer.OrdinalIgnoreCase);

                int reassigned = 0;
                int spawned = 0;

                for (int i = 0; i < towns.Count; i++)
                {
                    string town = towns[i];
                    H51CitizenAnchor theaterWork = FindAnchor(livingAnchors, town, "Theater Staff");
                    H51CitizenAnchor doctorWork = FindAnchor(livingAnchors, town, "Doctor Station");
                    H51CitizenAnchor nurseWork = FindAnchor(livingAnchors, town, "Nurse Station");

                    if (theaterWork)
                        EnsureRolePopulation(citizenRoot, registry, catalog, town, theaterWork, "Theater Attendant", i == 0 ? 2 : 1, usedNames, ref reassigned, ref spawned);
                    if (doctorWork)
                        EnsureRolePopulation(citizenRoot, registry, catalog, town, doctorWork, "Doctor", i == 0 ? 3 : 1, usedNames, ref reassigned, ref spawned);
                    if (nurseWork)
                        EnsureRolePopulation(citizenRoot, registry, catalog, town, nurseWork, "Nurse", i == 0 ? 4 : 2, usedNames, ref reassigned, ref spawned);
                }

                EnsureRolePopulation(citizenRoot, registry, catalog, primaryTown, policeWork, "Police Officer", 4, usedNames, ref reassigned, ref spawned);
                EnsureRolePopulation(citizenRoot, registry, catalog, primaryTown, swatWork, "SWAT Officer", 2, usedNames, ref reassigned, ref spawned);
                EnsureRolePopulation(citizenRoot, registry, catalog, primaryTown, dispatchWork, "Police Dispatcher", 2, usedNames, ref reassigned, ref spawned);

                H51CitizenAnchor towWork = FindAnchor(livingAnchors, primaryTown, "Roadside Service");
                if (towWork)
                    EnsureRolePopulation(citizenRoot, registry, catalog, primaryTown, towWork, "Tow Operator", 2, usedNames, ref reassigned, ref spawned);

                EditorUtility.DisplayProgressBar("Hanger 51 Civic Interiors", "Rebaking pedestrian navigation through the new interiors", .82f);

                Physics.SyncTransforms();
                int navMeshes = RebuildNavMeshes(citizenRoot);

                registry.RefreshSceneReferences();
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();

                Selection.activeGameObject = policeStation.gameObject;

                Debug.Log(
                    $"Step 146 complete. walkable theater interiors={theaterInteriors}, walkable medical interiors={medicalInteriors}, walkable roadside interiors={serviceInteriors}, police station=1, interactive civic doors={civicDoors}, verified interior clearance points created={clearanceMarkers}, police parking={policeParking}, existing citizens reassigned={reassigned}, new specialist citizens spawned={spawned}, NavMeshes rebaked={navMeshes}. Doctors, nurses, theater staff, police, dispatch and SWAT are populated with real work anchors inside their buildings. Run Step 147.",
                    policeStation.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/147 - Validate Police Staffing And Civic Interiors")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform livingWorld = world ? FindChild(world.transform, LivingWorldName) : null;
            Transform civicRoot = world ? FindChild(world.transform, CivicRootName) : null;

            if (!world || !citizenRoot || !livingWorld || !civicRoot)
            {
                Debug.LogError("Step 147 failed. Run Step 146 first.");
                return;
            }

            H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            H51EnterableBuilding[] enterables = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            H51TownDoor[] doors = civicRoot.GetComponentsInChildren<H51TownDoor>(true)
                .Concat(livingWorld.GetComponentsInChildren<H51TownDoor>(true))
                .ToArray();
            H51CitizenWorkplace[] workplaces = world.GetComponentsInChildren<H51CitizenWorkplace>(true);
            NavMeshSurface[] navs = citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);

            int policeStations = workplaces.Count(w => w && w.Category == "Police");
            int theaters = workplaces.Count(w => w && w.Category == "Entertainment");
            int medical = workplaces.Count(w => w && w.Category == "Medical");
            int roadside = workplaces.Count(w => w && w.Category == "Roadside Service");

            int police = citizens.Count(c => c && c.Occupation == "Police Officer");
            int swat = citizens.Count(c => c && c.Occupation == "SWAT Officer");
            int swatVisuals = citizens.Count(c => c && c.Occupation == "SWAT Officer" &&
                !string.IsNullOrWhiteSpace(c.VisualArchetype) &&
                c.VisualArchetype.IndexOf("Swat", StringComparison.OrdinalIgnoreCase) >= 0);
            int dispatch = citizens.Count(c => c && c.Occupation == "Police Dispatcher");
            int doctors = citizens.Count(c => c && c.Occupation == "Doctor");
            int nurses = citizens.Count(c => c && c.Occupation == "Nurse");
            int theaterStaff = citizens.Count(c => c && c.Occupation == "Theater Attendant");

            int modeledInteriors = 0;
            int missingInteriorCenter = 0;
            for (int i = 0; i < enterables.Length; i++)
            {
                H51EnterableBuilding b = enterables[i];
                if (!b) continue;
                if (b.GetComponent<H51CitizenWorkplace>())
                {
                    if (FindChild(b.transform, "Modeled Interior")) modeledInteriors++;
                    if (!b.InteriorCenter || !b.Entrance) missingInteriorCenter++;
                }
            }

            int badDoors = 0;
            for (int i = 0; i < doors.Length; i++)
            {
                H51TownDoor door = doors[i];
                if (!door) continue;
                Renderer r = door.GetComponent<Renderer>();
                if (!r) { badDoors++; continue; }
                Vector3 size = r.bounds.size;
                float height = Mathf.Max(size.y, Mathf.Max(size.x, size.z));
                float width = MiddleDimension(size);
                if (height < 2.0f || height > 2.9f || width < .9f || width > 2.6f)
                    badDoors++;
            }

            int blockedClearance = CountBlockedClearanceMarkers(world.transform);
            int navReady = navs.Count(n => n && n.navMeshData);

            List<string> towns = citizens
                .Where(c => c && !string.IsNullOrWhiteSpace(c.TownName))
                .Select(c => c.TownName)
                .Distinct()
                .ToList();

            bool medicalStaffReady = doctors >= towns.Count + 2 && nurses >= towns.Count * 2;
            bool theaterStaffReady = theaterStaff >= towns.Count;
            bool ok =
                policeStations >= 1 &&
                theaters >= towns.Count &&
                medical >= towns.Count &&
                roadside >= 1 &&
                police >= 4 &&
                swat >= 2 &&
                swatVisuals >= 2 &&
                dispatch >= 2 &&
                medicalStaffReady &&
                theaterStaffReady &&
                modeledInteriors >= theaters + medical + roadside + policeStations &&
                missingInteriorCenter == 0 &&
                badDoors == 0 &&
                blockedClearance == 0 &&
                navReady == navs.Length;

            if (ok)
            {
                Debug.Log(
                    $"Step 147 passed. police stations={policeStations}, police officers={police}, SWAT={swat} with SWAT visuals={swatVisuals}, dispatchers={dispatch}, doctors={doctors}, nurses={nurses}, theater staff={theaterStaff}, modeled civic/work interiors={modeledInteriors}, bad doors=0, blocked interior clearance points=0, NavMeshes={navReady}/{navs.Length}.",
                    civicRoot.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 147 failed. police stations={policeStations}, police={police}/4, SWAT={swat}/2 (SWAT visuals={swatVisuals}/2), dispatch={dispatch}/2, doctors={doctors}, nurses={nurses}, theater staff={theaterStaff}, theaters={theaters}, medical buildings={medical}, roadside={roadside}, modeled interiors={modeledInteriors}, missing entrance/interior refs={missingInteriorCenter}, bad door sizes={badDoors}, blocked clearance points={blockedClearance}, NavMeshes={navReady}/{navs.Length}.",
                    civicRoot.gameObject);
            }
        }

        static void RebuildTheater(
            H51CitizenWorkplace workplace,
            Transform anchorRoot,
            Mats m,
            ref int doors,
            ref int clearances)
        {
            Transform root = workplace.transform;
            ClearChildren(root);

            BuildShell(root, 18f, 14f, 4.3f, 2.2f, 2.45f, m.theater, m, out Transform entrance, out Transform interior, ref doors);

            Transform modeled = New("Modeled Interior", root);
            Box(modeled, "Ticket Counter", new Vector3(4.6f,.58f,4.3f), new Vector3(4.2f,1.16f,.75f), m.counter, true);
            Box(modeled, "Ticket Register", new Vector3(5.0f,1.30f,4.3f), new Vector3(.48f,.28f,.38f), m.black, false);
            Box(modeled, "Concession Counter", new Vector3(-4.4f,.62f,3.8f), new Vector3(4.6f,1.24f,.78f), m.counter, true);
            Box(modeled, "Concession Back Shelf", new Vector3(-6.4f,1.45f,2.4f), new Vector3(.45f,2.65f,3.0f), m.darkWood, true);

            for (int row = 0; row < 4; row++)
            {
                float z = 1.1f - row * 2.1f;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int seat = 0; seat < 3; seat++)
                    {
                        float x = side * (2.0f + seat * 1.25f);
                        Chair(modeled, new Vector3(x,0f,z), Quaternion.Euler(0,180f,0), m);
                    }
                }
            }

            Box(modeled, "Movie Screen", new Vector3(0,2.25f,-6.65f), new Vector3(10.5f,4.0f,.12f), m.white, false);
            Box(modeled, "Projection Console", new Vector3(6.0f,.62f,5.2f), new Vector3(2.4f,1.24f,.7f), m.darkWood, true);
            InteriorLight(modeled, new Vector3(0f,3.7f,3.0f), 11f, 1.25f);
            InteriorLight(modeled, new Vector3(0f,3.7f,-3.4f), 11f, 1.10f);

            Clearance(modeled, new Vector3(0f,1.0f,5.2f), new Vector3(1.2f,1.8f,1.6f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,2.8f), new Vector3(1.3f,1.8f,1.4f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,-1.0f), new Vector3(1.15f,1.8f,2.2f), ref clearances);

            ConfigureEnterable(root, workplace, H51EnterableBuildingKind.Workplace, entrance, interior);
            WorldText(root, workplace.DisplayName.ToUpperInvariant(), new Vector3(0,3.45f,7.15f), Quaternion.Euler(0,180,0), .19f);

            H51CitizenAnchor leisure = FindAnchor(anchorRoot, workplace.TownName,
                workplace.DisplayName.IndexOf("Cinema", StringComparison.OrdinalIgnoreCase) >= 0 ? "Cinema" : "Community Theater");
            if (leisure)
            {
                MoveAnchor(leisure, root.TransformPoint(new Vector3(0f,.05f,-1.0f)), root.rotation);
                ResetActivities(leisure);
                AddActivity(leisure.transform, new Vector3(-3.2f,0f,0.3f), Quaternion.Euler(0,180,0), "watching a movie", 12f, 25f);
                AddActivity(leisure.transform, new Vector3(3.2f,0f,-1.8f), Quaternion.Euler(0,180,0), "watching a movie with friends", 12f, 25f);
                AddActivity(leisure.transform, new Vector3(0f,0f,5.0f), Quaternion.identity, "buying a theater ticket", 4f, 8f);
            }

            H51CitizenAnchor staff = FindAnchor(anchorRoot, workplace.TownName, "Theater Staff");
            if (staff)
            {
                MoveAnchor(staff, root.TransformPoint(new Vector3(4.3f,.05f,3.2f)), root.rotation);
                ResetActivities(staff);
                AddActivity(staff.transform, Vector3.zero, Quaternion.Euler(0,180,0), "working the theater ticket counter", 7f, 15f);
                AddActivity(staff.transform, root.InverseTransformPoint(root.TransformPoint(new Vector3(-4.2f,.05f,3.0f))) - root.InverseTransformPoint(staff.transform.position), Quaternion.Euler(0,180,0), "restocking the concession counter", 7f, 15f);
            }
        }

        static void RebuildMedical(
            H51CitizenWorkplace workplace,
            Transform anchorRoot,
            Mats m,
            ref int doors,
            ref int clearances)
        {
            Transform root = workplace.transform;
            ClearChildren(root);

            bool hospital = workplace.DisplayName.IndexOf("Hospital", StringComparison.OrdinalIgnoreCase) >= 0;
            float w = hospital ? 24f : 19f;
            float d = hospital ? 17f : 14f;
            float h = hospital ? 4.8f : 4.3f;

            BuildShell(root, w, d, h, 2.25f, 2.5f, m.hospital, m, out Transform entrance, out Transform interior, ref doors);

            Transform modeled = New("Modeled Interior", root);

            Box(modeled, "Reception Counter", new Vector3(4.4f,.62f,d*.5f-3.0f), new Vector3(5.0f,1.24f,.80f), m.counter, true);
            Box(modeled, "Reception Computer", new Vector3(4.8f,1.36f,d*.5f-3.0f), new Vector3(.55f,.42f,.20f), m.black, false);
            Box(modeled, "Records Cabinet", new Vector3(w*.5f-1.2f,1.1f,1.2f), new Vector3(.65f,2.2f,2.6f), m.metal, true);

            for (int i = 0; i < 4; i++)
            {
                float x = -w*.33f + (i % 2) * 2.2f;
                float z = d*.5f - 2.6f - (i / 2) * 1.8f;
                Chair(modeled, new Vector3(x,0f,z), Quaternion.Euler(0,180,0), m);
            }

            Box(modeled, "Nurse Station", new Vector3(0,.62f,-.6f), new Vector3(5.5f,1.24f,1.0f), m.counter, true);
            Box(modeled, "Medical Supply Cabinet", new Vector3(-w*.5f+1.1f,1.2f,-1.0f), new Vector3(.65f,2.4f,3.2f), m.metal, true);

            float examZ = -d*.5f + 3.0f;
            for (int bay = -1; bay <= 1; bay++)
            {
                float x = bay * (hospital ? 5.3f : 4.0f);
                Box(modeled, "Exam Bed " + (bay+2), new Vector3(x,.48f,examZ), new Vector3(2.0f,.55f,4.0f), m.white, true);
                Box(modeled, "Exam Pillow " + (bay+2), new Vector3(x,.82f,examZ-1.35f), new Vector3(1.3f,.18f,.55f), m.blue, false);
                if (bay < 1)
                    Box(modeled, "Exam Divider " + (bay+2), new Vector3(x+2.6f,1.45f,examZ), new Vector3(.12f,2.9f,4.8f), m.wall, true);
            }

            Box(modeled, "Doctor Chart Desk", new Vector3(w*.31f,.62f,-1.4f), new Vector3(2.4f,1.24f,.85f), m.darkWood, true);
            Box(modeled, "Medical Waste Station", new Vector3(-w*.30f,.50f,-1.5f), new Vector3(.85f,1.0f,.85f), m.red, true);
            InteriorLight(modeled, new Vector3(0f,h-.65f,3.2f), 12f, 1.35f);
            InteriorLight(modeled, new Vector3(0f,h-.65f,-3.6f), 12f, 1.35f);

            Clearance(modeled, new Vector3(0f,1.0f,d*.5f-1.2f), new Vector3(1.2f,1.8f,1.5f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,2.0f), new Vector3(1.25f,1.8f,1.5f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,-3.0f), new Vector3(1.15f,1.8f,1.3f), ref clearances);

            ConfigureEnterable(root, workplace, H51EnterableBuildingKind.Workplace, entrance, interior);
            WorldText(root, workplace.DisplayName.ToUpperInvariant(), new Vector3(1.7f,h-.95f,d*.5f+.18f), Quaternion.Euler(0,180,0), hospital ? .18f : .17f);
            Box(root, "Medical Cross Vertical", new Vector3(-w*.35f,h-.95f,d*.5f+.16f), new Vector3(.55f,2.1f,.13f), m.red, false);
            Box(root, "Medical Cross Horizontal", new Vector3(-w*.35f,h-.95f,d*.5f+.17f), new Vector3(2.1f,.55f,.13f), m.red, false);

            H51CitizenAnchor patient = FindAnchor(anchorRoot, workplace.TownName, hospital ? "Regional Hospital" : "Medical Clinic");
            if (patient)
            {
                MoveAnchor(patient, root.TransformPoint(new Vector3(-4.2f,.05f,d*.5f-3.2f)), root.rotation);
                ResetActivities(patient);
                AddActivity(patient.transform, Vector3.zero, Quaternion.Euler(0,180,0), "waiting for a medical appointment", 8f, 18f);
                AddActivity(patient.transform, new Vector3(3.0f,0f,-1.1f), Quaternion.Euler(0,180,0), "checking in at reception", 5f, 10f);
                AddActivity(patient.transform, new Vector3(4.0f,0f,-6.0f), Quaternion.identity, "being seen in an exam room", 10f, 20f);
            }

            H51CitizenAnchor doctor = FindAnchor(anchorRoot, workplace.TownName, "Doctor Station");
            if (doctor)
            {
                MoveAnchor(doctor, root.TransformPoint(new Vector3(w*.29f,.05f,-1.1f)), root.rotation);
                ResetActivities(doctor);
                AddActivity(doctor.transform, Vector3.zero, Quaternion.Euler(0,180,0), "reviewing patient charts", 8f, 18f);
                AddActivity(doctor.transform, root.InverseTransformPoint(root.TransformPoint(new Vector3(0f,.05f,examZ+1.2f))) - root.InverseTransformPoint(doctor.transform.position), Quaternion.identity, "examining a patient", 9f, 20f);
            }

            H51CitizenAnchor nurse = FindAnchor(anchorRoot, workplace.TownName, "Nurse Station");
            if (nurse)
            {
                MoveAnchor(nurse, root.TransformPoint(new Vector3(-1.4f,.05f,-.1f)), root.rotation);
                ResetActivities(nurse);
                AddActivity(nurse.transform, Vector3.zero, Quaternion.identity, "working at the nurse station", 8f, 18f);
                AddActivity(nurse.transform, new Vector3(-3.4f,0f,-1.2f), Quaternion.Euler(0,90,0), "preparing medical supplies", 7f, 16f);
            }
        }

        static void RebuildRoadside(
            H51CitizenWorkplace workplace,
            Transform anchorRoot,
            Mats m,
            ref int doors,
            ref int clearances)
        {
            Transform root = workplace.transform;
            ClearChildren(root);

            BuildShell(root, 18f, 14f, 4.4f, 2.2f, 2.5f, m.service, m, out Transform entrance, out Transform interior, ref doors);
            Transform modeled = New("Modeled Interior", root);

            Box(modeled, "Service Counter", new Vector3(4.6f,.62f,3.3f), new Vector3(4.0f,1.24f,.85f), m.counter, true);
            Box(modeled, "Roadside Desk", new Vector3(5.2f,.62f,.8f), new Vector3(2.2f,1.24f,1.0f), m.darkWood, true);
            Box(modeled, "Main Workbench", new Vector3(-5.6f,.68f,-3.8f), new Vector3(4.6f,1.36f,.90f), m.metal, true);
            Box(modeled, "Tool Wall", new Vector3(-7.7f,1.8f,-1.5f), new Vector3(.22f,3.2f,4.0f), m.darkWood, true);
            Box(modeled, "Parts Shelving", new Vector3(6.9f,1.55f,-3.5f), new Vector3(1.8f,3.1f,3.8f), m.metal, true);
            Box(modeled, "Service Bay Mat", new Vector3(-2.2f,.08f,-1.2f), new Vector3(5.0f,.08f,7.0f), m.black, false);
            Box(modeled, "Air Compressor", new Vector3(1.9f,.75f,-4.6f), new Vector3(.85f,1.5f,.85f), m.blue, true);
            InteriorLight(modeled, new Vector3(0f,3.7f,2.8f), 11f, 1.20f);
            InteriorLight(modeled, new Vector3(0f,3.7f,-3.6f), 11f, 1.20f);

            Clearance(modeled, new Vector3(0f,1.0f,5.2f), new Vector3(1.2f,1.8f,1.5f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,1.8f), new Vector3(1.2f,1.8f,1.5f), ref clearances);
            Clearance(modeled, new Vector3(-2.2f,1.0f,-1.2f), new Vector3(1.3f,1.8f,2.0f), ref clearances);

            ConfigureEnterable(root, workplace, H51EnterableBuildingKind.Workplace, entrance, interior);
            WorldText(root, "TOW & ROADSIDE", new Vector3(0,3.45f,7.15f), Quaternion.Euler(0,180,0), .19f);

            H51CitizenAnchor tow = FindAnchor(anchorRoot, workplace.TownName, "Roadside Service");
            if (tow)
            {
                MoveAnchor(tow, root.TransformPoint(new Vector3(-4.7f,.05f,-2.8f)), root.rotation);
                ResetActivities(tow);
                AddActivity(tow.transform, Vector3.zero, Quaternion.Euler(0,90,0), "working at the roadside service workbench", 8f, 18f);
                AddActivity(tow.transform, new Vector3(5.0f,0f,4.8f), Quaternion.Euler(0,180,0), "handling a roadside service call", 6f, 14f);
                AddActivity(tow.transform, new Vector3(2.5f,0f,-1.2f), Quaternion.identity, "checking shop equipment", 6f, 14f);
            }
        }

        static Transform BuildPoliceStation(
            Transform parent,
            string town,
            Vector3 site,
            Vector3 townCenter,
            Mats m,
            ref int doors,
            ref int clearances)
        {
            Transform root = New(town + " Police Station", parent);
            root.position = site;
            root.rotation = FaceToward(site, townCenter);

            H51CitizenWorkplace workplace = root.gameObject.AddComponent<H51CitizenWorkplace>();
            workplace.Configure(
                "life-" + Safe(town) + "-police",
                town + " Police Department",
                town,
                "Police",
                "Police Officer",
                14);

            BuildShell(root, 24f, 18f, 4.7f, 2.25f, 2.5f, m.policeBlue, m, out Transform entrance, out Transform interior, ref doors);
            Transform modeled = New("Modeled Interior", root);

            Box(modeled, "Front Desk", new Vector3(4.5f,.65f,5.4f), new Vector3(6.2f,1.30f,.9f), m.counter, true);
            Box(modeled, "Front Desk Computer", new Vector3(5.2f,1.40f,5.4f), new Vector3(.65f,.46f,.20f), m.black, false);

            Chair(modeled, new Vector3(-6.4f,0f,5.0f), Quaternion.Euler(0,180,0), m);
            Chair(modeled, new Vector3(-4.5f,0f,5.0f), Quaternion.Euler(0,180,0), m);
            Chair(modeled, new Vector3(-2.6f,0f,5.0f), Quaternion.Euler(0,180,0), m);

            Box(modeled, "Dispatch Desk A", new Vector3(5.6f,.65f,-3.2f), new Vector3(2.6f,1.30f,1.1f), m.darkWood, true);
            Box(modeled, "Dispatch Desk B", new Vector3(2.5f,.65f,-3.2f), new Vector3(2.6f,1.30f,1.1f), m.darkWood, true);
            Box(modeled, "Dispatch Monitor A", new Vector3(5.6f,1.43f,-3.5f), new Vector3(.75f,.55f,.16f), m.black, false);
            Box(modeled, "Dispatch Monitor B", new Vector3(2.5f,1.43f,-3.5f), new Vector3(.75f,.55f,.16f), m.black, false);

            Box(modeled, "Briefing Table", new Vector3(-.3f,.55f,-1.7f), new Vector3(4.2f,1.10f,2.3f), m.darkWood, true);
            for (int i = -1; i <= 1; i += 2)
            {
                Chair(modeled, new Vector3(-.3f + i*2.8f,0f,-1.7f), Quaternion.Euler(0,i<0?90:-90,0), m);
                Chair(modeled, new Vector3(-.3f + i*.9f,0f,-3.3f), Quaternion.identity, m);
            }
            Box(modeled, "Briefing Board", new Vector3(5.5f,2.2f,-8.7f), new Vector3(4.5f,2.1f,.12f), m.white, false);

            for (int i = 0; i < 5; i++)
                Box(modeled, "SWAT Locker " + (i+1), new Vector3(-10.6f,1.25f,-5.8f+i*1.45f), new Vector3(.75f,2.5f,1.15f), m.metal, true);

            Box(modeled, "SWAT Equipment Bench", new Vector3(-6.7f,.60f,-4.6f), new Vector3(4.6f,1.20f,.85f), m.darkWood, true);
            Box(modeled, "Evidence Cabinet", new Vector3(10.6f,1.35f,-6.0f), new Vector3(.75f,2.7f,3.0f), m.metal, true);
            InteriorLight(modeled, new Vector3(0f,4.0f,4.8f), 12f, 1.35f);
            InteriorLight(modeled, new Vector3(0f,4.0f,-1.0f), 12f, 1.30f);
            InteriorLight(modeled, new Vector3(-6.0f,4.0f,-5.8f), 9f, 1.15f);

            Transform cells = New("Holding Cells", modeled);
            cells.localPosition = new Vector3(-6.2f,0f,-7.1f);
            for (int cell = 0; cell < 2; cell++)
            {
                float x = cell * 4.2f;
                Box(cells, "Cell Back " + cell, new Vector3(x,1.4f,-1.4f), new Vector3(3.8f,2.8f,.12f), m.wall, true);
                Box(cells, "Cell Side L " + cell, new Vector3(x-1.9f,1.4f,0), new Vector3(.12f,2.8f,2.9f), m.wall, true);
                Box(cells, "Cell Side R " + cell, new Vector3(x+1.9f,1.4f,0), new Vector3(.12f,2.8f,2.9f), m.wall, true);
                for (int bar = -3; bar <= 3; bar++)
                    Box(cells, "Cell Bar " + cell + "-" + bar, new Vector3(x+bar*.46f,1.4f,1.4f), new Vector3(.08f,2.8f,.08f), m.metal, true);
                Box(cells, "Cell Bench " + cell, new Vector3(x,.42f,-.8f), new Vector3(2.4f,.35f,.55f), m.darkWood, true);
            }

            Clearance(modeled, new Vector3(0f,1.0f,7.0f), new Vector3(1.25f,1.8f,1.5f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,3.8f), new Vector3(1.25f,1.8f,1.5f), ref clearances);
            Clearance(modeled, new Vector3(0f,1.0f,.8f), new Vector3(1.15f,1.8f,1.2f), ref clearances);
            Clearance(modeled, new Vector3(-5.2f,1.0f,-3.0f), new Vector3(1.0f,1.8f,1.2f), ref clearances);
            Clearance(modeled, new Vector3(4.1f,1.0f,-5.6f), new Vector3(1.0f,1.8f,1.2f), ref clearances);

            ConfigureEnterable(root, workplace, H51EnterableBuildingKind.Civic, entrance, interior);

            WorldText(root, "POLICE", new Vector3(0,3.65f,9.15f), Quaternion.Euler(0,180,0), .28f);
            Box(root, "Police Badge Sign", new Vector3(-8.4f,3.65f,9.10f), new Vector3(2.2f,2.2f,.14f), m.blue, false);

            return root;
        }

        static void BuildShell(
            Transform root,
            float width,
            float depth,
            float height,
            float doorWidth,
            float doorHeight,
            Material exterior,
            Mats m,
            out Transform entrance,
            out Transform interior,
            ref int doorCount)
        {
            Box(root, "Interior Floor", new Vector3(0,.08f,0), new Vector3(width,.16f,depth), m.floor, true);
            Box(root, "Left Wall", new Vector3(-width*.5f+.10f,height*.5f,0), new Vector3(.20f,height,depth), exterior, true);
            Box(root, "Right Wall", new Vector3(width*.5f-.10f,height*.5f,0), new Vector3(.20f,height,depth), exterior, true);
            Box(root, "Back Wall", new Vector3(0,height*.5f,-depth*.5f+.10f), new Vector3(width,height,.20f), exterior, true);

            float frontSegment = (width - doorWidth) * .5f;
            float frontZ = depth*.5f-.10f;
            Box(root, "Front Wall Left",
                new Vector3(-(doorWidth*.5f + frontSegment*.5f),height*.5f,frontZ),
                new Vector3(frontSegment,height,.20f), exterior, true);
            Box(root, "Front Wall Right",
                new Vector3(doorWidth*.5f + frontSegment*.5f,height*.5f,frontZ),
                new Vector3(frontSegment,height,.20f), exterior, true);

            float headerHeight = Mathf.Max(.25f,height-doorHeight);
            Box(root, "Door Header",
                new Vector3(0,doorHeight+headerHeight*.5f,frontZ),
                new Vector3(doorWidth,headerHeight,.20f), exterior, true);

            Box(root, "Roof", new Vector3(0,height+.14f,0), new Vector3(width+.6f,.28f,depth+.6f), m.ceiling, true);
            Box(root, "Door Frame Left", new Vector3(-doorWidth*.5f-.07f,doorHeight*.5f,depth*.5f+.035f), new Vector3(.14f,doorHeight+.12f,.24f), m.trim, true);
            Box(root, "Door Frame Right", new Vector3(doorWidth*.5f+.07f,doorHeight*.5f,depth*.5f+.035f), new Vector3(.14f,doorHeight+.12f,.24f), m.trim, true);
            Box(root, "Door Frame Header", new Vector3(0,doorHeight+.06f,depth*.5f+.035f), new Vector3(doorWidth+.28f,.14f,.24f), m.trim, true);

            float leafWidth = doorWidth-.16f;
            float leafHeight = doorHeight-.10f;
            Quaternion openRotation = Quaternion.Euler(0,-86f,0);
            Vector3 hinge = new Vector3(-doorWidth*.5f+.09f,leafHeight*.5f,depth*.5f+.08f);
            Vector3 closedPosition = new Vector3(0,leafHeight*.5f,depth*.5f+.08f);
            Vector3 openPosition = hinge + openRotation * Vector3.right * (leafWidth*.5f);

            Transform door = Box(root, "Civic Door", openPosition, new Vector3(leafWidth,leafHeight,.10f), m.darkWood, true, openRotation);
            H51TownDoor interactive = door.gameObject.AddComponent<H51TownDoor>();
            interactive.Configure(closedPosition, Quaternion.identity, openPosition, openRotation, true);
            Sphere(door, "Door Knob", new Vector3(leafWidth*.37f,0,-.10f), .055f, m.metal);
            doorCount++;

            entrance = New("Entrance Point", root);
            entrance.localPosition = new Vector3(0,.08f,depth*.5f+1.25f);
            entrance.localRotation = Quaternion.Euler(0,180,0);

            interior = New("Interior Center", root);
            interior.localPosition = new Vector3(0,.08f,0);
            interior.localRotation = Quaternion.identity;
        }

        static void ConfigureEnterable(
            Transform root,
            H51CitizenWorkplace workplace,
            H51EnterableBuildingKind kind,
            Transform entrance,
            Transform interior)
        {
            H51EnterableBuilding building = root.GetComponent<H51EnterableBuilding>();
            if (!building) building = root.gameObject.AddComponent<H51EnterableBuilding>();
            building.Configure(
                workplace.WorkplaceId,
                workplace.DisplayName,
                workplace.TownName,
                kind,
                entrance,
                interior);
            EditorUtility.SetDirty(building);
        }

        static void EnsureRolePopulation(
            Transform citizenRoot,
            H51CitizenRegistry registry,
            H51CitizenVisualCatalog catalog,
            string town,
            H51CitizenAnchor work,
            string occupation,
            int desired,
            HashSet<string> usedNames,
            ref int reassigned,
            ref int spawned)
        {
            if (!work || desired <= 0) return;

            H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            List<H51CitizenIdentity> assigned = citizens
                .Where(c => c && c.TownName == town && c.Occupation == occupation)
                .ToList();

            for (int i = 0; i < assigned.Count; i++)
            {
                assigned[i].SetWorkAssignment(work.AnchorId, occupation);
                EditorUtility.SetDirty(assigned[i]);
            }

            int needed = desired - assigned.Count;
            if (needed <= 0) return;

            bool requiresDedicatedVisual =
                occupation.IndexOf("SWAT", StringComparison.OrdinalIgnoreCase) >= 0;

            List<H51CitizenIdentity> candidates = requiresDedicatedVisual
                ? new List<H51CitizenIdentity>()
                : citizens
                    .Where(c => c &&
                                c.TownName == town &&
                                IsReassignableOccupation(c.Occupation))
                    .OrderBy(c => StableHash(c.CitizenId + occupation))
                    .ToList();

            for (int i = 0; i < candidates.Count && needed > 0; i++)
            {
                H51CitizenIdentity c = candidates[i];
                c.SetWorkAssignment(work.AnchorId, occupation);
                c.name = "Citizen - " + c.DisplayName + " - " + occupation;
                EditorUtility.SetDirty(c);
                EditorUtility.SetDirty(c.gameObject);
                reassigned++;
                needed--;
            }

            for (int i = 0; i < needed; i++)
            {
                if (SpawnSpecialist(citizenRoot, registry, catalog, town, work, occupation, usedNames))
                    spawned++;
            }
        }

        static bool SpawnSpecialist(
            Transform citizenRoot,
            H51CitizenRegistry registry,
            H51CitizenVisualCatalog catalog,
            string town,
            H51CitizenAnchor work,
            string occupation,
            HashSet<string> usedNames)
        {
            H51CitizenVisualCatalog.Entry visual = PickVisual(catalog, occupation);
            if (visual == null || !visual.prefab) return false;

            H51CitizenAnchor[] homes = citizenRoot.GetComponentsInChildren<H51CitizenAnchor>(true)
                .Where(a => a && a.Kind == H51CitizenAnchorKind.Home && a.TownName == town)
                .ToArray();
            if (homes.Length == 0) return false;

            Transform population = FindChild(citizenRoot, "Citizen Population") ?? citizenRoot;
            Transform townPopulation = FindChild(population, town + " Residents") ?? population;

            GameObject citizen = PrefabUtility.InstantiatePrefab(visual.prefab) as GameObject;
            if (!citizen) return false;

            citizen.transform.SetParent(townPopulation,false);

            H51CitizenPresentation presentation = DetectPresentation(citizen.transform);
            string displayName = UniqueName(presentation, usedNames, StableHash(town + occupation + citizen.GetInstanceID()));
            string id = UniqueCitizenId(citizenRoot, town, occupation);
            int seed = StableHash(id + displayName);
            H51CitizenAnchor home = homes[Math.Abs(seed) % homes.Length];

            citizen.name = "Citizen - " + displayName + " - " + occupation;

            Vector3 spawn = home.transform.position + new Vector3(
                ((seed & 7)-3)*.25f,
                0f,
                (((seed >> 3) & 7)-3)*.25f);
            if (NavMesh.SamplePosition(spawn,out NavMeshHit hit,15f,NavMesh.AllAreas))
                spawn = hit.position;
            citizen.transform.position = spawn;
            citizen.transform.rotation = Quaternion.Euler(0f,Mathf.Abs(seed)%360,0f);

            H51CitizenIdentity identity = citizen.GetComponent<H51CitizenIdentity>();
            if (!identity) identity = citizen.AddComponent<H51CitizenIdentity>();
            identity.Configure(
                id,
                displayName,
                24 + Mathf.Abs(seed)%34,
                town,
                occupation,
                home.AnchorId,
                work.AnchorId,
                visual.packId + "-" + visual.archetype,
                seed,
                .42f + Mathf.Abs(seed%30)/100f,
                .62f,
                .28f,
                .48f);
            identity.SetPresentation(presentation);

            H51CitizenVisualVariation variation = citizen.GetComponent<H51CitizenVisualVariation>();
            if (variation) variation.Configure(seed,1f);

            NavMeshAgent agent = citizen.GetComponent<NavMeshAgent>();
            if (agent)
            {
                agent.speed = 1.55f + Mathf.Abs(seed%30)/100f;
                agent.acceleration = 7f;
                agent.angularSpeed = 320f;
                agent.radius = .32f;
                agent.height = 1.8f;
                agent.stoppingDistance = .65f;
                agent.avoidancePriority = 25 + Mathf.Abs(seed%50);
            }

            if (!citizen.GetComponent<H51CitizenGrounding>())
                citizen.AddComponent<H51CitizenGrounding>();

            EditorUtility.SetDirty(identity);
            EditorUtility.SetDirty(citizen);
            return true;
        }

        static H51CitizenVisualCatalog.Entry PickVisual(H51CitizenVisualCatalog catalog, string occupation)
        {
            if (!catalog || catalog.entries == null || catalog.entries.Count == 0) return null;

            string[] preferred;
            string lower = (occupation ?? "").ToLowerInvariant();

            if (lower.Contains("swat"))
                preferred = new[] { "Swat" };
            else if (lower.Contains("police"))
                preferred = new[] { "Soldier", "Swat", "Worker", "Suit" };
            else if (lower.Contains("doctor"))
                preferred = new[] { "Suit", "Formal", "Casual2", "Casual" };
            else if (lower.Contains("nurse"))
                preferred = new[] { "Worker", "Casual2", "Casual", "Suit" };
            else if (lower.Contains("theater"))
                preferred = new[] { "Formal", "Suit", "Casual2", "Casual" };
            else if (lower.Contains("tow"))
                preferred = new[] { "Worker", "Adventurer", "Casual" };
            else
                preferred = new[] { "Suit", "Worker", "Casual", "Formal" };

            for (int p = 0; p < preferred.Length; p++)
            {
                H51CitizenVisualCatalog.Entry found = catalog.entries.FirstOrDefault(
                    e => e != null && e.prefab &&
                         e.archetype.Equals(preferred[p],StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }

            return catalog.entries.FirstOrDefault(e => e != null && e.prefab);
        }

        static H51CitizenPresentation DetectPresentation(Transform root)
        {
            int female = 0;
            int male = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (!r) continue;
                ScorePresentationText(r.name,ref female,ref male);

                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m]) ScorePresentationText(mats[m].name,ref female,ref male);

                Transform t = r.transform;
                for (int d = 0; t && d < 5; d++, t=t.parent)
                {
                    ScorePresentationText(t.name,ref female,ref male);
                    if (t == root) break;
                }
            }

            if (female >= male + 2) return H51CitizenPresentation.Female;
            if (male >= female + 2) return H51CitizenPresentation.Male;
            return H51CitizenPresentation.Unknown;
        }

        static void ScorePresentationText(string value, ref int female, ref int male)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string s = " " + value.ToLowerInvariant()
                .Replace("_"," ")
                .Replace("-"," ")
                .Replace("."," ") + " ";
            if (s.Contains(" female ") || s.Contains(" woman ") || s.Contains(" girl ")) female += 4;
            if (s.Contains(" male ") || s.Contains(" man ") || s.Contains(" boy ")) male += 4;
        }

        static string UniqueName(H51CitizenPresentation presentation, HashSet<string> used, int seed)
        {
            string[] firsts = presentation == H51CitizenPresentation.Female
                ? FemaleNames
                : presentation == H51CitizenPresentation.Male
                    ? MaleNames
                    : NeutralNames;

            int startFirst = Math.Abs(seed) % firsts.Length;
            int startLast = Math.Abs(seed / 31) % LastNames.Length;

            for (int f = 0; f < firsts.Length; f++)
            {
                for (int l = 0; l < LastNames.Length; l++)
                {
                    string name = firsts[(startFirst+f)%firsts.Length] + " " +
                                  LastNames[(startLast+l)%LastNames.Length];
                    if (used.Add(name)) return name;
                }
            }

            string fallback = "Citizen " + (used.Count+1).ToString("000");
            used.Add(fallback);
            return fallback;
        }

        static string UniqueCitizenId(Transform citizenRoot, string town, string occupation)
        {
            HashSet<string> ids = new HashSet<string>(
                citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true)
                    .Where(c => c && !string.IsNullOrWhiteSpace(c.CitizenId))
                    .Select(c => c.CitizenId));

            string prefix = "H51-STAFF-" + Safe(town) + "-" + Safe(occupation);
            for (int i = 1; i < 1000; i++)
            {
                string id = prefix + "-" + i.ToString("000");
                if (!ids.Contains(id)) return id;
            }
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0,8);
        }

        static bool IsReassignableOccupation(string occupation)
        {
            if (string.IsNullOrWhiteSpace(occupation)) return true;
            string lower = occupation.ToLowerInvariant();

            if (lower == "resident" ||
                lower.Contains("community worker") ||
                lower.Contains("town worker") ||
                lower.Contains("general worker"))
                return true;

            return false;
        }

        static H51CitizenAnchor FindAnchor(Transform root, string town, string label)
        {
            if (!root) return null;
            H51CitizenAnchor[] anchors = root.GetComponentsInChildren<H51CitizenAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
            {
                H51CitizenAnchor a = anchors[i];
                if (a && a.TownName == town &&
                    string.Equals(a.DisplayLabel,label,StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }

        static void MoveAnchor(H51CitizenAnchor anchor, Vector3 position, Quaternion rotation)
        {
            anchor.transform.SetPositionAndRotation(position,rotation);
            EditorUtility.SetDirty(anchor.transform);
        }

        static void ResetActivities(H51CitizenAnchor anchor)
        {
            if (!anchor) return;
            for (int i = anchor.transform.childCount-1; i >= 0; i--)
            {
                Transform child = anchor.transform.GetChild(i);
                if (child.GetComponent<H51CitizenActivitySpot>())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        static H51CitizenAnchor CreateAnchor(
            Transform parent,
            string town,
            string label,
            H51CitizenAnchorKind kind,
            Vector3 position,
            Quaternion rotation,
            string occupation,
            int capacity,
            int seed)
        {
            Transform t = New(label + " Anchor",parent);
            t.SetPositionAndRotation(position,rotation);

            H51CitizenAnchor anchor = t.gameObject.AddComponent<H51CitizenAnchor>();
            anchor.Configure(
                "civic-" + Safe(town) + "-" + Safe(occupation) + "-" + seed,
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
            Quaternion localRotation,
            string label,
            float minDuration,
            float maxDuration)
        {
            Transform t = New("Activity - " + label,anchor);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            H51CitizenActivitySpot activity = t.gameObject.AddComponent<H51CitizenActivitySpot>();
            activity.Configure(label,"Interact",minDuration,maxDuration);
            EditorUtility.SetDirty(activity);
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
            Transform root = New("Parking - " + town + " - " + idPrefix,parent);
            root.position = center;
            root.rotation = rotation;

            float width = Mathf.Max(8f,count*3.0f+2f);
            Box(root,"Parking Surface",new Vector3(0,.035f,0),new Vector3(width,.07f,6.5f),m.asphalt,true);

            int nearest = traffic.FindNearestNode(center);
            Vector3 roadApproach = nearest >= 0 ? traffic.GetNode(nearest) : center + root.forward*8f;

            for (int i = 0; i < count; i++)
            {
                float x = (i-(count-1)*.5f)*3.0f;
                Transform spot = New("Parking Spot " + (i+1),root);
                spot.localPosition = new Vector3(x,.08f,0);
                Box(spot,"Left Line",new Vector3(-1.25f,.015f,0),new Vector3(.08f,.025f,5.4f),m.line,false);
                Box(spot,"Right Line",new Vector3(1.25f,.015f,0),new Vector3(.08f,.025f,5.4f),m.line,false);

                H51ParkingSpot parking = spot.gameObject.AddComponent<H51ParkingSpot>();
                parking.Configure(
                    "civic-" + Safe(town) + "-" + idPrefix + "-" + i,
                    "civic-" + Safe(town) + "-" + idPrefix,
                    town,
                    roadApproach);
            }

            return count;
        }

        static int RebuildNavMeshes(Transform citizenRoot)
        {
            NavMeshSurface[] surfaces = citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);
            int built = 0;

            for (int i = 0; i < surfaces.Length; i++)
            {
                NavMeshSurface surface = surfaces[i];
                if (!surface) continue;
                surface.RemoveData();
                surface.BuildNavMesh();
                if (surface.navMeshData) built++;
                EditorUtility.SetDirty(surface);
            }

            return built;
        }

        static int CountBlockedClearanceMarkers(Transform world)
        {
            Transform[] all = world.GetComponentsInChildren<Transform>(true);
            int blocked = 0;

            for (int i = 0; i < all.Length; i++)
            {
                Transform marker = all[i];
                if (!marker || !marker.name.StartsWith("Interior Clearance",StringComparison.Ordinal)) continue;

                Vector3 half = new Vector3(
                    Mathf.Max(.25f,Mathf.Abs(marker.lossyScale.x)*.5f),
                    Mathf.Max(.6f,Mathf.Abs(marker.lossyScale.y)*.5f),
                    Mathf.Max(.25f,Mathf.Abs(marker.lossyScale.z)*.5f));

                Collider[] hits = Physics.OverlapBox(
                    marker.position,
                    half,
                    marker.rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                bool bad = false;
                for (int h = 0; h < hits.Length; h++)
                {
                    Collider hit = hits[h];
                    if (!hit) continue;
                    string n = hit.name.ToLowerInvariant();
                    if (n.Contains("floor") || n.Contains("roof") || n.Contains("ceiling") ||
                        n.Contains("door frame") || n.Contains("civic door"))
                        continue;
                    bad = true;
                    break;
                }
                if (bad) blocked++;
            }

            return blocked;
        }

        static void Clearance(Transform parent, Vector3 localPosition, Vector3 size, ref int count)
        {
            Transform marker = New("Interior Clearance " + (++count),parent);
            marker.localPosition = localPosition;
            marker.localScale = size;
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount-1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        static void InteriorLight(
            Transform parent,
            Vector3 localPosition,
            float range,
            float intensity)
        {
            Transform t = New("Interior Light",parent);
            t.localPosition = localPosition;
            Light light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f,.93f,.82f);
            light.shadows = LightShadows.None;
        }

        static void Chair(Transform parent, Vector3 position, Quaternion rotation, Mats m)
        {
            Transform root = New("Chair",parent);
            root.localPosition = position;
            root.localRotation = rotation;
            Box(root,"Seat",new Vector3(0,.48f,0),new Vector3(.78f,.13f,.78f),m.chair,true);
            Box(root,"Back",new Vector3(0,.92f,-.33f),new Vector3(.78f,.78f,.12f),m.chair,true);
            Box(root,"Leg FL",new Vector3(-.29f,.23f,.29f),new Vector3(.09f,.46f,.09f),m.darkWood,true);
            Box(root,"Leg FR",new Vector3(.29f,.23f,.29f),new Vector3(.09f,.46f,.09f),m.darkWood,true);
            Box(root,"Leg BL",new Vector3(-.29f,.23f,-.29f),new Vector3(.09f,.46f,.09f),m.darkWood,true);
            Box(root,"Leg BR",new Vector3(.29f,.23f,-.29f),new Vector3(.09f,.46f,.09f),m.darkWood,true);
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
            Renderer r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = material;
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
            go.transform.localScale = Vector3.one*scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = material;
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
            Transform t = New("World Label - " + text,parent);
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

            string path = Generated + "/Materials/CivicDepthText.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader) { name = "Civic Depth Text" };
                mat.SetTexture("_MainTex",font.material.mainTexture);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color",Color.white);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff",.02f);
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
                floor = Mat("Civic_Floor",new Color(.30f,.31f,.31f)),
                wall = Mat("Civic_Interior_Wall",new Color(.78f,.79f,.77f)),
                ceiling = Mat("Civic_Roof",new Color(.08f,.09f,.11f)),
                trim = Mat("Civic_Trim",new Color(.68f,.70f,.70f)),
                glass = Mat("Civic_Glass",new Color(.13f,.24f,.31f)),
                wood = Mat("Civic_Wood",new Color(.38f,.22f,.10f)),
                darkWood = Mat("Civic_DarkWood",new Color(.20f,.10f,.055f)),
                metal = Mat("Civic_Metal",new Color(.26f,.29f,.32f)),
                counter = Mat("Civic_Counter",new Color(.38f,.32f,.25f)),
                chair = Mat("Civic_Chair",new Color(.20f,.25f,.30f)),
                blue = Mat("Civic_Blue",new Color(.12f,.28f,.48f)),
                policeBlue = Mat("Police_Exterior",new Color(.16f,.23f,.31f)),
                red = Mat("Civic_Red",new Color(.65f,.06f,.07f)),
                white = Mat("Civic_White",new Color(.88f,.89f,.87f)),
                black = Mat("Civic_Black",new Color(.025f,.03f,.035f)),
                hospital = Mat("Hospital_Exterior",new Color(.73f,.78f,.77f)),
                theater = Mat("Theater_Exterior",new Color(.22f,.11f,.16f)),
                service = Mat("Roadside_Exterior",new Color(.27f,.34f,.25f)),
                asphalt = Mat("Civic_Asphalt",new Color(.10f,.11f,.12f)),
                line = Mat("Civic_Parking_Line",new Color(.94f,.89f,.58f))
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
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic",.02f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness",.22f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Vector3 TownCenter(string town, H51CitizenIdentity[] citizens)
        {
            H51CitizenAnchor[] homes = UnityEngine.Object.FindObjectsByType<H51CitizenAnchor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            List<Vector3> positions = homes
                .Where(a => a && a.Kind == H51CitizenAnchorKind.Home && a.TownName == town)
                .Select(a => a.transform.position)
                .ToList();

            if (positions.Count == 0)
                positions = citizens.Where(c => c && c.TownName == town).Select(c => c.transform.position).ToList();

            if (positions.Count == 0) return Vector3.zero;

            Vector3 total = Vector3.zero;
            for (int i = 0; i < positions.Count; i++) total += positions[i];
            return total/positions.Count;
        }

        static Vector3 FindClearSite(Terrain terrain, Vector3 center, float baseRadius, int seed, Vector3 halfExtents)
        {
            System.Random rng = new System.Random(seed);

            for (int ring = 0; ring < 6; ring++)
            {
                float radius = baseRadius + ring*18f;
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    float angle = ((attempt/24f)*360f + (float)rng.NextDouble()*12f)*Mathf.Deg2Rad;
                    Vector3 p = center + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    p.y = Ground(terrain,p);
                    Quaternion rotation = FaceToward(p,center);

                    Collider[] hits = Physics.OverlapBox(
                        p + Vector3.up*halfExtents.y,
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
                        if (n.Contains("grass") || n.Contains("tree") || n.Contains("detail")) continue;
                        blocked = true;
                        break;
                    }
                    if (!blocked) return p;
                }
            }

            Vector3 fallback = center + new Vector3(baseRadius+90f,0,70f);
            fallback.y = Ground(terrain,fallback);
            return fallback;
        }

        static Quaternion FaceToward(Vector3 from, Vector3 to)
        {
            Vector3 d = to-from;
            d.y = 0f;
            return d.sqrMagnitude > .01f ? Quaternion.LookRotation(d.normalized,Vector3.up) : Quaternion.identity;
        }

        static float Ground(Terrain terrain, Vector3 p)
        {
            return terrain ? terrain.SampleHeight(p)+terrain.transform.position.y : p.y;
        }

        static float MiddleDimension(Vector3 size)
        {
            float[] a = { Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z) };
            Array.Sort(a);
            return a[1];
        }

        static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (string.IsNullOrEmpty(value)) return hash;
                for (int i = 0; i < value.Length; i++) hash = hash*31 + value[i];
                return hash & 0x7fffffff;
            }
        }

        static Terrain FindTerrain()
        {
            Terrain[] all = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].gameObject.scene.IsValid()) return all[i];
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
                if (all[i] && all[i].name == name) return all[i];
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
            return result.Replace(" ","_").Replace("/","_").Replace("\\","_");
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
