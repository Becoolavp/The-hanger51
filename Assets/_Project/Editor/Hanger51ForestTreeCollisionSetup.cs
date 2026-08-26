using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51ForestTreeCollisionSetup
    {
        private const string TerrainObjectName = "Hanger 51 Editable Terrain";
        private const string PackRoot = "Assets/Supercyan Free Forest Sample";
        private const string TreePrefabRoot = PackRoot + "/Prefabs/High Quality/Tree";

        [MenuItem("Hanger 51/Environment/9 - Make Painted Forest Trees Solid")]
        public static void MakePaintedForestTreesSolid()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 9 failed. Exit Play mode before changing tree collision.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Environment Step 9 failed. Wait for Unity to finish compiling.");
                return;
            }

            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 9 failed. The Hanger 51 editable Terrain is missing.");
                return;
            }

            List<string> livingTreePaths = FindLivingTreePrefabPaths();
            if (livingTreePaths.Count == 0)
            {
                Debug.LogError("Environment Step 9 failed. No imported fir/broadleaf tree prefabs were found.");
                return;
            }

            int treesChecked = 0;
            int collidersCreated = 0;
            int collidersRepaired = 0;

            for (int index = 0; index < livingTreePaths.Count; index++)
            {
                string path = livingTreePaths[index];
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null)
                    {
                        Debug.LogWarning($"Environment Step 9 skipped '{path}' because Unity could not open the prefab contents.");
                        continue;
                    }

                    Collider solidCollider = FindPreferredSolidCollider(root);
                    if (solidCollider == null)
                    {
                        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                        ConfigureFallbackTrunkCapsule(root, capsule);
                        solidCollider = capsule;
                        collidersCreated++;
                    }
                    else
                    {
                        bool changed = false;
                        if (!solidCollider.enabled)
                        {
                            solidCollider.enabled = true;
                            changed = true;
                        }
                        if (solidCollider.isTrigger)
                        {
                            solidCollider.isTrigger = false;
                            changed = true;
                        }
                        if (changed)
                        {
                            collidersRepaired++;
                        }
                    }

                    // Keep the pack's optional detailed mesh collision disabled when a simpler
                    // trunk collider exists. The trunk should stop the player/aircraft without
                    // turning the complete leafy canopy into an oversized invisible wall.
                    MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
                    for (int meshIndex = 0; meshIndex < meshColliders.Length; meshIndex++)
                    {
                        MeshCollider mesh = meshColliders[meshIndex];
                        if (mesh != null && mesh != solidCollider && mesh.enabled)
                        {
                            mesh.enabled = false;
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    treesChecked++;
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
            }

            Undo.RecordObject(terrainCollider, "Enable painted tree collision");
            terrainCollider.terrainData = terrain.terrainData;
            terrainCollider.enabled = true;
            terrainCollider.isTrigger = false;

            bool foundTreeColliderFlag = SetTerrainTreeCollidersEnabled(terrainCollider, true);
            if (!foundTreeColliderFlag)
            {
                Debug.LogWarning(
                    "Environment Step 9 could not locate Unity's serialized 'Enable Tree Colliders' field automatically. "
                    + "Select the Terrain and verify Terrain Collider > Enable Tree Colliders is checked.",
                    terrainCollider);
            }

            terrain.drawTreesAndFoliage = true;

            // The tree prefabs were edited after they had already been registered on the Terrain.
            // Unity requires a prototype refresh for those prefab collider changes to propagate.
            terrain.terrainData.RefreshPrototypes();
            terrain.Flush();

            // Force the TerrainCollider to rebuild its PhysX representation. Tree colliders are
            // generated by the TerrainCollider at runtime, so this guarantees the next Play/Build
            // starts from the refreshed prototype/collider state even for trees painted earlier.
            TerrainData terrainData = terrain.terrainData;
            terrainCollider.enabled = false;
            terrainCollider.terrainData = null;
            terrainCollider.terrainData = terrainData;
            terrainCollider.enabled = true;
            Physics.SyncTransforms();

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrainCollider);
            EditorUtility.SetDirty(terrain.terrainData);

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
            Debug.Log(
                $"Environment Step 9 complete. Verified {treesChecked} imported living-tree prefab(s), created {collidersCreated} missing trunk collider(s), "
                + $"repaired {collidersRepaired} disabled/trigger collider(s), refreshed {terrain.terrainData.treeInstanceCount} already-painted tree instance(s), "
                + $"and {(foundTreeColliderFlag ? "enabled" : "attempted to enable")} Terrain Collider tree collision. "
                + "Existing painted fir/broadleaf trees should now block the CharacterController and physics-driven aircraft in Play mode/standalone.",
                terrain);
        }

        [MenuItem("Hanger 51/Environment/10 - Validate Painted Tree Collision")]
        public static void ValidatePaintedTreeCollision()
        {
            bool passed = true;
            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 10 failed: editable Terrain is missing.");
                return;
            }

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null || !terrainCollider.enabled || terrainCollider.isTrigger)
            {
                Debug.LogError("Environment Step 10 failed: TerrainCollider is missing, disabled, or configured as a trigger.", terrain);
                passed = false;
            }
            else
            {
                bool foundTreeColliderFlag;
                bool treeCollidersEnabled = GetTerrainTreeCollidersEnabled(terrainCollider, out foundTreeColliderFlag);
                if (!foundTreeColliderFlag)
                {
                    Debug.LogError(
                        "Environment Step 10 failed: Unity's serialized Enable Tree Colliders setting could not be found on this TerrainCollider.",
                        terrainCollider);
                    passed = false;
                }
                else if (!treeCollidersEnabled)
                {
                    Debug.LogError(
                        "Environment Step 10 failed: Terrain Collider > Enable Tree Colliders is OFF. Painted tree prefab colliders will not participate in physics.",
                        terrainCollider);
                    passed = false;
                }
            }

            List<string> livingTreePaths = FindLivingTreePrefabPaths();
            HashSet<GameObject> terrainTreePrefabs = new HashSet<GameObject>();
            TreePrototype[] prototypes = terrain.terrainData.treePrototypes ?? Array.Empty<TreePrototype>();
            for (int index = 0; index < prototypes.Length; index++)
            {
                TreePrototype prototype = prototypes[index];
                if (prototype != null && prototype.prefab != null)
                {
                    terrainTreePrefabs.Add(prototype.prefab);
                }
            }

            int validTrees = 0;
            for (int index = 0; index < livingTreePaths.Count; index++)
            {
                string path = livingTreePaths[index];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"Environment Step 10 failed: tree prefab could not be loaded: '{path}'.");
                    passed = false;
                    continue;
                }

                Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
                bool hasSolidEnabledCollider = false;
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    Collider collider = colliders[colliderIndex];
                    if (collider != null && collider.enabled && !collider.isTrigger)
                    {
                        hasSolidEnabledCollider = true;
                        break;
                    }
                }

                if (!hasSolidEnabledCollider)
                {
                    Debug.LogError($"Environment Step 10 failed: '{prefab.name}' has no enabled non-trigger collider.", prefab);
                    passed = false;
                }
                else if (!terrainTreePrefabs.Contains(prefab))
                {
                    Debug.LogError($"Environment Step 10 failed: '{prefab.name}' is solid but is missing from the Terrain Paint Trees palette.", prefab);
                    passed = false;
                }
                else
                {
                    validTrees++;
                }
            }

            if (livingTreePaths.Count == 0)
            {
                Debug.LogError("Environment Step 10 failed: no imported fir/broadleaf tree prefabs were found.");
                passed = false;
            }

            if (terrain.terrainData.treeInstanceCount <= 0)
            {
                Debug.LogWarning("Environment Step 10: no painted Terrain tree instances currently exist to test, although the collision setup itself is valid.", terrain);
            }

            if (passed)
            {
                Debug.Log(
                    $"Environment Step 10 passed. {validTrees}/{livingTreePaths.Count} imported living-tree prefab(s) have enabled solid trunk colliders, "
                    + "Terrain Collider > Enable Tree Colliders is ON, all living trees are registered in Paint Trees, "
                    + $"and {terrain.terrainData.treeInstanceCount} painted tree instance(s) are ready for runtime collision. "
                    + "Unity creates Terrain-tree physics colliders at runtime, so verify by walking into one in Play mode or the standalone build.",
                    terrain);
            }
        }

        private static bool SetTerrainTreeCollidersEnabled(TerrainCollider collider, bool enabled)
        {
            if (collider == null) return false;

            SerializedObject serialized = new SerializedObject(collider);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType != SerializedPropertyType.Boolean)
                {
                    continue;
                }

                string identifier = ((iterator.propertyPath ?? string.Empty) + " " + (iterator.displayName ?? string.Empty))
                    .Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .ToLowerInvariant();
                if (!identifier.Contains("tree") || !identifier.Contains("collider"))
                {
                    continue;
                }

                iterator.boolValue = enabled;
                serialized.ApplyModifiedProperties();
                return true;
            }
            return false;
        }

        private static bool GetTerrainTreeCollidersEnabled(TerrainCollider collider, out bool found)
        {
            found = false;
            if (collider == null) return false;

            SerializedObject serialized = new SerializedObject(collider);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType != SerializedPropertyType.Boolean)
                {
                    continue;
                }

                string identifier = ((iterator.propertyPath ?? string.Empty) + " " + (iterator.displayName ?? string.Empty))
                    .Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .ToLowerInvariant();
                if (!identifier.Contains("tree") || !identifier.Contains("collider"))
                {
                    continue;
                }

                found = true;
                return iterator.boolValue;
            }
            return false;
        }

        private static Collider FindPreferredSolidCollider(GameObject root)
        {
            if (root == null) return null;

            CapsuleCollider[] capsules = root.GetComponentsInChildren<CapsuleCollider>(true);
            if (capsules.Length > 0) return capsules[0];

            BoxCollider[] boxes = root.GetComponentsInChildren<BoxCollider>(true);
            if (boxes.Length > 0) return boxes[0];

            SphereCollider[] spheres = root.GetComponentsInChildren<SphereCollider>(true);
            if (spheres.Length > 0) return spheres[0];

            return null;
        }

        private static void ConfigureFallbackTrunkCapsule(GameObject root, CapsuleCollider capsule)
        {
            Bounds bounds = CalculateBoundsInRootSpace(root);
            float visibleHeight = Mathf.Max(1f, bounds.size.y);
            float horizontalSpan = Mathf.Max(0.5f, Mathf.Max(bounds.size.x, bounds.size.z));

            capsule.direction = 1;
            capsule.radius = Mathf.Clamp(horizontalSpan * 0.08f, 0.18f, 0.65f);
            capsule.height = Mathf.Max(capsule.radius * 2f, visibleHeight * 0.92f);
            capsule.center = new Vector3(
                bounds.center.x,
                bounds.min.y + capsule.height * 0.5f,
                bounds.center.z);
            capsule.enabled = true;
            capsule.isTrigger = false;
        }

        private static Bounds CalculateBoundsInRootSpace(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(1f, 3f, 1f));
            }

            bool initialized = false;
            Bounds result = default;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null) continue;

                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 local = root.transform.InverseTransformPoint(point);
                    if (!initialized)
                    {
                        result = new Bounds(local, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        result.Encapsulate(local);
                    }
                }
            }

            return initialized
                ? result
                : new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(1f, 3f, 1f));
        }

        private static List<string> FindLivingTreePrefabPaths()
        {
            List<string> result = new List<string>();
            if (!AssetDatabase.IsValidFolder(TreePrefabRoot)) return result;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TreePrefabRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (path.IndexOf("/Tree/Fir/", StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf("/Tree/Leaf/", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                result.Add(path);
            }
            return result;
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
