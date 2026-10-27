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
                marker.localScale = new Vector3(field.size.x, 1f, field.size.y);
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


        static int PaintFields(Terrain terrain, List<FieldParcel> fields, List<Road> roads, List<Bounds> exclusions)
        {
            TerrainData data = terrain.terrainData;
            int fieldLayer = FindTerrainLayer(data, "New Field");
            int meadowLayer = FindTerrainLayer(data, "New Meadow");
            if (fieldLayer < 0)
            {
                Debug.LogError("Step 116 could not find the 'New Field' terrain layer.");
                return 0;
            }

            int w = data.alphamapWidth;
            int h = data.alphamapHeight;
            float[,,] alpha = data.GetAlphamaps(0, 0, w, h);
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int painted = 0;

            for (int z = 0; z < h; z++)
            {
                float wz = origin.z + z / (float)Mathf.Max(1, h - 1) * size.z;
                for (int x = 0; x < w; x++)
                {
                    float wx = origin.x + x / (float)Mathf.Max(1, w - 1) * size.x;
                    Vector3 p = new Vector3(wx, 0f, wz);
                    FieldParcel field = FieldAt(fields, p, 10f);
                    if (field == null || InsideAny(p, exclusions) || NearRoad(p, roads, 7f)) continue;

                    Vector2 local = WorldToField(field, p);
                    float edgeX = 1f - Mathf.Abs(local.x) / Mathf.Max(1f, field.size.x * .5f);
                    float edgeZ = 1f - Mathf.Abs(local.y) / Mathf.Max(1f, field.size.y * .5f);
                    float edge = Mathf.Clamp01(Mathf.Min(edgeX, edgeZ) * 4f);
                    float row = .5f + .5f * Mathf.Sin((local.y + field.cropType * 3.7f) * (field.cropType == 2 ? .22f : .12f));
                    float strength = Mathf.Clamp01(.76f + row * .16f) * edge;

                    float keepRock = 0f;
                    int stoneLayer = FindTerrainLayer(data, "Natural Stone");
                    if (stoneLayer >= 0) keepRock = alpha[z, x, stoneLayer] * .25f;

                    for (int layer = 0; layer < alpha.GetLength(2); layer++) alpha[z, x, layer] *= 1f - strength;
                    alpha[z, x, fieldLayer] += strength * (1f - keepRock);
                    if (meadowLayer >= 0) alpha[z, x, meadowLayer] += (1f - edge) * .08f;

                    float sum = 0f;
                    for (int layer = 0; layer < alpha.GetLength(2); layer++) sum += alpha[z, x, layer];
                    if (sum > .0001f)
                        for (int layer = 0; layer < alpha.GetLength(2); layer++) alpha[z, x, layer] /= sum;
                    painted++;
                }
            }

            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            return painted;
        }

        static int BuildCropDetails(Terrain terrain, List<FieldParcel> fields, List<Road> roads, List<Bounds> exclusions)
        {
            TerrainData data = terrain.terrainData;
            DetailPrototype meadow = data.detailPrototypes != null && data.detailPrototypes.Length > 0
                ? data.detailPrototypes[0]
                : null;

            Texture2D green = CropTexture("GreenCropRows", new Color(.18f, .34f, .07f, 1f), new Color(.38f, .58f, .12f, 1f), 29);
            Texture2D gold = CropTexture("GoldenGrainRows", new Color(.48f, .32f, .06f, 1f), new Color(.84f, .67f, .17f, 1f), 41);
            Texture2D dry = CropTexture("DryFallowRows", new Color(.33f, .22f, .08f, 1f), new Color(.58f, .43f, .14f, 1f), 53);

            List<DetailPrototype> prototypes = new List<DetailPrototype>();
            if (meadow != null) prototypes.Add(meadow);
            prototypes.Add(MakeDetail(green, .55f, 1.05f, .7f, 1.55f));
            prototypes.Add(MakeDetail(gold, .48f, .9f, .65f, 1.35f));
            prototypes.Add(MakeDetail(dry, .42f, .8f, .35f, .85f));
            data.detailPrototypes = prototypes.ToArray();

            int width = data.detailWidth;
            int height = data.detailHeight;
            if (width < 512 || height < 512)
            {
                data.SetDetailResolution(768, 16);
                width = data.detailWidth;
                height = data.detailHeight;
            }

            int[,] meadowMap = prototypes.Count > 0 ? data.GetDetailLayer(0, 0, width, height, 0) : new int[height, width];
            int[,] greenMap = new int[height, width];
            int[,] goldMap = new int[height, width];
            int[,] dryMap = new int[height, width];
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int cropCells = 0;

            for (int z = 0; z < height; z++)
            {
                float wz = origin.z + z / (float)Mathf.Max(1, height - 1) * size.z;
                for (int x = 0; x < width; x++)
                {
                    float wx = origin.x + x / (float)Mathf.Max(1, width - 1) * size.x;
                    Vector3 p = new Vector3(wx, 0f, wz);
                    FieldParcel field = FieldAt(fields, p, 15f);
                    if (field == null || InsideAny(p, exclusions) || NearRoad(p, roads, 6f)) continue;

                    Vector2 local = WorldToField(field, p);
                    float rowSpacing = field.cropType == 0 ? 5.5f : field.cropType == 1 ? 4.2f : 7.5f;
                    float row = Mathf.Abs(Mathf.Repeat(local.x + rowSpacing * .5f, rowSpacing) - rowSpacing * .5f);
                    if (row > rowSpacing * .28f) continue;

                    int density = field.cropType == 0 ? 10 : field.cropType == 1 ? 8 : 4;
                    if (field.cropType == 0) greenMap[z, x] = density;
                    else if (field.cropType == 1) goldMap[z, x] = density;
                    else dryMap[z, x] = density;

                    if (meadowMap.GetLength(0) == height && meadowMap.GetLength(1) == width) meadowMap[z, x] = 0;
                    cropCells++;
                }
            }

            if (meadowMap.GetLength(0) == height && meadowMap.GetLength(1) == width) data.SetDetailLayer(0, 0, 0, meadowMap);
            data.SetDetailLayer(0, 0, 1, greenMap);
            data.SetDetailLayer(0, 0, 2, goldMap);
            data.SetDetailLayer(0, 0, 3, dryMap);
            EditorUtility.SetDirty(data);
            return cropCells;
        }

        static DetailPrototype MakeDetail(Texture2D texture, float minWidth, float maxWidth, float minHeight, float maxHeight)
        {
            return new DetailPrototype
            {
                prototypeTexture = texture,
                renderMode = DetailRenderMode.GrassBillboard,
                minWidth = minWidth,
                maxWidth = maxWidth,
                minHeight = minHeight,
                maxHeight = maxHeight,
                noiseSpread = .12f,
                healthyColor = Color.white,
                dryColor = Color.white
            };
        }

        static Texture2D CropTexture(string name, Color dark, Color light, int seed)
        {
            string path = Gen + "/Textures/" + name + ".asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "H51 " + name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            System.Random rng = new System.Random(seed);
            for (int blade = 0; blade < 34; blade++)
            {
                int baseX = rng.Next(3, size - 3);
                int top = rng.Next(34, size - 2);
                for (int y = 1; y < top; y++)
                {
                    float t = y / (float)top;
                    int x = Mathf.Clamp(baseX + Mathf.RoundToInt(Mathf.Sin((t + blade) * 4.7f) * 2f), 0, size - 1);
                    Color c = Color.Lerp(dark, light, .25f + t * .75f);
                    pixels[y * size + x] = c;
                    if (x + 1 < size && t < .7f) pixels[y * size + x + 1] = c;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        static int AddFieldEdgeTrees(Terrain terrain, Transform root, List<FieldParcel> fields, List<Road> roads, List<Bounds> exclusions)
        {
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafSource);
            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirSource);
            if (!leaf || !fir) return 0;

            Transform shelter = New("Field Edge Shelter Belts", root);
            System.Random rng = new System.Random(Seed + 80);
            int made = 0;

            foreach (FieldParcel field in fields)
            {
                if (field.cropType == 2 || rng.NextDouble() < .45) continue;
                bool alongX = rng.NextDouble() < .5;
                float half = alongX ? field.size.x * .5f : field.size.y * .5f;
                int count = Mathf.Clamp(Mathf.FloorToInt(half * 2f / 24f), 5, 28);
                int edgeSign = rng.NextDouble() < .5 ? -1 : 1;

                for (int i = 0; i <= count; i++)
                {
                    float u = Mathf.Lerp(-half, half, i / (float)count);
                    Vector2 local = alongX
                        ? new Vector2(u, edgeSign * (field.size.y * .5f + 11f))
                        : new Vector2(edgeSign * (field.size.x * .5f + 11f), u);
                    Vector3 p = FieldLocalToWorld(field, local);
                    if (InsideAny(p, exclusions) || NearRoad(p, roads, 10f)) continue;
                    p.y = Ground(terrain, p);

                    GameObject src = rng.NextDouble() < .72 ? leaf : fir;
                    GameObject tree = PrefabUtility.InstantiatePrefab(src) as GameObject;
                    if (!tree) continue;
                    tree.name = "Field Edge Shelter Tree " + (++made).ToString("0000");
                    tree.transform.SetParent(shelter, false);
                    tree.transform.position = p;
                    tree.transform.rotation = Quaternion.Euler(0f, Next(rng, 0f, 360f), 0f);
                    tree.transform.localScale = Vector3.one * Next(rng, 1.1f, 1.8f);
                    foreach (Collider col in tree.GetComponentsInChildren<Collider>(true))
                    {
                        col.enabled = true;
                        col.isTrigger = false;
                    }
                }
            }
            return made;
        }


        static int CountFieldPixels(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int layer = FindTerrainLayer(data, "New Field");
            if (layer < 0) return 0;
            float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            int count = 0;
            for (int z = 0; z < data.alphamapHeight; z++)
                for (int x = 0; x < data.alphamapWidth; x++)
                    if (alpha[z, x, layer] >= .58f) count++;
            return count;
        }

        static int CountCropDetailCells(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            if (data.detailPrototypes == null || data.detailPrototypes.Length < 4) return 0;
            int total = 0;
            for (int layer = 1; layer <= 3; layer++)
            {
                int[,] map = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
                for (int z = 0; z < map.GetLength(0); z++)
                    for (int x = 0; x < map.GetLength(1); x++)
                        if (map[z, x] > 0) total++;
            }
            return total;
        }

        static int CountTreeGroundAlignmentFailures(Terrain terrain, float tolerance)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int bad = 0;
            foreach (TreeInstance tree in data.treeInstances)
            {
                float x = origin.x + tree.position.x * size.x;
                float z = origin.z + tree.position.z * size.z;
                Vector3 p = new Vector3(x, origin.y, z);
                float sampled = terrain.SampleHeight(p) + origin.y;
                float stored = origin.y + tree.position.y * size.y;
                if (Mathf.Abs(sampled - stored) > tolerance) bad++;
            }
            return bad;
        }

        static int CountTreesInsideFieldMarkers(Terrain terrain, Transform markers)
        {
            List<FieldParcel> fields = ReadFieldMarkers(markers);
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int bad = 0;
            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 p = new Vector3(origin.x + tree.position.x * size.x, 0f, origin.z + tree.position.z * size.z);
                foreach (FieldParcel field in fields)
                {
                    if (InsideField(field, p, 28f))
                    {
                        bad++;
                        break;
                    }
                }
            }
            return bad;
        }

        static int CountNamedObjectsInsideFieldMarkers(Transform root, Transform markers, string prefix)
        {
            List<FieldParcel> fields = ReadFieldMarkers(markers);
            int bad = 0;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith(prefix)) continue;
                foreach (FieldParcel field in fields)
                {
                    if (InsideField(field, tr.position, 16f))
                    {
                        bad++;
                        break;
                    }
                }
            }
            return bad;
        }

        static List<FieldParcel> ReadFieldMarkers(Transform root)
        {
            List<FieldParcel> fields = new List<FieldParcel>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform marker = root.GetChild(i);
                if (!marker.name.StartsWith("Field Parcel ")) continue;
                int crop = marker.name.Contains("Green Crop") ? 0 : marker.name.Contains("Golden Grain") ? 1 : 2;
                fields.Add(new FieldParcel
                {
                    center = marker.position,
                    size = new Vector2(Mathf.Abs(marker.localScale.x), Mathf.Abs(marker.localScale.z)),
                    rotation = marker.eulerAngles.y,
                    cropType = crop,
                    name = marker.name
                });
            }
            return fields;
        }

        static int FindTerrainLayer(TerrainData data, string name)
        {
            TerrainLayer[] layers = data.terrainLayers;
            if (layers == null) return -1;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] && layers[i].name == name) return i;
            return -1;
        }

        static FieldParcel FieldAt(List<FieldParcel> fields, Vector3 p, float margin)
        {
            foreach (FieldParcel field in fields)
                if (InsideField(field, p, margin)) return field;
            return null;
        }

        static bool InsideField(FieldParcel field, Vector3 p, float margin)
        {
            Vector2 local = WorldToField(field, p);
            return Mathf.Abs(local.x) <= Mathf.Max(0f, field.size.x * .5f - margin) &&
                   Mathf.Abs(local.y) <= Mathf.Max(0f, field.size.y * .5f - margin);
        }

        static Vector2 WorldToField(FieldParcel field, Vector3 p)
        {
            Vector3 delta = p - field.center;
            float radians = -field.rotation * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(delta.x * c - delta.z * s, delta.x * s + delta.z * c);
        }

        static Vector3 FieldLocalToWorld(FieldParcel field, Vector2 local)
        {
            float radians = field.rotation * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return field.center + new Vector3(local.x * c - local.y * s, 0f, local.x * s + local.y * c);
        }

        static string CropName(int cropType)
        {
            return cropType == 0 ? "Green Crop" : cropType == 1 ? "Golden Grain" : "Fallow Hay";
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

        static List<Bounds> CollectExclusions(Transform world, Transform airport)
        {
            List<Bounds> bounds = new List<Bounds>();
            foreach (Transform tr in airport.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name.ToLowerInvariant();
                float pad =
                    n.Contains("runway") ? 95f :
                    n.Contains("taxiway") ? 38f :
                    n.Contains("apron") ? 32f :
                    n.Contains("hangar") || n.Contains("hanger") ? 24f : -1f;
                if (pad < 0f) continue;
                if (TryBounds(tr, out Bounds b))
                {
                    b.Expand(new Vector3(pad * 2f, 50f, pad * 2f));
                    bounds.Add(b);
                }
            }

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                bool protectedObject =
                    n.StartsWith("Detailed House") ||
                    n.StartsWith("Purpose -") ||
                    n.StartsWith("Farmstead -") ||
                    n == "Power Station Complex" ||
                    n.StartsWith("Detached Garage") ||
                    n.StartsWith("Community Park -") ||
                    n.StartsWith("Electrical Substation");
                if (!protectedObject) continue;
                if (TryBounds(tr, out Bounds b))
                {
                    b.Expand(new Vector3(28f, 40f, 28f));
                    bounds.Add(b);
                }
            }
            return bounds;
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
            Vector2 x = new Vector2(a.x, a.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            if (d.sqrMagnitude < .001f) return Vector2.Distance(q, x);
            return Vector2.Distance(q, x + d * Mathf.Clamp01(Vector2.Dot(q - x, d) / d.sqrMagnitude));
        }

        static bool InsideAny(Vector3 p, List<Bounds> exclusions)
        {
            foreach (Bounds b in exclusions)
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) return true;
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

        static void ResetFolder()
        {
            if (AssetDatabase.IsValidFolder(Gen)) AssetDatabase.DeleteAsset(Gen);
            Ensure(Gen + "/Prefabs");
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
