using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldDenseCollisionAndRadiatorRepair
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string AirportName = "Hanger 51 Airport Complex";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string RegionalPassName = "Hanger 51 Regional Infrastructure Pass";
        const string PassName = "Hanger 51 Dense Collidable Countryside And Radiator Repair";
        const string UtilityRootName = "Dense Connected Utility Network";
        const string VegetationRootName = "Dense Regional Ground Clutter";
        const string RadiatorRootName = "P-51 Functional Belly Radiator";
        const string RadiatorSkinName = "Radiator Scoop Lower Belly Skin";
        const string BaseGen = "Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen = "Assets/_Project/Environment/Generated/CountrysideDenseCollisionPass";
        const string LeafTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string FirTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string GrassA = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string GrassB = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string RockLarge = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Stone/forestpack_stone_large_1.prefab";
        const string RockMedium = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Stone/forestpack_stone_medium_1.prefab";
        const string AluminumPath = "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        const string DarkAircraftPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        const int Seed = 51114;

        class Road
        {
            public string name;
            public Transform root;
            public float width;
            public List<Vector3> path = new List<Vector3>();
        }

        class Town
        {
            public string name;
            public Transform root;
            public Vector3 center;
            public List<Transform> localRoads = new List<Transform>();
        }

        class UtilityRoute
        {
            public string name;
            public Vector3[] startConductors;
            public Vector3[] endConductors;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public Transform root;
        }

        struct Mats
        {
            public Material wood;
            public Material metal;
            public Material white;
        }

        [MenuItem("Hanger 51/World/Current/114 - Dense Collidable Countryside Infrastructure And Radiator")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 114 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51WorldRegionalForestAndTownSetbackPass.Build();

            GameObject world = Find(WorldName);
            GameObject airport = Find(AirportName);
            GameObject regionalPass = Find(RegionalPassName);
            Terrain terrain = FindTerrain();
            if (!world || !airport || !regionalPass || !terrain)
            {
                Debug.LogError("Step 114 could not find the Step 112 world, airport, regional pass, or editable terrain.");
                return;
            }

            Transform roadsRoot = DirectChild(world.transform, "Road Network");
            Transform settlements = FindChild(world.transform, "Settlements");
            Transform plant = FindChild(regionalPass.transform, "Power Station Complex");
            if (!roadsRoot || !settlements || !plant)
            {
                Debug.LogError("Step 114 could not find the road network, towns, or power station.", world);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Preparing dense countryside pass", .02f);

                GameObject old = Find(PassName);
                if (old) UnityEngine.Object.DestroyImmediate(old);
                ResetFolder();
                Transform pass = New(PassName, world.transform);
                Mats mats = LoadMats();

                List<Road> roads = CollectRoads(roadsRoot);
                List<Town> towns = CaptureTowns(settlements, roadsRoot);
                List<Bounds> hardExclusions = CollectHardExclusions(world.transform, airport.transform, plant);

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Rebuilding connected power infrastructure", .12f);
                RemoveAllNamed(world.transform, "Final Regional And Town Utilities");
                RemoveAllNamed(world.transform, "Regional Utilities");
                RemoveAllNamed(world.transform, "Regional Nature Detail");
                RemoveAllNamed(world.transform, "Regional Road Details");
                RemoveAllNamed(world.transform, UtilityRootName);
                Transform utilityRoot = New(UtilityRootName, pass);
                int routePoles;
                int townPoles;
                int utilityTies;
                int utilityWires;
                BuildConnectedUtilities(terrain, utilityRoot, roadsRoot, towns, plant, mats,
                    out routePoles, out townPoles, out utilityTies, out utilityWires);

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Populating mature trees across all usable terrain", .30f);
                int terrainTrees = BuildDenseTerrainForest(terrain, roads, hardExclusions);
                ConfigureTerrainTreeCollisionSupport(terrain);

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Filling ground with dense grass detail", .47f);
                int grassCells = FillDenseTerrainGrass(terrain, roads, hardExclusions);

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Adding project grass and solid clutter", .61f);
                RemoveAllNamed(world.transform, VegetationRootName);
                Transform vegetation = New(VegetationRootName, pass);
                int assetGrass = AddAssetGrass(terrain, vegetation, roads, hardExclusions, 2600);
                int solidRocks = AddSolidRocks(terrain, vegetation, roads, hardExclusions, 550);

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Installing runtime tree collision fallback", .74f);
                TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();
                if (!streamer) streamer = Undo.AddComponent<TerrainTreeCollisionProxyStreamer>(terrain.gameObject);
                streamer.Configure(terrain, 520f, .10f, 2200);
                EditorUtility.SetDirty(streamer);

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Restoring P-51 radiator lower belly skin", .84f);
                int radiatorRepairs = RepairRadiatorUndersides();

                EditorUtility.DisplayProgressBar("Hanger 51 Dense Countryside", "Final terrain and scene validation prep", .95f);
                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = pass.gameObject;

                Debug.Log(
                    $"Step 114 complete. terrain trees={terrainTrees}, dense grass cells={grassCells}, asset grass clumps={assetGrass}, solid collidable rocks={solidRocks}, regional utility poles={routePoles}, town utility poles={townPoles}, utility ties={utilityTies}, utility wires={utilityWires}, radiator undersides repaired={radiatorRepairs}. Run Step 115 to validate.",
                    pass.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/115 - Validate Dense Collisions Infrastructure And Radiator")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            GameObject pass = Find(PassName);
            Terrain terrain = FindTerrain();
            if (!world || !pass || !terrain)
            {
                Debug.LogError("Step 115 failed: run Step 114 first.");
                return;
            }

            Transform roadsRoot = DirectChild(world.transform, "Road Network");
            Transform utilityRoot = FindChild(pass.transform, UtilityRootName);
            Transform vegetation = FindChild(pass.transform, VegetationRootName);
            if (!roadsRoot || !utilityRoot || !vegetation)
            {
                Debug.LogError("Step 115 failed: road, utility, or vegetation roots are missing.", pass);
                return;
            }

            List<Road> roads = CollectRoads(roadsRoot);
            int treeRoadIntrusions = CountTerrainTreeRoadIntrusions(terrain, roads, 15f);
            int treeColliderPrototypeFailures = CountTreePrototypeColliderFailures(terrain);
            int rockColliderFailures = CountColliderFailures(vegetation, "Solid Ground Rock");
            int rockRoadIntrusions = CountNamedRoadIntrusions(vegetation, "Solid Ground Rock", roads, 8f);
            TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();

            int regionalCorridors = Count(utilityRoot, "Regional Utility Corridor");
            int townCorridors = Count(utilityRoot, "Town Utility Corridor");
            int tieWires = Count(utilityRoot, "Utility Tie Wire");
            int wires = Count(utilityRoot, "Utility Wire");
            int poles = Count(utilityRoot, "Utility Pole");
            int grass = Count(vegetation, "Dense Asset Grass");
            int rocks = Count(vegetation, "Solid Ground Rock");
            int radiatorAircraft = 0;
            int radiatorFailures = 0;

            P51FlightController[] aircraft = UnityEngine.Object.FindObjectsByType<P51FlightController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (P51FlightController flight in aircraft)
            {
                if (!flight || !flight.gameObject.scene.IsValid()) continue;
                radiatorAircraft++;
                Transform radiator = FindChild(flight.transform, RadiatorRootName);
                Transform skin = radiator ? DirectChild(radiator, RadiatorSkinName) : null;
                if (!skin || Count(skin, "Lower Belly Panel") < 4) radiatorFailures++;
            }

            int denseGrassCells = CountDenseGrassCells(terrain);
            bool ok =
                terrain.terrainData.treeInstanceCount >= 28000 &&
                denseGrassCells >= 90000 &&
                grass >= 2000 &&
                rocks >= 450 &&
                treeRoadIntrusions == 0 &&
