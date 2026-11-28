using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldScaleMatteIndustrialCleanup
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string RegionalPassName = "Hanger 51 Regional Infrastructure Pass";
        const string NaturalPassName = "Hanger 51 Natural Road Network Pass";
        const string CleanupName = "Hanger 51 Scale Matte Industrial Cleanup";
        const string BaseGen = "Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen = "Assets/_Project/Environment/Generated/CountrysideScaleMatteCleanup";
        const string GrassA = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string GrassB = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string LeafTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string FirTree = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const int Seed = 51110;

        static int meshId;

        class Road
        {
            public string name;
            public float width;
            public Transform root;
            public List<Vector3> path = new List<Vector3>();
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
        }

        [MenuItem("Hanger 51/World/Current/110 - Fix Scale Ground Shine And Power Plant Roads")]
        public static void Build()
        {
            Hanger51WorldNaturalRoadNetworkPass.Build();

            GameObject world = Find(WorldName);
            GameObject regionalPass = Find(RegionalPassName);
            GameObject naturalPass = Find(NaturalPassName);
            Terrain terrain = FindTerrain();

            if (!world || !regionalPass || !naturalPass || !terrain)
            {
                Debug.LogError("Step 110 could not find the Step 108 countryside.");
                return;
            }

            Transform roadsRoot = DirectChild(world.transform, "Road Network");
            Transform settlements = FindChild(world.transform, "Settlements");
            Transform plant = FindChild(regionalPass.transform, "Power Station Complex");
            Transform naturalRoadRoot = roadsRoot ? DirectChild(roadsRoot, "Natural Regional Road Network") : null;

            if (!roadsRoot || !settlements || !plant || !naturalRoadRoot)
            {
                Debug.LogError("Step 110 could not find roads, towns, or the power station.", world);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Preparing cleanup pass", .03f);

                GameObject old = Find(CleanupName);
                if (old) UnityEngine.Object.DestroyImmediate(old);

                ResetFolder();
                meshId = 0;
                Mats mats = LoadMats();
                Transform cleanup = New(CleanupName, naturalPass.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Removing polished terrain response", .12f);
                int matteLayers = ForceTerrainMatte(terrain);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Normalizing house and business scale", .25f);
                int houses = NormalizeHouseBuildings(settlements);
                int businesses = NormalizePurposeBuildings(world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Normalizing mature tree scale", .37f);
                int sceneTrees = NormalizeGeneratedTrees(world.transform);
                int terrainTrees = NormalizeTerrainTrees(terrain);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Protecting power station footprint from through-roads", .51f);
                int detoured = CleanPowerPlantRoads(terrain, naturalRoadRoot, plant, mats);
                List<Road> finalRoads = CollectRoads(naturalRoadRoot);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Building one clean industrial access road", .63f);
                Road access = BuildCleanPlantAccess(terrain, naturalRoadRoot, plant, finalRoads, mats);
                if (access != null) finalRoads.Add(access);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Rebuilding utilities on final road paths", .73f);
                RemoveDirectChild(naturalPass.transform, "Intercity Roadside Utilities");
                RemoveDirectChild(naturalPass.transform, "Natural Roadside Detail");
                RemoveDirectChild(naturalPass.transform, "Natural Road Vegetation");
                Transform utilities = New("Final Scaled Roadside Utilities", cleanup);
                int poles = BuildUtilities(terrain, utilities, finalRoads, mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Rebuilding roadside detail and foliage", .84f);
                Transform detail = New("Final Roadside Detail", cleanup);
                int markers = AddRoadMarkers(terrain, detail, finalRoads, mats);
                Transform vegetation = New("Final Roadside Vegetation", cleanup);
                int grass = AddGrass(terrain, vegetation, finalRoads);
                int trees = AddTrees(terrain, vegetation, finalRoads, plant);

                EditorUtility.DisplayProgressBar("Hanger 51 Scale/Matte Cleanup", "Final lane and terrain conformity check", .94f);
                int moved = ClearLaneIntrusions(world.transform, finalRoads, terrain, plant);
                ConformRoads(terrain, naturalRoadRoot);

                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = cleanup.gameObject;

                Debug.Log(
                    $"Step 110 complete. matte terrain layers={matteLayers}, houses normalized={houses}, businesses normalized={businesses}, scene trees normalized={sceneTrees}, terrain trees normalized={terrainTrees}, power-plant county roads detoured={detoured}, final utility poles={poles}, roadside markers={markers}, asset grass={grass}, mature roadside trees={trees}, objects moved clear of lanes={moved}. Run Step 111 to validate.",
                    cleanup.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/111 - Validate Scale Ground And Power Plant Roads")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            GameObject cleanup = Find(CleanupName);
            GameObject regionalPass = Find(RegionalPassName);
            Terrain terrain = FindTerrain();

            if (!world || !cleanup || !regionalPass || !terrain)
            {
                Debug.LogError("Step 111 failed: run Step 110 first.");
                return;
            }

            Transform roadsRoot = DirectChild(world.transform, "Road Network");
            Transform naturalRoadRoot = roadsRoot ? DirectChild(roadsRoot, "Natural Regional Road Network") : null;
            Transform settlements = FindChild(world.transform, "Settlements");
            Transform plant = FindChild(regionalPass.transform, "Power Station Complex");

            int matteBad = 0;
            if (terrain.terrainData.terrainLayers != null)
            {
                foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
                {
                    if (!layer) continue;
                    if (layer.smoothness > .001f || layer.metallic > .001f || layer.maskMapTexture)
                        matteBad++;
                }
            }

            int oversizedHouses = 0;
            int houseCount = 0;
            if (settlements)
            {
                foreach (Transform t in settlements.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("Detailed House")) continue;
                    Transform siding = DirectChild(t, "Textured Siding");
                    Renderer r = siding ? siding.GetComponent<Renderer>() : null;
                    if (!r) continue;
                    houseCount++;
                    if (r.bounds.size.y > 6.8f || Mathf.Max(r.bounds.size.x, r.bounds.size.z) > 15.5f)
                        oversizedHouses++;
                }
            }

            int treeCount = 0;
            int undersizedTrees = 0;
            foreach (Transform t in world.transform.GetComponentsInChildren<Transform>(true))
            {
                if (!IsGeneratedTree(t.name)) continue;
                float h = RendererHeight(t);
                if (h <= .01f) continue;
                treeCount++;
                if (h < 8.0f) undersizedTrees++;
            }

            int plantIntrusions = plant && naturalRoadRoot ? CountPlantRoadIntrusions(naturalRoadRoot, plant) : 999;
            int industrialRoads = naturalRoadRoot ? CountDirectRoadsContaining(naturalRoadRoot, "Industrial Access") : 0;
            int utilityPoles = Count(cleanup.transform, "Final Utility Pole");
            int wires = Count(cleanup.transform, "Final Utility Wire");
            int markers = Count(cleanup.transform, "Final Roadside Marker");
            int grass = Count(cleanup.transform, "Final Road Grass");
            int trees = Count(cleanup.transform, "Final Mature Tree");

            bool ok =
                matteBad == 0 &&
                oversizedHouses == 0 &&
                houseCount >= 60 &&
                undersizedTrees == 0 &&
                treeCount >= 25 &&
                plantIntrusions == 0 &&
                industrialRoads == 1 &&
                utilityPoles >= 40 &&
                wires >= 90 &&
                markers >= 50 &&
                grass >= 500 &&
                trees >= 35;

            if (ok)
            {
                Debug.Log(
                    $"Step 111 passed. matte failures={matteBad}, houses={houseCount}, oversized houses={oversizedHouses}, measured trees={treeCount}, undersized trees={undersizedTrees}, plant road intrusions={plantIntrusions}, industrial access roads={industrialRoads}, utility poles={utilityPoles}, wires={wires}, markers={markers}, grass={grass}, final mature trees={trees}.",
                    cleanup);
            }
            else
            {
                Debug.LogError(
                    $"Step 111 failed. matte failures={matteBad}, houses={houseCount}, oversized houses={oversizedHouses}, measured trees={treeCount}, undersized trees={undersizedTrees}, plant road intrusions={plantIntrusions}, industrial access roads={industrialRoads}, utility poles={utilityPoles}, wires={wires}, markers={markers}, grass={grass}, final mature trees={trees}.",
                    cleanup);
            }
        }

        static Mats LoadMats()
        {
            Mats m = new Mats();
            m.asphalt = Load("Matte_Asphalt");
            m.gravel = Load("Matte_Gravel");
            m.paint = Load("Road_Paint");
            m.wood = Load("Weathered_Wood");
            m.metal = Load("Dark_Metal");
            m.white = Load("Warm_White");
            m.red = Load("Barn_Red");
            return m;
        }

        static Material Load(string name)
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

        static int ForceTerrainMatte(Terrain terrain)
        {
            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            int fixedCount = 0;

            if (layers != null)
            {
                Ensure(Gen + "/Textures");

                for (int i = 0; i < layers.Length; i++)
                {
                    TerrainLayer layer = layers[i];
                    if (!layer) continue;

                    layer.smoothness = 0f;
                    layer.metallic = 0f;
                    layer.maskMapTexture = null;
                    layer.normalScale = Mathf.Min(layer.normalScale, .55f);

                    Texture2D source = layer.diffuseTexture;
                    if (source)
                    {
                        try
                        {
                            Color32[] pixels = source.GetPixels32();
                            for (int p = 0; p < pixels.Length; p++) pixels[p].a = 0;

                            Texture2D matte = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
                            {
                                name = "H51 Matte " + layer.name,
                                wrapMode = TextureWrapMode.Repeat,
                                filterMode = source.filterMode,
                                anisoLevel = source.anisoLevel
                            };
                            matte.SetPixels32(pixels);
                            matte.Apply(true, false);
                            string path = Gen + "/Textures/MatteTerrain_" + i.ToString("00") + ".asset";
                            AssetDatabase.CreateAsset(matte, path);
                            layer.diffuseTexture = matte;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("Step 110 could not make a zero-alpha matte copy of terrain layer " + layer.name + ": " + e.Message);
                        }
                    }

                    EditorUtility.SetDirty(layer);
                    fixedCount++;
                }
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Nature/Terrain/Standard");
            if (shader)
            {
                Material terrainMat = new Material(shader) { name = "H51 Completely Matte Terrain" };
                if (terrainMat.HasProperty("_Smoothness")) terrainMat.SetFloat("_Smoothness", 0f);
                if (terrainMat.HasProperty("_Metallic")) terrainMat.SetFloat("_Metallic", 0f);
                if (terrainMat.HasProperty("_SpecularHighlights")) terrainMat.SetFloat("_SpecularHighlights", 0f);
                if (terrainMat.HasProperty("_EnvironmentReflections")) terrainMat.SetFloat("_EnvironmentReflections", 0f);
                terrainMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                terrainMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(terrainMat, Gen + "/CompletelyMatteTerrain.mat");
                terrain.materialTemplate = terrainMat;
                EditorUtility.SetDirty(terrain);
            }

            return fixedCount;
        }

        static int NormalizeHouseBuildings(Transform settlements)
        {
            int changed = 0;

            foreach (Transform house in settlements.GetComponentsInChildren<Transform>(true))
            {
                if (!house.name.StartsWith("Detailed House")) continue;

                Transform siding = DirectChild(house, "Textured Siding");
                Renderer renderer = siding ? siding.GetComponent<Renderer>() : null;
                if (!renderer) continue;

                Vector3 size = renderer.bounds.size;
                float desiredHeight = Mathf.Lerp(5.2f, 6.15f, Hash01(house.name));
                float desiredWidth = Mathf.Lerp(11.5f, 14.2f, Hash01(house.name + "width"));
                float horizontal = Mathf.Max(size.x, size.z);
                float factor = Mathf.Min(desiredHeight / Mathf.Max(.01f, size.y), desiredWidth / Mathf.Max(.01f, horizontal));
                factor = Mathf.Clamp(factor, .58f, .96f);

                if (factor > .985f) continue;

                for (int i = 0; i < house.childCount; i++)
                {
                    Transform child = house.GetChild(i);
                    if (child.name == "Home Detail Set") continue;
                    child.localPosition *= factor;
                    child.localScale *= factor;
                }

                Transform details = DirectChild(house, "Home Detail Set");
                if (details)
                    PullLotDetailsTowardSmallerHouse(details, factor);

                changed++;
            }

            return changed;
        }

        static void PullLotDetailsTowardSmallerHouse(Transform details, float factor)
        {
            foreach (Transform t in details.GetComponentsInChildren<Transform>(true))
            {
                if (t == details) continue;
                if (t.parent != details) continue;

                Vector3 p = t.localPosition;
                p.x *= factor;
                p.z *= factor;
                t.localPosition = p;

                string n = t.name;
                if (n.Contains("Porch") || n.Contains("Walkway") || n.Contains("Fence"))
                {
                    Vector3 s = t.localScale;
                    s.x *= factor;
                    s.z *= factor;
                    t.localScale = s;
                }
            }
        }

        static int NormalizePurposeBuildings(Transform world)
        {
            Transform purposeRoot = FindChild(world, "Purposeful Buildings");
            if (!purposeRoot) return 0;
            int changed = 0;

            for (int i = 0; i < purposeRoot.childCount; i++)
            {
                Transform site = purposeRoot.GetChild(i);
                if (!site.name.StartsWith("Purpose -")) continue;

                Transform body = DirectChild(site, "Purpose Building");
                Renderer r = body ? body.GetComponent<Renderer>() : null;
                if (!r) continue;

                float targetHeight = site.name.Contains("Fire Station") ? 6.5f :
                                     site.name.Contains("Town Hall") ? 5.8f :
                                     site.name.Contains("Repair Garage") ? 5.4f :
                                     site.name.Contains("Farm Supply") ? 5.4f :
                                     site.name.Contains("Clinic") ? 5.0f : 4.7f;
                float targetWidth = site.name.Contains("Fire Station") ? 18f :
                                    site.name.Contains("Repair Garage") ? 17f :
                                    site.name.Contains("Farm Supply") ? 16f :
                                    site.name.Contains("Town Hall") ? 15f : 13.5f;

                Vector3 size = r.bounds.size;
                float factor = Mathf.Min(targetHeight / Mathf.Max(.01f, size.y), targetWidth / Mathf.Max(.01f, Mathf.Max(size.x, size.z)));
                factor = Mathf.Clamp(factor, .62f, .94f);
                if (factor > .985f) continue;

                for (int c = 0; c < site.childCount; c++)
                {
                    Transform child = site.GetChild(c);
                    if (child.name == "Parking Pad" || child.name.StartsWith("Parked Car") || child.name.Contains("Flag Pole") || child.name == "Flag")
                        continue;

                    child.localPosition *= factor;
                    child.localScale *= factor;
                }
                changed++;
            }

            return changed;
        }

        static int NormalizeGeneratedTrees(Transform world)
        {
            int changed = 0;

            foreach (Transform tree in world.GetComponentsInChildren<Transform>(true))
            {
                if (!IsGeneratedTree(tree.name)) continue;
                float height = RendererHeight(tree);
                if (height <= .01f) continue;

                float target = tree.name.StartsWith("Town Tree") || tree.name.StartsWith("Refinement Town Tree")
                    ? Mathf.Lerp(9.5f, 13.5f, Hash01(tree.name + tree.position.x.ToString("0")))
                    : Mathf.Lerp(11.5f, 17.5f, Hash01(tree.name + tree.position.z.ToString("0")));

                float factor = Mathf.Clamp(target / height, .75f, 3.8f);
                if (Mathf.Abs(factor - 1f) < .03f) continue;
                tree.localScale *= factor;
                changed++;
            }

            return changed;
        }

        static int NormalizeTerrainTrees(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            TreeInstance[] trees = data.treeInstances;
            if (trees == null || trees.Length == 0) return 0;

            for (int i = 0; i < trees.Length; i++)
            {
                TreeInstance tr = trees[i];
                float variation = .90f + Hash01("terrain" + i) * .30f;
                tr.heightScale = Mathf.Clamp(tr.heightScale * 1.85f * variation, 1.35f, 2.75f);
                tr.widthScale = Mathf.Clamp(tr.widthScale * 1.65f * variation, 1.20f, 2.45f);
                trees[i] = tr;
            }

            data.treeInstances = trees;
            EditorUtility.SetDirty(data);
            return trees.Length;
        }

        static int CleanPowerPlantRoads(Terrain terrain, Transform naturalRoadRoot, Transform plant, Mats mats)
        {
            List<GameObject> remove = new List<GameObject>();
            List<Tuple<string, List<Vector3>, float>> rebuild = new List<Tuple<string, List<Vector3>, float>>();
            int detoured = 0;

            for (int i = 0; i < naturalRoadRoot.childCount; i++)
            {
                Transform road = naturalRoadRoot.GetChild(i);
                List<Vector3> path = RoadPath(road);
                if (path.Count < 2) continue;

                if (road.name.Contains("Industrial"))
                {
                    remove.Add(road.gameObject);
                    continue;
                }

                if (!PathEntersPlant(path, plant, 128f, 112f)) continue;

                List<Vector3> detour = DetourAroundPlant(terrain, path, plant, 155f, 138f);
                if (detour.Count >= 2)
                {
                    rebuild.Add(new Tuple<string, List<Vector3>, float>(road.name, detour, RoadWidth(road)));
                    remove.Add(road.gameObject);
                    detoured++;
                }
            }

            foreach (GameObject g in remove)
                if (g) UnityEngine.Object.DestroyImmediate(g);

            foreach (Tuple<string, List<Vector3>, float> r in rebuild)
                CreateRoad(terrain, naturalRoadRoot, r.Item1, r.Item2, r.Item3, mats);

            return detoured;
        }

        static bool PathEntersPlant(List<Vector3> path, Transform plant, float halfX, float halfZ)
        {
            foreach (Vector3 p in path)
            {
                Vector3 l = plant.InverseTransformPoint(p);
                if (Mathf.Abs(l.x) < halfX && Mathf.Abs(l.z) < halfZ)
                    return true;
            }
            return false;
        }

        static List<Vector3> DetourAroundPlant(Terrain terrain, List<Vector3> original, Transform plant, float radiusX, float radiusZ)
        {
            int first = -1;
            int last = -1;

            for (int i = 0; i < original.Count; i++)
            {
                Vector3 l = plant.InverseTransformPoint(original[i]);
                bool near = Mathf.Abs(l.x) < radiusX && Mathf.Abs(l.z) < radiusZ;
                if (near && first < 0) first = i;
                if (near) last = i;
            }

            if (first < 0) return original;

            int entryIndex = Mathf.Max(0, first - 8);
            int exitIndex = Mathf.Min(original.Count - 1, last + 8);
            Vector3 entry = original[entryIndex];
            Vector3 exit = original[exitIndex];
            Vector3 le = plant.InverseTransformPoint(entry);
            Vector3 lx = plant.InverseTransformPoint(exit);

            float a0 = Mathf.Atan2(le.z / radiusZ, le.x / radiusX);
            float a1 = Mathf.Atan2(lx.z / radiusZ, lx.x / radiusX);

            List<Vector3> cw = PlantArc(terrain, plant, a0, a1, -1, radiusX, radiusZ);
            List<Vector3> ccw = PlantArc(terrain, plant, a0, a1, 1, radiusX, radiusZ);
            List<Vector3> arc = PathLength(cw) <= PathLength(ccw) ? cw : ccw;

            if (arc.Count < 2) return original;

            List<Vector3> result = new List<Vector3>();
            for (int i = 0; i <= entryIndex; i++) result.Add(original[i]);

            Vector3 entryTan = Tangent(original, entryIndex);
            Vector3 arcTanStart = Tangent(arc, 0);
            AddBezier(result, terrain, entry, arc[0], entryTan, arcTanStart, 36f);

            for (int i = 1; i < arc.Count - 1; i++) result.Add(arc[i]);

            Vector3 arcTanEnd = Tangent(arc, arc.Count - 1);
            Vector3 exitTan = Tangent(original, exitIndex);
            AddBezier(result, terrain, arc[arc.Count - 1], exit, arcTanEnd, exitTan, 36f);

            for (int i = exitIndex + 1; i < original.Count; i++) result.Add(original[i]);
            return RemoveNearDuplicatePoints(result, 1.0f);
        }

        static List<Vector3> PlantArc(Terrain terrain, Transform plant, float start, float end, int direction, float rx, float rz)
        {
            float delta;
            if (direction > 0)
                delta = Mathf.Repeat((end - start) * Mathf.Rad2Deg, 360f) * Mathf.Deg2Rad;
            else
                delta = -Mathf.Repeat((start - end) * Mathf.Rad2Deg, 360f) * Mathf.Deg2Rad;

            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta) * Mathf.Max(rx, rz) / 8f), 10, 160);
            List<Vector3> path = new List<Vector3>();

            for (int i = 0; i <= steps; i++)
            {
                float a = start + delta * (i / (float)steps);
                Vector3 local = new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                Vector3 p = plant.TransformPoint(local);
                p.y = Ground(terrain, p) + .04f;
                path.Add(p);
            }

            return path;
        }

        static void AddBezier(List<Vector3> output, Terrain terrain, Vector3 a, Vector3 b, Vector3 tangentA, Vector3 tangentB, float tangentLength)
        {
            Vector3 c1 = a + tangentA.normalized * tangentLength;
            Vector3 c2 = b - tangentB.normalized * tangentLength;
            int steps = Mathf.Max(6, Mathf.CeilToInt(Planar(a, b) / 7f));

            for (int i = 1; i <= steps; i++)
            {
                float u = i / (float)steps;
                Vector3 p = CubicBezier(a, c1, c2, b, u);
                p.y = Ground(terrain, p) + .04f;
                output.Add(p);
            }
        }

        static Road BuildCleanPlantAccess(Terrain terrain, Transform naturalRoadRoot, Transform plant, List<Road> countyRoads, Mats mats)
        {
            Vector3 gate = plant.TransformPoint(new Vector3(0f, 0f, 101f));
            Vector3 branch = gate;
            Vector3 branchTangent = -plant.forward;
            float best = float.MaxValue;

            foreach (Road road in countyRoads)
            {
                if (road.name.Contains("Industrial")) continue;
                for (int i = 0; i < road.path.Count; i++)
                {
                    Vector3 local = plant.InverseTransformPoint(road.path[i]);
                    if (Mathf.Abs(local.x) < 165f && Mathf.Abs(local.z) < 150f) continue;
                    float d = Planar(road.path[i], gate);
                    if (d < best)
                    {
                        best = d;
                        branch = road.path[i];
                        branchTangent = Tangent(road.path, i);
                    }
                }
            }

            Vector3 toGate = gate - branch;
            toGate.y = 0f;
            if (Vector3.Dot(branchTangent, toGate) < 0f) branchTangent = -branchTangent;
            branchTangent.y = 0f;
            branchTangent.Normalize();

            Vector3 incoming = -plant.forward;
            incoming.y = 0f;
            incoming.Normalize();

            float length = Mathf.Max(1f, Planar(branch, gate));
            float t1 = Mathf.Clamp(length * .18f, 45f, 120f);
            float t2 = Mathf.Clamp(length * .16f, 42f, 105f);
            Vector3 c1 = branch + branchTangent * t1;
            Vector3 c2 = gate - incoming * t2;

            int count = Mathf.Clamp(Mathf.CeilToInt(length / 6f), 18, 220);
            List<Vector3> path = new List<Vector3>();

            for (int i = 0; i <= count; i++)
            {
                float u = i / (float)count;
                Vector3 p = CubicBezier(branch, c1, c2, gate, u);
                p.y = Ground(terrain, p) + .04f;
                path.Add(p);
            }

            return CreateRoad(terrain, naturalRoadRoot, "Clean Industrial Access Road - Power Station", path, 7.6f, mats);
        }

        static List<Road> CollectRoads(Transform root)
        {
            List<Road> roads = new List<Road>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = root.GetChild(i);
                List<Vector3> path = RoadPath(r);
                if (path.Count < 2) continue;
                roads.Add(new Road { name = r.name, root = r, width = RoadWidth(r), path = path });
            }
            return roads;
        }

        static Road CreateRoad(Terrain terrain, Transform parent, string name, List<Vector3> path, float width, Mats mats)
        {
            if (path == null || path.Count < 2) return null;
            Transform root = New(name, parent);
            New("Natural Road Marker", root);
            Ribbon(terrain, root, "Gravel Shoulder", path, width + 7.5f, mats.gravel, .04f, false);
            Ribbon(terrain, root, "Road Surface", path, width, mats.asphalt, .11f, true);
            Ribbon(terrain, root, "Center Line", path, .18f, mats.paint, .15f, false);
            return new Road { name = name, root = root, width = width, path = path };
        }

        static int BuildUtilities(Terrain terrain, Transform root, List<Road> roads, Mats mats)
        {
            int made = 0;

            foreach (Road road in roads)
            {
                List<Vector3> points = Resample(road.path, road.name.Contains("Industrial") ? 58f : 54f);
                Vector3[] previous = new Vector3[3];
                bool havePrevious = false;
                int sideSign = Hash01(road.name) > .5f ? 1 : -1;

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 p = points[i] + side * sideSign * (road.width * .5f + 10.5f);
                    p.y = Ground(terrain, p);

                    Transform pole = New("Final Utility Pole " + (++made).ToString("000"), root);
                    pole.position = p;
                    pole.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                    Cylinder(pole, "Pole Shaft", new Vector3(0, 4.3f, 0), new Vector3(.135f, 4.3f, .135f), mats.wood, false);
                    Box(pole, "Crossarm", new Vector3(0, 8.0f, 0), new Vector3(2.3f, .14f, .14f), mats.wood, false);

                    for (int k = -1; k <= 1; k++)
                    {
                        Cylinder(pole, "Insulator", new Vector3(k * .74f, 8.22f, 0), new Vector3(.055f, .16f, .055f), mats.white, false);
                        Vector3 conductor = pole.TransformPoint(new Vector3(k * .74f, 8.42f, 0));
                        if (havePrevious) Wire(root, previous[k + 1], conductor, mats.metal, "Final Utility Wire");
                        previous[k + 1] = conductor;
                    }
                    havePrevious = true;
                }
            }

            return made;
        }

        static int AddRoadMarkers(Terrain terrain, Transform root, List<Road> roads, Mats mats)
        {
            int made = 0;
            foreach (Road road in roads)
            {
                List<Vector3> points = Resample(road.path, 78f);
                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 p = points[i] + side * s * (road.width * .5f + 4.2f);
                        p.y = Ground(terrain, p);
                        Transform marker = New("Final Roadside Marker " + (++made).ToString("000"), root);
                        marker.position = p;
                        marker.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                        Box(marker, "Post", new Vector3(0, .50f, 0), new Vector3(.095f, 1.0f, .095f), mats.white, false);
                        Box(marker, "Reflector", new Vector3(0, .88f, -.06f), new Vector3(.15f, .17f, .035f), s < 0 ? mats.red : mats.white, false);
                    }
                }
            }
            return made;
        }

        static int AddGrass(Terrain terrain, Transform root, List<Road> roads)
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(GrassA);
            GameObject b = AssetDatabase.LoadAssetAtPath<GameObject>(GrassB);
            if (!a && !b) return 0;
            System.Random rng = new System.Random(Seed + 1);
            int made = 0;

            foreach (Road road in roads)
            {
                List<Vector3> points = Resample(road.path, 12f);
                for (int i = 0; i < points.Count && made < 1350; i++)
                {
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (rng.NextDouble() < .32) continue;
                        Vector3 p = points[i] + side * s * (road.width * .5f + Next(rng, 6f, 15f)) + tangent * Next(rng, -3f, 3f);
                        p.y = Ground(terrain, p);
                        GameObject src = rng.NextDouble() < .5 ? a : b;
                        if (!src) src = a ? a : b;
                        SpawnPrefab(src, root, p, rng, "Final Road Grass ", ref made, .8f, 1.45f, 0f);
                    }
                }
            }
            return made;
        }

        static int AddTrees(Terrain terrain, Transform root, List<Road> roads, Transform plant)
        {
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafTree);
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirTree);
            if (!leaf && !fir) return 0;
            System.Random rng = new System.Random(Seed + 2);
            int made = 0;

            foreach (Road road in roads)
            {
                List<Vector3> points = Resample(road.path, 72f);
                for (int i = 1; i < points.Count - 1 && made < 180; i++)
                {
                    if (rng.NextDouble() < .48) continue;
                    Vector3 tangent = Tangent(points, i);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    int s = rng.NextDouble() < .5 ? -1 : 1;
                    Vector3 p = points[i] + side * s * Next(rng, 25f, 52f) + tangent * Next(rng, -9f, 9f);
                    if (Planar(p, plant.position) < 185f) continue;
                    p.y = Ground(terrain, p);
                    GameObject src = rng.NextDouble() < .76 ? leaf : fir;
                    if (!src) src = leaf ? leaf : fir;
                    GameObject g = SpawnPrefab(src, root, p, rng, "Final Mature Tree ", ref made, 1f, 1f, 0f);
                    if (g)
                    {
                        float target = Next(rng, 11.5f, 17.5f);
                        ScaleToHeight(g.transform, target);
                    }
                }
            }
            return made;
        }

        static int ClearLaneIntrusions(Transform world, List<Road> roads, Terrain terrain, Transform plant)
        {
            string[] names =
            {
                "Town Tree", "Refinement Town Tree", "Yard Shrub", "Parked Car", "Roadside Bus Stop",
                "Street Lamp", "Fire Hydrant", "Town Gateway", "Asset Grass", "Utility Pole"
            };

            List<Transform> candidates = new List<Transform>();
            foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
            {
                bool match = false;
                foreach (string n in names)
                    if (t.name.Contains(n)) { match = true; break; }
                if (match) candidates.Add(t);
            }

            int moved = 0;
            foreach (Transform t in candidates)
            {
                Road road;
                Vector3 nearest;
                Vector3 tangent;
                float distance;
                if (!NearestRoadInfo(t.position, roads, out road, out nearest, out tangent, out distance)) continue;
                float required = road.width * .5f + 5.0f;
                if (distance >= required) continue;
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                float sign = Vector3.Dot(t.position - nearest, side) >= 0f ? 1f : -1f;
                Vector3 p = nearest + side * sign * (required + 3f);
                p.y = Ground(terrain, p);
                t.position = p;
                moved++;
            }
            return moved;
        }

        static bool NearestRoadInfo(Vector3 p, List<Road> roads, out Road road, out Vector3 nearest, out Vector3 tangent, out float distance)
        {
            road = null;
            nearest = p;
            tangent = Vector3.forward;
            distance = float.MaxValue;

            foreach (Road r in roads)
            {
                for (int i = 0; i < r.path.Count - 1; i++)
                {
                    Vector3 q = ClosestXZ(p, r.path[i], r.path[i + 1]);
                    float d = Planar(p, q);
                    if (d >= distance) continue;
                    distance = d;
                    road = r;
                    nearest = q;
                    Vector3 tan = r.path[i + 1] - r.path[i];
                    tan.y = 0f;
                    tangent = tan.sqrMagnitude < .001f ? Vector3.forward : tan.normalized;
                }
            }
            return road != null;
        }

        static void ConformRoads(Terrain terrain, Transform root)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf || !mf.sharedMesh) continue;
                string n = mf.gameObject.name;
                if (n != "Road Surface" && n != "Gravel Shoulder" && n != "Center Line") continue;
                float offset = n == "Center Line" ? .15f : n == "Road Surface" ? .11f : .04f;
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
                if (mc) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
            }
        }

        static int CountPlantRoadIntrusions(Transform naturalRoadRoot, Transform plant)
        {
            int bad = 0;
            for (int i = 0; i < naturalRoadRoot.childCount; i++)
            {
                Transform road = naturalRoadRoot.GetChild(i);
                if (road.name.Contains("Industrial Access")) continue;
                List<Vector3> path = RoadPath(road);
                foreach (Vector3 p in path)
                {
                    Vector3 l = plant.InverseTransformPoint(p);
                    if (Mathf.Abs(l.x) < 128f && Mathf.Abs(l.z) < 112f)
                    {
                        bad++;
                        break;
                    }
                }
            }
            return bad;
        }

        static int CountDirectRoadsContaining(Transform root, string text)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name.Contains(text)) count++;
            return count;
        }

        static RoadWidthInfo GetRoadWidthInfo(Transform road)
        {
            float width = RoadWidth(road);
            return new RoadWidthInfo { width = width };
        }

        struct RoadWidthInfo { public float width; }

        static float RoadWidth(Transform road)
        {
            Transform surface = FindChild(road, "Road Surface");
            MeshFilter mf = surface ? surface.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh || mf.sharedMesh.vertices.Length < 2) return 7.2f;
            Vector3 a = surface.TransformPoint(mf.sharedMesh.vertices[0]);
            Vector3 b = surface.TransformPoint(mf.sharedMesh.vertices[1]);
            return Mathf.Clamp(Planar(a, b), 4f, 12f);
        }

        static List<Vector3> RoadPath(Transform road)
        {
            Transform surface = FindChild(road, "Road Surface");
            MeshFilter mf = surface ? surface.GetComponent<MeshFilter>() : null;
            List<Vector3> path = new List<Vector3>();
            if (!mf || !mf.sharedMesh) return path;
            Vector3[] v = mf.sharedMesh.vertices;
            for (int i = 0; i + 1 < v.Length; i += 2)
                path.Add((surface.TransformPoint(v[i]) + surface.TransformPoint(v[i + 1])) * .5f);
            return path;
        }

        static void Ribbon(Terrain terrain, Transform parent, string name, List<Vector3> path, float width, Material material, float yOffset, bool collider)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            Mesh mesh = RibbonMesh(terrain, g.transform, path, width, yOffset, name);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            g.isStatic = true;
        }

        static Mesh RibbonMesh(Terrain terrain, Transform holder, List<Vector3> path, float width, float offset, string name)
        {
            int count = path.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[Mathf.Max(0, (count - 1) * 6)];
            float distance = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = Tangent(path, i);
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * width * .5f;
                Vector3 left = path[i] - side;
                Vector3 right = path[i] + side;
                left.y = Ground(terrain, left) + offset;
                right.y = Ground(terrain, right) + offset;
                if (i > 0) distance += Planar(path[i - 1], path[i]);

                vertices[i * 2] = holder.InverseTransformPoint(left);
                vertices[i * 2 + 1] = holder.InverseTransformPoint(right);
                uv[i * 2] = new Vector2(0f, distance / 8f);
                uv[i * 2 + 1] = new Vector2(1f, distance / 8f);

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

            Mesh mesh = new Mesh { name = "H51_110_" + Safe(name) + "_" + (meshId++).ToString("0000") };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Gen + "/Meshes/" + mesh.name + ".asset");
            return mesh;
        }

        static List<Vector3> Resample(List<Vector3> path, float step)
        {
            List<Vector3> output = new List<Vector3>();
            if (path == null || path.Count == 0) return output;
            output.Add(path[0]);
            for (int i = 0; i < path.Count - 1; i++)
            {
                float d = Planar(path[i], path[i + 1]);
                int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
                for (int k = 1; k <= n; k++) output.Add(Vector3.Lerp(path[i], path[i + 1], k / (float)n));
            }
            return output;
        }

        static Vector3 Tangent(List<Vector3> path, int i)
        {
            if (path == null || path.Count < 2) return Vector3.forward;
            Vector3 d = i == 0 ? path[1] - path[0] :
                        i == path.Count - 1 ? path[path.Count - 1] - path[path.Count - 2] :
                        path[i + 1] - path[i - 1];
            d.y = 0f;
            return d.sqrMagnitude < .001f ? Vector3.forward : d.normalized;
        }

        static List<Vector3> RemoveNearDuplicatePoints(List<Vector3> path, float minDistance)
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Vector3 p in path)
            {
                if (result.Count == 0 || Planar(result[result.Count - 1], p) >= minDistance)
                    result.Add(p);
            }
            return result;
        }

        static float PathLength(List<Vector3> path)
        {
            float d = 0f;
            for (int i = 0; i < path.Count - 1; i++) d += Planar(path[i], path[i + 1]);
            return d;
        }

        static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 ClosestXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 x = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            if (d.sqrMagnitude < .001f) return a;
            float u = Mathf.Clamp01(Vector2.Dot(q - x, d) / d.sqrMagnitude);
            Vector2 c = x + d * u;
            return new Vector3(c.x, Mathf.Lerp(a.y, b.y, u), c.y);
        }

        static GameObject SpawnPrefab(GameObject source, Transform root, Vector3 position, System.Random rng, string prefix, ref int made, float minScale, float maxScale, float yOffset)
        {
            if (!source) return null;
            GameObject g = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (!g) return null;
            g.name = prefix + (++made).ToString("0000");
            g.transform.SetParent(root, false);
            g.transform.position = position + Vector3.up * yOffset;
            g.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
            g.transform.localScale = Vector3.one * Next(rng, minScale, maxScale);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            return g;
        }

        static void ScaleToHeight(Transform root, float desiredHeight)
        {
            float current = RendererHeight(root);
            if (current <= .01f) return;
            float factor = Mathf.Clamp(desiredHeight / current, .5f, 4f);
            root.localScale *= factor;
        }

        static float RendererHeight(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.size.y;
        }

        static bool IsGeneratedTree(string name)
        {
            return name.StartsWith("Town Tree") ||
                   name.StartsWith("Refinement Town Tree") ||
                   name.StartsWith("Shelter Belt Tree") ||
                   name.StartsWith("Natural Road Tree") ||
                   name.StartsWith("Regional Tree");
        }

        static float Hash01(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < text.Length; i++) h = (h ^ text[i]) * 16777619u;
                h ^= h >> 13;
                h *= 1274126177u;
                return (h & 0x00ffffffu) / 16777215f;
            }
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (mat) g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
            return g;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (mat) g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
            return g;
        }

        static void Wire(Transform parent, Vector3 a, Vector3 b, Material mat, string name)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < .01f) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = (a + b) * .5f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.transform.localScale = new Vector3(.022f, d.magnitude * .5f, .022f);
            if (mat) g.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
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

        static int Count(Transform root, string text)
        {
            if (!root) return 0;
            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.Contains(text)) count++;
            return count;
        }

        static void RemoveDirectChild(Transform root, string name)
        {
            Transform child = DirectChild(root, name);
            if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static string Safe(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        static void ResetFolder()
        {
            if (AssetDatabase.IsValidFolder(Gen)) AssetDatabase.DeleteAsset(Gen);
            Ensure(Gen + "/Meshes");
            Ensure(Gen + "/Textures");
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
