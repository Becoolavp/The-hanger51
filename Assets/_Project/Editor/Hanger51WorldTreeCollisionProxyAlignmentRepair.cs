using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldTreeCollisionProxyAlignmentRepair
    {
        const string TerrainName = "Hanger 51 Editable Terrain";

        [MenuItem("Hanger 51/World/Current/122 - Repair Tree Hitbox Alignment")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 122 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Terrain terrain = FindTerrain();
            if (!terrain)
            {
                Debug.LogError("Step 122 failed. Could not find Hanger 51 Editable Terrain.");
                return;
            }

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider)
            {
                SerializedObject serialized = new SerializedObject(terrainCollider);
                SerializedProperty enableTrees = serialized.FindProperty("m_EnableTreeColliders");
                if (enableTrees != null) enableTrees.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(terrainCollider);
            }

            TerrainTreeCollisionProxyStreamer streamer = terrain.GetComponent<TerrainTreeCollisionProxyStreamer>();
            if (!streamer) streamer = Undo.AddComponent<TerrainTreeCollisionProxyStreamer>(terrain.gameObject);

            streamer.Configure(terrain, 360f, .10f, 1800);
            streamer.RebuildAndRefreshNow();

            float maxError;
            int failures = streamer.ValidateActiveProxyAlignment(.02f, out maxError);

            EditorUtility.SetDirty(streamer);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            if (failures == 0)
            {
                Debug.Log(
                    $"Step 122 complete. Rebuilt the Terrain tree collision proxy index, discarded all stale proxy-to-tree assignments, disabled Unity native Terrain tree colliders, and immediately refreshed {streamer.ActiveProxyCount} active proxies. Maximum proxy position error={maxError:0.000} m.",
                    terrain);
            }
            else
            {
                Debug.LogError(
                    $"Step 122 completed but alignment verification found {failures} active proxy position failures. active proxies={streamer.ActiveProxyCount}, maximum error={maxError:0.000} m.",
                    terrain);
            }
        }

        [MenuItem("Hanger 51/World/Current/123 - Validate Tree Hitbox Alignment")]
        public static void Validate()
        {
            Terrain terrain = FindTerrain();
            TerrainTreeCollisionProxyStreamer streamer = terrain ? terrain.GetComponent<TerrainTreeCollisionProxyStreamer>() : null;
            if (!terrain || !streamer)
            {
                Debug.LogError("Step 123 failed. Run Step 122 first.");
                return;
            }

            // Force a fresh mapping so this validation also catches any later edits that reorder TreeInstances.
            streamer.RebuildAndRefreshNow();

            float maxError;
            int failures = streamer.ValidateActiveProxyAlignment(.02f, out maxError);

            bool nativeTreeColliders = false;
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider)
            {
                SerializedObject serialized = new SerializedObject(terrainCollider);
                SerializedProperty enableTrees = serialized.FindProperty("m_EnableTreeColliders");
                if (enableTrees != null) nativeTreeColliders = enableTrees.boolValue;
            }

            if (failures == 0 && !nativeTreeColliders)
            {
                Debug.Log(
                    $"Step 123 passed. active proxies={streamer.ActiveProxyCount}, stale/misaligned proxies=0, maximum position error={maxError:0.000} m, Unity native Terrain tree colliders=off.",
                    terrain);
            }
            else
            {
                Debug.LogError(
                    $"Step 123 failed. active proxies={streamer.ActiveProxyCount}, stale/misaligned proxies={failures}, maximum position error={maxError:0.000} m, Unity native Terrain tree colliders={(nativeTreeColliders ? "ON" : "off")}.",
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
