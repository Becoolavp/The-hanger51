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
                rockRoadIntrusions == 0 &&
                treeColliderPrototypeFailures == 0 &&
                rockColliderFailures == 0 &&
                streamer != null &&
                regionalCorridors >= 5 &&
                townCorridors >= 4 &&
                tieWires >= 18 &&
                wires >= 180 &&
                poles >= 90 &&
                radiatorAircraft > 0 &&
                radiatorFailures == 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 115 passed. trees={terrain.terrainData.treeInstanceCount}, grass cells={denseGrassCells}, asset grass={grass}, collidable rocks={rocks}, tree-road intrusions={treeRoadIntrusions}, rock-road intrusions={rockRoadIntrusions}, tree prefab collider failures={treeColliderPrototypeFailures}, rock collider failures={rockColliderFailures}, utility corridors={regionalCorridors}+{townCorridors}, poles={poles}, wires={wires}, tie wires={tieWires}, radiator aircraft={radiatorAircraft}.",
                    pass);
            }
            else
            {
                Debug.LogError(
                    $"Step 115 failed. trees={terrain.terrainData.treeInstanceCount}, grass cells={denseGrassCells}, asset grass={grass}, rocks={rocks}, tree-road intrusions={treeRoadIntrusions}, rock-road intrusions={rockRoadIntrusions}, tree prefab collider failures={treeColliderPrototypeFailures}, rock collider failures={rockColliderFailures}, collision streamer={(streamer ? "present" : "missing")}, regional utility corridors={regionalCorridors}, town corridors={townCorridors}, poles={poles}, wires={wires}, tie wires={tieWires}, radiator aircraft={radiatorAircraft}, radiator failures={radiatorFailures}.",
                    pass);
            }
        }

        static Mats LoadMats()
        {
            return new Mats
            {
                wood = LoadMat("Weathered_Wood"),
                metal = LoadMat("Dark_Metal"),
                white = LoadMat("Warm_White")
            };
        }

        static Material LoadMat(string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(BaseGen + "/Materials/" + name + ".mat");
            if (m)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", Color.black);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        static List<Road> CollectRoads(Transform roadsRoot)
        {
            List<Road> roads = new List<Road>();
            foreach (Transform tr in roadsRoot.GetComponentsInChildren<Transform>(true))
            {
                Transform surface = DirectChild(tr, "Road Surface");
                if (!surface) continue;
                MeshFilter mf = surface.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                Vector3[] vertices = mf.sharedMesh.vertices;
                if (vertices.Length < 4) continue;

                Road road = new Road { name = tr.name, root = tr };
                for (int i = 0; i + 1 < vertices.Length; i += 2)
                {
                    Vector3 a = mf.transform.TransformPoint(vertices[i]);
                    Vector3 b = mf.transform.TransformPoint(vertices[i + 1]);
                    road.path.Add((a + b) * .5f);
                    if (i == 0) road.width = Vector3.Distance(a, b);
                }
                if (road.path.Count >= 2) roads.Add(road);
            }
            return roads;
        }

        static List<Town> CaptureTowns(Transform settlements, Transform roadsRoot)
        {
            List<Town> towns = new List<Town>();
            for (int i = 0; i < settlements.childCount; i++)
            {
                Transform root = settlements.GetChild(i);
                List<Transform> houses = new List<Transform>();
                foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name.StartsWith("Detailed House")) houses.Add(tr);
                if (houses.Count == 0) continue;

                Vector3 center = Vector3.zero;
                foreach (Transform h in houses) center += h.position;
                center /= houses.Count;

                Town town = new Town { name = root.name, root = root, center = center };
                for (int r = 0; r < roadsRoot.childCount; r++)
                {
                    Transform road = roadsRoot.GetChild(r);
                    if (road.name.StartsWith(root.name) && DirectChild(road, "Road Surface")) town.localRoads.Add(road);
                }
                towns.Add(town);
            }
            return towns;
        }

        static List<Bounds> CollectHardExclusions(Transform world, Transform airport, Transform plant)
        {
            List<Bounds> bounds = new List<Bounds>();

            foreach (Transform tr in airport.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name.ToLowerInvariant();
                float pad = n.Contains("runway") ? 70f : n.Contains("taxiway") ? 28f : n.Contains("apron") ? 24f :
                            n.Contains("hangar") || n.Contains("hanger") ? 18f : -1f;
                if (pad < 0f) continue;
                if (TryBounds(tr, out Bounds b))
                {
                    b.Expand(new Vector3(pad * 2f, 40f, pad * 2f));
                    bounds.Add(b);
                }
            }

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                bool building = n.StartsWith("Detailed House") || n.StartsWith("Purpose -") || n.StartsWith("Farmstead -") ||
                                n == "Power Station Complex" || n.StartsWith("Detached Garage") || n.StartsWith("Community Park -");
                if (!building) continue;
                if (TryBounds(tr, out Bounds b))
                {
                    b.Expand(new Vector3(18f, 20f, 18f));
                    bounds.Add(b);
                }
            }

            if (plant && TryBounds(plant, out Bounds plantBounds))
            {
                plantBounds.Expand(new Vector3(70f, 40f, 70f));
                bounds.Add(plantBounds);
            }
            return bounds;
        }

        static int BuildDenseTerrainForest(Terrain terrain, List<Road> roads, List<Bounds> exclusions)
        {
            TerrainData data = terrain.terrainData;
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirTree);
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafTree);
            if (!fir || !leaf)
            {
                Debug.LogError("Step 114 could not load both Supercyan mature tree prefabs.");
                return data.treeInstanceCount;
            }

            data.treePrototypes = new[]
            {
                new TreePrototype { prefab = fir, bendFactor = .25f },
                new TreePrototype { prefab = leaf, bendFactor = .32f }
            };

            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            System.Random rng = new System.Random(Seed + 10);
            List<TreeInstance> trees = new List<TreeInstance>(32000);
            int target = 32000;
            int tries = 0;
            int maxTries = 850000;

            while (trees.Count < target && tries++ < maxTries)
            {
                float nx = Next(rng, .006f, .994f);
                float nz = Next(rng, .006f, .994f);
                Vector3 p = new Vector3(origin.x + nx * size.x, 0f, origin.z + nz * size.z);
                if (InsideAny(p, exclusions)) continue;
                if (NearRoad(p, roads, 18f)) continue;

                float broad = Fbm(p.x * .00031f + 17f, p.z * .00031f + 43f, 4);
                float fine = Fbm(p.x * .0011f + 61f, p.z * .0011f + 91f, 3);
                float acceptance = Mathf.Clamp01(.58f + broad * .24f + fine * .18f);
                if (rng.NextDouble() > acceptance) continue;

                int prototype = rng.NextDouble() < .46 ? 0 : 1;
                float heightScale = prototype == 0 ? Next(rng, 2.65f, 4.15f) : Next(rng, 4.10f, 6.25f);
                float widthScale = heightScale * Next(rng, .74f, .94f);
                trees.Add(new TreeInstance
                {
                    position = new Vector3(nx, data.GetInterpolatedHeight(nx, nz) / Mathf.Max(1f, size.y), nz),
                    prototypeIndex = prototype,
                    widthScale = widthScale,
                    heightScale = heightScale,
                    rotation = Next(rng, 0f, Mathf.PI * 2f),
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }

            data.treeInstances = trees.ToArray();
            EditorUtility.SetDirty(data);
            return trees.Count;
        }

        static void ConfigureTerrainTreeCollisionSupport(Terrain terrain)
        {
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (!collider) collider = Undo.AddComponent<TerrainCollider>(terrain.gameObject);
            collider.terrainData = terrain.terrainData;
            collider.enabled = true;

            SerializedObject serialized = new SerializedObject(collider);
            SerializedProperty enableTrees = serialized.FindProperty("m_EnableTreeColliders");
            if (enableTrees != null) enableTrees.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(collider);
        }

        static int FillDenseTerrainGrass(Terrain terrain, List<Road> roads, List<Bounds> exclusions)
        {
            TerrainData data = terrain.terrainData;
            if (data.detailPrototypes == null || data.detailPrototypes.Length == 0)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/_Project/Environment/Generated/CountrysideVisualUpgrade/Textures/MeadowGrass.asset");
                if (texture)
                {
