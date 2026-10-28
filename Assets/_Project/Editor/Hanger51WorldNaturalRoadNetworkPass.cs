using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldNaturalRoadNetworkPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string RegionalPassName = "Hanger 51 Regional Infrastructure Pass";
        const string FinalizerName = "Hanger 51 Regional Infrastructure Finalizer";
        const string NaturalPassName = "Hanger 51 Natural Road Network Pass";
        const string BaseGen = "Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen = "Assets/_Project/Environment/Generated/CountrysideNaturalRoadNetwork";
        const string GrassA = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string GrassB = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string LeafTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string FirTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const int Seed = 51108;

        static int meshId;

        class Town
        {
            public Transform root;
            public string name;
            public Vector3 center;
            public float angle;
            public List<Transform> localRoads = new List<Transform>();
        }

        class NaturalRoad
        {
            public string name;
            public float width;
            public List<Vector3> path = new List<Vector3>();
            public Transform root;
        }

        struct RoadEnd
        {
            public Vector3 position;
            public Vector3 outward;
        }

        struct Mats
        {
            public Material asphalt;
            public Material gravel;
            public Material paint;
            public Material wood;
            public Material metal;
            public Material white;
            public Material red;
            public Material concrete;
        }

        [MenuItem("Hanger 51/World/Current/108 - Naturalize Roads Utilities And Scale")]
        public static void Build()
        {
            Hanger51WorldRegionalInfrastructureFinalizer.Build();

            GameObject world = Find(WorldName);
            GameObject regionalPass = Find(RegionalPassName);
            GameObject finalizer = Find(FinalizerName);
            Terrain terrain = FindTerrain();

            if (!world || !regionalPass || !finalizer || !terrain)
            {
                Debug.LogError("Step 108 could not find the Step 106 countryside.");
                return;
            }

            Transform roads = DirectChild(world.transform, "Road Network");
            Transform settlements = FindChild(world.transform, "Settlements");
            Transform plant = FindChild(regionalPass.transform, "Power Station Complex");

            if (!roads || !settlements || !plant)
            {
                Debug.LogError("Step 108 could not find roads, settlements, or the power station.", world);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Preparing natural road pass", .03f);

                GameObject old = Find(NaturalPassName);
                if (old) UnityEngine.Object.DestroyImmediate(old);

                ResetFolder();
                meshId = 0;
                Mats mats = LoadMats();
                Transform root = New(NaturalPassName, regionalPass.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Removing straight regional roads and route-dependent clutter", .12f);
                RemoveOldRegionalNetwork(roads);
                RemoveOldRegionalDetail(regionalPass.transform);
                RemoveOldTownUtilities(finalizer.transform);
                List<Town> towns = CollectTowns(settlements, roads);

                if (towns.Count < 4)
                {
                    Debug.LogError("Step 108 needs four towns with local roads.");
                    return;
                }

                Vector3 regionCenter = AverageTownCenter(towns);
                SortTownsAroundCenter(towns, regionCenter);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Building curved town-to-town road loop", .28f);
                Transform roadRoot = New("Natural Regional Road Network", roads);
                List<NaturalRoad> naturalRoads = BuildTownLoop(terrain, roadRoot, towns, regionCenter, mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Building curved industrial spur", .41f);
                NaturalRoad plantRoad = BuildPlantSpur(terrain, roadRoot, towns, plant, regionCenter, mats);
                if (plantRoad != null) naturalRoads.Add(plantRoad);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Clearing objects from travel lanes", .51f);
                int shifted = ShiftRoadIntrusions(world.transform, naturalRoads, terrain);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Building continuous inter-city utility lines", .63f);
                Transform utilities = New("Intercity Roadside Utilities", root);
                int poles = BuildIntercityUtilities(terrain, utilities, naturalRoads, mats);
                int townPoles = BuildTownUtilities(terrain, utilities, towns, mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Adding correctly scaled roadside furniture", .74f);
                Transform detail = New("Natural Roadside Detail", root);
                int furniture = AddRoadFurniture(terrain, detail, naturalRoads, mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Adding asset grass and tree buffers", .85f);
                Transform nature = New("Natural Road Vegetation", root);
                int grass = AddRoadsideGrass(terrain, nature, naturalRoads);
                int trees = AddRoadsideTrees(terrain, nature, naturalRoads, towns, plant.position);

                EditorUtility.DisplayProgressBar("Hanger 51 Natural Roads", "Final road/scale safety pass", .95f);
                ConformRoadsToTerrain(terrain, roadRoot);

                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = root.gameObject;

                Debug.Log(
                    $"Step 108 complete. curved regional roads={naturalRoads.Count}, inter-city poles={poles}, correctly scaled town poles={townPoles}, roadside furniture={furniture}, asset grass={grass}, roadside trees={trees}, objects shifted clear of lanes={shifted}.",
                    root.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/109 - Validate Natural Roads Utilities And Scale")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            GameObject root = Find(NaturalPassName);
            Terrain terrain = FindTerrain();

            if (!world || !root || !terrain)
            {
                Debug.LogError("Step 109 failed: run Step 108 first.");
                return;
            }

            Transform roads = DirectChild(world.transform, "Road Network");
            Transform naturalRoot = roads ? DirectChild(roads, "Natural Regional Road Network") : null;

            int roadCount = 0;
            int buried = 0;
            int offLand = 0;
            int roadVertices = 0;
            float totalTurn = 0f;
            Bounds land = TerrainBounds(terrain);

            if (naturalRoot)
            {
                for (int i = 0; i < naturalRoot.childCount; i++)
                {
                    Transform road = naturalRoot.GetChild(i);
                    List<Vector3> path = RoadPath(road);
                    if (path.Count < 3) continue;

                    roadCount++;
                    totalTurn += TotalHeadingChange(path);

                    Transform surface = FindChild(road, "Road Surface");
                    MeshFilter mf = surface ? surface.GetComponent<MeshFilter>() : null;
                    if (!mf || !mf.sharedMesh) continue;

                    foreach (Vector3 v in mf.sharedMesh.vertices)
                    {
                        Vector3 w = mf.transform.TransformPoint(v);
                        roadVertices++;
                        if (!InsideXZ(land, w, 2f)) offLand++;
                        if (w.y < Ground(terrain, w) + .065f) buried++;
                    }
                }
            }

            int poles = Count(root.transform, "Intercity Utility Pole");
            int townPoles = Count(root.transform, "Scaled Town Utility Pole");
            int wires = Count(root.transform, "Intercity Power Wire") + Count(root.transform, "Scaled Town Power Wire");
            int furniture = Count(root.transform, "Natural Roadside");
            int grass = Count(root.transform, "Natural Road Grass");
            int trees = Count(root.transform, "Natural Road Tree");
            int intrusions = CountRoadIntrusions(world.transform, naturalRoot);

            bool ok =
                roadCount >= 5 &&
                totalTurn >= 70f &&
                buried == 0 &&
                offLand == 0 &&
                poles >= 45 &&
                townPoles >= 24 &&
                wires >= 120 &&
                furniture >= 60 &&
                grass >= 600 &&
                trees >= 50 &&
                intrusions == 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 109 passed. curved roads={roadCount}, heading change={totalTurn:0}°, vertices={roadVertices}, buried={buried}, off-land={offLand}, inter-city poles={poles}, town poles={townPoles}, wires={wires}, roadside detail={furniture}, grass={grass}, trees={trees}, lane intrusions={intrusions}.",
                    root);
            }
            else
            {
                Debug.LogError(
                    $"Step 109 failed. curved roads={roadCount}, heading change={totalTurn:0}°, vertices={roadVertices}, buried={buried}, off-land={offLand}, inter-city poles={poles}, town poles={townPoles}, wires={wires}, roadside detail={furniture}, grass={grass}, trees={trees}, lane intrusions={intrusions}.",
                    root);
            }
        }

        static Mats LoadMats()
        {
            Mats m = new Mats();
            m.asphalt = LoadBase("Matte_Asphalt");
            m.gravel = LoadBase("Matte_Gravel");
            m.paint = LoadBase("Road_Paint");
            m.wood = LoadBase("Weathered_Wood");
            m.metal = LoadBase("Dark_Metal");
            m.white = LoadBase("Warm_White");
            m.red = LoadBase("Barn_Red");
            m.concrete = LoadBase("Concrete");
            return m;
        }

        static Material LoadBase(string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(BaseGen + "/Materials/" + name + ".mat");
            if (!m) Debug.LogWarning("Step 108 could not load material " + name + ".");
            return m;
        }

        static void RemoveOldRegionalNetwork(Transform roads)
        {
            List<GameObject> kill = new List<GameObject>();

            for (int i = 0; i < roads.childCount; i++)
            {
                Transform c = roads.GetChild(i);
                if (c.name == "Regional Road Network" ||
                    c.name == "Natural Regional Road Network" ||
                    c.name.StartsWith("Regional County Route") ||
                    c.name.StartsWith("Regional Industrial Access") ||
                    c.name == "Town Road Connections")
                {
                    kill.Add(c.gameObject);
                }
            }

            foreach (GameObject g in kill)
                if (g) UnityEngine.Object.DestroyImmediate(g);
        }

        static void RemoveOldRegionalDetail(Transform regionalPass)
        {
            string[] names =
            {
                "Regional Utilities",
                "Regional Nature Detail",
                "Regional Road Details"
            };

            foreach (string name in names)
            {
                Transform t = DirectChild(regionalPass, name);
                if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        static void RemoveOldTownUtilities(Transform finalizer)
        {
            Transform old = DirectChild(finalizer, "Town Street Distribution Utilities");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        static List<Town> CollectTowns(Transform settlements, Transform roads)
        {
            List<Town> towns = new List<Town>();

            for (int i = 0; i < settlements.childCount; i++)
            {
                Transform townRoot = settlements.GetChild(i);
                Town town = new Town();
                town.root = townRoot;
                town.name = townRoot.name;
                town.center = TownCenter(townRoot);

                for (int r = 0; r < roads.childCount; r++)
                {
                    Transform road = roads.GetChild(r);
                    if (road.name.StartsWith(town.name))
                        town.localRoads.Add(road);
                }

                if (town.localRoads.Count > 0)
                    towns.Add(town);
            }

            return towns;
        }

        static Vector3 AverageTownCenter(List<Town> towns)
        {
            Vector3 center = Vector3.zero;
            foreach (Town t in towns) center += t.center;
            return center / Mathf.Max(1, towns.Count);
        }

        static void SortTownsAroundCenter(List<Town> towns, Vector3 center)
        {
            foreach (Town t in towns)
                t.angle = Mathf.Atan2(t.center.z - center.z, t.center.x - center.x);

            towns.Sort((a, b) => a.angle.CompareTo(b.angle));
        }

        static List<NaturalRoad> BuildTownLoop(Terrain terrain, Transform roadRoot, List<Town> towns, Vector3 regionCenter, Mats mats)
        {
            List<NaturalRoad> roads = new List<NaturalRoad>();

            for (int i = 0; i < towns.Count; i++)
            {
                Town a = towns[i];
                Town b = towns[(i + 1) % towns.Count];

                RoadEnd start = TownRoadEndpointToward(a, b.center);
                RoadEnd end = TownRoadEndpointToward(b, a.center);
                List<Vector3> path = CurvedRoute(terrain, start.position, end.position, start.outward, end.outward, regionCenter, towns, 7.2f);

                NaturalRoad road = CreateRoad(
                    terrain,
                    roadRoot,
                    $"Natural County Road {i + 1} - {a.name} to {b.name}",
                    path,
                    7.2f,
                    mats);

                if (road != null) roads.Add(road);
            }

            return roads;
        }

        static NaturalRoad BuildPlantSpur(Terrain terrain, Transform roadRoot, List<Town> towns, Transform plant, Vector3 regionCenter, Mats mats)
        {
            Town nearest = towns[0];
            float best = float.MaxValue;

            foreach (Town town in towns)
            {
                float d = Planar(town.center, plant.position);
                if (d < best)
                {
                    best = d;
                    nearest = town;
                }
            }

            RoadEnd start = TownRoadEndpointToward(nearest, plant.position);
            Vector3 end = plant.position + plant.forward * 92f;
            Vector3 plantOutward = plant.forward;
            List<Vector3> path = CurvedRoute(terrain, start.position, end, start.outward, plantOutward, regionCenter, towns, 8f);

            return CreateRoad(
                terrain,
                roadRoot,
                "Natural Industrial Road - Power Station",
                path,
                8f,
                mats);
        }

        static List<Vector3> CurvedRoute(Terrain terrain, Vector3 a, Vector3 b, Vector3 startOutward, Vector3 endOutward, Vector3 regionCenter, List<Town> towns, float width)
        {
            Bounds land = TerrainBounds(terrain);
            a = ClampToLand(terrain, land, a, 85f);
            b = ClampToLand(terrain, land, b, 85f);

            Vector3 chord = b - a;
            chord.y = 0f;
            float len = Mathf.Max(1f, chord.magnitude);
            Vector3 dir = chord / len;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 mid = (a + b) * .5f;
            Vector3 outward = mid - regionCenter;
            outward.y = 0f;

            if (outward.sqrMagnitude < 1f)
                outward = side;

            float preferredSign = Vector3.Dot(side, outward.normalized) >= 0f ? 1f : -1f;
            float bend = Mathf.Clamp(len * .16f, 90f, 310f);

            List<Vector3> first = BuildBezierCandidate(terrain, land, a, b, startOutward, endOutward, side * preferredSign, bend, width);
            List<Vector3> second = BuildBezierCandidate(terrain, land, a, b, startOutward, endOutward, side * -preferredSign, bend, width);

            float firstScore = RouteScore(first, land, regionCenter, towns, a, b);
            float secondScore = RouteScore(second, land, regionCenter, towns, a, b);
            List<Vector3> best = firstScore >= secondScore ? first : second;

            if (best.Count < 2)
                best = BuildBezierCandidate(terrain, land, a, b, startOutward, endOutward, side * preferredSign, bend * .55f, width);

            return best;
        }

        static List<Vector3> BuildBezierCandidate(Terrain terrain, Bounds land, Vector3 a, Vector3 b, Vector3 startOutward, Vector3 endOutward, Vector3 bendDir, float bend, float width)
        {
            Vector3 d = b - a;
            d.y = 0f;
            float len = Mathf.Max(1f, d.magnitude);
            if (startOutward.sqrMagnitude < .001f) startOutward = d.normalized;
            if (endOutward.sqrMagnitude < .001f) endOutward = -d.normalized;
            startOutward.y = 0f;
            endOutward.y = 0f;
            startOutward.Normalize();
            endOutward.Normalize();

            float tangentLength = Mathf.Clamp(len * .20f, 65f, 190f);
            Vector3 c1 = a + startOutward * tangentLength + bendDir * bend * .58f;
            Vector3 c2 = b + endOutward * tangentLength + bendDir * bend * .58f;
            c1 = ClampToLand(terrain, land, c1, 80f);
            c2 = ClampToLand(terrain, land, c2, 80f);

            float approxLength = Planar(a, c1) + Planar(c1, c2) + Planar(c2, b);
            int count = Mathf.Clamp(Mathf.CeilToInt(approxLength / 7f), 24, 360);
            List<Vector3> path = new List<Vector3>();

            for (int i = 0; i <= count; i++)
            {
                float u = i / (float)count;
                Vector3 q = CubicBezier(a, c1, c2, b, u);

                float meander = Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Min(12f, approxLength * .006f);
                Vector3 tangent = CubicTangent(a, c1, c2, b, u);
                Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;
                q += normal * meander * Mathf.Sin(u * Mathf.PI);

                q = ClampToLand(terrain, land, q, 72f + width);
                q.y = Ground(terrain, q) + .04f;
                path.Add(q);
            }

            return path;
        }

        static float RouteScore(List<Vector3> path, Bounds land, Vector3 regionCenter, List<Town> towns, Vector3 start, Vector3 end)
        {
            if (path == null || path.Count < 2) return float.MinValue;

            float edge = float.MaxValue;
            float centerClearance = float.MaxValue;
            float otherTownClearance = float.MaxValue;

            for (int i = 0; i < path.Count; i += Mathf.Max(1, path.Count / 30))
            {
                Vector3 p = path[i];
                edge = Mathf.Min(edge, EdgeMargin(land, p));
                centerClearance = Mathf.Min(centerClearance, Planar(p, regionCenter));

                foreach (Town town in towns)
                {
                    if (Planar(town.center, start) < 250f || Planar(town.center, end) < 250f) continue;
                    otherTownClearance = Mathf.Min(otherTownClearance, Planar(p, town.center));
                }
            }

            if (otherTownClearance == float.MaxValue) otherTownClearance = 1000f;
            return edge * 1.8f + centerClearance * .25f + otherTownClearance * .35f;
        }

        static NaturalRoad CreateRoad(Terrain terrain, Transform parent, string name, List<Vector3> path, float width, Mats mats)
        {
            if (path == null || path.Count < 2) return null;

            Transform root = New(name, parent);
            New("Natural Road Marker", root);
            Ribbon(terrain, root, "Gravel Shoulder", path, width + 7.5f, mats.gravel, .04f, false);
            Ribbon(terrain, root, "Road Surface", path, width, mats.asphalt, .11f, true);
            Ribbon(terrain, root, "Center Line", path, .18f, mats.paint, .15f, false);

            NaturalRoad road = new NaturalRoad();
            road.name = name;
            road.width = width;
            road.path = path;
            road.root = root;
            return road;
        }

        static int ShiftRoadIntrusions(Transform world, List<NaturalRoad> roads, Terrain terrain)
        {
            string[] prefixes =
            {
                "Refinement Town Tree",
                "Town Tree",
                "Asset Grass",
                "Yard Shrub",
                "Parked Car",
                "Roadside Asset Grass",
                "Roadside Bus Stop",
                "Town Distribution Pole",
                "Road Utility Pole",
                "Street Lamp",
                "Fire Hydrant",
                "Town Gateway",
                "Roadside Detail Delineator"
            };

            List<Transform> candidates = new List<Transform>();

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                foreach (string prefix in prefixes)
                {
                    if (tr.name.StartsWith(prefix))
                    {
                        candidates.Add(tr);
                        break;
                    }
                }
            }

            int shifted = 0;

            foreach (Transform tr in candidates)
            {
                NaturalRoad nearestRoad;
                Vector3 nearest;
                Vector3 tangent;
                float distance;

                if (!NearestRoadInfo(tr.position, roads, out nearestRoad, out nearest, out tangent, out distance))
                    continue;

                float required = nearestRoad.width * .5f + 4.2f;
                if (distance >= required) continue;

                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                float sign = Vector3.Dot(tr.position - nearest, side) >= 0f ? 1f : -1f;
                Vector3 target = nearest + side * sign * (required + 2.0f);
                target.y = Ground(terrain, target);
                tr.position = target;
                shifted++;
            }

            return shifted;
        }

        static int BuildIntercityUtilities(Terrain terrain, Transform root, List<NaturalRoad> roads, Mats mats)
        {
            int poleCount = 0;

            for (int r = 0; r < roads.Count; r++)
            {
                NaturalRoad road = roads[r];
                List<Vector3> points = Resample(road.path, 52f);
                Vector3[] previous = new Vector3[3];
                bool havePrevious = false;
                int sideSign = r % 2 == 0 ? 1 : -1;

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 position = points[i] + side * sideSign * (road.width * .5f + 9.5f);
                    position.y = Ground(terrain, position);

                    Transform pole = New("Intercity Utility Pole " + (++poleCount).ToString("000"), root);
                    pole.position = position;
                    pole.rotation = Quaternion.LookRotation(tangent, Vector3.up);

                    Cylinder(pole, "Pole Shaft", new Vector3(0, 4.25f, 0), new Vector3(.13f, 4.25f, .13f), mats.wood, false);
                    Box(pole, "Crossarm", new Vector3(0, 7.95f, 0), new Vector3(2.35f, .14f, .14f), mats.wood, false);

                    for (int k = -1; k <= 1; k++)
                    {
                        Cylinder(pole, "Insulator", new Vector3(k * .74f, 8.17f, 0), new Vector3(.055f, .16f, .055f), mats.white, false);
                        Vector3 conductor = pole.TransformPoint(new Vector3(k * .74f, 8.38f, 0));

                        if (havePrevious)
                            Wire(root, previous[k + 1], conductor, mats.metal, "Intercity Power Wire");

                        previous[k + 1] = conductor;
                    }

                    havePrevious = true;
                }
            }

            return poleCount;
        }

        static int BuildTownUtilities(Terrain terrain, Transform root, List<Town> towns, Mats mats)
        {
            Transform townRoot = New("Correctly Scaled Town Utilities", root);
            int poleCount = 0;

            foreach (Town town in towns)
            {
                Transform road = null;

                foreach (Transform candidate in town.localRoads)
                {
                    if (candidate.name.Contains("Avenue 2") || candidate.name.Contains("Street 2"))
                    {
                        road = candidate;
                        break;
                    }

                    if (!road) road = candidate;
                }

                if (!road) continue;

                List<Vector3> path = Resample(RoadPath(road), 44f);
                if (path.Count < 2) continue;

                Vector3[] previous = new Vector3[3];
                bool havePrevious = false;

                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 tangent = Tangent(path, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 position = path[i] + side * 8.2f;
                    position.y = Ground(terrain, position);

                    Transform pole = New("Scaled Town Utility Pole " + (++poleCount).ToString("000"), townRoot);
                    pole.position = position;
                    pole.rotation = Quaternion.LookRotation(tangent, Vector3.up);

                    Cylinder(pole, "Pole Shaft", new Vector3(0, 4.05f, 0), new Vector3(.12f, 4.05f, .12f), mats.wood, false);
                    Box(pole, "Crossarm", new Vector3(0, 7.55f, 0), new Vector3(2.15f, .13f, .13f), mats.wood, false);

                    for (int k = -1; k <= 1; k++)
                    {
                        Cylinder(pole, "Insulator", new Vector3(k * .68f, 7.76f, 0), new Vector3(.05f, .15f, .05f), mats.white, false);
                        Vector3 conductor = pole.TransformPoint(new Vector3(k * .68f, 7.96f, 0));

                        if (havePrevious)
                            Wire(townRoot, previous[k + 1], conductor, mats.metal, "Scaled Town Power Wire");

                        previous[k + 1] = conductor;
                    }

                    havePrevious = true;
                }
            }

            return poleCount;
        }

        static int AddRoadFurniture(Terrain terrain, Transform root, List<NaturalRoad> roads, Mats mats)
        {
            int made = 0;

            foreach (NaturalRoad road in roads)
            {
                List<Vector3> points = Resample(road.path, 72f);

                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;

                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 position = points[i] + side * s * (road.width * .5f + 3.8f);
                        position.y = Ground(terrain, position);

                        Transform marker = New("Natural Roadside Delineator " + (++made).ToString("000"), root);
                        marker.position = position;
                        marker.rotation = Quaternion.LookRotation(tangent, Vector3.up);

                        Box(marker, "Post", new Vector3(0, .52f, 0), new Vector3(.10f, 1.04f, .10f), mats.white, false);
                        Box(marker, "Reflector", new Vector3(0, .91f, -.065f), new Vector3(.16f, .18f, .035f), s < 0 ? mats.red : mats.white, false);
                    }
                }
            }

            return made;
        }

        static int AddRoadsideGrass(Terrain terrain, Transform root, List<NaturalRoad> roads)
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(GrassA);
            GameObject b = AssetDatabase.LoadAssetAtPath<GameObject>(GrassB);
            if (!a && !b) return 0;

            System.Random rng = new System.Random(Seed + 1);
            int made = 0;

            foreach (NaturalRoad road in roads)
            {
                List<Vector3> points = Resample(road.path, 12f);

                for (int i = 0; i < points.Count && made < 1200; i++)
                {
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;

                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (rng.NextDouble() < .35) continue;

                        float offset = road.width * .5f + Next(rng, 6f, 14f);
                        Vector3 p = points[i] + side * s * offset + tangent * Next(rng, -3f, 3f);
                        p.y = Ground(terrain, p);

                        GameObject src = rng.NextDouble() < .5 ? a : b;
                        if (!src) src = a ? a : b;
                        SpawnPrefab(src, root, p, rng, "Natural Road Grass ", ref made, .65f, 1.25f);
                    }
                }
            }

            return made;
        }

        static int AddRoadsideTrees(Terrain terrain, Transform root, List<NaturalRoad> roads, List<Town> towns, Vector3 plant)
        {
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafTree);
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirTree);
            if (!leaf && !fir) return 0;

            System.Random rng = new System.Random(Seed + 2);
            int made = 0;

            foreach (NaturalRoad road in roads)
            {
                List<Vector3> points = Resample(road.path, 68f);

                for (int i = 1; i < points.Count - 1 && made < 180; i++)
                {
                    if (rng.NextDouble() < .48) continue;

                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    int s = rng.NextDouble() < .5 ? -1 : 1;
                    Vector3 p = points[i] + side * s * Next(rng, 22f, 44f) + tangent * Next(rng, -8f, 8f);

                    if (Planar(p, plant) < 150f) continue;

                    bool nearTown = false;
                    foreach (Town town in towns)
                    {
                        if (Planar(p, town.center) < 120f)
                        {
                            nearTown = true;
                            break;
                        }
                    }

                    if (nearTown) continue;

                    p.y = Ground(terrain, p);
                    GameObject src = rng.NextDouble() < .78 ? leaf : fir;
                    if (!src) src = leaf ? leaf : fir;
                    SpawnPrefab(src, root, p, rng, "Natural Road Tree ", ref made, .72f, 1.05f);
                }
            }

            return made;
        }

        static int CountRoadIntrusions(Transform world, Transform naturalRoot)
        {
            if (!naturalRoot) return 999;

            List<NaturalRoad> roads = new List<NaturalRoad>();

            for (int i = 0; i < naturalRoot.childCount; i++)
            {
                Transform root = naturalRoot.GetChild(i);
                List<Vector3> path = RoadPath(root);
                if (path.Count < 2) continue;

                NaturalRoad r = new NaturalRoad();
                r.root = root;
                r.path = path;
                r.width = RoadWidth(root);
                roads.Add(r);
            }

            string[] prefixes =
            {
                "Refinement Town Tree",
                "Town Tree",
                "Yard Shrub",
                "Parked Car",
                "Roadside Bus Stop",
                "Town Distribution Pole",
                "Street Lamp",
                "Fire Hydrant"
            };

            int bad = 0;

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                bool candidate = false;
                foreach (string prefix in prefixes)
                {
                    if (tr.name.StartsWith(prefix))
                    {
                        candidate = true;
                        break;
                    }
                }

                if (!candidate) continue;

                NaturalRoad road;
                Vector3 nearest;
                Vector3 tangent;
                float distance;

                if (NearestRoadInfo(tr.position, roads, out road, out nearest, out tangent, out distance) &&
                    distance < road.width * .5f + 1.0f)
                {
                    bad++;
                }
            }

            return bad;
        }

        static bool NearestRoadInfo(Vector3 p, List<NaturalRoad> roads, out NaturalRoad road, out Vector3 nearest, out Vector3 tangent, out float distance)
        {
            road = null;
            nearest = p;
            tangent = Vector3.forward;
            distance = float.MaxValue;

            foreach (NaturalRoad r in roads)
            {
                for (int i = 0; i < r.path.Count - 1; i++)
                {
                    Vector3 q = ClosestXZ(p, r.path[i], r.path[i + 1]);
                    float d = Planar(p, q);

                    if (d < distance)
                    {
                        distance = d;
                        road = r;
                        nearest = q;
                        Vector3 t = r.path[i + 1] - r.path[i];
                        t.y = 0f;
                        tangent = t.sqrMagnitude < .001f ? Vector3.forward : t.normalized;
                    }
                }
            }

            return road != null;
        }

        static float TotalHeadingChange(List<Vector3> path)
        {
            float total = 0f;

            for (int i = 1; i < path.Count - 1; i += 4)
            {
                Vector3 a = path[i] - path[Mathf.Max(0, i - 1)];
                Vector3 b = path[Mathf.Min(path.Count - 1, i + 1)] - path[i];
                a.y = 0f;
                b.y = 0f;

                if (a.sqrMagnitude > .001f && b.sqrMagnitude > .001f)
                    total += Vector3.Angle(a, b);
            }

            return total;
        }

        static RoadEnd TownRoadEndpointToward(Town town, Vector3 target)
        {
            RoadEnd best = new RoadEnd { position = town.center, outward = (target - town.center).normalized };
            float bestDistance = float.MaxValue;

            foreach (Transform road in town.localRoads)
            {
                List<Vector3> path = RoadPath(road);
                if (path.Count < 2) continue;

                float d0 = Planar(path[0], target);
                if (d0 < bestDistance)
                {
                    bestDistance = d0;
                    Vector3 outward = path[0] - path[1];
                    outward.y = 0f;
                    best.position = path[0];
                    best.outward = outward.sqrMagnitude < .001f ? (target - town.center).normalized : outward.normalized;
                }

                int last = path.Count - 1;
                float d1 = Planar(path[last], target);
                if (d1 < bestDistance)
                {
                    bestDistance = d1;
                    Vector3 outward = path[last] - path[last - 1];
                    outward.y = 0f;
                    best.position = path[last];
                    best.outward = outward.sqrMagnitude < .001f ? (target - town.center).normalized : outward.normalized;
                }
            }

            return best;
        }

        static List<Vector3> RoadPath(Transform road)
        {
            Transform surface = FindChild(road, "Road Surface");
            if (!surface) return new List<Vector3>();

            MeshFilter mf = surface.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return new List<Vector3>();

            Vector3[] vertices = mf.sharedMesh.vertices;
            List<Vector3> path = new List<Vector3>();

            for (int i = 0; i + 1 < vertices.Length; i += 2)
                path.Add((surface.TransformPoint(vertices[i]) + surface.TransformPoint(vertices[i + 1])) * .5f);

            return path;
        }

        static float RoadWidth(Transform road)
        {
            Transform surface = FindChild(road, "Road Surface");
            if (!surface) return 7f;

            MeshFilter mf = surface.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh || mf.sharedMesh.vertexCount < 2) return 7f;

            Vector3[] v = mf.sharedMesh.vertices;
            return Vector3.Distance(surface.TransformPoint(v[0]), surface.TransformPoint(v[1]));
        }

        static void Ribbon(Terrain terrain, Transform parent, string name, List<Vector3> path, float width, Material material, float yOffset, bool collider)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);

            Mesh mesh = RibbonMesh(terrain, g.transform, path, width, yOffset, name);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = material;

            if (collider)
                g.AddComponent<MeshCollider>().sharedMesh = mesh;

            g.isStatic = true;
        }

        static Mesh RibbonMesh(Terrain terrain, Transform holder, List<Vector3> path, float width, float yOffset, string name)
        {
            int count = path.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[Mathf.Max(0, (count - 1) * 6)];
            float traveled = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = Tangent(path, i);
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * width * .5f;

                Vector3 left = path[i] - side;
                Vector3 right = path[i] + side;
                left.y = Ground(terrain, left) + yOffset;
                right.y = Ground(terrain, right) + yOffset;

                if (i > 0) traveled += Planar(path[i - 1], path[i]);

                vertices[i * 2] = holder.InverseTransformPoint(left);
                vertices[i * 2 + 1] = holder.InverseTransformPoint(right);
                uv[i * 2] = new Vector2(0f, traveled / 7f);
                uv[i * 2 + 1] = new Vector2(1f, traveled / 7f);

                if (i < count - 1)
                {
                    int q = i * 6;
                    int j = i * 2;
                    triangles[q] = j;
                    triangles[q + 1] = j + 2;
                    triangles[q + 2] = j + 1;
                    triangles[q + 3] = j + 1;
                    triangles[q + 4] = j + 2;
                    triangles[q + 5] = j + 3;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "H51_108_" + Safe(name) + "_" + (meshId++).ToString("0000");
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, Gen + "/Meshes/" + mesh.name + ".asset");
            return mesh;
        }

        static void ConformRoadsToTerrain(Terrain terrain, Transform roadRoot)
        {
            foreach (MeshFilter mf in roadRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf || !mf.sharedMesh) continue;

                string name = mf.gameObject.name;
                if (name != "Road Surface" && name != "Gravel Shoulder" && name != "Center Line") continue;

                float offset = name == "Center Line" ? .15f : name == "Road Surface" ? .11f : .04f;
                Mesh mesh = mf.sharedMesh;
                Vector3[] vertices = mesh.vertices;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 w = mf.transform.TransformPoint(vertices[i]);
                    w.y = Ground(terrain, w) + offset;
                    vertices[i] = mf.transform.InverseTransformPoint(w);
                }

                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);

                MeshCollider mc = mf.GetComponent<MeshCollider>();
                if (mc)
                {
                    mc.sharedMesh = null;
                    mc.sharedMesh = mesh;
                }
            }
        }

        static void SpawnPrefab(GameObject src, Transform parent, Vector3 position, System.Random rng, string prefix, ref int count, float minScale, float maxScale)
        {
            if (!src) return;

            GameObject g = PrefabUtility.InstantiatePrefab(src) as GameObject;
            if (!g) return;

            g.name = prefix + (++count).ToString("0000");
            g.transform.SetParent(parent, false);
            g.transform.position = position;
            g.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
            g.transform.localScale = Vector3.one * Next(rng, minScale, maxScale);

            foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);
        }

        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = localScale;
            if (material) g.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
            return g;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = localScale;
            if (material) g.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
            return g;
        }

        static void Wire(Transform parent, Vector3 a, Vector3 b, Material material, string name)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < .01f) return;

            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = (a + b) * .5f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.transform.localScale = new Vector3(.018f, d.magnitude * .5f, .018f);
            if (material) g.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
        }

        static List<Vector3> Resample(List<Vector3> path, float step)
        {
            List<Vector3> result = new List<Vector3>();
            if (path == null || path.Count == 0) return result;

            result.Add(path[0]);

            for (int i = 0; i < path.Count - 1; i++)
            {
                float d = Planar(path[i], path[i + 1]);
                int n = Mathf.Max(1, Mathf.CeilToInt(d / step));

                for (int k = 1; k <= n; k++)
                    result.Add(Vector3.Lerp(path[i], path[i + 1], k / (float)n));
            }

            return result;
        }

        static Vector3 Tangent(List<Vector3> path, int i)
        {
            if (path == null || path.Count < 2) return Vector3.forward;

            Vector3 d =
                i == 0 ? path[1] - path[0] :
                i == path.Count - 1 ? path[path.Count - 1] - path[path.Count - 2] :
                path[i + 1] - path[i - 1];

            d.y = 0f;
            return d.sqrMagnitude < .001f ? Vector3.forward : d.normalized;
        }

        static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 CubicTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            Vector3 v = 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
            v.y = 0f;
            return v.sqrMagnitude < .001f ? Vector3.forward : v.normalized;
        }

        static Vector3 ClosestXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 x = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            float u = d.sqrMagnitude < .001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - x, d) / d.sqrMagnitude);
            return Vector3.Lerp(a, b, u);
        }

        static Bounds TerrainBounds(Terrain terrain)
        {
            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            return new Bounds(o + s * .5f, s);
        }

        static Vector3 ClampToLand(Terrain terrain, Bounds land, Vector3 p, float margin)
        {
            p.x = Mathf.Clamp(p.x, land.min.x + margin, land.max.x - margin);
            p.z = Mathf.Clamp(p.z, land.min.z + margin, land.max.z - margin);
            p.y = Ground(terrain, p);
            return p;
        }

        static bool InsideXZ(Bounds land, Vector3 p, float margin)
        {
            return p.x >= land.min.x + margin &&
                   p.x <= land.max.x - margin &&
                   p.z >= land.min.z + margin &&
                   p.z <= land.max.z - margin;
        }

        static float EdgeMargin(Bounds land, Vector3 p)
        {
            return Mathf.Min(
                p.x - land.min.x,
                land.max.x - p.x,
                p.z - land.min.z,
                land.max.z - p.z);
        }

        static Vector3 TownCenter(Transform town)
        {
            List<Transform> buildings = new List<Transform>();

            for (int i = 0; i < town.childCount; i++)
            {
                Transform c = town.GetChild(i);
                if (c.name.StartsWith("Detailed House") || c.name.StartsWith("Building"))
                    buildings.Add(c);
            }

            if (buildings.Count == 0) return town.position;

            Vector3 center = Vector3.zero;
            foreach (Transform b in buildings) center += b.position;
            return center / buildings.Count;
        }

        static float Ground(Terrain terrain, Vector3 p)
        {
            return terrain.SampleHeight(p) + terrain.transform.position.y;
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        static float Next(System.Random rng, float a, float b)
        {
            return a + (float)rng.NextDouble() * (b - a);
        }

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
                if (t && t.name == name && t.gameObject.scene.IsValid())
                    return t.gameObject;

            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;

            return null;
        }

        static Transform DirectChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == name)
                    return root.GetChild(i);

            return null;
        }

        static Transform New(string name, Transform parent)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static int Count(Transform root, string text)
        {
            if (!root) return 0;

            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.Contains(text))
                    count++;

            return count;
        }

        static string Safe(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Replace(' ', '_');
        }

        static void ResetFolder()
        {
            if (AssetDatabase.IsValidFolder(Gen))
                AssetDatabase.DeleteAsset(Gen);

            Ensure(Gen + "/Meshes");
        }

        static void Ensure(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
