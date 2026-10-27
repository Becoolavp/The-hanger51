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
                int sceneTreesRemoved = ClearSceneTreesNearFarmSites(world.transform, sites);
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
                    $"Step 120 complete. scene trees rescaled={treesRescaled}, scene-tree collider repairs={treeColliderRepairs}, remaining scene-tree collider failures={treeColliderFailures}, working farms={sites.Count}, barns={barns}, silos={silos}, tractors={tractors}, combines={combines}, hay bales={hayBales}, fence sections={fences}, farm lanes={lanes}, terrain trees removed from farmyards={terrainTreesRemoved}, scene trees removed from farmyards={sceneTreesRemoved}, detail cells cleared from farmyards={detailCellsCleared}, larger field-edge trees added/upgraded={shelterTrees}. Run Step 121 to validate.",
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
                if (distance < edgeDistance + 18f || distance > edgeDistance + 520f) continue;

                float yardDistance = Mathf.Max(25f, edgeDistance - 46f);
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
                    if (Planar(site.yard, used.yard) < 280f) { close = true; break; }
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

        static void BuildTractor(Transform farm, Vector3 pos, Quaternion rot, Mats m)
        {
            Transform t = New("Farm Tractor", farm);
            t.localPosition = pos;
            t.localRotation = rot;
            Box(t, "Farm Equipment Tractor Body", new Vector3(0f, 1.25f, 0f), new Vector3(2.6f, 1.5f, 4.6f), m.green, true);
            Box(t, "Farm Equipment Tractor Hood", new Vector3(0f, 1.45f, -2.4f), new Vector3(2.3f, 1.1f, 2.1f), m.green, true);
            Box(t, "Farm Equipment Tractor Cab", new Vector3(0f, 2.45f, .55f), new Vector3(2.1f, 2.2f, 2.3f), m.glass, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder(t, "Farm Equipment Tractor Rear Wheel", new Vector3(side * 1.55f, 1.05f, 1.15f),
                    new Vector3(.95f, .38f, .95f), m.rubber, true, Quaternion.Euler(0f, 0f, 90f));
                Cylinder(t, "Farm Equipment Tractor Front Wheel", new Vector3(side * 1.28f, .78f, -1.85f),
                    new Vector3(.67f, .30f, .67f), m.rubber, true, Quaternion.Euler(0f, 0f, 90f));
            }
            Cylinder(t, "Farm Equipment Exhaust", new Vector3(.72f, 2.6f, -1.5f), new Vector3(.10f, .95f, .10f), m.metal, true, null);
        }

        static void BuildCombine(Transform farm, Vector3 pos, Quaternion rot, Mats m)
        {
            Transform c = New("Combine Harvester", farm);
            c.localPosition = pos;
            c.localRotation = rot;
            Box(c, "Farm Equipment Combine Body", new Vector3(0f, 2.0f, 0f), new Vector3(4.8f, 3.5f, 7.6f), m.green, true);
            Box(c, "Farm Equipment Combine Cab", new Vector3(0f, 3.6f, -1.9f), new Vector3(3.7f, 2.6f, 2.5f), m.glass, true);
            Box(c, "Farm Equipment Grain Tank", new Vector3(0f, 4.0f, 1.7f), new Vector3(4.2f, 1.5f, 3.1f), m.green, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder(c, "Farm Equipment Combine Main Wheel", new Vector3(side * 2.65f, 1.3f, -1.4f),
                    new Vector3(1.35f, .48f, 1.35f), m.rubber, true, Quaternion.Euler(0f, 0f, 90f));
                Cylinder(c, "Farm Equipment Combine Rear Wheel", new Vector3(side * 2.15f, .95f, 2.55f),
                    new Vector3(.85f, .38f, .85f), m.rubber, true, Quaternion.Euler(0f, 0f, 90f));
            }
            Box(c, "Farm Equipment Header Bar", new Vector3(0f, 1.2f, -5.0f), new Vector3(8.5f, .45f, 1.1f), m.metal, true);
            for (int i = -4; i <= 4; i++)
                Box(c, "Farm Equipment Header Tine", new Vector3(i * .95f, .75f, -5.55f), new Vector3(.10f, .65f, .7f), m.metal, true);
        }

        static void BuildEquipmentCluster(Transform farm, Vector3 basePos, Mats m)
        {
            Transform e = New("Farm Equipment Yard", farm);
            e.localPosition = basePos;

            Box(e, "Farm Equipment Flatbed Wagon", new Vector3(0f, .8f, 0f), new Vector3(5.8f, .35f, 2.5f), m.wood, true);
            for (int side = -1; side <= 1; side += 2)
                Cylinder(e, "Farm Equipment Wagon Wheel", new Vector3(side * 2.15f, .55f, 0f),
                    new Vector3(.55f, .22f, .55f), m.rubber, true, Quaternion.Euler(0f, 0f, 90f));

            Box(e, "Farm Equipment Plow Frame", new Vector3(0f, .65f, 4.0f), new Vector3(4.8f, .22f, 1.3f), m.metal, true);
            for (int i = -2; i <= 2; i++)
                Box(e, "Farm Equipment Plow Blade", new Vector3(i * .9f, .30f, 4.45f), new Vector3(.12f, .65f, .8f), m.metal, true, Quaternion.Euler(0f, 0f, -22f));

            Cylinder(e, "Farm Equipment Seed Tank", new Vector3(0f, 1.35f, -4.0f), new Vector3(1.15f, 1.35f, 1.15f), m.yellow, true, null);
            Box(e, "Farm Equipment Seeder Frame", new Vector3(0f, .45f, -4.0f), new Vector3(5.0f, .22f, 1.4f), m.metal, true);
        }

        static void BuildFarmFences(Transform farm, Mats m, ref int fences)
        {
            float halfX = 34f;
            float halfZ = 29f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = -3; i <= 3; i++)
                {
                    Box(farm, "Farm Fence Rail", new Vector3(i * 9f, 1.0f, side * halfZ),
                        new Vector3(8.3f, .14f, .14f), m.wood, true);
                    Box(farm, "Farm Fence Rail", new Vector3(i * 9f, 1.75f, side * halfZ),
                        new Vector3(8.3f, .14f, .14f), m.wood, true);
                    fences += 2;
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = -2; i <= 2; i++)
                {
                    Box(farm, "Farm Fence Rail", new Vector3(side * halfX, 1.0f, i * 10f),
                        new Vector3(.14f, .14f, 9.3f), m.wood, true);
                    Box(farm, "Farm Fence Rail", new Vector3(side * halfX, 1.75f, i * 10f),
                        new Vector3(.14f, .14f, 9.3f), m.wood, true);
                    fences += 2;
                }
            }

            for (int x = -3; x <= 3; x++)
                for (int z = -1; z <= 1; z += 2)
                    Box(farm, "Farm Fence Post", new Vector3(x * 9f, 1.0f, z * halfZ),
                        new Vector3(.22f, 2f, .22f), m.wood, true);

            for (int z = -2; z <= 2; z++)
                for (int x = -1; x <= 1; x += 2)
                    Box(farm, "Farm Fence Post", new Vector3(x * halfX, 1.0f, z * 10f),
                        new Vector3(.22f, 2f, .22f), m.wood, true);

            Box(farm, "Farm Gate Left", new Vector3(-3.2f, 1.2f, -halfZ), new Vector3(5.5f, 1.6f, .18f), m.metal, true);
            Box(farm, "Farm Gate Right", new Vector3(3.2f, 1.2f, -halfZ), new Vector3(5.5f, 1.6f, .18f), m.metal, true);
        }

        static void BuildWaterAndFuel(Transform farm, Mats m)
        {
            Cylinder(farm, "Farm Water Tank", new Vector3(28f, 2.1f, 4f), new Vector3(2.6f, 2.1f, 2.6f), m.metal, true, null);
            Cylinder(farm, "Farm Fuel Tank", new Vector3(27f, 1.25f, -3.5f), new Vector3(1.2f, 3.3f, 1.2f), m.white, true, Quaternion.Euler(0f, 0f, 90f));
            Box(farm, "Farm Fuel Pump", new Vector3(23f, 1.1f, -4f), new Vector3(.8f, 2.2f, .8f), m.red, true);
            Box(farm, "Livestock Water Trough", new Vector3(-25f, .55f, -9f), new Vector3(5.5f, 1.1f, 1.8f), m.metal, true);
        }

        static void BuildFarmAccessLane(Terrain terrain, Transform farm, Vector3 from, Vector3 to, Mats m)
        {
            Vector3 a = from;
            Vector3 b = to;
            a.y = Ground(terrain, a) + .04f;
            b.y = Ground(terrain, b) + .04f;
            float distance = Planar(a, b);
            int segments = Mathf.Max(1, Mathf.CeilToInt(distance / 18f));

            Transform laneRoot = New("Farm Access Lane", farm.parent);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector3 p0 = Vector3.Lerp(a, b, t0);
                Vector3 p1 = Vector3.Lerp(a, b, t1);
                p0.y = Ground(terrain, p0) + .04f;
                p1.y = Ground(terrain, p1) + .04f;
                Vector3 d = p1 - p0;
                Vector3 mid = (p0 + p1) * .5f;
                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Farm Access Lane Surface";
                strip.transform.SetParent(laneRoot, false);
                strip.transform.position = mid;
                strip.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z).normalized, Vector3.up);
                strip.transform.localScale = new Vector3(5.4f, .08f, Mathf.Max(.5f, d.magnitude));
                strip.GetComponent<Renderer>().sharedMaterial = m.gravel;
                UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
                strip.isStatic = true;
            }
        }

        static int ClearTerrainTreesNearFarmSites(Terrain terrain, List<FarmSite> sites)
        {
            TerrainData data = terrain.terrainData;
            TreeInstance[] source = data.treeInstances;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            List<TreeInstance> kept = new List<TreeInstance>(source.Length);
            int removed = 0;

            foreach (TreeInstance tree in source)
            {
                Vector3 p = new Vector3(origin.x + tree.position.x * size.x, 0f, origin.z + tree.position.z * size.z);
                bool kill = false;
                foreach (FarmSite site in sites)
                {
                    if (Planar(p, site.yard) < 58f || SegmentDistance(p, site.yard, site.nearestRoad) < 8f)
                    {
                        kill = true;
                        break;
                    }
                }
                if (kill) removed++;
                else kept.Add(tree);
            }

            data.treeInstances = kept.ToArray();
            return removed;
        }

        static int ClearSceneTreesNearFarmSites(Transform world, List<FarmSite> sites)
        {
            List<GameObject> remove = new List<GameObject>();
            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (!IsManagedTreeRoot(tr.name)) continue;
                foreach (FarmSite site in sites)
                {
                    if (Planar(tr.position, site.yard) < 62f || SegmentDistance(tr.position, site.yard, site.nearestRoad) < 9f)
                    {
                        remove.Add(tr.gameObject);
                        break;
                    }
                }
            }

            foreach (GameObject g in remove)
                if (g) UnityEngine.Object.DestroyImmediate(g);
            return remove.Count;
        }

        static int ClearTerrainDetailsNearFarmSites(Terrain terrain, List<FarmSite> sites)
        {
            TerrainData data = terrain.terrainData;
            if (data.detailPrototypes == null || data.detailPrototypes.Length == 0) return 0;
            int w = data.detailWidth;
            int h = data.detailHeight;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int cleared = 0;

            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                int[,] map = data.GetDetailLayer(0, 0, w, h, layer);
                for (int z = 0; z < h; z++)
                {
                    float wz = origin.z + z / (float)Mathf.Max(1, h - 1) * size.z;
                    for (int x = 0; x < w; x++)
                    {
                        if (map[z, x] == 0) continue;
                        float wx = origin.x + x / (float)Mathf.Max(1, w - 1) * size.x;
                        Vector3 p = new Vector3(wx, 0f, wz);
                        bool clear = false;
                        foreach (FarmSite site in sites)
                        {
                            if (Planar(p, site.yard) < 50f || SegmentDistance(p, site.yard, site.nearestRoad) < 5.5f)
                            {
                                clear = true;
                                break;
                            }
                        }
                        if (clear)
                        {
                            map[z, x] = 0;
                            cleared++;
                        }
                    }
                }
                data.SetDetailLayer(0, 0, layer, map);
            }
            return cleared;
        }

        static int UpgradeAndExpandFieldShelterBelts(Terrain terrain, Transform fieldRoot, List<Road> roads)
        {
            int touched = 0;

            foreach (Transform tr in fieldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith("Field Edge Shelter Tree")) continue;
                float h = RendererHeight(tr);
                if (h <= .01f) continue;
                float target = Mathf.Lerp(12.5f, 17.5f, Hash01(tr.name + tr.position.x.ToString("0.0")));
                float factor = Mathf.Clamp(target / h, .5f, 6f);
                tr.localScale *= factor;
                Vector3 p = tr.position;
                p.y = Ground(terrain, p);
                tr.position = p;
                EnableExistingColliders(tr);
                if (!HasSolidCollider(tr)) AddFallbackTrunkCollider(tr);
                touched++;
            }

            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab");
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab");
            if (!leaf || !fir) return touched;

            Transform previousExtra = DirectChild(fieldRoot, "Expanded Mature Field Shelter Belts");
            if (previousExtra) UnityEngine.Object.DestroyImmediate(previousExtra.gameObject);

            List<FieldParcel> fields = ReadFields(fieldRoot);
            System.Random rng = new System.Random(Seed + 77);
            Transform extraRoot = New("Expanded Mature Field Shelter Belts", fieldRoot);

            int extra = 0;
            for (int f = 0; f < fields.Count; f++)
            {
                if (f % 3 != 0) continue;
                FieldParcel field = fields[f];
                float half = field.size.x * .5f;
                int count = Mathf.Clamp(Mathf.RoundToInt(field.size.x / 38f), 5, 14);
                for (int i = 0; i <= count; i++)
                {
                    float x = Mathf.Lerp(-half, half, i / (float)count);
                    Vector3 p = FieldLocalToWorld(field, new Vector2(x, field.size.y * .5f + 18f));
                    if (NearRoad(p, roads, 11f)) continue;
                    p.y = Ground(terrain, p);

                    GameObject src = rng.NextDouble() < .7 ? leaf : fir;
                    GameObject g = PrefabUtility.InstantiatePrefab(src) as GameObject;
                    if (!g) continue;
                    g.name = "Field Edge Shelter Tree Extra " + (++extra).ToString("0000");
                    g.transform.SetParent(extraRoot, false);
                    g.transform.position = p;
                    g.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
                    float current = RendererHeight(g.transform);
                    if (current > .01f) g.transform.localScale *= Mathf.Lerp(12.5f, 17.5f, (float)rng.NextDouble()) / current;
                    EnableExistingColliders(g.transform);
                    if (!HasSolidCollider(g.transform)) AddFallbackTrunkCollider(g.transform);
                    touched++;
                }
            }

            return touched;
        }

        static Mats LoadMaterials()
        {
            return new Mats
            {
                red = LoadMat("Barn_Red"),
                green = LoadMat("Farm_Green"),
                white = LoadMat("Warm_White"),
                roof = LoadMat("Utility_Roof"),
                metal = LoadMat("Dark_Metal"),
                wood = LoadMat("Weathered_Wood"),
                gravel = LoadMat("Matte_Gravel"),
                dirt = LoadMat("Packed_Dirt"),
                concrete = LoadMat("Concrete"),
                rubber = LoadMat("Rubber"),
                glass = LoadMat("Dark_Glass"),
                yellow = LoadMat("Road_Paint")
            };
        }

        static Material LoadMat(string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(BaseMat + name + ".mat");
            if (m)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", Color.black);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        static int CountFarmColliderFailures(Transform farms)
        {
            int bad = 0;
            foreach (Transform tr in farms.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                bool shouldBeSolid =
                    n == "Main Barn" ||
                    n == "Machine Shed" ||
                    n == "Farm House" ||
                    n == "Grain Silo" ||
                    n == "Farm Tractor" ||
                    n == "Combine Harvester" ||
                    n.StartsWith("Hay Bale") ||
                    n.StartsWith("Farm Fence") ||
                    (n.StartsWith("Farm Equipment") && !n.EndsWith("Yard")) ||
                    n == "Farm Water Tank" ||
                    n == "Farm Fuel Tank";
                if (!shouldBeSolid) continue;
                Collider col = tr.GetComponent<Collider>();
                if (!col) col = tr.GetComponentInChildren<Collider>(true);
                if (!col || !col.enabled || col.isTrigger) bad++;
            }
            return bad;
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

        static Vector3 ClosestXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 av = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            float t = d.sqrMagnitude < .001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - av, d) / d.sqrMagnitude);
            Vector2 r = av + d * t;
            return new Vector3(r.x, Mathf.Lerp(a.y, b.y, t), r.y);
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

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 q = new Vector2(p.x, p.z);
            Vector2 av = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            if (d.sqrMagnitude < .001f) return Vector2.Distance(q, av);
            return Vector2.Distance(q, av + d * Mathf.Clamp01(Vector2.Dot(q - av, d) / d.sqrMagnitude));
        }

        static Vector3 FieldLocalToWorld(FieldParcel field, Vector2 local)
        {
            float radians = field.rotation * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return field.center + new Vector3(local.x * c - local.y * s, 0f, local.x * s + local.y * c);
        }

        static float RendererHeight(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.size.y;
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

        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collider, Quaternion? localRotation = null)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = localScale;
            if (localRotation.HasValue) g.transform.localRotation = localRotation.Value;
            Renderer renderer = g.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider col = g.GetComponent<Collider>();
            if (!collider && col) UnityEngine.Object.DestroyImmediate(col);
            else if (col) { col.enabled = true; col.isTrigger = false; }
            g.isStatic = true;
            return g;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collider, Quaternion? localRotation)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = localScale;
            if (localRotation.HasValue) g.transform.localRotation = localRotation.Value;
            Renderer renderer = g.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = material;
            Collider col = g.GetComponent<Collider>();
            if (!collider && col) UnityEngine.Object.DestroyImmediate(col);
            else if (col) { col.enabled = true; col.isTrigger = false; }
            g.isStatic = true;
            return g;
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        static float Next(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
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
            GameObject direct = GameObject.Find(name);
            if (direct) return direct;
            foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tr && tr.gameObject.scene.IsValid() && tr.name == name) return tr.gameObject;
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (!root) return null;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        static Transform DirectChild(Transform root, string name)
        {
            if (!root) return null;
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

        static int Count(Transform root, string namePart)
        {
            int count = 0;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr != root && tr.name.Contains(namePart)) count++;
            return count;
        }
    }
}
