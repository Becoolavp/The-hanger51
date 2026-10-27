using System;
using System.Collections.Generic;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldTreeAlignmentAndAgriculturePass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string AirportName = "Hanger 51 Airport Complex";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string PassName = "Hanger 51 Agricultural Land Use And Tree Alignment";
        const string FieldRootName = "Agricultural Field Parcels";
        const string Gen = "Assets/_Project/Environment/Generated/CountrysideAgriculturePass";
        const string LeafSource = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string FirSource = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string LeafGenerated = Gen + "/Prefabs/H51_TerrainTree_Leaf_NoNativeCollider.prefab";
        const string FirGenerated = Gen + "/Prefabs/H51_TerrainTree_Fir_NoNativeCollider.prefab";
        const int Seed = 51116;

        class Road
        {
            public float width;
            public List<Vector3> path = new List<Vector3>();
        }

        class FieldParcel
        {
            public Vector3 center;
            public Vector2 size;
            public float rotation;
            public int cropType;
            public string name;
        }

        [MenuItem("Hanger 51/World/Current/116 - Align Tree Hitboxes And Build Farmland")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 116 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51WorldDenseCollisionAndRadiatorRepair.Build();

            GameObject world = Find(WorldName);
            GameObject airport = Find(AirportName);
            Terrain terrain = FindTerrain();
            if (!world || !airport || !terrain)
            {
                Debug.LogError("Step 116 could not find the current world, airport, or editable terrain.");
                return;
            }

            Transform roadsRoot = FindChild(world.transform, "Road Network");
            if (!roadsRoot)
            {
                Debug.LogError("Step 116 could not find the road network.", world);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Creating collider-safe tree prototypes", .05f);
                ResetFolder();
                GameObject leaf = CreateColliderlessTreePrefab(LeafSource, LeafGenerated);
                GameObject fir = CreateColliderlessTreePrefab(FirSource, FirGenerated);
                if (!leaf || !fir)
                {
                    Debug.LogError("Step 116 could not create the colliderless terrain-tree visual prefabs.");
                    return;
                }

                TerrainData data = terrain.terrainData;
                data.treePrototypes = new[]
                {
                    new TreePrototype { prefab = fir, bendFactor = .25f },
                    new TreePrototype { prefab = leaf, bendFactor = .32f }
                };

                TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
                if (terrainCollider)
                {
                    SerializedObject so = new SerializedObject(terrainCollider);
                    SerializedProperty enableTrees = so.FindProperty("m_EnableTreeColliders");
                    if (enableTrees != null) enableTrees.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(terrainCollider);
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Planning realistic field parcels", .18f);
                List<Road> roads = CollectRoads(roadsRoot);
                List<Bounds> exclusions = CollectExclusions(world.transform, airport.transform);
                List<FieldParcel> fields = PlanFields(terrain, roads, exclusions, 64);

                Transform oldPass = FindChild(world.transform, PassName);
                if (oldPass) UnityEngine.Object.DestroyImmediate(oldPass.gameObject);
                Transform pass = New(PassName, world.transform);
                Transform fieldRoot = New(FieldRootName, pass);
                BuildFieldMarkers(fieldRoot, fields);

                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Clearing cultivated field interiors", .31f);
                int removedTrees = RemoveTreesInsideFields(terrain, fields);
                int removedRocks = RemoveNamedObjectsInsideFields(world.transform, fields, "Solid Ground Rock");

                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Painting patchwork farmland", .47f);
                int fieldPixels = PaintFields(terrain, fields, roads, exclusions);

                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Growing crop rows and hay fields", .63f);
                int cropCells = BuildCropDetails(terrain, fields, roads, exclusions);

                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Adding field-edge shelter belts", .77f);
                int edgeTrees = AddFieldEdgeTrees(terrain, fieldRoot, fields, roads, exclusions);

                EditorUtility.DisplayProgressBar("Hanger 51 Agriculture", "Rebuilding tree collision index", .89f);
                TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();
                if (!streamer) streamer = Undo.AddComponent<TerrainTreeCollisionProxyStreamer>(terrain.gameObject);
                streamer.Configure(terrain, 520f, .10f, 2200);
                streamer.RebuildIndex();
                EditorUtility.SetDirty(streamer);

                terrain.drawTreesAndFoliage = true;
                terrain.treeDistance = 6500f;
                terrain.detailObjectDistance = 750f;
                terrain.detailObjectDensity = 1f;
                terrain.Flush();

                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(terrain);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = pass.gameObject;

                Debug.Log(
                    $"Step 116 complete. fields={fields.Count}, field terrain pixels={fieldPixels}, crop detail cells={cropCells}, cultivated trees removed={removedTrees}, cultivated rocks removed={removedRocks}, field-edge shelter trees={edgeTrees}. Terrain tree visuals now use colliderless copies, so the only tree hitboxes are the streamed trunk colliders aligned to the rendered tree positions. Run Step 117 to validate.",
                    pass.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/117 - Validate Tree Hitboxes And Farmland")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            if (!world || !terrain)
            {
                Debug.LogError("Step 117 failed. Run Step 116 first.");
                return;
            }

            Transform pass = FindChild(world.transform, PassName);
            Transform fields = pass ? FindChild(pass, FieldRootName) : null;
            if (!pass || !fields)
            {
                Debug.LogError("Step 117 failed. Agricultural pass or field root is missing.", world);
                return;
            }

            int fieldCount = 0;
            foreach (Transform tr in fields.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("Field Parcel ")) fieldCount++;

            int prototypeColliderFailures = 0;
            TreePrototype[] prototypes = terrain.terrainData.treePrototypes;
            if (prototypes == null || prototypes.Length != 2) prototypeColliderFailures++;
            else
            {
                foreach (TreePrototype prototype in prototypes)
                {
                    if (prototype == null || !prototype.prefab) { prototypeColliderFailures++; continue; }
                    if (prototype.prefab.GetComponentInChildren<Collider>(true) != null) prototypeColliderFailures++;
                }
            }

            TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();
            int groundAlignmentFailures = CountTreeGroundAlignmentFailures(terrain, .08f);
            int fieldPixels = CountFieldPixels(terrain);
            int cropCells = CountCropDetailCells(terrain);
            int cultivatedTreeIntrusions = CountTreesInsideFieldMarkers(terrain, fields);
            int cultivatedRockIntrusions = CountNamedObjectsInsideFieldMarkers(world.transform, fields, "Solid Ground Rock");

            bool ok =
                fieldCount >= 40 &&
                fieldPixels >= 12000 &&
                cropCells >= 12000 &&
                prototypeColliderFailures == 0 &&
                streamer != null &&
                groundAlignmentFailures == 0 &&
                cultivatedTreeIntrusions == 0 &&
                cultivatedRockIntrusions == 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 117 passed. fields={fieldCount}, painted field pixels={fieldPixels}, crop cells={cropCells}, colliderless terrain-tree prototypes=2, streamed collision component=present, tree ground-alignment failures={groundAlignmentFailures}, trees inside cultivated fields={cultivatedTreeIntrusions}, rocks inside cultivated fields={cultivatedRockIntrusions}.",
                    pass.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 117 failed. fields={fieldCount}, painted field pixels={fieldPixels}, crop cells={cropCells}, prototype collider failures={prototypeColliderFailures}, streamer={(streamer ? "present" : "missing")}, tree ground-alignment failures={groundAlignmentFailures}, trees inside cultivated fields={cultivatedTreeIntrusions}, rocks inside cultivated fields={cultivatedRockIntrusions}.",
                    pass.gameObject);
            }
        }

        static GameObject CreateColliderlessTreePrefab(string sourcePath, string outputPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) return null;

            GameObject temp = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (!temp) return null;
            temp.name = System.IO.Path.GetFileNameWithoutExtension(outputPath);
            foreach (Collider collider in temp.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, outputPath);
            UnityEngine.Object.DestroyImmediate(temp);
            return saved;
        }

        static List<FieldParcel> PlanFields(Terrain terrain, List<Road> roads, List<Bounds> exclusions, int target)
        {
            List<FieldParcel> fields = new List<FieldParcel>();
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            System.Random rng = new System.Random(Seed);
            int tries = 0;

            while (fields.Count < target && tries++ < target * 90)
            {
                Vector3 center = new Vector3(
                    origin.x + Next(rng, .035f, .965f) * size.x,
                    0f,
                    origin.z + Next(rng, .035f, .965f) * size.z);
                center.y = Ground(terrain, center);

                Vector2 parcelSize = new Vector2(Next(rng, 260f, 560f), Next(rng, 180f, 410f));
                float rotation = Mathf.Round(Next(rng, -22f, 22f) / 5f) * 5f + (rng.NextDouble() < .25 ? 90f : 0f);
                int crop = rng.Next(3);

                FieldParcel parcel = new FieldParcel
                {
                    center = center,
                    size = parcelSize,
                    rotation = rotation,
                    cropType = crop,
                    name = "Field Parcel " + (fields.Count + 1).ToString("00") + " - " + CropName(crop)
                };

                if (!FieldIsUsable(terrain, parcel, roads, exclusions)) continue;
                bool overlaps = false;
                foreach (FieldParcel other in fields)
                {
                    if (Planar(parcel.center, other.center) < Mathf.Max(parcel.size.magnitude, other.size.magnitude) * .42f)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps) continue;
                fields.Add(parcel);
            }
            return fields;
        }

        static bool FieldIsUsable(Terrain terrain, FieldParcel field, List<Road> roads, List<Bounds> exclusions)
        {
            Vector3[] samples =
            {
                field.center,
                FieldLocalToWorld(field, new Vector2(-field.size.x * .46f, -field.size.y * .46f)),
                FieldLocalToWorld(field, new Vector2(field.size.x * .46f, -field.size.y * .46f)),
                FieldLocalToWorld(field, new Vector2(-field.size.x * .46f, field.size.y * .46f)),
                FieldLocalToWorld(field, new Vector2(field.size.x * .46f, field.size.y * .46f))
            };

            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            foreach (Vector3 sample in samples)
            {
                if (InsideAny(sample, exclusions) || NearRoad(sample, roads, 16f)) return false;
                float nx = Mathf.Clamp01((sample.x - o.x) / s.x);
                float nz = Mathf.Clamp01((sample.z - o.z) / s.z);
                if (terrain.terrainData.GetSteepness(nx, nz) > 9f) return false;
            }
            return true;
        }

        static void BuildFieldMarkers(Transform root, List<FieldParcel> fields)
        {
            foreach (FieldParcel field in fields)
            {
                Transform marker = New(field.name, root);
                marker.position = field.center;
                marker.rotation = Quaternion.Euler(0f, field.rotation, 0f);
                FieldParcelMarker data = marker.gameObject.AddComponent<FieldParcelMarker>();
                data.size = field.size;
                data.cropType = field.cropType;
            }
        }

        static int RemoveTreesInsideFields(Terrain terrain, List<FieldParcel> fields)
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
                bool inside = false;
                foreach (FieldParcel field in fields)
                {
                    if (InsideField(field, p, 28f))
                    {
                        inside = true;
                        break;
                    }
                }
                if (inside) removed++;
                else kept.Add(tree);
            }

            data.treeInstances = kept.ToArray();
            return removed;
        }

        static int RemoveNamedObjectsInsideFields(Transform root, List<FieldParcel> fields, string prefix)
        {
            List<GameObject> kill = new List<GameObject>();
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith(prefix)) continue;
                foreach (FieldParcel field in fields)
                {
                    if (InsideField(field, tr.position, 16f))
                    {
                        kill.Add(tr.gameObject);
                        break;
                    }
                }
            }
            foreach (GameObject g in kill) if (g) UnityEngine.Object.DestroyImmediate(g);
            return kill.Count;
        }
