using System;
using System.Collections.Generic;
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
    public static class Hanger51EmergencyAndPowerPlantPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string RegionalPassName = "Hanger 51 Regional Infrastructure Pass";
        const string PlantName = "Power Station Complex";
        const string ExpansionName = "H51 Emergency Utilities And Power Plant";
        const string PlantUpgradeName = "H51 Functional Power Plant";
        const string WorkAnchorRootName = "H51 Emergency And Utility Work Anchors";
        const string Generated = "Assets/_Project/Environment/Generated/FunctionalPowerPlant";
        const string CatalogPath = "Assets/_Project/Characters/H51Citizens/Generated/H51CitizenVisualCatalog.asset";

        struct Mats
        {
            public Material concrete;
            public Material floor;
            public Material wall;
            public Material darkWall;
            public Material metal;
            public Material stainless;
            public Material black;
            public Material white;
            public Material yellow;
            public Material red;
            public Material blue;
            public Material green;
            public Material glass;
            public Material asphalt;
            public Material line;
            public Material particle;
        }

        static readonly string[] NeutralFirstNames =
        {
            "Alex","Avery","Casey","Cameron","Jordan","Morgan","Riley","Sam","Taylor","Jamie",
            "Quinn","Parker","Reese","Skyler","Drew","Rowan","Dakota","Emerson","Hayden","Robin"
        };

        static readonly string[] LastNames =
        {
            "Bennett","Carter","Davis","Ellis","Foster","Grant","Hayes","Miller","Parker","Reed",
            "Sullivan","Turner","Walker","Young","Morgan","Brooks","Coleman","Fisher","Howard","Price"
        };

        [MenuItem("Hanger 51/World/Current/150 - Add Emergency Services And Functional Power Plant")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 150 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51CitizenFacadeAndRoofNavCleanup.Build();

            GameObject world = GameObject.Find(WorldName);
            Terrain terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            Transform citizenRoot = world ? FindChild(world.transform,CitizenRootName) : null;
            Transform regional = world ? FindChild(world.transform,RegionalPassName) : null;
            Transform plant = regional ? FindChild(regional,PlantName) : null;
            H51CitizenRegistry registry = citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;
            H51TrafficNetwork traffic = world
                ? world.GetComponentInChildren<H51TrafficNetwork>(true)
                : null;
            H51CitizenVisualCatalog catalog =
                AssetDatabase.LoadAssetAtPath<H51CitizenVisualCatalog>(CatalogPath);

            if (!world || !terrain || !citizenRoot || !plant || !registry || !traffic)
            {
                Debug.LogError(
                    "Step 150 failed. Missing prerequisite(s): " +
                    "world=" + (world ? "ready" : "MISSING") +
                    ", terrain=" + (terrain ? "ready" : "MISSING") +
                    ", citizen root=" + (citizenRoot ? "ready" : "MISSING") +
                    ", power station=" + (plant ? "ready" : "MISSING") +
                    ", citizen registry=" + (registry ? "ready" : "MISSING") +
                    ", traffic network=" + (traffic ? "ready" : "MISSING") +
                    ". The traffic network is created under 'Hanger 51 Enterable Town Interiors And Traffic', not under the citizen root.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Emergency And Utilities",
                    "Preparing emergency and utility expansion",
                    .03f);

                Transform oldExpansion = FindChild(world.transform,ExpansionName);
                if (oldExpansion) UnityEngine.Object.DestroyImmediate(oldExpansion.gameObject);

                Transform oldAnchors = FindChild(citizenRoot,WorkAnchorRootName);
                if (oldAnchors) UnityEngine.Object.DestroyImmediate(oldAnchors.gameObject);

                Transform oldUpgrade = DirectChild(plant,PlantUpgradeName);
                if (oldUpgrade) UnityEngine.Object.DestroyImmediate(oldUpgrade.gameObject);

                RemoveDirectChild(plant,"Turbine Hall");
                RemoveDirectChild(plant,"Generation Hall");
                RemoveDirectChild(plant,"Employee Parking");
                RemoveNamedDescendants(plant,"Power Station Parked Car");

                Mats mats = BuildMaterials();
                Transform expansion = New(ExpansionName,world.transform);
                Transform anchorRoot = New(WorkAnchorRootName,citizenRoot);

                H51CitizenIdentity[] existingCitizens =
                    citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
                string mainTown = existingCitizens
                    .Where(c => c && !string.IsNullOrWhiteSpace(c.TownName))
                    .GroupBy(c => c.TownName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? "Regional";

                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Emergency And Utilities",
                    "Rebuilding the power station as a walkable staffed facility",
                    .12f);

                BuildPlantUpgrade(
                    plant,
                    terrain,
                    traffic,
                    mainTown,
                    anchorRoot,
                    mats,
                    out Transform operations,
                    out Transform plantEntrance,
                    out Transform plantInterior,
                    out TextMesh plantStatus,
                    out List<ParticleSystem> vapor,
                    out List<H51CitizenAnchor> plantWorkAnchors,
                    out int plantDoors,
                    out int plantClearances,
                    out int plantParking);

                H51CitizenWorkplace plantWorkplace = plant.GetComponent<H51CitizenWorkplace>();
                if (!plantWorkplace) plantWorkplace = plant.gameObject.AddComponent<H51CitizenWorkplace>();
                plantWorkplace.Configure(
                    "regional-power-station",
                    "Regional Generating Station",
                    mainTown,
                    "Utility",
                    "Power Plant Operator",
                    14);

                H51EnterableBuilding plantBuilding = plant.GetComponent<H51EnterableBuilding>();
                if (!plantBuilding) plantBuilding = plant.gameObject.AddComponent<H51EnterableBuilding>();
                plantBuilding.Configure(
                    "regional-power-station",
                    "Regional Generating Station",
                    mainTown,
                    H51EnterableBuildingKind.Workplace,
                    plantEntrance,
                    plantInterior);

                H51PowerPlantController plantController =
                    plant.GetComponent<H51PowerPlantController>();
                if (!plantController)
                    plantController = plant.gameObject.AddComponent<H51PowerPlantController>();
                plantController.Configure(plantStatus,vapor.ToArray(),900f);

                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Emergency And Utilities",
                    "Adding police cars ambulances fire rescue and dispatch",
                    .48f);

                BuildEmergencyServices(
                    expansion,
                    anchorRoot,
                    world.transform,
                    terrain,
                    traffic,
                    mainTown,
                    mats,
                    out H51EnterableBuilding hospital,
                    out H51EnterableBuilding police,
                    out H51EnterableBuilding fireAnnex,
                    out H51CitizenAnchor paramedicAnchor,
                    out H51CitizenAnchor firefighterAnchor,
                    out int emergencyVehicles);

                Transform medicalDestination =
                    hospital && hospital.Entrance
                        ? hospital.Entrance
                        : hospital ? hospital.transform : null;

                H51EmergencyDispatchController dispatch =
                    citizenRoot.GetComponent<H51EmergencyDispatchController>();
                if (!dispatch)
                    dispatch = citizenRoot.gameObject.AddComponent<H51EmergencyDispatchController>();
                dispatch.Configure(traffic,medicalDestination,true);

                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Emergency And Utilities",
                    "Staffing plant medical and fire response roles",
                    .64f);

                List<H51CitizenIdentity> citizens =
                    citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true).Where(c => c).ToList();
                HashSet<string> usedNames =
                    new HashSet<string>(citizens.Select(c => c.DisplayName));

                int plantWorkers = AssignPlantWorkers(
                    citizens,
                    plantWorkAnchors,
                    mainTown);

                int paramedics = EnsureSpecialistStaff(
                    citizenRoot,
                    registry,
                    catalog,
                    mainTown,
                    paramedicAnchor,
                    "Paramedic",
                    2,
                    usedNames,
                    15020);

                int firefighters = EnsureSpecialistStaff(
                    citizenRoot,
                    registry,
                    catalog,
                    mainTown,
                    firefighterAnchor,
                    "Firefighter",
                    3,
                    usedNames,
                    15040);

                citizens =
                    citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true).Where(c => c).ToList();

                for (int i=0;i<citizens.Count;i++)
                {
                    H51CitizenEmergencyState emergency =
                        citizens[i].GetComponent<H51CitizenEmergencyState>();
                    if (!emergency)
                        emergency = citizens[i].gameObject.AddComponent<H51CitizenEmergencyState>();
                    emergency.Configure(dispatch);
                    EditorUtility.SetDirty(citizens[i].gameObject);
                }

                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Emergency And Utilities",
                    "Connecting buildings to the regional power and water network",
                    .76f);

                int utilityConsumers = ConfigureUtilityConsumers(
                    world.transform,
                    plant,
                    fireAnnex);

                H51UtilityGridController grid =
                    expansion.gameObject.AddComponent<H51UtilityGridController>();
                grid.Configure(plantController);

                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Emergency And Utilities",
                    "Baking walkable plant and town navigation",
                    .86f);

                int navMeshes = BuildPlantAndRefreshNavigation(
                    citizenRoot,
                    plant,
                    operations);

                registry.RefreshSceneReferences();

                EditorUtility.SetDirty(registry);
                EditorUtility.SetDirty(dispatch);
                EditorUtility.SetDirty(grid);
                EditorUtility.SetDirty(plantController);
                EditorUtility.SetDirty(plantWorkplace);
                EditorUtility.SetDirty(plantBuilding);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();

                Selection.activeGameObject = plant.gameObject;

                Debug.Log(
                    "Step 150 complete. functional power plant interior=1, plant doors=" + plantDoors +
                    ", verified walk clearances=" + plantClearances +
                    ", plant worker parking=" + plantParking +
                    ", assigned power-plant workers=" + plantWorkers +
                    ", paramedics=" + paramedics +
                    ", firefighters=" + firefighters +
                    ", emergency vehicles=" + emergencyVehicles +
                    ", utility consumers=" + utilityConsumers +
                    ", plant vapor systems=" + vapor.Count +
                    ", NavMeshes rebaked=" + navMeshes +
                    ". Civilians now carry an emergency phone state and can request police, fire, or ambulance response. Run Step 151.",
                    plant.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/151 - Validate Emergency Services And Functional Power Plant")]
        public static void Validate()
        {
            GameObject world = GameObject.Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform,CitizenRootName) : null;
            Transform regional = world ? FindChild(world.transform,RegionalPassName) : null;
            Transform plant = regional ? FindChild(regional,PlantName) : null;
            Transform upgrade = plant ? DirectChild(plant,PlantUpgradeName) : null;

            if (!world || !citizenRoot || !plant || !upgrade)
            {
                Debug.LogError("Step 151 failed. Run Step 150 first.");
                return;
            }

            H51PowerPlantController controller = plant.GetComponent<H51PowerPlantController>();
            H51UtilityGridController grid =
                world.GetComponentInChildren<H51UtilityGridController>(true);
            H51EmergencyDispatchController dispatch =
                citizenRoot.GetComponent<H51EmergencyDispatchController>();

            H51EmergencyResponseVehicle[] emergencyVehicles =
                world.GetComponentsInChildren<H51EmergencyResponseVehicle>(true);
            int ambulances = emergencyVehicles.Count(
                v => v && v.ServiceType == H51EmergencyServiceType.Ambulance);
            int policeCars = emergencyVehicles.Count(
                v => v && v.ServiceType == H51EmergencyServiceType.Police);
            int fireTrucks = emergencyVehicles.Count(
                v => v && v.ServiceType == H51EmergencyServiceType.Fire);

            H51CitizenIdentity[] citizens =
                citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            int emergencyReady = citizens.Count(
                c => c && c.GetComponent<H51CitizenEmergencyState>());

            int plantWorkers = citizens.Count(
                c => c &&
                (c.Occupation ?? string.Empty).IndexOf(
                    "Power Plant",
                    StringComparison.OrdinalIgnoreCase) >= 0);
            int paramedics = citizens.Count(
                c => c && string.Equals(c.Occupation,"Paramedic",StringComparison.OrdinalIgnoreCase));
            int firefighters = citizens.Count(
                c => c && string.Equals(c.Occupation,"Firefighter",StringComparison.OrdinalIgnoreCase));

            int placeholderCars = plant.GetComponentsInChildren<Transform>(true)
                .Count(t => t && t.name.StartsWith("Power Station Parked Car",StringComparison.Ordinal));

            int plantDoors = upgrade.GetComponentsInChildren<H51TownDoor>(true).Length;
            Transform workAnchorRoot = FindChild(citizenRoot,WorkAnchorRootName);
            int activities = workAnchorRoot
                ? workAnchorRoot.GetComponentsInChildren<H51CitizenActivitySpot>(true).Length
                : 0;
            int vapor = plant.GetComponentsInChildren<ParticleSystem>(true)
                .Count(p => p && p.name.Contains("Vapor"));

            int blockedClearance = 0;
            Physics.SyncTransforms();
            Transform[] clearanceMarkers = upgrade.GetComponentsInChildren<Transform>(true)
                .Where(t => t && t.name.StartsWith("Plant Walk Clearance",StringComparison.Ordinal))
                .ToArray();

            for (int i=0;i<clearanceMarkers.Length;i++)
            {
                Collider[] hits = Physics.OverlapBox(
                    clearanceMarkers[i].position + Vector3.up * .95f,
                    new Vector3(.48f,.72f,.48f),
                    clearanceMarkers[i].rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                bool blocked = false;
                for (int h=0;h<hits.Length;h++)
                {
                    Collider col = hits[h];
                    if (!col || col is TerrainCollider) continue;
                    if (col.transform.IsChildOf(clearanceMarkers[i])) continue;
                    if (col.name.IndexOf("Floor",StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    blocked = true;
                    break;
                }
                if (blocked) blockedClearance++;
            }

            H51UtilityConsumer[] consumers =
                world.GetComponentsInChildren<H51UtilityConsumer>(true);
            H51EnterableBuilding[] buildings =
                world.GetComponentsInChildren<H51EnterableBuilding>(true);
            int criticalConsumers = consumers.Count(c => c && c.CriticalFacility);

            NavMeshSurface[] surfaces =
                citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);
            int plantNav = surfaces.Count(
                s => s && s.name == "NavMesh - Regional Power Station" && s.navMeshData);

            Transform accessRoad =
                FindChild(world.transform,"Regional Industrial Access - Power Station");

            bool ok =
                controller &&
                grid &&
                dispatch &&
                ambulances >= 2 &&
                policeCars >= 2 &&
                fireTrucks >= 1 &&
                emergencyReady == citizens.Length &&
                plantWorkers >= 5 &&
                paramedics >= 2 &&
                firefighters >= 3 &&
                placeholderCars == 0 &&
                plantDoors >= 5 &&
                activities >= 10 &&
                vapor >= 3 &&
                blockedClearance == 0 &&
                clearanceMarkers.Length >= 8 &&
                consumers.Length >= buildings.Length &&
                criticalConsumers >= 3 &&
                plantNav >= 1 &&
                accessRoad;

            if (ok)
            {
                Debug.Log(
                    "Step 151 passed. ambulances=" + ambulances +
                    ", police cars=" + policeCars +
                    ", fire trucks=" + fireTrucks +
                    ", emergency-ready citizens=" + emergencyReady + "/" + citizens.Length +
                    ", plant workers=" + plantWorkers +
                    ", paramedics=" + paramedics +
                    ", firefighters=" + firefighters +
                    ", placeholder plant cars=0, plant doors=" + plantDoors +
                    ", plant activities=" + activities +
                    ", plant vapor systems=" + vapor +
                    ", blocked walk clearances=0, utility consumers=" + consumers.Length +
                    ", critical backup consumers=" + criticalConsumers +
                    ", plant NavMesh=ready, industrial access road=ready.",
                    plant.gameObject);
            }
            else
            {
                Debug.LogError(
                    "Step 151 failed. controller=" + (controller ? "ready" : "MISSING") +
                    ", grid=" + (grid ? "ready" : "MISSING") +
                    ", dispatch=" + (dispatch ? "ready" : "MISSING") +
                    ", ambulances=" + ambulances + "/2, police cars=" + policeCars + "/2, fire trucks=" + fireTrucks + "/1" +
                    ", emergency-ready citizens=" + emergencyReady + "/" + citizens.Length +
                    ", plant workers=" + plantWorkers + "/5, paramedics=" + paramedics + "/2, firefighters=" + firefighters + "/3" +
                    ", placeholder plant cars=" + placeholderCars +
                    ", plant doors=" + plantDoors + "/5, plant activities=" + activities + "/10" +
                    ", vapor=" + vapor + "/3, walk clearance markers=" + clearanceMarkers.Length +
                    ", blocked clearances=" + blockedClearance +
                    ", utility consumers=" + consumers.Length + "/" + buildings.Length +
                    ", critical consumers=" + criticalConsumers +
                    ", plant NavMesh=" + plantNav +
                    ", industrial access road=" + (accessRoad ? "ready" : "MISSING") + ".",
                    plant.gameObject);
            }
        }

        static void BuildPlantUpgrade(
            Transform plant,
            Terrain terrain,
            H51TrafficNetwork traffic,
            string town,
            Transform anchorRoot,
            Mats m,
            out Transform operations,
            out Transform entrance,
            out Transform interior,
            out TextMesh status,
            out List<ParticleSystem> vapor,
            out List<H51CitizenAnchor> workAnchors,
            out int doors,
            out int clearances,
            out int parking)
        {
            Transform upgrade = New(PlantUpgradeName,plant);
            doors = 0;
            clearances = 0;
            parking = 0;

            operations = New("Power Plant Operations Building",upgrade);
            operations.localPosition = new Vector3(-18f,0f,4f);
            operations.localRotation = Quaternion.identity;

            const float width = 82f;
            const float depth = 48f;
            const float height = 10f;
            const float halfW = width * .5f;
            const float halfD = depth * .5f;

            Box(operations,"Operations Floor",new Vector3(0,.10f,0),new Vector3(width,.20f,depth),m.floor,true);
            Box(operations,"Back Exterior Wall",new Vector3(0,height*.5f,-halfD),new Vector3(width,height,.36f),m.wall,true);
            Box(operations,"Left Exterior Wall",new Vector3(-halfW,height*.5f,0),new Vector3(.36f,height,depth),m.wall,true);
            Box(operations,"Right Exterior Wall",new Vector3(halfW,height*.5f,0),new Vector3(.36f,height,depth),m.wall,true);

            // Front wall has a human-scaled double public entrance and a second double service exit.
            Box(operations,"Front Wall West",new Vector3(-34.3f,height*.5f,halfD),new Vector3(13.4f,height,.36f),m.wall,true);
            Box(operations,"Front Wall Center",new Vector3(2.05f,height*.5f,halfD),new Vector3(52.9f,height,.36f),m.wall,true);
            Box(operations,"Front Wall East",new Vector3(36.25f,height*.5f,halfD),new Vector3(9.5f,height,.36f),m.wall,true);
            Box(operations,"Main Entrance Header",new Vector3(-26f,6.3f,halfD),new Vector3(3.2f,7.4f,.36f),m.wall,true);
            Box(operations,"Service Entrance Header",new Vector3(30f,6.3f,halfD),new Vector3(3.0f,7.4f,.36f),m.wall,true);

            Transform roof = Box(
                operations,
                "Operations Roof",
                new Vector3(0,height+.24f,0),
                new Vector3(width+.8f,.48f,depth+.8f),
                m.darkWall,
                true);
            NavMeshModifier roofModifier = roof.gameObject.AddComponent<NavMeshModifier>();
            roofModifier.ignoreFromBuild = true;

            CreateDoubleDoor(
                operations,
                "Main Entrance",
                new Vector3(-26f,0f,halfD-.21f),
                Quaternion.identity,
                3.0f,
                2.55f,
                m.blue,
                ref doors);

            CreateDoubleDoor(
                operations,
                "Service Entrance",
                new Vector3(30f,0f,halfD-.21f),
                Quaternion.identity,
                2.8f,
                2.55f,
                m.metal,
                ref doors);

            entrance = New("Power Plant Entrance",operations);
            entrance.localPosition = new Vector3(-26f,0f,halfD+1.8f);
            interior = New("Power Plant Interior Center",operations);
            interior.localPosition = new Vector3(0f,0f,0f);

            // Control room: enclosed, human-scaled and separated from the machinery hall.
            PartitionXWithDoor(operations,"Control Room North Wall",-10f,-39f,-10f,-21f,1.4f,3.35f,m.darkWall,m.blue,ref doors);
            Box(operations,"Control Room West Wall",new Vector3(-39f,1.675f,1f),new Vector3(.22f,3.35f,22f),m.darkWall,true);
            PartitionXWithDoor(operations,"Control Room South Wall",12f,-39f,-10f,-30f,1.4f,3.35f,m.darkWall,m.blue,ref doors);
            Box(operations,"Control Room Ceiling",new Vector3(-24.5f,3.42f,1f),new Vector3(29f,.18f,22f),m.darkWall,true);

            Transform control = New("Modeled Control Room",operations);
            for (int i=0;i<4;i++)
            {
                float z = -5.5f + i*3.4f;
                Box(control,"Control Desk "+(i+1),new Vector3(-31.5f,.62f,z),new Vector3(5.8f,1.22f,1.25f),m.black,true);
                for (int monitor=0;monitor<3;monitor++)
                    Box(control,"Control Monitor "+i+"-"+monitor,new Vector3(-33.1f+monitor*1.6f,1.52f,z-.25f),new Vector3(1.15f,.72f,.14f),m.green,false);
                Box(control,"Operator Chair "+(i+1),new Vector3(-31.5f,.48f,z+1.25f),new Vector3(.8f,.95f,.8f),m.blue,true);
            }
            Box(control,"Alarm Panel",new Vector3(-38.55f,1.75f,4.4f),new Vector3(.12f,2.5f,6f),m.black,false);

            Transform displayRoot = New("Plant Status Display",control);
            displayRoot.localPosition = new Vector3(-38.42f,2.45f,2.5f);
            displayRoot.localRotation = Quaternion.Euler(0,90f,0);
            status = displayRoot.gameObject.AddComponent<TextMesh>();
            status.fontSize = 64;
            status.characterSize = .045f;
            status.anchor = TextAnchor.UpperLeft;
            status.alignment = TextAlignment.Left;
            status.color = new Color(.55f,1f,.62f);
            status.text = "REGIONAL GENERATING STATION\nSTATUS: INITIALIZING";

            // Maintenance room.
            PartitionXWithDoor(operations,"Maintenance North Wall",-12f,-39f,-10f,-25f,1.4f,3.35f,m.wall,m.metal,ref doors);
            Box(operations,"Maintenance West Wall",new Vector3(-39f,1.675f,-18f),new Vector3(.22f,3.35f,12f),m.wall,true);
            Box(operations,"Maintenance Ceiling",new Vector3(-24.5f,3.42f,-18f),new Vector3(29f,.18f,12f),m.darkWall,true);
            Box(operations,"Maintenance Bench A",new Vector3(-33f,.65f,-18f),new Vector3(7f,1.3f,1.2f),m.metal,true);
            Box(operations,"Maintenance Bench B",new Vector3(-20f,.65f,-21f),new Vector3(8f,1.3f,1.2f),m.metal,true);
            for (int i=0;i<6;i++)
                Box(operations,"Maintenance Locker "+(i+1),new Vector3(-37.8f,1.2f,-22f+i*1.55f),new Vector3(1f,2.4f,1.1f),m.blue,true);

            // Electrical switchgear room.
            PartitionXWithDoor(operations,"Electrical North Wall",-12f,-8f,9f,0f,1.35f,3.35f,m.wall,m.yellow,ref doors);
            Box(operations,"Electrical West Wall",new Vector3(-8f,1.675f,-18f),new Vector3(.22f,3.35f,12f),m.wall,true);
            Box(operations,"Electrical East Wall",new Vector3(9f,1.675f,-18f),new Vector3(.22f,3.35f,12f),m.wall,true);
            Box(operations,"Electrical Ceiling",new Vector3(.5f,3.42f,-18f),new Vector3(17f,.18f,12f),m.darkWall,true);
            for (int i=0;i<5;i++)
                Box(operations,"Switchgear Cabinet "+(i+1),new Vector3(-5.5f+i*2.8f,1.3f,-22.3f),new Vector3(2.1f,2.6f,.75f),m.metal,true);

            // Fuel-service room. This is deliberately a game abstraction, not a simulation of
            // real nuclear fuel handling procedures.
            PartitionXWithDoor(operations,"Fuel Service North Wall",-12f,12f,39f,22f,1.5f,3.35f,m.wall,m.yellow,ref doors);
            Box(operations,"Fuel Service West Wall",new Vector3(12f,1.675f,-18f),new Vector3(.22f,3.35f,12f),m.wall,true);
            Box(operations,"Fuel Service East Wall",new Vector3(39f,1.675f,-18f),new Vector3(.22f,3.35f,12f),m.wall,true);
            Box(operations,"Fuel Service Ceiling",new Vector3(25.5f,3.42f,-18f),new Vector3(27f,.18f,12f),m.darkWall,true);
            Box(operations,"Fuel Service Work Platform",new Vector3(25.5f,.32f,-18.5f),new Vector3(18f,.45f,7f),m.stainless,true);
            for (int rack=0;rack<3;rack++)
            {
                Transform rackRoot = New("Abstract Fuel Assembly Rack "+(rack+1),operations);
                rackRoot.localPosition = new Vector3(17f+rack*8.5f,0f,-21f);
                Box(rackRoot,"Rack Base",new Vector3(0,.25f,0),new Vector3(6f,.5f,2f),m.metal,true);
                for (int rod=0;rod<10;rod++)
                {
                    float x = -2.4f + (rod%5)*1.2f;
                    float z = -.55f + (rod/5)*1.1f;
                    Cylinder(
                        rackRoot,
                        "Abstract Fuel Module "+(rod+1),
                        new Vector3(x,1.25f,z),
                        Quaternion.identity,
                        new Vector3(.22f,1.05f,.22f),
                        rod%2==0 ? m.yellow : m.stainless,
                        false);
                }
            }

            // Turbine-generator hall.
            Transform machinery = New("Turbine Generator Hall Equipment",operations);
            Box(machinery,"Turbine Foundation",new Vector3(13f,.35f,2f),new Vector3(47f,.7f,12f),m.concrete,true);
            Cylinder(machinery,"High Pressure Turbine",new Vector3(0f,2.4f,2f),Quaternion.Euler(0,0,90f),new Vector3(3.2f,7f,3.2f),m.stainless,true);
            Cylinder(machinery,"Low Pressure Turbine",new Vector3(14f,2.4f,2f),Quaternion.Euler(0,0,90f),new Vector3(4f,7f,4f),m.stainless,true);
            Cylinder(machinery,"Main Generator",new Vector3(29f,2.4f,2f),Quaternion.Euler(0,0,90f),new Vector3(4.4f,7.2f,4.4f),m.green,true);
            Cylinder(machinery,"Main Shaft",new Vector3(14f,2.4f,2f),Quaternion.Euler(0,0,90f),new Vector3(.55f,22f,.55f),m.metal,false);
            for (int i=0;i<4;i++)
            {
                Cylinder(
                    machinery,
                    "Steam Pipe "+(i+1),
                    new Vector3(-2f+i*10f,6.2f,8f),
                    Quaternion.Euler(90f,0,0),
                    new Vector3(.55f,4.5f,.55f),
                    m.stainless,
                    true);
            }

            Transform pump = New("Pump And Water Systems",operations);
            for (int i=0;i<4;i++)
            {
                Box(pump,"Pump Skid "+(i+1),new Vector3(14f+i*6.5f,.35f,14.5f),new Vector3(5.2f,.7f,4.5f),m.metal,true);
                Cylinder(pump,"Pump Motor "+(i+1),new Vector3(14f+i*6.5f,1.25f,14.5f),Quaternion.Euler(0,0,90f),new Vector3(1.4f,1.6f,1.4f),m.green,true);
            }
            WorldText(operations,"TURBINE - GENERATOR HALL",new Vector3(15f,7.5f,-23.75f),Quaternion.Euler(0,0,0),.26f,m.white);

            // Front lobby, security, lockers and break area.
            Box(operations,"Security Desk",new Vector3(-31f,.65f,18f),new Vector3(5.2f,1.3f,1.2f),m.black,true);
            Box(operations,"Visitor Checkpoint",new Vector3(-24f,.6f,18f),new Vector3(3f,1.2f,.8f),m.metal,true);
            Box(operations,"Break Room Table",new Vector3(-13f,.55f,18f),new Vector3(4.5f,1.1f,2.2f),m.floor,true);
            for (int i=0;i<5;i++)
                Box(operations,"Staff Locker "+(i+1),new Vector3(-5f+i*1.35f,1.2f,22.6f),new Vector3(1f,2.4f,.9f),m.blue,true);

            // Industrial lighting.
            InteriorLight(operations,new Vector3(-25f,3.0f,1f),13f,1.45f);
            InteriorLight(operations,new Vector3(-24f,3.0f,-18f),12f,1.35f);
            InteriorLight(operations,new Vector3(.5f,3.0f,-18f),10f,1.20f);
            InteriorLight(operations,new Vector3(25f,3.0f,-18f),13f,1.30f);
            InteriorLight(operations,new Vector3(8f,8.5f,2f),22f,1.60f);
            InteriorLight(operations,new Vector3(29f,8.5f,2f),22f,1.60f);
            InteriorLight(operations,new Vector3(22f,6.5f,15f),18f,1.40f);
            InteriorLight(operations,new Vector3(-22f,4.5f,18f),13f,1.30f);

            string[] roomLabels =
            {
                "CONTROL ROOM","MAINTENANCE","ELECTRICAL","FUEL SERVICE","SECURITY"
            };
            Vector3[] labelPositions =
            {
                new Vector3(-24f,2.75f,-9.82f),
                new Vector3(-24f,2.75f,-11.82f),
                new Vector3(.5f,2.75f,-11.82f),
                new Vector3(25.5f,2.75f,-11.82f),
                new Vector3(-31f,2.75f,23.82f)
            };
            for (int i=0;i<roomLabels.Length;i++)
                WorldText(operations,roomLabels[i],labelPositions[i],Quaternion.identity,.15f,m.white);

            Vector3[] clearanceLocal =
            {
                new Vector3(-26f,0f,21f),
                new Vector3(-26f,0f,15f),
                new Vector3(-21f,0f,-8f),
                new Vector3(-24f,0f,-14f),
                new Vector3(0f,0f,-14f),
                new Vector3(22f,0f,-14f),
                new Vector3(-3f,0f,10f),
                new Vector3(19f,0f,10f),
                new Vector3(30f,0f,20.5f)
            };
            for (int i=0;i<clearanceLocal.Length;i++)
            {
                Transform marker = New("Plant Walk Clearance "+(i+1).ToString("00"),operations);
                marker.localPosition = clearanceLocal[i];
                clearances++;
            }

            // Internal road and real reservable worker parking; intentionally no fake parked cars.
            Transform roads = New("Plant Internal Roads And Parking",upgrade);
            Box(roads,"Gate Access Lane",new Vector3(0,.04f,67f),new Vector3(15f,.08f,34f),m.asphalt,false);
            Box(roads,"Operations Access Lane",new Vector3(-24f,.04f,47f),new Vector3(54f,.08f,10f),m.asphalt,false);
            parking = BuildPlantParking(roads,traffic,town,m);

            // Add cooling-tower steam/vapor and subtle ventilation-stack vapor.
            vapor = BuildPlantVapor(plant,m.particle);

            // Work anchors live under the persistent citizen system but point inside this facility.
            workAnchors = new List<H51CitizenAnchor>();

            H51CitizenAnchor operators = WorkAnchor(
                anchorRoot,
                "plant-operator",
                town,
                "Regional Power Station Control Room",
                "Power Plant Operator",
                3,
                operations.TransformPoint(new Vector3(-26f,0f,0f)),
                operations.forward);
            Activity(operators.transform,new Vector3(-4f,0f,-4f),"monitoring the plant control boards",8f,18f);
            Activity(operators.transform,new Vector3(4f,0f,3f),"reviewing generator output and grid demand",7f,16f);
            workAnchors.Add(operators);

            H51CitizenAnchor turbine = WorkAnchor(
                anchorRoot,
                "plant-turbine",
                town,
                "Turbine Generator Hall",
                "Power Plant Turbine Technician",
                2,
                operations.TransformPoint(new Vector3(8f,0f,9f)),
                operations.forward);
            Activity(turbine.transform,new Vector3(8f,0f,-7f),"inspecting turbine-generator equipment",8f,18f);
            Activity(turbine.transform,new Vector3(19f,0f,-7f),"servicing turbine-generator equipment",8f,18f);
            workAnchors.Add(turbine);

            H51CitizenAnchor maintenance = WorkAnchor(
                anchorRoot,
                "plant-maintenance",
                town,
                "Power Station Maintenance Shop",
                "Power Plant Maintenance Technician",
                2,
                operations.TransformPoint(new Vector3(-25f,0f,-18f)),
                operations.forward);
            Activity(maintenance.transform,new Vector3(-7f,0f,0f),"working at the maintenance bench",7f,16f);
            Activity(maintenance.transform,new Vector3(6f,0f,-2f),"completing a plant equipment inspection",7f,16f);
            workAnchors.Add(maintenance);

            H51CitizenAnchor electrical = WorkAnchor(
                anchorRoot,
                "plant-electrical",
                town,
                "Power Station Electrical Room",
                "Power Plant Electrical Technician",
                1,
                operations.TransformPoint(new Vector3(.5f,0f,-18f)),
                operations.forward);
            Activity(electrical.transform,new Vector3(0f,0f,-3f),"checking electrical switchgear",7f,16f);
            Activity(electrical.transform,new Vector3(0f,0f,3f),"inspecting plant electrical distribution",7f,16f);
            workAnchors.Add(electrical);

            H51CitizenAnchor fuel = WorkAnchor(
                anchorRoot,
                "plant-fuel",
                town,
                "Power Station Fuel Service Bay",
                "Power Plant Fuel Technician",
                1,
                operations.TransformPoint(new Vector3(25.5f,0f,-18f)),
                operations.forward);
            Activity(fuel.transform,new Vector3(-5f,0f,0f),"performing scheduled fuel service",10f,22f);
            Activity(fuel.transform,new Vector3(5f,0f,0f),"checking the abstract fuel inventory",8f,18f);
            workAnchors.Add(fuel);

            WorldText(upgrade,"REGIONAL GENERATING STATION",new Vector3(-18f,8.3f,28.4f),Quaternion.Euler(0,180f,0),.34f,m.white);
        }

        static void BuildEmergencyServices(
            Transform expansion,
            Transform anchorRoot,
            Transform world,
            Terrain terrain,
            H51TrafficNetwork traffic,
            string town,
            Mats m,
            out H51EnterableBuilding hospital,
            out H51EnterableBuilding police,
            out H51EnterableBuilding fireAnnex,
            out H51CitizenAnchor paramedicAnchor,
            out H51CitizenAnchor firefighterAnchor,
            out int emergencyVehicles)
        {
            H51EnterableBuilding[] buildings =
                world.GetComponentsInChildren<H51EnterableBuilding>(true);

            hospital = buildings.FirstOrDefault(
                b => b && b.DisplayName.IndexOf("Hospital",StringComparison.OrdinalIgnoreCase) >= 0)
                ?? buildings.FirstOrDefault(
                    b => b && b.DisplayName.IndexOf("Medical",StringComparison.OrdinalIgnoreCase) >= 0);

            police = buildings.FirstOrDefault(
                b => b && b.DisplayName.IndexOf("Police",StringComparison.OrdinalIgnoreCase) >= 0);

            Transform root = New("Regional Emergency Services",expansion);
            emergencyVehicles = 0;

            Vector3 policeBase = police
                ? RoadHome(traffic,police.transform.position)
                : RoadHome(traffic,root.position);
            Quaternion policeRotation = police ? police.transform.rotation : Quaternion.identity;

            for (int i=0;i<2;i++)
            {
                Vector3 p = policeBase + policeRotation * new Vector3((i-.5f)*2.6f,0f,0f);
                BuildEmergencyVehicle(
                    root,
                    "Police Cruiser "+(i+1),
                    H51EmergencyServiceType.Police,
                    traffic,
                    p,
                    policeRotation,
                    m,
                    15.5f);
                emergencyVehicles++;
            }

            Vector3 hospitalBase = hospital
                ? RoadHome(traffic,hospital.transform.position)
                : policeBase + Vector3.forward*8f;
            Quaternion hospitalRotation = hospital ? hospital.transform.rotation : policeRotation;

            for (int i=0;i<2;i++)
            {
                Vector3 p = hospitalBase + hospitalRotation * new Vector3((i-.5f)*2.8f,0f,0f);
                BuildEmergencyVehicle(
                    root,
                    "Ambulance "+(i+1),
                    H51EmergencyServiceType.Ambulance,
                    traffic,
                    p,
                    hospitalRotation,
                    m,
                    14.5f);
                emergencyVehicles++;
            }

            Vector3 annexSite = police
                ? police.transform.position - police.transform.right * 30f + police.transform.forward * 3f
                : hospitalBase + new Vector3(24f,0f,0f);
            annexSite.y = Ground(terrain,annexSite);

            Transform annex = New(town+" Fire And Rescue Annex",root);
            annex.position = annexSite;
            annex.rotation = police ? police.transform.rotation : Quaternion.identity;

            Box(annex,"Fire Annex Floor",new Vector3(0,.08f,0),new Vector3(18f,.16f,14f),m.floor,true);
            Box(annex,"Fire Annex Back Wall",new Vector3(0,2.6f,-7f),new Vector3(18f,5.2f,.28f),m.wall,true);
            Box(annex,"Fire Annex Left Wall",new Vector3(-9f,2.6f,0),new Vector3(.28f,5.2f,14f),m.wall,true);
            Box(annex,"Fire Annex Right Wall",new Vector3(9f,2.6f,0),new Vector3(.28f,5.2f,14f),m.wall,true);
            Box(annex,"Fire Annex Front Left",new Vector3(-6.7f,2.6f,7f),new Vector3(4.6f,5.2f,.28f),m.wall,true);
            Box(annex,"Fire Annex Front Center",new Vector3(0f,4.25f,7f),new Vector3(8.4f,1.9f,.28f),m.wall,true);
            Box(annex,"Fire Annex Front Right",new Vector3(6.7f,2.6f,7f),new Vector3(4.6f,5.2f,.28f),m.wall,true);
            Transform annexRoof = Box(annex,"Fire Annex Roof",new Vector3(0,5.35f,0),new Vector3(18.6f,.35f,14.6f),m.darkWall,true);
            annexRoof.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            Transform annexEntrance = New("Fire Annex Entrance",annex);
            annexEntrance.localPosition = new Vector3(0f,0f,7.9f);
            Transform annexInterior = New("Fire Annex Interior",annex);
            annexInterior.localPosition = Vector3.zero;

            int fireDoors = 0;
            CreateDoubleDoor(
                annex,
                "Fire Annex Personnel Door",
                new Vector3(0f,0f,6.82f),
                Quaternion.identity,
                2.7f,
                2.5f,
                m.red,
                ref fireDoors);

            Box(annex,"Fire Equipment Locker A",new Vector3(-7.7f,1.2f,-4.7f),new Vector3(1.2f,2.4f,2.0f),m.red,true);
            Box(annex,"Fire Equipment Locker B",new Vector3(-6.2f,1.2f,-4.7f),new Vector3(1.2f,2.4f,2.0f),m.red,true);
            Box(annex,"Fire Crew Table",new Vector3(3f,.55f,-3f),new Vector3(4.5f,1.1f,2.2f),m.floor,true);
            InteriorLight(annex,new Vector3(0f,4.2f,0f),12f,1.35f);
            WorldText(annex,"FIRE & RESCUE",new Vector3(0f,4.1f,7.2f),Quaternion.Euler(0,180f,0),.22f,m.white);

            H51CitizenWorkplace fireWork = annex.gameObject.AddComponent<H51CitizenWorkplace>();
            fireWork.Configure(
                "life-"+Safe(town)+"-fire-rescue",
                town+" Fire & Rescue",
                town,
                "Fire",
                "Firefighter",
                6);

            fireAnnex = annex.gameObject.AddComponent<H51EnterableBuilding>();
            fireAnnex.Configure(
                "life-"+Safe(town)+"-fire-rescue",
                town+" Fire & Rescue",
                town,
                H51EnterableBuildingKind.Civic,
                annexEntrance,
                annexInterior);

            Vector3 fireBase = RoadHome(traffic,annex.position);
            BuildEmergencyVehicle(
                root,
                "Fire Engine 1",
                H51EmergencyServiceType.Fire,
                traffic,
                fireBase,
                annex.rotation,
                m,
                13f);
            emergencyVehicles++;

            Vector3 paramedicPos =
                hospital && hospital.InteriorCenter
                    ? hospital.InteriorCenter.position
                    : hospital ? hospital.transform.position : hospitalBase;
            paramedicAnchor = WorkAnchor(
                anchorRoot,
                "regional-paramedics",
                town,
                "Regional Hospital Ambulance Service",
                "Paramedic",
                4,
                paramedicPos,
                hospital ? hospital.transform.forward : Vector3.forward);
            Activity(paramedicAnchor.transform,new Vector3(-1.5f,0f,0f),"checking emergency medical equipment",6f,13f);
            Activity(paramedicAnchor.transform,new Vector3(1.5f,0f,0f),"stocking ambulance supplies",6f,13f);

            firefighterAnchor = WorkAnchor(
                anchorRoot,
                "regional-firefighters",
                town,
                town+" Fire & Rescue",
                "Firefighter",
                6,
                annexInterior.position,
                annex.forward);
            Activity(firefighterAnchor.transform,new Vector3(-2f,0f,-2f),"checking fire and rescue equipment",6f,13f);
            Activity(firefighterAnchor.transform,new Vector3(2f,0f,1f),"preparing the fire engine for response",6f,13f);
        }

        static int AssignPlantWorkers(
            List<H51CitizenIdentity> citizens,
            List<H51CitizenAnchor> anchors,
            string town)
        {
            if (citizens == null || anchors == null) return 0;

            string[] protectedWords =
            {
                "police","swat","dispatch","doctor","nurse","paramedic",
                "firefighter","tow","theater","power plant"
            };

            List<H51CitizenVehicle> vehicles = UnityEngine.Object.FindObjectsByType<H51CitizenVehicle>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Where(v => v).ToList();

            List<H51CitizenIdentity> pool = citizens
                .Where(c =>
                    c &&
                    c.TownName == town &&
                    !protectedWords.Any(
                        word => (c.Occupation ?? string.Empty).IndexOf(
                            word,
                            StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(c => vehicles.Any(v => v.IsOwnedBy(c)))
                .ThenBy(c => StableHash(c.CitizenId))
                .ToList();

            int assigned = 0;
            int poolIndex = 0;

            for (int a=0;a<anchors.Count;a++)
            {
                H51CitizenAnchor anchor = anchors[a];
                if (!anchor) continue;

                int desired =
                    anchor.OccupationHint == "Power Plant Operator" ? 2 :
                    anchor.OccupationHint == "Power Plant Maintenance Technician" ? 2 : 1;

                for (int i=0;i<desired && poolIndex<pool.Count;i++)
                {
                    H51CitizenIdentity c = pool[poolIndex++];
                    c.SetWorkAssignment(anchor.AnchorId,anchor.OccupationHint);
                    c.name = "Citizen - "+c.DisplayName+" - "+anchor.OccupationHint;
                    EditorUtility.SetDirty(c);
                    EditorUtility.SetDirty(c.gameObject);
                    assigned++;
                }
            }

            return assigned;
        }

        static int EnsureSpecialistStaff(
            Transform citizenRoot,
            H51CitizenRegistry registry,
            H51CitizenVisualCatalog catalog,
            string town,
            H51CitizenAnchor work,
            string occupation,
            int desired,
            HashSet<string> usedNames,
            int seedBase)
        {
            if (!citizenRoot || !work || desired <= 0) return 0;

            List<H51CitizenIdentity> existing =
                citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true)
                    .Where(c => c && string.Equals(
                        c.Occupation,
                        occupation,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

            for (int i=0;i<existing.Count;i++)
            {
                existing[i].SetWorkAssignment(work.AnchorId,occupation);
                EditorUtility.SetDirty(existing[i]);
            }

            int total = existing.Count;
            if (total >= desired || !catalog || catalog.entries == null || catalog.entries.Count == 0)
                return Mathf.Min(total,desired);

            H51CitizenAnchor[] homes =
                citizenRoot.GetComponentsInChildren<H51CitizenAnchor>(true)
                    .Where(a => a && a.Kind == H51CitizenAnchorKind.Home && a.TownName == town)
                    .ToArray();
            if (homes.Length == 0) return total;

            Transform population = FindChild(citizenRoot,"Citizen Population") ?? citizenRoot;
            Transform townPopulation = FindChild(population,town+" Residents");
            if (!townPopulation) townPopulation = New(town+" Residents",population);

            for (int index=total;index<desired;index++)
            {
                int seed = seedBase + index*137;
                H51CitizenVisualCatalog.Entry visual = PickVisual(catalog,occupation,seed);
                if (visual == null || !visual.prefab) continue;

                GameObject go = PrefabUtility.InstantiatePrefab(visual.prefab) as GameObject;
                if (!go) continue;
                go.transform.SetParent(townPopulation,false);

                H51CitizenAnchor home = homes[Math.Abs(seed)%homes.Length];
                Vector3 spawn = home.transform.position + new Vector3(
                    Mathf.Sin(seed*.17f)*2.1f,
                    0f,
                    Mathf.Cos(seed*.21f)*2.1f);
                if (NavMesh.SamplePosition(spawn,out NavMeshHit hit,15f,NavMesh.AllAreas))
                    spawn = hit.position;
                go.transform.position = spawn;
                go.transform.rotation = Quaternion.Euler(0f,Math.Abs(seed)%360,0f);

                string displayName = UniqueNeutralName(usedNames,seed);
                string citizenId = UniqueCitizenId(citizenRoot,occupation,seed);

                H51CitizenIdentity identity = go.GetComponent<H51CitizenIdentity>();
                if (!identity) identity = go.AddComponent<H51CitizenIdentity>();
                identity.Configure(
                    citizenId,
                    displayName,
                    22 + Math.Abs(seed)%39,
                    town,
                    occupation,
                    home.AnchorId,
                    work.AnchorId,
                    visual.packId+"-"+visual.archetype,
                    Math.Abs(seed),
                    .58f,
                    .78f,
                    .24f,
                    .48f);

                H51CitizenVisualVariation variation =
                    go.GetComponent<H51CitizenVisualVariation>();
                if (variation) variation.Configure(Math.Abs(seed),1f);

                NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
                if (!agent) agent = go.AddComponent<NavMeshAgent>();
                agent.speed = 1.6f;
                agent.acceleration = 7f;
                agent.angularSpeed = 320f;
                agent.radius = .32f;
                agent.height = 1.8f;
                agent.stoppingDistance = .65f;
                agent.avoidancePriority = 30 + Math.Abs(seed)%45;

                if (!go.GetComponent<H51CitizenBrain>()) go.AddComponent<H51CitizenBrain>();
                if (!go.GetComponent<H51CitizenDialogue>()) go.AddComponent<H51CitizenDialogue>();
                if (!go.GetComponent<H51CitizenGrounding>()) go.AddComponent<H51CitizenGrounding>();
                if (!go.GetComponent<Collider>())
                {
                    CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                    capsule.height = 1.8f;
                    capsule.radius = .32f;
                    capsule.center = new Vector3(0f,.9f,0f);
                }

                go.name = "Citizen - "+displayName+" - "+occupation;
                EditorUtility.SetDirty(go);
                EditorUtility.SetDirty(identity);
                total++;
            }

            registry.RefreshSceneReferences();
            return total;
        }

        static H51CitizenVisualCatalog.Entry PickVisual(
            H51CitizenVisualCatalog catalog,
            string occupation,
            int seed)
        {
            if (!catalog || catalog.entries == null || catalog.entries.Count == 0)
                return null;

            string lower = (occupation ?? string.Empty).ToLowerInvariant();
            string[] preferred =
                lower.Contains("fire")
                    ? new[] { "Worker","Swat","Soldier","Casual" }
                    : lower.Contains("paramedic")
                        ? new[] { "Worker","Casual2","Formal","Casual" }
                        : new[] { "Worker","Casual","Suit" };

            for (int p=0;p<preferred.Length;p++)
            {
                List<H51CitizenVisualCatalog.Entry> matches = catalog.entries
                    .Where(e =>
                        e != null &&
                        e.prefab &&
                        (e.archetype ?? string.Empty).IndexOf(
                            preferred[p],
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                if (matches.Count > 0)
                    return matches[Math.Abs(seed)%matches.Count];
            }

            return catalog.entries.FirstOrDefault(e => e != null && e.prefab);
        }

        static int ConfigureUtilityConsumers(
            Transform world,
            Transform plant,
            H51EnterableBuilding fireAnnex)
        {
            H51EnterableBuilding[] buildings =
                world.GetComponentsInChildren<H51EnterableBuilding>(true);

            int configured = 0;
            for (int i=0;i<buildings.Length;i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building) continue;

                string lower = building.DisplayName.ToLowerInvariant();
                bool critical =
                    lower.Contains("hospital") ||
                    lower.Contains("medical") ||
                    lower.Contains("police") ||
                    lower.Contains("fire") ||
                    lower.Contains("rescue");

                H51UtilityConsumer consumer =
                    building.GetComponent<H51UtilityConsumer>();
                if (!consumer)
                    consumer = building.gameObject.AddComponent<H51UtilityConsumer>();
                consumer.Configure(
                    critical,
                    critical,
                    true,
                    building.GetComponentsInChildren<Light>(true));
                EditorUtility.SetDirty(consumer);
                configured++;
            }

            if (plant)
            {
                H51UtilityConsumer plantConsumer =
                    plant.GetComponent<H51UtilityConsumer>();
                if (!plantConsumer)
                    plantConsumer = plant.gameObject.AddComponent<H51UtilityConsumer>();
                plantConsumer.Configure(
                    true,
                    false,
                    false,
                    plant.GetComponentsInChildren<Light>(true));
                EditorUtility.SetDirty(plantConsumer);
                configured++;
            }

            return configured;
        }

        static int BuildPlantAndRefreshNavigation(
            Transform citizenRoot,
            Transform plant,
            Transform operations)
        {
            Transform navigation = FindChild(citizenRoot,"Citizen Navigation");
            if (!navigation) navigation = New("Citizen Navigation",citizenRoot);

            Transform old = DirectChild(navigation,"NavMesh - Regional Power Station");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

            Transform navRoot = New("NavMesh - Regional Power Station",navigation);
            navRoot.position = plant.position;

            NavMeshSurface surface = navRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.center = new Vector3(0f,22f,0f);
            surface.size = new Vector3(235f,80f,200f);
            surface.BuildNavMesh();

            NavMeshSurface[] surfaces =
                citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);
            int built = 0;
            for (int i=0;i<surfaces.Length;i++)
            {
                if (!surfaces[i]) continue;
                surfaces[i].BuildNavMesh();
                if (surfaces[i].navMeshData) built++;
            }

            return built;
        }

        static int BuildPlantParking(
            Transform parent,
            H51TrafficNetwork traffic,
            string town,
            Mats m)
        {
            Transform parking = New("Employee Parking",parent);
            parking.localPosition = new Vector3(-45f,0f,48f);
            Box(parking,"Parking Surface",new Vector3(0,.04f,0),new Vector3(64f,.08f,24f),m.asphalt,false);

            int count = 0;
            for (int row=-1;row<=1;row+=2)
            {
                for (int x=-3;x<=3;x++)
                {
                    float px = x*8f;
                    float pz = row*6f;
                    Box(parking,"Parking Stripe "+row+"-"+x,new Vector3(px+3.6f,.09f,pz),new Vector3(.12f,.03f,9f),m.line,false);

                    Transform spot = New("Power Plant Parking Spot "+(++count).ToString("00"),parking);
                    spot.localPosition = new Vector3(px,0.13f,pz);
                    spot.localRotation = Quaternion.Euler(0,row>0?180f:0f,0);

                    Vector3 world = spot.position;
                    int node = traffic.FindNearestNode(world);
                    Vector3 approach = node >= 0 ? traffic.GetNode(node) : world + parking.forward*12f;

                    H51ParkingSpot parkingSpot =
                        spot.gameObject.AddComponent<H51ParkingSpot>();
                    parkingSpot.Configure(
                        "power-plant-"+count.ToString("00"),
                        "regional-power-station",
                        town,
                        approach);
                    EditorUtility.SetDirty(parkingSpot);
                }
            }

            WorldText(parking,"EMPLOYEE PARKING",new Vector3(-25f,.2f,-11f),Quaternion.identity,.18f,m.white);
            return count;
        }

        static List<ParticleSystem> BuildPlantVapor(
            Transform plant,
            Material particleMaterial)
        {
            List<ParticleSystem> result = new List<ParticleSystem>();

            for (int i=1;i<=2;i++)
            {
                Transform tower = FindChild(plant,"Cooling Tower "+i);
                if (!tower) continue;

                Transform old = DirectChild(tower,"H51 Cooling Tower Vapor");
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

                Transform plume = New("H51 Cooling Tower Vapor",tower);
                plume.localPosition = new Vector3(0f,42f,0f);
                result.Add(Particle(plume,particleMaterial,8f,7.5f,2.4f,.28f));
            }

            Transform stack = FindChild(plant,"Exhaust Stack");
            if (stack)
            {
                stack.name = "Ventilation Stack";
                Transform old = DirectChild(stack,"H51 Ventilation Vapor");
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

                Transform plume = New("H51 Ventilation Vapor",stack);
                plume.localPosition = new Vector3(0f,62.5f,0f);
                result.Add(Particle(plume,particleMaterial,2.4f,5f,1.4f,.16f));
            }

            return result;
        }

        static ParticleSystem Particle(
            Transform root,
            Material material,
            float radius,
            float lifetime,
            float speed,
            float alpha)
        {
            ParticleSystem ps = root.gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = radius*.75f;
            main.startColor = new Color(1f,1f,1f,alpha);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 8f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = radius;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-.15f,.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.15f,.15f);

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            if (renderer && material) renderer.sharedMaterial = material;

            ps.Play();
            return ps;
        }

        static H51EmergencyResponseVehicle BuildEmergencyVehicle(
            Transform parent,
            string name,
            H51EmergencyServiceType type,
            H51TrafficNetwork traffic,
            Vector3 position,
            Quaternion rotation,
            Mats m,
            float speed)
        {
            Transform root = New(name,parent);
            root.position = position;
            root.rotation = rotation;

            List<GameObject> lights = new List<GameObject>();

            if (type == H51EmergencyServiceType.Police)
            {
                Box(root,"Police Chassis",new Vector3(0,.48f,0),new Vector3(1.95f,.55f,4.6f),m.white,false);
                Box(root,"Police Hood",new Vector3(0,.78f,1.35f),new Vector3(1.85f,.35f,1.7f),m.white,false);
                Box(root,"Police Cabin",new Vector3(0,1.12f,-.25f),new Vector3(1.7f,.75f,2.1f),m.black,false);
                Box(root,"Police Side Stripe",new Vector3(0, .63f, -.15f),new Vector3(1.99f,.18f,3.3f),m.blue,false);
                lights.Add(Box(root,"Police Light Red",new Vector3(-.38f,1.58f,-.25f),new Vector3(.55f,.18f,.45f),m.red,false).gameObject);
                lights.Add(Box(root,"Police Light Blue",new Vector3(.38f,1.58f,-.25f),new Vector3(.55f,.18f,.45f),m.blue,false).gameObject);
                Wheels(root,m.black,1.65f,1.45f,.43f);
            }
            else if (type == H51EmergencyServiceType.Ambulance)
            {
                Box(root,"Ambulance Chassis",new Vector3(0,.45f,0),new Vector3(2.15f,.55f,5.6f),m.white,false);
                Box(root,"Ambulance Medical Box",new Vector3(0,1.55f,-.55f),new Vector3(2.2f,2.35f,3.85f),m.white,false);
                Box(root,"Ambulance Cab",new Vector3(0,1.25f,1.65f),new Vector3(2.05f,1.75f,1.55f),m.white,false);
                Box(root,"Ambulance Windshield",new Vector3(0,1.55f,2.45f),new Vector3(1.7f,.65f,.08f),m.glass,false);
                Box(root,"Ambulance Red Stripe",new Vector3(0,1.35f,-.55f),new Vector3(2.24f,.24f,3.9f),m.red,false);
                Cross(root,new Vector3(1.13f,1.85f,-.65f),Quaternion.Euler(0,90f,0),m.red);
                Cross(root,new Vector3(-1.13f,1.85f,-.65f),Quaternion.Euler(0,-90f,0),m.red);
                lights.Add(Box(root,"Ambulance Light Red",new Vector3(-.48f,2.78f,.45f),new Vector3(.65f,.20f,.45f),m.red,false).gameObject);
                lights.Add(Box(root,"Ambulance Light White",new Vector3(.48f,2.78f,.45f),new Vector3(.65f,.20f,.45f),m.white,false).gameObject);
                Wheels(root,m.black,1.75f,1.85f,.45f);
            }
            else
            {
                Box(root,"Fire Engine Chassis",new Vector3(0,.5f,0),new Vector3(2.45f,.65f,7.2f),m.red,false);
                Box(root,"Fire Engine Cab",new Vector3(0,1.55f,2.35f),new Vector3(2.35f,2.1f,2.1f),m.red,false);
                Box(root,"Fire Engine Equipment Body",new Vector3(0,1.45f,-1.25f),new Vector3(2.35f,1.9f,4.6f),m.red,false);
                Box(root,"Fire Engine Windshield",new Vector3(0,1.85f,3.43f),new Vector3(1.95f,.75f,.08f),m.glass,false);
                Box(root,"Fire Engine Pump Panel",new Vector3(1.22f,1.35f,-.7f),new Vector3(.08f,1.3f,2.2f),m.stainless,false);
                Box(root,"Fire Engine Ladder",new Vector3(0,2.65f,-1.1f),new Vector3(.55f,.20f,5.1f),m.stainless,false);
                lights.Add(Box(root,"Fire Light Red A",new Vector3(-.52f,2.73f,2.45f),new Vector3(.65f,.22f,.48f),m.red,false).gameObject);
                lights.Add(Box(root,"Fire Light Red B",new Vector3(.52f,2.73f,2.45f),new Vector3(.65f,.22f,.48f),m.red,false).gameObject);
                Wheels(root,m.black,2.0f,2.4f,.5f);
                Wheels(root,m.black,2.0f,-1.7f,.5f);
            }

            H51EmergencyResponseVehicle vehicle =
                root.gameObject.AddComponent<H51EmergencyResponseVehicle>();
            vehicle.Configure(type,traffic,position,rotation,speed,lights.ToArray());
            EditorUtility.SetDirty(vehicle);
            return vehicle;
        }

        static void Wheels(
            Transform root,
            Material material,
            float x,
            float z,
            float radius)
        {
            for (int side=-1;side<=1;side+=2)
            {
                Cylinder(
                    root,
                    "Wheel "+side+" "+z,
                    new Vector3(side*x*.5f,.45f,z),
                    Quaternion.Euler(0,0,90f),
                    new Vector3(radius,.22f,radius),
                    material,
                    false);
            }
        }

        static void Cross(
            Transform root,
            Vector3 position,
            Quaternion rotation,
            Material material)
        {
            Transform c = New("Medical Cross",root);
            c.localPosition = position;
            c.localRotation = rotation;
            Box(c,"Cross Vertical",Vector3.zero,new Vector3(.28f,1.0f,.06f),material,false);
            Box(c,"Cross Horizontal",Vector3.zero,new Vector3(.85f,.28f,.06f),material,false);
        }

        static Vector3 RoadHome(H51TrafficNetwork traffic,Vector3 reference)
        {
            if (!traffic) return reference;
            int node = traffic.FindNearestNode(reference);
            return node >= 0
                ? traffic.GetNode(node) + Vector3.up*.18f
                : reference;
        }

        static H51CitizenAnchor WorkAnchor(
            Transform parent,
            string id,
            string town,
            string label,
            string occupation,
            int capacity,
            Vector3 worldPosition,
            Vector3 forward)
        {
            Transform root = New("Work - "+label,parent);
            root.position = worldPosition;
            Vector3 flatForward = new Vector3(forward.x,0f,forward.z);
            if (flatForward.sqrMagnitude > .01f)
                root.rotation = Quaternion.LookRotation(flatForward.normalized,Vector3.up);

            H51CitizenAnchor anchor = root.gameObject.AddComponent<H51CitizenAnchor>();
            anchor.Configure(
                id,
                town,
                H51CitizenAnchorKind.Work,
                occupation,
                capacity,
                label,
                "regional-power-emergency");
            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        static void Activity(
            Transform anchor,
            Vector3 localOffset,
            string label,
            float minimum,
            float maximum)
        {
            Transform spot = New("Activity - "+label,anchor);
            spot.localPosition = localOffset;
            spot.localRotation = Quaternion.identity;
            H51CitizenActivitySpot activity =
                spot.gameObject.AddComponent<H51CitizenActivitySpot>();
            activity.Configure(label,"Interact",minimum,maximum);
            EditorUtility.SetDirty(activity);
        }

        static void PartitionXWithDoor(
            Transform parent,
            string name,
            float z,
            float xMin,
            float xMax,
            float doorCenter,
            float doorWidth,
            float height,
            Material wall,
            Material doorMaterial,
            ref int doors)
        {
            float doorMin = doorCenter-doorWidth*.5f;
            float doorMax = doorCenter+doorWidth*.5f;

            if (doorMin > xMin)
                Box(parent,name+" Left",new Vector3((xMin+doorMin)*.5f,height*.5f,z),new Vector3(doorMin-xMin,height,.22f),wall,true);
            if (xMax > doorMax)
                Box(parent,name+" Right",new Vector3((doorMax+xMax)*.5f,height*.5f,z),new Vector3(xMax-doorMax,height,.22f),wall,true);

            float doorHeight = 2.45f;
            Box(parent,name+" Header",new Vector3(doorCenter,(doorHeight+height)*.5f,z),new Vector3(doorWidth,height-doorHeight,.22f),wall,true);
            CreateSingleDoor(
                parent,
                name+" Door",
                new Vector3(doorMin,0f,z-.13f),
                Quaternion.identity,
                doorWidth,
                doorHeight,
                doorMaterial,
                true,
                -95f,
                ref doors);
        }

        static void CreateDoubleDoor(
            Transform parent,
            string name,
            Vector3 center,
            Quaternion rotation,
            float totalWidth,
            float height,
            Material material,
            ref int doors)
        {
            float leaf = totalWidth*.5f-.03f;
            Vector3 right = rotation * Vector3.right;

            CreateSingleDoor(
                parent,
                name+" Left",
                center-right*totalWidth*.5f,
                rotation,
                leaf,
                height,
                material,
                true,
                -92f,
                ref doors);

            CreateSingleDoor(
                parent,
                name+" Right",
                center+right*totalWidth*.5f,
                rotation,
                leaf,
                height,
                material,
                false,
                92f,
                ref doors);
        }

        static void CreateSingleDoor(
            Transform parent,
            string name,
            Vector3 hingePosition,
            Quaternion rotation,
            float width,
            float height,
            Material material,
            bool extendPositive,
            float openAngle,
            ref int doors)
        {
            Transform pivot = New(name,parent);
            pivot.localPosition = hingePosition;
            pivot.localRotation = rotation;

            float panelCenter = extendPositive ? width*.5f : -width*.5f;
            Box(
                pivot,
                "Door Panel",
                new Vector3(panelCenter,height*.5f,0f),
                new Vector3(width,height,.10f),
                material,
                true);
            Box(
                pivot,
                "Door Handle",
                new Vector3(extendPositive ? width*.82f : -width*.82f,1.05f,.08f),
                new Vector3(.10f,.20f,.10f),
                material,
                false);

            H51TownDoor door = pivot.gameObject.AddComponent<H51TownDoor>();
            Quaternion closedRotation = pivot.localRotation;
            door.Configure(
                pivot.localPosition,
                closedRotation,
                pivot.localPosition,
                closedRotation * Quaternion.Euler(0f,openAngle,0f),
                false);
            EditorUtility.SetDirty(door);
            doors++;
        }

        static void InteriorLight(
            Transform parent,
            Vector3 localPosition,
            float range,
            float intensity)
        {
            Transform root = New("Interior Utility Light",parent);
            root.localPosition = localPosition;
            Light light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f,.93f,.80f);
            light.shadows = LightShadows.None;

            H51InteriorLightProximity proximity =
                root.gameObject.AddComponent<H51InteriorLightProximity>();
            proximity.Configure(light,36f);
        }

        static string UniqueNeutralName(HashSet<string> used,int seed)
        {
            for (int attempt=0;attempt<100;attempt++)
            {
                string name =
                    NeutralFirstNames[Math.Abs(seed+attempt*7)%NeutralFirstNames.Length] +
                    " " +
                    LastNames[Math.Abs(seed*3+attempt*11)%LastNames.Length];
                if (used.Add(name)) return name;
            }

            string fallback = "Responder "+Math.Abs(seed);
            used.Add(fallback);
            return fallback;
        }

        static string UniqueCitizenId(Transform root,string occupation,int seed)
        {
            string baseId = "H51-"+Safe(occupation).ToUpperInvariant()+"-"+Math.Abs(seed).ToString("00000");
            HashSet<string> ids = new HashSet<string>(
                root.GetComponentsInChildren<H51CitizenIdentity>(true)
                    .Where(c => c)
                    .Select(c => c.CitizenId));

            string id = baseId;
            int suffix = 1;
            while (ids.Contains(id))
                id = baseId+"-"+(suffix++).ToString("00");
            return id;
        }

        static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i=0;i<text.Length;i++)
                    hash = hash*31 + text[i];
                return hash & 0x7fffffff;
            }
        }

        static Mats BuildMaterials()
        {
            EnsureFolder(Generated+"/Materials");
            Mats m = new Mats
            {
                concrete = Mat("Plant_Concrete",new Color(.43f,.45f,.44f),.10f),
                floor = Mat("Plant_Floor",new Color(.22f,.24f,.24f),.08f),
                wall = Mat("Plant_Wall",new Color(.68f,.70f,.68f),.10f),
                darkWall = Mat("Plant_Roof",new Color(.13f,.15f,.16f),.06f),
                metal = Mat("Plant_Metal",new Color(.30f,.32f,.33f),.34f),
                stainless = Mat("Plant_Stainless",new Color(.58f,.61f,.62f),.48f),
                black = Mat("Plant_Black",new Color(.035f,.045f,.05f),.18f),
                white = Mat("Plant_White",new Color(.86f,.87f,.84f),.12f),
                yellow = Mat("Plant_Safety_Yellow",new Color(.82f,.61f,.06f),.10f),
                red = Mat("Emergency_Red",new Color(.72f,.035f,.035f),.18f),
                blue = Mat("Emergency_Blue",new Color(.04f,.16f,.52f),.22f),
                green = Mat("Plant_Generator_Green",new Color(.16f,.31f,.22f),.20f),
                glass = Mat("Vehicle_Glass",new Color(.08f,.14f,.18f),.50f),
                asphalt = Mat("Plant_Asphalt",new Color(.075f,.08f,.08f),.05f),
                line = Mat("Plant_Parking_Line",new Color(.93f,.89f,.68f),.04f),
                particle = ParticleMaterial()
            };
            return m;
        }

        static Material Mat(string name,Color color,float smoothness)
        {
            string path = Generated+"/Materials/"+name+".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            if (!shader) return null;

            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color",color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic",name.Contains("Metal") || name.Contains("Stainless") ? .65f : 0f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        static Material ParticleMaterial()
        {
            string path = Generated+"/Materials/Plant_Vapor.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit");
            if (!shader) return null;

            Material material = new Material(shader) { name = "Plant Vapor" };
            Color color = new Color(.92f,.96f,1f,.32f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color",color);
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        static Transform Box(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer && material) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (!keepCollider && collider) UnityEngine.Object.DestroyImmediate(collider);
            return go.transform;
        }

        static Transform Cylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer && material) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (!keepCollider && collider) UnityEngine.Object.DestroyImmediate(collider);
            return go.transform;
        }

        static void WorldText(
            Transform parent,
            string text,
            Vector3 localPosition,
            Quaternion localRotation,
            float characterSize,
            Material material)
        {
            Transform root = New("Label - "+text,parent);
            root.localPosition = localPosition;
            root.localRotation = localRotation;
            TextMesh tm = root.gameObject.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = material == null ? Color.white : Color.white;
        }

        static Transform New(string name,Transform parent)
        {
            GameObject go = new GameObject(name);
            if (parent) go.transform.SetParent(parent,false);
            return go.transform;
        }

        static float Ground(Terrain terrain,Vector3 world)
        {
            if (!terrain) return world.y;
            return terrain.SampleHeight(world)+terrain.transform.position.y;
        }

        static void RemoveDirectChild(Transform root,string name)
        {
            Transform child = DirectChild(root,name);
            if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static void RemoveNamedDescendants(Transform root,string prefix)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            List<GameObject> kill = new List<GameObject>();
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i] != root && all[i].name.StartsWith(prefix,StringComparison.Ordinal))
                    kill.Add(all[i].gameObject);

            for (int i=0;i<kill.Count;i++)
                if (kill[i]) UnityEngine.Object.DestroyImmediate(kill[i]);
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

        static Transform FindChild(Transform root,string name)
        {
            if (!root) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].name == name)
                    return all[i];
            return null;
        }

        static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "regional";
            char[] chars = value.ToLowerInvariant().ToCharArray();
            for (int i=0;i<chars.Length;i++)
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '-';
            return new string(chars).Trim('-');
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i=1;i<parts.Length;i++)
            {
                string next = current+"/"+parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current,parts[i]);
                current = next;
            }
        }
    }
}
