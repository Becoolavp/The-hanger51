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
    public static class Hanger51CitizenInteriorAndTrafficPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string PassName = "Hanger 51 Enterable Town Interiors And Traffic";
        const string Gen = "Assets/_Project/Characters/H51Citizens/Generated/InteriorsAndTraffic";

        struct InteriorMats
        {
            public Material floor, interiorWall, wood, metal, fabric, counter, appliance, bed, carDark, tire;
            public Material[] carPaint;
        }

        [MenuItem("Hanger 51/World/Current/134 - Build Enterable Homes Workplaces And Citizen Cars")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 134 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            // Keep 134 self-contained: repair/upgrade the existing citizen jobs and talk system first.
            // Step 132 preserves citizen identities and memories while rebuilding anchors/workplaces.
            Hanger51CitizenSystemSetup.AddCitizenTalkAndWorkingLife();

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform settlements = world ? FindChild(world.transform, "Settlements") : null;
            H51CitizenRegistry registry = citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;

            if (!world || !terrain || !citizenRoot || !settlements || !registry)
            {
                Debug.LogError("Step 134 failed. Run the citizen build through Step 132 first.");
                return;
            }

            try
            {
                Ensure(Gen + "/Materials");
                InteriorMats mats = BuildMaterials();

                Transform oldPass = FindChild(world.transform, PassName);
                if (oldPass) UnityEngine.Object.DestroyImmediate(oldPass.gameObject);
                Transform pass = New(PassName, world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Opening and furnishing homes", .08f);

                H51CitizenAnchor[] anchors = citizenRoot.GetComponentsInChildren<H51CitizenAnchor>(true);
                List<H51CitizenAnchor> homeAnchors = anchors
                    .Where(a => a && a.Kind == H51CitizenAnchorKind.Home)
                    .ToList();

                int homesConverted = 0;
                for (int townIndex = 0; townIndex < settlements.childCount; townIndex++)
                {
                    Transform town = settlements.GetChild(townIndex);
                    List<Transform> houses = Houses(town);
                    if (houses.Count == 0) continue;

                    List<H51CitizenAnchor> townHomes = homeAnchors
                        .Where(a => a.TownName == town.name)
                        .OrderBy(a => a.name)
                        .ToList();

                    for (int i = 0; i < houses.Count; i++)
                    {
                        H51CitizenAnchor home = i < townHomes.Count ? townHomes[i] : null;
                        if (MakeHouseEnterable(houses[i], terrain, town.name, i, home, mats))
                            homesConverted++;
                    }
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Opening and furnishing stores and workplaces", .30f);

                H51CitizenWorkplace[] workplaces = world.GetComponentsInChildren<H51CitizenWorkplace>(true);
                int workplacesConverted = 0;
                int workActivitySpots = 0;

                for (int i = 0; i < workplaces.Length; i++)
                {
                    H51CitizenWorkplace workplace = workplaces[i];
                    if (!workplace) continue;

                    H51CitizenAnchor workAnchor = anchors.FirstOrDefault(a =>
                        a && a.Kind == H51CitizenAnchorKind.Work &&
                        a.WorkplaceId == workplace.WorkplaceId);

                    List<H51CitizenAnchor> visitorAnchors = anchors.Where(a =>
                        a && (a.Kind == H51CitizenAnchorKind.Shop || a.Kind == H51CitizenAnchorKind.Civic) &&
                        a.WorkplaceId == workplace.WorkplaceId).ToList();

                    if (MakeWorkplaceEnterable(workplace, terrain, workAnchor, visitorAnchors, mats, out int addedSpots))
                    {
                        workplacesConverted++;
                        workActivitySpots += addedSpots;
                    }
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Extracting a drivable traffic network from the road meshes", .50f);

                H51TrafficNetwork traffic = BuildTrafficNetwork(world.transform, pass, out List<Vector3> trafficNodes, out List<H51TrafficNetwork.Edge> trafficEdges);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Assigning household cars", .66f);
                int cars = BuildCitizenCars(citizenRoot, registry, traffic, trafficNodes, trafficEdges, terrain, mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Rebaking pedestrian navigation through doors and interiors", .82f);
                int surfacesBaked = RebuildNavMeshes(citizenRoot);

                registry.RefreshSceneReferences();
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = pass.gameObject;

                Debug.Log(
                    $"Step 134 complete. enterable homes={homesConverted}, enterable workplaces={workplacesConverted}, indoor work activity spots={workActivitySpots}, traffic nodes={trafficNodes.Count}, traffic edges={trafficEdges.Count}, citizen cars={cars}, NavMesh surfaces rebaked={surfacesBaked}. NPCs can now enter homes/stores/workplaces and use cars for longer trips. Run Step 135.",
                    pass.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/135 - Validate Enterable Town Life And Traffic")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            Transform pass = world ? FindChild(world.transform, PassName) : null;

            if (!world || !citizenRoot || !pass)
            {
                Debug.LogError("Step 135 failed. Run Step 134 first.");
                return;
            }

            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            H51CitizenWorkplace[] workplaces = world.GetComponentsInChildren<H51CitizenWorkplace>(true);
            H51CitizenAnchor[] anchors = citizenRoot.GetComponentsInChildren<H51CitizenAnchor>(true);
            H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            H51CitizenVehicle[] vehicles = citizenRoot.GetComponentsInChildren<H51CitizenVehicle>(true);
            H51TrafficNetwork traffic = pass.GetComponentInChildren<H51TrafficNetwork>(true);
            NavMeshSurface[] surfaces = citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);

            HashSet<string> citizenIds = new HashSet<string>(citizens.Where(c => c).Select(c => c.CitizenId));
            int badBuildings = 0;
            int homeBuildings = 0;
            int workBuildings = 0;

            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding b = buildings[i];
                if (!b) continue;
                if (b.Kind == H51EnterableBuildingKind.Home) homeBuildings++;
                else workBuildings++;
                if (!b.Entrance || !b.InteriorCenter) badBuildings++;
            }

            int indoorHomeAnchors = 0;
            int indoorWorkAnchors = 0;
            int anchorsWithoutIndoorSpots = 0;
            int totalIndoorSpots = 0;

            for (int i = 0; i < anchors.Length; i++)
            {
                H51CitizenAnchor a = anchors[i];
                if (!a) continue;
                H51CitizenActivitySpot[] spots = a.GetComponentsInChildren<H51CitizenActivitySpot>(true);
                totalIndoorSpots += spots.Length;

                if (a.Kind == H51CitizenAnchorKind.Home)
                {
                    if (FindContainingBuilding(buildings, a.transform.position, H51EnterableBuildingKind.Home)) indoorHomeAnchors++;
                    if (spots.Length < 3) anchorsWithoutIndoorSpots++;
                }
                else if (a.Kind == H51CitizenAnchorKind.Work)
                {
                    if (FindContainingBuilding(buildings, a.transform.position, null)) indoorWorkAnchors++;
                    if (spots.Length < 3) anchorsWithoutIndoorSpots++;
                }
            }

            int badVehicleOwner = 0;
            int badVehicleCollider = 0;
            for (int i = 0; i < vehicles.Length; i++)
            {
                H51CitizenVehicle v = vehicles[i];
                if (!v) continue;
                if (string.IsNullOrEmpty(v.OwnerCitizenId) || !citizenIds.Contains(v.OwnerCitizenId)) badVehicleOwner++;
                if (!v.GetComponent<BoxCollider>() || !v.GetComponent<Rigidbody>()) badVehicleCollider++;
            }

            int surfacesWithData = surfaces.Count(s => s && s.navMeshData);
            List<Vector3> route = new List<Vector3>();
            bool networkConnected = traffic && traffic.NodeCount > 2 &&
                traffic.TryBuildPath(traffic.GetNode(0), traffic.GetNode(traffic.NodeCount - 1), route);

            int expectedHomes = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Home);
            int expectedWork = anchors.Count(a => a && a.Kind == H51CitizenAnchorKind.Work);

            bool ok =
                homeBuildings >= expectedHomes &&
                workBuildings >= workplaces.Length &&
                badBuildings == 0 &&
                indoorHomeAnchors == expectedHomes &&
                indoorWorkAnchors == expectedWork &&
                anchorsWithoutIndoorSpots == 0 &&
                totalIndoorSpots >= (expectedHomes + expectedWork) * 3 &&
                traffic && traffic.NodeCount >= 80 &&
                networkConnected &&
                vehicles.Length >= Mathf.Max(8, citizens.Length / 4) &&
                badVehicleOwner == 0 &&
                badVehicleCollider == 0 &&
                surfacesWithData == surfaces.Length;

            if (ok)
            {
                Debug.Log(
                    $"Step 135 passed. enterable homes={homeBuildings}/{expectedHomes}, enterable workplaces={workBuildings}/{workplaces.Length}, indoor home anchors={indoorHomeAnchors}, indoor work anchors={indoorWorkAnchors}, indoor activity spots={totalIndoorSpots}, traffic nodes={traffic.NodeCount}, route test points={route.Count}, citizen cars={vehicles.Length}, NavMeshes={surfacesWithData}.",
                    pass.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 135 failed. enterable homes={homeBuildings}/{expectedHomes}, enterable workplaces={workBuildings}/{workplaces.Length}, bad buildings={badBuildings}, indoor homes={indoorHomeAnchors}, indoor work={indoorWorkAnchors}, anchors missing activity spots={anchorsWithoutIndoorSpots}, total spots={totalIndoorSpots}, traffic nodes={(traffic ? traffic.NodeCount : 0)}, network connected={networkConnected}, cars={vehicles.Length}, bad car owners={badVehicleOwner}, bad car physics={badVehicleCollider}, NavMeshes={surfacesWithData}/{surfaces.Length}.",
                    pass.gameObject);
            }
        }

        static bool MakeHouseEnterable(
            Transform house,
            Terrain terrain,
            string townName,
            int index,
            H51CitizenAnchor homeAnchor,
            InteriorMats mats)
        {
            Transform body = DirectChild(house, "Textured Siding") ?? DirectChild(house, "House Body");
            if (!body) return false;

            Renderer bodyRenderer = body.GetComponent<Renderer>();
            Material wallMaterial = bodyRenderer ? bodyRenderer.sharedMaterial : mats.interiorWall;
            Vector3 size = body.localScale;
            Vector3 center = body.localPosition;
            if (size.x < 6f || size.z < 6f) return false;

            body.gameObject.SetActive(false);
            DisableDoorObjects(house, false);

            Transform old = DirectChild(house, "H51 Enterable Interior");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform root = New("H51 Enterable Interior", house);

            float floorY = center.y - size.y * .5f + .04f;
            BuildShell(root, center, size, wallMaterial, mats.floor, 2.25f, 3.35f, false);
            BuildHomeFurniture(root, size.x, size.z, floorY, mats);

            Transform entrance = New("Entrance", root);
            entrance.localPosition = new Vector3(0f, floorY + .05f, size.z * .5f + .8f);
            Transform inside = New("Interior Center", root);
            inside.localPosition = new Vector3(0f, floorY + .05f, 0f);

            H51EnterableBuilding building = house.GetComponent<H51EnterableBuilding>();
            if (!building) building = house.gameObject.AddComponent<H51EnterableBuilding>();
            building.Configure(
                "HOME-BUILDING-" + townName + "-" + index.ToString("000"),
                townName + " home " + (index + 1).ToString("00"),
                townName,
                H51EnterableBuildingKind.Home,
                entrance,
                inside);

            if (homeAnchor)
            {
                homeAnchor.transform.position = house.TransformPoint(new Vector3(0f, floorY + .08f, -.15f * size.z));
                homeAnchor.transform.rotation = house.rotation;
                SetIndoorActivitySpots(
                    homeAnchor,
                    house,
                    new[]
                    {
                        new SpotDef("relaxing in the living room", new Vector3(-size.x*.22f, floorY+.05f, size.z*.10f)),
                        new SpotDef("making something in the kitchen", new Vector3(size.x*.25f, floorY+.05f, -size.z*.25f)),
                        new SpotDef("getting things settled around the house", new Vector3(0f, floorY+.05f, -size.z*.05f))
                    });
            }

            EditorUtility.SetDirty(house.gameObject);
            return true;
        }

        static bool MakeWorkplaceEnterable(
            H51CitizenWorkplace workplace,
            Terrain terrain,
            H51CitizenAnchor workAnchor,
            List<H51CitizenAnchor> visitors,
            InteriorMats mats,
            out int activitySpots)
        {
            activitySpots = 0;
            Transform site = workplace.transform;
            Transform body =
                DirectChild(site, "Purpose Building") ??
                DirectChild(site, "Building") ??
                DirectChild(site, "Barn");

            if (!body) return false;

            Renderer bodyRenderer = body.GetComponent<Renderer>();
            Material wallMaterial = bodyRenderer ? bodyRenderer.sharedMaterial : mats.interiorWall;
            Vector3 size = body.localScale;
            Vector3 center = body.localPosition;
            if (size.x < 6f || size.z < 6f) return false;

            body.gameObject.SetActive(false);
            bool service = workplace.Category.IndexOf("Garage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           workplace.Category.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           workplace.Category.IndexOf("Farm", StringComparison.OrdinalIgnoreCase) >= 0;
            DisableDoorObjects(site, service);

            Transform old = DirectChild(site, "H51 Enterable Interior");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform root = New("H51 Enterable Interior", site);

            float floorY = center.y - size.y * .5f + .04f;
            float doorWidth = service ? Mathf.Min(5.2f, size.x * .42f) : 2.4f;
            float doorHeight = service ? Mathf.Min(4.4f, size.y - .7f) : 3.35f;
            BuildShell(root, center, size, wallMaterial, mats.floor, doorWidth, doorHeight, service);

            Transform furnitureFrame = New("Interior Furniture", root);
            furnitureFrame.localPosition = new Vector3(center.x, 0f, center.z);
            BuildWorkFurniture(furnitureFrame, workplace.Category, size.x, size.z, floorY, mats);

            Transform activityFrame = New("Interior Activity Frame", root);
            activityFrame.localPosition = new Vector3(center.x, 0f, center.z);

            Transform entrance = New("Entrance", root);
            entrance.localPosition = new Vector3(center.x, floorY + .05f, center.z + size.z * .5f + .9f);
            Transform inside = New("Interior Center", root);
            inside.localPosition = new Vector3(center.x, floorY + .05f, center.z);

            H51EnterableBuildingKind kind =
                workplace.Category.IndexOf("Store", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workplace.Category.IndexOf("Cafe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workplace.Category.IndexOf("Supply", StringComparison.OrdinalIgnoreCase) >= 0
                    ? H51EnterableBuildingKind.Store
                    : workplace.Category.IndexOf("Farm", StringComparison.OrdinalIgnoreCase) >= 0
                        ? H51EnterableBuildingKind.Farm
                        : workplace.Category.IndexOf("Hall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          workplace.Category.IndexOf("Post", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          workplace.Category.IndexOf("Clinic", StringComparison.OrdinalIgnoreCase) >= 0
                            ? H51EnterableBuildingKind.Civic
                            : H51EnterableBuildingKind.Workplace;

            H51EnterableBuilding building = site.GetComponent<H51EnterableBuilding>();
            if (!building) building = site.gameObject.AddComponent<H51EnterableBuilding>();
            building.Configure(
                workplace.WorkplaceId,
                workplace.DisplayName,
                workplace.TownName,
                kind,
                entrance,
                inside);

            if (workAnchor)
            {
                workAnchor.transform.position = site.TransformPoint(new Vector3(center.x, floorY + .08f, center.z - size.z * .12f));
                workAnchor.transform.rotation = site.rotation;
                SpotDef[] defs = WorkplaceSpots(workplace.Category, size.x, size.z, floorY);
                SetIndoorActivitySpots(workAnchor, activityFrame, defs);
                activitySpots += defs.Length;
            }

            for (int i = 0; i < visitors.Count; i++)
            {
                H51CitizenAnchor visitor = visitors[i];
                if (!visitor) continue;
                visitor.transform.position = site.TransformPoint(new Vector3(
                    center.x + Mathf.Lerp(-1.8f, 1.8f, visitors.Count <= 1 ? .5f : i / (float)(visitors.Count - 1)),
                    floorY + .08f,
                    center.z + size.z * .12f));
                visitor.transform.rotation = site.rotation;

                SpotDef[] visitorDefs =
                {
                    new SpotDef("browsing inside", new Vector3(-size.x*.23f, floorY+.05f, 0f)),
                    new SpotDef("checking something at the counter", new Vector3(size.x*.18f, floorY+.05f, -size.z*.22f)),
                    new SpotDef("taking care of an errand", new Vector3(0f, floorY+.05f, size.z*.05f))
                };
                SetIndoorActivitySpots(visitor, activityFrame, visitorDefs);
            }

            EditorUtility.SetDirty(site.gameObject);
            return true;
        }

        static void BuildShell(
            Transform root,
            Vector3 center,
            Vector3 size,
            Material wall,
            Material floor,
            float doorWidth,
            float doorHeight,
            bool wideEntry)
        {
            float thickness = .24f;
            float bottom = center.y - size.y * .5f;
            float wallY = center.y;
            float front = center.z + size.z * .5f;
            float back = center.z - size.z * .5f;

            Box(root, "Interior Floor", new Vector3(center.x, bottom + .035f, center.z),
                new Vector3(size.x - .22f, .07f, size.z - .22f), floor, true);

            Box(root, "Back Wall", new Vector3(center.x, wallY, back),
                new Vector3(size.x, size.y, thickness), wall, true);
            Box(root, "Left Wall", new Vector3(center.x - size.x*.5f, wallY, center.z),
                new Vector3(thickness, size.y, size.z), wall, true);
            Box(root, "Right Wall", new Vector3(center.x + size.x*.5f, wallY, center.z),
                new Vector3(thickness, size.y, size.z), wall, true);

            float sideWidth = Mathf.Max(.6f, (size.x - doorWidth) * .5f);
            float sideOffset = doorWidth * .5f + sideWidth * .5f;
            Box(root, "Front Wall Left", new Vector3(center.x - sideOffset, wallY, front),
                new Vector3(sideWidth, size.y, thickness), wall, true);
            Box(root, "Front Wall Right", new Vector3(center.x + sideOffset, wallY, front),
                new Vector3(sideWidth, size.y, thickness), wall, true);

            float headerHeight = Mathf.Max(.5f, size.y - doorHeight);
            Box(root, "Door Header", new Vector3(center.x, bottom + doorHeight + headerHeight*.5f, front),
                new Vector3(doorWidth, headerHeight, thickness), wall, true);

            Material doorMat = MaterialAsset("Open Door", new Color(.25f,.13f,.07f), 0f);
            Box(root, "Open Door", new Vector3(center.x - doorWidth*.5f + .08f, bottom + doorHeight*.5f, front - .85f),
                new Vector3(.12f, doorHeight*.94f, Mathf.Min(1.7f, doorWidth*.76f)), doorMat, true);

            if (wideEntry)
            {
                Box(root, "Safety Bollard Left", new Vector3(center.x-doorWidth*.55f, bottom+.55f, front+.55f),
                    new Vector3(.16f,1.1f,.16f), MaterialAsset("Safety Yellow", new Color(.72f,.55f,.05f), 0f), true);
                Box(root, "Safety Bollard Right", new Vector3(center.x+doorWidth*.55f, bottom+.55f, front+.55f),
                    new Vector3(.16f,1.1f,.16f), MaterialAsset("Safety Yellow", new Color(.72f,.55f,.05f), 0f), true);
            }
        }

        static void BuildHomeFurniture(Transform root, float w, float d, float floorY, InteriorMats m)
        {
            Box(root, "Living Room Rug", new Vector3(-w*.18f,floorY+.025f,d*.12f), new Vector3(w*.35f,.05f,d*.28f), m.fabric, false);
            Box(root, "Couch Base", new Vector3(-w*.30f,floorY+.42f,d*.05f), new Vector3(Mathf.Min(3.4f,w*.32f),.65f,1.0f), m.fabric, true);
            Box(root, "Couch Back", new Vector3(-w*.30f,floorY+.88f,d*.48f/5f), new Vector3(Mathf.Min(3.4f,w*.32f),.75f,.18f), m.fabric, true);
            Box(root, "Coffee Table", new Vector3(-w*.12f,floorY+.42f,d*.20f), new Vector3(1.5f,.12f,.85f), m.wood, true);
            Box(root, "TV Stand", new Vector3(w*.32f,floorY+.38f,d*.14f), new Vector3(1.8f,.75f,.45f), m.wood, true);
            Box(root, "TV", new Vector3(w*.32f,floorY+1.15f,d*.14f), new Vector3(1.45f,.82f,.10f), m.carDark, false);

            Box(root, "Kitchen Counter", new Vector3(w*.28f,floorY+.55f,-d*.36f), new Vector3(w*.34f,1.1f,.75f), m.counter, true);
            Box(root, "Kitchen Sink", new Vector3(w*.22f,floorY+1.13f,-d*.36f), new Vector3(.75f,.08f,.45f), m.metal, false);
            Box(root, "Refrigerator", new Vector3(w*.40f,floorY+1.05f,-d*.30f), new Vector3(.85f,2.1f,.8f), m.appliance, true);

            Box(root, "Bed", new Vector3(-w*.30f,floorY+.40f,-d*.30f), new Vector3(Mathf.Min(2.2f,w*.25f),.7f,2.6f), m.bed, true);
            Box(root, "Dresser", new Vector3(-w*.05f,floorY+.55f,-d*.38f), new Vector3(1.4f,1.1f,.55f), m.wood, true);
            Box(root, "Dining Table", new Vector3(w*.08f,floorY+.72f,-d*.04f), new Vector3(1.7f,.14f,1.15f), m.wood, true);
            Chair(root, new Vector3(w*.08f-1.15f,floorY,-d*.04f), Quaternion.Euler(0,90,0), m);
            Chair(root, new Vector3(w*.08f+1.15f,floorY,-d*.04f), Quaternion.Euler(0,-90,0), m);
        }

        static void BuildWorkFurniture(Transform root, string type, float w, float d, float floorY, InteriorMats m)
        {
            string lower = (type ?? "").ToLowerInvariant();

            if (lower.Contains("garage") || lower.Contains("fire"))
            {
                Box(root, "Rear Workbench", new Vector3(0,floorY+.65f,-d*.40f), new Vector3(w*.52f,1.3f,.75f), m.wood, true);
                Box(root, "Tool Cabinet Left", new Vector3(-w*.40f,floorY+.9f,-d*.23f), new Vector3(1.0f,1.8f,.7f), m.metal, true);
                Box(root, "Tool Cabinet Right", new Vector3(w*.40f,floorY+.9f,-d*.23f), new Vector3(1.0f,1.8f,.7f), m.metal, true);
                Box(root, "Service Pad Left", new Vector3(-w*.20f,floorY+.04f,d*.08f), new Vector3(2.2f,.08f,5.0f), m.carDark, false);
                Box(root, "Service Pad Right", new Vector3(w*.20f,floorY+.04f,d*.08f), new Vector3(2.2f,.08f,5.0f), m.carDark, false);
                Box(root, "Parts Shelf", new Vector3(-w*.38f,floorY+1.2f,d*.22f), new Vector3(.8f,2.4f,2.5f), m.metal, true);
            }
            else if (lower.Contains("clinic"))
            {
                Box(root, "Reception Counter", new Vector3(0,floorY+.62f,d*.18f), new Vector3(w*.48f,1.24f,.7f), m.counter, true);
                Box(root, "Exam Bed Left", new Vector3(-w*.26f,floorY+.55f,-d*.22f), new Vector3(2.0f,1.0f,.85f), m.bed, true);
                Box(root, "Exam Bed Right", new Vector3(w*.26f,floorY+.55f,-d*.22f), new Vector3(2.0f,1.0f,.85f), m.bed, true);
                Box(root, "Medical Cabinet", new Vector3(w*.40f,floorY+1.0f,-d*.38f), new Vector3(1.0f,2.0f,.55f), m.appliance, true);
            }
            else if (lower.Contains("cafe"))
            {
                Box(root, "Cafe Counter", new Vector3(0,floorY+.65f,-d*.31f), new Vector3(w*.55f,1.3f,.8f), m.counter, true);
                Box(root, "Back Counter", new Vector3(0,floorY+.55f,-d*.43f), new Vector3(w*.52f,1.1f,.55f), m.appliance, true);
                for (int i=-1;i<=1;i+=2)
                {
                    Box(root, "Cafe Table " + i, new Vector3(i*w*.23f,floorY+.72f,d*.08f), new Vector3(1.25f,.12f,1.25f), m.wood, true);
                    Chair(root, new Vector3(i*w*.23f,floorY,d*.08f+1.05f), Quaternion.identity, m);
                }
            }
            else if (lower.Contains("store") || lower.Contains("supply") || lower.Contains("post"))
            {
                Box(root, "Service Counter", new Vector3(0,floorY+.62f,-d*.31f), new Vector3(w*.55f,1.24f,.75f), m.counter, true);
                for (int side=-1;side<=1;side+=2)
                {
                    Box(root, "Shelf " + side + "A", new Vector3(side*w*.33f,floorY+.85f,d*.06f), new Vector3(1.0f,1.7f,d*.34f), m.wood, true);
                    Box(root, "Shelf " + side + "B", new Vector3(side*w*.33f,floorY+.85f,-d*.20f), new Vector3(1.0f,1.7f,d*.22f), m.wood, true);
                }
                Box(root, "Register", new Vector3(w*.18f,floorY+1.35f,-d*.30f), new Vector3(.48f,.28f,.42f), m.appliance, false);
            }
            else if (lower.Contains("farm"))
            {
                Box(root, "Farm Workbench", new Vector3(0,floorY+.68f,-d*.38f), new Vector3(w*.48f,1.35f,.8f), m.wood, true);
                Box(root, "Feed Pallets", new Vector3(-w*.30f,floorY+.45f,d*.10f), new Vector3(2.4f,.9f,2.2f), m.counter, true);
                Box(root, "Equipment Rack", new Vector3(w*.32f,floorY+1.2f,d*.10f), new Vector3(1.0f,2.4f,3.0f), m.metal, true);
            }
            else
            {
                for (int i=-1;i<=1;i+=2)
                {
                    Box(root, "Desk " + i, new Vector3(i*w*.22f,floorY+.72f,-d*.10f), new Vector3(2.0f,.12f,1.0f), m.wood, true);
                    Chair(root, new Vector3(i*w*.22f,floorY,-d*.10f+1.0f), Quaternion.identity, m);
                }
                Box(root, "Records Cabinet", new Vector3(w*.37f,floorY+1.0f,-d*.38f), new Vector3(1.1f,2.0f,.6f), m.metal, true);
                Box(root, "Public Counter", new Vector3(0,floorY+.62f,d*.22f), new Vector3(w*.42f,1.24f,.7f), m.counter, true);
            }
        }

        static SpotDef[] WorkplaceSpots(string type, float w, float d, float floorY)
        {
            string lower = (type ?? "").ToLowerInvariant();
            if (lower.Contains("garage"))
                return new[] {
                    new SpotDef("working at the service bay", new Vector3(-w*.20f,floorY+.05f,d*.08f)),
                    new SpotDef("organizing tools", new Vector3(w*.38f,floorY+.05f,-d*.22f)),
                    new SpotDef("checking parts at the workbench", new Vector3(0,floorY+.05f,-d*.30f))
                };
            if (lower.Contains("fire"))
                return new[] {
                    new SpotDef("checking emergency equipment", new Vector3(-w*.22f,floorY+.05f,d*.08f)),
                    new SpotDef("inspecting station equipment", new Vector3(w*.22f,floorY+.05f,d*.08f)),
                    new SpotDef("doing station work", new Vector3(0,floorY+.05f,-d*.28f))
                };
            if (lower.Contains("clinic"))
                return new[] {
                    new SpotDef("helping at reception", new Vector3(0,floorY+.05f,d*.25f)),
                    new SpotDef("checking an exam room", new Vector3(-w*.26f,floorY+.05f,-d*.20f)),
                    new SpotDef("checking medical supplies", new Vector3(w*.34f,floorY+.05f,-d*.30f))
                };
            if (lower.Contains("cafe"))
                return new[] {
                    new SpotDef("working the counter", new Vector3(0,floorY+.05f,-d*.23f)),
                    new SpotDef("preparing an order", new Vector3(w*.22f,floorY+.05f,-d*.34f)),
                    new SpotDef("cleaning a table", new Vector3(-w*.23f,floorY+.05f,d*.08f))
                };
            if (lower.Contains("store") || lower.Contains("supply"))
                return new[] {
                    new SpotDef("helping at the counter", new Vector3(0,floorY+.05f,-d*.23f)),
                    new SpotDef("stocking shelves", new Vector3(-w*.23f,floorY+.05f,d*.03f)),
                    new SpotDef("checking inventory", new Vector3(w*.23f,floorY+.05f,-d*.12f))
                };
            if (lower.Contains("post"))
                return new[] {
                    new SpotDef("sorting mail", new Vector3(-w*.22f,floorY+.05f,-d*.22f)),
                    new SpotDef("checking parcels", new Vector3(w*.22f,floorY+.05f,-d*.22f)),
                    new SpotDef("working the counter", new Vector3(0,floorY+.05f,d*.18f))
                };
            if (lower.Contains("farm"))
                return new[] {
                    new SpotDef("checking farm equipment", new Vector3(w*.28f,floorY+.05f,d*.08f)),
                    new SpotDef("handling chores in the barn", new Vector3(0,floorY+.05f,-d*.18f)),
                    new SpotDef("checking feed and supplies", new Vector3(-w*.28f,floorY+.05f,d*.05f))
                };
            return new[] {
                new SpotDef("doing paperwork", new Vector3(-w*.20f,floorY+.05f,-d*.08f)),
                new SpotDef("checking records", new Vector3(w*.20f,floorY+.05f,-d*.08f)),
                new SpotDef("helping at the public counter", new Vector3(0,floorY+.05f,d*.18f))
            };
        }

        struct SpotDef
        {
            public string label;
            public Vector3 localPosition;
            public SpotDef(string l, Vector3 p) { label=l; localPosition=p; }
        }

        static void SetIndoorActivitySpots(H51CitizenAnchor anchor, Transform building, SpotDef[] defs)
        {
            if (!anchor || !building || defs == null) return;

            for (int i = anchor.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = anchor.transform.GetChild(i);
                if (child.GetComponent<H51CitizenActivitySpot>())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            for (int i = 0; i < defs.Length; i++)
            {
                Transform spot = New("Indoor Activity - " + defs[i].label, anchor.transform);
                spot.position = building.TransformPoint(defs[i].localPosition);
                spot.rotation = building.rotation;
                H51CitizenActivitySpot component = spot.gameObject.AddComponent<H51CitizenActivitySpot>();
                component.Configure(defs[i].label, "Interact", 4f, 10f);
            }

            EditorUtility.SetDirty(anchor.gameObject);
        }

        static H51TrafficNetwork BuildTrafficNetwork(
            Transform world,
            Transform pass,
            out List<Vector3> nodes,
            out List<H51TrafficNetwork.Edge> edges)
        {
            Transform old = FindChild(pass, "Citizen Traffic Network");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform root = New("Citizen Traffic Network", pass);

            nodes = new List<Vector3>();
            edges = new List<H51TrafficNetwork.Edge>();
            HashSet<ulong> edgeKeys = new HashSet<ulong>();

            Transform roads = FindChild(world, "Road Network");
            if (roads)
            {
                MeshFilter[] surfaces = roads.GetComponentsInChildren<MeshFilter>(true);
                for (int r = 0; r < surfaces.Length; r++)
                {
                    MeshFilter mf = surfaces[r];
                    if (!mf || !mf.sharedMesh || mf.gameObject.name != "Road Surface") continue;

                    Vector3[] vertices = mf.sharedMesh.vertices;
                    if (vertices == null || vertices.Length < 4) continue;

                    List<Vector3> centers = new List<Vector3>();
                    for (int i = 0; i + 1 < vertices.Length; i += 2)
                        centers.Add(mf.transform.TransformPoint((vertices[i] + vertices[i+1]) * .5f));

                    List<int> roadNodes = new List<int>();
                    if (centers.Count > 0)
                    {
                        int first = nodes.Count;
                        nodes.Add(centers[0]);
                        roadNodes.Add(first);

                        for (int i = 1; i < centers.Count; i++)
                        {
                            Vector3 a = centers[i - 1];
                            Vector3 b = centers[i];
                            float distance = Vector3.Distance(a, b);
                            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 7f));

                            for (int step = 1; step <= steps; step++)
                            {
                                Vector3 center = Vector3.Lerp(a, b, step / (float)steps);
                                int nodeIndex = nodes.Count;
                                nodes.Add(center);
                                AddTrafficEdge(nodes, edges, edgeKeys, roadNodes[roadNodes.Count - 1], nodeIndex);
                                roadNodes.Add(nodeIndex);
                            }
                        }
                    }
                }
            }

            float intersectionThreshold = 7.5f;
            float thresholdSqr = intersectionThreshold * intersectionThreshold;
            for (int a = 0; a < nodes.Count; a++)
            {
                for (int b = a + 1; b < nodes.Count; b++)
                {
                    Vector3 d = nodes[a] - nodes[b];
                    d.y = 0f;
                    if (d.sqrMagnitude > thresholdSqr) continue;
                    AddTrafficEdge(nodes, edges, edgeKeys, a, b);
                }
            }

            H51TrafficNetwork network = root.gameObject.AddComponent<H51TrafficNetwork>();
            network.Configure(nodes, edges);
            EditorUtility.SetDirty(network);
            return network;
        }

        static void AddTrafficEdge(
            List<Vector3> nodes,
            List<H51TrafficNetwork.Edge> edges,
            HashSet<ulong> keys,
            int a,
            int b)
        {
            if (a == b || a < 0 || b < 0 || a >= nodes.Count || b >= nodes.Count) return;
            int lo = Mathf.Min(a,b), hi = Mathf.Max(a,b);
            ulong key = ((ulong)(uint)lo << 32) | (uint)hi;
            if (!keys.Add(key)) return;
            edges.Add(new H51TrafficNetwork.Edge(a,b,Vector3.Distance(nodes[a],nodes[b])));
        }

        static int BuildCitizenCars(
            Transform citizenRoot,
            H51CitizenRegistry registry,
            H51TrafficNetwork network,
            List<Vector3> nodes,
            List<H51TrafficNetwork.Edge> edges,
            Terrain terrain,
            InteriorMats mats)
        {
            Transform old = FindChild(citizenRoot, "Citizen Vehicles");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform root = New("Citizen Vehicles", citizenRoot);

            if (!network || nodes == null || nodes.Count == 0) return 0;

            H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            Dictionary<string,int> carsPerHome = new Dictionary<string,int>();
            int made = 0;

            for (int i = 0; i < citizens.Length; i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (!citizen || string.IsNullOrEmpty(citizen.HomeAnchorId)) continue;

                int seed = citizen.AppearanceSeed & 0x7fffffff;
                if (seed % 100 >= 72) continue;

                carsPerHome.TryGetValue(citizen.HomeAnchorId, out int atHome);
                if (atHome >= 2) continue;

                H51CitizenAnchor home = registry.FindAnchor(citizen.HomeAnchorId);
                if (!home) continue;

                int node = FindNearestNode(nodes, home.transform.position);
                if (node < 0) continue;

                Vector3 tangent = NodeTangent(nodes, edges, node);
                if (tangent.sqrMagnitude < .01f) tangent = home.transform.forward;
                tangent.y = 0f;
                tangent.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, tangent);

                float lateral = atHome == 0 ? 4.7f : 6.8f;
                Vector3 p = nodes[node] + right * lateral;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y + .08f;

                Transform car = New("Citizen Car - " + citizen.DisplayName, root);
                car.position = p;
                car.rotation = Quaternion.LookRotation(tangent, Vector3.up);

                Material paint = mats.carPaint[(seed + made) % mats.carPaint.Length];
                BuildCarVisual(car, paint, mats.carDark, mats.tire);
                foreach (Transform child in car.GetComponentsInChildren<Transform>(true))
                    child.gameObject.isStatic = false;

                NavMeshModifier modifier = car.gameObject.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;

                BoxCollider collider = car.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f,.72f,0f);
                collider.size = new Vector3(1.85f,1.45f,4.35f);

                Rigidbody body = car.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                H51CitizenVehicle vehicle = car.gameObject.AddComponent<H51CitizenVehicle>();
                vehicle.Configure(
                    "CAR-" + citizen.CitizenId,
                    citizen.CitizenId,
                    citizen.DisplayName + "'s car",
                    network,
                    Mathf.Lerp(8.5f, 12.5f, (seed % 31) / 30f));

                carsPerHome[citizen.HomeAnchorId] = atHome + 1;
                made++;
            }

            return made;
        }

        static void BuildCarVisual(Transform car, Material paint, Material dark, Material tire)
        {
            Box(car, "Car Body", new Vector3(0f,.62f,0f), new Vector3(1.82f,.62f,4.25f), paint, false);
            Box(car, "Car Cabin", new Vector3(0f,1.16f,-.18f), new Vector3(1.62f,.75f,2.05f), dark, false);
            Box(car, "Front Glass", new Vector3(0f,1.35f,.88f), new Vector3(1.48f,.48f,.08f), dark, false);
            Box(car, "Rear Glass", new Vector3(0f,1.35f,-1.14f), new Vector3(1.48f,.48f,.08f), dark, false);
            Box(car, "Front Bumper", new Vector3(0f,.48f,2.18f), new Vector3(1.75f,.18f,.16f), dark, false);
            Box(car, "Rear Bumper", new Vector3(0f,.48f,-2.18f), new Vector3(1.75f,.18f,.16f), dark, false);

            Wheel(car, "Wheel FL", new Vector3(-.96f,.45f,1.35f), tire);
            Wheel(car, "Wheel FR", new Vector3(.96f,.45f,1.35f), tire);
            Wheel(car, "Wheel RL", new Vector3(-.96f,.45f,-1.38f), tire);
            Wheel(car, "Wheel RR", new Vector3(.96f,.45f,-1.38f), tire);
        }

        static void Wheel(Transform parent, string name, Vector3 pos, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f,0f,90f);
            g.transform.localScale = new Vector3(.34f,.16f,.34f);
            Renderer r = g.GetComponent<Renderer>();
            if (r) r.sharedMaterial = mat;
            Collider c = g.GetComponent<Collider>();
            if (c) UnityEngine.Object.DestroyImmediate(c);
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

        static H51EnterableBuilding FindContainingBuilding(
            H51EnterableBuilding[] buildings,
            Vector3 position,
            H51EnterableBuildingKind? requiredKind)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building) continue;
                if (requiredKind.HasValue && building.Kind != requiredKind.Value) continue;

                Collider[] colliders = building.GetComponentsInChildren<Collider>(true);
                Bounds bounds = new Bounds(building.transform.position, Vector3.zero);
                bool set = false;
                for (int c = 0; c < colliders.Length; c++)
                {
                    Collider col = colliders[c];
                    if (!col || !col.enabled) continue;
                    if (!set) { bounds = col.bounds; set = true; }
                    else bounds.Encapsulate(col.bounds);
                }

                if (!set) continue;
                Vector3 p = position;
                if (p.x >= bounds.min.x && p.x <= bounds.max.x &&
                    p.z >= bounds.min.z && p.z <= bounds.max.z)
                    return building;
            }
            return null;
        }

        static void DisableDoorObjects(Transform building, bool service)
        {
            foreach (Transform t in building.GetComponentsInChildren<Transform>(true))
            {
                if (t == building) continue;
                string n = t.name;
                bool door =
                    (n.Contains("Wood Door") || n == "Front Door" || n.Contains("Service Door")) ||
                    (service && n.Contains("Service Bay"));
                if (door) t.gameObject.SetActive(false);
            }
        }

        static void Chair(Transform root, Vector3 basePos, Quaternion rot, InteriorMats m)
        {
            Transform chair = New("Chair", root);
            chair.localPosition = basePos;
            chair.localRotation = rot;
            Box(chair, "Seat", new Vector3(0f,.48f,0f), new Vector3(.72f,.12f,.72f), m.wood, true);
            Box(chair, "Back", new Vector3(0f,.92f,-.31f), new Vector3(.72f,.82f,.12f), m.wood, true);
        }

        static GameObject Box(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = localScale;
            Renderer r = g.GetComponent<Renderer>();
            if (r) r.sharedMaterial = material;
            Collider c = g.GetComponent<Collider>();
            if (!collider && c) UnityEngine.Object.DestroyImmediate(c);
            g.isStatic = true;
            return g;
        }

        static InteriorMats BuildMaterials()
        {
            InteriorMats m = new InteriorMats();
            m.floor = MaterialAsset("Interior Floor", new Color(.31f,.27f,.21f), .02f);
            m.interiorWall = MaterialAsset("Interior Wall", new Color(.72f,.70f,.65f), 0f);
            m.wood = MaterialAsset("Interior Wood", new Color(.28f,.16f,.08f), .02f);
            m.metal = MaterialAsset("Interior Metal", new Color(.25f,.27f,.28f), .05f);
            m.fabric = MaterialAsset("Fabric", new Color(.20f,.26f,.30f), 0f);
            m.counter = MaterialAsset("Counter", new Color(.43f,.36f,.27f), .02f);
            m.appliance = MaterialAsset("Appliance", new Color(.43f,.45f,.44f), .08f);
            m.bed = MaterialAsset("Bed Fabric", new Color(.55f,.60f,.58f), 0f);
            m.carDark = MaterialAsset("Car Dark", new Color(.055f,.065f,.075f), .12f);
            m.tire = MaterialAsset("Tire", new Color(.025f,.025f,.025f), 0f);
            m.carPaint = new[]
            {
                MaterialAsset("Car Blue", new Color(.08f,.20f,.34f), .12f),
                MaterialAsset("Car Red", new Color(.42f,.08f,.06f), .12f),
                MaterialAsset("Car Green", new Color(.11f,.25f,.14f), .10f),
                MaterialAsset("Car Cream", new Color(.62f,.58f,.48f), .10f),
                MaterialAsset("Car Gray", new Color(.25f,.27f,.28f), .11f),
                MaterialAsset("Car Brown", new Color(.30f,.16f,.09f), .08f)
            };
            return m;
        }

        static Material MaterialAsset(string name, Color color, float smoothness)
        {
            Ensure(Gen + "/Materials");
            string path = Gen + "/Materials/" + Safe(name) + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "H51 " + name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static int FindNearestNode(List<Vector3> nodes, Vector3 point)
        {
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 d = nodes[i] - point;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        static Vector3 NodeTangent(
            List<Vector3> nodes,
            List<H51TrafficNetwork.Edge> edges,
            int node)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                H51TrafficNetwork.Edge e = edges[i];
                int other = e.a == node ? e.b : e.b == node ? e.a : -1;
                if (other < 0 || other >= nodes.Count) continue;
                Vector3 tangent = nodes[other] - nodes[node];
                tangent.y = 0f;
                if (tangent.sqrMagnitude > .01f) return tangent.normalized;
            }
            return Vector3.forward;
        }

        static List<Transform> Houses(Transform town)
        {
            List<Transform> houses = new List<Transform>();
            if (!town) return houses;
            for (int i = 0; i < town.childCount; i++)
            {
                Transform c = town.GetChild(i);
                if (c.name.StartsWith("Detailed House", StringComparison.Ordinal) ||
                    c.name.StartsWith("Building", StringComparison.Ordinal))
                    houses.Add(c);
            }
            return houses;
        }

        static Terrain FindTerrain()
        {
            GameObject g = Find(TerrainName);
            Terrain terrain = g ? (g.GetComponent<Terrain>() ?? g.GetComponentInChildren<Terrain>(true)) : null;
            if (terrain) return terrain;
            Terrain[] all = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        static GameObject Find(string name)
        {
            GameObject direct = GameObject.Find(name);
            if (direct) return direct;
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        static Transform DirectChild(Transform root, string name)
        {
            if (!root) return null;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == name) return root.GetChild(i);
            return null;
        }

        static Transform New(string name, Transform parent)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static void Ensure(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string Safe(string value)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
