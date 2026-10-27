using System;
using System.Collections.Generic;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldFarmScaleAndEquipmentPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string AgriculturePassName = "Hanger 51 Agricultural Land Use And Tree Alignment";
        const string FieldRootName = "Agricultural Field Parcels";
        const string PassName = "Hanger 51 Mature Trees And Working Farms";
        const string FarmRootName = "Working Farmsteads";
        const string BaseMat = "Assets/_Project/Environment/Generated/CountrysideLivedInPass/Materials/";
        const int Seed = 51120;

        class Road
        {
            public float width;
            public List<Vector3> path = new List<Vector3>();
        }

        class FieldParcel
        {
            public Transform marker;
            public Vector3 center;
            public Vector2 size;
            public float rotation;
            public int cropType;
            public string name;
        }

        class FarmSite
        {
            public FieldParcel field;
            public Vector3 yard;
            public Vector3 nearestRoad;
            public Vector3 roadTangent;
            public float roadDistance;
        }

        struct Mats
        {
            public Material red;
            public Material green;
            public Material white;
            public Material roof;
            public Material metal;
            public Material wood;
            public Material gravel;
            public Material dirt;
            public Material concrete;
            public Material rubber;
            public Material glass;
            public Material yellow;
        }

        [MenuItem("Hanger 51/World/Current/120 - Mature Trees And Build Working Farms")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 120 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform agriculture = world ? FindChild(world.transform, AgriculturePassName) : null;
            Transform fieldRoot = agriculture ? FindChild(agriculture, FieldRootName) : null;
            Transform roadsRoot = world ? FindChild(world.transform, "Road Network") : null;
            if (!world || !terrain || !agriculture || !fieldRoot || !roadsRoot)
            {
                Debug.LogError("Step 120 failed. Run Step 116 first so the agricultural fields exist.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Farms", "Normalizing all existing generated trees", .06f);
                int treesRescaled;
                int treeColliderRepairs;
                int treeColliderFailures;
                NormalizeAllSceneTrees(world.transform, terrain, out treesRescaled, out treeColliderRepairs, out treeColliderFailures);

                EditorUtility.DisplayProgressBar("Hanger 51 Farms", "Reading field parcels and roads", .18f);
                List<FieldParcel> fields = ReadFields(fieldRoot);
                List<Road> roads = CollectRoads(roadsRoot);
                if (fields.Count == 0 || roads.Count == 0)
                {
                    Debug.LogError("Step 120 could not read the field parcels or road network.");
                    return;
                }

                Transform old = FindChild(world.transform, PassName);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform pass = New(PassName, world.transform);
                Transform farmRoot = New(FarmRootName, pass);
                Mats mats = LoadMaterials();

                EditorUtility.DisplayProgressBar("Hanger 51 Farms", "Selecting road-accessible working farm sites", .28f);
                List<FarmSite> sites = ChooseFarmSites(terrain, fields, roads, 18);

                EditorUtility.DisplayProgressBar("Hanger 51 Farms", "Building barns, silos, sheds, machinery and yards", .42f);
                int barns = 0, silos = 0, tractors = 0, combines = 0, hayBales = 0, fences = 0, lanes = 0;
                for (int i = 0; i < sites.Count; i++)
                {
                    BuildWorkingFarm(terrain, farmRoot, sites[i], i, mats,
                        ref barns, ref silos, ref tractors, ref combines, ref hayBales, ref fences, ref lanes);
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Farms", "Clearing trees and crops from farmyards and lanes", .72f);
                int terrainTreesRemoved = ClearTerrainTreesNearFarmSites(terrain, sites);
                int detailCellsCleared = ClearTerrainDetailsNearFarmSites(terrain, sites);

                EditorUtility.DisplayProgressBar("Hanger 51 Farms", "Adding larger shelter belts around farm country", .84f);
                int shelterTrees = UpgradeAndExpandFieldShelterBelts(terrain, fieldRoot, roads);

                TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();
                if (streamer)
                {
                    streamer.RebuildIndex();
                    EditorUtility.SetDirty(streamer);
                }

                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
                EditorUtility.SetDirty(terrain);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = pass.gameObject;

                Debug.Log(
                    $"Step 120 complete. scene trees rescaled={treesRescaled}, scene-tree collider repairs={treeColliderRepairs}, remaining scene-tree collider failures={treeColliderFailures}, working farms={sites.Count}, barns={barns}, silos={silos}, tractors={tractors}, combines={combines}, hay bales={hayBales}, fence sections={fences}, farm lanes={lanes}, terrain trees removed from farmyards={terrainTreesRemoved}, detail cells cleared from farmyards={detailCellsCleared}, larger field-edge trees added/upgraded={shelterTrees}. Run Step 121 to validate.",
                    pass.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/121 - Validate Mature Trees And Working Farms")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform pass = world ? FindChild(world.transform, PassName) : null;
            Transform farms = pass ? FindChild(pass, FarmRootName) : null;
            if (!world || !terrain || !pass || !farms)
            {
                Debug.LogError("Step 121 failed. Run Step 120 first.");
                return;
            }

            int generatedTrees = 0;
            int undersizedTrees = 0;
            int treeColliderFailures = 0;
            foreach (Transform tr in world.transform.GetComponentsInChildren<Transform>(true))
            {
                if (!IsManagedTreeRoot(tr.name)) continue;
                float h = RendererHeight(tr);
                if (h <= .01f) continue;
                generatedTrees++;
                if (h < 9.0f) undersizedTrees++;
                if (!HasSolidCollider(tr)) treeColliderFailures++;
            }

            int farmCount = Count(farms, "Working Farm ");
            int barns = Count(farms, "Main Barn");
            int silos = Count(farms, "Grain Silo");
            int sheds = Count(farms, "Machine Shed");
            int tractors = Count(farms, "Farm Tractor");
            int combines = Count(farms, "Combine Harvester");
            int hay = Count(farms, "Hay Bale");
            int fences = Count(farms, "Farm Fence");
            int lanes = Count(farms, "Farm Access Lane");
            int equipment = Count(farms, "Farm Equipment");
            int farmColliderFailures = CountFarmColliderFailures(farms);

            bool ok =
                generatedTrees >= 40 &&
                undersizedTrees == 0 &&
                treeColliderFailures == 0 &&
                farmCount >= 12 &&
                barns >= 12 &&
                silos >= 12 &&
                sheds >= 12 &&
                tractors >= 10 &&
                combines >= 5 &&
                hay >= 120 &&
                fences >= 120 &&
                lanes >= 12 &&
                equipment >= 20 &&
                farmColliderFailures == 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 121 passed. managed scene trees={generatedTrees}, undersized trees={undersizedTrees}, tree collider failures={treeColliderFailures}, farms={farmCount}, barns={barns}, silos={silos}, sheds={sheds}, tractors={tractors}, combines={combines}, hay bales={hay}, fence sections={fences}, access lanes={lanes}, equipment props={equipment}, farm collider failures={farmColliderFailures}.",
                    pass.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 121 failed. managed scene trees={generatedTrees}, undersized trees={undersizedTrees}, tree collider failures={treeColliderFailures}, farms={farmCount}, barns={barns}, silos={silos}, sheds={sheds}, tractors={tractors}, combines={combines}, hay bales={hay}, fence sections={fences}, access lanes={lanes}, equipment props={equipment}, farm collider failures={farmColliderFailures}.",
                    pass.gameObject);
            }
        }
