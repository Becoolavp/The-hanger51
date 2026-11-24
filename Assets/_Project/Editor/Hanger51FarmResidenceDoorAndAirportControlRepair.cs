using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hanger51.NPCs;
using Hanger51.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51FarmResidenceDoorAndAirportControlRepair
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string FarmRootName = "Working Farmsteads";
        const string FarmResidenceRootName = "H51 Rural Farm Residence Anchors";
        const string FarmNavigationRootName = "H51 Rural Farm Navigation";
        const string AirportRootName = "Hanger 51 Airport Complex";
        const string HangarRootName = "Hanger 51 Test Hangar";
        const string RunwayControlName = "Hanger 51 Runway Lighting Control";
        const string DoorRepairRootName = "H51 Interactive Front Door";
        const string GeneratedFolder =
            "Assets/_Project/Environment/Generated/HomeDoorAndFarmRepair";
        const string DoorMaterialPath = GeneratedFolder + "/RepairedHouseDoor.mat";
        const string HandleMaterialPath = GeneratedFolder + "/RepairedDoorHandle.mat";

        [MenuItem("Hanger 51/World/Current/154 - Repair House Doors Farm Residents And Airport Light Control")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 154 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = GameObject.Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform,CitizenRootName) : null;
            H51CitizenRegistry registry =
                citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;

            if (!world || !citizenRoot || !registry)
            {
                Debug.LogError(
                    "Step 154 failed. The current world/citizen system/registry is missing.");
                return;
            }

            EnsureFolder(GeneratedFolder);
            Material fallbackDoor = CreateMaterial(
                DoorMaterialPath,
                new Color(.24f,.12f,.055f,1f),
                .02f,
                .24f);
            Material handleMaterial = CreateMaterial(
                HandleMaterialPath,
                new Color(.58f,.43f,.16f,1f),
                .55f,
                .58f);

            int repairedDoors = RepairHomeDoors(
                world.transform,
                fallbackDoor,
                handleMaterial);

            bool runwayMoved = MoveRunwayControlInsideAirport();

            Transform farms = FindChild(world.transform,FarmRootName);
            int farmAnchors = 0;
            int farmNavMeshes = 0;
            int farmersAssigned = 0;

            if (farms)
            {
                BuildFarmResidences(
                    citizenRoot,
                    farms,
                    registry,
                    out farmAnchors,
                    out farmNavMeshes,
                    out farmersAssigned);
            }
            else
            {
                Debug.LogWarning(
                    "Step 154 could not find Working Farmsteads, so farmer residency was skipped.");
            }

            registry.RefreshSceneReferences();
            EditorUtility.SetDirty(registry);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 154 complete. Rebuilt interactive house doors=" + repairedDoors +
                ", runway control moved inside airport wall=" + runwayMoved +
                ", rural farm anchors=" + farmAnchors +
                ", rural farm NavMeshes=" + farmNavMeshes +
                ", farmers assigned to farmsteads=" + farmersAssigned +
                ". Run Step 155 to validate.");
        }

        [MenuItem("Hanger 51/World/Current/155 - Validate House Doors Farm Residents And Airport Light Control")]
        public static void Validate()
        {
            GameObject world = GameObject.Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform,CitizenRootName) : null;
            Transform farms = world ? FindChild(world.transform,FarmRootName) : null;

            if (!world || !citizenRoot)
            {
                Debug.LogError("Step 155 failed. Run Step 154 first.");
                return;
            }

            H51EnterableBuilding[] homes =
                world.GetComponentsInChildren<H51EnterableBuilding>(true)
                    .Where(b => b && b.Kind == H51EnterableBuildingKind.Home)
                    .ToArray();

            int goodDoors = 0;
            int badHandles = 0;
            for (int i=0;i<homes.Length;i++)
            {
                H51EnterableBuilding home = homes[i];
                Transform repair = FindChild(home.transform,DoorRepairRootName);
                H51TownDoor door = repair ? repair.GetComponent<H51TownDoor>() : null;
                Transform leaf = repair ? DirectChild(repair,"Door Leaf") : null;
                Transform outsideHandle = leaf ? DirectChild(leaf,"Outside Door Handle") : null;
                Transform insideHandle = leaf ? DirectChild(leaf,"Inside Door Handle") : null;

                bool doorReady =
                    door &&
                    leaf &&
                    leaf.GetComponent<Collider>() &&
                    outsideHandle &&
                    insideHandle;

                if (doorReady) goodDoors++;
                if (!outsideHandle || !insideHandle ||
                    (outsideHandle && outsideHandle.parent != leaf) ||
                    (insideHandle && insideHandle.parent != leaf))
                    badHandles++;
            }

            H51CitizenIdentity[] citizens =
                citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            H51CitizenAnchor[] anchors =
                citizenRoot.GetComponentsInChildren<H51CitizenAnchor>(true);

            Dictionary<string,H51CitizenAnchor> anchorById =
                anchors.Where(a => a && !string.IsNullOrWhiteSpace(a.AnchorId))
                    .ToDictionary(a => a.AnchorId,a => a);

            int ruralFarmers = 0;
            int badFarmerAssignments = 0;
            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity c = citizens[i];
                if (!c || !string.Equals(c.Occupation,"Farmer",StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!c.TownName.StartsWith("Farmstead ",StringComparison.Ordinal))
                    continue;

                ruralFarmers++;

                bool homeReady =
                    !string.IsNullOrWhiteSpace(c.HomeAnchorId) &&
                    anchorById.TryGetValue(c.HomeAnchorId,out H51CitizenAnchor home) &&
                    home &&
                    home.Kind == H51CitizenAnchorKind.Home;
                bool workReady =
                    !string.IsNullOrWhiteSpace(c.WorkAnchorId) &&
                    anchorById.TryGetValue(c.WorkAnchorId,out H51CitizenAnchor work) &&
                    work &&
                    work.Kind == H51CitizenAnchorKind.Work;

                if (!homeReady || !workReady || home.TownName != c.TownName || work.TownName != c.TownName)
                    badFarmerAssignments++;
            }

            NavMeshSurface[] farmSurfaces =
                citizenRoot.GetComponentsInChildren<NavMeshSurface>(true)
                    .Where(s => s && s.name.StartsWith("NavMesh - Farmstead ",StringComparison.Ordinal))
                    .ToArray();
            int farmNavReady = farmSurfaces.Count(s => s && s.navMeshData);

            H51RunwayLightSwitch runwaySwitch =
                UnityEngine.Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);
            bool runwayInside = runwaySwitch && IsControlInsideAirportWall(runwaySwitch.transform);

            int farmCount = farms
                ? farms.Cast<Transform>()
                    .Count(t => t && t.name.StartsWith("Working Farm ",StringComparison.Ordinal))
                : 0;

            bool ok =
                homes.Length > 0 &&
                goodDoors == homes.Length &&
                badHandles == 0 &&
                farmCount > 0 &&
                ruralFarmers >= Mathf.Min(farmCount,citizens.Length) &&
                badFarmerAssignments == 0 &&
                farmNavReady >= farmCount &&
                runwayInside;

            if (ok)
            {
                Debug.Log(
                    "Step 155 passed. Interactive home doors=" + goodDoors + "/" + homes.Length +
                    ", detached/missing handles=" + badHandles +
                    ", working farmsteads=" + farmCount +
                    ", farm-resident farmers=" + ruralFarmers +
                    ", farm NavMeshes=" + farmNavReady +
                    ", runway control inside airport wall=" + runwayInside + ".");
            }
            else
            {
                Debug.LogError(
                    "Step 155 failed. Interactive home doors=" + goodDoors + "/" + homes.Length +
                    ", detached/missing handles=" + badHandles +
                    ", working farmsteads=" + farmCount +
                    ", farm-resident farmers=" + ruralFarmers +
                    ", bad farmer assignments=" + badFarmerAssignments +
                    ", farm NavMeshes=" + farmNavReady + "/" + farmCount +
                    ", runway control inside airport wall=" + runwayInside + ".");
            }
        }

        static int RepairHomeDoors(
            Transform world,
            Material fallbackDoor,
            Material handleMaterial)
        {
            H51EnterableBuilding[] homes =
                world.GetComponentsInChildren<H51EnterableBuilding>(true)
                    .Where(b => b && b.Kind == H51EnterableBuildingKind.Home)
                    .ToArray();

            int repaired = 0;

            for (int i=0;i<homes.Length;i++)
            {
                H51EnterableBuilding home = homes[i];
                Transform interior = FindChild(home.transform,"H51 Enterable Interior");
                if (!interior) continue;

                Transform header = DirectChild(interior,"Door Header");
                Transform floor = DirectChild(interior,"Interior Floor");
                if (!header || !floor) continue;

                Material doorMaterial = FindLegacyDoorMaterial(home.transform) ?? fallbackDoor;

                Transform oldRepair = DirectChild(interior,DoorRepairRootName);
                if (oldRepair)
                    Undo.DestroyObjectImmediate(oldRepair.gameObject);

                DisableLegacyDoorLeaves(home.transform);

                float centerX = header.localPosition.x;
                float frontZ = header.localPosition.z;
                float bottom =
                    floor.localPosition.y - Mathf.Abs(floor.localScale.y) * .5f;
                float headerBottom =
                    header.localPosition.y - Mathf.Abs(header.localScale.y) * .5f;
                float doorHeight = Mathf.Max(2.0f,headerBottom - bottom);
                float doorWidth = Mathf.Max(1.0f,Mathf.Abs(header.localScale.x));

                float leafWidth = Mathf.Max(.90f,doorWidth - .16f);
                float leafHeight = Mathf.Max(1.95f,doorHeight - .12f);
                float leafDepth = .10f;

                Transform pivot = New(DoorRepairRootName,interior);
                Vector3 hingePosition = new Vector3(
                    centerX - leafWidth * .5f,
                    bottom + leafHeight * .5f,
                    frontZ - .14f);
                pivot.localPosition = hingePosition;
                pivot.localRotation = Quaternion.identity;

                GameObject leafObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(leafObject,"Create repaired house door leaf");
                leafObject.name = "Door Leaf";
                leafObject.transform.SetParent(pivot,false);
                leafObject.transform.localPosition =
                    new Vector3(leafWidth * .5f,0f,0f);
                leafObject.transform.localRotation = Quaternion.identity;
                leafObject.transform.localScale =
                    new Vector3(leafWidth,leafHeight,leafDepth);

                Renderer leafRenderer = leafObject.GetComponent<Renderer>();
                if (leafRenderer) leafRenderer.sharedMaterial = doorMaterial;

                CreateHandle(
                    leafObject.transform,
                    "Outside Door Handle",
                    new Vector3(leafWidth * .36f,0f,-leafDepth * .75f),
                    handleMaterial);
                CreateHandle(
                    leafObject.transform,
                    "Inside Door Handle",
                    new Vector3(leafWidth * .36f,0f,leafDepth * .75f),
                    handleMaterial);

                H51TownDoor interactive =
                    pivot.gameObject.AddComponent<H51TownDoor>();
                interactive.Configure(
                    hingePosition,
                    Quaternion.identity,
                    hingePosition,
                    Quaternion.Euler(0f,-88f,0f),
                    false);

                EditorUtility.SetDirty(interactive);
                EditorUtility.SetDirty(home);
                repaired++;
            }

            return repaired;
        }

        static void CreateHandle(
            Transform leaf,
            string objectName,
            Vector3 localPosition,
            Material material)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(handle,"Create attached door handle");
            handle.name = objectName;
            handle.transform.SetParent(leaf,false);
            handle.transform.localPosition = localPosition;
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = new Vector3(.11f,.11f,.085f);

            Renderer renderer = handle.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;

            Collider collider = handle.GetComponent<Collider>();
            if (collider) Undo.DestroyObjectImmediate(collider);
        }

        static Material FindLegacyDoorMaterial(Transform home)
        {
            Renderer[] renderers = home.GetComponentsInChildren<Renderer>(true);
            for (int i=0;i<renderers.Length;i++)
            {
                Renderer r = renderers[i];
                if (!r) continue;
                string n = r.name;
                if (n == "Open Door" ||
                    n == "Facade Door" ||
                    n == "Civic Door")
                    return r.sharedMaterial;
            }
            return null;
        }

        static void DisableLegacyDoorLeaves(Transform home)
        {
            Transform[] all = home.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
            {
                Transform t = all[i];
                if (!t || t == home) continue;
                if (t.name == DoorRepairRootName || t.IsChildOf(
                    FindChild(home,DoorRepairRootName)))
                    continue;

                string n = t.name;
                bool legacy =
                    n == "Open Door" ||
                    n == "Door Knob" ||
                    n == "Facade Door" ||
                    n == "Facade Door Knob";

                if (legacy)
                    t.gameObject.SetActive(false);
            }
        }

        static void BuildFarmResidences(
            Transform citizenRoot,
            Transform farms,
            H51CitizenRegistry registry,
            out int anchorsCreated,
            out int navMeshesBuilt,
            out int farmersAssigned)
        {
            anchorsCreated = 0;
            navMeshesBuilt = 0;
            farmersAssigned = 0;

            Transform oldAnchors = DirectChild(citizenRoot,FarmResidenceRootName);
            if (oldAnchors)
                Undo.DestroyObjectImmediate(oldAnchors.gameObject);

            Transform oldNavigation = DirectChild(citizenRoot,FarmNavigationRootName);
            if (oldNavigation)
                Undo.DestroyObjectImmediate(oldNavigation.gameObject);

            Transform anchorRoot = New(FarmResidenceRootName,citizenRoot);
            Transform navigationRoot = New(FarmNavigationRootName,citizenRoot);

            List<Transform> farmList = new List<Transform>();
            for (int i=0;i<farms.childCount;i++)
            {
                Transform farm = farms.GetChild(i);
                if (farm && farm.name.StartsWith("Working Farm ",StringComparison.Ordinal))
                    farmList.Add(farm);
            }
            farmList.Sort((a,b) => string.CompareOrdinal(a.name,b.name));

            H51CitizenIdentity[] citizens =
                citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true)
                    .Where(c => c)
                    .OrderBy(c => FarmerPriority(c))
                    .ThenBy(c => c.CitizenId)
                    .ToArray();

            HashSet<H51CitizenIdentity> used = new HashSet<H51CitizenIdentity>();

            for (int i=0;i<farmList.Count;i++)
            {
                Transform farm = farmList[i];
                Transform house = DirectChild(farm,"Farm House");
                Transform barn = DirectChild(farm,"Main Barn");
                if (!house || !barn) continue;

                string farmTown = "Farmstead " + (i + 1).ToString("00");
                string workplaceId = "rural-farm-" + (i + 1).ToString("000");
                string homeId = workplaceId + "-home";
                string workId = workplaceId + "-work";

                Vector3 homePosition = farm.TransformPoint(
                    house.localPosition +
                    new Vector3(
                        0f,
                        -Mathf.Abs(house.localScale.y) * .5f + .12f,
                        -Mathf.Abs(house.localScale.z) * .5f - 1.15f));

                Vector3 workPosition = farm.TransformPoint(
                    barn.localPosition +
                    new Vector3(
                        0f,
                        -Mathf.Abs(barn.localScale.y) * .5f + .12f,
                        -Mathf.Abs(barn.localScale.z) * .5f - 2.0f));

                Transform farmAnchorGroup =
                    New(farmTown + " Anchors",anchorRoot);

                H51CitizenAnchor homeAnchor =
                    CreateAnchor(
                        farmAnchorGroup,
                        "Farmhouse Home",
                        homePosition,
                        farm.forward,
                        homeId,
                        farmTown,
                        H51CitizenAnchorKind.Home,
                        "Farmer",
                        2,
                        farmTown + " farmhouse",
                        workplaceId);

                H51CitizenAnchor workAnchor =
                    CreateAnchor(
                        farmAnchorGroup,
                        "Farm Work",
                        workPosition,
                        farm.forward,
                        workId,
                        farmTown,
                        H51CitizenAnchorKind.Work,
                        "Farmer",
                        2,
                        farmTown + " working farm",
                        workplaceId);

                AddActivitySpot(
                    homeAnchor.transform,
                    "Farmhouse Evening Spot",
                    Vector3.zero,
                    "relaxing at the farmhouse");
                AddActivitySpot(
                    workAnchor.transform,
                    "Farm Chore Spot",
                    Vector3.zero,
                    "working around the farmyard");
                AddActivitySpot(
                    workAnchor.transform,
                    "Barn Chore Spot",
                    farm.InverseTransformDirection(farm.right) * 2f,
                    "checking the barn and equipment");

                anchorsCreated += 2;

                H51CitizenWorkplace workplace =
                    farm.GetComponent<H51CitizenWorkplace>();
                if (!workplace)
                    workplace = Undo.AddComponent<H51CitizenWorkplace>(farm.gameObject);
                workplace.Configure(
                    workplaceId,
                    farmTown + " Working Farm",
                    farmTown,
                    "Farm",
                    "Farmer",
                    2);
                EditorUtility.SetDirty(workplace);

                Transform navRoot =
                    New("NavMesh - " + farmTown,navigationRoot);
                navRoot.position = farm.position;
                navRoot.rotation = farm.rotation;

                NavMeshSurface surface =
                    Undo.AddComponent<NavMeshSurface>(navRoot.gameObject);
                surface.collectObjects = CollectObjects.Volume;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = ~0;
                surface.center = new Vector3(0f,18f,0f);
                surface.size = new Vector3(105f,55f,95f);
                surface.BuildNavMesh();
                if (surface.navMeshData) navMeshesBuilt++;

                H51CitizenIdentity farmer =
                    citizens.FirstOrDefault(c => c && !used.Contains(c) && !IsEssentialCitizen(c));
                if (!farmer) continue;

                used.Add(farmer);

                farmer.Configure(
                    farmer.CitizenId,
                    farmer.DisplayName,
                    farmer.Age,
                    farmTown,
                    "Farmer",
                    homeAnchor.AnchorId,
                    workAnchor.AnchorId,
                    farmer.VisualArchetype,
                    farmer.AppearanceSeed,
                    farmer.Sociability,
                    farmer.Responsibility,
                    farmer.Temper,
                    farmer.Curiosity);

                Vector3 spawn = homePosition;
                if (NavMesh.SamplePosition(homePosition,out NavMeshHit hit,8f,NavMesh.AllAreas))
                    spawn = hit.position;

                Undo.RecordObject(farmer.transform,"Move farmer to farmhouse");
                farmer.transform.position = spawn;
                farmer.transform.rotation =
                    Quaternion.LookRotation(farm.forward,Vector3.up);

                EditorUtility.SetDirty(farmer);
                EditorUtility.SetDirty(farmer.transform);
                farmersAssigned++;
            }

            registry.RefreshSceneReferences();
        }

        static int FarmerPriority(H51CitizenIdentity citizen)
        {
            if (!citizen) return 100;
            string job = citizen.Occupation ?? string.Empty;
            string visual = citizen.VisualArchetype ?? string.Empty;

            if (job.IndexOf("Farmer",StringComparison.OrdinalIgnoreCase) >= 0)
                return 0;
            if (visual.IndexOf("Farmer",StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;
            if (job == "Resident")
                return 2;
            if (job.IndexOf("Town Worker",StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Community",StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            return 10;
        }

        static bool IsEssentialCitizen(H51CitizenIdentity citizen)
        {
            if (!citizen) return true;
            string job = (citizen.Occupation ?? string.Empty).ToLowerInvariant();
            return
                job.Contains("police") ||
                job.Contains("swat") ||
                job.Contains("firefighter") ||
                job.Contains("paramedic") ||
                job.Contains("doctor") ||
                job.Contains("nurse") ||
                job.Contains("power plant") ||
                job.Contains("utility") ||
                job.Contains("dispatcher") ||
                job.Contains("ambulance");
        }

        static H51CitizenAnchor CreateAnchor(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            Vector3 forward,
            string id,
            string town,
            H51CitizenAnchorKind kind,
            string occupation,
            int capacity,
            string label,
            string workplaceId)
        {
            Transform root = New(objectName,parent);
            root.position = worldPosition;
            Vector3 flatForward =
                Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            if (flatForward.sqrMagnitude > .01f)
                root.rotation = Quaternion.LookRotation(flatForward,Vector3.up);

            H51CitizenAnchor anchor =
                Undo.AddComponent<H51CitizenAnchor>(root.gameObject);
            anchor.Configure(
                id,
                town,
                kind,
                occupation,
                capacity,
                label,
                workplaceId);
            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        static void AddActivitySpot(
            Transform parent,
            string objectName,
            Vector3 localOffset,
            string label)
        {
            Transform spotRoot = New(objectName,parent);
            spotRoot.localPosition = localOffset;
            spotRoot.localRotation = Quaternion.identity;

            H51CitizenActivitySpot spot =
                Undo.AddComponent<H51CitizenActivitySpot>(spotRoot.gameObject);
            spot.Configure(label,"Interact",4f,9f);
            EditorUtility.SetDirty(spot);
        }

        static bool MoveRunwayControlInsideAirport()
        {
            H51RunwayLightSwitch runwaySwitch =
                UnityEngine.Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);
            GameObject airport = GameObject.Find(AirportRootName);
            if (!runwaySwitch || !airport) return false;

            GameObject hangar = GameObject.Find(HangarRootName);
            Vector3 interiorReference = hangar
                ? hangar.transform.position
                : airport.transform.position;

            Renderer[] renderers =
                airport.GetComponentsInChildren<Renderer>(true);

            Renderer nearestWall = null;
            float bestSqr = float.PositiveInfinity;
            for (int i=0;i<renderers.Length;i++)
            {
                Renderer r = renderers[i];
                if (!r) continue;
                string n = r.name;
                bool wall =
                    n.IndexOf("Perimeter Wall",StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Airport Wall",StringComparison.OrdinalIgnoreCase) >= 0;
                if (!wall) continue;

                Vector3 closest = r.bounds.ClosestPoint(runwaySwitch.transform.position);
                float sqr =
                    (closest - runwaySwitch.transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                nearestWall = r;
            }

            if (!nearestWall) return false;

            Bounds bounds = nearestWall.bounds;
            Vector3 inside = interiorReference - bounds.center;
            inside.y = 0f;
            if (inside.sqrMagnitude < .01f) return false;
            inside.Normalize();

            Vector3 closestPoint =
                bounds.ClosestPoint(runwaySwitch.transform.position);
            float y = Mathf.Clamp(
                bounds.min.y + 1.35f,
                bounds.min.y + .75f,
                bounds.max.y - .45f);

            Undo.RecordObject(
                runwaySwitch.transform,
                "Move runway light control inside airport wall");

            runwaySwitch.transform.position =
                new Vector3(closestPoint.x,y,closestPoint.z) +
                inside * .50f;
            runwaySwitch.transform.rotation =
                Quaternion.LookRotation(-inside,Vector3.up);

            EditorUtility.SetDirty(runwaySwitch.transform);
            return IsControlInsideAirportWall(runwaySwitch.transform);
        }

        static bool IsControlInsideAirportWall(Transform control)
        {
            GameObject airport = GameObject.Find(AirportRootName);
            if (!control || !airport) return false;

            GameObject hangar = GameObject.Find(HangarRootName);
            Vector3 interiorReference =
                hangar ? hangar.transform.position : airport.transform.position;

            Renderer[] renderers =
                airport.GetComponentsInChildren<Renderer>(true);
            Renderer nearestWall = null;
            float bestSqr = float.PositiveInfinity;

            for (int i=0;i<renderers.Length;i++)
            {
                Renderer r = renderers[i];
                if (!r) continue;
                string n = r.name;
                bool wall =
                    n.IndexOf("Perimeter Wall",StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Airport Wall",StringComparison.OrdinalIgnoreCase) >= 0;
                if (!wall) continue;

                float sqr =
                    (r.bounds.ClosestPoint(control.position) - control.position)
                    .sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                nearestWall = r;
            }

            if (!nearestWall) return false;

            Vector3 inside =
                Vector3.ProjectOnPlane(
                    interiorReference - nearestWall.bounds.center,
                    Vector3.up);
            Vector3 controlSide =
                Vector3.ProjectOnPlane(
                    control.position - nearestWall.bounds.center,
                    Vector3.up);

            return
                inside.sqrMagnitude > .01f &&
                controlSide.sqrMagnitude > .01f &&
                Vector3.Dot(inside.normalized,controlSide.normalized) > .15f;
        }

        static Material CreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");

            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material,path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor",color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color",color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic",metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness",smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness",smoothness);

            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform New(string name,Transform parent)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go,"Create " + name);
            go.transform.SetParent(parent,false);
            return go.transform;
        }

        static Transform FindChild(Transform root,string name)
        {
            if (!root) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].name == name) return all[i];
            return null;
        }

        static Transform DirectChild(Transform root,string name)
        {
            if (!root) return null;
            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child && child.name == name) return child;
            }
            return null;
        }

        static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) ||
                folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath)?.Replace("\\","/");
            string folder = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) ||
                string.IsNullOrWhiteSpace(folder))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,folder);
        }
    }
}
