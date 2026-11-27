using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldCurrentCleanup166
    {
        const string BusPrefix = "Roadside Bus Stop -";
        const float BusClearanceBeyondRoadEdge = 8.5f;
        const float InteriorGrassMargin = 1.15f;

        struct RoadHit
        {
            public bool valid;
            public Vector3 center;
            public Vector3 tangent;
            public float width;
            public float distance;
        }

        [MenuItem("Hanger 51/World/Current/166 - Repair Bus Stops And Interior Grass")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 166 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Terrain terrain =
                FindTerrain();
            if (!terrain || !terrain.terrainData)
            {
                Debug.LogError(
                    "Step 166 failed. No editable Terrain was found.");
                return;
            }

            List<Transform> roadSurfaces =
                FindRoadSurfaces();
            if (roadSurfaces.Count == 0)
            {
                Debug.LogError(
                    "Step 166 failed. No generated Road Surface meshes were found.");
                return;
            }

            Transform[] all =
                UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            List<Transform> busStops =
                all.Where(t =>
                    t &&
                    t.gameObject.scene.IsValid() &&
                    t.name.StartsWith(
                        BusPrefix,
                        StringComparison.Ordinal))
                    .ToList();

            int busRepaired = 0;
            for (int i=0;i<busStops.Count;i++)
            {
                Transform bus =
                    busStops[i];
                RoadHit hit =
                    FindNearestRoad(
                        bus.position,
                        roadSurfaces);

                if (!hit.valid)
                    continue;

                Vector3 side =
                    Vector3.Cross(
                        Vector3.up,
                        hit.tangent)
                        .normalized;

                float signed =
                    Vector3.Dot(
                        bus.position - hit.center,
                        side);

                float sign =
                    Mathf.Abs(signed) > .2f
                        ? Mathf.Sign(signed)
                        : ((bus.name.GetHashCode() & 1) == 0 ? 1f : -1f);

                Vector3 outward =
                    side * sign;

                Vector3 repaired =
                    hit.center +
                    outward *
                    (hit.width * .5f +
                     BusClearanceBeyondRoadEdge);

                repaired.y =
                    terrain.SampleHeight(repaired) +
                    terrain.transform.position.y +
                    .02f;

                Undo.RecordObject(
                    bus,
                    "Repair roadside bus stop");
                bus.position =
                    repaired;

                // The shelter's long local-X axis runs parallel to the road.
                // Local +Z is the open/front side, so point +Z back toward traffic.
                Vector3 towardRoad =
                    -outward;
                bus.rotation =
                    Quaternion.LookRotation(
                        towardRoad,
                        Vector3.up);

                EditorUtility.SetDirty(
                    bus);
                busRepaired++;
            }

            List<Bounds> interiors =
                FindCivicInteriorFootprints();

            if (interiors.Count == 0)
            {
                Debug.LogWarning(
                    "Step 166 found no theater/hospital Interior Floor footprints. Bus stops were still repaired.");
            }

            int detailCells =
                ClearTerrainDetails(
                    terrain,
                    interiors);

            int grassObjects =
                RemoveSpawnedGrass(
                    interiors);

            EditorUtility.SetDirty(
                terrain);
            EditorUtility.SetDirty(
                terrain.terrainData);

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 166 complete. Bus stops repaired=" +
                busRepaired + "/" + busStops.Count +
                ", theater/medical interior footprints=" +
                interiors.Count +
                ", terrain grass/detail cells cleared=" +
                detailCells +
                ", spawned grass objects removed=" +
                grassObjects +
                ". Bus shelters now sit beyond the road shoulder with their long side parallel to the road and their opening facing traffic. Run Step 167.");
        }

        [MenuItem("Hanger 51/World/Current/167 - Validate Bus Stops And Interior Grass")]
        public static void Validate()
        {
            Terrain terrain =
                FindTerrain();

            List<Transform> roads =
                FindRoadSurfaces();

            Transform[] all =
                UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            List<Transform> buses =
                all.Where(t =>
                    t &&
                    t.gameObject.scene.IsValid() &&
                    t.name.StartsWith(
                        BusPrefix,
                        StringComparison.Ordinal))
                    .ToList();

            int tooClose = 0;
            int badFacing = 0;

            for (int i=0;i<buses.Count;i++)
            {
                RoadHit hit =
                    FindNearestRoad(
                        buses[i].position,
                        roads);

                if (!hit.valid)
                {
                    tooClose++;
                    continue;
                }

                float required =
                    hit.width * .5f +
                    6.4f;

                if (hit.distance < required)
                    tooClose++;

                Vector3 side =
                    Vector3.Cross(
                        Vector3.up,
                        hit.tangent)
                        .normalized;

                Vector3 fromRoad =
                    buses[i].position -
                    hit.center;
                fromRoad.y = 0f;

                if (fromRoad.sqrMagnitude < .01f)
                {
                    badFacing++;
                    continue;
                }

                Vector3 towardRoad =
                    -fromRoad.normalized;

                float openingFacesRoad =
                    Vector3.Dot(
                        buses[i].forward,
                        towardRoad);

                float longSideFollowsRoad =
                    Mathf.Abs(
                        Vector3.Dot(
                            buses[i].right,
                            hit.tangent));

                if (openingFacesRoad < .90f ||
                    longSideFollowsRoad < .90f)
                    badFacing++;
            }

            List<Bounds> interiors =
                FindCivicInteriorFootprints();

            int remainingDetail =
                terrain
                    ? CountTerrainDetailInside(
                        terrain,
                        interiors)
                    : int.MaxValue;

            int remainingGrassObjects =
                CountSpawnedGrassInside(
                    interiors);

            bool ok =
                terrain &&
                roads.Count > 0 &&
                buses.Count > 0 &&
                tooClose == 0 &&
                badFacing == 0 &&
                interiors.Count > 0 &&
                remainingDetail == 0 &&
                remainingGrassObjects == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 167 passed. Bus stops=" +
                    buses.Count +
                    " are clear of the roadway and aligned with the road. Civic interior footprints=" +
                    interiors.Count +
                    " have zero terrain-detail grass and zero spawned grass objects.");
            }
            else
            {
                Debug.LogError(
                    "Step 167 failed. terrain=" +
                    (terrain ? "yes" : "MISSING") +
                    ", road surfaces=" + roads.Count +
                    ", bus stops=" + buses.Count +
                    ", bus stops too close=" + tooClose +
                    ", bad bus orientation=" + badFacing +
                    ", civic interior footprints=" + interiors.Count +
                    ", remaining terrain detail density=" + remainingDetail +
                    ", remaining spawned grass objects=" + remainingGrassObjects +
                    ".");
            }
        }

        static List<Transform> FindRoadSurfaces()
        {
            MeshFilter[] filters =
                UnityEngine.Object.FindObjectsByType<MeshFilter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            List<Transform> result =
                new List<Transform>();

            for (int i=0;i<filters.Length;i++)
            {
                MeshFilter filter =
                    filters[i];

                if (!filter ||
                    !filter.gameObject.scene.IsValid() ||
                    filter.name != "Road Surface" ||
                    !filter.sharedMesh ||
                    filter.sharedMesh.vertexCount < 4)
                    continue;

                result.Add(
                    filter.transform);
            }

            return result;
        }

        static RoadHit FindNearestRoad(
            Vector3 point,
            List<Transform> roadSurfaces)
        {
            RoadHit best =
                new RoadHit
                {
                    valid = false,
                    distance = float.MaxValue,
                    tangent = Vector3.forward,
                    width = 6f,
                    center = point
                };

            for (int r=0;r<roadSurfaces.Count;r++)
            {
                Transform road =
                    roadSurfaces[r];
                MeshFilter filter =
                    road
                        ? road.GetComponent<MeshFilter>()
                        : null;
                Mesh mesh =
                    filter
                        ? filter.sharedMesh
                        : null;

                if (!mesh)
                    continue;

                Vector3[] vertices =
                    mesh.vertices;

                int samples =
                    vertices.Length / 2;

                if (samples < 2)
                    continue;

                Vector3 previousCenter =
                    Vector3.zero;
                float previousWidth =
                    6f;

                for (int i=0;i<samples;i++)
                {
                    Vector3 left =
                        road.TransformPoint(
                            vertices[i*2]);
                    Vector3 right =
                        road.TransformPoint(
                            vertices[i*2+1]);

                    Vector3 center =
                        (left + right) * .5f;
                    float width =
                        Planar(left,right);

                    if (i > 0)
                    {
                        float u;
                        Vector3 nearest =
                            ClosestXZ(
                                point,
                                previousCenter,
                                center,
                                out u);

                        float distance =
                            Planar(
                                point,
                                nearest);

                        if (distance < best.distance)
                        {
                            Vector3 tangent =
                                center -
                                previousCenter;
                            tangent.y = 0f;

                            if (tangent.sqrMagnitude > .001f)
                            {
                                best.valid = true;
                                best.center = nearest;
                                best.tangent = tangent.normalized;
                                best.width =
                                    Mathf.Lerp(
                                        previousWidth,
                                        width,
                                        u);
                                best.distance = distance;
                            }
                        }
                    }

                    previousCenter =
                        center;
                    previousWidth =
                        width;
                }
            }

            return best;
        }

        static Vector3 ClosestXZ(
            Vector3 p,
            Vector3 a,
            Vector3 b,
            out float u)
        {
            Vector2 point =
                new Vector2(p.x,p.z);
            Vector2 start =
                new Vector2(a.x,a.z);
            Vector2 delta =
                new Vector2(
                    b.x-a.x,
                    b.z-a.z);

            u =
                delta.sqrMagnitude < .001f
                    ? 0f
                    : Mathf.Clamp01(
                        Vector2.Dot(
                            point-start,
                            delta) /
                        delta.sqrMagnitude);

            return
                Vector3.Lerp(
                    a,
                    b,
                    u);
        }

        static List<Bounds> FindCivicInteriorFootprints()
        {
            Renderer[] renderers =
                UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            List<Bounds> result =
                new List<Bounds>();

            for (int i=0;i<renderers.Length;i++)
            {
                Renderer renderer =
                    renderers[i];

                if (!renderer ||
                    !renderer.gameObject.scene.IsValid() ||
                    renderer.name != "Interior Floor" ||
                    !IsTheaterOrMedicalHierarchy(renderer.transform))
                    continue;

                Bounds bounds =
                    renderer.bounds;

                bounds.Expand(
                    new Vector3(
                        InteriorGrassMargin * 2f,
                        3f,
                        InteriorGrassMargin * 2f));

                result.Add(
                    bounds);
            }

            return result;
        }

        static bool IsTheaterOrMedicalHierarchy(
            Transform transform)
        {
            Transform current =
                transform;

            while (current)
            {
                string n =
                    current.name.ToLowerInvariant();

                if (n.Contains("theater") ||
                    n.Contains("cinema") ||
                    n.Contains("hospital") ||
                    n.Contains("medical"))
                    return true;

                current =
                    current.parent;
            }

            return false;
        }

        static int ClearTerrainDetails(
            Terrain terrain,
            List<Bounds> footprints)
        {
            if (!terrain ||
                !terrain.terrainData ||
                footprints.Count == 0)
                return 0;

            TerrainData data =
                terrain.terrainData;

            int width =
                data.detailWidth;
            int height =
                data.detailHeight;
            int layers =
                data.detailPrototypes != null
                    ? data.detailPrototypes.Length
                    : 0;

            if (width <= 0 ||
                height <= 0 ||
                layers <= 0)
                return 0;

            Undo.RegisterCompleteObjectUndo(
                data,
                "Clear civic interior grass");

            int changed = 0;

            for (int layer=0;layer<layers;layer++)
            {
                int[,] map =
                    data.GetDetailLayer(
                        0,
                        0,
                        width,
                        height,
                        layer);

                for (int f=0;f<footprints.Count;f++)
                {
                    Bounds bounds =
                        footprints[f];

                    WorldBoundsToDetailRect(
                        terrain,
                        bounds,
                        width,
                        height,
                        out int minX,
                        out int maxX,
                        out int minZ,
                        out int maxZ);

                    for (int z=minZ;z<=maxZ;z++)
                    for (int x=minX;x<=maxX;x++)
                    {
                        if (map[z,x] == 0)
                            continue;

                        changed +=
                            map[z,x];
                        map[z,x] = 0;
                    }
                }

                data.SetDetailLayer(
                    0,
                    0,
                    layer,
                    map);
            }

            return changed;
        }

        static int CountTerrainDetailInside(
            Terrain terrain,
            List<Bounds> footprints)
        {
            if (!terrain ||
                !terrain.terrainData ||
                footprints.Count == 0)
                return 0;

            TerrainData data =
                terrain.terrainData;
            int width =
                data.detailWidth;
            int height =
                data.detailHeight;
            int layers =
                data.detailPrototypes != null
                    ? data.detailPrototypes.Length
                    : 0;

            int total = 0;

            for (int layer=0;layer<layers;layer++)
            {
                int[,] map =
                    data.GetDetailLayer(
                        0,
                        0,
                        width,
                        height,
                        layer);

                for (int f=0;f<footprints.Count;f++)
                {
                    WorldBoundsToDetailRect(
                        terrain,
                        footprints[f],
                        width,
                        height,
                        out int minX,
                        out int maxX,
                        out int minZ,
                        out int maxZ);

                    for (int z=minZ;z<=maxZ;z++)
                    for (int x=minX;x<=maxX;x++)
                        total += map[z,x];
                }
            }

            return total;
        }

        static void WorldBoundsToDetailRect(
            Terrain terrain,
            Bounds bounds,
            int width,
            int height,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            Vector3 origin =
                terrain.transform.position;
            Vector3 size =
                terrain.terrainData.size;

            minX = Mathf.Clamp(
                Mathf.FloorToInt(
                    (bounds.min.x-origin.x) /
                    size.x *
                    width),
                0,
                width-1);

            maxX = Mathf.Clamp(
                Mathf.CeilToInt(
                    (bounds.max.x-origin.x) /
                    size.x *
                    width),
                0,
                width-1);

            minZ = Mathf.Clamp(
                Mathf.FloorToInt(
                    (bounds.min.z-origin.z) /
                    size.z *
                    height),
                0,
                height-1);

            maxZ = Mathf.Clamp(
                Mathf.CeilToInt(
                    (bounds.max.z-origin.z) /
                    size.z *
                    height),
                0,
                height-1);
        }

        static int RemoveSpawnedGrass(
            List<Bounds> footprints)
        {
            if (footprints.Count == 0)
                return 0;

            Transform[] all =
                UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            HashSet<GameObject> remove =
                new HashSet<GameObject>();

            for (int i=0;i<all.Length;i++)
            {
                Transform transform =
                    all[i];

                if (!transform ||
                    !transform.gameObject.scene.IsValid() ||
                    !IsSpawnedGrassName(transform.name) ||
                    !InsideAnyXZ(
                        transform.position,
                        footprints))
                    continue;

                Transform root =
                    transform;

                while (root.parent &&
                       IsSpawnedGrassName(
                           root.parent.name))
                    root =
                        root.parent;

                remove.Add(
                    root.gameObject);
            }

            foreach (GameObject grass in remove)
            {
                if (grass)
                    Undo.DestroyObjectImmediate(
                        grass);
            }

            return remove.Count;
        }

        static int CountSpawnedGrassInside(
            List<Bounds> footprints)
        {
            Transform[] all =
                UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int count = 0;
            for (int i=0;i<all.Length;i++)
            {
                Transform transform =
                    all[i];

                if (transform &&
                    transform.gameObject.scene.IsValid() &&
                    IsSpawnedGrassName(transform.name) &&
                    InsideAnyXZ(
                        transform.position,
                        footprints))
                    count++;
            }

            return count;
        }

        static bool IsSpawnedGrassName(
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string lower =
                name.ToLowerInvariant();

            return
                lower.StartsWith("asset grass") ||
                lower.StartsWith("roadside asset grass") ||
                lower.Contains("grasspatch") ||
                lower.Contains("grass patch");
        }

        static bool InsideAnyXZ(
            Vector3 point,
            List<Bounds> bounds)
        {
            for (int i=0;i<bounds.Count;i++)
            {
                Bounds b =
                    bounds[i];

                if (point.x >= b.min.x &&
                    point.x <= b.max.x &&
                    point.z >= b.min.z &&
                    point.z <= b.max.z)
                    return true;
            }

            return false;
        }

        static Terrain FindTerrain()
        {
            Terrain[] terrains =
                UnityEngine.Object.FindObjectsByType<Terrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i=0;i<terrains.Length;i++)
                if (terrains[i] &&
                    terrains[i].gameObject.scene.IsValid())
                    return terrains[i];

            return null;
        }

        static float Planar(
            Vector3 a,
            Vector3 b)
        {
            float x =
                a.x-b.x;
            float z =
                a.z-b.z;

            return
                Mathf.Sqrt(
                    x*x +
                    z*z);
        }
    }
}
