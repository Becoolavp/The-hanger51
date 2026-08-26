using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51PaintedTreeCollisionProxySetup
    {
        private const string TerrainObjectName = "Hanger 51 Editable Terrain";
        private const string ProxyRootName = "Hanger 51 Painted Tree Collision Proxies";
        private const string TreePackRoot = "Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree";

        [MenuItem("Hanger 51/Environment/11 - Rebuild Painted Tree Hitboxes")]
        public static void RebuildPaintedTreeHitboxes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 11 failed. Exit Play mode before rebuilding painted-tree hitboxes.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Environment Step 11 failed. Wait for Unity to finish compiling.");
                return;
            }

            if (!SyncForBuild(true))
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        [MenuItem("Hanger 51/Environment/12 - Validate Painted Tree Hitboxes")]
        public static void ValidatePaintedTreeHitboxes()
        {
            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 12 failed: editable Terrain is missing.");
                return;
            }

            int expected = CountPaintedLivingTrees(terrain);
            Transform root = terrain.transform.Find(ProxyRootName);

            if (expected <= 0)
            {
                Debug.LogWarning("Environment Step 12 found no painted fir/leaf tree instances to validate.", terrain);
                return;
            }

            if (root == null)
            {
                Debug.LogError($"Environment Step 12 failed: expected {expected} painted-tree hitboxes, but '{ProxyRootName}' is missing. Run Step 11.", terrain);
                return;
            }

            CapsuleCollider[] colliders = root.GetComponentsInChildren<CapsuleCollider>(true);
            int valid = 0;
            for (int index = 0; index < colliders.Length; index++)
            {
                CapsuleCollider collider = colliders[index];
                if (collider != null
                    && collider.enabled
                    && !collider.isTrigger
                    && collider.radius > 0.02f
                    && collider.height >= collider.radius * 2f)
                {
                    valid++;
                }
            }

            if (valid != expected)
            {
                Debug.LogError($"Environment Step 12 failed: painted living trees={expected}, valid explicit trunk hitboxes={valid}. Run Step 11 again.", terrain);
                return;
            }

            Debug.Log(
                $"Environment Step 12 passed. {valid}/{expected} currently painted fir/leaf trees have explicit scene CapsuleCollider trunk hitboxes. "
                + "These are ordinary Unity physics colliders and do not depend on TerrainCollider tree-collider support.",
                terrain);
        }

        public static bool SyncForBuild(bool logSuccess)
        {
            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                // Utility scenes can legitimately have no world Terrain.
                return true;
            }

            TerrainData data = terrain.terrainData;
            TreeInstance[] instances = data.treeInstances ?? Array.Empty<TreeInstance>();
            TreePrototype[] prototypes = data.treePrototypes ?? Array.Empty<TreePrototype>();

            Transform oldRoot = terrain.transform.Find(ProxyRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
            }

            GameObject rootObject = new GameObject(ProxyRootName);
            rootObject.transform.SetParent(terrain.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;

            Dictionary<GameObject, CapsuleTemplate> templates = new Dictionary<GameObject, CapsuleTemplate>();
            int livingTreeInstances = 0;
            int proxiesCreated = 0;
            int missingColliderPrefabs = 0;

            for (int index = 0; index < instances.Length; index++)
            {
                TreeInstance instance = instances[index];
                if (instance.prototypeIndex < 0 || instance.prototypeIndex >= prototypes.Length)
                {
                    continue;
                }

                TreePrototype prototype = prototypes[instance.prototypeIndex];
                GameObject prefab = prototype != null ? prototype.prefab : null;
                if (!IsLivingForestTree(prefab))
                {
                    continue;
                }

                livingTreeInstances++;

                CapsuleTemplate template;
                if (!templates.TryGetValue(prefab, out template))
                {
                    if (!TryCreateCapsuleTemplate(prefab, out template))
                    {
                        missingColliderPrefabs++;
                        templates[prefab] = CapsuleTemplate.Invalid;
                        continue;
                    }

                    templates[prefab] = template;
                }

                if (!template.valid)
                {
                    continue;
                }

                CreateProxy(rootObject.transform, data, instance, template, prefab.name, index, terrain.gameObject.layer);
                proxiesCreated++;
            }

            // Keep the normal TerrainCollider active for the ground itself. The painted-tree
            // proxies above are independent ordinary colliders and require no TerrainCollider
            // tree-specific API or serialized setting.
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
            }
            terrainCollider.terrainData = data;
            terrainCollider.enabled = true;
            terrainCollider.isTrigger = false;

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrainCollider);
            EditorUtility.SetDirty(rootObject);
            Physics.SyncTransforms();

            Scene scene = terrain.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            if (missingColliderPrefabs > 0 || proxiesCreated != livingTreeInstances)
            {
                Debug.LogError(
                    $"Painted-tree hitbox sync incomplete. Painted living trees={livingTreeInstances}, explicit hitboxes={proxiesCreated}, prefab collider failures={missingColliderPrefabs}. "
                    + "Run Environment Step 9 once to repair the imported tree prefab colliders, then rerun Step 11.",
                    terrain);
                return false;
            }

            if (logSuccess)
            {
                Debug.Log(
                    $"Environment Step 11 complete. Built {proxiesCreated} explicit CapsuleCollider trunk hitbox(es) for {livingTreeInstances} currently painted fir/leaf tree instance(s). "
                    + "These are real scene colliders, so the Player and physics-driven aircraft should collide with the painted trunks in Play mode. "
                    + "Future builds resync the hitboxes automatically.",
                    terrain);
            }

            return true;
        }

        private static void CreateProxy(
            Transform parent,
            TerrainData data,
            TreeInstance instance,
            CapsuleTemplate template,
            string prefabName,
            int treeIndex,
            int layer)
        {
            float widthScale = Mathf.Max(0.01f, instance.widthScale);
            float heightScale = Mathf.Max(0.01f, instance.heightScale);
            Vector3 instanceScale = new Vector3(widthScale, heightScale, widthScale);

            Quaternion treeRotation = Quaternion.Euler(0f, instance.rotation * Mathf.Rad2Deg, 0f);
            Vector3 instanceLocalPosition = new Vector3(
                instance.position.x * data.size.x,
                instance.position.y * data.size.y,
                instance.position.z * data.size.z);

            Vector3 scaledCenter = Vector3.Scale(template.centerInPrefabRoot, instanceScale);
            Vector3 colliderLocalPosition = instanceLocalPosition + treeRotation * scaledCenter;

            GameObject proxy = new GameObject($"Tree Hitbox {treeIndex:D6} - {prefabName}");
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = colliderLocalPosition;
            proxy.transform.localRotation = treeRotation * template.rotationInPrefabRoot;
            proxy.transform.localScale = Vector3.one;
            proxy.layer = layer;
            proxy.isStatic = true;

            CapsuleCollider collider = proxy.AddComponent<CapsuleCollider>();
            collider.center = Vector3.zero;
            collider.direction = template.direction;
            collider.radius = Mathf.Max(0.04f, template.radius * GetRadialScale(instanceScale, template.direction));
            collider.height = Mathf.Max(
                collider.radius * 2f,
                template.height * GetAxisScale(instanceScale, template.direction));
            collider.enabled = true;
            collider.isTrigger = false;
        }

        private static bool TryCreateCapsuleTemplate(GameObject prefab, out CapsuleTemplate template)
        {
            template = CapsuleTemplate.Invalid;
            if (prefab == null)
            {
                return false;
            }

            CapsuleCollider[] colliders = prefab.GetComponentsInChildren<CapsuleCollider>(true);
            CapsuleCollider source = null;
            for (int index = 0; index < colliders.Length; index++)
            {
                CapsuleCollider candidate = colliders[index];
                if (candidate == null || candidate.isTrigger)
                {
                    continue;
                }

                source = candidate;
                if (candidate.enabled)
                {
                    break;
                }
            }

            if (source == null)
            {
                return false;
            }

            Transform prefabRoot = prefab.transform;
            Vector3 centerWorld = source.transform.TransformPoint(source.center);
            Vector3 centerInRoot = prefabRoot.InverseTransformPoint(centerWorld);
            Quaternion rotationInRoot = Quaternion.Inverse(prefabRoot.rotation) * source.transform.rotation;

            Vector3 relativeScale = DivideScale(source.transform.lossyScale, prefabRoot.lossyScale);
            float radialScale = GetRadialScale(relativeScale, source.direction);
            float axisScale = GetAxisScale(relativeScale, source.direction);

            template = new CapsuleTemplate
            {
                valid = true,
                centerInPrefabRoot = centerInRoot,
                rotationInPrefabRoot = rotationInRoot,
                direction = source.direction,
                radius = Mathf.Max(0.04f, source.radius * radialScale),
                height = Mathf.Max(source.radius * radialScale * 2f, source.height * axisScale)
            };

            return true;
        }

        private static Vector3 DivideScale(Vector3 numerator, Vector3 denominator)
        {
            return new Vector3(
                SafeDivide(numerator.x, denominator.x),
                SafeDivide(numerator.y, denominator.y),
                SafeDivide(numerator.z, denominator.z));
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) > 0.0001f
                ? Mathf.Abs(numerator / denominator)
                : Mathf.Abs(numerator);
        }

        private static float GetAxisScale(Vector3 scale, int direction)
        {
            switch (direction)
            {
                case 0: return Mathf.Abs(scale.x);
                case 2: return Mathf.Abs(scale.z);
                default: return Mathf.Abs(scale.y);
            }
        }

        private static float GetRadialScale(Vector3 scale, int direction)
        {
            switch (direction)
            {
                case 0: return Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                case 2: return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                default: return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            }
        }

        private static int CountPaintedLivingTrees(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return 0;
            }

            TreePrototype[] prototypes = terrain.terrainData.treePrototypes ?? Array.Empty<TreePrototype>();
            TreeInstance[] instances = terrain.terrainData.treeInstances ?? Array.Empty<TreeInstance>();
            int count = 0;

            for (int index = 0; index < instances.Length; index++)
            {
                int prototypeIndex = instances[index].prototypeIndex;
                if (prototypeIndex < 0 || prototypeIndex >= prototypes.Length)
                {
                    continue;
                }

                TreePrototype prototype = prototypes[prototypeIndex];
                if (prototype != null && IsLivingForestTree(prototype.prefab))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsLivingForestTree(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(path)
                || !path.StartsWith(TreePackRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.IndexOf("/Fir/", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("/Leaf/", StringComparison.OrdinalIgnoreCase) >= 0;
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

        private struct CapsuleTemplate
        {
            public bool valid;
            public Vector3 centerInPrefabRoot;
            public Quaternion rotationInPrefabRoot;
            public int direction;
            public float radius;
            public float height;

            public static CapsuleTemplate Invalid
            {
                get { return new CapsuleTemplate { valid = false }; }
            }
        }
    }
}
