using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51ForestVegetationSetup
    {
        private const string TerrainObjectName = "Hanger 51 Editable Terrain";
        private const string PackRoot = "Assets/Supercyan Free Forest Sample";
        private const string HighQualityPrefabRoot = PackRoot + "/Prefabs/High Quality";
        private const string TreePrefabRoot = HighQualityPrefabRoot + "/Tree";
        private const string StonePrefabRoot = HighQualityPrefabRoot + "/Stone";
        private const string FoliagePrefabRoot = HighQualityPrefabRoot + "/Foliage";

        [MenuItem("Hanger 51/Environment/7 - Repair Forest Textures and Add Terrain Paint Assets")]
        public static void RepairForestAndConfigureTerrainPalette()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 7 failed. Exit Play mode before repairing forest assets.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Environment Step 7 failed. Wait for Unity to finish compiling.");
                return;
            }

            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 7 failed. The Hanger 51 editable Terrain is missing. Run Environment Step 3 first.");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Environment Step 7 failed. Universal Render Pipeline/Lit could not be found.");
                return;
            }

            List<GameObject> treePrefabs = FindPrefabs(TreePrefabRoot, StonePrefabRoot);
            List<GameObject> detailPrefabs = FindPrefabs(FoliagePrefabRoot);
            if (treePrefabs.Count == 0 || detailPrefabs.Count == 0)
            {
                Debug.LogError(
                    $"Environment Step 7 failed. Forest pack prefabs are incomplete. Trees/objects={treePrefabs.Count}, details={detailPrefabs.Count}.");
                return;
            }

            HashSet<Material> materials = CollectPrefabMaterials(treePrefabs, detailPrefabs);
            int repairedMaterials = 0;
            foreach (Material material in materials)
            {
                if (RepairMaterialForUrp(material, urpLit))
                {
                    repairedMaterials++;
                }
            }

            TerrainData data = terrain.terrainData;
            Undo.RecordObject(data, "Add forest terrain paint assets");
            Undo.RecordObject(terrain, "Configure forest terrain draw settings");

            int treesAdded = AddTreePrototypes(data, treePrefabs);
            int detailsAdded = AddDetailPrototypes(data, detailPrefabs);
            int layersAdded = AddTerrainLayers(data);

            terrain.drawTreesAndFoliage = true;
            terrain.treeDistance = Mathf.Max(terrain.treeDistance, 1800f);
            terrain.treeBillboardDistance = Mathf.Clamp(
                Mathf.Max(terrain.treeBillboardDistance, 120f),
                50f,
                terrain.treeDistance);
            terrain.treeCrossFadeLength = Mathf.Max(terrain.treeCrossFadeLength, 25f);
            terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 240f);
            terrain.detailObjectDensity = Mathf.Max(terrain.detailObjectDensity, 1f);

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
            terrain.Flush();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (scene.IsValid() && !string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeGameObject = terrain.gameObject;
            EditorGUIUtility.PingObject(terrain.gameObject);

            Debug.Log(
                $"Environment Step 7 complete. Repaired {repairedMaterials} imported forest material(s) for URP while preserving their original diffuse textures. "
                + $"Terrain palette now has {data.treePrototypes.Length} Paint Trees/object prototype(s), {data.detailPrototypes.Length} Paint Details prototype(s), "
                + $"and {data.terrainLayers.Length} Paint Texture layer(s). This run added {treesAdded} tree/stump/rock prototype(s), {detailsAdded} grass/mushroom detail(s), "
                + $"and {layersAdded} forest terrain layer(s). Existing terrain palette entries were preserved.",
                terrain);
        }

        [MenuItem("Hanger 51/Environment/8 - Validate Forest Terrain Paint Assets")]
        public static void ValidateForestTerrainPalette()
        {
            bool passed = true;
            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 8 failed: editable Terrain is missing.");
                return;
            }

            List<GameObject> treePrefabs = FindPrefabs(TreePrefabRoot, StonePrefabRoot);
            List<GameObject> detailPrefabs = FindPrefabs(FoliagePrefabRoot);
            TerrainLayer[] packLayers = FindTerrainLayers();
            TerrainData data = terrain.terrainData;

            int missingTrees = CountMissingTreePrototypes(data, treePrefabs);
            int missingDetails = CountMissingDetailPrototypes(data, detailPrefabs);
            int missingLayers = CountMissingTerrainLayers(data, packLayers);
            if (missingTrees > 0)
            {
                Debug.LogError($"Environment Step 8 failed: {missingTrees} forest tree/stump/rock prefab(s) are missing from Paint Trees.", terrain);
                passed = false;
            }
            if (missingDetails > 0)
            {
                Debug.LogError($"Environment Step 8 failed: {missingDetails} grass/mushroom prefab(s) are missing from Paint Details.", terrain);
                passed = false;
            }
            if (missingLayers > 0)
            {
                Debug.LogError($"Environment Step 8 failed: {missingLayers} forest Terrain Layer asset(s) are missing from Paint Texture.", terrain);
                passed = false;
            }

            HashSet<Material> materials = CollectPrefabMaterials(treePrefabs, detailPrefabs);
            int validMaterials = 0;
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                bool urp = material.shader != null
                    && material.shader.name == "Universal Render Pipeline/Lit";
                Texture albedo = GetAlbedoTexture(material);
                if (!urp || albedo == null)
                {
                    Debug.LogError(
                        $"Environment Step 8 failed: material '{material.name}' is not a textured URP Lit material. Shader='{(material.shader != null ? material.shader.name : "missing")}', texture='{(albedo != null ? albedo.name : "missing")}'.",
                        material);
                    passed = false;
                }
                else
                {
                    validMaterials++;
                }
            }

            if (treePrefabs.Count == 0 || detailPrefabs.Count == 0)
            {
                Debug.LogError("Environment Step 8 failed: imported forest prefabs could not be discovered.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"Environment Step 8 passed. Forest materials textured/URP-ready={validMaterials}, Paint Trees/object assets={treePrefabs.Count}, "
                    + $"Paint Details assets={detailPrefabs.Count}, imported Paint Texture layers={packLayers.Length}. "
                    + "Select 'Hanger 51 Editable Terrain' and use the Terrain paintbrush tabs to paint trees, grass/details, rocks/stumps, and ground textures.",
                    terrain);
            }
        }

        private static bool RepairMaterialForUrp(Material material, Shader urpLit)
        {
            if (material == null || urpLit == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.StartsWith(PackRoot + "/", StringComparison.Ordinal))
            {
                return false;
            }

            Texture albedo = GetAlbedoTexture(material);
            if (albedo == null)
            {
                return false;
            }

            string sourceTextureProperty = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null
                ? "_BaseMap"
                : material.HasProperty("_MainTex") ? "_MainTex" : string.Empty;
            Vector2 textureScale = !string.IsNullOrEmpty(sourceTextureProperty)
                ? material.GetTextureScale(sourceTextureProperty)
                : Vector2.one;
            Vector2 textureOffset = !string.IsNullOrEmpty(sourceTextureProperty)
                ? material.GetTextureOffset(sourceTextureProperty)
                : Vector2.zero;

            Undo.RecordObject(material, "Convert imported forest material to URP");
            material.shader = urpLit;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", albedo);
                material.SetTextureScale("_BaseMap", textureScale);
                material.SetTextureOffset("_BaseMap", textureOffset);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", albedo);
                material.SetTextureScale("_MainTex", textureScale);
                material.SetTextureOffset("_MainTex", textureOffset);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);

            bool alphaCutout = IsCutoutVegetation(material.name);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", alphaCutout ? 1f : 0f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", alphaCutout ? 0.42f : 0.5f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", alphaCutout ? (float)CullMode.Off : (float)CullMode.Back);

            if (alphaCutout)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.doubleSidedGI = true;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
                material.doubleSidedGI = false;
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return true;
        }

        private static Texture GetAlbedoTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }
            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null) return texture;
            }
            if (material.HasProperty("_MainTex"))
            {
                Texture texture = material.GetTexture("_MainTex");
                if (texture != null) return texture;
            }
            return material.mainTexture;
        }

        private static bool IsCutoutVegetation(string materialName)
        {
            string value = (materialName ?? string.Empty).ToLowerInvariant();
            return value.Contains("foliage")
                || value.Contains("tree_fir")
                || value.Contains("tree_leaf");
        }

        private static int AddTreePrototypes(TerrainData data, List<GameObject> prefabs)
        {
            List<TreePrototype> prototypes = new List<TreePrototype>(data.treePrototypes ?? Array.Empty<TreePrototype>());
            HashSet<GameObject> existing = new HashSet<GameObject>();
            for (int index = 0; index < prototypes.Count; index++)
            {
                if (prototypes[index] != null && prototypes[index].prefab != null)
                {
                    existing.Add(prototypes[index].prefab);
                }
            }

            int added = 0;
            for (int index = 0; index < prefabs.Count; index++)
            {
                GameObject prefab = prefabs[index];
                if (prefab == null || existing.Contains(prefab))
                {
                    continue;
                }

                bool livingTree = AssetDatabase.GetAssetPath(prefab).Contains("/Tree/Fir/")
                    || AssetDatabase.GetAssetPath(prefab).Contains("/Tree/Leaf/");
                prototypes.Add(new TreePrototype
                {
                    prefab = prefab,
                    bendFactor = livingTree ? 0.18f : 0f
                });
                existing.Add(prefab);
                added++;
            }

            data.treePrototypes = prototypes.ToArray();
            return added;
        }

        private static int AddDetailPrototypes(TerrainData data, List<GameObject> prefabs)
        {
            List<DetailPrototype> prototypes = new List<DetailPrototype>(data.detailPrototypes ?? Array.Empty<DetailPrototype>());
            HashSet<GameObject> existing = new HashSet<GameObject>();
            for (int index = 0; index < prototypes.Count; index++)
            {
                DetailPrototype prototype = prototypes[index];
                if (prototype != null && prototype.prototype != null)
                {
                    existing.Add(prototype.prototype);
                }
            }

            int added = 0;
            for (int index = 0; index < prefabs.Count; index++)
            {
                GameObject prefab = prefabs[index];
                if (prefab == null || existing.Contains(prefab))
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(prefab);
                bool mushroom = path.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0;
                DetailPrototype prototype = new DetailPrototype
                {
                    prototype = prefab,
                    usePrototypeMesh = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = mushroom ? 0.80f : 0.75f,
                    maxWidth = mushroom ? 1.20f : 1.28f,
                    minHeight = mushroom ? 0.80f : 0.78f,
                    maxHeight = mushroom ? 1.20f : 1.35f,
                    healthyColor = Color.white,
                    dryColor = Color.white,
                    noiseSpread = mushroom ? 0.25f : 0.16f
                };
                prototypes.Add(prototype);
                existing.Add(prefab);
                added++;
            }

            data.detailPrototypes = prototypes.ToArray();
            return added;
        }

        private static int AddTerrainLayers(TerrainData data)
        {
            TerrainLayer[] imported = FindTerrainLayers();
            List<TerrainLayer> layers = new List<TerrainLayer>(data.terrainLayers ?? Array.Empty<TerrainLayer>());
            HashSet<TerrainLayer> existing = new HashSet<TerrainLayer>(layers);
            int added = 0;
            for (int index = 0; index < imported.Length; index++)
            {
                TerrainLayer layer = imported[index];
                if (layer == null || existing.Contains(layer))
                {
                    continue;
                }
                layers.Add(layer);
                existing.Add(layer);
                added++;
            }
            data.terrainLayers = layers.ToArray();
            return added;
        }

        private static TerrainLayer[] FindTerrainLayers()
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { PackRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            List<TerrainLayer> result = new List<TerrainLayer>();
            for (int index = 0; index < guids.Length; index++)
            {
                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guids[index]));
                if (layer != null) result.Add(layer);
            }
            return result.ToArray();
        }

        private static List<GameObject> FindPrefabs(params string[] roots)
        {
            HashSet<GameObject> unique = new HashSet<GameObject>();
            List<GameObject> result = new List<GameObject>();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root))
                {
                    continue;
                }
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                Array.Sort(guids, StringComparer.Ordinal);
                for (int index = 0; index < guids.Length; index++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[index]));
                    if (prefab != null && unique.Add(prefab))
                    {
                        result.Add(prefab);
                    }
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right)));
            return result;
        }

        private static HashSet<Material> CollectPrefabMaterials(params List<GameObject>[] prefabLists)
        {
            HashSet<Material> result = new HashSet<Material>();
            for (int listIndex = 0; listIndex < prefabLists.Length; listIndex++)
            {
                List<GameObject> prefabs = prefabLists[listIndex];
                if (prefabs == null) continue;
                for (int prefabIndex = 0; prefabIndex < prefabs.Count; prefabIndex++)
                {
                    GameObject prefab = prefabs[prefabIndex];
                    if (prefab == null) continue;
                    Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Material[] shared = renderers[rendererIndex].sharedMaterials;
                        for (int materialIndex = 0; materialIndex < shared.Length; materialIndex++)
                        {
                            Material material = shared[materialIndex];
                            if (material != null) result.Add(material);
                        }
                    }
                }
            }
            return result;
        }

        private static int CountMissingTreePrototypes(TerrainData data, List<GameObject> required)
        {
            HashSet<GameObject> current = new HashSet<GameObject>();
            TreePrototype[] prototypes = data.treePrototypes ?? Array.Empty<TreePrototype>();
            for (int index = 0; index < prototypes.Length; index++)
            {
                if (prototypes[index] != null && prototypes[index].prefab != null) current.Add(prototypes[index].prefab);
            }
            int missing = 0;
            for (int index = 0; index < required.Count; index++) if (required[index] != null && !current.Contains(required[index])) missing++;
            return missing;
        }

        private static int CountMissingDetailPrototypes(TerrainData data, List<GameObject> required)
        {
            HashSet<GameObject> current = new HashSet<GameObject>();
            DetailPrototype[] prototypes = data.detailPrototypes ?? Array.Empty<DetailPrototype>();
            for (int index = 0; index < prototypes.Length; index++)
            {
                if (prototypes[index] != null && prototypes[index].prototype != null) current.Add(prototypes[index].prototype);
            }
            int missing = 0;
            for (int index = 0; index < required.Count; index++) if (required[index] != null && !current.Contains(required[index])) missing++;
            return missing;
        }

        private static int CountMissingTerrainLayers(TerrainData data, TerrainLayer[] required)
        {
            HashSet<TerrainLayer> current = new HashSet<TerrainLayer>(data.terrainLayers ?? Array.Empty<TerrainLayer>());
            int missing = 0;
            for (int index = 0; index < required.Length; index++) if (required[index] != null && !current.Contains(required[index])) missing++;
            return missing;
        }

        private static Terrain FindTerrain()
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < terrains.Length; index++)
            {
                Terrain terrain = terrains[index];
                if (terrain != null && terrain.gameObject.name == TerrainObjectName)
                {
                    return terrain;
                }
            }
            return Terrain.activeTerrain;
        }
    }
}
