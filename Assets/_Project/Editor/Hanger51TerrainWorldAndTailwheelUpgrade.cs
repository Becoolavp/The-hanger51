using System;
using System.IO;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class Hanger51TerrainWorldAndTailwheelUpgrade
    {
        private const string TerrainObjectName = "Hanger 51 Editable Terrain";
        private const string LegacyPlaneName = "Plane";
        private const string MasterAircraftName = "P-51D Mustang Test Aircraft";
        private const string TerrainFolder = "Assets/_Project/Environment/Terrain";
        private const string TerrainLayerFolder = TerrainFolder + "/Layers";
        private const string TerrainTextureFolder = TerrainFolder + "/Textures";
        private const string TerrainDataPath = TerrainFolder + "/Hanger51WorldTerrain.asset";
        private const string GrassLayerPath = TerrainLayerFolder + "/Grass.terrainlayer";
        private const string DirtLayerPath = TerrainLayerFolder + "/Dirt.terrainlayer";
        private const string RockLayerPath = TerrainLayerFolder + "/Rock.terrainlayer";
        private const string GrassTexturePath = TerrainTextureFolder + "/GrassTexture.asset";
        private const string DirtTexturePath = TerrainTextureFolder + "/DirtTexture.asset";
        private const string RockTexturePath = TerrainTextureFolder + "/RockTexture.asset";

        private const float WorldSize = 6000f;
        private const float TerrainHeight = 600f;
        private const float InitialSurfaceHeight = 90f;
        private const int HeightmapResolution = 2049;
        private const int AlphamapResolution = 512;
        private const float TailwheelAircraftLocalZ = -4.58f;

        [MenuItem("Hanger 51/Environment/3 - Convert Ground Plane to Sculptable 6 km Terrain and Fix Tailwheel")]
        public static void ConvertGroundToTerrainAndFixTailwheel()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 3 failed. Exit Play mode before converting the world ground.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Environment Step 3 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Environment Step 3 failed. Open the saved Hanger 51 scene first.");
                return;
            }

            EnsureFolder(TerrainFolder);
            EnsureFolder(TerrainLayerFolder);
            EnsureFolder(TerrainTextureFolder);

            Terrain terrain = FindTerrain();
            bool createdTerrain = terrain == null;
            float currentGroundY = DetermineCurrentGroundSurfaceY();
            Vector3 worldCenter = DetermineWorldCenter();

            if (createdTerrain)
            {
                TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
                if (terrainData == null)
                {
                    terrainData = new TerrainData
                    {
                        name = "Hanger 51 World Terrain",
                        heightmapResolution = HeightmapResolution,
                        alphamapResolution = AlphamapResolution,
                        baseMapResolution = 1024,
                        size = new Vector3(WorldSize, TerrainHeight, WorldSize)
                    };
                    AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
                    InitializeFlatTerrain(terrainData);
                }

                TerrainLayer grass = CreateOrUpdateLayer(
                    GrassLayerPath,
                    GrassTexturePath,
                    "Grass",
                    new Color(0.19f, 0.31f, 0.105f, 1f),
                    17f,
                    0.16f,
                    0.09f);
                TerrainLayer dirt = CreateOrUpdateLayer(
                    DirtLayerPath,
                    DirtTexturePath,
                    "Dirt",
                    new Color(0.30f, 0.22f, 0.12f, 1f),
                    11f,
                    0.22f,
                    0.06f);
                TerrainLayer rock = CreateOrUpdateLayer(
                    RockLayerPath,
                    RockTexturePath,
                    "Rock",
                    new Color(0.29f, 0.30f, 0.29f, 1f),
                    14f,
                    0.30f,
                    0.13f);
                terrainData.terrainLayers = new[] { grass, dirt, rock };
                InitializeBaseTexture(terrainData);
                EditorUtility.SetDirty(terrainData);

                GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                Undo.RegisterCreatedObjectUndo(terrainObject, "Create Hanger 51 sculptable Terrain");
                terrainObject.name = TerrainObjectName;
                terrainObject.transform.position = new Vector3(
                    worldCenter.x - WorldSize * 0.5f,
                    currentGroundY - InitialSurfaceHeight,
                    worldCenter.z - WorldSize * 0.5f);
                terrain = terrainObject.GetComponent<Terrain>();
            }
            else
            {
                TerrainData data = terrain.terrainData;
                if (data != null)
                {
                    // Preserve all existing sculpt/paint work on reruns. Only make sure
                    // the terrain remains large enough for the expanded world.
                    if (data.size.x < WorldSize || data.size.z < WorldSize)
                    {
                        Undo.RecordObject(data, "Expand Hanger 51 terrain");
                        Vector3 size = data.size;
                        size.x = Mathf.Max(size.x, WorldSize);
                        size.z = Mathf.Max(size.z, WorldSize);
                        size.y = Mathf.Max(size.y, TerrainHeight);
                        data.size = size;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 3 failed while creating the Unity Terrain.");
                return;
            }

            ConfigureTerrain(terrain);
            DisableLegacyGroundPlane();

            int tailwheelAircraft = RepairAllTailwheelStations();
            if (tailwheelAircraft <= 0)
            {
                Debug.LogWarning("Environment Step 3 created the Terrain, but no serviceable P-51 tailwheel station was found to move.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Environment Step 3 completed its edits but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = terrain.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log(
                $"Environment Step 3 complete. Created/preserved a real {terrain.terrainData.size.x:F0} m × {terrain.terrainData.size.z:F0} m Unity Terrain with "
                + "~90 m of downward carving room, grass/dirt/rock paint layers, TerrainCollider ground physics, and the old mesh Plane disabled as a backup. "
                + $"Moved the complete P-51 tailwheel station aft to aircraft-local Z {TailwheelAircraftLocalZ:F2} on {tailwheelAircraft} aircraft/template hierarchy set(s).",
                terrain);
        }

        [MenuItem("Hanger 51/Environment/4 - Validate Sculptable Terrain and Tailwheel Position")]
        public static void ValidateTerrainAndTailwheel()
        {
            bool passed = true;
            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 4 failed: Hanger 51 Editable Terrain is missing.");
                return;
            }

            TerrainData data = terrain.terrainData;
            if (data.size.x < WorldSize || data.size.z < WorldSize)
            {
                Debug.LogError($"Environment Step 4 failed: Terrain is only {data.size.x:F0} m × {data.size.z:F0} m; expected at least 6000 m × 6000 m.", terrain);
                passed = false;
            }
            if (terrain.GetComponent<TerrainCollider>() == null)
            {
                Debug.LogError("Environment Step 4 failed: TerrainCollider is missing.", terrain);
                passed = false;
            }
            if (data.terrainLayers == null || data.terrainLayers.Length < 3)
            {
                Debug.LogError("Environment Step 4 failed: Terrain needs grass, dirt, and rock paint layers.", terrain);
                passed = false;
            }

            GameObject legacyPlane = FindSceneObjectByExactName(LegacyPlaneName);
            if (legacyPlane != null && legacyPlane.activeSelf)
            {
                Debug.LogError("Environment Step 4 failed: the old mesh Plane is still active and can interfere with Terrain sculpted collision.", legacyPlane);
                passed = false;
            }

            P51LandingGearMaintenanceController[] controllers = Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int validatedTailwheels = 0;
            for (int index = 0; index < controllers.Length; index++)
            {
                P51LandingGearMaintenanceController maintenance = controllers[index];
                if (maintenance == null) continue;

                P51RaycastLandingGear physics = maintenance.GetComponent<P51RaycastLandingGear>();
                Transform tailAnchor = physics != null ? physics.TailwheelAnchor : null;
                Transform serviceTail = FindDescendant(maintenance.transform, "Tailwheel Serviceable Gear Visual");
                if (tailAnchor == null || serviceTail == null)
                {
                    continue;
                }

                float anchorZ = maintenance.transform.InverseTransformPoint(tailAnchor.position).z;
                float visualZ = maintenance.transform.InverseTransformPoint(serviceTail.position).z;
                if (Mathf.Abs(anchorZ - TailwheelAircraftLocalZ) > 0.03f
                    || Mathf.Abs(visualZ - TailwheelAircraftLocalZ) > 0.03f)
                {
                    Debug.LogError(
                        $"Environment Step 4 failed: '{maintenance.gameObject.name}' tailwheel mismatch. Physics Z={anchorZ:F2}, visual Z={visualZ:F2}, expected {TailwheelAircraftLocalZ:F2}.",
                        maintenance);
                    passed = false;
                }
                else
                {
                    validatedTailwheels++;
                }
            }

            if (validatedTailwheels <= 0)
            {
                Debug.LogError("Environment Step 4 failed: no repaired P-51 tailwheel hierarchy was found.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Environment Step 4 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"Environment Step 4 passed. Terrain={data.size.x:F0}×{data.size.z:F0} m, heightmap={data.heightmapResolution}, paint layers={data.terrainLayers.Length}, "
                    + $"TerrainCollider ready, legacy Plane disabled, and {validatedTailwheels} P-51 tailwheel hierarchy set(s) are aligned aft at Z {TailwheelAircraftLocalZ:F2}.",
                    terrain);
            }
        }

        private static void InitializeFlatTerrain(TerrainData data)
        {
            if (data == null) return;
            float normalized = Mathf.Clamp01(InitialSurfaceHeight / Mathf.Max(1f, TerrainHeight));
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    heights[y, x] = normalized;
                }
            }
            data.SetHeights(0, 0, heights);
        }

        private static void InitializeBaseTexture(TerrainData data)
        {
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0) return;
            int resolution = data.alphamapResolution;
            int layers = data.terrainLayers.Length;
            float[,,] alpha = new float[resolution, resolution, layers];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    alpha[y, x, 0] = 1f;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        private static void ConfigureTerrain(Terrain terrain)
        {
            if (terrain == null) return;
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = true;
            terrain.groupingID = 51;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 1200f;
            terrain.treeDistance = 2200f;
            terrain.detailObjectDistance = 300f;
            terrain.drawTreesAndFoliage = true;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
            EditorUtility.SetDirty(terrain);
            if (collider != null) EditorUtility.SetDirty(collider);
        }

        private static TerrainLayer CreateOrUpdateLayer(
            string layerPath,
            string texturePath,
            string displayName,
            Color baseColor,
            float tileMeters,
            float noiseScale,
            float smoothness)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = CreateTerrainTexture(displayName + " Terrain Texture", baseColor, noiseScale);
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { name = displayName };
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(tileMeters, tileMeters);
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = smoothness;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Texture2D CreateTerrainTexture(string textureName, Color baseColor, float noiseStrength)
        {
            const int resolution = 128;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.095f + 11.3f, y * 0.095f + 27.1f) - 0.5f;
                    float multiplier = 1f + noise * Mathf.Clamp(noiseStrength, 0f, 0.6f);
                    pixels[y * resolution + x] = new Color(
                        Mathf.Clamp01(baseColor.r * multiplier),
                        Mathf.Clamp01(baseColor.g * multiplier),
                        Mathf.Clamp01(baseColor.b * multiplier),
                        1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static float DetermineCurrentGroundSurfaceY()
        {
            Terrain terrain = FindTerrain();
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 center = terrain.transform.position + new Vector3(
                    terrain.terrainData.size.x * 0.5f,
                    0f,
                    terrain.terrainData.size.z * 0.5f);
                return terrain.SampleHeight(center) + terrain.transform.position.y;
            }

            GameObject plane = FindSceneObjectByExactName(LegacyPlaneName);
            if (plane != null)
            {
                Renderer renderer = plane.GetComponent<Renderer>();
                if (renderer != null) return renderer.bounds.max.y;
                return plane.transform.position.y;
            }

            GameObject runway = GameObject.Find("P-51 Flight Test Runway");
            if (runway != null)
            {
                return runway.transform.position.y - 0.09f;
            }
            return -0.20f;
        }

        private static Vector3 DetermineWorldCenter()
        {
            Terrain terrain = FindTerrain();
            if (terrain != null && terrain.terrainData != null)
            {
                return terrain.transform.position + new Vector3(
                    terrain.terrainData.size.x * 0.5f,
                    0f,
                    terrain.terrainData.size.z * 0.5f);
            }

            GameObject plane = FindSceneObjectByExactName(LegacyPlaneName);
            if (plane != null) return plane.transform.position;
            GameObject runway = GameObject.Find("P-51 Flight Test Runway");
            if (runway != null) return runway.transform.position;
            GameObject aircraft = GameObject.Find(MasterAircraftName);
            if (aircraft != null) return aircraft.transform.position;
            return Vector3.zero;
        }

        private static void DisableLegacyGroundPlane()
        {
            GameObject plane = FindSceneObjectByExactName(LegacyPlaneName);
            if (plane == null) return;
            Undo.RecordObject(plane, "Disable legacy mesh ground Plane");
            plane.SetActive(false);
            EditorUtility.SetDirty(plane);
        }

        private static int RepairAllTailwheelStations()
        {
            P51LandingGearMaintenanceController[] controllers = Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int repaired = 0;
            for (int index = 0; index < controllers.Length; index++)
            {
                if (RepairTailwheelStation(controllers[index])) repaired++;
            }
            return repaired;
        }

        private static bool RepairTailwheelStation(P51LandingGearMaintenanceController maintenance)
        {
            if (maintenance == null) return false;
            Transform aircraft = maintenance.transform;
            P51RaycastLandingGear physics = maintenance.GetComponent<P51RaycastLandingGear>();
            Transform tailAnchor = physics != null ? physics.TailwheelAnchor : null;
            Transform serviceTail = FindDescendant(aircraft, "Tailwheel Serviceable Gear Visual");
            if (tailAnchor == null || serviceTail == null) return false;

            Undo.RegisterFullObjectHierarchyUndo(maintenance.gameObject, "Move complete P-51 tailwheel station aft");

            Vector3 anchorAircraftLocal = aircraft.InverseTransformPoint(tailAnchor.position);
            anchorAircraftLocal.z = TailwheelAircraftLocalZ;
            tailAnchor.position = aircraft.TransformPoint(anchorAircraftLocal);
            EditorUtility.SetDirty(tailAnchor);

            Vector3 serviceAircraftLocal = aircraft.InverseTransformPoint(serviceTail.position);
            serviceAircraftLocal.z = TailwheelAircraftLocalZ;
            Vector3 serviceWorld = aircraft.TransformPoint(serviceAircraftLocal);
            Vector3 newDeployedLocal = serviceTail.parent != null
                ? serviceTail.parent.InverseTransformPoint(serviceWorld)
                : serviceWorld;

            SerializedObject serialized = new SerializedObject(maintenance);
            SerializedProperty deployed = serialized.FindProperty("deployedLocalPositions");
            SerializedProperty retracted = serialized.FindProperty("retractedLocalPositions");
            Vector3 oldDeployed = GetArrayVector(deployed, 2, serviceTail.localPosition);
            Vector3 oldRetracted = GetArrayVector(retracted, 2, oldDeployed + new Vector3(0f, 0.48f, 0.26f));
            Vector3 retractDelta = oldRetracted - oldDeployed;
            if (retractDelta.sqrMagnitude < 0.001f || retractDelta.sqrMagnitude > 4f)
            {
                retractDelta = new Vector3(0f, 0.48f, 0.26f);
            }
            SetArrayVector(deployed, 2, newDeployedLocal);
            SetArrayVector(retracted, 2, newDeployedLocal + retractDelta);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serviceTail.localPosition = newDeployedLocal;
            EditorUtility.SetDirty(serviceTail);
            EditorUtility.SetDirty(maintenance);

            Transform legacyTail = FindDescendant(aircraft, "Tailwheel Assembly");
            if (legacyTail != null)
            {
                Vector3 legacyAircraftLocal = aircraft.InverseTransformPoint(legacyTail.position);
                legacyAircraftLocal.z = TailwheelAircraftLocalZ;
                legacyTail.position = aircraft.TransformPoint(legacyAircraftLocal);
                EditorUtility.SetDirty(legacyTail);
            }

            P51LandingGearServiceAttachmentFollower follower = maintenance.GetComponent<P51LandingGearServiceAttachmentFollower>();
            follower?.RepairHierarchy();
            return true;
        }

        private static Vector3 GetArrayVector(SerializedProperty array, int index, Vector3 fallback)
        {
            if (array == null || !array.isArray || index < 0 || index >= array.arraySize) return fallback;
            return array.GetArrayElementAtIndex(index).vector3Value;
        }

        private static void SetArrayVector(SerializedProperty array, int index, Vector3 value)
        {
            if (array == null || !array.isArray || index < 0) return;
            if (array.arraySize <= index) array.arraySize = index + 1;
            array.GetArrayElementAtIndex(index).vector3Value = value;
        }

        private static Terrain FindTerrain()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < terrains.Length; index++)
            {
                if (terrains[index] != null && terrains[index].gameObject.name == TerrainObjectName)
                {
                    return terrains[index];
                }
            }
            return null;
        }

        private static GameObject FindSceneObjectByExactName(string objectName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate == null || candidate.name != objectName) continue;
                if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded) continue;
                return candidate.gameObject;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == objectName) return all[index];
            }
            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)
                || folderPath == "Assets"
                || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
