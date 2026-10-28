using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class TerrainTreeCollisionProxyStreamer : MonoBehaviour
    {
        [SerializeField] Terrain terrain;
        [SerializeField, Min(100f)] float activationRadius = 500f;
        [SerializeField, Min(0.05f)] float refreshInterval = 0.12f;
        [SerializeField, Min(64)] int maxProxies = 1800;
        [SerializeField, Min(25f)] float spatialCellSize = 100f;
        [SerializeField, Min(0.5f)] float trunkRadiusMultiplier = 1.15f;

        readonly Dictionary<Vector2Int, List<int>> spatial = new Dictionary<Vector2Int, List<int>>();
        readonly List<TreeRecord> trees = new List<TreeRecord>();
        readonly List<Proxy> pool = new List<Proxy>();
        readonly Dictionary<int, Proxy> activeByTree = new Dictionary<int, Proxy>();
        readonly HashSet<int> wanted = new HashSet<int>();
        readonly List<int> releaseScratch = new List<int>();
        readonly List<Transform> targets = new List<Transform>();

        float nextRefresh;
        float nextTargetRefresh;

        struct PrototypeShape
        {
            public Vector3 center;
            public float radius;
            public float height;
            public int direction;
        }

        struct TreeRecord
        {
            public Vector3 worldPosition;
            public float rotationDegrees;
            public float widthScale;
            public float heightScale;
            public int prototypeIndex;
            public Vector2Int cell;
        }

        sealed class Proxy
        {
            public GameObject gameObject;
            public CapsuleCollider collider;
            public int treeIndex = -1;
        }

        PrototypeShape[] shapes;
        bool[] validPrototypes;

        public int ActiveProxyCount => activeByTree.Count;

        public void Configure(Terrain sourceTerrain, float radius, float interval, int proxyLimit)
        {
            terrain = sourceTerrain;
            activationRadius = Mathf.Max(100f, radius);
            refreshInterval = Mathf.Max(0.05f, interval);
            maxProxies = Mathf.Max(64, proxyLimit);
        }

        void Awake()
        {
            if (!terrain) terrain = GetComponent<Terrain>();
            RebuildIndex();
            RefreshTargets();
            RefreshProxies();
        }

        void OnEnable()
        {
            if (!terrain) terrain = GetComponent<Terrain>();
            if (trees.Count == 0) RebuildIndex();
        }

        void OnDisable()
        {
            activeByTree.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].treeIndex = -1;
                if (pool[i].gameObject) pool[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                ResetProxyAssignments();
                return;
            }

            if (!terrain) return;

            if (Time.unscaledTime >= nextTargetRefresh)
            {
                RefreshTargets();
                nextTargetRefresh = Time.unscaledTime + 1.5f;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                RefreshProxies();
                nextRefresh = Time.unscaledTime + refreshInterval;
            }
        }

        public void RebuildIndex()
        {
            ResetProxyAssignments();
            trees.Clear();
            spatial.Clear();
            if (!terrain || !terrain.terrainData) return;

            TerrainData data = terrain.terrainData;
            TreePrototype[] prototypes = data.treePrototypes;
            shapes = new PrototypeShape[prototypes != null ? prototypes.Length : 0];
            validPrototypes = new bool[shapes.Length];
            for (int i = 0; i < shapes.Length; i++)
            {
                TreePrototype prototype = prototypes[i];
                validPrototypes[i] =
                    prototype != null &&
                    prototype.prefab &&
                    prototype.prefab.GetComponentInChildren<Renderer>(true) &&
                    prototype.prefab.GetComponentInChildren<CapsuleCollider>(true);
                shapes[i] = validPrototypes[i] ? ReadPrototypeShape(prototype) : DefaultShape();
            }

            TreeInstance[] instances = data.treeInstances;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            for (int i = 0; i < instances.Length; i++)
            {
                TreeInstance instance = instances[i];
                if (instance.prototypeIndex < 0 ||
                    instance.prototypeIndex >= validPrototypes.Length ||
                    !validPrototypes[instance.prototypeIndex])
                    continue;

                float worldX = origin.x + instance.position.x * size.x;
                float worldZ = origin.z + instance.position.z * size.z;
                Vector3 samplePoint = new Vector3(worldX, origin.y, worldZ);
                float worldY = terrain.SampleHeight(samplePoint) + origin.y;
                Vector3 p = new Vector3(worldX, worldY, worldZ);
                Vector2Int cell = CellFor(p);
                TreeRecord record = new TreeRecord
                {
                    worldPosition = p,
                    rotationDegrees = instance.rotation * Mathf.Rad2Deg,
                    widthScale = Mathf.Max(0.05f, instance.widthScale),
                    heightScale = Mathf.Max(0.05f, instance.heightScale),
                    prototypeIndex = instance.prototypeIndex,
                    cell = cell
                };
                int recordIndex = trees.Count;
                trees.Add(record);
                if (!spatial.TryGetValue(cell, out List<int> bucket))
                {
                    bucket = new List<int>();
                    spatial.Add(cell, bucket);
                }
                bucket.Add(recordIndex);
            }
        }

        void RefreshTargets()
        {
            targets.Clear();
            P51FlightController[] aircraft = FindObjectsByType<P51FlightController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < aircraft.Length; i++)
                if (aircraft[i]) targets.Add(aircraft[i].transform);

            CharacterController[] characters = FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Transform t = characters[i] ? characters[i].transform : null;
                if (t && !targets.Contains(t)) targets.Add(t);
            }
        }

        void RefreshProxies()
        {
            wanted.Clear();
            if (targets.Count == 0 || trees.Count == 0)
            {
                DeactivateUnused();
                return;
            }

            float radiusSq = activationRadius * activationRadius;
            int cellRadius = Mathf.CeilToInt(activationRadius / Mathf.Max(1f, spatialCellSize));

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                Transform target = targets[targetIndex];
                if (!target) continue;
                Vector3 position = target.position;
                Vector2Int center = CellFor(position);

                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        Vector2Int key = new Vector2Int(center.x + dx, center.y + dz);
                        if (!spatial.TryGetValue(key, out List<int> bucket)) continue;
                        for (int b = 0; b < bucket.Count; b++)
                        {
                            int treeIndex = bucket[b];
                            Vector3 d = trees[treeIndex].worldPosition - position;
                            d.y = 0f;
                            if (d.sqrMagnitude <= radiusSq) wanted.Add(treeIndex);
                        }
                    }
                }
            }

            int assigned = 0;
            foreach (int treeIndex in wanted)
            {
                if (assigned >= maxProxies) break;
                if (!activeByTree.TryGetValue(treeIndex, out Proxy proxy))
                {
                    proxy = AcquireProxy();
                    if (proxy == null) break;
                    activeByTree[treeIndex] = proxy;
                }

                // Reapply the exact TreeInstance transform every refresh. This prevents stale
                // pooled proxies from remaining at an old tree position after terrain edits.
                ConfigureProxy(proxy, treeIndex);
                assigned++;
            }

            DeactivateUnused();
        }

        Proxy AcquireProxy()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf)
                {
                    pool[i].gameObject.SetActive(true);
                    return pool[i];
                }
            }

            if (pool.Count >= maxProxies) return null;

            GameObject go = new GameObject("Terrain Tree Collision Proxy");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = terrain ? terrain.gameObject.layer : 0;
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            Proxy created = new Proxy { gameObject = go, collider = capsule, treeIndex = -1 };
            pool.Add(created);
            return created;
        }

        void ConfigureProxy(Proxy proxy, int treeIndex)
        {
            TreeRecord tree = trees[treeIndex];
            PrototypeShape shape = tree.prototypeIndex >= 0 && tree.prototypeIndex < shapes.Length
                ? shapes[tree.prototypeIndex]
                : DefaultShape();

            proxy.treeIndex = treeIndex;
            Vector3 grounded = tree.worldPosition;
            grounded.y = terrain ? terrain.SampleHeight(grounded) + terrain.transform.position.y : grounded.y;
            proxy.gameObject.transform.position = grounded;
            proxy.gameObject.transform.rotation = Quaternion.Euler(0f, tree.rotationDegrees, 0f);
            proxy.gameObject.transform.localScale = Vector3.one;

            float radialScale = tree.widthScale;
            float verticalScale = tree.heightScale;
            proxy.collider.direction = shape.direction;
            proxy.collider.center = new Vector3(
                shape.center.x * radialScale,
                shape.center.y * verticalScale,
                shape.center.z * radialScale);
            proxy.collider.radius = Mathf.Max(0.18f, shape.radius * radialScale * trunkRadiusMultiplier);
            proxy.collider.height = Mathf.Max(proxy.collider.radius * 2f, shape.height * verticalScale);
            proxy.collider.enabled = true;
        }

        public void RebuildAndRefreshNow()
        {
            if (!terrain) terrain = GetComponent<Terrain>();
            RebuildIndex();

            if (!Application.isPlaying)
            {
                ResetProxyAssignments();
                return;
            }

            RefreshTargets();
            EnsurePool();
            RefreshProxies();
        }

        public void ClearAllProxiesNow()
        {
            ResetProxyAssignments();
        }

        public int ValidateActiveProxyAlignment(float tolerance, out float maximumError)
        {
            maximumError = 0f;
            int failures = 0;
            float limit = Mathf.Max(.001f, tolerance);

            foreach (KeyValuePair<int, Proxy> pair in activeByTree)
            {
                int treeIndex = pair.Key;
                Proxy proxy = pair.Value;
                if (treeIndex < 0 || treeIndex >= trees.Count || proxy == null || !proxy.gameObject)
                {
                    failures++;
                    continue;
                }

                Vector3 expected = trees[treeIndex].worldPosition;
                expected.y = terrain ? terrain.SampleHeight(expected) + terrain.transform.position.y : expected.y;
                float error = Vector3.Distance(proxy.gameObject.transform.position, expected);
                maximumError = Mathf.Max(maximumError, error);
                if (error > limit) failures++;
            }
            return failures;
        }

        void ResetProxyAssignments()
        {
            activeByTree.Clear();
            wanted.Clear();
            releaseScratch.Clear();

            for (int i = 0; i < pool.Count; i++)
            {
                Proxy proxy = pool[i];
                if (proxy == null || !proxy.gameObject) continue;
                proxy.treeIndex = -1;
                if (proxy.collider) proxy.collider.enabled = false;
                proxy.gameObject.SetActive(false);
            }
        }

        void DeactivateUnused()
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<int, Proxy> pair in activeByTree)
            {
                if (!wanted.Contains(pair.Key)) releaseScratch.Add(pair.Key);
            }

            for (int i = 0; i < releaseScratch.Count; i++)
            {
                int treeIndex = releaseScratch[i];
                Proxy proxy = activeByTree[treeIndex];
                activeByTree.Remove(treeIndex);
                proxy.treeIndex = -1;
                proxy.collider.enabled = false;
                proxy.gameObject.SetActive(false);
            }
        }

        PrototypeShape ReadPrototypeShape(TreePrototype prototype)
        {
            if (prototype != null && prototype.prefab)
            {
                CapsuleCollider capsule = prototype.prefab.GetComponentInChildren<CapsuleCollider>(true);
                if (capsule)
                {
                    Transform source = capsule.transform;
                    Vector3 centerWorld = source.TransformPoint(capsule.center);
                    Vector3 centerLocal = prototype.prefab.transform.InverseTransformPoint(centerWorld);
                    Vector3 lossy = source.lossyScale;
                    float radialScale = capsule.direction == 0
                        ? Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z))
                        : capsule.direction == 1
                            ? Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z))
                            : Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
                    float heightScale = capsule.direction == 0
                        ? Mathf.Abs(lossy.x)
                        : capsule.direction == 1
                            ? Mathf.Abs(lossy.y)
                            : Mathf.Abs(lossy.z);
                    return new PrototypeShape
                    {
                        center = centerLocal,
                        radius = Mathf.Max(0.12f, capsule.radius * radialScale),
                        height = Mathf.Max(capsule.radius * 2f, capsule.height * heightScale),
                        direction = capsule.direction
                    };
                }

                string n = prototype.prefab.name.ToLowerInvariant();
                if (n.Contains("fir"))
                    return new PrototypeShape
                    {
                        center = new Vector3(.04f, 1.6552377f, -.01f),
                        radius = .25f,
                        height = 3.5331335f,
                        direction = 1
                    };
                if (n.Contains("leaf"))
                    return new PrototypeShape
                    {
                        center = new Vector3(.05f, 1.0146283f, -.0314104f),
                        radius = .33f,
                        height = 2.3331134f,
                        direction = 1
                    };

                return DefaultShape();
            }
            return DefaultShape();
        }

        PrototypeShape DefaultShape()
        {
            return new PrototypeShape { center = Vector3.up * 1.5f, radius = .35f, height = 3f, direction = 1 };
        }

        Vector2Int CellFor(Vector3 world)
        {
            float size = Mathf.Max(1f, spatialCellSize);
            return new Vector2Int(Mathf.FloorToInt(world.x / size), Mathf.FloorToInt(world.z / size));
        }
    }
}
