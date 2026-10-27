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
                    data.detailPrototypes = new[]
                    {
                        new DetailPrototype
                        {
                            prototypeTexture = texture,
                            renderMode = DetailRenderMode.GrassBillboard,
                            minWidth = .5f,
                            maxWidth = 1.15f,
                            minHeight = .55f,
                            maxHeight = 1.5f,
                            noiseSpread = .16f,
                            healthyColor = Color.white,
                            dryColor = new Color(.76f, .70f, .45f)
                        }
                    };
                }
            }
            if (data.detailPrototypes == null || data.detailPrototypes.Length == 0) return 0;

            data.SetDetailResolution(768, 16);
            int width = data.detailWidth;
            int height = data.detailHeight;
            bool[,] blocked = new bool[height, width];
            RasterizeExclusions(blocked, terrain, exclusions);
            RasterizeRoads(blocked, terrain, roads, 8f);

            int[,] map = new int[height, width];
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int filled = 0;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (blocked[z, x]) continue;
                    float wx = origin.x + x / (float)Mathf.Max(1, width - 1) * size.x;
                    float wz = origin.z + z / (float)Mathf.Max(1, height - 1) * size.z;
                    float noise = Fbm(wx * .0017f + 33f, wz * .0017f + 67f, 3);
                    map[z, x] = Mathf.RoundToInt(Mathf.Lerp(5f, 14f, noise));
                    if (map[z, x] > 0) filled++;
                }
            }
            data.SetDetailLayer(0, 0, 0, map);
            EditorUtility.SetDirty(data);
            return filled;
        }

        static int AddAssetGrass(Terrain terrain, Transform root, List<Road> roads, List<Bounds> exclusions, int target)
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(GrassA);
            GameObject b = AssetDatabase.LoadAssetAtPath<GameObject>(GrassB);
            if (!a && !b) return 0;

            System.Random rng = new System.Random(Seed + 20);
            int made = 0;
            int tries = 0;
            while (made < target && tries++ < target * 22)
            {
                Vector3 p = RandomTerrainPoint(rng, terrain);
                if (InsideAny(p, exclusions) || NearRoad(p, roads, 7f)) continue;
                GameObject src = rng.NextDouble() < .5 ? a : b;
                if (!src) src = a ? a : b;
                GameObject g = PrefabUtility.InstantiatePrefab(src) as GameObject;
                if (!g) continue;
                g.name = "Dense Asset Grass " + (++made).ToString("0000");
                g.transform.SetParent(root, false);
                g.transform.position = p;
                g.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
                g.transform.localScale = Vector3.one * Next(rng, .8f, 1.65f);
                DisableAllColliders(g.transform);
            }
            return made;
        }

        static int AddSolidRocks(Terrain terrain, Transform root, List<Road> roads, List<Bounds> exclusions, int target)
        {
            GameObject large = AssetDatabase.LoadAssetAtPath<GameObject>(RockLarge);
            GameObject medium = AssetDatabase.LoadAssetAtPath<GameObject>(RockMedium);
            if (!large && !medium) return 0;

            System.Random rng = new System.Random(Seed + 30);
            int made = 0;
            int tries = 0;
            while (made < target && tries++ < target * 30)
            {
                Vector3 p = RandomTerrainPoint(rng, terrain);
                if (InsideAny(p, exclusions) || NearRoad(p, roads, 11f)) continue;
                if (rng.NextDouble() > .28) continue;
                GameObject src = rng.NextDouble() < .38 ? large : medium;
                if (!src) src = large ? large : medium;
                GameObject g = PrefabUtility.InstantiatePrefab(src) as GameObject;
                if (!g) continue;
                g.name = "Solid Ground Rock " + (++made).ToString("0000");
                g.transform.SetParent(root, false);
                g.transform.position = p - Vector3.up * Next(rng, .02f, .12f);
                g.transform.rotation = Quaternion.Euler(Next(rng, -8f, 8f), Next(rng, 0f, 360f), Next(rng, -8f, 8f));
                g.transform.localScale = Vector3.one * Next(rng, .85f, 2.2f);
                EnsureSolidCollider(g);
            }
            return made;
        }

        static void BuildConnectedUtilities(
            Terrain terrain,
            Transform root,
            Transform roadsRoot,
            List<Town> towns,
            Transform plant,
            Mats mats,
            out int regionalPoles,
            out int townPoles,
            out int ties,
            out int wires)
        {
            regionalPoles = 0;
            townPoles = 0;
            ties = 0;
            wires = 0;

            Transform regionalRoadRoot = DirectChild(roadsRoot, "Natural Regional Road Network");
            if (!regionalRoadRoot) return;

            Dictionary<Town, UtilityRoute> townRoutes = new Dictionary<Town, UtilityRoute>();
            foreach (Town town in towns)
            {
                Transform longest = LongestRoad(town.localRoads);
                if (!longest) continue;
                Road road = ReadRoad(longest);
                if (road == null) continue;
                UtilityRoute route = BuildUtilityRoute(terrain, root, road, "Town Utility Corridor - " + town.name,
                    44f, 8.5f, 7.9f, mats, ref townPoles, ref wires);
                if (route != null) townRoutes[town] = route;
            }

            List<UtilityRoute> regionalRoutes = new List<UtilityRoute>();
            for (int i = 0; i < regionalRoadRoot.childCount; i++)
            {
                Transform roadTransform = regionalRoadRoot.GetChild(i);
                Road road = ReadRoad(roadTransform);
                if (road == null) continue;
                UtilityRoute route = BuildUtilityRoute(terrain, root, road,
                    "Regional Utility Corridor - " + road.name, 54f, 11f, 8.35f, mats, ref regionalPoles, ref wires);
                if (route != null) regionalRoutes.Add(route);
            }

            foreach (UtilityRoute route in regionalRoutes)
            {
                ties += TieUtilityEnd(route.startPosition, route.startConductors, towns, townRoutes, plant, root, mats, ref wires);
                ties += TieUtilityEnd(route.endPosition, route.endConductors, towns, townRoutes, plant, root, mats, ref wires);
            }
        }

        static UtilityRoute BuildUtilityRoute(
            Terrain terrain,
            Transform parent,
            Road road,
            string routeName,
            float spacing,
            float sideOffset,
            float wireHeight,
            Mats mats,
            ref int poleCount,
            ref int wireCount)
        {
            List<Vector3> sampled = Resample(road.path, spacing);
            if (sampled.Count < 2) return null;
            Transform routeRoot = New(routeName, parent);
            Vector3[] previous = null;
            Vector3[] first = null;
            Vector3[] last = null;
            Vector3 firstPole = Vector3.zero;
            Vector3 lastPole = Vector3.zero;
            int sideSign = StableSign(road.name);

            for (int i = 0; i < sampled.Count; i++)
            {
                Vector3 tangent = PathTangent(sampled, i);
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 position = sampled[i] + side * sideSign * (road.width * .5f + sideOffset);
                position.y = Ground(terrain, position);

                Transform pole = New("Utility Pole " + (++poleCount).ToString("0000"), routeRoot);
                pole.position = position;
                pole.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                Cylinder(pole, "Pole", new Vector3(0f, wireHeight * .53f, 0f), new Vector3(.13f, wireHeight * .53f, .13f), mats.wood, true);
                Box(pole, "Crossarm", new Vector3(0f, wireHeight - .35f, 0f), new Vector3(2.35f, .14f, .14f), mats.wood, true);

                Vector3[] current = new Vector3[3];
                for (int k = -1; k <= 1; k++)
                {
                    Cylinder(pole, "Insulator", new Vector3(k * .74f, wireHeight - .12f, 0f), new Vector3(.055f, .15f, .055f), mats.white, true);
                    current[k + 1] = pole.TransformPoint(new Vector3(k * .74f, wireHeight + .08f, 0f));
                    if (previous != null)
                    {
                        Wire(routeRoot, previous[k + 1], current[k + 1], mats.metal, "Utility Wire", true);
                        wireCount++;
                    }
                }

                if (first == null)
                {
                    first = current;
                    firstPole = position;
                }
                last = current;
                lastPole = position;
                previous = current;
            }

            return new UtilityRoute
            {
                name = routeName,
                root = routeRoot,
                startConductors = first,
                endConductors = last,
                startPosition = firstPole,
                endPosition = lastPole
            };
        }

        static int TieUtilityEnd(
            Vector3 position,
            Vector3[] conductors,
            List<Town> towns,
            Dictionary<Town, UtilityRoute> townRoutes,
            Transform plant,
            Transform root,
            Mats mats,
            ref int wireCount)
        {
            if (conductors == null || conductors.Length != 3) return 0;

            if (plant && Planar(position, plant.position) < 350f)
            {
                Transform substation = FindChild(plant, "Electrical Substation");
                Vector3 target = substation ? substation.position : plant.position;
                Vector3 approach = position - target;
                approach.y = 0f;
                if (approach.sqrMagnitude < .01f) approach = plant.forward;
                approach.Normalize();
                target += approach * 18f;
                target.y = GroundAtPlantConnection(position.y, substation ? substation : plant) + 8.2f;
                Vector3 cross = Vector3.Cross(Vector3.up, approach).normalized;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 end = target + cross * ((i - 1) * .75f);
                    Wire(root, conductors[i], end, mats.metal, "Utility Tie Wire - Plant Substation", true);
                    wireCount++;
                }
                return 1;
            }

            Town nearestTown = null;
            float best = float.MaxValue;
            foreach (Town town in towns)
            {
                float d = Planar(position, town.center);
                if (d < best) { best = d; nearestTown = town; }
            }
            if (nearestTown == null || !townRoutes.TryGetValue(nearestTown, out UtilityRoute townRoute)) return 0;

            Vector3[] targetConductors;
            float startDist = Planar(position, townRoute.startPosition);
            float endDist = Planar(position, townRoute.endPosition);
            targetConductors = startDist <= endDist ? townRoute.startConductors : townRoute.endConductors;
            if (targetConductors == null) return 0;

            for (int i = 0; i < 3; i++)
            {
                Wire(root, conductors[i], targetConductors[i], mats.metal, "Utility Tie Wire - " + nearestTown.name, true);
                wireCount++;
            }
            return 1;
        }

        static float GroundAtPlantConnection(float fallback, Transform target)
        {
            if (!target) return fallback;
            return target.position.y;
        }

        static int RepairRadiatorUndersides()
        {
            P51FlightController[] aircraft = UnityEngine.Object.FindObjectsByType<P51FlightController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool missingRadiator = false;
            foreach (P51FlightController flight in aircraft)
                if (flight && flight.gameObject.scene.IsValid() && !FindChild(flight.transform, RadiatorRootName)) missingRadiator = true;

            if (missingRadiator)
            {
                P51RadiatorCoolingSystemSetup.BuildFunctionalRadiatorAndCoolantSystem();
                P51RadiatorAndTailwheelVisualFitRepair.RepairRadiatorAndTailwheelFit();
                aircraft = UnityEngine.Object.FindObjectsByType<P51FlightController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkAircraftPath);
            if (!aluminum || !dark)
            {
                Debug.LogError("Step 114 could not load the P-51 radiator materials.");
                return 0;
            }

            int repaired = 0;
            foreach (P51FlightController flight in aircraft)
            {
                if (!flight || !flight.gameObject.scene.IsValid()) continue;
                Transform radiator = FindChild(flight.transform, RadiatorRootName);
                if (!radiator) continue;

                Transform old = DirectChild(radiator, RadiatorSkinName);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform skin = New(RadiatorSkinName, radiator);

                Box(skin, "Lower Belly Panel Front", new Vector3(0f, .525f, -.96f), new Vector3(.73f, .045f, .38f), aluminum, false, new Vector3(-4f, 0f, 0f));
                Box(skin, "Lower Belly Panel Forward Mid", new Vector3(0f, .495f, -1.30f), new Vector3(.71f, .045f, .46f), aluminum, false, new Vector3(-1.5f, 0f, 0f));
                Box(skin, "Lower Belly Panel Aft Mid", new Vector3(0f, .485f, -1.68f), new Vector3(.69f, .045f, .45f), aluminum, false, new Vector3(1.8f, 0f, 0f));
                Box(skin, "Lower Belly Panel Aft", new Vector3(0f, .525f, -2.02f), new Vector3(.66f, .045f, .34f), aluminum, false, new Vector3(7f, 0f, 0f));
                Box(skin, "Lower Belly Dark Intake Floor", new Vector3(0f, .555f, -.88f), new Vector3(.64f, .018f, .24f), dark, false, new Vector3(-5f, 0f, 0f));
                Box(skin, "Lower Belly Left Blend", new Vector3(-.345f, .555f, -1.53f), new Vector3(.035f, .13f, 1.18f), aluminum, false, new Vector3(0f, 0f, -3f));
                Box(skin, "Lower Belly Right Blend", new Vector3(.345f, .555f, -1.53f), new Vector3(.035f, .13f, 1.18f), aluminum, false, new Vector3(0f, 0f, 3f));
                repaired++;
            }
            return repaired;
        }

        static Road ReadRoad(Transform roadTransform)
        {
            Transform surface = DirectChild(roadTransform, "Road Surface");
            if (!surface) return null;
            MeshFilter mf = surface.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return null;
            Vector3[] vertices = mf.sharedMesh.vertices;
            if (vertices.Length < 4) return null;
            Road road = new Road { name = roadTransform.name, root = roadTransform };
            for (int i = 0; i + 1 < vertices.Length; i += 2)
            {
                Vector3 a = mf.transform.TransformPoint(vertices[i]);
                Vector3 b = mf.transform.TransformPoint(vertices[i + 1]);
                road.path.Add((a + b) * .5f);
                if (i == 0) road.width = Vector3.Distance(a, b);
            }
            return road.path.Count >= 2 ? road : null;
        }

        static Transform LongestRoad(List<Transform> roads)
        {
            Transform best = null;
            float bestLength = -1f;
            foreach (Transform tr in roads)
            {
                Road road = ReadRoad(tr);
                if (road == null) continue;
                float len = PathLength(road.path);
                if (len > bestLength) { bestLength = len; best = tr; }
            }
            return best;
        }

        static List<Vector3> Resample(List<Vector3> source, float spacing)
        {
            List<Vector3> result = new List<Vector3>();
            if (source == null || source.Count == 0) return result;
            result.Add(source[0]);
            float carry = 0f;
            Vector3 from = source[0];
            for (int i = 1; i < source.Count; i++)
            {
                Vector3 to = source[i];
                float segment = Vector3.Distance(from, to);
                if (segment < .001f) { from = to; continue; }
                Vector3 direction = (to - from) / segment;
                while (carry + segment >= spacing)
                {
                    float need = spacing - carry;
                    from += direction * need;
                    result.Add(from);
                    segment -= need;
                    carry = 0f;
                    if (segment < .001f) break;
                    direction = (to - from).normalized;
                }
                carry += segment;
                from = to;
            }
            if (Vector3.Distance(result[result.Count - 1], source[source.Count - 1]) > spacing * .30f)
                result.Add(source[source.Count - 1]);
            return result;
        }

        static Vector3 PathTangent(List<Vector3> path, int index)
        {
            Vector3 tangent = index == 0 ? path[1] - path[0] : index == path.Count - 1 ? path[index] - path[index - 1] : path[index + 1] - path[index - 1];
            tangent.y = 0f;
            return tangent.sqrMagnitude > .001f ? tangent.normalized : Vector3.forward;
        }

        static float PathLength(List<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        static bool NearRoad(Vector3 p, List<Road> roads, float extra)
        {
            foreach (Road road in roads)
            {
                float limit = road.width * .5f + extra;
                for (int i = 0; i < road.path.Count - 1; i++)
                    if (SegmentDistance(p, road.path[i], road.path[i + 1]) < limit) return true;
            }
            return false;
        }

        static int CountTerrainTreeRoadIntrusions(Terrain terrain, List<Road> roads, float extra)
        {
            int bad = 0;
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            TreeInstance[] instances = data.treeInstances;
            for (int i = 0; i < instances.Length; i++)
            {
                Vector3 p = new Vector3(origin.x + instances[i].position.x * size.x, 0f, origin.z + instances[i].position.z * size.z);
                if (NearRoad(p, roads, extra)) bad++;
            }
            return bad;
        }

        static int CountTreePrototypeColliderFailures(Terrain terrain)
        {
            int bad = 0;
            TreePrototype[] prototypes = terrain.terrainData.treePrototypes;
            if (prototypes == null || prototypes.Length == 0) return 1;
            foreach (TreePrototype prototype in prototypes)
            {
                if (prototype == null || !prototype.prefab) { bad++; continue; }
                Collider collider = prototype.prefab.GetComponentInChildren<Collider>(true);
                if (!collider || !collider.enabled || collider.isTrigger) bad++;
            }
            return bad;
        }

        static int CountColliderFailures(Transform root, string prefix)
        {
            int bad = 0;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith(prefix)) continue;
                Collider collider = tr.GetComponentInChildren<Collider>(true);
                if (!collider || !collider.enabled || collider.isTrigger) bad++;
            }
            return bad;
        }

        static int CountNamedRoadIntrusions(Transform root, string prefix, List<Road> roads, float extra)
        {
            int bad = 0;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith(prefix) && NearRoad(tr.position, roads, extra)) bad++;
            return bad;
        }

        static int CountDenseGrassCells(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            if (data.detailPrototypes == null || data.detailPrototypes.Length == 0 || data.detailWidth <= 0 || data.detailHeight <= 0) return 0;
            int[,] layer = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0);
            int count = 0;
            for (int z = 0; z < layer.GetLength(0); z++)
                for (int x = 0; x < layer.GetLength(1); x++)
                    if (layer[z, x] > 0) count++;
            return count;
        }

        static void RasterizeExclusions(bool[,] blocked, Terrain terrain, List<Bounds> exclusions)
        {
            int height = blocked.GetLength(0);
            int width = blocked.GetLength(1);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            foreach (Bounds b in exclusions)
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - origin.x) / size.x * width), 0, width - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x - origin.x) / size.x * width), 0, width - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - origin.z) / size.z * height), 0, height - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z - origin.z) / size.z * height), 0, height - 1);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) blocked[z, x] = true;
            }
        }

        static void RasterizeRoads(bool[,] blocked, Terrain terrain, List<Road> roads, float extra)
        {
            int height = blocked.GetLength(0);
            int width = blocked.GetLength(1);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            foreach (Road road in roads)
            {
                float radius = road.width * .5f + extra;
                for (int i = 0; i < road.path.Count - 1; i++)
                {
                    Vector3 a = road.path[i];
                    Vector3 b = road.path[i + 1];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 7f));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                        int cx = Mathf.RoundToInt((p.x - origin.x) / size.x * (width - 1));
                        int cz = Mathf.RoundToInt((p.z - origin.z) / size.z * (height - 1));
                        int rx = Mathf.Max(1, Mathf.CeilToInt(radius / size.x * width));
                        int rz = Mathf.Max(1, Mathf.CeilToInt(radius / size.z * height));
                        for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(height - 1, cz + rz); z++)
                            for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(width - 1, cx + rx); x++)
                                blocked[z, x] = true;
                    }
                }
            }
        }

        static bool InsideAny(Vector3 p, List<Bounds> exclusions)
        {
            for (int i = 0; i < exclusions.Count; i++)
            {
                Bounds b = exclusions[i];
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) return true;
            }
            return false;
        }

        static bool TryBounds(Transform root, out Bounds bounds)
        {
            bool set = false;
            bounds = new Bounds(root.position, Vector3.zero);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!set) { bounds = renderer.bounds; set = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (!set) { bounds = collider.bounds; set = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            return set;
        }

        static void DisableAllColliders(Transform root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        static void EnsureSolidCollider(GameObject root)
        {
            Collider collider = root.GetComponentInChildren<Collider>(true);
            if (collider)
            {
                collider.enabled = true;
                collider.isTrigger = false;
                return;
            }
            MeshFilter mf = root.GetComponentInChildren<MeshFilter>(true);
            if (mf && mf.sharedMesh)
            {
                MeshCollider mc = root.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }
        }

        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collide, Vector3? localEuler = null)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = localScale;
            if (localEuler.HasValue) g.transform.localRotation = Quaternion.Euler(localEuler.Value);
            Renderer renderer = g.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider collider = g.GetComponent<Collider>();
            if (collider) collider.enabled = collide;
            g.isStatic = true;
            return g;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collide)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = localScale;
            Renderer renderer = g.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider collider = g.GetComponent<Collider>();
            if (collider) collider.enabled = collide;
            g.isStatic = true;
            return g;
        }

        static void Wire(Transform parent, Vector3 a, Vector3 b, Material material, string name, bool collide)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < .001f) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = (a + b) * .5f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.transform.localScale = new Vector3(.035f, d.magnitude * .5f, .035f);
            Renderer renderer = g.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider collider = g.GetComponent<Collider>();
            if (collider) collider.enabled = collide;
            g.isStatic = true;
        }

        static Vector3 RandomTerrainPoint(System.Random rng, Terrain terrain)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 p = new Vector3(origin.x + Next(rng, .008f, .992f) * size.x, 0f, origin.z + Next(rng, .008f, .992f) * size.z);
            p.y = Ground(terrain, p);
            return p;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 x = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            return d.sqrMagnitude < .001f ? Vector2.Distance(q, x) : Vector2.Distance(q, x + d * Mathf.Clamp01(Vector2.Dot(q - x, d) / d.sqrMagnitude));
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 1f;
            float sum = 0f;
            float frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                sum += amplitude;
                amplitude *= .5f;
                frequency *= 2.03f;
            }
            return sum > 0f ? value / sum : 0f;
        }

        static float Next(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static int StableSign(string value) => (value.GetHashCode() & 1) == 0 ? 1 : -1;
        static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        static Terrain FindTerrain()
        {
            GameObject g = Find(TerrainName);
            Terrain t = g ? (g.GetComponent<Terrain>() ?? g.GetComponentInChildren<Terrain>(true)) : null;
            if (t) return t;
            Terrain[] all = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        static GameObject Find(string name)
        {
            GameObject g = GameObject.Find(name);
            if (g) return g;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (!root) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static Transform DirectChild(Transform root, string name)
        {
            if (!root) return null;
            for (int i = 0; i < root.childCount; i++) if (root.GetChild(i).name == name) return root.GetChild(i);
            return null;
        }

        static Transform New(string name, Transform parent)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static int Count(Transform root, string namePart)
        {
            if (!root) return 0;
            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t != root && t.name.Contains(namePart)) count++;
            return count;
        }

        static void RemoveAllNamed(Transform root, string exactName)
        {
            if (!root) return;
            List<GameObject> kill = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == exactName) kill.Add(t.gameObject);
            for (int i = kill.Count - 1; i >= 0; i--) if (kill[i]) UnityEngine.Object.DestroyImmediate(kill[i]);
        }

        static void ResetFolder()
        {
            if (AssetDatabase.IsValidFolder(Gen)) AssetDatabase.DeleteAsset(Gen);
            Ensure(Gen);
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
    }
}
