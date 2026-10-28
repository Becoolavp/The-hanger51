using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldVisibleTerrainTreePhysicsRepair
    {
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string FirSource = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string LeafSource = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";

        [MenuItem("Hanger 51/World/Current/124 - Restore Visible Terrain Trees And Hitboxes")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 124 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Terrain terrain = FindTerrain();
            if (!terrain || !terrain.terrainData)
            {
                Debug.LogError("Step 124 failed. Could not find the editable Terrain.");
                return;
            }

            GameObject fir = AssetDatabase.LoadAssetAtPath<GameObject>(FirSource);
            GameObject leaf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafSource);
            if (!fir || !leaf)
            {
                Debug.LogError("Step 124 failed. Could not load the original Supercyan fir/leaf tree prefabs.");
                return;
            }

            TerrainData data = terrain.terrainData;
            int treeCountBefore = data.treeInstanceCount;
            TreeInstance[] instances = data.treeInstances;

            data.treePrototypes = new[]
            {
                new TreePrototype { prefab = fir, bendFactor = .25f },
                new TreePrototype { prefab = leaf, bendFactor = .32f }
            };

            // Preserve the exact existing tree population and placement.
            data.treeInstances = instances;

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (!terrainCollider) terrainCollider = Undo.AddComponent<TerrainCollider>(terrain.gameObject);
            terrainCollider.terrainData = data;
            terrainCollider.enabled = true;

            SerializedObject serialized = new SerializedObject(terrainCollider);
            SerializedProperty enableTrees = serialized.FindProperty("m_EnableTreeColliders");
            if (enableTrees != null) enableTrees.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            terrain.drawTreesAndFoliage = true;
            terrain.treeDistance = 6500f;
            terrain.treeBillboardDistance = 500f;
            terrain.treeCrossFadeLength = 100f;
            terrain.treeMaximumFullLODCount = 3000;
            terrain.detailObjectDistance = 750f;
            terrain.detailObjectDensity = 1f;

            TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();
            if (!streamer) streamer = Undo.AddComponent<TerrainTreeCollisionProxyStreamer>(terrain.gameObject);
            streamer.Configure(terrain, 360f, .10f, 1800);

            terrain.Flush();
            streamer.RebuildAndRefreshNow();

            float maxError;
            int alignmentFailures = streamer.ValidateActiveProxyAlignment(.02f, out maxError);

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrainCollider);
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(streamer);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log(
                $"Step 124 complete. Preserved {treeCountBefore} Terrain tree instances, restored the original visible Supercyan fir/leaf prototypes, kept Unity native Terrain tree collisions OFF, rebuilt {streamer.ActiveProxyCount} streamed trunk hitboxes, alignment failures={alignmentFailures}, maximum proxy position error={maxError:0.000} m. Run Step 125.",
                terrain);
        }

        [MenuItem("Hanger 51/World/Current/125 - Validate Visible Terrain Trees And Hitboxes")]
        public static void Validate()
        {
            Terrain terrain = FindTerrain();
            TerrainTreeCollisionProxyStreamer streamer = terrain ? terrain.GetComponent<TerrainTreeCollisionProxyStreamer>() : null;
            if (!terrain || !terrain.terrainData || !streamer)
            {
                Debug.LogError("Step 125 failed. Run Step 124 first.");
                return;
            }

            TerrainData data = terrain.terrainData;
            TreePrototype[] prototypes = data.treePrototypes;
            int prototypeFailures = 0;
            int rendererFailures = 0;
            int trunkFailures = 0;

            if (prototypes == null || prototypes.Length != 2)
            {
                prototypeFailures++;
            }
            else
            {
                string[] expected = { FirSource, LeafSource };
                for (int i = 0; i < 2; i++)
                {
                    TreePrototype p = prototypes[i];
                    if (p == null || !p.prefab)
                    {
                        prototypeFailures++;
                        rendererFailures++;
                        trunkFailures++;
                        continue;
                    }

                    if (AssetDatabase.GetAssetPath(p.prefab) != expected[i]) prototypeFailures++;
                    if (!p.prefab.GetComponentInChildren<Renderer>(true)) rendererFailures++;

                    CapsuleCollider trunk = p.prefab.GetComponentInChildren<CapsuleCollider>(true);
                    if (!trunk || !trunk.enabled || trunk.isTrigger) trunkFailures++;
                }
            }

            int invalidInstancePrototypeIndices = 0;
            TreeInstance[] instances = data.treeInstances;
            for (int i = 0; i < instances.Length; i++)
                if (instances[i].prototypeIndex < 0 || instances[i].prototypeIndex >= 2)
                    invalidInstancePrototypeIndices++;

            bool nativeTreeColliders = false;
            TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
            if (tc)
            {
                SerializedObject so = new SerializedObject(tc);
                SerializedProperty enableTrees = so.FindProperty("m_EnableTreeColliders");
                if (enableTrees != null) nativeTreeColliders = enableTrees.boolValue;
            }

            streamer.RebuildAndRefreshNow();
            float maxError;
            int alignmentFailures = streamer.ValidateActiveProxyAlignment(.02f, out maxError);

            bool ok =
                data.treeInstanceCount > 0 &&
                prototypeFailures == 0 &&
                rendererFailures == 0 &&
                trunkFailures == 0 &&
                invalidInstancePrototypeIndices == 0 &&
                !nativeTreeColliders &&
                alignmentFailures == 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 125 passed. Terrain trees={data.treeInstanceCount}, original visible prototypes=2, renderer failures=0, source trunk failures=0, invalid prototype indices=0, native Terrain tree collisions=off, active streamed hitboxes={streamer.ActiveProxyCount}, alignment failures=0, maximum proxy position error={maxError:0.000} m.",
                    terrain);
            }
            else
            {
                Debug.LogError(
                    $"Step 125 failed. Terrain trees={data.treeInstanceCount}, prototype failures={prototypeFailures}, renderer failures={rendererFailures}, source trunk failures={trunkFailures}, invalid prototype indices={invalidInstancePrototypeIndices}, native Terrain tree collisions={(nativeTreeColliders ? "ON" : "off")}, active streamed hitboxes={streamer.ActiveProxyCount}, alignment failures={alignmentFailures}, maximum proxy position error={maxError:0.000} m.",
                    terrain);
            }
        }

        static Terrain FindTerrain()
        {
            GameObject named = GameObject.Find(TerrainName);
            Terrain t = named ? (named.GetComponent<Terrain>() ?? named.GetComponentInChildren<Terrain>(true)) : null;
            if (t) return t;

            Terrain[] all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }
    }
}
