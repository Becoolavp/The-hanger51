using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldGhostTreeHitboxPurge
    {
        const string TerrainName = "Hanger 51 Editable Terrain";

        [MenuItem("Hanger 51/World/Current/126 - Purge Ghost Tree Hitboxes")]
        public static void Purge()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 126 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Terrain terrain = FindTerrain();
            TerrainTreeCollisionProxyStreamer streamer = terrain ? terrain.GetComponent<TerrainTreeCollisionProxyStreamer>() : null;
            if (streamer) streamer.ClearAllProxiesNow();

            int destroyed = 0;
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = all.Length - 1; i >= 0; i--)
            {
                GameObject go = all[i];
                if (!go) continue;

                bool oldProxy = go.name == "Terrain Tree Collision Proxy";
                bool newRuntimeProxy = go.name == "H51 Runtime Tree Collision Proxy";
                if (!oldProxy && !newRuntimeProxy) continue;

                CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
                if (!capsule) continue;

                Object.DestroyImmediate(go);
                destroyed++;
            }

            if (terrain)
            {
                TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
                if (tc)
                {
                    SerializedObject so = new SerializedObject(tc);
                    SerializedProperty enableTrees = so.FindProperty("m_EnableTreeColliders");
                    if (enableTrees != null) enableTrees.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(tc);
                }

                if (streamer)
                {
                    streamer.Configure(terrain, 360f, .10f, 1800);
                    streamer.RebuildIndex();
                    streamer.ClearAllProxiesNow();
                    EditorUtility.SetDirty(streamer);
                }

                EditorUtility.SetDirty(terrain);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            int remaining = CountHiddenProxyObjects();
            if (remaining == 0)
                Debug.Log($"Step 126 complete. Destroyed {destroyed} orphaned/hidden Terrain tree proxy objects. Remaining hidden tree proxies in Edit mode=0. Runtime tree hitboxes will only be created after entering Play mode.", terrain);
            else
                Debug.LogError($"Step 126 completed but {remaining} hidden Terrain tree proxy objects still remain in Edit mode.", terrain);
        }

        [MenuItem("Hanger 51/World/Current/127 - Validate No Ghost Tree Hitboxes")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Step 127 is an Edit-mode validation. Exit Play mode first.");
                return;
            }

            int remaining = CountHiddenProxyObjects();
            Terrain terrain = FindTerrain();
            TerrainTreeCollisionProxyStreamer streamer = terrain ? terrain.GetComponent<TerrainTreeCollisionProxyStreamer>() : null;

            bool nativeTreeColliders = false;
            if (terrain)
            {
                TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
                if (tc)
                {
                    SerializedObject so = new SerializedObject(tc);
                    SerializedProperty enableTrees = so.FindProperty("m_EnableTreeColliders");
                    if (enableTrees != null) nativeTreeColliders = enableTrees.boolValue;
                }
            }

            int active = streamer ? streamer.ActiveProxyCount : 0;
            bool ok = remaining == 0 && active == 0 && !nativeTreeColliders;

            if (ok)
                Debug.Log("Step 127 passed. Edit mode contains zero hidden tree collision proxies, streamer active proxies=0, and Unity native Terrain tree colliders=off. Tree hitboxes will exist only at runtime around the aircraft/player.", terrain);
            else
                Debug.LogError($"Step 127 failed. hidden proxy objects={remaining}, streamer active proxies={active}, Unity native Terrain tree colliders={(nativeTreeColliders ? "ON" : "off")}.", terrain);
        }

        static int CountHiddenProxyObjects()
        {
            int count = 0;
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject go in all)
            {
                if (!go) continue;
                if ((go.name == "Terrain Tree Collision Proxy" || go.name == "H51 Runtime Tree Collision Proxy") &&
                    go.GetComponent<CapsuleCollider>())
                    count++;
            }
            return count;
        }

        static Terrain FindTerrain()
        {
            GameObject named = GameObject.Find(TerrainName);
            Terrain terrain = named ? (named.GetComponent<Terrain>() ?? named.GetComponentInChildren<Terrain>(true)) : null;
            if (terrain) return terrain;

            Terrain[] all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }
    }
}
