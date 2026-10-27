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

        static void NormalizeAllSceneTrees(Transform world, Terrain terrain, out int rescaled, out int colliderRepairs, out int colliderFailures)
        {
            rescaled = 0;
            colliderRepairs = 0;
            colliderFailures = 0;

            foreach (Transform tree in world.GetComponentsInChildren<Transform>(true))
            {
                if (!IsManagedTreeRoot(tree.name)) continue;
                float height = RendererHeight(tree);
                if (height <= .01f) continue;

                float target;
                if (tree.name.StartsWith("Town Tree") || tree.name.StartsWith("Refinement Town Tree"))
                    target = Mathf.Lerp(10.5f, 14.5f, Hash01(tree.name + tree.position.x.ToString("0.0")));
                else if (tree.name.StartsWith("Field Edge Shelter Tree"))
                    target = Mathf.Lerp(12.0f, 17.0f, Hash01(tree.name + tree.position.z.ToString("0.0")));
                else
                    target = Mathf.Lerp(12.0f, 18.0f, Hash01(tree.name + tree.position.x.ToString("0.0") + tree.position.z.ToString("0.0")));

                if (height < target * .92f || height > target * 1.12f)
                {
                    float factor = Mathf.Clamp(target / height, .45f, 6.0f);
                    tree.localScale *= factor;
                    rescaled++;
                }

                Vector3 p = tree.position;
                p.y = Ground(terrain, p);
                tree.position = p;

                if (!HasSolidCollider(tree))
                {
                    EnableExistingColliders(tree);
                    if (!HasSolidCollider(tree))
                        AddFallbackTrunkCollider(tree);
                    colliderRepairs++;
                }

                if (!HasSolidCollider(tree)) colliderFailures++;
            }
        }

        static bool IsManagedTreeRoot(string name)
        {
            return name.StartsWith("Town Tree") ||
                   name.StartsWith("Refinement Town Tree") ||
                   name.StartsWith("Shelter Belt Tree") ||
                   name.StartsWith("Natural Road Tree") ||
                   name.StartsWith("Regional Tree") ||
                   name.StartsWith("Final Mature Tree") ||
                   name.StartsWith("Field Edge Shelter Tree");
        }

        static void EnableExistingColliders(Transform root)
        {
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = true;
                col.isTrigger = false;
            }
        }

        static bool HasSolidCollider(Transform root)
        {
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
                if (col && col.enabled && !col.isTrigger) return true;
            return false;
        }

        static void AddFallbackTrunkCollider(Transform tree)
        {
            Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float trunkHeightWorld = Mathf.Clamp(b.size.y * .34f, 2.8f, 6.2f);
            float trunkRadiusWorld = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * .055f, .24f, .62f);
            Vector3 centerWorld = new Vector3(b.center.x, b.min.y + trunkHeightWorld * .5f, b.center.z);
            Vector3 centerLocal = tree.InverseTransformPoint(centerWorld);
            Vector3 lossy = tree.lossyScale;
            float radialScale = Mathf.Max(.001f, (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.z)) * .5f);
            float verticalScale = Mathf.Max(.001f, Mathf.Abs(lossy.y));

            CapsuleCollider capsule = tree.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = centerLocal;
            capsule.radius = trunkRadiusWorld / radialScale;
            capsule.height = Mathf.Max(capsule.radius * 2f, trunkHeightWorld / verticalScale);
            capsule.isTrigger = false;
        }

        static List<FieldParcel> ReadFields(Transform root)
        {
            List<FieldParcel> fields = new List<FieldParcel>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform marker = root.GetChild(i);
                if (!marker.name.StartsWith("Field Parcel ")) continue;
                int crop = marker.name.Contains("Green Crop") ? 0 : marker.name.Contains("Golden Grain") ? 1 : 2;
                fields.Add(new FieldParcel
                {
                    marker = marker,
                    center = marker.position,
                    size = new Vector2(Mathf.Abs(marker.localScale.x), Mathf.Abs(marker.localScale.z)),
                    rotation = marker.eulerAngles.y,
                    cropType = crop,
                    name = marker.name
                });
            }
            return fields;
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

                Road road = new Road();
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

        static List<FarmSite> ChooseFarmSites(Terrain terrain, List<FieldParcel> fields, List<Road> roads, int target)
        {
            List<FarmSite> candidates = new List<FarmSite>();
            foreach (FieldParcel field in fields)
            {
                Road nearestRoad;
                Vector3 nearest;
                Vector3 tangent;
                float distance;
                if (!NearestRoadInfo(field.center, roads, out nearestRoad, out nearest, out tangent, out distance)) continue;

                Vector3 dir = nearest - field.center;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1f) continue;
                dir.Normalize();

                float edgeDistance = DistanceToFieldEdge(field, dir);
                if (distance < edgeDistance + 45f || distance > edgeDistance + 360f) continue;

                float yardDistance = edgeDistance + Mathf.Min(55f, Mathf.Max(32f, (distance - edgeDistance) * .30f));
                Vector3 yard = field.center + dir * yardDistance;
                yard.y = Ground(terrain, yard);

                candidates.Add(new FarmSite
                {
                    field = field,
                    yard = yard,
                    nearestRoad = nearest,
                    roadTangent = tangent,
                    roadDistance = Vector3.Distance(yard, nearest)
                });
            }

            candidates.Sort((a, b) => a.roadDistance.CompareTo(b.roadDistance));
            List<FarmSite> chosen = new List<FarmSite>();
            foreach (FarmSite site in candidates)
            {
                bool close = false;
                foreach (FarmSite used in chosen)
                    if (Planar(site.yard, used.yard) < 360f) { close = true; break; }
                if (close) continue;
                chosen.Add(site);
                if (chosen.Count >= target) break;
            }
            return chosen;
        }

        static float DistanceToFieldEdge(FieldParcel field, Vector3 worldDir)
        {
            float radians = -field.rotation * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            Vector2 d = new Vector2(worldDir.x * c - worldDir.z * s, worldDir.x * s + worldDir.z * c);
            float tx = Mathf.Abs(d.x) < .001f ? float.MaxValue : field.size.x * .5f / Mathf.Abs(d.x);
            float tz = Mathf.Abs(d.y) < .001f ? float.MaxValue : field.size.y * .5f / Mathf.Abs(d.y);
            return Mathf.Min(tx, tz);
        }

        static void BuildWorkingFarm(
            Terrain terrain, Transform parent, FarmSite site, int index, Mats m,
            ref int barns, ref int silos, ref int tractors, ref int combines,
            ref int hayBales, ref int fences, ref int lanes)
        {
            Vector3 forward = site.nearestRoad - site.yard;
            forward.y = 0f;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            forward.Normalize();

            Transform farm = New("Working Farm " + (index + 1).ToString("00") + " - " + site.field.name, parent);
            farm.position = site.yard;
            farm.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Box(farm, "Farm Gravel Yard", new Vector3(0f, .03f, 0f), new Vector3(64f, .06f, 54f), m.gravel, false);

            BuildBarn(farm, new Vector3(-14f, 0f, 8f), m);
            barns++;

            BuildMachineShed(farm, new Vector3(16f, 0f, -10f), m);
            BuildFarmhouse(farm, new Vector3(-18f, 0f, -17f), m);

            BuildSilo(farm, new Vector3(20f, 0f, 15f), m);
            silos++;

            if (index % 2 == 0)
            {
                BuildSilo(farm, new Vector3(11f, 0f, 18f), m);
                silos++;
            }

            BuildTractor(farm, new Vector3(6f, 0f, -3f), Quaternion.Euler(0f, 12f, 0f), m);
            tractors++;

            if (index % 3 == 0)
            {
                BuildCombine(farm, new Vector3(3f, 0f, 14f), Quaternion.Euler(0f, -18f, 0f), m);
                combines++;
            }

            BuildEquipmentCluster(farm, new Vector3(22f, 0f, -20f), m);

            int baleCount = 8 + (index % 5);
            for (int i = 0; i < baleCount; i++)
            {
                float x = -29f + (i % 4) * 4.2f;
                float z = 20f + (i / 4) * 3.8f;
                Cylinder(farm, "Hay Bale " + i.ToString("00"), new Vector3(x, .85f, z),
                    new Vector3(.95f, .80f, .95f), m.dirt, true, Quaternion.Euler(0f, 0f, 90f));
                hayBales++;
            }

            BuildFarmFences(farm, m, ref fences);
            BuildWaterAndFuel(farm, m);

            BuildFarmAccessLane(terrain, farm, site.yard + forward * 28f, site.nearestRoad, m);
            lanes++;
        }

        static void BuildBarn(Transform farm, Vector3 basePos, Mats m)
        {
            Box(farm, "Main Barn", basePos + new Vector3(0f, 4.5f, 0f), new Vector3(18f, 9f, 27f), m.red, true);
            Box(farm, "Barn Roof Left", basePos + new Vector3(-4.5f, 9.5f, 0f), new Vector3(10f, .65f, 29f), m.roof, false, Quaternion.Euler(0f, 0f, -24f));
            Box(farm, "Barn Roof Right", basePos + new Vector3(4.5f, 9.5f, 0f), new Vector3(10f, .65f, 29f), m.roof, false, Quaternion.Euler(0f, 0f, 24f));
            Box(farm, "Barn Door Left", basePos + new Vector3(-2.7f, 3.2f, -13.58f), new Vector3(5.0f, 6.4f, .22f), m.wood, true);
            Box(farm, "Barn Door Right", basePos + new Vector3(2.7f, 3.2f, -13.58f), new Vector3(5.0f, 6.4f, .22f), m.wood, true);
            Box(farm, "Barn Loft Door", basePos + new Vector3(0f, 7.4f, -13.62f), new Vector3(3.3f, 2.8f, .18f), m.white, false);
        }

        static void BuildMachineShed(Transform farm, Vector3 basePos, Mats m)
        {
            Box(farm, "Machine Shed", basePos + new Vector3(0f, 3.4f, 0f), new Vector3(20f, 6.8f, 16f), m.green, true);
            Box(farm, "Machine Shed Roof", basePos + new Vector3(0f, 7.0f, 0f), new Vector3(21.5f, .55f, 17.5f), m.roof, false);
            for (int i = -1; i <= 1; i++)
                Box(farm, "Machine Shed Bay Door", basePos + new Vector3(i * 5.8f, 2.6f, -8.12f), new Vector3(4.8f, 5.2f, .18f), m.metal, false);
        }

        static void BuildFarmhouse(Transform farm, Vector3 basePos, Mats m)
        {
            Box(farm, "Farm House", basePos + new Vector3(0f, 2.7f, 0f), new Vector3(12f, 5.4f, 10f), m.white, true);
            Box(farm, "Farm House Roof Left", basePos + new Vector3(-3f, 5.8f, 0f), new Vector3(7f, .5f, 11.5f), m.roof, false, Quaternion.Euler(0f, 0f, -25f));
            Box(farm, "Farm House Roof Right", basePos + new Vector3(3f, 5.8f, 0f), new Vector3(7f, .5f, 11.5f), m.roof, false, Quaternion.Euler(0f, 0f, 25f));
            Box(farm, "Farm House Porch", basePos + new Vector3(0f, .35f, -6.0f), new Vector3(9f, .7f, 3.5f), m.wood, true);
        }

        static void BuildSilo(Transform farm, Vector3 basePos, Mats m)
        {
            Cylinder(farm, "Grain Silo", basePos + new Vector3(0f, 6.5f, 0f), new Vector3(4.1f, 6.5f, 4.1f), m.metal, true, null);
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Grain Silo Roof";
            cap.transform.SetParent(farm, false);
            cap.transform.localPosition = basePos + new Vector3(0f, 13.1f, 0f);
            cap.transform.localScale = new Vector3(8.4f, 2.3f, 8.4f);
            cap.GetComponent<Renderer>().sharedMaterial = m.roof;
            UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.isStatic = true;
        }
