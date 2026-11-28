using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldRegionalForestAndTownSetbackPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string AirportName = "Hanger 51 Airport Complex";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string RegionalPassName = "Hanger 51 Regional Infrastructure Pass";
        const string PassName = "Hanger 51 Regional Forest And Town Setback Pass";
        const string BaseGen = "Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen = "Assets/_Project/Environment/Generated/CountrysideRegionalForestSetback";
        const string LeafTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string FirTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string GrassA = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string GrassB = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const int Seed = 51112;

        static int meshId;

        class Town
        {
            public Transform root;
            public string name;
            public Vector3 oldCenter;
            public Vector3 center;
            public Vector3 delta;
            public float radius;
            public List<Transform> localRoads = new List<Transform>();
            public List<Transform> houses = new List<Transform>();
        }

        class Road
        {
            public string name;
            public float width;
            public Transform root;
            public List<Vector3> path = new List<Vector3>();
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
        }

        [MenuItem("Hanger 51/World/Current/112 - Expand Forest Push Towns And Smooth Entries")]
        public static void Build()
        {
            Hanger51WorldScaleMatteIndustrialCleanup.Build();

            GameObject world = Find(WorldName);
            GameObject airport = Find(AirportName);
            GameObject regionalPass = Find(RegionalPassName);
            Terrain terrain = FindTerrain();

            if (!world || !airport || !regionalPass || !terrain)
            {
                Debug.LogError("Step 112 could not find the Step 110 countryside, airport, regional pass, or terrain.");
                return;
            }

            Transform settlements = FindChild(world.transform, "Settlements");
            Transform roadsRoot = DirectChild(world.transform, "Road Network");
            Transform plant = FindChild(regionalPass.transform, "Power Station Complex");

            if (!settlements || settlements.childCount < 4 || !roadsRoot || !plant)
            {
                Debug.LogError("Step 112 could not find the four towns, road network, or power station.", world);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Preparing final regional expansion", .03f);

                GameObject oldPass = Find(PassName);
                if (oldPass) UnityEngine.Object.DestroyImmediate(oldPass);

                ResetFolder();
                meshId = 0;
                Mats mats = LoadMats();
                Transform pass = New(PassName, world.transform);

                Bounds airportSafety = AirportSafetyBounds(airport);
                Vector3 airportCenter = airportSafety.center;
                airportCenter.y = Ground(terrain, airportCenter);
                float airportRadius = Mathf.Max(
                    3000f,
                    Mathf.Sqrt(airportSafety.extents.x * airportSafety.extents.x + airportSafety.extents.z * airportSafety.extents.z) + 900f);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Capturing towns and clearing obsolete regional roads", .10f);
                List<Town> towns = CaptureTowns(settlements, roadsRoot);
                RemoveExternalRoads(roadsRoot, towns);
                RemoveLegacyRoadsideGroups(world.transform);
                DestroyLegacySceneTrees(world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Moving towns much farther from the airport", .20f);
                float nearestTown = RelocateTowns(terrain, world.transform, roadsRoot, towns, airportCenter, airportRadius);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Reforming local streets on the new terrain", .31f);
                RefreshTownRoadLists(roadsRoot, towns);
                foreach (Town town in towns)
                    foreach (Transform road in town.localRoads)
                        ConformRoad(terrain, road);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Building smooth town-to-town road entries", .43f);
                Transform regionalRoadRoot = New("Natural Regional Road Network", roadsRoot);
                List<Road> regionalRoads = BuildRegionalRing(terrain, regionalRoadRoot, towns, airportCenter, airportRadius, plant, mats);
                Road plantAccess = BuildPlantAccess(terrain, regionalRoadRoot, towns, regionalRoads, plant, mats);
                if (plantAccess != null) regionalRoads.Add(plantAccess);

                List<Road> allRoads = CollectLocalAndRegionalRoads(towns, regionalRoads);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Rebuilding continuous regional power lines", .56f);
                Transform utilities = New("Final Regional And Town Utilities", pass);
                int regionalPoles = BuildRegionalUtilities(terrain, utilities, regionalRoads, plant, mats);
                int townPoles = BuildTownUtilities(terrain, utilities, towns, mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Repopulating the entire countryside with mature forest", .68f);
                int terrainTrees = BuildRegionalTerrainForest(terrain, towns, allRoads, airportSafety, plant);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Adding settlement trees and asset grass", .80f);
                Transform vegetation = New("Full Regional Vegetation", pass);
                int settlementTrees = AddSettlementTrees(terrain, vegetation, towns, allRoads);
                int grass = AddRegionalAssetGrass(terrain, vegetation, towns, allRoads, airportSafety, plant);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Expansion", "Removing any last tree-road conflicts", .91f);
                int removedTrees = ClearSceneTreesFromRoads(world.transform, allRoads);
                ConformRoads(terrain, roadsRoot);

                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = pass.gameObject;

                Debug.Log(
                    $"Step 112 complete. nearest town to airport={nearestTown:0}m, regional roads={regionalRoads.Count}, regional utility poles={regionalPoles}, town utility poles={townPoles}, terrain trees={terrainTrees}, settlement trees={settlementTrees}, asset grass={grass}, tree-road conflicts removed={removedTrees}. Run Step 113 to validate.",
                    pass.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/113 - Validate Forest Town Setback And Entries")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            GameObject airport = Find(AirportName);
            GameObject pass = Find(PassName);
            Terrain terrain = FindTerrain();

            if (!world || !airport || !pass || !terrain)
            {
                Debug.LogError("Step 113 failed: run Step 112 first.");
                return;
            }

            Transform settlements = FindChild(world.transform, "Settlements");
            Transform roadsRoot = DirectChild(world.transform, "Road Network");
            Transform regionalRoot = roadsRoot ? DirectChild(roadsRoot, "Natural Regional Road Network") : null;
            if (!settlements || !roadsRoot || !regionalRoot)
            {
                Debug.LogError("Step 113 failed: required town or road roots are missing.", pass);
                return;
            }

            Bounds safety = AirportSafetyBounds(airport);
            Vector3 airportCenter = safety.center;
            float safeRadius = Mathf.Max(
                3000f,
                Mathf.Sqrt(safety.extents.x * safety.extents.x + safety.extents.z * safety.extents.z) + 900f);

            List<Town> towns = CaptureTowns(settlements, roadsRoot);
            float nearestTown = float.MaxValue;
            foreach (Town town in towns)
                nearestTown = Mathf.Min(nearestTown, Planar(town.center, airportCenter));

            List<Road> roads = new List<Road>();
            foreach (Town town in towns)
            {
                foreach (Transform tr in town.localRoads)
                {
                    List<Vector3> path = RoadPath(tr);
                    if (path.Count < 2) continue;
                    roads.Add(new Road { name = tr.name, root = tr, width = RoadWidth(tr), path = path });
                }
            }
            for (int i = 0; i < regionalRoot.childCount; i++)
            {
                Transform tr = regionalRoot.GetChild(i);
                List<Vector3> path = RoadPath(tr);
                if (path.Count < 2) continue;
                roads.Add(new Road { name = tr.name, root = tr, width = RoadWidth(tr), path = path });
            }

            int sceneTreeIntrusions = CountSceneTreeRoadIntrusions(world.transform, roads);
            int sharpEntries = CountSharpRegionalEntries(regionalRoot, towns);
            int regionalRoads = regionalRoot.childCount;
            int plantRoads = CountDirectRoadsContaining(regionalRoot, "Industrial Access");
            int utilityPoles = Count(pass.transform, "Regional Utility Pole") + Count(pass.transform, "Town Utility Pole");
            int wires = Count(pass.transform, "Regional Utility Wire") + Count(pass.transform, "Town Utility Wire");
            int settlementTrees = Count(pass.transform, "Settlement Tree");
            int grass = Count(pass.transform, "Regional Asset Grass");
            int terrainTrees = terrain.terrainData.treeInstanceCount;

            bool ok =
                towns.Count >= 4 &&
                nearestTown >= safeRadius + 3900f &&
                regionalRoads >= 5 &&
                plantRoads == 1 &&
                sharpEntries == 0 &&
                sceneTreeIntrusions == 0 &&
                terrainTrees >= 15000 &&
                settlementTrees >= 45 &&
                grass >= 650 &&
                utilityPoles >= 75 &&
                wires >= 150;

            if (ok)
            {
                Debug.Log(
                    $"Step 113 passed. towns={towns.Count}, nearest town={nearestTown:0}m (airport exclusion={safeRadius:0}m), regional roads={regionalRoads}, sharp town entries={sharpEntries}, tree-road intrusions={sceneTreeIntrusions}, terrain trees={terrainTrees}, settlement trees={settlementTrees}, asset grass={grass}, utility poles={utilityPoles}, wires={wires}.",
                    pass);
            }
            else
            {
                Debug.LogError(
                    $"Step 113 failed. towns={towns.Count}, nearest town={nearestTown:0}m (need >= {safeRadius + 3900f:0}m), regional roads={regionalRoads}, plant access roads={plantRoads}, sharp town entries={sharpEntries}, tree-road intrusions={sceneTreeIntrusions}, terrain trees={terrainTrees}, settlement trees={settlementTrees}, asset grass={grass}, utility poles={utilityPoles}, wires={wires}.",
                    pass);
            }
        }

        static Mats LoadMats()
        {
            Mats m = new Mats();
            m.asphalt = LoadMat("Matte_Asphalt");
            m.gravel = LoadMat("Matte_Gravel");
            m.paint = LoadMat("Road_Paint");
            m.wood = LoadMat("Weathered_Wood");
            m.metal = LoadMat("Dark_Metal");
            m.white = LoadMat("Warm_White");
            return m;
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
            else Debug.LogWarning("Step 112 could not load material " + name + ".");
            return m;
        }

        static List<Town> CaptureTowns(Transform settlements, Transform roadsRoot)
        {
            List<Town> towns = new List<Town>();
            for (int i = 0; i < settlements.childCount; i++)
            {
                Transform root = settlements.GetChild(i);
                List<Transform> houses = Houses(root);
                if (houses.Count == 0) continue;

                Town town = new Town();
                town.root = root;
                town.name = root.name;
                town.houses.AddRange(houses);
                town.oldCenter = Average(houses);
                town.center = town.oldCenter;
                town.radius = TownRadius(houses, town.center);

                for (int r = 0; r < roadsRoot.childCount; r++)
                {
                    Transform road = roadsRoot.GetChild(r);
                    if (road.name.StartsWith(town.name)) town.localRoads.Add(road);
                }
                towns.Add(town);
            }
            return towns;
        }

        static void RemoveExternalRoads(Transform roadsRoot, List<Town> towns)
        {
            List<GameObject> kill = new List<GameObject>();
            for (int i = 0; i < roadsRoot.childCount; i++)
            {
                Transform road = roadsRoot.GetChild(i);
                bool local = false;
                foreach (Town town in towns)
                {
                    if (road.name.StartsWith(town.name))
                    {
                        local = true;
                        break;
                    }
                }
                if (!local) kill.Add(road.gameObject);
            }
            foreach (GameObject g in kill) if (g) UnityEngine.Object.DestroyImmediate(g);
        }

        static void RemoveLegacyRoadsideGroups(Transform world)
        {
            string[] names =
            {
                "Final Scaled Roadside Utilities",
                "Final Roadside Detail",
                "Final Roadside Vegetation",
                "Correctly Scaled Town Utilities",
                "Intercity Roadside Utilities",
                "Natural Roadside Detail",
                "Natural Road Vegetation",
                "Town Street Distribution Utilities",
                "Asset Vegetation"
            };
            foreach (string name in names) RemoveAllNamed(world, name);
        }

        static void DestroyLegacySceneTrees(Transform world)
        {
            string[] prefixes = { "Town Tree", "Refinement Town Tree", "Natural Road Tree", "Final Mature Tree" };
            List<GameObject> kill = new List<GameObject>();
            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                bool match = false;
                foreach (string prefix in prefixes)
                    if (tr.name.StartsWith(prefix)) { match = true; break; }
                if (!match) continue;

                bool ancestorMatch = false;
                Transform p = tr.parent;
                while (p && p != world)
                {
                    foreach (string prefix in prefixes)
                        if (p.name.StartsWith(prefix)) { ancestorMatch = true; break; }
                    if (ancestorMatch) break;
                    p = p.parent;
                }
                if (!ancestorMatch) kill.Add(tr.gameObject);
            }
            foreach (GameObject g in kill) if (g) UnityEngine.Object.DestroyImmediate(g);
        }

        static float RelocateTowns(Terrain terrain, Transform world, Transform roadsRoot, List<Town> towns, Vector3 airportCenter, float safeRadius)
        {
            float nearest = float.MaxValue;
            Bounds land = TerrainBounds(terrain);
            HashSet<Transform> alreadyShifted = new HashSet<Transform>();

            for (int i = 0; i < towns.Count; i++)
            {
                Town town = towns[i];
                Vector3 newCenter = FindFarTownPosition(terrain, land, airportCenter, town.oldCenter, safeRadius, i);
                town.delta = newCenter - town.oldCenter;
                town.delta.y = 0f;
                town.center = newCenter;

                town.root.position += town.delta;
                foreach (Transform house in town.houses)
                {
                    Vector3 p = house.position;
                    p.y = Ground(terrain, p);
                    house.position = p;
                }

                foreach (Transform road in town.localRoads)
                    road.position += town.delta;

                ShiftTownNamedRoots(world, roadsRoot, town, alreadyShifted, terrain);
                ShiftNearbyTownObjects(world, roadsRoot, town, alreadyShifted, terrain);

                town.radius = TownRadius(town.houses, town.center);
                nearest = Mathf.Min(nearest, Planar(town.center, airportCenter));
            }
            return nearest;
        }

        static Vector3 FindFarTownPosition(Terrain terrain, Bounds land, Vector3 airportCenter, Vector3 oldCenter, float safeRadius, int index)
        {
            Vector3 baseDir = oldCenter - airportCenter;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 1f)
            {
                float a = (35f + index * 91f) * Mathf.Deg2Rad;
                baseDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            baseDir.Normalize();

            float current = Planar(oldCenter, airportCenter);
            float desired = Mathf.Max(current + 1700f, safeRadius + 4850f + index * 180f);
            float[] offsets = { 0f, 14f, -14f, 28f, -28f, 42f, -42f, 58f, -58f };
            Vector3 best = oldCenter;
            float bestScore = float.MinValue;

            foreach (float offset in offsets)
            {
                Vector3 dir = Quaternion.Euler(0f, offset, 0f) * baseDir;
                float max = MaxTravelInsideLand(land, airportCenter, dir, 950f);
                float distance = Mathf.Min(desired, max - 220f);
                if (distance < safeRadius + 1500f) continue;

                Vector3 candidate = airportCenter + dir * distance;
                candidate = ClampToTerrain(terrain, candidate, 900f);
                float actual = Planar(candidate, airportCenter);
                float score = actual - Mathf.Abs(offset) * 3f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            best.y = Ground(terrain, best);
            return best;
        }

        static void ShiftTownNamedRoots(Transform world, Transform roadsRoot, Town town, HashSet<Transform> shifted, Terrain terrain)
        {
            List<Transform> roots = new List<Transform>();
            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (tr == town.root || IsDescendantOf(tr, town.root) || IsDescendantOf(tr, roadsRoot)) continue;
                if (!tr.name.Contains(town.name)) continue;

                Transform p = tr.parent;
                bool parentMatches = false;
                while (p && p != world)
                {
                    if (p.name.Contains(town.name)) { parentMatches = true; break; }
                    p = p.parent;
                }
                if (!parentMatches) roots.Add(tr);
            }

            foreach (Transform tr in roots)
            {
                tr.position += town.delta;
                SnapRootToTerrain(terrain, tr);
                shifted.Add(tr);
            }
        }

        static void ShiftNearbyTownObjects(Transform world, Transform roadsRoot, Town town, HashSet<Transform> shifted, Terrain terrain)
        {
            string[] prefixes = { "Roadside Bus Stop", "Street Lamp", "Fire Hydrant", "Town Gateway" };
            List<Transform> roots = new List<Transform>();

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (IsDescendantOf(tr, town.root) || IsDescendantOf(tr, roadsRoot) || HasShiftedAncestor(tr, shifted)) continue;
                if (Planar(tr.position, town.oldCenter) > 520f) continue;

                bool match = false;
                foreach (string prefix in prefixes)
                    if (tr.name.StartsWith(prefix)) { match = true; break; }
                if (match) roots.Add(tr);
            }

            foreach (Transform tr in roots)
            {
                tr.position += town.delta;
                SnapRootToTerrain(terrain, tr);
                shifted.Add(tr);
            }
        }

        static void SnapRootToTerrain(Terrain terrain, Transform root)
        {
            Vector3 p = root.position;
            p.y = Ground(terrain, p);
            root.position = p;
        }

        static void RefreshTownRoadLists(Transform roadsRoot, List<Town> towns)
        {
            foreach (Town town in towns)
            {
                town.localRoads.Clear();
                for (int i = 0; i < roadsRoot.childCount; i++)
                {
                    Transform road = roadsRoot.GetChild(i);
                    if (road.name.StartsWith(town.name)) town.localRoads.Add(road);
                }
                town.center = Average(town.houses);
                town.radius = TownRadius(town.houses, town.center);
            }
        }

        static List<Road> BuildRegionalRing(Terrain terrain, Transform root, List<Town> towns, Vector3 airportCenter, float safeRadius, Transform plant, Mats mats)
        {
            List<Road> roads = new List<Road>();
            towns.Sort((a, b) =>
                Mathf.Atan2(a.center.z - airportCenter.z, a.center.x - airportCenter.x)
                    .CompareTo(Mathf.Atan2(b.center.z - airportCenter.z, b.center.x - airportCenter.x)));

            for (int i = 0; i < towns.Count; i++)
            {
                Town a = towns[i];
                Town b = towns[(i + 1) % towns.Count];
                RoadEnd start = TownRoadEndpointToward(a, b.center);
                RoadEnd end = TownRoadEndpointToward(b, a.center);

                List<Vector3> path = SmoothTownToTownRoute(
                    terrain, start, end, airportCenter, safeRadius, plant.position);

                Road road = CreateRoad(
                    terrain,
                    root,
                    $"Expanded County Road {i + 1} - {a.name} to {b.name}",
                    path,
                    7.2f,
                    mats);
                if (road != null) roads.Add(road);
            }
            return roads;
        }

        static List<Vector3> SmoothTownToTownRoute(Terrain terrain, RoadEnd a, RoadEnd b, Vector3 airportCenter, float safeRadius, Vector3 plantCenter)
        {
            Vector3 chord = b.position - a.position;
            chord.y = 0f;
            float length = Mathf.Max(1f, chord.magnitude);
            Vector3 dir = chord / length;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 midpoint = (a.position + b.position) * .5f;
            Vector3 away = midpoint - airportCenter;
            away.y = 0f;
            float sign = away.sqrMagnitude > .1f && Vector3.Dot(side, away.normalized) < 0f ? -1f : 1f;
            float bend = Mathf.Clamp(length * .11f, 90f, 360f);

            List<Vector3> first = BuildTwoStageCurve(terrain, a, b, side * sign, bend);
            List<Vector3> second = BuildTwoStageCurve(terrain, a, b, side * -sign, bend);
            float scoreA = RouteScore(first, terrain, airportCenter, safeRadius, plantCenter);
            float scoreB = RouteScore(second, terrain, airportCenter, safeRadius, plantCenter);
            return scoreA >= scoreB ? first : second;
        }

        static List<Vector3> BuildTwoStageCurve(Terrain terrain, RoadEnd a, RoadEnd b, Vector3 bendDirection, float bend)
        {
            Vector3 chord = b.position - a.position;
            chord.y = 0f;
            float length = Mathf.Max(1f, chord.magnitude);
            Vector3 chordDir = chord / length;
            Vector3 startOut = FlatNormalized(a.outward, chordDir);
            Vector3 endOut = FlatNormalized(b.outward, -chordDir);
            Vector3 mid = (a.position + b.position) * .5f + bendDirection.normalized * bend;
            mid = ClampToTerrain(terrain, mid, 100f);

            Vector3 midTan = chordDir;
            float lead = Mathf.Clamp(length * .13f, 110f, 260f);
            float midLead = Mathf.Clamp(length * .10f, 90f, 220f);

            Vector3 a1 = a.position + startOut * lead;
            Vector3 a2 = mid - midTan * midLead;
            Vector3 b1 = mid + midTan * midLead;
            Vector3 b2 = b.position + endOut * lead;

            List<Vector3> path = new List<Vector3>();
            int firstCount = Mathf.Clamp(Mathf.CeilToInt((Planar(a.position, mid) + bend) / 9f), 18, 220);
            int secondCount = Mathf.Clamp(Mathf.CeilToInt((Planar(mid, b.position) + bend) / 9f), 18, 220);

            for (int i = 0; i <= firstCount; i++)
            {
                float u = i / (float)firstCount;
                Vector3 p = CubicBezier(a.position, a1, a2, mid, u);
                p = ClampToTerrain(terrain, p, 78f);
                p.y = Ground(terrain, p) + .04f;
                path.Add(p);
            }
            for (int i = 1; i <= secondCount; i++)
            {
                float u = i / (float)secondCount;
                Vector3 p = CubicBezier(mid, b1, b2, b.position, u);
                p = ClampToTerrain(terrain, p, 78f);
                p.y = Ground(terrain, p) + .04f;
                path.Add(p);
            }
            return RemoveNearDuplicatePoints(path, .75f);
        }

        static float RouteScore(List<Vector3> path, Terrain terrain, Vector3 airportCenter, float safeRadius, Vector3 plantCenter)
        {
            if (path == null || path.Count < 2) return float.MinValue;
            Bounds land = TerrainBounds(terrain);
            float minAirport = float.MaxValue;
            float minPlant = float.MaxValue;
            float minEdge = float.MaxValue;
            float length = 0f;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 p = path[i];
                minAirport = Mathf.Min(minAirport, Planar(p, airportCenter));
                minPlant = Mathf.Min(minPlant, Planar(p, plantCenter));
                minEdge = Mathf.Min(minEdge, EdgeMargin(land, p));
                if (i > 0) length += Planar(path[i - 1], p);
            }

            float airportPenalty = minAirport < safeRadius + 350f ? (safeRadius + 350f - minAirport) * 30f : 0f;
            float plantPenalty = minPlant < 260f ? (260f - minPlant) * 18f : 0f;
            return minAirport * .35f + minPlant * .18f + minEdge * 1.8f - length * .03f - airportPenalty - plantPenalty;
        }

        static Road BuildPlantAccess(Terrain terrain, Transform root, List<Town> towns, List<Road> regionalRoads, Transform plant, Mats mats)
        {
            if (regionalRoads.Count == 0) return null;

            Vector3 plantCenter = plant.position;
            Road nearestRoad = null;
            Vector3 join = Vector3.zero;
            Vector3 tangent = Vector3.forward;
            float best = float.MaxValue;

            foreach (Road road in regionalRoads)
            {
                for (int i = 0; i < road.path.Count - 1; i++)
                {
                    Vector3 q = ClosestXZ(plantCenter, road.path[i], road.path[i + 1]);
                    float d = Planar(q, plantCenter);
                    if (d >= best) continue;
                    best = d;
                    nearestRoad = road;
                    join = q;
                    Vector3 t = road.path[i + 1] - road.path[i];
                    t.y = 0f;
                    tangent = t.sqrMagnitude < .001f ? Vector3.forward : t.normalized;
                }
            }

            if (nearestRoad == null) return null;

            Vector3 outward = join - plantCenter;
            outward.y = 0f;
            if (outward.sqrMagnitude < .01f) outward = plant.forward;
            outward.Normalize();
            Vector3 gate = plantCenter + outward * 155f;
            gate = ClampToTerrain(terrain, gate, 85f);
            gate.y = Ground(terrain, gate) + .04f;

            if (Vector3.Dot(tangent, gate - join) < 0f) tangent = -tangent;
            float len = Planar(join, gate);
            float lead = Mathf.Clamp(len * .28f, 45f, 110f);
            Vector3 c1 = join + tangent * lead;
            Vector3 c2 = gate + outward * Mathf.Clamp(len * .22f, 35f, 85f);

            int count = Mathf.Clamp(Mathf.CeilToInt(len / 6f), 18, 120);
            List<Vector3> path = new List<Vector3>();
            for (int i = 0; i <= count; i++)
            {
                float u = i / (float)count;
                Vector3 p = CubicBezier(join, c1, c2, gate, u);
                p = ClampToTerrain(terrain, p, 80f);
                p.y = Ground(terrain, p) + .04f;
                path.Add(p);
            }

            return CreateRoad(terrain, root, "Regional Industrial Access Road", path, 8f, mats);
        }

        static Road CreateRoad(Terrain terrain, Transform parent, string name, List<Vector3> path, float width, Mats mats)
        {
            if (path == null || path.Count < 2) return null;
            Transform root = New(name, parent);
            Ribbon(terrain, root, "Gravel Shoulder", path, width + 7.5f, mats.gravel, .04f, false);
            Ribbon(terrain, root, "Road Surface", path, width, mats.asphalt, .11f, true);
            Ribbon(terrain, root, "Center Line", path, .18f, mats.paint, .15f, false);
            return new Road { name = name, width = width, root = root, path = path };
        }

        static void Ribbon(Terrain terrain, Transform parent, string name, List<Vector3> path, float width, Material mat, float lift, bool collider)
        {
            int count = path.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[(count - 1) * 6];
            float distance = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = i == 0 ? path[1] - path[0] : i == count - 1 ? path[count - 1] - path[count - 2] : path[i + 1] - path[i - 1];
                tangent.y = 0f;
                tangent = tangent.sqrMagnitude < .001f ? Vector3.forward : tangent.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * width * .5f;
                Vector3 left = path[i] - side;
                Vector3 right = path[i] + side;
                left.y = Ground(terrain, left) + lift;
                right.y = Ground(terrain, right) + lift;

                if (i > 0) distance += Planar(path[i - 1], path[i]);
                vertices[i * 2] = parent.InverseTransformPoint(left);
                vertices[i * 2 + 1] = parent.InverseTransformPoint(right);
                uv[i * 2] = new Vector2(0f, distance / 8f);
                uv[i * 2 + 1] = new Vector2(1f, distance / 8f);

                if (i < count - 1)
                {
                    int q = i * 6;
                    int v = i * 2;
                    triangles[q] = v;
                    triangles[q + 1] = v + 2;
                    triangles[q + 2] = v + 1;
                    triangles[q + 3] = v + 1;
                    triangles[q + 4] = v + 2;
                    triangles[q + 5] = v + 3;
                }
            }

            Mesh mesh = new Mesh { name = "H51_112_" + Safe(name) + "_" + (++meshId).ToString("000") };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Gen + "/Meshes/" + mesh.name + ".asset");

            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            g.isStatic = true;
        }

        static List<Road> CollectLocalAndRegionalRoads(List<Town> towns, List<Road> regional)
        {
            List<Road> roads = new List<Road>();
            foreach (Town town in towns)
            {
                foreach (Transform tr in town.localRoads)
                {
                    List<Vector3> path = RoadPath(tr);
                    if (path.Count < 2) continue;
                    roads.Add(new Road { name = tr.name, root = tr, width = RoadWidth(tr), path = path });
                }
            }
            roads.AddRange(regional);
            return roads;
        }

        static int BuildRegionalUtilities(Terrain terrain, Transform root, List<Road> roads, Transform plant, Mats mats)
        {
            int poleCount = 0;
            foreach (Road road in roads)
            {
                List<Vector3> points = Resample(road.path, 55f);
                Vector3[] previous = new Vector3[3];
                bool havePrevious = false;
                int sideSign = road.name.GetHashCode() % 2 == 0 ? 1 : -1;

                for (int i = 0; i < points.Count; i++)
                {
                    if (road.name.Contains("Industrial Access") && Planar(points[i], plant.position) < 175f) continue;

                    Vector3 tangent = PathTangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 position = points[i] + side * sideSign * (road.width * .5f + 10.5f);
                    position.y = Ground(terrain, position);

                    Transform pole = New("Regional Utility Pole " + (++poleCount).ToString("000"), root);
                    pole.position = position;
                    pole.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                    Cylinder(pole, "Pole", new Vector3(0f, 4.3f, 0f), new Vector3(.13f, 4.3f, .13f), mats.wood);
                    Box(pole, "Crossarm", new Vector3(0f, 8.0f, 0f), new Vector3(2.35f, .14f, .14f), mats.wood);

                    Vector3[] current = new Vector3[3];
                    for (int k = -1; k <= 1; k++)
                    {
                        Cylinder(pole, "Insulator", new Vector3(k * .74f, 8.2f, 0f), new Vector3(.055f, .15f, .055f), mats.white);
                        current[k + 1] = pole.TransformPoint(new Vector3(k * .74f, 8.38f, 0f));
                        if (havePrevious) Wire(root, previous[k + 1], current[k + 1], mats.metal, "Regional Utility Wire");
                    }
                    previous = current;
                    havePrevious = true;
                }
            }
            return poleCount;
        }

        static int BuildTownUtilities(Terrain terrain, Transform root, List<Town> towns, Mats mats)
        {
            int poleCount = 0;
            foreach (Town town in towns)
            {
                Transform road = LongestRoad(town.localRoads);
                if (!road) continue;
                List<Vector3> path = Resample(RoadPath(road), 46f);
                if (path.Count < 2) continue;

                Vector3[] previous = new Vector3[3];
                bool havePrevious = false;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 tangent = PathTangent(path, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 position = path[i] + side * 8.2f;
                    position.y = Ground(terrain, position);

                    Transform pole = New("Town Utility Pole " + (++poleCount).ToString("000"), root);
                    pole.position = position;
                    pole.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                    Cylinder(pole, "Pole", new Vector3(0f, 4.05f, 0f), new Vector3(.12f, 4.05f, .12f), mats.wood);
                    Box(pole, "Crossarm", new Vector3(0f, 7.55f, 0f), new Vector3(2.1f, .13f, .13f), mats.wood);

                    Vector3[] current = new Vector3[3];
                    for (int k = -1; k <= 1; k++)
                    {
                        Cylinder(pole, "Insulator", new Vector3(k * .68f, 7.76f, 0f), new Vector3(.05f, .14f, .05f), mats.white);
                        current[k + 1] = pole.TransformPoint(new Vector3(k * .68f, 7.95f, 0f));
                        if (havePrevious) Wire(root, previous[k + 1], current[k + 1], mats.metal, "Town Utility Wire");
                    }
                    previous = current;
                    havePrevious = true;
                }
            }
            return poleCount;
        }

        static int BuildRegionalTerrainForest(Terrain terrain, List<Town> towns, List<Road> roads, Bounds airportSafety, Transform plant)
        {
            TerrainData data = terrain.terrainData;
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafTree);
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirTree);
            if (leaf && fir)
                data.treePrototypes = new[]
                {
                    new TreePrototype { prefab = fir, bendFactor = .25f },
                    new TreePrototype { prefab = leaf, bendFactor = .32f }
                };
            if (data.treePrototypes == null || data.treePrototypes.Length == 0)
            {
                Debug.LogWarning("Step 112 found no terrain tree prototypes.");
                return 0;
            }

            Bounds airportExclusion = airportSafety;
            airportExclusion.Expand(new Vector3(800f, 300f, 800f));
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            System.Random rng = new System.Random(Seed + 31);
            List<TreeInstance> trees = new List<TreeInstance>();
            int target = 18500;
            int tries = 0;

            while (trees.Count < target && tries++ < 360000)
            {
                float nx = Next(rng, .012f, .988f);
                float nz = Next(rng, .012f, .988f);
                Vector3 p = new Vector3(origin.x + nx * size.x, 0f, origin.z + nz * size.z);

                if (ContainsXZ(airportExclusion, p)) continue;
                if (Planar(p, plant.position) < 235f) continue;
                if (InsideTownCore(p, towns, 55f)) continue;
                if (NearRoad(p, roads, 14f)) continue;

                float broad = Fbm(p.x * .00034f + 13f, p.z * .00034f + 37f, 4);
                float fine = Fbm(p.x * .00105f + 71f, p.z * .00105f + 19f, 3);
                float forest = broad * .72f + fine * .28f;
                if (forest < .38f) continue;
                if (rng.NextDouble() > Mathf.Clamp01(.24f + (forest - .38f) * 2.3f)) continue;

                float h = Next(rng, 1.38f, 2.35f);
                float w = h * Next(rng, .78f, .96f);
                trees.Add(new TreeInstance
                {
                    position = new Vector3(nx, data.GetInterpolatedHeight(nx, nz) / Mathf.Max(1f, size.y), nz),
                    prototypeIndex = rng.Next(data.treePrototypes.Length),
                    widthScale = w,
                    heightScale = h,
                    rotation = Next(rng, 0f, 6.283185f),
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }

            data.treeInstances = trees.ToArray();
            EditorUtility.SetDirty(data);
            return trees.Count;
        }

        static int AddSettlementTrees(Terrain terrain, Transform root, List<Town> towns, List<Road> roads)
        {
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafTree);
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirTree);
            if (!leaf && !fir) return 0;

            System.Random rng = new System.Random(Seed + 42);
            int made = 0;
            foreach (Town town in towns)
            {
                int desired = town == towns[0] ? 30 : 20;
                int tries = 0;
                int local = 0;
                while (local < desired && tries++ < desired * 25)
                {
                    float angle = Next(rng, 0f, Mathf.PI * 2f);
                    float radius = Next(rng, Mathf.Max(120f, town.radius * .70f), town.radius + 150f);
                    Vector3 p = town.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (NearRoad(p, roads, 13f) || NearHouse(p, town.houses, 18f)) continue;
                    p.y = Ground(terrain, p);

                    GameObject src = rng.NextDouble() < .72 ? leaf : fir;
                    if (!src) src = leaf ? leaf : fir;
                    GameObject g = PrefabUtility.InstantiatePrefab(src) as GameObject;
                    if (!g) continue;
                    g.name = "Settlement Tree " + (++made).ToString("000");
                    g.transform.SetParent(root, false);
                    g.transform.position = p;
                    g.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
                    float scale = Next(rng, 1.05f, 1.65f);
                    g.transform.localScale = Vector3.one * scale;
                    local++;
                }
            }
            return made;
        }

        static int AddRegionalAssetGrass(Terrain terrain, Transform root, List<Town> towns, List<Road> roads, Bounds airportSafety, Transform plant)
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(GrassA);
            GameObject b = AssetDatabase.LoadAssetAtPath<GameObject>(GrassB);
            if (!a && !b) return 0;

            Bounds airportExclusion = airportSafety;
            airportExclusion.Expand(new Vector3(650f, 250f, 650f));
            System.Random rng = new System.Random(Seed + 53);
            int made = 0;
            int tries = 0;

            while (made < 950 && tries++ < 30000)
            {
                Vector3 p = RandomTerrainPoint(rng, terrain);
                if (ContainsXZ(airportExclusion, p)) continue;
                if (Planar(p, plant.position) < 190f) continue;
                if (InsideTownCore(p, towns, 10f)) continue;
                if (NearRoad(p, roads, 5.5f)) continue;
                if (Fbm(p.x * .0018f, p.z * .0018f, 3) < .34f) continue;

                GameObject src = rng.NextDouble() < .5 ? a : b;
                if (!src) src = a ? a : b;
                GameObject g = PrefabUtility.InstantiatePrefab(src) as GameObject;
                if (!g) continue;
                g.name = "Regional Asset Grass " + (++made).ToString("000");
                g.transform.SetParent(root, false);
                g.transform.position = p;
                g.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
                g.transform.localScale = Vector3.one * Next(rng, .75f, 1.45f);
            }
            return made;
        }

        static int ClearSceneTreesFromRoads(Transform world, List<Road> roads)
        {
            List<GameObject> kill = new List<GameObject>();
            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (!IsSceneTreeRoot(tr)) continue;
                if (NearRoad(tr.position, roads, 8f)) kill.Add(tr.gameObject);
            }
            foreach (GameObject g in kill) if (g) UnityEngine.Object.DestroyImmediate(g);
            return kill.Count;
        }

        static int CountSceneTreeRoadIntrusions(Transform world, List<Road> roads)
        {
            int bad = 0;
            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
                if (IsSceneTreeRoot(tr) && NearRoad(tr.position, roads, 6f)) bad++;
            return bad;
        }

        static bool IsSceneTreeRoot(Transform tr)
        {
            string n = tr.name;
            bool match = n.StartsWith("Settlement Tree") || n.StartsWith("Town Tree") || n.StartsWith("Refinement Town Tree") ||
                         n.StartsWith("Natural Road Tree") || n.StartsWith("Final Mature Tree") || n.StartsWith("forestpack_tree");
            if (!match) return false;
            Transform p = tr.parent;
            if (!p) return true;
            string pn = p.name;
            return !(pn.StartsWith("Settlement Tree") || pn.StartsWith("Town Tree") || pn.StartsWith("Refinement Town Tree") ||
                     pn.StartsWith("Natural Road Tree") || pn.StartsWith("Final Mature Tree") || pn.StartsWith("forestpack_tree"));
        }

        static int CountSharpRegionalEntries(Transform regionalRoot, List<Town> towns)
        {
            int sharp = 0;
            for (int i = 0; i < regionalRoot.childCount; i++)
            {
                Transform road = regionalRoot.GetChild(i);
                if (road.name.Contains("Industrial Access")) continue;
                List<Vector3> path = RoadPath(road);
                if (path.Count < 6) { sharp++; continue; }

                Town startTown = NearestTown(path[0], towns);
                Town endTown = NearestTown(path[path.Count - 1], towns);
                if (startTown != null)
                {
                    RoadEnd e = TownRoadEndpointToward(startTown, path[path.Count - 1]);
                    Vector3 t = path[Mathf.Min(5, path.Count - 1)] - path[0];
                    t.y = 0f;
                    if (t.sqrMagnitude > .001f && Vector3.Angle(t.normalized, e.outward) > 14f) sharp++;
                }
                if (endTown != null)
                {
                    RoadEnd e = TownRoadEndpointToward(endTown, path[0]);
                    Vector3 t = path[Mathf.Max(0, path.Count - 6)] - path[path.Count - 1];
                    t.y = 0f;
                    if (t.sqrMagnitude > .001f && Vector3.Angle(t.normalized, e.outward) > 14f) sharp++;
                }
            }
            return sharp;
        }

        static Town NearestTown(Vector3 p, List<Town> towns)
        {
            Town bestTown = null;
            float best = float.MaxValue;
            foreach (Town town in towns)
            {
                float d = Planar(p, town.center);
                if (d < best) { best = d; bestTown = town; }
            }
            return bestTown;
        }

        static RoadEnd TownRoadEndpointToward(Town town, Vector3 target)
        {
            RoadEnd best = new RoadEnd { position = town.center, outward = FlatNormalized(target - town.center, Vector3.forward) };
            float bestDistance = float.MaxValue;

            foreach (Transform road in town.localRoads)
            {
                List<Vector3> path = RoadPath(road);
                if (path.Count < 2) continue;

                float d0 = Planar(path[0], target);
                if (d0 < bestDistance)
                {
                    bestDistance = d0;
                    best.position = path[0];
                    best.outward = FlatNormalized(path[0] - path[1], target - town.center);
                }

                int last = path.Count - 1;
                float d1 = Planar(path[last], target);
                if (d1 < bestDistance)
                {
                    bestDistance = d1;
                    best.position = path[last];
                    best.outward = FlatNormalized(path[last] - path[last - 1], target - town.center);
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
                path.Add((mf.transform.TransformPoint(vertices[i]) + mf.transform.TransformPoint(vertices[i + 1])) * .5f);
            return path;
        }

        static float RoadWidth(Transform road)
        {
            Transform surface = FindChild(road, "Road Surface");
            MeshFilter mf = surface ? surface.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh || mf.sharedMesh.vertexCount < 2) return 7f;
            Vector3[] v = mf.sharedMesh.vertices;
            return Vector3.Distance(mf.transform.TransformPoint(v[0]), mf.transform.TransformPoint(v[1]));
        }

        static void ConformRoads(Terrain terrain, Transform roadsRoot)
        {
            foreach (Transform road in roadsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (road.name == "Road Surface" || road.name == "Gravel Shoulder" || road.name == "Center Line")
                    ConformMeshObject(terrain, road);
            }
        }

        static void ConformRoad(Terrain terrain, Transform road)
        {
            foreach (Transform tr in road.GetComponentsInChildren<Transform>(true))
                if (tr.name == "Road Surface" || tr.name == "Gravel Shoulder" || tr.name == "Center Line")
                    ConformMeshObject(terrain, tr);
        }

        static void ConformMeshObject(Terrain terrain, Transform tr)
        {
            MeshFilter mf = tr.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return;
            Mesh mesh = mf.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            float lift = tr.name == "Center Line" ? .15f : tr.name == "Road Surface" ? .11f : .04f;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = tr.TransformPoint(vertices[i]);
                world.y = Ground(terrain, world) + lift;
                vertices[i] = tr.InverseTransformPoint(world);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            MeshCollider mc = tr.GetComponent<MeshCollider>();
            if (mc) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
        }

        static bool NearRoad(Vector3 p, List<Road> roads, float extra)
        {
            foreach (Road road in roads)
                for (int i = 0; i < road.path.Count - 1; i++)
                    if (SegmentDistanceXZ(p, road.path[i], road.path[i + 1]) < road.width * .5f + extra)
                        return true;
            return false;
        }

        static bool InsideTownCore(Vector3 p, List<Town> towns, float extra)
        {
            foreach (Town town in towns)
                if (Planar(p, town.center) < town.radius + extra) return true;
            return false;
        }

        static bool NearHouse(Vector3 p, List<Transform> houses, float extra)
        {
            foreach (Transform h in houses)
                if (Planar(p, h.position) < extra) return true;
            return false;
        }

        static float TownRadius(List<Transform> houses, Vector3 center)
        {
            float radius = 100f;
            foreach (Transform h in houses) radius = Mathf.Max(radius, Planar(h.position, center) + 35f);
            return radius;
        }

        static Transform LongestRoad(List<Transform> roads)
        {
            Transform best = null;
            float length = 0f;
            foreach (Transform road in roads)
            {
                List<Vector3> p = RoadPath(road);
                float l = PathLength(p);
                if (l > length) { length = l; best = road; }
            }
            return best;
        }

        static List<Vector3> Resample(List<Vector3> path, float spacing)
        {
            List<Vector3> result = new List<Vector3>();
            if (path == null || path.Count == 0) return result;
            result.Add(path[0]);
            float carried = 0f;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[i + 1];
                float length = Planar(a, b);
                if (length < .001f) continue;
                Vector3 dir = (b - a) / length;
                float d = spacing - carried;
                while (d <= length)
                {
                    result.Add(a + dir * d);
                    d += spacing;
                }
                carried = Mathf.Max(0f, length - (d - spacing));
                carried = Mathf.Repeat(carried, spacing);
            }
            if (Planar(result[result.Count - 1], path[path.Count - 1]) > spacing * .3f)
                result.Add(path[path.Count - 1]);
            return result;
        }

        static Vector3 PathTangent(List<Vector3> path, int index)
        {
            Vector3 t = index == 0 ? path[1] - path[0] : index == path.Count - 1 ? path[index] - path[index - 1] : path[index + 1] - path[index - 1];
            t.y = 0f;
            return t.sqrMagnitude < .001f ? Vector3.forward : t.normalized;
        }

        static Vector3 ClosestXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 x = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            float u = d.sqrMagnitude < .001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - x, d) / d.sqrMagnitude);
            Vector3 result = Vector3.Lerp(a, b, u);
            result.y = Mathf.Lerp(a.y, b.y, u);
            return result;
        }

        static float SegmentDistanceXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            return Planar(p, ClosestXZ(p, a, b));
        }

        static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 FlatNormalized(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            if (v.sqrMagnitude < .001f)
            {
                fallback.y = 0f;
                return fallback.sqrMagnitude < .001f ? Vector3.forward : fallback.normalized;
            }
            return v.normalized;
        }

        static List<Vector3> RemoveNearDuplicatePoints(List<Vector3> input, float minDistance)
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Vector3 p in input)
            {
                if (result.Count == 0 || Planar(result[result.Count - 1], p) >= minDistance)
                    result.Add(p);
            }
            return result;
        }

        static float PathLength(List<Vector3> path)
        {
            float length = 0f;
            if (path == null) return length;
            for (int i = 1; i < path.Count; i++) length += Planar(path[i - 1], path[i]);
            return length;
        }

        static void Box(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
        }

        static void Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
        }

        static void Wire(Transform parent, Vector3 a, Vector3 b, Material material, string name)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < .001f) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = (a + b) * .5f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.transform.localScale = new Vector3(.025f, d.magnitude * .5f, .025f);
            g.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
        }

        static Vector3 RandomTerrainPoint(System.Random rng, Terrain terrain)
        {
            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            Vector3 p = new Vector3(o.x + Next(rng, .015f, .985f) * s.x, 0f, o.z + Next(rng, .015f, .985f) * s.z);
            p.y = Ground(terrain, p);
            return p;
        }

        static float Fbm(float x, float y, int octaves)
        {
            float value = 0f, amplitude = 1f, total = 0f, frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                total += amplitude;
                amplitude *= .5f;
                frequency *= 2.03f;
            }
            return total > 0f ? value / total : 0f;
        }

        static float Next(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static Bounds AirportSafetyBounds(GameObject airport)
        {
            Bounds b = BoundsOf(airport);
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                if (!t || !t.gameObject.scene.IsValid()) continue;
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("runway") && !n.Contains("taxiway") && !n.Contains("apron")) continue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
                foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) b.Encapsulate(c.bounds);
            }
            return b;
        }

        static Bounds BoundsOf(GameObject g)
        {
            bool set = false;
            Bounds b = new Bounds(g.transform.position, Vector3.zero);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (!set) { b = r.bounds; set = true; }
                else b.Encapsulate(r.bounds);
            }
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
            {
                if (!set) { b = c.bounds; set = true; }
                else b.Encapsulate(c.bounds);
            }
            return b;
        }

        static Bounds TerrainBounds(Terrain terrain)
        {
            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            return new Bounds(o + s * .5f, s);
        }

        static float MaxTravelInsideLand(Bounds land, Vector3 origin, Vector3 dir, float margin)
        {
            float best = float.MaxValue;
            if (dir.x > .0001f) best = Mathf.Min(best, (land.max.x - margin - origin.x) / dir.x);
            else if (dir.x < -.0001f) best = Mathf.Min(best, (land.min.x + margin - origin.x) / dir.x);
            if (dir.z > .0001f) best = Mathf.Min(best, (land.max.z - margin - origin.z) / dir.z);
            else if (dir.z < -.0001f) best = Mathf.Min(best, (land.min.z + margin - origin.z) / dir.z);
            return best == float.MaxValue ? Mathf.Min(land.size.x, land.size.z) * .35f : best;
        }

        static Vector3 ClampToTerrain(Terrain terrain, Vector3 p, float margin)
        {
            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            p.x = Mathf.Clamp(p.x, o.x + margin, o.x + s.x - margin);
            p.z = Mathf.Clamp(p.z, o.z + margin, o.z + s.z - margin);
            p.y = Ground(terrain, p);
            return p;
        }

        static float EdgeMargin(Bounds land, Vector3 p)
        {
            return Mathf.Min(p.x - land.min.x, land.max.x - p.x, p.z - land.min.z, land.max.z - p.z);
        }

        static bool ContainsXZ(Bounds bounds, Vector3 p)
        {
            return p.x >= bounds.min.x && p.x <= bounds.max.x && p.z >= bounds.min.z && p.z <= bounds.max.z;
        }

        static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        static List<Transform> Houses(Transform town)
        {
            List<Transform> houses = new List<Transform>();
            for (int i = 0; i < town.childCount; i++)
            {
                Transform c = town.GetChild(i);
                if (c.name.StartsWith("Detailed House") || c.name.StartsWith("Building")) houses.Add(c);
            }
            return houses;
        }

        static Vector3 Average(List<Transform> transforms)
        {
            if (transforms.Count == 0) return Vector3.zero;
            Vector3 c = Vector3.zero;
            foreach (Transform t in transforms) c += t.position;
            return c / transforms.Count;
        }

        static bool IsDescendantOf(Transform child, Transform ancestor)
        {
            if (!child || !ancestor) return false;
            Transform p = child.parent;
            while (p)
            {
                if (p == ancestor) return true;
                p = p.parent;
            }
            return false;
        }

        static bool HasShiftedAncestor(Transform child, HashSet<Transform> shifted)
        {
            Transform p = child.parent;
            while (p)
            {
                if (shifted.Contains(p)) return true;
                p = p.parent;
            }
            return false;
        }

        static void RemoveAllNamed(Transform root, string exactName)
        {
            List<GameObject> kill = new List<GameObject>();
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr != root && tr.name == exactName) kill.Add(tr.gameObject);
            foreach (GameObject g in kill) if (g) UnityEngine.Object.DestroyImmediate(g);
        }

        static int Count(Transform root, string contains)
        {
            int count = 0;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr != root && tr.name.Contains(contains)) count++;
            return count;
        }

        static int CountDirectRoadsContaining(Transform root, string text)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name.Contains(text)) count++;
            return count;
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
                if (t && t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static Transform DirectChild(Transform root, string name)
        {
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

        static string Safe(string n)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Replace(' ', '_');
        }

        static void ResetFolder()
        {
            if (AssetDatabase.IsValidFolder(Gen)) AssetDatabase.DeleteAsset(Gen);
            Ensure(Gen + "/Meshes");
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
